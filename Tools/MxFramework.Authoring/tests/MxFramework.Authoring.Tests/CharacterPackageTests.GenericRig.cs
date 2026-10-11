using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    private static void AnimationAuthoringPackage_GenericRigRoundTripAndCompilerProjection()
    {
        var package = new AnimationAuthoringPackage
        {
            PackageId = "animation.000003",
            StableId = "charpkg.000003.animation",
            RigType = GlobalAnimationRigTypes.Generic,
            SkeletonProfileId = "skeleton.000003.generic",
            AvatarProfileId = "avatar.000003.generic",
            Sets = new List<AnimationAuthoringSet>
            {
                new AnimationAuthoringSet
                {
                    SetId = "set.locomotion",
                    Groups = new List<AnimationGroupAuthoring>
                    {
                        new AnimationGroupAuthoring
                        {
                            GroupId = "group.locomotion",
                            Clips = new List<AnimationClipMappingAuthoring>
                            {
                                new AnimationClipMappingAuthoring
                                {
                                    ClipId = "run.f",
                                    SourceSubClipId = "Unreal Take",
                                    SourceClipName = "Dragon14_Run1",
                                    Loop = true,
                                    Metadata = new Dictionary<string, string> { ["loopBlend"] = "true" }
                                }
                            }
                        }
                    }
                }
            }
        };

        string json = JsonSerializer.Serialize(package, JsonOptions);
        AnimationAuthoringPackage roundTrip = JsonSerializer.Deserialize<AnimationAuthoringPackage>(json, JsonOptions);
        AnimationAuthoringCompileResult compiled = AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest
        {
            Package = roundTrip
        });

        Require(roundTrip?.RigType == GlobalAnimationRigTypes.Generic,
            "AnimationAuthoringPackage rigType must survive JSON roundtrip.");
        Require(compiled.AnimationSetDefinition.RigType == GlobalAnimationRigTypes.Generic
            && compiled.AnimationPackageExpectation.RigType == GlobalAnimationRigTypes.Generic,
            "Generic rigType must be projected into both compiled animation contracts.");
        Require(compiled.AnimationSetDefinition.Sets[0].Groups[0].Clips[0].LoopBlend,
            "animation loopBlend metadata must reach the compiled Unity-facing clip contract.");
    }

    private static void GlobalAnimationCompatibility_GenericRequiresExactNativeIdentity()
    {
        var character = new CharacterResourcePackage
        {
            Geometry = new CharacterAuthoringGeometry
            {
                BodyProfile = new CharacterBodyGeometryProfile { BodyKind = CharacterAuthoringBodyKind.Skeletal }
            },
            AnimationAuthoring = new AnimationAuthoringPackage
            {
                RigType = GlobalAnimationRigTypes.Generic,
                SkeletonProfileId = "skeleton.000003.generic",
                AvatarProfileId = "avatar.000003.generic"
            }
        };
        var native = new GlobalAnimationConfiguration
        {
            RigType = GlobalAnimationRigTypes.Generic,
            SkeletonProfileId = "skeleton.000003.generic",
            AvatarProfileId = "avatar.000003.generic"
        };
        var foreign = new GlobalAnimationConfiguration
        {
            RigType = GlobalAnimationRigTypes.Generic,
            SkeletonProfileId = "skeleton.other.generic",
            AvatarProfileId = "avatar.other.generic"
        };

        Require(GlobalWeaponConfigurationResolver.ResolveAnimationCompatibility(character, native).Kind
                == GlobalAnimationCompatibilityKind.Native,
            "Generic animation should be compatible with the exact native Skeleton and Avatar identities.");
        Require(!GlobalWeaponConfigurationResolver.ResolveAnimationCompatibility(character, foreign).IsCompatible,
            "Generic animation must reject cross-skeleton reuse even when retargeting flags are present.");

        character.AnimationAuthoring.RigType = GlobalAnimationRigTypes.Unspecified;
        Require(!GlobalWeaponConfigurationResolver.ResolveAnimationCompatibility(character, native).IsCompatible,
            "Skeletal Generic-looking identities must remain blocked when rigType is unspecified.");

        character.AnimationAuthoring.SkeletonProfileId = "skeleton.legacy.humanoid";
        character.AnimationAuthoring.AvatarProfileId = "avatar.legacy.humanoid";
        Require(GlobalWeaponConfigurationResolver.ResolveCharacterRigType(character) == GlobalAnimationRigTypes.Humanoid,
            "legacy Humanoid identities must remain inferable for backward compatibility.");
    }

    private static void CharacterSourceManifest_TextureSemanticRoundTrip()
    {
        var manifest = new CharacterSourceManifestDocument
        {
            CharacterId = "000003",
            PackageId = "000003",
            Items = new List<CharacterSourceManifestItem>
            {
                new CharacterSourceManifestItem
                {
                    Id = "body-normal",
                    PresetId = "texture",
                    Source = new CharacterSourceReference { Path = "Textures/T_Dragon14_Body_skin2_Normal.png" },
                    Target = new CharacterSourceTarget
                    {
                        ResourceKey = "char.000003.texture.body_normal",
                        ResourceKind = CharacterPackageResourceTypeIds.Texture,
                        ResourceUsage = CharacterPackageResourceUsageIds.Texture,
                        TextureSemantic = CharacterTextureSemanticIds.Normal
                    }
                }
            }
        };

        string json = JsonSerializer.Serialize(manifest, JsonOptions);
        CharacterSourceManifestDocument roundTrip = JsonSerializer.Deserialize<CharacterSourceManifestDocument>(json, JsonOptions);
        Require(roundTrip?.Items[0].Target.TextureSemantic == CharacterTextureSemanticIds.Normal,
            "character source texture semantic must survive JSON roundtrip.");
    }

    private static void CharacterResourcePipeline_GenericAnimationFbxIsSupported()
    {
        Require(CharacterPackageResourcePipeline.IsSupportedV1Format(new CharacterPackageResourceEntry
        {
            TypeId = CharacterPackageResourceTypeIds.Animation,
            SourceFormat = CharacterPackageResourceFormatIds.Fbx
        }), "Generic native animation FBX must be a supported package resource format.");
    }

    private static void CharacterAuthoringCompiler_GenericSkeletalBodyProjectsAsCustomBodyWithExplicitParts()
    {
        string root = FindRepoRoot();
        string packagePath = Path.Combine(root, "Authoring", "CharacterPackages", "character-000003");
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, JsonOptions);
        CharacterAuthoringCompileResult result = CharacterAuthoringCompiler.Compile(
            new CharacterAuthoringCompileRequest { Package = package, PackageRootPath = packagePath });
        PatchEntry bodyProfile = result.GeneratedConfigPatch.Patch.Entries.Find(entry =>
            entry.Source == CharacterApplicationCompilerTableNames.CharacterBodyProfileConfig);

        Require(bodyProfile != null && bodyProfile.Fields["BodyKind"].Scalar == "Custom",
            "a Generic Skeletal character must project to Custom application body kind, not Humanoid.");
        Require(result.GeneratedConfigPatch.Patch.Entries.Exists(entry =>
                entry.Source == CharacterApplicationCompilerTableNames.CharacterBodyPartConfig),
            "Dragon14 must preserve its explicitly authored creature BodyParts.");
    }

    private static void CharacterWeaponValidation_EmptyOptionalReactionConfigurationIsValid()
    {
        GlobalWeaponLibrary library = GlobalWeaponLibrarySchema.CreateDefault();
        library.ReactionConfigurations.Add(new GlobalReactionConfiguration
        {
            ReactionConfigurationId = "reaction.000003.default",
            DisplayName = "Dragon14 Default Reaction",
            DefaultSlotId = string.Empty,
            Slots = new List<GlobalReactionSlotConfiguration>()
        });
        var report = new CharacterAuthoringValidationReport();
        CharacterWeaponConfigurationValidator.Validate(report, new CharacterResourcePackage
        {
            ApplicationConfig = new CharacterApplicationAuthoringSummary { SchemaVersion = "8.0" },
            GlobalWeaponLibrary = library
        });

        Require(!report.Issues.Exists(issue =>
                issue.Code == CharacterAuthoringValidationCodes.GlobalReactionDefaultSlotMissing
                || issue.Code == CharacterAuthoringValidationCodes.GlobalReactionDefaultSlotNotFound),
            "an empty optional reaction configuration must not require a synthetic reaction Slot.");
    }

    private static void CharacterReadiness_EmptyOptionalReactionConfigurationIsReady()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-character-empty-reaction-" + Guid.NewGuid().ToString("N"));
        const string packageRelative = "Tools/MxFramework.Authoring/samples/character-codex-flow-test";
        try
        {
            CopyDirectory(
                FindSamplePath("character-codex-flow-test"),
                Path.Combine(tempRoot, packageRelative.Replace('/', Path.DirectorySeparatorChar)));
            CopyTestFile(
                Path.Combine(FindRepoRoot(), "WeaponSource", "global_weapon_library.json"),
                Path.Combine(tempRoot, "WeaponSource", "global_weapon_library.json"));
            CopyDirectory(
                Path.Combine(FindRepoRoot(), "Assets", "MxFrameworkGenerated", "CharacterActions"),
                Path.Combine(tempRoot, "Assets", "MxFrameworkGenerated", "CharacterActions"));

            string applicationPath = Path.Combine(
                tempRoot,
                packageRelative.Replace('/', Path.DirectorySeparatorChar),
                "config",
                "character_application.json");
            CharacterApplicationAuthoringSummary application = JsonSerializer.Deserialize<CharacterApplicationAuthoringSummary>(
                File.ReadAllText(applicationPath),
                JsonOptions) ?? new CharacterApplicationAuthoringSummary();
            string libraryPath = Path.Combine(tempRoot, "WeaponSource", "global_weapon_library.json");
            GlobalWeaponLibrary library = JsonSerializer.Deserialize<GlobalWeaponLibrary>(
                File.ReadAllText(libraryPath),
                JsonOptions) ?? GlobalWeaponLibrarySchema.CreateDefault();
            Require(application.CombinedWeaponConfigurationBindings != null &&
                    application.CombinedWeaponConfigurationBindings.Count > 0,
                "readiness fixture must reference at least one combined weapon configuration.");
            string combinedId = application.CombinedWeaponConfigurationBindings[0].CombinedWeaponConfigurationId;
            CombinedWeaponConfigurationAuthoringSummary combined = library.CombinedWeaponConfigurations.Find(item =>
                item != null && string.Equals(item.CombinedWeaponConfigurationId, combinedId, StringComparison.Ordinal));
            Require(combined != null && !string.IsNullOrWhiteSpace(combined.ReactionConfigurationId),
                "readiness fixture must resolve a reaction configuration.");
            string reactionId = combined.ReactionConfigurationId;
            GlobalReactionConfiguration reaction = library.ReactionConfigurations.Find(item =>
                item != null && string.Equals(item.ReactionConfigurationId, reactionId, StringComparison.Ordinal));
            Require(reaction != null, "readiness fixture reaction configuration must exist.");
            reaction.DefaultSlotId = string.Empty;
            reaction.Slots = new List<GlobalReactionSlotConfiguration>();
            File.WriteAllText(libraryPath, JsonSerializer.Serialize(library, JsonOptions));

            CharacterAuthoringReadinessSnapshot snapshot =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(
                    tempRoot,
                    packageRelative,
                    packageRelative,
                    JsonOptions);
            CharacterAuthoringReadinessStage stage = snapshot.Stages.Find(item => item.Id == "ReactionBinding");
            Require(stage != null && stage.Status == "Ready",
                "an explicitly empty optional reaction configuration must keep ReactionBinding ready: " +
                JsonSerializer.Serialize(stage?.Blockers, JsonOptions));
            Require(!stage.Blockers.Exists(issue =>
                    issue.Code == "REACTION_DEFAULT_SLOT_MISSING" || issue.Code == "REACTION_DEFAULT_SLOT_NOT_FOUND"),
                "empty optional reaction readiness must not require a synthetic default Slot.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void AnimationAuthoringCompiler_FbxTakeIsAConcreteClipSource()
    {
        const string resourceKey = "char.000003.animation.idle1";
        var package = new AnimationAuthoringPackage
        {
            PackageId = "animation.000003",
            RigType = GlobalAnimationRigTypes.Generic,
            SkeletonProfileId = "skeleton.000003.generic",
            AvatarProfileId = "avatar.000003.generic",
            Sets = new List<AnimationAuthoringSet>
            {
                new AnimationAuthoringSet
                {
                    SetId = "set.000003.locomotion",
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
                                    RuntimeResourceKey = resourceKey,
                                    SourceSubClipId = "Unreal Take",
                                    SourceClipName = "Dragon14_Idle1",
                                    SourceSelection = new AuthoringResourceSelectionRef
                                    {
                                        PackageResourceKey = resourceKey,
                                        RuntimeResourceKey = resourceKey
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
        AnimationAuthoringCompileResult compiled = AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest
        {
            Package = package,
            ResourceCatalog = new CharacterPackageResourceCatalog
            {
                Entries = new List<CharacterPackageResourceEntry>
                {
                    new CharacterPackageResourceEntry
                    {
                        ResourceKey = resourceKey,
                        StableId = "charpkg.000003.resource.anim.idle1",
                        TypeId = CharacterPackageResourceTypeIds.Animation,
                        Usage = CharacterPackageResourceUsageIds.AnimationClipGroup,
                        SourceFormat = CharacterPackageResourceFormatIds.Fbx,
                        RelativePath = "resources/animations/anim000003_idle1.fbx"
                    }
                }
            }
        });

        Require(!compiled.AnimationValidationReport.Issues.Exists(issue => issue.Code == "ANIM_RUNTIME_TYPE_MISMATCH"),
            "an FBX animationClipGroup with an explicit source Take must compile as one concrete AnimationClip source.");
    }

    private static void CharacterAnimationFbx_CompilesToStandaloneAnimRuntimeAsset()
    {
        const string resourceKey = "char.000003.anim.attack1a";
        var entry = new CharacterPackageResourceEntry
        {
            ResourceKey = resourceKey,
            StableId = "charpkg.000003.resource.anim.attack1a",
            TypeId = CharacterPackageResourceTypeIds.Animation,
            Usage = CharacterPackageResourceUsageIds.AnimationClipGroup,
            SourceFormat = CharacterPackageResourceFormatIds.Fbx,
            RelativePath = "resources/animations/anim000003_attack1a.fbx"
        };
        var package = new CharacterResourcePackage
        {
            Manifest = new CharacterPackageManifest { PackageId = "000003" },
            ResourceCatalog = new CharacterPackageResourceCatalog
            {
                Entries = new List<CharacterPackageResourceEntry> { entry }
            },
            AnimationAuthoring = new AnimationAuthoringPackage
            {
                PackageId = "animation.000003",
                RigType = GlobalAnimationRigTypes.Generic,
                SkeletonProfileId = "skeleton.000003.generic",
                AvatarProfileId = "avatar.000003.generic",
                Sets = new List<AnimationAuthoringSet>
                {
                    new AnimationAuthoringSet
                    {
                        SetId = "set.000003.attacks",
                        Groups = new List<AnimationGroupAuthoring>
                        {
                            new AnimationGroupAuthoring
                            {
                                GroupId = "group.attacks",
                                Clips = new List<AnimationClipMappingAuthoring>
                                {
                                    new AnimationClipMappingAuthoring
                                    {
                                        ClipId = "attack1a",
                                        RuntimeResourceKey = resourceKey,
                                        SourceSubClipId = "Unreal Take",
                                        SourceClipName = "Dragon14_Attack1a",
                                        SourceSelection = new AuthoringResourceSelectionRef
                                        {
                                            PackageResourceKey = resourceKey,
                                            RuntimeResourceKey = resourceKey
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        CharacterResourcePlanCompileResult plan = CharacterResourcePlanCompiler.Compile(
            new CharacterResourcePlanCompileRequest { Package = package });
        RuntimeResourceCatalogEntryDocument runtime = plan.RuntimeResourceCatalog.Entries.Find(item => item.Id == resourceKey);
        Require(runtime != null, "referenced FBX animation source should produce a runtime catalog entry.");
        Require(runtime.Address == "Assets/MxFrameworkGenerated/CharacterPackages/000003/runtime_assets/animation_clips/char.000003.anim.attack1a.anim",
            "runtime animation address must be the extracted standalone .anim asset.");
        Require(runtime.ProviderData["sourceFormat"] == CharacterPackageResourceFormatIds.Anim
            && runtime.ProviderData["authoringSourceFormat"] == CharacterPackageResourceFormatIds.Fbx,
            "runtime catalog must distinguish .anim runtime format from FBX authoring provenance.");
        Require(runtime.ProviderData["sourceFbxAssetPath"].EndsWith("anim000003_attack1a.fbx", StringComparison.OrdinalIgnoreCase),
            "runtime catalog must retain the source FBX path only as extraction provenance.");

        CharacterResourceMappingV2Materialization mapping = CharacterResourceMappingV2Producer.Materialize(
            package,
            new CharacterPackageResourceMapping
            {
                PackageId = "000003",
                Entries = new List<CharacterPackageResourceMappingEntry>
                {
                    new CharacterPackageResourceMappingEntry
                    {
                        PackageResourceKey = resourceKey,
                        ProjectResourceKey = resourceKey,
                        TypeId = CharacterPackageResourceTypeIds.Animation,
                        SourceFormat = CharacterPackageResourceFormatIds.Fbx,
                        ImportTargetPath = "Assets/MxFrameworkGenerated/CharacterPackages/000003/resources/animations/anim000003_attack1a.fbx"
                    }
                }
            });
        Require(!mapping.HasErrors && mapping.Snapshot.Entries.Count == 1,
            "FBX animation source should materialize one global resource mapping row.");
        Require(mapping.Snapshot.Entries[0].SourceLocator.CachedPath.EndsWith("char.000003.anim.attack1a.anim", StringComparison.Ordinal),
            "Global Resource Mapping must reference the extracted .anim instead of the FBX source.");
    }
}
