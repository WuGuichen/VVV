using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public static class AnimationAuthoringResourceFieldKeys
    {
        public const string SourceClip = "Animation.SourceClip";
        public const string AvatarMask = "Animation.AvatarMask";
        public const string BakeArtifact = "Animation.BakeArtifact";
        public const string CompatibilityProfile = "Animation.CompatibilityProfile";
        public const string EventVfx = "Animation.EventVfx";
        public const string EventAudioCue = "Animation.EventAudioCue";
    }

    public static class AnimationAuthoringResourceKinds
    {
        public const string AvatarMask = "avatarMask";
        public const string Generated = "generated";
    }

    public static class AnimationAuthoringResourceUsages
    {
        public const string AnimationClip = "animationClip";
        public const string AnimationBakeArtifact = "animationBakeArtifact";
        public const string AnimationCompatibilityProfile = "animationCompatibilityProfile";
        public const string AvatarMask = "avatarMask";
        public const string FmodEvent = "fmodEvent";
    }

    public static class AnimationAuthoringResourceFieldSpecs
    {
        public static AuthoringResourceFieldSpec CreateSourceClip()
        {
            return new AuthoringResourceFieldSpec
            {
                FieldKey = AnimationAuthoringResourceFieldKeys.SourceClip,
                EditorKind = "AnimationEditor",
                DisplayName = "Source Clip",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Animation },
                AcceptedUsages = new List<string>
                {
                    AnimationAuthoringResourceUsages.AnimationClip,
                    CharacterPackageResourceUsageIds.AnimationClipGroup
                },
                AcceptedSourceKinds = new List<AuthoringResourceSourceKind>
                {
                    AuthoringResourceSourceKind.UnityAsset,
                    AuthoringResourceSourceKind.RuntimeCatalogAsset,
                    AuthoringResourceSourceKind.PackageResource
                },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind>
                {
                    AuthoringResourceBindingKind.UnityEditorOnlyAsset,
                    AuthoringResourceBindingKind.UnityAsset,
                    AuthoringResourceBindingKind.ResourceManagerAsset,
                    AuthoringResourceBindingKind.PackageResource
                },
                PreloadPolicy = AuthoringResourcePreloadPolicies.AnimationWarmup,
                OutputKind = AuthoringResourceSelectionOutputKind.ResourceSelectionRef
            };
        }

        public static AuthoringResourceFieldSpec CreateAvatarMask()
        {
            return new AuthoringResourceFieldSpec
            {
                FieldKey = AnimationAuthoringResourceFieldKeys.AvatarMask,
                EditorKind = "AnimationEditor",
                DisplayName = "Avatar Mask",
                AcceptedKinds = new List<string> { AnimationAuthoringResourceKinds.AvatarMask },
                AcceptedUsages = new List<string> { AnimationAuthoringResourceUsages.AvatarMask },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind>
                {
                    AuthoringResourceBindingKind.UnityAsset,
                    AuthoringResourceBindingKind.ResourceManagerAsset,
                    AuthoringResourceBindingKind.PackageResource
                },
                PreloadPolicy = AuthoringResourcePreloadPolicies.AnimationWarmup,
                OutputKind = AuthoringResourceSelectionOutputKind.ResourceSelectionRef
            };
        }

        public static AuthoringResourceFieldSpec CreateBakeArtifact()
        {
            return new AuthoringResourceFieldSpec
            {
                FieldKey = AnimationAuthoringResourceFieldKeys.BakeArtifact,
                EditorKind = "AnimationEditor",
                DisplayName = "Bake Artifact",
                AcceptedKinds = new List<string>
                {
                    AnimationAuthoringResourceKinds.Generated,
                    CharacterPackageResourceTypeIds.Config
                },
                AcceptedUsages = new List<string> { AnimationAuthoringResourceUsages.AnimationBakeArtifact },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind>
                {
                    AuthoringResourceBindingKind.GeneratedPreviewOnly,
                    AuthoringResourceBindingKind.ResourceManagerAsset,
                    AuthoringResourceBindingKind.PackageResource
                },
                OutputKind = AuthoringResourceSelectionOutputKind.ResourceSelectionRef
            };
        }

        public static AuthoringResourceFieldSpec CreateCompatibilityProfile()
        {
            return new AuthoringResourceFieldSpec
            {
                FieldKey = AnimationAuthoringResourceFieldKeys.CompatibilityProfile,
                EditorKind = "AnimationEditor",
                DisplayName = "Compatibility Profile",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Config },
                AcceptedUsages = new List<string> { AnimationAuthoringResourceUsages.AnimationCompatibilityProfile },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind>
                {
                    AuthoringResourceBindingKind.ResourceManagerAsset,
                    AuthoringResourceBindingKind.PackageResource,
                    AuthoringResourceBindingKind.UnityAsset
                },
                OutputKind = AuthoringResourceSelectionOutputKind.ResourceSelectionRef
            };
        }

        public static AuthoringResourceFieldSpec CreateEventVfx()
        {
            return new AuthoringResourceFieldSpec
            {
                FieldKey = AnimationAuthoringResourceFieldKeys.EventVfx,
                EditorKind = "AnimationEditor",
                DisplayName = "Event VFX",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Vfx },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.VfxCue },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind>
                {
                    AuthoringResourceBindingKind.ResourceManagerAsset,
                    AuthoringResourceBindingKind.PackageResource,
                    AuthoringResourceBindingKind.UnityAsset
                },
                PreloadPolicy = AuthoringResourcePreloadPolicies.VfxWarmup,
                OutputKind = AuthoringResourceSelectionOutputKind.ResourceSelectionRef
            };
        }

        public static AuthoringResourceFieldSpec CreateEventAudioCue()
        {
            return CreateEventAudioCue(AuthoringResourceSelectionOutputKind.AudioCueId);
        }

        public static AuthoringResourceFieldSpec CreateEventAudioCue(AuthoringResourceSelectionOutputKind outputKind)
        {
            if (outputKind != AuthoringResourceSelectionOutputKind.AudioCueId &&
                outputKind != AuthoringResourceSelectionOutputKind.AudioEventDefinitionId &&
                outputKind != AuthoringResourceSelectionOutputKind.ResourceSelectionRef)
                throw new ArgumentOutOfRangeException(nameof(outputKind), "Animation event audio fields only output ResourceSelectionRef, AudioCueId, or AudioEventDefinitionId.");

            return new AuthoringResourceFieldSpec
            {
                FieldKey = AnimationAuthoringResourceFieldKeys.EventAudioCue,
                EditorKind = "AnimationEditor",
                DisplayName = "Event Audio Cue",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Audio },
                AcceptedUsages = new List<string>
                {
                    CharacterPackageResourceUsageIds.AudioCue,
                    AnimationAuthoringResourceUsages.FmodEvent
                },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind>
                {
                    AuthoringResourceBindingKind.AudioCue,
                    AuthoringResourceBindingKind.AudioEventDefinition
                },
                PreloadPolicy = AuthoringResourcePreloadPolicies.AudioBank,
                OutputKind = outputKind
            };
        }
    }
}
