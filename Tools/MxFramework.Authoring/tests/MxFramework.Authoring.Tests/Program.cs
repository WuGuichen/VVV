using System;
using System.Collections.Generic;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests;

internal static class Program
{
    public static int Main(string[] args)
    {
        MxFramework.Authoring.CharacterContentIdentityCatalogSnapshot.DefaultAllowAutoRegisterForTests = true;
        try
        {
            if (args.Length == 1 && args[0] == "tactical-card-assembly-isolation")
            {
                TacticalCardAssemblyIsolationTests.Run();
                return 0;
            }
            if (args.Length == 1 && args[0] == "tactical-card-query")
            {
                TacticalCardQueryTests.Run();
                return 0;
            }
            if (args.Length == 1 && args[0] == "vfx-slot-identity")
            {
                int passed = VfxSlotIdentityTests.Run();
                Console.WriteLine("VFX slot identity tests passed: " + passed);
                return 0;
            }
            if (args.Length == 1 && args[0] == "ui-composer-fonts")
            {
                UiComposerAuthoringTests.PreservesExplicitFontsThroughPublishAndSync();
                Console.WriteLine("UI Composer font publish/sync test passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "editor-hub-diagnostics")
            {
                CharacterPackageTests.RunEditorHubDiagnostics();
                Console.WriteLine("Editor Hub diagnostic and snapshot tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-tactical-defaults")
            {
                CharacterPackageTests.RunTacticalDefaults();
                Console.WriteLine("Character tactical default compiler tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-basic-attributes")
            {
                CharacterPackageTests.RunBasicAttributes();
                Console.WriteLine("Character basic attribute compiler tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "humanoid-hurtboxes")
            {
                CharacterHumanoidHurtboxTemplateTests.RunAll();
                Console.WriteLine("Humanoid eleven-hurtbox template tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "projectile")
            {
                ProjectileAuthoringTests.RunAll();
                Console.WriteLine("ProjectileAuthoringTests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "projectile-sync")
            {
                ProjectileAuthoringTests.RunSynchronizedApply();
                Console.WriteLine("Projectile synchronized apply tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "fairygui-atlases")
            {
                ResourceMappingProfileV2Tests.RunFairyGuiAtlasChecks();
                Console.WriteLine("FairyGUI atlas resource checks passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "resource-mapping")
            {
                ResourceMappingProfileV2Tests.RunAll();
                Console.WriteLine("ResourceMappingProfileV2Tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "animation-profile-slots")
            {
                CharacterPackageTests.RunAnimationProfileSlots();
                Console.WriteLine("Animation profile-slot authoring tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "common-resources")
            {
                CharacterPackageTests.RunCommonResources();
                Console.WriteLine("Common resource authoring tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "blade-trail-styles")
            {
                CharacterPackageTests.RunBladeTrailStyles();
                Console.WriteLine("Blade trail authoring round-trip, compiler, fingerprint and validation checks passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-weapons")
            {
                CharacterPackageTests.RunWeaponConfigurationContract();
                Console.WriteLine("Character public combined-weapon contract tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-actions")
            {
                CharacterPackageTests.RunCharacterActions();
                Console.WriteLine("Character action source, compiler, and transaction tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-lifecycle-actions")
            {
                CharacterPackageTests.RunCharacterLifecycleActions();
                Console.WriteLine("Character lifecycle action-library tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-readiness-dedupe")
            {
                CharacterPackageTests.RunCharacterReadinessDedupe();
                Console.WriteLine("Character readiness deduplication tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-save-draft")
            {
                CharacterPackageTests.RunCharacterSaveDraft();
                Console.WriteLine("Character draft-save tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-unity-import-receipt")
            {
                CharacterPackageTests.RunCharacterUnityImportReceipt();
                Console.WriteLine("Character Unity import receipt tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-unity-import-bridge")
            {
                CharacterPackageTests.RunCharacterUnityImportBridge();
                Console.WriteLine("Character Unity import bridge tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-export-layout")
            {
                CharacterPackageTests.RunCharacterExportLayout();
                Console.WriteLine("Character export layout tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-animation-runtime-plan")
            {
                CharacterPackageTests.RunCharacterAnimationRuntimePlan();
                Console.WriteLine("Character animation runtime plan tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-preview-glb")
            {
                CharacterPackageTests.RunCharacterPreviewGlb();
                Console.WriteLine("Character preview GLB tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-abstract-bones")
            {
                CharacterPackageTests.RunCharacterAbstractBones();
                Console.WriteLine("Character abstract-bone contract tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-reaction-projection")
            {
                CharacterPackageTests.RunCharacterReactionProjection();
                Console.WriteLine("Character reaction projection ownership tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-generic-rig")
            {
                CharacterPackageTests.RunCharacterGenericRig();
                Console.WriteLine("Character Generic rig authoring tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-gameplay-camera")
            {
                CharacterPackageTests.RunCharacterGameplayCameraContract();
                Console.WriteLine("Character gameplay camera authoring tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-hurtbox")
            {
                CharacterPackageTests.RunCharacterHurtboxContract();
                Console.WriteLine("Character hurtbox authoring, compiler, and receipt tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-damage-zones")
            {
                CharacterPackageTests.RunCharacterDamageZones();
                Console.WriteLine("Character DamageZone authoring and compiler tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-engagement-anchors")
            {
                CharacterPackageTests.RunCharacterEngagementAnchors();
                Console.WriteLine("Character EngagementAnchor authoring and compiler tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-body-presence")
            {
                CharacterPackageTests.RunCharacterBodyPresence();
                Console.WriteLine("Character Body Presence authoring tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-body-part-damage")
            {
                CharacterPackageTests.RunCharacterBodyPartDamage();
                Console.WriteLine("Character body-part-damage authoring tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-authority-pose")
            {
                CharacterPackageTests.RunCharacterAuthorityPose();
                Console.WriteLine("Character authority-pose authoring tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "lcc-m0-integration")
            {
                CharacterPackageTests.RunLccM0Integration();
                Console.WriteLine("LCC M0 CharacterStudio and service integration tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-source-contract")
            {
                CharacterSourceContractTests.RunAll();
                Console.WriteLine("Character source-contract tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-skeleton-contract")
            {
                CharacterSkeletonContractTests.RunAll();
                Console.WriteLine("Character skeleton-contract tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-palette-usage")
            {
                CharacterPaletteUsageAuthorityTests.RunAll();
                Console.WriteLine("Character palette-usage authority tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "character-import-materials")
            {
                CharacterPackageTests.RunCharacterImportMaterials();
                Console.WriteLine("Character raw-material import tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "audio-workbench-fmod-script")
            {
                CharacterPackageTests.RunAudioWorkbenchFmodScript();
                Console.WriteLine("Audio Workbench FMOD script tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "audio-workbench-server")
            {
                CharacterPackageTests.RunAudioWorkbenchServer();
                Console.WriteLine("Audio Workbench server tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "fairygui-art-slice")
            {
                FairyGuiArtSliceAuthoringTests.RunAll();
                Console.WriteLine("FairyGUI art-slice authoring tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "runtime-ai-lab-ui-composer")
            {
                FairyGuiArtSliceAuthoringTests.RunRuntimeAiLabComposerPreflight();
                Console.WriteLine("Runtime AI Lab UI Composer preflight passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "compact-icon-button")
            {
                FairyGuiArtSliceAuthoringTests.CompactIconButton_MaterializesCenteredGlyph();
                Console.WriteLine("Compact icon-button materialization test passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "ui-composer-icon-centering")
            {
                UiComposerAuthoringTests.RunIconOnlyButtonCentering();
                Console.WriteLine("UI Composer icon-only button centering test passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "ui-composer")
            {
                UiComposerAuthoringTests.RunAll();
                Console.WriteLine("UI Composer authoring tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "ui-composer-publish-all")
            {
                UiComposerAuthoringTests.RunPublishAll();
                Console.WriteLine("UI Composer publish-all tests passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "ui-composer-list")
            {
                UiComposerAuthoringTests.RunControlledListNode();
                Console.WriteLine("UI Composer controlled List node test passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "ui-composer-player-guidance")
            {
                UiComposerAuthoringTests.RunPlayerGuidancePackage();
                Console.WriteLine("UI Composer Player Guidance package test passed.");
                return 0;
            }
            if (args.Length == 1 && args[0] == "ui-composer-character-test-player-panels")
            {
                UiComposerAuthoringTests.RunCharacterTestPlayerPanels();
                Console.WriteLine("UI Composer CharacterTest player-panel tests passed.");
                return 0;
            }
            PackageValidator_BlocksBaseWrites();
            PatchMerger_ReturnsLatestRecord();
            LayeredMerger_PatchSitsBetween();
            FieldValue_ListAndMapAreSupported();
            ResourceMappingProfileV2Tests.RunAll();
            ArtSourceAuthoringResourceProviderTests.RunAll();
            StorySourceAuthoringResourceProviderTests.RunAll();
            CharacterActionAuthoringResourceProviderTests.RunAll();
            AbilityPackageTests.RunAll();
            ProjectileAuthoringTests.RunAll();
            BuffPackageTests.RunAll();
            FairyGuiArtSliceAuthoringTests.RunAll();
            VfxAuthoringCatalogTests.RunAll();
            UiComposerAuthoringTests.RunAll();
            VfxReferencePackTests.RunAll();
            CharacterPaletteUsageAuthorityTests.RunAll();
            VfxSourceLibraryTests.RunAll();
            VfxCreativeFlywheelTests.RunAll();
            VfxDevelopmentAgentTaskTests.RunAll();
            CharacterPackageTests.RunAll();
            CharacterSourceContractTests.RunAll();
            CharacterSkeletonContractTests.RunAll();
            ModDiagnoseCommandTests.RunAll();
            Console.WriteLine("MxFramework.Authoring.Tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void PackageValidator_BlocksBaseWrites()
    {
        var patch = new PatchDocument { SchemaVersion = "1.0", Source = "RuntimeConfig" };
        patch.Entries.Add(new PatchEntry
        {
            Operation = PatchOperation.Upsert,
            Source = "RuntimeConfig",
            Id = "1",
            Layer = "Base"
        });

        var report = PackageValidator.Validate(new ModPackageManifest
        {
            PackageId = "package.validator.test",
            SchemaVersion = "1"
        }, new[] { patch });

        Require(report.HasErrors, "Base writes should be blocked.");
        Require(report.Issues.Exists(issue => issue.Code == "entry.baseWrite"), "Base write issue missing.");
    }

    private static void PatchMerger_ReturnsLatestRecord()
    {
        var patch = new PatchDocument { SchemaVersion = "1.0", Source = "RuntimeConfig" };
        patch.Entries.Add(MakeEntry("1", "Patch", PatchOperation.Upsert, ("Name", "first")));
        patch.Entries.Add(MakeEntry("1", "Patch", PatchOperation.Upsert, ("Name", "second")));

        MergePreview preview = PatchMerger.Merge(patch);

        Require(preview.Records.Count == 1, "Merge should contain one final record.");
        Require(preview.Records[0].ChangeKind == "Replaced", "Second upsert should replace first.");
        Require(preview.Records[0].Fields["Name"].Scalar == "second", "Latest field value was not preserved.");
    }

    private static void LayeredMerger_PatchSitsBetween()
    {
        var baseDoc = MakeDoc("RuntimeConfig", MakeEntry("1", "Base", PatchOperation.Upsert, ("Name", "fromBase"), ("Duration", "1000")));
        var patchDoc = MakeDoc("RuntimeConfig", MakeEntry("1", "Patch", PatchOperation.Upsert, ("Duration", "2000")));
        var modDoc = MakeDoc("RuntimeConfig", MakeEntry("1", "Mod", PatchOperation.Upsert, ("Name", "fromMod")));

        MergePreview preview = LayeredMerger.Merge(new[] { baseDoc }, new[] { patchDoc }, new[] { modDoc });

        Require(preview.Records.Count == 1, "Layered merge should produce one record.");
        Require(preview.Records[0].Fields["Duration"].Scalar == "2000", "Patch layer should override Base duration.");
        Require(preview.Records[0].Fields["Name"].Scalar == "fromMod", "Mod layer should override Patch/Base name.");
    }

    private static void FieldValue_ListAndMapAreSupported()
    {
        var entry = new PatchEntry { Operation = PatchOperation.Upsert, Source = "RuntimeConfig", Id = "1", Layer = "Mod" };
        entry.Fields["Tags"] = FieldValue.FromList(new[]
        {
            FieldValue.FromScalar("fire"),
            FieldValue.FromScalar("aura")
        });
        entry.Fields["Meta"] = FieldValue.FromMap(new Dictionary<string, FieldValue>
        {
            ["school"] = FieldValue.FromScalar("protection"),
            ["tier"] = FieldValue.FromScalar("1")
        });

        Require(entry.Fields["Tags"].Kind == FieldValueKind.List, "List field kind missing.");
        Require(entry.Fields["Tags"].List.Count == 2, "List field values missing.");
        Require(entry.Fields["Meta"].Kind == FieldValueKind.Map, "Map field kind missing.");
        Require(entry.Fields["Meta"].Map["school"].Scalar == "protection", "Map field values missing.");
    }

    private static PatchDocument MakeDoc(string source, params PatchEntry[] entries)
    {
        var doc = new PatchDocument { SchemaVersion = "1.0", Source = source };
        doc.Entries.AddRange(entries);
        return doc;
    }

    private static PatchEntry MakeEntry(string id, string layer, PatchOperation op, params (string k, string v)[] fields)
    {
        var entry = new PatchEntry { Operation = op, Source = "RuntimeConfig", Id = id, Layer = layer };
        for (int i = 0; i < fields.Length; i++)
            entry.Fields[fields[i].k] = FieldValue.FromScalar(fields[i].v);
        return entry;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
