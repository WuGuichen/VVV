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
    internal static void RunBasicAttributes()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        CharacterAuthoringCompileResult result = Compile(package, checkFiles: true, checkHashes: true);
        Require(result.Status == CharacterAuthoringCompilerStatus.Ready, "Character attributes must compile successfully.");
        PatchEntry profile = result.GeneratedConfigPatch.Patch.Entries.Find(entry =>
            entry.Source == CharacterApplicationCompilerTableNames.CharacterAttributeProfileConfig);
        Require(profile != null, "Character attribute profile must be generated.");
        List<FieldValue> attributes = profile.Fields["Attributes"].List;
        Require(attributes.Count == 4, "Character attributes must contain HP, Attack, Defense and Stamina.");
        Require(attributes.Exists(value => value.Map["AttributeId"].Scalar == "1"
            && value.Map["StableId"].Scalar == "attr.hp"), "HP must use the combat authority identity.");
        Require(attributes.Exists(value => value.Map["AttributeId"].Scalar == "2"
            && value.Map["StableId"].Scalar == "attr.attack"), "Attack must use the combat authority identity.");
        Require(attributes.Exists(value => value.Map["AttributeId"].Scalar == "3"
            && value.Map["StableId"].Scalar == "attr.defense"), "Defense must use the combat authority identity.");
        Require(attributes.Exists(value => value.Map["AttributeId"].Scalar == "920002"
            && value.Map["StableId"].Scalar == "attr.stamina"), "Stamina identity must remain unchanged.");
        package.BaseAttributes = new CharacterBaseAttributesAuthoring
        {
            Hp = 240, Attack = 35, Defense = 18, BaseCohesion = 140,
            ScatterRecoveryRate = 3, ScatterRecoveryDelayFrames = 120, CohesionBreakRecoveryDelayFrames = 180
        };
        CharacterAuthoringCompileResult customized = Compile(package, checkFiles: true, checkHashes: true);
        Require(customized.Status == CharacterAuthoringCompilerStatus.Ready, "Custom attributes must compile.");
        PatchEntry customProfile = customized.GeneratedConfigPatch.Patch.Entries.Find(entry =>
            entry.Source == CharacterApplicationCompilerTableNames.CharacterAttributeProfileConfig);
        Require(customProfile.Fields["Attributes"].List.Find(value => value.Map["AttributeId"].Scalar == "1")
            .Map["InitialValue"].Scalar == "240", "Authored HP must reach runtime config.");
        Require(customProfile.Fields["BaseCohesion"].Scalar == "140"
            && customProfile.Fields["ScatterRecoveryRate"].Scalar == "3"
            && customProfile.Fields["ScatterRecoveryDelayFrames"].Scalar == "120"
            && customProfile.Fields["CohesionBreakRecoveryDelayFrames"].Scalar == "180", "Cohesion settings must survive compilation.");
        Require(customized.Hashes.GeneratedConfigHash != result.Hashes.GeneratedConfigHash
            && customized.Hashes.SourcePackageHash != result.Hashes.SourcePackageHash, "Attribute edits must invalidate source and generated hashes.");
        package.BaseAttributes = JsonSerializer.Deserialize<CharacterBaseAttributesAuthoring>(JsonSerializer.Serialize(package.BaseAttributes));
        Require(Compile(package, checkFiles: true, checkHashes: true).Hashes.GeneratedConfigHash == customized.Hashes.GeneratedConfigHash,
            "Saving and reopening attributes must preserve runtime output.");
        package.BaseAttributes.Hp = 0;
        Require(Compile(package, checkFiles: true, checkHashes: true).GateReport.ExportBlocked, "Invalid HP must block compilation.");
        package.BaseAttributes.Hp = int.MaxValue;
        Require(Compile(package, checkFiles: true, checkHashes: true).GateReport.ExportBlocked, "Values that lose integer precision must block compilation.");
        package.BaseAttributes.Hp = 240;
        package.BaseAttributes.SchemaVersion = "99.0";
        Require(Compile(package, checkFiles: true, checkHashes: true).GateReport.ExportBlocked, "Future attribute schemas must not be silently overwritten.");
    }

    private static void CodexFlowTestSample_CompilesToConfigPatchGeometryMappingAndWritePlan()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        CharacterAuthoringCompileResult result = Compile(package, checkFiles: true, checkHashes: true);
        CharacterAuthoringCompileResult second = Compile(LoadSample("character-codex-flow-test"), checkFiles: true, checkHashes: true);

        Require(result.Status == CharacterAuthoringCompilerStatus.Ready, "Codex Flow Test compiler status should be Ready.");
        Require(result.IsDeterministicFullCompile, "v1 compiler should declare deterministic full compile.");
        Require(result.Hashes.SourcePackageHash == second.Hashes.SourcePackageHash, "source package hash should be deterministic.");
        Require(result.Hashes.GeneratedConfigHash == second.Hashes.GeneratedConfigHash, "generated config hash should be deterministic.");
        Require(result.GeneratedConfigPatch.Patch.Entries.Exists(entry => entry.Source == CharacterApplicationCompilerTableNames.CharacterConfig), "compiler should generate CharacterConfig patch entry.");
        Require(result.GeneratedConfigPatch.Patch.Entries.Exists(entry => entry.Source == CharacterApplicationCompilerTableNames.EquipmentStateConfig), "compiler should generate EquipmentStateConfig patch entries.");
        PatchEntry equipmentSchema = result.GeneratedConfigPatch.Patch.Entries.Find(entry => entry.Source == CharacterApplicationCompilerTableNames.EquipmentSchemaConfig);
        Require(equipmentSchema != null, "compiler should generate EquipmentSchemaConfig.");
        Require(equipmentSchema.Fields["Slots"].List.Exists(slot => (slot.Map.ContainsKey("SlotLabel") ? slot.Map["SlotLabel"].Scalar : slot.Map["SlotId"].Scalar) == "mainHand"), "generated equipment schema should include the character main-hand slot.");
        Require(equipmentSchema.Fields["Slots"].List.Exists(slot => (slot.Map.ContainsKey("SlotLabel") ? slot.Map["SlotLabel"].Scalar : slot.Map["SlotId"].Scalar) == "offHand"), "generated equipment schema should include the character off-hand slot.");
        Require(result.GeneratedConfigPatch.Patch.Entries.Exists(entry => entry.Source == CharacterApplicationCompilerTableNames.CombatActionSetConfig), "compiler should generate CombatActionSetConfig patch entries.");
        Require(!GeneratedCombatActionsContainTraceProfileOverride(result), "new compiler output should leave CombatActionSet Actions TraceProfileIdOverride empty; equipmentSlotId resolves the weapon trace.");
        Require(result.GeometryBinding.BodyColliders.Exists(collider => collider.ColliderLabel == "head_sphere" && collider.Shape == CharacterColliderShape.Sphere), "compiler geometry binding should include body collider data.");
        Require(result.GeometryBinding.WeaponAttachments.Exists(attachment => (attachment.WeaponLabel == "weapon.000001.main" || attachment.WeaponLabel == "sword") && attachment.EquipSlotLabel == "mainHand" && attachment.TraceLabel == "trace.iron_sword.blade"), "compiler geometry binding should include the explicit sword main-hand attachment and trace binding.");
        Require(result.ResourceMapping.Entries.Exists(entry => entry.PackageResourceKey == "char.codex_flow_test.model.body" && entry.ImportTargetPath.Contains("Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test")), "compiler resource mapping should map package ResourceKey to Unity import target.");
        Require(result.UnityImportWritePlan.CanWriteToUnityProject, "Ready compiler result should be importable.");
        Require(result.UnityImportWritePlan.CanSpawnAfterImport, "Ready compiler result should be spawnable after import.");
        Require(result.UnityImportWritePlan.Writes.Exists(write => write.Kind == CharacterAuthoringCompilerWriteKinds.GeneratedConfigPatch), "write plan should include generated config patch target.");
        Require(result.UnityImportWritePlan.Writes.Exists(write => write.Kind == CharacterAuthoringCompilerWriteKinds.ResourceFile && write.SourcePath == "resources/models/character_00.fbx"), "write plan should include resource file copy target.");
        Require(result.ResolverVerificationPlan.Status == "Ready", "resolver verification plan should be ready.");
        Require(result.ResolverVerificationPlan.ExpectedResolverEntrypoint == "CharacterPackageResolver.Resolve", "resolver verification plan should name the runtime resolver.");
        Require(result.ResolverVerificationPlan.DefaultLoadoutStableId == "equip_loadout.codex_flow_test.sword_empty", "default loadout should be sword empty.");
        Require(result.ResolverVerificationPlan.ExpectedActiveEquipmentStateStableId == "equip_state.codex_flow_test.sword_empty", "default active equipment state should match sword empty.");
        Require(result.ResolverVerificationPlan.RequiredTables.Count >= 12, "resolver verification plan should enumerate all Character Application tables.");
        Require(result.ResolverVerificationPlan.KnownAbilityIds.Contains(900001), "resolver verification plan should include generated base ability ids.");
        Compiler_BodyPresenceV4_IsDeterministicAndMissingCompatible();
    }

    private static void Compiler_BodyPresenceV4_IsDeterministicAndMissingCompatible()
    {
        CharacterResourcePackage baselinePackage = LoadSample("character-codex-flow-test");
        CharacterAuthoringCompileResult baseline = Compile(baselinePackage);
        string baselineBindingJson = JsonSerializer.Serialize(baseline.GeometryBinding, JsonOptions);

        CharacterResourcePackage explicitMissingPackage = LoadSample("character-codex-flow-test");
        explicitMissingPackage.Geometry.BodyPresenceDocumentState =
            CharacterBodyPresenceDocumentState.Missing;
        explicitMissingPackage.Geometry.BodyPresence = CompilerPresenceDocument(
            CompilerPresenceVolume("presence.ignored", CharacterBodyPresenceShape.Sphere));
        CharacterAuthoringCompileResult explicitMissing = Compile(explicitMissingPackage);
        Require(explicitMissing.GeometryBinding.Format == baseline.GeometryBinding.Format
                && explicitMissing.Hashes.SourcePackageHash == baseline.Hashes.SourcePackageHash
                && explicitMissing.Hashes.GeometryBindingHash == baseline.Hashes.GeometryBindingHash
                && JsonSerializer.Serialize(explicitMissing.GeometryBinding, JsonOptions) == baselineBindingJson,
            "Missing Body Presence must preserve the existing v1-v3 binding bytes and hashes.");

        CharacterResourcePackage emptyPackage = LoadSample("character-codex-flow-test");
        emptyPackage.Geometry.BodyPresenceDocumentState =
            CharacterBodyPresenceDocumentState.Current;
        emptyPackage.Geometry.BodyPresence = CompilerPresenceDocument();
        CharacterAuthoringCompileResult empty = Compile(emptyPackage);
        Require((empty.GeometryBinding.Format == CharacterAuthoringCompilerFormats.GeometryBindingV4 || empty.GeometryBinding.Format == CharacterAuthoringCompilerFormats.GeometryBindingV8)
                && (empty.GeometryBinding.SchemaVersion == "4.0" || empty.GeometryBinding.SchemaVersion == "8.0")
                && empty.GeometryBinding.BodyPresenceMode == CharacterBodyPresenceBindingMode.Authored
                && empty.GeometryBinding.BodyPresenceRevision == 3
                && empty.GeometryBinding.BodyPresenceVolumes.Count == 0
                && empty.GeometryBinding.BodyPresenceContentHash
                    == CharacterBodyPresenceDocumentCodec.ComputeContentHash(
                        emptyPackage.Geometry.BodyPresence),
            "an explicit empty Current Body Presence must compile to an authored v4 binding.");
        Require(empty.Hashes.SourcePackageHash != baseline.Hashes.SourcePackageHash
                && empty.Hashes.GeometryBindingHash != baseline.Hashes.GeometryBindingHash,
            "Current Body Presence must participate in source and geometry identities.");
        Require(empty.SourceMappings.Exists(mapping =>
                mapping.SourcePath == "geometry/body_presence.json"
                && mapping.SourceObjectPath == "bodyPresence"
                && mapping.TargetPath == "bodyPresence"),
            "an explicit empty Presence set must retain a document-level source mapping.");

        CharacterResourcePackage firstPackage = LoadSample("character-codex-flow-test");
        firstPackage.Geometry.BodyPresenceDocumentState = CharacterBodyPresenceDocumentState.Current;
        firstPackage.Geometry.BodyPresence = CompilerPresenceDocument(
            CompilerPresenceVolume("presence.tail", CharacterBodyPresenceShape.Box),
            CompilerPresenceVolume("presence.core", CharacterBodyPresenceShape.Capsule));
        CharacterResourcePackage secondPackage = LoadSample("character-codex-flow-test");
        secondPackage.Geometry.BodyPresenceDocumentState = CharacterBodyPresenceDocumentState.Current;
        secondPackage.Geometry.BodyPresence = CompilerPresenceDocument(
            CompilerPresenceVolume("presence.core", CharacterBodyPresenceShape.Capsule),
            CompilerPresenceVolume("presence.tail", CharacterBodyPresenceShape.Box));
        CharacterAuthoringCompileResult first = Compile(firstPackage);
        CharacterAuthoringCompileResult second = Compile(secondPackage);
        string firstJson = JsonSerializer.Serialize(first.GeometryBinding, JsonOptions);
        string secondJson = JsonSerializer.Serialize(second.GeometryBinding, JsonOptions);
        Require(first.Hashes.SourcePackageHash == second.Hashes.SourcePackageHash
                && first.Hashes.GeometryBindingHash == second.Hashes.GeometryBindingHash
                && firstJson == secondJson
                && first.GeometryBinding.BodyPresenceVolumes[0].VolumeId == "presence.core"
                && first.GeometryBinding.BodyPresenceVolumes[1].VolumeId == "presence.tail",
            "Body Presence input order must not change v4 bytes, hashes, or volume order.");
        Require(first.SourceMappings.Exists(mapping =>
                mapping.SourcePath == "geometry/body_presence.json"
                && mapping.SourceObjectPath == "bodyPresence/volumes/presence.core"
                && mapping.TargetPath == "bodyPresenceVolumes/presence.core")
                && first.SourceMappings.Exists(mapping =>
                    mapping.SourcePath == "geometry/body_presence.json"
                    && mapping.SourceObjectPath == "bodyPresence/volumes/presence.tail"
                    && mapping.TargetPath == "bodyPresenceVolumes/presence.tail"),
            "each compiled Presence volume must retain its exact source object path.");

        CharacterResourcePackage changedPackage = LoadSample("character-codex-flow-test");
        changedPackage.Geometry.BodyPresenceDocumentState = CharacterBodyPresenceDocumentState.Current;
        changedPackage.Geometry.BodyPresence = CompilerPresenceDocument(
            CompilerPresenceVolume("presence.core", CharacterBodyPresenceShape.Capsule),
            CompilerPresenceVolume("presence.tail", CharacterBodyPresenceShape.Box));
        changedPackage.Geometry.BodyPresence.Volumes[0].Radius += 0.125f;
        changedPackage.Geometry.BodyPresence.Volumes[0].Height += 0.25f;
        CharacterAuthoringCompileResult changed = Compile(changedPackage);
        Require(changed.GeometryBinding.BodyPresenceContentHash
                    != first.GeometryBinding.BodyPresenceContentHash
                && changed.Hashes.SourcePackageHash != first.Hashes.SourcePackageHash
                && changed.Hashes.GeometryBindingHash != first.Hashes.GeometryBindingHash,
            "a Presence content change must invalidate all Presence-bearing compiler identities.");

        CharacterResourcePackage invalidPackage = LoadSample("character-codex-flow-test");
        invalidPackage.Geometry.BodyPresenceDocumentState = CharacterBodyPresenceDocumentState.Current;
        invalidPackage.Geometry.BodyPresence = CompilerPresenceDocument(
            CompilerPresenceVolume("presence.same", CharacterBodyPresenceShape.Sphere),
            CompilerPresenceVolume("presence.same", CharacterBodyPresenceShape.Sphere));
        CharacterAuthoringCompileResult invalid = Compile(invalidPackage);
        Require(invalid.Status == CharacterAuthoringCompilerStatus.ExportBlocked
                && invalid.GeometryBinding.Format != CharacterAuthoringCompilerFormats.GeometryBindingV4
                && invalid.GeometryBinding.BodyPresenceVolumes == null,
            "invalid Current Body Presence must fail closed without a partial v4 binding.");

        CharacterResourcePackage futurePackage = LoadSample("character-codex-flow-test");
        futurePackage.Geometry.BodyPresenceDocumentState =
            CharacterBodyPresenceDocumentState.UnsupportedSchema;
        futurePackage.Geometry.BodyPresence = null;
        futurePackage.Geometry.BodyPresenceRawPayload =
            "{\"format\":\"mx.characterBodyPresence.v9\",\"schemaVersion\":\"9.0\"}";
        CharacterAuthoringCompileResult future = Compile(futurePackage);
        Require(future.Status == CharacterAuthoringCompilerStatus.ExportBlocked
                && future.GeometryBinding.Format != CharacterAuthoringCompilerFormats.GeometryBindingV4
                && future.GeometryBinding.BodyPresenceVolumes == null,
            "future Body Presence must remain read-only and must not generate a partial v4 binding.");

        firstPackage.Geometry.BodyProfile.DefaultCapsule.Center.X = 0.25f;
        firstPackage.Geometry.BodyProfile.DefaultCapsule.Center.Z = -0.5f;
        CharacterAuthoringGeometryBinding diagnostic = Compile(firstPackage).GeometryBinding;
        Require(diagnostic.AuthorCapsuleCenterX == 0.25f
                && diagnostic.AuthorCapsuleCenterZ == -0.5f
                && diagnostic.RuntimeConsumedCapsuleCenterX == 0f
                && diagnostic.RuntimeConsumedCapsuleCenterZ == 0f
                && !diagnostic.RuntimeConsumesAuthorCapsuleCenterXZ,
            "compiler diagnostics must expose authored X/Z and the current runtime consumption fact.");
    }

    private static CharacterBodyPresenceDocument CompilerPresenceDocument(
        params CharacterBodyPresenceVolumeAuthoring[] volumes)
    {
        return new CharacterBodyPresenceDocument
        {
            Format = CharacterBodyPresenceDocument.LegacyFormat,
            SchemaVersion = CharacterBodyPresenceDocument.LegacySchemaVersion,
            Revision = 3,
            Volumes = new List<CharacterBodyPresenceVolumeAuthoring>(volumes)
        };
    }

    private static CharacterBodyPresenceVolumeAuthoring CompilerPresenceVolume(
        string volumeId,
        CharacterBodyPresenceShape shape)
    {
        return new CharacterBodyPresenceVolumeAuthoring
        {
            VolumeId = volumeId,
            ReferenceFrame = CharacterBodyPresenceReferenceFrame.ModelRoot,
            Shape = shape,
            LocalPose = new CharacterBodyPresenceLocalPose
            {
                Position = new CharacterAuthoringVector3(0.25f, 1f, -0.5f),
                Rotation = new CharacterAuthoringQuaternion(0f, 0f, 0f, 1f),
                Scale = new CharacterAuthoringVector3(1f, 1f, 1f)
            },
            Radius = shape == CharacterBodyPresenceShape.Sphere
                || shape == CharacterBodyPresenceShape.Capsule ? 0.5f : 0f,
            Height = shape == CharacterBodyPresenceShape.Capsule ? 2f : 0f,
            Size = shape == CharacterBodyPresenceShape.Box
                ? new CharacterAuthoringVector3(1f, 2f, 3f)
                : new CharacterAuthoringVector3(),
            SkinMillimeters = 25,
            PressureScaleMilli = 1000,
            Enabled = !string.Equals(volumeId, "presence.tail", StringComparison.Ordinal)
        };
    }

    private static void Compiler_EffectiveAnimationProfileRequiresExactLocomotionSlot()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        GlobalAnimationConfiguration selectedConfiguration = GlobalWeaponConfigurationResolver.FindAnimation(
            package.GlobalWeaponLibrary,
            "animation.codex_flow_test.profile.locomotion");
        AnimationAuthoringProfile profile = selectedConfiguration?.Profiles.Find(item =>
            item.ProfileId == "profile.locomotion");
        Require(profile != null && profile.Slots.Count > 0, "sample should expose the effective locomotion profile slot.");
        profile.Slots[0].SlotId = "Locomotion";
        profile.Slots[0].Purpose = "locomotion";
        package.AnimationAuthoring.Sets[0].Groups[0].Usage = "locomotion";

        CharacterAuthoringCompileResult result = Compile(package);

        Require(result.Status == CharacterAuthoringCompilerStatus.ExportBlocked,
            "a wrong-case effective locomotion slot must block character export.");
        Require(result.GateReport.Issues.Exists(issue =>
                issue.Code == CharacterAuthoringCompilerValidationCodes.EffectiveLocomotionSlotMissing &&
                issue.Gate == CharacterAuthoringValidationGate.ExportBlocked &&
                issue.Field == "AnimationProfileId"),
            "character compile should surface one compiler-backed effective locomotion diagnostic.");
        Require(!result.UnityImportWritePlan.CanWriteToUnityProject,
            "invalid animation profiles must disable Unity import writes.");
        Require(result.AnimationCompileResult != null,
            "character compile should retain the animation artifacts used by the gate and Unity write plan.");
    }

    private static void Compiler_HumanoidAnimationConfigurationRetargetsAcrossCharacters()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.AnimationAuthoring.SkeletonProfileId = "skeleton.other_character.humanoid";
        package.AnimationAuthoring.AvatarProfileId = "avatar.other_character.humanoid";
        GlobalAnimationConfiguration selectedConfiguration = GlobalWeaponConfigurationResolver.FindAnimation(
            package.GlobalWeaponLibrary,
            "animation.codex_flow_test.profile.locomotion");

        GlobalAnimationCompatibilityResult compatibility =
            GlobalWeaponConfigurationResolver.ResolveAnimationCompatibility(package, selectedConfiguration);
        CharacterAuthoringCompileResult result = Compile(package);

        Require(compatibility.Kind == GlobalAnimationCompatibilityKind.HumanoidRetargeting,
            "different Humanoid skeleton/avatar identities should resolve through explicit retargeting.");
        Require(!result.GateReport.ExportBlocked && !result.GateReport.ImportBlocked &&
                result.UnityImportWritePlan.CanWriteToUnityProject,
            "a retargetable Humanoid animation configuration should remain import-ready for another Humanoid character. status=" +
            result.Status + " firstIssue=" + (result.GateReport.Issues.Count > 0 ? result.GateReport.Issues[0].Code : "none"));
        Require(result.AnimationCompileResult.AnimationSetDefinition.SkeletonProfileId ==
                "skeleton.other_character.humanoid" &&
                result.AnimationCompileResult.AnimationSetDefinition.AvatarProfileId ==
                "avatar.other_character.humanoid",
            "the compiled package should target the character rig while retaining reusable source compatibility entries.");
        Require(result.AnimationCompileResult.AnimationSetDefinition.Profiles.Exists(item =>
                item.ProfileId == "profile.locomotion"),
            "the selected global configuration profile should replace the character-local animation payload.");
        Require(result.AnimationCompileResult.AnimationPackageExpectation.Compatibility.Exists(item =>
                item.ProfileId == "profile.locomotion" && item.AllowRetargeting &&
                item.SkeletonProfileId == "skeleton.codex_flow_test.humanoid"),
            "compiled compatibility evidence should retain the Humanoid source rig and retargeting opt-in.");

        package.Geometry.BodyProfile.BodyKind = CharacterAuthoringBodyKind.Primitive;
        package.AnimationAuthoring.SkeletonProfileId = "skeleton.other_character.generic";
        package.AnimationAuthoring.AvatarProfileId = "avatar.other_character.generic";
        CharacterAuthoringCompileResult incompatible = Compile(package);
        Require(incompatible.GateReport.Issues.Exists(issue =>
                issue.Code == CharacterAuthoringValidationCodes.GlobalAnimationConfigurationIncompatible),
            "a non-Humanoid target must reject cross-skeleton Humanoid reuse instead of silently binding it.");

        package.Geometry.BodyProfile.BodyKind = CharacterAuthoringBodyKind.Skeletal;
        for (int i = 0; i < package.ResourceCatalog.Entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = package.ResourceCatalog.Entries[i];
            if (string.Equals(entry?.Usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.Ordinal))
                entry.Tags.RemoveAll(tag => string.Equals(tag, "humanoid", StringComparison.OrdinalIgnoreCase));
        }
        CharacterAuthoringCompileResult genericSkeletal = Compile(package);
        Require(genericSkeletal.GateReport.Issues.Exists(issue =>
                issue.Code == CharacterAuthoringValidationCodes.GlobalAnimationConfigurationIncompatible),
            "a Generic skeletal target must not be treated as Humanoid only because it has a skeletal body.");
    }

    private static void Compiler_GeneratedLoadoutSlotMissing_BlocksExport()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.Geometry.WeaponSlots.Clear();

        CharacterAuthoringCompileResult result = Compile(package, checkFiles: true, checkHashes: true);

        Require(result.Status == CharacterAuthoringCompilerStatus.ExportBlocked, "a generated loadout slot missing from the equipment schema must block export.");
        Require(result.GateReport.Issues.Exists(issue =>
            issue.Code == CharacterAuthoringValidationCodes.GeneratedEquipmentSlotMissing &&
            issue.Gate == CharacterAuthoringValidationGate.ExportBlocked),
            "generated equipment slot mismatch should produce a stable ExportBlocked diagnostic.");
        Require(!result.UnityImportWritePlan.CanWriteToUnityProject, "generated equipment slot mismatch must prevent Unity import writes.");
        Require(!result.UnityImportWritePlan.CanSpawnAfterImport, "generated equipment slot mismatch must prevent runtime spawn.");
    }

    private static void Compiler_ExplicitAbilityLoadouts_OverrideGeneratedDefaults()
    {
        CharacterResourcePackage fallbackPackage = LoadSample("character-codex-flow-test");
        fallbackPackage.ApplicationConfig.AbilityLoadouts.Clear();
        CharacterAuthoringCompileResult fallback = Compile(fallbackPackage, checkFiles: true, checkHashes: true);
        Require(fallback.ResolverVerificationPlan.KnownAbilityIds.Contains(900001), "global complete ability configurations should include dodge without a character base layer.");
        Require(fallback.ResolverVerificationPlan.KnownAbilityIds.Contains(900102), "global complete ability configurations should include combination-specific abilities.");

        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        GlobalAbilityConfiguration ability = package.GlobalWeaponLibrary.AbilityConfigurations.Find(item =>
            item.AbilityConfigurationId == "ability.codex_flow_test.sword_empty");
        ability.Abilities = new List<GlobalAbilityEntry>
        {
            new GlobalAbilityEntry
            {
                AbilityId = 901001,
                DisplayName = "Authoring Primary",
                Tier = GlobalAbilityTiers.Basic,
                ExecutionMode = GlobalAbilityExecutionModes.ActionOnly,
                TriggerMode = GlobalAbilityTriggerModes.Input,
                SlotId = "authoringPrimary",
                InputIntentId = "intent.authoring.primary",
                ActionBindingMode = GlobalAbilityActionBindingModes.Required,
                ActionRole = "ability.authoring_primary"
            }
        };
        CombinedWeaponConfigurationAuthoringSummary combined = package.ApplicationConfig.CombinedWeaponConfigurations.Find(item =>
            item.CombinedWeaponConfigurationId == "codex_flow_test.sword_empty");
        combined.ActionConfiguration.Actions.RemoveAll(item => item.ActionRole.StartsWith("ability.", StringComparison.Ordinal));
        combined.ActionConfiguration.AbilityActionBindings.Clear();
        combined.ActionConfiguration.Actions.Add(new CombinedWeaponActionEntry
        {
            ActionRole = "ability.authoring_primary",
            ActionId = 960001,
            TimelineId = "codex_flow_test.sword_empty.authoring_primary.timeline"
        });
        combined.ActionConfiguration.AbilityActionBindings.Add(new CombinedWeaponAbilityActionBinding
        {
            AbilityId = 901001,
            ActionRole = "ability.authoring_primary",
            ActionId = 960001
        });

        CharacterAuthoringCompileResult result = Compile(package, checkFiles: true, checkHashes: true);
        PatchEntry loadout = result.GeneratedConfigPatch.Patch.Entries.Find(entry =>
            entry.Source == CharacterApplicationCompilerTableNames.AbilityLoadoutConfig &&
            entry.Fields.GetScalar("StableId") == ability.AbilityConfigurationId);

        Require(loadout != null, "the selected global complete ability configuration should compile.");
        Require(loadout.Fields["AbilityIds"].List.Count == 1 && loadout.Fields["AbilityIds"].List[0].Scalar == "901001", "global complete ability ids should project exactly.");
        Require(loadout.Fields["RemoveAbilityIds"].List.Count == 0, "schema 4.0 complete ability configurations should not emit legacy removals.");
        Require(loadout.Fields["SlotBindings"].List.Count == 1, "global ability slot bindings should compile.");
        Require((loadout.Fields["SlotBindings"].List[0].Map.ContainsKey("SlotLabel") ? loadout.Fields["SlotBindings"].List[0].Map["SlotLabel"].Scalar : loadout.Fields["SlotBindings"].List[0].Map["SlotId"].Scalar) == "authoringPrimary", "global slot id should compile.");
        Require(loadout.Fields["SlotBindings"].List[0].Map["AbilityId"].Scalar == "901001", "global slot ability should compile.");
        Require(loadout.Fields["SlotBindings"].List[0].Map["InputIntentId"].Scalar == "intent.authoring.primary", "global input intent should compile.");
        Require(result.ResolverVerificationPlan.KnownAbilityIds.Contains(901001), "resolver verification plan should include the edited global ability id.");
        Require(!result.ResolverVerificationPlan.KnownAbilityIds.Contains(900002),
            "schema 4.0 must not synthesize a single-weapon ability fallback.");
    }

    private static void Compiler_OptionalAbilityConfiguration_OmitsLoadout()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        CombinedWeaponConfigurationAuthoringSummary combined = package.GlobalWeaponLibrary.CombinedWeaponConfigurations.Find(item =>
            item.CombinedWeaponConfigurationId == "codex_flow_test.sword_empty");
        combined.AbilityConfigurationId = string.Empty;
        combined.ActionConfiguration.AbilityActionBindings.Clear();
        combined.ActionConfiguration.Actions.RemoveAll(item => item.ActionRole.StartsWith("ability.", StringComparison.Ordinal));

        CharacterAuthoringCompileResult result = Compile(package, checkFiles: true, checkHashes: true);
        Require(result.Status == CharacterAuthoringCompilerStatus.Ready,
            "a combination without an ability configuration should remain compile-ready.");
        Require(!result.GeneratedConfigPatch.Patch.Entries.Exists(entry =>
                entry.Source == CharacterApplicationCompilerTableNames.AbilityLoadoutConfig &&
                entry.Fields.GetScalar("StableId") == "ability.codex_flow_test.sword_empty"),
            "an unconfigured optional ability should not generate an AbilityLoadoutConfig.");
        PatchEntry state = result.GeneratedConfigPatch.Patch.Entries.Find(entry =>
            entry.Source == CharacterApplicationCompilerTableNames.EquipmentStateConfig &&
            entry.Fields.GetScalar("StableId") == "equip_state.codex_flow_test.sword_empty");
        Require(state != null && state.Fields.GetScalar("GrantedAbilityLoadoutId") == "0",
            "an unconfigured optional ability should project GrantedAbilityLoadoutId zero.");
    }

    private static void Compiler_AllowedStateIdsReferenceGeneratedStatesOnly()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        CharacterAuthoringCompileResult result = Compile(package);
        PatchEntry schema = FindPatchEntry(result, CharacterApplicationCompilerTableNames.EquipmentSchemaConfig, "750001");
        Require(schema != null, "humanoid starter schema should be generated.");
        Require(schema.Fields.TryGetValue("AllowedStateIds", out FieldValue allowedStateIds), "schema should include allowed equipment states.");

        var generatedStateIds = new HashSet<string>(StringComparer.Ordinal);
        List<PatchEntry> entries = result.GeneratedConfigPatch.Patch.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            PatchEntry entry = entries[i];
            if (entry != null && entry.Source == CharacterApplicationCompilerTableNames.EquipmentStateConfig)
                generatedStateIds.Add(entry.Id);
        }

        Require(allowedStateIds.List.Count == generatedStateIds.Count, "allowed state ids should match generated equipment state count.");
        for (int i = 0; i < allowedStateIds.List.Count; i++)
        {
            string id = allowedStateIds.List[i].Scalar;
            Require(generatedStateIds.Contains(id), "allowed state id should reference a generated EquipmentStateConfig: " + id);
        }
    }

    private static void Compiler_WeaponTraceProximalRangeFlowsToGeometryAndTraceBindings()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        SingleWeaponConfiguration sourceWeapon = package.GlobalWeaponLibrary.SingleWeaponConfigurations.Find(item =>
            item.SingleWeaponConfigurationId == "weapon.000001.main" || item.SingleWeaponConfigurationId == "sword");
        SingleWeaponTraceProfile sourceTrace = sourceWeapon.TraceProfiles.Find(item =>
            item.TraceId == "trace.iron_sword.blade");
        sourceTrace.ProximalExtension = 0.35f;
        sourceTrace.ProximalRadius = 0.18f;

        CharacterAuthoringCompileResult result = Compile(package);

        CharacterWeaponTraceBinding trace = result.GeometryBinding.WeaponTraces.Find(
            item => item.TraceLabel == "trace.iron_sword.blade");
        Require(trace != null, "compiled geometry binding should include main hand weapon trace.");
        Require(Math.Abs(trace.ProximalExtension - 0.35f) < 0.0001f, "compiled geometry binding should keep proximal extension.");
        Require(Math.Abs(trace.ProximalRadius - 0.18f) < 0.0001f, "compiled geometry binding should keep proximal radius.");

        PatchEntry weaponConfig = result.GeneratedConfigPatch.Patch.Entries.Find(entry =>
            entry.Source == CharacterApplicationCompilerTableNames.WeaponConfig
            && entry.Fields.TryGetValue("TraceBindings", out FieldValue traceBindings)
            && traceBindings.List != null
            && traceBindings.List.Count > 0);
        Require(weaponConfig != null, "generated WeaponConfig should include trace bindings.");
        FieldValue firstTraceBinding = weaponConfig.Fields["TraceBindings"].List[0];
        Require(firstTraceBinding.Map["ProximalExtension"].Scalar == "0.35", "generated TraceBindings should include proximal extension.");
        Require(firstTraceBinding.Map["ProximalRadius"].Scalar == "0.18", "generated TraceBindings should include proximal radius.");
    }

    private static bool GeneratedCombatActionsContainTraceProfileOverride(CharacterAuthoringCompileResult result)
    {
        List<PatchEntry> entries = result?.GeneratedConfigPatch?.Patch?.Entries;
        if (entries == null)
            return false;

        for (int i = 0; i < entries.Count; i++)
        {
            PatchEntry entry = entries[i];
            if (entry == null || entry.Source != CharacterApplicationCompilerTableNames.CombatActionSetConfig)
                continue;
            if (!entry.Fields.TryGetValue("Actions", out FieldValue actions) || actions.List == null)
                continue;
            for (int actionIndex = 0; actionIndex < actions.List.Count; actionIndex++)
            {
                FieldValue action = actions.List[actionIndex];
                if (action.Map == null ||
                    !action.Map.TryGetValue("TraceProfileIdOverride", out FieldValue traceOverride))
                    continue;
                if (!string.IsNullOrWhiteSpace(traceOverride.Scalar))
                    return true;
            }
        }

        return false;
    }

    private static void CodexFlowTestSample_CompilesResourcePlanArtifacts()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        CharacterResourcePlanCompileResult result = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = FindSamplePath("character-codex-flow-test"),
            ValidateResourceFiles = true,
            ValidateResourceHashes = true
        });
        CharacterResourcePlanCompileResult second = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = LoadSample("character-codex-flow-test"),
            PackageRootPath = FindSamplePath("character-codex-flow-test"),
            ValidateResourceFiles = true,
            ValidateResourceHashes = true
        });

        Require(result.RuntimeResourceCatalog.Format == CharacterResourcePlanFormats.RuntimeResourceCatalog, "runtime resource catalog should declare character runtime catalog format.");
        Require(result.RuntimeResourceCatalog.SchemaVersion == 1, "runtime resource catalog should use ResourceCatalog schemaVersion 1.");
        Require(result.RuntimeResourceCatalog.Entries.Count == 3, "Codex Flow Test runtime catalog should include the referenced package-local ResourceManager-loadable entries.");
        Require(result.RuntimeResourceCatalog.Entries.Exists(entry =>
            entry.Id == "char.codex_flow_test.model.body" &&
            entry.Type == "GameObject" &&
            entry.Provider == "memory" &&
            entry.ProviderData["packageResourceKey"] == "char.codex_flow_test.model.body"), "body model should map to ResourceCatalogEntry-compatible GameObject entry.");
        Require(!result.RuntimeResourceCatalog.Entries.Exists(entry =>
            entry.Id == "char.codex_flow_test.anim.locomotion"), "codex flow test source package should not emit legacy package-local locomotion animation entries.");
        Require(result.CharacterResourcePlan.SpawnCritical.Resources.Count == 1, "resource plan should include SpawnCritical body resource.");
        Require(result.CharacterResourcePlan.EquipmentInitial.Resources.Count == 0, "direct source-package compile should not invent public weapon equipment resources.");
        Require(result.CharacterResourcePlan.AnimationWarmup.Resources.Count == 0, "direct source-package compile should not warm up package-local locomotion animation.");
        Require(result.CharacterResourcePlan.UiDeferred.Resources.Count == 1, "resource plan should include deferred UI thumbnail resource.");
        Require(result.CharacterResourcePlan.Audio.RequiredCues.Count == 0, "sample has no audio cues yet but should still expose Audio group.");
        Require(result.AudioCueManifest.Format == CharacterResourcePlanFormats.AudioCueManifest, "audio cue manifest should be emitted even when empty.");
        Require(result.ResourceValidationReport.Status == "Ready", "clean Codex Flow Test resource plan should be ready.");
        Require(!result.ResourceValidationReport.HasErrors, "clean Codex Flow Test resource plan should not have errors.");
        Require(result.ResourceValidationReport.GroupCounts.SpawnCritical == 1, "validation report should summarize SpawnCritical count.");
        Require(result.CharacterResourcePlan.PlanHash == second.CharacterResourcePlan.PlanHash, "resource plan hash should be deterministic.");
    }

    private static void CharacterResourcePlanCompiler_FmodAudioCueDoesNotEnterRuntimeCatalog()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.ResourceCatalog.Entries.Add(new CharacterPackageResourceEntry
        {
            ResourceKey = "audio.cue.codex_flow_test.sword_slash",
            LocalId = "audio.sword_slash",
            StableId = "charpkg.codex_flow_test.resource.audio.sword_slash",
            TypeId = CharacterPackageResourceTypeIds.Audio,
            Usage = CharacterPackageResourceUsageIds.AudioCue,
            PackageId = "codex_flow_test",
            ImportHints = new CharacterPackageImportHint
            {
                ProviderId = AuthoringResourceProviderIds.Fmod,
                Metadata = new Dictionary<string, string>
                {
                    { "runtimeBindingKind", AuthoringResourceBindingKind.AudioCue.ToString() },
                    { "audioCueId", "audio.cue.codex_flow_test.sword_slash" },
                    { "audioEventDefinitionId", "audio.event.codex_flow_test.sword_slash" },
                    { "fmodEventPath", "event:/Character/CodexFlowTest/SwordSlash" },
                    { "fmodGuid", "{11111111-2222-3333-4444-555555555555}" },
                    { "bank", "Character" }
                }
            }
        });
        package.ApplicationConfig.ResourceKeys.Add("audio.cue.codex_flow_test.sword_slash");

        CharacterResourcePlanCompileResult result = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = FindSamplePath("character-codex-flow-test")
        });

        Require(!result.RuntimeResourceCatalog.Entries.Exists(entry => entry.Id == "audio.cue.codex_flow_test.sword_slash"), "FMOD audio cue must not enter ordinary runtime resource catalog.");
        Require(result.AudioCueManifest.Cues.Exists(cue =>
            cue.CueId == "audio.cue.codex_flow_test.sword_slash" &&
            cue.EventPath == "event:/Character/CodexFlowTest/SwordSlash"), "FMOD audio cue should enter audio cue manifest.");
        Require(result.AudioCueManifest.Banks.Contains("Character"), "FMOD audio cue should contribute required banks.");
        Require(result.CharacterResourcePlan.Audio.RequiredCues.Contains("audio.cue.codex_flow_test.sword_slash"), "FMOD audio cue should be required through Audio plan group.");
        Require(result.CharacterResourcePlan.Audio.RequiredBanks.Contains("Character"), "FMOD bank should be required through Audio plan group.");
    }

    private static void CharacterResourcePlanCompiler_WeaponRefDoesNotFallbackToLegacyPreviewKey()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        AuthoringResourceCollection collection = BuildAuthoringSelectionCollection();
        collection.Items.Add(CreateRuntimeResource("runtime.weapon.public", CharacterPackageResourceTypeIds.Model, CharacterPackageResourceUsageIds.WeaponModel, "weapon.public.model", "mainHand", "skeleton.humanoid"));

        package.ApplicationConfig.ResourceKeys.Remove("char.codex_flow_test.weapon.sword.model");
        SingleWeaponConfiguration sword = package.GlobalWeaponLibrary.SingleWeaponConfigurations.Find(
            definition => definition.SingleWeaponConfigurationId == "weapon.000001.main" || definition.SingleWeaponConfigurationId == "sword");
        Require(sword != null, "sword definition missing from sample.");

        sword.WeaponRef = new AuthoringResourceSelectionRef
        {
            ResourceStableId = "runtime.weapon.public",
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            RuntimeResourceKey = "weapon.public.model",
            ProviderResourceKey = "weapon.public.model",
            ExpectedKind = CharacterPackageResourceTypeIds.Model,
            ExpectedUsage = CharacterPackageResourceUsageIds.WeaponModel
        };

        CharacterResourcePlanCompileResult result = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = FindSamplePath("character-codex-flow-test"),
            AuthoringResources = collection
        });

        Require(result.RuntimeResourceCatalog.Entries.Exists(entry => entry.Id == "weapon.public.model"), "weaponRef runtime resource should enter runtime catalog.");
        Require(result.CharacterResourcePlan.EquipmentInitial.Resources.Exists(resource => resource.ResourceKey == "weapon.public.model"), "weaponRef runtime resource should enter EquipmentInitial.");
        Require(!result.CharacterResourcePlan.EquipmentInitial.Resources.Exists(resource => resource.ResourceKey == "char.codex_flow_test.weapon.sword.model"), "legacy previewResourceKey must not re-enter EquipmentInitial when weaponRef is authoritative.");
    }

    private static void CharacterResourcePlanCompiler_PublicWeaponRefUsesGlobalProfileResource()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        AuthoringResourceCollection collection = BuildAuthoringSelectionCollection();
        collection.Items.Add(CreateGlobalProfileResource(
            "global.profile.common.model.weapon.common_weapon_1.model",
            CharacterPackageResourceTypeIds.Model,
            CharacterPackageResourceUsageIds.WeaponModel,
            "weapon.common_weapon_1.model",
            "mainHand",
            "skeleton.humanoid"));

        package.ApplicationConfig.ResourceKeys.Remove("char.codex_flow_test.weapon.sword.model");
        SingleWeaponConfiguration sword = package.GlobalWeaponLibrary.SingleWeaponConfigurations.Find(
            definition => definition.SingleWeaponConfigurationId == "weapon.000001.main" || definition.SingleWeaponConfigurationId == "sword");
        Require(sword != null, "sword definition missing from sample.");

        sword.WeaponRef = new AuthoringResourceSelectionRef
        {
            ResourceStableId = "global.profile.common.model.weapon.common_weapon_1.model",
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            RuntimeResourceKey = "weapon.common_weapon_1.model",
            ProviderResourceKey = "weapon.common_weapon_1.model",
            ExpectedKind = CharacterPackageResourceTypeIds.Model,
            ExpectedUsage = CharacterPackageResourceUsageIds.WeaponModel
        };

        CharacterResourcePlanCompileResult plan = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = FindSamplePath("character-codex-flow-test"),
            AuthoringResources = collection
        });

        Require(plan.RuntimeResourceCatalog.Entries.Exists(entry => entry.Id == "weapon.common_weapon_1.model" && entry.PackageId == "common"), "global profile weaponRef should enter runtime catalog with common package identity.");
        Require(plan.CharacterResourcePlan.EquipmentInitial.Resources.Exists(resource => resource.ResourceKey == "weapon.common_weapon_1.model" && resource.PackageId == "common"), "global profile weaponRef should enter EquipmentInitial with common package identity.");
        Require(!plan.CharacterResourcePlan.EquipmentInitial.Resources.Exists(resource => resource.ResourceKey == "char.codex_flow_test.weapon.sword.model"), "package-local preview key must not enter EquipmentInitial when public weaponRef is authoritative.");

        CharacterAuthoringCompileResult compile = Compile(package);
        PatchEntry weaponConfig = compile.GeneratedConfigPatch.Patch.Entries.Find(entry =>
            entry.Source == CharacterApplicationCompilerTableNames.WeaponConfig &&
            ResourceKeysContain(entry, "weapon.common_weapon_1.model"));

        Require(weaponConfig != null, "generated WeaponConfig should include public weapon resource key.");
        Require(!ResourceKeysContain(weaponConfig, "char.codex_flow_test.weapon.sword.model"), "generated WeaponConfig must not retain the package-local preview resource when weaponRef is public.");
    }

    private static void CharacterResourcePlanCompiler_ResolvesAuthoringSelectionManifest()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.ApplicationConfig.DefaultCombinedWeaponConfigurationId = "codex_flow_test.empty_empty";
        package.ApplicationConfig.CombinedWeaponConfigurationBindings.RemoveAll(b => b.CombinedWeaponConfigurationId != "codex_flow_test.empty_empty");
        package.ApplicationConfig.CombinedWeaponConfigurations?.RemoveAll(c => c.CombinedWeaponConfigurationId != "codex_flow_test.empty_empty");
        var emptyCombined = package.GlobalWeaponLibrary?.CombinedWeaponConfigurations?.Find(c => c.CombinedWeaponConfigurationId == "codex_flow_test.empty_empty");
        if (emptyCombined != null)
        {
            emptyCombined.ReactionConfigurationId = "reaction.codex_flow_test.reaction_profile.default";
        }
        var emptyBinding = package.ApplicationConfig.CombinedWeaponConfigurationBindings.Find(b => b.CombinedWeaponConfigurationId == "codex_flow_test.empty_empty");
        if (emptyBinding != null)
        {
            emptyBinding.Overrides = emptyBinding.Overrides ?? new CharacterCombinedWeaponConfigurationOverrides();
            emptyBinding.Overrides.ReactionConfigurationId = "reaction.codex_flow_test.reaction_profile.default";
        }
        if (package.ApplicationConfig.CombinedWeaponConfigurations != null && package.ApplicationConfig.CombinedWeaponConfigurations.Count > 0)
        {
            package.ApplicationConfig.CombinedWeaponConfigurations[0].ReactionConfigurationId = "reaction.codex_flow_test.reaction_profile.default";
        }
        AuthoringResourceCollection collection = BuildAuthoringSelectionCollection();
        var manifest = new AuthoringResourceSelectionManifestDocument
        {
            PackageId = "codex_flow_test",
            ConsumerKind = "character",
            ConsumerStableId = "character.codex_flow_test"
        };
        manifest.Selections.Add(new AuthoringResourceSelectionCompileInput
        {
            SourceConfigKind = "vfx",
            SourceStableId = "vfx.codex_flow_test.slash",
            SourceField = "prefab",
            FieldSpec = new AuthoringResourceFieldSpec
            {
                FieldKey = "Vfx.Prefab",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Vfx },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.VfxCue },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.ResourceManagerAsset },
                RequireRuntimeLoadable = true,
                PreloadPolicy = AuthoringResourcePreloadPolicies.VfxWarmup,
                OutputKind = AuthoringResourceSelectionOutputKind.RuntimeResourceKey
            },
            Context = new AuthoringResourceConsumerContext
            {
                ConsumerKind = "vfx",
                ConsumerStableId = "vfx.codex_flow_test.slash",
                PackageId = "codex_flow_test"
            },
            Selection = new AuthoringResourceSelectionRef
            {
                ResourceStableId = "runtime.vfx.slash",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset
            }
        });
        manifest.Selections.Add(new AuthoringResourceSelectionCompileInput
        {
            SourceConfigKind = "combatAction",
            SourceStableId = "combat.codex_flow_test.hit",
            SourceField = "hitSfx",
            FieldSpec = new AuthoringResourceFieldSpec
            {
                FieldKey = "CombatAction.HitSfx",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Audio },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.AudioCue },
                AcceptedProviderIds = new List<string> { AuthoringResourceProviderIds.Fmod },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.AudioCue },
                PreloadPolicy = AuthoringResourcePreloadPolicies.AudioBank,
                OutputKind = AuthoringResourceSelectionOutputKind.AudioCueId
            },
            Context = new AuthoringResourceConsumerContext
            {
                ConsumerKind = "combatAction",
                ConsumerStableId = "combat.codex_flow_test.hit",
                PackageId = "codex_flow_test"
            },
            Selection = new AuthoringResourceSelectionRef
            {
                ResourceStableId = "audio.hit",
                SourceProviderId = AuthoringResourceProviderIds.Fmod,
                BindingKind = AuthoringResourceBindingKind.AudioCue
            }
        });

        CharacterResourcePlanCompileResult result = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = FindSamplePath("character-codex-flow-test"),
            AuthoringResources = collection,
            ResourceSelectionManifest = manifest
        });

        Require(result.RuntimeResourceCatalog.Entries.Exists(entry => entry.Id == "char.codex_flow_test.vfx.slash"), "selection manifest should add runtime catalog entries from ResourceSelectionRef.");
        Require(result.CharacterResourcePlan.VfxWarmup.Resources.Exists(resource => resource.ResourceKey == "char.codex_flow_test.vfx.slash"), "selection preload policy should place runtime resource into VfxWarmup.");
        Require(result.AudioCueManifest.Cues.Exists(cue => cue.CueId == "cue.hit"), "audio ResourceSelectionRef should compile into audio cue manifest.");
        Require(result.CharacterResourcePlan.Audio.RequiredCues.Contains("cue.hit"), "audio ResourceSelectionRef should compile into Audio plan group.");
        Require(!result.RuntimeResourceCatalog.Entries.Exists(entry => entry.Id == "cue.hit"), "audio ResourceSelectionRef must not enter ordinary runtime catalog.");
        Require(!result.ResourceValidationReport.HasErrors, "valid ResourceSelectionRef manifest should not block resource plan: " + FormatResourcePlanDiagnostics(result.ResourceValidationReport.Diagnostics));

        var invalidManifest = new AuthoringResourceSelectionManifestDocument
        {
            PackageId = "codex_flow_test",
            ConsumerKind = "ui",
            ConsumerStableId = "ui.codex_flow_test"
        };
        invalidManifest.Selections.Add(new AuthoringResourceSelectionCompileInput
        {
            SourceConfigKind = "ui",
            SourceStableId = "ui.codex_flow_test.icon",
            SourceField = "icon",
            FieldSpec = new AuthoringResourceFieldSpec
            {
                FieldKey = "Ui.Icon",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Texture },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.PreviewThumbnail },
                AcceptedProviderIds = new List<string> { AuthoringResourceProviderIds.UnityAssetDatabase },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.UnityAsset },
                RequireRuntimeLoadable = true,
                PreloadPolicy = AuthoringResourcePreloadPolicies.UiDeferred,
                OutputKind = AuthoringResourceSelectionOutputKind.UnityGuid
            },
            Context = new AuthoringResourceConsumerContext { ConsumerKind = "ui", PackageId = "codex_flow_test" },
            Selection = new AuthoringResourceSelectionRef
            {
                ResourceStableId = "unity.icon.portrait",
                SourceProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                BindingKind = AuthoringResourceBindingKind.UnityAsset
            }
        });

        CharacterResourcePlanCompileResult invalid = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = FindSamplePath("character-codex-flow-test"),
            AuthoringResources = collection,
            ResourceSelectionManifest = invalidManifest
        });

        Require(invalid.ResourceValidationReport.HasErrors, "runtime-required editor-only selection should block resource plan.");
        Require(invalid.ResourceValidationReport.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.EditorOnlySelectedForRuntime), "editor-only runtime field should produce structured diagnostic.");
    }

    private static void CharacterResourcesPlanCommand_WritesRuntimePlanArtifacts()
    {
        string root = Path.Combine(FindRepoRoot(), "Temp", "MxFrameworkAuthoringTests", "issue280-resource-plan");
        ResetDirectory(root);

        int exit = CharacterPackageCommands.Dispatch(new[]
        {
            "character",
            "resources",
            "plan",
            "--package",
            FindSamplePath("character-codex-flow-test"),
            "--out",
            root,
            "--check-files",
            "--check-hashes"
        }, MxFramework.Authoring.Cli.Program.CreateJsonOptions());

        string runtimeCatalogPath = Path.Combine(root, "runtime_resource_catalog.json");
        string resourcePlanPath = Path.Combine(root, "character_resource_plan.json");
        string audioManifestPath = Path.Combine(root, "audio_cue_manifest.json");
        string validationReportPath = Path.Combine(root, "resource_validation_report.json");

        Require(exit == MxFramework.Authoring.Cli.Program.ExitReady, "resource plan command should return ready for Codex Flow Test.");
        Require(File.Exists(runtimeCatalogPath), "resource plan command should write runtime_resource_catalog.json.");
        Require(File.Exists(resourcePlanPath), "resource plan command should write character_resource_plan.json.");
        Require(File.Exists(audioManifestPath), "resource plan command should write audio_cue_manifest.json.");
        Require(File.Exists(validationReportPath), "resource plan command should write resource_validation_report.json.");

        string planJson = File.ReadAllText(resourcePlanPath);
        Require(planJson.Contains("\"spawnCritical\""), "character resource plan should contain SpawnCritical group.");
        Require(planJson.Contains("\"equipmentInitial\""), "character resource plan should contain EquipmentInitial group.");
        Require(planJson.Contains("\"animationWarmup\""), "character resource plan should contain AnimationWarmup group.");
        Require(planJson.Contains("\"uiDeferred\""), "character resource plan should contain UiDeferred group.");
        Require(planJson.Contains("\"audio\""), "character resource plan should contain Audio group.");

        ResourceValidationReportDocument report = JsonSerializer.Deserialize<ResourceValidationReportDocument>(File.ReadAllText(validationReportPath), JsonOptions);
        Require(report != null && report.Status == "Ready", "resource validation report should deserialize as Ready.");
        Require(report.GroupCounts.EquipmentInitial == 1 && (report.GroupCounts.AnimationWarmup == 10 || report.GroupCounts.AnimationWarmup == 111),
            "resource validation report should include the retained sword plus referenced global animation and reaction clips.");
    }

    private static void Compiler_ModelWrapperPoseChangesImportAndResourceMappingHash()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        CharacterPackageResourceEntry body = package.ResourceCatalog.Entries.Find(entry => entry.ResourceKey == "char.codex_flow_test.model.body");
        Require(body != null, "body model resource missing from sample.");

        body.ImportHints.ModelWrapperPose = new CharacterAuthoringLocalPose();
        string baselineImportHash = CharacterPackageResourcePipeline.ComputeImportHash(body);
        CharacterAuthoringCompileResult baseline = Compile(package);

        body.ImportHints.ModelWrapperPose = new CharacterAuthoringLocalPose
        {
            Position = new CharacterAuthoringVector3(0.05f, -0.1f, 0.2f),
            Scale = new CharacterAuthoringVector3(1.25f, 1.25f, 1.25f),
            EulerHint = new CharacterAuthoringVector3(0f, 90f, 0f)
        };

        string adjustedImportHash = CharacterPackageResourcePipeline.ComputeImportHash(body);
        CharacterAuthoringCompileResult adjusted = Compile(package);
        CharacterPackageResourceMappingEntry mappedBody = adjusted.ResourceMapping.Entries.Find(entry => entry.PackageResourceKey == "char.codex_flow_test.model.body");

        Require(baselineImportHash != adjustedImportHash, "model wrapper pose should affect import hash.");
        Require(baseline.Hashes.ResourceMappingHash != adjusted.Hashes.ResourceMappingHash, "model wrapper pose should affect resource mapping hash.");
        Require(mappedBody != null && mappedBody.ModelWrapperPose.Scale.X == 1.25f, "resource mapping should carry model wrapper pose.");
    }

    private static void Compiler_SkeletonBindingsFlowToGeometryBindingAndHashes()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        CharacterBodyPartAuthoring handPart = package.Geometry.BodyParts.Find(part => part.PartId == "right_hand") ?? package.Geometry.BodyParts[0];
        handPart.PartKind = CharacterAuthoringBodyPartKind.Bone;
        handPart.BonePath = "Armature/Hips/Spine/RightShoulder/RightArm/RightForeArm/RightHand";
        handPart.LocatorId = "bone.RightHand";
        handPart.Tags = new List<string> { "hand", "main" };

        CharacterBodyColliderProfile collider = package.Geometry.Colliders[0];
        collider.PartId = handPart.PartId;
        collider.LocalPose = new CharacterAuthoringLocalPose
        {
            Position = new CharacterAuthoringVector3(0.01f, 0.02f, 0.03f),
            Scale = new CharacterAuthoringVector3(1f, 1f, 1f)
        };
        collider.DamageMultiplierOverride = 1.25f;
        collider.ImpactPowerScaleOverride = 0.8f;
        collider.PhysicsLayer = "CharacterHurtbox";
        collider.MaterialStableId = "material.test.scale";

        CharacterSocketProfile mainHand = package.Geometry.Sockets.Find(socket => socket.SocketId == "mainHand") ?? package.Geometry.Sockets[0];
        mainHand.ParentPartId = handPart.PartId;
        mainHand.BonePath = handPart.BonePath;
        mainHand.LocatorPath = string.Empty;
        mainHand.LocalPose = new CharacterAuthoringLocalPose
        {
            Position = new CharacterAuthoringVector3(0.05f, 0.01f, 0.02f),
            EulerHint = new CharacterAuthoringVector3(0f, 90f, 0f),
            Scale = new CharacterAuthoringVector3(1f, 1f, 1f)
        };
        mainHand.MirrorPairSocketId = "offHand";
        mainHand.Handedness = CharacterSocketHandedness.Right;
        mainHand.SideTag = CharacterSocketSideTag.Right;
        mainHand.Tags = new List<string> { "weapon", "main" };

        CharacterSocketProfile offHand = package.Geometry.Sockets.Find(socket => socket.SocketId == "offHand");
        if (offHand == null)
        {
            offHand = new CharacterSocketProfile
            {
                SocketId = "offHand",
                ParentPartId = handPart.PartId,
                BonePath = "Armature/Hips/Spine/LeftShoulder/LeftArm/LeftForeArm/LeftHand",
                Usage = CharacterSocketUsage.Weapon
            };
            package.Geometry.Sockets.Add(offHand);
        }

        SingleWeaponConfiguration definition = package.GlobalWeaponLibrary.SingleWeaponConfigurations.Find(item =>
            item.SingleWeaponConfigurationId == "sword" || item.SingleWeaponConfigurationId == "weapon.000001.main")
            ?? package.GlobalWeaponLibrary.SingleWeaponConfigurations.FirstOrDefault();
        CharacterWeaponSlotProfile weaponSlot = package.Geometry.WeaponSlots.Find(item => item.SlotId == "mainHand");
        weaponSlot.AttachSocketId = "mainHand";
        weaponSlot.SlotPose = new CharacterAuthoringLocalPose
        {
            ParentKind = CharacterPoseParentKind.Socket,
            ParentPath = "mainHand",
            Position = new CharacterAuthoringVector3(0f, -0.02f, 0.01f),
            Scale = new CharacterAuthoringVector3(1f, 1f, 1f)
        };
        SingleWeaponTraceProfile weaponTrace = definition.TraceProfiles[0];
        weaponTrace.Radius = 0.123f;
        weaponTrace.SampleRule = WeaponTraceSampleRule.FixedSamples;

        CharacterAuthoringCompileResult baseline = Compile(package);
        CharacterBodyPartBinding partBinding = baseline.GeometryBinding.BodyParts.Find(part => part.PartLabel == handPart.PartId);
        CharacterBodyColliderBinding colliderBinding = baseline.GeometryBinding.BodyColliders.Find(item => item.ColliderLabel == collider.ColliderId);
        CharacterSocketBinding socketBinding = baseline.GeometryBinding.Sockets.Find(socket => socket.SocketLabel == "mainHand");
        CharacterWeaponAttachmentBinding attachmentBinding = baseline.GeometryBinding.WeaponAttachments.Find(item =>
            item.WeaponLabel == definition.SingleWeaponConfigurationId && item.EquipSlotLabel == "mainHand");
        CharacterWeaponTraceBinding traceBinding = baseline.GeometryBinding.WeaponTraces.Find(item =>
            item.WeaponLabel == definition.SingleWeaponConfigurationId && item.TraceLabel == weaponTrace.TraceId);

        Require(partBinding != null && partBinding.BonePath == handPart.BonePath && partBinding.Tags.Contains("main"), "geometry binding should carry body part bone binding.");
        Require(colliderBinding != null && colliderBinding.LocalPose.ParentKind == CharacterPoseParentKind.BodyPart && colliderBinding.LocalPose.ParentPath == handPart.PartId, "collider local pose should default to its body part parent.");
        Require(colliderBinding != null && colliderBinding.DamageMultiplierOverride == 1.25f && colliderBinding.ImpactPowerScaleOverride == 0.8f, "collider damage and impact overrides should flow to geometry binding.");
        Require(colliderBinding != null && colliderBinding.PhysicsLayer == "CharacterHurtbox" && colliderBinding.MaterialStableId == "material.test.scale", "collider physics layer and material stable id should flow to geometry binding.");
        Require(socketBinding != null && socketBinding.LocalPose.ParentKind == CharacterPoseParentKind.Bone && socketBinding.LocalPose.ParentPath == handPart.BonePath, "socket local pose should default to bone parent.");
        Require(socketBinding.AbstractBoneId == CharacterAbstractBoneIds.RightHand, "socket abstract bone semantic should flow to geometry binding.");
        Require(socketBinding.MirrorPairSocketLabel == "offHand" && socketBinding.SideTag == CharacterSocketSideTag.Right && socketBinding.Tags.Contains("weapon"), "socket binding should carry mirror, side and tags.");
        Require(attachmentBinding != null && attachmentBinding.SlotPose.ParentKind == CharacterPoseParentKind.Socket && attachmentBinding.SlotPose.ParentPath == "mainHand", "character weapon slot pose should retain its socket parent.");
        Require(attachmentBinding.WeaponModelPose.ParentKind == CharacterPoseParentKind.ModelRoot, "weapon model pose should remain weapon-local.");
        Require(traceBinding != null && traceBinding.Radius == 0.123f && traceBinding.SampleRule == WeaponTraceSampleRule.FixedSamples, "single-weapon trace fields should flow to geometry binding.");
        Require(baseline.SourceMappings.Exists(mapping => mapping.TargetPath == "sockets/mainHand" && mapping.TargetField == "socket"), "source mappings should include socket geometry binding targets.");

        mainHand.SideTag = CharacterSocketSideTag.Left;
        CharacterAuthoringCompileResult changed = Compile(package);

        Require(baseline.Hashes.SourcePackageHash != changed.Hashes.SourcePackageHash, "socket side tag should affect source package hash.");
        Require(baseline.Hashes.GeometryBindingHash != changed.Hashes.GeometryBindingHash, "socket side tag should affect geometry binding hash.");
    }

    private static void Compiler_BodyPartContactPoints_EmitStableMillimeterConfig()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        CharacterAuthoringCompileResult result = Compile(package);

        Require(
            result.GeometryBinding.BodyPartContactPoints.Exists(point => point.StableId == "contact.foot_l.sole" && point.PartLabel == "foot_l"),
            "geometry binding should include body part contact point source data.");

        PatchEntry left = FindPatchEntry(
            result,
            CharacterApplicationCompilerTableNames.CharacterBodyPartContactPointConfig,
            "830001");
        Require(left != null, "compiler should emit first body part contact point config row.");
        Require(left.Fields.GetScalar("StableId") == "contact.foot_l.sole", "contact point stable id should compile.");
        Require(left.Fields.GetScalar("PartSetId") == "codex_flow_test_parts", "contact point part set should match generated body profile.");
        Require((left.Fields.ContainsKey("PartLabel") ? left.Fields.GetScalar("PartLabel") : left.Fields.GetScalar("PartId")) == "foot_l", "contact point part id should compile.");
        Require(left.Fields.GetScalar("ContactPointId") == "1", "contact point numeric id should compile.");
        Require(int.TryParse(left.Fields.GetScalar("BodyPartId"), out int bpid) && bpid > 0, $"contact point body part numeric id should compile. Actual: {left.Fields.GetScalar("BodyPartId")}");
        Require(left.Fields.GetScalar("LocalOffsetXMillimeters") == "-29", "contact point x offset should quantize to millimeters.");
        Require(left.Fields.GetScalar("LocalOffsetYMillimeters") == "104", "contact point y offset should quantize to millimeters.");
        Require(left.Fields.GetScalar("LocalOffsetZMillimeters") == "-36", "contact point z offset should quantize to millimeters.");
        Require(left.Fields.GetScalar("RadiusMillimeters") == "90", "contact point radius should quantize to millimeters.");
        Require(left.Fields.GetScalar("Usage") == "PresentationAudio", "contact point usage should compile as presentation audio.");
        Require(left.Fields.GetScalar("Authority") == "PresentationOnly", "contact point authority should compile as presentation only.");
        Require(left.Fields.GetScalar("ActivationMode") == "Always", "contact point activation mode should compile.");
        Require(
            result.ResolverVerificationPlan.RequiredTables.Exists(table => table.TableName == CharacterApplicationCompilerTableNames.CharacterBodyPartContactPointConfig),
            "resolver verification should include the body part contact point table.");
    }

    private static void Compiler_ApplicationResourceKeysAreCharacterReferences()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");

        CharacterAuthoringCompileResult result = Compile(package);
        PatchEntry presentation = result.GeneratedConfigPatch.Patch.Entries.Find(entry => entry.Source == CharacterApplicationCompilerTableNames.CharacterPresentationProfileConfig);
        PatchEntry swordWeapon = result.GeneratedConfigPatch.Patch.Entries.Find(entry =>
            entry.Source == CharacterApplicationCompilerTableNames.WeaponConfig);

        Require(!result.ResourceMapping.Entries.Exists(entry => entry.PackageResourceKey == "weapon.common_weapon_1.model" || entry.PackageResourceKey == "weapon.000001.model"), "public weapon refs should not be copied as package-local resource mappings.");
        Require(!ResourceKeysContain(presentation, "weapon.common_weapon_1.model") && !ResourceKeysContain(presentation, "weapon.000001.model"), "character presentation references should follow application resource keys.");
        Require(swordWeapon != null && (ResourceKeysContain(swordWeapon, "weapon.common_weapon_1.model") || ResourceKeysContain(swordWeapon, "weapon.000001.model")), "weapon config should keep its own public model reference.");
    }

    private static void Compiler_UnsupportedConvexShape_BlocksExport()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.Geometry.Colliders[0].Shape = CharacterColliderShape.Convex;

        CharacterAuthoringCompileResult result = Compile(package);

        Require(result.Status == CharacterAuthoringCompilerStatus.ExportBlocked, "convex collider should block export/importable artifact.");
        Require(!result.UnityImportWritePlan.CanWriteToUnityProject, "ExportBlocked result must not be writable to Unity project.");
        Require(HasCompilerIssue(result, CharacterAuthoringValidationCodes.UnsupportedColliderShape, CharacterAuthoringValidationGate.ExportBlocked), "unsupported collider shape should keep stable ExportBlocked issue.");
    }

    private static void Compiler_MissingSocket_BlocksSpawnOnly()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.Geometry.WeaponSlots[0].AttachSocketId = "missing.socket";

        CharacterAuthoringCompileResult result = Compile(package);

        Require(result.Status == CharacterAuthoringCompilerStatus.SpawnBlocked, "missing weapon socket should block spawn.");
        Require(result.UnityImportWritePlan.CanWriteToUnityProject, "SpawnBlocked result can still import resources/config.");
        Require(!result.UnityImportWritePlan.CanSpawnAfterImport, "SpawnBlocked result must not spawn after import.");
        Require(HasCompilerIssue(result, CharacterAuthoringValidationCodes.CharacterWeaponSlotSocketMissing, CharacterAuthoringValidationGate.SpawnBlocked), "missing character weapon-slot socket should keep stable SpawnBlocked issue.");
    }

    private static void Compiler_MissingResource_BlocksImport()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.ResourceCatalog.Entries[0].RelativePath = "resources/models/missing.glb";

        CharacterAuthoringCompileResult result = Compile(package, checkFiles: true);

        Require(result.Status == CharacterAuthoringCompilerStatus.ImportBlocked, "missing resource should block import.");
        Require(!result.UnityImportWritePlan.CanWriteToUnityProject, "ImportBlocked result must not write Unity project.");
        Require(HasCompilerIssue(result, CharacterAuthoringValidationCodes.MissingResourceFile, CharacterAuthoringValidationGate.ImportBlocked), "missing resource should keep stable ImportBlocked issue.");
    }

    private static void Compiler_CoordinateMismatch_ReportsWarningOnly()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.Manifest.CoordinateConvention.UpAxis = CharacterCoordinateAxis.ZPositive;

        CharacterAuthoringCompileResult result = Compile(package);

        Require(result.Status == CharacterAuthoringCompilerStatus.WarningOnly, "coordinate mismatch should be warning-only when conversion plan is available.");
        Require(result.GeometryBinding.CoordinateConversion.RequiresConversion, "coordinate mismatch should emit conversion plan.");
        Require(result.UnityImportWritePlan.CanWriteToUnityProject, "WarningOnly result should remain importable.");
        Require(HasCompilerIssue(result, CharacterAuthoringCompilerValidationCodes.CoordinateTargetMismatch, CharacterAuthoringValidationGate.WarningOnly), "coordinate mismatch should use stable compiler warning code.");
    }

    private static void Compiler_HashMismatch_BlocksImport()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        package.ResourceCatalog.Entries[0].Hashes.ContentHash = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

        CharacterAuthoringCompileResult result = Compile(package, checkFiles: true, checkHashes: true);

        Require(result.Status == CharacterAuthoringCompilerStatus.ImportBlocked, "hash mismatch should block import.");
        Require(HasCompilerIssue(result, CharacterAuthoringValidationCodes.ResourceHashMismatch, CharacterAuthoringValidationGate.ImportBlocked), "hash mismatch should keep stable ImportBlocked issue.");
    }

    private static void Compiler_ResourceKeyConflict_BlocksImport()
    {
        CharacterResourcePackage package = LoadSample("character-codex-flow-test");
        var projectCatalog = new CharacterPackageResourceCatalog();
        projectCatalog.Entries.Add(new CharacterPackageResourceEntry
        {
            ResourceKey = package.ResourceCatalog.Entries[0].ResourceKey,
            StableId = package.ResourceCatalog.Entries[0].StableId,
            TypeId = package.ResourceCatalog.Entries[0].TypeId,
            Usage = package.ResourceCatalog.Entries[0].Usage,
            RelativePath = "Assets/Existing/codex_flow_test.glb",
            Hashes = new CharacterPackageResourceHashes
            {
                ContentHash = "sha256:1111111111111111111111111111111111111111111111111111111111111111"
            }
        });

        CharacterAuthoringCompileResult result = Compile(package, projectCatalog: projectCatalog);

        Require(result.Status == CharacterAuthoringCompilerStatus.ImportBlocked, "ResourceKey conflict with different hash should block import.");
        Require(HasCompilerIssue(result, CharacterAuthoringCompilerValidationCodes.ResourceKeyConflict, CharacterAuthoringValidationGate.ImportBlocked), "ResourceKey conflict should use stable compiler issue code.");
    }

}
