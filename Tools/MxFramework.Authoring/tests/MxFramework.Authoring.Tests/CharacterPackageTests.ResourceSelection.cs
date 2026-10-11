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
    private static void AuthoringResourceSelectionService_FiltersAndResolvesFieldSpecs()
    {
        AuthoringResourceCollection collection = BuildAuthoringSelectionCollection();
        var service = new AuthoringResourceSelectionService();
        var context = new AuthoringResourceConsumerContext
        {
            ConsumerKind = "CharacterStudio",
            ConsumerStableId = "character.codex_flow_test",
            ScopeId = "character.codex_flow_test",
            SkeletonStableId = "skeleton.humanoid"
        };

        var characterModelSpec = new AuthoringResourceFieldSpec
        {
            FieldKey = "Character.Model",
            EditorKind = "CharacterStudio",
            AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Model },
            AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.CharacterModel },
            AcceptedProviderIds = new List<string> { AuthoringResourceProviderIds.RuntimeCatalog },
            AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.ResourceManagerAsset },
            RequireRuntimeLoadable = true,
            RequireUnityImported = true,
            PreloadPolicy = AuthoringResourcePreloadPolicies.SpawnCritical,
            OutputKind = AuthoringResourceSelectionOutputKind.RuntimeResourceKey
        };

        AuthoringResourcePickerQueryResult query = service.Query(collection, characterModelSpec, context);
        Require(query.Items.Count == collection.Items.Count, "query should return one picker item per resource.");
        AuthoringResourcePickerItem modelCandidate = query.Items.Find(item => item.Item.StableId == "runtime.character.body");
        Require(modelCandidate != null && modelCandidate.Selectable, "runtime character model should be selectable.");
        AuthoringResourcePickerItem audioCandidate = query.Items.Find(item => item.Item.StableId == "audio.hit");
        Require(audioCandidate != null && !audioCandidate.Selectable, "audio item should not be selectable for character model field.");
        Require(audioCandidate.Reasons.Exists(reason => reason.Code == AuthoringResourceSelectionReasonCodes.KindMismatch), "blocked candidate should include structured kind mismatch reason.");

        AuthoringResourceSelectionResolutionResult modelResult = service.Resolve(
            collection,
            characterModelSpec,
            context,
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "runtime.character.body",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset
            });
        Require(modelResult.Accepted, "runtime character model selection should resolve.");
        Require(modelResult.Selection.RuntimeResourceKey == "char.codex_flow_test.model.body.prefab", "runtime selection should fill runtime resource key.");
        Require(string.IsNullOrEmpty(modelResult.Selection.PackageResourceKey), "runtime selection should not gain package-local key.");

        AuthoringResourceSelectionResolutionResult packageResult = service.Resolve(
            collection,
            new AuthoringResourceFieldSpec
            {
                FieldKey = "ResourceLibrary.PackageResource",
                AcceptedProviderIds = new List<string> { AuthoringResourceProviderIds.CharacterPackage },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.PackageResource },
                OutputKind = AuthoringResourceSelectionOutputKind.PackageResourceKey
            },
            context,
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "charpkg.codex_flow_test.resource.model.body",
                SourceProviderId = AuthoringResourceProviderIds.CharacterPackage,
                BindingKind = AuthoringResourceBindingKind.PackageResource
            });
        Require(packageResult.Accepted, "package resource selection should resolve.");
        Require(packageResult.Selection.PackageResourceKey == "char.codex_flow_test.model.body", "package resource should fill package key.");
        Require(string.IsNullOrEmpty(packageResult.Selection.RuntimeResourceKey), "package resource must not be copied into runtime key.");

        AuthoringResourceSelectionResolutionResult iconResult = service.Resolve(
            collection,
            new AuthoringResourceFieldSpec
            {
                FieldKey = "Ui.Icon",
                EditorKind = "UiEditor",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Texture },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.PreviewThumbnail },
                AcceptedProviderIds = new List<string> { AuthoringResourceProviderIds.UnityAssetDatabase },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.UnityAsset },
                RequireUnityImported = true,
                OutputKind = AuthoringResourceSelectionOutputKind.UnityGuid
            },
            context,
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "unity.icon.portrait",
                SourceProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                BindingKind = AuthoringResourceBindingKind.UnityAsset
            });
        Require(iconResult.Accepted, "UI icon Unity asset should resolve.");
        Require(iconResult.Selection.UnityGuid == "guid-portrait", "Unity icon selection should fill Unity GUID.");
        Require(string.IsNullOrEmpty(iconResult.Selection.RuntimeResourceKey), "Unity-only selection must not fill runtime key.");

        AuthoringResourceSelectionResolutionResult audioResult = service.Resolve(
            collection,
            new AuthoringResourceFieldSpec
            {
                FieldKey = "CombatAction.HitSfx",
                EditorKind = "CombatEditor",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Audio },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.AudioCue },
                AcceptedProviderIds = new List<string> { AuthoringResourceProviderIds.Fmod },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.AudioCue },
                PreloadPolicy = AuthoringResourcePreloadPolicies.AudioBank,
                OutputKind = AuthoringResourceSelectionOutputKind.AudioCueId
            },
            context,
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "audio.hit",
                SourceProviderId = AuthoringResourceProviderIds.Fmod,
                BindingKind = AuthoringResourceBindingKind.AudioCue
            });
        Require(audioResult.Accepted, "FMOD audio cue should resolve.");
        Require(audioResult.Selection.AudioCueId == "cue.hit", "audio selection should fill audio cue id.");
        Require(string.IsNullOrEmpty(audioResult.Selection.RuntimeResourceKey), "audio cue must not gain runtime key.");

        AuthoringResourceSelectionResolutionResult externalResult = service.Resolve(
            collection,
            characterModelSpec,
            context,
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "external.body.fbx",
                SourceProviderId = AuthoringResourceProviderIds.ExternalImportStaging,
                BindingKind = AuthoringResourceBindingKind.ExternalSource
            });
        Require(!externalResult.Accepted, "external source should be blocked when runtime loadable model is required.");
        Require(externalResult.Reasons.Exists(reason => reason.Code == AuthoringResourceSelectionReasonCodes.ProviderMismatch), "external source should report provider mismatch.");
        Require(externalResult.Reasons.Exists(reason => reason.Code == AuthoringResourceSelectionReasonCodes.NotRuntimeLoadable), "external source should report runtime availability mismatch.");

        AuthoringResourceSelectionResolutionResult warnResult = service.Resolve(
            collection,
            new AuthoringResourceFieldSpec
            {
                FieldKey = "Equipment.MainHand.Model",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Model },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.WeaponModel },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.ResourceManagerAsset },
                AllowIncompatibleWithWarning = true,
                CompatibilityFilter = new AuthoringResourceCompatibility { SlotId = "mainHand" },
                OutputKind = AuthoringResourceSelectionOutputKind.RuntimeResourceKey
            },
            context,
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "runtime.weapon.offhand",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset
            });
        Require(warnResult.Accepted, "incompatible weapon slot should be selectable when warnings are allowed.");
        Require(warnResult.Reasons.Exists(reason => reason.Code == AuthoringResourceSelectionReasonCodes.SlotMismatch && !reason.BlocksSelection), "slot mismatch should be a non-blocking structured warning.");

        AuthoringResourceSelectionResolutionResult animationResult = service.Resolve(
            collection,
            new AuthoringResourceFieldSpec
            {
                FieldKey = "Animation.ClipGroup",
                EditorKind = "AnimationEditor",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Animation },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.AnimationClipGroup },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.ResourceManagerAsset },
                RequireRuntimeLoadable = true,
                OutputKind = AuthoringResourceSelectionOutputKind.RuntimeResourceKey
            },
            context,
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "runtime.anim.locomotion",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset
            });
        Require(animationResult.Accepted && animationResult.Selection.RuntimeResourceKey == "char.codex_flow_test.anim.locomotion", "animation clip group field should resolve through same contract.");

        AuthoringResourceSelectionResolutionResult vfxResult = service.Resolve(
            collection,
            new AuthoringResourceFieldSpec
            {
                FieldKey = "Vfx.Prefab",
                EditorKind = "VfxEditor",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Vfx },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.VfxCue },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.ResourceManagerAsset },
                RequireRuntimeLoadable = true,
                OutputKind = AuthoringResourceSelectionOutputKind.RuntimeResourceKey
            },
            context,
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "runtime.vfx.slash",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset
            });
        Require(vfxResult.Accepted && vfxResult.Selection.RuntimeResourceKey == "char.codex_flow_test.vfx.slash", "VFX prefab field should resolve through same contract.");
    }

    private static void AuthoringResourceSelectionService_SourceClipAndFmodMultiBinding_DoNotDriftResolution()
    {
        var service = new AuthoringResourceSelectionService();
        var collection = new AuthoringResourceCollection { ScopeId = "selection-regression" };

        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "runtime:anim.run_r",
            StableId = "runtime.anim.run_r",
            DisplayName = "Run_R",
            Kind = CharacterPackageResourceTypeIds.Animation,
            Usage = AnimationAuthoringResourceUsages.AnimationClip,
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            SourceKind = AuthoringResourceSourceKind.RuntimeCatalogAsset,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.RuntimeReady,
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                    BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.RuntimeResourceKey,
                    IsPrimary = true,
                    ProviderResourceKey = "char.codex_flow_test.anim.run_r",
                    RuntimeResourceKey = "char.codex_flow_test.anim.run_r"
                },
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                    BindingKind = AuthoringResourceBindingKind.UnityAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.UnityAssetPath,
                    ProviderResourceKey = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_right.anim",
                    UnityAssetPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_right.anim"
                }
            }
        });

        AuthoringResourceSelectionResolutionResult sourceClipResult = service.Resolve(
            collection,
            AnimationAuthoringResourceFieldSpecs.CreateSourceClip(),
            new AuthoringResourceConsumerContext { ConsumerKind = "AnimationEditor" },
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "runtime.anim.run_r",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset
            });

        Require(sourceClipResult.Accepted, "source clip selection should resolve for runtime animation item.");
        Require(sourceClipResult.Selection.BindingKind == AuthoringResourceBindingKind.ResourceManagerAsset, "source clip selection should keep the intended binding kind.");
        Require(sourceClipResult.Selection.RuntimeResourceKey == "char.codex_flow_test.anim.run_r", "source clip selection should keep runtime resource key.");
        Require(sourceClipResult.Selection.UnityAssetPath == "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_right.anim", "source clip selection should backfill unity asset path from sibling binding.");

        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "fmod:event.hit",
            StableId = "fmod.event.hit",
            DisplayName = "Hit",
            Kind = CharacterPackageResourceTypeIds.Audio,
            Usage = CharacterPackageResourceUsageIds.AudioCue,
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
                    ProviderResourceKey = "event:/Character/Hit",
                    FmodEventGuid = "{fmod-hit}",
                    ProviderData = new Dictionary<string, string>
                    {
                        { "audioCueId", "cue.hit" },
                        { "audioEventDefinitionId", "event.hit" }
                    }
                },
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.Fmod,
                    BindingKind = AuthoringResourceBindingKind.AudioCue,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.FmodEventGuid,
                    ProviderResourceKey = "cue.hit",
                    FmodEventGuid = "{fmod-hit}",
                    ProviderData = new Dictionary<string, string>
                    {
                        { "audioCueId", "cue.hit" },
                        { "audioEventDefinitionId", "event.hit" }
                    }
                },
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.Fmod,
                    BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.RuntimeResourceKey,
                    ProviderResourceKey = "runtime.should.not.leak",
                    RuntimeResourceKey = "runtime.should.not.leak"
                }
            }
        });

        AuthoringResourceSelectionResolutionResult audioCueResult = service.Resolve(
            collection,
            AnimationAuthoringResourceFieldSpecs.CreateEventAudioCue(AuthoringResourceSelectionOutputKind.AudioCueId),
            new AuthoringResourceConsumerContext { ConsumerKind = "AnimationEditor" },
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "fmod.event.hit",
                SourceProviderId = AuthoringResourceProviderIds.Fmod,
                BindingKind = AuthoringResourceBindingKind.AudioCue
            });

        Require(audioCueResult.Accepted, "fmod audio cue selection should resolve.");
        Require(audioCueResult.Selection.BindingKind == AuthoringResourceBindingKind.AudioCue, "fmod cue selection should keep audio cue binding kind.");
        Require(audioCueResult.Selection.AudioCueId == "cue.hit", "fmod cue selection should preserve cue id.");
        Require(string.IsNullOrEmpty(audioCueResult.Selection.RuntimeResourceKey), "fmod cue selection should not backfill runtime keys from sibling bindings.");
        Require(string.IsNullOrEmpty(audioCueResult.Selection.UnityAssetPath), "fmod cue selection should not backfill unity asset path from sibling bindings.");
    }

    private static AuthoringResourceCollection BuildAuthoringSelectionCollection()
    {
        var collection = new AuthoringResourceCollection { ScopeId = "test" };
        collection.Items.Add(CreateRuntimeResource("runtime.character.body", CharacterPackageResourceTypeIds.Model, CharacterPackageResourceUsageIds.CharacterModel, "char.codex_flow_test.model.body.prefab", "body", "skeleton.humanoid"));
        collection.Items.Add(CreateRuntimeResource("runtime.anim.locomotion", CharacterPackageResourceTypeIds.Animation, CharacterPackageResourceUsageIds.AnimationClipGroup, "char.codex_flow_test.anim.locomotion", "", "skeleton.humanoid"));
        string[] animationKeys =
        {
            "art.character.skeleton.animation.standing_idle",
            "art.character.skeleton.animation.standing_walk_forward",
            "art.character.skeleton.animation.standing_run_forward",
            "art.character.skeleton.animation.standing_walk_right",
            "art.character.skeleton.animation.standing_walk_left",
            "art.character.skeleton.animation.standing_walk_back",
            "art.character.skeleton.animation.standing_run_right",
            "art.character.skeleton.animation.standing_run_left",
            "art.character.skeleton.animation.standing_run_back"
        };
        for (int i = 0; i < animationKeys.Length; i++)
            collection.Items.Add(CreateRuntimeResource(
                "runtime." + animationKeys[i],
                CharacterPackageResourceTypeIds.Animation,
                AnimationAuthoringResourceUsages.AnimationClip,
                animationKeys[i],
                string.Empty,
                "skeleton.humanoid"));
        collection.Items.Add(CreateRuntimeResource(
            "art.character.skeleton.animation.damage_ko_mid_2",
            CharacterPackageResourceTypeIds.Animation,
            AnimationAuthoringResourceUsages.AnimationClip,
            "art.character.skeleton.animation.damage_ko_mid_2",
            string.Empty,
            "skeleton.humanoid"));
        collection.Items.Add(CreateRuntimeResource("runtime.vfx.slash", CharacterPackageResourceTypeIds.Vfx, CharacterPackageResourceUsageIds.VfxCue, "char.codex_flow_test.vfx.slash", "", ""));
        collection.Items.Add(CreateRuntimeResource("runtime.weapon.offhand", CharacterPackageResourceTypeIds.Model, CharacterPackageResourceUsageIds.WeaponModel, "char.codex_flow_test.weapon.shield.prefab", "offHand", "skeleton.humanoid"));
        collection.Items.Add(CreateGlobalProfileResource(
            "global.profile.common.model.weapon.common_weapon_1.model",
            CharacterPackageResourceTypeIds.Model,
            CharacterPackageResourceUsageIds.WeaponModel,
            "weapon.common_weapon_1.model",
            "mainHand",
            "skeleton.humanoid"));
        collection.Items.Add(CreateGlobalProfileResource(
            "global.profile.common.model.weapon.000001.model",
            CharacterPackageResourceTypeIds.Model,
            CharacterPackageResourceUsageIds.WeaponModel,
            "weapon.000001.model",
            "mainHand",
            "skeleton.humanoid"));
        collection.Items.Add(CreateGlobalProfileResource(
            "global.profile.common.model.weapon.common_kite_shield.model",
            CharacterPackageResourceTypeIds.Model,
            CharacterPackageResourceUsageIds.WeaponModel,
            "weapon.common_kite_shield.model",
            "offHand",
            "skeleton.humanoid"));
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "characterPackage:charpkg.codex_flow_test.resource.model.body",
            StableId = "charpkg.codex_flow_test.resource.model.body",
            DisplayName = "Body Package Resource",
            Kind = CharacterPackageResourceTypeIds.Model,
            Usage = CharacterPackageResourceUsageIds.CharacterModel,
            SourceProviderId = AuthoringResourceProviderIds.CharacterPackage,
            SourceKind = AuthoringResourceSourceKind.PackageResource,
            BindingKind = AuthoringResourceBindingKind.PackageResource,
            ImportStatus = AuthoringResourceImportStatus.New,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.Unknown,
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.CharacterPackage,
                    BindingKind = AuthoringResourceBindingKind.PackageResource,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.PackageResourceKey,
                    IsPrimary = true,
                    ProviderResourceKey = "char.codex_flow_test.model.body",
                    PackageResourceKey = "char.codex_flow_test.model.body"
                }
            }
        });
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "unity:portrait",
            StableId = "unity.icon.portrait",
            DisplayName = "Portrait",
            Kind = CharacterPackageResourceTypeIds.Texture,
            Usage = CharacterPackageResourceUsageIds.PreviewThumbnail,
            SourceProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
            SourceKind = AuthoringResourceSourceKind.UnityAsset,
            BindingKind = AuthoringResourceBindingKind.UnityAsset,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.EditorOnly,
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                    BindingKind = AuthoringResourceBindingKind.UnityAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.UnityGuid,
                    IsPrimary = true,
                    UnityGuid = "guid-portrait",
                    UnityAssetPath = "Assets/UI/portrait.png"
                }
            }
        });
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "fmod:hit",
            StableId = "audio.hit",
            DisplayName = "Hit",
            Kind = CharacterPackageResourceTypeIds.Audio,
            Usage = CharacterPackageResourceUsageIds.AudioCue,
            SourceProviderId = AuthoringResourceProviderIds.Fmod,
            SourceKind = AuthoringResourceSourceKind.FmodLibrary,
            BindingKind = AuthoringResourceBindingKind.AudioCue,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.AudioCueOnly,
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.Fmod,
                    BindingKind = AuthoringResourceBindingKind.AudioCue,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.FmodEventGuid,
                    IsPrimary = true,
                    FmodEventPath = "event:/Character/CodexFlowTest/Hit",
                    FmodEventGuid = "{hit}",
                    ProviderData = new Dictionary<string, string>
                    {
                        { "audioCueId", "cue.hit" },
                        { "audioEventDefinitionId", "event.hit" }
                    }
                }
            }
        });
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "external:body.fbx",
            StableId = "external.body.fbx",
            DisplayName = "body.fbx",
            Kind = CharacterPackageResourceTypeIds.Model,
            Usage = CharacterPackageResourceUsageIds.CharacterModel,
            SourceProviderId = AuthoringResourceProviderIds.ExternalImportStaging,
            SourceKind = AuthoringResourceSourceKind.ExternalFile,
            BindingKind = AuthoringResourceBindingKind.ExternalSource,
            ImportStatus = AuthoringResourceImportStatus.New,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.NotRuntimeLoadable,
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.ExternalImportStaging,
                    BindingKind = AuthoringResourceBindingKind.ExternalSource,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.ExternalSourcePath,
                    IsPrimary = true,
                    ExternalSourcePath = "/imports/body.fbx"
                }
            }
        });
        return collection;
    }

    private static AuthoringResourceItem CreateRuntimeResource(string stableId, string kind, string usage, string runtimeKey, string slotId, string skeletonStableId)
    {
        return new AuthoringResourceItem
        {
            ResourceId = "runtime:" + runtimeKey,
            StableId = stableId,
            DisplayName = stableId,
            Kind = kind,
            Usage = usage,
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            SourceKind = AuthoringResourceSourceKind.RuntimeCatalogAsset,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.RuntimeReady,
            Compatibility = new AuthoringResourceCompatibility
            {
                SlotId = slotId,
                SkeletonStableId = skeletonStableId
            },
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                    BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.RuntimeResourceKey,
                    IsPrimary = true,
                    RuntimeResourceKey = runtimeKey,
                    ProviderResourceKey = runtimeKey,
                    AssetType = "UnityEngine.GameObject",
                    Hash = "sha256:" + stableId
                }
            }
        };
    }

}
