using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Authoring.FairyGuiArtSlices;
using MxFramework.Authoring.UiComposer;

namespace MxFramework.Authoring.Tests;

internal static class FairyGuiArtSliceAuthoringTests
{
    public static void RunAll()
    {
        UiArtResourceLibrary_LoadsMxUiFoundationSeed();
        CompactIconButton_MaterializesCenteredGlyph();
        RuntimeAiLabTimelineRecipe_RemovesSourceLessStaticHistory();
        UiComposer_LoadsRuntimeAiLabShellBlueprint();
        UiArtFairyGuiIntegration_ListsPackagesAndBuildsStableRecipe();
        UiArtFairyGuiIntegration_DetectsImportAndUsageDrift();
        UiArtFairyGuiIntegration_BlocksEscapingPackageImagePath();
        UiArtResourceLibrary_BlocksEscapingResourceRoot();
        UiArtResourceSave_ValidatesBeforeWriting();
        UiArtResourceSave_BlocksStaleHashWithoutWriting();
        ArtSliceState_LoadsCharacterTestPackage();
        SaveCatalogPlan_WritesOnlyCatalog();
        UiArtResourceCreate_WritesPngAndCatalogOnly();
        UiArtResourceCreate_BlockedExecutionLeavesFilesUnchanged();
        UiArtResourceCreate_AcceptsOpaqueResourcePolicy();
        UiArtResourceReplacementPlan_UsesLibraryConstraints();
        UiArtResourceReplacement_WritesOnlyTargetPng();
        ReplacementPlan_BlocksStaleHashAndInvalidPng();
        ReplacementPlan_AcceptsSamePathValidPng();
        DraftPlan_OnlyTargetsSidecarCatalog();
    }

    public static void RunRuntimeAiLabComposerPreflight()
    {
        RuntimeAiLabTimelineRecipe_RemovesSourceLessStaticHistory();
        string root = FindRepoRoot();
        const string screenId = "ui.screen.character_test.runtime_ai_lab";
        UiComposerAuthoringState state = UiComposerService.ReadState(root, screenId);
        Require(
            state.ErrorCount == 0,
            "Runtime AI Lab project-catalog UI should satisfy its binding contract: "
            + string.Join(" | ", state.Diagnostics.Select(item => item.Code + " " + item.Message)));
        string[] requiredNodes =
        {
            "behaviorCardLibrary",
            "behaviorCardTimeline",
            "workbenchEntryList",
            "btnReloadProjectDraft",
            "btnReapplyProjectDraft",
            "projectCardMetadataPanel",
            "projectCardMetadataTitle",
            "inputProjectCardDisplayName",
            "inputProjectCardCost",
            "inputProjectCardDescription",
            "inputProjectCardUseSummary",
            "inputProjectCardRiskSummary",
            "inputProjectCardTags",
            "btnPublishDraft",
            "btnSaveBehaviorGroup"
        };
        for (int i = 0; i < requiredNodes.Length; i++)
        {
            string required = requiredNodes[i];
            Require(
                state.Blueprint.Nodes.Any(node => node.Name == required),
                "Runtime AI Lab project-catalog UI is missing " + required + ".");
        }
        UiComposerOperationPlan plan = UiComposerService.PlanApply(
            root,
            screenId,
            new UiComposerExpectedHashes
            {
                BlueprintHash = state.BlueprintHash,
                RecipeCatalogHash = state.RecipeCatalogHash,
                CatalogHash = state.CatalogHash,
                ContractHash = state.ContractHash,
                SourceXmlHash = state.SourceXmlHash,
                MappingHash = state.MappingHash
            });
        Require(!plan.Blocked, "Runtime AI Lab project-catalog UI should produce a deterministic dry-run Apply plan.");
        Require(
            plan.Materialization?.PanelComponentXmlTemplate?.Contains(
                "name=\"btnReloadProjectDraft\"",
                StringComparison.Ordinal) == true
            && plan.Materialization.PanelComponentXmlTemplate.Contains(
                "name=\"btnReapplyProjectDraft\"",
                StringComparison.Ordinal),
            "Runtime AI Lab Apply materialization should include both explicit project conflict actions.");
        Require(
            plan.Materialization.PanelComponentXmlTemplate.Contains(
                "<list",
                StringComparison.Ordinal)
            && plan.Materialization.PanelComponentXmlTemplate.Contains(
                "name=\"workbenchEntryList\"",
                StringComparison.Ordinal)
            && plan.Materialization.PanelComponentXmlTemplate.Contains(
                "defaultItem=",
                StringComparison.Ordinal)
            && !plan.Materialization.PanelComponentXmlTemplate.Contains(
                "btnWorkbenchEntry",
                StringComparison.Ordinal),
            "Runtime AI Lab Apply materialization should use one native behavior-card List.");
        UiComposerMaterializedComponent behaviorCard = plan.Materialization.Components.FirstOrDefault(
            item => item.ComponentRecipeId == "ui.component.runtime_ai.behavior_card");
        Require(
            behaviorCard?.XmlTemplate?.Contains("name=\"inlineToggleEntry\"", StringComparison.Ordinal) == true
            && behaviorCard.XmlTemplate.Contains("name=\"inlineDeleteEntry\"", StringComparison.Ordinal),
            "Runtime AI Lab behavior-card materialization should own its selected-item actions.");
        Require(
            plan.Materialization.PanelComponentXmlTemplate.Contains(
                "name=\"projectCardMetadataPanel\"",
                StringComparison.Ordinal)
            && plan.Materialization.PanelComponentXmlTemplate.Contains(
                "name=\"inputProjectCardDisplayName\"",
                StringComparison.Ordinal)
            && plan.Materialization.PanelComponentXmlTemplate.Contains(
                "name=\"inputProjectCardCost\"",
                StringComparison.Ordinal),
            "Runtime AI Lab Apply materialization should include the project-card metadata editor.");
    }

    private static void RuntimeAiLabTimelineRecipe_RemovesSourceLessStaticHistory()
    {
        string root = FindRepoRoot();
        const string screenId = "ui.screen.character_test.runtime_ai_lab";
        UiComposerAuthoringState state = UiComposerService.ReadState(root, screenId);
        UiComposerOperationPlan plan = UiComposerService.PlanApply(root, screenId, new UiComposerExpectedHashes
        {
            BlueprintHash = state.BlueprintHash,
            RecipeCatalogHash = state.RecipeCatalogHash,
            CatalogHash = state.CatalogHash,
            ContractHash = state.ContractHash,
            SourceXmlHash = state.SourceXmlHash,
            MappingHash = state.MappingHash
        });
        Require(!plan.Blocked, "Runtime AI Lab timeline recipe should produce a deterministic materialization plan.");
        UiComposerMaterializedComponent timeline = FindMaterializedComponent(
            plan.Materialization,
            "ui.component.runtime_ai.timeline");
        UiComposerMaterializedComponent viewport = FindMaterializedComponent(
            plan.Materialization,
            "ui.component.runtime_ai.timeline_viewport");
        Require(timeline != null, "Runtime AI Lab timeline recipe should materialize.");
        Require(viewport != null, "Runtime AI Lab five-track viewport should materialize independently.");
        Require(timeline.XmlTemplate.Contains("name=\"runtimeFrameFiveTrackViewport\"", StringComparison.Ordinal), "Live RuntimeFrame shell should embed the reusable five-track viewport.");
        Require(viewport.XmlTemplate.Contains("name=\"markerPrototype\"", StringComparison.Ordinal)
            && viewport.XmlTemplate.Contains("name=\"spanPrototype\"", StringComparison.Ordinal)
            && viewport.XmlTemplate.Contains("name=\"tickPrototype\"", StringComparison.Ordinal), "Five-track viewport should retain hidden dynamic item prototypes.");
        Require(!timeline.XmlTemplate.Contains("name=\"timelineRevisionTrackText\"", StringComparison.Ordinal)
            && !timeline.XmlTemplate.Contains("name=\"timelineDecisionTrackText\"", StringComparison.Ordinal)
            && !timeline.XmlTemplate.Contains("name=\"timelineWarningTrackText\"", StringComparison.Ordinal), "Live shell must not duplicate track labels owned by the viewport tracks.");
        Require(!viewport.XmlTemplate.Contains("name=\"timelineMarkerRevision\"", StringComparison.Ordinal)
            && !viewport.XmlTemplate.Contains("name=\"revisionSpan\"", StringComparison.Ordinal)
            && !viewport.XmlTemplate.Contains("name=\"warningSpan\"", StringComparison.Ordinal), "Five-track viewport must not materialize source-less history examples.");
    }

