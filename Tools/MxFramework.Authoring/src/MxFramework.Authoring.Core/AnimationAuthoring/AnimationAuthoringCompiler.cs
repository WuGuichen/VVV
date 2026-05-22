using System;
using System.Collections.Generic;
using System.Text;

namespace MxFramework.Authoring
{
    public static class AnimationAuthoringCompileFormats
    {
        public const string AnimationSetDefinition = "mx.animationSetDefinition.v1";
        public const string AnimationPackageExpectation = "mx.animationPackageExpectation.v1";
        public const string AnimationResourcePlan = "mx.animationResourcePlan.v1";
        public const string AnimationClipRegistry = "mx.animationClipRegistry.v1";
    }

    public sealed class AnimationAuthoringCompileRequest
    {
        public AnimationAuthoringPackage Package { get; set; }
        public string PackageRootPath { get; set; } = string.Empty;
        public CharacterPackageResourceCatalog ResourceCatalog { get; set; }
        public RuntimeResourceCatalogDocument RuntimeResourceCatalog { get; set; }
    }

    public sealed class AnimationAuthoringCompileResult
    {
        public string PackageId { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public AnimationSetDefinitionDocument AnimationSetDefinition { get; set; } = new AnimationSetDefinitionDocument();
        public AnimationPackageExpectationDocument AnimationPackageExpectation { get; set; } = new AnimationPackageExpectationDocument();
        public AnimationResourcePlanDocument AnimationResourcePlan { get; set; } = new AnimationResourcePlanDocument();
        public AnimationClipRegistryDocument AnimationClipRegistry { get; set; } = new AnimationClipRegistryDocument();
        public CharacterAuthoringValidationReport AnimationValidationReport { get; set; } = new CharacterAuthoringValidationReport();
    }

    public sealed class AnimationSetDefinitionDocument
    {
        public string Format { get; set; } = AnimationAuthoringCompileFormats.AnimationSetDefinition;
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SkeletonProfileId { get; set; } = string.Empty;
        public string AvatarProfileId { get; set; } = string.Empty;
        public List<AnimationSetDefinitionSet> Sets { get; set; } = new List<AnimationSetDefinitionSet>();
        public List<AnimationSetDefinitionProfile> Profiles { get; set; } = new List<AnimationSetDefinitionProfile>();
    }

    public sealed class AnimationSetDefinitionSet
    {
        public string SetId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string DefaultClipId { get; set; } = string.Empty;
        public string FallbackClipId { get; set; } = string.Empty;
        public List<AnimationSetDefinitionLayer> Layers { get; set; } = new List<AnimationSetDefinitionLayer>();
        public List<AnimationSetDefinitionGroup> Groups { get; set; } = new List<AnimationSetDefinitionGroup>();
        public List<AnimationSetDefinitionActionBinding> ActionBindings { get; set; } = new List<AnimationSetDefinitionActionBinding>();
    }

    public sealed class AnimationSetDefinitionLayer
    {
        public string LayerId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public float Weight { get; set; } = 1f;
        public bool Additive { get; set; }
        public string SyncLayerId { get; set; } = string.Empty;
        public string RootMotionPolicy { get; set; } = string.Empty;
        public AuthoringResourceSelectionRef AvatarMaskSelection { get; set; } = new AuthoringResourceSelectionRef();
    }

    public sealed class AnimationSetDefinitionGroup
    {
        public string GroupId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Usage { get; set; } = string.Empty;
        public List<AnimationSetDefinitionClipRef> Clips { get; set; } = new List<AnimationSetDefinitionClipRef>();
        public List<AnimationSetDefinitionBlend1D> Blend1D { get; set; } = new List<AnimationSetDefinitionBlend1D>();
        public List<AnimationSetDefinitionBlend2D> Blend2D { get; set; } = new List<AnimationSetDefinitionBlend2D>();
        public List<AnimationSetDefinitionTimeline> Timelines { get; set; } = new List<AnimationSetDefinitionTimeline>();
    }

    public sealed class AnimationSetDefinitionClipRef
    {
        public string ClipId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string RuntimeResourceKey { get; set; } = string.Empty;
        public string SourceClipName { get; set; } = string.Empty;
        public string SourceSubClipId { get; set; } = string.Empty;
        public bool Loop { get; set; }
        public float Speed { get; set; } = 1f;
        public string RootMotionPolicy { get; set; } = string.Empty;
        public AnimationClipCalibrationDocument Calibration { get; set; }
    }

    public sealed class AnimationClipCalibrationDocument
    {
        public float NativeVelocityX { get; set; }
        public float NativeVelocityY { get; set; }
        public float PlaybackSpeed { get; set; } = 1f;
        public float CycleDurationSeconds { get; set; }
        public List<AnimationFootContactWindowDocument> LeftFootContactWindows { get; set; } = new List<AnimationFootContactWindowDocument>();
        public List<AnimationFootContactWindowDocument> RightFootContactWindows { get; set; } = new List<AnimationFootContactWindowDocument>();
    }

    public sealed class AnimationFootContactWindowDocument
    {
        public float StartNormalized { get; set; }
        public float EndNormalized { get; set; }
        public float Confidence { get; set; } = 1f;
    }

    public sealed class AnimationSetDefinitionBlend1D
    {
        public string BlendId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Parameter { get; set; } = string.Empty;
        public string DefaultClipId { get; set; } = string.Empty;
        public List<AnimationBlend1DPointAuthoring> Points { get; set; } = new List<AnimationBlend1DPointAuthoring>();
    }

    public sealed class AnimationSetDefinitionBlend2D
    {
        public string BlendId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string XParameter { get; set; } = string.Empty;
        public string YParameter { get; set; } = string.Empty;
        public string DefaultClipId { get; set; } = string.Empty;
        public List<AnimationBlend2DPointAuthoring> Points { get; set; } = new List<AnimationBlend2DPointAuthoring>();
    }

    public sealed class AnimationSetDefinitionTimeline
    {
        public string TimelineId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ClipId { get; set; } = string.Empty;
        public string TimeDomain { get; set; } = string.Empty;
        public List<AnimationTimelineEventAuthoring> Events { get; set; } = new List<AnimationTimelineEventAuthoring>();
    }

    public sealed class AnimationSetDefinitionActionBinding
    {
        public string BindingId { get; set; } = string.Empty;
        public string ActionId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string ClipId { get; set; } = string.Empty;
        public string BlendId { get; set; } = string.Empty;
        public string TimelineId { get; set; } = string.Empty;
        public bool Required { get; set; }
    }

    public sealed class AnimationSetDefinitionProfile
    {
        public string ProfileId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string DefaultSetId { get; set; } = string.Empty;
        public string DefaultGroupId { get; set; } = string.Empty;
        public List<AnimationProfileSlotAuthoring> Slots { get; set; } = new List<AnimationProfileSlotAuthoring>();
    }

    public sealed class AnimationPackageExpectationDocument
    {
        public string Format { get; set; } = AnimationAuthoringCompileFormats.AnimationPackageExpectation;
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public string SkeletonProfileId { get; set; } = string.Empty;
        public string AvatarProfileId { get; set; } = string.Empty;
        public List<AnimationCompatibilityExpectationSummary> Compatibility { get; set; } = new List<AnimationCompatibilityExpectationSummary>();
        public List<AnimationWarmupExpectationSummary> Warmup { get; set; } = new List<AnimationWarmupExpectationSummary>();
    }

    public sealed class AnimationCompatibilityExpectationSummary
    {
        public string Source { get; set; } = string.Empty;
        public string SetId { get; set; } = string.Empty;
        public string ProfileId { get; set; } = string.Empty;
        public string CompatibilityId { get; set; } = string.Empty;
        public string SkeletonProfileId { get; set; } = string.Empty;
        public string AvatarProfileId { get; set; } = string.Empty;
        public string CoordinateConvention { get; set; } = string.Empty;
        public bool AllowRetargeting { get; set; }
        public List<string> RequiredBoneIds { get; set; } = new List<string>();
        public List<string> RequiredSocketIds { get; set; } = new List<string>();
    }

    public sealed class AnimationWarmupExpectationSummary
    {
        public string Source { get; set; } = string.Empty;
        public string SetId { get; set; } = string.Empty;
        public string ProfileId { get; set; } = string.Empty;
        public string WarmupId { get; set; } = string.Empty;
        public string PreloadPolicy { get; set; } = string.Empty;
        public List<string> RequiredClipIds { get; set; } = new List<string>();
        public List<string> RequiredBlendIds { get; set; } = new List<string>();
    }

    public sealed class AnimationResourcePlanDocument
    {
        public string Format { get; set; } = AnimationAuthoringCompileFormats.AnimationResourcePlan;
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public string PlanHash { get; set; } = string.Empty;
        public RuntimeResourceCatalogDocument RuntimeResourceCatalog { get; set; } = new RuntimeResourceCatalogDocument();
        public CharacterResourcePlanDocument CharacterResourcePlan { get; set; } = new CharacterResourcePlanDocument();
        public AudioCueManifestDocument AudioCueManifest { get; set; } = new AudioCueManifestDocument();
        public List<CharacterResourcePlanDiagnostic> Diagnostics { get; set; } = new List<CharacterResourcePlanDiagnostic>();
    }

