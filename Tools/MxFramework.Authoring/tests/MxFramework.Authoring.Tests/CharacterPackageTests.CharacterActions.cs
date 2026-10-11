using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    internal static void RunCharacterActions()
    {
        CharacterActionSources_ValidateLibrariesPermissionsReferencesAndSequences();
        CharacterActionCompiler_EmitsDeterministicProfileActionSetAndLibraries();
        CharacterActionCompiler_EmptyReactionProfileOmitsInvalidLegacyBinding();
        CharacterActionCompiler_CodexFlowPublicCombinationsRetainLightAttackSequence();
        EditorServer_CharacterActionCatalog_ExposesFixedProfileActionSetsAndGroupedLibraries();
        EditorServer_CharacterActionCopy_IsAtomicAndRejectsStaleCommonSource();
        EditorServer_CharacterPackageDuplicate_ReservesRangeAndReassignsCharacterActions();
    }

    private static void CharacterActionSources_ValidateLibrariesPermissionsReferencesAndSequences()
    {
        CharacterResourcePackage valid = LoadCombatGirlPackage();
        Require(valid.CommonCharacterActionLibrary.SchemaVersion == CharacterActionSourceLibrary.CurrentSchemaVersion &&
            valid.CommonCharacterActionLibrary.Actions.Count >= 3,
            "formal Common CharacterAction source should load from CharacterActionSource.");
        Require(valid.CharacterActionLibrary.SchemaVersion == CharacterActionSourceLibrary.CurrentSchemaVersion,
            "formal character-specific CharacterAction source should load from the character package.");
        Require(valid.CommonCharacterActionLibrary.TryGetAction(301, out JsonElement commonAttack),
            "formal Common source should contain light_attack.");
        GlobalAnimationCompatibilityResult commonAttackCompatibility =
            CharacterActionAnimationCompatibilityResolver.Resolve(
                valid,
                commonAttack,
                valid.CommonCharacterActionLibrary,
                valid.CharacterActionLibrary);
        Require(commonAttackCompatibility.Kind == GlobalAnimationCompatibilityKind.HumanoidRetargeting,
            "Common light_attack should be proven Humanoid-retargetable for Combat Girl.");

        CharacterResourcePackage dragon = CharacterPackageCommands.ReadPackage(
            Path.Combine(FindRepoRoot(), "Authoring", "CharacterPackages", "character-000003"),
            JsonOptions);
        Require(dragon.CharacterActionLibrary.SchemaVersion == CharacterActionSourceLibrary.CurrentSchemaVersion &&
            dragon.CharacterActionLibrary.Actions.Count >= 3,
            "formal character-specific CharacterAction source should load from Dragon14 package.");
        JsonElement nativeAttack = dragon.CharacterActionLibrary.Actions.First(action =>
            CharacterActionSourceLibrary.GetActionId(action) == 940000);
        GlobalAnimationCompatibilityResult nativeAttackCompatibility =
            CharacterActionAnimationCompatibilityResolver.Resolve(
                dragon,
                nativeAttack,
                dragon.CommonCharacterActionLibrary,
                dragon.CharacterActionLibrary);
        Require(nativeAttackCompatibility.Kind == GlobalAnimationCompatibilityKind.Native,
            "Dragon14 Right Claw Slam should resolve as a native character action.");
        CharacterAuthoringValidationReport validReport = CharacterResourcePackageValidator.Validate(valid);
        Require(!HasCharacterActionSourceIssue(validReport),
            "valid Combat Girl CharacterAction source references and command sequence should pass validation.");

        CharacterResourcePackage publicCharacterReference = LoadCombatGirlPackage();
        CombinedWeaponConfigurationAuthoringSummary publicCombination = FindCombatGirlCombination(
            publicCharacterReference.GlobalWeaponLibrary.CombinedWeaponConfigurations);
        publicCombination.ActionConfiguration.Actions[0].ActionId =
            CharacterActionSourceLibrary.GetActionId(dragon.CharacterActionLibrary.Actions[0]);
        CharacterAuthoringValidationReport publicReport = CharacterResourcePackageValidator.Validate(publicCharacterReference);
        Require(publicReport.Issues.Exists(issue =>
                issue.Code == CharacterAuthoringValidationCodes.CombinedWeaponActionRoleMissing &&
                issue.SourcePath == "WeaponSource/global_weapon_library.json"),
            "public combinations must reject character-specific CharacterActions.");

        CharacterResourcePackage duplicate = LoadCombatGirlPackage();
        duplicate.CharacterActionLibrary.Actions.Add(JsonSerializer.Deserialize<JsonElement>(
            duplicate.CommonCharacterActionLibrary.Actions[0].GetRawText(),
            JsonOptions));
        CharacterAuthoringValidationReport duplicateReport = CharacterResourcePackageValidator.Validate(duplicate);
        Require(duplicateReport.Issues.Exists(issue =>
                issue.Code == CharacterAuthoringValidationCodes.CombinedWeaponActionIdShared),
            "one effective Profile must reject duplicate stable IDs across Common and character libraries.");

        CharacterResourcePackage missing = LoadCombatGirlPackage();
        GetCombatGirlOverrideActions(missing).Actions[0].ActionId = 999999;
        CharacterAuthoringValidationReport missingReport = CharacterResourcePackageValidator.Validate(missing);
        Require(missingReport.Issues.Exists(issue =>
                issue.Code == CharacterAuthoringValidationCodes.CombinedWeaponActionRoleMissing &&
                issue.Field.EndsWith("/actionId", StringComparison.Ordinal)),
            "character overrides must reject missing CharacterAction references.");

        CharacterResourcePackage invalidSequence = LoadCombatGirlPackage();
        GetCombatGirlOverrideActions(invalidSequence).CommandSequences[0]
            .ActionRoles.Add("missing.combo.role");
        CharacterAuthoringValidationReport invalidSequenceReport = CharacterResourcePackageValidator.Validate(invalidSequence);
        Require(invalidSequenceReport.Issues.Exists(issue =>
                issue.Code == CharacterAuthoringValidationCodes.CombinedWeaponActionRoleMissing &&
                issue.Field.Contains("commandSequences", StringComparison.Ordinal)),
            "command sequences must resolve every ordered action role.");

        CharacterResourcePackage incompatibleAnimation = LoadCombatGirlPackage();
        incompatibleAnimation.CommonCharacterActionLibrary.AnimationSources[0].RigType = GlobalAnimationRigTypes.Generic;
        CharacterAuthoringValidationReport incompatibleAnimationReport =
            CharacterResourcePackageValidator.Validate(incompatibleAnimation);
        Require(incompatibleAnimationReport.Issues.Exists(issue =>
                issue.Code == CharacterAuthoringValidationCodes.CharacterActionAnimationIncompatible),
            "a Common action must be rejected when its declared source rig cannot drive the target character.");

        CharacterResourcePackage conflict = LoadCombatGirlPackage();
        CombinedWeaponActionConfiguration conflictActions = GetCombatGirlOverrideActions(conflict);
        CharacterCombinedWeaponConfigurationBinding conflictBinding =
            conflict.ApplicationConfig.CombinedWeaponConfigurationBindings.Find(item =>
                item.CombinedWeaponConfigurationId == "combat_girl_sword.sword_empty");
        conflictBinding.Overrides.AbilityConfigurationId = "ability.test.light-attack";
        conflict.GlobalWeaponLibrary.AbilityConfigurations.Add(new GlobalAbilityConfiguration
        {
            AbilityConfigurationId = conflictBinding.Overrides.AbilityConfigurationId,
            Abilities =
            {
                new GlobalAbilityEntry
                {
                    AbilityId = 999001,
                    TriggerMode = GlobalAbilityTriggerModes.Input,
                    InputIntentId = "LightAttack",
                    ActionBindingMode = GlobalAbilityActionBindingModes.Required,
                    ActionRole = "primary"
                }
            }
        });
        int primaryActionId = conflictActions.Actions.Find(item =>
            item.ActionRole == "primary")?.ActionId ?? 0;
        conflictActions.AbilityActionBindings.Add(
            new CombinedWeaponAbilityActionBinding
            {
                AbilityId = 999001,
                ActionRole = "primary",
                ActionId = primaryActionId
            });
        CharacterAuthoringValidationReport conflictReport = CharacterResourcePackageValidator.Validate(conflict);
        Require(conflictReport.Issues.Exists(issue =>
                issue.Code == CharacterAuthoringValidationCodes.CombinedWeaponCommandIntentConflict),
            "one intent must not compile into both commandBindings and commandSequences.");
    }

    private static void CharacterActionCompiler_EmitsDeterministicProfileActionSetAndLibraries()
    {
        string root = FindRepoRoot();
        string packagePath = Path.Combine(root, "Authoring", "CharacterPackages", "character-000001");
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, JsonOptions);
        CharacterAuthoringCompileResult compile = CharacterAuthoringCompiler.Compile(
            new CharacterAuthoringCompileRequest
            {
                Package = package,
                PackageRootPath = packagePath
            });
        AuthoringResourceCollection resources = CharacterPackageCommands.BuildCharacterPackageAuthoringResources(
            root,
            packagePath,
            package,
            JsonOptions);
        var writes = CharacterPackageCommands.CreateUnityImportAdditionalWrites(
            packagePath,
            package,
            compile,
            resources,
            JsonOptions);

        CharacterUnityImportWriteInput profileWrite = writes.Single(write =>
            write.TargetPath.EndsWith(
                "CharacterActions/Profiles/000001/character_action_profile.json",
                StringComparison.Ordinal));
        CharacterUnityImportWriteInput actionSetWrite = writes.Single(write =>
            write.TargetPath.EndsWith(
                "CharacterActions/ActionSets/000001/character_action_sets.json",
                StringComparison.Ordinal));
        CharacterUnityImportWriteInput characterLibraryWrite = writes.Single(write =>
            write.TargetPath.EndsWith(
                "CharacterActions/Libraries/000001/character_actions.json",
                StringComparison.Ordinal));
        CharacterUnityImportWriteInput commonLibraryWrite = writes.Single(write =>
            write.TargetPath.EndsWith(
                "CharacterActions/Libraries/Common/character_actions.json",
                StringComparison.Ordinal));

        using JsonDocument profile = JsonDocument.Parse(profileWrite.Content);
        JsonElement profileEntry = profile.RootElement.GetProperty("profiles")[0];
        Require(profile.RootElement.GetProperty("schemaVersion").GetString() == "0.6.0" &&
            profileEntry.GetProperty("stableId").GetString() == "000001.default",
            "compiler should deterministically derive the schema 0.6.0 Profile ID.");
        string[] libraryIds = profileEntry.GetProperty("actionLibraryIds")
            .EnumerateArray().Select(item => item.GetString()).ToArray();
        Require(libraryIds.SequenceEqual(new[] { "000001", "Common" }),
            "effective Profile should explicitly load the character library and Common.");

        using JsonDocument actionSets = JsonDocument.Parse(actionSetWrite.Content);
        JsonElement sword = actionSets.RootElement.GetProperty("actionSets").EnumerateArray().Single(item =>
            item.GetProperty("stableId").GetString() == "000001.combat_girl_sword.sword_empty");
        Require(actionSets.RootElement.GetProperty("schemaVersion").GetString() == "0.6.0" &&
            sword.GetProperty("characterStableId").GetString() == "char.000001" &&
            sword.GetProperty("equipmentStateStableId").GetString() == "equip_state.000001.combat_girl_sword.sword_empty",
            "compiler should derive the ActionSet identity and runtime context from package and combination.");
        Require(!sword.GetProperty("commandBindings").EnumerateArray().Any(item =>
                (item.TryGetProperty("intentLabel", out JsonElement label) ? label.GetString() : item.GetProperty("intentId").GetString()) == "LightAttack"),
            "LightAttack must not be emitted as a legacy single-action binding when a sequence owns it.");
        JsonElement sequence = sword.GetProperty("commandSequences").EnumerateArray().Single(item =>
            (item.TryGetProperty("intentLabel", out JsonElement label) ? label.GetString() : item.GetProperty("intentId").GetString()) == "LightAttack");
        int[] actionIds = sequence.GetProperty("actionIds").EnumerateArray()
            .Select(item => item.GetInt32()).ToArray();
        Require(actionIds.SequenceEqual(new[] { 455, 456, 457, 458 }) &&
            sequence.GetProperty("wrapAfterLast").GetBoolean(),
            "compiler should resolve the authored role sequence to the real four Common actions.");

        using JsonDocument characterLibrary = JsonDocument.Parse(characterLibraryWrite.Content);
        Require(characterLibrary.RootElement.GetProperty("schemaVersion").GetString() == "0.5.0" &&
            characterLibrary.RootElement.GetProperty("actions").EnumerateArray().All(item =>
                item.GetProperty("actionId").GetInt32() >= 950000),
            "character library projection should contain character-owned source actions only.");

        CharacterResourcePackage repeatedPackage = CharacterPackageCommands.ReadPackage(packagePath, JsonOptions);
        CharacterAuthoringCompileResult repeatedCompile = CharacterAuthoringCompiler.Compile(
            new CharacterAuthoringCompileRequest { Package = repeatedPackage, PackageRootPath = packagePath });
        AuthoringResourceCollection repeatedResources = CharacterPackageCommands.BuildCharacterPackageAuthoringResources(
            root, packagePath, repeatedPackage, JsonOptions);
        var repeatedWrites = CharacterPackageCommands.CreateUnityImportAdditionalWrites(
            packagePath, repeatedPackage, repeatedCompile, repeatedResources, JsonOptions);
        Require(profileWrite.Content == repeatedWrites.Single(write => write.TargetPath == profileWrite.TargetPath).Content &&
            actionSetWrite.Content == repeatedWrites.Single(write => write.TargetPath == actionSetWrite.TargetPath).Content,
            "repeated compilation should produce byte-identical Profile and ActionSet projections.");

        CharacterResourcePackage changedSourcePackage = CharacterPackageCommands.ReadPackage(packagePath, JsonOptions);
        var changedAction = new JsonObject
        {
            ["actionId"] = 999901,
            ["displayName"] = "Character Action changed",
            ["category"] = "BasicAttack",
            ["timelineAuthority"] = "CharacterAuthored",
            ["tags"] = new JsonArray("test"),
            ["priority"] = 10,
            ["durationFrames"] = 30,
            ["requirements"] = new JsonArray(),
            ["phases"] = new JsonArray(),
            ["cancelRules"] = new JsonArray(),
            ["interruptRules"] = new JsonArray()
        };
        changedSourcePackage.CharacterActionLibrary.Actions.Add(JsonSerializer.Deserialize<JsonElement>(
            changedAction.ToJsonString(),
            JsonOptions));
        CharacterAuthoringCompileResult changedSourceCompile = CharacterAuthoringCompiler.Compile(
            new CharacterAuthoringCompileRequest
            {
                Package = changedSourcePackage,
                PackageRootPath = packagePath
            });
        Require(compile.Hashes.SourcePackageHash != changedSourceCompile.Hashes.SourcePackageHash,
            "editing a formal CharacterAction source must invalidate the Unity import source hash.");

        CharacterResourcePackage changedCommonPackage = CharacterPackageCommands.ReadPackage(packagePath, JsonOptions);
        changedCommonPackage.CommonCharacterActionLibrary.AnimationSources[0].AllowRetargeting =
            !changedCommonPackage.CommonCharacterActionLibrary.AnimationSources[0].AllowRetargeting;
        CharacterAuthoringCompileResult changedCommonCompile = CharacterAuthoringCompiler.Compile(
            new CharacterAuthoringCompileRequest
            {
                Package = changedCommonPackage,
                PackageRootPath = packagePath
            });
        Require(compile.Hashes.SourcePackageHash != changedCommonCompile.Hashes.SourcePackageHash,
            "editing Common animation compatibility metadata must invalidate every referencing character import hash.");
    }

    private static void EditorServer_CharacterActionCatalog_ExposesFixedProfileActionSetsAndGroupedLibraries()
    {
        string root = FindRepoRoot();
        object result = EditorServer.ReadCharacterActionCatalog(
            root,
            "Authoring/CharacterPackages/character-000001",
            JsonOptions);
        using JsonDocument catalog = JsonDocument.Parse(JsonSerializer.Serialize(result, JsonOptions));
        JsonElement document = catalog.RootElement;
        Require(document.GetProperty("profileId").GetString() == "000001.default",
            "CharacterStudio catalog should expose the compiler-derived Profile instead of a writable ID.");
        Require(document.GetProperty("actionSets").EnumerateArray().Any(item =>
            item.GetProperty("actionSetId").GetString() == "000001.combat_girl_sword.sword_empty"),
            "CharacterStudio catalog should expose compiler-derived ActionSets.");
        JsonElement common = document.GetProperty("libraries").EnumerateArray().Single(item =>
            item.GetProperty("sourceKind").GetString() == "common");
        JsonElement character = document.GetProperty("libraries").EnumerateArray().Single(item =>
            item.GetProperty("sourceKind").GetString() == "character");
        Require(common.GetProperty("readOnly").GetBoolean() && !character.GetProperty("readOnly").GetBoolean(),
            "Common must be read-only in character context while the character source remains editable.");
        Require(document.GetProperty("actions").EnumerateArray().Any(item =>
            item.GetProperty("sourceKind").GetString() == "common" &&
            item.GetProperty("actionId").GetInt32() == 301 &&
            item.GetProperty("hasHitTrack").GetBoolean() &&
            item.GetProperty("compatibilityKind").GetString() == "HumanoidRetarget"),
            "catalog should expose Common attack hit tracks and proven Humanoid retargeting.");
        object dragonResult = EditorServer.ReadCharacterActionCatalog(
            root,
            "Authoring/CharacterPackages/character-000003",
            JsonOptions);
        using JsonDocument dragonCatalog = JsonDocument.Parse(JsonSerializer.Serialize(dragonResult, JsonOptions));
        Require(dragonCatalog.RootElement.GetProperty("actions").EnumerateArray().Any(item =>
            item.GetProperty("sourceKind").GetString() == "character" &&
            item.GetProperty("actionId").GetInt32() == 940000 &&
            item.GetProperty("compatibilityKind").GetString() == "Native"),
            "catalog should label the Dragon14 character-owned candidate as native.");
    }

    private static void CharacterActionCompiler_EmptyReactionProfileOmitsInvalidLegacyBinding()
    {
        string root = FindRepoRoot();
        string packagePath = Path.Combine(root, "Authoring", "CharacterPackages", "character-000003");
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, JsonOptions);
        CharacterAuthoringCompileResult compile = CharacterAuthoringCompiler.Compile(
            new CharacterAuthoringCompileRequest { Package = package, PackageRootPath = packagePath });
        AuthoringResourceCollection resources = CharacterPackageCommands.BuildCharacterPackageAuthoringResources(
            root,
            packagePath,
            package,
            JsonOptions);
        var writes = CharacterPackageCommands.CreateUnityImportAdditionalWrites(
            packagePath,
            package,
            compile,
            resources,
            JsonOptions);

        CharacterUnityImportWriteInput actionSetWrite = writes.Single(write =>
            write.TargetPath.EndsWith(
                "CharacterActions/ActionSets/000003/character_action_sets.json",
                StringComparison.Ordinal));
        CharacterUnityImportWriteInput reactionWrite = writes.Single(write =>
            write.TargetPath.EndsWith(
                "CharacterActions/ActionSets/000003/character_reaction_profiles.json",
                StringComparison.Ordinal));

        using JsonDocument actionSets = JsonDocument.Parse(actionSetWrite.Content);
        JsonElement actionSet = actionSets.RootElement.GetProperty("actionSets")[0];
        Require(!actionSet.GetProperty("reactionBindings").EnumerateArray().Any(),
            "an empty reaction profile must not emit a legacy binding without a target action.");

        using JsonDocument reactions = JsonDocument.Parse(reactionWrite.Content);
        JsonElement reaction = reactions.RootElement.GetProperty("reactionProfiles")[0];
        Require(reaction.GetProperty("stableId").GetString() == "reaction.000003.unarmed" &&
                reaction.GetProperty("defaultActionId").GetInt32() == 0 &&
                !reaction.GetProperty("rules").EnumerateArray().Any(),
            "Dragon14 must retain an explicit empty reaction profile for future authoring.");
    }

    private static void CharacterActionCompiler_CodexFlowPublicCombinationsRetainLightAttackSequence()
    {
        string root = FindRepoRoot();
        string packagePath = FindSamplePath("character-codex-flow-test");
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, JsonOptions);
        CharacterAuthoringCompileResult compile = CharacterAuthoringCompiler.Compile(
            new CharacterAuthoringCompileRequest
            {
                Package = package,
                PackageRootPath = packagePath
            });
        AuthoringResourceCollection resources = CharacterPackageCommands.BuildCharacterPackageAuthoringResources(
            root,
            packagePath,
            package,
            JsonOptions);
        var writes = CharacterPackageCommands.CreateUnityImportAdditionalWrites(
            packagePath,
            package,
            compile,
            resources,
            JsonOptions);
        CharacterUnityImportWriteInput actionSetWrite = writes.Single(write =>
            write.TargetPath.EndsWith(
                "CharacterActions/ActionSets/codex_flow_test/character_action_sets.json",
                StringComparison.Ordinal));

        using JsonDocument actionSets = JsonDocument.Parse(actionSetWrite.Content);
        JsonElement[] entries = actionSets.RootElement.GetProperty("actionSets")
            .EnumerateArray()
            .ToArray();
        Require(entries.Length == 2,
            "Codex Flow should compile both sword/empty and empty/empty ActionSets.");
        foreach (JsonElement entry in entries)
        {
            Require(!entry.GetProperty("commandBindings").EnumerateArray().Any(item =>
                    (item.TryGetProperty("intentLabel", out JsonElement label) ? label.GetString() : item.GetProperty("intentId").GetString()) == "LightAttack"),
                "Codex Flow LightAttack must not fall back to a legacy single-action binding.");
            JsonElement sequence = entry.GetProperty("commandSequences").EnumerateArray().Single(item =>
                (item.TryGetProperty("intentLabel", out JsonElement label) ? label.GetString() : item.GetProperty("intentId").GetString()) == "LightAttack");
            int[] actionIds = sequence.GetProperty("actionIds")
                .EnumerateArray()
                .Select(item => item.GetInt32())
                .ToArray();
            Require(actionIds.SequenceEqual(new[] { 301, 324, 325 }) &&
                    sequence.GetProperty("wrapAfterLast").GetBoolean(),
                "every maintained Codex Flow equipment context must retain the Common three-hit LightAttack sequence.");
        }
    }

    private static void EditorServer_CharacterActionCopy_IsAtomicAndRejectsStaleCommonSource()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-character-action-copy-" + Guid.NewGuid().ToString("N"));
        const string packageRelative = "Authoring/CharacterPackages/character-000001";
        try
        {
            CopyDirectory(
                Path.Combine(FindRepoRoot(), "Authoring", "CharacterPackages", "character-000001"),
                Path.Combine(tempRoot, packageRelative.Replace('/', Path.DirectorySeparatorChar)));
            CopyDirectory(Path.Combine(FindRepoRoot(), "CharacterActionSource"), Path.Combine(tempRoot, "CharacterActionSource"));
            CopyDirectory(Path.Combine(FindRepoRoot(), "WeaponSource"), Path.Combine(tempRoot, "WeaponSource"));

            string packagePath = Path.Combine(tempRoot, packageRelative.Replace('/', Path.DirectorySeparatorChar));
            string commonPath = Path.Combine(tempRoot, "CharacterActionSource", "Libraries", "Common", "character_actions.json");
            string actionPath = Path.Combine(packagePath, "config", "character_actions.json");
            string applicationPath = Path.Combine(packagePath, "config", "character_application.json");
            string commonHash = CharacterPackageHashUtility.ComputeFileSha256(commonPath);
            string revision = EditorServer.ComputeCharacterPackageRevision(packagePath);
            var request = new EditorServer.CharacterActionCopyRequest
            {
                package = packageRelative,
                combinedWeaponConfigurationId = "combat_girl_sword.sword_empty",
                actionRole = "primary",
                sourceActionId = 301,
                sourceHash = commonHash,
                expectedRevision = revision
            };

            object copied = EditorServer.CopyCommonActionToCharacter(tempRoot, request, JsonOptions);
            int copiedActionId = int.Parse(GetAnonymousPropertyForTest(copied, "actionId"));
            Require(copiedActionId > 0 && copiedActionId != 301,
                "copy should allocate a distinct positive CharacterAction ID.");
            CharacterResourcePackage copiedPackage = CharacterPackageCommands.ReadPackage(packagePath, JsonOptions);
            Require(copiedPackage.CharacterActionLibrary.TryGetAction(copiedActionId, out _),
                "copied action should be persisted in the character source library.");
            string catalogPath = Path.Combine(tempRoot, "CharacterActionSource", "action_id_catalog.json");
            using (JsonDocument catalog = JsonDocument.Parse(File.ReadAllText(catalogPath)))
            {
                JsonElement allocation = catalog.RootElement.GetProperty("allocations")
                    .EnumerateArray()
                    .Single(item => item.GetProperty("actionId").GetInt32() == copiedActionId);
                Require(allocation.GetProperty("libraryId").GetString() == "000001"
                    && allocation.GetProperty("status").GetString() == "active",
                    "copy should register the new action in the shared global ID catalog.");
            }
            CombinedWeaponConfigurationAuthoringSummary copiedCombination =
                FindCombatGirlCombination(copiedPackage.ApplicationConfig.CombinedWeaponConfigurations);
            Require(copiedCombination.ActionConfiguration.Actions.Find(item => item.ActionRole == "primary")?.ActionId == copiedActionId &&
                copiedCombination.ActionConfiguration.Actions.Exists(item => item.ActionRole == "primary.combo2"),
                "copy should atomically update one role while retaining the complete override mapping.");
            string applicationJson = File.ReadAllText(applicationPath);
            Require(!applicationJson.Contains("\"timeline\"", StringComparison.Ordinal) &&
                !applicationJson.Contains("\"actionSetId\"", StringComparison.Ordinal),
                "copy should persist pure action references without legacy inline JSON.");

            string actionsBeforeStale = File.ReadAllText(actionPath);
            string applicationBeforeStale = File.ReadAllText(applicationPath);
            request.expectedRevision = EditorServer.ComputeCharacterPackageRevision(packagePath);
            request.sourceHash = "sha256:stale";
            bool staleRejected = false;
            try
            {
                EditorServer.CopyCommonActionToCharacter(tempRoot, request, JsonOptions);
            }
            catch (EditorServer.CharacterActionSourceConflictException)
            {
                staleRejected = true;
            }
            Require(staleRejected &&
                File.ReadAllText(actionPath) == actionsBeforeStale &&
                File.ReadAllText(applicationPath) == applicationBeforeStale,
                "stale Common source hash should reject without changing either source file.");

            request.expectedRevision = EditorServer.ComputeCharacterPackageRevision(packagePath);
            request.sourceHash = commonHash;
            string catalogBeforeRollback = File.ReadAllText(catalogPath);
            bool rollbackTriggered = false;
            try
            {
                EditorServer.CopyCommonActionToCharacter(
                    tempRoot,
                    request,
                    JsonOptions,
                    beforeApplicationWrite: () => throw new IOException("injected application write failure"));
            }
            catch (IOException)
            {
                rollbackTriggered = true;
            }
            Require(rollbackTriggered &&
                File.ReadAllText(actionPath) == actionsBeforeStale &&
                File.ReadAllText(applicationPath) == applicationBeforeStale &&
                File.ReadAllText(catalogPath) == catalogBeforeRollback,
                "copy transaction should restore both source files and the global ID catalog when the second write fails.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static CharacterResourcePackage LoadCombatGirlPackage()
    {
        return CharacterPackageCommands.ReadPackage(
            Path.Combine(FindRepoRoot(), "Authoring", "CharacterPackages", "character-000001"),
            JsonOptions);
    }

    private static void EditorServer_CharacterPackageDuplicate_ReservesRangeAndReassignsCharacterActions()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-character-action-range-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyDirectory(Path.Combine(FindRepoRoot(), "CharacterActionSource"), Path.Combine(tempRoot, "CharacterActionSource"));
            string catalogBefore = File.ReadAllText(
                Path.Combine(tempRoot, "CharacterActionSource", "action_id_catalog.json"));
            EditorServer.CharacterActionIdCatalogLease lease = EditorServer.ReserveCharacterActionIdRange(
                tempRoot,
                "numeric_clone_test",
                new[] { 950000, 950001, 950002 },
                "Authoring/CharacterPackages/character-numeric-clone-test/config/character_actions.json");
            int[] cloneIds = lease.ReassignedActionIds.Values
                .OrderBy(value => value)
                .ToArray();
            Require(cloneIds.Length == 3 && !new[] { 950000, 950001, 950002 }.Intersect(cloneIds).Any(),
                "duplicated package must not reuse source CharacterAction IDs.");
            Require(cloneIds.Zip(cloneIds.Skip(1), (left, right) => right == left + 1).All(value => value),
                "duplicated package actions should consume its reserved range deterministically.");

            using JsonDocument catalog = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(tempRoot, "CharacterActionSource", "action_id_catalog.json")));
            JsonElement library = catalog.RootElement.GetProperty("libraries").EnumerateArray().Single(item =>
                item.GetProperty("libraryId").GetString() == "numeric_clone_test");
            int rangeStart = library.GetProperty("ranges")[0].GetProperty("start").GetInt32();
            Require(cloneIds[0] == rangeStart,
                "new package should allocate cloned actions from the first ID in its reserved range.");
            Require(catalog.RootElement.GetProperty("allocations").EnumerateArray().Count(item =>
                    item.GetProperty("libraryId").GetString() == "numeric_clone_test") == cloneIds.Length,
                "new package range transaction should register every cloned action allocation.");
            lease.Rollback();
            Require(File.ReadAllText(Path.Combine(tempRoot, "CharacterActionSource", "action_id_catalog.json")) == catalogBefore,
                "range reservation lease should restore the catalog when package creation rolls back.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static CombinedWeaponConfigurationAuthoringSummary FindCombatGirlCombination(
        System.Collections.Generic.List<CombinedWeaponConfigurationAuthoringSummary> combinations)
    {
        return combinations.Find(item =>
            item != null && item.CombinedWeaponConfigurationId == "combat_girl_sword.sword_empty")
            ?? throw new InvalidDataException("Combat Girl sword/empty combination is missing.");
    }

    private static CombinedWeaponActionConfiguration GetCombatGirlOverrideActions(
        CharacterResourcePackage package)
    {
        CharacterCombinedWeaponConfigurationBinding binding =
            package.ApplicationConfig.CombinedWeaponConfigurationBindings.Find(item =>
                item != null && item.CombinedWeaponConfigurationId == "combat_girl_sword.sword_empty")
            ?? throw new InvalidDataException("Combat Girl sword/empty binding is missing.");
        return binding.Overrides?.ActionConfiguration
            ?? throw new InvalidDataException("Combat Girl action override is missing.");
    }

    private static bool HasCharacterActionSourceIssue(CharacterAuthoringValidationReport report)
    {
        return report.Issues.Exists(issue =>
            issue.Code == CharacterAuthoringValidationCodes.CombinedWeaponActionRoleMissing ||
            issue.Code == CharacterAuthoringValidationCodes.CombinedWeaponActionRoleDuplicate ||
            issue.Code == CharacterAuthoringValidationCodes.CombinedWeaponActionIdShared ||
            issue.Code == CharacterAuthoringValidationCodes.CharacterActionAnimationIncompatible ||
            issue.Code == CharacterAuthoringValidationCodes.CombinedWeaponCommandIntentConflict);
    }

    private static void RequireProjectionSemanticHashMatchesCheckout(
        string root,
        CharacterUnityImportWriteInput write,
        string projectionName)
    {
        string checkedInPath = Path.Combine(
            root,
            write.TargetPath.Replace('/', Path.DirectorySeparatorChar));
        Require(File.Exists(checkedInPath),
            projectionName + " projection is missing from the Unity generated output.");
        string generatedHash = ComputeJsonSemanticHash(write.Content);
        string checkedInHash = ComputeJsonSemanticHash(File.ReadAllText(checkedInPath));
        Require(generatedHash == checkedInHash,
            projectionName + " semantic hash differs from the formal CharacterAction source compilation.");
    }

    private static string ComputeJsonSemanticHash(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        var canonical = new StringBuilder(json?.Length ?? 0);
        AppendCanonicalJson(document.RootElement, canonical);
        return CharacterPackageHashUtility.ComputeTextSha256(canonical.ToString());
    }

    private static void AppendCanonicalJson(JsonElement element, StringBuilder target)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                target.Append('{');
                bool firstProperty = true;
                foreach (JsonProperty property in element.EnumerateObject()
                             .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    if (!firstProperty)
                        target.Append(',');
                    firstProperty = false;
                    target.Append(JsonSerializer.Serialize(property.Name));
                    target.Append(':');
                    AppendCanonicalJson(property.Value, target);
                }
                target.Append('}');
                break;
            case JsonValueKind.Array:
                target.Append('[');
                bool firstItem = true;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (!firstItem)
                        target.Append(',');
                    firstItem = false;
                    AppendCanonicalJson(item, target);
                }
                target.Append(']');
                break;
            case JsonValueKind.String:
                target.Append(JsonSerializer.Serialize(element.GetString()));
                break;
            default:
                target.Append(element.GetRawText());
                break;
        }
    }
}
