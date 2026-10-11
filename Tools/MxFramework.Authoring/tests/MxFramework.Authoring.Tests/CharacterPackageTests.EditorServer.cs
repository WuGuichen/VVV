using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;
using MxFramework.ResourceMapping.Authoring;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    private static void EditorServer_CharacterSave_PreservesIgnoredCharacterActionLibrary()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-character-save-actions-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateCharacterTemplateFixture(tempRoot, corruptResourceCatalog: false);
            object created = MxFramework.Authoring.Cli.EditorServer.CreateCharacterPackage(
                tempRoot,
                new MxFramework.Authoring.Cli.EditorServer.CharacterStudioCreateRequest
                {
                    packageId = "save_actions_slime",
                    displayName = "Save Actions Slime",
                    template = "creature"
                },
                JsonOptions);

            string packageRelative = GetAnonymousPropertyForTest(created, "packageRelative");
            string packagePath = Path.Combine(tempRoot, packageRelative.Replace('/', Path.DirectorySeparatorChar));
            string actionPath = Path.Combine(packagePath, "config", "character_actions.json");
            var actionLibrary = new CharacterActionSourceLibrary();
            actionLibrary.Actions.Add(JsonSerializer.Deserialize<JsonElement>(
                "{\"actionId\":901,\"displayName\":\"Saved Attack\",\"category\":\"BasicAttack\",\"timelineAuthority\":\"CharacterAuthored\",\"tags\":[],\"priority\":0,\"durationFrames\":1,\"requirements\":[],\"phases\":[],\"cancelRules\":[],\"interruptRules\":[]}",
                JsonOptions));
            File.WriteAllText(actionPath, JsonSerializer.Serialize(actionLibrary, JsonOptions));

            CharacterResourcePackage loaded = CharacterPackageCommands.ReadPackage(packagePath, JsonOptions);
            CharacterResourcePackage studioPayload = JsonSerializer.Deserialize<CharacterResourcePackage>(
                JsonSerializer.Serialize(loaded, JsonOptions),
                JsonOptions);
            Require(studioPayload != null && studioPayload.CharacterActionLibrary.Actions.Count == 0,
                "the CharacterStudio package payload should reproduce the JsonIgnore boundary used by the HTTP save contract.");
            studioPayload.ApplicationConfig.AuthoringNote = "camera-only save";

            MxFramework.Authoring.Cli.EditorServer.SaveCharacterStudioPackage(
                tempRoot,
                packageRelative,
                studioPayload,
                JsonOptions,
                MxFramework.Authoring.Cli.EditorServer.ComputeCharacterPackageRevision(packagePath));

            CharacterResourcePackage saved = CharacterPackageCommands.ReadPackage(packagePath, JsonOptions);
            Require(saved.ApplicationConfig.AuthoringNote == "camera-only save",
                "CharacterStudio save should persist ordinary package edits.");
            Require(saved.CharacterActionLibrary.Actions.Exists(action =>
                    CharacterActionSourceLibrary.GetActionId(action) == 901),
                "CharacterStudio save must preserve the file-backed character action library omitted from its package DTO.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_CharacterCreate_CreatesArtSourceAndRejectsDuplicatePackageId()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-character-create-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateCharacterTemplateFixture(tempRoot, corruptResourceCatalog: false);

            object created = MxFramework.Authoring.Cli.EditorServer.CreateCharacterPackage(
                tempRoot,
                new MxFramework.Authoring.Cli.EditorServer.CharacterStudioCreateRequest
                {
                    packageId = "test_slime",
                    displayName = "Test Slime",
                    directoryName = "test-slime",
                    template = "creature"
                },
                JsonOptions);

            string packageRelative = GetAnonymousPropertyForTest(created, "packageRelative");
            string sourceManifestRelative = GetAnonymousPropertyForTest(created, "sourceManifestRelative");
            Require(packageRelative == "Authoring/CharacterPackages/character-test-slime", "character create should return the created package path.");
            Require(sourceManifestRelative == "ArtSource/Characters/test_slime/character_sources.json", "character create should return the canonical ArtSource manifest path.");

            string packagePath = Path.Combine(tempRoot, packageRelative.Replace('/', Path.DirectorySeparatorChar));
            string sourceRoot = Path.Combine(tempRoot, "ArtSource", "Characters", "test_slime");
            Require(Directory.Exists(packagePath), "character create should create the Authoring package.");
            Require(Directory.Exists(sourceRoot), "character create should create the ArtSource root in the same transaction.");
            Require(File.Exists(Path.Combine(sourceRoot, "training_slime.glb")), "character create should copy template-owned source files.");
            string createdActionLibraryPath = Path.Combine(packagePath, "config", "character_actions.json");
            Require(File.Exists(createdActionLibraryPath), "new characters should always receive a formal character action source library.");
            CharacterActionSourceLibrary createdActionLibrary = JsonSerializer.Deserialize<CharacterActionSourceLibrary>(
                File.ReadAllText(createdActionLibraryPath),
                JsonOptions);
            Require(createdActionLibrary != null &&
                createdActionLibrary.SchemaVersion == CharacterActionSourceLibrary.CurrentSchemaVersion,
                "new character action libraries should use the current 0.2 source format.");
            CharacterSourceManifestDocument sourceManifest = JsonSerializer.Deserialize<CharacterSourceManifestDocument>(
                File.ReadAllText(Path.Combine(sourceRoot, "character_sources.json")),
                JsonOptions);
            Require(sourceManifest != null && sourceManifest.PackageId == "test_slime" && sourceManifest.CharacterId == "test_slime", "created source manifest should use the new character identity.");
            Require(sourceManifest.Items.Count == 1 && sourceManifest.Items[0].Target.ResourceKey == "char.test_slime.model.body", "created source manifest should rewrite package-local resource keys.");

            CharacterImportMaterialsStateDocument importState = JsonSerializer.Deserialize<CharacterImportMaterialsStateDocument>(
                File.ReadAllText(Path.Combine(packagePath, "config", "import_materials_state.json")),
                JsonOptions);
            Require(importState != null && importState.SourceManifestRelative == sourceManifestRelative, "created import state should point at the new ArtSource manifest.");
            Require(importState.Items.Count == 1 && importState.Items[0].ResourceKey == "char.test_slime.model.body", "created import state should use rewritten resource keys.");
            Require(string.IsNullOrEmpty(importState.Items[0].LastImportedHash) && string.IsNullOrEmpty(importState.Items[0].LastImportedUtc), "created import state should not inherit template import history.");
            using (JsonDocument audioCueManifest = JsonDocument.Parse(
                       File.ReadAllText(Path.Combine(packagePath, "config", "audio_cue_manifest.json"))))
            {
                Require(
                    audioCueManifest.RootElement.GetProperty("packageId").GetString() == "test_slime",
                    "created audio cue manifest should use the new package identity.");
            }

            bool duplicateRejected = false;
            try
            {
                MxFramework.Authoring.Cli.EditorServer.CreateCharacterPackage(
                    tempRoot,
                    new MxFramework.Authoring.Cli.EditorServer.CharacterStudioCreateRequest
                    {
                        packageId = "test_slime",
                        directoryName = "different-directory",
                        template = "creature"
                    },
                    JsonOptions);
            }
            catch (MxFramework.Authoring.Cli.EditorServer.CharacterPackageCreateConflictException ex)
            {
                duplicateRejected = ex.Code == "CHARACTER_PACKAGE_ID_CONFLICT"
                    && ex.ConflictPackageRelative == packageRelative;
            }

            Require(duplicateRejected, "character create should reject a duplicate packageId even when directoryName differs.");
            Require(!Directory.Exists(Path.Combine(tempRoot, "Authoring", "CharacterPackages", "character-different-directory")), "duplicate packageId rejection should not create another package directory.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_CharacterCreate_RewritePreservesPublicConfigurationReferences()
    {
        var package = new CharacterResourcePackage
        {
            Manifest = new CharacterPackageManifest
            {
                PackageId = "source_character",
                DisplayName = "Source Character"
            },
            ApplicationConfig = new CharacterApplicationAuthoringSummary
            {
                CharacterStableId = "char.source_character",
                BodyProfileStableId = "body.source_character.authoring",
                AttributeProfileStableId = "attr.source_character.base",
                DefaultCombinedWeaponConfigurationId = "public.combination.default",
                CombinedWeaponConfigurationBindings = new List<CharacterCombinedWeaponConfigurationBinding>
                {
                    new CharacterCombinedWeaponConfigurationBinding
                    {
                        CombinedWeaponConfigurationId = "public.combination.default",
                        Overrides = new CharacterCombinedWeaponConfigurationOverrides
                        {
                            AnimationConfigurationId = "public.animation.override",
                            AbilityConfigurationId = "public.ability.override",
                            ReactionConfigurationId = "public.reaction.override"
                        }
                    }
                }
            }
        };
        MethodInfo rewrite = typeof(EditorServer).GetMethod(
            "RewriteCharacterTemplatePackage",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException("RewriteCharacterTemplatePackage");
        rewrite.Invoke(
            null,
            new object[] { package, "created_character", "Created Character", JsonOptions });

        Require(
            package.ApplicationConfig.CharacterStableId == "char.created_character",
            "character create should rewrite character-local stable identity.");
        Require(
            package.ApplicationConfig.DefaultCombinedWeaponConfigurationId
                == "public.combination.default",
            "character create should preserve the public default combined-weapon configuration id.");
        Require(
            package.ApplicationConfig.CombinedWeaponConfigurationBindings.Count == 1
            && package.ApplicationConfig.CombinedWeaponConfigurationBindings[0]
                .CombinedWeaponConfigurationId == "public.combination.default",
            "character create should preserve public combined-weapon bindings.");
        CharacterCombinedWeaponConfigurationOverrides overrides =
            package.ApplicationConfig.CombinedWeaponConfigurationBindings[0].Overrides;
        Require(
            overrides.AnimationConfigurationId == "public.animation.override"
            && overrides.AbilityConfigurationId == "public.ability.override"
            && overrides.ReactionConfigurationId == "public.reaction.override",
            "character create should preserve public configuration override ids.");
    }

    private static void EditorServer_CharacterCreate_RollsBackPackageAndArtSourceOnFailure()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-character-create-rollback-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateCharacterTemplateFixture(tempRoot, corruptResourceCatalog: true);

            bool failed = false;
            try
            {
                MxFramework.Authoring.Cli.EditorServer.CreateCharacterPackage(
                    tempRoot,
                    new MxFramework.Authoring.Cli.EditorServer.CharacterStudioCreateRequest
                    {
                        packageId = "broken_slime",
                        template = "creature"
                    },
                    JsonOptions);
            }
            catch (JsonException)
            {
                failed = true;
            }

            Require(failed, "invalid template content should fail character creation.");
            Require(!Directory.Exists(Path.Combine(tempRoot, "Authoring", "CharacterPackages", "character-broken-slime")), "failed character creation should roll back the Authoring package directory.");
            Require(!Directory.Exists(Path.Combine(tempRoot, "ArtSource", "Characters", "broken_slime")), "failed character creation should roll back the ArtSource directory.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_CharacterDuplicate_RewritesIdentityAndCopiesArtSource()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-character-duplicate-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateCharacterTemplateFixture(tempRoot, corruptResourceCatalog: false);
            object source = MxFramework.Authoring.Cli.EditorServer.CreateCharacterPackage(
                tempRoot,
                new MxFramework.Authoring.Cli.EditorServer.CharacterStudioCreateRequest
                {
                    packageId = "source_slime",
                    displayName = "Source Slime",
                    template = "creature"
                },
                JsonOptions);
            string sourcePackageRelative = GetAnonymousPropertyForTest(source, "packageRelative");
            string sourcePackagePath = Path.Combine(
                tempRoot,
                sourcePackageRelative.Replace('/', Path.DirectorySeparatorChar));
            string sourceActionPath = Path.Combine(sourcePackagePath, "config", "character_actions.json");
            File.WriteAllText(sourceActionPath, JsonSerializer.Serialize(new CharacterActionSourceLibrary
            {
                Actions =
                {
                    JsonSerializer.Deserialize<JsonElement>(
                        "{\"actionId\":901,\"displayName\":\"Source Attack\",\"category\":\"BasicAttack\",\"timelineAuthority\":\"CharacterAuthored\",\"tags\":[],\"priority\":0,\"durationFrames\":1,\"requirements\":[],\"phases\":[],\"cancelRules\":[],\"interruptRules\":[]}",
                        JsonOptions)
                }
            }, JsonOptions));

            object duplicated = MxFramework.Authoring.Cli.EditorServer.DuplicateCharacterPackage(
                tempRoot,
                new MxFramework.Authoring.Cli.EditorServer.CharacterStudioDuplicateRequest
                {
                    sourcePackage = sourcePackageRelative,
                    packageId = "copied_slime",
                    displayName = "Copied Slime"
                },
                JsonOptions);

            string duplicateRelative = GetAnonymousPropertyForTest(duplicated, "packageRelative");
            string duplicatePath = Path.Combine(tempRoot, duplicateRelative.Replace('/', Path.DirectorySeparatorChar));
            CharacterResourcePackage duplicatePackage = CharacterPackageCommands.ReadPackage(duplicatePath, JsonOptions);
            Require(duplicatePackage.Manifest.PackageId == "copied_slime", "duplicated package should use the new packageId.");
            Require(duplicatePackage.Manifest.StableId == "charpkg.copied_slime", "duplicated package should regenerate stable identity.");
            Require(duplicatePackage.ResourceCatalog.Entries.Exists(entry => entry.ResourceKey == "char.copied_slime.model.body"), "duplicated package should rewrite package-local resource keys.");
            Require(duplicatePackage.CharacterActionLibrary.Actions.Exists(action =>
                    CharacterActionSourceLibrary.GetActionId(action) > 0),
                "duplicating a character should retain positive numeric action IDs in the formal action source.");

            string duplicatedSourceRoot = Path.Combine(tempRoot, "ArtSource", "Characters", "copied_slime");
            Require(File.Exists(Path.Combine(duplicatedSourceRoot, "training_slime.glb")), "duplicated character should copy character-owned ArtSource files.");
            CharacterSourceManifestDocument duplicatedManifest = JsonSerializer.Deserialize<CharacterSourceManifestDocument>(
                File.ReadAllText(Path.Combine(duplicatedSourceRoot, "character_sources.json")),
                JsonOptions);
            Require(duplicatedManifest != null && duplicatedManifest.PackageId == "copied_slime", "duplicated ArtSource manifest should use the new package identity.");
            Require(duplicatedManifest.Items[0].Target.ResourceKey == "char.copied_slime.model.body", "duplicated ArtSource manifest should rewrite resource keys.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_CharacterArchive_MovesActiveSourcesAndRetainsGeneratedOutputs()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-character-archive-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateCharacterTemplateFixture(tempRoot, corruptResourceCatalog: false);
            object created = MxFramework.Authoring.Cli.EditorServer.CreateCharacterPackage(
                tempRoot,
                new MxFramework.Authoring.Cli.EditorServer.CharacterStudioCreateRequest
                {
                    packageId = "archive_slime",
                    displayName = "Archive Slime",
                    template = "creature"
                },
                JsonOptions);
            string packageRelative = GetAnonymousPropertyForTest(created, "packageRelative");
            string packagePath = Path.Combine(tempRoot, packageRelative.Replace('/', Path.DirectorySeparatorChar));
            string sourcePath = Path.Combine(tempRoot, "ArtSource", "Characters", "archive_slime");
            string generatedPath = Path.Combine(tempRoot, "Assets", "MxFrameworkGenerated", "CharacterPackages", "archive_slime");
            Directory.CreateDirectory(generatedPath);
            File.WriteAllText(Path.Combine(generatedPath, "retained.txt"), "generated output remains until explicit cleanup");

            object archived = MxFramework.Authoring.Cli.EditorServer.ArchiveCharacterPackage(
                tempRoot,
                new MxFramework.Authoring.Cli.EditorServer.CharacterStudioArchiveRequest
                {
                    package = packageRelative,
                    confirmPackageId = "archive_slime"
                },
                JsonOptions);

            string packageArchiveRelative = GetAnonymousPropertyForTest(archived, "packageArchiveRelative");
            string sourceArchiveRelative = GetAnonymousPropertyForTest(archived, "sourceArchiveRelative");
            Require(!Directory.Exists(packagePath), "archived character should leave the active package root.");
            Require(!Directory.Exists(sourcePath), "archived character should leave the active ArtSource root.");
            Require(Directory.Exists(Path.Combine(tempRoot, packageArchiveRelative.Replace('/', Path.DirectorySeparatorChar))), "archive should retain the Authoring package under the package archive root.");
            Require(File.Exists(Path.Combine(
                    tempRoot,
                    packageArchiveRelative.Replace('/', Path.DirectorySeparatorChar),
                    "config",
                    "character_actions.json")),
                "archive should move the formal character action source with the package.");
            Require(Directory.Exists(Path.Combine(tempRoot, sourceArchiveRelative.Replace('/', Path.DirectorySeparatorChar))), "archive should retain character ArtSource under the source archive root.");
            Require(File.Exists(Path.Combine(generatedPath, "retained.txt")), "archive should not implicitly delete generated Unity outputs.");

            bool templateProtected = false;
            try
            {
                MxFramework.Authoring.Cli.EditorServer.ArchiveCharacterPackage(
                    tempRoot,
                    new MxFramework.Authoring.Cli.EditorServer.CharacterStudioArchiveRequest
                    {
                        package = "Tools/MxFramework.Authoring/samples/character-slime",
                        confirmPackageId = "training_slime"
                    },
                    JsonOptions);
            }
            catch (MxFramework.Authoring.Cli.EditorServer.CharacterPackageManagementConflictException ex)
            {
                templateProtected = ex.Code == "CHARACTER_PACKAGE_PROTECTED";
            }
            Require(templateProtected, "built-in character templates should not be archivable.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_GlobalWeaponLibrary_UsesRevisionAndReferenceProtection()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-global-weapon-library-" + Guid.NewGuid().ToString("N"));
        const string packageRelative = "Tools/MxFramework.Authoring/samples/character-codex-flow-test";
        try
        {
            CopyDirectory(
                FindSamplePath("character-codex-flow-test"),
                Path.Combine(tempRoot, packageRelative.Replace('/', Path.DirectorySeparatorChar)));
            string libraryPath = Path.Combine(tempRoot, "WeaponSource", "global_weapon_library.json");
            Directory.CreateDirectory(Path.GetDirectoryName(libraryPath));
            File.Copy(
                Path.Combine(FindRepoRoot(), "WeaponSource", "global_weapon_library.json"),
                libraryPath);

            string initialRevision = EditorServer.ComputeGlobalWeaponLibraryRevision(tempRoot);
            GlobalWeaponLibrary library = EditorServer.ReadGlobalWeaponLibraryAtRoot(tempRoot, JsonOptions);
            library.SingleWeaponConfigurations.Add(new SingleWeaponConfiguration
            {
                SingleWeaponConfigurationId = "weapon.test_axe",
                DisplayName = "Test Axe",
                Category = "OneHandMelee",
                AllowedSlots = new List<string> { "mainHand", "offHand" }
            });
            EditorServer.SaveGlobalWeaponLibrary(
                tempRoot,
                new EditorServer.GlobalWeaponLibrarySaveRequest
                {
                    library = library,
                    expectedRevision = initialRevision
                },
                JsonOptions);
            GlobalWeaponLibrary saved = EditorServer.ReadGlobalWeaponLibraryAtRoot(tempRoot, JsonOptions);
            Require(saved.SingleWeaponConfigurations.Exists(item =>
                item.SingleWeaponConfigurationId == "weapon.test_axe"),
                "global weapon library save should round-trip a new single-weapon configuration.");

            GlobalAnimationConfiguration animationCopy = JsonSerializer.Deserialize<GlobalAnimationConfiguration>(
                JsonSerializer.Serialize(saved.AnimationConfigurations[0], JsonOptions), JsonOptions);
            animationCopy.AnimationConfigurationId = "animation.test.targeted";
            animationCopy.DisplayName = "Targeted Animation";
            string targetedRevision = EditorServer.ComputeGlobalWeaponLibraryRevision(tempRoot);
            EditorServer.SaveGlobalConfiguration(
                tempRoot,
                new EditorServer.GlobalConfigurationMutationRequest
                {
                    configurationType = "animation",
                    configurationId = animationCopy.AnimationConfigurationId,
                    operation = "create",
                    expectedRevision = targetedRevision,
                    configuration = JsonSerializer.SerializeToElement(animationCopy, JsonOptions)
                },
                JsonOptions);
            GlobalWeaponLibrary targetedSaved = EditorServer.ReadGlobalWeaponLibraryAtRoot(tempRoot, JsonOptions);
            Require(targetedSaved.AnimationConfigurations.Exists(item =>
                item.AnimationConfigurationId == animationCopy.AnimationConfigurationId),
                "targeted configuration save should create one global animation without replacing other types.");

            GlobalReactionConfiguration invalidReaction = new GlobalReactionConfiguration
            {
                ReactionConfigurationId = "reaction.invalid",
                DefaultSlotId = "missing",
                Slots = new List<GlobalReactionSlotConfiguration>
                {
                    new GlobalReactionSlotConfiguration
                    {
                        SlotId = "hit",
                        ActionRole = string.Empty,
                        MinImpactForce = 10,
                        MaxImpactForce = 2
                    }
                }
            };
            EditorServer.GlobalConfigurationValidationResult invalidResult =
                EditorServer.ValidateGlobalConfiguration(
                    tempRoot,
                    new EditorServer.GlobalConfigurationMutationRequest
                    {
                        configurationType = "reaction",
                        configuration = JsonSerializer.SerializeToElement(invalidReaction, JsonOptions)
                    },
                    JsonOptions);
            Require(!invalidResult.Success && invalidResult.Issues.Exists(issue =>
                    issue.Code == "GLOBAL_REACTION_ACTION_ROLE_REQUIRED") &&
                invalidResult.Issues.Exists(issue => issue.Code == "GLOBAL_REACTION_IMPACT_RANGE_INVALID") &&
                invalidResult.Issues.Exists(issue => issue.Code == "GLOBAL_REACTION_DEFAULT_SLOT_NOT_FOUND"),
                "targeted validation should report stable structured reaction diagnostics without writing.");

            bool stableRenameRejected = false;
            GlobalAnimationConfiguration renamed = JsonSerializer.Deserialize<GlobalAnimationConfiguration>(
                JsonSerializer.Serialize(animationCopy, JsonOptions), JsonOptions);
            renamed.AnimationConfigurationId = "animation.test.renamed";
            try
            {
                EditorServer.SaveGlobalConfiguration(
                    tempRoot,
                    new EditorServer.GlobalConfigurationMutationRequest
                    {
                        configurationType = "animation",
                        configurationId = animationCopy.AnimationConfigurationId,
                        operation = "update",
                        expectedRevision = EditorServer.ComputeGlobalWeaponLibraryRevision(tempRoot),
                        configuration = JsonSerializer.SerializeToElement(renamed, JsonOptions)
                    },
                    JsonOptions);
            }
            catch (ArgumentException ex)
            {
                stableRenameRejected = ex.Message.Contains("cannot be changed", StringComparison.Ordinal);
            }
            Require(stableRenameRejected,
                "targeted configuration update should reject stable ID mutation.");

            EditorServer.DeleteGlobalConfiguration(
                tempRoot,
                new EditorServer.GlobalConfigurationDeleteRequest
                {
                    configurationType = "animation",
                    configurationId = animationCopy.AnimationConfigurationId,
                    expectedRevision = EditorServer.ComputeGlobalWeaponLibraryRevision(tempRoot)
                },
                JsonOptions);
            Require(!EditorServer.ReadGlobalWeaponLibraryAtRoot(tempRoot, JsonOptions)
                    .AnimationConfigurations.Exists(item =>
                        item.AnimationConfigurationId == animationCopy.AnimationConfigurationId),
                "targeted delete should remove an unreferenced configuration.");

            const string secondPackageRelative = "Tools/MxFramework.Authoring/samples/character-codex-flow-test-copy";
            CopyDirectory(
                FindSamplePath("character-codex-flow-test"),
                Path.Combine(tempRoot, secondPackageRelative.Replace('/', Path.DirectorySeparatorChar)));
            EditorServer.GlobalWeaponLibraryReferenceIndex referenceIndex =
                EditorServer.BuildGlobalConfigurationReferenceIndex(
                    tempRoot,
                    "singleWeapon",
                    JsonOptions);
            EditorServer.GlobalWeaponLibraryReferenceIndexEntry axeIndex = referenceIndex.Entries.Find(item =>
                item.ConfigurationId == "weapon.test_axe");
            EditorServer.GlobalWeaponLibraryReferenceIndexEntry swordIndex = referenceIndex.Entries.Find(item =>
                item.ConfigurationId == "weapon.000001.main");
            Require(axeIndex != null && axeIndex.ReferenceCount == 0 && axeIndex.CharacterCount == 0 &&
                axeIndex.CombinationCount == 0,
                "global weapon reference index should include unreferenced library records with zero counts.");
            Require(swordIndex != null && swordIndex.CharacterCount == 2 && swordIndex.CombinationCount == 2,
                "global weapon reference index should aggregate distinct characters and combinations in one scan.");
            Require(swordIndex.ReferenceCount == swordIndex.CombinationCount && swordIndex.References.TrueForAll(reference =>
                    reference.SlotId == "mainHand"),
                "the retained sword/empty combinations should reference sword only from mainHand.");
            for (int index = 1; index < referenceIndex.Entries.Count; index++)
                Require(string.CompareOrdinal(
                    referenceIndex.Entries[index - 1].ConfigurationId,
                    referenceIndex.Entries[index].ConfigurationId) <= 0,
                    "global weapon reference index should use stable configuration-id ordering.");

            EditorServer.GlobalWeaponLibraryReferenceIndex combinedIndex =
                EditorServer.BuildGlobalConfigurationReferenceIndex(
                    tempRoot,
                    "combinedWeapon",
                    JsonOptions);
            EditorServer.GlobalWeaponLibraryReferenceIndexEntry publicSwordIndex = combinedIndex.Entries.Find(item =>
                item.ConfigurationId == "codex_flow_test.sword_empty");
            Require(publicSwordIndex != null && publicSwordIndex.CharacterCount == 2 &&
                publicSwordIndex.ReferenceCount == 2,
                "public combined reference index should count character bindings rather than transient resolved copies.");

            GlobalWeaponLibrary combinedPayload = EditorServer.ReadGlobalWeaponLibraryAtRoot(tempRoot, JsonOptions);
            string combinedRevision = EditorServer.ComputeGlobalWeaponLibraryRevision(tempRoot);
            combinedPayload.CombinedWeaponConfigurations.RemoveAll(item =>
                item.CombinedWeaponConfigurationId == "codex_flow_test.sword_empty");
            bool combinedDeleteRejected = false;
            try
            {
                EditorServer.SaveGlobalWeaponLibrary(
                    tempRoot,
                    new EditorServer.GlobalWeaponLibrarySaveRequest
                    {
                        library = combinedPayload,
                        expectedRevision = combinedRevision
                    },
                    JsonOptions);
            }
            catch (EditorServer.GlobalWeaponLibraryInUseException ex)
            {
                combinedDeleteRejected = ex.ConfigurationType == "combinedWeapon" &&
                    ex.ConfigurationId == "codex_flow_test.sword_empty" &&
                    ex.References.Exists(reference => reference.PackageRelative == packageRelative);
            }
            Require(combinedDeleteRejected,
                "public combined deletion should be blocked while characters still bind to it.");

            string deleteRevision = EditorServer.ComputeGlobalWeaponLibraryRevision(tempRoot);
            saved.SingleWeaponConfigurations.RemoveAll(item => item.SingleWeaponConfigurationId == "weapon.000001.main");
            bool referencedDeleteRejected = false;
            try
            {
                EditorServer.SaveGlobalWeaponLibrary(
                    tempRoot,
                    new EditorServer.GlobalWeaponLibrarySaveRequest
                    {
                        library = saved,
                        expectedRevision = deleteRevision
                    },
                    JsonOptions);
            }
            catch (EditorServer.GlobalWeaponLibraryInUseException ex)
            {
                referencedDeleteRejected = ex.SingleWeaponConfigurationId == "weapon.000001.main"
                    && ex.References.Exists(reference =>
                        reference.PackageRelative == packageRelative
                        && !string.IsNullOrWhiteSpace(reference.CombinedWeaponConfigurationId));
            }
            Require(referencedDeleteRejected,
                "global single-weapon deletion should be blocked while a character combination references it.");

            GlobalWeaponLibrary behaviorPayload = EditorServer.ReadGlobalWeaponLibraryAtRoot(tempRoot, JsonOptions);
            string behaviorRevision = EditorServer.ComputeGlobalWeaponLibraryRevision(tempRoot);
            int animationIndex = behaviorPayload.AnimationConfigurations.FindIndex(item =>
                item.AnimationConfigurationId == "animation.combat_girl_sword.profile.sword");
            Require(animationIndex >= 0, "referenced animation configuration should exist in test library.");
            string animationId = behaviorPayload.AnimationConfigurations[animationIndex].AnimationConfigurationId;
            behaviorPayload.AnimationConfigurations.RemoveAt(animationIndex);
            bool behaviorDeleteRejected = false;
            try
            {
                EditorServer.SaveGlobalWeaponLibrary(
                    tempRoot,
                    new EditorServer.GlobalWeaponLibrarySaveRequest
                    {
                        library = behaviorPayload,
                        expectedRevision = behaviorRevision
                    },
                    JsonOptions);
            }
            catch (EditorServer.GlobalWeaponLibraryInUseException ex)
            {
                behaviorDeleteRejected = ex.ConfigurationType == "animation"
                    && ex.ConfigurationId == animationId
                    && ex.References.Exists(reference =>
                        reference.PackageRelative == packageRelative
                        && !string.IsNullOrWhiteSpace(reference.CombinedWeaponConfigurationId));
            }
            Require(behaviorDeleteRejected,
                "global animation deletion should be blocked while character combinations share it.");

            GlobalWeaponLibrary stalePayload = EditorServer.ReadGlobalWeaponLibraryAtRoot(tempRoot, JsonOptions);
            string staleRevision = EditorServer.ComputeGlobalWeaponLibraryRevision(tempRoot);
            File.AppendAllText(libraryPath, Environment.NewLine);
            bool staleRejected = false;
            try
            {
                EditorServer.SaveGlobalWeaponLibrary(
                    tempRoot,
                    new EditorServer.GlobalWeaponLibrarySaveRequest
                    {
                        library = stalePayload,
                        expectedRevision = staleRevision
                    },
                    JsonOptions);
            }
            catch (EditorServer.GlobalWeaponLibraryWriteConflictException)
            {
                staleRejected = true;
            }
            Require(staleRejected, "global weapon library save should reject a stale expectedRevision.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void CreateCharacterTemplateFixture(string tempRoot, bool corruptResourceCatalog)
    {
        CopyDirectory(
            Path.Combine(FindRepoRoot(), "CharacterActionSource"),
            Path.Combine(tempRoot, "CharacterActionSource"));
        string templatePath = Path.Combine(tempRoot, "Tools", "MxFramework.Authoring", "samples", "character-slime");
        CopyDirectory(FindSamplePath("character-slime"), templatePath);
        if (corruptResourceCatalog)
            File.WriteAllText(Path.Combine(templatePath, "resource_catalog.json"), "{ invalid json");

        string sourceRoot = Path.Combine(tempRoot, "ArtSource", "Characters", "training_slime");
        Directory.CreateDirectory(sourceRoot);
        File.Copy(
            Path.Combine(FindSamplePath("character-slime"), "resources", "models", "training_slime.glb"),
            Path.Combine(sourceRoot, "training_slime.glb"));
        File.WriteAllText(Path.Combine(sourceRoot, "character_sources.json"), JsonSerializer.Serialize(new CharacterSourceManifestDocument
        {
            CharacterId = "training_slime",
            PackageId = "training_slime",
            Items = new List<CharacterSourceManifestItem>
            {
                new CharacterSourceManifestItem
                {
                    Id = "body-model",
                    Label = "Body model",
                    PresetId = "characterModel",
                    BindTarget = "body",
                    Required = true,
                    Source = new CharacterSourceReference
                    {
                        Kind = "file",
                        Scope = "package",
                        Path = "training_slime.glb"
                    },
                    Target = new CharacterSourceTarget
                    {
                        ResourceKey = "char.training_slime.model.body",
                        BindTarget = "body"
                    }
                }
            }
        }, JsonOptions));

        Directory.CreateDirectory(Path.Combine(templatePath, "config"));
        File.WriteAllText(
            Path.Combine(templatePath, "config", "audio_cue_manifest.json"),
            JsonSerializer.Serialize(new MxFramework.Authoring.Cli.EditorServer.AudioPackageCueSubsetDocument
            {
                PackageId = "training_slime"
            }, JsonOptions));
        File.WriteAllText(Path.Combine(templatePath, "config", "import_materials_state.json"), JsonSerializer.Serialize(new CharacterImportMaterialsStateDocument
        {
            SourceManifestRelative = "ArtSource/Characters/training_slime/character_sources.json",
            Items = new List<CharacterImportMaterialItemState>
            {
                new CharacterImportMaterialItemState
                {
                    Id = "body-model",
                    ResourceKey = "char.training_slime.model.body",
                    LastImportedHash = "sha256:template-history",
                    LastImportedUtc = "2026-01-01T00:00:00Z"
                }
            }
        }, JsonOptions));
    }

    private static void EditorServer_AnimationPackageApi_IsFileBackedAndValidatesShallowDraft()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-animation-api-" + Guid.NewGuid().ToString("N"));
        string packagePath = Path.Combine(tempRoot, "Tools", "MxFramework.Authoring", "samples", "animation-test");
        Directory.CreateDirectory(Path.Combine(packagePath, "config"));
        File.WriteAllText(Path.Combine(packagePath, "manifest.json"), JsonSerializer.Serialize(new CharacterPackageManifest
        {
            PackageId = "animation_test",
            StableId = "char.animation_test",
            DisplayName = "Animation Test"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "resource_catalog.json"), JsonSerializer.Serialize(new CharacterPackageResourceCatalog(), JsonOptions));

        try
        {
            string packageRelative = "Tools/MxFramework.Authoring/samples/animation-test";
            List<object> packages = MxFramework.Authoring.Cli.EditorServer.ListAnimationPackages(tempRoot, packageRelative, JsonOptions);
            Require(packages.Count == 1, "animation package list should include character package scopes.");
            Require(GetAnonymousPropertyForTest(packages[0], "packageId") == "animation.animation_test", "animation package list should seed package id from character manifest.");
            Require(GetAnonymousPropertyForTest(packages[0], "characterPackageId") == "animation_test", "animation package list should expose the source character package id.");

            object initialState = MxFramework.Authoring.Cli.EditorServer.ReadAnimationPackageState(tempRoot, "animation_test", packageRelative, JsonOptions);
            Require(GetAnonymousPropertyForTest(initialState, "documentRelative").EndsWith("config/animation_authoring.json", StringComparison.Ordinal), "animation load should resolve id to a package-scoped document.");

            var draft = new AnimationAuthoringPackage
            {
                PackageId = "animation.animation_test",
                StableId = "anim.animation_test",
                DisplayName = "Animation Test"
            };
            draft.Sets.Add(new AnimationAuthoringSet
            {
                SetId = "set.base",
                Groups = new List<AnimationGroupAuthoring>
                {
                    new AnimationGroupAuthoring
                    {
                        GroupId = "group.locomotion",
                        Clips = new List<AnimationClipMappingAuthoring>
                        {
                            new AnimationClipMappingAuthoring { ClipId = "idle" },
                            new AnimationClipMappingAuthoring { ClipId = "idle", SourceSelection = new AuthoringResourceSelectionRef { ResourceStableId = "unity.project.idle" } }
                        }
                    }
                }
            });

            CharacterAuthoringValidationReport validation = MxFramework.Authoring.Cli.EditorServer.ValidateAnimationPackage(draft);
            Require(validation.Issues.Exists(issue => issue.Code == "ANIM_DUPLICATE_CLIP_ID"), "animation validation should report duplicate clip ids.");
            Require(validation.Issues.Exists(issue => issue.Code == "ANIM_MISSING_SOURCE_SELECTION"), "animation validation should report missing source selections.");

            MxFramework.Authoring.Cli.EditorServer.SaveAnimationPackage(tempRoot, packageRelative, packageRelative, draft, JsonOptions);
            string documentPath = Path.Combine(packagePath, "config", "animation_authoring.json");
            Require(File.Exists(documentPath), "animation save should write the package-scoped animation authoring document.");
            AnimationAuthoringPackage saved = JsonSerializer.Deserialize<AnimationAuthoringPackage>(File.ReadAllText(documentPath), JsonOptions);
            Require(saved != null && saved.PackageId == "animation.animation_test", "animation save should persist the draft package.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_AnimationPreview_IsCompilerBackedAndResolvesResources()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-animation-preview-" + Guid.NewGuid().ToString("N"));
        string packageRelative = "Tools/MxFramework.Authoring/samples/animation-preview-test";
        string packagePath = Path.Combine(tempRoot, "Tools", "MxFramework.Authoring", "samples", "animation-preview-test");
        Directory.CreateDirectory(Path.Combine(packagePath, "config"));
        Directory.CreateDirectory(Path.Combine(packagePath, "resources", "animations"));
            File.WriteAllText(Path.Combine(packagePath, "resources", "animations", "idle.anim"), "preview animation placeholder");
        File.WriteAllText(Path.Combine(packagePath, "manifest.json"), JsonSerializer.Serialize(new CharacterPackageManifest
        {
            PackageId = "animation_preview_test",
            StableId = "char.animation_preview_test",
            DisplayName = "Animation Preview Test"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "resource_catalog.json"), JsonSerializer.Serialize(new CharacterPackageResourceCatalog
        {
            Entries = new List<CharacterPackageResourceEntry>
            {
                new CharacterPackageResourceEntry
                {
                    ResourceKey = "runtime.anim.idle",
                    StableId = "stable.anim.idle",
                    TypeId = CharacterPackageResourceTypeIds.Animation,
                    Usage = AnimationAuthoringResourceUsages.AnimationClip,
                    RelativePath = "resources/animations/idle.anim",
                    Hash = "sha256:idle"
                }
            }
        }, JsonOptions));

        try
        {
            var sourceSelection = new AuthoringResourceSelectionRef
            {
                ResourceStableId = "stable.anim.idle",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                ExpectedKind = CharacterPackageResourceTypeIds.Animation,
                ExpectedUsage = AnimationAuthoringResourceUsages.AnimationClip,
                RuntimeResourceKey = "runtime.anim.idle"
            };
            var draft = new AnimationAuthoringPackage
            {
                PackageId = "animation.preview",
                StableId = "animation.preview",
                DisplayName = "Animation Preview"
            };
            draft.Sets.Add(new AnimationAuthoringSet
            {
                SetId = "set.base",
                DisplayName = "Base",
                DefaultClipId = "idle",
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
                                RuntimeResourceKey = "runtime.anim.idle",
                                SourceSelection = sourceSelection,
                                Loop = true
                            }
                        }
                    }
                },
                Warmup = new AnimationWarmupAuthoring
                {
                    RequiredClipIds = new List<string> { "idle" }
                }
            });

            object preview = MxFramework.Authoring.Cli.EditorServer.ReadAnimationPreview(tempRoot, packageRelative, packageRelative, draft, JsonOptions);
            using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(preview, JsonOptions));
            JsonElement root = document.RootElement;
            Require(root.GetProperty("serviceStatus").GetString() == "ready", "animation preview endpoint should report ready service status.");
            Require(root.GetProperty("animationSetDefinition").GetProperty("sets").GetArrayLength() == 1, "animation preview should expose compiled animation set definition.");
            Require(root.GetProperty("animationClipRegistry").GetProperty("clips").GetArrayLength() == 1, "animation preview should expose compiled clip registry.");
            JsonElement animationClip = root.GetProperty("previewResources").GetProperty("animationClips")[0];
            Require(animationClip.GetProperty("runtimeResourceKey").GetString() == "runtime.anim.idle", "animation preview should map clip ids to runtime resource keys.");
            Require(animationClip.GetProperty("loop").GetBoolean(), "animation preview should expose authoring loop metadata for the player.");
            Require(Math.Abs(animationClip.GetProperty("speed").GetSingle() - 1f) < 0.001f, "animation preview should expose authoring speed metadata for the player.");
            Require(animationClip.GetProperty("rootMotionPolicy").GetString() == "Ignore", "animation preview should expose root motion policy metadata for the player.");
            JsonElement resource = animationClip.GetProperty("resource");
            Require(resource.GetProperty("resourceKey").GetString() == "runtime.anim.idle", "animation preview should resolve runtime resource metadata.");
            Require(resource.GetProperty("url").GetString().EndsWith("/resources/animations/idle.anim", StringComparison.Ordinal), "animation preview should expose a static resource URL for the viewport.");
            Require(resource.GetProperty("exists").GetBoolean(), "animation preview should mark existing preview resource files.");
            Require(root.GetProperty("animationValidationReport").GetProperty("hasBlockingIssues").GetBoolean() == false, "valid animation preview draft should compile without blocking diagnostics.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_CharacterReadinessApi_ReportsDeliveryStagesAndDeferredAbility()
    {
        string root = FindRepoRoot();
        const string packageRelative = "Tools/MxFramework.Authoring/samples/character-codex-flow-test";

        CharacterAuthoringReadinessSnapshot snapshot =
            MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(root, packageRelative, packageRelative, JsonOptions);

        Require(snapshot != null, "readiness snapshot should be returned.");
        Require(snapshot.PackageRelative == packageRelative, "readiness should preserve package relative path.");
        Require(snapshot.PackageId == "codex_flow_test", "readiness should expose package id.");
        Require(snapshot.Stages.Count >= 7, "readiness should expose the character delivery stages.");
        Require(snapshot.Stages.Exists(stage => stage.Id == "CharacterPackage"), "readiness should include CharacterPackage stage.");
        Require(snapshot.Stages.Exists(stage => stage.Id == "ResourceBinding"), "readiness should include ResourceBinding stage.");
        Require(snapshot.Stages.Exists(stage => stage.Id == "AnimationBinding"), "readiness should include AnimationBinding stage.");
        Require(snapshot.Stages.Exists(stage => stage.Id == "ActionTimeline"), "readiness should include ActionTimeline stage.");
        Require(snapshot.Stages.Exists(stage => stage.Id == "UnityImport"), "readiness should include UnityImport stage.");
        Require(snapshot.Stages.Exists(stage => stage.Id == "RuntimeValidation"), "readiness should include RuntimeValidation stage.");

        Require(!snapshot.Stages.Exists(stage => stage.Id == "AbilityRuntimeBinding"),
            "frozen experimental ability compatibility should not appear in normal readiness stages.");
        Require(snapshot.ReportRefs.Exists(report => report.Id == "unityImportReport"), "readiness should include Unity import report reference.");

        string json = JsonSerializer.Serialize(snapshot, JsonOptions);
        Require(json.Contains("\"overallStatus\""), "readiness JSON should use camelCase field names.");
        Require(!json.Contains("\"AbilityRuntimeBinding\""), "normal readiness JSON should hide frozen ability compatibility.");
    }

    private static void EditorServer_CharacterReadinessApi_DeduplicatesUpstreamValidationIssues()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-character-readiness-dedupe-" + Guid.NewGuid().ToString("N"));
        const string packageRelative = "Tools/MxFramework.Authoring/samples/character-codex-flow-test";
        try
        {
            string packagePath = Path.Combine(tempRoot, packageRelative.Replace('/', Path.DirectorySeparatorChar));
            CopyDirectory(FindSamplePath("character-codex-flow-test"), packagePath);
            CopyTestFile(
                Path.Combine(FindRepoRoot(), "WeaponSource", "global_weapon_library.json"),
                Path.Combine(tempRoot, "WeaponSource", "global_weapon_library.json"));

            string applicationPath = Path.Combine(packagePath, "config", "character_application.json");
            CharacterApplicationAuthoringSummary application = JsonSerializer.Deserialize<CharacterApplicationAuthoringSummary>(
                File.ReadAllText(applicationPath),
                JsonOptions) ?? new CharacterApplicationAuthoringSummary();
            application.CombinedWeaponConfigurations = new List<CombinedWeaponConfigurationAuthoringSummary>();
            application.CombinedWeaponConfigurationBindings = new List<CharacterCombinedWeaponConfigurationBinding>();
            application.DefaultCombinedWeaponConfigurationId = string.Empty;
            File.WriteAllText(applicationPath, JsonSerializer.Serialize(application, JsonOptions));

            CharacterAuthoringReadinessSnapshot snapshot =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions);

            Require(snapshot.Blockers.FindAll(issue =>
                    issue.Code == CharacterAuthoringValidationCodes.CombinedWeaponConfigurationMissing).Count == 1,
                "readiness should count an upstream missing combination once even when downstream stages repeat it: " +
                JsonSerializer.Serialize(snapshot.Blockers, JsonOptions));
            Require(snapshot.Stages.TrueForAll(stage => stage.Blockers.FindAll(issue =>
                    issue.Code == CharacterAuthoringValidationCodes.CombinedWeaponConfigurationMissing).Count <= 1),
                "each readiness stage should render a repeated upstream missing-combination issue at most once.");
            Require(!snapshot.Blockers.Exists(issue =>
                    issue.Code == CharacterAuthoringValidationCodes.CombinedWeaponConfigurationDefaultInvalid),
                "an empty combination list should not also emit a dependent invalid-default diagnostic.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_CharacterReadinessApi_UsesPersistentUnityImportReceiptEvidence()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-character-readiness-receipt-" + Guid.NewGuid().ToString("N"));
        const string packageRelative = "Tools/MxFramework.Authoring/samples/character-codex-flow-test";
        const string generatedRootRelative = "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test";
        const string receiptRelative = generatedRootRelative + "/package_cache/unity_editor_import_receipt.json";
        const string prefabRelative = generatedRootRelative + "/prefabs/codex_flow_test_character_preview.prefab";
        const string runtimePrefabRelative = generatedRootRelative + "/runtime_resources/Resources/MxFrameworkGenerated/CharacterPackages/codex_flow_test/prefabs/codex_flow_test_character_preview.prefab";
        const string avatarRelative = generatedRootRelative + "/resources/models/character_00.fbx";
        const string materialRelative = generatedRootRelative + "/materials/body.mat";
        const string profileRelative = "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test/character_action_profile.json";
        const string actionSetRelative = "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test/character_action_sets.json";
        const string importReportRelative = generatedRootRelative + "/package_cache/import_report.json";

        try
        {
            CopyDirectory(
                FindSamplePath("character-codex-flow-test"),
                Path.Combine(tempRoot, packageRelative.Replace('/', Path.DirectorySeparatorChar)));
            CopyTestFile(
                Path.Combine(FindRepoRoot(), "WeaponSource", "global_weapon_library.json"),
                Path.Combine(tempRoot, "WeaponSource", "global_weapon_library.json"));
            CopyDirectory(
                Path.Combine(FindRepoRoot(), "CharacterActionSource"),
                Path.Combine(tempRoot, "CharacterActionSource"));

            WriteTestArtifact(tempRoot, prefabRelative, "prefab");
            WriteTestArtifact(tempRoot, runtimePrefabRelative, "runtime prefab");
            WriteTestArtifact(tempRoot, avatarRelative, "avatar");
            WriteTestArtifact(tempRoot, materialRelative, "material");
            WriteTestArtifact(tempRoot, profileRelative, "{}");
            WriteTestArtifact(tempRoot, actionSetRelative, "{}");
            WriteTestArtifact(tempRoot, generatedRootRelative + "/config/unity_resource_catalog.json", "{}");
            WriteTestArtifact(tempRoot, generatedRootRelative + "/generated/weapon_trace_bake_catalog.json", "{}");
            var socketEvidence = WriteEmptySocketBakePairForReadiness(tempRoot, "codex_flow_test");
            WriteTestArtifact(tempRoot, generatedRootRelative + "/generated/character_hurtbox_bake_catalog.json", "{}");
            WriteTestArtifact(
                tempRoot,
                importReportRelative,
                "{\"status\":\"Ready\",\"sourcePackageHash\":\"sha256:stale\",\"canWriteToUnityProject\":true,\"canSpawnAfterImport\":true,\"errorCount\":0,\"conflictCount\":0,\"operations\":[]}");

            CharacterAuthoringReadinessSnapshot initial =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions);
            CharacterAuthoringReadinessStage initialUnity = initial.Stages.Find(item => item.Id == "UnityImport");
            Require(initialUnity != null && initialUnity.Status == "Deferred",
                "missing session and receipt should defer Unity import rather than block character content.");
            CharacterAuthoringReadinessStage initialRuntime = initial.Stages.Find(item => item.Id == "RuntimeValidation");
            Require(initialRuntime != null && initialRuntime.Status == "Deferred",
                "runtime validation should defer while Unity acceptance evidence is missing.");

            string sourceHash = initialUnity.Metrics["currentSourcePackageHash"];
            string generatedConfigHash = initialUnity.Metrics["currentGeneratedConfigHash"];
            string geometryBindingHash = initialUnity.Metrics["currentGeometryBindingHash"];
            string resourceMappingHash = initialUnity.Metrics["currentResourceMappingHash"];
            string writePlanHash = initialUnity.Metrics["currentWritePlanHash"];
            Require(!string.IsNullOrWhiteSpace(sourceHash) &&
                    !string.IsNullOrWhiteSpace(generatedConfigHash) &&
                    !string.IsNullOrWhiteSpace(geometryBindingHash) &&
                    !string.IsNullOrWhiteSpace(resourceMappingHash) &&
                    !string.IsNullOrWhiteSpace(writePlanHash),
                "readiness should expose all current compiler hashes used by the receipt gate.");

            WriteTestArtifact(
                tempRoot,
                importReportRelative,
                JsonSerializer.Serialize(new
                {
                    packageId = "codex_flow_test",
                    status = "Ready",
                    sourcePackageHash = sourceHash,
                    generatedConfigHash,
                    geometryBindingHash,
                    resourceMappingHash,
                    writePlanHash,
                    canWriteToUnityProject = true,
                    canSpawnAfterImport = true,
                    errorCount = 0,
                    conflictCount = 0,
                    operations = Array.Empty<object>()
                }, JsonOptions));

            MxFramework.Authoring.Cli.EditorServer.CharacterUnityEditorImportReceipt receipt =
                CreateUnityImportReceipt(
                    sourceHash,
                    generatedConfigHash,
                    geometryBindingHash,
                    resourceMappingHash,
                    writePlanHash,
                    prefabRelative,
                    runtimePrefabRelative,
                    avatarRelative,
                    materialRelative,
                    profileRelative,
                    actionSetRelative);
            receipt.socketBake.indexHash = socketEvidence.IndexHash;
            receipt.socketBake.binaryHash = socketEvidence.BinaryHash;
            receipt.socketBake.binarySize = socketEvidence.BinarySize;
            WriteTestArtifact(tempRoot, receiptRelative, JsonSerializer.Serialize(receipt, JsonOptions));

            CharacterAuthoringReadinessSnapshot accepted =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions);
            CharacterAuthoringReadinessStage acceptedUnity = accepted.Stages.Find(item => item.Id == "UnityImport");
            Require(acceptedUnity != null && acceptedUnity.Status == "Ready",
                "a current deterministic receipt should keep Unity import ready without Temp session status.");
            Require(acceptedUnity.Metrics["evidenceSource"] == "persistentReceipt",
                "readiness should identify persistent receipt evidence.");
            Require(acceptedUnity.Metrics["receiptStatus"] == "Current",
                "readiness should mark a matching receipt current.");

            string socketBinaryFile = Path.Combine(tempRoot, generatedRootRelative, "generated/character_socket_bake_catalog.bytes");
            string socketIndexFile = Path.Combine(tempRoot, generatedRootRelative, "generated/character_socket_bake_catalog.json");
            byte[] originalBinary = File.ReadAllBytes(socketBinaryFile);
            string originalIndex = File.ReadAllText(socketIndexFile);
            byte[] tamperedBinary = (byte[])originalBinary.Clone();
            tamperedBinary[tamperedBinary.Length - 1] ^= 1;
            File.WriteAllBytes(socketBinaryFile, tamperedBinary);
            CharacterAuthoringReadinessStage changedBinaryUnity =
                EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(changedBinaryUnity.Status == "Blocked"
                    && changedBinaryUnity.Blockers.Exists(item => item.Code == "UNITY_SOCKET_BAKE_ARTIFACT_MISMATCH"),
                "a same-size modified socket payload must invalidate persistent receipt evidence.");
            File.WriteAllBytes(socketBinaryFile, originalBinary);
            File.WriteAllText(socketIndexFile, originalIndex + " ");
            CharacterAuthoringReadinessStage changedIndexUnity =
                EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(changedIndexUnity.Status == "Blocked", "changed index bytes must invalidate persistent evidence.");
            File.WriteAllText(socketIndexFile, originalIndex);

            receipt.format = "mx.characterUnityEditorImportReceipt.v1";
            receipt.schemaVersion = "1.0";
            receipt.avatar.rigType = string.Empty;
            receipt.avatar.rigStatus = string.Empty;
            receipt.avatar.humanoidStatus = "ValidHuman";
            receipt.materials = Array.Empty<MxFramework.Authoring.Cli.EditorServer.CharacterUnityEditorImportReceiptMaterial>();
            WriteTestArtifact(tempRoot, receiptRelative, JsonSerializer.Serialize(receipt, JsonOptions));
            CharacterAuthoringReadinessStage acceptedLegacyUnity =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(acceptedLegacyUnity != null && acceptedLegacyUnity.Status == "Deferred" &&
                    acceptedLegacyUnity.Metrics["receiptStatus"] == "LegacyNeedsAbstractBoneReimport",
                "legacy Humanoid v1 receipt evidence should remain readable but require reimport for abstract-bone evidence.");

            receipt.format = "mx.characterUnityEditorImportReceipt.v2";
            receipt.schemaVersion = "2.0";
            WriteTestArtifact(tempRoot, receiptRelative, JsonSerializer.Serialize(receipt, JsonOptions));
            CharacterAuthoringReadinessStage legacyV2Unity =
                EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(legacyV2Unity.Status == "Deferred"
                    && legacyV2Unity.Metrics["receiptStatus"] == "LegacyNeedsSocketBakeReimport",
                "v2 receipt must require a real v3 reimport.");
            receipt.format = "mx.characterUnityEditorImportReceipt.v3";
            receipt.schemaVersion = "3.0";
            receipt.avatar.rigType = "Humanoid";
            receipt.avatar.rigStatus = "ValidHuman";
            receipt.materials = new[]
            {
                new MxFramework.Authoring.Cli.EditorServer.CharacterUnityEditorImportReceiptMaterial
                {
                    status = "Assigned",
                    materialPath = materialRelative,
                    textureResourceKey = "char.codex_flow_test.texture.body_albedo",
                    sourceMaterialName = "Body",
                    assignedSlotCount = 1
                }
            };
            WriteTestArtifact(tempRoot, receiptRelative, JsonSerializer.Serialize(receipt, JsonOptions));

            WriteTestArtifact(
                tempRoot,
                importReportRelative,
                JsonSerializer.Serialize(new
                {
                    packageId = "codex_flow_test",
                    status = "WarningOnly",
                    sourcePackageHash = sourceHash,
                    generatedConfigHash,
                    geometryBindingHash,
                    resourceMappingHash,
                    writePlanHash,
                    canWriteToUnityProject = true,
                    canSpawnAfterImport = true,
                    errorCount = 0,
                    conflictCount = 0,
                    operations = Array.Empty<object>()
                }, JsonOptions));
            CharacterAuthoringReadinessStage warningOnlyUnity =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(warningOnlyUnity != null && warningOnlyUnity.Status == "Ready" &&
                    warningOnlyUnity.Metrics["importReportStatus"] == "WarningOnly",
                "a current warning-only report with a spawnable artifact should keep Unity import ready.");

            WriteTestArtifact(
                tempRoot,
                importReportRelative,
                JsonSerializer.Serialize(new
                {
                    packageId = "codex_flow_test",
                    status = "Failed",
                    sourcePackageHash = sourceHash,
                    generatedConfigHash = "sha256:stale-generated",
                    geometryBindingHash,
                    resourceMappingHash,
                    writePlanHash,
                    canWriteToUnityProject = false,
                    canSpawnAfterImport = false,
                    errorCount = 1,
                    conflictCount = 0,
                    operations = Array.Empty<object>()
                }, JsonOptions));
            CharacterAuthoringReadinessStage staleReportUnity =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(staleReportUnity != null && staleReportUnity.Status == "Ready" &&
                    staleReportUnity.Metrics["importReportStatus"] == "Stale",
                "an import report with the same source hash but a different compiler identity should be ignored as stale.");

            WriteTestArtifact(
                tempRoot,
                importReportRelative,
                JsonSerializer.Serialize(new
                {
                    packageId = "codex_flow_test",
                    status = "Failed",
                    sourcePackageHash = sourceHash,
                    generatedConfigHash,
                    geometryBindingHash,
                    resourceMappingHash,
                    writePlanHash,
                    canWriteToUnityProject = false,
                    canSpawnAfterImport = false,
                    errorCount = 1,
                    conflictCount = 0,
                    operations = Array.Empty<object>()
                }, JsonOptions));
            CharacterAuthoringReadinessStage failedReportUnity =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(failedReportUnity != null && failedReportUnity.Status == "Blocked" &&
                    failedReportUnity.Blockers.Exists(item => item.Code == "UNITY_IMPORT_REPORT_FAILED"),
                "a failed import report matching packageId and all five compiler hashes should block readiness.");

            WriteTestArtifact(
                tempRoot,
                importReportRelative,
                JsonSerializer.Serialize(new
                {
                    packageId = "codex_flow_test",
                    status = "Ready",
                    sourcePackageHash = sourceHash,
                    generatedConfigHash,
                    geometryBindingHash,
                    resourceMappingHash,
                    writePlanHash,
                    canWriteToUnityProject = true,
                    canSpawnAfterImport = true,
                    errorCount = 0,
                    conflictCount = 0,
                    operations = Array.Empty<object>()
                }, JsonOptions));

            receipt.hashes.sourcePackageHash = "sha256:stale";
            WriteTestArtifact(tempRoot, receiptRelative, JsonSerializer.Serialize(receipt, JsonOptions));
            CharacterAuthoringReadinessStage staleUnity =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(staleUnity != null && staleUnity.Status == "Deferred" && staleUnity.Metrics["receiptStatus"] == "Stale",
                "a stale receipt should request re-import without blocking character content.");

            WriteTestArtifact(tempRoot, receiptRelative, "{");
            CharacterAuthoringReadinessStage invalidUnity =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(invalidUnity != null && invalidUnity.Status == "Blocked" && invalidUnity.Metrics["receiptStatus"] == "Invalid",
                "malformed receipt JSON should block readiness as corrupted acceptance evidence.");

            receipt.hashes.sourcePackageHash = sourceHash;
            WriteTestArtifact(tempRoot, receiptRelative, JsonSerializer.Serialize(receipt, JsonOptions));
            string receiptFullPath = Path.Combine(tempRoot, receiptRelative.Replace('/', Path.DirectorySeparatorChar));
            CharacterAuthoringReadinessStage unreadableUnity;
            using (FileStream lockedReceipt = new FileStream(
                       receiptFullPath,
                       FileMode.Open,
                       FileAccess.ReadWrite,
                       FileShare.None))
            {
                unreadableUnity =
                    MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                        .Stages.Find(item => item.Id == "UnityImport");
            }
            Require(unreadableUnity != null && unreadableUnity.Status == "Blocked" &&
                    unreadableUnity.Blockers.Exists(item => item.Code == "UNITY_EDITOR_IMPORT_RECEIPT_READ_FAILED"),
                "an unreadable receipt should produce a stable blocked diagnostic instead of failing the readiness API.");

            receipt.packageId = "other_package";
            WriteTestArtifact(tempRoot, receiptRelative, JsonSerializer.Serialize(receipt, JsonOptions));
            CharacterAuthoringReadinessStage packageMismatchUnity =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(packageMismatchUnity != null && packageMismatchUnity.Status == "Blocked" &&
                    packageMismatchUnity.Blockers.Exists(item => item.Code == "UNITY_EDITOR_IMPORT_RECEIPT_PACKAGE_MISMATCH"),
                "a receipt for another package should block a claimed ready state.");

            receipt.packageId = "codex_flow_test";
            receipt.prefab.prefabPath = "../outside.prefab";
            WriteTestArtifact(tempRoot, receiptRelative, JsonSerializer.Serialize(receipt, JsonOptions));
            CharacterAuthoringReadinessStage escapedPathUnity =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(escapedPathUnity != null && escapedPathUnity.Status == "Blocked" && escapedPathUnity.Metrics["receiptStatus"] == "Invalid",
                "project-escaping receipt paths should block readiness.");

            receipt.prefab.prefabPath = prefabRelative;
            File.Delete(Path.Combine(tempRoot, prefabRelative.Replace('/', Path.DirectorySeparatorChar)));
            WriteTestArtifact(tempRoot, receiptRelative, JsonSerializer.Serialize(receipt, JsonOptions));
            CharacterAuthoringReadinessStage missingArtifactUnity =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(missingArtifactUnity != null && missingArtifactUnity.Status == "Blocked" &&
                    missingArtifactUnity.Metrics["receiptStatus"] == "CriticalArtifactMissing",
                "a missing receipt artifact should block a claimed ready state.");
            WriteTestArtifact(tempRoot, prefabRelative, "prefab");

            WriteTestArtifact(tempRoot, receiptRelative, JsonSerializer.Serialize(receipt, JsonOptions));
            string statusRelative = "Temp/MxFrameworkAuthoring/CharacterImport/status/current-failed.json";
            WriteTestArtifact(
                tempRoot,
                statusRelative,
                JsonSerializer.Serialize(new
                {
                    requestId = "current-failed",
                    status = "failed",
                    message = "explicit test failure",
                    package = packageRelative,
                    packageId = "codex_flow_test",
                    sourcePackageHash = sourceHash,
                    generatedConfigHash,
                    geometryBindingHash,
                    resourceMappingHash,
                    writePlanHash,
                    updatedUtc = DateTimeOffset.UtcNow.ToString("O")
                }, JsonOptions));
            CharacterAuthoringReadinessStage failedSessionUnity =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(failedSessionUnity != null && failedSessionUnity.Status == "Blocked" &&
                    failedSessionUnity.Metrics["evidenceSource"] == "currentUnitySession" &&
                    failedSessionUnity.Metrics["receiptStatus"] == "Current",
                "a current failed session should override an otherwise valid persistent receipt.");

            WriteTestArtifact(
                tempRoot,
                statusRelative,
                JsonSerializer.Serialize(new
                {
                    requestId = "old-session",
                    status = "failed",
                    message = "old failure",
                    package = packageRelative,
                    packageId = "codex_flow_test",
                    sourcePackageHash = sourceHash,
                    generatedConfigHash = "sha256:old-generated",
                    geometryBindingHash,
                    resourceMappingHash,
                    writePlanHash,
                    updatedUtc = DateTimeOffset.UtcNow.AddMinutes(1).ToString("O")
                }, JsonOptions));
            CharacterAuthoringReadinessStage oldSessionUnity =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(oldSessionUnity != null && oldSessionUnity.Status == "Ready" &&
                    oldSessionUnity.Metrics["evidenceSource"] == "persistentReceipt",
                "a Temp status with the same source hash but a different compiler identity should not override a current persistent receipt.");

            File.Delete(Path.Combine(tempRoot, receiptRelative.Replace('/', Path.DirectorySeparatorChar)));
            File.Delete(Path.Combine(tempRoot, importReportRelative.Replace('/', Path.DirectorySeparatorChar)));
            WriteTestArtifact(
                tempRoot,
                statusRelative,
                JsonSerializer.Serialize(new
                {
                    requestId = "current-completed",
                    status = "completed",
                    message = "completed for current source",
                    package = packageRelative,
                    packageId = "codex_flow_test",
                    sourcePackageHash = sourceHash,
                    generatedConfigHash,
                    geometryBindingHash,
                    resourceMappingHash,
                    writePlanHash,
                    builderVersion = "test-builder",
                    artifactHash = "sha256:artifact",
                    prefabPath = prefabRelative,
                    runtimePrefabPath = runtimePrefabRelative,
                    avatarSourceStatus = "Valid",
                    avatarSourceAssetPath = avatarRelative,
                    avatarSourceGuid = "test-avatar-guid",
                    avatarSourceHumanoidStatus = "ValidHuman",
                    characterActionProfileReady = true,
                    characterActionActionSetReady = true,
                    characterActionProfilePath = profileRelative,
                    characterActionSetPath = actionSetRelative,
                    weaponTraceBakeStatus = "Ready",
                    weaponTraceBakeHash = "sha256:trace",
                    weaponTraceBakeReady = true,
                    weaponTraceBakeIssueCount = 0,
                    socketBakeStatus = "Ready",
                    socketBakeHash = "sha256:socket-bake",
                    socketBakeRequestCount = 0,
                    socketBakeClipCount = 0,
                    socketBakeSocketTrackCount = 0,
                    socketBakeCheckedFrameCount = 0,
                    socketBakeIssueCount = 0,
                    socketBakeReady = true,
                    socketBakeFormat = receipt.socketBake.format,
                    socketBakeIndexPath = receipt.socketBake.indexPath,
                    socketBakeIndexHash = socketEvidence.IndexHash,
                    socketBakeBinaryPath = receipt.socketBake.binaryPath,
                    socketBakeBinaryHash = socketEvidence.BinaryHash,
                    socketBakeBinarySize = socketEvidence.BinarySize,
                    hurtboxBakeStatus = "Ready",
                    hurtboxBakeHash = "sha256:hurtbox-bake",
                    hurtboxBakeClipCount = 1,
                    hurtboxBakeColliderCount = 1,
                    hurtboxBakeShapeCount = 1,
                    hurtboxBakeCheckedFrameCount = 1,
                    hurtboxBakeIssueCount = 0,
                    hurtboxBakeReady = true,
                    abstractBoneStatus = "Ready",
                    abstractBoneDeclaredCount = 5,
                    abstractBoneResolvedCount = 5,
                    abstractBoneIssueCount = 0,
                    abstractBoneRequiredIds = new[] { "head" },
                    abstractBoneIssues = Array.Empty<string>(),
                    issues = Array.Empty<string>(),
                    updatedUtc = DateTimeOffset.UtcNow.AddMinutes(2).ToString("O")
                }, JsonOptions));
            CharacterAuthoringReadinessStage completedSessionUnity =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(completedSessionUnity != null && completedSessionUnity.Status == "Ready" &&
                    completedSessionUnity.Metrics["evidenceSource"] == "currentUnitySession" &&
                    completedSessionUnity.Metrics["importReportStatus"] == "Missing",
                "a fully accepted current Unity session should remain primary evidence when the import report is absent.");
            File.WriteAllBytes(socketBinaryFile, tamperedBinary);
            CharacterAuthoringReadinessStage changedSessionBinary =
                EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(changedSessionBinary.Status == "Blocked",
                "a same-size modified socket payload must invalidate current session evidence too.");
            File.WriteAllBytes(socketBinaryFile, originalBinary);
            File.WriteAllText(socketIndexFile, originalIndex + " ");
            CharacterAuthoringReadinessStage changedSessionIndex =
                EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(changedSessionIndex.Status == "Blocked", "changed index bytes must invalidate current session evidence.");
            File.WriteAllText(socketIndexFile, originalIndex);


            WriteTestArtifact(
                tempRoot,
                "Temp/MxFrameworkAuthoring/CharacterImport/status/newer-mismatched-failed.json",
                JsonSerializer.Serialize(new
                {
                    requestId = "newer-mismatched-failed",
                    status = "failed",
                    message = "same source but different generated config",
                    package = packageRelative,
                    packageId = "codex_flow_test",
                    sourcePackageHash = sourceHash,
                    generatedConfigHash = "sha256:other-generated",
                    geometryBindingHash,
                    resourceMappingHash,
                    writePlanHash,
                    updatedUtc = DateTimeOffset.UtcNow.AddMinutes(3).ToString("O")
                }, JsonOptions));
            CharacterAuthoringReadinessStage matchingCompletedUnity =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions)
                    .Stages.Find(item => item.Id == "UnityImport");
            Require(matchingCompletedUnity != null && matchingCompletedUnity.Status == "Ready" &&
                    matchingCompletedUnity.Metrics["editorRequestId"] == "current-completed",
                "the latest fully matching completed session should take precedence over a newer status for another compiler identity.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static (string IndexHash, string BinaryHash, long BinarySize) WriteEmptySocketBakePairForReadiness(
        string projectRoot, string packageId)
    {
        string root = Path.Combine(projectRoot, "Assets/MxFrameworkGenerated/CharacterPackages", packageId, "generated");
        Directory.CreateDirectory(root);
        byte[] binary;
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(0x3342534Du);
                writer.Write((ushort)3);
                writer.Write((ushort)0);
                writer.Write(0u);
                writer.Write(1u);
                writer.Write(0u);
                writer.Write(0u);
                writer.Write(0u);
                byte[] packageBytes = System.Text.Encoding.UTF8.GetBytes(packageId);
                writer.Write((uint)packageBytes.Length);
                writer.Write(packageBytes);
            }
            binary = stream.ToArray();
        }
        string binaryFile = Path.Combine(root, "character_socket_bake_catalog.bytes");
        File.WriteAllBytes(binaryFile, binary);
        string binaryHash = CharacterPackageHashUtility.ComputeFileSha256(binaryFile);
        string indexFile = Path.Combine(root, "character_socket_bake_catalog.json");
        File.WriteAllText(indexFile, JsonSerializer.Serialize(new
        {
            format = "mx.character.socketBakeCatalog.v3", packageId,
            binaryPath = "generated/character_socket_bake_catalog.bytes", binaryVersion = 3,
            binaryHash, binarySize = binary.Length, trackCount = 0, frameCount = 0, basisFrameCount = 0
        }, JsonOptions));
        return (CharacterPackageHashUtility.ComputeFileSha256(indexFile), binaryHash, binary.Length);
    }

    private static MxFramework.Authoring.Cli.EditorServer.CharacterUnityEditorImportReceipt CreateUnityImportReceipt(
        string sourceHash,
        string generatedConfigHash,
        string geometryBindingHash,
        string resourceMappingHash,
        string writePlanHash,
        string prefabPath,
        string runtimePrefabPath,
        string avatarPath,
        string materialPath,
        string profilePath,
        string actionSetPath)
    {
        return new MxFramework.Authoring.Cli.EditorServer.CharacterUnityEditorImportReceipt
        {
            format = "mx.characterUnityEditorImportReceipt.v3",
            schemaVersion = "3.0",
            ready = true,
            packageId = "codex_flow_test",
            builderVersion = "test-builder",
            hashes = new MxFramework.Authoring.Cli.EditorServer.CharacterUnityEditorImportReceiptHashes
            {
                sourcePackageHash = sourceHash,
                generatedConfigHash = generatedConfigHash,
                geometryBindingHash = geometryBindingHash,
                resourceMappingHash = resourceMappingHash,
                writePlanHash = writePlanHash,
                unityArtifactHash = "sha256:artifact"
            },
            prefab = new MxFramework.Authoring.Cli.EditorServer.CharacterUnityEditorImportReceiptPrefab
            {
                prefabPath = prefabPath,
                runtimePrefabPath = runtimePrefabPath
            },
            avatar = new MxFramework.Authoring.Cli.EditorServer.CharacterUnityEditorImportReceiptAvatar
            {
                status = "Valid",
                assetPath = avatarPath,
                guid = "test-avatar-guid",
                rigType = "Humanoid",
                rigStatus = "ValidHuman",
                humanoidStatus = "ValidHuman"
            },
            materials = new[]
            {
                new MxFramework.Authoring.Cli.EditorServer.CharacterUnityEditorImportReceiptMaterial
                {
                    status = "Assigned",
                    materialPath = materialPath,
                    textureResourceKey = "char.codex_flow_test.texture.body_albedo",
                    sourceMaterialName = "Body",
                    assignedSlotCount = 1
                }
            },
            characterAction = new MxFramework.Authoring.Cli.EditorServer.CharacterUnityEditorImportReceiptCharacterAction
            {
                profileReady = true,
                actionSetReady = true,
                profilePath = profilePath,
                actionSetPath = actionSetPath
            },
            weaponTraceBake = new MxFramework.Authoring.Cli.EditorServer.CharacterUnityEditorImportReceiptWeaponTraceBake
            {
                status = "Ready",
                hash = "sha256:trace",
                requestCount = 2,
                artifactCount = 2,
                checkedFrameCount = 10
            },
            socketBake = new MxFramework.Authoring.Cli.EditorServer.CharacterUnityEditorImportReceiptSocketBake
            {
                status = "Ready",
                hash = "sha256:socket-bake",
                requestCount = 0,
                clipCount = 0,
                socketTrackCount = 0,
                checkedFrameCount = 0,
                issueCount = 0,
                format = "mx.character.socketBakeCatalog.v3",
                indexPath = "generated/character_socket_bake_catalog.json",
                indexHash = "sha256:socket-bake-index",
                binaryPath = "generated/character_socket_bake_catalog.bytes",
                binaryHash = "sha256:socket-bake-binary",
                binarySize = 53
            },
            hurtboxBake = new MxFramework.Authoring.Cli.EditorServer.CharacterUnityEditorImportReceiptHurtboxBake
            {
                status = "Ready",
                hash = "sha256:hurtbox-bake",
                clipCount = 1,
                colliderCount = 1,
                shapeCount = 1,
                checkedFrameCount = 1,
                issueCount = 0
            },
            abstractBones = new MxFramework.Authoring.Cli.EditorServer.CharacterUnityEditorImportReceiptAbstractBones
            {
                status = "Ready",
                requiredIds = new[] { "head" },
                declaredCount = 5,
                resolvedCount = 5,
                issueCount = 0,
                issues = Array.Empty<string>()
            }
        };
    }

    private static void WriteTestArtifact(string rootPath, string relativePath, string content)
    {
        string path = Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? rootPath);
        File.WriteAllText(path, content);
    }

    private static void CopyTestFile(string sourcePath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);
        File.Copy(sourcePath, targetPath, overwrite: true);
    }

    private static void EditorServer_CharacterAbilityChainApi_UsesCompiledDefaultScenario()
    {
        string root = FindRepoRoot();
        const string packageRelative = "Tools/MxFramework.Authoring/samples/character-codex-flow-test";

        CharacterAbilityChainSnapshot snapshot =
            MxFramework.Authoring.Cli.EditorServer.ReadCharacterAbilityChain(root, packageRelative, JsonOptions);

        Require(snapshot != null && snapshot.Status == "Ready", "ability chain snapshot should resolve from the compiled package.");
        Require(snapshot.PackageId == "codex_flow_test", "ability chain should preserve package id.");
        Require(snapshot.PotentialAbilityCount == 2,
            "compatibility ability chain should expose the union of the two retained combinations.");
        Require(snapshot.Scenarios.Count == 2, "compatibility ability chain should expose the two retained combinations.");

        CharacterAbilityScenarioSnapshot defaultScenario = snapshot.Scenarios.Find(item => item.ScenarioId == snapshot.DefaultScenarioId);
        Require(defaultScenario != null && defaultScenario.IsDefault, "ability chain should identify the compiled default scenario.");
        Require(defaultScenario.StableId == "equip_loadout.codex_flow_test.sword_empty", "default scenario should be sword-empty.");
        Require(defaultScenario.EquipmentStateStableId == "equip_state.codex_flow_test.sword_empty", "default scenario should resolve its equipment state.");
        Require(defaultScenario.ActionSetStableId == "codex_flow_test.sword_empty",
            "default scenario should resolve the combination-private action set selected by its equipment state.");
        Require(defaultScenario.EffectiveAbilityCount == 2,
            "default sword-empty scenario should preserve its historical ability compatibility projection.");
        Require(defaultScenario.SourceCounts["base"] == 0, "schema 4.0 has no character base ability layer.");
        Require(defaultScenario.SourceCounts["weapon"] == 0,
            "schema 4.0 single weapons must not contribute abilities.");
        Require(defaultScenario.SourceCounts["equipmentState"] == 2, "the selected complete ability configuration should grant both abilities from the equipment state.");

        int[] expectedAbilityIds = { 900001, 900102 };
        for (int i = 0; i < expectedAbilityIds.Length; i++)
        {
            int abilityId = expectedAbilityIds[i];
            CharacterAbilityChainAbility ability = defaultScenario.Abilities.Find(item => item.AbilityId == abilityId);
            Require(ability != null, "default scenario should include ability " + abilityId + ".");
            Require(ability.GrantedStatus == "Ready", "compiled grants should be ready.");
            Require(ability.SlotStatus == "Ready", "sample ability grants should expose their compiled slot/input bindings.");
            Require(ability.ActionStatus == "Ready", "sample 900xxx abilities should resolve package-scoped private CharacterAction bindings.");
            Require(ability.ActionBindings.Count == 1 && ability.ActionBindings[0].ActionExists,
                "each active ability role should resolve one existing private action target.");
            Require(ability.RuntimeStatus == "Deferred", "sample abilities should expose deferred gameplay registry binding.");
            Require(ability.NextAction == "RegisterGameplayBinding", "the remaining actionable gap should be runtime gameplay registration.");
        }

        string json = JsonSerializer.Serialize(snapshot, JsonOptions);
        Require(json.Contains("\"defaultScenarioId\""), "ability chain JSON should use camelCase fields.");
        Require(json.Contains("\"sourceCounts\""), "ability chain JSON should expose resolved source counts.");
    }

    private static void EditorServer_AbilityActionBindings_ArePackageScoped()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-ability-action-scope-" + Guid.NewGuid().ToString("N"));
        string packageARoot = Path.Combine(tempRoot, "Assets", "MxFrameworkGenerated", "CharacterActions", "ActionSets", "package_a");
        string packageBRoot = Path.Combine(tempRoot, "Assets", "MxFrameworkGenerated", "CharacterActions", "ActionSets", "package_b");
        Directory.CreateDirectory(packageARoot);
        Directory.CreateDirectory(packageBRoot);
        File.WriteAllText(
            Path.Combine(packageARoot, "character_action_sets.json"),
            "{\"actionSets\":[{\"stableId\":\"set.package_a\",\"abilityBindings\":[{\"abilityId\":101,\"actionId\":901}]}]}");
        File.WriteAllText(
            Path.Combine(packageBRoot, "character_action_sets.json"),
            "{\"actionSets\":[{\"stableId\":\"set.package_b\",\"abilityBindings\":[{\"abilityId\":202,\"actionId\":902}]}]}");

        try
        {
            MxFramework.Authoring.Cli.EditorServer.AbilityActionBindingReadiness readiness =
                MxFramework.Authoring.Cli.EditorServer.ReadAbilityActionBindings(tempRoot, "package_a");
            Require(readiness.BoundAbilityIds.SetEquals(new[] { 101 }), "ability action readiness should only read the selected package directory.");
            Require(readiness.Bindings.Count == 1 && readiness.Bindings[0].ActionSetStableId == "set.package_a", "ability action readiness should preserve only the selected package action set.");
            Require(readiness.SourcePaths.Count == 1 && readiness.SourcePaths[0].Contains("/package_a/", StringComparison.Ordinal), "ability action readiness source evidence should be package scoped.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_CharacterReadinessApi_ReportsLegacyReactionProfileHandoff()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-reaction-readiness-" + Guid.NewGuid().ToString("N"));
        const string packageRelative = "Tools/MxFramework.Authoring/samples/character-legacy-reaction";
        string packagePath = Path.Combine(tempRoot, "Tools", "MxFramework.Authoring", "samples", "character-legacy-reaction");
        Directory.CreateDirectory(Path.Combine(packagePath, "config"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "Assets", "MxFrameworkGenerated", "CharacterActions", "ActionSets", "legacy_reaction"));
        File.WriteAllText(Path.Combine(packagePath, "manifest.json"), JsonSerializer.Serialize(new CharacterPackageManifest
        {
            PackageId = "legacy_reaction",
            StableId = "char.legacy_reaction",
            DisplayName = "Legacy Reaction"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "resource_catalog.json"), JsonSerializer.Serialize(new CharacterPackageResourceCatalog(), JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "config", "character_application.json"), JsonSerializer.Serialize(new CharacterApplicationAuthoringSummary(), JsonOptions));
        File.WriteAllText(
            Path.Combine(tempRoot, "Assets", "MxFrameworkGenerated", "CharacterActions", "ActionSets", "legacy_reaction", "character_action_sets.json"),
            "{\n"
            + "  \"schemaVersion\": \"0.4.0\",\n"
            + "  \"actionSets\": [\n"
            + "    { \"id\": 1, \"stableId\": \"legacy_reaction.default\", \"reactionProfileId\": \"reaction.default\", \"commandBindings\": [], \"abilityBindings\": [], \"reactionBindings\": [] }\n"
            + "  ]\n"
            + "}\n");

        try
        {
            CharacterAuthoringReadinessSnapshot snapshot =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterReadiness(tempRoot, packageRelative, packageRelative, JsonOptions);
            CharacterAuthoringReadinessStage stage = snapshot.Stages.Find(item => item.Id == "ReactionBinding");
            Require(stage != null, "readiness should include ReactionBinding stage.");
            Require(stage.Status == "Blocked", "legacy reaction handoff should block ReactionBinding readiness.");
            Require(stage.Blockers.Exists(issue => issue.Code == "CHARACTER_ACTION_REACTION_PROFILE_LEGACY_ID"), "readiness should report legacy reaction.default binding.");
            Require(stage.Blockers.Exists(issue => issue.Code == "CHARACTER_ACTION_REACTION_PROFILES_FILE_MISSING"), "readiness should report missing package reaction profile JSON.");
            Require(stage.Blockers.Exists(issue => issue.SuggestedFix.Contains("Save/Regenerate Reaction Profile", StringComparison.Ordinal)), "readiness should provide a self-service fix.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_ReactionProfileSave_RebuildsConsistentCharacterActionHandoff()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-reaction-save-" + Guid.NewGuid().ToString("N"));
        string actionSetRoot = Path.Combine(tempRoot, "Assets", "MxFrameworkGenerated", "CharacterActions", "ActionSets", "save_test");
        string libraryRoot = Path.Combine(tempRoot, "Assets", "MxFrameworkGenerated", "CharacterActions", "Libraries", "Common");
        Directory.CreateDirectory(actionSetRoot);
        Directory.CreateDirectory(libraryRoot);
        Directory.CreateDirectory(Path.Combine(tempRoot, "CharacterActionSource"));
        File.Copy(
            Path.Combine(FindRepoRoot(), "CharacterActionSource", "action_id_catalog.json"),
            Path.Combine(tempRoot, "CharacterActionSource", "action_id_catalog.json"));
        File.WriteAllText(
            Path.Combine(actionSetRoot, "character_action_sets.json"),
            "{\n"
            + "  \"schemaVersion\": \"0.1.0\",\n"
            + "  \"actionSets\": [\n"
            + "    { \"id\": 1, \"stableId\": \"save_test.default\", \"reactionProfileId\": \"reaction.default\", \"commandBindings\": [], \"abilityBindings\": [], \"reactionBindings\": [] }\n"
            + "  ]\n"
            + "}\n");
        File.WriteAllText(
            Path.Combine(libraryRoot, "character_actions.json"),
            "{\n  \"schemaVersion\": \"0.3.0\",\n  \"actions\": []\n}\n");

        try
        {
            var request = new MxFramework.Authoring.Cli.EditorServer.CharacterActionReactionProfileSaveRequest
            {
                packageId = "save_test",
                actionSetStableId = "save_test.default",
                reactionProfileId = "reaction.save_test.default",
                displayName = "Save Test Reaction",
                slots = new[]
                {
                    new MxFramework.Authoring.Cli.EditorServer.CharacterActionReactionSlotSaveRequest
                    {
                        slotId = "cohesionBreak",
                        displayName = "Cohesion Break",
                        trigger = "CohesionBreak",
                        pressureBand = "Broken",
                        actionId = 330,
                        durationFrames = 18,
                        priority = 120
                    },
                    new MxFramework.Authoring.Cli.EditorServer.CharacterActionReactionSlotSaveRequest
                    {
                        slotId = "hitMid",
                        displayName = "Mid Hit",
                        trigger = "Hit",
                        pressureBand = "Broken",
                        reactionGroupId = "reaction.height.mid",
                        maxImpactForce = 59,
                        actionId = 331,
                        durationFrames = 12,
                        priority = 89
                    }
                }
            };

            MxFramework.Authoring.Cli.EditorServer.CharacterActionReactionProfileSaveResult result =
                MxFramework.Authoring.Cli.EditorServer.SaveCharacterActionReactionProfile(tempRoot, request);
            Require(result.success, "reaction profile save should succeed.");
            Require(File.Exists(Path.Combine(actionSetRoot, "character_reaction_profiles.json")), "save should write character_reaction_profiles.json.");

            CharacterActionHandoffConsistencyReport report =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterActionHandoffConsistency(tempRoot, "save_test");
            Require(!report.HasBlockingIssues, "saved reaction handoff should validate without blockers.");
            Require(report.ActionSetReactionBindingCount == 1, "saved handoff should expose one action-set binding.");
            Require(report.ReactionProfileCount == 1, "saved handoff should expose one reaction profile.");
            Require(report.ReactionActionReferenceCount == 3, "saved handoff should validate default and rule action references.");

            using JsonDocument generatedActions = JsonDocument.Parse(File.ReadAllText(Path.Combine(libraryRoot, "character_actions.json")));
            JsonElement cohesionAction = default;
            foreach (JsonElement action in generatedActions.RootElement.GetProperty("actions").EnumerateArray())
            {
                if (action.GetProperty("actionId").GetInt32() == 330)
                {
                    cohesionAction = action;
                    break;
                }
            }
            Require(cohesionAction.ValueKind == JsonValueKind.Object, "save should generate the cohesion reaction action.");
            JsonElement motionEvents = cohesionAction.GetProperty("motionTrack").GetProperty("events");
            Require(motionEvents.GetArrayLength() == 1, "reaction control should be authored as one inclusive stateful range.");
            JsonElement controlRange = motionEvents[0];
            Require(controlRange.GetProperty("frame").GetInt32() == 0, "reaction control range should start on frame zero.");
            Require(controlRange.GetProperty("endFrame").GetInt32() == 17, "reaction control range should end on the final action frame.");
            Require(controlRange.GetProperty("stableEventId").GetString() == "character_action.330.control_lock", "reaction control range should keep a stable lifecycle identity.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_ReactionProfileSave_RejectsSchema8GeneratedProjectionWrites()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-reaction-schema8-save-" + Guid.NewGuid().ToString("N"));
        const string packageRelative = "Authoring/CharacterPackages/schema8-save-test";
        string configRoot = Path.Combine(tempRoot, packageRelative, "config");
        Directory.CreateDirectory(configRoot);
        File.WriteAllText(
            Path.Combine(configRoot, "character_application.json"),
            "{\n  \"schemaVersion\": \"8.0\"\n}\n");

        try
        {
            var request = new MxFramework.Authoring.Cli.EditorServer.CharacterActionReactionProfileSaveRequest
            {
                package = packageRelative,
                packageId = "schema8_save_test",
                actionSetStableId = "schema8_save_test.sword_empty",
                reactionProfileId = "reaction.schema8_save_test.sword_empty",
                slots = new[]
                {
                    new MxFramework.Authoring.Cli.EditorServer.CharacterActionReactionSlotSaveRequest
                    {
                        slotId = "hit_default",
                        trigger = "Hit",
                        actionId = 1000100,
                        durationFrames = 12,
                        priority = 60
                    }
                }
            };

            MxFramework.Authoring.Cli.EditorServer.CharacterActionReactionProfileSaveResult result =
                MxFramework.Authoring.Cli.EditorServer.SaveCharacterActionReactionProfile(tempRoot, request);

            Require(!result.success, "schema 8 reaction projection writes should be rejected.");
            Require(result.error == "CHARACTER_ACTION_REACTION_PROFILE_COMPILER_OWNED", "schema 8 rejection should identify compiler ownership.");
            Require(
                !Directory.Exists(Path.Combine(tempRoot, "Assets", "MxFrameworkGenerated", "CharacterActions")),
                "schema 8 rejection must not create or mutate generated CharacterAction projections.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_ImportMaterialSourceCandidates_AreScopedAndClassified()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-import-material-candidates-" + Guid.NewGuid().ToString("N"));
        string packageRelative = "Tools/MxFramework.Authoring/samples/import-material-candidates-test";
        string packagePath = Path.Combine(tempRoot, "Tools", "MxFramework.Authoring", "samples", "import-material-candidates-test");
        string sourceRoot = Path.Combine(tempRoot, "ArtSource", "Characters", "CandidateTest");
        Directory.CreateDirectory(packagePath);
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(Path.Combine(sourceRoot, "hero_body.fbx"), "body");
        File.WriteAllText(Path.Combine(sourceRoot, "locomotion_run.glb"), "locomotion");
        File.WriteAllText(Path.Combine(sourceRoot, "right_sword.fbx"), "sword");
        File.WriteAllText(Path.Combine(sourceRoot, "left_shield.fbx"), "shield");
        File.WriteAllText(Path.Combine(sourceRoot, "paint.psd"), "psd");
        File.WriteAllText(Path.Combine(sourceRoot, "ignored.txt"), "txt");

        try
        {
            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialSourceCandidatesState outOfScopeRoot =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterImportMaterialSourceCandidates(tempRoot, packageRelative, packageRelative, "../Outside", string.Empty, JsonOptions);
            Require(!outOfScopeRoot.success, "out-of-scope sourceRoot should be rejected.");
            Require(outOfScopeRoot.diagnostics.Exists(item => item.code == "IMPORT_MATERIALS_UNSAFE_SOURCE_ROOT"), "out-of-scope sourceRoot should return a stable diagnostic.");

            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialSourceCandidatesState missingRoot =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterImportMaterialSourceCandidates(tempRoot, packageRelative, packageRelative, "ArtSource/Characters/Missing", string.Empty, JsonOptions);
            Require(missingRoot.success && missingRoot.items.Count == 0, "missing sourceRoot should not throw.");
            Require(missingRoot.diagnostics.Exists(item => item.code == "IMPORT_MATERIALS_SOURCE_ROOT_MISSING"), "missing sourceRoot should return a stable diagnostic.");

            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialSourceCandidatesState candidates =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterImportMaterialSourceCandidates(tempRoot, packageRelative, packageRelative, "ArtSource/Characters/CandidateTest", string.Empty, JsonOptions);
            Require(candidates.success, "candidate scan should succeed.");
            Require(candidates.items.Exists(item => item.sourcePath == "hero_body.fbx" && item.slotKey == "body" && item.presetId == "characterModel"), "body model should be classified.");
            Require(candidates.items.Exists(item => item.sourcePath == "locomotion_run.glb" && item.slotKey == string.Empty && item.presetId == "modelPreview"), "locomotion-looking files should not be classified as a source manifest slot.");
            Require(candidates.items.Exists(item => item.sourcePath == "right_sword.fbx" && item.slotKey == string.Empty && item.presetId == "modelPreview"), "weapon-looking files should not be classified as character source slots.");
            Require(candidates.items.Exists(item => item.sourcePath == "left_shield.fbx" && item.slotKey == string.Empty && item.presetId == "modelPreview"), "off-hand weapon-looking files should not be classified as character source slots.");
            Require(candidates.items.Exists(item => item.sourcePath == "paint.psd" && item.presetId == "texture" && !item.selectable), "unsupported direct sync sources should remain visible with diagnostics.");
            Require(!candidates.items.Exists(item => item.sourcePath == "ignored.txt"), "unsupported extensions should not become candidates.");

            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialSourceCandidatesState queried =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterImportMaterialSourceCandidates(tempRoot, packageRelative, packageRelative, "ArtSource/Characters/CandidateTest", "shield", JsonOptions);
            Require(queried.items.Count == 1 && queried.items[0].slotKey == string.Empty, "query should filter candidate paths without assigning a weapon source slot.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_ImportMaterialsApi_SavesStatusAndSyncsChangedSources()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-import-materials-" + Guid.NewGuid().ToString("N"));
        string packageRelative = "Tools/MxFramework.Authoring/samples/import-materials-test";
        string packagePath = Path.Combine(tempRoot, "Tools", "MxFramework.Authoring", "samples", "import-materials-test");
        string sourceRoot = Path.Combine(tempRoot, "ArtSource", "Characters", "import_materials_test");
        Directory.CreateDirectory(Path.Combine(packagePath, "config"));
        Directory.CreateDirectory(Path.Combine(packagePath, "geometry"));
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(Path.Combine(sourceRoot, "rules.json"), "{ \"version\": 1 }");
        File.WriteAllText(Path.Combine(packagePath, "manifest.json"), JsonSerializer.Serialize(new CharacterPackageManifest
        {
            PackageId = "import_materials_test",
            StableId = "char.import_materials_test",
            DisplayName = "Import Materials Test"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "resource_catalog.json"), JsonSerializer.Serialize(new CharacterPackageResourceCatalog(), JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "config", "character_application.json"), JsonSerializer.Serialize(new CharacterApplicationAuthoringSummary
        {
            CharacterStableId = "char.import_materials_test"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "geometry", "body_geometry.json"), JsonSerializer.Serialize(new CharacterBodyGeometryProfile
        {
            ProfileId = "body.import_materials_test",
            BodyKind = CharacterAuthoringBodyKind.Primitive,
            HeightMeters = 1.8f,
            RadiusMeters = 0.35f,
            MassKg = 80f
        }, JsonOptions));

        try
        {
            var request = new MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSaveRequest
            {
                package = packageRelative,
                document = new CharacterImportMaterialsDocument
                {
                    SourceRoot = "ArtSource/Characters/import_materials_test",
                    Items = new List<CharacterImportMaterialItem>
                    {
                        new CharacterImportMaterialItem
                        {
                            Id = "rules",
                            Label = "Rules",
                            SourcePath = "rules.json",
                            PresetId = "config",
                            ResourceKey = "char.import_materials_test.config.rules",
                            Required = true
                        },
                        new CharacterImportMaterialItem
                        {
                            Id = "missing",
                            Label = "Missing",
                            SourcePath = "missing.json",
                            PresetId = "config",
                            ResourceKey = "char.import_materials_test.config.missing",
                            Required = true
                        }
                    }
                }
            };

            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsState saved =
                MxFramework.Authoring.Cli.EditorServer.SaveCharacterImportMaterials(tempRoot, packageRelative, packageRelative, request, JsonOptions);
            string sourceManifestPath = Path.Combine(tempRoot, "ArtSource", "Characters", "import_materials_test", "character_sources.json");
            Require(File.Exists(sourceManifestPath), "import materials save should write the ArtSource character source manifest.");
            Require(!File.ReadAllText(sourceManifestPath).Contains("lastImportedHash", StringComparison.Ordinal), "source manifest should not store import hashes.");
            Require(saved.items.Exists(item => item.id == "rules" && item.status == "NotImported"), "existing source should start NotImported.");
            Require(saved.items.Exists(item => item.id == "missing" && item.status == "Missing"), "missing source should report Missing.");

            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSyncResponse firstSync =
                MxFramework.Authoring.Cli.EditorServer.SyncCharacterImportMaterials(tempRoot, packageRelative, packageRelative, new MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSyncRequest { allChanged = true }, JsonOptions);
            Require(firstSync.success, "first import materials sync should succeed.");
            Require(File.Exists(Path.Combine(packagePath, "config", "import_materials_state.json")), "sync should write package-local import materials state.");
            Require(firstSync.operations.Count == 1 && firstSync.operations[0].itemId == "rules", "allChanged sync should import only importable dirty items.");
            Require(firstSync.state.items.Exists(item => item.id == "rules" && item.resourceExists && item.status == "UnityPending"), "synced config resource should exist and wait for Unity import.");
            Require(firstSync.state.items.Exists(item => item.id == "missing" && item.status == "Missing"), "missing source should remain a stable diagnostic after sync.");

            CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, JsonOptions);
            CharacterPackageResourceEntry rules = CharacterPackageResourcePipeline.FindByKey(package.ResourceCatalog, "char.import_materials_test.config.rules");
            Require(rules != null, "sync should preserve requested resourceKey.");
            Require(rules.RelativePath == "resources/config/rules.json", "generic sync should write config resource into package resources.");

            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSyncResponse unchangedSync =
                MxFramework.Authoring.Cli.EditorServer.SyncCharacterImportMaterials(tempRoot, packageRelative, packageRelative, new MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSyncRequest { allChanged = true }, JsonOptions);
            Require(unchangedSync.operations.Count == 0, "unchanged allChanged sync should skip clean source links.");

            File.WriteAllText(Path.Combine(sourceRoot, "rules.json"), "{ \"version\": 2 }");
            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsState changed =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterImportMaterials(tempRoot, packageRelative, packageRelative, JsonOptions);
            Require(changed.items.Exists(item => item.id == "rules" && item.status == "Changed"), "changed source hash should report Changed.");

            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSyncResponse changedSync =
                MxFramework.Authoring.Cli.EditorServer.SyncCharacterImportMaterials(tempRoot, packageRelative, packageRelative, new MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSyncRequest { allChanged = true }, JsonOptions);
            Require(changedSync.operations.Count == 1 && changedSync.operations[0].success, "changed source should reimport.");
            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsState cleanAfterChange =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterImportMaterials(tempRoot, packageRelative, packageRelative, JsonOptions);
            Require(cleanAfterChange.items.Exists(item => item.id == "rules" && item.status == "UnityPending" && !item.sourceDirty), "after reimport source should no longer be dirty.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_ImportMaterialsApi_ReportsManifestPresence()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-import-materials-manifest-" + Guid.NewGuid().ToString("N"));
        string packageRelative = "Tools/MxFramework.Authoring/samples/import-materials-manifest-test";
        string packagePath = Path.Combine(tempRoot, "Tools", "MxFramework.Authoring", "samples", "import-materials-manifest-test");
        Directory.CreateDirectory(Path.Combine(packagePath, "config"));
        Directory.CreateDirectory(Path.Combine(packagePath, "geometry"));
        File.WriteAllText(Path.Combine(packagePath, "manifest.json"), JsonSerializer.Serialize(new CharacterPackageManifest
        {
            PackageId = "import_materials_manifest_test",
            StableId = "char.import_materials_manifest_test",
            DisplayName = "Import Materials Manifest Test"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "resource_catalog.json"), JsonSerializer.Serialize(new CharacterPackageResourceCatalog(), JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "config", "character_application.json"), JsonSerializer.Serialize(new CharacterApplicationAuthoringSummary
        {
            CharacterStableId = "char.import_materials_manifest_test"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "geometry", "body_geometry.json"), JsonSerializer.Serialize(new CharacterBodyGeometryProfile
        {
            ProfileId = "body.import_materials_manifest_test",
            BodyKind = CharacterAuthoringBodyKind.Primitive,
            HeightMeters = 1.8f,
            RadiusMeters = 0.35f,
            MassKg = 80f
        }, JsonOptions));

        try
        {
            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsState missing =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterImportMaterials(tempRoot, packageRelative, packageRelative, JsonOptions);
            Require(!missing.sourceManifestExists, "missing source manifest should be reported explicitly.");
            Require(!missing.legacyDocumentExists, "missing legacy import materials document should be reported explicitly.");
            Require(missing.items.Count == 0, "missing source manifest should not invent source manifest items.");

            File.WriteAllText(Path.Combine(packagePath, "config", "import_materials.json"), JsonSerializer.Serialize(new CharacterImportMaterialsDocument
            {
                SourceRoot = "ArtSource/Characters/import_materials_manifest_test",
                Items = new List<CharacterImportMaterialItem>
                {
                    new CharacterImportMaterialItem
                    {
                        Id = "legacy-body",
                        SourcePath = "legacy_body.fbx",
                        PresetId = "characterModel",
                        BindTarget = "body",
                        Required = true
                    }
                }
            }, JsonOptions));

            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsState legacy =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterImportMaterials(tempRoot, packageRelative, packageRelative, JsonOptions);
            Require(!legacy.sourceManifestExists && legacy.legacyDocumentExists, "legacy package document should not masquerade as an ArtSource source manifest.");
            Require(legacy.documentKind == "legacyPackageDocument", "legacy package document should be reported as legacy.");
            Require(legacy.items.Count == 1 && legacy.items[0].id == "legacy-body", "legacy package document should remain readable for migration.");

            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsState saved =
                MxFramework.Authoring.Cli.EditorServer.SaveCharacterImportMaterials(tempRoot, packageRelative, packageRelative, new MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSaveRequest
                {
                    package = packageRelative,
                    document = new CharacterImportMaterialsDocument
                    {
                        SourceRoot = "ArtSource/Characters/import_materials_manifest_test",
                        Items = new List<CharacterImportMaterialItem>()
                    }
                }, JsonOptions);
            Require(saved.sourceManifestExists, "saving should create the ArtSource source manifest.");
            Require(saved.legacyDocumentExists, "saving the source manifest should not delete the legacy document.");
            Require(saved.documentKind == "sourceManifest" && saved.items.Count == 0, "saved empty source manifest should be empty and authoritative.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_ImportMaterialsApi_ReportsFbxTextureDiagnostics()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-import-materials-fbx-textures-" + Guid.NewGuid().ToString("N"));
        string packageRelative = "Tools/MxFramework.Authoring/samples/import-materials-fbx-textures-test";
        string packagePath = Path.Combine(tempRoot, "Tools", "MxFramework.Authoring", "samples", "import-materials-fbx-textures-test");
        string sourceRoot = Path.Combine(tempRoot, "ArtSource", "Characters", "import_materials_fbx_textures_test");
        Directory.CreateDirectory(Path.Combine(packagePath, "config"));
        Directory.CreateDirectory(Path.Combine(packagePath, "geometry"));
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllText(Path.Combine(sourceRoot, "embedded.fbx"), "Objects: { Video: \"Video::EmbeddedTexture\", \"Clip\" { Filename: \"embedded.png\" RelativeFilename: \"embedded.png\" Content: \"AAAA\" } }");
        File.WriteAllText(Path.Combine(sourceRoot, "external.fbx"), "Objects: { Texture: \"Texture::Body\", \"TextureVideoClip\" { RelativeFilename: \"Textures/body_albedo.png\" } }");
        File.WriteAllText(Path.Combine(sourceRoot, "plain.fbx"), "Objects: { Model: \"Model::Body\", \"Mesh\" { } }");
        File.WriteAllText(Path.Combine(packagePath, "manifest.json"), JsonSerializer.Serialize(new CharacterPackageManifest
        {
            PackageId = "import_materials_fbx_textures_test",
            StableId = "char.import_materials_fbx_textures_test",
            DisplayName = "Import Materials Fbx Textures Test"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "resource_catalog.json"), JsonSerializer.Serialize(new CharacterPackageResourceCatalog(), JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "config", "character_application.json"), JsonSerializer.Serialize(new CharacterApplicationAuthoringSummary
        {
            CharacterStableId = "char.import_materials_fbx_textures_test"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "geometry", "body_geometry.json"), JsonSerializer.Serialize(new CharacterBodyGeometryProfile
        {
            ProfileId = "body.import_materials_fbx_textures_test",
            BodyKind = CharacterAuthoringBodyKind.Primitive,
            HeightMeters = 1.8f,
            RadiusMeters = 0.35f,
            MassKg = 80f
        }, JsonOptions));

        try
        {
            var request = new MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSaveRequest
            {
                package = packageRelative,
                document = new CharacterImportMaterialsDocument
                {
                    SourceRoot = "ArtSource/Characters/import_materials_fbx_textures_test",
                    Items = new List<CharacterImportMaterialItem>
                    {
                        new CharacterImportMaterialItem
                        {
                            Id = "embedded-body",
                            Label = "Embedded Body",
                            SourcePath = "embedded.fbx",
                            PresetId = "characterModel",
                            BindTarget = "body",
                            Required = true
                        },
                        new CharacterImportMaterialItem
                        {
                            Id = "external-body",
                            Label = "External Body",
                            SourcePath = "external.fbx",
                            PresetId = "characterModel",
                            BindTarget = "body",
                            Required = true
                        },
                        new CharacterImportMaterialItem
                        {
                            Id = "plain-body",
                            Label = "Plain Body",
                            SourcePath = "plain.fbx",
                            PresetId = "characterModel",
                            BindTarget = "body",
                            Required = false
                        }
                    }
                }
            };

            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsState saved =
                MxFramework.Authoring.Cli.EditorServer.SaveCharacterImportMaterials(tempRoot, packageRelative, packageRelative, request, JsonOptions);
            Require(saved.items.Exists(item => item.id == "embedded-body" && item.fbxTextureDiagnostic?.status == "embeddedTextureDetected"), "embedded FBX media should report embeddedTextureDetected.");
            Require(saved.items.Exists(item => item.id == "external-body" && item.fbxTextureDiagnostic?.status == "externalTextureRefs" && item.fbxTextureDiagnostic.externalTexturePaths.Contains("Textures/body_albedo.png")), "external FBX texture paths should report externalTextureRefs.");
            Require(saved.items.Exists(item => item.id == "plain-body" && item.fbxTextureDiagnostic?.status == "noTextureEvidence"), "plain FBX should report noTextureEvidence without blocking.");

            string sourceManifestPath = Path.Combine(tempRoot, "ArtSource", "Characters", "import_materials_fbx_textures_test", "character_sources.json");
            string sourceManifestJson = File.ReadAllText(sourceManifestPath);
            Require(!sourceManifestJson.Contains("fbxTextureDiagnostic", StringComparison.Ordinal), "source manifest should not persist FBX texture diagnostics.");
            Require(!sourceManifestJson.Contains("embeddedTextureDetected", StringComparison.Ordinal), "source manifest should not persist embedded texture status.");
            Require(!sourceManifestJson.Contains("externalTextureRefs", StringComparison.Ordinal), "source manifest should not persist external texture status.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_ImportMaterialsSync_InfersExistingSlotResourceKeys()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-import-materials-slots-" + Guid.NewGuid().ToString("N"));
        string packageRelative = "Tools/MxFramework.Authoring/samples/import-materials-slots-test";
        string packagePath = Path.Combine(tempRoot, "Tools", "MxFramework.Authoring", "samples", "import-materials-slots-test");
        string sourceRoot = Path.Combine(tempRoot, "ArtSource", "Characters", "import_materials_slots_test");
        Directory.CreateDirectory(Path.Combine(packagePath, "config"));
        Directory.CreateDirectory(Path.Combine(packagePath, "geometry"));
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllBytes(Path.Combine(sourceRoot, "body.fbx"), new byte[] { 1, 2, 3, 4 });
        File.WriteAllBytes(Path.Combine(sourceRoot, "locomotion.glb"), new byte[] { 5, 6, 7, 8 });
        File.WriteAllText(Path.Combine(packagePath, "manifest.json"), JsonSerializer.Serialize(new CharacterPackageManifest
        {
            PackageId = "import_materials_slots_test",
            StableId = "char.import_materials_slots_test",
            DisplayName = "Import Materials Slots Test"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "resource_catalog.json"), JsonSerializer.Serialize(new CharacterPackageResourceCatalog
        {
            Entries = new List<CharacterPackageResourceEntry>
            {
                new CharacterPackageResourceEntry
                {
                    ResourceKey = "char.import_materials_slots_test.model.body",
                    LocalId = "model.body",
                    StableId = "charpkg.import_materials_slots_test.resource.model.body",
                    TypeId = CharacterPackageResourceTypeIds.Model,
                    Usage = CharacterPackageResourceUsageIds.CharacterModel,
                    SourceFormat = CharacterPackageResourceFormatIds.Glb,
                    PackageId = "import_materials_slots_test",
                    RelativePath = "resources/models/placeholder_body.glb",
                    Tags = new List<string> { "body", "weapon", "converted-from-fbx" },
                    ImportHints = new CharacterPackageImportHint
                    {
                        Metadata = new Dictionary<string, string> { ["placeholderResource"] = "true" }
                    },
                    Preview = new CharacterPackagePreviewMetadata
                    {
                        IsPlaceholder = true
                    }
                },
                new CharacterPackageResourceEntry
                {
                    ResourceKey = "char.import_materials_slots_test.anim.locomotion",
                    LocalId = "anim.locomotion",
                    StableId = "charpkg.import_materials_slots_test.resource.anim.locomotion",
                    TypeId = CharacterPackageResourceTypeIds.Animation,
                    Usage = CharacterPackageResourceUsageIds.AnimationClipGroup,
                    SourceFormat = CharacterPackageResourceFormatIds.Glb,
                    PackageId = "import_materials_slots_test",
                    RelativePath = "resources/animations/placeholder_locomotion.glb"
                }
            }
        }, JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "config", "character_application.json"), JsonSerializer.Serialize(new CharacterApplicationAuthoringSummary
        {
            CharacterStableId = "char.import_materials_slots_test"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "geometry", "body_geometry.json"), JsonSerializer.Serialize(new CharacterBodyGeometryProfile
        {
            ProfileId = "body.import_materials_slots_test",
            BodyKind = CharacterAuthoringBodyKind.Primitive,
            HeightMeters = 1.8f,
            RadiusMeters = 0.35f,
            MassKg = 80f
        }, JsonOptions));

        try
        {
            var request = new MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSaveRequest
            {
                package = packageRelative,
                document = new CharacterImportMaterialsDocument
                {
                    SourceRoot = "ArtSource/Characters/import_materials_slots_test",
                    Items = new List<CharacterImportMaterialItem>
                    {
                        new CharacterImportMaterialItem
                        {
                            Id = "body-model",
                            SourcePath = "body.fbx",
                            PresetId = "characterModel",
                            BindTarget = "body",
                            Required = true
                        },
                        new CharacterImportMaterialItem
                        {
                            Id = "locomotion-animation",
                            SourcePath = "locomotion.glb",
                            PresetId = "animationClipGroup",
                            Slot = "locomotion",
                            Required = true
                        }
                    }
                }
            };

            MxFramework.Authoring.Cli.EditorServer.SaveCharacterImportMaterials(tempRoot, packageRelative, packageRelative, request, JsonOptions);
            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSyncResponse sync =
                MxFramework.Authoring.Cli.EditorServer.SyncCharacterImportMaterials(tempRoot, packageRelative, packageRelative, new MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSyncRequest { allChanged = true }, JsonOptions);
            Require(sync.success, "slot import materials sync should succeed without user-entered resource keys.");
            Require(sync.operations.Count == 2, "slot import materials sync should import body and locomotion.");
            Require(sync.operations.Exists(operation => operation.itemId == "body-model" && operation.resourceKey == "char.import_materials_slots_test.model.body"), "body sync should infer the existing body resource key.");
            Require(sync.operations.Exists(operation => operation.itemId == "locomotion-animation" && operation.resourceKey == "char.import_materials_slots_test.anim.locomotion"), "locomotion sync should infer the existing animation resource key.");

            CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, JsonOptions);
            Require(package.ResourceCatalog.Entries.Count(entry => entry.StableId == "charpkg.import_materials_slots_test.resource.model.body") == 1, "body sync should replace the existing slot entry instead of duplicating stableId.");
            CharacterPackageResourceEntry body = CharacterPackageResourcePipeline.FindByKey(package.ResourceCatalog, "char.import_materials_slots_test.model.body");
            Require(body != null && body.RelativePath == "resources/models/body.fbx", "body sync should replace the package model file without converting FBX to GLB.");
            Require(body.SourceFormat == CharacterPackageResourceFormatIds.Fbx, "body sync should keep FBX as the package characterModel source format.");
            Require(!body.Tags.Contains("weapon") && !body.Tags.Contains("converted-from-fbx"), "body sync should remove stale template model tags.");
            Require(body.ImportHints.Metadata.TryGetValue("placeholderResource", out string placeholder) && placeholder == "false", "body sync should clear placeholder metadata.");
            Require(!body.Preview.IsPlaceholder, "body sync should clear placeholder preview metadata.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_ImportMaterialsSync_GeneratesAndAuditsMaterialGraph()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-import-materials-integrity-" + Guid.NewGuid().ToString("N"));
        string packageRelative = "Tools/MxFramework.Authoring/samples/import-materials-integrity-test";
        string packagePath = Path.Combine(tempRoot, "Tools", "MxFramework.Authoring", "samples", "import-materials-integrity-test");
        string sourceRoot = Path.Combine(tempRoot, "ArtSource", "Characters", "import_materials_integrity_test");
        Directory.CreateDirectory(Path.Combine(packagePath, "config"));
        Directory.CreateDirectory(Path.Combine(packagePath, "geometry"));
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllBytes(Path.Combine(sourceRoot, "body.fbx"), new byte[] { 1, 2, 3, 4 });
        File.WriteAllBytes(Path.Combine(sourceRoot, "body_base_map.png"), new byte[] { 5, 6, 7, 8 });
        File.WriteAllBytes(Path.Combine(sourceRoot, "body_normal.png"), new byte[] { 9, 10, 11, 12 });
        File.WriteAllBytes(Path.Combine(sourceRoot, "body_mask_map.png"), new byte[] { 13, 14, 15, 16 });
        File.WriteAllBytes(Path.Combine(sourceRoot, "eye_albedo.png"), new byte[] { 17, 18, 19, 20 });
        File.WriteAllText(Path.Combine(packagePath, "manifest.json"), JsonSerializer.Serialize(new CharacterPackageManifest
        {
            PackageId = "import_materials_integrity_test",
            StableId = "char.import_materials_integrity_test",
            DisplayName = "Import Materials Integrity Test"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "resource_catalog.json"), JsonSerializer.Serialize(new CharacterPackageResourceCatalog(), JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "config", "character_application.json"), JsonSerializer.Serialize(new CharacterApplicationAuthoringSummary
        {
            CharacterStableId = "char.import_materials_integrity_test"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "geometry", "body_geometry.json"), JsonSerializer.Serialize(new CharacterBodyGeometryProfile
        {
            ProfileId = "body.import_materials_integrity_test",
            BodyKind = CharacterAuthoringBodyKind.Primitive,
            HeightMeters = 1.8f,
            RadiusMeters = 0.35f,
            MassKg = 80f
        }, JsonOptions));

        const string bodyKey = "char.import_materials_integrity_test.model.body";
        const string bodyColorKey = "char.import_materials_integrity_test.texture.body_color";
        const string bodyNormalKey = "char.import_materials_integrity_test.texture.body_normal";
        const string bodyMaskKey = "char.import_materials_integrity_test.texture.body_mask";
        const string eyeColorKey = "char.import_materials_integrity_test.texture.eye_color";
        try
        {
            var request = new MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSaveRequest
            {
                package = packageRelative,
                document = new CharacterImportMaterialsDocument
                {
                    SourceRoot = "ArtSource/Characters/import_materials_integrity_test",
                    Items = new List<CharacterImportMaterialItem>
                    {
                        new CharacterImportMaterialItem { Id = "body-model", SourcePath = "body.fbx", PresetId = "characterModel", BindTarget = "body", ResourceKey = bodyKey, Required = true },
                        new CharacterImportMaterialItem
                        {
                            Id = "body-color",
                            SourcePath = "body_base_map.png",
                            PresetId = "texture",
                            Slot = "body",
                            TextureSemantic = CharacterTextureSemanticIds.Color,
                            ResourceKey = bodyColorKey,
                            Required = true,
                            Target = new CharacterSourceTarget
                            {
                                MaterialSurfaceMode = CharacterMaterialSurfaceModeIds.AlphaClip,
                                AlphaCutoff = 0.42f
                            }
                        },
                        new CharacterImportMaterialItem { Id = "body-normal", SourcePath = "body_normal.png", PresetId = "texture", Slot = "body", TextureSemantic = CharacterTextureSemanticIds.Normal, ResourceKey = bodyNormalKey, Required = true },
                        new CharacterImportMaterialItem { Id = "body-mask", SourcePath = "body_mask_map.png", PresetId = "texture", Slot = "body", TextureSemantic = CharacterTextureSemanticIds.Mask, ResourceKey = bodyMaskKey, Required = true },
                        new CharacterImportMaterialItem { Id = "eye-color", SourcePath = "eye_albedo.png", PresetId = "texture", Slot = "eye", TextureSemantic = CharacterTextureSemanticIds.Color, ResourceKey = eyeColorKey, Required = true }
                    }
                }
            };

            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsState saved =
                MxFramework.Authoring.Cli.EditorServer.SaveCharacterImportMaterials(tempRoot, packageRelative, packageRelative, request, JsonOptions);
            Require(saved.integrityReceipt.Status == "Blocked", "an unsynchronized source manifest should not claim that its material graph is ready.");
            Require(saved.integrityReceipt.Diagnostics.Exists(item => item.Code == "BODY_MODEL_RESOURCE_MISSING"), "the raw-material receipt should name a missing body resource.");

            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSyncResponse firstSync =
                MxFramework.Authoring.Cli.EditorServer.SyncCharacterImportMaterials(tempRoot, packageRelative, packageRelative, new MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSyncRequest { allChanged = true }, JsonOptions);
            Require(firstSync.success, "raw-material sync should succeed after generating the complete material graph.");
            Require(firstSync.state.integrityReceipt.Status == "Ready", "the generated material graph should produce a Ready integrity receipt.");
            Require(firstSync.state.integrityReceipt.BlockingIssueCount == 0, "the generated material graph should have no blocking diagnostics.");
            Require(firstSync.state.integrityReceipt.TextureCount == 4, "the integrity receipt should count all four textures.");
            Require(firstSync.state.integrityReceipt.ExpectedMaterialCount == 2 && firstSync.state.integrityReceipt.BoundMaterialCount == 2, "the integrity receipt should prove both Body and Eye materials.");
            Require(firstSync.state.integrityReceipt.RendererBindingCount == 2, "the raw-material receipt should prove both source-material-name bindings.");
            CharacterImportMaterialBindingReceipt bodyReceipt = firstSync.state.integrityReceipt.Materials.Single(item => item.SourceMaterialName == "Body");
            Require(bodyReceipt.SurfaceMode == CharacterMaterialSurfaceModeIds.AlphaClip && Math.Abs(bodyReceipt.AlphaCutoff - 0.42f) < 0.0001f, "the raw-material receipt should preserve Body AlphaClip evidence.");
            Require(firstSync.state.integrityReceipt.Diagnostics.Exists(item => item.Code == "MATERIAL_SURFACE_MODE_RESOLVED" && item.ResourceKey == "char.import_materials_integrity_test.material.body"), "the raw-material log should explain the resolved material surface mode.");

            CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, JsonOptions);
            CharacterPackageResourceEntry body = CharacterPackageResourcePipeline.FindByKey(package.ResourceCatalog, bodyKey);
            CharacterPackageResourceEntry bodyMaterial = CharacterPackageResourcePipeline.FindByKey(package.ResourceCatalog, "char.import_materials_integrity_test.material.body");
            CharacterPackageResourceEntry eyeMaterial = CharacterPackageResourcePipeline.FindByKey(package.ResourceCatalog, "char.import_materials_integrity_test.material.eye");
            Require(body != null && body.Dependencies.Count(item => item.Relation == "usesMaterial") == 2, "the body model should depend on both generated materials.");
            Require(body.ImportHints.MaterialPolicy == "generateFromPackageMaterialDescriptors", "the body model should select package material descriptor generation.");
            Require(body.ImportHints.Metadata.ContainsKey("rendererMaterialBindingsJson"), "the body model should store source-material-name bindings.");
            Require(bodyMaterial != null && bodyMaterial.Dependencies.Count(item => item.Relation == "usesTexture") == 3, "Body should depend on Color, Normal, and Mask textures.");
            Require(eyeMaterial != null && eyeMaterial.Dependencies.Count(item => item.Relation == "usesTexture") == 1, "Eye should depend on its Color texture.");
            Require(File.Exists(Path.Combine(packagePath, bodyMaterial.RelativePath.Replace('/', Path.DirectorySeparatorChar))), "Body material descriptor should be generated.");
            Require(File.Exists(Path.Combine(packagePath, eyeMaterial.RelativePath.Replace('/', Path.DirectorySeparatorChar))), "Eye material descriptor should be generated.");

            string bodyDescriptorPath = Path.Combine(packagePath, bodyMaterial.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            using (JsonDocument bodyDescriptor = JsonDocument.Parse(File.ReadAllText(bodyDescriptorPath)))
            {
                Require(bodyDescriptor.RootElement.GetProperty("surfaceMode").GetString() == CharacterMaterialSurfaceModeIds.AlphaClip, "the generated descriptor should declare AlphaClip.");
                Require(Math.Abs(bodyDescriptor.RootElement.GetProperty("alphaCutoff").GetSingle() - 0.42f) < 0.0001f, "the generated descriptor should preserve alphaCutoff.");
                Require(bodyDescriptor.RootElement.GetProperty("renderQueue").GetInt32() == 2450, "the generated AlphaClip descriptor should use the AlphaTest queue.");
            }
            File.Delete(bodyDescriptorPath);
            body.Dependencies.Clear();
            body.ImportHints.Metadata.Remove("rendererMaterialBindingsJson");
            body.ImportHints.Metadata.Remove("sourceRendererMaterialBindingCount");
            bodyMaterial.Dependencies.Clear();
            File.WriteAllText(Path.Combine(packagePath, "resource_catalog.json"), JsonSerializer.Serialize(package.ResourceCatalog, JsonOptions));

            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsState broken =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterImportMaterials(tempRoot, packageRelative, packageRelative, JsonOptions);
            Require(broken.integrityReceipt.Status == "Blocked", "missing material relationships should block raw-material readiness even when every source hash is clean.");
            Require(broken.integrityReceipt.Diagnostics.Exists(item => item.Code == "MATERIAL_DESCRIPTOR_MISSING"), "the receipt should name a missing material descriptor.");
            Require(broken.integrityReceipt.Diagnostics.Exists(item => item.Code == "DEPENDENCY_GRAPH_INCOMPLETE"), "the receipt should name an incomplete dependency graph.");
            Require(broken.integrityReceipt.Diagnostics.Exists(item => item.Code == "RENDERER_BINDING_INCOMPLETE"), "the receipt should name missing source-material bindings.");

            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSyncResponse repaired =
                MxFramework.Authoring.Cli.EditorServer.SyncCharacterImportMaterials(tempRoot, packageRelative, packageRelative, new MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSyncRequest { allChanged = true }, JsonOptions);
            Require(repaired.operations.Count == 0, "repairing a clean source set should not recopy source files.");
            Require(repaired.success && repaired.state.integrityReceipt.Status == "Ready", "sync should repair material relationships and return Ready.");
            Require(repaired.diagnostics.Exists(item => item.Code == "DEPENDENCY_GRAPH_RECONCILED"), "sync should log dependency-graph reconciliation.");

            MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsState reloaded =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterImportMaterials(tempRoot, packageRelative, packageRelative, JsonOptions);
            Require(reloaded.integrityReceipt.Diagnostics.Exists(item => item.Code == "DEPENDENCY_GRAPH_RECONCILED"), "reconciliation evidence should remain visible after reloading CharacterStudio state.");
            CharacterImportMaterialsStateDocument persisted = JsonSerializer.Deserialize<CharacterImportMaterialsStateDocument>(
                File.ReadAllText(Path.Combine(packagePath, "config", "import_materials_state.json")), JsonOptions);
            Require(persisted != null && persisted.SchemaVersion == "2.0" && persisted.IntegrityReceipt.Status == "Ready", "the package-local v2 state should persist the current raw-material receipt.");

            package = CharacterPackageCommands.ReadPackage(packagePath, JsonOptions);
            eyeMaterial = CharacterPackageResourcePipeline.FindByKey(package.ResourceCatalog, "char.import_materials_integrity_test.material.eye");
            eyeMaterial.ImportHints.Metadata.Remove("rawMaterialDescriptorOwner");
            File.WriteAllText(Path.Combine(packagePath, "resource_catalog.json"), JsonSerializer.Serialize(package.ResourceCatalog, JsonOptions));
            string eyeDescriptorPath = Path.Combine(packagePath, eyeMaterial.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            string manualEyeDescriptor = "{\"sourceAssetPath\":\"Eye.mat\",\"textures\":[{\"property\":\"_BaseMap\",\"resourceKey\":\"" + eyeColorKey + "\"}],\"customMarker\":\"preserve\"}";
            File.WriteAllText(eyeDescriptorPath, manualEyeDescriptor);
            MxFramework.Authoring.Cli.EditorServer.SyncCharacterImportMaterials(tempRoot, packageRelative, packageRelative, new MxFramework.Authoring.Cli.EditorServer.CharacterImportMaterialsSyncRequest { allChanged = true }, JsonOptions);
            Require(File.ReadAllText(eyeDescriptorPath) == manualEyeDescriptor, "sync should preserve an existing material descriptor that is not tool-owned.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_CommonResourcesApi_SyncsPublicModelsIntoGlobalProfile()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-common-resources-" + Guid.NewGuid().ToString("N"));
        string commonCharacterRoot = Path.Combine(tempRoot, "ArtSource", "Common", "Characters", "common_character_1");
        string commonWeaponRoot = Path.Combine(tempRoot, "ArtSource", "Common", "Weapons", "common_weapon_1");
        string commonTextureRoot = Path.Combine(tempRoot, "ArtSource", "Common", "Textures", "common_body_albedo");
        Directory.CreateDirectory(Path.Combine(commonCharacterRoot, "model"));
        Directory.CreateDirectory(Path.Combine(commonWeaponRoot, "model"));
        Directory.CreateDirectory(Path.Combine(commonWeaponRoot, "textures"));
        Directory.CreateDirectory(Path.Combine(commonTextureRoot, "textures"));
        File.WriteAllBytes(Path.Combine(commonCharacterRoot, "model", "common_character_1.fbx"), new byte[] { 1, 2, 3, 4 });
        File.WriteAllText(Path.Combine(commonWeaponRoot, "model", "common_weapon_1.fbx"),
            "FBXHeaderExtension:  {\n\tGlobalSettings:  {\n\t\tProperties70:  {\n\t\t\tP: \"UnitScaleFactor\", \"double\", \"Number\", \"\",1\n\t\t}\n\t}\n}\n");
        File.WriteAllBytes(Path.Combine(commonWeaponRoot, "textures", "weapon_albedo.png"), new byte[] { 5, 6, 7, 8 });
        File.WriteAllBytes(Path.Combine(commonTextureRoot, "textures", "body_albedo.png"), new byte[] { 9, 10, 11, 12 });
        string characterTestScene = Path.Combine(tempRoot, "Assets", "Scenes", "CharacterTestScene.unity");
        Directory.CreateDirectory(Path.GetDirectoryName(characterTestScene)!);
        File.WriteAllText(characterTestScene, "scene placeholder");
        File.WriteAllText(characterTestScene + ".meta", "fileFormatVersion: 2\nguid: 55555555555555555555555555555555\n");
        string profilePath = Path.Combine(tempRoot, "Assets", "Config", "MxFramework", "ResourceProfiles", "global_resource_mapping_profile.v2.json");
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
        ResourceMappingProducerSnapshot policy = new ResourceMappingProfilePolicyProducerAdapter().BuildSnapshot(tempRoot).Snapshot;
        for (int i = 0; i < policy.BundleRules.Count; i++)
        {
            ResourceMappingBundleRule rule = policy.BundleRules[i];
            rule.AllowEmpty = rule.Id != "common.models" && rule.Id != "common.textures" && rule.Id != "scene.character_test";
        }
        for (int i = 0; i < policy.PreloadGroups.Count; i++)
        {
            ResourceMappingPreloadGroup group = policy.PreloadGroups[i];
            group.AllowEmpty = group.Id != "SpawnCritical" && group.Id != "EquipmentInitial";
        }
        var initialProfile = new ResourceMappingProfileV2
        {
            ProfileId = "global.default",
            CatalogId = "global.runtime",
            BundleRules = policy.BundleRules,
            PreloadGroups = policy.PreloadGroups
        };
        File.WriteAllText(profilePath, new GlobalResourceBuildPlanCompiler().SerializeCanonicalProfile(initialProfile));
        File.WriteAllText(Path.Combine(commonCharacterRoot, "character_sources.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            characterId = "common_character_1",
            displayName = "Common Character 1",
            sourceRoot = "ArtSource/Common",
            items = new[]
            {
                new
                {
                    id = "body-model",
                    label = "Body model",
                    presetId = "characterModel",
                    bindTarget = "body",
                    required = true,
                    source = new { kind = "file", scope = "public", path = "Characters/common_character_1/model/common_character_1.fbx" },
                    target = new { resourceKey = "character.common_character_1.model", resourceKind = "Model", resourceUsage = "characterModel", bindTarget = "body", slot = "" }
                }
            }
        }, JsonOptions));
        File.WriteAllText(Path.Combine(commonWeaponRoot, "weapon_sources.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            weaponId = "common_weapon_1",
            displayName = "Common Weapon 1",
            paletteMode = "sharedWGameAtlas",
            sourceRoot = "ArtSource/Common",
            items = new object[]
            {
                new
                {
                    id = "weapon-model",
                    label = "Weapon model",
                    presetId = "weaponModel",
                    required = true,
                    source = new { kind = "file", scope = "public", path = "Weapons/common_weapon_1/model/common_weapon_1.fbx" },
                    target = new
                    {
                        resourceKey = "weapon.common_weapon_1.model",
                        resourceKind = "Model",
                        resourceUsage = "weaponModel",
                        bindTarget = "",
                        slot = "",
                        importHints = new
                        {
                            modelWrapperPose = new
                            {
                                position = new { x = 0f, y = -0.125054f, z = 0f },
                                rotation = new { x = 0f, y = 0.70710677f, z = 0f, w = 0.70710677f },
                                scale = new { x = 1f, y = 1f, z = 1f }
                            }
                        }
                    }
                },
                new
                {
                    id = "weapon-albedo",
                    label = "Weapon albedo",
                    presetId = "texture",
                    required = false,
                    source = new { kind = "file", scope = "public", path = "Weapons/common_weapon_1/textures/weapon_albedo.png" },
                    target = new { resourceKey = "weapon.common_weapon_1.texture.albedo", resourceKind = "Texture", resourceUsage = "texture", bindTarget = "weapon.albedo", slot = "" }
                }
            }
        }, JsonOptions));
        File.WriteAllText(Path.Combine(commonTextureRoot, "resource_sources.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            resourceId = "common_body_albedo",
            displayName = "Common Body Albedo",
            sourceRoot = "ArtSource/Common",
            items = new[]
            {
                new
                {
                    id = "body-albedo",
                    label = "Body albedo",
                    presetId = "texture",
                    required = true,
                    source = new { kind = "file", scope = "public", path = "Textures/common_body_albedo/textures/body_albedo.png" },
                    target = new { resourceKey = "texture.common_body_albedo", resourceKind = "Texture", resourceUsage = "texture" }
                }
            }
        }, JsonOptions));

        try
        {
            MxFramework.Authoring.Cli.EditorServer.CommonResourceState initial =
                MxFramework.Authoring.Cli.EditorServer.ReadCommonResources(tempRoot, JsonOptions);
            Require(initial.summary.total == 4, "common resource scan should discover character, weapon, weapon texture, and texture manifests.");
            Require(initial.items.Exists(item => item.resourceKey == "character.common_character_1.model" && item.status == "MissingGenerated"), "common character model should require generated copy before sync.");
            Require(initial.items.Exists(item => item.resourceKey == "weapon.common_weapon_1.model" && item.status == "MissingGenerated"), "common weapon model should require generated copy before sync.");
            Require(initial.items.Exists(item => item.resourceKey == "weapon.common_weapon_1.texture.albedo" && item.resourceKind == "Texture" && item.usage == CharacterPackageResourceUsageIds.Texture && item.bindTarget == "weapon.albedo"), "weapon_sources.json should support an optional weapon albedo texture item.");
            Require(initial.items.Exists(item => item.resourceKey == "weapon.common_weapon_1.model" && item.paletteMode == "sharedWGameAtlas"), "weapon_sources.json should expose the shared palette mode in common-resource state.");
            Require(initial.items.Exists(item => item.resourceKey == "texture.common_body_albedo" && item.resourceKind == "Texture" && item.usage == CharacterPackageResourceUsageIds.Texture), "common texture should be classified as a public texture resource.");
            Require(initial.items.Exists(item => item.resourceKey == "weapon.common_weapon_1.model" && item.modelScaleDiagnostic.status == "PreviewUnitScale" && Math.Abs(item.modelScaleDiagnostic.previewUnitScale - 0.01f) < 0.0001f && Math.Abs(item.modelScaleDiagnostic.recommendedScale - 1f) < 0.0001f), "common weapon FBX UnitScaleFactor=1 should use 0.01 only as a CharacterStudio preview unit scale.");

            MxFramework.Authoring.Cli.EditorServer.CommonResourceSyncResponse missingTarget =
                MxFramework.Authoring.Cli.EditorServer.SyncCommonResources(tempRoot, new MxFramework.Authoring.Cli.EditorServer.CommonResourceSyncRequest { allChanged = true }, JsonOptions);
            Require(!missingTarget.success && missingTarget.operations.Exists(operation => operation.code == "RESOURCE_MAPPING_BUILD_TARGET_REQUIRED"),
                "common resource sync should reject a missing concrete build target before mutation.");
            string generatedCharacter = Path.Combine(tempRoot, "Assets", "MxFrameworkGenerated", "Common", "Characters", "common_character_1", "model", "common_character_1.fbx");
            Require(!File.Exists(generatedCharacter), "missing build-target evidence must not leave a partially synchronized common resource.");

            MxFramework.Authoring.Cli.EditorServer.CommonResourceSyncResponse firstSync =
                MxFramework.Authoring.Cli.EditorServer.SyncCommonResources(tempRoot, new MxFramework.Authoring.Cli.EditorServer.CommonResourceSyncRequest { allChanged = true, buildTarget = "StandaloneOSX" }, JsonOptions);
            Require(firstSync.success, "common resource sync should copy source files and register profile entries: " +
                string.Join("; ", firstSync.operations.ConvertAll(operation => operation.code + "=" + operation.message)));
            string generatedWeapon = Path.Combine(tempRoot, "Assets", "MxFrameworkGenerated", "Common", "Weapons", "common_weapon_1", "model", "common_weapon_1.fbx");
            string generatedWeaponWrapper = Path.Combine(tempRoot, "Assets", "MxFrameworkGenerated", "Common", "Weapons", "common_weapon_1", "prefabs", "common_weapon_1_weapon.prefab");
            string generatedWeaponAlbedo = Path.Combine(tempRoot, "Assets", "MxFrameworkGenerated", "Common", "Weapons", "common_weapon_1", "textures", "weapon_albedo.png");
            string generatedTexture = Path.Combine(tempRoot, "Assets", "MxFrameworkGenerated", "Common", "Textures", "common_body_albedo", "textures", "body_albedo.png");
            Require(File.Exists(generatedCharacter), "common character source should be copied into generated common assets.");
            Require(File.Exists(generatedWeapon), "common weapon source should be copied into generated common assets.");
            Require(File.Exists(generatedWeaponAlbedo), "common weapon albedo source should be copied into generated common weapon assets.");
            Require(File.Exists(generatedTexture), "common texture source should be copied into generated common assets.");
            Require(File.Exists(Path.Combine(tempRoot, "Assets", "MxFrameworkGenerated", "Common", "common_resource_import_state.json")), "common sync should write tool state outside source manifests.");

            ResourceMappingProfileReadResult firstProfileRead = ResourceMappingProfileJson.ReadStrict(File.ReadAllText(profilePath));
            List<ResourceMappingEntry> firstDemoEntries = firstProfileRead.Profile.Entries.FindAll(entry => entry.ProducerReceipt?.ProducerId == "demo.samples.global-profile");
            Require(firstProfileRead.Success && firstDemoEntries.Count == 5,
                "production Profile should contain the complete demo/common producer snapshot, including the scene row.");
            Require(firstDemoEntries.TrueForAll(entry => entry.DeliveryIntent == ResourceMappingDeliveryIntent.Internal),
                "pending common imports should retain runtime identity and policy while GeneratedUnityAsset records await Unity GUIDs.");
            Require(firstDemoEntries.FindAll(entry => entry.SourceLocator?.Kind == ResourceMappingSourceLocatorKind.GeneratedUnityAsset).Count == 4,
                "common imports without Unity GUIDs should be represented as pending generated assets, not as a different delivery mode.");
            Require(firstDemoEntries.Exists(entry => entry.ResourceKey?.Id == "scene.character_test" && entry.DeliveryIntent == ResourceMappingDeliveryIntent.Internal),
                "common resource synchronization must preserve the demo producer's scene authority.");
            ResourceMappingEntry pendingWeapon = firstDemoEntries.Find(entry => entry.ResourceKey?.Id == "weapon.common_weapon_1.model");
            Require(pendingWeapon != null &&
                pendingWeapon.ProviderData.TryGetValue("rawGeneratedAssetPath", out string pendingRawWeaponPath) &&
                pendingRawWeaponPath.EndsWith("/common_weapon_1.fbx", StringComparison.Ordinal) &&
                pendingWeapon.ProviderData.TryGetValue("modelWrapperPose", out string pendingWeaponPose) &&
                pendingWeaponPose.Contains("-0.125054", StringComparison.Ordinal) &&
                pendingWeapon.ProviderData.TryGetValue("paletteMode", out string pendingPaletteMode) &&
                pendingPaletteMode == "sharedWGameAtlas",
                "common weapon producer should carry raw-model, wrapper-pose, and palette-mode metadata before Unity builds the wrapper prefab.");

            File.WriteAllText(generatedCharacter + ".meta", "fileFormatVersion: 2\nguid: 11111111111111111111111111111111\n");
            Directory.CreateDirectory(Path.GetDirectoryName(generatedWeaponWrapper)!);
            File.WriteAllText(generatedWeaponWrapper, "prefab placeholder");
            File.WriteAllText(generatedWeapon + ".meta", "fileFormatVersion: 2\nguid: raw22222222222222222222222222222\nModelImporter:\n  meshes:\n    useFileUnits: 1\n    useFileScale: 1\n");
            File.WriteAllText(generatedWeaponWrapper + ".meta", "fileFormatVersion: 2\nguid: 22222222222222222222222222222222\n");
            File.WriteAllText(generatedWeaponAlbedo + ".meta", "fileFormatVersion: 2\nguid: 44444444444444444444444444444444\n");
            File.WriteAllText(generatedTexture + ".meta", "fileFormatVersion: 2\nguid: 33333333333333333333333333333333\n");
            MxFramework.Authoring.Cli.EditorServer.CommonResourceSyncResponse secondSync =
                MxFramework.Authoring.Cli.EditorServer.SyncCommonResources(tempRoot, new MxFramework.Authoring.Cli.EditorServer.CommonResourceSyncRequest { allChanged = true, buildTarget = "StandaloneOSX" }, JsonOptions);
            Require(secondSync.success, "common resource sync should refresh profile entries after Unity import writes meta files.");
            ResourceMappingProfileV2 profile = ResourceMappingProfileJson.ReadStrict(File.ReadAllText(profilePath)).Profile;
            Require(profile.Entries.Exists(entry => entry.ResourceKey?.Id == "character.common_character_1.model" && entry.DeliveryIntent == ResourceMappingDeliveryIntent.Internal && entry.SourceLocator?.UnityGuid == "11111111111111111111111111111111"), "common character model should become runtime-loadable after Unity guid is known.");
            Require(profile.Entries.Exists(entry => entry.ResourceKey?.Id == "weapon.common_weapon_1.model" && entry.DeliveryIntent == ResourceMappingDeliveryIntent.Internal && entry.SourceLocator?.UnityGuid == "22222222222222222222222222222222"), "common weapon model should become runtime-loadable after Unity guid is known.");
            ResourceMappingEntry readyWeapon = profile.Entries.Find(entry => entry.ResourceKey?.Id == "weapon.common_weapon_1.model");
            Require(readyWeapon != null &&
                readyWeapon.ProviderData.TryGetValue("generatedAssetPath", out string readyWeaponPath) &&
                readyWeaponPath.EndsWith("/common_weapon_1_weapon.prefab", StringComparison.Ordinal) &&
                readyWeapon.ProviderData.TryGetValue("modelWrapperPose", out string readyWeaponPose) &&
                readyWeaponPose.Contains("-0.125054", StringComparison.Ordinal) &&
                readyWeapon.ProviderData.TryGetValue("paletteMode", out string readyPaletteMode) &&
                readyPaletteMode == "sharedWGameAtlas",
                "common weapon producer should retain wrapper identity, pose, and palette-mode metadata after Unity import.");
            string readyWeaponFingerprint = readyWeapon.ProducerReceipt.SourceRecordFingerprint;
            Require(profile.Entries.Exists(entry => entry.ResourceKey?.Id == "weapon.common_weapon_1.texture.albedo" && entry.ResourceKey?.TypeId == "Texture2D" && entry.SourceLocator?.UnityGuid == "44444444444444444444444444444444"), "common weapon albedo should become runtime-loadable after Unity guid is known.");
            Require(profile.Entries.Exists(entry => entry.ResourceKey?.Id == "texture.common_body_albedo" && entry.ResourceKey?.TypeId == "Texture2D" && entry.SourceLocator?.UnityGuid == "33333333333333333333333333333333"), "common texture should become runtime-loadable after Unity guid is known.");
            Require(profile.BundleRules.Exists(rule => rule.Id == "common.models"), "common resources should use the central common models bundle rule.");
            Require(profile.BundleRules.Exists(rule => rule.Id == "common.textures"), "common resources should use the central common textures bundle rule.");

            ResourceMappingEntry preserved = profile.Entries.Find(entry => entry.ResourceKey?.Id == "character.common_character_1.model");
            ResourceMappingEntry dependency = profile.Entries.Find(entry => entry.ResourceKey?.Id == "texture.common_body_albedo");
            preserved.Dependencies.Add(new ResourceMappingResourceKey
            {
                Id = dependency.ResourceKey.Id,
                TypeId = dependency.ResourceKey.TypeId,
                Variant = dependency.ResourceKey.Variant,
                PackageId = dependency.ResourceKey.PackageId
            });
            preserved.ProviderData["customSemantic"] = "preserve";
            preserved.Labels.Add("custom.preserved");
            File.WriteAllText(profilePath, new GlobalResourceBuildPlanCompiler().SerializeCanonicalProfile(profile));
            MxFramework.Authoring.Cli.EditorServer.CommonResourceSyncResponse thirdSync =
                MxFramework.Authoring.Cli.EditorServer.SyncCommonResources(tempRoot, new MxFramework.Authoring.Cli.EditorServer.CommonResourceSyncRequest { force = true, buildTarget = "StandaloneOSX" }, JsonOptions);
            Require(thirdSync.success, "forced common resource refresh should publish the same complete demo producer snapshot.");
            ResourceMappingEntry refreshed = ResourceMappingProfileJson.ReadStrict(File.ReadAllText(profilePath)).Profile.Entries.Find(
                entry => entry.ResourceKey?.Id == "character.common_character_1.model");
            Require(refreshed.Dependencies.Count == 1 && refreshed.Dependencies[0].Id == "texture.common_body_albedo" &&
                refreshed.ProviderData.TryGetValue("customSemantic", out string semantic) && semantic == "preserve" &&
                refreshed.Labels.Contains("custom.preserved"),
                "complete demo producer refresh should preserve dependency, provider extension, and label semantics for an unchanged Unity GUID.");

            string weaponManifestPath = Path.Combine(commonWeaponRoot, "weapon_sources.json");
            File.WriteAllText(
                weaponManifestPath,
                File.ReadAllText(weaponManifestPath).Replace("-0.125054", "-0.25", StringComparison.Ordinal));
            MxFramework.Authoring.Cli.EditorServer.CommonResourceSyncResponse poseSync =
                MxFramework.Authoring.Cli.EditorServer.SyncCommonResources(tempRoot, new MxFramework.Authoring.Cli.EditorServer.CommonResourceSyncRequest { force = true, buildTarget = "StandaloneOSX" }, JsonOptions);
            Require(poseSync.success, "common resource refresh should republish changed wrapper-pose metadata.");
            ResourceMappingEntry poseUpdatedWeapon = ResourceMappingProfileJson.ReadStrict(File.ReadAllText(profilePath)).Profile.Entries.Find(
                entry => entry.ResourceKey?.Id == "weapon.common_weapon_1.model");
            Require(poseUpdatedWeapon != null &&
                poseUpdatedWeapon.ProviderData.TryGetValue("modelWrapperPose", out string poseUpdatedJson) &&
                poseUpdatedJson.Contains("-0.25", StringComparison.Ordinal) &&
                !string.Equals(poseUpdatedWeapon.ProducerReceipt.SourceRecordFingerprint, readyWeaponFingerprint, StringComparison.Ordinal),
                "wrapper-pose changes should update both provider metadata and the producer source fingerprint.");

            MxFramework.Authoring.Cli.EditorServer.CommonResourceState ready =
                MxFramework.Authoring.Cli.EditorServer.ReadCommonResources(tempRoot, JsonOptions);
            Require(ready.summary.ready == 4, "common resources should report ready after generated files, profile entries, and Unity guids match.");
            Require(ready.items.Exists(item => item.resourceKey == "weapon.common_weapon_1.model" && item.modelScaleDiagnostic.status == "UnityConvertsUnits" && item.modelScaleDiagnostic.unityConvertsUnits && Math.Abs(item.modelScaleDiagnostic.recommendedScale - 1f) < 0.0001f), "common weapon FBX Convert Units should keep wrapper scale recommendations at 1.");
            string characterSourceManifest = File.ReadAllText(Path.Combine(commonCharacterRoot, "character_sources.json"));
            Require(!characterSourceManifest.Contains("lastImportedHash", StringComparison.Ordinal), "common source manifest should not persist sync hashes.");
            Require(!characterSourceManifest.Contains("unityGuid", StringComparison.Ordinal), "common source manifest should not persist Unity import status.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_CharacterActionOpenApi_WritesRequestAndStatus()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-character-action-open-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var request = new MxFramework.Authoring.Cli.EditorServer.CharacterActionOpenRequest
            {
                package = "Tools/MxFramework.Authoring/samples/character-codex-flow-test",
                abilityId = 900001,
                profileSourceDirectory = "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test",
                actionSetSourceDirectory = "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test",
                actionLibrarySourceDirectory = "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common",
                actionSetStableId = "codex_flow_test.default",
                mode = "focusOnly"
            };

            MxFramework.Authoring.Cli.EditorServer.CharacterActionOpenResponse response =
                MxFramework.Authoring.Cli.EditorServer.WriteCharacterActionOpenRequest(tempRoot, request, JsonOptions);

            Require(response.abilityId == 900001, "open response should preserve abilityId.");
            Require(response.status == "pending", "open response should start pending.");
            Require(!string.IsNullOrWhiteSpace(response.requestId), "open response should include request id.");
            Require(File.Exists(Path.Combine(tempRoot, response.requestPath)), "open request should be written under Temp.");
            Require(response.requestPath.StartsWith("Temp/MxFrameworkAuthoring/CharacterAction/", StringComparison.Ordinal), "open request should stay under Temp.");

            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(tempRoot, response.requestPath))))
            {
                JsonElement root = doc.RootElement;
                Require(root.GetProperty("abilityId").GetInt32() == 900001, "open request file should contain abilityId.");
                Require(root.GetProperty("mode").GetString() == "focusOnly", "open request file should contain focusOnly mode.");
                Require(root.GetProperty("profileSourceDirectory").GetString() == "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test", "open request file should target codex_flow_test profile source.");
                Require(root.GetProperty("actionSetSourceDirectory").GetString() == "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test", "open request file should target codex_flow_test action set source.");
                Require(root.GetProperty("actionSetStableId").GetString() == "codex_flow_test.default", "open request file should carry codex_flow_test action set id.");
            }

            MxFramework.Authoring.Cli.EditorServer.CharacterActionOpenResponse defaultResponse =
                MxFramework.Authoring.Cli.EditorServer.WriteCharacterActionOpenRequest(
                    tempRoot,
                    new MxFramework.Authoring.Cli.EditorServer.CharacterActionOpenRequest { abilityId = 900002 },
                    JsonOptions);
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(tempRoot, defaultResponse.requestPath))))
            {
                JsonElement root = doc.RootElement;
                Require(root.GetProperty("profileSourceDirectory").GetString() == "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test", "empty open request should fall back to codex_flow_test profile source.");
                Require(root.GetProperty("actionSetSourceDirectory").GetString() == "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test", "empty open request should fall back to codex_flow_test action set source.");
            }

            MxFramework.Authoring.Cli.EditorServer.CharacterActionOpenStatus pending =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterActionOpenStatus(tempRoot, response.requestId, JsonOptions);
            Require(pending.status == "pending", "status should be pending before Unity consumes request.");

            MxFramework.Authoring.Cli.EditorServer.WriteCharacterActionOpenStatus(
                tempRoot,
                new MxFramework.Authoring.Cli.EditorServer.CharacterActionOpenStatus
                {
                    requestId = response.requestId,
                    status = "opened",
                    message = "opened for test",
                    abilityId = 900001
                },
                JsonOptions);
            MxFramework.Authoring.Cli.EditorServer.CharacterActionOpenStatus opened =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterActionOpenStatus(tempRoot, response.requestId, JsonOptions);
            Require(opened.status == "opened", "status should read Unity opened result.");
            Require(opened.abilityId == 900001, "opened status should preserve abilityId.");

            bool invalidAbilityRejected = false;
            try
            {
                MxFramework.Authoring.Cli.EditorServer.WriteCharacterActionOpenRequest(
                    tempRoot,
                    new MxFramework.Authoring.Cli.EditorServer.CharacterActionOpenRequest { abilityId = 0 },
                    JsonOptions);
            }
            catch (ArgumentException)
            {
                invalidAbilityRejected = true;
            }
            Require(invalidAbilityRejected, "open request should reject invalid abilityId.");

            bool escapeRejected = false;
            try
            {
                MxFramework.Authoring.Cli.EditorServer.WriteCharacterActionOpenRequest(
                    tempRoot,
                    new MxFramework.Authoring.Cli.EditorServer.CharacterActionOpenRequest
                    {
                        abilityId = 900001,
                        actionSetSourceDirectory = "../outside"
                    },
                    JsonOptions);
            }
            catch (ArgumentException)
            {
                escapeRejected = true;
            }
            Require(escapeRejected, "open request should reject source paths that escape the project root.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_CharacterUnityImportEditorStatus_PreservesWeaponTraceBakeFields()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-character-unity-import-status-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            MxFramework.Authoring.Cli.EditorServer.CharacterUnityImportEditorRequestResponse response =
                MxFramework.Authoring.Cli.EditorServer.WriteCharacterUnityImportEditorRequest(
                    tempRoot,
                    "Tools/MxFramework.Authoring/samples/character-codex-flow-test",
                    "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test",
                    new CharacterUnityImportReport
                    {
                        PackageId = "codex_flow_test",
                        SourcePackageHash = "sha256:source",
                        GeneratedConfigHash = "sha256:config",
                        GeometryBindingHash = "sha256:geometry",
                        ResourceMappingHash = "sha256:resources",
                        WritePlanHash = "sha256:plan"
                    },
                    JsonOptions);

            using (JsonDocument requestDocument = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(tempRoot, response.requestPath))))
            {
                JsonElement requestRoot = requestDocument.RootElement;
                Require(requestRoot.GetProperty("packageId").GetString() == "codex_flow_test",
                    "Unity import editor request should include packageId.");
                Require(requestRoot.GetProperty("sourcePackageHash").GetString() == "sha256:source",
                    "Unity import editor request should include the expected source hash.");
                Require(requestRoot.GetProperty("generatedConfigHash").GetString() == "sha256:config",
                    "Unity import editor request should include the expected generated config hash.");
                Require(requestRoot.GetProperty("geometryBindingHash").GetString() == "sha256:geometry" &&
                        requestRoot.GetProperty("resourceMappingHash").GetString() == "sha256:resources" &&
                        requestRoot.GetProperty("writePlanHash").GetString() == "sha256:plan",
                    "Unity import editor request should include all five expected compiler hashes.");
            }

            bool missingHashesRejected = false;
            try
            {
                MxFramework.Authoring.Cli.EditorServer.WriteCharacterUnityImportEditorRequest(
                    tempRoot,
                    "Tools/MxFramework.Authoring/samples/character-codex-flow-test",
                    "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test",
                    new CharacterUnityImportReport { PackageId = "codex_flow_test" },
                    JsonOptions);
            }
            catch (ArgumentException)
            {
                missingHashesRejected = true;
            }
            Require(missingHashesRejected,
                "Unity import editor request should reject a report missing any expected compiler hash.");

            string statusPath = Path.Combine(tempRoot, response.statusPath);
            var unityStatus = new
            {
                requestId = response.requestId,
                status = "completed",
                message = "rebuilt for test",
                package = "Tools/MxFramework.Authoring/samples/character-codex-flow-test",
                generatedRoot = "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test",
                packageId = "codex_flow_test",
                prefabPath = "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test/prefabs/codex_flow_test_character_preview.prefab",
                sourcePackageHash = "sha256:source",
                generatedConfigHash = "sha256:config",
                geometryBindingHash = "sha256:geometry",
                resourceMappingHash = "sha256:resources",
                writePlanHash = "sha256:plan",
                weaponTraceBakeStatus = "Ready",
                weaponTraceBakeHash = "sha256:test-trace-bake",
                weaponTraceBakeRequestCount = 2,
                weaponTraceBakeArtifactCount = 2,
                weaponTraceBakeCheckedFrameCount = 12,
                weaponTraceBakeIssueCount = 1,
                weaponTraceBakeReady = true,
                weaponTraceBakeIssues = new[]
                {
                    new
                    {
                        code = "WeaponTraceBakeFrameMissing",
                        message = "frame missing for test",
                        actionStableId = "action.primary",
                        clipKey = "primary",
                        traceId = "trace.iron_sword.blade",
                        frame = 3,
                        sourcePath = "generated/weapon_trace_bake_catalog.json",
                        sourceObjectPath = "artifacts/0"
                    }
                },
                updatedUtc = DateTimeOffset.UtcNow.ToString("O")
            };
            File.WriteAllText(statusPath, JsonSerializer.Serialize(unityStatus, JsonOptions));

            MxFramework.Authoring.Cli.EditorServer.CharacterUnityImportEditorStatus status =
                MxFramework.Authoring.Cli.EditorServer.ReadCharacterUnityImportEditorStatus(
                    tempRoot,
                    response.requestId,
                    JsonOptions);

            Require(status.status == "completed", "Unity import editor status should read the completed bridge status.");
            Require(status.packageId == "codex_flow_test", "Unity import editor status should preserve packageId.");
            Require(status.generatedConfigHash == "sha256:config", "Unity import editor status should preserve generated config hash.");
            Require(status.weaponTraceBakeStatus == "Ready", "Unity import editor status should preserve weapon trace bake status.");
            Require(status.weaponTraceBakeHash == "sha256:test-trace-bake", "Unity import editor status should preserve weapon trace bake hash.");
            Require(status.weaponTraceBakeRequestCount == 2, "Unity import editor status should preserve weapon trace bake request count.");
            Require(status.weaponTraceBakeArtifactCount == 2, "Unity import editor status should preserve weapon trace bake artifact count.");
            Require(status.weaponTraceBakeCheckedFrameCount == 12, "Unity import editor status should preserve weapon trace bake checked frame count.");
            Require(status.weaponTraceBakeIssueCount == 1, "Unity import editor status should preserve weapon trace bake issue count.");
            Require(status.weaponTraceBakeReady, "Unity import editor status should preserve weapon trace bake ready flag.");
            Require(status.weaponTraceBakeIssues.Length == 1, "Unity import editor status should preserve weapon trace bake issues.");
            Require(status.weaponTraceBakeIssues[0].traceId == "trace.iron_sword.blade", "Unity import editor status should preserve weapon trace bake issue trace id.");
            Require(status.weaponTraceBakeIssues[0].frame == 3, "Unity import editor status should preserve weapon trace bake issue frame.");

            MxFramework.Authoring.Cli.EditorServer.CharacterUnityImportEditorStatus latest =
                MxFramework.Authoring.Cli.EditorServer.ReadLatestCharacterUnityImportEditorStatus(
                    tempRoot,
                    "Tools/MxFramework.Authoring/samples/character-codex-flow-test",
                    JsonOptions);
            Require(latest != null && latest.requestId == response.requestId,
                "readiness should resolve the latest Unity Editor status for the selected package without a request id.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

}
