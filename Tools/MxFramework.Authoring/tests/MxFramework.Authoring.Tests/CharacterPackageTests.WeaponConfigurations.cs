using System;
using System.Collections.Generic;
using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    public static void RunBladeTrailStyles()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        SingleWeaponConfiguration sword = package.GlobalWeaponLibrary.SingleWeaponConfigurations.Find(
            item => item.SingleWeaponConfigurationId == "weapon.000001.main");
        Require(sword != null, "The formal sword fixture must exist.");
        sword.PresentationConfiguration.BladeTrailStyleId = 0;
        CharacterAuthoringCompileResult baseline = Compile(package);
        Require(baseline.GeneratedConfigPatch != null, "Default trail configuration must compile.");

        sword.PresentationConfiguration.BladeTrailStyleId = 4;
        GlobalWeaponLibrary roundTrip = JsonSerializer.Deserialize<GlobalWeaponLibrary>(
            JsonSerializer.Serialize(package.GlobalWeaponLibrary, JsonOptions), JsonOptions);
        Require(roundTrip.SingleWeaponConfigurations.Find(item => item.SingleWeaponConfigurationId == sword.SingleWeaponConfigurationId)
                .PresentationConfiguration.BladeTrailStyleId == 4,
            "A selected trail type must survive authoring save/load.");
        CharacterAuthoringCompileResult energy = Compile(package);
        List<PatchEntry> generatedWeapons = energy.GeneratedConfigPatch.Patch.Entries.FindAll(entry =>
            entry.Source == CharacterApplicationCompilerTableNames.WeaponConfig);
        Require(generatedWeapons.Count == 1, "The fixture must compile exactly one equipped single weapon.");
        PatchEntry generated = generatedWeapons[0];
        Require(generated != null && generated.Fields.GetScalar("BladeTrailStyleId") == "4",
            "The compiler must publish the selected trail type to WeaponConfig.");
        Require(baseline.Hashes.SourcePackageHash != energy.Hashes.SourcePackageHash,
            "Changing a trail type must invalidate the source content fingerprint.");
        Require(baseline.Hashes.GeometryBindingHash == energy.Hashes.GeometryBindingHash,
            "A presentation-only trail type must not alter weapon hit geometry.");

        sword.PresentationConfiguration.BladeTrailStyleId = 99;
        CharacterAuthoringValidationReport invalid = CharacterResourcePackageValidator.Validate(package);
        Require(invalid.Issues.Exists(issue => issue.SourceObjectPath.EndsWith("/presentationConfiguration/bladeTrailStyleId", StringComparison.Ordinal)),
            "Unsupported trail types must be rejected before publication.");
    }

    public static void RunWeaponConfigurationContract()
    {
        WeaponConfiguration3_JsonRoundTrip_SeparatesGlobalSinglesAndCharacterCombinations();
        WeaponConfiguration3_SchemasExposeSinglesCombinationsAndExplicitMounts();
        WeaponConfiguration3_Validator_RejectsInvalidReferencesAndTwoHandConflicts();
        WeaponConfiguration3_Validator_AcceptsMinimumReactionBindings();
        WeaponConfiguration3_Compiler_EmitsExactStableFourHandCombinations();
        CodexFlowTestSample_CompilesToConfigPatchGeometryMappingAndWritePlan();
        Compiler_ExplicitAbilityLoadouts_OverrideGeneratedDefaults();
        Compiler_OptionalAbilityConfiguration_OmitsLoadout();
        EditorServer_CombinedWeaponConfigurationsApi_UsesSharedConfigurationIdentity();
        CharacterPackageMigration_PromotesLegacyWeaponsAndRejectsGlobalConflicts();
        EditorServer_GlobalWeaponLibrary_UsesRevisionAndReferenceProtection();
    }

    private static void WeaponConfiguration3_JsonRoundTrip_SeparatesGlobalSinglesAndCharacterCombinations()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        string applicationJson = JsonSerializer.Serialize(package.ApplicationConfig, JsonOptions);
        string libraryJson = JsonSerializer.Serialize(package.GlobalWeaponLibrary, JsonOptions);
        string slotJson = JsonSerializer.Serialize(package.Geometry.WeaponSlots[0], JsonOptions);
        CharacterApplicationAuthoringSummary roundTrip =
            JsonSerializer.Deserialize<CharacterApplicationAuthoringSummary>(applicationJson, JsonOptions);
        GlobalWeaponLibrary libraryRoundTrip =
            JsonSerializer.Deserialize<GlobalWeaponLibrary>(libraryJson, JsonOptions);

        Require(roundTrip.SchemaVersion == "8.0" &&
            roundTrip.CombinedWeaponConfigurationBindings.Count == 2 &&
            roundTrip.CombinedWeaponConfigurations.Count == 0,
            "schema 8.0 should serialize character references without copying effective public combinations.");
        Require(libraryRoundTrip.SchemaVersion == "6.0" &&
            libraryRoundTrip.SingleWeaponConfigurations.Count >= 2 &&
            libraryRoundTrip.CombinedWeaponConfigurations.Count >= 2 &&
            libraryRoundTrip.AnimationConfigurations.Count > 0 &&
            libraryRoundTrip.AbilityConfigurations.Count == 5 &&
            libraryRoundTrip.ReactionConfigurations.Count > 0,
            "global reusable single, combined, animation, ability and reaction configurations should round-trip together.");
        Require(!applicationJson.Contains("\"weaponDefinitions\"", StringComparison.Ordinal) &&
            !applicationJson.Contains("\"weaponConfigurations\"", StringComparison.Ordinal) &&
            !applicationJson.Contains("\"defaultWeaponConfigurationId\"", StringComparison.Ordinal) &&
            !applicationJson.Contains("\"abilityLoadouts\"", StringComparison.Ordinal) &&
            !applicationJson.Contains("\"animationProfiles\"", StringComparison.Ordinal) &&
            !applicationJson.Contains("\"reactionProfiles\"", StringComparison.Ordinal),
            "schema 8.0 must not serialize legacy weapon or root behavior fields.");
        Require(applicationJson.Contains("\"combinedWeaponConfigurationBindings\"", StringComparison.Ordinal) &&
            !applicationJson.Contains("\"combinedWeaponConfigurations\"", StringComparison.Ordinal) &&
            libraryJson.Contains("\"combinedWeaponConfigurations\"", StringComparison.Ordinal),
            "schema 8.0 should keep public recipes in the global library and only references in the character package.");
        Require(slotJson.Contains("\"slotId\"", StringComparison.Ordinal) &&
            slotJson.Contains("\"attachSocketId\"", StringComparison.Ordinal) &&
            slotJson.Contains("\"slotPose\"", StringComparison.Ordinal) &&
            !slotJson.Contains("singleWeaponConfigurationId", StringComparison.Ordinal),
            "character weapon slots should own only socket binding and character-local pose.");
        Require(!applicationJson.Contains("\"attachmentId\"", StringComparison.Ordinal),
            "schema 8.0 bindings must not serialize attachment identities.");

        CharacterCombinedWeaponConfigurationBinding swordBinding =
            roundTrip.CombinedWeaponConfigurationBindings.Find(item =>
                item.CombinedWeaponConfigurationId == "codex_flow_test.sword_empty");
        swordBinding.Overrides.AnimationConfigurationId = "animation.character.override";
        swordBinding.Overrides.ActionConfiguration = new CombinedWeaponActionConfiguration();
        swordBinding.Overrides.ActionConfiguration.Actions.Add(new CombinedWeaponActionEntry
        {
            ActionRole = "primary",
            ActionId = 301
        });
        List<CombinedWeaponConfigurationAuthoringSummary> resolved =
            GlobalWeaponConfigurationResolver.ResolveCombinedWeapons(roundTrip, libraryRoundTrip);
        CombinedWeaponConfigurationAuthoringSummary resolvedSword = resolved.Find(item =>
            item.CombinedWeaponConfigurationId == "codex_flow_test.sword_empty");
        CombinedWeaponConfigurationAuthoringSummary publicSword = libraryRoundTrip.CombinedWeaponConfigurations.Find(item =>
            item.CombinedWeaponConfigurationId == "codex_flow_test.sword_empty");
        Require(resolvedSword.AnimationConfigurationId == "animation.character.override" &&
            resolvedSword.ReactionConfigurationId == publicSword.ReactionConfigurationId &&
            resolvedSword.ActionConfiguration.Actions.Count == 1 &&
            resolvedSword.ActionConfiguration.Actions[0].ActionId == 301 &&
            resolvedSword.Slots[0].SingleWeaponConfigurationId == publicSword.Slots[0].SingleWeaponConfigurationId,
            "character overrides should completely replace action-role selections while inheriting other public defaults and slots.");
        Require(GlobalWeaponConfigurationResolver.GetCharacterScopedCombinationIdentity(
                "codex_flow_test",
                "codex_flow_test.sword_empty") == "sword_empty" &&
            GlobalWeaponConfigurationResolver.GetCharacterScopedCombinationIdentity(
                "another_character",
                "codex_flow_test.sword_empty") == "codex_flow_test.sword_empty",
            "runtime identities should preserve historical owner-local IDs while keeping external public IDs collision-safe.");
    }

    private static void WeaponConfiguration3_Validator_RejectsInvalidReferencesAndTwoHandConflicts()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        SingleWeaponConfiguration sword = package.GlobalWeaponLibrary.SingleWeaponConfigurations.Find(item =>
            item.SingleWeaponConfigurationId == "weapon.000001.main");
        sword.OccupiesSlots = new List<string> { "mainHand", "offHand" };
        CombinedWeaponConfigurationAuthoringSummary dual =
            package.ApplicationConfig.CombinedWeaponConfigurations.Find(item =>
                item.CombinedWeaponConfigurationId == "codex_flow_test.sword_empty");
        dual.Slots.Find(slot => slot.SlotId == "offHand").SingleWeaponConfigurationId = "weapon.000001.main";
        package.Geometry.WeaponSlots.RemoveAll(slot => slot.SlotId == "offHand");
        package.GlobalWeaponLibrary.ReactionConfigurations[0].DefaultSlotId = "missing.default";
        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);

        Require(report.Issues.Exists(issue =>
            issue.Code == CharacterAuthoringValidationCodes.TwoHandWeaponSlotConflict),
            "two-hand single weapons should reject an equipped off-hand weapon.");
        Require(report.Issues.Exists(issue =>
            issue.Code == CharacterAuthoringValidationCodes.CombinedWeaponAttachmentRequired),
            "equipped combined slots should require a character-owned semantic slot binding.");
        Require(report.Issues.Exists(issue =>
            issue.Code == CharacterAuthoringValidationCodes.GlobalReactionDefaultSlotNotFound),
            "reaction configurations should reject a defaultSlotId that does not resolve in the same configuration.");
    }

    private static void WeaponConfiguration3_Validator_AcceptsMinimumReactionBindings()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        CombinedWeaponConfigurationAuthoringSummary configuration =
            package.ApplicationConfig.CombinedWeaponConfigurations.Find(item =>
                item.CombinedWeaponConfigurationId == "codex_flow_test.sword_empty");
        GlobalReactionConfiguration reaction = package.GlobalWeaponLibrary.ReactionConfigurations.Find(item =>
            item.ReactionConfigurationId == configuration.ReactionConfigurationId);
        GlobalReactionSlotConfiguration death = reaction.Slots.Find(item => item.SlotId == "death");
        death.Required = true;

        var requiredRoles = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < reaction.Slots.Count; i++)
        {
            GlobalReactionSlotConfiguration slot = reaction.Slots[i];
            if (slot.Required || string.Equals(slot.SlotId, reaction.DefaultSlotId, StringComparison.Ordinal))
                requiredRoles.Add(slot.ActionRole);
        }
        configuration.ActionConfiguration.ReactionActionBindings.RemoveAll(binding =>
            !requiredRoles.Contains(binding.ActionRole));

        CharacterAuthoringValidationReport minimumReport = CharacterResourcePackageValidator.Validate(package);
        Require(!minimumReport.Issues.Exists(issue =>
                issue.Code == CharacterAuthoringValidationCodes.CombinedWeaponActionRoleMissing),
            "a reaction profile should run with its default slot and explicitly required slots while optional slots remain enhancements.");

        configuration.ActionConfiguration.ReactionActionBindings.RemoveAll(binding =>
            string.Equals(binding.ReactionSlotId, reaction.DefaultSlotId, StringComparison.Ordinal));
        CharacterAuthoringValidationReport missingDefaultReport = CharacterResourcePackageValidator.Validate(package);
        Require(missingDefaultReport.Issues.Exists(issue =>
                issue.Code == CharacterAuthoringValidationCodes.CombinedWeaponActionRoleMissing),
            "the reaction default slot should remain part of the minimum runnable contract even when its required flag is false.");
    }

    private static void WeaponConfiguration3_SchemasExposeSinglesCombinationsAndExplicitMounts()
    {
        IReadOnlyList<ConfigSchema> schemas = CharacterResourcePackageSchemas.CreateAll();
        ConfigSchema application = FindSchema(schemas, CharacterResourcePackageSchemas.CharacterApplicationSchemaId);
        ConfigSchema binding = FindSchema(schemas, CharacterResourcePackageSchemas.WeaponConfigurationBindingSchemaId);
        ConfigSchema overrides = FindSchema(schemas, CharacterResourcePackageSchemas.WeaponConfigurationOverridesSchemaId);
        ConfigSchema definition = FindSchema(schemas, CharacterResourcePackageSchemas.WeaponDefinitionSchemaId);
        ConfigSchema configuration = FindSchema(schemas, CharacterResourcePackageSchemas.WeaponConfigurationSchemaId);
        ConfigSchema attachment = FindSchema(schemas, CharacterResourcePackageSchemas.WeaponAttachmentSchemaId);

        Require(HasField(application, "defaultCombinedWeaponConfigurationId") &&
            HasField(application, "combinedWeaponConfigurationBindings") &&
            !HasField(application, "combinedWeaponConfigurations") &&
            !HasField(application, "weaponDefinitions"),
            "application schema should expose public-combination references and character overrides only.");
        Require(HasField(binding, "combinedWeaponConfigurationId") && HasField(binding, "overrides"),
            "character binding schema should expose the public ID and optional behavior overrides.");
        Require(HasField(overrides, "animationConfigurationId") &&
            HasField(overrides, "abilityConfigurationId") &&
            HasField(overrides, "reactionConfigurationId") &&
            HasField(overrides, "actionConfiguration"),
            "character override schema should expose only behavior replacement fields and no slots.");
        Require(HasField(definition, "singleWeaponConfigurationId") &&
            HasField(definition, "weaponRef") && HasField(definition, "modelPose") &&
            HasField(definition, "traceProfiles") && HasField(definition, "occupiesSlots") &&
            !HasField(definition, "abilityLoadoutId"),
            "single-weapon schema should own resource identity but no behavior binding.");
        Require(HasField(configuration, "combinedWeaponConfigurationId") &&
            HasField(configuration, "slots") && HasField(configuration, "animationConfigurationId") &&
            HasField(configuration, "abilityConfigurationId") && HasField(configuration, "reactionConfigurationId") &&
            HasField(configuration, "actionConfiguration"),
            "public combined schema should expose global defaults and action authoring.");
        Require(HasField(attachment, "slotId") && HasField(attachment, "attachSocketId") &&
            HasField(attachment, "slotPose") && !HasField(attachment, "singleWeaponConfigurationId") &&
            !HasField(attachment, "traceId"),
            "character weapon-slot schema should be independent of weapon and trace ownership.");
    }

    private static void WeaponConfiguration3_Compiler_EmitsExactStableFourHandCombinations()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        CharacterAuthoringCompileResult first = Compile(package);
        List<PatchEntry> loadouts = first.GeneratedConfigPatch.Patch.Entries.FindAll(entry =>
            entry.Source == CharacterApplicationCompilerTableNames.EquipmentLoadoutConfig);
        List<PatchEntry> states = first.GeneratedConfigPatch.Patch.Entries.FindAll(entry =>
            entry.Source == CharacterApplicationCompilerTableNames.EquipmentStateConfig);
        List<PatchEntry> weapons = first.GeneratedConfigPatch.Patch.Entries.FindAll(entry =>
            entry.Source == CharacterApplicationCompilerTableNames.WeaponConfig);

        Require(loadouts.Count == 2 && states.Count == 2,
            "compiler should emit one loadout and one state for every explicit combination.");
        Require(weapons.Count == 1 && weapons.TrueForAll(entry =>
            entry.Fields.GetScalar("GrantedAbilityLoadoutId") == "0"),
            "compiler should project only referenced ordinary singles without single-weapon abilities.");
        AssertWeaponConfigurationSlots(loadouts, "empty_empty", Array.Empty<string>(), Array.Empty<int>());
        AssertWeaponConfigurationSlots(loadouts, "sword_empty", new[] { "mainHand" }, new[] { 780001 });
        PatchEntry swordEmpty = FindLoadout(loadouts, "sword_empty");
        int stateId = int.Parse(swordEmpty.Fields.GetScalar("EquipmentStateId"));
        PatchEntry state = states.Find(entry => entry.Id == stateId.ToString());
        Require(state != null && state.Fields.GetScalar("AnimationProfileId") == "profile.sword",
            "equipment loadout should directly select its exact state and animation profile.");
        Require(state.Fields["MatchRules"].List.Count == 0,
            "schema 3.0 states should contain no name/category/tag matching heuristics.");

        package.GlobalWeaponLibrary.SingleWeaponConfigurations.Reverse();
        package.GlobalWeaponLibrary.CombinedWeaponConfigurations.Reverse();
        package.GlobalWeaponLibrary.AnimationConfigurations.Reverse();
        package.GlobalWeaponLibrary.AbilityConfigurations.Reverse();
        package.GlobalWeaponLibrary.ReactionConfigurations.Reverse();
        for (int i = 0; i < package.GlobalWeaponLibrary.AbilityConfigurations.Count; i++)
        {
            package.GlobalWeaponLibrary.AbilityConfigurations[i].Abilities.Reverse();
        }
        for (int i = 0; i < package.GlobalWeaponLibrary.ReactionConfigurations.Count; i++)
            package.GlobalWeaponLibrary.ReactionConfigurations[i].Slots.Reverse();
        for (int i = 0; i < package.GlobalWeaponLibrary.AnimationConfigurations.Count; i++)
        {
            package.GlobalWeaponLibrary.AnimationConfigurations[i].Sets.Reverse();
            package.GlobalWeaponLibrary.AnimationConfigurations[i].Profiles.Reverse();
        }
        package.ApplicationConfig.CombinedWeaponConfigurationBindings.Reverse();
        package.ApplicationConfig.CombinedWeaponConfigurations.Reverse();
        package.Geometry.WeaponAttachments.Reverse();
        CharacterAuthoringCompileResult reordered = Compile(package);
        Require(first.Hashes.GeneratedConfigHash == reordered.Hashes.GeneratedConfigHash,
            "compiler output hash should remain stable when source arrays are reordered.");
        Require(first.Hashes.SourcePackageHash == reordered.Hashes.SourcePackageHash,
            "source hash should normalize global singles and character combinations.");
    }

    private static void EditorServer_CombinedWeaponConfigurationsApi_UsesSharedConfigurationIdentity()
    {
        CharacterCombinedWeaponConfigurationsSnapshot snapshot =
            MxFramework.Authoring.Cli.EditorServer.ReadCharacterWeaponConfigurations(
                FindRepoRoot(),
                "Tools/MxFramework.Authoring/samples/character-codex-flow-test",
                JsonOptions);
        Require(snapshot.CombinedWeaponConfigurations.Count == 2 &&
            snapshot.DefaultCombinedWeaponConfigurationId == "codex_flow_test.sword_empty",
            "combined snapshot should expose the two accepted configurations and the explicit sword/empty default.");
        CharacterCombinedWeaponConfigurationSnapshot emptyEmpty =
            snapshot.CombinedWeaponConfigurations.Find(item =>
                item.CombinedWeaponConfigurationId == "codex_flow_test.empty_empty");
        Require(emptyEmpty != null && emptyEmpty.Slots.Find(item =>
            item.SlotId == "mainHand").IsUnarmed,
            "snapshot should explicitly display main-hand weapon.unarmed.");
        Require(emptyEmpty.Slots.Find(item => item.SlotId == "offHand").IsUnarmed && emptyEmpty.EquipmentStateId > 0,
            "snapshot should keep the explicit two-unarmed state identity together.");
        CharacterCombinedWeaponConfigurationSnapshot swordEmpty =
            snapshot.CombinedWeaponConfigurations.Find(item =>
                item.CombinedWeaponConfigurationId == "codex_flow_test.sword_empty");
        Require(swordEmpty != null && swordEmpty.LoadoutStableId == "equip_loadout.codex_flow_test.sword_empty" &&
            swordEmpty.Slots.Find(item => item.SlotId == "mainHand").AttachmentId ==
                "weapon.attachment.sword_empty.mainhand.weapon.000001.main",
            "snapshot runtime identities should match compiler identities after public-ID promotion.");
    }

    private static void AssertWeaponConfigurationSlots(
        List<PatchEntry> loadouts,
        string configurationId,
        string[] expectedSlots,
        int[] expectedWeapons)
    {
        PatchEntry loadout = FindLoadout(loadouts, configurationId);
        Require(loadout != null && int.Parse(loadout.Fields.GetScalar("EquipmentStateId")) > 0,
            configurationId + " should have an explicit EquipmentStateId.");
        List<FieldValue> slots = loadout.Fields["Slots"].List;
        Require(slots.Count == expectedSlots.Length, configurationId + " slot count mismatch.");
        for (int i = 0; i < slots.Count; i++)
        {
            string slotKey = slots[i].Map.ContainsKey("SlotLabel")
                ? slots[i].Map.GetScalar("SlotLabel")
                : slots[i].Map.GetScalar("SlotId");
            Require(slotKey == expectedSlots[i],
                configurationId + " slot identity mismatch.");
            Require(int.Parse(slots[i].Map.GetScalar("WeaponId")) == expectedWeapons[i],
                configurationId + " weapon identity mismatch.");
        }
    }

    private static PatchEntry FindLoadout(List<PatchEntry> loadouts, string configurationId)
    {
        return loadouts.Find(entry => entry.Fields.GetScalar("StableId")
            .EndsWith("." + configurationId, StringComparison.Ordinal));
    }
}
