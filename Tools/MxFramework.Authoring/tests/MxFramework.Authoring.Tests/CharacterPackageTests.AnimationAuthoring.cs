using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    private static void AuthoringResourceSelectionContracts_JsonRoundTrip()
    {
        var spec = new AuthoringResourceFieldSpec
        {
            FieldKey = "CombatAction.HitSfx",
            EditorKind = "CombatEditor",
            DisplayName = "Hit Sfx",
            AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Audio },
            AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.AudioCue },
            AcceptedProviderIds = new List<string> { AuthoringResourceProviderIds.Fmod },
            AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.AudioCue },
            PreloadPolicy = AuthoringResourcePreloadPolicies.AudioBank,
            OutputKind = AuthoringResourceSelectionOutputKind.AudioCueId
        };
        var context = new AuthoringResourceConsumerContext
        {
            ConsumerKind = "combatAction",
            ConsumerStableId = "combat.codex_flow_test.light_attack",
            ScopeId = "character.codex_flow_test",
            PackageId = "codex_flow_test",
            PackagePath = "Tools/MxFramework.Authoring/samples/character-codex-flow-test",
            WeaponClass = "sword"
        };
        var selection = new AuthoringResourceSelectionRef
        {
            ResourceStableId = "audio.codex_flow_test.hit",
            SourceProviderId = AuthoringResourceProviderIds.Fmod,
            BindingKind = AuthoringResourceBindingKind.AudioCue,
            AudioCueId = "cue.codex_flow_test.hit"
        };
        var reason = new AuthoringResourceSelectionReason
        {
            Code = AuthoringResourceSelectionReasonCodes.NotRuntimeLoadable,
            Category = "runtime",
            Severity = CharacterAuthoringValidationSeverity.Error,
            BlocksSelection = true,
            FieldKey = spec.FieldKey,
            ResourceStableId = selection.ResourceStableId,
            ProviderId = selection.SourceProviderId,
            ExpectedValue = "RuntimeReady",
            ActualValue = "NotRuntimeLoadable",
            BindingKind = AuthoringResourceBindingKind.ExternalSource,
            BindingKeyKind = AuthoringResourceBindingKeyKinds.ExternalSourcePath,
            Message = "not runtime loadable"
        };

        string json = JsonSerializer.Serialize(new AuthoringResourceSelectionResolutionResult
        {
            Accepted = false,
            Selection = selection,
            Reasons = new List<AuthoringResourceSelectionReason> { reason }
        }, JsonOptions);
        AuthoringResourceSelectionResolutionResult roundTrip = JsonSerializer.Deserialize<AuthoringResourceSelectionResolutionResult>(json, JsonOptions);
        string specJson = JsonSerializer.Serialize(spec, JsonOptions);
        AuthoringResourceFieldSpec specRoundTrip = JsonSerializer.Deserialize<AuthoringResourceFieldSpec>(specJson, JsonOptions);
        string contextJson = JsonSerializer.Serialize(context, JsonOptions);
        AuthoringResourceConsumerContext contextRoundTrip = JsonSerializer.Deserialize<AuthoringResourceConsumerContext>(contextJson, JsonOptions);

        Require(roundTrip != null && roundTrip.Selection.AudioCueId == "cue.codex_flow_test.hit", "authoring selection ref should roundtrip audio cue id.");
        Require(roundTrip.Reasons.Count == 1 && roundTrip.Reasons[0].BlocksSelection, "selection reason should roundtrip blocksSelection.");
        Require(specRoundTrip != null && specRoundTrip.AcceptedProviderIds[0] == AuthoringResourceProviderIds.Fmod, "field spec should roundtrip accepted provider ids.");
        Require(specRoundTrip.OutputKind == AuthoringResourceSelectionOutputKind.AudioCueId, "field spec should roundtrip output kind.");
        Require(contextRoundTrip != null && contextRoundTrip.ConsumerKind == "combatAction", "consumer context should roundtrip.");
    }

    private static void CharacterAnimationAuthoringSummary_JsonRoundTrip_PreservesSlots()
    {
        var summary = new CharacterApplicationAuthoringSummary
        {
            CharacterStableId = "char.codex_flow_test",
            AnimationGroups = new List<CharacterAnimationGroupAuthoringSummary>
            {
                new CharacterAnimationGroupAuthoringSummary
                {
                    GroupId = "anim.locomotion",
                    DisplayName = "Locomotion",
                    Description = "Idle / walk / run clip group.",
                    SourceResourceKey = "char.codex_flow_test.anim.locomotion",
                    AnimationPolicy = "splitByClipNames",
                    DefaultBlendSpaceId = "locomotion2d",
                    Clips = new List<CharacterAnimationClipAuthoringSummary>
                    {
                        new CharacterAnimationClipAuthoringSummary
                        {
                            ClipId = "idle",
                            SourceClipName = "Idle",
                            DisplayName = "Idle",
                            Loop = true,
                            RootMotionPolicy = "Ignore",
                            Speed = 1f
                        },
                        new CharacterAnimationClipAuthoringSummary
                        {
                            ClipId = "run",
                            SourceClipName = "Run Forward",
                            DisplayName = "Run Forward",
                            Loop = true,
                            RootMotionPolicy = "MotionDelta",
                            Speed = 1.2f
                        }
                    },
                    BlendSpaces = new List<CharacterAnimationBlendSpaceAuthoringSummary>
                    {
                        new CharacterAnimationBlendSpaceAuthoringSummary
                        {
                            BlendSpaceId = "locomotion2d",
                            DisplayName = "Locomotion 2D",
                            XParameter = "moveX",
                            YParameter = "moveY",
                            DefaultClipId = "idle",
                            Clips = new List<CharacterAnimationBlendSpaceClipAuthoringSummary>
                            {
                                new CharacterAnimationBlendSpaceClipAuthoringSummary { ClipId = "idle", X = 0f, Y = 0f },
                                new CharacterAnimationBlendSpaceClipAuthoringSummary { ClipId = "run", X = 0f, Y = 1f }
                            }
                        }
                    }
                }
            },
            AnimationProfiles = new List<CharacterAnimationProfileAuthoringSummary>
            {
                new CharacterAnimationProfileAuthoringSummary
                {
                    ProfileId = "anim_profile.default",
                    DisplayName = "Default Animation Profile",
                    Slots = new List<CharacterAnimationSlotAuthoringSummary>
                    {
                        new CharacterAnimationSlotAuthoringSummary
                        {
                            SlotId = "locomotion",
                            DisplayName = "Locomotion",
                            Purpose = "idle walk run",
                            AnimationGroupId = "anim.locomotion",
                            ResourceKey = "char.codex_flow_test.anim.locomotion",
                            PreloadPolicy = AuthoringResourcePreloadPolicies.AnimationWarmup,
                            Required = true,
                            ResourceSelection = new AuthoringResourceSelectionRef
                            {
                                ResourceStableId = "charpkg.codex_flow_test.resource.anim.locomotion",
                                SourceProviderId = AuthoringResourceProviderIds.CharacterPackage,
                                BindingKind = AuthoringResourceBindingKind.PackageResource,
                                ExpectedKind = CharacterPackageResourceTypeIds.Animation,
                                ExpectedUsage = CharacterPackageResourceUsageIds.AnimationClipGroup,
                                PackageResourceKey = "char.codex_flow_test.anim.locomotion"
                            }
                        }
                    }
                }
            }
        };

        string json = JsonSerializer.Serialize(summary, JsonOptions);
        CharacterApplicationAuthoringSummary roundTrip = JsonSerializer.Deserialize<CharacterApplicationAuthoringSummary>(json, JsonOptions);
        Require(!json.Contains("\"animationGroups\""), "schema 4.0 character application must not serialize legacy animationGroups.");
        Require(!json.Contains("\"animationProfiles\""), "schema 4.0 character application must not serialize legacy animationProfiles.");
        Require(roundTrip != null && roundTrip.AnimationGroups.Count == 0 && roundTrip.AnimationProfiles.Count == 0,
            "schema 4.0 animation source belongs to the global library, not the character root.");
    }

    private static void CharacterAbilityLoadouts_JsonRoundTrip_PreservesSourceAndSlots()
    {
        var summary = new CharacterApplicationAuthoringSummary
        {
            CharacterStableId = "char.test",
            AbilityLoadouts = new List<CharacterAbilityLoadoutAuthoringSummary>
            {
                new CharacterAbilityLoadoutAuthoringSummary
                {
                    LoadoutId = "base",
                    StableId = "mx.ability_loadout.test.base",
                    DisplayName = "Base Ability",
                    SourceKind = "base",
                    SourceStableId = "char.test",
                    AbilityIds = new List<int> { 901001 },
                    RemoveAbilityIds = new List<int> { 901099 },
                    SlotBindings = new List<CharacterAbilitySlotBindingAuthoringSummary>
                    {
                        new CharacterAbilitySlotBindingAuthoringSummary
                        {
                            SlotId = "primary",
                            AbilityId = 901001,
                            InputIntentId = "intent.primary"
                        }
                    }
                }
            }
        };

        string json = JsonSerializer.Serialize(summary, JsonOptions);
        CharacterApplicationAuthoringSummary roundTrip = JsonSerializer.Deserialize<CharacterApplicationAuthoringSummary>(json, JsonOptions);

        Require(!json.Contains("\"abilityLoadouts\""), "schema 4.0 character application must not serialize a base ability layer.");
        Require(roundTrip != null && roundTrip.AbilityLoadouts.Count == 0,
            "complete ability configurations belong to the global library in schema 4.0.");
    }

    private static void AnimationAuthoringPackage_JsonRoundTrip_PreservesAnimationEditorContracts()
    {
        var package = new AnimationAuthoringPackage
        {
            PackageId = "animation.codex_flow_test",
            StableId = "anim.codex_flow_test",
            DisplayName = "Codex Flow Test Animation",
            SkeletonProfileId = "skeleton.humanoid",
            AvatarProfileId = "avatar.codex_flow_test",
            Diagnostics = new List<AnimationAuthoringDiagnostic>
            {
                new AnimationAuthoringDiagnostic
                {
                    Severity = CharacterAuthoringValidationSeverity.Warning,
                    Gate = CharacterAuthoringValidationGate.WarningOnly,
                    Code = "ANIM_BLEND_POINT_MISSING_CLIP",
                    SourceObjectPath = "sets/base/groups/locomotion/blend2D/locomotion2d",
                    Field = "points[2].clipId",
                    SetId = "set.base",
                    GroupId = "group.locomotion",
                    BlendId = "locomotion2d",
                    Message = "Blend point references a missing local clip."
                }
            }
        };

        package.Sets.Add(new AnimationAuthoringSet
        {
            SetId = "set.base",
            DisplayName = "Base Set",
            DefaultClipId = "idle",
            FallbackClipId = "idle",
            Layers = new List<AnimationLayerAuthoring>
            {
                new AnimationLayerAuthoring
                {
                    LayerId = "base",
                    DisplayName = "Base",
                    Weight = 1f,
                    RootMotionPolicy = "MotionDelta",
                    AvatarMaskSelection = new AuthoringResourceSelectionRef
                    {
                        ResourceStableId = "mask.full_body",
                        SourceProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
                        BindingKind = AuthoringResourceBindingKind.UnityAsset,
                        ExpectedKind = "avatarMask",
                        ExpectedUsage = "avatarMask",
                        UnityGuid = "mask-guid"
                    }
                }
            },
            Groups = new List<AnimationGroupAuthoring>
            {
                new AnimationGroupAuthoring
                {
                    GroupId = "group.locomotion",
                    DisplayName = "Locomotion",
                    Usage = "locomotion",
                    Clips = new List<AnimationClipMappingAuthoring>
                    {
                        new AnimationClipMappingAuthoring
                        {
                            ClipId = "idle",
                            DisplayName = "Idle",
                            SourceSelection = new AuthoringResourceSelectionRef
                            {
                                ResourceStableId = "anim.source.codex_flow_test.locomotion",
                                SourceProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
                                BindingKind = AuthoringResourceBindingKind.UnityAsset,
                                ExpectedKind = CharacterPackageResourceTypeIds.Animation,
                                ExpectedUsage = CharacterPackageResourceUsageIds.AnimationClipGroup,
                                UnityGuid = "anim-guid",
                                UnityAssetPath = "Assets/Art/Characters/CodexFlowTest/locomotion.glb"
                            },
                            SourceSubClipId = "Take 001/Idle",
                            SourceClipName = "Idle",
                            RuntimeResourceKey = "char.codex_flow_test.anim.idle",
                            Loop = true,
                            Speed = 1f,
                            RootMotionPolicy = "Ignore",
                            Calibration = new AnimationClipCalibrationAuthoring
                            {
                                NativeVelocityX = 0f,
                                NativeVelocityY = 0f,
                                PlaybackSpeed = 1f,
                                CycleDurationSeconds = 1.2f,
                                LeftFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                                {
                                    new AnimationFootContactWindowAuthoring { StartNormalized = 0f, EndNormalized = 1f, Confidence = 1f }
                                },
                                RightFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                                {
                                    new AnimationFootContactWindowAuthoring { StartNormalized = 0f, EndNormalized = 1f, Confidence = 1f }
                                }
                            },
                            Tags = new List<string> { "locomotion", "idle" },
                            GeneratedArtifactSelections = new List<AuthoringResourceSelectionRef>
                            {
                                new AuthoringResourceSelectionRef
                                {
                                    ResourceStableId = "artifact.idle.bake",
                                    SourceProviderId = AuthoringResourceProviderIds.GeneratedAssets,
                                    BindingKind = AuthoringResourceBindingKind.GeneratedPreviewOnly,
                                    ExpectedKind = CharacterPackageResourceTypeIds.Config,
                                    ExpectedUsage = "animationBakeArtifact",
                                    ProviderResourceKey = "generated/idle_bake.json"
                                }
                            }
                        },
                        new AnimationClipMappingAuthoring
                        {
                            ClipId = "run",
                            DisplayName = "Run",
                            SourceSelection = new AuthoringResourceSelectionRef
                            {
                                ResourceStableId = "anim.source.codex_flow_test.locomotion",
                                SourceProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
                                BindingKind = AuthoringResourceBindingKind.UnityAsset,
                                ExpectedKind = CharacterPackageResourceTypeIds.Animation,
                                ExpectedUsage = CharacterPackageResourceUsageIds.AnimationClipGroup,
                                UnityGuid = "anim-guid",
                                UnityAssetPath = "Assets/Art/Characters/CodexFlowTest/locomotion.glb"
                            },
                            SourceSubClipId = "Take 001/Run",
                            SourceClipName = "Run Forward",
                            RuntimeResourceKey = "char.codex_flow_test.anim.run",
                            Loop = true,
                            Speed = 1.1f,
                            RootMotionPolicy = "MotionDelta",
                            Calibration = new AnimationClipCalibrationAuthoring
                            {
                                NativeVelocityX = 0f,
                                NativeVelocityY = 2.2f,
                                PlaybackSpeed = 1.1f,
                                CycleDurationSeconds = 0.75f,
                                LeftFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                                {
                                    new AnimationFootContactWindowAuthoring { StartNormalized = 0.1f, EndNormalized = 0.35f, Confidence = 0.8f }
                                },
                                RightFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                                {
                                    new AnimationFootContactWindowAuthoring { StartNormalized = 0.6f, EndNormalized = 0.85f, Confidence = 0.8f }
                                }
                            }
                        }
                    },
                    Blend1D = new List<AnimationBlend1DAuthoring>
                    {
                        new AnimationBlend1DAuthoring
                        {
                            BlendId = "locomotion_speed",
                            Parameter = "speed",
                            DefaultClipId = "idle",
                            Points = new List<AnimationBlend1DPointAuthoring>
                            {
                                new AnimationBlend1DPointAuthoring { ClipId = "idle", Value = 0f },
                                new AnimationBlend1DPointAuthoring { ClipId = "run", Value = 1f }
                            }
                        }
                    },
                    Blend2D = new List<AnimationBlend2DAuthoring>
                    {
                        new AnimationBlend2DAuthoring
                        {
                            BlendId = "locomotion2d",
                            XParameter = "moveX",
                            YParameter = "moveY",
                            DefaultClipId = "idle",
                            Points = new List<AnimationBlend2DPointAuthoring>
                            {
                                new AnimationBlend2DPointAuthoring { ClipId = "idle", X = 0f, Y = 0f },
                                new AnimationBlend2DPointAuthoring { ClipId = "run", X = 0f, Y = 1f }
                            }
                        }
                    },
                    Timelines = new List<AnimationTimelineAuthoring>
                    {
                        new AnimationTimelineAuthoring
                        {
                            TimelineId = "idle.events",
                            ClipId = "idle",
                            TimeDomain = "NormalizedTime",
                            Events = new List<AnimationTimelineEventAuthoring>
                            {
                                new AnimationTimelineEventAuthoring
                                {
                                    EventId = "idle.audio.breath",
                                    ClipId = "idle",
                                    TimeDomain = "NormalizedTime",
                                    Time = 0.5f,
                                    EventKind = "AudioCue",
                                    ResourceSelection = new AuthoringResourceSelectionRef
                                    {
                                        ResourceStableId = "audio.cue.breath",
                                        SourceProviderId = AuthoringResourceProviderIds.Fmod,
                                        BindingKind = AuthoringResourceBindingKind.AudioCue,
                                        ExpectedKind = CharacterPackageResourceTypeIds.Audio,
                                        ExpectedUsage = CharacterPackageResourceUsageIds.AudioCue,
                                        AudioCueId = "cue.codex_flow_test.breath",
                                        AudioEventDefinitionId = "event:/Character/CodexFlowTest/Breath"
                                    },
                                    PayloadJson = "{\"volume\":0.8}"
                                },
                                new AnimationTimelineEventAuthoring
                                {
                                    EventId = "idle.vfx.steam",
                                    ClipId = "idle",
                                    TimeDomain = "Seconds",
                                    Time = 0.2f,
                                    EventKind = "Vfx",
                                    ResourceSelection = new AuthoringResourceSelectionRef
                                    {
                                        ResourceStableId = "vfx.steam",
                                        SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                                        BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                                        ExpectedKind = CharacterPackageResourceTypeIds.Vfx,
                                        ExpectedUsage = CharacterPackageResourceUsageIds.VfxCue,
                                        RuntimeResourceKey = "vfx.codex_flow_test.steam"
                                    }
                                }
                            }
                        }
                    }
                }
            },
            ActionBindings = new List<AnimationActionBindingAuthoring>
            {
                new AnimationActionBindingAuthoring
                {
                    BindingId = "action.move",
                    ActionId = "character.move",
                    GroupId = "group.locomotion",
                    BlendId = "locomotion2d",
                    Required = true
                }
            },
            Compatibility = new AnimationCompatibilityExpectationAuthoring
            {
                CompatibilityId = "compat.humanoid",
                SkeletonProfileId = "skeleton.humanoid",
                AvatarProfileId = "avatar.codex_flow_test",
                CoordinateConvention = "UnityYUp",
                AllowRetargeting = true,
                CompatibilityProfileSelection = new AuthoringResourceSelectionRef
                {
                    ResourceStableId = "compat.profile.humanoid",
                    SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                    BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                    ExpectedKind = CharacterPackageResourceTypeIds.Config,
                    ExpectedUsage = "animationCompatibilityProfile",
                    RuntimeResourceKey = "animation.compatibility.humanoid"
                },
                AvatarMaskSelection = new AuthoringResourceSelectionRef
                {
                    ResourceStableId = "mask.full_body",
                    SourceProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
                    BindingKind = AuthoringResourceBindingKind.UnityAsset,
                    ExpectedKind = "avatarMask",
                    ExpectedUsage = "avatarMask",
                    UnityGuid = "mask-guid"
                },
                RequiredBoneIds = new List<string> { "hips", "spine" },
                RequiredSocketIds = new List<string> { "weapon.main" }
            },
            Warmup = new AnimationWarmupAuthoring
            {
                WarmupId = "warmup.base",
                RequiredClipIds = new List<string> { "idle", "run" },
                RequiredBlendIds = new List<string> { "locomotion2d" },
                AudioCueSelections = new List<AuthoringResourceSelectionRef>
                {
                    new AuthoringResourceSelectionRef
                    {
                        ResourceStableId = "audio.cue.breath",
                        SourceProviderId = AuthoringResourceProviderIds.Fmod,
                        BindingKind = AuthoringResourceBindingKind.AudioCue,
                        AudioCueId = "cue.codex_flow_test.breath"
                    }
                },
                VfxSelections = new List<AuthoringResourceSelectionRef>
                {
                    new AuthoringResourceSelectionRef
                    {
                        ResourceStableId = "vfx.steam",
                        SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                        BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                        RuntimeResourceKey = "vfx.codex_flow_test.steam"
                    }
                },
                GeneratedArtifactSelections = new List<AuthoringResourceSelectionRef>
                {
                    new AuthoringResourceSelectionRef
                    {
                        ResourceStableId = "artifact.idle.bake",
                        SourceProviderId = AuthoringResourceProviderIds.GeneratedAssets,
                        BindingKind = AuthoringResourceBindingKind.GeneratedPreviewOnly,
                        ProviderResourceKey = "generated/idle_bake.json"
                    }
                }
            }
        });

        package.Profiles.Add(new AnimationAuthoringProfile
        {
            ProfileId = "profile.default",
            DisplayName = "Default",
            DefaultSetId = "set.base",
            DefaultGroupId = "group.locomotion",
            Slots = new List<AnimationProfileSlotAuthoring>
            {
                new AnimationProfileSlotAuthoring
                {
                    SlotId = "locomotion",
                    Purpose = "base movement",
                    SetId = "set.base",
                    GroupId = "group.locomotion",
                    DefaultClipId = "idle",
                    DefaultBlendId = "locomotion2d",
                    Required = true
                }
            }
        });

        string json = JsonSerializer.Serialize(package, JsonOptions);
        AnimationAuthoringPackage roundTrip = JsonSerializer.Deserialize<AnimationAuthoringPackage>(json, JsonOptions);

        Require(json.Contains("\"sourceSelection\""), "animation clip sourceSelection should serialize.");
        Require(json.Contains("\"sourceSubClipId\""), "animation clip sourceSubClipId should serialize.");
        Require(json.Contains("\"audioCueId\""), "timeline AudioCue ResourceSelectionRef should serialize.");
        Require(roundTrip != null && roundTrip.Sets.Count == 1, "animation set should roundtrip.");
        AnimationGroupAuthoring group = roundTrip.Sets[0].Groups[0];
        Require(group.Clips[0].SourceSelection.ResourceStableId == "anim.source.codex_flow_test.locomotion", "source selection should be the primary clip identity.");
        Require(group.Clips[0].SourceSubClipId == "Take 001/Idle", "source sub clip id should roundtrip.");
        Require(group.Clips[0].SourceClipName == "Idle", "source clip name should roundtrip as metadata.");
        Require(group.Clips[0].Calibration.CycleDurationSeconds == 1.2f, "clip calibration cycle duration should roundtrip.");
        Require(group.Clips[0].Calibration.LeftFootContactWindows.Count == 1, "left foot contact windows should roundtrip.");
        Require(group.Clips[1].Calibration.NativeVelocityY == 2.2f, "clip native velocity should roundtrip.");
        Require(group.Blend1D[0].Points[1].ClipId == "run", "1D blend points should reference local clip ids.");
        Require(group.Blend2D[0].Points[1].ClipId == "run", "2D blend points should reference local clip ids.");
        Require(group.Timelines[0].Events[0].ResourceSelection.BindingKind == AuthoringResourceBindingKind.AudioCue, "timeline audio event should use AudioCue ResourceSelectionRef.");
        Require(group.Timelines[0].Events[0].ResourceSelection.AudioCueId == "cue.codex_flow_test.breath", "timeline audio cue id should roundtrip.");
        Require(group.Timelines[0].Events[1].ResourceSelection.RuntimeResourceKey == "vfx.codex_flow_test.steam", "timeline VFX selection should roundtrip.");
        Require(roundTrip.Sets[0].Layers[0].AvatarMaskSelection.UnityGuid == "mask-guid", "AvatarMask selection should roundtrip.");
        Require(roundTrip.Sets[0].Compatibility.CompatibilityProfileSelection.RuntimeResourceKey == "animation.compatibility.humanoid", "compatibility profile selection should roundtrip.");
        Require(roundTrip.Sets[0].Compatibility.RequiredSocketIds[0] == "weapon.main", "compatibility socket expectation should roundtrip.");
        Require(roundTrip.Sets[0].Warmup.AudioCueSelections[0].AudioCueId == "cue.codex_flow_test.breath", "warmup AudioCue selection should roundtrip.");
        Require(roundTrip.Sets[0].Warmup.GeneratedArtifactSelections[0].ProviderResourceKey == "generated/idle_bake.json", "warmup generated artifact selection should roundtrip.");
        Require(roundTrip.Profiles[0].Slots[0].GroupId == "group.locomotion", "profile group reference should roundtrip.");
        Require(roundTrip.Diagnostics[0].Field == "points[2].clipId", "animation diagnostics should roundtrip.");
    }

    private static void AnimationAuthoringCompiler_EmitsRuntimePlanArtifacts()
    {
        var package = new AnimationAuthoringPackage
        {
            PackageId = "animation.test",
            StableId = "character.test.animation",
            DisplayName = "Test Animation",
            SkeletonProfileId = "skeleton.humanoid",
            AvatarProfileId = "avatar.test"
        };
        var idleSelection = new AuthoringResourceSelectionRef
        {
            ResourceStableId = "stable.anim.idle",
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ExpectedKind = CharacterPackageResourceTypeIds.Animation,
            ExpectedUsage = AnimationAuthoringResourceUsages.AnimationClip,
            RuntimeResourceKey = "runtime.anim.idle"
        };
        var runSelection = new AuthoringResourceSelectionRef
        {
            ResourceStableId = "stable.anim.run",
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ExpectedKind = CharacterPackageResourceTypeIds.Animation,
            ExpectedUsage = AnimationAuthoringResourceUsages.AnimationClip,
            RuntimeResourceKey = "runtime.anim.run"
        };
        var maskSelection = new AuthoringResourceSelectionRef
        {
            ResourceStableId = "stable.mask.full_body",
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ExpectedKind = AnimationAuthoringResourceKinds.AvatarMask,
            ExpectedUsage = AnimationAuthoringResourceUsages.AvatarMask,
            RuntimeResourceKey = "runtime.mask.full_body"
        };
        var bakeSelection = new AuthoringResourceSelectionRef
        {
            ResourceStableId = "stable.anim.idle.bake",
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ExpectedKind = CharacterPackageResourceTypeIds.Config,
            ExpectedUsage = AnimationAuthoringResourceUsages.AnimationBakeArtifact,
            RuntimeResourceKey = "runtime.anim.idle.bake"
        };
        var vfxSelection = new AuthoringResourceSelectionRef
        {
            ResourceStableId = "stable.vfx.sole_contact",
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ExpectedKind = CharacterPackageResourceTypeIds.Vfx,
            ExpectedUsage = CharacterPackageResourceUsageIds.VfxCue,
            RuntimeResourceKey = "runtime.vfx.sole_contact"
        };
        var audioSelection = new AuthoringResourceSelectionRef
        {
            ResourceStableId = "stable.audio.sole_contact",
            SourceProviderId = AuthoringResourceProviderIds.Fmod,
            BindingKind = AuthoringResourceBindingKind.AudioCue,
            ExpectedKind = CharacterPackageResourceTypeIds.Audio,
            ExpectedUsage = CharacterPackageResourceUsageIds.AudioCue,
            AudioCueId = "cue.test.sole_contact"
        };

        package.Sets.Add(new AnimationAuthoringSet
        {
            SetId = "set.base",
            DisplayName = "Base",
            DefaultClipId = "idle",
            FallbackClipId = "run",
            Layers = new List<AnimationLayerAuthoring>
            {
                new AnimationLayerAuthoring { LayerId = "base", AvatarMaskSelection = maskSelection }
            },
            Groups = new List<AnimationGroupAuthoring>
            {
                new AnimationGroupAuthoring
                {
                    GroupId = "group.locomotion",
                    Usage = "locomotion",
                    Clips = new List<AnimationClipMappingAuthoring>
                    {
                        new AnimationClipMappingAuthoring
                        {
                            ClipId = "idle",
                            DisplayName = "Idle",
                            SourceSubClipId = "Idle",
                            SourceClipName = "Idle",
                            SourceSelection = idleSelection,
                            Loop = true,
                            Calibration = new AnimationClipCalibrationAuthoring
                            {
                                NativeVelocityX = 0f,
                                NativeVelocityY = 0f,
                                PlaybackSpeed = 1f,
                                CycleDurationSeconds = 1.2f,
                                LeftFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                                {
                                    new AnimationFootContactWindowAuthoring { StartNormalized = 0f, EndNormalized = 1f, Confidence = 1f }
                                },
                                RightFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                                {
                                    new AnimationFootContactWindowAuthoring { StartNormalized = 0f, EndNormalized = 1f, Confidence = 1f }
                                }
                            }
                        },
                        new AnimationClipMappingAuthoring
                        {
                            ClipId = "run",
                            DisplayName = "Run",
                            SourceSubClipId = "Run",
                            SourceClipName = "Run",
                            SourceSelection = runSelection,
                            Loop = true,
                            Calibration = new AnimationClipCalibrationAuthoring
                            {
                                NativeVelocityX = 0f,
                                NativeVelocityY = 2.3f,
                                PlaybackSpeed = 1f,
                                CycleDurationSeconds = 0.7f,
                                LeftFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                                {
                                    new AnimationFootContactWindowAuthoring { StartNormalized = 0.08f, EndNormalized = 0.3f, Confidence = 0.85f }
                                },
                                RightFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                                {
                                    new AnimationFootContactWindowAuthoring { StartNormalized = 0.58f, EndNormalized = 0.8f, Confidence = 0.85f }
                                }
                            }
                        }
                    },
                    Blend2D = new List<AnimationBlend2DAuthoring>
                    {
                        new AnimationBlend2DAuthoring
                        {
                            BlendId = "locomotion",
                            XParameter = "moveX",
                            YParameter = "moveY",
                            DefaultClipId = "idle",
                            Points = new List<AnimationBlend2DPointAuthoring>
                            {
                                new AnimationBlend2DPointAuthoring { ClipId = "idle", X = 0f, Y = 0f },
                                new AnimationBlend2DPointAuthoring { ClipId = "run", X = 0f, Y = 1f }
                            }
                        }
                    },
                    Timelines = new List<AnimationTimelineAuthoring>
                    {
                        new AnimationTimelineAuthoring
                        {
                            TimelineId = "run.events",
                            ClipId = "run",
                            Events = new List<AnimationTimelineEventAuthoring>
                            {
                                new AnimationTimelineEventAuthoring { EventId = "run.vfx.sole_contact", ClipId = "run", EventKind = "Vfx", ResourceSelection = vfxSelection },
                                new AnimationTimelineEventAuthoring { EventId = "run.audio.sole_contact", ClipId = "run", EventKind = "AudioCue", ResourceSelection = audioSelection }
                            }
                        }
                    }
                }
            },
            ActionBindings = new List<AnimationActionBindingAuthoring>
            {
                new AnimationActionBindingAuthoring { BindingId = "move", ActionId = "character.move", GroupId = "group.locomotion", BlendId = "locomotion", Required = true }
            },
            Warmup = new AnimationWarmupAuthoring
            {
                RequiredClipIds = new List<string> { "idle", "run" },
                AvatarMaskSelections = new List<AuthoringResourceSelectionRef> { maskSelection },
                GeneratedArtifactSelections = new List<AuthoringResourceSelectionRef> { bakeSelection },
                VfxSelections = new List<AuthoringResourceSelectionRef> { vfxSelection },
                AudioCueSelections = new List<AuthoringResourceSelectionRef> { audioSelection }
            }
        });

        package.Profiles.Add(new AnimationAuthoringProfile
        {
            ProfileId = "profile.default",
            DefaultSetId = "set.base",
            DefaultGroupId = "group.locomotion",
            Slots = new List<AnimationProfileSlotAuthoring>
            {
                new AnimationProfileSlotAuthoring { SlotId = "locomotion", SetId = "set.base", GroupId = "group.locomotion", DefaultBlendId = "locomotion", Required = true }
            }
        });

        var catalog = new CharacterPackageResourceCatalog
        {
            Entries = new List<CharacterPackageResourceEntry>
            {
                new CharacterPackageResourceEntry { ResourceKey = "runtime.anim.idle", StableId = "stable.anim.idle", TypeId = CharacterPackageResourceTypeIds.Animation, Usage = AnimationAuthoringResourceUsages.AnimationClip, RelativePath = "resources/animations/idle.anim", Hash = "sha256:idle" },
                new CharacterPackageResourceEntry { ResourceKey = "runtime.anim.run", StableId = "stable.anim.run", TypeId = CharacterPackageResourceTypeIds.Animation, Usage = AnimationAuthoringResourceUsages.AnimationClip, RelativePath = "resources/animations/run.anim", Hash = "sha256:run" },
                new CharacterPackageResourceEntry { ResourceKey = "runtime.mask.full_body", StableId = "stable.mask.full_body", TypeId = AnimationAuthoringResourceKinds.AvatarMask, Usage = AnimationAuthoringResourceUsages.AvatarMask, RelativePath = "resources/animations/full_body.mask", Hash = "sha256:mask" },
                new CharacterPackageResourceEntry { ResourceKey = "runtime.anim.idle.bake", StableId = "stable.anim.idle.bake", TypeId = CharacterPackageResourceTypeIds.Config, Usage = AnimationAuthoringResourceUsages.AnimationBakeArtifact, RelativePath = "generated/animation/idle_bake.json", Hash = "sha256:bake" },
                new CharacterPackageResourceEntry { ResourceKey = "runtime.vfx.sole_contact", StableId = "stable.vfx.sole_contact", TypeId = CharacterPackageResourceTypeIds.Vfx, Usage = CharacterPackageResourceUsageIds.VfxCue, RelativePath = "resources/vfx/sole_contact.prefab", Hash = "sha256:vfx" }
            }
        };

        AnimationAuthoringCompileResult result = AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest
        {
            Package = package,
            ResourceCatalog = catalog
        });

        Require(result.AnimationSetDefinition.Format == AnimationAuthoringCompileFormats.AnimationSetDefinition, "animation set definition should declare format.");
        Require(result.AnimationSetDefinition.Sets.Count == 1, "animation set definition should contain the authored set.");
        Require(result.AnimationSetDefinition.Sets[0].Groups[0].Clips[0].RuntimeResourceKey == "runtime.anim.idle", "animation set definition should use resolved runtime clip keys.");
        Require(result.AnimationSetDefinition.Sets[0].Groups[0].Clips[1].Calibration.NativeVelocityY == 2.3f, "animation set definition should include clip calibration native velocity.");
        Require(result.AnimationSetDefinition.Sets[0].Groups[0].Clips[1].Calibration.LeftFootContactWindows.Count == 1, "animation set definition should include left foot contact windows.");
        Require(result.AnimationClipRegistry.Clips.Count == 2, "animation clip registry should contain authored clips.");
        Require(result.AnimationClipRegistry.Clips[0].SourceSelection.ResourceStableId == "stable.anim.idle", "clip registry should retain source selection.");
        Require(result.AnimationClipRegistry.Clips[0].SourceSubClipId == "Idle", "clip registry should retain source sub-clip.");
        Require(result.AnimationClipRegistry.Clips[0].RuntimeResourceKey == "runtime.anim.idle", "clip registry should retain resolved runtime key.");
        Require(result.AnimationClipRegistry.Clips[0].Hash == "sha256:idle", "clip registry should retain runtime resource hash.");
        Require(result.AnimationResourcePlan.RuntimeResourceCatalog.Entries.Exists(entry => entry.Id == "runtime.anim.idle"), "animation runtime catalog should include idle.");
        RuntimeResourceCatalogEntryDocument idleRuntimeEntry = result.AnimationResourcePlan.RuntimeResourceCatalog.Entries.Find(entry => entry.Id == "runtime.anim.idle");
        Require(idleRuntimeEntry != null && idleRuntimeEntry.Type == "AnimationClip", "runtime idle entry should declare AnimationClip type.");
        Require(idleRuntimeEntry.Address == "resources/animations/idle.anim", "runtime idle entry should use package/runtime address, not Unity asset path.");
        Require(!idleRuntimeEntry.Address.StartsWith("Assets/", StringComparison.Ordinal), "runtime idle entry should not use Unity asset paths as runtime input.");
        Require(result.AnimationResourcePlan.CharacterResourcePlan.AnimationWarmup.Resources.Exists(resource => resource.ResourceKey == "runtime.anim.run"), "animation warmup should include run.");
        Require(result.AnimationResourcePlan.CharacterResourcePlan.AnimationWarmup.Resources.Exists(resource => resource.ResourceKey == "runtime.anim.idle"), "animation warmup should include default idle.");
        Require(result.AnimationResourcePlan.CharacterResourcePlan.AnimationWarmup.Resources.Exists(resource => resource.ResourceKey == "runtime.mask.full_body"), "animation warmup should include AvatarMask resources.");
        Require(result.AnimationResourcePlan.CharacterResourcePlan.AnimationWarmup.Resources.Exists(resource => resource.ResourceKey == "runtime.anim.idle.bake"), "animation warmup should include bake artifacts.");
        Require(result.AnimationResourcePlan.CharacterResourcePlan.VfxWarmup.Resources.Exists(resource => resource.ResourceKey == "runtime.vfx.sole_contact"), "VFX timeline events should enter VfxWarmup.");
        Require(result.AnimationResourcePlan.CharacterResourcePlan.Audio.RequiredCues.Contains("cue.test.sole_contact"), "AudioCue timeline events should enter the Audio plan.");
        Require(result.AnimationResourcePlan.AudioCueManifest.Cues.Exists(cue => cue.CueId == "cue.test.sole_contact"), "AudioCue manifest should include FMOD cue references.");
        Require(!string.IsNullOrWhiteSpace(result.AnimationResourcePlan.PlanHash), "animation resource plan should have a deterministic hash.");
        Require(!result.AnimationValidationReport.HasBlockingIssues, "valid animation authoring package should compile without blocking issues.");
        Require(!result.AnimationValidationReport.Issues.Exists(issue => issue.Code == "LOCO_CAL_CLIP_METADATA_MISSING"), "complete locomotion calibration should not warn.");

        package.Sets[0].Groups[0].Clips[1].Calibration = new AnimationClipCalibrationAuthoring();
        AnimationAuthoringCompileResult missingCalibration = AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest { Package = package, ResourceCatalog = catalog });
        Require(missingCalibration.AnimationValidationReport.Issues.Exists(issue => issue.Code == "LOCO_CAL_CLIP_METADATA_MISSING"), "locomotion clips missing calibration metadata should emit warning diagnostics.");
        package.Sets[0].Groups[0].Clips[1].Calibration = new AnimationClipCalibrationAuthoring
        {
            NativeVelocityX = 0f,
            NativeVelocityY = 2.3f,
            PlaybackSpeed = 1f,
            CycleDurationSeconds = 0.7f,
            LeftFootContactWindows = new List<AnimationFootContactWindowAuthoring>
            {
                new AnimationFootContactWindowAuthoring { StartNormalized = 0.08f, EndNormalized = 0.3f, Confidence = 0.85f }
            },
            RightFootContactWindows = new List<AnimationFootContactWindowAuthoring>
            {
                new AnimationFootContactWindowAuthoring { StartNormalized = 0.58f, EndNormalized = 0.8f, Confidence = 0.85f }
            }
        };

        var groupCatalog = new CharacterPackageResourceCatalog
        {
            Entries = new List<CharacterPackageResourceEntry>
            {
                new CharacterPackageResourceEntry { ResourceKey = "runtime.anim.idle", StableId = "stable.anim.idle", TypeId = CharacterPackageResourceTypeIds.Animation, Usage = CharacterPackageResourceUsageIds.AnimationClipGroup, RelativePath = "resources/animations/idle.glb", Hash = "sha256:idle" },
                new CharacterPackageResourceEntry { ResourceKey = "runtime.anim.run", StableId = "stable.anim.run", TypeId = CharacterPackageResourceTypeIds.Animation, Usage = AnimationAuthoringResourceUsages.AnimationClip, RelativePath = "resources/animations/run.anim", Hash = "sha256:run" }
            }
        };
        AnimationAuthoringCompileResult groupInvalid = AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest { Package = package, ResourceCatalog = groupCatalog });
        Require(groupInvalid.AnimationValidationReport.Issues.Exists(issue => issue.Code == "ANIM_RUNTIME_TYPE_MISMATCH"), "animationClipGroup runtime bindings should be rejected as runtime clips.");

        var missingRuntimePackage = new AnimationAuthoringPackage { PackageId = "animation.missing_runtime", StableId = "animation.missing_runtime" };
        missingRuntimePackage.Sets.Add(new AnimationAuthoringSet
        {
            SetId = "set.base",
            Groups = new List<AnimationGroupAuthoring>
            {
                new AnimationGroupAuthoring
                {
                    GroupId = "group.base",
                    Clips = new List<AnimationClipMappingAuthoring>
                    {
                        new AnimationClipMappingAuthoring
                        {
                            ClipId = "idle",
                            SourceSelection = new AuthoringResourceSelectionRef
                            {
                                ResourceStableId = "unity.project.idle",
                                SourceProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
                                BindingKind = AuthoringResourceBindingKind.UnityEditorOnlyAsset,
                                ExpectedKind = CharacterPackageResourceTypeIds.Animation,
                                ExpectedUsage = AnimationAuthoringResourceUsages.AnimationClip,
                                RuntimeResourceKey = "runtime.anim.editor_only"
                            }
                        }
                    }
                }
            }
        });
        AnimationAuthoringCompileResult notReady = AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest { Package = missingRuntimePackage, ResourceCatalog = new CharacterPackageResourceCatalog() });
        Require(notReady.AnimationValidationReport.Issues.Exists(issue => issue.Code == "ANIM_SOURCE_NOT_RUNTIME_READY"), "editor-only required runtime clips should emit source-not-runtime-ready diagnostics.");

        missingRuntimePackage.Sets[0].Groups[0].Clips[0].SourceSelection.RuntimeResourceKey = string.Empty;
        AnimationAuthoringCompileResult missingRuntimeKey = AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest { Package = missingRuntimePackage, ResourceCatalog = new CharacterPackageResourceCatalog() });
        Require(missingRuntimeKey.AnimationValidationReport.Issues.Exists(issue => issue.Code == "ANIM_RUNTIME_KEY_MISSING"), "required runtime clips without runtime keys should emit structured missing-key diagnostics.");

        var missingWarmupClipPackage = new AnimationAuthoringPackage { PackageId = "animation.missing_warmup", StableId = "animation.missing_warmup" };
        missingWarmupClipPackage.Sets.Add(new AnimationAuthoringSet
        {
            SetId = "set.base",
            Warmup = new AnimationWarmupAuthoring { RequiredClipIds = new List<string> { "missing" } }
        });
        AnimationAuthoringCompileResult missingWarmupClip = AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest { Package = missingWarmupClipPackage, ResourceCatalog = new CharacterPackageResourceCatalog() });
        Require(missingWarmupClip.AnimationValidationReport.Issues.Exists(issue => issue.Code == "ANIM_WARMUP_REQUIRED_CLIP_MISSING"), "missing warmup required clips should emit structured diagnostics.");

        package.Sets[0].Groups[0].Blend2D[0].Points.Add(new AnimationBlend2DPointAuthoring { ClipId = "missing", X = 1f, Y = 1f });
        AnimationAuthoringCompileResult invalid = AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest { Package = package, ResourceCatalog = catalog });
        Require(invalid.AnimationValidationReport.HasBlockingIssues, "missing clip references should block animation compile.");
        Require(invalid.AnimationValidationReport.Issues.Exists(issue => issue.Code == "ANIM_MISSING_CLIP_REFERENCE"), "missing clip references should emit a stable diagnostic code.");
    }

    private static void AnimationAuthoringCompiler_ProfileSlotContractRejectsLegacyFallbacks()
    {
        AnimationAuthoringPackage package = CreateLocomotionSlotContractPackage();
        var catalog = new CharacterPackageResourceCatalog
        {
            Entries = new List<CharacterPackageResourceEntry>
            {
                new CharacterPackageResourceEntry
                {
                    ResourceKey = "runtime.anim.idle",
                    StableId = "stable.anim.idle",
                    TypeId = CharacterPackageResourceTypeIds.Animation,
                    Usage = AnimationAuthoringResourceUsages.AnimationClip,
                    RelativePath = "animations/idle.anim"
                },
                new CharacterPackageResourceEntry
                {
                    ResourceKey = "runtime.anim.run",
                    StableId = "stable.anim.run",
                    TypeId = CharacterPackageResourceTypeIds.Animation,
                    Usage = AnimationAuthoringResourceUsages.AnimationClip,
                    RelativePath = "animations/run.anim"
                }
            }
        };
        AnimationAuthoringCompileResult valid = AnimationAuthoringCompiler.Compile(
            new AnimationAuthoringCompileRequest { Package = package, ResourceCatalog = catalog });
        Require(!valid.AnimationValidationReport.HasBlockingIssues,
            "a complete Blend1D locomotion profile slot should pass structural compilation.");

        package.Profiles[0].Slots[0].SlotId = "Locomotion";
        AnimationAuthoringCompileResult wrongCase = AnimationAuthoringCompiler.Compile(
            new AnimationAuthoringCompileRequest { Package = package, ResourceCatalog = catalog });
        Require(!wrongCase.AnimationValidationReport.Issues.Exists(issue => issue.Code == "ANIM_LOCOMOTION_SLOT_MISSING"),
            "the standalone animation compiler should allow non-character profiles without a locomotion slot.");

        package.Profiles[0].Slots[0].SlotId = "locomotion";
        package.Sets[0].Groups[0].Blend2D.Add(new AnimationBlend2DAuthoring
        {
            BlendId = "blend.speed",
            DefaultClipId = "idle",
            Points = new List<AnimationBlend2DPointAuthoring>
            {
                new AnimationBlend2DPointAuthoring { ClipId = "idle", X = 0f, Y = 0f }
            }
        });
        AnimationAuthoringCompileResult kindConflict = AnimationAuthoringCompiler.Compile(
            new AnimationAuthoringCompileRequest { Package = package, ResourceCatalog = catalog });
        Require(kindConflict.AnimationValidationReport.Issues.Exists(issue =>
                issue.Code == "ANIM_BLEND_ID_DUPLICATE" ||
                issue.Code == "ANIM_LOCOMOTION_BLEND_KIND_CONFLICT"),
            "the same blend id across Blend1D and Blend2D should block export.");

        package.Sets[0].Groups[0].Blend2D.Clear();
        package.Sets[0].Groups[0].Blend1D[0].Points[1].ClipId = "missing";
        AnimationAuthoringCompileResult missingClip = AnimationAuthoringCompiler.Compile(
            new AnimationAuthoringCompileRequest { Package = package, ResourceCatalog = catalog });
        Require(missingClip.AnimationValidationReport.Issues.Exists(issue =>
                issue.Code == "ANIM_LOCOMOTION_CLIP_NOT_FOUND"),
            "locomotion blend clips must belong to the selected group.");
    }

    private static AnimationAuthoringPackage CreateLocomotionSlotContractPackage()
    {
        var group = new AnimationGroupAuthoring
        {
            GroupId = "group.locomotion",
            Usage = "legacy usage must not select locomotion",
            Clips = new List<AnimationClipMappingAuthoring>
            {
                new AnimationClipMappingAuthoring
                {
                    ClipId = "idle",
                    RuntimeResourceKey = "runtime.anim.idle",
                    Calibration = new AnimationClipCalibrationAuthoring
                    {
                        CycleDurationSeconds = 1f,
                        LeftFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                        {
                            new AnimationFootContactWindowAuthoring { StartNormalized = 0f, EndNormalized = 1f }
                        },
                        RightFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                        {
                            new AnimationFootContactWindowAuthoring { StartNormalized = 0f, EndNormalized = 1f }
                        }
                    }
                },
                new AnimationClipMappingAuthoring
                {
                    ClipId = "run",
                    RuntimeResourceKey = "runtime.anim.run",
                    Calibration = new AnimationClipCalibrationAuthoring
                    {
                        NativeVelocityY = 4f,
                        CycleDurationSeconds = 0.6f,
                        LeftFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                        {
                            new AnimationFootContactWindowAuthoring { StartNormalized = 0.1f, EndNormalized = 0.3f }
                        },
                        RightFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                        {
                            new AnimationFootContactWindowAuthoring { StartNormalized = 0.6f, EndNormalized = 0.8f }
                        }
                    }
                }
            },
            Blend1D = new List<AnimationBlend1DAuthoring>
            {
                new AnimationBlend1DAuthoring
                {
                    BlendId = "blend.speed",
                    Parameter = "speed",
                    DefaultClipId = "idle",
                    Points = new List<AnimationBlend1DPointAuthoring>
                    {
                        new AnimationBlend1DPointAuthoring { ClipId = "idle", Value = 0f },
                        new AnimationBlend1DPointAuthoring { ClipId = "run", Value = 4f }
                    }
                }
            }
        };
        return new AnimationAuthoringPackage
        {
            PackageId = "animation.slot.contract",
            StableId = "character.slot.contract.animation",
            SkeletonProfileId = "skeleton.humanoid",
            AvatarProfileId = "avatar.humanoid",
            Sets = new List<AnimationAuthoringSet>
            {
                new AnimationAuthoringSet
                {
                    SetId = "set.sword",
                    DefaultClipId = "idle",
                    FallbackClipId = "idle",
                    Groups = new List<AnimationGroupAuthoring> { group },
                    ActionBindings = new List<AnimationActionBindingAuthoring>
                    {
                        new AnimationActionBindingAuthoring
                        {
                            BindingId = "legacy.move",
                            ActionId = "character.move",
                            GroupId = group.GroupId,
                            BlendId = "blend.speed"
                        }
                    }
                }
            },
            Profiles = new List<AnimationAuthoringProfile>
            {
                new AnimationAuthoringProfile
                {
                    ProfileId = "profile.sword",
                    Slots = new List<AnimationProfileSlotAuthoring>
                    {
                        new AnimationProfileSlotAuthoring
                        {
                            SlotId = "locomotion",
                            Purpose = "ignored",
                            SetId = "set.sword",
                            GroupId = group.GroupId,
                            DefaultClipId = "idle",
                            DefaultBlendId = "blend.speed",
                            Required = true
                        }
                    }
                }
            }
        };
    }

    private static void AnimationAuthoringResourceFieldSpecs_FilterAndResolveContracts()
    {
        var collection = new AuthoringResourceCollection { ScopeId = "animation.test" };
        collection.Items.Add(CreateAnimationSourceItem(
            "unity.project.idle",
            AnimationAuthoringResourceUsages.AnimationClip,
            AuthoringResourceProviderIds.UnityProjectAssets,
            AuthoringResourceSourceKind.UnityAsset,
            AuthoringResourceBindingKind.UnityEditorOnlyAsset,
            "Assets/Art/Animations/idle.anim",
            string.Empty));
        collection.Items.Add(CreateAnimationSourceItem(
            "runtime.anim.locomotion",
            CharacterPackageResourceUsageIds.AnimationClipGroup,
            AuthoringResourceProviderIds.RuntimeCatalog,
            AuthoringResourceSourceKind.RuntimeCatalogAsset,
            AuthoringResourceBindingKind.ResourceManagerAsset,
            "char.test.anim.locomotion",
            "char.test.anim.locomotion"));
        collection.Items.Add(CreateAnimationSourceItem(
            "charpkg.test.anim.locomotion",
            CharacterPackageResourceUsageIds.AnimationClipGroup,
            AuthoringResourceProviderIds.CharacterPackage,
            AuthoringResourceSourceKind.PackageResource,
            AuthoringResourceBindingKind.PackageResource,
            "char.test.anim.locomotion",
            string.Empty));
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "unity:fbx",
            StableId = "unity.project.fbx.container",
            DisplayName = "Standing Run Forward.fbx",
            Kind = CharacterPackageResourceTypeIds.Model,
            Usage = CharacterPackageResourceUsageIds.PreviewMesh,
            SourceProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
            SourceKind = AuthoringResourceSourceKind.UnityAsset,
            BindingKind = AuthoringResourceBindingKind.UnityEditorOnlyAsset,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.EditorOnly,
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
                    BindingKind = AuthoringResourceBindingKind.UnityEditorOnlyAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.UnityAssetPath,
                    IsPrimary = true,
                    UnityAssetPath = "Assets/Art/Animations/Standing Run Forward.fbx",
                    ProviderResourceKey = "Assets/Art/Animations/Standing Run Forward.fbx"
                }
            }
        });
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "fmod:event",
            StableId = "fmod.event.body_contact.sole",
            DisplayName = "Sole Contact",
            Kind = CharacterPackageResourceTypeIds.Audio,
            Usage = AnimationAuthoringResourceUsages.FmodEvent,
            SourceProviderId = AuthoringResourceProviderIds.Fmod,
            SourceKind = AuthoringResourceSourceKind.FmodLibrary,
            BindingKind = AuthoringResourceBindingKind.AudioEventDefinition,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.AudioCueOnly,
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.Fmod,
                    BindingKind = AuthoringResourceBindingKind.AudioEventDefinition,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.FmodEventGuid,
                    IsPrimary = true,
                    ProviderResourceKey = "event:/Character/Test/SoleContact",
                    FmodEventGuid = "{sole_contact}",
                    ProviderData = new Dictionary<string, string>
                    {
                        { "audioCueId", "audio.cue.character.test.sole_contact" },
                        { "audioEventDefinitionId", "audio.event.character.test.sole_contact" }
                    }
                },
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.Fmod,
                    BindingKind = AuthoringResourceBindingKind.AudioCue,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.FmodEventGuid,
                    ProviderResourceKey = "audio.cue.character.test.sole_contact",
                    FmodEventGuid = "{sole_contact}",
                    ProviderData = new Dictionary<string, string>
                    {
                        { "audioCueId", "audio.cue.character.test.sole_contact" },
                        { "audioEventDefinitionId", "audio.event.character.test.sole_contact" }
                    }
                }
            }
        });
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "runtime:vfx",
            StableId = "runtime.vfx.spark",
            DisplayName = "Spark",
            Kind = CharacterPackageResourceTypeIds.Vfx,
            Usage = CharacterPackageResourceUsageIds.VfxCue,
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            SourceKind = AuthoringResourceSourceKind.RuntimeCatalogAsset,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.RuntimeReady
        });

        var service = new AuthoringResourceSelectionService();
        AuthoringResourceFieldSpec sourceClipSpec = AnimationAuthoringResourceFieldSpecs.CreateSourceClip();
        AuthoringResourcePickerQueryResult sourceQuery = service.Query(collection, sourceClipSpec, new AuthoringResourceConsumerContext { ConsumerKind = "AnimationEditor" });
        Require(sourceClipSpec.FieldKey == AnimationAuthoringResourceFieldKeys.SourceClip, "source clip field key should be stable.");
        Require(sourceClipSpec.PreloadPolicy == AuthoringResourcePreloadPolicies.AnimationWarmup, "source clip should use animation warmup preload policy.");
        Require(sourceQuery.Items.Find(item => item.Item.StableId == "unity.project.idle").Selectable, "direct Unity .anim source clip should be selectable.");
        Require(sourceQuery.Items.Find(item => item.Item.StableId == "runtime.anim.locomotion").Selectable, "runtime animation clip group should be selectable.");
        Require(sourceQuery.Items.Find(item => item.Item.StableId == "charpkg.test.anim.locomotion").Selectable, "package animation clip group should be selectable.");
        AuthoringResourcePickerItem fbxCandidate = sourceQuery.Items.Find(item => item.Item.StableId == "unity.project.fbx.container");
        Require(fbxCandidate != null && !fbxCandidate.Selectable, "FBX/model containers should not be selectable as Animation.SourceClip.");
        Require(fbxCandidate.Reasons.Exists(reason => reason.Code == AuthoringResourceSelectionReasonCodes.KindMismatch), "FBX/model rejection should include a kind mismatch.");

        AuthoringResourceSelectionResolutionResult sourceResult = service.Resolve(
            collection,
            sourceClipSpec,
            new AuthoringResourceConsumerContext(),
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "runtime.anim.locomotion",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog
            });
        Require(sourceResult.Accepted, "Animation.SourceClip should resolve runtime clip groups.");
        Require(sourceResult.Selection.RuntimeResourceKey == "char.test.anim.locomotion", "Animation.SourceClip runtime selection should preserve runtime resource key.");

        AuthoringResourceFieldSpec audioCueSpec = AnimationAuthoringResourceFieldSpecs.CreateEventAudioCue();
        AuthoringResourceSelectionResolutionResult audioCueResult = service.Resolve(
            collection,
            audioCueSpec,
            new AuthoringResourceConsumerContext(),
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "fmod.event.body_contact.sole",
                SourceProviderId = AuthoringResourceProviderIds.Fmod
            });
        Require(audioCueResult.Accepted, "Animation.EventAudioCue should accept FMOD event audio resources.");
        Require(audioCueResult.Selection.AudioCueId == "audio.cue.character.test.sole_contact", "Animation.EventAudioCue default output should fill AudioCueId.");
        Require(audioCueSpec.AcceptedUsages.Contains(AnimationAuthoringResourceUsages.FmodEvent), "Animation.EventAudioCue should accept fmodEvent usage.");

        AuthoringResourceSelectionResolutionResult audioEventResult = service.Resolve(
            collection,
            AnimationAuthoringResourceFieldSpecs.CreateEventAudioCue(AuthoringResourceSelectionOutputKind.AudioEventDefinitionId),
            new AuthoringResourceConsumerContext(),
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "fmod.event.body_contact.sole",
                SourceProviderId = AuthoringResourceProviderIds.Fmod
            });
        Require(audioEventResult.Accepted, "Animation.EventAudioCue should resolve audio event definitions when requested.");
        Require(audioEventResult.Selection.AudioEventDefinitionId == "audio.event.character.test.sole_contact", "Animation.EventAudioCue should fill AudioEventDefinitionId output.");

        AuthoringResourceSelectionResolutionResult wrongAudioResult = service.Resolve(
            collection,
            audioCueSpec,
            new AuthoringResourceConsumerContext(),
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "runtime.vfx.spark",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog
            });
        Require(!wrongAudioResult.Accepted, "Animation.EventAudioCue should reject VFX resources.");
        Require(wrongAudioResult.Reasons.Exists(reason => reason.Code == AuthoringResourceSelectionReasonCodes.KindMismatch), "Animation.EventAudioCue kind mismatch should be structured.");
    }

    private static AuthoringResourceItem CreateAnimationSourceItem(
        string stableId,
        string usage,
        string providerId,
        AuthoringResourceSourceKind sourceKind,
        AuthoringResourceBindingKind bindingKind,
        string providerKey,
        string runtimeKey)
    {
        return new AuthoringResourceItem
        {
            ResourceId = providerId + ":" + stableId,
            StableId = stableId,
            DisplayName = stableId,
            Kind = CharacterPackageResourceTypeIds.Animation,
            Usage = usage,
            SourceProviderId = providerId,
            SourceKind = sourceKind,
            BindingKind = bindingKind,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = string.IsNullOrWhiteSpace(runtimeKey) ? AuthoringResourceRuntimeAvailability.EditorOnly : AuthoringResourceRuntimeAvailability.RuntimeReady,
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = providerId,
                    BindingKind = bindingKind,
                    BindingKeyKind = string.IsNullOrWhiteSpace(runtimeKey) ? AuthoringResourceBindingKeyKinds.UnityAssetPath : AuthoringResourceBindingKeyKinds.RuntimeResourceKey,
                    IsPrimary = true,
                    ProviderResourceKey = providerKey,
                    RuntimeResourceKey = runtimeKey,
                    PackageResourceKey = bindingKind == AuthoringResourceBindingKind.PackageResource ? providerKey : string.Empty,
                    UnityAssetPath = bindingKind == AuthoringResourceBindingKind.UnityEditorOnlyAsset || bindingKind == AuthoringResourceBindingKind.UnityAsset ? providerKey : string.Empty
                }
            }
        };
    }

}
