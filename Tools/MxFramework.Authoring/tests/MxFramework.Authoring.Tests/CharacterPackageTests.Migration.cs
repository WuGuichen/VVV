using System;
using System.IO;
using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    private static void CharacterPackageMigration_NormalizesSupportedLegacySchemasAndBlocksFutureSchemas()
    {
        var package = new CharacterResourcePackage();
        package.Manifest.PackageSchemaVersion = "0.9";
        package.Manifest.SourceSchemaVersion = "0.8";
        package.ResourceCatalog.SchemaVersion = "0.9";
        package.Geometry.SchemaVersion = "0.7";
        package.ApplicationConfig.SchemaVersion = "0.9";
        package.ValidationReport.SchemaVersion = "0.9";
        package.ResourceCatalog.Entries.Add(new CharacterPackageResourceEntry
        {
            Provenance = new CharacterPackageResourceProvenance
            {
                AuthoringSchemaVersion = string.Empty
            }
        });

        CharacterResourcePackageMigrationPlan plan = CharacterResourcePackageMigrator.CreatePlan(package);
        Require(plan.CanMigrate && plan.RequiresMigration && plan.Steps.Count >= 6,
            "legacy character package schemas should produce an explicit migration plan.");

        CharacterResourcePackageMigrator.Apply(package);
        Require(package.Manifest.PackageSchemaVersion == "8.0"
            && package.Manifest.SourceSchemaVersion == "8.0"
            && package.ResourceCatalog.SchemaVersion == "1.0"
            && package.Geometry.SchemaVersion == "1.0"
            && package.Geometry.WeaponSlotsSchemaVersion == "1.0"
            && package.ApplicationConfig.SchemaVersion == "8.0"
            && package.ApplicationConfig.CombinedWeaponConfigurations.Count == 0
            && package.GlobalWeaponLibrary.SingleWeaponConfigurations.Exists(item =>
                item.SingleWeaponConfigurationId == GlobalWeaponLibrarySchema.UnarmedConfigurationId)
            && package.ValidationReport.SchemaVersion == "1.0"
            && package.ResourceCatalog.Entries[0].Provenance.AuthoringSchemaVersion == "1.0",
            "migration should upgrade package and Character Application to 8.0 with public combination bindings and unchanged documents on 1.0.");

        package.Manifest.PackageSchemaVersion = "9.0";
        CharacterResourcePackageMigrationPlan future = CharacterResourcePackageMigrator.CreatePlan(package);
        Require(!future.CanMigrate && future.BlockingReasons.Count > 0,
            "future character package schemas should be blocked rather than downgraded.");
    }

    private static void CharacterPackageMigration_PromotesLegacyWeaponsAndRejectsGlobalConflicts()
    {
        var package = new CharacterResourcePackage
        {
            GlobalWeaponLibrary = GlobalWeaponLibrarySchema.CreateDefault()
        };
        package.ApplicationConfig.SchemaVersion = "2.0";
        package.Geometry.WeaponAttachmentsSchemaVersion = "2.0";
        package.ApplicationConfig.LegacyDefaultWeaponConfigurationId = "sword_empty";
        package.ApplicationConfig.LegacyWeaponDefinitions.Add(new CharacterWeaponDefinitionAuthoringSummary
        {
            WeaponId = "weapon.sword",
            DisplayName = "Sword",
            Category = "OneHandMelee",
            Tags = new System.Collections.Generic.List<string> { "blade" },
            AllowedSlots = new System.Collections.Generic.List<string> { "mainHand", "offHand" },
            WeaponRef = new AuthoringResourceSelectionRef
            {
                SourceProviderId = "public",
                ResourceStableId = "resource.weapon.sword"
            }
        });
        var legacyConfiguration = new CharacterWeaponConfigurationAuthoringSummary
        {
            ConfigurationId = "sword_empty",
            DisplayName = "Sword / Empty",
            AnimationProfileId = "profile.sword_empty",
            ActionSetId = "action_set.sword_empty",
            AbilityLoadoutId = "ability.legacy"
        };
        legacyConfiguration.Slots.Add(new CharacterWeaponConfigurationSlotAuthoringSummary
        {
            SlotId = "mainHand",
            WeaponId = "weapon.sword",
            AttachmentId = "mount.sword.main"
        });
        legacyConfiguration.Slots.Add(new CharacterWeaponConfigurationSlotAuthoringSummary
        {
            SlotId = "offHand",
            WeaponId = string.Empty,
            AttachmentId = "legacy.empty.attachment"
        });
        package.ApplicationConfig.LegacyWeaponConfigurations.Add(legacyConfiguration);

        CharacterResourcePackageMigrationPlan plan = CharacterResourcePackageMigrator.CreatePlan(package);
        Require(plan.CanMigrate && plan.GlobalWeaponLibraryWriteRequired,
            "2.0 local weapon definitions should require a global library write.");
        CharacterResourcePackageMigrator.Apply(package);
        Require(package.GlobalWeaponLibrary.SingleWeaponConfigurations.Exists(item =>
                item.SingleWeaponConfigurationId == "weapon.sword")
            && package.GlobalWeaponLibrary.CombinedWeaponConfigurations.Count == 1
            && package.ApplicationConfig.CombinedWeaponConfigurationBindings.Count == 1
            && package.ApplicationConfig.CombinedWeaponConfigurations.Count == 1,
            "migration should promote the local definition and combination, then bind the character to the public configuration.");
        CombinedWeaponConfigurationAuthoringSummary migrated = package.ApplicationConfig.CombinedWeaponConfigurations[0];
        Require(!string.IsNullOrWhiteSpace(migrated.AbilityConfigurationId) &&
            package.GlobalWeaponLibrary.AbilityConfigurations.Exists(item =>
                item.AbilityConfigurationId == migrated.AbilityConfigurationId),
            "migration should create an explicit complete global ability configuration draft.");
        Require(!string.IsNullOrWhiteSpace(migrated.AnimationConfigurationId) &&
            !string.IsNullOrWhiteSpace(migrated.ReactionConfigurationId) &&
            string.IsNullOrWhiteSpace(migrated.ActionConfiguration.ActionSetId),
            "migration should promote behavior references and remove the legacy hand-authored ActionSet identity.");
        CombinedWeaponConfigurationSlotAuthoringSummary offHand = migrated.Slots.Find(slot => slot.SlotId == "offHand");
        Require(offHand != null
            && offHand.SingleWeaponConfigurationId == GlobalWeaponLibrarySchema.UnarmedConfigurationId,
            "an empty legacy slot should migrate to explicit weapon.unarmed without an attachment field.");

        var schemaSix = new CharacterResourcePackage
        {
            GlobalWeaponLibrary = GlobalWeaponLibrarySchema.CreateDefault()
        };
        schemaSix.Manifest.PackageId = "schema_six";
        schemaSix.ApplicationConfig.SchemaVersion = "6.0";
        schemaSix.GlobalWeaponLibrary.SchemaVersion = "4.0";
        schemaSix.GlobalWeaponLibrary.AbilityConfigurations.Add(new GlobalAbilityConfiguration
        {
            AbilityConfigurationId = "ability.schema_six.sword_empty",
            DisplayName = "Already public and intentionally edited"
        });
        schemaSix.ApplicationConfig.CombinedWeaponConfigurations.Add(new CombinedWeaponConfigurationAuthoringSummary
        {
            CombinedWeaponConfigurationId = "sword_empty",
            DisplayName = "Sword / Empty",
            AbilityConfigurationId = "ability.schema_six.sword_empty"
        });
        CharacterResourcePackageMigrationPlan schemaSixPlan = CharacterResourcePackageMigrator.CreatePlan(schemaSix);
        Require(schemaSixPlan.CanMigrate,
            "schema 6.0 to 8.0 should promote only combined recipes and must not re-promote already public behavior configurations.");
        CharacterResourcePackageMigrator.Apply(schemaSix);
        Require(schemaSix.GlobalWeaponLibrary.SchemaVersion == "6.0" &&
            schemaSix.GlobalWeaponLibrary.CombinedWeaponConfigurations.Exists(item =>
                item.CombinedWeaponConfigurationId == "schema_six.sword_empty") &&
            schemaSix.ApplicationConfig.CombinedWeaponConfigurationBindings.Exists(item =>
                item.CombinedWeaponConfigurationId == "schema_six.sword_empty"),
            "schema 6.0 migration should create a namespaced public combination and a character binding in the current library.");

        var conflicting = new CharacterResourcePackage
        {
            GlobalWeaponLibrary = GlobalWeaponLibrarySchema.CreateDefault()
        };
        conflicting.ApplicationConfig.SchemaVersion = "2.0";
        conflicting.ApplicationConfig.LegacyWeaponDefinitions.Add(new CharacterWeaponDefinitionAuthoringSummary
        {
            WeaponId = "weapon.sword",
            DisplayName = "Local Sword",
            Category = "OneHandMelee"
        });
        conflicting.GlobalWeaponLibrary.SingleWeaponConfigurations.Add(new SingleWeaponConfiguration
        {
            SingleWeaponConfigurationId = "weapon.sword",
            DisplayName = "Different Global Sword",
            Category = "OneHandMelee"
        });
        CharacterResourcePackageMigrationPlan conflictPlan = CharacterResourcePackageMigrator.CreatePlan(conflicting);
        Require(!conflictPlan.CanMigrate
            && conflictPlan.BlockingReasons.Exists(reason =>
                reason.Contains(CharacterAuthoringValidationCodes.WeaponSchemaMigrationGlobalConflict, StringComparison.Ordinal)),
            "same-ID different-content promotion should block the cross-file migration.");

        var ownershipConflict = new CharacterResourcePackage
        {
            GlobalWeaponLibrary = GlobalWeaponLibrarySchema.CreateDefault()
        };
        ownershipConflict.ApplicationConfig.SchemaVersion = "4.0";
        ownershipConflict.Geometry.WeaponAttachments.Add(new WeaponAttachmentProfile
        {
            AttachmentId = "legacy.sword.main",
            SlotId = "mainHand",
            AttachSocketId = "mainHand",
            SingleWeaponConfigurationId = "weapon.sword",
            LocalGripPose = new CharacterAuthoringLocalPose()
        });
        ownershipConflict.Geometry.WeaponAttachments.Add(new WeaponAttachmentProfile
        {
            AttachmentId = "legacy.axe.main",
            SlotId = "mainHand",
            AttachSocketId = "mainHand",
            SingleWeaponConfigurationId = "weapon.axe",
            LocalGripPose = new CharacterAuthoringLocalPose
            {
                Position = new CharacterAuthoringVector3 { X = 0.25f }
            }
        });
        CharacterResourcePackageMigrationPlan ownershipConflictPlan =
            CharacterResourcePackageMigrator.CreatePlan(ownershipConflict);
        Require(!ownershipConflictPlan.CanMigrate
            && ownershipConflictPlan.BlockingReasons.Exists(reason =>
                reason.Contains(CharacterAuthoringValidationCodes.WeaponSlotMigrationConflict, StringComparison.Ordinal)),
            "different legacy poses for one semantic character slot should block migration instead of selecting one.");
    }

    private static void EditorServer_CharacterSave_RejectsStaleRevisionAndMigrationUsesSameGate()
    {
        Require(EditorServer.IsCharacterPackageDraftIssue(CharacterAuthoringValidationCodes.CombinedWeaponActionRoleMissing)
            && EditorServer.IsCharacterPackageDraftIssue(CharacterAuthoringValidationCodes.CombinedWeaponActionRoleDuplicate)
            && EditorServer.IsCharacterPackageDraftIssue(CharacterAuthoringValidationCodes.CombinedWeaponActionIdShared),
            "incomplete combined action bindings should remain saveable drafts and block only export/import readiness.");

        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-character-migration-" + Guid.NewGuid().ToString("N"));
        const string packageRelative = "Tools/MxFramework.Authoring/samples/character-slime";
        string packagePath = Path.Combine(tempRoot, packageRelative.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            CopyDirectory(FindSamplePath("character-slime"), packagePath);
            CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, JsonOptions);
            string loadedRevision = EditorServer.ComputeCharacterPackageRevision(packagePath);

            string manifestPath = Path.Combine(packagePath, "manifest.json");
            CharacterPackageManifest manifest = JsonSerializer.Deserialize<CharacterPackageManifest>(
                File.ReadAllText(manifestPath),
                JsonOptions);
            manifest.DisplayName += " External";
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));

            bool staleSaveRejected = false;
            try
            {
                EditorServer.SaveCharacterPackage(
                    tempRoot,
                    packageRelative,
                    package,
                    JsonOptions,
                    loadedRevision,
                    force: false);
            }
            catch (EditorServer.CharacterPackageWriteConflictException ex)
            {
                staleSaveRejected = ex.ExpectedRevision == loadedRevision
                    && ex.CurrentRevision != loadedRevision;
            }
            Require(staleSaveRejected, "stale character package saves should return a revision conflict.");

            string currentRevision = EditorServer.ComputeCharacterPackageRevision(packagePath);
            manifest.PackageSchemaVersion = "0.9";
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
            bool staleMigrationRejected = false;
            try
            {
                EditorServer.MigrateCharacterPackage(
                    tempRoot,
                    packageRelative,
                    currentRevision,
                    force: false,
                    JsonOptions);
            }
            catch (EditorServer.CharacterPackageWriteConflictException)
            {
                staleMigrationRejected = true;
            }
            Require(staleMigrationRejected, "schema migration should use the same optimistic revision gate as save.");

            string migrationRevision = EditorServer.ComputeCharacterPackageRevision(packagePath);
            EditorServer.MigrateCharacterPackage(
                tempRoot,
                packageRelative,
                migrationRevision,
                force: false,
                JsonOptions);
            CharacterResourcePackage migrated = CharacterPackageCommands.ReadPackage(packagePath, JsonOptions);
            Require(migrated.Manifest.PackageSchemaVersion == "8.0"
                && migrated.ApplicationConfig.SchemaVersion == "8.0"
                && migrated.ApplicationConfig.CombinedWeaponConfigurations.Count == 0,
                "accepted schema migration should persist the current package schema.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_CharacterMigration_UsesDualRevisionAndRollsBackBothFiles()
    {
        string staleRoot = Path.Combine(Path.GetTempPath(), "mx-character-dual-revision-" + Guid.NewGuid().ToString("N"));
        string rollbackRoot = Path.Combine(Path.GetTempPath(), "mx-character-dual-rollback-" + Guid.NewGuid().ToString("N"));
        const string packageRelative = "Tools/MxFramework.Authoring/samples/character-slime";
        try
        {
            string stalePackagePath = Path.Combine(staleRoot, packageRelative.Replace('/', Path.DirectorySeparatorChar));
            CopyDirectory(FindSamplePath("character-slime"), stalePackagePath);
            string staleManifestPath = Path.Combine(stalePackagePath, "manifest.json");
            CharacterPackageManifest staleManifest = JsonSerializer.Deserialize<CharacterPackageManifest>(
                File.ReadAllText(staleManifestPath), JsonOptions);
            staleManifest.PackageSchemaVersion = "0.9";
            File.WriteAllText(staleManifestPath, JsonSerializer.Serialize(staleManifest, JsonOptions));
            string staleLibraryPath = Path.Combine(staleRoot, "WeaponSource", "global_weapon_library.json");
            Directory.CreateDirectory(Path.GetDirectoryName(staleLibraryPath));
            File.WriteAllText(staleLibraryPath, JsonSerializer.Serialize(GlobalWeaponLibrarySchema.CreateDefault(), JsonOptions));
            string packageRevision = EditorServer.ComputeCharacterPackageRevision(stalePackagePath);
            string libraryRevision = EditorServer.ComputeGlobalWeaponLibraryRevision(staleRoot);
            File.AppendAllText(staleLibraryPath, Environment.NewLine);
            bool dualRevisionRejected = false;
            try
            {
                EditorServer.MigrateCharacterPackage(
                    staleRoot,
                    packageRelative,
                    packageRevision,
                    libraryRevision,
                    force: false,
                    JsonOptions);
            }
            catch (EditorServer.GlobalWeaponLibraryWriteConflictException)
            {
                dualRevisionRejected = true;
            }
            Require(dualRevisionRejected,
                "cross-file migration should reject a stale global weapon library revision independently of the character revision.");

            string rollbackPackagePath = Path.Combine(rollbackRoot, packageRelative.Replace('/', Path.DirectorySeparatorChar));
            CopyDirectory(FindSamplePath("character-slime"), rollbackPackagePath);
            string manifestPath = Path.Combine(rollbackPackagePath, "manifest.json");
            CharacterPackageManifest manifest = JsonSerializer.Deserialize<CharacterPackageManifest>(
                File.ReadAllText(manifestPath), JsonOptions);
            manifest.PackageId = string.Empty;
            manifest.PackageSchemaVersion = "0.9";
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
            string manifestBefore = File.ReadAllText(manifestPath);
            bool migrationFailed = false;
            try
            {
                EditorServer.MigrateCharacterPackage(
                    rollbackRoot,
                    packageRelative,
                    EditorServer.ComputeCharacterPackageRevision(rollbackPackagePath),
                    "missing",
                    force: false,
                    JsonOptions);
            }
            catch (Exception ex) when (ex is ArgumentException or EditorServer.CharacterPackageValidationException)
            {
                migrationFailed = true;
            }
            Require(migrationFailed, "invalid migrated package data should fail before the transaction is committed.");
            Require(File.ReadAllText(manifestPath) == manifestBefore,
                "failed migration should restore the character package files exactly.");
            Require(!File.Exists(Path.Combine(rollbackRoot, "WeaponSource", "global_weapon_library.json")),
                "failed migration should remove a newly created global weapon library as part of rollback.");
        }
        finally
        {
            if (Directory.Exists(staleRoot))
                Directory.Delete(staleRoot, recursive: true);
            if (Directory.Exists(rollbackRoot))
                Directory.Delete(rollbackRoot, recursive: true);
        }
    }
}