    private static void ArtSliceState_LoadsCharacterTestPackage()
    {
        string root = FindRepoRoot();
        FairyGuiArtSliceAuthoringState state = FairyGuiArtSliceCatalogService.ReadAuthoringState(root, "MxCharacterTestHud");
        Require(state.PackageId == "MxCharacterTestHud", "Art slice state should load CharacterTest package.");
        Require(state.PackageImages.Count == 51, "Art slice state should include package image inventory.");
        Require(state.Catalog != null && state.Catalog.Slices.Length > 0, "Art slice state should include catalog slices.");
        Require(!string.IsNullOrWhiteSpace(state.CatalogHash), "Art slice state should expose catalog hash.");
    }

    private static void UiArtResourceLibrary_LoadsMxUiFoundationSeed()
    {
        string root = FindRepoRoot();
        UiArtResourceLibraryAuthoringState state = UiArtResourceLibraryService.ReadAuthoringState(root, "MxUiFoundation");
        Require(state.LibraryId == "MxUiFoundation", "UI art resource library should load MxUiFoundation.");
        Require(state.Catalog != null && state.Catalog.Resources.Length == 86, "MxUiFoundation should contain the demo seed, player UI, and Runtime AI Lab foundation resources.");
        Require(state.ErrorCount == 0, "MxUiFoundation seed should not have blocking diagnostics.");
        Require(!string.IsNullOrWhiteSpace(state.CatalogHash), "UI art resource library should expose catalog hash.");
        Require(
            Array.Exists(
                state.Catalog.Resources,
                item => item.ResourceId == "ui.icon.minimal.search"),
            "MxUiFoundation should include the Runtime AI Lab search icon resource.");

        UiArtResource resource = state.Catalog.Resources[0];
        Require(resource.ResourceId == "ui.button.generic.base", "Seed resource id should be stable.");
        Require(resource.RecommendedFairyGuiUsage != null, "Seed resource should include recommended FairyGUI usage.");
        Require(resource.RecommendedFairyGuiUsage.DesignSize != null, "Seed resource should include a design size.");
        Require(resource.RecommendedFairyGuiUsage.DesignSize.Width == 240, "Seed resource design width should be 240.");
        Require(resource.RecommendedFairyGuiUsage.DesignSize.Height == 64, "Seed resource design height should be 64.");
        Require(string.Equals(resource.RecommendedFairyGuiUsage.Policy, "NineSlice", StringComparison.Ordinal), "Seed resource should recommend NineSlice usage.");

        string absolutePngPath = Path.Combine(root, state.ResourceRoot.Replace('/', Path.DirectorySeparatorChar), resource.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Require(File.Exists(absolutePngPath), "Seed resource PNG should exist.");
        Require(IsPathAtOrUnderRoot(Path.Combine(root, state.ResourceRoot.Replace('/', Path.DirectorySeparatorChar)), absolutePngPath), "Seed resource PNG should stay inside the resource library root.");

        FairyGuiArtSlicePngInfo png = FairyGuiArtSliceCatalogService.ReadPngInfo(absolutePngPath);
        Require(png.Valid, "Seed resource PNG metadata should be readable.");
        Require(png.Width == 240 && png.Height == 64, "Seed resource PNG should be 240x64.");
        Require(png.HasAlphaChannel, "Seed resource PNG should include an alpha channel.");

        UiArtResource toolButton = FindResource(state, "ui.button.tool.base");
        Require(toolButton.RecommendedFairyGuiUsage.Policy == "NineSlice", "Tool buttons should use deterministic NineSlice semantics.");
        Require(toolButton.RecommendedFairyGuiUsage.DesignSize.Width == 192 && toolButton.RecommendedFairyGuiUsage.DesignSize.Height == 40, "Tool button source size should remain stable.");
        Require(toolButton.RecommendedFairyGuiUsage.Scale9Grid.X == 20 && toolButton.RecommendedFairyGuiUsage.Scale9Grid.Width == 152, "Tool button horizontal stretch region should remain stable.");

        UiArtResource workbench = FindResource(state, "ui.panel.workbench.frame");
        Require(workbench.RecommendedFairyGuiUsage.Policy == "NineSlice", "Workbench panels should use NineSlice semantics.");
        Require(workbench.Replacement.AllowStretch, "Workbench panels should explicitly allow resizing.");

        UiArtResource timelineMarker = FindResource(state, "ui.timeline.marker.base");
        Require(timelineMarker.RecommendedFairyGuiUsage.Policy == "PreserveAspect", "Timeline markers should preserve aspect ratio.");
        Require(!timelineMarker.Replacement.AllowStretch, "Timeline markers must not be stretched.");

        string resourceRoot = Path.Combine(root, state.ResourceRoot.Replace('/', Path.DirectorySeparatorChar));
        foreach (string resourceId in new[] { "ui.button.tool.base", "ui.panel.workbench.frame", "ui.flow.node.action.frame", "ui.flow.node.condition.frame", "ui.flow.node.terminal.frame", "ui.timeline.span.base", "ui.timeline.marker.base", "ui.timeline.event.detail.frame", "ui.drag.handle.horizontal" })
        {
            UiArtResource labResource = FindResource(state, resourceId);
            string pngPath = Path.Combine(resourceRoot, labResource.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            FairyGuiArtSlicePngInfo labPng = FairyGuiArtSliceCatalogService.ReadPngInfo(pngPath);
            Require(labPng.Valid && labPng.HasAlphaChannel && labPng.HasNonOpaqueAlpha, "Runtime AI Lab resource should be readable RGBA art: " + resourceId);
            Require(labPng.Width == labResource.RecommendedFairyGuiUsage.DesignSize.Width && labPng.Height == labResource.RecommendedFairyGuiUsage.DesignSize.Height, "Runtime AI Lab resource dimensions should match catalog semantics: " + resourceId);
        }

        UiComposerAuthoringState composer = UiComposerService.ReadState(root, UiComposerService.DemoScreenId);
        UiComponentRecipe toolRecipe = FindRecipe(composer.ComponentRecipes, "ui.component.button.tool.neutral");
        Require(toolRecipe.DefaultSize.Width == 160 && toolRecipe.DefaultSize.Height == 32, "Tool-button recipes should use the compact workbench default size.");
        Require(toolRecipe.ResourceSlots.Length == 1 && toolRecipe.ResourceSlots[0].ResourceId == "ui.button.tool.base", "Tool-button recipes should reference the dedicated semantic base resource.");
        Require(toolRecipe.StatePresentation.TitleFontSize == 12, "Tool-button typography should remain workbench-dense.");
    }

    public static void CompactIconButton_MaterializesCenteredGlyph()
    {
        string root = FindRepoRoot();
        UiComposerAuthoringState composer = UiComposerService.ReadState(root, UiComposerService.DemoScreenId);
        UiComponentRecipe iconRecipe = FindRecipe(composer.ComponentRecipes, "ui.component.button.icon.compact.neutral");
        Require(iconRecipe.DefaultSize.Width == 52 && iconRecipe.DefaultSize.Height == 52, "Compact icon buttons should expose a stable 52x52px hit area.");
        Require(iconRecipe.StatePresentation.IconOnly, "Compact icon buttons should use icon-only centering semantics.");
        Require(iconRecipe.StatePresentation.IconSize == 30, "Compact icon buttons should render a stable 30px glyph.");

        UiComposerAuthoringState behaviorDeck = UiComposerService.ReadState(root, "ui.screen.character_test.behavior_deck");
        UiComposerOperationPlan behaviorDeckPlan = UiComposerService.PlanApply(
            root,
            behaviorDeck.Screen.ScreenId,
            new UiComposerExpectedHashes
            {
                BlueprintHash = behaviorDeck.BlueprintHash,
                RecipeCatalogHash = behaviorDeck.RecipeCatalogHash,
                CatalogHash = behaviorDeck.CatalogHash,
                ContractHash = behaviorDeck.ContractHash,
                SourceXmlHash = behaviorDeck.SourceXmlHash,
                MappingHash = behaviorDeck.MappingHash
            });
        Require(!behaviorDeckPlan.Blocked, "Behavior deck should remain applyable after compact icon-button migration.");
        UiComposerMaterializedComponent iconMaterialization = behaviorDeckPlan.Materialization.Components.Single(
            item => item.ComponentRecipeId == "ui.component.button.icon.compact.neutral");
        Require(
            iconMaterialization.XmlTemplate.Contains("xy=\"11,11\" size=\"30,30\"", StringComparison.Ordinal),
            "Compact icon-button materialization should center its 30px glyph inside the 52x52px hit area.");
    }

    private static void UiComposer_LoadsRuntimeAiLabShellBlueprint()
    {
        string root = FindRepoRoot();
        const string screenId = "ui.screen.character_test.runtime_ai_lab";
        UiComposerAuthoringState state = UiComposerService.ReadState(root, screenId);
        Require(
            state.ErrorCount == 0,
            "Runtime AI Lab shell blueprint should satisfy its binding contract: "
            + string.Join(" | ", state.Diagnostics.Select(item => item.Code + " " + item.Message)));
        Require(state.Blueprint.SchemaVersion == "mx.uiScreenBlueprint.v3", "Runtime AI Lab shell should use layout containers.");
        Require(state.Blueprint.Containers.Length == 7, "Runtime AI Lab shell should expose the current stable workbench regions.");
        Require(state.Blueprint.Nodes.Length == 201, "Runtime AI Lab should expose setup behavior-group selection, card authoring, diagnostics, apply, and runtime controls without the retired behavior-group editor entry.");
        Require(state.BindingContract.Bindings.Length == 144, "Runtime AI Lab should bind the setup, unified behavior-group, and card-oriented behavior workspaces without retired root timeline bindings.");
        Require(state.BindingContract.ComponentBindings.Length == 20, "Runtime AI Lab should contract the controller-neutral five-track viewport separately from its live shell and authoring controls.");
        Require(state.Blueprint.Nodes.Any(node => node.Name == "behaviorCardLibrary"), "Runtime AI Lab should expose the Behavior Card library composite.");
        Require(state.Blueprint.Nodes.Any(node => node.Name == "behaviorCardTimeline"), "Runtime AI Lab should expose the card-oriented behavior timeline composite.");
        Require(!state.Blueprint.Nodes.Any(node => node.Name == "behaviorCardCompileView"), "Runtime AI Lab should not restore the retired compiled-card presenter.");
        Require(!state.Blueprint.Nodes.Any(node => node.Name == "gameplayCardEditor"), "Runtime AI Lab should not restore the retired gameplay-card editor.");
        UiScreenNode workspace = state.Blueprint.Nodes.Single(node => node.Name == "workspacePanel");
        UiScreenNode runtimeFrameWorkspace = state.Blueprint.Nodes.Single(node => node.Name == "runtimeFrameWorkspace");
        Require(workspace.Layout.X == 600 && workspace.Layout.Width == 1296, "Runtime AI Lab should reserve workspace panel at 1920x1080.");
        Require(runtimeFrameWorkspace.Layout.X == 0 && runtimeFrameWorkspace.Layout.Width == 1920 && runtimeFrameWorkspace.Layout.Height == 1020, "RuntimeFrame workspace should own the full fixed diagnostics canvas without runtime coordinate compensation.");
        Require(state.Screen.PackageId == "MxCharacterTestRuntimeAiLabUi", "Runtime AI Lab semantic work should target an isolated Composer-owned package.");
        Require(state.BindingContract.ViewId == "ui.character_test.runtime_ai_planner.dev", "Runtime AI Lab shell should preserve the existing runtime view id.");

        UiComposerOperationPlan plan = UiComposerService.PlanApply(root, screenId, new UiComposerExpectedHashes
        {
            BlueprintHash = state.BlueprintHash,
            RecipeCatalogHash = state.RecipeCatalogHash,
            CatalogHash = state.CatalogHash,
            ContractHash = state.ContractHash,
            SourceXmlHash = state.SourceXmlHash,
            MappingHash = state.MappingHash
        });
        Require(!plan.Blocked, "Runtime AI Lab shell should produce a deterministic dry-run apply plan.");
        Require(plan.Materialization != null && plan.Materialization.PackageId == "MxCharacterTestRuntimeAiLabUi", "Runtime AI Lab shell plan should remain isolated from the legacy package.");
        Require(ContainsMaterializedResource(plan.Materialization, "ui.panel.workbench.frame"), "Runtime AI Lab shell should materialize the workbench frame resource.");
        Require(ContainsMaterializedComponent(plan.Materialization, "ui.component.runtime_ai.timeline_span"), "Runtime AI Lab shell should materialize the pooled timeline span component.");
        Require(ContainsMaterializedComponent(plan.Materialization, "ui.component.button.tool.neutral"), "Runtime AI Lab shell should materialize the tool-button recipe.");
        UiComposerMaterializedComponent assetCard = FindMaterializedComponent(plan.Materialization, "ui.component.runtime_ai.behavior_asset_card.lab");
        UiComposerMaterializedComponent library = FindMaterializedComponent(plan.Materialization, "ui.component.runtime_ai.behavior_card_library");
        UiComposerMaterializedComponent behaviorTimeline = FindMaterializedComponent(plan.Materialization, "ui.component.runtime_ai.behavior_timeline");
        UiComposerMaterializedComponent setupTeamCard = FindMaterializedComponent(plan.Materialization, "ui.component.runtime_ai.setup_team_card");
        UiComposerMaterializedComponent setupGroupRow = FindMaterializedComponent(plan.Materialization, "ui.component.runtime_ai.setup_behavior_group_row_visual");
        UiComposerMaterializedComponent timeline = FindMaterializedComponent(plan.Materialization, "ui.component.runtime_ai.timeline");
        UiComposerMaterializedComponent timelineViewport = FindMaterializedComponent(plan.Materialization, "ui.component.runtime_ai.timeline_viewport");
        UiComposerMaterializedComponent timelineTrack = FindMaterializedComponent(plan.Materialization, "ui.component.runtime_ai.timeline_track");
        UiComposerMaterializedComponent timelineSpan = FindMaterializedComponent(plan.Materialization, "ui.component.runtime_ai.timeline_span");
        Require(assetCard != null && assetCard.XmlTemplate.Contains("name=\"btnAction\"", StringComparison.Ordinal), "Behavior-card library entries should expose one contextual card action.");
        Require(library != null && library.XmlTemplate.Contains("name=\"libraryQuery\"", StringComparison.Ordinal), "Behavior-card library should materialize query and filtering controls.");
        Require(behaviorTimeline != null && behaviorTimeline.XmlTemplate.Contains("name=\"btnTimelineCanonicalize\"", StringComparison.Ordinal), "Behavior timeline should materialize its card-authoring toolbar.");
        Require(behaviorTimeline.XmlTemplate.Contains("name=\"timelinePlayhead\"", StringComparison.Ordinal), "Behavior timeline should materialize the timeline playhead.");
        Require(setupGroupRow != null && setupGroupRow.XmlTemplate.Contains("name=\"name\"", StringComparison.Ordinal), "Setup behavior group row should materialize name.");
        Require(setupTeamCard != null && setupTeamCard.XmlTemplate.Contains("name=\"validation\"", StringComparison.Ordinal), "Setup workspace should materialize team summary cards.");
        Require(plan.Materialization.PanelComponentXmlTemplate.Contains("name=\"setupReadyBackground\"", StringComparison.Ordinal), "Setup workspace should materialize its ready-to-start surface.");
        Require(timeline != null && timeline.XmlTemplate.Contains("name=\"runtimeFrameFiveTrackViewport\"", StringComparison.Ordinal), "RuntimeFrame live shell should host the shared five-track viewport.");
        Require(timelineViewport != null && timelineViewport.XmlTemplate.Contains("name=\"trackWarnings\"", StringComparison.Ordinal), "Shared viewport should materialize the warning track exactly once.");
        Require(timelineTrack != null && timelineSpan != null, "Timeline should materialize track and span components.");

        UiComponentRecipe detectorRecipe = FindRecipe(
            state.ComponentRecipes,
            "ui.component.runtime_ai.timeline_detector_band");
        detectorRecipe.Template.Nodes = detectorRecipe.Template.Nodes
            .Where(node => !string.Equals(node.Name, "outputPort", StringComparison.Ordinal))
            .ToArray();
        UiComposerWorkspacePlan missingGestureControl = UiComposerService.PlanWorkspace(
            root,
            screenId,
            new UiComposerExpectedHashes
            {
                BlueprintHash = state.BlueprintHash,
                RecipeCatalogHash = state.RecipeCatalogHash,
                CatalogHash = state.CatalogHash,
                ContractHash = state.ContractHash,
                SourceXmlHash = state.SourceXmlHash,
                MappingHash = state.MappingHash
            },
            state.ResourceCatalog,
            state.ComponentRecipes,
            state.Blueprint);
        Require(missingGestureControl.Blocked
            && missingGestureControl.Diagnostics.Any(item => item.Code == "UICOMP004"), "Removing a required nested gesture control must fail Composer preflight.");
        Require(timeline.XmlTemplate.Contains("name=\"btnBranchRecompute\"", StringComparison.Ordinal), "Timeline composite should retain branch recompute control semantics.");
        Require(plan.Materialization.PanelComponentXmlTemplate.Contains("name=\"behaviorCardLibrary\"", StringComparison.Ordinal)
            && plan.Materialization.PanelComponentXmlTemplate.Contains("name=\"behaviorCardTimeline\"", StringComparison.Ordinal), "The panel should host the card library and card-oriented behavior timeline composites.");
        Require(!plan.Materialization.PanelComponentXmlTemplate.Contains("name=\"behaviorCardCompileView\"", StringComparison.Ordinal)
            && !plan.Materialization.PanelComponentXmlTemplate.Contains("name=\"gameplayCardEditor\"", StringComparison.Ordinal), "The panel should not rematerialize retired behavior-card presenters.");
    }

    private static bool ContainsMaterializedResource(UiComposerMaterializationPayload materialization, string resourceId)
    {
        foreach (UiComposerMaterializedResource resource in materialization.Resources)
        {
            if (string.Equals(resource.ResourceId, resourceId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool ContainsMaterializedComponent(UiComposerMaterializationPayload materialization, string componentId)
    {
        return FindMaterializedComponent(materialization, componentId) != null;
    }

    private static UiComposerMaterializedComponent FindMaterializedComponent(UiComposerMaterializationPayload materialization, string componentId)
    {
        foreach (UiComposerMaterializedComponent component in materialization.Components)
            if (string.Equals(component.ComponentRecipeId, componentId, StringComparison.Ordinal))
                return component;
        return null;
    }

    private static UiArtResource FindResource(UiArtResourceLibraryAuthoringState state, string resourceId)
    {
        foreach (UiArtResource resource in state.Catalog.Resources)
        {
            if (string.Equals(resource.ResourceId, resourceId, StringComparison.Ordinal))
                return resource;
        }

        throw new InvalidOperationException("Missing UI art resource: " + resourceId);
    }

    private static UiComponentRecipe FindRecipe(UiComponentRecipeCatalog catalog, string componentId)
    {
        foreach (UiComponentRecipe recipe in catalog.Recipes)
        {
            if (string.Equals(recipe.ComponentId, componentId, StringComparison.Ordinal))
                return recipe;
        }

        throw new InvalidOperationException("Missing UI component recipe: " + componentId);
    }

    private static void UiArtFairyGuiIntegration_ListsPackagesAndBuildsStableRecipe()
    {
        using TempUiArtResourceLibrary temp = TempUiArtResourceLibrary.Create();
        string packageRoot = CreateIntegrationPackage(temp, "IntegrationTarget", importResource: false, writeAlignedSidecar: false);
        string packageXmlPath = Path.Combine(packageRoot, "package.xml");
        string packageXmlHash = HashFile(packageXmlPath);
        string sourcePngPath = Path.Combine(temp.LibraryRoot, "images", "buttons", "button_generic_240x64.png");
        string sourcePngHash = HashFile(sourcePngPath);

        IReadOnlyList<UiArtFairyGuiTargetPackage> packages = UiArtResourceFairyGuiIntegrationService.ListTargetPackages(temp.Root);
        Require(packages.Count == 1 && packages[0].PackageId == "IntegrationTarget", "Integration package discovery should find package.xml-backed targets.");

        UiArtFairyGuiIntegrationPlan plan = UiArtResourceFairyGuiIntegrationService.PlanIntegration(
            temp.Root,
            "MxUiFoundation",
            "ui.button.generic.base",
            "IntegrationTarget");
        Require(!plan.Blocked, "A valid read-only FairyGUI integration plan should not be blocked.");
        Require(plan.Status == "NotImported", "Fresh target package should report NotImported.");
        Require(plan.RequiresFairyGuiImport && plan.RequiresFairyGuiEditor && plan.RequiresFairyGuiPublish, "Fresh target package should require manual import, Editor work, and publish.");
        Require(plan.SuggestedSourcePath == "Images/MxUiFoundation/buttons/button_generic_240x64.png", "Integration plan should produce a stable package-local source path.");
        Require(!plan.WouldModifyCatalog && !plan.WouldModifyPng && !plan.WouldModifyFairyGuiXml && !plan.WouldModifyPublishedOutput, "Integration planning must be read-only.");

        string jsonA = UiArtResourceFairyGuiIntegrationService.GenerateJsonRecipe(plan);
        string jsonB = UiArtResourceFairyGuiIntegrationService.GenerateJsonRecipe(plan);
        string markdownA = UiArtResourceFairyGuiIntegrationService.GenerateMarkdownRecipe(plan);
        string markdownB = UiArtResourceFairyGuiIntegrationService.GenerateMarkdownRecipe(plan);
        Require(jsonA == jsonB && markdownA == markdownB, "Integration recipes should be deterministic.");
        Require(!jsonA.Contains(temp.Root, StringComparison.OrdinalIgnoreCase) && !markdownA.Contains(temp.Root, StringComparison.OrdinalIgnoreCase), "Integration recipes must not expose absolute paths.");
        Require(markdownA.Contains("Development Agent", StringComparison.Ordinal), "Markdown recipe should identify the Development Agent handoff audience.");
        Require(packageXmlHash == HashFile(packageXmlPath), "Integration planning must not modify package.xml.");
        Require(sourcePngHash == HashFile(sourcePngPath), "Integration planning must not modify the resource-library PNG.");
    }

    private static void UiArtFairyGuiIntegration_DetectsImportAndUsageDrift()
    {
        using TempUiArtResourceLibrary temp = TempUiArtResourceLibrary.Create();
        string packageRoot = CreateIntegrationPackage(temp, "IntegrationTarget", importResource: true, writeAlignedSidecar: true);
        string packageXmlPath = Path.Combine(packageRoot, "package.xml");
        string packageXmlHash = HashFile(packageXmlPath);
        string targetPngPath = Path.Combine(packageRoot, "Images", "MxUiFoundation", "buttons", "button_generic_240x64.png");
        string targetPngHash = HashFile(targetPngPath);

        UiArtFairyGuiIntegrationPlan aligned = UiArtResourceFairyGuiIntegrationService.PlanIntegration(
            temp.Root,
            "MxUiFoundation",
            "ui.button.generic.base",
            "IntegrationTarget");
        Require(!aligned.Blocked && aligned.Status == "ImportedExact", "Matching package declaration and PNG should report ImportedExact.");
        Require(aligned.UsageStatus == "Aligned", "Matching sidecar usage should report Aligned.");
        Require(!aligned.RequiresFairyGuiImport && !aligned.RequiresFairyGuiPublish, "Aligned import should not require another import or publish.");
        Require(aligned.ExistingImage != null && aligned.ExistingImage.ResourceId == "intg001" && aligned.ExistingImage.ContentMatches, "Aligned plan should expose the existing FairyGUI resource id and matching content.");

        string sidecarPath = Path.Combine(packageRoot, "ui-art-slices.json");
        FairyGuiArtSliceCatalog sidecar = FairyGuiArtSliceCatalogService.ReadCatalogFile(temp.Root, "FGUIProject/assets/IntegrationTarget/ui-art-slices.json");
        sidecar.Slices[0].RecommendedUsage.LogicalSize.Width = 241;
        File.WriteAllText(sidecarPath, FairyGuiArtSliceCatalogService.SerializeCatalog(sidecar));
        UiArtFairyGuiIntegrationPlan usageDrift = UiArtResourceFairyGuiIntegrationService.PlanIntegration(
            temp.Root,
            "MxUiFoundation",
            "ui.button.generic.base",
            "IntegrationTarget");
        Require(usageDrift.UsageStatus == "Drift" && ContainsDiagnostic(usageDrift.Diagnostics, "UIARTINT111"), "Sidecar parameter mismatch should report usage drift.");
        Require(usageDrift.RequiresFairyGuiPublish, "Usage drift should require FairyGUI review and publish.");

        byte[] targetBytes = ReadBytes(targetPngPath);
        Array.Resize(ref targetBytes, targetBytes.Length + 1);
        File.WriteAllBytes(targetPngPath, targetBytes);
        UiArtFairyGuiIntegrationPlan contentDrift = UiArtResourceFairyGuiIntegrationService.PlanIntegration(
            temp.Root,
            "MxUiFoundation",
            "ui.button.generic.base",
            "IntegrationTarget");
        Require(contentDrift.Status == "ContentDrift" && ContainsDiagnostic(contentDrift.Diagnostics, "UIARTINT102"), "Changed package PNG should report content drift.");
        Require(contentDrift.RequiresFairyGuiImport && contentDrift.RequiresFairyGuiPublish, "Content drift should require re-import and publish.");
        Require(packageXmlHash == HashFile(packageXmlPath), "Drift analysis must not modify package.xml.");
        Require(targetPngHash != HashFile(targetPngPath), "Test setup should change only the target PNG before drift analysis.");
    }

    private static void UiArtFairyGuiIntegration_BlocksEscapingPackageImagePath()
    {
        using TempUiArtResourceLibrary temp = TempUiArtResourceLibrary.Create();
        string packageRoot = CreateIntegrationPackage(temp, "IntegrationTarget", importResource: false, writeAlignedSidecar: false);
        string unsafePackageXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
            + "<packageDescription id=\"intgpkg0\"><resources>\n"
            + "  <image id=\"unsafe1\" name=\"button_generic_240x64.png\" path=\"/../../../../\" exported=\"true\"/>\n"
            + "</resources></packageDescription>\n";
        File.WriteAllText(Path.Combine(packageRoot, "package.xml"), unsafePackageXml);

        UiArtFairyGuiIntegrationPlan plan = UiArtResourceFairyGuiIntegrationService.PlanIntegration(
            temp.Root,
            "MxUiFoundation",
            "ui.button.generic.base",
            "IntegrationTarget");
        Require(plan.Blocked && ContainsDiagnostic(plan.Diagnostics, "UIARTINT008"), "Escaping package image path should block integration planning.");
        string json = UiArtResourceFairyGuiIntegrationService.GenerateJsonRecipe(plan);
        Require(!json.Contains(temp.Root, StringComparison.OrdinalIgnoreCase), "Blocked integration recipe must not expose absolute paths.");
    }

    private static void UiArtResourceCreate_WritesPngAndCatalogOnly()
    {
        using TempUiArtResourceLibrary temp = TempUiArtResourceLibrary.Create();
        UiArtResourceLibraryAuthoringState state = UiArtResourceLibraryService.ReadAuthoringState(temp.Root, "MxUiFoundation");
        string catalogPath = Path.Combine(temp.LibraryRoot, "ui-art-resources.json");
        string originalCatalogHash = HashFile(catalogPath);
        string existingPngPath = Path.Combine(temp.LibraryRoot, "images", "buttons", "button_generic_240x64.png");
        string originalPngHash = HashFile(existingPngPath);
        byte[] pngBytes = ReadBytes(existingPngPath);

        UiArtResource resource = CreateSecondaryTestButtonResource("ui.button.test.secondary", "images/buttons/button_secondary_240x64.png");
        FairyGuiArtSliceOperationPlan plan = UiArtResourceLibraryService.PlanCreateResource(
            temp.Root,
            "MxUiFoundation",
            state.CatalogHash,
            resource,
            pngBytes);

        Require(!plan.Blocked, "Valid UI art resource create plan should not be blocked.");
        Require(plan.WouldModifyCatalog, "Create plan should declare catalog modification.");
        Require(plan.WouldModifyPng, "Create plan should declare PNG creation.");
        Require(!plan.WouldModifyFairyGuiXml && !plan.WouldModifyPublishedOutput && !plan.RequiresFairyGuiPublish, "Create plan should not touch FairyGUI XML or published output.");

        UiArtResourceLibraryService.CreateResource(temp.Root, "MxUiFoundation", state.CatalogHash, resource, pngBytes);
        UiArtResourceLibraryAuthoringState nextState = UiArtResourceLibraryService.ReadAuthoringState(temp.Root, "MxUiFoundation");
        string createdPngPath = Path.Combine(temp.LibraryRoot, "images", "buttons", "button_secondary_240x64.png");
        Require(File.Exists(createdPngPath), "Create resource should write the uploaded PNG under the resource library.");
        Require(nextState.Catalog.Resources.Length == state.Catalog.Resources.Length + 1, "Create resource should append exactly one catalog resource.");
        Require(!string.Equals(originalCatalogHash, HashFile(catalogPath), StringComparison.Ordinal), "Catalog hash should change after resource creation.");
        Require(originalPngHash == HashFile(existingPngPath), "Create resource must not modify existing PNG files.");

        FairyGuiArtSliceOperationPlan duplicateId = UiArtResourceLibraryService.PlanCreateResource(
            temp.Root,
            "MxUiFoundation",
            nextState.CatalogHash,
            CreateSecondaryTestButtonResource("ui.button.test.secondary", "images/buttons/button_duplicate_id_240x64.png"),
            pngBytes);
        Require(duplicateId.Blocked && ContainsDiagnostic(duplicateId, "UIARTRESAPI061"), "Duplicate resourceId should block create.");

        FairyGuiArtSliceOperationPlan duplicatePath = UiArtResourceLibraryService.PlanCreateResource(
            temp.Root,
            "MxUiFoundation",
            nextState.CatalogHash,
            CreateSecondaryTestButtonResource("ui.button.test.tertiary", "images/buttons/button_secondary_240x64.png"),
            pngBytes);
        Require(duplicatePath.Blocked && ContainsDiagnostic(duplicatePath, "UIARTRESAPI062"), "Duplicate relativePath should block create.");
    }

    private static void UiArtResourceSave_ValidatesBeforeWriting()
    {
        using TempUiArtResourceLibrary temp = TempUiArtResourceLibrary.Create();
        UiArtResourceLibraryAuthoringState state = UiArtResourceLibraryService.ReadAuthoringState(temp.Root, "MxUiFoundation");
        string catalogPath = Path.Combine(temp.LibraryRoot, "ui-art-resources.json");
        string pngPath = Path.Combine(temp.LibraryRoot, "images", "buttons", "button_generic_240x64.png");
        string pngHash = HashFile(pngPath);

        state.Catalog.Resources[0].Description = "Validated semantic save.";
        FairyGuiArtSliceOperationPlan validPlan = UiArtResourceLibraryService.PlanSaveCatalogSemantics(
            temp.Root,
            "MxUiFoundation",
            state.CatalogHash,
            state.Catalog);
        Require(!validPlan.Blocked, "Valid UI art resource semantic save should not be blocked.");
        UiArtResourceLibraryService.SaveCatalogSemantics(temp.Root, "MxUiFoundation", state.CatalogHash, state.Catalog);
        Require(pngHash == HashFile(pngPath), "Semantic save must not modify resource PNG files.");

        UiArtResourceLibraryAuthoringState invalidState = UiArtResourceLibraryService.ReadAuthoringState(temp.Root, "MxUiFoundation");
        string validCatalogHash = HashFile(catalogPath);
        invalidState.Catalog.Resources[0].RecommendedFairyGuiUsage.Scale9Grid.X = 999;
        FairyGuiArtSliceOperationPlan invalidGrid = UiArtResourceLibraryService.PlanSaveCatalogSemantics(
            temp.Root,
            "MxUiFoundation",
            invalidState.CatalogHash,
            invalidState.Catalog);
        Require(invalidGrid.Blocked && ContainsDiagnostic(invalidGrid, "UIARTRES018"), "Out-of-bounds NineSlice edits should block semantic save.");
        ExpectThrows(
            () => UiArtResourceLibraryService.SaveCatalogSemantics(temp.Root, "MxUiFoundation", invalidState.CatalogHash, invalidState.Catalog),
            "Blocked semantic save should throw before writing.");
        Require(validCatalogHash == HashFile(catalogPath), "Blocked semantic save must leave the catalog unchanged.");

        UiArtResourceLibraryAuthoringState structuralState = UiArtResourceLibraryService.ReadAuthoringState(temp.Root, "MxUiFoundation");
        structuralState.Catalog.Resources[0].RelativePath = "images/buttons/renamed.png";
        FairyGuiArtSliceOperationPlan structuralChange = UiArtResourceLibraryService.PlanSaveCatalogSemantics(
            temp.Root,
            "MxUiFoundation",
            structuralState.CatalogHash,
            structuralState.Catalog);
        Require(structuralChange.Blocked && ContainsDiagnostic(structuralChange, "UIARTRESAPI015"), "Semantic save must not rename resource paths.");
    }

    private static void UiArtResourceSave_BlocksStaleHashWithoutWriting()
    {
        using TempUiArtResourceLibrary temp = TempUiArtResourceLibrary.Create();
        UiArtResourceLibraryAuthoringState state = UiArtResourceLibraryService.ReadAuthoringState(temp.Root, "MxUiFoundation");
        string catalogPath = Path.Combine(temp.LibraryRoot, "ui-art-resources.json");
        string originalCatalogHash = HashFile(catalogPath);
        state.Catalog.Resources[0].Description = "This stale edit must not be written.";

        FairyGuiArtSliceOperationPlan plan = UiArtResourceLibraryService.PlanSaveCatalogSemantics(
            temp.Root,
            "MxUiFoundation",
            "stale",
            state.Catalog);
        Require(plan.Blocked && ContainsDiagnostic(plan, "UIARTRESAPI001"), "Stale catalog hash should block semantic save.");
        ExpectThrows(
            () => UiArtResourceLibraryService.SaveCatalogSemantics(temp.Root, "MxUiFoundation", "stale", state.Catalog),
            "Stale semantic save should throw before writing.");
        Require(originalCatalogHash == HashFile(catalogPath), "Stale semantic save must leave the catalog unchanged.");
    }

    private static void UiArtResourceCreate_BlockedExecutionLeavesFilesUnchanged()
    {
        using TempUiArtResourceLibrary temp = TempUiArtResourceLibrary.Create();
        UiArtResourceLibraryAuthoringState state = UiArtResourceLibraryService.ReadAuthoringState(temp.Root, "MxUiFoundation");
        string catalogPath = Path.Combine(temp.LibraryRoot, "ui-art-resources.json");
        string originalCatalogHash = HashFile(catalogPath);
        string existingPngPath = Path.Combine(temp.LibraryRoot, "images", "buttons", "button_generic_240x64.png");
        string originalPngHash = HashFile(existingPngPath);
        string escapingPath = Path.GetFullPath(Path.Combine(temp.LibraryRoot, "..", "escape.png"));
        UiArtResource resource = CreateSecondaryTestButtonResource("ui.button.test.escape", "../escape.png");
        byte[] pngBytes = ReadBytes(existingPngPath);

        FairyGuiArtSliceOperationPlan plan = UiArtResourceLibraryService.PlanCreateResource(
            temp.Root,
            "MxUiFoundation",
            state.CatalogHash,
            resource,
            pngBytes);
        Require(plan.Blocked && ContainsDiagnostic(plan, "UIARTRESAPI054"), "Escaping create path should be blocked.");
        ExpectThrows(
            () => UiArtResourceLibraryService.CreateResource(temp.Root, "MxUiFoundation", state.CatalogHash, resource, pngBytes),
            "Blocked create should throw before writing.");
        Require(!File.Exists(escapingPath), "Blocked create must not write a PNG outside the resource library.");
        Require(originalCatalogHash == HashFile(catalogPath), "Blocked create must leave the catalog unchanged.");
        Require(originalPngHash == HashFile(existingPngPath), "Blocked create must leave existing PNG files unchanged.");
    }

    private static void UiArtResourceCreate_AcceptsOpaqueResourcePolicy()
    {
        using TempUiArtResourceLibrary temp = TempUiArtResourceLibrary.Create();
        UiArtResourceLibraryAuthoringState state = UiArtResourceLibraryService.ReadAuthoringState(temp.Root, "MxUiFoundation");
        UiArtResource opaque = CreateOpaqueFixedResource();
        FairyGuiArtSliceOperationPlan plan = UiArtResourceLibraryService.PlanCreateResource(
            temp.Root,
            "MxUiFoundation",
            state.CatalogHash,
            opaque,
            CreateOnePixelRgbPng());
        Require(!plan.Blocked, "Opaque PNG should be accepted when alpha constraints are disabled.");
    }

    private static void UiArtResourceReplacementPlan_UsesLibraryConstraints()
    {
        using TempUiArtResourceLibrary temp = TempUiArtResourceLibrary.Create();
        UiArtResourceLibraryAuthoringState state = UiArtResourceLibraryService.ReadAuthoringState(temp.Root, "MxUiFoundation");
        string pngPath = Path.Combine(temp.LibraryRoot, "images", "buttons", "button_generic_240x64.png");
        byte[] currentBytes = ReadBytes(pngPath);

        FairyGuiArtSliceOperationPlan valid = UiArtResourceLibraryService.PlanReplaceExistingPng(
            temp.Root,
            "MxUiFoundation",
            state.CatalogHash,
            "ui.button.generic.base",
            currentBytes);
        Require(!valid.Blocked && valid.WouldModifyPng, "Current library PNG should be a valid same-path replacement.");

        FairyGuiArtSliceOperationPlan stale = UiArtResourceLibraryService.PlanReplaceExistingPng(
            temp.Root,
            "MxUiFoundation",
            "stale",
            "ui.button.generic.base",
            currentBytes);
        Require(stale.Blocked && ContainsDiagnostic(stale, "UIARTRESAPI001"), "Stale resource-library hash should block replacement.");

        FairyGuiArtSliceOperationPlan invalid = UiArtResourceLibraryService.PlanReplaceExistingPng(
            temp.Root,
            "MxUiFoundation",
            state.CatalogHash,
            "ui.button.generic.base",
            CreateOnePixelRgbPng());
        Require(invalid.Blocked && ContainsDiagnostic(invalid, "UIARTRESAPI041"), "Resource-library replacement should enforce alpha constraints.");
    }

    private static void UiArtResourceReplacement_WritesOnlyTargetPng()
    {
        using TempUiArtResourceLibrary temp = TempUiArtResourceLibrary.Create();
        UiArtResourceLibraryAuthoringState state = UiArtResourceLibraryService.ReadAuthoringState(temp.Root, "MxUiFoundation");
        string catalogPath = Path.Combine(temp.LibraryRoot, "ui-art-resources.json");
        string targetPath = Path.Combine(temp.LibraryRoot, "images", "buttons", "button_generic_240x64.png");
        string originalCatalogHash = HashFile(catalogPath);
        string originalPngHash = HashFile(targetPath);
        byte[] originalBytes = ReadBytes(targetPath);
        var replacementBytes = new byte[originalBytes.Length + 1];
        Array.Copy(originalBytes, replacementBytes, originalBytes.Length);

        FairyGuiArtSliceOperationPlan plan = UiArtResourceLibraryService.PlanReplaceExistingPng(
            temp.Root,
            "MxUiFoundation",
            state.CatalogHash,
            "ui.button.generic.base",
            replacementBytes);
        Require(!plan.Blocked, "Valid same-size PNG replacement should be executable.");

        UiArtResourceLibraryService.ReplaceExistingPng(
            temp.Root,
            "MxUiFoundation",
            state.CatalogHash,
            "ui.button.generic.base",
            replacementBytes);
        Require(originalCatalogHash == HashFile(catalogPath), "PNG replacement must not modify the resource catalog.");
        Require(originalPngHash != HashFile(targetPath), "PNG replacement should update the target file.");
        Require(ReadBytes(targetPath).Length == replacementBytes.Length, "PNG replacement should write the complete candidate bytes.");
    }

    private static void UiArtResourceLibrary_BlocksEscapingResourceRoot()
    {
        using TempUiArtResourceLibrary temp = TempUiArtResourceLibrary.Create();
        string unsafeDirectory = Path.Combine(temp.Root, "ArtSource", "UIKits", "UnsafeLibrary");
        Directory.CreateDirectory(unsafeDirectory);
        var catalog = new UiArtResourceLibraryCatalog
        {
            SchemaVersion = "mx.uiArtResourceLibrary.v1",
            LibraryId = "UnsafeLibrary",
            ResourceRoot = "../outside",
            PathStyle = "library-relative-posix",
            DesignResolution = new UiArtSize { Width = 1920, Height = 1080 },
            Resources = Array.Empty<UiArtResource>()
        };
        string catalogPath = Path.Combine(unsafeDirectory, "ui-art-resources.json");
        File.WriteAllText(catalogPath, UiArtResourceLibraryService.SerializeCatalog(catalog));

        IReadOnlyList<UiArtResourceLibraryDescriptor> libraries = UiArtResourceLibraryService.ListLibraries(temp.Root);
        for (int i = 0; i < libraries.Count; i++)
            Require(libraries[i].LibraryId != "UnsafeLibrary", "Library discovery must omit resource roots outside ArtSource/UIKits.");

        ExpectThrows(
            () => UiArtResourceLibraryService.ResolveLibraryDescriptor(temp.Root, "ArtSource/UIKits/UnsafeLibrary/ui-art-resources.json"),
            "Direct descriptor resolution must reject resource roots outside ArtSource/UIKits.");
    }

    private static void SaveCatalogPlan_WritesOnlyCatalog()
    {
        using TempFairyGuiPackage temp = TempFairyGuiPackage.Create();
        FairyGuiArtSliceAuthoringState state = FairyGuiArtSliceCatalogService.ReadAuthoringState(temp.Root, "MxCharacterTestHud");
        string packageXmlHash = HashFile(Path.Combine(temp.PackageRoot, "package.xml"));
        string pngHash = HashFile(Path.Combine(temp.PackageRoot, "Images", "hud", "player_plate_360x112.png"));

        state.Catalog.Slices[0].Description = "Temporary authoring test description.";
        FairyGuiArtSliceOperationPlan plan = FairyGuiArtSliceCatalogService.PlanSaveCatalogSemantics(
            temp.Root,
            "MxCharacterTestHud",
            state.CatalogHash,
            state.Catalog);
        Require(!plan.Blocked, "Valid catalog save plan should not be blocked.");
        Require(plan.WouldModifyCatalog, "Catalog save plan should declare catalog modification.");
        Require(!plan.WouldModifyPng && !plan.WouldModifyFairyGuiXml && !plan.WouldModifyPublishedOutput, "Catalog save plan should not touch PNG, FairyGUI XML, or published output.");

        FairyGuiArtSliceCatalogService.WriteCatalogFile(temp.Root, FairyGuiArtSliceCatalogService.CharacterTestCatalogPath, state.Catalog);
        string nextHash = FairyGuiArtSliceCatalogService.ComputeCatalogHash(temp.Root, FairyGuiArtSliceCatalogService.CharacterTestCatalogPath);
        Require(!string.Equals(nextHash, state.CatalogHash, StringComparison.Ordinal), "Catalog hash should change after saving edited semantics.");
        Require(packageXmlHash == HashFile(Path.Combine(temp.PackageRoot, "package.xml")), "Catalog save must not modify package.xml.");
        Require(pngHash == HashFile(Path.Combine(temp.PackageRoot, "Images", "hud", "player_plate_360x112.png")), "Catalog save must not modify PNG files.");
    }

    private static void ReplacementPlan_BlocksStaleHashAndInvalidPng()
    {
        using TempFairyGuiPackage temp = TempFairyGuiPackage.Create();
        FairyGuiArtSliceAuthoringState state = FairyGuiArtSliceCatalogService.ReadAuthoringState(temp.Root, "MxCharacterTestHud");

        FairyGuiArtSliceOperationPlan staleHash = FairyGuiArtSliceCatalogService.PlanReplaceExistingPng(
            temp.Root,
            "MxCharacterTestHud",
            "stale",
            "hud_player_plate",
            ReadBytes(Path.Combine(temp.PackageRoot, "Images", "hud", "player_plate_360x112.png")));
        Require(staleHash.Blocked && ContainsDiagnostic(staleHash, "UIARTAPI001"), "Stale catalog hash should block replacement.");

        FairyGuiArtSliceOperationPlan badPng = FairyGuiArtSliceCatalogService.PlanReplaceExistingPng(
            temp.Root,
            "MxCharacterTestHud",
            state.CatalogHash,
            "hud_player_plate",
            CreateOnePixelRgbPng());
        Require(badPng.Blocked, "Replacement with invalid constraints should be blocked.");
        Require(ContainsDiagnostic(badPng, "UIARTAPI036"), "Pixel size mismatch should be reported.");
        Require(ContainsDiagnostic(badPng, "UIARTAPI041"), "Missing alpha channel should be reported.");
    }

    private static void ReplacementPlan_AcceptsSamePathValidPng()
    {
        using TempFairyGuiPackage temp = TempFairyGuiPackage.Create();
        FairyGuiArtSliceAuthoringState state = FairyGuiArtSliceCatalogService.ReadAuthoringState(temp.Root, "MxCharacterTestHud");
        byte[] currentBytes = ReadBytes(Path.Combine(temp.PackageRoot, "Images", "hud", "player_plate_360x112.png"));

        FairyGuiArtSliceOperationPlan plan = FairyGuiArtSliceCatalogService.PlanReplaceExistingPng(
            temp.Root,
            "MxCharacterTestHud",
            state.CatalogHash,
            "hud_player_plate",
            currentBytes);
        Require(!plan.Blocked, "Current same-path PNG should be a valid replacement candidate.");
        Require(plan.WouldModifyPng, "Replacement plan should declare PNG modification.");
        Require(plan.RequiresFairyGuiPublish, "Replacement plan should require FairyGUI publish.");
        Require(!plan.WouldModifyCatalog && !plan.WouldModifyFairyGuiXml && !plan.WouldModifyPublishedOutput, "Replacement plan should not modify catalog, XML, or published output directly.");
    }

    private static void DraftPlan_OnlyTargetsSidecarCatalog()
    {
        using TempFairyGuiPackage temp = TempFairyGuiPackage.Create();
        FairyGuiArtSliceAuthoringState state = FairyGuiArtSliceCatalogService.ReadAuthoringState(temp.Root, "MxCharacterTestHud");
        FairyGuiArtSliceOperationPlan plan = FairyGuiArtSliceCatalogService.PlanRegisterDraftSlice(
            temp.Root,
            "MxCharacterTestHud",
            state.CatalogHash,
            "draft_test_slice",
            "Images/draft/draft_test_slice.png");

        Require(!plan.Blocked, "Valid draft slice plan should not be blocked.");
        Require(plan.WouldModifyCatalog, "Draft plan should only update catalog.");
        Require(!plan.WouldModifyPng && !plan.WouldModifyFairyGuiXml && !plan.RequiresFairyGuiPublish, "Draft plan should not touch PNG, XML, or require publish by itself.");
        Require(ContainsDiagnostic(plan, "UIARTAPI023"), "Draft plan should warn that FairyGUI Editor must create real resources/usages.");
    }

    private static string CreateIntegrationPackage(
        TempUiArtResourceLibrary temp,
        string packageId,
        bool importResource,
        bool writeAlignedSidecar)
    {
        string packageRoot = Path.Combine(temp.Root, "FGUIProject", "assets", packageId);
        Directory.CreateDirectory(packageRoot);
        string imageDeclaration = string.Empty;
        if (importResource)
        {
            string targetDirectory = Path.Combine(packageRoot, "Images", "MxUiFoundation", "buttons");
            Directory.CreateDirectory(targetDirectory);
            File.Copy(
                Path.Combine(temp.LibraryRoot, "images", "buttons", "button_generic_240x64.png"),
                Path.Combine(targetDirectory, "button_generic_240x64.png"));
            imageDeclaration = "    <image id=\"intg001\" name=\"button_generic_240x64.png\" path=\"/Images/MxUiFoundation/buttons/\" exported=\"true\"/>\n";
        }

        string packageXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
            + "<packageDescription id=\"intgpkg0\">\n"
            + "  <resources>\n"
            + imageDeclaration
            + "  </resources>\n"
            + "</packageDescription>\n";
        File.WriteAllText(Path.Combine(packageRoot, "package.xml"), packageXml);

        if (writeAlignedSidecar)
        {
            var catalog = new FairyGuiArtSliceCatalog
            {
                SchemaVersion = "mx.uiArtSliceCatalog.v1",
                PackageId = packageId,
                PackageSourcePath = "FGUIProject/assets/" + packageId,
                PathStyle = "package-relative-posix",
                DesignResolution = new FairyGuiDesignResolution { Width = 1920, Height = 1080 },
                StrictCoverage = false,
                Groups = Array.Empty<FairyGuiArtSliceGroup>(),
                Slices = new[]
                {
                    new FairyGuiArtSlice
                    {
                        SliceId = "ui_button_generic_base",
                        FguiResourceId = "intg001",
                        ExpectedSourcePath = "Images/MxUiFoundation/buttons/button_generic_240x64.png",
                        Category = "ButtonBase",
                        DisplayName = "Generic Button Base",
                        Description = "Integration test resource.",
                        Tags = new[] { "button", "test" },
                        RecommendedUsage = new FairyGuiRecommendedUsage
                        {
                            Policy = "NineSlice",
                            LogicalSize = new FairyGuiDesignResolution { Width = 240, Height = 64 },
                            PreserveAspect = false,
                            RelationHint = string.Empty
                        },
                        Scale9Grid = new FairyGuiScale9Grid { X = 24, Y = 18, Width = 192, Height = 28 },
                        Replacement = new FairyGuiReplacementPolicy
                        {
                            RequireAlphaChannel = true,
                            RequireNonOpaqueAlpha = true,
                            RequireSamePixelSize = true,
                            AllowStretch = true,
                            MinPixelWidth = 240,
                            MinPixelHeight = 64,
                            ReplaceTogether = false
                        }
                    }
                }
            };
            File.WriteAllText(Path.Combine(packageRoot, "ui-art-slices.json"), FairyGuiArtSliceCatalogService.SerializeCatalog(catalog));
        }

        return packageRoot;
    }

    private static UiArtResource CreateSecondaryTestButtonResource(string resourceId, string relativePath)
    {
        return new UiArtResource
        {
            ResourceId = resourceId,
            RelativePath = relativePath,
            Category = "ButtonBase",
            DisplayName = "Secondary Test Button",
            Description = "Temporary authoring test button.",
            Tags = new[] { "button", "test" },
            RecommendedFairyGuiUsage = new UiArtRecommendedFairyGuiUsage
            {
                Policy = "NineSlice",
                DesignSize = new UiArtSize { Width = 240, Height = 64 },
                Scale9Grid = new UiArtRect { X = 24, Y = 18, Width = 192, Height = 28 },
                PreserveAspect = false
            },
            Replacement = new UiArtResourceReplacementPolicy
            {
                RequireAlphaChannel = true,
                RequireNonOpaqueAlpha = true,
                RequireSamePixelSize = true,
                AllowStretch = true,
                MinPixelWidth = 240,
                MinPixelHeight = 64,
                ReplaceTogether = false
            }
        };
    }

    private static UiArtResource CreateOpaqueFixedResource()
    {
        return new UiArtResource
        {
            ResourceId = "ui.background.opaque.test",
            RelativePath = "images/imports/opaque_test.png",
            Category = "Decoration",
            DisplayName = "Opaque Test",
            Description = "Opaque resource policy test.",
            Tags = new[] { "opaque", "test" },
            RecommendedFairyGuiUsage = new UiArtRecommendedFairyGuiUsage
            {
                Policy = "Fixed",
                DesignSize = new UiArtSize { Width = 1, Height = 1 },
                PreserveAspect = false
            },
            Replacement = new UiArtResourceReplacementPolicy
            {
                RequireAlphaChannel = false,
                RequireNonOpaqueAlpha = false,
                RequireSamePixelSize = true,
                AllowStretch = false,
                MinPixelWidth = 1,
                MinPixelHeight = 1,
                ReplaceTogether = false
            }
        };
    }

    private static void ExpectThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static bool ContainsDiagnostic(FairyGuiArtSliceOperationPlan plan, string code)
    {
        for (int i = 0; i < plan.Diagnostics.Count; i++)
        {
            if (string.Equals(plan.Diagnostics[i].Code, code, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool ContainsDiagnostic(IReadOnlyList<FairyGuiArtSliceOperationDiagnostic> diagnostics, string code)
    {
        for (int i = 0; i < diagnostics.Count; i++)
        {
            if (string.Equals(diagnostics[i].Code, code, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static byte[] CreateOnePixelRgbPng()
    {
        return new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01,
            0x00, 0x00, 0x00, 0x01,
            0x08, 0x02, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x49, 0x45, 0x4E, 0x44,
            0x00, 0x00, 0x00, 0x00
        };
    }

    private static byte[] ReadBytes(string path)
    {
        return File.ReadAllBytes(path);
    }

    private static string HashFile(string path)
    {
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(File.ReadAllBytes(path));
        var builder = new StringBuilder(hash.Length * 2);
        for (int i = 0; i < hash.Length; i++)
            builder.Append(hash[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static string FindRepoRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "WGameFramework.sln")) || Directory.Exists(Path.Combine(current, "FGUIProject")))
                return current;

            DirectoryInfo parent = Directory.GetParent(current);
            current = parent?.FullName;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private static bool IsPathAtOrUnderRoot(string rootPath, string path)
    {
        string fullRoot = Path.GetFullPath(rootPath ?? string.Empty)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(path ?? string.Empty);
        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), fullRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class TempFairyGuiPackage : IDisposable
    {
        private TempFairyGuiPackage(string root, string packageRoot)
        {
            Root = root;
            PackageRoot = packageRoot;
        }

        public string Root { get; }
        public string PackageRoot { get; }

        public static TempFairyGuiPackage Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "mx-fairygui-art-slice-authoring-" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(FindRepoRoot(), "FGUIProject", "assets", "MxCharacterTestHud");
            string destination = Path.Combine(root, "FGUIProject", "assets", "MxCharacterTestHud");
            CopyDirectory(source, destination);
            return new TempFairyGuiPackage(root, destination);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            string[] files = Directory.GetFiles(source);
            for (int i = 0; i < files.Length; i++)
                File.Copy(files[i], Path.Combine(destination, Path.GetFileName(files[i])), overwrite: true);

            string[] directories = Directory.GetDirectories(source);
            for (int i = 0; i < directories.Length; i++)
                CopyDirectory(directories[i], Path.Combine(destination, Path.GetFileName(directories[i])));
        }
    }

    private sealed class TempUiArtResourceLibrary : IDisposable
    {
        private TempUiArtResourceLibrary(string root, string libraryRoot)
        {
            Root = root;
            LibraryRoot = libraryRoot;
        }

        public string Root { get; }
        public string LibraryRoot { get; }

        public static TempUiArtResourceLibrary Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "mx-ui-art-resource-library-" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(FindRepoRoot(), "ArtSource", "UIKits", "MxUiFoundation");
            string destination = Path.Combine(root, "ArtSource", "UIKits", "MxUiFoundation");
            CopyDirectory(source, destination);
            return new TempUiArtResourceLibrary(root, destination);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            string[] files = Directory.GetFiles(source);
            for (int i = 0; i < files.Length; i++)
                File.Copy(files[i], Path.Combine(destination, Path.GetFileName(files[i])), overwrite: true);

            string[] directories = Directory.GetDirectories(source);
            for (int i = 0; i < directories.Length; i++)
                CopyDirectory(directories[i], Path.Combine(destination, Path.GetFileName(directories[i])));
        }
    }
}