    public sealed class AnimationClipRegistryDocument
    {
        public string Format { get; set; } = AnimationAuthoringCompileFormats.AnimationClipRegistry;
        public string SchemaVersion { get; set; } = "1.0";
        public string PackageId { get; set; } = string.Empty;
        public List<AnimationClipRegistryEntry> Clips { get; set; } = new List<AnimationClipRegistryEntry>();
    }

    public sealed class AnimationClipRegistryEntry
    {
        public string SetId { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string ClipId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SourceClipName { get; set; } = string.Empty;
        public string SourceSubClipId { get; set; } = string.Empty;
        public string RuntimeResourceKey { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public AuthoringResourceSelectionRef SourceSelection { get; set; } = new AuthoringResourceSelectionRef();
        public List<AuthoringResourceSelectionRef> GeneratedArtifactSelections { get; set; } = new List<AuthoringResourceSelectionRef>();
    }

    public static class AnimationAuthoringCompiler
    {
        private const string SourcePath = "config/animation_authoring.json";
        private const string RuntimeAnimationClipTypeId = "AnimationClip";
        private const string DiagnosticSourceKind = "AnimationAuthoring";
        private const string RuntimeKeyMissingCode = "ANIM_RUNTIME_KEY_MISSING";
        private const string SourceNotRuntimeReadyCode = "ANIM_SOURCE_NOT_RUNTIME_READY";
        private const string RuntimeTypeMismatchCode = "ANIM_RUNTIME_TYPE_MISMATCH";
        private const string SourceSubClipMissingCode = "ANIM_SOURCE_SUBCLIP_MISSING";
        private const string WarmupRequiredClipMissingCode = "ANIM_WARMUP_REQUIRED_CLIP_MISSING";
        private const string LocomotionClipMetadataMissingCode = "LOCO_CAL_CLIP_METADATA_MISSING";

        public static AnimationAuthoringCompileResult Compile(AnimationAuthoringCompileRequest request)
        {
            if (request == null)
                request = new AnimationAuthoringCompileRequest();

            AnimationAuthoringPackage package = request.Package ?? new AnimationAuthoringPackage();
            string packageId = package.PackageId ?? string.Empty;
            string stableId = package.StableId ?? string.Empty;
            var resourceLookup = BuildResourceLookup(request.ResourceCatalog, request.RuntimeResourceCatalog);
            var validation = new CharacterAuthoringValidationReport { PackageId = packageId };
            var runtimeCatalog = new RuntimeResourceCatalogDocument
            {
                CatalogId = "animation.package." + packageId + ".runtime",
                PackageId = packageId
            };
            var characterPlan = new CharacterResourcePlanDocument
            {
                PackageId = packageId,
                CharacterStableId = stableId
            };
            var audioManifest = new AudioCueManifestDocument
            {
                PackageId = packageId,
                CharacterStableId = stableId
            };
            var diagnostics = new List<CharacterResourcePlanDiagnostic>();

            AnimationSetDefinitionDocument setDefinition = BuildSetDefinition(package, resourceLookup);
            AnimationPackageExpectationDocument expectation = BuildExpectation(package, validation);
            AnimationClipRegistryDocument clipRegistry = new AnimationClipRegistryDocument { PackageId = packageId };

            if (string.IsNullOrWhiteSpace(package.SkeletonProfileId))
                AddValidation(validation, "ANIM_MISSING_SKELETON_PROFILE", "skeletonProfileId", "skeletonProfileId", "Animation package is missing a skeleton profile id.", CharacterAuthoringValidationSeverity.Warning);
            if (string.IsNullOrWhiteSpace(package.AvatarProfileId))
                AddValidation(validation, "ANIM_MISSING_AVATAR_PROFILE", "avatarProfileId", "avatarProfileId", "Animation package is missing an avatar profile id.", CharacterAuthoringValidationSeverity.Warning);

            for (int setIndex = 0; package.Sets != null && setIndex < package.Sets.Count; setIndex++)
            {
                AnimationAuthoringSet set = package.Sets[setIndex];
                if (set == null)
                    continue;

                string setPath = "sets/" + setIndex;
                var clipsById = BuildClipLookup(set, setIndex, validation);
                AddClipReference(set.DefaultClipId, clipsById, setPath, "defaultClipId", validation);
                AddClipReference(set.FallbackClipId, clipsById, setPath, "fallbackClipId", validation);

                for (int groupIndex = 0; set.Groups != null && groupIndex < set.Groups.Count; groupIndex++)
                {
                    AnimationGroupAuthoring group = set.Groups[groupIndex];
                    if (group == null)
                        continue;

                    string groupPath = setPath + "/groups/" + groupIndex;
                    for (int clipIndex = 0; group.Clips != null && clipIndex < group.Clips.Count; clipIndex++)
                    {
                        AnimationClipMappingAuthoring clip = group.Clips[clipIndex];
                        if (clip == null)
                            continue;

                        string clipPath = groupPath + "/clips/" + clipIndex;
                        AnimationRuntimeResourceBinding clipBinding = ResolveAnimationClipBinding(clip, resourceLookup, clipPath, "sourceSelection", validation, diagnostics, true);
                        clipRegistry.Clips.Add(new AnimationClipRegistryEntry
                        {
                            SetId = set.SetId ?? string.Empty,
                            GroupId = group.GroupId ?? string.Empty,
                            ClipId = clip.ClipId ?? string.Empty,
                            DisplayName = clip.DisplayName ?? string.Empty,
                            SourceClipName = clip.SourceClipName ?? string.Empty,
                            SourceSubClipId = clip.SourceSubClipId ?? string.Empty,
                            RuntimeResourceKey = clipBinding.RuntimeResourceKey,
                            Hash = clipBinding.Hash,
                            SourceSelection = clip.SourceSelection ?? new AuthoringResourceSelectionRef(),
                            GeneratedArtifactSelections = clip.GeneratedArtifactSelections ?? new List<AuthoringResourceSelectionRef>()
                        });

                        if (IsSelectionEmpty(clip.SourceSelection) && string.IsNullOrWhiteSpace(clip.RuntimeResourceKey))
                            AddValidation(validation, "ANIM_MISSING_SOURCE_SELECTION", clipPath, "sourceSelection", "Animation clip mapping has no source selection or runtime resource key.", CharacterAuthoringValidationSeverity.Error);
                        ValidateLocomotionClipCalibration(group, clip, clipPath, validation);

                        AddAnimationClipBinding(clipBinding, AuthoringResourcePreloadPolicies.AnimationWarmup, packageId, runtimeCatalog, characterPlan, diagnostics, resourceLookup, clipPath, "runtimeResourceKey");
                        for (int artifactIndex = 0; clip.GeneratedArtifactSelections != null && artifactIndex < clip.GeneratedArtifactSelections.Count; artifactIndex++)
                            AddSelection(clip.GeneratedArtifactSelections[artifactIndex], AuthoringResourcePreloadPolicies.AnimationWarmup, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup, clipPath + "/generatedArtifactSelections/" + artifactIndex, "generatedArtifactSelections");
                    }

                    CompileBlendRefs(group.Blend1D, clipsById, groupPath + "/blend1D", packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, validation, resourceLookup);
                    CompileBlendRefs(group.Blend2D, clipsById, groupPath + "/blend2D", packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, validation, resourceLookup);
                    CompileTimelineRefs(group, clipsById, groupPath, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, validation, resourceLookup);
                }

                for (int layerIndex = 0; set.Layers != null && layerIndex < set.Layers.Count; layerIndex++)
                    AddSelection(set.Layers[layerIndex] != null ? set.Layers[layerIndex].AvatarMaskSelection : null, AuthoringResourcePreloadPolicies.AnimationWarmup, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup, setPath + "/layers/" + layerIndex, "avatarMaskSelection");

                for (int bindingIndex = 0; set.ActionBindings != null && bindingIndex < set.ActionBindings.Count; bindingIndex++)
                {
                    AnimationActionBindingAuthoring binding = set.ActionBindings[bindingIndex];
                    if (binding == null)
                        continue;

                    string bindingPath = setPath + "/actionBindings/" + bindingIndex;
                    AnimationGroupAuthoring group = FindGroup(set, binding.GroupId);
                    Dictionary<string, AnimationClipMappingAuthoring> groupClips = BuildClipLookup(group, bindingPath, validation);
                    AddClipReference(binding.ClipId, groupClips, bindingPath, "clipId", validation);
                    AddClipFromRef(binding.ClipId, groupClips, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup, bindingPath, "clipId");
                    AddBlendFromRef(binding.BlendId, group, groupClips, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, validation, resourceLookup, bindingPath, "blendId");
                }

                CompileCompatibility(set.Compatibility, setPath + "/compatibility", validation, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup);
                CompileWarmup(set.Warmup, clipsById, setPath + "/warmup", packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, validation, resourceLookup);
            }

            for (int profileIndex = 0; package.Profiles != null && profileIndex < package.Profiles.Count; profileIndex++)
            {
                AnimationAuthoringProfile profile = package.Profiles[profileIndex];
                if (profile == null)
                    continue;

                string profilePath = "profiles/" + profileIndex;
                CompileCompatibility(profile.Compatibility, profilePath + "/compatibility", validation, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup);
                CompileWarmup(profile.Warmup, null, profilePath + "/warmup", packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, validation, resourceLookup);
            }

            Sort(runtimeCatalog);
            Sort(characterPlan);
            Sort(audioManifest);
            string planHash = CharacterPackageHashUtility.ComputeTextSha256(BuildPlanHashText(runtimeCatalog, characterPlan, audioManifest));
            characterPlan.PlanHash = planHash;
            var resourcePlan = new AnimationResourcePlanDocument
            {
                PackageId = packageId,
                StableId = stableId,
                PlanHash = planHash,
                RuntimeResourceCatalog = runtimeCatalog,
                CharacterResourcePlan = characterPlan,
                AudioCueManifest = audioManifest,
                Diagnostics = diagnostics
            };
            characterPlan.Diagnostics.Clear();
            characterPlan.Diagnostics.AddRange(diagnostics);

            return new AnimationAuthoringCompileResult
            {
                PackageId = packageId,
                StableId = stableId,
                AnimationSetDefinition = setDefinition,
                AnimationPackageExpectation = expectation,
                AnimationResourcePlan = resourcePlan,
                AnimationClipRegistry = clipRegistry,
                AnimationValidationReport = validation
            };
        }

        private static AnimationSetDefinitionDocument BuildSetDefinition(AnimationAuthoringPackage package, Dictionary<string, CharacterPackageResourceEntry> resourceLookup)
        {
            var document = new AnimationSetDefinitionDocument
            {
                PackageId = package.PackageId ?? string.Empty,
                StableId = package.StableId ?? string.Empty,
                DisplayName = package.DisplayName ?? string.Empty,
                SkeletonProfileId = package.SkeletonProfileId ?? string.Empty,
                AvatarProfileId = package.AvatarProfileId ?? string.Empty
            };

            for (int setIndex = 0; package.Sets != null && setIndex < package.Sets.Count; setIndex++)
            {
                AnimationAuthoringSet set = package.Sets[setIndex];
                if (set == null)
                    continue;

                var setDoc = new AnimationSetDefinitionSet
                {
                    SetId = set.SetId ?? string.Empty,
                    DisplayName = set.DisplayName ?? string.Empty,
                    Version = set.Version ?? string.Empty,
                    DefaultClipId = set.DefaultClipId ?? string.Empty,
                    FallbackClipId = set.FallbackClipId ?? string.Empty
                };

                for (int layerIndex = 0; set.Layers != null && layerIndex < set.Layers.Count; layerIndex++)
                {
                    AnimationLayerAuthoring layer = set.Layers[layerIndex];
                    if (layer == null)
                        continue;
                    setDoc.Layers.Add(new AnimationSetDefinitionLayer
                    {
                        LayerId = layer.LayerId ?? string.Empty,
                        DisplayName = layer.DisplayName ?? string.Empty,
                        Purpose = layer.Purpose ?? string.Empty,
                        Weight = layer.Weight,
                        Additive = layer.Additive,
                        SyncLayerId = layer.SyncLayerId ?? string.Empty,
                        RootMotionPolicy = layer.RootMotionPolicy ?? string.Empty,
                        AvatarMaskSelection = layer.AvatarMaskSelection ?? new AuthoringResourceSelectionRef()
                    });
                }

                for (int groupIndex = 0; set.Groups != null && groupIndex < set.Groups.Count; groupIndex++)
                {
                    AnimationGroupAuthoring group = set.Groups[groupIndex];
                    if (group == null)
                        continue;

                    var groupDoc = new AnimationSetDefinitionGroup
                    {
                        GroupId = group.GroupId ?? string.Empty,
                        DisplayName = group.DisplayName ?? string.Empty,
                        Usage = group.Usage ?? string.Empty
                    };
                    for (int clipIndex = 0; group.Clips != null && clipIndex < group.Clips.Count; clipIndex++)
                    {
                        AnimationClipMappingAuthoring clip = group.Clips[clipIndex];
                        if (clip == null)
                            continue;
                        AnimationRuntimeResourceBinding clipBinding = ResolveAnimationClipBinding(clip, resourceLookup, string.Empty, string.Empty, null, null, false);
                        groupDoc.Clips.Add(new AnimationSetDefinitionClipRef
                        {
                            ClipId = clip.ClipId ?? string.Empty,
                            DisplayName = clip.DisplayName ?? string.Empty,
                            RuntimeResourceKey = clipBinding.IsRuntimeConsumable ? clipBinding.RuntimeResourceKey : string.Empty,
                            SourceClipName = clip.SourceClipName ?? string.Empty,
                            SourceSubClipId = clip.SourceSubClipId ?? string.Empty,
                            Loop = clip.Loop,
                            Speed = clip.Speed,
                            RootMotionPolicy = clip.RootMotionPolicy ?? string.Empty,
                            Calibration = CreateCalibrationDocument(clip.Calibration)
                        });
                    }

                    for (int blendIndex = 0; group.Blend1D != null && blendIndex < group.Blend1D.Count; blendIndex++)
                    {
                        AnimationBlend1DAuthoring blend = group.Blend1D[blendIndex];
                        if (blend != null)
                            groupDoc.Blend1D.Add(new AnimationSetDefinitionBlend1D { BlendId = blend.BlendId ?? string.Empty, DisplayName = blend.DisplayName ?? string.Empty, Parameter = blend.Parameter ?? string.Empty, DefaultClipId = blend.DefaultClipId ?? string.Empty, Points = blend.Points ?? new List<AnimationBlend1DPointAuthoring>() });
                    }
                    for (int blendIndex = 0; group.Blend2D != null && blendIndex < group.Blend2D.Count; blendIndex++)
                    {
                        AnimationBlend2DAuthoring blend = group.Blend2D[blendIndex];
                        if (blend != null)
                            groupDoc.Blend2D.Add(new AnimationSetDefinitionBlend2D { BlendId = blend.BlendId ?? string.Empty, DisplayName = blend.DisplayName ?? string.Empty, XParameter = blend.XParameter ?? string.Empty, YParameter = blend.YParameter ?? string.Empty, DefaultClipId = blend.DefaultClipId ?? string.Empty, Points = blend.Points ?? new List<AnimationBlend2DPointAuthoring>() });
                    }
                    for (int timelineIndex = 0; group.Timelines != null && timelineIndex < group.Timelines.Count; timelineIndex++)
                    {
                        AnimationTimelineAuthoring timeline = group.Timelines[timelineIndex];
                        if (timeline != null)
                            groupDoc.Timelines.Add(new AnimationSetDefinitionTimeline { TimelineId = timeline.TimelineId ?? string.Empty, DisplayName = timeline.DisplayName ?? string.Empty, ClipId = timeline.ClipId ?? string.Empty, TimeDomain = timeline.TimeDomain ?? string.Empty, Events = timeline.Events ?? new List<AnimationTimelineEventAuthoring>() });
                    }
                    setDoc.Groups.Add(groupDoc);
                }

                for (int bindingIndex = 0; set.ActionBindings != null && bindingIndex < set.ActionBindings.Count; bindingIndex++)
                {
                    AnimationActionBindingAuthoring binding = set.ActionBindings[bindingIndex];
                    if (binding != null)
                        setDoc.ActionBindings.Add(new AnimationSetDefinitionActionBinding { BindingId = binding.BindingId ?? string.Empty, ActionId = binding.ActionId ?? string.Empty, DisplayName = binding.DisplayName ?? string.Empty, GroupId = binding.GroupId ?? string.Empty, ClipId = binding.ClipId ?? string.Empty, BlendId = binding.BlendId ?? string.Empty, TimelineId = binding.TimelineId ?? string.Empty, Required = binding.Required });
                }

                document.Sets.Add(setDoc);
            }

            for (int profileIndex = 0; package.Profiles != null && profileIndex < package.Profiles.Count; profileIndex++)
            {
                AnimationAuthoringProfile profile = package.Profiles[profileIndex];
                if (profile != null)
                    document.Profiles.Add(new AnimationSetDefinitionProfile { ProfileId = profile.ProfileId ?? string.Empty, DisplayName = profile.DisplayName ?? string.Empty, DefaultSetId = profile.DefaultSetId ?? string.Empty, DefaultGroupId = profile.DefaultGroupId ?? string.Empty, Slots = profile.Slots ?? new List<AnimationProfileSlotAuthoring>() });
            }

            return document;
        }

        private static AnimationPackageExpectationDocument BuildExpectation(AnimationAuthoringPackage package, CharacterAuthoringValidationReport validation)
        {
            var document = new AnimationPackageExpectationDocument
            {
                PackageId = package.PackageId ?? string.Empty,
                SkeletonProfileId = package.SkeletonProfileId ?? string.Empty,
                AvatarProfileId = package.AvatarProfileId ?? string.Empty
            };

            for (int setIndex = 0; package.Sets != null && setIndex < package.Sets.Count; setIndex++)
            {
                AnimationAuthoringSet set = package.Sets[setIndex];
                if (set == null)
                    continue;
                AddCompatibilityExpectation(document, "set", set.SetId, string.Empty, set.Compatibility, validation, "sets/" + setIndex + "/compatibility");
                AddWarmupExpectation(document, "set", set.SetId, string.Empty, set.Warmup);
            }
            for (int profileIndex = 0; package.Profiles != null && profileIndex < package.Profiles.Count; profileIndex++)
            {
                AnimationAuthoringProfile profile = package.Profiles[profileIndex];
                if (profile == null)
                    continue;
                AddCompatibilityExpectation(document, "profile", string.Empty, profile.ProfileId, profile.Compatibility, validation, "profiles/" + profileIndex + "/compatibility");
                AddWarmupExpectation(document, "profile", string.Empty, profile.ProfileId, profile.Warmup);
            }

            return document;
        }

        private static void AddCompatibilityExpectation(AnimationPackageExpectationDocument document, string source, string setId, string profileId, AnimationCompatibilityExpectationAuthoring compatibility, CharacterAuthoringValidationReport validation, string path)
        {
            if (compatibility == null)
                return;

            if (string.IsNullOrWhiteSpace(compatibility.SkeletonProfileId))
                AddValidation(validation, "ANIM_COMPAT_MISSING_SKELETON_PROFILE", path, "skeletonProfileId", "Animation compatibility expectation is missing skeletonProfileId.", CharacterAuthoringValidationSeverity.Warning);
            if (string.IsNullOrWhiteSpace(compatibility.AvatarProfileId))
                AddValidation(validation, "ANIM_COMPAT_MISSING_AVATAR_PROFILE", path, "avatarProfileId", "Animation compatibility expectation is missing avatarProfileId.", CharacterAuthoringValidationSeverity.Warning);

            document.Compatibility.Add(new AnimationCompatibilityExpectationSummary
            {
                Source = source ?? string.Empty,
                SetId = setId ?? string.Empty,
                ProfileId = profileId ?? string.Empty,
                CompatibilityId = compatibility.CompatibilityId ?? string.Empty,
                SkeletonProfileId = compatibility.SkeletonProfileId ?? string.Empty,
                AvatarProfileId = compatibility.AvatarProfileId ?? string.Empty,
                CoordinateConvention = compatibility.CoordinateConvention ?? string.Empty,
                AllowRetargeting = compatibility.AllowRetargeting,
                RequiredBoneIds = compatibility.RequiredBoneIds ?? new List<string>(),
                RequiredSocketIds = compatibility.RequiredSocketIds ?? new List<string>()
            });
        }

        private static void AddWarmupExpectation(AnimationPackageExpectationDocument document, string source, string setId, string profileId, AnimationWarmupAuthoring warmup)
        {
            if (warmup == null)
                return;
            document.Warmup.Add(new AnimationWarmupExpectationSummary
            {
                Source = source ?? string.Empty,
                SetId = setId ?? string.Empty,
                ProfileId = profileId ?? string.Empty,
                WarmupId = warmup.WarmupId ?? string.Empty,
                PreloadPolicy = warmup.PreloadPolicy ?? string.Empty,
                RequiredClipIds = warmup.RequiredClipIds ?? new List<string>(),
                RequiredBlendIds = warmup.RequiredBlendIds ?? new List<string>()
            });
        }

        private static void ValidateLocomotionClipCalibration(
            AnimationGroupAuthoring group,
            AnimationClipMappingAuthoring clip,
            string clipPath,
            CharacterAuthoringValidationReport validation)
        {
            if (group == null || clip == null || !IsLocomotionGroup(group))
                return;

            AnimationClipCalibrationAuthoring calibration = clip.Calibration;
            if (HasCompleteCalibration(calibration))
                return;

            AddValidation(
                validation,
                LocomotionClipMetadataMissingCode,
                clipPath,
                "calibration",
                "Locomotion clip '" + (clip.ClipId ?? string.Empty) + "' is missing native velocity, cycle duration, playback speed, or foot contact calibration metadata.",
                CharacterAuthoringValidationSeverity.Warning);
        }

        private static bool IsLocomotionGroup(AnimationGroupAuthoring group)
        {
            return string.Equals(group.Usage, "locomotion", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasCompleteCalibration(AnimationClipCalibrationAuthoring calibration)
        {
            if (calibration == null)
                return false;

            if (calibration.CycleDurationSeconds <= 0f || float.IsNaN(calibration.CycleDurationSeconds) || float.IsInfinity(calibration.CycleDurationSeconds))
                return false;
            if (calibration.PlaybackSpeed <= 0f || float.IsNaN(calibration.PlaybackSpeed) || float.IsInfinity(calibration.PlaybackSpeed))
                return false;

            return calibration.LeftFootContactWindows != null
                && calibration.LeftFootContactWindows.Count > 0
                && calibration.RightFootContactWindows != null
                && calibration.RightFootContactWindows.Count > 0;
        }

        private static AnimationClipCalibrationDocument CreateCalibrationDocument(AnimationClipCalibrationAuthoring calibration)
        {
            if (!HasAnyCalibration(calibration))
                return null;

            var document = new AnimationClipCalibrationDocument
            {
                NativeVelocityX = calibration.NativeVelocityX,
                NativeVelocityY = calibration.NativeVelocityY,
                PlaybackSpeed = calibration.PlaybackSpeed,
                CycleDurationSeconds = calibration.CycleDurationSeconds
            };
            CopyFootContactWindows(calibration.LeftFootContactWindows, document.LeftFootContactWindows);
            CopyFootContactWindows(calibration.RightFootContactWindows, document.RightFootContactWindows);
            return document;
        }

        private static bool HasAnyCalibration(AnimationClipCalibrationAuthoring calibration)
        {
            if (calibration == null)
                return false;
            if (Math.Abs(calibration.NativeVelocityX) > 0.0001f || Math.Abs(calibration.NativeVelocityY) > 0.0001f)
                return true;
            if (Math.Abs(calibration.PlaybackSpeed - 1f) > 0.0001f)
                return true;
            if (calibration.CycleDurationSeconds > 0f)
                return true;
            return calibration.LeftFootContactWindows != null && calibration.LeftFootContactWindows.Count > 0
                || calibration.RightFootContactWindows != null && calibration.RightFootContactWindows.Count > 0;
        }

        private static void CopyFootContactWindows(
            List<AnimationFootContactWindowAuthoring> source,
            List<AnimationFootContactWindowDocument> target)
        {
            for (int i = 0; source != null && i < source.Count; i++)
            {
                AnimationFootContactWindowAuthoring window = source[i];
                if (window == null)
                    continue;

                target.Add(new AnimationFootContactWindowDocument
                {
                    StartNormalized = window.StartNormalized,
                    EndNormalized = window.EndNormalized,
                    Confidence = window.Confidence
                });
            }
        }

        private static Dictionary<string, CharacterPackageResourceEntry> BuildResourceLookup(CharacterPackageResourceCatalog catalog, RuntimeResourceCatalogDocument runtimeCatalog)
        {
            var result = new Dictionary<string, CharacterPackageResourceEntry>(StringComparer.Ordinal);
            for (int i = 0; catalog != null && catalog.Entries != null && i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;
                AddResourceLookup(result, entry.ResourceKey, entry);
                AddResourceLookup(result, entry.StableId, entry);
                AddResourceLookup(result, entry.LocalId, entry);
            }

            for (int i = 0; runtimeCatalog != null && runtimeCatalog.Entries != null && i < runtimeCatalog.Entries.Count; i++)
            {
                RuntimeResourceCatalogEntryDocument runtimeEntry = runtimeCatalog.Entries[i];
                CharacterPackageResourceEntry entry = CreateResourceLookupEntry(runtimeEntry);
                if (entry == null)
                    continue;

                AddResourceLookup(result, entry.ResourceKey, entry);
                AddResourceLookup(result, entry.StableId, entry);
                AddResourceLookup(result, entry.LocalId, entry);
            }

            return result;
        }

        private static CharacterPackageResourceEntry CreateResourceLookupEntry(RuntimeResourceCatalogEntryDocument runtimeEntry)
        {
            if (runtimeEntry == null || string.IsNullOrWhiteSpace(runtimeEntry.Id))
                return null;

            string stableId = FirstNonEmpty(GetProviderData(runtimeEntry.ProviderData, "stableId"), "runtime." + runtimeEntry.Id);
            string usage = NormalizeRuntimeUsage(runtimeEntry.Type, GetProviderData(runtimeEntry.ProviderData, "usage"));
            var entry = new CharacterPackageResourceEntry
            {
                ResourceKey = runtimeEntry.Id ?? string.Empty,
                LocalId = runtimeEntry.Id ?? string.Empty,
                StableId = stableId,
                TypeId = FirstNonEmpty(runtimeEntry.Type, CharacterPackageResourceTypeIds.Animation),
                Variant = runtimeEntry.Variant ?? string.Empty,
                Usage = usage,
                SourceFormat = GetProviderData(runtimeEntry.ProviderData, "sourceFormat"),
                PackageId = runtimeEntry.PackageId ?? string.Empty,
                RelativePath = runtimeEntry.Address ?? string.Empty,
                Hash = runtimeEntry.Hash ?? string.Empty,
                ImportHints = new CharacterPackageImportHint
                {
                    ProviderId = runtimeEntry.Provider ?? string.Empty,
                    TargetRelativePath = runtimeEntry.Address ?? string.Empty,
                    Metadata = runtimeEntry.ProviderData != null
                        ? new Dictionary<string, string>(runtimeEntry.ProviderData, StringComparer.Ordinal)
                        : new Dictionary<string, string>(StringComparer.Ordinal)
                },
                Tags = runtimeEntry.Labels != null ? new List<string>(runtimeEntry.Labels) : new List<string>()
            };
            return entry;
        }

        private static string NormalizeRuntimeUsage(string runtimeType, string usage)
        {
            if (string.Equals(runtimeType, RuntimeAnimationClipTypeId, StringComparison.OrdinalIgnoreCase))
                return AnimationAuthoringResourceUsages.AnimationClip;

            return usage ?? string.Empty;
        }

        private static string GetProviderData(Dictionary<string, string> providerData, string key)
        {
            if (providerData == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            string value;
            return providerData.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        private static void AddResourceLookup(Dictionary<string, CharacterPackageResourceEntry> lookup, string key, CharacterPackageResourceEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(key) && !lookup.ContainsKey(key))
                lookup.Add(key, entry);
        }

        private sealed class AnimationRuntimeResourceBinding
        {
            public string RuntimeResourceKey { get; set; } = string.Empty;
            public string RuntimeTypeId { get; set; } = string.Empty;
            public string Usage { get; set; } = string.Empty;
            public string StableId { get; set; } = string.Empty;
            public string Hash { get; set; } = string.Empty;
            public AuthoringResourceSelectionRef SourceSelection { get; set; }
            public CharacterPackageResourceEntry CatalogEntry { get; set; }
            public bool IsRuntimeReady { get; set; }
            public bool IsRuntimeConsumable { get; set; }
        }

        private static Dictionary<string, AnimationClipMappingAuthoring> BuildClipLookup(AnimationAuthoringSet set, int setIndex, CharacterAuthoringValidationReport validation)
        {
            var result = new Dictionary<string, AnimationClipMappingAuthoring>(StringComparer.Ordinal);
            for (int groupIndex = 0; set != null && set.Groups != null && groupIndex < set.Groups.Count; groupIndex++)
                AddGroupClips(result, set.Groups[groupIndex], "sets/" + setIndex + "/groups/" + groupIndex, validation);
            return result;
        }

        private static Dictionary<string, AnimationClipMappingAuthoring> BuildClipLookup(AnimationGroupAuthoring group, string path, CharacterAuthoringValidationReport validation)
        {
            var result = new Dictionary<string, AnimationClipMappingAuthoring>(StringComparer.Ordinal);
            AddGroupClips(result, group, path, validation);
            return result;
        }

        private static void AddGroupClips(Dictionary<string, AnimationClipMappingAuthoring> result, AnimationGroupAuthoring group, string path, CharacterAuthoringValidationReport validation)
        {
            for (int clipIndex = 0; group != null && group.Clips != null && clipIndex < group.Clips.Count; clipIndex++)
            {
                AnimationClipMappingAuthoring clip = group.Clips[clipIndex];
                if (clip == null || string.IsNullOrWhiteSpace(clip.ClipId))
                    continue;
                if (result.ContainsKey(clip.ClipId))
                    AddValidation(validation, "ANIM_DUPLICATE_CLIP_ID", path + "/clips/" + clipIndex, "clipId", "Animation clip id is duplicated: " + clip.ClipId, CharacterAuthoringValidationSeverity.Error);
                else
                    result.Add(clip.ClipId, clip);
            }
        }

        private static void CompileBlendRefs(List<AnimationBlend1DAuthoring> blends, Dictionary<string, AnimationClipMappingAuthoring> clipsById, string path, string packageId, RuntimeResourceCatalogDocument runtimeCatalog, CharacterResourcePlanDocument characterPlan, AudioCueManifestDocument audioManifest, List<CharacterResourcePlanDiagnostic> diagnostics, CharacterAuthoringValidationReport validation, Dictionary<string, CharacterPackageResourceEntry> resourceLookup)
        {
            for (int blendIndex = 0; blends != null && blendIndex < blends.Count; blendIndex++)
            {
                AnimationBlend1DAuthoring blend = blends[blendIndex];
                if (blend == null)
                    continue;
                string blendPath = path + "/" + blendIndex;
                AddClipReference(blend.DefaultClipId, clipsById, blendPath, "defaultClipId", validation);
                AddClipFromRef(blend.DefaultClipId, clipsById, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup, blendPath, "defaultClipId");
                for (int pointIndex = 0; blend.Points != null && pointIndex < blend.Points.Count; pointIndex++)
                {
                    string pointPath = blendPath + "/points/" + pointIndex;
                    AddClipReference(blend.Points[pointIndex].ClipId, clipsById, pointPath, "clipId", validation);
                    AddClipFromRef(blend.Points[pointIndex].ClipId, clipsById, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup, pointPath, "clipId");
                }
            }
        }

        private static void CompileBlendRefs(List<AnimationBlend2DAuthoring> blends, Dictionary<string, AnimationClipMappingAuthoring> clipsById, string path, string packageId, RuntimeResourceCatalogDocument runtimeCatalog, CharacterResourcePlanDocument characterPlan, AudioCueManifestDocument audioManifest, List<CharacterResourcePlanDiagnostic> diagnostics, CharacterAuthoringValidationReport validation, Dictionary<string, CharacterPackageResourceEntry> resourceLookup)
        {
            for (int blendIndex = 0; blends != null && blendIndex < blends.Count; blendIndex++)
            {
                AnimationBlend2DAuthoring blend = blends[blendIndex];
                if (blend == null)
                    continue;
                string blendPath = path + "/" + blendIndex;
                AddClipReference(blend.DefaultClipId, clipsById, blendPath, "defaultClipId", validation);
                AddClipFromRef(blend.DefaultClipId, clipsById, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup, blendPath, "defaultClipId");
                for (int pointIndex = 0; blend.Points != null && pointIndex < blend.Points.Count; pointIndex++)
                {
                    string pointPath = blendPath + "/points/" + pointIndex;
                    AddClipReference(blend.Points[pointIndex].ClipId, clipsById, pointPath, "clipId", validation);
                    AddClipFromRef(blend.Points[pointIndex].ClipId, clipsById, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup, pointPath, "clipId");
                }
            }
        }

        private static void CompileTimelineRefs(AnimationGroupAuthoring group, Dictionary<string, AnimationClipMappingAuthoring> clipsById, string groupPath, string packageId, RuntimeResourceCatalogDocument runtimeCatalog, CharacterResourcePlanDocument characterPlan, AudioCueManifestDocument audioManifest, List<CharacterResourcePlanDiagnostic> diagnostics, CharacterAuthoringValidationReport validation, Dictionary<string, CharacterPackageResourceEntry> resourceLookup)
        {
            for (int timelineIndex = 0; group != null && group.Timelines != null && timelineIndex < group.Timelines.Count; timelineIndex++)
            {
                AnimationTimelineAuthoring timeline = group.Timelines[timelineIndex];
                if (timeline == null)
                    continue;
                string timelinePath = groupPath + "/timelines/" + timelineIndex;
                AddClipReference(timeline.ClipId, clipsById, timelinePath, "clipId", validation);
                AddClipFromRef(timeline.ClipId, clipsById, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup, timelinePath, "clipId");
                for (int eventIndex = 0; timeline.Events != null && eventIndex < timeline.Events.Count; eventIndex++)
                {
                    AnimationTimelineEventAuthoring timelineEvent = timeline.Events[eventIndex];
                    if (timelineEvent == null)
                        continue;
                    string eventPath = timelinePath + "/events/" + eventIndex;
                    string preload = string.Equals(timelineEvent.EventKind, "AudioCue", StringComparison.OrdinalIgnoreCase)
                        ? AuthoringResourcePreloadPolicies.Audio
                        : string.Equals(timelineEvent.EventKind, "Vfx", StringComparison.OrdinalIgnoreCase)
                            ? AuthoringResourcePreloadPolicies.VfxWarmup
                            : AuthoringResourcePreloadPolicies.PresentationCritical;
                    AddClipReference(timelineEvent.ClipId, clipsById, eventPath, "clipId", validation);
                    AddSelection(timelineEvent.ResourceSelection, preload, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup, eventPath, "resourceSelection");
                }
            }
        }

        private static void CompileCompatibility(AnimationCompatibilityExpectationAuthoring compatibility, string path, CharacterAuthoringValidationReport validation, string packageId, RuntimeResourceCatalogDocument runtimeCatalog, CharacterResourcePlanDocument characterPlan, AudioCueManifestDocument audioManifest, List<CharacterResourcePlanDiagnostic> diagnostics, Dictionary<string, CharacterPackageResourceEntry> resourceLookup)
        {
            if (compatibility == null)
                return;
            AddSelection(compatibility.CompatibilityProfileSelection, AuthoringResourcePreloadPolicies.PresentationCritical, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup, path, "compatibilityProfileSelection");
            AddSelection(compatibility.AvatarMaskSelection, AuthoringResourcePreloadPolicies.AnimationWarmup, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup, path, "avatarMaskSelection");
        }

        private static void CompileWarmup(AnimationWarmupAuthoring warmup, Dictionary<string, AnimationClipMappingAuthoring> clipsById, string path, string packageId, RuntimeResourceCatalogDocument runtimeCatalog, CharacterResourcePlanDocument characterPlan, AudioCueManifestDocument audioManifest, List<CharacterResourcePlanDiagnostic> diagnostics, CharacterAuthoringValidationReport validation, Dictionary<string, CharacterPackageResourceEntry> resourceLookup)
        {
            if (warmup == null)
                return;

            if (clipsById != null)
            {
                for (int i = 0; warmup.RequiredClipIds != null && i < warmup.RequiredClipIds.Count; i++)
                {
                    if (!clipsById.ContainsKey(warmup.RequiredClipIds[i]))
                    {
                        AddValidation(validation, WarmupRequiredClipMissingCode, path + "/requiredClipIds/" + i, "requiredClipIds", "Animation warmup references a missing required clip id: " + warmup.RequiredClipIds[i], CharacterAuthoringValidationSeverity.Error);
                        AddResourceDiagnostic(diagnostics, validation, WarmupRequiredClipMissingCode, string.Empty, string.Empty, path + "/requiredClipIds/" + i, "requiredClipIds", "Animation warmup references a missing required clip id: " + warmup.RequiredClipIds[i], "Add the clip mapping or remove the required warmup reference.", CharacterAuthoringValidationSeverity.Error, false);
                        continue;
                    }
                    AddClipFromRef(warmup.RequiredClipIds[i], clipsById, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup, path + "/requiredClipIds/" + i, "requiredClipIds");
                }
            }
            for (int i = 0; warmup.AvatarMaskSelections != null && i < warmup.AvatarMaskSelections.Count; i++)
                AddSelection(warmup.AvatarMaskSelections[i], AuthoringResourcePreloadPolicies.AnimationWarmup, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup, path + "/avatarMaskSelections/" + i, "avatarMaskSelections");
            for (int i = 0; warmup.VfxSelections != null && i < warmup.VfxSelections.Count; i++)
                AddSelection(warmup.VfxSelections[i], AuthoringResourcePreloadPolicies.VfxWarmup, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup, path + "/vfxSelections/" + i, "vfxSelections");
            for (int i = 0; warmup.AudioCueSelections != null && i < warmup.AudioCueSelections.Count; i++)
                AddSelection(warmup.AudioCueSelections[i], AuthoringResourcePreloadPolicies.Audio, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup, path + "/audioCueSelections/" + i, "audioCueSelections");
            for (int i = 0; warmup.GeneratedArtifactSelections != null && i < warmup.GeneratedArtifactSelections.Count; i++)
                AddSelection(warmup.GeneratedArtifactSelections[i], AuthoringResourcePreloadPolicies.AnimationWarmup, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup, path + "/generatedArtifactSelections/" + i, "generatedArtifactSelections");
            for (int i = 0; warmup.AdditionalResourceSelections != null && i < warmup.AdditionalResourceSelections.Count; i++)
                AddSelection(warmup.AdditionalResourceSelections[i], FirstNonEmpty(warmup.PreloadPolicy, AuthoringResourcePreloadPolicies.AnimationWarmup), packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, resourceLookup, path + "/additionalResourceSelections/" + i, "additionalResourceSelections");
        }

        private static void AddClipReference(string clipId, Dictionary<string, AnimationClipMappingAuthoring> clipsById, string path, string field, CharacterAuthoringValidationReport validation)
        {
            if (string.IsNullOrWhiteSpace(clipId))
                return;
            if (clipsById == null || !clipsById.ContainsKey(clipId))
                AddValidation(validation, "ANIM_MISSING_CLIP_REFERENCE", path, field, "Animation reference points to a missing clip id: " + clipId, CharacterAuthoringValidationSeverity.Error);
        }

        private static void AddClipFromRef(string clipId, Dictionary<string, AnimationClipMappingAuthoring> clipsById, string packageId, RuntimeResourceCatalogDocument runtimeCatalog, CharacterResourcePlanDocument characterPlan, AudioCueManifestDocument audioManifest, List<CharacterResourcePlanDiagnostic> diagnostics, Dictionary<string, CharacterPackageResourceEntry> resourceLookup, string path, string field)
        {
            if (string.IsNullOrWhiteSpace(clipId) || clipsById == null)
                return;
            AnimationClipMappingAuthoring clip;
            if (!clipsById.TryGetValue(clipId, out clip) || clip == null)
                return;
            AnimationRuntimeResourceBinding binding = ResolveAnimationClipBinding(clip, resourceLookup, path, field, null, diagnostics, true);
            AddAnimationClipBinding(binding, AuthoringResourcePreloadPolicies.AnimationWarmup, packageId, runtimeCatalog, characterPlan, diagnostics, resourceLookup, path, field);
        }

        private static void AddBlendFromRef(string blendId, AnimationGroupAuthoring group, Dictionary<string, AnimationClipMappingAuthoring> clipsById, string packageId, RuntimeResourceCatalogDocument runtimeCatalog, CharacterResourcePlanDocument characterPlan, AudioCueManifestDocument audioManifest, List<CharacterResourcePlanDiagnostic> diagnostics, CharacterAuthoringValidationReport validation, Dictionary<string, CharacterPackageResourceEntry> resourceLookup, string path, string field)
        {
            if (string.IsNullOrWhiteSpace(blendId) || group == null)
                return;
            bool found = false;
            for (int i = 0; group.Blend1D != null && i < group.Blend1D.Count; i++)
            {
                if (string.Equals(group.Blend1D[i].BlendId, blendId, StringComparison.Ordinal))
                {
                    found = true;
                    CompileBlendRefs(new List<AnimationBlend1DAuthoring> { group.Blend1D[i] }, clipsById, path + "/" + field, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, validation, resourceLookup);
                }
            }
            for (int i = 0; group.Blend2D != null && i < group.Blend2D.Count; i++)
            {
                if (string.Equals(group.Blend2D[i].BlendId, blendId, StringComparison.Ordinal))
                {
                    found = true;
                    CompileBlendRefs(new List<AnimationBlend2DAuthoring> { group.Blend2D[i] }, clipsById, path + "/" + field, packageId, runtimeCatalog, characterPlan, audioManifest, diagnostics, validation, resourceLookup);
                }
            }
            if (!found)
                AddValidation(validation, "ANIM_MISSING_BLEND_REFERENCE", path, field, "Animation action binding points to a missing blend id: " + blendId, CharacterAuthoringValidationSeverity.Error);
        }

        private static AnimationGroupAuthoring FindGroup(AnimationAuthoringSet set, string groupId)
        {
            for (int i = 0; set != null && set.Groups != null && i < set.Groups.Count; i++)
            {
                if (string.Equals(set.Groups[i].GroupId, groupId, StringComparison.Ordinal))
                    return set.Groups[i];
            }
            return null;
        }

        private static AnimationRuntimeResourceBinding ResolveAnimationClipBinding(AnimationClipMappingAuthoring clip, Dictionary<string, CharacterPackageResourceEntry> resourceLookup, string path, string field, CharacterAuthoringValidationReport validation, List<CharacterResourcePlanDiagnostic> diagnostics, bool emitDiagnostics)
        {
            AuthoringResourceSelectionRef selection = clip != null ? clip.SourceSelection : null;
            CharacterPackageResourceEntry entry = ResolveCatalogEntry(resourceLookup, clip != null ? clip.RuntimeResourceKey : string.Empty, selection);
            string runtimeKey = FirstNonEmpty(
                clip != null ? clip.RuntimeResourceKey : string.Empty,
                selection != null ? selection.RuntimeResourceKey : string.Empty,
                entry != null ? entry.ResourceKey : string.Empty);
            string kind = FirstNonEmpty(entry != null ? entry.TypeId : string.Empty, selection != null ? selection.ExpectedKind : string.Empty);
            string usage = FirstNonEmpty(entry != null ? entry.Usage : string.Empty, selection != null ? selection.ExpectedUsage : string.Empty);
            string runtimeType = string.Equals(kind, RuntimeAnimationClipTypeId, StringComparison.Ordinal)
                ? RuntimeAnimationClipTypeId
                : MapRuntimeType(kind);
            string stableId = FirstNonEmpty(selection != null ? selection.ResourceStableId : string.Empty, entry != null ? entry.StableId : string.Empty);
            string hash = FirstNonEmpty(entry != null ? CharacterPackageResourcePipeline.GetDeclaredContentHash(entry) : string.Empty, selection != null ? selection.ExpectedHash : string.Empty);
            bool hasRuntimeKey = !string.IsNullOrWhiteSpace(runtimeKey);
            bool runtimeReady = hasRuntimeKey && (entry != null ||
                (selection != null &&
                 (selection.BindingKind == AuthoringResourceBindingKind.ResourceManagerAsset ||
                  string.Equals(selection.SourceProviderId, AuthoringResourceProviderIds.RuntimeCatalog, StringComparison.Ordinal))));
            bool validKind = string.Equals(kind, CharacterPackageResourceTypeIds.Animation, StringComparison.Ordinal) ||
                string.Equals(kind, RuntimeAnimationClipTypeId, StringComparison.Ordinal);
            bool validUsage = string.Equals(usage, AnimationAuthoringResourceUsages.AnimationClip, StringComparison.Ordinal);
            bool validRuntimeType = string.Equals(runtimeType, RuntimeAnimationClipTypeId, StringComparison.Ordinal);
            bool runtimeConsumable = hasRuntimeKey && runtimeReady && validKind && validUsage && validRuntimeType;

            if (emitDiagnostics)
            {
                if (!hasRuntimeKey)
                {
                    AddResourceDiagnostic(
                        diagnostics,
                        validation,
                        RuntimeKeyMissingCode,
                        stableId,
                        runtimeKey,
                        path,
                        field,
                        "Animation clip mapping cannot resolve a runtime AnimationClip ResourceKey.",
                        "Choose a runtime-ready AnimationClip resource or refresh the runtime catalog binding.",
                        CharacterAuthoringValidationSeverity.Error,
                        true);
                }
                else if (!runtimeReady)
                {
                    AddResourceDiagnostic(
                        diagnostics,
                        validation,
                        SourceNotRuntimeReadyCode,
                        stableId,
                        runtimeKey,
                        path,
                        field,
                        "Animation clip source is not runtime-ready.",
                        "Select a RuntimeReady AnimationClip or compile/import the selected source into the runtime catalog.",
                        CharacterAuthoringValidationSeverity.Error,
                        true);
                }

                if (string.Equals(usage, CharacterPackageResourceUsageIds.AnimationClipGroup, StringComparison.Ordinal) &&
                    string.IsNullOrWhiteSpace(clip != null ? clip.SourceSubClipId : string.Empty))
                {
                    AddResourceDiagnostic(
                        diagnostics,
                        validation,
                        SourceSubClipMissingCode,
                        stableId,
                        runtimeKey,
                        path,
                        "sourceSubClipId",
                        "Animation clip group selection is missing a source sub-clip id.",
                        "Choose a concrete AnimationClip sub-asset before compiling runtime animation resources.",
                        CharacterAuthoringValidationSeverity.Error,
                        true);
                }

                if (!validKind || !validUsage || !validRuntimeType)
                {
                    AddResourceDiagnostic(
                        diagnostics,
                        validation,
                        RuntimeTypeMismatchCode,
                        stableId,
                        runtimeKey,
                        path,
                        field,
                        "Resolved animation runtime resource must be kind=animation, usage=animationClip, type=AnimationClip. Actual kind=" + kind + ", usage=" + usage + ", type=" + runtimeType + ".",
                        "Bind the clip to a runtime AnimationClip catalog entry instead of an animationClipGroup or editor-only source.",
                        CharacterAuthoringValidationSeverity.Error,
                        true);
                }
            }

            return new AnimationRuntimeResourceBinding
            {
                RuntimeResourceKey = runtimeKey ?? string.Empty,
                RuntimeTypeId = runtimeType ?? string.Empty,
                Usage = usage ?? string.Empty,
                StableId = stableId ?? string.Empty,
                Hash = hash ?? string.Empty,
                SourceSelection = selection,
                CatalogEntry = entry,
                IsRuntimeReady = runtimeReady,
                IsRuntimeConsumable = runtimeConsumable
            };
        }

        private static CharacterPackageResourceEntry ResolveCatalogEntry(Dictionary<string, CharacterPackageResourceEntry> resourceLookup, string runtimeKey, AuthoringResourceSelectionRef selection)
        {
            if (resourceLookup == null)
                return null;

            CharacterPackageResourceEntry entry;
            if (!string.IsNullOrWhiteSpace(runtimeKey) && resourceLookup.TryGetValue(runtimeKey, out entry))
                return entry;
            if (selection != null)
            {
                if (!string.IsNullOrWhiteSpace(selection.RuntimeResourceKey) && resourceLookup.TryGetValue(selection.RuntimeResourceKey, out entry))
                    return entry;
                if (!string.IsNullOrWhiteSpace(selection.PackageResourceKey) && resourceLookup.TryGetValue(selection.PackageResourceKey, out entry))
                    return entry;
                if (!string.IsNullOrWhiteSpace(selection.ProviderResourceKey) && resourceLookup.TryGetValue(selection.ProviderResourceKey, out entry))
                    return entry;
                if (!string.IsNullOrWhiteSpace(selection.ResourceStableId) && resourceLookup.TryGetValue(selection.ResourceStableId, out entry))
                    return entry;
            }

            return null;
        }

        private static void AddAnimationClipBinding(AnimationRuntimeResourceBinding binding, string preloadPolicy, string packageId, RuntimeResourceCatalogDocument runtimeCatalog, CharacterResourcePlanDocument characterPlan, List<CharacterResourcePlanDiagnostic> diagnostics, Dictionary<string, CharacterPackageResourceEntry> resourceLookup, string path, string field)
        {
            if (binding == null || !binding.IsRuntimeConsumable)
                return;

            AddRuntimeKey(binding.RuntimeResourceKey, binding.SourceSelection, CharacterPackageResourceTypeIds.Animation, AnimationAuthoringResourceUsages.AnimationClip, preloadPolicy, packageId, runtimeCatalog, characterPlan, diagnostics, resourceLookup, path, field);
        }

        private static void AddSelection(AuthoringResourceSelectionRef selection, string preloadPolicy, string packageId, RuntimeResourceCatalogDocument runtimeCatalog, CharacterResourcePlanDocument characterPlan, AudioCueManifestDocument audioManifest, List<CharacterResourcePlanDiagnostic> diagnostics, Dictionary<string, CharacterPackageResourceEntry> resourceLookup, string path, string field)
        {
            if (IsSelectionEmpty(selection))
                return;

            if (selection.BindingKind == AuthoringResourceBindingKind.AudioCue || selection.BindingKind == AuthoringResourceBindingKind.AudioEventDefinition || !string.IsNullOrWhiteSpace(selection.AudioCueId) || !string.IsNullOrWhiteSpace(selection.AudioEventDefinitionId))
            {
                string cueId = FirstNonEmpty(selection.AudioCueId, selection.AudioEventDefinitionId, selection.ProviderResourceKey, selection.ResourceStableId);
                if (string.IsNullOrWhiteSpace(cueId))
                    return;
                AddUnique(characterPlan.Audio.RequiredCues, cueId);
                AddUnique(audioManifest.Cues, new AudioCueManifestEntry
                {
                    CueId = cueId,
                    StableId = selection.ResourceStableId ?? string.Empty,
                    ResourceKey = selection.PackageResourceKey ?? string.Empty,
                    ProviderData = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["sourceProviderId"] = selection.SourceProviderId ?? string.Empty,
                        ["audioEventDefinitionId"] = selection.AudioEventDefinitionId ?? string.Empty,
                        ["sourceField"] = field ?? string.Empty
                    }
                });
                return;
            }

            CharacterPackageResourceEntry resolvedEntry = ResolveCatalogEntry(resourceLookup, selection.RuntimeResourceKey, selection);
            string runtimeKey = FirstNonEmpty(selection.RuntimeResourceKey, resolvedEntry != null ? resolvedEntry.ResourceKey : string.Empty);
            if (string.IsNullOrWhiteSpace(runtimeKey))
                return;

            AddRuntimeKey(runtimeKey, selection, selection.ExpectedKind, selection.ExpectedUsage, preloadPolicy, packageId, runtimeCatalog, characterPlan, diagnostics, resourceLookup, path, field);
        }

        private static void AddRuntimeKey(string runtimeKey, AuthoringResourceSelectionRef selection, string kind, string usage, string preloadPolicy, string packageId, RuntimeResourceCatalogDocument runtimeCatalog, CharacterResourcePlanDocument characterPlan, List<CharacterResourcePlanDiagnostic> diagnostics, Dictionary<string, CharacterPackageResourceEntry> resourceLookup, string path, string field)
        {
            if (string.IsNullOrWhiteSpace(runtimeKey))
                return;

            CharacterPackageResourceEntry entry = null;
            resourceLookup.TryGetValue(runtimeKey, out entry);
            string typeId = FirstNonEmpty(entry != null ? entry.TypeId : string.Empty, kind);
            string usageId = FirstNonEmpty(entry != null ? entry.Usage : string.Empty, usage);
            RuntimeResourceCatalogEntryDocument runtimeEntry = FindRuntimeEntry(runtimeCatalog, runtimeKey);
            if (runtimeEntry == null)
            {
                runtimeEntry = new RuntimeResourceCatalogEntryDocument
                {
                    Id = runtimeKey,
                    Type = MapRuntimeType(typeId),
                    PackageId = packageId ?? string.Empty,
                    Provider = NormalizeRuntimeProvider(FirstNonEmpty(entry != null && entry.ImportHints != null ? entry.ImportHints.ProviderId : string.Empty, selection != null ? selection.SourceProviderId : string.Empty, "memory")),
                    Address = FirstNonEmpty(entry != null ? entry.RelativePath : string.Empty, runtimeKey),
                    Hash = FirstNonEmpty(entry != null ? CharacterPackageResourcePipeline.GetDeclaredContentHash(entry) : string.Empty, selection != null ? selection.ExpectedHash : string.Empty),
                    ProviderData = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["sourcePath"] = path ?? string.Empty,
                        ["sourceField"] = field ?? string.Empty,
                        ["usage"] = usageId ?? string.Empty,
                        ["stableId"] = FirstNonEmpty(selection != null ? selection.ResourceStableId : string.Empty, entry != null ? entry.StableId : string.Empty)
                    }
                };
                runtimeCatalog.Entries.Add(runtimeEntry);
            }

            AddResourceToGroup(SelectGroup(characterPlan, preloadPolicy, usageId, typeId), runtimeEntry, usageId, FirstNonEmpty(selection != null ? selection.ResourceStableId : string.Empty, entry != null ? entry.StableId : string.Empty));
            if (selection != null && !string.IsNullOrWhiteSpace(selection.ExpectedHash) && entry != null)
            {
                string actualHash = CharacterPackageResourcePipeline.GetDeclaredContentHash(entry);
                if (!string.IsNullOrWhiteSpace(actualHash) && !string.Equals(selection.ExpectedHash, actualHash, StringComparison.Ordinal))
                    diagnostics.Add(new CharacterResourcePlanDiagnostic { Severity = "Warning", Code = "ANIM_BAKE_HASH_MISMATCH", LibraryItemStableId = selection.ResourceStableId ?? string.Empty, ResourceKey = runtimeKey, SourceConfigKind = "AnimationAuthoring", SourceField = field ?? string.Empty, Message = "Animation selection expected hash differs from catalog hash.", SuggestedFix = "Rebake or refresh the selected animation resource." });
            }
        }

        private static CharacterResourcePlanGroup SelectGroup(CharacterResourcePlanDocument plan, string preloadPolicy, string usage, string typeId)
        {
            if (string.Equals(preloadPolicy, AuthoringResourcePreloadPolicies.SpawnCritical, StringComparison.Ordinal))
                return plan.SpawnCritical;
            if (string.Equals(preloadPolicy, AuthoringResourcePreloadPolicies.PresentationCritical, StringComparison.Ordinal))
                return plan.PresentationCritical;
            if (string.Equals(preloadPolicy, AuthoringResourcePreloadPolicies.EquipmentInitial, StringComparison.Ordinal))
                return plan.EquipmentInitial;
            if (string.Equals(preloadPolicy, AuthoringResourcePreloadPolicies.VfxWarmup, StringComparison.Ordinal))
                return plan.VfxWarmup;
            if (string.Equals(preloadPolicy, AuthoringResourcePreloadPolicies.UiDeferred, StringComparison.Ordinal))
                return plan.UiDeferred;
            if (string.Equals(preloadPolicy, AuthoringResourcePreloadPolicies.AnimationWarmup, StringComparison.Ordinal))
                return plan.AnimationWarmup;

            if (string.Equals(typeId, CharacterPackageResourceTypeIds.Vfx, StringComparison.Ordinal) || string.Equals(usage, CharacterPackageResourceUsageIds.VfxCue, StringComparison.Ordinal))
                return plan.VfxWarmup;
            if (string.Equals(typeId, CharacterPackageResourceTypeIds.Animation, StringComparison.Ordinal) ||
                string.Equals(usage, AnimationAuthoringResourceUsages.AnimationClip, StringComparison.Ordinal) ||
                string.Equals(usage, CharacterPackageResourceUsageIds.AnimationClipGroup, StringComparison.Ordinal))
                return plan.AnimationWarmup;
            return plan.PresentationCritical;
        }

        private static void AddResourceToGroup(CharacterResourcePlanGroup group, RuntimeResourceCatalogEntryDocument runtimeEntry, string usage, string stableId)
        {
            if (group == null || runtimeEntry == null || string.IsNullOrWhiteSpace(runtimeEntry.Id))
                return;
            for (int i = 0; i < group.Resources.Count; i++)
            {
                if (string.Equals(group.Resources[i].ResourceKey, runtimeEntry.Id, StringComparison.Ordinal))
                    return;
            }
            group.Resources.Add(new CharacterResourcePlanResourceRef { ResourceKey = runtimeEntry.Id, TypeId = runtimeEntry.Type, Variant = runtimeEntry.Variant, PackageId = runtimeEntry.PackageId, Usage = usage ?? string.Empty, StableId = stableId ?? string.Empty });
        }

        private static RuntimeResourceCatalogEntryDocument FindRuntimeEntry(RuntimeResourceCatalogDocument catalog, string key)
        {
            for (int i = 0; catalog != null && catalog.Entries != null && i < catalog.Entries.Count; i++)
            {
                if (string.Equals(catalog.Entries[i].Id, key, StringComparison.Ordinal))
                    return catalog.Entries[i];
            }
            return null;
        }

        private static bool IsSelectionEmpty(AuthoringResourceSelectionRef selection)
        {
            return selection == null ||
                (string.IsNullOrWhiteSpace(selection.ResourceStableId) &&
                 string.IsNullOrWhiteSpace(selection.ProviderResourceKey) &&
                 string.IsNullOrWhiteSpace(selection.PackageResourceKey) &&
                 string.IsNullOrWhiteSpace(selection.RuntimeResourceKey) &&
                 string.IsNullOrWhiteSpace(selection.UnityGuid) &&
                 string.IsNullOrWhiteSpace(selection.UnityAssetPath) &&
                 string.IsNullOrWhiteSpace(selection.AudioCueId) &&
                 string.IsNullOrWhiteSpace(selection.AudioEventDefinitionId));
        }

        private static void AddValidation(CharacterAuthoringValidationReport report, string code, string sourceObjectPath, string field, string message, CharacterAuthoringValidationSeverity severity)
        {
            report.Issues.Add(new CharacterAuthoringValidationIssue
            {
                Severity = severity,
                Gate = severity == CharacterAuthoringValidationSeverity.Error ? CharacterAuthoringValidationGate.ExportBlocked : CharacterAuthoringValidationGate.WarningOnly,
                Code = code,
                SourcePath = SourcePath,
                SourceObjectPath = sourceObjectPath ?? string.Empty,
                Field = field ?? string.Empty,
                Message = message ?? string.Empty,
                SuggestedFix = "Open AnimationEditor, fix the animation authoring data, then compile again."
            });
        }

        private static void AddResourceDiagnostic(List<CharacterResourcePlanDiagnostic> diagnostics, CharacterAuthoringValidationReport validation, string code, string stableId, string resourceKey, string path, string field, string message, string suggestedFix, CharacterAuthoringValidationSeverity severity, bool addValidationIssue)
        {
            if (addValidationIssue && validation != null)
                AddValidation(validation, code, path, field, message, severity);

            if (diagnostics == null)
                return;

            diagnostics.Add(new CharacterResourcePlanDiagnostic
            {
                Severity = severity.ToString(),
                Code = code ?? string.Empty,
                LibraryItemStableId = stableId ?? string.Empty,
                ResourceKey = resourceKey ?? string.Empty,
                SourceConfigKind = DiagnosticSourceKind,
                SourceField = string.IsNullOrWhiteSpace(path) ? field ?? string.Empty : string.IsNullOrWhiteSpace(field) ? path : path + "/" + field,
                Message = message ?? string.Empty,
                SuggestedFix = suggestedFix ?? string.Empty
            });
        }

        private static string NormalizeRuntimeProvider(string providerId)
        {
            if (string.Equals(providerId, "memory", StringComparison.Ordinal) ||
                string.Equals(providerId, "resources", StringComparison.Ordinal) ||
                string.Equals(providerId, "assetBundle", StringComparison.Ordinal) ||
                string.Equals(providerId, "remoteBundle", StringComparison.Ordinal))
                return providerId;

            return "memory";
        }

        private static string MapRuntimeType(string typeId)
        {
            if (string.Equals(typeId, CharacterPackageResourceTypeIds.Animation, StringComparison.Ordinal))
                return "AnimationClip";
            if (string.Equals(typeId, CharacterPackageResourceTypeIds.Vfx, StringComparison.Ordinal))
                return "GameObject";
            if (string.Equals(typeId, CharacterPackageResourceTypeIds.Config, StringComparison.Ordinal))
                return "TextAsset";
            return string.IsNullOrWhiteSpace(typeId) ? "Object" : typeId;
        }

        private static void Sort(RuntimeResourceCatalogDocument catalog)
        {
            if (catalog != null && catalog.Entries != null)
                catalog.Entries.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        }

        private static void Sort(CharacterResourcePlanDocument plan)
        {
            Sort(plan.SpawnCritical);
            Sort(plan.PresentationCritical);
            Sort(plan.EquipmentInitial);
            Sort(plan.AnimationWarmup);
            Sort(plan.VfxWarmup);
            Sort(plan.UiDeferred);
            plan.Audio.RequiredCues.Sort(StringComparer.Ordinal);
            plan.Audio.RequiredBanks.Sort(StringComparer.Ordinal);
        }

        private static void Sort(CharacterResourcePlanGroup group)
        {
            if (group != null && group.Resources != null)
                group.Resources.Sort((a, b) => string.CompareOrdinal(a.ResourceKey, b.ResourceKey));
        }

        private static void Sort(AudioCueManifestDocument manifest)
        {
            if (manifest != null && manifest.Cues != null)
                manifest.Cues.Sort((a, b) => string.CompareOrdinal(a.CueId, b.CueId));
            if (manifest != null && manifest.Banks != null)
                manifest.Banks.Sort(StringComparer.Ordinal);
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value))
                values.Add(value);
        }

        private static void AddUnique(List<AudioCueManifestEntry> values, AudioCueManifestEntry value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.CueId))
                return;
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i].CueId, value.CueId, StringComparison.Ordinal))
                    return;
            }
            values.Add(value);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; values != null && i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            }
            return string.Empty;
        }

        private static string BuildPlanHashText(RuntimeResourceCatalogDocument runtimeCatalog, CharacterResourcePlanDocument plan, AudioCueManifestDocument audioManifest)
        {
            var builder = new StringBuilder();
            for (int i = 0; runtimeCatalog != null && i < runtimeCatalog.Entries.Count; i++)
                builder.Append(runtimeCatalog.Entries[i].Id).Append('|').Append(runtimeCatalog.Entries[i].Hash).Append('\n');
            AppendGroup(builder, "spawn", plan.SpawnCritical);
            AppendGroup(builder, "presentation", plan.PresentationCritical);
            AppendGroup(builder, "equipment", plan.EquipmentInitial);
            AppendGroup(builder, "animation", plan.AnimationWarmup);
            AppendGroup(builder, "vfx", plan.VfxWarmup);
            AppendGroup(builder, "ui", plan.UiDeferred);
            for (int i = 0; audioManifest != null && i < audioManifest.Cues.Count; i++)
                builder.Append("audio|").Append(audioManifest.Cues[i].CueId).Append('\n');
            return builder.ToString();
        }

        private static void AppendGroup(StringBuilder builder, string name, CharacterResourcePlanGroup group)
        {
            for (int i = 0; group != null && i < group.Resources.Count; i++)
                builder.Append(name).Append('|').Append(group.Resources[i].ResourceKey).Append('\n');
        }
    }
}
