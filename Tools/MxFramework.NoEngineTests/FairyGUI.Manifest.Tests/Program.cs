using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MxFramework.Authoring.FairyGuiArtSlices;
using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.NoEngineTests.FairyGUI.Manifest
{
    internal static partial class Program
    {
        private static int Main(string[] args)
        {
            if (args != null && args.Length > 0)
                return RunCommand(args);

            ValidManifest_HasNoDiagnostics();
            RuntimeHudSourceManifest_ValidatesCurrentPackageSources();
            RuntimeHudManifestProjection_FeedsViewContract();
            RuntimeHudManifestGenerator_ProducesCurrentManifestMetadata();
            RuntimeHudGeneratedManifest_IsCurrent();
            StoryDialogSourceManifest_ValidatesCurrentPackageSources();
            StoryDialogManifestProjection_FeedsViewContract();
            StoryDialogManifestGenerator_ProducesCurrentManifestMetadata();
            StoryDialogGeneratedManifest_IsCurrent();
            CharacterTestCombatHudManifestProjection_IncludesAtlasAndHudControls();
            CharacterTestCombatHudManifestGenerator_ProducesCurrentManifestMetadata();
            CharacterTestCombatHudGeneratedManifest_IsCurrent();
            CharacterTestCombatHudGeneratedBinder_IsCurrent();
            CharacterTestCombatHudBinderGenerator_ProducesCurrentSource();
            CharacterTestCombatHudComposerBindingContract_IsCurrent();
            CharacterTestSettingsPanelManifestProjection_IncludesAtlasAndCommands();
            CharacterTestSettingsPanelManifestGenerator_ProducesCurrentManifestMetadata();
            CharacterTestSettingsPanelGeneratedManifest_IsCurrent();
            CharacterTestSettingsPanelGeneratedBinder_IsCurrent();
            CharacterTestSettingsPanelBinderGenerator_ProducesCurrentSource();
            CharacterTestSettingsPanelComposerBindingContract_IsCurrent();
            CharacterTestNetworkPanelManifestProjection_IncludesAtlasAndCommands();
            CharacterTestNetworkPanelManifestGenerator_ProducesCurrentManifestMetadata();
            CharacterTestNetworkPanelGeneratedManifest_IsCurrent();
            CharacterTestNetworkPanelGeneratedBinder_IsCurrent();
            CharacterTestNetworkPanelBinderGenerator_ProducesCurrentSource();
            CharacterTestMainMenuManifestProjection_IncludesAtlasAndCommands();
            CharacterTestMainMenuManifestGenerator_ProducesCurrentManifestMetadata();
            CharacterTestMainMenuGeneratedManifest_IsCurrent();
            CharacterTestMainMenuGeneratedBinder_IsCurrent();
            CharacterTestMainMenuBinderGenerator_ProducesCurrentSource();
            CharacterTestMainMenuComposerBindingContract_IsCurrent();
            CharacterTestAbilityBindingPanelManifestProjection_IncludesAtlasAndCommands();
            CharacterTestAbilityBindingPanelManifestGenerator_ProducesCurrentManifestMetadata();
            CharacterTestAbilityBindingPanelGeneratedManifest_IsCurrent();
            CharacterTestAbilityBindingPanelGeneratedBinder_IsCurrent();
            CharacterTestAbilityBindingPanelBinderGenerator_ProducesCurrentSource();
            CharacterTestBehaviorDeckPanelManifestProjection_IncludesAtlasAndCommands();
            CharacterTestBehaviorDeckPanelManifestGenerator_ProducesCurrentManifestMetadata();
            CharacterTestBehaviorDeckPanelGeneratedManifest_IsCurrent();
            CharacterTestBehaviorDeckPanelGeneratedBinder_IsCurrent();
            CharacterTestBehaviorDeckPanelBinderGenerator_ProducesCurrentSource();
            CharacterTestBehaviorDeckPanelPublishedCards_ContainLocalFeedbackControls();
            CharacterTestStoryDialogManifestProjection_IncludesCommands();
            CharacterTestStoryDialogManifestGenerator_ProducesCurrentManifestMetadata();
            CharacterTestStoryDialogGeneratedManifest_IsCurrent();
            CharacterTestStoryDialogGeneratedBinder_IsCurrent();
            CharacterTestStoryDialogBinderGenerator_ProducesCurrentSource();
            UiFoundationDemoManifestProjection_IncludesAtlasAndCommands();
            UiFoundationDemoGeneratedOutputs_AreCurrent();
            ManifestBatchCheck_CoversRuntimeHudStoryDialogCombatHudSettingsPanelNetworkPanelMainMenuAbilityBindingPanelAndCharacterTestStoryDialog();
            ManifestReviewReport_IsDeterministicAndCoversBatchTargets();
            RuntimeHudGeneratedManifest_MismatchDiagnostic();
            CharacterTestSettingsPanelGeneratedBinder_MismatchDiagnostic();
            LocalizationBindings_ValidateTextControlsAndRequests();
            LocalizationBindings_ReportMissingKeyAndNonTextControl();
            LocalizationBindings_ReportSourceXmlDrift();
            RuntimeHudSourceManifest_ReportsMissingControl();
            RuntimeHudSourceManifest_ReportsMissingCommandButton();
            InvalidManifest_ReturnsStructuredDiagnostics();
            InvalidSchemaVersion_IsReported();
            CommandBoundToTextControl_IsReported();
            DuplicateIds_AreReported();
            FeedbackPolicy_DefaultsProjectsAndGeneratesExplicitly();
            FairyGuiReviewReportBuilderTests.RunAll();
            FairyGuiArtSliceCatalogTests.RunAll(FindRepositoryRoot());
            Console.WriteLine("FairyGUI manifest noEngine tests passed.");
            return 0;
        }

        private static int RunCommand(string[] args)
        {
            if (args[0] == "--write-ai-lab-card-trial-contract") return WriteAiLabCardTrialContract();
            if (args[0] == "--check-ai-lab-card-trial-plan") return CheckAiLabCardTrialPlan();
            if (args[0] == "--check-character-test-main-menu")
            {
                CharacterTestMainMenuManifestProjection_IncludesAtlasAndCommands();
                CharacterTestMainMenuGeneratedManifest_IsCurrent();
                CharacterTestMainMenuGeneratedBinder_IsCurrent();
                CharacterTestMainMenuComposerBindingContract_IsCurrent();
                Console.WriteLine("CharacterTest main menu manifest, binder and Composer contract checks passed.");
                return 0;
            }
            if (args[0] == "--write-tactical-preparation-contract") return WriteTacticalPreparationContract();
            if (args[0] == "--check-tactical-preparation") return CheckTacticalPreparation();
            if (args[0] == "--check-tactical-preparation-plan") return CheckTacticalPreparation(sourceOnly: true);
            if (args[0] == "--write-tactical-preparation-manifest") { WriteGeneratedManifest(FindTarget("character-test-tactical-deck-panel")); return 0; }
            string command = args[0] ?? string.Empty;
            if (command == "--write-tactical-preview-contract") return WriteTacticalPreviewContract();
            if (string.Equals(command, "--write-runtime-hud-manifest", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("runtime-hud");
                WriteGeneratedManifest(target);
                return 0;
            }

            if (string.Equals(command, "--write-story-dialog-manifest", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("story-dialog");
                WriteGeneratedManifest(target);
                return 0;
            }

            if (string.Equals(command, "--write-character-test-settings-panel-manifest", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("character-test-settings-panel");
                WriteGeneratedManifest(target);
                return 0;
            }

            if (string.Equals(command, "--write-character-test-combat-hud-manifest", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("character-test-combat-hud");
                WriteGeneratedManifest(target);
                return 0;
            }

            if (string.Equals(command, "--write-character-test-combat-hud-binder", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("character-test-combat-hud");
                WriteGeneratedBinder(target);
                return 0;
            }

            if (string.Equals(command, "--write-character-test-combat-hud-composer-contract", StringComparison.Ordinal))
            {
                WriteCharacterTestCombatHudComposerBindingContract();
                return 0;
            }

            if (string.Equals(command, "--write-character-test-settings-panel-binder", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("character-test-settings-panel");
                WriteGeneratedBinder(target);
                return 0;
            }

            if (string.Equals(command, "--write-character-test-settings-panel-composer-contract", StringComparison.Ordinal))
            {
                WriteCharacterTestSettingsPanelComposerBindingContract();
                return 0;
            }

            if (string.Equals(command, "--write-character-test-network-panel-manifest", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("character-test-network-panel");
                WriteGeneratedManifest(target);
                return 0;
            }

            if (string.Equals(command, "--write-character-test-network-panel-binder", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("character-test-network-panel");
                WriteGeneratedBinder(target);
                return 0;
            }

            if (string.Equals(command, "--write-character-test-main-menu-manifest", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("character-test-main-menu");
                WriteGeneratedManifest(target);
                return 0;
            }

            if (string.Equals(command, "--write-character-test-main-menu-binder", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("character-test-main-menu");
                WriteGeneratedBinder(target);
                return 0;
            }

            if (string.Equals(command, "--write-character-test-main-menu-composer-contract", StringComparison.Ordinal))
            {
                WriteCharacterTestMainMenuComposerBindingContract();
                return 0;
            }

            if (string.Equals(command, "--write-character-test-ability-binding-panel-manifest", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("character-test-ability-binding-panel");
                WriteGeneratedManifest(target);
                return 0;
            }

            if (string.Equals(command, "--write-character-test-ability-binding-panel-binder", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("character-test-ability-binding-panel");
                WriteGeneratedBinder(target);
                return 0;
            }

            if (string.Equals(command, "--write-character-test-behavior-deck-panel-manifest", StringComparison.Ordinal))
            {
                WriteGeneratedManifest(FindTarget("character-test-behavior-deck-panel"));
                return 0;
            }

            if (string.Equals(command, "--write-character-test-behavior-deck-panel-binder", StringComparison.Ordinal))
            {
                WriteGeneratedBinder(FindTarget("character-test-behavior-deck-panel"));
                return 0;
            }

            if (string.Equals(command, "--check-character-test-behavior-deck-panel", StringComparison.Ordinal))
            {
                CharacterTestBehaviorDeckPanelManifestProjection_IncludesAtlasAndCommands();
                CharacterTestBehaviorDeckPanelManifestGenerator_ProducesCurrentManifestMetadata();
                CharacterTestBehaviorDeckPanelGeneratedManifest_IsCurrent();
                CharacterTestBehaviorDeckPanelGeneratedBinder_IsCurrent();
                CharacterTestBehaviorDeckPanelBinderGenerator_ProducesCurrentSource();
                Console.WriteLine("CharacterTest BehaviorDeckPanel generated contract is current.");
                return 0;
            }

            if (string.Equals(command, "--write-character-test-story-dialog-manifest", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("character-test-story-dialog");
                WriteGeneratedManifest(target);
                return 0;
            }

            if (string.Equals(command, "--write-character-test-story-dialog-binder", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("character-test-story-dialog");
                WriteGeneratedBinder(target);
                return 0;
            }

            if (string.Equals(command, "--write-ui-foundation-demo-manifest", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("ui-foundation-demo");
                WriteGeneratedManifest(target);
                return 0;
            }

            if (string.Equals(command, "--write-ui-foundation-demo-binder", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("ui-foundation-demo");
                WriteGeneratedBinder(target);
                return 0;
            }

            if (string.Equals(command, "--write-player-guidance-card", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("player-guidance-card");
                WriteGeneratedManifest(target);
                WriteGeneratedBinder(target);
                return 0;
            }

            if (string.Equals(command, "--write-player-guidance-notices", StringComparison.Ordinal))
            {
                ManifestTarget target = FindTarget("player-guidance-notices");
                WriteGeneratedManifest(target);
                WriteGeneratedBinder(target);
                return 0;
            }

            if (string.Equals(command, "--write-manifests", StringComparison.Ordinal))
            {
                ManifestTarget[] targets = CreateManifestTargets();
                for (int i = 0; i < targets.Length; i++)
                {
                    WriteGeneratedManifest(targets[i]);
                    WriteGeneratedBinder(targets[i]);
                }

                WriteCharacterTestMainMenuComposerBindingContract();
                WriteCharacterTestCombatHudComposerBindingContract();
                WriteCharacterTestSettingsPanelComposerBindingContract();

                return 0;
            }

            if (string.Equals(command, "--check-generated", StringComparison.Ordinal))
            {
                MxFairyGuiManifestGenerationBatchResult result = GenerateManifestBatch(validateGeneratedOutput: true);
                Require(result.Success, "FairyGUI generated manifest/binder batch is not current.\n" + MxFairyGuiManifestGenerator.GenerateReviewReport(result));
                CharacterTestMainMenuComposerBindingContract_IsCurrent();
                CharacterTestCombatHudComposerBindingContract_IsCurrent();
                CharacterTestSettingsPanelComposerBindingContract_IsCurrent();
                Console.WriteLine("FairyGUI generated manifest/binder output is current for " + result.Items.Count + " targets.");
                return 0;
            }

            if (string.Equals(command, "--review-report", StringComparison.Ordinal))
            {
                MxFairyGuiManifestGenerationBatchResult result = GenerateManifestBatch(validateGeneratedOutput: true);
                FairyGuiArtSliceValidationResult artSliceResult = FairyGuiArtSliceCatalogTool.ValidateCharacterTest(FindRepositoryRoot());
                string report = FairyGuiReviewReportBuilder.Generate(result, artSliceResult);
                if (args.Length > 1)
                {
                    File.WriteAllText(args[1], report);
                    Console.WriteLine("Wrote FairyGUI manifest review report: " + args[1]);
                }
                else
                {
                    Console.Write(report);
                }

                Require(result.Success, "FairyGUI manifest review report contains errors.");
                Require(artSliceResult.Success, "FairyGUI art slice review summary contains errors.\n" + FairyGuiArtSliceCatalogTool.GenerateMarkdownReport(artSliceResult));
                return 0;
            }

            if (string.Equals(command, "--check-art-slices", StringComparison.Ordinal))
            {
                FairyGuiArtSliceValidationResult result = FairyGuiArtSliceCatalogTool.ValidateCharacterTest(FindRepositoryRoot());
                Require(result.Success, "FairyGUI art slice catalog contains errors.\n" + FairyGuiArtSliceCatalogTool.GenerateMarkdownReport(result));
                Console.WriteLine("FairyGUI art slice catalog is valid for "
                    + result.PackageId
                    + " with "
                    + result.Diagnostics.Count
                    + " diagnostics.");
                return 0;
            }

            if (string.Equals(command, "--art-slice-report", StringComparison.Ordinal))
            {
                FairyGuiArtSliceValidationResult result = FairyGuiArtSliceCatalogTool.ValidateCharacterTest(FindRepositoryRoot());
                Console.Write(FairyGuiArtSliceCatalogTool.GenerateMarkdownReport(result));
                Require(result.Success, "FairyGUI art slice report contains errors.");
                return 0;
            }

            if (string.Equals(command, "--write-art-slice-report", StringComparison.Ordinal))
            {
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("--write-art-slice-report requires a destination path.");
                    return 2;
                }

                FairyGuiArtSliceValidationResult result = FairyGuiArtSliceCatalogTool.ValidateCharacterTest(FindRepositoryRoot());
                string reportPath = args[1];
                string report = string.Equals(Path.GetExtension(reportPath), ".json", StringComparison.OrdinalIgnoreCase)
                    ? FairyGuiArtSliceCatalogTool.GenerateJsonReport(result)
                    : FairyGuiArtSliceCatalogTool.GenerateMarkdownReport(result);
                string directory = Path.GetDirectoryName(reportPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(reportPath, report);
                Console.WriteLine("Wrote FairyGUI art slice report: " + reportPath);
                Require(result.Success, "FairyGUI art slice report contains errors.");
                return 0;
            }

            Console.Error.WriteLine("Unknown FairyGUI manifest test command: " + command);
            Console.Error.WriteLine("Supported commands: --check-generated, --check-character-test-behavior-deck-panel, --review-report [path], --check-art-slices, --art-slice-report, --write-art-slice-report <path>, --write-manifests, --write-runtime-hud-manifest, --write-story-dialog-manifest, --write-character-test-combat-hud-manifest, --write-character-test-combat-hud-binder, --write-character-test-settings-panel-manifest, --write-character-test-settings-panel-binder, --write-character-test-network-panel-manifest, --write-character-test-network-panel-binder, --write-character-test-main-menu-manifest, --write-character-test-main-menu-binder, --write-character-test-ability-binding-panel-manifest, --write-character-test-ability-binding-panel-binder, --write-character-test-behavior-deck-panel-manifest, --write-character-test-behavior-deck-panel-binder, --write-character-test-story-dialog-manifest, --write-character-test-story-dialog-binder, --write-ui-foundation-demo-manifest, --write-ui-foundation-demo-binder, --write-player-guidance-card, --write-player-guidance-notices");
            return 2;
        }

        private static void ValidManifest_HasNoDiagnostics()
        {
            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.Validate(CreateValidManifest());

            Require(result.Success, "valid manifest should pass validation.");
            Require(result.Diagnostics.Count == 0, "valid manifest should not produce diagnostics.");
        }

        private static void RuntimeHudSourceManifest_ValidatesCurrentPackageSources()
        {
            MxFairyGuiManifest manifest = CreateRuntimeHudManifest();
            ResourceCatalog catalog = CreateRuntimeHudCatalog();

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.ValidateSources(
                manifest,
                FindRepositoryRoot(),
                catalog);

            Require(result.Success, "Runtime HUD manifest should validate against source XML and package bytes.");
        }

        private static void RuntimeHudManifestProjection_FeedsViewContract()
        {
            MxFairyGuiManifest manifest = CreateRuntimeHudManifest();
            var contracts = MxFairyGuiManifestProjection.CreateViewContracts(manifest);

            Require(contracts.Count == 1, "Runtime HUD manifest should project one view contract.");
            MxUiViewContract contract = contracts[0];
            Require(contract.Descriptor.Id == new MxUiViewId("ui.runtimehud.main"), "projected view id should match manifest.");
            Require(contract.Descriptor.PackageKey == "MxRuntimeHud", "projected package key should match manifest package.");
            Require(contract.Descriptor.ComponentName == "RuntimeHudPanel", "projected component should match manifest component.");
            Require(contract.Descriptor.Layer == MxUiLayer.Hud, "projected layer should match manifest layer.");
            Require(contract.Commands.Count == 2, "projected commands should come from manifest command bindings.");
            Require(contract.RequiredResources.Count == 1 && contract.RequiredResources[0] == "ui.fairygui.runtimehud.fui", "projected resources should include package bytes id.");
        }

        private static void RuntimeHudManifestGenerator_ProducesCurrentManifestMetadata()
        {
            MxFairyGuiManifestGenerationResult result = GenerateRuntimeHudManifestSource();

            Require(result.Success, "Runtime HUD generator should read current source metadata.");
            MxFairyGuiManifest manifest = result.Manifest;
            Require(manifest.Packages.Count == 1, "generated manifest should include one package.");
            Require(manifest.Views.Count == 1, "generated manifest should include one view.");
            Require(manifest.Views[0].NamedControls.Count == 9, "generated manifest should include source text and button controls.");
            Require(manifest.Views[0].LocalizedTexts.Count == 2, "generated manifest should include authored localization metadata.");
            Require(manifest.Views[0].Commands.Count == 2, "generated manifest should include authored command metadata.");
            Require(result.SourceText.Contains("// <auto-generated />"), "generated source should declare itself generated.");
            Require(result.SourceText.Contains("new MxFairyGuiLocalizedTextBinding(RuntimeAbilitySliceFairyGuiHudIds.Title"), "generated source should emit localization bindings.");
            Require(result.SourceText.Contains("RuntimeAbilitySliceFairyGuiHudIds.Strike"), "generated source should reference stable handwritten ids.");
        }

        private static void StoryDialogSourceManifest_ValidatesCurrentPackageSources()
        {
            MxFairyGuiManifest manifest = GenerateStoryDialogManifestSource().Manifest;
            ResourceCatalog catalog = CreateStoryDialogCatalog();

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.ValidateSources(
                manifest,
                FindRepositoryRoot(),
                catalog);

            Require(result.Success, "Story dialog manifest should validate against source XML and package bytes.");
        }

        private static void StoryDialogManifestProjection_FeedsViewContract()
        {
            MxFairyGuiManifest manifest = GenerateStoryDialogManifestSource().Manifest;
            var contracts = MxFairyGuiManifestProjection.CreateViewContracts(manifest);

            Require(contracts.Count == 1, "Story dialog manifest should project one view contract.");
            MxUiViewContract contract = contracts[0];
            Require(contract.Descriptor.Id == new MxUiViewId("ui.story.dialog"), "projected Story dialog view id should match manifest.");
            Require(contract.Descriptor.PackageKey == "MxStoryDialog", "projected Story dialog package key should match manifest package.");
            Require(contract.Descriptor.ComponentName == "StoryDialogPanel", "projected Story dialog component should match manifest component.");
            Require(contract.Descriptor.Layer == MxUiLayer.Modal, "projected Story dialog layer should match manifest layer.");
            Require(contract.Commands.Count == 2, "projected Story dialog commands should come from manifest command bindings.");
            Require(contract.RequiredResources.Count == 1 && contract.RequiredResources[0] == "ui.fairygui.storydialog.fui", "projected Story dialog resources should include package bytes id.");
        }

        private static void StoryDialogManifestGenerator_ProducesCurrentManifestMetadata()
        {
            MxFairyGuiManifestGenerationResult result = GenerateStoryDialogManifestSource();

            Require(result.Success, "Story dialog generator should read current source metadata.");
            MxFairyGuiManifest manifest = result.Manifest;
            Require(manifest.Packages.Count == 1, "generated Story dialog manifest should include one package.");
            Require(manifest.Views.Count == 1, "generated Story dialog manifest should include one view.");
            Require(manifest.Views[0].NamedControls.Count == 8, "generated Story dialog manifest should include source text and button controls.");
            Require(manifest.Views[0].LocalizedTexts.Count == 1, "generated Story dialog manifest should include authored localization metadata.");
            Require(manifest.Views[0].Commands.Count == 2, "generated Story dialog manifest should include authored command metadata.");
            Require(result.SourceText.Contains("StoryRuntimeFairyGuiDialogManifest"), "generated Story dialog source should use expected class name.");
            Require(result.SourceText.Contains("StoryRuntimeFairyGuiDialogIds.Continue"), "generated Story dialog source should reference stable handwritten ids.");
        }

        private static void StoryDialogGeneratedManifest_IsCurrent()
        {
            RequireGeneratedManifestCurrent(FindTarget("story-dialog"), "--write-story-dialog-manifest");
        }

        private static void CharacterTestCombatHudManifestProjection_IncludesAtlasAndHudControls()
        {
            MxFairyGuiManifest manifest = GenerateCharacterTestCombatHudManifestSource().Manifest;
            var contracts = MxFairyGuiManifestProjection.CreateViewContracts(manifest);

            Require(contracts.Count == 1, "CombatHud manifest should project one view contract.");
            MxUiViewContract contract = contracts[0];
            Require(contract.Descriptor.Id == new MxUiViewId("ui.character_test.combat.hud"), "projected CombatHud view id should match manifest.");
            Require(contract.Descriptor.PackageKey == "MxCharacterTestCombatHud", "projected CombatHud package key should match manifest package.");
            Require(contract.Descriptor.ComponentName == "CombatHudPanel", "projected CombatHud component should match manifest component.");
            Require(contract.Descriptor.Layer == MxUiLayer.Hud, "projected CombatHud layer should be HUD.");
            Require(contract.Commands.Count == 0, "projected CombatHud should not claim generated command bindings.");
            Require(contract.RequiredResources.Count == 2, "projected CombatHud resources should include package bytes and atlas.");
            Require(contract.RequiredResources[0] == "ui.fairygui.charactertest.combat_hud.fui", "CombatHud resources should include package bytes id.");
            Require(contract.RequiredResources[1] == "ui.fairygui.charactertest.combat_hud.atlas0", "CombatHud resources should include atlas id.");
        }

        private static void CharacterTestCombatHudManifestGenerator_ProducesCurrentManifestMetadata()
        {
            MxFairyGuiManifestGenerationResult result = GenerateCharacterTestCombatHudManifestSource();

            Require(result.Success, "CombatHud generator should read current source metadata.");
            MxFairyGuiManifest manifest = result.Manifest;
            Require(manifest.Packages.Count == 1, "generated CombatHud manifest should include one package.");
            Require(manifest.Packages[0].RequiredResources.Count == 1, "generated CombatHud package should include atlas resource.");
            Require(manifest.Views.Count == 1, "generated CombatHud manifest should include one view.");
            Require(manifest.Views[0].NamedControls.Count == 88, "generated CombatHud manifest should include source text controls for static, ability, buff, and behavior-card HUD presentation.");
            Require(manifest.Views[0].LocalizedTexts.Count == 0, "generated CombatHud manifest should not claim authored localization bindings.");
            Require(manifest.Views[0].Commands.Count == 0, "generated CombatHud manifest should not claim authored command bindings.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiCombatHudManifest"), "generated CombatHud source should use expected class name.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiCombatHudIds.Atlas0Key"), "generated CombatHud source should reference stable atlas id.");
        }

        private static void CharacterTestCombatHudGeneratedManifest_IsCurrent()
        {
            RequireGeneratedManifestCurrent(FindTarget("character-test-combat-hud"), "--write-character-test-combat-hud-manifest");
        }

        private static void CharacterTestCombatHudGeneratedBinder_IsCurrent()
        {
            RequireGeneratedBinderCurrent(FindTarget("character-test-combat-hud"), "--write-character-test-combat-hud-binder");
        }

        private static void CharacterTestCombatHudBinderGenerator_ProducesCurrentSource()
        {
            MxFairyGuiBinderGenerationResult result = GenerateCharacterTestCombatHudBinderSource();

            Require(result.Success, "CombatHud binder generator should not produce errors.");
            Require(result.SourceText.Contains("public sealed partial class CharacterTestFairyGuiCombatHudBinder"), "generated CombatHud binder should preserve public partial class name.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiCombatHudIds.NetworkLatencyText"), "generated CombatHud binder should bind network latency text.");
            Require(result.SourceText.Contains("OnAfterGeneratedBind(root, viewModel);"), "generated CombatHud binder should preserve partial dynamic HUD extension point.");
        }

        private static void CharacterTestSettingsPanelManifestProjection_IncludesAtlasAndCommands()
        {
            MxFairyGuiManifest manifest = GenerateCharacterTestSettingsPanelManifestSource().Manifest;
            var contracts = MxFairyGuiManifestProjection.CreateViewContracts(manifest);

            Require(contracts.Count == 1, "SettingsPanel manifest should project one view contract.");
            MxUiViewContract contract = contracts[0];
            Require(contract.Descriptor.Id == new MxUiViewId("ui.character_test.settings.panel"), "projected SettingsPanel view id should match manifest.");
            Require(contract.Descriptor.PackageKey == "MxCharacterTestSettingsUi", "projected SettingsPanel package key should match manifest package.");
            Require(contract.Descriptor.ComponentName == "SettingsPanel", "projected SettingsPanel component should match manifest component.");
            Require(contract.Descriptor.Layer == MxUiLayer.Modal, "projected SettingsPanel layer should be modal.");
            Require(contract.Commands.Count == 18, "projected SettingsPanel commands should come from manifest command bindings.");
            Require(contract.Commands[1].PayloadType == "MxFramework.Game.CharacterTest.CharacterTestFairyGuiSettingsPanelEditPayload", "projected SettingsPanel Apply command should keep edit payload type.");
            Require(contract.Commands[16].PayloadType == "MxFramework.Game.CharacterTest.CharacterTestFairyGuiSettingsPanelInputPayload", "projected SettingsPanel rebind command should keep input payload type.");
            Require(contract.Commands[0].Feedback.Activation == MxUiFeedbackKind.Back, "projected SettingsPanel Close command should keep Back feedback.");
            Require(contract.Commands[1].Feedback.Success == MxUiFeedbackKind.Confirm, "projected SettingsPanel Apply command should keep result feedback.");
            Require(contract.RequiredResources.Count == 2, "projected SettingsPanel resources should include package bytes and atlas.");
            Require(contract.RequiredResources[0] == "ui.fairygui.charactertest.settings_ui.fui", "SettingsPanel resources should include package bytes id.");
            Require(contract.RequiredResources[1] == "ui.fairygui.charactertest.settings_ui.atlas0", "SettingsPanel resources should include atlas id.");
        }

        private static void CharacterTestSettingsPanelManifestGenerator_ProducesCurrentManifestMetadata()
        {
            MxFairyGuiManifestGenerationResult result = GenerateCharacterTestSettingsPanelManifestSource();

            Require(result.Success, "SettingsPanel generator should read current source metadata.");
            MxFairyGuiManifest manifest = result.Manifest;
            Require(manifest.Packages.Count == 1, "generated SettingsPanel manifest should include one package.");
            Require(manifest.Packages[0].RequiredResources.Count == 1, "generated SettingsPanel package should include atlas resource.");
            Require(manifest.Views.Count == 1, "generated SettingsPanel manifest should include one view.");
            Require(manifest.Views[0].NamedControls.Count == 48, "generated SettingsPanel manifest should include navigation, page controls, and the dynamic input list.");
            Require(manifest.Views[0].LocalizedTexts.Count == 11, "generated SettingsPanel manifest should include authored localization metadata.");
            Require(manifest.Views[0].Commands.Count == 18, "generated SettingsPanel manifest should include authored command metadata.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiSettingsPanelManifest"), "generated SettingsPanel source should use expected class name.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiSettingsPanelIds.Atlas0Key"), "generated SettingsPanel source should reference stable atlas id.");
        }

        private static void CharacterTestSettingsPanelGeneratedManifest_IsCurrent()
        {
            RequireGeneratedManifestCurrent(FindTarget("character-test-settings-panel"), "--write-character-test-settings-panel-manifest");
        }

        private static void CharacterTestSettingsPanelGeneratedBinder_IsCurrent()
        {
            RequireGeneratedBinderCurrent(FindTarget("character-test-settings-panel"), "--write-character-test-settings-panel-binder");
        }

        private static void CharacterTestSettingsPanelBinderGenerator_ProducesCurrentSource()
        {
            MxFairyGuiBinderGenerationResult result = GenerateCharacterTestSettingsPanelBinderSource();

            Require(result.Success, "SettingsPanel binder generator should not produce errors.");
            Require(result.SourceText.Contains("public sealed partial class CharacterTestFairyGuiSettingsPanelBinder"), "generated SettingsPanel binder should preserve public partial class name.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiSettingsPanelIds.InputInstruction"), "generated SettingsPanel binder should bind the input guidance copy.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiSettingsPanelIds.KeyboardMouseTab"), "generated SettingsPanel binder should bind device tabs.");
            Require(result.SourceText.Contains("CreateEditPayload(root)"), "generated SettingsPanel binder should delegate edit payload readback to partial code.");
            Require(!result.SourceText.Contains("BindRepeated0(root, viewModel);"), "generated SettingsPanel binder should leave dynamic list rows to the partial binder.");
        }

        private static void CharacterTestNetworkPanelManifestProjection_IncludesAtlasAndCommands()
        {
            MxFairyGuiManifest manifest = GenerateCharacterTestNetworkPanelManifestSource().Manifest;
            var contracts = MxFairyGuiManifestProjection.CreateViewContracts(manifest);

            Require(contracts.Count == 1, "NetworkPanel manifest should project one view contract.");
            MxUiViewContract contract = contracts[0];
            Require(contract.Descriptor.Id == new MxUiViewId("ui.character_test.network.panel"), "projected NetworkPanel view id should match manifest.");
            Require(contract.Descriptor.PackageKey == "MxCharacterTestNetworkUi", "projected NetworkPanel package key should match manifest package.");
            Require(contract.Descriptor.ComponentName == "NetworkPanel", "projected NetworkPanel component should match manifest component.");
            Require(contract.Descriptor.Layer == MxUiLayer.Modal, "projected NetworkPanel layer should be modal.");
            Require(contract.Commands.Count == 10, "projected NetworkPanel commands should come from manifest command bindings.");
            Require(contract.Commands[2].PayloadType == "MxFramework.Game.CharacterTest.CharacterTestNetworkSessionOptions", "projected NetworkPanel Host command should keep session options payload type.");
            Require(contract.Commands[5].PayloadType == typeof(bool).FullName, "projected NetworkPanel Ready command should keep bool payload type.");
            Require(contract.RequiredResources.Count == 2, "projected NetworkPanel resources should include package bytes and atlas.");
            Require(contract.RequiredResources[0] == "ui.fairygui.charactertest.network_ui.fui", "NetworkPanel resources should include package bytes id.");
            Require(contract.RequiredResources[1] == "ui.fairygui.charactertest.network_ui.atlas0", "NetworkPanel resources should include atlas id.");
        }

        private static void CharacterTestNetworkPanelManifestGenerator_ProducesCurrentManifestMetadata()
        {
            MxFairyGuiManifestGenerationResult result = GenerateCharacterTestNetworkPanelManifestSource();

            Require(result.Success, "NetworkPanel generator should read current source metadata.");
            MxFairyGuiManifest manifest = result.Manifest;
            Require(manifest.Packages.Count == 1, "generated NetworkPanel manifest should include one package.");
            Require(manifest.Packages[0].RequiredResources.Count == 1, "generated NetworkPanel package should include atlas resource.");
            Require(manifest.Views.Count == 1, "generated NetworkPanel manifest should include one view.");
            Require(manifest.Views[0].NamedControls.Count == 39, "generated NetworkPanel manifest should include source text, input, and button controls.");
            Require(manifest.Views[0].LocalizedTexts.Count == 7, "generated NetworkPanel manifest should include authored localization metadata.");
            Require(manifest.Views[0].Commands.Count == 10, "generated NetworkPanel manifest should include authored command metadata.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiNetworkPanelManifest"), "generated NetworkPanel source should use expected class name.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiNetworkPanelIds.Atlas0Key"), "generated NetworkPanel source should reference stable atlas id.");
        }

        private static void CharacterTestNetworkPanelGeneratedManifest_IsCurrent()
        {
            RequireGeneratedManifestCurrent(FindTarget("character-test-network-panel"), "--write-character-test-network-panel-manifest");
        }

        private static void CharacterTestNetworkPanelGeneratedBinder_IsCurrent()
        {
            RequireGeneratedBinderCurrent(FindTarget("character-test-network-panel"), "--write-character-test-network-panel-binder");
        }

        private static void CharacterTestNetworkPanelBinderGenerator_ProducesCurrentSource()
        {
            MxFairyGuiBinderGenerationResult result = GenerateCharacterTestNetworkPanelBinderSource();

            Require(result.Success, "NetworkPanel binder generator should not produce errors.");
            Require(result.SourceText.Contains("public sealed partial class CharacterTestFairyGuiNetworkPanelBinder"), "generated NetworkPanel binder should preserve public partial class name.");
            Require(result.SourceText.Contains("CreateHostOptions(root, viewModel)"), "generated NetworkPanel binder should delegate host payload readback to partial code.");
            Require(result.SourceText.Contains("CreateJoinOptions(root, viewModel)"), "generated NetworkPanel binder should delegate join payload readback to partial code.");
            Require(result.SourceText.Contains("checkMode: true"), "generated NetworkPanel binder should emit check button configuration.");
        }

        private static void CharacterTestMainMenuManifestProjection_IncludesAtlasAndCommands()
        {
            MxFairyGuiManifest manifest = GenerateCharacterTestMainMenuManifestSource().Manifest;
            var contracts = MxFairyGuiManifestProjection.CreateViewContracts(manifest);

            Require(contracts.Count == 1, "MainMenu manifest should project one view contract.");
            MxUiViewContract contract = contracts[0];
            Require(contract.Descriptor.Id == new MxUiViewId("ui.character_test.main.menu"), "projected MainMenu view id should match manifest.");
            Require(contract.Descriptor.PackageKey == "MxCharacterTestUi", "projected MainMenu package key should match manifest package.");
            Require(contract.Descriptor.ComponentName == "MainMenuPanel", "projected MainMenu component should match manifest component.");
            Require(contract.Descriptor.Layer == MxUiLayer.Modal, "projected MainMenu layer should be modal.");
            Require(contract.Commands.Count == 7, "projected MainMenu commands should come from manifest command bindings.");
            Require(contract.RequiredResources.Count == 2, "projected MainMenu resources should include package bytes and atlas.");
            Require(contract.RequiredResources[0] == "ui.fairygui.charactertest.player_ui.fui", "MainMenu resources should include the player UI package bytes id.");
            Require(contract.RequiredResources[1] == "ui.fairygui.charactertest.player_ui.atlas0", "MainMenu resources should include the player UI atlas id.");
        }

        private static void CharacterTestMainMenuManifestGenerator_ProducesCurrentManifestMetadata()
        {
            MxFairyGuiManifestGenerationResult result = GenerateCharacterTestMainMenuManifestSource();

            Require(result.Success, "MainMenu generator should read current source metadata.");
            MxFairyGuiManifest manifest = result.Manifest;
            Require(manifest.Packages.Count == 1, "generated MainMenu manifest should include one package.");
            Require(manifest.Packages[0].RequiredResources.Count == 1, "generated MainMenu package should include atlas resource.");
            Require(manifest.Views.Count == 1, "generated MainMenu manifest should include one view.");
            Require(manifest.Views[0].NamedControls.Count == 10, "generated MainMenu manifest should include source text and button controls.");
            Require(manifest.Views[0].LocalizedTexts.Count == 0, "generated MainMenu manifest should not claim button localization bindings.");
            Require(manifest.Views[0].Commands.Count == 7, "generated MainMenu manifest should include authored command metadata.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiMainMenuManifest"), "generated MainMenu source should use expected class name.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiMainMenuIds.Atlas0Key"), "generated MainMenu source should reference stable atlas id.");
        }

        private static void CharacterTestMainMenuGeneratedManifest_IsCurrent()
        {
            RequireGeneratedManifestCurrent(FindTarget("character-test-main-menu"), "--write-character-test-main-menu-manifest");
        }

        private static void CharacterTestMainMenuGeneratedBinder_IsCurrent()
        {
            RequireGeneratedBinderCurrent(FindTarget("character-test-main-menu"), "--write-character-test-main-menu-binder");
        }

        private static void CharacterTestMainMenuBinderGenerator_ProducesCurrentSource()
        {
            MxFairyGuiBinderGenerationResult result = GenerateCharacterTestMainMenuBinderSource();

            Require(result.Success, "MainMenu binder generator should not produce errors.");
            Require(result.SourceText.Contains("public sealed partial class CharacterTestFairyGuiMainMenuBinder"), "generated MainMenu binder should preserve public partial class name.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiMainMenuCommandIds.SettingsToggle"), "generated MainMenu binder should emit settings command binding.");
            Require(result.SourceText.Contains("OnAfterGeneratedBind(root, viewModel);"), "generated MainMenu binder should preserve partial layout extension point.");
        }

        private static void CharacterTestAbilityBindingPanelManifestProjection_IncludesAtlasAndCommands()
        {
            MxFairyGuiManifest manifest = GenerateCharacterTestAbilityBindingPanelManifestSource().Manifest;
            var contracts = MxFairyGuiManifestProjection.CreateViewContracts(manifest);

            Require(contracts.Count == 1, "AbilityBindingPanel manifest should project one view contract.");
            MxUiViewContract contract = contracts[0];
            Require(contract.Descriptor.Id == new MxUiViewId("ui.character_test.ability.bindings"), "projected AbilityBindingPanel view id should match manifest.");
            Require(contract.Descriptor.PackageKey == "MxCharacterTestAbilityUi", "projected AbilityBindingPanel package key should match manifest package.");
            Require(contract.Descriptor.ComponentName == "AbilityBindingPanel", "projected AbilityBindingPanel component should match manifest component.");
            Require(contract.Descriptor.Layer == MxUiLayer.Modal, "projected AbilityBindingPanel layer should be modal.");
            Require(contract.Commands.Count == 6, "projected AbilityBindingPanel commands should come from manifest command bindings.");
            Require(contract.Commands[0].PayloadType == "MxFramework.Game.CharacterTest.CharacterTestFairyGuiAbilityBindingPanelPayload", "projected AbilityBindingPanel SelectSlot command should keep payload type.");
            Require(contract.Commands[1].PayloadType == "MxFramework.Game.CharacterTest.CharacterTestFairyGuiAbilityBindingPanelPayload", "projected AbilityBindingPanel SelectAbility command should keep payload type.");
            Require(contract.RequiredResources.Count == 2, "projected AbilityBindingPanel resources should include package bytes and atlas.");
            Require(contract.RequiredResources[0] == "ui.fairygui.charactertest.ability_ui.fui", "AbilityBindingPanel resources should include package bytes id.");
            Require(contract.RequiredResources[1] == "ui.fairygui.charactertest.ability_ui.atlas0", "AbilityBindingPanel resources should include atlas id.");
        }

        private static void CharacterTestAbilityBindingPanelManifestGenerator_ProducesCurrentManifestMetadata()
        {
            MxFairyGuiManifestGenerationResult result = GenerateCharacterTestAbilityBindingPanelManifestSource();

            Require(result.Success, "AbilityBindingPanel generator should read current source metadata.");
            MxFairyGuiManifest manifest = result.Manifest;
            Require(manifest.Packages.Count == 1, "generated AbilityBindingPanel manifest should include one package.");
            Require(manifest.Packages[0].RequiredResources.Count == 1, "generated AbilityBindingPanel package should include atlas resource.");
            Require(manifest.Views.Count == 1, "generated AbilityBindingPanel manifest should include one view.");
            Require(manifest.Views[0].NamedControls.Count == 35, "generated AbilityBindingPanel manifest should include source text and button controls.");
            Require(manifest.Views[0].LocalizedTexts.Count == 3, "generated AbilityBindingPanel manifest should include authored header localization metadata.");
            Require(manifest.Views[0].Commands.Count == 6, "generated AbilityBindingPanel manifest should include authored command metadata.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiAbilityBindingPanelManifest"), "generated AbilityBindingPanel source should use expected class name.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiAbilityBindingPanelIds.Atlas0Key"), "generated AbilityBindingPanel source should reference stable atlas id.");
        }

        private static void CharacterTestAbilityBindingPanelGeneratedManifest_IsCurrent()
        {
            RequireGeneratedManifestCurrent(FindTarget("character-test-ability-binding-panel"), "--write-character-test-ability-binding-panel-manifest");
        }

        private static void CharacterTestAbilityBindingPanelGeneratedBinder_IsCurrent()
        {
            RequireGeneratedBinderCurrent(FindTarget("character-test-ability-binding-panel"), "--write-character-test-ability-binding-panel-binder");
        }

        private static void CharacterTestAbilityBindingPanelBinderGenerator_ProducesCurrentSource()
        {
            MxFairyGuiBinderGenerationResult result = GenerateCharacterTestAbilityBindingPanelBinderSource();

            Require(result.Success, "AbilityBindingPanel binder generator should not produce errors.");
            Require(result.SourceText.Contains("public sealed partial class CharacterTestFairyGuiAbilityBindingPanelBinder"), "generated AbilityBindingPanel binder should preserve public partial class name.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiAbilityBindingPanelCommandIds.ClearSlot"), "generated AbilityBindingPanel binder should emit clear command binding.");
            Require(result.SourceText.Contains("OnAfterGeneratedBind(root, viewModel);"), "generated AbilityBindingPanel binder should preserve partial dynamic binding extension point.");
        }

        private static void CharacterTestBehaviorDeckPanelManifestProjection_IncludesAtlasAndCommands()
        {
            MxFairyGuiManifest manifest = GenerateCharacterTestBehaviorDeckPanelManifestSource().Manifest;
            var contracts = MxFairyGuiManifestProjection.CreateViewContracts(manifest);

            Require(contracts.Count == 1, "BehaviorDeckPanel manifest should project one view contract.");
            MxUiViewContract contract = contracts[0];
            Require(contract.Descriptor.Id == new MxUiViewId("ui.character_test.runtime_ai_planner.behavior_deck"), "projected BehaviorDeckPanel view id should match manifest.");
            Require(contract.Descriptor.PackageKey == "MxCharacterTestBehaviorDeckUi", "projected BehaviorDeckPanel package key should match manifest package.");
            Require(contract.Descriptor.ComponentName == "BehaviorDeckPanel", "projected BehaviorDeckPanel component should match manifest component.");
            Require(contract.Commands.Count == 14, "projected BehaviorDeckPanel commands should include deck navigation, add selection, details pagination, recommended-preset preview/confirm/cancel/undo, Setup continuation, and card-details dismissal commands.");
            Require(contract.RequiredResources.Count == 2, "projected BehaviorDeckPanel resources should include package bytes and atlas.");
        }

        private static void CharacterTestBehaviorDeckPanelManifestGenerator_ProducesCurrentManifestMetadata()
        {
            MxFairyGuiManifestGenerationResult result = GenerateCharacterTestBehaviorDeckPanelManifestSource();
            Require(result.Success, "BehaviorDeckPanel generator should read current source metadata.");
            Require(result.Manifest.Views.Count == 1, "generated BehaviorDeckPanel manifest should include one view.");
            Require(result.Manifest.Views[0].NamedControls.Count == 46, "generated BehaviorDeckPanel manifest should include every top-level bound text/button control, including cost budget, add selection, the selected-card context strip, and preset confirmation modal; decorative graphs and nested card fields are bound by the pooled card binder.");
            Require(result.Manifest.Views[0].Commands.Count == 14, "generated BehaviorDeckPanel manifest should include deck navigation, add selection, details pagination, recommended-preset preview/confirm/cancel/undo, Setup continuation, and card-details dismissal commands.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiBehaviorDeckPanelManifest"), "generated BehaviorDeckPanel source should use expected class name.");
        }

        private static void CharacterTestBehaviorDeckPanelGeneratedManifest_IsCurrent()
        {
            RequireGeneratedManifestCurrent(FindTarget("character-test-behavior-deck-panel"), "--write-character-test-behavior-deck-panel-manifest");
        }

        private static void CharacterTestBehaviorDeckPanelGeneratedBinder_IsCurrent()
        {
            RequireGeneratedBinderCurrent(FindTarget("character-test-behavior-deck-panel"), "--write-character-test-behavior-deck-panel-binder");
        }

        private static void CharacterTestBehaviorDeckPanelPublishedCards_ContainLocalFeedbackControls()
        {
            string root = FindRepositoryRoot();
            string fullCard = File.ReadAllText(Path.Combine(
                root,
                "FGUIProject",
                "assets",
                "MxCharacterTestBehaviorDeckUi",
                "Components",
                "RuntimeAiPlayerBehaviorCard.xml"));
            string compactCard = File.ReadAllText(Path.Combine(
                root,
                "FGUIProject",
                "assets",
                "MxCharacterTestBehaviorDeckUi",
                "Components",
                "RuntimeAiPlayerBehaviorCardCompact.xml"));

            Require(fullCard.Contains("name=\"slot\"", StringComparison.Ordinal), "full Behavior Deck card should expose its installed slot label.");
            Require(fullCard.Contains("name=\"blockReason\"", StringComparison.Ordinal), "full Behavior Deck card should expose a persistent blocker label.");
            Require(fullCard.Contains("name=\"inlineFeedback\"", StringComparison.Ordinal), "full Behavior Deck card should expose transient local feedback.");
            Require(fullCard.Contains("name=\"btnSecondaryAction\"", StringComparison.Ordinal), "full Behavior Deck card should expose a separate repair action.");
            Require(fullCard.Contains("name=\"btnDetails\"", StringComparison.Ordinal), "full Behavior Deck card should expose an explicit details action.");
            Require(compactCard.Contains("name=\"slot\"", StringComparison.Ordinal), "compact Behavior Deck card should expose its installed slot label.");
            Require(compactCard.Contains("name=\"blockReason\"", StringComparison.Ordinal), "compact Behavior Deck card should expose a persistent blocker label.");
            Require(compactCard.Contains("name=\"inlineFeedback\"", StringComparison.Ordinal), "compact Behavior Deck card should expose transient local feedback.");
            Require(compactCard.Contains("name=\"btnSecondaryAction\"", StringComparison.Ordinal), "compact Behavior Deck card should expose a separate repair action.");
        }

        private static void CharacterTestBehaviorDeckPanelBinderGenerator_ProducesCurrentSource()
        {
            MxFairyGuiBinderGenerationResult result = GenerateCharacterTestBehaviorDeckPanelBinderSource();
            Require(result.Success, "BehaviorDeckPanel binder generator should not produce errors.");
            Require(result.SourceText.Contains("public sealed partial class CharacterTestFairyGuiBehaviorDeckPanelBinder"), "generated BehaviorDeckPanel binder should preserve public partial class name.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviousPage"), "generated BehaviorDeckPanel binder should emit page navigation.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviewRecommendedPreset"), "generated BehaviorDeckPanel binder should emit recommended-preset preview.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ConfirmRecommendedPreset"), "generated BehaviorDeckPanel binder should emit recommended-preset confirmation.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CancelRecommendedPreset"), "generated BehaviorDeckPanel binder should emit recommended-preset cancellation.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiBehaviorDeckPanelCommandIds.UndoRecommendedPreset"), "generated BehaviorDeckPanel binder should emit recommended-preset undo.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ContinueToSetup"), "generated BehaviorDeckPanel binder should emit Setup continuation.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CloseDetails"), "generated BehaviorDeckPanel binder should emit card-details dismissal.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviousDetailsPage"), "generated BehaviorDeckPanel binder should emit previous details page.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiBehaviorDeckPanelCommandIds.NextDetailsPage"), "generated BehaviorDeckPanel binder should emit next details page.");
            Require(result.SourceText.Contains("OnAfterGeneratedBind(root, viewModel);"), "generated BehaviorDeckPanel binder should preserve dynamic card binding extension point.");
        }

        private static void CharacterTestStoryDialogManifestProjection_IncludesCommands()
        {
            MxFairyGuiManifest manifest = GenerateCharacterTestStoryDialogManifestSource().Manifest;
            var contracts = MxFairyGuiManifestProjection.CreateViewContracts(manifest);

            Require(contracts.Count == 1, "CharacterTest StoryDialog manifest should project one view contract.");
            MxUiViewContract contract = contracts[0];
            Require(contract.Descriptor.Id == new MxUiViewId("ui.character_test.story.dialog"), "projected CharacterTest StoryDialog view id should match manifest.");
            Require(contract.Descriptor.PackageKey == "MxStoryDialog", "projected CharacterTest StoryDialog package key should match manifest package.");
            Require(contract.Descriptor.ComponentName == "StoryDialogPanel", "projected CharacterTest StoryDialog component should match manifest component.");
            Require(contract.Descriptor.Layer == MxUiLayer.Modal, "projected CharacterTest StoryDialog layer should be modal.");
            Require(contract.Commands.Count == 2, "projected CharacterTest StoryDialog commands should come from manifest command bindings.");
            Require(contract.Commands[0].PayloadType == "MxFramework.Game.CharacterTest.CharacterTestFairyGuiStoryCompletePresentationPayload", "projected StoryDialog Continue command should keep payload type.");
            Require(contract.Commands[1].PayloadType == "MxFramework.Game.CharacterTest.CharacterTestFairyGuiStorySelectChoicePayload", "projected StoryDialog choice command should keep payload type.");
            Require(contract.RequiredResources.Count == 1 && contract.RequiredResources[0] == "ui.fairygui.storydialog.fui", "CharacterTest StoryDialog resources should include package bytes id.");
        }

        private static void CharacterTestStoryDialogManifestGenerator_ProducesCurrentManifestMetadata()
        {
            MxFairyGuiManifestGenerationResult result = GenerateCharacterTestStoryDialogManifestSource();

            Require(result.Success, "CharacterTest StoryDialog generator should read current source metadata.");
            MxFairyGuiManifest manifest = result.Manifest;
            Require(manifest.Packages.Count == 1, "generated CharacterTest StoryDialog manifest should include one package.");
            Require(manifest.Views.Count == 1, "generated CharacterTest StoryDialog manifest should include one view.");
            Require(manifest.Views[0].NamedControls.Count == 8, "generated CharacterTest StoryDialog manifest should include source text and button controls.");
            Require(manifest.Views[0].LocalizedTexts.Count == 1, "generated CharacterTest StoryDialog manifest should include authored title localization metadata.");
            Require(manifest.Views[0].Commands.Count == 2, "generated CharacterTest StoryDialog manifest should include authored command metadata.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiStoryDialogManifest"), "generated CharacterTest StoryDialog source should use expected class name.");
            Require(result.SourceText.Contains("CharacterTestFairyGuiStoryDialogIds.Continue"), "generated CharacterTest StoryDialog source should reference stable ids.");
        }

        private static void CharacterTestStoryDialogGeneratedManifest_IsCurrent()
        {
            RequireGeneratedManifestCurrent(FindTarget("character-test-story-dialog"), "--write-character-test-story-dialog-manifest");
        }

        private static void CharacterTestStoryDialogGeneratedBinder_IsCurrent()
        {
            RequireGeneratedBinderCurrent(FindTarget("character-test-story-dialog"), "--write-character-test-story-dialog-binder");
        }

        private static void CharacterTestStoryDialogBinderGenerator_ProducesCurrentSource()
        {
            MxFairyGuiBinderGenerationResult result = GenerateCharacterTestStoryDialogBinderSource();

            Require(result.Success, "CharacterTest StoryDialog binder generator should not produce errors.");
            Require(result.SourceText.Contains("public sealed partial class CharacterTestFairyGuiStoryDialogBinder"), "generated CharacterTest StoryDialog binder should preserve public partial class name.");
            Require(result.SourceText.Contains("ResolveContinuePayload(viewModel)"), "generated CharacterTest StoryDialog binder should delegate continue payload construction to partial code.");
            Require(result.SourceText.Contains("OnAfterGeneratedBind(root, viewModel);"), "generated CharacterTest StoryDialog binder should preserve partial dynamic choice extension point.");
        }

        private static void UiFoundationDemoManifestProjection_IncludesAtlasAndCommands()
        {
            MxFairyGuiManifestGenerationResult result = MxFairyGuiManifestGenerator.Generate(
                CreateUiFoundationDemoGenerationSpec(),
                FindRepositoryRoot());

            Require(result.Success, "UI Foundation Demo manifest generation should read the materialized package.");
            Require(result.Manifest.Packages.Count == 1, "UI Foundation Demo manifest should include one package.");
            Require(result.Manifest.Packages[0].RequiredResources.Count == 1, "UI Foundation Demo package should include its atlas.");
            Require(result.Manifest.Views.Count == 1, "UI Foundation Demo manifest should include one view.");
            Require(result.Manifest.Views[0].NamedControls.Count == 5, "UI Foundation Demo view should include its title and four binding slots.");
            Require(result.Manifest.Views[0].Commands.Count == 3, "UI Foundation Demo view should include its three fixed commands.");
        }

        private static void UiFoundationDemoGeneratedOutputs_AreCurrent()
        {
            ManifestTarget target = FindTarget("ui-foundation-demo");
            RequireGeneratedManifestCurrent(target, "--write-ui-foundation-demo-manifest");
            RequireGeneratedBinderCurrent(target, "--write-ui-foundation-demo-binder");
        }

        private static void RuntimeHudGeneratedManifest_IsCurrent()
        {
            RequireGeneratedManifestCurrent(FindTarget("runtime-hud"), "--write-runtime-hud-manifest");
        }

        private static void ManifestBatchCheck_CoversRuntimeHudStoryDialogCombatHudSettingsPanelNetworkPanelMainMenuAbilityBindingPanelAndCharacterTestStoryDialog()
        {
            MxFairyGuiManifestGenerationBatchResult result = GenerateManifestBatch(validateGeneratedOutput: true);

            Require(result.Success, "batch manifest check should pass for current checked-in generated sources.");
            Require(result.Items.Count == 12, "batch manifest check should also cover the two shared Player Guidance views.");
            Require(result.Diagnostics.Count == 0, "batch manifest check should not produce diagnostics for current targets.");
            Require(result.Items[0].Generation.Manifest.Packages[0].PackageId == "MxRuntimeHud", "first batch item should be Runtime HUD.");
            Require(result.Items[0].Generation.Manifest.Views[0].ViewId == new MxUiViewId("ui.runtimehud.main"), "Runtime HUD batch item should carry its view id.");
            Require(result.Items[1].Generation.Manifest.Packages[0].PackageId == "MxStoryDialog", "second batch item should be Story Dialog.");
            Require(result.Items[1].Generation.Manifest.Views[0].ViewId == new MxUiViewId("ui.story.dialog"), "Story Dialog batch item should carry its view id.");
            Require(result.Items[2].Generation.Manifest.Packages[0].PackageId == "MxCharacterTestCombatHud", "third batch item should be CharacterTest CombatHud.");
            Require(result.Items[2].Generation.Manifest.Views[0].ViewId == new MxUiViewId("ui.character_test.combat.hud"), "CombatHud batch item should carry its view id.");
            Require(result.Items[2].BinderGeneration != null && result.Items[2].BinderGeneration.Success, "CombatHud batch item should generate binder source.");
            Require(result.Items[3].Generation.Manifest.Packages[0].PackageId == "MxCharacterTestSettingsUi", "fourth batch item should be CharacterTest SettingsPanel.");
            Require(result.Items[3].Generation.Manifest.Views[0].ViewId == new MxUiViewId("ui.character_test.settings.panel"), "SettingsPanel batch item should carry its view id.");
            Require(result.Items[3].BinderGeneration != null && result.Items[3].BinderGeneration.Success, "SettingsPanel batch item should generate binder source.");
            Require(result.Items[4].Generation.Manifest.Packages[0].PackageId == "MxCharacterTestNetworkUi", "fifth batch item should be CharacterTest NetworkPanel.");
            Require(result.Items[4].Generation.Manifest.Views[0].ViewId == new MxUiViewId("ui.character_test.network.panel"), "NetworkPanel batch item should carry its view id.");
            Require(result.Items[4].BinderGeneration != null && result.Items[4].BinderGeneration.Success, "NetworkPanel batch item should generate binder source.");
            Require(result.Items[5].Generation.Manifest.Packages[0].PackageId == "MxCharacterTestUi", "sixth batch item should be CharacterTest MainMenu.");
            Require(result.Items[5].Generation.Manifest.Views[0].ViewId == new MxUiViewId("ui.character_test.main.menu"), "MainMenu batch item should carry its view id.");
            Require(result.Items[5].BinderGeneration != null && result.Items[5].BinderGeneration.Success, "MainMenu batch item should generate binder source.");
            Require(result.Items[6].Generation.Manifest.Packages[0].PackageId == "MxCharacterTestAbilityUi", "seventh batch item should be CharacterTest AbilityBindingPanel.");
            Require(result.Items[6].Generation.Manifest.Views[0].ViewId == new MxUiViewId("ui.character_test.ability.bindings"), "AbilityBindingPanel batch item should carry its view id.");
            Require(result.Items[6].BinderGeneration != null && result.Items[6].BinderGeneration.Success, "AbilityBindingPanel batch item should generate binder source.");
            Require(result.Items[7].Generation.Manifest.Packages[0].PackageId == "MxCharacterTestBehaviorDeckUi", "eighth batch item should be CharacterTest BehaviorDeckPanel.");
            Require(result.Items[7].Generation.Manifest.Views[0].ViewId == new MxUiViewId("ui.character_test.runtime_ai_planner.behavior_deck"), "BehaviorDeckPanel batch item should carry its view id.");
            Require(result.Items[7].BinderGeneration != null && result.Items[7].BinderGeneration.Success, "BehaviorDeckPanel batch item should generate binder source.");
            Require(result.Items[8].Generation.Manifest.Packages[0].PackageId == "MxStoryDialog", "ninth batch item should be CharacterTest StoryDialog.");
            Require(result.Items[8].Generation.Manifest.Views[0].ViewId == new MxUiViewId("ui.character_test.story.dialog"), "CharacterTest StoryDialog batch item should carry its view id.");
            Require(result.Items[8].BinderGeneration != null && result.Items[8].BinderGeneration.Success, "CharacterTest StoryDialog batch item should generate binder source.");
            Require(result.Items[9].Generation.Manifest.Packages[0].PackageId == "MxUiFoundationDemo", "tenth batch item should be the UI Foundation Demo.");
            Require(result.Items[9].Generation.Manifest.Views[0].ViewId == new MxUiViewId("ui.foundation.demo"), "UI Foundation Demo batch item should carry its fixed view id.");
            Require(result.Items[9].BinderGeneration != null && result.Items[9].BinderGeneration.Success, "UI Foundation Demo batch item should generate binder source.");
            Require(result.Items[10].Generation.Manifest.Packages[0].PackageId == "MxPlayerGuidanceUi", "eleventh batch item should be Player Guidance Card.");
            Require(result.Items[10].Generation.Manifest.Views[0].ViewId == new MxUiViewId("ui.guidance.card"), "Player Guidance Card batch item should carry its fixed view id.");
            Require(result.Items[10].Generation.Manifest.Views[0].Layer == MxUiLayer.Hud, "Player Guidance Card should remain on the HUD layer.");
            Require(result.Items[10].BinderGeneration != null && result.Items[10].BinderGeneration.Success, "Player Guidance Card should generate binder source.");
            Require(result.Items[11].Generation.Manifest.Packages[0].PackageId == "MxPlayerGuidanceUi", "twelfth batch item should be Player Guidance Notices.");
            Require(result.Items[11].Generation.Manifest.Views[0].ViewId == new MxUiViewId("ui.guidance.notices"), "Player Guidance Notices batch item should carry its fixed view id.");
            Require(result.Items[11].Generation.Manifest.Views[0].Layer == MxUiLayer.Toast, "Player Guidance Notices should remain on the Toast layer.");
            Require(result.Items[11].BinderGeneration != null && result.Items[11].BinderGeneration.Success, "Player Guidance Notices should generate binder source.");
        }

        private static void ManifestReviewReport_IsDeterministicAndCoversBatchTargets()
        {
            MxFairyGuiManifestGenerationBatchResult result = GenerateManifestBatch(validateGeneratedOutput: true);
            FairyGuiArtSliceValidationResult artSliceResult = FairyGuiArtSliceCatalogTool.ValidateCharacterTest(FindRepositoryRoot());
            string first = FairyGuiReviewReportBuilder.Generate(result, artSliceResult);
            string second = FairyGuiReviewReportBuilder.Generate(result, artSliceResult);

            Require(string.Equals(first, second, StringComparison.Ordinal), "manifest review report should be deterministic for the same batch result.");
            Require(first.Contains("FairyGUI Manifest Review Report"), "manifest review report should include a stable title.");
            Require(first.Contains("items: 12"), "manifest review report should include target count.");
            Require(first.Contains("Targets"), "manifest review report should include targets section.");
            Require(first.Contains("Diagnostics"), "manifest review report should include diagnostics section.");
            Require(first.Contains("generatedBinderSourcePath:"), "manifest review report should include generated binder path when a target owns one.");
            Require(first.Contains("FairyGUI Art Slice Summary"), "manifest review report should include art slice summary.");
        }

        private static void LocalizationBindings_ValidateTextControlsAndRequests()
        {
            MxFairyGuiManifest manifest = CreateValidManifest();
            manifest.Views = new[]
            {
                new MxFairyGuiManifestView
                {
                    ViewId = new MxUiViewId("ui.smoke"),
                    PackageId = "mx.smoke",
                    ComponentName = "SmokePanel",
                    ViewModelType = "SmokeViewModel",
                    NamedControls = new[]
                    {
                        new MxFairyGuiNamedControl("txtTitle", "GTextField"),
                        new MxFairyGuiNamedControl("btnRefresh", "GButton")
                    },
                    LocalizedTexts = new[]
                    {
                        new MxFairyGuiLocalizedTextBinding("txtTitle", "ui.smoke.title", "Smoke")
                    },
                    Commands = new[]
                    {
                        new MxFairyGuiCommandBinding("refresh", "btnRefresh")
                    }
                }
            };

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.Validate(manifest);
            MxUiLocalizedTextRequest request = manifest.Views[0].LocalizedTexts[0].ToRequest();

            Require(result.Success, "valid localization binding should pass validation.");
            Require(request.Key == new MxUiTextKey("ui.smoke.title"), "localization binding should create provider-neutral text request.");
            Require(request.FallbackText == "Smoke", "localization binding should carry fallback text.");
        }

        private static void LocalizationBindings_ReportMissingKeyAndNonTextControl()
        {
            MxFairyGuiManifest manifest = CreateValidManifest();
            manifest.Views = new[]
            {
                new MxFairyGuiManifestView
                {
                    ViewId = new MxUiViewId("ui.smoke"),
                    PackageId = "mx.smoke",
                    ComponentName = "SmokePanel",
                    ViewModelType = "SmokeViewModel",
                    NamedControls = new[]
                    {
                        new MxFairyGuiNamedControl("txtTitle", "GTextField"),
                        new MxFairyGuiNamedControl("btnRefresh", "GButton")
                    },
                    LocalizedTexts = new[]
                    {
                        new MxFairyGuiLocalizedTextBinding("txtTitle", ""),
                        new MxFairyGuiLocalizedTextBinding("btnRefresh", "ui.smoke.refresh")
                    },
                    Commands = new[]
                    {
                        new MxFairyGuiCommandBinding("refresh", "btnRefresh")
                    }
                }
            };

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.Validate(manifest);

            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.LocalizationKeyMissing), "missing localization key should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.LocalizationControlNotText), "localization binding to button should be reported.");
        }

        private static void LocalizationBindings_ReportSourceXmlDrift()
        {
            MxFairyGuiManifest manifest = CreateRuntimeHudManifest();
            manifest.Views = new[]
            {
                new MxFairyGuiManifestView
                {
                    ViewId = manifest.Views[0].ViewId,
                    PackageId = manifest.Views[0].PackageId,
                    ComponentName = manifest.Views[0].ComponentName,
                    ComponentSourcePath = manifest.Views[0].ComponentSourcePath,
                    ViewModelType = manifest.Views[0].ViewModelType,
                    Layer = manifest.Views[0].Layer,
                    RequiredResources = manifest.Views[0].RequiredResources,
                    NamedControls = manifest.Views[0].NamedControls,
                    LocalizedTexts = new[]
                    {
                        new MxFairyGuiLocalizedTextBinding("ghostText", "ui.runtimehud.ghost", required: false),
                        new MxFairyGuiLocalizedTextBinding("btnStrike", "ui.runtimehud.strike")
                    },
                    Commands = manifest.Views[0].Commands
                }
            };

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.ValidateSources(
                manifest,
                FindRepositoryRoot(),
                CreateRuntimeHudCatalog());

            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.LocalizationControlUnknown), "source XML missing localization control should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.LocalizationControlNotText), "source XML non-text localization control should be reported.");
        }

        private static void RuntimeHudGeneratedManifest_MismatchDiagnostic()
        {
            MxFairyGuiManifestValidationResult staleResult = MxFairyGuiManifestGenerator.ValidateGeneratedOutput(
                "stale",
                "current",
                "Generated/RuntimeAbilitySliceFairyGuiHudManifest.g.cs");

            Require(!staleResult.Success, "stale generated output should fail validation.");
            Require(staleResult.HasCode(MxFairyGuiManifestDiagnosticCode.GeneratedDescriptorMismatch), "stale generated output should report GeneratedDescriptorMismatch.");
        }

        private static void CharacterTestSettingsPanelGeneratedBinder_MismatchDiagnostic()
        {
            MxFairyGuiManifestValidationResult staleResult = MxFairyGuiManifestGenerator.ValidateGeneratedOutput(
                "stale",
                "current",
                "Generated/CharacterTestFairyGuiSettingsPanelBinder.g.cs");

            Require(!staleResult.Success, "stale generated binder output should fail validation.");
            Require(staleResult.HasCode(MxFairyGuiManifestDiagnosticCode.GeneratedDescriptorMismatch), "stale generated binder output should report GeneratedDescriptorMismatch.");
        }

        private static void RuntimeHudSourceManifest_ReportsMissingControl()
        {
            MxFairyGuiManifest manifest = CreateRuntimeHudManifest();
            manifest.Views = new[]
            {
                new MxFairyGuiManifestView
                {
                    ViewId = manifest.Views[0].ViewId,
                    PackageId = manifest.Views[0].PackageId,
                    ComponentName = manifest.Views[0].ComponentName,
                    ComponentSourcePath = manifest.Views[0].ComponentSourcePath,
                    ViewModelType = manifest.Views[0].ViewModelType,
                    Layer = manifest.Views[0].Layer,
                    RequiredResources = manifest.Views[0].RequiredResources,
                    NamedControls = new[]
                    {
                        new MxFairyGuiNamedControl("txtTitle", "Text")
                    },
                    Commands = manifest.Views[0].Commands
                }
            };

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.ValidateSources(
                manifest,
                FindRepositoryRoot(),
                CreateRuntimeHudCatalog());

            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.ControlMissing), "missing renamed control should be reported.");
        }

        private static void RuntimeHudSourceManifest_ReportsMissingCommandButton()
        {
            MxFairyGuiManifest manifest = CreateRuntimeHudManifest();
            manifest.Views = new[]
            {
                new MxFairyGuiManifestView
                {
                    ViewId = manifest.Views[0].ViewId,
                    PackageId = manifest.Views[0].PackageId,
                    ComponentName = manifest.Views[0].ComponentName,
                    ComponentSourcePath = manifest.Views[0].ComponentSourcePath,
                    ViewModelType = manifest.Views[0].ViewModelType,
                    Layer = manifest.Views[0].Layer,
                    RequiredResources = manifest.Views[0].RequiredResources,
                    NamedControls = new[]
                    {
                        new MxFairyGuiNamedControl("btnGhost", "Button", required: false)
                    },
                    Commands = new[]
                    {
                        new MxFairyGuiCommandBinding("runtimeHud.ghost", "btnGhost")
                    }
                }
            };

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.ValidateSources(
                manifest,
                FindRepositoryRoot(),
                CreateRuntimeHudCatalog());

            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.CommandControlUnknown), "missing command button in source XML should be reported.");
        }

        private static void InvalidManifest_ReturnsStructuredDiagnostics()
        {
            var manifest = new MxFairyGuiManifest
            {
                Packages = new[]
                {
                    new MxFairyGuiManifestPackage
                    {
                        PackageId = "mx.smoke",
                        PackageName = "",
                        PackageBytes = new ResourceKey("Bad Key", MxFairyGuiManifestResourceTypeIds.PackageBytes)
                    }
                },
                Views = new[]
                {
                    new MxFairyGuiManifestView
                    {
                        ViewId = new MxUiViewId("ui.smoke"),
                        PackageId = "mx.missing",
                        ComponentName = "",
                        RequiredResources = new[] { new ResourceKey("", ResourceTypeIds.Texture2D) },
                        NamedControls = new[]
                        {
                            new MxFairyGuiNamedControl("txtTitle", "", required: true)
                        },
                        Commands = new[]
                        {
                            new MxFairyGuiCommandBinding("refresh", "btnMissing")
                        }
                    }
                }
            };

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.Validate(manifest);

            Require(!result.Success, "invalid manifest should fail validation.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.PackageNameMissing), "missing package name should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.ResourceKeyInvalid), "invalid resources should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.ViewPackageUnknown), "unknown package should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.ComponentMissing), "missing component should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.ControlTypeMissing), "missing control type should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.CommandControlUnknown), "unknown command control should be reported.");

            MxFairyGuiManifestDiagnostic diagnostic = Find(result, MxFairyGuiManifestDiagnosticCode.CommandControlUnknown);
            Require(diagnostic.Target == MxFairyGuiManifestDiagnosticTarget.Command, "command diagnostic should target commands.");
            Require(diagnostic.PackageId == "mx.missing", "command diagnostic should carry package id.");
            Require(diagnostic.ViewId == "ui.smoke", "command diagnostic should carry view id.");
            Require(diagnostic.Field == "commands[0].controlName", "command diagnostic should carry field path.");
        }

        private static void InvalidSchemaVersion_IsReported()
        {
            MxFairyGuiManifest manifest = CreateValidManifest();
            manifest.SchemaVersion = "mx.fairygui.manifest.v0";

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.Validate(manifest);

            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.ManifestSchemaVersionUnsupported), "unsupported schema version should be reported.");
        }

        private static void CommandBoundToTextControl_IsReported()
        {
            MxFairyGuiManifest manifest = CreateValidManifest();
            manifest.Views = new[]
            {
                new MxFairyGuiManifestView
                {
                    ViewId = new MxUiViewId("ui.smoke"),
                    PackageId = "mx.smoke",
                    ComponentName = "SmokePanel",
                    ViewModelType = "SmokeViewModel",
                    NamedControls = new[]
                    {
                        new MxFairyGuiNamedControl("txtTitle", "GTextField")
                    },
                    Commands = new[]
                    {
                        new MxFairyGuiCommandBinding("refresh", "txtTitle")
                    }
                }
            };

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.Validate(manifest);

            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.CommandControlNotButton), "command binding to a text control should be reported.");
        }

        private static void DuplicateIds_AreReported()
        {
            MxFairyGuiManifest manifest = CreateValidManifest();
            manifest.Packages = new[]
            {
                manifest.Packages[0],
                new MxFairyGuiManifestPackage
                {
                    PackageId = "mx.smoke",
                    PackageName = "MxFguiSmoke",
                    PackageBytes = new ResourceKey("ui.smoke.copy.bytes", MxFairyGuiManifestResourceTypeIds.PackageBytes)
                }
            };
            manifest.Views = new[]
            {
                manifest.Views[0],
                new MxFairyGuiManifestView
                {
                    ViewId = new MxUiViewId("ui.smoke"),
                    PackageId = "mx.smoke",
                    ComponentName = "OtherPanel",
                    NamedControls = new[]
                    {
                        new MxFairyGuiNamedControl("txtTitle", "GTextField"),
                        new MxFairyGuiNamedControl("txtTitle", "GTextField")
                    },
                    Commands = new[]
                    {
                        new MxFairyGuiCommandBinding("refresh", "txtTitle"),
                        new MxFairyGuiCommandBinding("refresh", "txtTitle")
                    }
                }
            };

            MxFairyGuiManifestValidationResult result = MxFairyGuiManifestValidator.Validate(manifest);

            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.PackageIdDuplicate), "duplicate package id should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.PackageNameDuplicate), "duplicate package name should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.ViewIdDuplicate), "duplicate view id should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.ControlDuplicate), "duplicate control should be reported.");
            Require(result.HasCode(MxFairyGuiManifestDiagnosticCode.CommandDuplicate), "duplicate command should be reported.");
        }

        private static void FeedbackPolicy_DefaultsProjectsAndGeneratesExplicitly()
        {
            var clickBinding = new MxFairyGuiCommandBinding("ui.smoke.click", "btnClick");
            var backPolicy = new MxUiFeedbackPolicy(
                MxUiFeedbackKind.Back,
                MxUiFeedbackKind.None,
                MxUiFeedbackKind.None);
            var backBinding = new MxFairyGuiCommandBinding(
                "ui.smoke.back",
                "btnBack",
                backPolicy);
            var manifest = new MxFairyGuiManifest
            {
                Views = new[]
                {
                    new MxFairyGuiManifestView
                    {
                        ViewId = new MxUiViewId("ui.smoke"),
                        Commands = new[] { clickBinding, backBinding }
                    }
                }
            };

            Require(
                clickBinding.Feedback == new MxUiFeedbackPolicy(
                    MxUiFeedbackKind.Click,
                    MxUiFeedbackKind.None,
                    MxUiFeedbackKind.None),
                "New generation commands should default to Click feedback.");
            Require(
                MxFairyGuiManifestProjection.CreateFeedbackPolicyRegistry(manifest)
                    .TryGetPolicy(
                        new MxUiViewId("ui.smoke"),
                        "ui.smoke.back",
                        out MxUiFeedbackPolicy projected) &&
                projected == backPolicy,
                "Manifest projection should preserve explicit feedback policy.");
            Require(
                MxFairyGuiManifestProjection.CreateViewContracts(manifest)[0]
                    .Commands[1].Feedback == backPolicy,
                "View command descriptors should preserve feedback policy.");

            MxFairyGuiManifestGenerationResult generated =
                MxFairyGuiManifestGenerator.Generate(
                    CreateCharacterTestSettingsPanelGenerationSpec(),
                    FindRepositoryRoot());
            Require(generated.Success, "Settings manifest generation should remain valid.");
            Require(
                generated.SourceText.Contains(
                    "new MxUiFeedbackPolicy(MxUiFeedbackKind.None, MxUiFeedbackKind.Confirm, MxUiFeedbackKind.Error)",
                    StringComparison.Ordinal),
                "Generated manifest source should emit explicit result feedback.");
        }

        private static MxFairyGuiManifest CreateValidManifest()
        {
            return new MxFairyGuiManifest
            {
                Packages = new[]
                {
                    new MxFairyGuiManifestPackage
                    {
                        PackageId = "mx.smoke",
                        PackageName = "MxFguiSmoke",
                        PackageBytes = new ResourceKey("ui.smoke.bytes", MxFairyGuiManifestResourceTypeIds.PackageBytes),
                        RequiredResources = new[]
                        {
                            new MxFairyGuiManifestResource(new ResourceKey("ui.smoke.atlas", ResourceTypeIds.Texture2D), MxFairyGuiManifestResourceKind.Atlas)
                        }
                    }
                },
                Views = new[]
                {
                    new MxFairyGuiManifestView
                    {
                        ViewId = new MxUiViewId("ui.smoke"),
                        PackageId = "mx.smoke",
                        ComponentName = "SmokePanel",
                        ViewModelType = "SmokeViewModel",
                        RequiredResources = new[] { new ResourceKey("ui.smoke.title", ResourceTypeIds.Object) },
                        NamedControls = new[]
                        {
                            new MxFairyGuiNamedControl("txtTitle", "GTextField"),
                            new MxFairyGuiNamedControl("btnRefresh", "GButton")
                        },
                        Commands = new[]
                        {
                            new MxFairyGuiCommandBinding("refresh", "btnRefresh")
                        }
                    }
                }
            };
        }

        private static MxFairyGuiManifest CreateRuntimeHudManifest()
        {
            ResourceKey packageBytes = new ResourceKey(
                "ui.fairygui.runtimehud.fui",
                MxFairyGuiManifestResourceTypeIds.PackageBytes,
                packageId: "MxRuntimeHud");

            return new MxFairyGuiManifest
            {
                Packages = new[]
                {
                    new MxFairyGuiManifestPackage
                    {
                        PackageId = "MxRuntimeHud",
                        PackageName = "MxRuntimeHud",
                        PackageSourcePath = "FGUIProject/assets/MxRuntimeHud/package.xml",
                        PackageBytesPath = "Assets/Bundles/FGUI/MxRuntimeHud/MxRuntimeHud_fui.bytes",
                        PackageBytes = packageBytes
                    }
                },
                Views = new[]
                {
                    new MxFairyGuiManifestView
                    {
                        ViewId = new MxUiViewId("ui.runtimehud.main"),
                        PackageId = "MxRuntimeHud",
                        ComponentName = "RuntimeHudPanel",
                        ComponentSourcePath = "FGUIProject/assets/MxRuntimeHud/RuntimeHudPanel.xml",
                        ViewModelType = "MxFramework.Demo.RuntimeAbilitySliceHudViewModel",
                        Layer = MxUiLayer.Hud,
                        RequiredResources = new[] { packageBytes },
                        NamedControls = new[]
                        {
                            new MxFairyGuiNamedControl("title", "Text", bindPath: "Title"),
                            new MxFairyGuiNamedControl("mode", "Text", bindPath: "ModeName"),
                            new MxFairyGuiNamedControl("playerName", "Text", bindPath: "Player.DisplayName"),
                            new MxFairyGuiNamedControl("playerHp", "Text", bindPath: "Player.Hp"),
                            new MxFairyGuiNamedControl("enemyName", "Text", bindPath: "Enemy.DisplayName"),
                            new MxFairyGuiNamedControl("enemyHp", "Text", bindPath: "Enemy.Hp"),
                            new MxFairyGuiNamedControl("recentAction", "Text", bindPath: "Feedback.RecentActionText"),
                            new MxFairyGuiNamedControl("btnStrike", "Button"),
                            new MxFairyGuiNamedControl("btnReset", "Button")
                        },
                        LocalizedTexts = new[]
                        {
                            new MxFairyGuiLocalizedTextBinding("title", "ui.runtimehud.title", "Runtime HUD"),
                            new MxFairyGuiLocalizedTextBinding("mode", "ui.runtimehud.mode", "Mode")
                        },
                        Commands = new[]
                        {
                            new MxFairyGuiCommandBinding("runtimeHud.strike", "btnStrike"),
                            new MxFairyGuiCommandBinding("runtimeHud.reset", "btnReset")
                        }
                    }
                }
            };
        }

        private static ResourceCatalog CreateRuntimeHudCatalog()
        {
            return new ResourceCatalog(
                "ui.runtimehud.test.catalog",
                "MxRuntimeHud",
                new[]
                {
                    new ResourceCatalogEntry(
                        "ui.fairygui.runtimehud.fui",
                        MxFairyGuiManifestResourceTypeIds.PackageBytes,
                        "memory",
                        "fgui/MxRuntimeHud_fui.bytes",
                        packageId: "MxRuntimeHud")
                });
        }

        private static ResourceCatalog CreateStoryDialogCatalog()
        {
            return new ResourceCatalog(
                "ui.storydialog.test.catalog",
                "MxStoryDialog",
                new[]
                {
                    new ResourceCatalogEntry(
                        "ui.fairygui.storydialog.fui",
                        MxFairyGuiManifestResourceTypeIds.PackageBytes,
                        "memory",
                        "fgui/MxStoryDialog_fui.bytes",
                        packageId: "MxStoryDialog")
                });
        }

        private static MxFairyGuiManifestGenerationResult GenerateRuntimeHudManifestSource()
        {
            return MxFairyGuiManifestGenerator.Generate(CreateRuntimeHudGenerationSpec(), FindRepositoryRoot());
        }

        private static MxFairyGuiManifestGenerationResult GenerateStoryDialogManifestSource()
        {
            return MxFairyGuiManifestGenerator.Generate(CreateStoryDialogGenerationSpec(), FindRepositoryRoot());
        }

        private static MxFairyGuiManifestGenerationResult GenerateCharacterTestCombatHudManifestSource()
        {
            return MxFairyGuiManifestGenerator.Generate(CreateCharacterTestCombatHudGenerationSpec(), FindRepositoryRoot());
        }

        private static MxFairyGuiBinderGenerationResult GenerateCharacterTestCombatHudBinderSource()
        {
            return MxFairyGuiManifestGenerator.GenerateBinder(CreateCharacterTestCombatHudBinderGenerationSpec());
        }

        private static MxFairyGuiManifestGenerationResult GenerateCharacterTestSettingsPanelManifestSource()
        {
            return MxFairyGuiManifestGenerator.Generate(CreateCharacterTestSettingsPanelGenerationSpec(), FindRepositoryRoot());
        }

        private static MxFairyGuiBinderGenerationResult GenerateCharacterTestSettingsPanelBinderSource()
        {
            return MxFairyGuiManifestGenerator.GenerateBinder(CreateCharacterTestSettingsPanelBinderGenerationSpec());
        }

        private static MxFairyGuiManifestGenerationResult GenerateCharacterTestNetworkPanelManifestSource()
        {
            return MxFairyGuiManifestGenerator.Generate(CreateCharacterTestNetworkPanelGenerationSpec(), FindRepositoryRoot());
        }

        private static MxFairyGuiBinderGenerationResult GenerateCharacterTestNetworkPanelBinderSource()
        {
            return MxFairyGuiManifestGenerator.GenerateBinder(CreateCharacterTestNetworkPanelBinderGenerationSpec());
        }

        private static MxFairyGuiManifestGenerationResult GenerateCharacterTestMainMenuManifestSource()
        {
            return MxFairyGuiManifestGenerator.Generate(CreateCharacterTestMainMenuGenerationSpec(), FindRepositoryRoot());
        }

        private static MxFairyGuiBinderGenerationResult GenerateCharacterTestMainMenuBinderSource()
        {
            return MxFairyGuiManifestGenerator.GenerateBinder(CreateCharacterTestMainMenuBinderGenerationSpec());
        }

        private static MxFairyGuiManifestGenerationResult GenerateCharacterTestAbilityBindingPanelManifestSource()
        {
            return MxFairyGuiManifestGenerator.Generate(CreateCharacterTestAbilityBindingPanelGenerationSpec(), FindRepositoryRoot());
        }

        private static MxFairyGuiBinderGenerationResult GenerateCharacterTestAbilityBindingPanelBinderSource()
        {
            return MxFairyGuiManifestGenerator.GenerateBinder(CreateCharacterTestAbilityBindingPanelBinderGenerationSpec());
        }

        private static MxFairyGuiManifestGenerationResult GenerateCharacterTestBehaviorDeckPanelManifestSource()
        {
            return MxFairyGuiManifestGenerator.Generate(CreateCharacterTestBehaviorDeckPanelGenerationSpec(), FindRepositoryRoot());
        }

        private static MxFairyGuiBinderGenerationResult GenerateCharacterTestBehaviorDeckPanelBinderSource()
        {
            return MxFairyGuiManifestGenerator.GenerateBinder(CreateCharacterTestBehaviorDeckPanelBinderGenerationSpec());
        }

        private static MxFairyGuiManifestGenerationResult GenerateCharacterTestStoryDialogManifestSource()
        {
            return MxFairyGuiManifestGenerator.Generate(CreateCharacterTestStoryDialogGenerationSpec(), FindRepositoryRoot());
        }

        private static MxFairyGuiBinderGenerationResult GenerateCharacterTestStoryDialogBinderSource()
        {
            return MxFairyGuiManifestGenerator.GenerateBinder(CreateCharacterTestStoryDialogBinderGenerationSpec());
        }

        private static MxFairyGuiManifestGenerationBatchResult GenerateManifestBatch(bool validateGeneratedOutput)
        {
            ManifestTarget[] targets = CreateManifestTargets();
            var items = new MxFairyGuiManifestGenerationBatchItem[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                items[i] = new MxFairyGuiManifestGenerationBatchItem(
                    targets[i].Name,
                    targets[i].Spec,
                    targets[i].GeneratedSourcePath,
                    targets[i].BinderSpec,
                    targets[i].GeneratedBinderSourcePath);
            }

            return MxFairyGuiManifestGenerator.GenerateBatch(
                items,
                FindRepositoryRoot(),
                validateGeneratedOutput);
        }

        private static ManifestTarget[] CreateManifestTargets()
        {
            return new[]
            {
                new ManifestTarget(
                    "runtime-hud",
                    CreateRuntimeHudGenerationSpec(),
                    RuntimeHudGeneratedManifestPath()),
                new ManifestTarget(
                    "story-dialog",
                    CreateStoryDialogGenerationSpec(),
                    StoryDialogGeneratedManifestPath()),
                new ManifestTarget(
                    "character-test-combat-hud",
                    CreateCharacterTestCombatHudGenerationSpec(),
                    CharacterTestCombatHudGeneratedManifestPath(),
                    CreateCharacterTestCombatHudBinderGenerationSpec(),
                    CharacterTestCombatHudGeneratedBinderPath()),
                new ManifestTarget(
                    "character-test-settings-panel",
                    CreateCharacterTestSettingsPanelGenerationSpec(),
                    CharacterTestSettingsPanelGeneratedManifestPath(),
                    CreateCharacterTestSettingsPanelBinderGenerationSpec(),
                    CharacterTestSettingsPanelGeneratedBinderPath()),
                new ManifestTarget(
                    "character-test-network-panel",
                    CreateCharacterTestNetworkPanelGenerationSpec(),
                    CharacterTestNetworkPanelGeneratedManifestPath(),
                    CreateCharacterTestNetworkPanelBinderGenerationSpec(),
                    CharacterTestNetworkPanelGeneratedBinderPath()),
                new ManifestTarget(
                    "character-test-main-menu",
                    CreateCharacterTestMainMenuGenerationSpec(),
                    CharacterTestMainMenuGeneratedManifestPath(),
                    CreateCharacterTestMainMenuBinderGenerationSpec(),
                    CharacterTestMainMenuGeneratedBinderPath()),
                new ManifestTarget(
                    "character-test-ability-binding-panel",
                    CreateCharacterTestAbilityBindingPanelGenerationSpec(),
                    CharacterTestAbilityBindingPanelGeneratedManifestPath(),
                    CreateCharacterTestAbilityBindingPanelBinderGenerationSpec(),
                    CharacterTestAbilityBindingPanelGeneratedBinderPath()),
                new ManifestTarget(
                    "character-test-behavior-deck-panel",
                    CreateCharacterTestBehaviorDeckPanelGenerationSpec(),
                    CharacterTestBehaviorDeckPanelGeneratedManifestPath(),
                    CreateCharacterTestBehaviorDeckPanelBinderGenerationSpec(),
                    CharacterTestBehaviorDeckPanelGeneratedBinderPath()),
                new ManifestTarget(
                    "character-test-tactical-deck-panel",
                    CreateCharacterTestTacticalDeckPanelGenerationSpec(),
                    CharacterTestTacticalDeckPanelGeneratedManifestPath()),
                new ManifestTarget(
                    "character-test-story-dialog",
                    CreateCharacterTestStoryDialogGenerationSpec(),
                    CharacterTestStoryDialogGeneratedManifestPath(),
                    CreateCharacterTestStoryDialogBinderGenerationSpec(),
                    CharacterTestStoryDialogGeneratedBinderPath()),
                new ManifestTarget(
                    "ui-foundation-demo",
                    CreateUiFoundationDemoGenerationSpec(),
                    UiFoundationDemoGeneratedManifestPath(),
                    CreateUiFoundationDemoBinderGenerationSpec(),
                    UiFoundationDemoGeneratedBinderPath()),
                new ManifestTarget(
                    "player-guidance-card",
                    CreatePlayerGuidanceCardGenerationSpec(),
                    PlayerGuidanceCardGeneratedManifestPath(),
                    CreatePlayerGuidanceCardBinderGenerationSpec(),
                    PlayerGuidanceCardGeneratedBinderPath()),
                new ManifestTarget(
                    "player-guidance-notices",
                    CreatePlayerGuidanceNoticesGenerationSpec(),
                    PlayerGuidanceNoticesGeneratedManifestPath(),
                    CreatePlayerGuidanceNoticesBinderGenerationSpec(),
                    PlayerGuidanceNoticesGeneratedBinderPath())
            };
        }

        private static ManifestTarget FindTarget(string name)
        {
            ManifestTarget[] targets = CreateManifestTargets();
            for (int i = 0; i < targets.Length; i++)
            {
                if (string.Equals(targets[i].Name, name, StringComparison.Ordinal))
                    return targets[i];
            }

            throw new InvalidOperationException("FairyGUI manifest target was not found: " + name + ".");
        }

        private static void CharacterTestMainMenuComposerBindingContract_IsCurrent()
        {
            string path = CharacterTestMainMenuComposerBindingContractPath();
            Require(File.Exists(path), "CharacterTest main menu Composer binding contract is missing.");
            Require(
                string.Equals(File.ReadAllText(path), GenerateCharacterTestMainMenuComposerBindingContract(), StringComparison.Ordinal),
                "CharacterTest main menu Composer binding contract is stale; run --write-character-test-main-menu-composer-contract.");
        }

        private static void CharacterTestCombatHudComposerBindingContract_IsCurrent()
        {
            string path = CharacterTestCombatHudComposerBindingContractPath();
            Require(File.Exists(path), "CharacterTest Combat HUD Composer binding contract is missing.");
            Require(
                string.Equals(File.ReadAllText(path), GenerateCharacterTestCombatHudComposerBindingContract(), StringComparison.Ordinal),
                "CharacterTest Combat HUD Composer binding contract is stale; run --write-character-test-combat-hud-composer-contract.");
        }

        private static void CharacterTestSettingsPanelComposerBindingContract_IsCurrent()
        {
            string path = CharacterTestSettingsPanelComposerBindingContractPath();
            Require(File.Exists(path), "CharacterTest settings panel Composer binding contract is missing.");
            string generatedContract = GenerateCharacterTestSettingsPanelComposerBindingContract();
            Require(
                generatedContract.Contains("\"controlName\": \"inputList\"", StringComparison.Ordinal) &&
                generatedContract.Contains("\"controlName\": \"btnInputRebindProxy\"", StringComparison.Ordinal),
                "CharacterTest settings panel Composer contract should protect the dynamic input list and its command proxy.");
            Require(
                string.Equals(File.ReadAllText(path), generatedContract, StringComparison.Ordinal),
                "CharacterTest settings panel Composer binding contract is stale; run --write-character-test-settings-panel-composer-contract.");
        }

        private static void WriteCharacterTestSettingsPanelComposerBindingContract()
        {
            string path = CharacterTestSettingsPanelComposerBindingContractPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, GenerateCharacterTestSettingsPanelComposerBindingContract());
            Console.WriteLine("Regenerated ArtSource/UIKits/MxUiFoundation/contracts/character_test_settings.ui-composer-binding-contract.json");
        }

        private static string GenerateCharacterTestSettingsPanelComposerBindingContract()
        {
            MxFairyGuiManifestGenerationSpec manifest = CreateCharacterTestSettingsPanelGenerationSpec();
            MxFairyGuiBinderGenerationSpec binder = CreateCharacterTestSettingsPanelBinderGenerationSpec();
            var bindPaths = manifest.ControlBindPaths.ToDictionary(item => item.ControlName, item => item.BindPath, StringComparer.Ordinal);
            var commands = manifest.Commands.ToDictionary(item => item.ControlName, item => item, StringComparer.Ordinal);
            var bindings = new List<object>();
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(CharacterTestSettingsPanelBlueprintPath())))
            {
                foreach (JsonElement node in document.RootElement.GetProperty("nodes").EnumerateArray())
                {
                    string slotId = node.GetProperty("bindingSlot").GetString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(slotId)) continue;
                    string controlName = node.GetProperty("name").GetString() ?? string.Empty;
                    string nodeType = node.GetProperty("type").GetString() ?? string.Empty;
                    if (commands.TryGetValue(controlName, out MxFairyGuiCommandBinding command))
                    {
                        bindings.Add(new
                        {
                            slotId,
                            controlName,
                            nodeType,
                            required = true,
                            bindPath = bindPaths.TryGetValue(controlName, out string bindPath) ? bindPath : string.Empty,
                            commandId = command.CommandId,
                            feedback = FeedbackContract(command.Feedback)
                        });
                    }
                    else
                    {
                        bindings.Add(new
                        {
                            slotId,
                            controlName,
                            nodeType,
                            required = true,
                            bindPath = bindPaths.TryGetValue(controlName, out string bindPath) ? bindPath : string.Empty,
                            commandId = string.Empty
                        });
                    }
                }
            }

            string[] focusOrder = binder.FocusOrderExpressions
                .Select(expression => ResolveControlNameExpression(manifest.ControlNameExpressions, expression))
                .ToArray();
            string defaultFocus = ResolveControlNameExpression(manifest.ControlNameExpressions, binder.DefaultFocusExpression);
            var contract = new
            {
                schemaVersion = "mx.uiComposerBindingContract.v2",
                screenId = "ui.screen.character_test.settings",
                viewId = manifest.ViewId,
                target = new
                {
                    packageId = manifest.PackageId,
                    packageSourcePath = Path.GetDirectoryName(manifest.PackageSourcePath).Replace('\\', '/'),
                    componentName = manifest.ComponentName,
                    publishedPackagePath = manifest.PackageBytesPath,
                    publishedAtlasPath = "Assets/Bundles/FGUI/MxCharacterTestSettingsUi/MxCharacterTestSettingsUi_atlas0.png",
                    packageBytesResourceId = manifest.PackageBytesResourceId,
                    atlasResourceId = manifest.AdditionalPackageResources[0].ResourceId
                },
                bindings,
                focusOrder,
                defaultFocus
            };
            return JsonSerializer.Serialize(contract, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        }

        private static object FeedbackContract(MxUiFeedbackPolicy policy)
        {
            return new
            {
                activation = policy.Activation.ToString(),
                success = policy.Success.ToString(),
                failure = policy.Failure.ToString()
            };
        }

        private static void WriteCharacterTestCombatHudComposerBindingContract()
        {
            string path = CharacterTestCombatHudComposerBindingContractPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, GenerateCharacterTestCombatHudComposerBindingContract());
            Console.WriteLine("Regenerated ArtSource/UIKits/MxUiFoundation/contracts/character_test_combat_hud.ui-composer-binding-contract.json");
        }

        private static string GenerateCharacterTestCombatHudComposerBindingContract()
        {
            MxFairyGuiManifestGenerationSpec manifest = CreateCharacterTestCombatHudGenerationSpec();
            var bindPaths = manifest.ControlBindPaths.ToDictionary(item => item.ControlName, item => item.BindPath, StringComparer.Ordinal);
            var bindings = new List<object>();
            IReadOnlyList<MxFairyGuiCodeExpressionSpec> controls = manifest.ControlNameExpressions;
            for (int i = 0; i < controls.Count; i++)
            {
                string controlName = controls[i].Value;
                bindings.Add(new
                {
                    slotId = controlName,
                    controlName,
                    nodeType = CharacterTestCombatHudComposerNodeType(controlName),
                    required = true,
                    bindPath = bindPaths.TryGetValue(controlName, out string bindPath) ? bindPath : string.Empty,
                    commandId = string.Empty
                });
            }

            var contract = new
            {
                schemaVersion = "mx.uiComposerBindingContract.v1",
                screenId = "ui.screen.character_test.combat_hud",
                viewId = manifest.ViewId,
                target = new
                {
                    packageId = "MxCharacterTestCombatHud",
                    packageSourcePath = "FGUIProject/assets/MxCharacterTestCombatHud",
                    componentName = manifest.ComponentName,
                    publishedPackagePath = "Assets/Bundles/FGUI/MxCharacterTestCombatHud/MxCharacterTestCombatHud_fui.bytes",
                    publishedAtlasPath = "Assets/Bundles/FGUI/MxCharacterTestCombatHud/MxCharacterTestCombatHud_atlas0.png",
                    packageBytesResourceId = "ui.fairygui.charactertest.combat_hud.fui",
                    atlasResourceId = "ui.fairygui.charactertest.combat_hud.atlas0"
                },
                bindings,
                focusOrder = Array.Empty<string>(),
                defaultFocus = string.Empty
            };
            return JsonSerializer.Serialize(contract, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        }

        private static string CharacterTestCombatHudComposerNodeType(string controlName)
        {
            if (controlName == "feedbackTrim"
                || controlName == "behaviorCardPanel"
                || controlName == "behaviorCardIcon"
                || controlName.Contains("BuffSlot", StringComparison.Ordinal) &&
                   (controlName.EndsWith("Bg", StringComparison.Ordinal)
                    || controlName.EndsWith("Border", StringComparison.Ordinal)
                    || controlName.EndsWith("CooldownFill", StringComparison.Ordinal))
                || controlName.StartsWith("abilitySlot", StringComparison.Ordinal)
                   && controlName.EndsWith("CooldownFill", StringComparison.Ordinal))
            {
                return "Graph";
            }

            if (controlName == "playerPlate"
                || controlName == "playerHealthIcon"
                || controlName == "playerHpTrack"
                || controlName == "playerHpFill"
                || controlName == "playerPostureIcon"
                || controlName == "playerPostureTrack"
                || controlName == "playerPostureFill"
                || controlName == "playerPostureBreakMarker"
                || controlName == "targetPlate"
                || controlName == "targetHpTrack"
                || controlName == "targetHpFill"
                || controlName == "targetPostureTrack"
                || controlName == "targetPostureFill"
                || controlName == "targetPostureBreakMarker"
                || controlName == "lockCrosshair"
                || controlName == "feedbackPanel"
                || controlName == "networkLatencyBadgeBg"
                || controlName.StartsWith("abilitySlot", StringComparison.Ordinal)
                   && (controlName.EndsWith("Bg", StringComparison.Ordinal)
                       || controlName.EndsWith("Selected", StringComparison.Ordinal)))
            {
                return "Image";
            }

            return "Text";
        }

        private static void WriteCharacterTestMainMenuComposerBindingContract()
        {
            string path = CharacterTestMainMenuComposerBindingContractPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, GenerateCharacterTestMainMenuComposerBindingContract());
            Console.WriteLine("Regenerated ArtSource/UIKits/MxUiFoundation/contracts/character_test_main_menu.ui-composer-binding-contract.json");
        }

        private static string GenerateCharacterTestMainMenuComposerBindingContract()
        {
            MxFairyGuiManifestGenerationSpec manifest = CreateCharacterTestMainMenuGenerationSpec();
            MxFairyGuiBinderGenerationSpec binder = CreateCharacterTestMainMenuBinderGenerationSpec();
            var bindings = new List<object>();
            IReadOnlyList<MxFairyGuiControlBindPathSpec> textBindings = manifest.ControlBindPaths;
            for (int i = 0; i < textBindings.Count; i++)
            {
                MxFairyGuiControlBindPathSpec binding = textBindings[i];
                bindings.Add(new
                {
                    slotId = ComposerSlotId(binding.ControlName),
                    controlName = binding.ControlName,
                    nodeType = "Text",
                    required = true,
                    bindPath = binding.BindPath,
                    commandId = string.Empty
                });
            }

            IReadOnlyList<MxFairyGuiCommandBinding> commands = manifest.Commands;
            for (int i = 0; i < commands.Count; i++)
            {
                MxFairyGuiCommandBinding binding = commands[i];
                bindings.Add(new
                {
                    slotId = ComposerSlotId(binding.ControlName),
                    controlName = binding.ControlName,
                    nodeType = "ComponentInstance",
                    required = true,
                    bindPath = string.Empty,
                    commandId = binding.CommandId
                });
            }

            bindings.Add(new { slotId = "tacticalHome", controlName = "tacticalHome", nodeType = "ComponentInstance", required = true, bindPath = string.Empty, commandId = string.Empty });

            string[] focusOrder = binder.FocusOrderExpressions
                .Select(expression => ResolveControlNameExpression(manifest.ControlNameExpressions, expression))
                .ToArray();
            string defaultFocus = ResolveControlNameExpression(manifest.ControlNameExpressions, binder.DefaultFocusExpression);
            var contract = new
            {
                schemaVersion = "mx.uiComposerBindingContract.v1",
                screenId = "ui.screen.character_test.main_menu",
                viewId = manifest.ViewId,
                target = new
                {
                    packageId = manifest.PackageId,
                    packageSourcePath = Path.GetDirectoryName(manifest.PackageSourcePath).Replace('\\', '/'),
                    componentName = manifest.ComponentName,
                    publishedPackagePath = manifest.PackageBytesPath,
                    publishedAtlasPath = "Assets/Bundles/FGUI/MxCharacterTestUi/MxCharacterTestUi_atlas0.png",
                    packageBytesResourceId = manifest.PackageBytesResourceId,
                    atlasResourceId = manifest.AdditionalPackageResources[0].ResourceId
                },
                bindings,
                componentBindings = CreateTacticalHomepageComponentContracts(),
                focusOrder,
                defaultFocus
            };
            return JsonSerializer.Serialize(contract, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        }

        private static object[] CreateTacticalHomepageComponentContracts()
        {
            object Slot(string name, string type, string path = "", string command = "") => new
            {
                slotId = name, controlName = name, nodeType = type, required = true,
                bindPath = path, commandId = command
            };
            return new object[]
            {
                new
                {
                    componentRecipeId = "ui.component.tactical.home.surface",
                    bindings = new[]
                    {
                        Slot("title", "Text", "Title"), Slot("message", "Text", "Message"),
                        Slot("btnContinue", "ComponentInstance", command: "character_test.main_menu.start"),
                        Slot("btnNew", "ComponentInstance", command: "character_test.main_menu.restart_offline"),
                        Slot("btnSettings", "ComponentInstance", command: "character_test.main_menu.settings"),
                        Slot("btnExit", "ComponentInstance", command: "character_test.main_menu.close"),
                        Slot("confirmationShade", "Graph"), Slot("confirmation", "ComponentInstance")
                    }
                },
                new
                {
                    componentRecipeId = "ui.component.tactical.home.confirmation",
                    bindings = new[]
                    {
                        Slot("title", "Text"), Slot("body", "Text"),
                        Slot("btnCancel", "ComponentInstance"), Slot("btnConfirm", "ComponentInstance")
                    }
                },
                new
                {
                    componentRecipeId = "ui.component.tactical.loading.surface",
                    bindings = new[] { Slot("shade", "Graph"), Slot("panel", "ComponentInstance") }
                },
                new
                {
                    componentRecipeId = "ui.component.tactical.loading.panel",
                    bindings = new[]
                    {
                        Slot("title", "Text"), Slot("stage", "Text"), Slot("detail", "Text"),
                        Slot("waterTrace", "Image"), Slot("btnRetry", "ComponentInstance"), Slot("btnBack", "ComponentInstance")
                    }
                }
            };
        }

        private static string ResolveControlNameExpression(IReadOnlyList<MxFairyGuiCodeExpressionSpec> expressions, string expression)
        {
            for (int i = 0; i < expressions.Count; i++)
            {
                if (string.Equals(expressions[i].Expression, expression, StringComparison.Ordinal))
                    return expressions[i].Value;
            }
            throw new InvalidOperationException("Composer binding contract cannot resolve control expression: " + expression + ".");
        }

        private static string ComposerSlotId(string controlName)
        {
            return controlName.StartsWith("btn", StringComparison.Ordinal) && controlName.Length > 3
                ? char.ToLowerInvariant(controlName[3]) + controlName.Substring(4)
                : controlName;
        }

        private static string CharacterTestMainMenuComposerBindingContractPath()
        {
            return Path.Combine(FindRepositoryRoot(), "ArtSource", "UIKits", "MxUiFoundation", "contracts", "character_test_main_menu.ui-composer-binding-contract.json");
        }

        private static string CharacterTestCombatHudComposerBindingContractPath()
        {
            return Path.Combine(FindRepositoryRoot(), "ArtSource", "UIKits", "MxUiFoundation", "contracts", "character_test_combat_hud.ui-composer-binding-contract.json");
        }

        private static string CharacterTestSettingsPanelComposerBindingContractPath()
        {
            return Path.Combine(FindRepositoryRoot(), "ArtSource", "UIKits", "MxUiFoundation", "contracts", "character_test_settings.ui-composer-binding-contract.json");
        }

        private static string CharacterTestSettingsPanelBlueprintPath()
        {
            return Path.Combine(FindRepositoryRoot(), "ArtSource", "UIKits", "MxUiFoundation", "screens", "character_test_settings.ui-screen.json");
        }

        private static void WriteGeneratedManifest(ManifestTarget target)
        {
            MxFairyGuiManifestGenerationResult result = MxFairyGuiManifestGenerator.Generate(target.Spec, FindRepositoryRoot());
            Require(result.Success, target.Name + " manifest generation should not produce errors.");
            string path = ResolveGeneratedPath(target);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, result.SourceText);
            Console.WriteLine("Regenerated " + target.GeneratedSourcePath);
        }

        private static void WriteGeneratedBinder(ManifestTarget target)
        {
            if (!target.HasBinder)
                return;

            MxFairyGuiBinderGenerationResult result = MxFairyGuiManifestGenerator.GenerateBinder(target.BinderSpec);
            Require(result.Success, target.Name + " binder generation should not produce errors.");
            string path = ResolveGeneratedBinderPath(target);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, result.SourceText);
            Console.WriteLine("Regenerated " + target.GeneratedBinderSourcePath);
        }

        private static void RequireGeneratedManifestCurrent(ManifestTarget target, string writeCommand)
        {
            MxFairyGuiManifestGenerationResult result = MxFairyGuiManifestGenerator.Generate(target.Spec, FindRepositoryRoot());
            Require(result.Success, target.Name + " generator should pass before stale comparison.");

            string current = File.ReadAllText(ResolveGeneratedPath(target));
            MxFairyGuiManifestValidationResult staleResult = MxFairyGuiManifestGenerator.ValidateGeneratedOutput(
                current,
                result.SourceText,
                target.GeneratedSourcePath);

            Require(staleResult.Success, "checked-in " + target.Name + " generated manifest is stale; run " + writeCommand + ".");
        }

        private static void RequireGeneratedBinderCurrent(ManifestTarget target, string writeCommand)
        {
            Require(target.HasBinder, target.Name + " should define a generated binder path.");
            MxFairyGuiBinderGenerationResult result = MxFairyGuiManifestGenerator.GenerateBinder(target.BinderSpec);
            Require(result.Success, target.Name + " binder generator should pass before stale comparison.");

            string current = File.ReadAllText(ResolveGeneratedBinderPath(target));
            MxFairyGuiManifestValidationResult staleResult = MxFairyGuiManifestGenerator.ValidateGeneratedOutput(
                current,
                result.SourceText,
                target.GeneratedBinderSourcePath);

            Require(staleResult.Success, "checked-in " + target.Name + " generated binder is stale; run " + writeCommand + ".");
        }

        private static string ResolveGeneratedPath(ManifestTarget target)
        {
            return Path.Combine(FindRepositoryRoot(), target.GeneratedSourcePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ResolveGeneratedBinderPath(ManifestTarget target)
        {
            return Path.Combine(FindRepositoryRoot(), target.GeneratedBinderSourcePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static MxFairyGuiManifestGenerationSpec CreateRuntimeHudGenerationSpec()
        {
            return new MxFairyGuiManifestGenerationSpec
            {
                NamespaceName = "MxFramework.Demo.FairyGui",
                ClassName = "RuntimeAbilitySliceFairyGuiHudManifest",
                PackageId = "MxRuntimeHud",
                PackageName = "MxRuntimeHud",
                PackageSourcePath = "FGUIProject/assets/MxRuntimeHud/package.xml",
                PackageBytesPath = "Assets/Bundles/FGUI/MxRuntimeHud/MxRuntimeHud_fui.bytes",
                PackageBytesResourceId = "ui.fairygui.runtimehud.fui",
                ComponentName = "RuntimeHudPanel",
                ComponentSourcePath = "FGUIProject/assets/MxRuntimeHud/RuntimeHudPanel.xml",
                ViewId = "ui.runtimehud.main",
                ViewModelTypeName = "MxFramework.Demo.RuntimeAbilitySliceHudViewModel",
                PackageIdExpression = "RuntimeAbilitySliceFairyGuiHudIds.PackageId",
                PackageNameExpression = "RuntimeAbilitySliceFairyGuiHudIds.PackageId",
                PackageBytesExpression = "RuntimeAbilitySliceFairyGuiHudIds.PackageBytesKey",
                ViewIdExpression = "RuntimeAbilitySliceFairyGuiHudIds.ViewId",
                ComponentNameExpression = "RuntimeAbilitySliceFairyGuiHudIds.ComponentName",
                ViewModelTypeExpression = "typeof(RuntimeAbilitySliceHudViewModel).FullName",
                Layer = MxUiLayer.Hud,
                ControlBindPaths = new[]
                {
                    new MxFairyGuiControlBindPathSpec("title", "Title"),
                    new MxFairyGuiControlBindPathSpec("mode", "ModeName"),
                    new MxFairyGuiControlBindPathSpec("playerName", "Player.DisplayName"),
                    new MxFairyGuiControlBindPathSpec("playerHp", "Player.Hp"),
                    new MxFairyGuiControlBindPathSpec("enemyName", "Enemy.DisplayName"),
                    new MxFairyGuiControlBindPathSpec("enemyHp", "Enemy.Hp"),
                    new MxFairyGuiControlBindPathSpec("recentAction", "Feedback.RecentActionText")
                },
                ControlNameExpressions = new[]
                {
                    new MxFairyGuiCodeExpressionSpec("title", "RuntimeAbilitySliceFairyGuiHudIds.Title"),
                    new MxFairyGuiCodeExpressionSpec("mode", "RuntimeAbilitySliceFairyGuiHudIds.Mode"),
                    new MxFairyGuiCodeExpressionSpec("playerName", "RuntimeAbilitySliceFairyGuiHudIds.PlayerName"),
                    new MxFairyGuiCodeExpressionSpec("playerHp", "RuntimeAbilitySliceFairyGuiHudIds.PlayerHp"),
                    new MxFairyGuiCodeExpressionSpec("enemyName", "RuntimeAbilitySliceFairyGuiHudIds.EnemyName"),
                    new MxFairyGuiCodeExpressionSpec("enemyHp", "RuntimeAbilitySliceFairyGuiHudIds.EnemyHp"),
                    new MxFairyGuiCodeExpressionSpec("recentAction", "RuntimeAbilitySliceFairyGuiHudIds.RecentAction"),
                    new MxFairyGuiCodeExpressionSpec("btnStrike", "RuntimeAbilitySliceFairyGuiHudIds.Strike"),
                    new MxFairyGuiCodeExpressionSpec("btnReset", "RuntimeAbilitySliceFairyGuiHudIds.Reset")
                },
                LocalizedTexts = new[]
                {
                    new MxFairyGuiLocalizedTextBinding("title", "ui.runtimehud.title", "Runtime HUD"),
                    new MxFairyGuiLocalizedTextBinding("mode", "ui.runtimehud.mode", "Mode")
                },
                Commands = new[]
                {
                    new MxFairyGuiCommandBinding("runtimeHud.strike", "btnStrike"),
                    new MxFairyGuiCommandBinding("runtimeHud.reset", "btnReset")
                },
                CommandIdExpressions = new[]
                {
                    new MxFairyGuiCodeExpressionSpec("runtimeHud.strike", "RuntimeAbilitySliceHudCommandIds.Strike"),
                    new MxFairyGuiCodeExpressionSpec("runtimeHud.reset", "RuntimeAbilitySliceHudCommandIds.Reset")
                }
            };
        }

        private static string RuntimeHudGeneratedManifestPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/Demos/FairyGUI/Generated/RuntimeAbilitySliceFairyGuiHudManifest.g.cs";
        }

        private static MxFairyGuiManifestGenerationSpec CreateUiFoundationDemoGenerationSpec()
        {
            return new MxFairyGuiManifestGenerationSpec
            {
                NamespaceName = "MxFramework.Demo.FairyGui",
                ClassName = "UiFoundationDemoFairyGuiManifest",
                PackageId = "MxUiFoundationDemo",
                PackageName = "MxUiFoundationDemo",
                PackageSourcePath = "FGUIProject/assets/MxUiFoundationDemo/package.xml",
                PackageBytesPath = "Assets/Bundles/FGUI/MxUiFoundationDemo/MxUiFoundationDemo_fui.bytes",
                PackageBytesResourceId = "ui.fairygui.foundationdemo.fui",
                ComponentName = "UiFoundationDemoPanel",
                ComponentSourcePath = "FGUIProject/assets/MxUiFoundationDemo/UiFoundationDemoPanel.xml",
                ViewId = "ui.foundation.demo",
                ViewModelTypeName = "MxFramework.Demo.FairyGui.UiFoundationDemoViewModel",
                PackageIdExpression = "UiFoundationDemoFairyGuiIds.PackageId",
                PackageNameExpression = "UiFoundationDemoFairyGuiIds.PackageId",
                PackageBytesExpression = "UiFoundationDemoFairyGuiIds.PackageBytesKey",
                ViewIdExpression = "UiFoundationDemoFairyGuiIds.ViewId",
                ComponentNameExpression = "UiFoundationDemoFairyGuiIds.ComponentName",
                ViewModelTypeExpression = "typeof(UiFoundationDemoViewModel).FullName",
                Layer = MxUiLayer.Modal,
                ControlBindPaths = new[]
                {
                    new MxFairyGuiControlBindPathSpec("status", "StatusText")
                },
                ControlNameExpressions = new[]
                {
                    new MxFairyGuiCodeExpressionSpec("status", "UiFoundationDemoFairyGuiIds.Status"),
                    new MxFairyGuiCodeExpressionSpec("btnPrimary", "UiFoundationDemoFairyGuiIds.Primary"),
                    new MxFairyGuiCodeExpressionSpec("btnSecondary", "UiFoundationDemoFairyGuiIds.Secondary"),
                    new MxFairyGuiCodeExpressionSpec("btnClose", "UiFoundationDemoFairyGuiIds.Close")
                },
                AdditionalPackageResources = new[]
                {
                    new MxFairyGuiPackageResourceGenerationSpec(
                        "ui.fairygui.foundationdemo.atlas0",
                        ResourceTypeIds.Texture2D,
                        MxFairyGuiManifestResourceKind.Atlas,
                        packageId: "MxUiFoundationDemo",
                        resourceKeyExpression: "UiFoundationDemoFairyGuiIds.Atlas0Key")
                },
                Commands = new[]
                {
                    new MxFairyGuiCommandBinding("ui.foundation.demo.primary", "btnPrimary"),
                    new MxFairyGuiCommandBinding("ui.foundation.demo.secondary", "btnSecondary"),
                    new MxFairyGuiCommandBinding("ui.foundation.demo.close", "btnClose")
                },
                CommandIdExpressions = new[]
                {
                    new MxFairyGuiCodeExpressionSpec("ui.foundation.demo.primary", "UiFoundationDemoCommandIds.Primary"),
                    new MxFairyGuiCodeExpressionSpec("ui.foundation.demo.secondary", "UiFoundationDemoCommandIds.Secondary"),
                    new MxFairyGuiCodeExpressionSpec("ui.foundation.demo.close", "UiFoundationDemoCommandIds.Close")
                }
            };
        }

        private static MxFairyGuiBinderGenerationSpec CreateUiFoundationDemoBinderGenerationSpec()
        {
            return new MxFairyGuiBinderGenerationSpec
            {
                NamespaceName = "MxFramework.Demo.FairyGui",
                ClassName = "UiFoundationDemoFairyGuiBinder",
                ViewModelTypeName = "UiFoundationDemoViewModel",
                ViewModelDefaultExpression = "new UiFoundationDemoViewModel()",
                ViewIdExpression = "UiFoundationDemoFairyGuiIds.ViewId",
                InvalidHandleMessage = "UI Foundation Demo binder requires a concrete FairyGUI component handle.",
                TextBindings = new[]
                {
                    new MxFairyGuiTextBindingSpec("UiFoundationDemoFairyGuiIds.Status", "viewModel.StatusText")
                },
                ButtonBindings = new[]
                {
                    new MxFairyGuiButtonBindingSpec("UiFoundationDemoFairyGuiIds.Primary", "viewModel.PrimaryLabel", "viewModel.CanInteract", "UiFoundationDemoCommandIds.Primary"),
                    new MxFairyGuiButtonBindingSpec("UiFoundationDemoFairyGuiIds.Secondary", "viewModel.SecondaryLabel", "viewModel.CanInteract", "UiFoundationDemoCommandIds.Secondary"),
                    new MxFairyGuiButtonBindingSpec("UiFoundationDemoFairyGuiIds.Close", "viewModel.CloseLabel", "viewModel.CanInteract", "UiFoundationDemoCommandIds.Close")
                },
                FocusOrderExpressions = new[]
                {
                    "UiFoundationDemoFairyGuiIds.Primary",
                    "UiFoundationDemoFairyGuiIds.Secondary",
                    "UiFoundationDemoFairyGuiIds.Close"
                },
                DefaultFocusExpression = "UiFoundationDemoFairyGuiIds.Primary"
            };
        }

        private static string UiFoundationDemoGeneratedManifestPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/Demos/FairyGUI/Generated/UiFoundationDemoFairyGuiManifest.g.cs";
        }

        private static string UiFoundationDemoGeneratedBinderPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/Demos/FairyGUI/Generated/UiFoundationDemoFairyGuiBinder.g.cs";
        }

        private static MxFairyGuiManifestGenerationSpec CreateStoryDialogGenerationSpec()
        {
            return new MxFairyGuiManifestGenerationSpec
            {
                NamespaceName = "MxFramework.Demo.FairyGui",
                ClassName = "StoryRuntimeFairyGuiDialogManifest",
                PackageId = "MxStoryDialog",
                PackageName = "MxStoryDialog",
                PackageSourcePath = "FGUIProject/assets/MxStoryDialog/package.xml",
                PackageBytesPath = "Assets/Bundles/FGUI/MxStoryDialog/MxStoryDialog_fui.bytes",
                PackageBytesResourceId = "ui.fairygui.storydialog.fui",
                ComponentName = "StoryDialogPanel",
                ComponentSourcePath = "FGUIProject/assets/MxStoryDialog/StoryDialogPanel.xml",
                ViewId = "ui.story.dialog",
                ViewModelTypeName = "MxFramework.Demo.Story.StoryRuntimeVerticalSliceFairyGuiViewModel",
                PackageIdExpression = "StoryRuntimeFairyGuiDialogIds.PackageId",
                PackageNameExpression = "StoryRuntimeFairyGuiDialogIds.PackageId",
                PackageBytesExpression = "StoryRuntimeFairyGuiDialogIds.PackageBytesKey",
                ViewIdExpression = "StoryRuntimeFairyGuiDialogIds.ViewId",
                ComponentNameExpression = "StoryRuntimeFairyGuiDialogIds.ComponentName",
                ViewModelTypeExpression = "typeof(StoryRuntimeVerticalSliceFairyGuiViewModel).FullName",
                Layer = MxUiLayer.Modal,
                AdditionalUsings = new[] { "MxFramework.Demo.Story" },
                ControlBindPaths = new[]
                {
                    new MxFairyGuiControlBindPathSpec("title", "Title"),
                    new MxFairyGuiControlBindPathSpec("phase", "Phase"),
                    new MxFairyGuiControlBindPathSpec("dialogueText", "DialogueText"),
                    new MxFairyGuiControlBindPathSpec("choiceText", "ChoiceText"),
                    new MxFairyGuiControlBindPathSpec("signalText", "SignalText"),
                    new MxFairyGuiControlBindPathSpec("eventLog", "EventLogText")
                },
                ControlNameExpressions = new[]
                {
                    new MxFairyGuiCodeExpressionSpec("title", "StoryRuntimeFairyGuiDialogIds.Title"),
                    new MxFairyGuiCodeExpressionSpec("phase", "StoryRuntimeFairyGuiDialogIds.Phase"),
                    new MxFairyGuiCodeExpressionSpec("dialogueText", "StoryRuntimeFairyGuiDialogIds.DialogueText"),
                    new MxFairyGuiCodeExpressionSpec("choiceText", "StoryRuntimeFairyGuiDialogIds.ChoiceText"),
                    new MxFairyGuiCodeExpressionSpec("signalText", "StoryRuntimeFairyGuiDialogIds.SignalText"),
                    new MxFairyGuiCodeExpressionSpec("eventLog", "StoryRuntimeFairyGuiDialogIds.EventLog"),
                    new MxFairyGuiCodeExpressionSpec("btnContinue", "StoryRuntimeFairyGuiDialogIds.Continue"),
                    new MxFairyGuiCodeExpressionSpec("btnChoice", "StoryRuntimeFairyGuiDialogIds.Choice")
                },
                LocalizedTexts = new[]
                {
                    new MxFairyGuiLocalizedTextBinding("title", "ui.story.dialog.title", "Story")
                },
                Commands = new[]
                {
                    new MxFairyGuiCommandBinding("story.dialog.continue", "btnContinue"),
                    new MxFairyGuiCommandBinding("story.dialog.selectChoice", "btnChoice")
                },
                CommandIdExpressions = new[]
                {
                    new MxFairyGuiCodeExpressionSpec("story.dialog.continue", "StoryRuntimeVerticalSliceUiCommandIds.CompletePresentation"),
                    new MxFairyGuiCodeExpressionSpec("story.dialog.selectChoice", "StoryRuntimeVerticalSliceUiCommandIds.SelectChoice")
                }
            };
        }

        private static string StoryDialogGeneratedManifestPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/Demos/FairyGUI/Generated/StoryRuntimeFairyGuiDialogManifest.g.cs";
        }

        private static MxFairyGuiManifestGenerationSpec CreateCharacterTestCombatHudGenerationSpec()
        {
            return new MxFairyGuiManifestGenerationSpec
            {
                NamespaceName = "MxFramework.Game.CharacterTest",
                ClassName = "CharacterTestFairyGuiCombatHudManifest",
                PackageId = "MxCharacterTestCombatHud",
                PackageName = "MxCharacterTestCombatHud",
                PackageSourcePath = "FGUIProject/assets/MxCharacterTestCombatHud/package.xml",
                PackageBytesPath = "Assets/Bundles/FGUI/MxCharacterTestCombatHud/MxCharacterTestCombatHud_fui.bytes",
                PackageBytesResourceId = "ui.fairygui.charactertest.combat_hud.fui",
                ComponentName = "CombatHudPanel",
                ComponentSourcePath = "FGUIProject/assets/MxCharacterTestCombatHud/CombatHudPanel.xml",
                ViewId = "ui.character_test.combat.hud",
                ViewModelTypeName = "MxFramework.Game.CharacterTest.CharacterTestFairyGuiCombatHudViewModel",
                PackageIdExpression = "CharacterTestFairyGuiCombatHudIds.PackageId",
                PackageNameExpression = "CharacterTestFairyGuiCombatHudIds.PackageId",
                PackageBytesExpression = "CharacterTestFairyGuiCombatHudIds.PackageBytesKey",
                ViewIdExpression = "CharacterTestFairyGuiCombatHudIds.ViewId",
                ComponentNameExpression = "CharacterTestFairyGuiCombatHudIds.ComponentName",
                ViewModelTypeExpression = "typeof(CharacterTestFairyGuiCombatHudViewModel).FullName",
                Layer = MxUiLayer.Hud,
                ControlBindPaths = CharacterTestCombatHudControlBindPaths(),
                ControlNameExpressions = CharacterTestCombatHudControlNameExpressions(),
                AdditionalPackageResources = new[]
                {
                    new MxFairyGuiPackageResourceGenerationSpec(
                        "ui.fairygui.charactertest.combat_hud.atlas0",
                        ResourceTypeIds.Texture2D,
                        MxFairyGuiManifestResourceKind.Atlas,
                        packageId: "MxCharacterTestCombatHud",
                        resourceKeyExpression: "CharacterTestFairyGuiCombatHudIds.Atlas0Key")
                }
            };
        }

        private static MxFairyGuiBinderGenerationSpec CreateCharacterTestCombatHudBinderGenerationSpec()
        {
            return new MxFairyGuiBinderGenerationSpec
            {
                NamespaceName = "MxFramework.Game.CharacterTest",
                ClassName = "CharacterTestFairyGuiCombatHudBinder",
                ViewModelTypeName = "CharacterTestFairyGuiCombatHudViewModel",
                ViewModelDefaultExpression = "new CharacterTestFairyGuiCombatHudViewModel()",
                ViewIdExpression = "CharacterTestFairyGuiCombatHudIds.ViewId",
                InvalidHandleMessage = "CharacterTest Combat HUD binder requires a concrete FairyGUI component handle.",
                LocalizedTextResolverExpression = "CharacterTestUiText.Resolve({provider}, {key}, {fallback})",
                RequestDefaultFocusInPlayMode = false,
                RootDefaultWidthExpression = "1920f",
                RootDefaultHeightExpression = "1080f",
                TextBindings = new[]
                {
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiCombatHudIds.PlayerName", "viewModel.PlayerName"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiCombatHudIds.PlayerHealthText", "viewModel.PlayerHealth + \"/\" + viewModel.PlayerMaxHealth"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiCombatHudIds.PlayerPostureText", "viewModel.PlayerPosturePressure + \"/\" + viewModel.PlayerMaxPosturePressure"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiCombatHudIds.PlayerActionText", "viewModel.PlayerActionText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiCombatHudIds.TargetName", "viewModel.TargetName"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiCombatHudIds.TargetStatusText", "viewModel.TargetStatusText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiCombatHudIds.TargetHealthText", "viewModel.TargetHealth + \"/\" + viewModel.TargetMaxHealth"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiCombatHudIds.TargetPostureText", "viewModel.TargetPosturePressure + \"/\" + viewModel.TargetMaxPosturePressure"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiCombatHudIds.FeedbackText", "viewModel.FeedbackVisible ? viewModel.FeedbackText : string.Empty"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiCombatHudIds.NetworkLatencyText", "viewModel.NetworkLatencyVisible ? viewModel.NetworkLatencyText : string.Empty"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiCombatHudIds.BehaviorCardIconText", "viewModel.BehaviorCardIconText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiCombatHudIds.BehaviorCardName", "viewModel.BehaviorCardName"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiCombatHudIds.BehaviorCardReason", "viewModel.BehaviorCardReasonText")
                }
            };
        }

        private static MxFairyGuiManifestGenerationSpec CreateCharacterTestSettingsPanelGenerationSpec()
        {
            return new MxFairyGuiManifestGenerationSpec
            {
                NamespaceName = "MxFramework.Game.CharacterTest",
                ClassName = "CharacterTestFairyGuiSettingsPanelManifest",
                PackageId = "MxCharacterTestSettingsUi",
                PackageName = "MxCharacterTestSettingsUi",
                PackageSourcePath = "FGUIProject/assets/MxCharacterTestSettingsUi/package.xml",
                PackageBytesPath = "Assets/Bundles/FGUI/MxCharacterTestSettingsUi/MxCharacterTestSettingsUi_fui.bytes",
                PackageBytesResourceId = "ui.fairygui.charactertest.settings_ui.fui",
                ComponentName = "SettingsPanel",
                ComponentSourcePath = "FGUIProject/assets/MxCharacterTestSettingsUi/SettingsPanel.xml",
                ViewId = "ui.character_test.settings.panel",
                ViewModelTypeName = "MxFramework.Game.CharacterTest.CharacterTestFairyGuiSettingsPanelViewModel",
                PackageIdExpression = "CharacterTestFairyGuiSettingsPanelIds.PackageId",
                PackageNameExpression = "CharacterTestFairyGuiSettingsPanelIds.PackageId",
                PackageBytesExpression = "CharacterTestFairyGuiSettingsPanelIds.PackageBytesKey",
                ViewIdExpression = "CharacterTestFairyGuiSettingsPanelIds.ViewId",
                ComponentNameExpression = "CharacterTestFairyGuiSettingsPanelIds.ComponentName",
                ViewModelTypeExpression = "typeof(CharacterTestFairyGuiSettingsPanelViewModel).FullName",
                Layer = MxUiLayer.Modal,
                ControlBindPaths = CharacterTestSettingsPanelControlBindPaths(),
                ControlNameExpressions = CharacterTestSettingsPanelControlNameExpressions(),
                AdditionalPackageResources = new[]
                {
                    new MxFairyGuiPackageResourceGenerationSpec(
                        "ui.fairygui.charactertest.settings_ui.atlas0",
                        ResourceTypeIds.Texture2D,
                        MxFairyGuiManifestResourceKind.Atlas,
                        packageId: "MxCharacterTestSettingsUi",
                        resourceKeyExpression: "CharacterTestFairyGuiSettingsPanelIds.Atlas0Key")
                },
                LocalizedTexts = new[]
                {
                    new MxFairyGuiLocalizedTextBinding("graphicsHeader", "ui.character_test.settings.graphics_header", "画面与阴影"),
                    new MxFairyGuiLocalizedTextBinding("antiAliasingLabel", "ui.character_test.settings.anti_aliasing", "抗锯齿"),
                    new MxFairyGuiLocalizedTextBinding("shadowDistanceLabel", "ui.character_test.settings.shadow_distance", "阴影距离"),
                    new MxFairyGuiLocalizedTextBinding("mainShadowResolutionLabel", "ui.character_test.settings.directional_shadow_quality", "阴影质量"),
                    new MxFairyGuiLocalizedTextBinding("waterQualityLabel", "ui.character_test.settings.water_quality", "水面反射"),
                    new MxFairyGuiLocalizedTextBinding("rippleQualityLabel", "ui.character_test.settings.ripple_quality", "交互涟漪"),
                    new MxFairyGuiLocalizedTextBinding("shadowCascadeCountLabel", "ui.character_test.settings.shadow_cascades", "阴影级联数"),
                    new MxFairyGuiLocalizedTextBinding("localShadowResolutionLabel", "ui.character_test.settings.local_shadow_resolution", "局部灯阴影图集"),
                    new MxFairyGuiLocalizedTextBinding("hdrLabel", "ui.character_test.settings.hdr.label", "HDR 显示"),
                    new MxFairyGuiLocalizedTextBinding("hdrPaperWhiteLabel", "ui.character_test.settings.hdr.paper_white", "参考白亮度"),
                    new MxFairyGuiLocalizedTextBinding("hdrPeakLabel", "ui.character_test.settings.hdr.peak", "峰值亮度"),
                    new MxFairyGuiLocalizedTextBinding("vSyncLabel", "ui.character_test.settings.vsync", "垂直同步"),
                    new MxFairyGuiLocalizedTextBinding("renderScaleLabel", "ui.character_test.settings.render", "渲染比例"),
                    new MxFairyGuiLocalizedTextBinding("resolutionLabel", "ui.character_test.settings.resolution", "分辨率"),
                    new MxFairyGuiLocalizedTextBinding("windowModeLabel", "ui.character_test.settings.window", "窗口模式"),
                    new MxFairyGuiLocalizedTextBinding("languageLabel", "ui.character_test.settings.language", "语言"),
                    new MxFairyGuiLocalizedTextBinding("audioHeader", "ui.character_test.settings.audio_header", "音频混音"),
                    new MxFairyGuiLocalizedTextBinding("audioCategoryLabel", "ui.character_test.settings.audio_category", "类别"),
                    new MxFairyGuiLocalizedTextBinding("audioVolumeLabel", "ui.character_test.settings.volume", "音量"),
                    new MxFairyGuiLocalizedTextBinding("controlsHeader", "ui.character_test.settings.controls_header", "输入设备")
                },
                Commands = new[]
                {
                    new MxFairyGuiCommandBinding(
                        "character_test.settings.close",
                        "btnClose",
                        new MxUiFeedbackPolicy(MxUiFeedbackKind.Back, MxUiFeedbackKind.None, MxUiFeedbackKind.None)),
                    new MxFairyGuiCommandBinding(
                        "character_test.settings.apply",
                        "btnApply",
                        new MxUiFeedbackPolicy(MxUiFeedbackKind.None, MxUiFeedbackKind.Confirm, MxUiFeedbackKind.Error),
                        payloadType: "MxFramework.Game.CharacterTest.CharacterTestFairyGuiSettingsPanelEditPayload"),
                    new MxFairyGuiCommandBinding(
                        "character_test.settings.cancel",
                        "btnCancel",
                        new MxUiFeedbackPolicy(MxUiFeedbackKind.Back, MxUiFeedbackKind.None, MxUiFeedbackKind.None)),
                    new MxFairyGuiCommandBinding("character_test.settings.page.graphics", "btnNavGraphics"),
                    new MxFairyGuiCommandBinding("character_test.settings.page.audio", "btnNavAudio"),
                    new MxFairyGuiCommandBinding("character_test.settings.page.controls", "btnNavControls"),
                    new MxFairyGuiCommandBinding("character_test.settings.device.keyboard_mouse", "btnKeyboardMouse"),
                    new MxFairyGuiCommandBinding("character_test.settings.device.gamepad", "btnGamepad"),
                    new MxFairyGuiCommandBinding("character_test.settings.anti_aliasing.previous", "btnAntiAliasingPrev"),
                    new MxFairyGuiCommandBinding("character_test.settings.anti_aliasing.next", "btnAntiAliasingNext"),
                    new MxFairyGuiCommandBinding("character_test.settings.directional_shadow_quality.previous", "btnMainShadowResolutionPrev"),
                    new MxFairyGuiCommandBinding("character_test.settings.water_quality.previous", "btnWaterQualityPrev"),
                    new MxFairyGuiCommandBinding("character_test.settings.ripple_quality.previous", "btnRippleQualityPrev"),
                    new MxFairyGuiCommandBinding("character_test.settings.directional_shadow_quality.next", "btnMainShadowResolutionNext"),
                    new MxFairyGuiCommandBinding("character_test.settings.water_quality.next", "btnWaterQualityNext"),
                    new MxFairyGuiCommandBinding("character_test.settings.ripple_quality.next", "btnRippleQualityNext"),
                    new MxFairyGuiCommandBinding("character_test.settings.shadow_cascades.previous", "btnShadowCascadeCountPrev"),
                    new MxFairyGuiCommandBinding("character_test.settings.shadow_cascades.next", "btnShadowCascadeCountNext"),
                    new MxFairyGuiCommandBinding("character_test.settings.local_shadow_resolution.previous", "btnLocalShadowResolutionPrev"),
                    new MxFairyGuiCommandBinding("character_test.settings.local_shadow_resolution.next", "btnLocalShadowResolutionNext"),
                    new MxFairyGuiCommandBinding("character_test.settings.hdr.toggle", "btnHdr"),
                    new MxFairyGuiCommandBinding("character_test.settings.hdr.paper_white.previous", "btnHdrPaperWhitePrev"),
                    new MxFairyGuiCommandBinding("character_test.settings.hdr.paper_white.next", "btnHdrPaperWhiteNext"),
                    new MxFairyGuiCommandBinding("character_test.settings.hdr.peak.previous", "btnHdrPeakPrev"),
                    new MxFairyGuiCommandBinding("character_test.settings.hdr.peak.next", "btnHdrPeakNext"),
                    new MxFairyGuiCommandBinding("character_test.settings.language.previous", "btnLanguagePrev"),
                    new MxFairyGuiCommandBinding("character_test.settings.language.next", "btnLanguageNext"),
                    new MxFairyGuiCommandBinding("character_test.settings.vsync.toggle", "btnVSync"),
                    new MxFairyGuiCommandBinding("character_test.settings.audio.category.previous", "btnAudioCategoryPrev"),
                    new MxFairyGuiCommandBinding("character_test.settings.audio.category.next", "btnAudioCategoryNext"),
                    new MxFairyGuiCommandBinding("character_test.settings.audio.mute.toggle", "btnMute"),
                    new MxFairyGuiCommandBinding(
                        "character_test.settings.input.rebind",
                        "btnInputRebindProxy",
                        new MxUiFeedbackPolicy(MxUiFeedbackKind.None, MxUiFeedbackKind.Click, MxUiFeedbackKind.Error),
                        payloadType: "MxFramework.Game.CharacterTest.CharacterTestFairyGuiSettingsPanelInputPayload"),
                    new MxFairyGuiCommandBinding(
                        "character_test.settings.input.reset",
                        "btnResetInput",
                        new MxUiFeedbackPolicy(MxUiFeedbackKind.None, MxUiFeedbackKind.Confirm, MxUiFeedbackKind.Error))
                },
                CommandIdExpressions = CharacterTestSettingsPanelCommandIdExpressions()
            };
        }

        private static MxFairyGuiBinderGenerationSpec CreateCharacterTestSettingsPanelBinderGenerationSpec()
        {
            return new MxFairyGuiBinderGenerationSpec
            {
                NamespaceName = "MxFramework.Game.CharacterTest",
                ClassName = "CharacterTestFairyGuiSettingsPanelBinder",
                ViewModelTypeName = "CharacterTestFairyGuiSettingsPanelViewModel",
                ViewModelDefaultExpression = "new CharacterTestFairyGuiSettingsPanelViewModel()",
                ViewIdExpression = "CharacterTestFairyGuiSettingsPanelIds.ViewId",
                InvalidHandleMessage = "CharacterTest settings panel binder requires a concrete FairyGUI component handle.",
                LocalizedTextResolverExpression = "CharacterTestUiText.Resolve({provider}, {key}, {fallback})",
                TextBindings = new[]
                {
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.Title", "viewModel.Title"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.Subtitle", "viewModel.Subtitle"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.PageTitle", "viewModel.PageTitle"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.PageDescription", "viewModel.PageDescription"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.Status", "viewModel.StatusText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.DirtyMarker", "viewModel.DirtyText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.InputInstruction", "viewModel.InputInstruction"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.ActionColumnTitle", "viewModel.ActionColumnTitle"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.PrimaryColumnTitle", "viewModel.PrimaryColumnTitle"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.SecondaryColumnTitle", "viewModel.SecondaryColumnTitle"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.GraphicsHeader", "L(\"ui.character_test.settings.graphics_header\", \"画面与阴影\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.AntiAliasingLabel", "L(\"ui.character_test.settings.anti_aliasing\", \"抗锯齿\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.AntiAliasingValue", "viewModel.AntiAliasingText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.ShadowDistanceLabel", "L(\"ui.character_test.settings.shadow_distance\", \"阴影距离\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityLabel", "L(\"ui.character_test.settings.directional_shadow_quality\", \"阴影质量\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.WaterQualityLabel", "L(\"ui.character_test.settings.water_quality\", \"水面反射\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.RippleQualityLabel", "L(\"ui.character_test.settings.ripple_quality\", \"交互涟漪\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityValue", "viewModel.DirectionalShadowQualityText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.WaterQualityValue", "viewModel.WaterQualityText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.RippleQualityValue", "viewModel.RippleQualityText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountLabel", "L(\"ui.character_test.settings.shadow_cascades\", \"阴影级联数\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountValue", "viewModel.ShadowCascadeCountText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionLabel", "L(\"ui.character_test.settings.local_shadow_resolution\", \"局部灯阴影图集\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionValue", "viewModel.LocalShadowResolutionText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.HdrLabel", "L(\"ui.character_test.settings.hdr.label\", \"HDR 显示\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.HdrPaperWhiteLabel", "L(\"ui.character_test.settings.hdr.paper_white\", \"参考白亮度\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.HdrPeakLabel", "L(\"ui.character_test.settings.hdr.peak\", \"峰值亮度\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.HdrValue", "viewModel.HdrModeText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.HdrStatus", "viewModel.HdrStatusText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.HdrPaperWhiteValue", "viewModel.HdrPaperWhiteText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.HdrPeakValue", "viewModel.HdrPeakText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.VSyncLabel", "L(\"ui.character_test.settings.vsync\", \"垂直同步\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.VSyncValue", "viewModel.VSyncText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.RenderScaleLabel", "L(\"ui.character_test.settings.render\", \"渲染比例\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.ResolutionLabel", "L(\"ui.character_test.settings.resolution\", \"分辨率\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.ResolutionValue", "viewModel.ResolutionText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.WindowModeLabel", "L(\"ui.character_test.settings.window\", \"窗口模式\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.WindowModeValue", "viewModel.WindowModeText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.LanguageLabel", "L(\"ui.character_test.settings.language\", \"语言\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.LanguageValue", "viewModel.LocaleText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.AudioHeader", "L(\"ui.character_test.settings.audio_header\", \"音频混音\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.AudioCategoryLabel", "L(\"ui.character_test.settings.audio_category\", \"类别\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.AudioCategoryValue", "viewModel.AudioCategoryText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.AudioVolumeLabel", "L(\"ui.character_test.settings.volume\", \"音量\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.AudioMutedValue", "viewModel.AudioMutedText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiSettingsPanelIds.ControlsHeader", "L(\"ui.character_test.settings.controls_header\", \"输入设备\")")
                },
                InputBindings = new[]
                {
                    new MxFairyGuiInputBindingSpec("CharacterTestFairyGuiSettingsPanelIds.RenderScaleInput", "viewModel.RenderScaleText"),
                    new MxFairyGuiInputBindingSpec("CharacterTestFairyGuiSettingsPanelIds.ShadowDistanceInput", "viewModel.ShadowDistanceText"),
                    new MxFairyGuiInputBindingSpec("CharacterTestFairyGuiSettingsPanelIds.AudioVolumeInput", "viewModel.AudioVolumeText")
                },
                ButtonBindings = new[]
                {
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.Close", "string.Empty", "true", "CharacterTestFairyGuiSettingsPanelCommandIds.Close"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.NavGraphics", "L(\"ui.character_test.settings.nav.graphics\", \"画面\")", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.SelectGraphics", selectedExpression: "viewModel.SelectedPage == CharacterTestSettingsPage.Graphics", checkMode: true),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.NavAudio", "L(\"ui.character_test.settings.nav.audio\", \"音频\")", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.SelectAudio", selectedExpression: "viewModel.SelectedPage == CharacterTestSettingsPage.Audio", checkMode: true),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.NavControls", "L(\"ui.character_test.settings.nav.controls\", \"按键\")", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.SelectControls", selectedExpression: "viewModel.SelectedPage == CharacterTestSettingsPage.Controls", checkMode: true),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.KeyboardMouseTab", "L(\"ui.character_test.settings.device.keyboard_mouse\", \"键盘与鼠标\")", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.SelectKeyboardMouse", selectedExpression: "viewModel.SelectedInputDevice == CharacterTestSettingsInputDevicePage.KeyboardMouse", checkMode: true),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.GamepadTab", "L(\"ui.character_test.settings.device.gamepad\", \"手柄\")", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.SelectGamepad", selectedExpression: "viewModel.SelectedInputDevice == CharacterTestSettingsInputDevicePage.Gamepad", checkMode: true),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.AntiAliasingPrev", "\"<\"", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.AntiAliasingPrevious"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.AntiAliasingNext", "\">\"", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.AntiAliasingNext"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityPrev", "\"<\"", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.DirectionalShadowQualityPrevious"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.WaterQualityPrev", "\"<\"", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.WaterQualityPrevious"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.RippleQualityPrev", "\"<\"", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.RippleQualityPrevious"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityNext", "\">\"", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.DirectionalShadowQualityNext"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.WaterQualityNext", "\">\"", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.WaterQualityNext"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.RippleQualityNext", "\">\"", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.RippleQualityNext"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountPrev", "\"<\"", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.ShadowCascadeCountPrevious"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountNext", "\">\"", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.ShadowCascadeCountNext"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionPrev", "\"<\"", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.LocalShadowResolutionPrevious"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionNext", "\">\"", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.LocalShadowResolutionNext"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.HdrToggle", "L(\"ui.character_test.settings.hdr.toggle_label\", \"切换\")", "viewModel.CanEdit && viewModel.CanToggleHdr", "CharacterTestFairyGuiSettingsPanelCommandIds.ToggleHdr"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.HdrPaperWhitePrev", "\"<\"", "viewModel.CanEdit && viewModel.CanCalibrateHdr", "CharacterTestFairyGuiSettingsPanelCommandIds.HdrPaperWhitePrevious"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.HdrPaperWhiteNext", "\">\"", "viewModel.CanEdit && viewModel.CanCalibrateHdr", "CharacterTestFairyGuiSettingsPanelCommandIds.HdrPaperWhiteNext"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.HdrPeakPrev", "\"<\"", "viewModel.CanEdit && viewModel.CanCalibrateHdr", "CharacterTestFairyGuiSettingsPanelCommandIds.HdrPeakPrevious"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.HdrPeakNext", "\">\"", "viewModel.CanEdit && viewModel.CanCalibrateHdr", "CharacterTestFairyGuiSettingsPanelCommandIds.HdrPeakNext"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.LanguagePrev", "\"<\"", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.LanguagePrevious"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.LanguageNext", "\">\"", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.LanguageNext"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.VSyncToggle", "L(\"ui.character_test.settings.toggle\", \"切换\")", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.ToggleVSync", selectedExpression: "viewModel.VSyncCount > 0", checkMode: true),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.AudioCategoryPrevious", "\"<\"", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.AudioCategoryPrevious"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.AudioCategoryNext", "\">\"", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.AudioCategoryNext"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.AudioMuteToggle", "viewModel.AudioMuted ? L(\"ui.character_test.settings.unmute\", \"取消静音\") : L(\"ui.character_test.settings.mute\", \"静音\")", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.ToggleMute", selectedExpression: "viewModel.AudioMuted", checkMode: true),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.Apply", "L(\"ui.character_test.common.apply\", \"应用\")", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.Apply", "() => CreateEditPayload(root)"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.CancelChanges", "L(\"ui.character_test.common.cancel\", \"取消\")", "true", "CharacterTestFairyGuiSettingsPanelCommandIds.CancelChanges"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiSettingsPanelIds.ResetInput", "L(\"ui.character_test.settings.reset_input\", \"恢复默认\")", "viewModel.CanEdit", "CharacterTestFairyGuiSettingsPanelCommandIds.ResetInput")
                },
                FocusOrderExpressions = new[]
                {
                    "CharacterTestFairyGuiSettingsPanelIds.NavGraphics",
                    "CharacterTestFairyGuiSettingsPanelIds.NavAudio",
                    "CharacterTestFairyGuiSettingsPanelIds.NavControls",
                    "CharacterTestFairyGuiSettingsPanelIds.KeyboardMouseTab",
                    "CharacterTestFairyGuiSettingsPanelIds.GamepadTab",
                    "CharacterTestFairyGuiSettingsPanelIds.AntiAliasingPrev",
                    "CharacterTestFairyGuiSettingsPanelIds.AntiAliasingNext",
                    "CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityPrev",
                    "CharacterTestFairyGuiSettingsPanelIds.WaterQualityPrev",
                    "CharacterTestFairyGuiSettingsPanelIds.RippleQualityPrev",
                    "CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityNext",
                    "CharacterTestFairyGuiSettingsPanelIds.WaterQualityNext",
                    "CharacterTestFairyGuiSettingsPanelIds.RippleQualityNext",
                    "CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountPrev",
                    "CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountNext",
                    "CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionPrev",
                    "CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionNext",
                    "CharacterTestFairyGuiSettingsPanelIds.HdrToggle",
                    "CharacterTestFairyGuiSettingsPanelIds.HdrPaperWhitePrev",
                    "CharacterTestFairyGuiSettingsPanelIds.HdrPaperWhiteNext",
                    "CharacterTestFairyGuiSettingsPanelIds.HdrPeakPrev",
                    "CharacterTestFairyGuiSettingsPanelIds.HdrPeakNext",
                    "CharacterTestFairyGuiSettingsPanelIds.LanguagePrev",
                    "CharacterTestFairyGuiSettingsPanelIds.LanguageNext",
                    "CharacterTestFairyGuiSettingsPanelIds.VSyncToggle",
                    "CharacterTestFairyGuiSettingsPanelIds.AudioCategoryPrevious",
                    "CharacterTestFairyGuiSettingsPanelIds.AudioCategoryNext",
                    "CharacterTestFairyGuiSettingsPanelIds.AudioMuteToggle",
                    "CharacterTestFairyGuiSettingsPanelIds.Close",
                    "CharacterTestFairyGuiSettingsPanelIds.Apply",
                    "CharacterTestFairyGuiSettingsPanelIds.CancelChanges",
                    "CharacterTestFairyGuiSettingsPanelIds.ResetInput"
                },
                DefaultFocusExpression = "CharacterTestFairyGuiSettingsPanelIds.NavControls"
            };
        }

        private static MxFairyGuiManifestGenerationSpec CreateCharacterTestNetworkPanelGenerationSpec()
        {
            return new MxFairyGuiManifestGenerationSpec
            {
                NamespaceName = "MxFramework.Game.CharacterTest",
                ClassName = "CharacterTestFairyGuiNetworkPanelManifest",
                PackageId = "MxCharacterTestNetworkUi",
                PackageName = "MxCharacterTestNetworkUi",
                PackageSourcePath = "FGUIProject/assets/MxCharacterTestNetworkUi/package.xml",
                PackageBytesPath = "Assets/Bundles/FGUI/MxCharacterTestNetworkUi/MxCharacterTestNetworkUi_fui.bytes",
                PackageBytesResourceId = "ui.fairygui.charactertest.network_ui.fui",
                ComponentName = "NetworkPanel",
                ComponentSourcePath = "FGUIProject/assets/MxCharacterTestNetworkUi/NetworkPanel.xml",
                ViewId = "ui.character_test.network.panel",
                ViewModelTypeName = "MxFramework.Game.CharacterTest.CharacterTestFairyGuiNetworkPanelViewModel",
                PackageIdExpression = "CharacterTestFairyGuiNetworkPanelIds.PackageId",
                PackageNameExpression = "CharacterTestFairyGuiNetworkPanelIds.PackageId",
                PackageBytesExpression = "CharacterTestFairyGuiNetworkPanelIds.PackageBytesKey",
                ViewIdExpression = "CharacterTestFairyGuiNetworkPanelIds.ViewId",
                ComponentNameExpression = "CharacterTestFairyGuiNetworkPanelIds.ComponentName",
                ViewModelTypeExpression = "typeof(CharacterTestFairyGuiNetworkPanelViewModel).FullName",
                Layer = MxUiLayer.Modal,
                ControlBindPaths = CharacterTestNetworkPanelControlBindPaths(),
                ControlNameExpressions = CharacterTestNetworkPanelControlNameExpressions(),
                AdditionalPackageResources = new[]
                {
                    new MxFairyGuiPackageResourceGenerationSpec(
                        "ui.fairygui.charactertest.network_ui.atlas0",
                        ResourceTypeIds.Texture2D,
                        MxFairyGuiManifestResourceKind.Atlas,
                        packageId: "MxCharacterTestNetworkUi",
                        resourceKeyExpression: "CharacterTestFairyGuiNetworkPanelIds.Atlas0Key")
                },
                LocalizedTexts = new[]
                {
                    new MxFairyGuiLocalizedTextBinding("connectionHeader", "ui.character_test.network.connection", "Connection"),
                    new MxFairyGuiLocalizedTextBinding("remoteAddressLabel", "ui.character_test.network.address", "Address"),
                    new MxFairyGuiLocalizedTextBinding("portLabel", "ui.character_test.network.port_title", "Port"),
                    new MxFairyGuiLocalizedTextBinding("displayNameLabel", "ui.character_test.network.display_name", "Player"),
                    new MxFairyGuiLocalizedTextBinding("roomNameLabel", "ui.character_test.network.room_name", "Room"),
                    new MxFairyGuiLocalizedTextBinding("replayHeader", "ui.character_test.network.replay_check", "Replay Check"),
                    new MxFairyGuiLocalizedTextBinding("detailsHeader", "ui.character_test.network.details", "Details")
                },
                Commands = new[]
                {
                    new MxFairyGuiCommandBinding("character_test.network.mode.direct_lan", "btnDirectLan"),
                    new MxFairyGuiCommandBinding("character_test.network.mode.room", "btnRoom"),
                    new MxFairyGuiCommandBinding("character_test.network.host", "btnHost", payloadType: "MxFramework.Game.CharacterTest.CharacterTestNetworkSessionOptions"),
                    new MxFairyGuiCommandBinding("character_test.network.join", "btnJoin", payloadType: "MxFramework.Game.CharacterTest.CharacterTestNetworkSessionOptions"),
                    new MxFairyGuiCommandBinding("character_test.network.disconnect", "btnDisconnect"),
                    new MxFairyGuiCommandBinding("character_test.network.ready", "btnReady", payloadType: "System.Boolean"),
                    new MxFairyGuiCommandBinding("character_test.network.enter_game", "btnEnterGame"),
                    new MxFairyGuiCommandBinding("character_test.network.auto_sync_probe", "btnAutoSyncProbe", payloadType: "System.Boolean"),
                    new MxFairyGuiCommandBinding("character_test.network.run_sync_probe", "btnRunSyncProbe"),
                    new MxFairyGuiCommandBinding("character_test.network.close", "btnClose")
                },
                CommandIdExpressions = CharacterTestNetworkPanelCommandIdExpressions()
            };
        }

        private static MxFairyGuiBinderGenerationSpec CreateCharacterTestNetworkPanelBinderGenerationSpec()
        {
            return new MxFairyGuiBinderGenerationSpec
            {
                NamespaceName = "MxFramework.Game.CharacterTest",
                ClassName = "CharacterTestFairyGuiNetworkPanelBinder",
                ViewModelTypeName = "CharacterTestFairyGuiNetworkPanelViewModel",
                ViewModelDefaultExpression = "new CharacterTestFairyGuiNetworkPanelViewModel()",
                ViewIdExpression = "CharacterTestFairyGuiNetworkPanelIds.ViewId",
                InvalidHandleMessage = "CharacterTest network panel binder requires a concrete FairyGUI component handle.",
                LocalizedTextResolverExpression = "CharacterTestUiText.Resolve({provider}, {key}, {fallback})",
                TextBindings = new[]
                {
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.Title", "viewModel.Title"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.Status", "viewModel.StatusText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.Endpoint", "viewModel.EndpointText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.ModeDescription", "viewModel.ModeDescriptionText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.ConnectionHeader", "viewModel.ConnectionHeaderText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.RemoteAddressLabel", "L(\"ui.character_test.network.address\", \"Address\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.PortLabel", "L(\"ui.character_test.network.port_title\", \"Port\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.DisplayNameLabel", "L(\"ui.character_test.network.display_name\", \"Player\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.RoomNameLabel", "L(\"ui.character_test.network.room_name\", \"Room\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.ConnectionSummary", "viewModel.ConnectionSummaryText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.RoomHeader", "viewModel.RoomHeaderText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.RoomSummary", "viewModel.RoomSummaryText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.MemberSummary", "viewModel.MemberSummaryText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.ReadinessSummary", "viewModel.ReadinessSummaryText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.SyncProbeSummary", "viewModel.SyncProbeSummaryText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.ReplayHeader", "L(\"ui.character_test.network.replay_check\", \"Replay Check\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.ReplaySummary", "viewModel.ReplaySummaryText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.DetailsHeader", "viewModel.DetailsHeaderText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.DetailTransport", "viewModel.DetailTransportText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.DetailPeer", "viewModel.DetailPeerText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.DetailRtt", "viewModel.DetailRttText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.DetailFrames", "viewModel.DetailFramesText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.DetailSnapshot", "viewModel.DetailSnapshotText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.DetailDisconnect", "viewModel.DetailDisconnectText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiNetworkPanelIds.Message", "viewModel.Message")
                },
                InputBindings = new[]
                {
                    new MxFairyGuiInputBindingSpec("CharacterTestFairyGuiNetworkPanelIds.RemoteAddressInput", "ResolveRemoteAddress(viewModel.JoinOptions)"),
                    new MxFairyGuiInputBindingSpec("CharacterTestFairyGuiNetworkPanelIds.PortInput", "ResolvePortText(viewModel.JoinOptions, viewModel.HostOptions)"),
                    new MxFairyGuiInputBindingSpec("CharacterTestFairyGuiNetworkPanelIds.DisplayNameInput", "ResolveDisplayName(viewModel.JoinOptions, viewModel.HostOptions)"),
                    new MxFairyGuiInputBindingSpec("CharacterTestFairyGuiNetworkPanelIds.RoomNameInput", "ResolveRoomName(viewModel.JoinOptions, viewModel.HostOptions)")
                },
                ButtonBindings = new[]
                {
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiNetworkPanelIds.DirectLanMode", "L(\"ui.character_test.network.mode.direct_lan\", \"Direct LAN\")", "viewModel.CanSelectLaunchMode", "CharacterTestFairyGuiNetworkPanelCommandIds.DirectLanMode", selectedExpression: "viewModel.SelectedLaunchMode == CharacterTestNetworkLaunchMode.DirectLan", checkMode: true),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiNetworkPanelIds.RoomMode", "L(\"ui.character_test.network.mode.room\", \"LAN Room\")", "viewModel.CanSelectLaunchMode", "CharacterTestFairyGuiNetworkPanelCommandIds.RoomMode", selectedExpression: "viewModel.SelectedLaunchMode == CharacterTestNetworkLaunchMode.Room", checkMode: true),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiNetworkPanelIds.Host", "L(\"ui.character_test.network.host\", \"Host\")", "viewModel.CanHost", "CharacterTestFairyGuiNetworkPanelCommandIds.Host", "() => CreateHostOptions(root, viewModel)"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiNetworkPanelIds.Join", "L(\"ui.character_test.network.join\", \"Join\")", "viewModel.CanJoin", "CharacterTestFairyGuiNetworkPanelCommandIds.Join", "() => CreateJoinOptions(root, viewModel)"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiNetworkPanelIds.Disconnect", "L(\"ui.character_test.network.disconnect\", \"Disconnect\")", "viewModel.CanDisconnect", "CharacterTestFairyGuiNetworkPanelCommandIds.Disconnect"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiNetworkPanelIds.Ready", "viewModel.ReadyButtonText", "viewModel.CanReady", "CharacterTestFairyGuiNetworkPanelCommandIds.Ready", "() => !viewModel.LocalReady"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiNetworkPanelIds.EnterGame", "viewModel.EnterGameButtonText", "viewModel.CanEnterGame", "CharacterTestFairyGuiNetworkPanelCommandIds.EnterGame"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiNetworkPanelIds.AutoSyncProbe", "viewModel.AutoSyncProbeButtonText", "viewModel.CanToggleAutoSyncProbe", "CharacterTestFairyGuiNetworkPanelCommandIds.AutoSyncProbe", "() => !viewModel.AutoSyncProbeAfterEnterGame", selectedExpression: "viewModel.AutoSyncProbeAfterEnterGame", checkMode: true),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiNetworkPanelIds.RunSyncProbe", "viewModel.RunSyncProbeButtonText", "viewModel.CanRunSyncProbe", "CharacterTestFairyGuiNetworkPanelCommandIds.RunSyncProbe"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiNetworkPanelIds.Close", "L(\"ui.character_test.common.back\", \"Back\")", "viewModel.CanClose", "CharacterTestFairyGuiNetworkPanelCommandIds.Close")
                },
                FocusOrderExpressions = new[]
                {
                    "CharacterTestFairyGuiNetworkPanelIds.DirectLanMode",
                    "CharacterTestFairyGuiNetworkPanelIds.RoomMode",
                    "CharacterTestFairyGuiNetworkPanelIds.Host",
                    "CharacterTestFairyGuiNetworkPanelIds.Join",
                    "CharacterTestFairyGuiNetworkPanelIds.Disconnect",
                    "CharacterTestFairyGuiNetworkPanelIds.Ready",
                    "CharacterTestFairyGuiNetworkPanelIds.EnterGame",
                    "CharacterTestFairyGuiNetworkPanelIds.AutoSyncProbe",
                    "CharacterTestFairyGuiNetworkPanelIds.RunSyncProbe",
                    "CharacterTestFairyGuiNetworkPanelIds.Close"
                },
                DefaultFocusExpression = "CharacterTestFairyGuiNetworkPanelIds.DirectLanMode"
            };
        }

        private static MxFairyGuiManifestGenerationSpec CreateCharacterTestMainMenuGenerationSpec()
        {
            return new MxFairyGuiManifestGenerationSpec
            {
                NamespaceName = "MxFramework.Game.CharacterTest",
                ClassName = "CharacterTestFairyGuiMainMenuManifest",
                PackageId = "MxCharacterTestUi",
                PackageName = "MxCharacterTestUi",
                PackageSourcePath = "FGUIProject/assets/MxCharacterTestUi/package.xml",
                PackageBytesPath = "Assets/Bundles/FGUI/MxCharacterTestUi/MxCharacterTestUi_fui.bytes",
                PackageBytesResourceId = "ui.fairygui.charactertest.player_ui.fui",
                ComponentName = "MainMenuPanel",
                ComponentSourcePath = "FGUIProject/assets/MxCharacterTestUi/MainMenuPanel.xml",
                ViewId = "ui.character_test.main.menu",
                ViewModelTypeName = "MxFramework.Game.CharacterTest.CharacterTestFairyGuiMainMenuViewModel",
                PackageIdExpression = "CharacterTestFairyGuiMainMenuIds.PackageId",
                PackageNameExpression = "CharacterTestFairyGuiMainMenuIds.PackageId",
                PackageBytesExpression = "CharacterTestFairyGuiMainMenuIds.PackageBytesKey",
                ViewIdExpression = "CharacterTestFairyGuiMainMenuIds.ViewId",
                ComponentNameExpression = "CharacterTestFairyGuiMainMenuIds.ComponentName",
                ViewModelTypeExpression = "typeof(CharacterTestFairyGuiMainMenuViewModel).FullName",
                Layer = MxUiLayer.Modal,
                ControlBindPaths = new[]
                {
                    new MxFairyGuiControlBindPathSpec("title", "Title"),
                    new MxFairyGuiControlBindPathSpec("status", "StatusText"),
                    new MxFairyGuiControlBindPathSpec("message", "Message")
                },
                ControlNameExpressions = CharacterTestMainMenuControlNameExpressions(),
                AdditionalPackageResources = new[]
                {
                    new MxFairyGuiPackageResourceGenerationSpec(
                        "ui.fairygui.charactertest.player_ui.atlas0",
                        ResourceTypeIds.Texture2D,
                        MxFairyGuiManifestResourceKind.Atlas,
                        packageId: "MxCharacterTestUi",
                        resourceKeyExpression: "CharacterTestFairyGuiMainMenuIds.Atlas0Key")
                },
                Commands = new[]
                {
                    new MxFairyGuiCommandBinding("character_test.main_menu.start", "btnStart"),
                    new MxFairyGuiCommandBinding("character_test.main_menu.network", "btnNetwork"),
                    new MxFairyGuiCommandBinding("character_test.main_menu.abilities", "btnAbilities"),
                    new MxFairyGuiCommandBinding("character_test.main_menu.behavior_deck", "btnBehaviorDeck"),
                    new MxFairyGuiCommandBinding("character_test.main_menu.settings", "btnSettings"),
                    new MxFairyGuiCommandBinding("character_test.main_menu.restart_offline", "btnRestart"),
                    new MxFairyGuiCommandBinding("character_test.main_menu.close", "btnClose")
                },
                CommandIdExpressions = CharacterTestMainMenuCommandIdExpressions()
            };
        }

        private static MxFairyGuiBinderGenerationSpec CreateCharacterTestMainMenuBinderGenerationSpec()
        {
            return new MxFairyGuiBinderGenerationSpec
            {
                NamespaceName = "MxFramework.Game.CharacterTest",
                ClassName = "CharacterTestFairyGuiMainMenuBinder",
                ViewModelTypeName = "CharacterTestFairyGuiMainMenuViewModel",
                ViewModelDefaultExpression = "new CharacterTestFairyGuiMainMenuViewModel()",
                ViewIdExpression = "CharacterTestFairyGuiMainMenuIds.ViewId",
                InvalidHandleMessage = "CharacterTest main menu binder requires a concrete FairyGUI component handle.",
                LocalizedTextResolverExpression = "CharacterTestUiText.Resolve({provider}, {key}, {fallback})",
                RootDefaultWidthExpression = "1920f",
                RootDefaultHeightExpression = "1080f",
                TextBindings = new[]
                {
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiMainMenuIds.Title", "viewModel.Title"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiMainMenuIds.Status", "viewModel.StatusText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiMainMenuIds.Message", "viewModel.Message")
                },
                ButtonBindings = new[]
                {
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiMainMenuIds.Start", "viewModel.StartLabel", "viewModel.CanStart", "CharacterTestFairyGuiMainMenuCommandIds.Start"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiMainMenuIds.Network", "L(\"ui.character_test.main.network\", \"Network\")", "viewModel.CanNetwork", "CharacterTestFairyGuiMainMenuCommandIds.Network"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiMainMenuIds.Abilities", "L(\"ui.character_test.main.abilities\", \"Abilities\")", "viewModel.CanAbilities", "CharacterTestFairyGuiMainMenuCommandIds.AbilitiesToggle"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiMainMenuIds.BehaviorDeck", "L(\"ui.character_test.main.behavior_deck\", \"Behavior Deck\")", "viewModel.CanBehaviorDeck", "CharacterTestFairyGuiMainMenuCommandIds.BehaviorDeckToggle"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiMainMenuIds.Settings", "L(\"ui.character_test.main.settings\", \"Settings\")", "viewModel.CanSettings", "CharacterTestFairyGuiMainMenuCommandIds.SettingsToggle"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiMainMenuIds.Restart", "L(\"ui.character_test.main.restart_offline\", \"Reset Local\")", "viewModel.CanRestart", "CharacterTestFairyGuiMainMenuCommandIds.RestartOffline"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiMainMenuIds.Close", "L(\"ui.character_test.common.close\", \"Close\")", "viewModel.CanClose", "CharacterTestFairyGuiMainMenuCommandIds.Close")
                },
                FocusOrderExpressions = new[]
                {
                    "CharacterTestFairyGuiMainMenuIds.Start",
                    "CharacterTestFairyGuiMainMenuIds.Network",
                    "CharacterTestFairyGuiMainMenuIds.Abilities",
                    "CharacterTestFairyGuiMainMenuIds.BehaviorDeck",
                    "CharacterTestFairyGuiMainMenuIds.Settings",
                    "CharacterTestFairyGuiMainMenuIds.Restart",
                    "CharacterTestFairyGuiMainMenuIds.Close"
                },
                DefaultFocusExpression = "CharacterTestFairyGuiMainMenuIds.Start"
            };
        }

        private static MxFairyGuiManifestGenerationSpec CreateCharacterTestAbilityBindingPanelGenerationSpec()
        {
            return new MxFairyGuiManifestGenerationSpec
            {
                NamespaceName = "MxFramework.Game.CharacterTest",
                ClassName = "CharacterTestFairyGuiAbilityBindingPanelManifest",
                PackageId = "MxCharacterTestAbilityUi",
                PackageName = "MxCharacterTestAbilityUi",
                PackageSourcePath = "FGUIProject/assets/MxCharacterTestAbilityUi/package.xml",
                PackageBytesPath = "Assets/Bundles/FGUI/MxCharacterTestAbilityUi/MxCharacterTestAbilityUi_fui.bytes",
                PackageBytesResourceId = "ui.fairygui.charactertest.ability_ui.fui",
                ComponentName = "AbilityBindingPanel",
                ComponentSourcePath = "FGUIProject/assets/MxCharacterTestAbilityUi/AbilityBindingPanel.xml",
                ViewId = "ui.character_test.ability.bindings",
                ViewModelTypeName = "MxFramework.Game.CharacterTest.CharacterTestFairyGuiAbilityBindingPanelViewModel",
                PackageIdExpression = "CharacterTestFairyGuiAbilityBindingPanelIds.PackageId",
                PackageNameExpression = "CharacterTestFairyGuiAbilityBindingPanelIds.PackageId",
                PackageBytesExpression = "CharacterTestFairyGuiAbilityBindingPanelIds.PackageBytesKey",
                ViewIdExpression = "CharacterTestFairyGuiAbilityBindingPanelIds.ViewId",
                ComponentNameExpression = "CharacterTestFairyGuiAbilityBindingPanelIds.ComponentName",
                ViewModelTypeExpression = "typeof(CharacterTestFairyGuiAbilityBindingPanelViewModel).FullName",
                Layer = MxUiLayer.Modal,
                ControlBindPaths = new[]
                {
                    new MxFairyGuiControlBindPathSpec("title", "Title"),
                    new MxFairyGuiControlBindPathSpec("status", "StatusText"),
                    new MxFairyGuiControlBindPathSpec("message", "Message"),
                    new MxFairyGuiControlBindPathSpec("selectedSlotLabel", "SelectedSlotText")
                },
                ControlNameExpressions = CharacterTestAbilityBindingPanelControlNameExpressions(),
                AdditionalPackageResources = new[]
                {
                    new MxFairyGuiPackageResourceGenerationSpec(
                        "ui.fairygui.charactertest.ability_ui.atlas0",
                        ResourceTypeIds.Texture2D,
                        MxFairyGuiManifestResourceKind.Atlas,
                        packageId: "MxCharacterTestAbilityUi",
                        resourceKeyExpression: "CharacterTestFairyGuiAbilityBindingPanelIds.Atlas0Key")
                },
                LocalizedTexts = new[]
                {
                    new MxFairyGuiLocalizedTextBinding("slotsHeader", "ui.character_test.abilities.hotbar", "HOTBAR"),
                    new MxFairyGuiLocalizedTextBinding("abilitiesHeader", "ui.character_test.abilities.abilities", "ABILITIES"),
                    new MxFairyGuiLocalizedTextBinding("actionsHeader", "ui.character_test.abilities.actions", "ACTIONS")
                },
                Commands = new[]
                {
                    new MxFairyGuiCommandBinding("character_test.abilities.select_slot", "btnSlot1", payloadType: "MxFramework.Game.CharacterTest.CharacterTestFairyGuiAbilityBindingPanelPayload"),
                    new MxFairyGuiCommandBinding("character_test.abilities.select_ability", "btnAbility0", payloadType: "MxFramework.Game.CharacterTest.CharacterTestFairyGuiAbilityBindingPanelPayload"),
                    new MxFairyGuiCommandBinding("character_test.abilities.clear_slot", "btnClear"),
                    new MxFairyGuiCommandBinding("character_test.abilities.reset_defaults", "btnReset"),
                    new MxFairyGuiCommandBinding("character_test.abilities.save", "btnSave"),
                    new MxFairyGuiCommandBinding("character_test.abilities.cancel", "btnCancel")
                },
                CommandIdExpressions = CharacterTestAbilityBindingPanelCommandIdExpressions()
            };
        }

        private static MxFairyGuiBinderGenerationSpec CreateCharacterTestAbilityBindingPanelBinderGenerationSpec()
        {
            return new MxFairyGuiBinderGenerationSpec
            {
                NamespaceName = "MxFramework.Game.CharacterTest",
                ClassName = "CharacterTestFairyGuiAbilityBindingPanelBinder",
                ViewModelTypeName = "CharacterTestFairyGuiAbilityBindingPanelViewModel",
                ViewModelDefaultExpression = "new CharacterTestFairyGuiAbilityBindingPanelViewModel()",
                ViewIdExpression = "CharacterTestFairyGuiAbilityBindingPanelIds.ViewId",
                InvalidHandleMessage = "CharacterTest ability binding binder requires a concrete FairyGUI component handle.",
                LocalizedTextResolverExpression = "CharacterTestUiText.Resolve({provider}, {key}, {fallback})",
                TextBindings = new[]
                {
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiAbilityBindingPanelIds.Title", "viewModel.Title"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiAbilityBindingPanelIds.Status", "viewModel.StatusText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiAbilityBindingPanelIds.Message", "viewModel.Message"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiAbilityBindingPanelIds.SelectedSlotLabel", "viewModel.SelectedSlotText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiAbilityBindingPanelIds.SlotsHeader", "L(\"ui.character_test.abilities.hotbar\", \"HOTBAR\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiAbilityBindingPanelIds.AbilitiesHeader", "L(\"ui.character_test.abilities.abilities\", \"ABILITIES\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiAbilityBindingPanelIds.ActionsHeader", "L(\"ui.character_test.abilities.actions\", \"ACTIONS\")")
                },
                ButtonBindings = new[]
                {
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiAbilityBindingPanelIds.Clear", "L(\"ui.character_test.abilities.clear\", \"Clear\")", "viewModel.CanEdit", "CharacterTestFairyGuiAbilityBindingPanelCommandIds.ClearSlot"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiAbilityBindingPanelIds.ResetDefaults", "L(\"ui.character_test.abilities.reset\", \"Reset Defaults\")", "viewModel.CanEdit", "CharacterTestFairyGuiAbilityBindingPanelCommandIds.ResetDefaults"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiAbilityBindingPanelIds.Save", "L(\"ui.character_test.common.save\", \"Save\")", "viewModel.CanEdit", "CharacterTestFairyGuiAbilityBindingPanelCommandIds.Save"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiAbilityBindingPanelIds.Cancel", "L(\"ui.character_test.common.cancel\", \"Cancel\")", "true", "CharacterTestFairyGuiAbilityBindingPanelCommandIds.Cancel")
                }
            };
        }

        private static MxFairyGuiManifestGenerationSpec CreateCharacterTestBehaviorDeckPanelGenerationSpec()
        {
            return new MxFairyGuiManifestGenerationSpec
            {
                NamespaceName = "MxFramework.Game.CharacterTest",
                ClassName = "CharacterTestFairyGuiBehaviorDeckPanelManifest",
                PackageId = "MxCharacterTestBehaviorDeckUi",
                PackageName = "MxCharacterTestBehaviorDeckUi",
                PackageSourcePath = "FGUIProject/assets/MxCharacterTestBehaviorDeckUi/package.xml",
                PackageBytesPath = "Assets/Bundles/FGUI/MxCharacterTestBehaviorDeckUi/MxCharacterTestBehaviorDeckUi_fui.bytes",
                PackageBytesResourceId = "ui.fairygui.charactertest.behavior_deck_ui.fui",
                ComponentName = "BehaviorDeckPanel",
                ComponentSourcePath = "FGUIProject/assets/MxCharacterTestBehaviorDeckUi/BehaviorDeckPanel.xml",
                ViewId = "ui.character_test.runtime_ai_planner.behavior_deck",
                ViewModelTypeName = "MxFramework.Game.CharacterTest.CharacterTestFairyGuiBehaviorDeckPanelViewModel",
                PackageIdExpression = "CharacterTestFairyGuiBehaviorDeckPanelIds.PackageId",
                PackageNameExpression = "CharacterTestFairyGuiBehaviorDeckPanelIds.PackageId",
                PackageBytesExpression = "CharacterTestFairyGuiBehaviorDeckPanelIds.PackageBytesKey",
                ViewIdExpression = "CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId",
                ComponentNameExpression = "CharacterTestFairyGuiBehaviorDeckPanelIds.ComponentName",
                ViewModelTypeExpression = "typeof(CharacterTestFairyGuiBehaviorDeckPanelViewModel).FullName",
                Layer = MxUiLayer.Modal,
                ControlBindPaths = new[]
                {
                    new MxFairyGuiControlBindPathSpec("title", "Title"),
                    new MxFairyGuiControlBindPathSpec("character", "CharacterText"),
                    new MxFairyGuiControlBindPathSpec("budget", "BudgetText"),
                    new MxFairyGuiControlBindPathSpec("costBudget", "CostBudgetText"),
                    new MxFairyGuiControlBindPathSpec("lockStatus", "LockStatusText"),
                    new MxFairyGuiControlBindPathSpec("librarySummary", "LibrarySummaryText"),
                    new MxFairyGuiControlBindPathSpec("pageStatus", "PageStatusText"),
                    new MxFairyGuiControlBindPathSpec("deckSummary", "DeckSummaryText"),
                    new MxFairyGuiControlBindPathSpec("diagnostics", "DiagnosticsText"),
                    new MxFairyGuiControlBindPathSpec("commandStatus", "CommandStatusText"),
                    new MxFairyGuiControlBindPathSpec("detailsTitle", "DetailsTitleText"),
                    new MxFairyGuiControlBindPathSpec("detailsBody", "DetailsBodyText"),
                    new MxFairyGuiControlBindPathSpec("detailsPageStatus", "DetailsPageStatusText"),
                    new MxFairyGuiControlBindPathSpec("presetTitle", "PresetTitleText"),
                    new MxFairyGuiControlBindPathSpec("presetSummary", "PresetSummaryText"),
                    new MxFairyGuiControlBindPathSpec("presetSourceBudget", "PresetSourceBudgetText"),
                    new MxFairyGuiControlBindPathSpec("presetCandidateBudget", "PresetCandidateBudgetText"),
                    new MxFairyGuiControlBindPathSpec("presetCostDelta", "PresetCostDeltaText"),
                    new MxFairyGuiControlBindPathSpec("presetDiagnostics", "PresetDiagnosticsText")
                },
                ControlNameExpressions = CharacterTestBehaviorDeckPanelControlNameExpressions(),
                AdditionalPackageResources = new[]
                {
                    new MxFairyGuiPackageResourceGenerationSpec(
                        "ui.fairygui.charactertest.behavior_deck_ui.atlas0",
                        ResourceTypeIds.Texture2D,
                        MxFairyGuiManifestResourceKind.Atlas,
                        packageId: "MxCharacterTestBehaviorDeckUi",
                        resourceKeyExpression: "CharacterTestFairyGuiBehaviorDeckPanelIds.Atlas0Key")
                },
                Commands = new[]
                {
                    new MxFairyGuiCommandBinding("character_test.runtime_ai_planner.behavior_deck.close", "btnClose"),
                    new MxFairyGuiCommandBinding("character_test.runtime_ai_planner.behavior_deck.previous_page", "btnPreviousPage"),
                    new MxFairyGuiCommandBinding("character_test.runtime_ai_planner.behavior_deck.next_page", "btnNextPage"),
                    new MxFairyGuiCommandBinding("character_test.runtime_ai_planner.behavior_deck.previous_character", "btnPreviousCharacter"),
                    new MxFairyGuiCommandBinding("character_test.runtime_ai_planner.behavior_deck.next_character", "btnNextCharacter"),
                    new MxFairyGuiCommandBinding("character_test.runtime_ai_planner.behavior_deck.toggle_install_selection", "btnAddCard"),
                    new MxFairyGuiCommandBinding("character_test.runtime_ai_planner.behavior_deck.preview_recommended_preset", "btnPreviewRecommendedPreset"),
                    new MxFairyGuiCommandBinding("character_test.runtime_ai_planner.behavior_deck.undo_recommended_preset", "btnUndoRecommendedPreset"),
                    new MxFairyGuiCommandBinding("character_test.runtime_ai_planner.behavior_deck.continue_to_setup", "btnContinueToSetup"),
                    new MxFairyGuiCommandBinding("character_test.runtime_ai_planner.behavior_deck.close_details", "btnCloseDetails"),
                    new MxFairyGuiCommandBinding("character_test.runtime_ai_planner.behavior_deck.previous_details_page", "btnPreviousDetailsPage"),
                    new MxFairyGuiCommandBinding("character_test.runtime_ai_planner.behavior_deck.next_details_page", "btnNextDetailsPage"),
                    new MxFairyGuiCommandBinding("character_test.runtime_ai_planner.behavior_deck.cancel_recommended_preset", "btnCancelRecommendedPreset"),
                    new MxFairyGuiCommandBinding("character_test.runtime_ai_planner.behavior_deck.confirm_recommended_preset", "btnConfirmRecommendedPreset")
                },
                CommandIdExpressions = CharacterTestBehaviorDeckPanelCommandIdExpressions()
            };
        }

        private static MxFairyGuiBinderGenerationSpec CreateCharacterTestBehaviorDeckPanelBinderGenerationSpec()
        {
            return new MxFairyGuiBinderGenerationSpec
            {
                NamespaceName = "MxFramework.Game.CharacterTest",
                ClassName = "CharacterTestFairyGuiBehaviorDeckPanelBinder",
                ViewModelTypeName = "CharacterTestFairyGuiBehaviorDeckPanelViewModel",
                ViewModelDefaultExpression = "new CharacterTestFairyGuiBehaviorDeckPanelViewModel()",
                ViewIdExpression = "CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId",
                InvalidHandleMessage = "CharacterTest behavior deck binder requires a concrete FairyGUI component handle.",
                LocalizedTextResolverExpression = "CharacterTestUiText.Resolve({provider}, {key}, {fallback})",
                RootDefaultWidthExpression = "1920f",
                RootDefaultHeightExpression = "1080f",
                RequestDefaultFocusInPlayMode = false,
                TextBindings = new[]
                {
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.Title", "viewModel.Title"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.Character", "viewModel.CharacterText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.Budget", "viewModel.BudgetText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.CostBudget", "viewModel.CostBudgetText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.LockStatus", "viewModel.LockStatusText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryHeader", "L(\"ui.character_test.behavior_deck.library\", \"行为卡牌库\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.LibrarySummary", "viewModel.LibrarySummaryText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.PageStatus", "viewModel.PageStatusText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.DeckHeader", "L(\"ui.character_test.behavior_deck.installed\", \"已安装卡组\")"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.DeckSummary", "viewModel.DeckSummaryText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.Diagnostics", "viewModel.DiagnosticsText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.CommandStatus", "viewModel.CommandStatusText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsTitle", "viewModel.DetailsTitleText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsBody", "viewModel.DetailsBodyText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsPageStatus", "viewModel.DetailsPageStatusText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.PresetTitle", "viewModel.PresetTitleText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.PresetSummary", "viewModel.PresetSummaryText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.PresetSourceBudget", "viewModel.PresetSourceBudgetText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.PresetCandidateBudget", "viewModel.PresetCandidateBudgetText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.PresetCostDelta", "viewModel.PresetCostDeltaText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.PresetDiagnostics", "viewModel.PresetDiagnosticsText")
                },
                ButtonBindings = new[]
                {
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.Close", "L(\"ui.character_test.behavior_deck.hide\", \"隐藏\")", "true", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.Close"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousPage", "\"<\"", "viewModel.CanPreviousPage", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviousPage"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.NextPage", "\">\"", "viewModel.CanNextPage", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.NextPage"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousCharacter", "\"<\"", "viewModel.CanPreviousCharacter", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviousCharacter"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.NextCharacter", "\">\"", "viewModel.CanNextCharacter", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.NextCharacter"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.AddCard", "L(\"ui.character_test.behavior_deck.add_card\", \"添加卡牌\")", "viewModel.ShowAddCard", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ToggleInstallSelection"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.PreviewRecommendedPreset", "L(\"ui.character_test.behavior_deck.preview_recommended_preset\", \"预览推荐预设\")", "viewModel.CanPreviewRecommendedPreset", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviewRecommendedPreset"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.UndoRecommendedPreset", "L(\"ui.character_test.behavior_deck.undo_recommended_preset\", \"撤销预设\")", "viewModel.CanUndoRecommendedPreset", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.UndoRecommendedPreset"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.ContinueToSetup", "L(\"ui.character_test.behavior_deck.continue_to_setup\", \"继续到实验设置\")", "viewModel.CanContinueToSetup", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ContinueToSetup"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.CloseDetails", "L(\"ui.character_test.behavior_deck.close_details\", \"返回卡组\")", "true", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CloseDetails"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousDetailsPage", "L(\"ui.character_test.behavior_deck.previous_details_page\", \"上一页\")", "viewModel.CanPreviousDetailsPage", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviousDetailsPage"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.NextDetailsPage", "L(\"ui.character_test.behavior_deck.next_details_page\", \"下一页\")", "viewModel.CanNextDetailsPage", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.NextDetailsPage"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.CancelRecommendedPreset", "L(\"ui.character_test.behavior_deck.cancel_recommended_preset\", \"取消\")", "true", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CancelRecommendedPreset"),
                    new MxFairyGuiButtonBindingSpec("CharacterTestFairyGuiBehaviorDeckPanelIds.ConfirmRecommendedPreset", "L(\"ui.character_test.behavior_deck.confirm_recommended_preset\", \"确认应用\")", "viewModel.CanConfirmRecommendedPreset", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ConfirmRecommendedPreset")
                },
                FocusOrderExpressions = new[]
                {
                    "CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousPage",
                    "CharacterTestFairyGuiBehaviorDeckPanelIds.NextPage",
                    "CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousCharacter",
                    "CharacterTestFairyGuiBehaviorDeckPanelIds.NextCharacter",
                    "CharacterTestFairyGuiBehaviorDeckPanelIds.AddCard",
                    "CharacterTestFairyGuiBehaviorDeckPanelIds.PreviewRecommendedPreset",
                    "CharacterTestFairyGuiBehaviorDeckPanelIds.UndoRecommendedPreset",
                    "CharacterTestFairyGuiBehaviorDeckPanelIds.ContinueToSetup",
                    "CharacterTestFairyGuiBehaviorDeckPanelIds.CloseDetails",
                    "CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousDetailsPage",
                    "CharacterTestFairyGuiBehaviorDeckPanelIds.NextDetailsPage",
                    "CharacterTestFairyGuiBehaviorDeckPanelIds.CancelRecommendedPreset",
                    "CharacterTestFairyGuiBehaviorDeckPanelIds.ConfirmRecommendedPreset",
                    "CharacterTestFairyGuiBehaviorDeckPanelIds.Close"
                },
                DefaultFocusExpression = "CharacterTestFairyGuiBehaviorDeckPanelIds.Close"
            };
        }

        private static MxFairyGuiManifestGenerationSpec CreateCharacterTestTacticalDeckPanelGenerationSpec()
        {
            return new MxFairyGuiManifestGenerationSpec
            {
                NamespaceName = "MxFramework.Game.CharacterTest",
                ClassName = "CharacterTestFairyGuiTacticalDeckPanelManifest",
                PackageId = "MxCharacterTestTacticalDeckUi",
                PackageName = "MxCharacterTestTacticalDeckUi",
                PackageSourcePath = "FGUIProject/assets/MxCharacterTestTacticalDeckUi/package.xml",
                PackageBytesPath = "Assets/Bundles/FGUI/MxCharacterTestTacticalDeckUi/MxCharacterTestTacticalDeckUi_fui.bytes",
                PackageBytesResourceId = "ui.fairygui.charactertest.tactical_deck_ui.fui",
                ComponentName = "TacticalDeckPanel",
                ComponentSourcePath = "FGUIProject/assets/MxCharacterTestTacticalDeckUi/TacticalDeckPanel.xml",
                ViewId = "ui.character_test.tactical_deck",
                ViewModelTypeName = "MxFramework.Game.CharacterTest.CharacterTestFairyGuiTacticalDeckPanelViewModel",
                PackageIdExpression = "CharacterTestFairyGuiTacticalDeckPanelIds.PackageId",
                PackageNameExpression = "CharacterTestFairyGuiTacticalDeckPanelIds.PackageId",
                PackageBytesExpression = "CharacterTestFairyGuiTacticalDeckPanelIds.PackageBytesKey",
                ViewIdExpression = "CharacterTestFairyGuiTacticalDeckPanelIds.ViewId",
                ComponentNameExpression = "CharacterTestFairyGuiTacticalDeckPanelIds.ComponentName",
                ViewModelTypeExpression = "typeof(CharacterTestFairyGuiTacticalDeckPanelViewModel).FullName",
                Layer = MxUiLayer.Modal,
                ControlBindPaths = Array.Empty<MxFairyGuiControlBindPathSpec>(),
                AdditionalPackageResources = new[]
                {
                    new MxFairyGuiPackageResourceGenerationSpec(
                        "ui.fairygui.charactertest.tactical_deck_ui.atlas0",
                        ResourceTypeIds.Texture2D,
                        MxFairyGuiManifestResourceKind.Atlas,
                        packageId: "MxCharacterTestTacticalDeckUi",
                        resourceKeyExpression: "CharacterTestFairyGuiTacticalDeckPanelIds.Atlas0Key"),
                    new MxFairyGuiPackageResourceGenerationSpec(
                        "ui.fairygui.charactertest.tactical_deck_ui.atlas0_1",
                        ResourceTypeIds.Texture2D,
                        MxFairyGuiManifestResourceKind.Atlas,
                        packageId: "MxCharacterTestTacticalDeckUi",
                        resourceKeyExpression: "CharacterTestFairyGuiTacticalDeckPanelIds.Atlas0Page1Key"),
                    new MxFairyGuiPackageResourceGenerationSpec(
                        "ui.fairygui.charactertest.tactical_deck_ui.atlas0_2",
                        ResourceTypeIds.Texture2D, MxFairyGuiManifestResourceKind.Atlas,
                        packageId: "MxCharacterTestTacticalDeckUi",
                        resourceKeyExpression: "CharacterTestFairyGuiTacticalDeckPanelIds.Atlas0Page2Key"),
                    new MxFairyGuiPackageResourceGenerationSpec(
                        "ui.fairygui.charactertest.tactical_deck_ui.atlas0_3",
                        ResourceTypeIds.Texture2D, MxFairyGuiManifestResourceKind.Atlas,
                        packageId: "MxCharacterTestTacticalDeckUi",
                        resourceKeyExpression: "CharacterTestFairyGuiTacticalDeckPanelIds.Atlas0Page3Key")
                },
                Commands = Array.Empty<MxFairyGuiCommandBinding>()
            };
        }

        private static MxFairyGuiManifestGenerationSpec CreateCharacterTestStoryDialogGenerationSpec()
        {
            return new MxFairyGuiManifestGenerationSpec
            {
                NamespaceName = "MxFramework.Game.CharacterTest",
                ClassName = "CharacterTestFairyGuiStoryDialogManifest",
                PackageId = "MxStoryDialog",
                PackageName = "MxStoryDialog",
                PackageSourcePath = "FGUIProject/assets/MxStoryDialog/package.xml",
                PackageBytesPath = "Assets/Bundles/FGUI/MxStoryDialog/MxStoryDialog_fui.bytes",
                PackageBytesResourceId = "ui.fairygui.storydialog.fui",
                ComponentName = "StoryDialogPanel",
                ComponentSourcePath = "FGUIProject/assets/MxStoryDialog/StoryDialogPanel.xml",
                ViewId = "ui.character_test.story.dialog",
                ViewModelTypeName = "MxFramework.Game.CharacterTest.CharacterTestFairyGuiStoryDialogViewModel",
                PackageIdExpression = "CharacterTestFairyGuiStoryDialogIds.PackageId",
                PackageNameExpression = "CharacterTestFairyGuiStoryDialogIds.PackageId",
                PackageBytesExpression = "CharacterTestFairyGuiStoryDialogIds.PackageBytesKey",
                ViewIdExpression = "CharacterTestFairyGuiStoryDialogIds.ViewId",
                ComponentNameExpression = "CharacterTestFairyGuiStoryDialogIds.ComponentName",
                ViewModelTypeExpression = "typeof(CharacterTestFairyGuiStoryDialogViewModel).FullName",
                Layer = MxUiLayer.Modal,
                ControlBindPaths = new[]
                {
                    new MxFairyGuiControlBindPathSpec("title", "Title"),
                    new MxFairyGuiControlBindPathSpec("phase", "Phase"),
                    new MxFairyGuiControlBindPathSpec("dialogueText", "DialogueText"),
                    new MxFairyGuiControlBindPathSpec("choiceText", "ChoiceText"),
                    new MxFairyGuiControlBindPathSpec("signalText", "SignalText"),
                    new MxFairyGuiControlBindPathSpec("eventLog", "EventLogText")
                },
                ControlNameExpressions = CharacterTestStoryDialogControlNameExpressions(),
                LocalizedTexts = new[]
                {
                    new MxFairyGuiLocalizedTextBinding("title", "ui.character_test.story.title", "Character Story")
                },
                Commands = new[]
                {
                    new MxFairyGuiCommandBinding("character_test.story.dialog.continue", "btnContinue", payloadType: "MxFramework.Game.CharacterTest.CharacterTestFairyGuiStoryCompletePresentationPayload"),
                    new MxFairyGuiCommandBinding("character_test.story.dialog.selectChoice", "btnChoice", payloadType: "MxFramework.Game.CharacterTest.CharacterTestFairyGuiStorySelectChoicePayload")
                },
                CommandIdExpressions = CharacterTestStoryDialogCommandIdExpressions()
            };
        }

        private static MxFairyGuiBinderGenerationSpec CreateCharacterTestStoryDialogBinderGenerationSpec()
        {
            return new MxFairyGuiBinderGenerationSpec
            {
                NamespaceName = "MxFramework.Game.CharacterTest",
                ClassName = "CharacterTestFairyGuiStoryDialogBinder",
                ViewModelTypeName = "CharacterTestFairyGuiStoryDialogViewModel",
                ViewModelDefaultExpression = "new CharacterTestFairyGuiStoryDialogViewModel()",
                ViewIdExpression = "CharacterTestFairyGuiStoryDialogIds.ViewId",
                InvalidHandleMessage = "CharacterTest Story dialog binder requires a concrete FairyGUI component handle.",
                LocalizedTextResolverExpression = "CharacterTestUiText.Resolve({provider}, {key}, {fallback})",
                TextBindings = new[]
                {
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiStoryDialogIds.Title", "viewModel.Title"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiStoryDialogIds.Phase", "viewModel.Phase"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiStoryDialogIds.DialogueText", "viewModel.DialogueText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiStoryDialogIds.ChoiceText", "viewModel.ChoiceText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiStoryDialogIds.SignalText", "viewModel.SignalText"),
                    new MxFairyGuiTextBindingSpec("CharacterTestFairyGuiStoryDialogIds.EventLog", "viewModel.EventLogText")
                },
                ButtonBindings = new[]
                {
                    new MxFairyGuiButtonBindingSpec(
                        "CharacterTestFairyGuiStoryDialogIds.Continue",
                        "ResolveContinueLabel(viewModel)",
                        "ResolveContinueEnabled(viewModel)",
                        "CharacterTestFairyGuiStoryCommandIds.CompletePresentation",
                        "() => ResolveContinuePayload(viewModel)")
                }
            };
        }

        private static MxFairyGuiManifestGenerationSpec CreatePlayerGuidanceCardGenerationSpec()
        {
            return new MxFairyGuiManifestGenerationSpec
            {
                NamespaceName = "MxFramework.UI.FairyGui",
                ClassName = "MxPlayerGuidanceCardManifest",
                PackageId = "MxPlayerGuidanceUi",
                PackageName = "MxPlayerGuidanceUi",
                PackageSourcePath = "FGUIProject/assets/MxPlayerGuidanceUi/package.xml",
                PackageBytesPath = "Assets/Bundles/FGUI/MxPlayerGuidanceUi/MxPlayerGuidanceUi_fui.bytes",
                PackageBytesResourceId = "ui.fairygui.player_guidance.fui",
                ComponentName = "GuidanceCardPanel",
                ComponentSourcePath = "FGUIProject/assets/MxPlayerGuidanceUi/Components/GuidanceCardPanel.xml",
                ViewId = "ui.guidance.card",
                ViewModelTypeName = "MxFramework.UI.FairyGui.MxFairyGuiGuidanceCardViewModel",
                PackageIdExpression = "MxPlayerGuidanceFairyGuiIds.PackageId",
                PackageNameExpression = "MxPlayerGuidanceFairyGuiIds.PackageId",
                PackageBytesExpression = "MxPlayerGuidanceFairyGuiIds.PackageBytesKey",
                ViewIdExpression = "MxPlayerGuidanceFairyGuiIds.CardViewId",
                ComponentNameExpression = "MxPlayerGuidanceFairyGuiIds.CardComponentName",
                ViewModelTypeExpression = "typeof(MxFairyGuiGuidanceCardViewModel).FullName",
                Layer = MxUiLayer.Hud,
                ControlBindPaths = new[]
                {
                    new MxFairyGuiControlBindPathSpec("headerIcon", "HeaderGlyph"),
                    new MxFairyGuiControlBindPathSpec("title", "Title"),
                    new MxFairyGuiControlBindPathSpec("actionIcon", "ActionGlyph"),
                    new MxFairyGuiControlBindPathSpec("actionLabel", "ActionLabel"),
                    new MxFairyGuiControlBindPathSpec("actionBody", "ActionBody"),
                    new MxFairyGuiControlBindPathSpec("focusIcon", "FocusGlyph"),
                    new MxFairyGuiControlBindPathSpec("focusLabel", "FocusLabel"),
                    new MxFairyGuiControlBindPathSpec("focusBody", "FocusBody"),
                    new MxFairyGuiControlBindPathSpec("statusIcon", "StatusGlyph"),
                    new MxFairyGuiControlBindPathSpec("statusLabel", "StatusLabel"),
                    new MxFairyGuiControlBindPathSpec("statusBody", "StatusBody")
                },
                ControlNameExpressions = new[]
                {
                    new MxFairyGuiCodeExpressionSpec("cardChrome", "MxPlayerGuidanceFairyGuiIds.CardChrome"),
                    new MxFairyGuiCodeExpressionSpec("headerIcon", "MxPlayerGuidanceFairyGuiIds.HeaderIcon"),
                    new MxFairyGuiCodeExpressionSpec("title", "MxPlayerGuidanceFairyGuiIds.Title"),
                    new MxFairyGuiCodeExpressionSpec("actionIcon", "MxPlayerGuidanceFairyGuiIds.ActionIcon"),
                    new MxFairyGuiCodeExpressionSpec("actionLabel", "MxPlayerGuidanceFairyGuiIds.ActionLabel"),
                    new MxFairyGuiCodeExpressionSpec("actionBody", "MxPlayerGuidanceFairyGuiIds.ActionBody"),
                    new MxFairyGuiCodeExpressionSpec("focusIcon", "MxPlayerGuidanceFairyGuiIds.FocusIcon"),
                    new MxFairyGuiCodeExpressionSpec("focusLabel", "MxPlayerGuidanceFairyGuiIds.FocusLabel"),
                    new MxFairyGuiCodeExpressionSpec("focusBody", "MxPlayerGuidanceFairyGuiIds.FocusBody"),
                    new MxFairyGuiCodeExpressionSpec("statusIcon", "MxPlayerGuidanceFairyGuiIds.StatusIcon"),
                    new MxFairyGuiCodeExpressionSpec("statusLabel", "MxPlayerGuidanceFairyGuiIds.StatusLabel"),
                    new MxFairyGuiCodeExpressionSpec("statusBody", "MxPlayerGuidanceFairyGuiIds.StatusBody")
                },
                AdditionalPackageResources = PlayerGuidancePackageResources()
            };
        }

        private static MxFairyGuiBinderGenerationSpec CreatePlayerGuidanceCardBinderGenerationSpec()
        {
            return new MxFairyGuiBinderGenerationSpec
            {
                NamespaceName = "MxFramework.UI.FairyGui",
                ClassName = "MxPlayerGuidanceCardBinder",
                ViewModelTypeName = "MxFairyGuiGuidanceCardViewModel",
                ViewModelDefaultExpression = "new MxFairyGuiGuidanceCardViewModel()",
                ViewIdExpression = "MxPlayerGuidanceFairyGuiIds.CardViewId",
                InvalidHandleMessage = "Player Guidance Card binder requires a concrete FairyGUI component handle.",
                RequestDefaultFocusInPlayMode = false,
                RootDefaultWidthExpression = "1920f",
                RootDefaultHeightExpression = "1080f",
                TextBindings = new[]
                {
                    new MxFairyGuiTextBindingSpec("MxPlayerGuidanceFairyGuiIds.Title", "viewModel.Title"),
                    new MxFairyGuiTextBindingSpec("MxPlayerGuidanceFairyGuiIds.ActionLabel", "viewModel.ActionLabel"),
                    new MxFairyGuiTextBindingSpec("MxPlayerGuidanceFairyGuiIds.ActionBody", "viewModel.ActionBody"),
                    new MxFairyGuiTextBindingSpec("MxPlayerGuidanceFairyGuiIds.FocusLabel", "viewModel.FocusLabel"),
                    new MxFairyGuiTextBindingSpec("MxPlayerGuidanceFairyGuiIds.FocusBody", "viewModel.FocusBody"),
                    new MxFairyGuiTextBindingSpec("MxPlayerGuidanceFairyGuiIds.StatusLabel", "viewModel.StatusLabel"),
                    new MxFairyGuiTextBindingSpec("MxPlayerGuidanceFairyGuiIds.StatusBody", "viewModel.StatusBody")
                }
            };
        }

        private static MxFairyGuiManifestGenerationSpec CreatePlayerGuidanceNoticesGenerationSpec()
        {
            return new MxFairyGuiManifestGenerationSpec
            {
                NamespaceName = "MxFramework.UI.FairyGui",
                ClassName = "MxPlayerGuidanceNoticeStackManifest",
                PackageId = "MxPlayerGuidanceUi",
                PackageName = "MxPlayerGuidanceUi",
                PackageSourcePath = "FGUIProject/assets/MxPlayerGuidanceUi/package.xml",
                PackageBytesPath = "Assets/Bundles/FGUI/MxPlayerGuidanceUi/MxPlayerGuidanceUi_fui.bytes",
                PackageBytesResourceId = "ui.fairygui.player_guidance.fui",
                ComponentName = "GuidanceNoticeStackPanel",
                ComponentSourcePath = "FGUIProject/assets/MxPlayerGuidanceUi/Components/GuidanceNoticeStackPanel.xml",
                ViewId = "ui.guidance.notices",
                ViewModelTypeName = "MxFramework.UI.FairyGui.MxFairyGuiGuidanceNoticeStackViewModel",
                PackageIdExpression = "MxPlayerGuidanceFairyGuiIds.PackageId",
                PackageNameExpression = "MxPlayerGuidanceFairyGuiIds.PackageId",
                PackageBytesExpression = "MxPlayerGuidanceFairyGuiIds.PackageBytesKey",
                ViewIdExpression = "MxPlayerGuidanceFairyGuiIds.NoticeViewId",
                ComponentNameExpression = "MxPlayerGuidanceFairyGuiIds.NoticeComponentName",
                ViewModelTypeExpression = "typeof(MxFairyGuiGuidanceNoticeStackViewModel).FullName",
                Layer = MxUiLayer.Toast,
                ControlBindPaths = new[]
                {
                    new MxFairyGuiControlBindPathSpec("notice1Chrome", "Notice1Visible"),
                    new MxFairyGuiControlBindPathSpec("notice1ToneOverlay", "Notice1Tone"),
                    new MxFairyGuiControlBindPathSpec("notice1Icon", "Notice1Glyph"),
                    new MxFairyGuiControlBindPathSpec("notice1Text", "Notice1Text"),
                    new MxFairyGuiControlBindPathSpec("notice2Chrome", "Notice2Visible"),
                    new MxFairyGuiControlBindPathSpec("notice2ToneOverlay", "Notice2Tone"),
                    new MxFairyGuiControlBindPathSpec("notice2Icon", "Notice2Glyph"),
                    new MxFairyGuiControlBindPathSpec("notice2Text", "Notice2Text")
                },
                ControlNameExpressions = new[]
                {
                    new MxFairyGuiCodeExpressionSpec("notice1Chrome", "MxPlayerGuidanceFairyGuiIds.Notice1Chrome"),
                    new MxFairyGuiCodeExpressionSpec("notice1ToneOverlay", "MxPlayerGuidanceFairyGuiIds.Notice1ToneOverlay"),
                    new MxFairyGuiCodeExpressionSpec("notice1Icon", "MxPlayerGuidanceFairyGuiIds.Notice1Icon"),
                    new MxFairyGuiCodeExpressionSpec("notice1Text", "MxPlayerGuidanceFairyGuiIds.Notice1Text"),
                    new MxFairyGuiCodeExpressionSpec("notice2Chrome", "MxPlayerGuidanceFairyGuiIds.Notice2Chrome"),
                    new MxFairyGuiCodeExpressionSpec("notice2ToneOverlay", "MxPlayerGuidanceFairyGuiIds.Notice2ToneOverlay"),
                    new MxFairyGuiCodeExpressionSpec("notice2Icon", "MxPlayerGuidanceFairyGuiIds.Notice2Icon"),
                    new MxFairyGuiCodeExpressionSpec("notice2Text", "MxPlayerGuidanceFairyGuiIds.Notice2Text")
                },
                AdditionalPackageResources = PlayerGuidancePackageResources()
            };
        }

        private static MxFairyGuiBinderGenerationSpec CreatePlayerGuidanceNoticesBinderGenerationSpec()
        {
            return new MxFairyGuiBinderGenerationSpec
            {
                NamespaceName = "MxFramework.UI.FairyGui",
                ClassName = "MxPlayerGuidanceNoticeStackBinder",
                ViewModelTypeName = "MxFairyGuiGuidanceNoticeStackViewModel",
                ViewModelDefaultExpression = "new MxFairyGuiGuidanceNoticeStackViewModel()",
                ViewIdExpression = "MxPlayerGuidanceFairyGuiIds.NoticeViewId",
                InvalidHandleMessage = "Player Guidance Notice Stack binder requires a concrete FairyGUI component handle.",
                RequestDefaultFocusInPlayMode = false,
                RootDefaultWidthExpression = "1920f",
                RootDefaultHeightExpression = "1080f",
                TextBindings = new[]
                {
                    new MxFairyGuiTextBindingSpec("MxPlayerGuidanceFairyGuiIds.Notice1Text", "viewModel.Notice1Visible ? viewModel.Notice1Text : string.Empty"),
                    new MxFairyGuiTextBindingSpec("MxPlayerGuidanceFairyGuiIds.Notice2Text", "viewModel.Notice2Visible ? viewModel.Notice2Text : string.Empty")
                }
            };
        }

        private static MxFairyGuiPackageResourceGenerationSpec[] PlayerGuidancePackageResources()
        {
            return new[]
            {
                new MxFairyGuiPackageResourceGenerationSpec(
                    "ui.fairygui.player_guidance.atlas0",
                    ResourceTypeIds.Texture2D,
                    MxFairyGuiManifestResourceKind.Atlas,
                    packageId: "MxPlayerGuidanceUi",
                    resourceKeyExpression: "MxPlayerGuidanceFairyGuiIds.Atlas0Key")
            };
        }

        private static string CharacterTestSettingsPanelGeneratedManifestPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/Generated/CharacterTestFairyGuiSettingsPanelManifest.g.cs";
        }

        private static string CharacterTestCombatHudGeneratedManifestPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/Generated/CharacterTestFairyGuiCombatHudManifest.g.cs";
        }

        private static string CharacterTestCombatHudGeneratedBinderPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/Generated/CharacterTestFairyGuiCombatHudBinder.g.cs";
        }

        private static string CharacterTestSettingsPanelGeneratedBinderPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/Generated/CharacterTestFairyGuiSettingsPanelBinder.g.cs";
        }

        private static string CharacterTestNetworkPanelGeneratedManifestPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/Generated/CharacterTestFairyGuiNetworkPanelManifest.g.cs";
        }

        private static string CharacterTestNetworkPanelGeneratedBinderPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/Generated/CharacterTestFairyGuiNetworkPanelBinder.g.cs";
        }

        private static string CharacterTestMainMenuGeneratedManifestPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/Generated/CharacterTestFairyGuiMainMenuManifest.g.cs";
        }

        private static string CharacterTestMainMenuGeneratedBinderPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/Generated/CharacterTestFairyGuiMainMenuBinder.g.cs";
        }

        private static string CharacterTestAbilityBindingPanelGeneratedManifestPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/Generated/CharacterTestFairyGuiAbilityBindingPanelManifest.g.cs";
        }

        private static string CharacterTestAbilityBindingPanelGeneratedBinderPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/Generated/CharacterTestFairyGuiAbilityBindingPanelBinder.g.cs";
        }

        private static string CharacterTestBehaviorDeckPanelGeneratedManifestPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/Generated/CharacterTestFairyGuiBehaviorDeckPanelManifest.g.cs";
        }

        private static string CharacterTestBehaviorDeckPanelGeneratedBinderPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/Generated/CharacterTestFairyGuiBehaviorDeckPanelBinder.g.cs";
        }

        private static string CharacterTestTacticalDeckPanelGeneratedManifestPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/Generated/CharacterTestFairyGuiTacticalDeckPanelManifest.g.cs";
        }

        private static string CharacterTestStoryDialogGeneratedManifestPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/Generated/CharacterTestFairyGuiStoryDialogManifest.g.cs";
        }

        private static string CharacterTestStoryDialogGeneratedBinderPath()
        {
            return "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/Generated/CharacterTestFairyGuiStoryDialogBinder.g.cs";
        }

        private static string PlayerGuidanceCardGeneratedManifestPath()
        {
            return "Packages/com.vincent.wgameframework/UI/FairyGUI/Guidance/Generated/MxPlayerGuidanceCardManifest.g.cs";
        }

        private static string PlayerGuidanceCardGeneratedBinderPath()
        {
            return "Packages/com.vincent.wgameframework/UI/FairyGUI/Guidance/Generated/MxPlayerGuidanceCardBinder.g.cs";
        }

        private static string PlayerGuidanceNoticesGeneratedManifestPath()
        {
            return "Packages/com.vincent.wgameframework/UI/FairyGUI/Guidance/Generated/MxPlayerGuidanceNoticeStackManifest.g.cs";
        }

        private static string PlayerGuidanceNoticesGeneratedBinderPath()
        {
            return "Packages/com.vincent.wgameframework/UI/FairyGUI/Guidance/Generated/MxPlayerGuidanceNoticeStackBinder.g.cs";
        }

        private static MxFairyGuiControlBindPathSpec[] CharacterTestCombatHudControlBindPaths()
        {
            return new[]
            {
                new MxFairyGuiControlBindPathSpec("playerName", "PlayerName"),
                new MxFairyGuiControlBindPathSpec("playerHpText", "PlayerHealth"),
                new MxFairyGuiControlBindPathSpec("playerPostureText", "PlayerPosturePressure"),
                new MxFairyGuiControlBindPathSpec("playerActionText", "PlayerActionText"),
                new MxFairyGuiControlBindPathSpec("targetName", "TargetName"),
                new MxFairyGuiControlBindPathSpec("targetStatusText", "TargetStatusText"),
                new MxFairyGuiControlBindPathSpec("targetHpText", "TargetHealth"),
                new MxFairyGuiControlBindPathSpec("targetPostureText", "TargetPosturePressure"),
                new MxFairyGuiControlBindPathSpec("feedbackText", "FeedbackText"),
                new MxFairyGuiControlBindPathSpec("networkLatencyText", "NetworkLatencyText"),
                new MxFairyGuiControlBindPathSpec("behaviorCardIconText", "BehaviorCardIconText"),
                new MxFairyGuiControlBindPathSpec("behaviorCardName", "BehaviorCardName"),
                new MxFairyGuiControlBindPathSpec("behaviorCardReason", "BehaviorCardReasonText")
            };
        }

        private static MxFairyGuiCodeExpressionSpec[] CharacterTestCombatHudControlNameExpressions()
        {
            var values = new List<MxFairyGuiCodeExpressionSpec>
            {
                new MxFairyGuiCodeExpressionSpec("playerPlate", "CharacterTestFairyGuiCombatHudIds.PlayerPlate"),
                new MxFairyGuiCodeExpressionSpec("playerName", "CharacterTestFairyGuiCombatHudIds.PlayerName"),
                new MxFairyGuiCodeExpressionSpec("playerHealthIcon", "CharacterTestFairyGuiCombatHudIds.PlayerHealthIcon"),
                new MxFairyGuiCodeExpressionSpec("playerHpTrack", "CharacterTestFairyGuiCombatHudIds.PlayerHealthTrack"),
                new MxFairyGuiCodeExpressionSpec("playerHpFill", "CharacterTestFairyGuiCombatHudIds.PlayerHealthFill"),
                new MxFairyGuiCodeExpressionSpec("playerHpText", "CharacterTestFairyGuiCombatHudIds.PlayerHealthText"),
                new MxFairyGuiCodeExpressionSpec("playerPostureIcon", "CharacterTestFairyGuiCombatHudIds.PlayerPostureIcon"),
                new MxFairyGuiCodeExpressionSpec("playerPostureTrack", "CharacterTestFairyGuiCombatHudIds.PlayerPostureTrack"),
                new MxFairyGuiCodeExpressionSpec("playerPostureFill", "CharacterTestFairyGuiCombatHudIds.PlayerPostureFill"),
                new MxFairyGuiCodeExpressionSpec("playerPostureBreakMarker", "CharacterTestFairyGuiCombatHudIds.PlayerPostureBreakMarker"),
                new MxFairyGuiCodeExpressionSpec("playerPostureText", "CharacterTestFairyGuiCombatHudIds.PlayerPostureText"),
                new MxFairyGuiCodeExpressionSpec("playerActionText", "CharacterTestFairyGuiCombatHudIds.PlayerActionText"),
                new MxFairyGuiCodeExpressionSpec("targetPlate", "CharacterTestFairyGuiCombatHudIds.TargetPlate"),
                new MxFairyGuiCodeExpressionSpec("targetName", "CharacterTestFairyGuiCombatHudIds.TargetName"),
                new MxFairyGuiCodeExpressionSpec("targetStatusText", "CharacterTestFairyGuiCombatHudIds.TargetStatusText"),
                new MxFairyGuiCodeExpressionSpec("targetHpTrack", "CharacterTestFairyGuiCombatHudIds.TargetHealthTrack"),
                new MxFairyGuiCodeExpressionSpec("targetHpFill", "CharacterTestFairyGuiCombatHudIds.TargetHealthFill"),
                new MxFairyGuiCodeExpressionSpec("targetHpText", "CharacterTestFairyGuiCombatHudIds.TargetHealthText"),
                new MxFairyGuiCodeExpressionSpec("targetPostureTrack", "CharacterTestFairyGuiCombatHudIds.TargetPostureTrack"),
                new MxFairyGuiCodeExpressionSpec("targetPostureFill", "CharacterTestFairyGuiCombatHudIds.TargetPostureFill"),
                new MxFairyGuiCodeExpressionSpec("targetPostureBreakMarker", "CharacterTestFairyGuiCombatHudIds.TargetPostureBreakMarker"),
                new MxFairyGuiCodeExpressionSpec("targetPostureText", "CharacterTestFairyGuiCombatHudIds.TargetPostureText"),
                new MxFairyGuiCodeExpressionSpec("lockCrosshair", "CharacterTestFairyGuiCombatHudIds.LockCrosshair"),
                new MxFairyGuiCodeExpressionSpec("feedbackPanel", "CharacterTestFairyGuiCombatHudIds.FeedbackPanel"),
                new MxFairyGuiCodeExpressionSpec("feedbackTrim", "CharacterTestFairyGuiCombatHudIds.FeedbackTrim"),
                new MxFairyGuiCodeExpressionSpec("feedbackText", "CharacterTestFairyGuiCombatHudIds.FeedbackText"),
                new MxFairyGuiCodeExpressionSpec("networkLatencyBadgeBg", "CharacterTestFairyGuiCombatHudIds.NetworkLatencyBadgeBackground"),
                new MxFairyGuiCodeExpressionSpec("networkLatencyText", "CharacterTestFairyGuiCombatHudIds.NetworkLatencyText"),
                new MxFairyGuiCodeExpressionSpec("behaviorCardPanel", "CharacterTestFairyGuiCombatHudIds.BehaviorCardPanel"),
                new MxFairyGuiCodeExpressionSpec("behaviorCardIcon", "CharacterTestFairyGuiCombatHudIds.BehaviorCardIcon"),
                new MxFairyGuiCodeExpressionSpec("behaviorCardIconText", "CharacterTestFairyGuiCombatHudIds.BehaviorCardIconText"),
                new MxFairyGuiCodeExpressionSpec("behaviorCardName", "CharacterTestFairyGuiCombatHudIds.BehaviorCardName"),
                new MxFairyGuiCodeExpressionSpec("behaviorCardReason", "CharacterTestFairyGuiCombatHudIds.BehaviorCardReason")
            };

            for (int slot = 1; slot <= 9; slot++)
            {
                values.Add(new MxFairyGuiCodeExpressionSpec("abilitySlot" + slot + "Bg", "CharacterTestFairyGuiCombatHudIds.AbilitySlotBackground(" + slot + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("abilitySlot" + slot + "Selected", "CharacterTestFairyGuiCombatHudIds.AbilitySlotSelected(" + slot + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("abilitySlot" + slot + "Key", "CharacterTestFairyGuiCombatHudIds.AbilitySlotKey(" + slot + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("abilitySlot" + slot + "Name", "CharacterTestFairyGuiCombatHudIds.AbilitySlotName(" + slot + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("abilitySlot" + slot + "CooldownFill", "CharacterTestFairyGuiCombatHudIds.AbilitySlotCooldownFill(" + slot + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("abilitySlot" + slot + "CooldownText", "CharacterTestFairyGuiCombatHudIds.AbilitySlotCooldownText(" + slot + ")"));
            }

            for (int slot = 1; slot <= 8; slot++)
            {
                values.Add(new MxFairyGuiCodeExpressionSpec("playerBuffSlot" + slot + "Bg", "CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotBackground(" + slot + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("playerBuffSlot" + slot + "Border", "CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotBorder(" + slot + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("playerBuffSlot" + slot + "Label", "CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotLabel(" + slot + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("playerBuffSlot" + slot + "CooldownFill", "CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotCooldownFill(" + slot + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("playerBuffSlot" + slot + "Stack", "CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotStack(" + slot + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("playerBuffSlot" + slot + "Time", "CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotTime(" + slot + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("targetBuffSlot" + slot + "Bg", "CharacterTestFairyGuiCombatHudIds.TargetBuffSlotBackground(" + slot + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("targetBuffSlot" + slot + "Border", "CharacterTestFairyGuiCombatHudIds.TargetBuffSlotBorder(" + slot + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("targetBuffSlot" + slot + "Label", "CharacterTestFairyGuiCombatHudIds.TargetBuffSlotLabel(" + slot + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("targetBuffSlot" + slot + "CooldownFill", "CharacterTestFairyGuiCombatHudIds.TargetBuffSlotCooldownFill(" + slot + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("targetBuffSlot" + slot + "Stack", "CharacterTestFairyGuiCombatHudIds.TargetBuffSlotStack(" + slot + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("targetBuffSlot" + slot + "Time", "CharacterTestFairyGuiCombatHudIds.TargetBuffSlotTime(" + slot + ")"));
            }

            return values.ToArray();
        }

        private static MxFairyGuiControlBindPathSpec[] CharacterTestSettingsPanelControlBindPaths()
        {
            return new[]
            {
                new MxFairyGuiControlBindPathSpec("title", "Title"),
                new MxFairyGuiControlBindPathSpec("subtitle", "Subtitle"),
                new MxFairyGuiControlBindPathSpec("pageTitle", "PageTitle"),
                new MxFairyGuiControlBindPathSpec("pageDescription", "PageDescription"),
                new MxFairyGuiControlBindPathSpec("status", "StatusText"),
                new MxFairyGuiControlBindPathSpec("dirtyMarker", "DirtyText"),
                new MxFairyGuiControlBindPathSpec("inputInstruction", "InputInstruction"),
                new MxFairyGuiControlBindPathSpec("actionColumnTitle", "ActionColumnTitle"),
                new MxFairyGuiControlBindPathSpec("primaryColumnTitle", "PrimaryColumnTitle"),
                new MxFairyGuiControlBindPathSpec("secondaryColumnTitle", "SecondaryColumnTitle"),
                new MxFairyGuiControlBindPathSpec("inputList", "InputRows"),
                new MxFairyGuiControlBindPathSpec("antiAliasingValue", "AntiAliasingText"),
                new MxFairyGuiControlBindPathSpec("vSyncValue", "VSyncText"),
                new MxFairyGuiControlBindPathSpec("renderScaleInput", "RenderScaleText"),
                new MxFairyGuiControlBindPathSpec("shadowDistanceInput", "ShadowDistanceText"),
                new MxFairyGuiControlBindPathSpec("mainShadowResolutionValue", "DirectionalShadowQualityText"),
                new MxFairyGuiControlBindPathSpec("waterQualityValue", "WaterQualityText"),
                new MxFairyGuiControlBindPathSpec("rippleQualityValue", "RippleQualityText"),
                new MxFairyGuiControlBindPathSpec("shadowCascadeCountValue", "ShadowCascadeCountText"),
                new MxFairyGuiControlBindPathSpec("localShadowResolutionValue", "LocalShadowResolutionText"),
                    new MxFairyGuiControlBindPathSpec("hdrValue", "HdrModeText"),
                    new MxFairyGuiControlBindPathSpec("hdrStatus", "HdrStatusText"),
                    new MxFairyGuiControlBindPathSpec("hdrPaperWhiteValue", "HdrPaperWhiteText"),
                    new MxFairyGuiControlBindPathSpec("hdrPeakValue", "HdrPeakText"),
                new MxFairyGuiControlBindPathSpec("resolutionValue", "ResolutionText"),
                new MxFairyGuiControlBindPathSpec("windowModeValue", "WindowModeText"),
                new MxFairyGuiControlBindPathSpec("languageValue", "LocaleText"),
                new MxFairyGuiControlBindPathSpec("audioCategoryValue", "AudioCategoryText"),
                new MxFairyGuiControlBindPathSpec("audioVolumeInput", "AudioVolumeText"),
                new MxFairyGuiControlBindPathSpec("audioMutedValue", "AudioMutedText")
            };
        }

        private static MxFairyGuiCodeExpressionSpec[] CharacterTestSettingsPanelControlNameExpressions()
        {
            return new[]
            {
                new MxFairyGuiCodeExpressionSpec("panelFrame", "CharacterTestFairyGuiSettingsPanelIds.PanelFrame"),
                new MxFairyGuiCodeExpressionSpec("title", "CharacterTestFairyGuiSettingsPanelIds.Title"),
                new MxFairyGuiCodeExpressionSpec("subtitle", "CharacterTestFairyGuiSettingsPanelIds.Subtitle"),
                new MxFairyGuiCodeExpressionSpec("pageTitle", "CharacterTestFairyGuiSettingsPanelIds.PageTitle"),
                new MxFairyGuiCodeExpressionSpec("pageDescription", "CharacterTestFairyGuiSettingsPanelIds.PageDescription"),
                new MxFairyGuiCodeExpressionSpec("status", "CharacterTestFairyGuiSettingsPanelIds.Status"),
                new MxFairyGuiCodeExpressionSpec("dirtyMarker", "CharacterTestFairyGuiSettingsPanelIds.DirtyMarker"),
                new MxFairyGuiCodeExpressionSpec("btnClose", "CharacterTestFairyGuiSettingsPanelIds.Close"),
                new MxFairyGuiCodeExpressionSpec("btnNavGraphics", "CharacterTestFairyGuiSettingsPanelIds.NavGraphics"),
                new MxFairyGuiCodeExpressionSpec("btnNavAudio", "CharacterTestFairyGuiSettingsPanelIds.NavAudio"),
                new MxFairyGuiCodeExpressionSpec("btnNavControls", "CharacterTestFairyGuiSettingsPanelIds.NavControls"),
                new MxFairyGuiCodeExpressionSpec("btnKeyboardMouse", "CharacterTestFairyGuiSettingsPanelIds.KeyboardMouseTab"),
                new MxFairyGuiCodeExpressionSpec("btnGamepad", "CharacterTestFairyGuiSettingsPanelIds.GamepadTab"),
                new MxFairyGuiCodeExpressionSpec("inputInstruction", "CharacterTestFairyGuiSettingsPanelIds.InputInstruction"),
                new MxFairyGuiCodeExpressionSpec("actionColumnTitle", "CharacterTestFairyGuiSettingsPanelIds.ActionColumnTitle"),
                new MxFairyGuiCodeExpressionSpec("primaryColumnTitle", "CharacterTestFairyGuiSettingsPanelIds.PrimaryColumnTitle"),
                new MxFairyGuiCodeExpressionSpec("secondaryColumnTitle", "CharacterTestFairyGuiSettingsPanelIds.SecondaryColumnTitle"),
                new MxFairyGuiCodeExpressionSpec("inputList", "CharacterTestFairyGuiSettingsPanelIds.InputList"),
                new MxFairyGuiCodeExpressionSpec("btnInputRebindProxy", "CharacterTestFairyGuiSettingsPanelIds.InputRebindProxy"),
                new MxFairyGuiCodeExpressionSpec("antiAliasingLabel", "CharacterTestFairyGuiSettingsPanelIds.AntiAliasingLabel"),
                new MxFairyGuiCodeExpressionSpec("graphicsHeader", "CharacterTestFairyGuiSettingsPanelIds.GraphicsHeader"),
                new MxFairyGuiCodeExpressionSpec("antiAliasingValue", "CharacterTestFairyGuiSettingsPanelIds.AntiAliasingValue"),
                new MxFairyGuiCodeExpressionSpec("btnAntiAliasingPrev", "CharacterTestFairyGuiSettingsPanelIds.AntiAliasingPrev"),
                new MxFairyGuiCodeExpressionSpec("btnAntiAliasingNext", "CharacterTestFairyGuiSettingsPanelIds.AntiAliasingNext"),
                new MxFairyGuiCodeExpressionSpec("shadowDistanceLabel", "CharacterTestFairyGuiSettingsPanelIds.ShadowDistanceLabel"),
                new MxFairyGuiCodeExpressionSpec("shadowDistanceInput", "CharacterTestFairyGuiSettingsPanelIds.ShadowDistanceInput"),
                new MxFairyGuiCodeExpressionSpec("mainShadowResolutionLabel", "CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityLabel"),
                new MxFairyGuiCodeExpressionSpec("waterQualityLabel", "CharacterTestFairyGuiSettingsPanelIds.WaterQualityLabel"),
                new MxFairyGuiCodeExpressionSpec("rippleQualityLabel", "CharacterTestFairyGuiSettingsPanelIds.RippleQualityLabel"),
                new MxFairyGuiCodeExpressionSpec("mainShadowResolutionValue", "CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityValue"),
                new MxFairyGuiCodeExpressionSpec("waterQualityValue", "CharacterTestFairyGuiSettingsPanelIds.WaterQualityValue"),
                new MxFairyGuiCodeExpressionSpec("rippleQualityValue", "CharacterTestFairyGuiSettingsPanelIds.RippleQualityValue"),
                new MxFairyGuiCodeExpressionSpec("btnMainShadowResolutionPrev", "CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityPrev"),
                new MxFairyGuiCodeExpressionSpec("btnWaterQualityPrev", "CharacterTestFairyGuiSettingsPanelIds.WaterQualityPrev"),
                new MxFairyGuiCodeExpressionSpec("btnRippleQualityPrev", "CharacterTestFairyGuiSettingsPanelIds.RippleQualityPrev"),
                new MxFairyGuiCodeExpressionSpec("btnMainShadowResolutionNext", "CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityNext"),
                new MxFairyGuiCodeExpressionSpec("btnWaterQualityNext", "CharacterTestFairyGuiSettingsPanelIds.WaterQualityNext"),
                new MxFairyGuiCodeExpressionSpec("btnRippleQualityNext", "CharacterTestFairyGuiSettingsPanelIds.RippleQualityNext"),
                new MxFairyGuiCodeExpressionSpec("shadowCascadeCountLabel", "CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountLabel"),
                new MxFairyGuiCodeExpressionSpec("shadowCascadeCountValue", "CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountValue"),
                new MxFairyGuiCodeExpressionSpec("btnShadowCascadeCountPrev", "CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountPrev"),
                new MxFairyGuiCodeExpressionSpec("btnShadowCascadeCountNext", "CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountNext"),
                new MxFairyGuiCodeExpressionSpec("localShadowResolutionLabel", "CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionLabel"),
                new MxFairyGuiCodeExpressionSpec("localShadowResolutionValue", "CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionValue"),
                new MxFairyGuiCodeExpressionSpec("btnLocalShadowResolutionPrev", "CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionPrev"),
                new MxFairyGuiCodeExpressionSpec("btnLocalShadowResolutionNext", "CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionNext"),
                    new MxFairyGuiCodeExpressionSpec("hdrLabel", "CharacterTestFairyGuiSettingsPanelIds.HdrLabel"),
                    new MxFairyGuiCodeExpressionSpec("hdrPaperWhiteLabel", "CharacterTestFairyGuiSettingsPanelIds.HdrPaperWhiteLabel"),
                    new MxFairyGuiCodeExpressionSpec("hdrPeakLabel", "CharacterTestFairyGuiSettingsPanelIds.HdrPeakLabel"),
                    new MxFairyGuiCodeExpressionSpec("hdrValue", "CharacterTestFairyGuiSettingsPanelIds.HdrValue"),
                    new MxFairyGuiCodeExpressionSpec("hdrStatus", "CharacterTestFairyGuiSettingsPanelIds.HdrStatus"),
                    new MxFairyGuiCodeExpressionSpec("hdrPaperWhiteValue", "CharacterTestFairyGuiSettingsPanelIds.HdrPaperWhiteValue"),
                    new MxFairyGuiCodeExpressionSpec("hdrPeakValue", "CharacterTestFairyGuiSettingsPanelIds.HdrPeakValue"),
                    new MxFairyGuiCodeExpressionSpec("btnHdr", "CharacterTestFairyGuiSettingsPanelIds.HdrToggle"),
                    new MxFairyGuiCodeExpressionSpec("btnHdrPaperWhitePrev", "CharacterTestFairyGuiSettingsPanelIds.HdrPaperWhitePrev"),
                    new MxFairyGuiCodeExpressionSpec("btnHdrPaperWhiteNext", "CharacterTestFairyGuiSettingsPanelIds.HdrPaperWhiteNext"),
                    new MxFairyGuiCodeExpressionSpec("btnHdrPeakPrev", "CharacterTestFairyGuiSettingsPanelIds.HdrPeakPrev"),
                    new MxFairyGuiCodeExpressionSpec("btnHdrPeakNext", "CharacterTestFairyGuiSettingsPanelIds.HdrPeakNext"),
                new MxFairyGuiCodeExpressionSpec("vSyncLabel", "CharacterTestFairyGuiSettingsPanelIds.VSyncLabel"),
                new MxFairyGuiCodeExpressionSpec("vSyncValue", "CharacterTestFairyGuiSettingsPanelIds.VSyncValue"),
                new MxFairyGuiCodeExpressionSpec("btnVSync", "CharacterTestFairyGuiSettingsPanelIds.VSyncToggle"),
                new MxFairyGuiCodeExpressionSpec("renderScaleLabel", "CharacterTestFairyGuiSettingsPanelIds.RenderScaleLabel"),
                new MxFairyGuiCodeExpressionSpec("renderScaleInput", "CharacterTestFairyGuiSettingsPanelIds.RenderScaleInput"),
                new MxFairyGuiCodeExpressionSpec("resolutionLabel", "CharacterTestFairyGuiSettingsPanelIds.ResolutionLabel"),
                new MxFairyGuiCodeExpressionSpec("resolutionValue", "CharacterTestFairyGuiSettingsPanelIds.ResolutionValue"),
                new MxFairyGuiCodeExpressionSpec("windowModeLabel", "CharacterTestFairyGuiSettingsPanelIds.WindowModeLabel"),
                new MxFairyGuiCodeExpressionSpec("windowModeValue", "CharacterTestFairyGuiSettingsPanelIds.WindowModeValue"),
                new MxFairyGuiCodeExpressionSpec("languageLabel", "CharacterTestFairyGuiSettingsPanelIds.LanguageLabel"),
                new MxFairyGuiCodeExpressionSpec("languageValue", "CharacterTestFairyGuiSettingsPanelIds.LanguageValue"),
                new MxFairyGuiCodeExpressionSpec("btnLanguagePrev", "CharacterTestFairyGuiSettingsPanelIds.LanguagePrev"),
                new MxFairyGuiCodeExpressionSpec("btnLanguageNext", "CharacterTestFairyGuiSettingsPanelIds.LanguageNext"),
                new MxFairyGuiCodeExpressionSpec("audioVolumeLabel", "CharacterTestFairyGuiSettingsPanelIds.AudioVolumeLabel"),
                new MxFairyGuiCodeExpressionSpec("audioHeader", "CharacterTestFairyGuiSettingsPanelIds.AudioHeader"),
                new MxFairyGuiCodeExpressionSpec("audioCategoryLabel", "CharacterTestFairyGuiSettingsPanelIds.AudioCategoryLabel"),
                new MxFairyGuiCodeExpressionSpec("audioCategoryValue", "CharacterTestFairyGuiSettingsPanelIds.AudioCategoryValue"),
                new MxFairyGuiCodeExpressionSpec("btnAudioCategoryPrev", "CharacterTestFairyGuiSettingsPanelIds.AudioCategoryPrevious"),
                new MxFairyGuiCodeExpressionSpec("btnAudioCategoryNext", "CharacterTestFairyGuiSettingsPanelIds.AudioCategoryNext"),
                new MxFairyGuiCodeExpressionSpec("audioVolumeInput", "CharacterTestFairyGuiSettingsPanelIds.AudioVolumeInput"),
                new MxFairyGuiCodeExpressionSpec("audioMutedValue", "CharacterTestFairyGuiSettingsPanelIds.AudioMutedValue"),
                new MxFairyGuiCodeExpressionSpec("btnMute", "CharacterTestFairyGuiSettingsPanelIds.AudioMuteToggle"),
                new MxFairyGuiCodeExpressionSpec("btnApply", "CharacterTestFairyGuiSettingsPanelIds.Apply"),
                new MxFairyGuiCodeExpressionSpec("btnCancel", "CharacterTestFairyGuiSettingsPanelIds.CancelChanges"),
                new MxFairyGuiCodeExpressionSpec("btnResetInput", "CharacterTestFairyGuiSettingsPanelIds.ResetInput"),
                new MxFairyGuiCodeExpressionSpec("controlsHeader", "CharacterTestFairyGuiSettingsPanelIds.ControlsHeader")
            };
        }

        private static MxFairyGuiCodeExpressionSpec[] CharacterTestSettingsPanelCommandIdExpressions()
        {
            return new[]
            {
                new MxFairyGuiCodeExpressionSpec("character_test.settings.close", "CharacterTestFairyGuiSettingsPanelCommandIds.Close"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.apply", "CharacterTestFairyGuiSettingsPanelCommandIds.Apply"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.cancel", "CharacterTestFairyGuiSettingsPanelCommandIds.CancelChanges"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.page.graphics", "CharacterTestFairyGuiSettingsPanelCommandIds.SelectGraphics"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.page.audio", "CharacterTestFairyGuiSettingsPanelCommandIds.SelectAudio"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.page.controls", "CharacterTestFairyGuiSettingsPanelCommandIds.SelectControls"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.device.keyboard_mouse", "CharacterTestFairyGuiSettingsPanelCommandIds.SelectKeyboardMouse"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.device.gamepad", "CharacterTestFairyGuiSettingsPanelCommandIds.SelectGamepad"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.anti_aliasing.previous", "CharacterTestFairyGuiSettingsPanelCommandIds.AntiAliasingPrevious"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.anti_aliasing.next", "CharacterTestFairyGuiSettingsPanelCommandIds.AntiAliasingNext"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.directional_shadow_quality.previous", "CharacterTestFairyGuiSettingsPanelCommandIds.DirectionalShadowQualityPrevious"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.water_quality.previous", "CharacterTestFairyGuiSettingsPanelCommandIds.WaterQualityPrevious"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.ripple_quality.previous", "CharacterTestFairyGuiSettingsPanelCommandIds.RippleQualityPrevious"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.directional_shadow_quality.next", "CharacterTestFairyGuiSettingsPanelCommandIds.DirectionalShadowQualityNext"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.water_quality.next", "CharacterTestFairyGuiSettingsPanelCommandIds.WaterQualityNext"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.ripple_quality.next", "CharacterTestFairyGuiSettingsPanelCommandIds.RippleQualityNext"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.shadow_cascades.previous", "CharacterTestFairyGuiSettingsPanelCommandIds.ShadowCascadeCountPrevious"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.shadow_cascades.next", "CharacterTestFairyGuiSettingsPanelCommandIds.ShadowCascadeCountNext"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.local_shadow_resolution.previous", "CharacterTestFairyGuiSettingsPanelCommandIds.LocalShadowResolutionPrevious"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.local_shadow_resolution.next", "CharacterTestFairyGuiSettingsPanelCommandIds.LocalShadowResolutionNext"),
                    new MxFairyGuiCodeExpressionSpec("character_test.settings.hdr.toggle", "CharacterTestFairyGuiSettingsPanelCommandIds.ToggleHdr"),
                    new MxFairyGuiCodeExpressionSpec("character_test.settings.hdr.paper_white.previous", "CharacterTestFairyGuiSettingsPanelCommandIds.HdrPaperWhitePrevious"),
                    new MxFairyGuiCodeExpressionSpec("character_test.settings.hdr.paper_white.next", "CharacterTestFairyGuiSettingsPanelCommandIds.HdrPaperWhiteNext"),
                    new MxFairyGuiCodeExpressionSpec("character_test.settings.hdr.peak.previous", "CharacterTestFairyGuiSettingsPanelCommandIds.HdrPeakPrevious"),
                    new MxFairyGuiCodeExpressionSpec("character_test.settings.hdr.peak.next", "CharacterTestFairyGuiSettingsPanelCommandIds.HdrPeakNext"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.language.previous", "CharacterTestFairyGuiSettingsPanelCommandIds.LanguagePrevious"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.language.next", "CharacterTestFairyGuiSettingsPanelCommandIds.LanguageNext"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.vsync.toggle", "CharacterTestFairyGuiSettingsPanelCommandIds.ToggleVSync"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.audio.category.previous", "CharacterTestFairyGuiSettingsPanelCommandIds.AudioCategoryPrevious"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.audio.category.next", "CharacterTestFairyGuiSettingsPanelCommandIds.AudioCategoryNext"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.audio.mute.toggle", "CharacterTestFairyGuiSettingsPanelCommandIds.ToggleMute"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.input.rebind", "CharacterTestFairyGuiSettingsPanelCommandIds.StartInputRebind"),
                new MxFairyGuiCodeExpressionSpec("character_test.settings.input.reset", "CharacterTestFairyGuiSettingsPanelCommandIds.ResetInput")
            };
        }

        private static MxFairyGuiControlBindPathSpec[] CharacterTestNetworkPanelControlBindPaths()
        {
            return new[]
            {
                new MxFairyGuiControlBindPathSpec("title", "Title"),
                new MxFairyGuiControlBindPathSpec("status", "StatusText"),
                new MxFairyGuiControlBindPathSpec("endpoint", "EndpointText"),
                new MxFairyGuiControlBindPathSpec("modeDescription", "ModeDescriptionText"),
                new MxFairyGuiControlBindPathSpec("remoteAddressInput", "JoinOptions.RemoteAddress"),
                new MxFairyGuiControlBindPathSpec("portInput", "JoinOptions.Port"),
                new MxFairyGuiControlBindPathSpec("displayNameInput", "JoinOptions.DisplayName"),
                new MxFairyGuiControlBindPathSpec("roomNameInput", "JoinOptions.RoomName"),
                new MxFairyGuiControlBindPathSpec("connectionSummary", "ConnectionSummaryText"),
                new MxFairyGuiControlBindPathSpec("roomSummary", "RoomSummaryText"),
                new MxFairyGuiControlBindPathSpec("memberSummary", "MemberSummaryText"),
                new MxFairyGuiControlBindPathSpec("readinessSummary", "ReadinessSummaryText"),
                new MxFairyGuiControlBindPathSpec("syncProbeSummary", "SyncProbeSummaryText"),
                new MxFairyGuiControlBindPathSpec("replaySummary", "ReplaySummaryText"),
                new MxFairyGuiControlBindPathSpec("detailTransport", "DetailTransportText"),
                new MxFairyGuiControlBindPathSpec("detailPeer", "DetailPeerText"),
                new MxFairyGuiControlBindPathSpec("detailRtt", "DetailRttText"),
                new MxFairyGuiControlBindPathSpec("detailFrames", "DetailFramesText"),
                new MxFairyGuiControlBindPathSpec("detailSnapshot", "DetailSnapshotText"),
                new MxFairyGuiControlBindPathSpec("detailDisconnect", "DetailDisconnectText"),
                new MxFairyGuiControlBindPathSpec("message", "Message")
            };
        }

        private static MxFairyGuiCodeExpressionSpec[] CharacterTestNetworkPanelControlNameExpressions()
        {
            return new[]
            {
                new MxFairyGuiCodeExpressionSpec("title", "CharacterTestFairyGuiNetworkPanelIds.Title"),
                new MxFairyGuiCodeExpressionSpec("status", "CharacterTestFairyGuiNetworkPanelIds.Status"),
                new MxFairyGuiCodeExpressionSpec("btnClose", "CharacterTestFairyGuiNetworkPanelIds.Close"),
                new MxFairyGuiCodeExpressionSpec("endpoint", "CharacterTestFairyGuiNetworkPanelIds.Endpoint"),
                new MxFairyGuiCodeExpressionSpec("btnDirectLan", "CharacterTestFairyGuiNetworkPanelIds.DirectLanMode"),
                new MxFairyGuiCodeExpressionSpec("btnRoom", "CharacterTestFairyGuiNetworkPanelIds.RoomMode"),
                new MxFairyGuiCodeExpressionSpec("modeDescription", "CharacterTestFairyGuiNetworkPanelIds.ModeDescription"),
                new MxFairyGuiCodeExpressionSpec("connectionHeader", "CharacterTestFairyGuiNetworkPanelIds.ConnectionHeader"),
                new MxFairyGuiCodeExpressionSpec("remoteAddressLabel", "CharacterTestFairyGuiNetworkPanelIds.RemoteAddressLabel"),
                new MxFairyGuiCodeExpressionSpec("remoteAddressInput", "CharacterTestFairyGuiNetworkPanelIds.RemoteAddressInput"),
                new MxFairyGuiCodeExpressionSpec("portLabel", "CharacterTestFairyGuiNetworkPanelIds.PortLabel"),
                new MxFairyGuiCodeExpressionSpec("portInput", "CharacterTestFairyGuiNetworkPanelIds.PortInput"),
                new MxFairyGuiCodeExpressionSpec("displayNameLabel", "CharacterTestFairyGuiNetworkPanelIds.DisplayNameLabel"),
                new MxFairyGuiCodeExpressionSpec("displayNameInput", "CharacterTestFairyGuiNetworkPanelIds.DisplayNameInput"),
                new MxFairyGuiCodeExpressionSpec("roomNameLabel", "CharacterTestFairyGuiNetworkPanelIds.RoomNameLabel"),
                new MxFairyGuiCodeExpressionSpec("roomNameInput", "CharacterTestFairyGuiNetworkPanelIds.RoomNameInput"),
                new MxFairyGuiCodeExpressionSpec("message", "CharacterTestFairyGuiNetworkPanelIds.Message"),
                new MxFairyGuiCodeExpressionSpec("btnHost", "CharacterTestFairyGuiNetworkPanelIds.Host"),
                new MxFairyGuiCodeExpressionSpec("btnJoin", "CharacterTestFairyGuiNetworkPanelIds.Join"),
                new MxFairyGuiCodeExpressionSpec("btnDisconnect", "CharacterTestFairyGuiNetworkPanelIds.Disconnect"),
                new MxFairyGuiCodeExpressionSpec("btnReady", "CharacterTestFairyGuiNetworkPanelIds.Ready"),
                new MxFairyGuiCodeExpressionSpec("btnEnterGame", "CharacterTestFairyGuiNetworkPanelIds.EnterGame"),
                new MxFairyGuiCodeExpressionSpec("readinessSummary", "CharacterTestFairyGuiNetworkPanelIds.ReadinessSummary"),
                new MxFairyGuiCodeExpressionSpec("syncProbeSummary", "CharacterTestFairyGuiNetworkPanelIds.SyncProbeSummary"),
                new MxFairyGuiCodeExpressionSpec("btnAutoSyncProbe", "CharacterTestFairyGuiNetworkPanelIds.AutoSyncProbe"),
                new MxFairyGuiCodeExpressionSpec("btnRunSyncProbe", "CharacterTestFairyGuiNetworkPanelIds.RunSyncProbe"),
                new MxFairyGuiCodeExpressionSpec("replayHeader", "CharacterTestFairyGuiNetworkPanelIds.ReplayHeader"),
                new MxFairyGuiCodeExpressionSpec("replaySummary", "CharacterTestFairyGuiNetworkPanelIds.ReplaySummary"),
                new MxFairyGuiCodeExpressionSpec("connectionSummary", "CharacterTestFairyGuiNetworkPanelIds.ConnectionSummary"),
                new MxFairyGuiCodeExpressionSpec("roomHeader", "CharacterTestFairyGuiNetworkPanelIds.RoomHeader"),
                new MxFairyGuiCodeExpressionSpec("roomSummary", "CharacterTestFairyGuiNetworkPanelIds.RoomSummary"),
                new MxFairyGuiCodeExpressionSpec("memberSummary", "CharacterTestFairyGuiNetworkPanelIds.MemberSummary"),
                new MxFairyGuiCodeExpressionSpec("detailsHeader", "CharacterTestFairyGuiNetworkPanelIds.DetailsHeader"),
                new MxFairyGuiCodeExpressionSpec("detailTransport", "CharacterTestFairyGuiNetworkPanelIds.DetailTransport"),
                new MxFairyGuiCodeExpressionSpec("detailPeer", "CharacterTestFairyGuiNetworkPanelIds.DetailPeer"),
                new MxFairyGuiCodeExpressionSpec("detailRtt", "CharacterTestFairyGuiNetworkPanelIds.DetailRtt"),
                new MxFairyGuiCodeExpressionSpec("detailFrames", "CharacterTestFairyGuiNetworkPanelIds.DetailFrames"),
                new MxFairyGuiCodeExpressionSpec("detailSnapshot", "CharacterTestFairyGuiNetworkPanelIds.DetailSnapshot"),
                new MxFairyGuiCodeExpressionSpec("detailDisconnect", "CharacterTestFairyGuiNetworkPanelIds.DetailDisconnect")
            };
        }

        private static MxFairyGuiCodeExpressionSpec[] CharacterTestNetworkPanelCommandIdExpressions()
        {
            return new[]
            {
                new MxFairyGuiCodeExpressionSpec("character_test.network.mode.direct_lan", "CharacterTestFairyGuiNetworkPanelCommandIds.DirectLanMode"),
                new MxFairyGuiCodeExpressionSpec("character_test.network.mode.room", "CharacterTestFairyGuiNetworkPanelCommandIds.RoomMode"),
                new MxFairyGuiCodeExpressionSpec("character_test.network.host", "CharacterTestFairyGuiNetworkPanelCommandIds.Host"),
                new MxFairyGuiCodeExpressionSpec("character_test.network.join", "CharacterTestFairyGuiNetworkPanelCommandIds.Join"),
                new MxFairyGuiCodeExpressionSpec("character_test.network.disconnect", "CharacterTestFairyGuiNetworkPanelCommandIds.Disconnect"),
                new MxFairyGuiCodeExpressionSpec("character_test.network.ready", "CharacterTestFairyGuiNetworkPanelCommandIds.Ready"),
                new MxFairyGuiCodeExpressionSpec("character_test.network.enter_game", "CharacterTestFairyGuiNetworkPanelCommandIds.EnterGame"),
                new MxFairyGuiCodeExpressionSpec("character_test.network.auto_sync_probe", "CharacterTestFairyGuiNetworkPanelCommandIds.AutoSyncProbe"),
                new MxFairyGuiCodeExpressionSpec("character_test.network.run_sync_probe", "CharacterTestFairyGuiNetworkPanelCommandIds.RunSyncProbe"),
                new MxFairyGuiCodeExpressionSpec("character_test.network.close", "CharacterTestFairyGuiNetworkPanelCommandIds.Close")
            };
        }

        private static MxFairyGuiCodeExpressionSpec[] CharacterTestMainMenuControlNameExpressions()
        {
            return new[]
            {
                new MxFairyGuiCodeExpressionSpec("overlay", "CharacterTestFairyGuiMainMenuIds.Overlay"),
                new MxFairyGuiCodeExpressionSpec("panelFrame", "CharacterTestFairyGuiMainMenuIds.PanelFrame"),
                new MxFairyGuiCodeExpressionSpec("panelTrim", "CharacterTestFairyGuiMainMenuIds.PanelTrim"),
                new MxFairyGuiCodeExpressionSpec("title", "CharacterTestFairyGuiMainMenuIds.Title"),
                new MxFairyGuiCodeExpressionSpec("status", "CharacterTestFairyGuiMainMenuIds.Status"),
                new MxFairyGuiCodeExpressionSpec("message", "CharacterTestFairyGuiMainMenuIds.Message"),
                new MxFairyGuiCodeExpressionSpec("btnStart", "CharacterTestFairyGuiMainMenuIds.Start"),
                new MxFairyGuiCodeExpressionSpec("btnNetwork", "CharacterTestFairyGuiMainMenuIds.Network"),
                new MxFairyGuiCodeExpressionSpec("btnAbilities", "CharacterTestFairyGuiMainMenuIds.Abilities"),
                new MxFairyGuiCodeExpressionSpec("btnBehaviorDeck", "CharacterTestFairyGuiMainMenuIds.BehaviorDeck"),
                new MxFairyGuiCodeExpressionSpec("btnSettings", "CharacterTestFairyGuiMainMenuIds.Settings"),
                new MxFairyGuiCodeExpressionSpec("btnRestart", "CharacterTestFairyGuiMainMenuIds.Restart"),
                new MxFairyGuiCodeExpressionSpec("btnClose", "CharacterTestFairyGuiMainMenuIds.Close")
            };
        }

        private static MxFairyGuiCodeExpressionSpec[] CharacterTestMainMenuCommandIdExpressions()
        {
            return new[]
            {
                new MxFairyGuiCodeExpressionSpec("character_test.main_menu.start", "CharacterTestFairyGuiMainMenuCommandIds.Start"),
                new MxFairyGuiCodeExpressionSpec("character_test.main_menu.network", "CharacterTestFairyGuiMainMenuCommandIds.Network"),
                new MxFairyGuiCodeExpressionSpec("character_test.main_menu.abilities", "CharacterTestFairyGuiMainMenuCommandIds.AbilitiesToggle"),
                new MxFairyGuiCodeExpressionSpec("character_test.main_menu.behavior_deck", "CharacterTestFairyGuiMainMenuCommandIds.BehaviorDeckToggle"),
                new MxFairyGuiCodeExpressionSpec("character_test.main_menu.settings", "CharacterTestFairyGuiMainMenuCommandIds.SettingsToggle"),
                new MxFairyGuiCodeExpressionSpec("character_test.main_menu.restart_offline", "CharacterTestFairyGuiMainMenuCommandIds.RestartOffline"),
                new MxFairyGuiCodeExpressionSpec("character_test.main_menu.close", "CharacterTestFairyGuiMainMenuCommandIds.Close")
            };
        }

        private static MxFairyGuiCodeExpressionSpec[] CharacterTestAbilityBindingPanelControlNameExpressions()
        {
            var values = new List<MxFairyGuiCodeExpressionSpec>
            {
                new MxFairyGuiCodeExpressionSpec("title", "CharacterTestFairyGuiAbilityBindingPanelIds.Title"),
                new MxFairyGuiCodeExpressionSpec("status", "CharacterTestFairyGuiAbilityBindingPanelIds.Status"),
                new MxFairyGuiCodeExpressionSpec("message", "CharacterTestFairyGuiAbilityBindingPanelIds.Message"),
                new MxFairyGuiCodeExpressionSpec("selectedSlotLabel", "CharacterTestFairyGuiAbilityBindingPanelIds.SelectedSlotLabel"),
                new MxFairyGuiCodeExpressionSpec("slotsHeader", "CharacterTestFairyGuiAbilityBindingPanelIds.SlotsHeader"),
                new MxFairyGuiCodeExpressionSpec("abilitiesHeader", "CharacterTestFairyGuiAbilityBindingPanelIds.AbilitiesHeader"),
                new MxFairyGuiCodeExpressionSpec("actionsHeader", "CharacterTestFairyGuiAbilityBindingPanelIds.ActionsHeader")
            };
            for (int slot = 1; slot <= 9; slot++)
                values.Add(new MxFairyGuiCodeExpressionSpec("btnSlot" + slot, "CharacterTestFairyGuiAbilityBindingPanelIds.SlotButton(" + slot + ")"));
            for (int i = 0; i < 10; i++)
                values.Add(new MxFairyGuiCodeExpressionSpec("btnAbility" + i, "CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(" + i + ")"));
            values.Add(new MxFairyGuiCodeExpressionSpec("btnClear", "CharacterTestFairyGuiAbilityBindingPanelIds.Clear"));
            values.Add(new MxFairyGuiCodeExpressionSpec("btnReset", "CharacterTestFairyGuiAbilityBindingPanelIds.ResetDefaults"));
            values.Add(new MxFairyGuiCodeExpressionSpec("btnSave", "CharacterTestFairyGuiAbilityBindingPanelIds.Save"));
            values.Add(new MxFairyGuiCodeExpressionSpec("btnCancel", "CharacterTestFairyGuiAbilityBindingPanelIds.Cancel"));
            return values.ToArray();
        }

        private static MxFairyGuiCodeExpressionSpec[] CharacterTestAbilityBindingPanelCommandIdExpressions()
        {
            return new[]
            {
                new MxFairyGuiCodeExpressionSpec("character_test.abilities.select_slot", "CharacterTestFairyGuiAbilityBindingPanelCommandIds.SelectSlot"),
                new MxFairyGuiCodeExpressionSpec("character_test.abilities.select_ability", "CharacterTestFairyGuiAbilityBindingPanelCommandIds.SelectAbility"),
                new MxFairyGuiCodeExpressionSpec("character_test.abilities.clear_slot", "CharacterTestFairyGuiAbilityBindingPanelCommandIds.ClearSlot"),
                new MxFairyGuiCodeExpressionSpec("character_test.abilities.reset_defaults", "CharacterTestFairyGuiAbilityBindingPanelCommandIds.ResetDefaults"),
                new MxFairyGuiCodeExpressionSpec("character_test.abilities.save", "CharacterTestFairyGuiAbilityBindingPanelCommandIds.Save"),
                new MxFairyGuiCodeExpressionSpec("character_test.abilities.cancel", "CharacterTestFairyGuiAbilityBindingPanelCommandIds.Cancel")
            };
        }

        private static MxFairyGuiCodeExpressionSpec[] CharacterTestBehaviorDeckPanelControlNameExpressions()
        {
            var values = new List<MxFairyGuiCodeExpressionSpec>
            {
                new MxFairyGuiCodeExpressionSpec("background", "\"background\""),
                new MxFairyGuiCodeExpressionSpec("headerBand", "\"headerBand\""),
                new MxFairyGuiCodeExpressionSpec("title", "CharacterTestFairyGuiBehaviorDeckPanelIds.Title"),
                new MxFairyGuiCodeExpressionSpec("character", "CharacterTestFairyGuiBehaviorDeckPanelIds.Character"),
                new MxFairyGuiCodeExpressionSpec("btnPreviousCharacter", "CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousCharacter"),
                new MxFairyGuiCodeExpressionSpec("btnNextCharacter", "CharacterTestFairyGuiBehaviorDeckPanelIds.NextCharacter"),
                new MxFairyGuiCodeExpressionSpec("budget", "CharacterTestFairyGuiBehaviorDeckPanelIds.Budget"),
                new MxFairyGuiCodeExpressionSpec("costBudget", "CharacterTestFairyGuiBehaviorDeckPanelIds.CostBudget"),
                new MxFairyGuiCodeExpressionSpec("lockStatus", "CharacterTestFairyGuiBehaviorDeckPanelIds.LockStatus"),
                new MxFairyGuiCodeExpressionSpec("btnPreviewRecommendedPreset", "CharacterTestFairyGuiBehaviorDeckPanelIds.PreviewRecommendedPreset"),
                new MxFairyGuiCodeExpressionSpec("btnUndoRecommendedPreset", "CharacterTestFairyGuiBehaviorDeckPanelIds.UndoRecommendedPreset"),
                new MxFairyGuiCodeExpressionSpec("btnContinueToSetup", "CharacterTestFairyGuiBehaviorDeckPanelIds.ContinueToSetup"),
                new MxFairyGuiCodeExpressionSpec("btnClose", "CharacterTestFairyGuiBehaviorDeckPanelIds.Close"),
                new MxFairyGuiCodeExpressionSpec("libraryPanel", "\"libraryPanel\""),
                new MxFairyGuiCodeExpressionSpec("libraryHeader", "CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryHeader"),
                new MxFairyGuiCodeExpressionSpec("librarySummary", "CharacterTestFairyGuiBehaviorDeckPanelIds.LibrarySummary"),
                new MxFairyGuiCodeExpressionSpec("pageStatus", "CharacterTestFairyGuiBehaviorDeckPanelIds.PageStatus"),
                new MxFairyGuiCodeExpressionSpec("btnPreviousPage", "CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousPage"),
                new MxFairyGuiCodeExpressionSpec("btnNextPage", "CharacterTestFairyGuiBehaviorDeckPanelIds.NextPage")
            };
            for (int i = 0; i < 8; i++)
                values.Add(new MxFairyGuiCodeExpressionSpec("libraryCard" + i, "CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryCard(" + i + ")"));
            values.Add(new MxFairyGuiCodeExpressionSpec("deckPanel", "\"deckPanel\""));
            values.Add(new MxFairyGuiCodeExpressionSpec("deckHeader", "CharacterTestFairyGuiBehaviorDeckPanelIds.DeckHeader"));
            values.Add(new MxFairyGuiCodeExpressionSpec("deckSummary", "CharacterTestFairyGuiBehaviorDeckPanelIds.DeckSummary"));
            values.Add(new MxFairyGuiCodeExpressionSpec("btnAddCard", "CharacterTestFairyGuiBehaviorDeckPanelIds.AddCard"));
            for (int i = 0; i < 6; i++)
                values.Add(new MxFairyGuiCodeExpressionSpec("deckCard" + i, "CharacterTestFairyGuiBehaviorDeckPanelIds.DeckCard(" + i + ")"));
            values.Add(new MxFairyGuiCodeExpressionSpec("footerBand", "\"footerBand\""));
            values.Add(new MxFairyGuiCodeExpressionSpec("diagnostics", "CharacterTestFairyGuiBehaviorDeckPanelIds.Diagnostics"));
            values.Add(new MxFairyGuiCodeExpressionSpec("commandStatus", "CharacterTestFairyGuiBehaviorDeckPanelIds.CommandStatus"));
            values.Add(new MxFairyGuiCodeExpressionSpec("detailsBackdrop", "CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsBackdrop"));
            values.Add(new MxFairyGuiCodeExpressionSpec("detailsTitle", "CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsTitle"));
            values.Add(new MxFairyGuiCodeExpressionSpec("detailsBody", "CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsBody"));
            values.Add(new MxFairyGuiCodeExpressionSpec("btnCloseDetails", "CharacterTestFairyGuiBehaviorDeckPanelIds.CloseDetails"));
            values.Add(new MxFairyGuiCodeExpressionSpec("btnPreviousDetailsPage", "CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousDetailsPage"));
            values.Add(new MxFairyGuiCodeExpressionSpec("detailsPageStatus", "CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsPageStatus"));
            values.Add(new MxFairyGuiCodeExpressionSpec("btnNextDetailsPage", "CharacterTestFairyGuiBehaviorDeckPanelIds.NextDetailsPage"));
            values.Add(new MxFairyGuiCodeExpressionSpec("presetBackdrop", "CharacterTestFairyGuiBehaviorDeckPanelIds.PresetBackdrop"));
            values.Add(new MxFairyGuiCodeExpressionSpec("presetPanel", "CharacterTestFairyGuiBehaviorDeckPanelIds.PresetPanel"));
            values.Add(new MxFairyGuiCodeExpressionSpec("presetTitle", "CharacterTestFairyGuiBehaviorDeckPanelIds.PresetTitle"));
            values.Add(new MxFairyGuiCodeExpressionSpec("presetSummary", "CharacterTestFairyGuiBehaviorDeckPanelIds.PresetSummary"));
            values.Add(new MxFairyGuiCodeExpressionSpec("presetSourceBudget", "CharacterTestFairyGuiBehaviorDeckPanelIds.PresetSourceBudget"));
            values.Add(new MxFairyGuiCodeExpressionSpec("presetCandidateBudget", "CharacterTestFairyGuiBehaviorDeckPanelIds.PresetCandidateBudget"));
            values.Add(new MxFairyGuiCodeExpressionSpec("presetCostDelta", "CharacterTestFairyGuiBehaviorDeckPanelIds.PresetCostDelta"));
            values.Add(new MxFairyGuiCodeExpressionSpec("presetDiagnostics", "CharacterTestFairyGuiBehaviorDeckPanelIds.PresetDiagnostics"));
            for (int i = 0; i < 6; i++)
            {
                values.Add(new MxFairyGuiCodeExpressionSpec("presetAdded" + i, "CharacterTestFairyGuiBehaviorDeckPanelIds.PresetAddedRow(" + i + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("presetRetained" + i, "CharacterTestFairyGuiBehaviorDeckPanelIds.PresetRetainedRow(" + i + ")"));
                values.Add(new MxFairyGuiCodeExpressionSpec("presetRemoved" + i, "CharacterTestFairyGuiBehaviorDeckPanelIds.PresetRemovedRow(" + i + ")"));
            }
            values.Add(new MxFairyGuiCodeExpressionSpec("btnCancelRecommendedPreset", "CharacterTestFairyGuiBehaviorDeckPanelIds.CancelRecommendedPreset"));
            values.Add(new MxFairyGuiCodeExpressionSpec("btnConfirmRecommendedPreset", "CharacterTestFairyGuiBehaviorDeckPanelIds.ConfirmRecommendedPreset"));
            return values.ToArray();
        }

        private static MxFairyGuiCodeExpressionSpec[] CharacterTestBehaviorDeckPanelCommandIdExpressions()
        {
            return new[]
            {
                new MxFairyGuiCodeExpressionSpec("character_test.runtime_ai_planner.behavior_deck.close", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.Close"),
                new MxFairyGuiCodeExpressionSpec("character_test.runtime_ai_planner.behavior_deck.previous_page", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviousPage"),
                new MxFairyGuiCodeExpressionSpec("character_test.runtime_ai_planner.behavior_deck.next_page", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.NextPage"),
                new MxFairyGuiCodeExpressionSpec("character_test.runtime_ai_planner.behavior_deck.previous_character", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviousCharacter"),
                new MxFairyGuiCodeExpressionSpec("character_test.runtime_ai_planner.behavior_deck.next_character", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.NextCharacter"),
                new MxFairyGuiCodeExpressionSpec("character_test.runtime_ai_planner.behavior_deck.toggle_install_selection", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ToggleInstallSelection"),
                new MxFairyGuiCodeExpressionSpec("character_test.runtime_ai_planner.behavior_deck.preview_recommended_preset", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviewRecommendedPreset"),
                new MxFairyGuiCodeExpressionSpec("character_test.runtime_ai_planner.behavior_deck.undo_recommended_preset", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.UndoRecommendedPreset"),
                new MxFairyGuiCodeExpressionSpec("character_test.runtime_ai_planner.behavior_deck.continue_to_setup", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ContinueToSetup"),
                new MxFairyGuiCodeExpressionSpec("character_test.runtime_ai_planner.behavior_deck.close_details", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CloseDetails"),
                new MxFairyGuiCodeExpressionSpec("character_test.runtime_ai_planner.behavior_deck.previous_details_page", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviousDetailsPage"),
                new MxFairyGuiCodeExpressionSpec("character_test.runtime_ai_planner.behavior_deck.next_details_page", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.NextDetailsPage"),
                new MxFairyGuiCodeExpressionSpec("character_test.runtime_ai_planner.behavior_deck.cancel_recommended_preset", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CancelRecommendedPreset"),
                new MxFairyGuiCodeExpressionSpec("character_test.runtime_ai_planner.behavior_deck.confirm_recommended_preset", "CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ConfirmRecommendedPreset")
            };
        }

        private static MxFairyGuiCodeExpressionSpec[] CharacterTestStoryDialogControlNameExpressions()
        {
            return new[]
            {
                new MxFairyGuiCodeExpressionSpec("title", "CharacterTestFairyGuiStoryDialogIds.Title"),
                new MxFairyGuiCodeExpressionSpec("phase", "CharacterTestFairyGuiStoryDialogIds.Phase"),
                new MxFairyGuiCodeExpressionSpec("dialogueText", "CharacterTestFairyGuiStoryDialogIds.DialogueText"),
                new MxFairyGuiCodeExpressionSpec("choiceText", "CharacterTestFairyGuiStoryDialogIds.ChoiceText"),
                new MxFairyGuiCodeExpressionSpec("signalText", "CharacterTestFairyGuiStoryDialogIds.SignalText"),
                new MxFairyGuiCodeExpressionSpec("eventLog", "CharacterTestFairyGuiStoryDialogIds.EventLog"),
                new MxFairyGuiCodeExpressionSpec("btnContinue", "CharacterTestFairyGuiStoryDialogIds.Continue"),
                new MxFairyGuiCodeExpressionSpec("btnChoice", "CharacterTestFairyGuiStoryDialogIds.Choice")
            };
        }

        private static MxFairyGuiCodeExpressionSpec[] CharacterTestStoryDialogCommandIdExpressions()
        {
            return new[]
            {
                new MxFairyGuiCodeExpressionSpec("character_test.story.dialog.continue", "CharacterTestFairyGuiStoryCommandIds.CompletePresentation"),
                new MxFairyGuiCodeExpressionSpec("character_test.story.dialog.selectChoice", "CharacterTestFairyGuiStoryCommandIds.SelectChoice")
            };
        }

        private static string FindRepositoryRoot()
        {
            string path = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(path))
            {
                if (File.Exists(Path.Combine(path, "FGUIProject", "FGUIProject.fairy")))
                    return path;

                path = Directory.GetParent(path)?.FullName;
            }

            throw new InvalidOperationException("Repository root was not found.");
        }

        private static MxFairyGuiManifestDiagnostic Find(MxFairyGuiManifestValidationResult result, MxFairyGuiManifestDiagnosticCode code)
        {
            for (int i = 0; i < result.Diagnostics.Count; i++)
            {
                if (result.Diagnostics[i].Code == code)
                    return result.Diagnostics[i];
            }

            throw new InvalidOperationException("Diagnostic not found: " + code + ".");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private readonly struct ManifestTarget
        {
            public ManifestTarget(
                string name,
                MxFairyGuiManifestGenerationSpec spec,
                string generatedSourcePath)
                : this(name, spec, generatedSourcePath, null, string.Empty)
            {
            }

            public ManifestTarget(
                string name,
                MxFairyGuiManifestGenerationSpec spec,
                string generatedSourcePath,
                MxFairyGuiBinderGenerationSpec binderSpec,
                string generatedBinderSourcePath)
            {
                Name = name ?? string.Empty;
                Spec = spec;
                GeneratedSourcePath = generatedSourcePath ?? string.Empty;
                BinderSpec = binderSpec;
                GeneratedBinderSourcePath = generatedBinderSourcePath ?? string.Empty;
            }

            public string Name { get; }
            public MxFairyGuiManifestGenerationSpec Spec { get; }
            public string GeneratedSourcePath { get; }
            public MxFairyGuiBinderGenerationSpec BinderSpec { get; }
            public string GeneratedBinderSourcePath { get; }
            public bool HasBinder => BinderSpec != null && !string.IsNullOrWhiteSpace(GeneratedBinderSourcePath);
        }
    }
}
