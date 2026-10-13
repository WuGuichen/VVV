using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MxFramework.Authoring.Cli;
using MxFramework.Authoring.UiComposer;

namespace MxFramework.Authoring.Tests;

internal static class UiComposerAuthoringTests
{
    public static void RunAll()
    {
        LoadsBlueprintAndBuildsStableApplyPlan();
        PreservesExplicitFontsThroughPublishAndSync();
        LoadsCharacterTestMenuContractAndSemanticImages();
        LoadsCharacterTestCombatHudContractAndSemanticAssets();
        LoadsCharacterTestPlayerPanelContractsAndInputSemantics();
        FairyGuiPublishTimeoutDoesNotLeaveARejectingRace();
        FairyGuiHelperSupportsReadOnlyProbe();
        ExportsRuntimeAddressedCompositeRecipes();
        IconOnlyButtonsRemainCenteredWhenInstancesResize();
        ProjectsV1FeedbackAsSilentAndValidatesV2();
        BlocksInvalidBindingsAndStaleSave();
        QueueApplyWritesOnlyTemporaryRequest();
        SynchronizesSupportedXmlAndBlocksUnknownAttributes();
        MaterializesBlueprintContainersAndValidatesStructure();
        MaterializesAndSynchronizesControlledListNode();
        AuthorsWorkspaceResourcesRecipesAndReferences();
        RunComponentPathCollisions();
        WorkspaceApplyPreflightValidatesTheCompleteBlueprint();
        WorkspaceApplyRollsBackWhenQueueingFails();
        QueueProbeUsesReadOnlyHelperAction();
        PublishAllRejectsConflictingVerificationModes();
        PublishAllPreflightStopsBeforeHelperOperations();
        PublishAllCancellationRemovesOnlyUnclaimedRequest();
    }

    public static void RunIconOnlyButtonCentering()
    {
        IconOnlyButtonsRemainCenteredWhenInstancesResize();
    }

    public static void RunComponentPathCollisions()
    {
        var cases = new[]
        {
            ("ui.component.button.generic", "GenericButton", "ui.component.test.second", "GenericButton"),
            ("ui.component.button.generic", "GenericButton", "ui.component.test.second", "genericbutton"),
            ("ui.component.button.generic", "", "ui.component.test.generic", " "),
            ("ui.component.button.generic", "generic", "ui.component.test.generic", ""),
            ("ui.component.button.generic", "", "ui.component.test.second", "GENERIC")
        };
        foreach (var (firstId, firstName, secondId, secondName) in cases)
        {
            using TempUiComposerProject temp = TempUiComposerProject.Create();
            UiComposerAuthoringState state = UiComposerService.ReadState(temp.Root, UiComposerService.DemoScreenId);
            Require(!state.Diagnostics.Any(item => item.Severity == "error"), "Baseline demo must be valid.");
            UiComponentRecipe first = state.ComponentRecipes.Recipes.Single(item => item.ComponentId == firstId);
            UiComponentRecipe second = JsonSerializer.Deserialize<UiComponentRecipe>(JsonSerializer.Serialize(first));
            first.FairyGuiComponentName = firstName;
            second.ComponentId = secondId;
            second.FairyGuiComponentName = secondName;
            second.DefaultSize.Width += 40;
            state.ComponentRecipes.Recipes = state.ComponentRecipes.Recipes.Append(second).ToArray();
            state.Blueprint.Nodes.Single(item => item.Name == "btnSecondary").Component.ComponentRecipeId = secondId;
            string recipePath = Path.Combine(temp.Root, UiComposerService.DefaultRecipeCatalogPath);
            string originalRecipes = File.ReadAllText(recipePath);

            UiComposerWorkspacePlan save = UiComposerService.PlanSaveRecipes(temp.Root, state.Screen.ScreenId, state.RecipeCatalogHash, state.ComponentRecipes);
            Require(save.Blocked, "Same effective component path must block recipe save.");
            Require(save.Diagnostics.Any(item => item.Code == "UICOMP020" && item.Path.EndsWith("#" + firstId, StringComparison.Ordinal)), "Collision must locate the first recipe.");
            Require(save.Diagnostics.Any(item => item.Code == "UICOMP020" && item.Path.EndsWith("#" + secondId, StringComparison.Ordinal)), "Collision must locate the second recipe.");
            Require(save.Diagnostics.Where(item => item.Code == "UICOMP020").All(item => item.Message.Contains(firstId) && item.Message.Contains(secondId)), "Collision messages must identify both recipes.");
            bool saveRejected = false;
            try { UiComposerService.SaveRecipes(temp.Root, state.Screen.ScreenId, state.RecipeCatalogHash, state.ComponentRecipes); }
            catch (InvalidOperationException) { saveRejected = true; }
            Require(saveRejected && File.ReadAllText(recipePath) == originalRecipes, "Blocked save must preserve the catalog.");

            UiComposerWorkspacePlan workspace = UiComposerService.PlanWorkspace(temp.Root, state.Screen.ScreenId, Hashes(state), state.ResourceCatalog, state.ComponentRecipes, state.Blueprint);
            Require(workspace.Blocked && workspace.Diagnostics.Any(item => item.Code == "UICOMP020"), "Unified preflight must block the same collision.");
            bool applyRejected = false;
            try { UiComposerService.ApplyWorkspace(temp.Root, state.Screen.ScreenId, Hashes(state), state.ResourceCatalog, state.ComponentRecipes, state.Blueprint); }
            catch (InvalidOperationException) { applyRejected = true; }
            Require(applyRejected && File.ReadAllText(recipePath) == originalRecipes, "Blocked apply must preserve the catalog.");
            Require(!Directory.Exists(Path.Combine(temp.Root, "FGUIProject", ".agent")), "Blocked apply must not queue helper writes.");

            File.WriteAllText(recipePath, JsonSerializer.Serialize(state.ComponentRecipes));
            UiComposerAuthoringState diskCollision = UiComposerService.ReadState(temp.Root, state.Screen.ScreenId);
            UiComposerOperationPlan blocked = UiComposerService.PlanApply(temp.Root, state.Screen.ScreenId, Hashes(diskCollision));
            Require(blocked.Blocked && blocked.Materialization == null && blocked.Diagnostics.Any(item => item.Code == "UICOMP020"), "Disk catalog collisions must block materialization even if a recipe is unused.");
            File.WriteAllText(recipePath, originalRecipes);

            second.FairyGuiComponentName = "T07UniqueProbe";
            Require(!UiComposerService.PlanSaveRecipes(temp.Root, state.Screen.ScreenId, state.RecipeCatalogHash, state.ComponentRecipes).Blocked, "Distinct effective names must remain saveable.");
            Require(!UiComposerService.PlanWorkspace(temp.Root, state.Screen.ScreenId, Hashes(state), state.ResourceCatalog, state.ComponentRecipes, state.Blueprint).Blocked, "Distinct names must preserve existing binding contracts.");
            UiComposerWorkspaceState saved = UiComposerService.SaveRecipes(temp.Root, state.Screen.ScreenId, state.RecipeCatalogHash, state.ComponentRecipes);
            UiComposerService.SaveBlueprint(temp.Root, state.Screen.ScreenId, saved.AuthoringState.BlueprintHash, state.Blueprint);
            UiComposerAuthoringState unique = UiComposerService.ReadState(temp.Root, state.Screen.ScreenId);
            UiComposerOperationPlan plan = UiComposerService.PlanApply(temp.Root, state.Screen.ScreenId, Hashes(unique));
            Require(!plan.Blocked && plan.Materialization.Components.Length == 2, "Two used, uniquely named recipes must materialize.");
            Require(plan.Materialization.Components.Select(item => item.ComponentRelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2, "Unique recipes need independent files.");
            Require(plan.Materialization.Components.Select(item => item.Token).Distinct(StringComparer.Ordinal).Count() == 2, "Unique recipes need independent tokens.");
            Require(UiComposerService.PlanSaveRecipes(temp.Root, state.Screen.ScreenId, state.RecipeCatalogHash, state.ComponentRecipes).Blocked, "Collision validation must preserve stale hash guards.");
        }

        using TempUiComposerProject compositeTemp = TempUiComposerProject.Create();
        UiComposerAuthoringState compositeState = UiComposerService.ReadState(compositeTemp.Root, UiComposerService.DemoScreenId);
        UiComponentRecipe button = compositeState.ComponentRecipes.Recipes.Single(item => item.ComponentId == "ui.component.button.generic");
        UiComponentRecipe composite = compositeState.ComponentRecipes.Recipes.First(item => item.ComponentType == "Composite");
        composite.FairyGuiComponentName = button.FairyGuiComponentName;
        Require(UiComposerService.PlanSaveRecipes(compositeTemp.Root, compositeState.Screen.ScreenId, compositeState.RecipeCatalogHash, compositeState.ComponentRecipes).Diagnostics.Any(item => item.Code == "UICOMP020" && item.Path.EndsWith("#" + composite.ComponentId, StringComparison.Ordinal)), "Controlled Composite recipes must share the path uniqueness gate.");
        composite.FairyGuiComponentName = "../UnsafeComponent";
        Require(UiComposerService.PlanSaveRecipes(compositeTemp.Root, compositeState.Screen.ScreenId, compositeState.RecipeCatalogHash, compositeState.ComponentRecipes).Diagnostics.Any(item => item.Code == "UICOMP019"), "Server component names must obey the helper filename alphabet.");
    }

    public static void RunPlayerGuidancePackage()
    {
        ExportsRuntimeAddressedCompositeRecipes();
    }

    public static void RunCharacterTestPlayerPanels()
    {
        LoadsCharacterTestPlayerPanelContractsAndInputSemantics();
        FairyGuiPublishTimeoutDoesNotLeaveARejectingRace();
    }

    public static void RunControlledListNode()
    {
        MaterializesAndSynchronizesControlledListNode();
    }

    public static void RunPublishAll()
    {
        FairyGuiHelperSupportsReadOnlyProbe();
        QueueProbeUsesReadOnlyHelperAction();
        PublishAllRejectsConflictingVerificationModes();
        PublishAllPreflightStopsBeforeHelperOperations();
        PublishAllCancellationRemovesOnlyUnclaimedRequest();
    }

    private static void FairyGuiHelperSupportsReadOnlyProbe()
    {
        string root = FindTestRepositoryRoot();
        string helperPath = Path.Combine(root, "FGUIProject", "plugins", "wgameframework-agent-helper", "main.js");
        string helper = File.ReadAllText(helperPath);
        Require(helper.Contains("request.action === \"Probe\"", StringComparison.Ordinal), "FairyGUI helper must recognize the Probe action.");
        Require(helper.Contains("helper-ready", StringComparison.Ordinal), "Probe must publish an explicit helper-ready phase.");
        int probeStart = helper.IndexOf("request.action === \"Probe\"", StringComparison.Ordinal);
        int materializeStart = helper.IndexOf("request.action === \"Materialize\"", probeStart, StringComparison.Ordinal);
        string probeBlock = helper.Substring(probeStart, materializeStart - probeStart);
        Require(!probeBlock.Contains("materializeUiComposerPackage", StringComparison.Ordinal), "Probe must not invoke package materialization.");
        Require(!probeBlock.Contains("UI_COMPOSER_STATE_FILE", StringComparison.Ordinal), "Probe must not read or write ui-composer-state.json.");
    }

    private static void AuthorsWorkspaceResourcesRecipesAndReferences()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        UiComposerWorkspaceState workspace = UiComposerService.ReadWorkspace(temp.Root, UiComposerService.DemoScreenId);
        Require(workspace.References.Any(item => item.SourceKind == "recipe" && item.TargetKind == "resource"), "Workspace should index recipe-to-resource references.");
        Require(workspace.References.Any(item => item.SourceKind == "node" && item.TargetKind == "recipe"), "Workspace should index node-to-recipe references.");

        UiComponentRecipe recipe = workspace.AuthoringState.ComponentRecipes.Recipes[0];
        UiComposerWorkspacePlan deletePlan = UiComposerService.PlanDeleteRecipe(temp.Root, UiComposerService.DemoScreenId, recipe.ComponentId);
        Require(deletePlan.Blocked && deletePlan.Diagnostics.Any(item => item.Code == "UICOMP202"), "Deleting a referenced recipe must be blocked.");

        recipe.DisplayName = "Updated button recipe";
        UiComposerWorkspacePlan savePlan = UiComposerService.PlanSaveRecipes(temp.Root, UiComposerService.DemoScreenId, workspace.AuthoringState.RecipeCatalogHash, workspace.AuthoringState.ComponentRecipes);
        Require(!savePlan.Blocked, "A valid button recipe edit should be saveable.");
        UiComposerWorkspaceState saved = UiComposerService.SaveRecipes(temp.Root, UiComposerService.DemoScreenId, workspace.AuthoringState.RecipeCatalogHash, workspace.AuthoringState.ComponentRecipes);
        Require(saved.AuthoringState.ComponentRecipes.Recipes[0].DisplayName == "Updated button recipe", "Recipe save should persist semantic edits.");
        Require(UiComposerService.PlanSaveRecipes(temp.Root, UiComposerService.DemoScreenId, workspace.AuthoringState.RecipeCatalogHash, saved.AuthoringState.ComponentRecipes).Blocked, "A stale recipe hash must be rejected.");

        UiComposerWorkspacePlan workspacePlan = UiComposerService.PlanWorkspace(temp.Root, UiComposerService.DemoScreenId, Hashes(saved.AuthoringState), saved.AuthoringState.ResourceCatalog, saved.AuthoringState.ComponentRecipes, saved.AuthoringState.Blueprint);
        Require(!workspacePlan.Blocked, "A current three-layer workspace should pass unified preflight.");
        saved.AuthoringState.ComponentRecipes.Recipes[0].StatePresentation.ControllerName = "custom";
        UiComposerWorkspacePlan invalidPlan = UiComposerService.PlanWorkspace(temp.Root, UiComposerService.DemoScreenId, Hashes(saved.AuthoringState), saved.AuthoringState.ResourceCatalog, saved.AuthoringState.ComponentRecipes, saved.AuthoringState.Blueprint);
        Require(invalidPlan.Blocked && invalidPlan.Diagnostics.Any(item => item.Code == "UICOMP211"), "Unsupported button controllers must block unified preflight.");

        saved.AuthoringState.ComponentRecipes.Recipes[0].StatePresentation.ControllerName = "button";
        var cyclic = new UiComponentRecipe
        {
            ComponentId = "ui.component.test.cyclic",
            DisplayName = "Cyclic composite",
            ComponentType = "Composite",
            FairyGuiComponentName = "CyclicComposite",
            DefaultSize = new UiSize { Width = 100, Height = 100 },
            Template = new UiComponentTemplate
            {
                Nodes = new[]
                {
                    new UiScreenNode
                    {
                        NodeId = "self", Name = "self", Type = "ComponentInstance", Order = 0,
                        Layout = new UiNodeLayout { X = 0, Y = 0, Width = 100, Height = 100 },
                        Component = new UiComponentInstanceNode { ComponentRecipeId = "ui.component.test.cyclic" }
                    }
                }
            }
        };
        saved.AuthoringState.ComponentRecipes.Recipes = saved.AuthoringState.ComponentRecipes.Recipes.Append(cyclic).ToArray();
        UiComposerWorkspacePlan cyclicPlan = UiComposerService.PlanWorkspace(temp.Root, UiComposerService.DemoScreenId, Hashes(saved.AuthoringState), saved.AuthoringState.ResourceCatalog, saved.AuthoringState.ComponentRecipes, saved.AuthoringState.Blueprint);
        Require(cyclicPlan.Blocked && cyclicPlan.Diagnostics.Any(item => item.Code == "UICOMP006" && item.Message.Contains("cycle", StringComparison.OrdinalIgnoreCase)), "Composite recipe dependency cycles must be rejected.");
    }

    private static void WorkspaceApplyPreflightValidatesTheCompleteBlueprint()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        UiComposerAuthoringState state = UiComposerService.ReadState(temp.Root, UiComposerService.DemoScreenId);
        UiComposerExpectedHashes expected = Hashes(state);
        string catalogPath = Path.Combine(temp.Root, "ArtSource", "UIKits", "MxUiFoundation", "ui-art-resources.json");
        string recipePath = Path.Combine(temp.Root, "ArtSource", "UIKits", "MxUiFoundation", "ui-component-recipes.json");
        string blueprintPath = Path.Combine(temp.Root, "ArtSource", "UIKits", "MxUiFoundation", "screens", "ui_foundation_demo.ui-screen.json");
        string catalogHash = HashFile(catalogPath);
        string recipeHash = HashFile(recipePath);
        string blueprintHash = HashFile(blueprintPath);

        state.ResourceCatalog.Resources[0].Description += " Workspace preflight edit.";
        state.ComponentRecipes.Recipes[0].DisplayName = "Workspace preflight button";
        state.Blueprint.Nodes.First(node => node.Name == "btnPrimary").BindingSlot = "secondary";

        UiComposerWorkspacePlan plan = UiComposerService.PlanWorkspace(
            temp.Root,
            state.Screen.ScreenId,
            expected,
            state.ResourceCatalog,
            state.ComponentRecipes,
            state.Blueprint);
        Require(plan.Blocked && plan.Diagnostics.Any(item => item.Code == "UICOMP004"), "Unified workspace preflight must run the complete binding-contract validation.");

        bool threw = false;
        try
        {
            UiComposerService.ApplyWorkspace(
                temp.Root,
                state.Screen.ScreenId,
                expected,
                state.ResourceCatalog,
                state.ComponentRecipes,
                state.Blueprint);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Require(threw, "Blocked workspace apply must throw before writing.");
        Require(HashFile(catalogPath) == catalogHash, "Blocked workspace apply must not change the resource catalog.");
        Require(HashFile(recipePath) == recipeHash, "Blocked workspace apply must not change the recipe catalog.");
        Require(HashFile(blueprintPath) == blueprintHash, "Blocked workspace apply must not change the screen blueprint.");
    }

    private static void WorkspaceApplyRollsBackWhenQueueingFails()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        UiComposerAuthoringState state = UiComposerService.ReadState(temp.Root, UiComposerService.DemoScreenId);
        UiComposerExpectedHashes expected = Hashes(state);
        string catalogPath = Path.Combine(temp.Root, "ArtSource", "UIKits", "MxUiFoundation", "ui-art-resources.json");
        string recipePath = Path.Combine(temp.Root, "ArtSource", "UIKits", "MxUiFoundation", "ui-component-recipes.json");
        string blueprintPath = Path.Combine(temp.Root, "ArtSource", "UIKits", "MxUiFoundation", "screens", "ui_foundation_demo.ui-screen.json");
        string catalogHash = HashFile(catalogPath);
        string recipeHash = HashFile(recipePath);
        string blueprintHash = HashFile(blueprintPath);

        state.ResourceCatalog.Resources[0].Description += " Transaction edit.";
        state.ComponentRecipes.Recipes[0].DisplayName = "Transactional button";
        state.Blueprint.DisplayName = "Transactional demo screen";
        UiComposerWorkspacePlan plan = UiComposerService.PlanWorkspace(
            temp.Root,
            state.Screen.ScreenId,
            expected,
            state.ResourceCatalog,
            state.ComponentRecipes,
            state.Blueprint);
        Require(!plan.Blocked, "Valid workspace edits should pass before exercising transaction rollback.");

        File.WriteAllText(Path.Combine(temp.Root, "FGUIProject", ".agent"), "queue directory blocker", new UTF8Encoding(false));
        bool threw = false;
        try
        {
            UiComposerService.ApplyWorkspace(
                temp.Root,
                state.Screen.ScreenId,
                expected,
                state.ResourceCatalog,
                state.ComponentRecipes,
                state.Blueprint);
        }
        catch (IOException)
        {
            threw = true;
        }

        Require(threw, "Queue failure must surface to the workspace caller.");
        Require(HashFile(catalogPath) == catalogHash, "Queue failure must roll back the resource catalog.");
        Require(HashFile(recipePath) == recipeHash, "Queue failure must roll back the recipe catalog.");
        Require(HashFile(blueprintPath) == blueprintHash, "Queue failure must roll back the screen blueprint.");
        Require(!Directory.GetFiles(temp.Root, "*.tmp-*", SearchOption.AllDirectories).Any(), "Workspace rollback must clean temporary files.");
        Require(!Directory.GetFiles(temp.Root, "*.bak-*", SearchOption.AllDirectories).Any(), "Workspace rollback must consume backup files.");
    }

    private static void QueueProbeUsesReadOnlyHelperAction()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        UiComposerQueuedOperation operation = UiComposerService.QueueProbe(temp.Root, "MxUiFoundationDemo");
        string requestPath = Path.Combine(temp.Root, operation.RequestPath.Replace('/', Path.DirectorySeparatorChar));
        string json = File.ReadAllText(requestPath);
        Require(json.Contains("\"action\": \"Probe\"", StringComparison.Ordinal), "Helper readiness must use an explicit Probe operation.");
        Require(!json.Contains("\"materialization\": {", StringComparison.Ordinal), "Probe must not carry a materialization payload.");
        Require(!Directory.Exists(Path.Combine(temp.Root, "FGUIProject", "assets", "MxUiFoundationDemo")), "Probe queueing must not touch FairyGUI package source.");
    }

    private static void PublishAllRejectsConflictingVerificationModes()
    {
        bool threw = false;
        try
        {
            UiComposerPublishAllCommands.Dispatch(
                new[]
                {
                    "ui-composer", "publish-all", "--project-root", Directory.GetCurrentDirectory(),
                    "--screen", UiComposerService.DemoScreenId, "--fast", "--verify-unity"
                },
                new JsonSerializerOptions());
        }
        catch (ArgumentException)
        {
            threw = true;
        }
        Require(threw, "Fast and Unity verification modes must be mutually exclusive.");
    }

    private static void PublishAllPreflightStopsBeforeHelperOperations()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        UiComposerAuthoringState state = UiComposerService.ReadState(temp.Root, UiComposerService.DemoScreenId);
        state.Blueprint.Nodes.First(node => node.Name == "btnPrimary").BindingSlot = "missing-slot";
        string blueprintPath = Path.Combine(temp.Root, state.Screen.BlueprintPath.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllText(blueprintPath, JsonSerializer.Serialize(state.Blueprint, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

        var orchestrator = new UiComposerPublishAllOrchestrator(new JsonSerializerOptions { WriteIndented = true }, null);
        UiComposerPublishAllResult result = orchestrator.Run(new UiComposerPublishAllRequest
        {
            ProjectRoot = temp.Root,
            ScreenId = UiComposerService.DemoScreenId,
            VerificationLevel = UiComposerPublishVerificationLevel.Fast
        });

        Require(!result.Success && result.FailureCode == UiComposerPublishFailureCode.PreflightBlocked, "Invalid Composer authority must stop publish-all in preflight.");
        string agentPath = Path.Combine(temp.Root, "FGUIProject", ".agent");
        Require(!Directory.Exists(agentPath) || Directory.GetFiles(agentPath, "ui-composer-*.request.json").Length == 0, "Blocked preflight must not queue Probe, Apply or Publish.");
    }

    private static void PublishAllCancellationRemovesOnlyUnclaimedRequest()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var orchestrator = new UiComposerPublishAllOrchestrator(
            new JsonSerializerOptions { WriteIndented = true },
            null);
        UiComposerPublishAllResult result = orchestrator.Run(
            new UiComposerPublishAllRequest
            {
                ProjectRoot = temp.Root,
                ScreenId = UiComposerService.DemoScreenId,
                VerificationLevel = UiComposerPublishVerificationLevel.Fast
            },
            cancellation.Token);

        Require(
            !result.Success && result.FailureCode == UiComposerPublishFailureCode.Cancelled,
            "A cancelled publish-all operation must return the stable Cancelled failure code.");
        string agentPath = Path.Combine(temp.Root, "FGUIProject", ".agent");
        Require(
            !Directory.Exists(agentPath)
            || Directory.GetFiles(agentPath, "ui-composer-*.request.json").Length == 0,
            "Cancellation must remove this operation's unclaimed helper request.");
    }

    private static void MaterializesBlueprintContainersAndValidatesStructure()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        UiComposerAuthoringState initial = UiComposerService.ReadState(temp.Root, UiComposerService.DemoScreenId);
        Require(initial.Blueprint.SchemaVersion == "mx.uiScreenBlueprint.v1" && initial.Blueprint.Containers.Length == 0, "V1 blueprints should load without eager migration.");

        UiScreenNode title = initial.Blueprint.Nodes.First(node => node.NodeId == "title");
        UiScreenNode status = initial.Blueprint.Nodes.First(node => node.NodeId == "status");
        title.ParentContainerId = "copy";
        status.ParentContainerId = "copy";
        title.Layout.X = 0; title.Layout.Y = 0;
        status.Layout.X = 0; status.Layout.Y = 0;
        initial.Blueprint.SchemaVersion = "mx.uiScreenBlueprint.v2";
        initial.Blueprint.Containers = new[]
        {
            new UiLayoutContainer
            {
                ContainerId = "copy", Name = "Copy", Mode = "Vertical", Order = 2,
                Layout = new UiNodeLayout { X = 700, Y = 300, Width = 520, Height = 150, HorizontalAnchor = "Center", VerticalAnchor = "Middle" },
                Padding = new UiContainerPadding { Left = 20, Top = 10, Right = 20, Bottom = 10 }, GapY = 12, Columns = 1,
                MainAxisAlign = "Start", CrossAxisAlign = "Center"
            }
        };
        UiComposerValidationResult valid = UiComposerService.ValidateBlueprint(temp.Root, initial.Screen.ScreenId, initial.Blueprint);
        Require(valid.Success, "A valid vertical container should pass validation.");
        UiComposerAuthoringState saved = UiComposerService.SaveBlueprint(temp.Root, initial.Screen.ScreenId, initial.BlueprintHash, initial.ContractHash, initial.Blueprint);
        UiComposerOperationPlan plan = UiComposerService.PlanApply(temp.Root, saved.Screen.ScreenId, Hashes(saved));
        Require(!plan.Blocked, "A V2 container blueprint should produce an apply plan.");
        Require(plan.Materialization.PanelComponentXmlTemplate.Contains("name=\"title\" xy=\"720,310\"", StringComparison.Ordinal), "Vertical layout should flatten the title to a deterministic absolute position.");
        Require(plan.Materialization.PanelComponentXmlTemplate.Contains("name=\"status\" xy=\"740,376\"", StringComparison.Ordinal), "Vertical layout should place the second child after gap and preserve its size.");

        initial.Blueprint.Containers[0].ParentContainerId = "copy";
        initial.Blueprint.Containers[0].Mode = "Grid";
        initial.Blueprint.Containers[0].Columns = 0;
        UiComposerValidationResult invalid = UiComposerService.ValidateBlueprint(temp.Root, initial.Screen.ScreenId, initial.Blueprint);
        Require(!invalid.Success && invalid.Diagnostics.Any(item => item.Code == "UICOMP013"), "Container cycles must be rejected.");
        Require(invalid.Diagnostics.Any(item => item.Code == "UICOMP014"), "Invalid grid parameters must be rejected.");
    }

    private static void MaterializesAndSynchronizesControlledListNode()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        UiComposerAuthoringState initial = UiComposerService.ReadState(temp.Root, UiComposerService.DemoScreenId);
        var listNode = new UiScreenNode
        {
            NodeId = "items",
            Name = "items",
            Type = "List",
            Order = initial.Blueprint.Nodes.Length,
            Layout = new UiNodeLayout { X = 120, Y = 180, Width = 420, Height = 320, HorizontalAnchor = "Left", VerticalAnchor = "Top" },
            List = new UiListNodeStyle
            {
                ItemComponentRecipeId = "ui.component.button.command.neutral",
                Layout = "Column",
                LineGap = 8,
                ColumnGap = 12,
                SelectionMode = "Single",
                Overflow = "Scroll",
                Virtualized = false
            }
        };
        initial.Blueprint.Nodes = initial.Blueprint.Nodes.Concat(new[] { listNode }).ToArray();

        initial.Blueprint.SchemaVersion = "mx.uiScreenBlueprint.v2";
        UiComposerValidationResult legacy = UiComposerService.ValidateBlueprint(temp.Root, initial.Screen.ScreenId, initial.Blueprint);
        Require(!legacy.Success && legacy.Diagnostics.Any(item => item.Code == "UICOMP001"), "List nodes must require the V3 screen blueprint schema.");

        initial.Blueprint.SchemaVersion = "mx.uiScreenBlueprint.v3";
        listNode.List.LineGap = -1;
        UiComposerValidationResult invalid = UiComposerService.ValidateBlueprint(temp.Root, initial.Screen.ScreenId, initial.Blueprint);
        Require(!invalid.Success && invalid.Diagnostics.Any(item => item.Code == "UICOMP016"), "List properties outside the controlled subset must be rejected.");
        listNode.List.LineGap = 8;

        UiComposerValidationResult valid = UiComposerService.ValidateBlueprint(temp.Root, initial.Screen.ScreenId, initial.Blueprint);
        Require(valid.Success, "A V3 list with a known item recipe should pass validation.");
        UiComposerAuthoringState saved = UiComposerService.SaveBlueprint(temp.Root, initial.Screen.ScreenId, initial.BlueprintHash, initial.ContractHash, initial.Blueprint);
        UiComposerOperationPlan apply = UiComposerService.PlanApply(temp.Root, saved.Screen.ScreenId, Hashes(saved));
        Require(!apply.Blocked, "A controlled List node should produce an apply plan.");
        string template = apply.Materialization.PanelComponentXmlTemplate;
        Require(template.Contains("<list", StringComparison.Ordinal) && template.Contains("name=\"items\"", StringComparison.Ordinal), "List nodes should materialize as FairyGUI list elements.");
        Require(template.Contains("overflow=\"scroll\" lineGap=\"8\" colGap=\"12\"", StringComparison.Ordinal), "List scrolling and gaps should reach FairyGUI source XML.");
        Require(template.Contains("defaultItem=\"ui://{{PACKAGE_DESCRIPTION_ID}}{{COMPONENT_UI_COMPONENT_BUTTON_COMMAND_NEUTRAL}}\"", StringComparison.Ordinal), "List default items should use the mapped component recipe URL.");
        Require(apply.Materialization.Components.Any(item => item.ComponentRecipeId == "ui.component.button.command.neutral"), "The List item recipe should participate in materialization dependencies.");
        UiComposerWorkspaceState workspace = UiComposerService.ReadWorkspace(temp.Root, saved.Screen.ScreenId);
        Require(workspace.References.Any(item => item.SourceId == "items" && item.TargetId == "ui.component.button.command.neutral" && item.Role == "item"), "Workspace references should index List item recipes.");

        UiScreenNode savedList = saved.Blueprint.Nodes.First(node => node.NodeId == "items");
        savedList.List.Layout = "Row";
        savedList.List.SelectionMode = "None";
        UiComposerAuthoringState rowSaved = UiComposerService.SaveBlueprint(temp.Root, saved.Screen.ScreenId, saved.BlueprintHash, saved.ContractHash, saved.Blueprint);
        UiComposerOperationPlan rowApply = UiComposerService.PlanApply(temp.Root, rowSaved.Screen.ScreenId, Hashes(rowSaved));
        Require(rowApply.Materialization.PanelComponentXmlTemplate.Contains("layout=\"row\" selectionMode=\"none\" overflow=\"scroll\" scroll=\"horizontal\"", StringComparison.Ordinal), "Row Lists should materialize FairyGUI horizontal scrolling and selection mode.");
        UiScreenNode restoredList = rowSaved.Blueprint.Nodes.First(node => node.NodeId == "items");
        restoredList.List.Layout = "Column";
        restoredList.List.SelectionMode = "Single";
        restoredList.List.ItemComponentRecipeId = "ui.component.button.generic";
        saved = UiComposerService.SaveBlueprint(temp.Root, rowSaved.Screen.ScreenId, rowSaved.BlueprintHash, rowSaved.ContractHash, rowSaved.Blueprint);

        UiComposerAuthoringState materialized = temp.Materialize();
        string panelPath = Path.Combine(temp.Root, "FGUIProject", "assets", "MxUiFoundationDemo", "UiFoundationDemoPanel.xml");
        string panel = File.ReadAllText(panelPath).Replace(
            "overflow=\"scroll\" lineGap=\"8\" colGap=\"12\" defaultItem=",
            "layout=\"row\" selectionMode=\"multipleSingleClick\" overflow=\"scroll\" scroll=\"horizontal\" lineGap=\"10\" colGap=\"14\" defaultItem=",
            StringComparison.Ordinal);
        File.WriteAllText(panelPath, panel, new UTF8Encoding(false));
        UiComposerAuthoringState drifted = UiComposerService.ReadState(temp.Root, materialized.Screen.ScreenId);
        UiComposerSyncPlan sync = UiComposerService.PlanSync(temp.Root, drifted.Screen.ScreenId, Hashes(drifted));
        Require(!sync.Blocked, "Controlled FairyGUI List edits should be synchronizable.");
        UiListNodeStyle synchronized = sync.ProposedBlueprint.Nodes.First(node => node.NodeId == "items").List;
        Require(synchronized.Layout == "Row" && synchronized.LineGap == 10 && synchronized.ColumnGap == 14, "List layout and gaps should round-trip from FairyGUI XML.");
        Require(synchronized.SelectionMode == "MultipleSingleClick" && !synchronized.Virtualized, "List selection should round-trip while runtime virtualization intent is preserved.");

        panel = panel.Replace("name=\"items\"", "name=\"items\" autoItemSize=\"false\"", StringComparison.Ordinal);
        File.WriteAllText(panelPath, panel, new UTF8Encoding(false));
        UiComposerAuthoringState unsupported = UiComposerService.ReadState(temp.Root, materialized.Screen.ScreenId);
        UiComposerSyncPlan blocked = UiComposerService.PlanSync(temp.Root, unsupported.Screen.ScreenId, Hashes(unsupported));
        Require(blocked.Blocked && blocked.Diagnostics.Any(item => item.Code == "UICOMP009"), "Uncontrolled FairyGUI List properties must block synchronization.");
    }

    internal static void PreservesExplicitFontsThroughPublishAndSync()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        var state = UiComposerService.ReadState(temp.Root, UiComposerService.DemoScreenId);
        var label = state.Blueprint.Nodes.First(node => node.Text != null);
        label.Text.Font = "MxUI Serif SC";
        label.Text.Bold = false;
        state = UiComposerService.SaveBlueprint(temp.Root, state.Screen.ScreenId, state.BlueprintHash, state.ContractHash, state.Blueprint);
        var plan = UiComposerService.PlanApply(temp.Root, state.Screen.ScreenId, Hashes(state));
        Require(!plan.Blocked, "Explicit bundled fonts must pass preflight.");
        Require(plan.Materialization.PanelComponentXmlTemplate.Contains("font=\"MxUI Serif SC\""), "Text font must reach the published component.");
        var button = System.Xml.Linq.XDocument.Parse(plan.Materialization.Components[0].XmlTemplate)
            .Descendants("text").First(e => (string)e.Attribute("name") == "title");
        Require((string)button.Attribute("font") == "MxUI Sans SC" && button.Attribute("bold") == null,
            "Buttons must use the real Medium face without synthetic bold.");
        state = temp.Materialize();
        string path = Path.Combine(temp.Root, "FGUIProject/assets/MxUiFoundationDemo/UiFoundationDemoPanel.xml");
        File.WriteAllText(path, File.ReadAllText(path).Replace("font=\"MxUI Serif SC\"", "font=\"MxUI Sans SC\""));
        state = UiComposerService.ReadState(temp.Root, state.Screen.ScreenId);
        var sync = UiComposerService.PlanSync(temp.Root, state.Screen.ScreenId, Hashes(state));
        Require(!sync.Blocked, "Supported font edits must survive FairyGUI sync.");
        Require(sync.ProposedBlueprint.Nodes.First(n => n.NodeId == label.NodeId).Text.Font == "MxUI Sans SC", "Sync must preserve the chosen font.");
    }

    private static void LoadsBlueprintAndBuildsStableApplyPlan()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        UiComposerAuthoringState state = UiComposerService.ReadState(temp.Root, UiComposerService.DemoScreenId);
        Require(state.ErrorCount == 0, "Initial UI Composer state should be valid.");
        Require(state.Blueprint.Nodes.Length == 7, "Sample blueprint should contain seven nodes.");
        Require(state.BindingSlots.Count == 4, "C# binding contract should expose four slots.");

        UiComposerExpectedHashes expected = Hashes(state);
        UiComposerOperationPlan first = UiComposerService.PlanApply(temp.Root, state.Screen.ScreenId, expected);
        UiComposerOperationPlan second = UiComposerService.PlanApply(temp.Root, state.Screen.ScreenId, expected);
        Require(!first.Blocked, "Initial sample apply plan should not be blocked.");
        Require(first.Materialization != null, "Apply plan should contain materialization templates.");
        Require(first.Materialization.PanelComponentXmlTemplate == second.Materialization.PanelComponentXmlTemplate, "Panel materialization must be deterministic.");
        Require(first.Materialization.PackageXmlTemplate.Contains("scale9grid=\"24,18,192,28\"", StringComparison.Ordinal), "Package template should carry semantic NineSlice metadata.");
        Require(!first.Materialization.PanelComponentXmlTemplate.Contains(temp.Root, StringComparison.OrdinalIgnoreCase), "Materialization templates must not contain absolute paths.");
    }

    private static void LoadsCharacterTestMenuContractAndSemanticImages()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        UiComposerAuthoringState state = UiComposerService.ReadState(temp.Root, UiComposerService.DefaultScreenId);
        Require(state.ErrorCount == 0, "CharacterTest main menu blueprint should satisfy its generated binding contract.");
        Require(state.BindingSlots.Count == 10, "CharacterTest main menu contract should expose three text and seven command slots.");
        Require(state.Blueprint.Nodes.Count(item => item.Type == "Image") == 3, "CharacterTest main menu should use three semantic Image nodes.");
        Require(state.Blueprint.Nodes.Count(item => item.Component?.IconResourceId?.Length > 0) == 7, "Every main menu command should select a semantic icon.");
        UiComposerOperationPlan plan = UiComposerService.PlanApply(temp.Root, state.Screen.ScreenId, Hashes(state));
        Require(!plan.Blocked, "CharacterTest main menu apply plan should be valid.");
        Require(plan.Materialization.Resources.Length == 12, "CharacterTest main menu should materialize exactly twelve production resources.");
        Require(plan.Materialization.Components.Length == 3, "CharacterTest main menu should materialize three command button recipes.");
        Require(plan.Materialization.PanelComponentXmlTemplate.Contains("name=\"panelFrame\"", StringComparison.Ordinal), "Semantic panel image should reach FairyGUI XML.");
        Require(plan.Materialization.PanelComponentXmlTemplate.Contains(" icon=\"ui://{{PACKAGE_DESCRIPTION_ID}}", StringComparison.Ordinal), "Button icon resources should reach FairyGUI instance properties.");
    }

    private static void LoadsCharacterTestCombatHudContractAndSemanticAssets()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        UiComposerAuthoringState state = UiComposerService.ReadState(temp.Root, "ui.screen.character_test.combat_hud");
        Require(state.ErrorCount == 0, "CharacterTest Combat HUD blueprint should satisfy its generated binding contract.");
        Require(state.BindingSlots.Count == 183, "CharacterTest Combat HUD contract should protect all runtime-addressed controls.");
        Require(state.Blueprint.Nodes.Length == 183, "CharacterTest Combat HUD should preserve the existing runtime control surface.");
        UiComposerOperationPlan plan = UiComposerService.PlanApply(temp.Root, state.Screen.ScreenId, Hashes(state));
        Require(!plan.Blocked, "CharacterTest Combat HUD apply plan should be valid.");
        Require(plan.Materialization.Resources.Length == 14, "CharacterTest Combat HUD should materialize fourteen semantic HUD resources.");
        Require(plan.Materialization.Components.Length == 0, "Flat runtime-addressed HUD controls should not require synthetic component recipes.");
        Require(plan.Materialization.PanelComponentXmlTemplate.Contains("name=\"abilitySlot9CooldownText\"", StringComparison.Ordinal), "HUD materialization should preserve the ninth ability slot contract.");
        Require(plan.Materialization.PanelComponentXmlTemplate.Contains("sidePair=\"center-center,bottom-bottom\"", StringComparison.Ordinal), "HUD hotbar should compile centered bottom relations.");
    }

    private static void LoadsCharacterTestPlayerPanelContractsAndInputSemantics()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        string[] screenIds =
        {
            "ui.screen.character_test.settings",
            "ui.screen.character_test.network",
            "ui.screen.character_test.ability_binding"
        };
        int[] expectedBindings = { 60, 51, 44 };
        for (int i = 0; i < screenIds.Length; i++)
        {
            UiComposerAuthoringState state = UiComposerService.ReadState(temp.Root, screenIds[i]);
            Require(
                state.ErrorCount == 0,
                screenIds[i] + " should satisfy its binding contract. " +
                string.Join(" | ", state.Diagnostics.Select(item =>
                    item.Code + " " + item.Path + ": " + item.Message)));
            Require(state.BindingSlots.Count == expectedBindings[i], screenIds[i] + " should protect every authored control.");
            UiComposerOperationPlan plan = UiComposerService.PlanApply(temp.Root, screenIds[i], Hashes(state));
            Require(!plan.Blocked && plan.Materialization != null, screenIds[i] + " should produce an apply plan.");
            Require(plan.Materialization.PanelComponentXmlTemplate.Contains("name=\"panelFrame\"", StringComparison.Ordinal), screenIds[i] + " should materialize a semantic panel frame.");
        }

        UiComposerAuthoringState settings = UiComposerService.ReadState(temp.Root, screenIds[0]);
        UiComposerOperationPlan settingsPlan = UiComposerService.PlanApply(temp.Root, screenIds[0], Hashes(settings));
        Require(settingsPlan.Materialization.PanelComponentXmlTemplate.Contains("name=\"renderScaleInput\"", StringComparison.Ordinal), "Settings should retain render-scale input.");
        Require(settingsPlan.Materialization.PanelComponentXmlTemplate.Contains("input=\"true\" restrict=\"[0-9.]\" maxLength=\"5\"", StringComparison.Ordinal), "Settings input constraints should reach FairyGUI XML.");
        Require(settingsPlan.Materialization.PanelComponentXmlTemplate.Contains("name=\"inputList\"", StringComparison.Ordinal), "Settings should materialize the dynamic input binding list.");
        Require(settingsPlan.Materialization.PanelComponentXmlTemplate.Contains("defaultItem=", StringComparison.Ordinal), "Settings input list should reference its controlled row recipe.");
        Require(settingsPlan.Materialization.Components.Any(item => item.ComponentRelativePath.EndsWith("SettingsInputBindingRow.xml", StringComparison.Ordinal)), "Settings should materialize the dual-slot input row recipe.");

        UiComposerAuthoringState network = UiComposerService.ReadState(temp.Root, screenIds[1]);
        UiComposerOperationPlan networkPlan = UiComposerService.PlanApply(temp.Root, screenIds[1], Hashes(network));
        Require(networkPlan.Materialization.PanelComponentXmlTemplate.Contains("name=\"portInput\"", StringComparison.Ordinal), "Network should retain the port input.");
        Require(networkPlan.Materialization.PanelComponentXmlTemplate.Contains("input=\"true\" restrict=\"[0-9]\" maxLength=\"5\"", StringComparison.Ordinal), "Network port constraints should reach FairyGUI XML.");
    }

    private static void FairyGuiPublishTimeoutDoesNotLeaveARejectingRace()
    {
        string root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "AGENTS.md")))
        {
            DirectoryInfo parent = Directory.GetParent(root);
            if (parent == null)
                throw new InvalidOperationException("Repository root was not found.");
            root = parent.FullName;
        }

        string helperPath = Path.Combine(
            root,
            "FGUIProject",
            "plugins",
            "wgameframework-agent-helper",
            "main.js");
        string source = File.ReadAllText(helperPath);
        int functionStart = source.IndexOf("async function awaitWithTimeout", StringComparison.Ordinal);
        int functionEnd = source.IndexOf("function reportPublishPhase", functionStart, StringComparison.Ordinal);
        Require(functionStart >= 0 && functionEnd > functionStart, "FairyGUI publish timeout helper should remain discoverable.");

        string timeoutHelper = source.Substring(functionStart, functionEnd - functionStart);
        Require(!timeoutHelper.Contains("Promise.race([", StringComparison.Ordinal), "FairyGUI publish timeout must not leave a rejecting Promise.race loser after success.");
        Require(timeoutHelper.Contains("Stopwatch.StartNew", StringComparison.Ordinal), "FairyGUI publish timeout should retain a monotonic deadline.");
        Require(timeoutHelper.Contains("while (!settled)", StringComparison.Ordinal), "FairyGUI publish timeout should observe completion without an orphan timeout promise.");
    }

    private static void ExportsRuntimeAddressedCompositeRecipes()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        const string screenId = "ui.screen.player_guidance.package";
        UiComposerAuthoringState state = UiComposerService.ReadState(temp.Root, screenId);
        Require(state.ErrorCount == 0, "Player Guidance package blueprint should satisfy its component binding contract.");

        UiComposerOperationPlan plan = UiComposerService.PlanApply(temp.Root, screenId, Hashes(state));
        Require(!plan.Blocked && plan.Materialization != null, "Player Guidance package should produce an apply plan.");
        Require(
            plan.Materialization.PackageXmlTemplate.Contains(
                "name=\"GuidanceCardPanel.xml\" path=\"/Components/\" exported=\"true\"",
                StringComparison.Ordinal),
            "A Composite recipe claimed by componentBindings must be exported for direct runtime creation.");
        Require(
            plan.Materialization.PackageXmlTemplate.Contains(
                "name=\"GuidanceNoticeStackPanel.xml\" path=\"/Components/\" exported=\"true\"",
                StringComparison.Ordinal),
            "Every runtime-addressed guidance panel must be exported.");
        Require(
            plan.Materialization.PackageXmlTemplate.Contains(
                "name=\"GuidanceCardChrome.xml\" path=\"/Components/\"/>",
                StringComparison.Ordinal),
            "Purely internal component recipes should remain non-exported.");
    }

    private static void IconOnlyButtonsRemainCenteredWhenInstancesResize()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        const string screenId = "ui.screen.character_test.behavior_deck";
        UiComposerAuthoringState state = UiComposerService.ReadState(temp.Root, screenId);
        UiComposerOperationPlan plan = UiComposerService.PlanApply(temp.Root, screenId, Hashes(state));
        Require(!plan.Blocked && plan.Materialization != null, "Behavior Deck should produce an apply plan.");

        string[] iconOnlyRecipeIds =
        {
            "ui.component.button.icon.compact.neutral",
            "ui.component.button.icon.pagination.neutral"
        };
        for (int i = 0; i < iconOnlyRecipeIds.Length; i++)
        {
            UiComposerMaterializedComponent component = plan.Materialization.Components
                .Single(item => item.ComponentRecipeId == iconOnlyRecipeIds[i]);
            Require(
                component.XmlTemplate.Contains(
                    "<relation target=\"\" sidePair=\"center-center,middle-middle\"/>",
                    StringComparison.Ordinal),
                iconOnlyRecipeIds[i] + " should keep its icon centered when the component instance is resized.");
        }
    }

    private static void ProjectsV1FeedbackAsSilentAndValidatesV2()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        UiComposerAuthoringState legacy = UiComposerService.ReadState(
            temp.Root,
            UiComposerService.DemoScreenId);
        Require(
            legacy.BindingContract.SchemaVersion == "mx.uiComposerBindingContract.v1",
            "Demo should remain a real v1 compatibility fixture.");
        Require(
            legacy.BindingSlots
                .Where(item => !string.IsNullOrWhiteSpace(item.CommandId))
                .All(item => item.Feedback != null &&
                             item.Feedback.Activation == "None" &&
                             item.Feedback.Success == "None" &&
                             item.Feedback.Failure == "None"),
            "V1 command bindings must project as silent regardless of generator defaults.");

        UiComposerAuthoringState settings = UiComposerService.ReadState(
            temp.Root,
            "ui.screen.character_test.settings");
        Require(
            settings.BindingContract.SchemaVersion == "mx.uiComposerBindingContract.v2",
            "Settings should be the first generated v2 feedback contract.");
        UiComposerBindingSlot close = settings.BindingSlots.First(
            item => item.CommandId == "character_test.settings.close");
        UiComposerBindingSlot apply = settings.BindingSlots.First(
            item => item.CommandId == "character_test.settings.apply");
        Require(
            close.Feedback.Activation == "Back" &&
            close.Feedback.Success == "None" &&
            close.Feedback.Failure == "None",
            "Settings Close should expose read-only Back feedback.");
        Require(
            apply.Feedback.Activation == "None" &&
            apply.Feedback.Success == "Confirm" &&
            apply.Feedback.Failure == "Error",
            "Settings Apply should expose result feedback.");

        string contractPath = Path.Combine(
            temp.Root,
            "ArtSource",
            "UIKits",
            "MxUiFoundation",
            "contracts",
            "ui_foundation_demo.ui-composer-binding-contract.json");
        UiComposerBindingContract proposed = JsonSerializer.Deserialize<UiComposerBindingContract>(
            File.ReadAllText(contractPath));
        proposed.SchemaVersion = "mx.uiComposerBindingContract.v2";
        for (int i = 0; i < proposed.Bindings.Length; i++)
            proposed.Bindings[i].Feedback = null;
        File.WriteAllText(
            contractPath,
            JsonSerializer.Serialize(
                proposed,
                new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
            new UTF8Encoding(false));

        UiComposerAuthoringState invalid = UiComposerService.ReadState(
            temp.Root,
            UiComposerService.DemoScreenId);
        Require(
            invalid.Diagnostics.Any(item => item.Code == "UICOMP015"),
            "V2 command bindings without explicit feedback must fail preflight.");

        for (int i = 0; i < proposed.Bindings.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(proposed.Bindings[i].CommandId))
                continue;
            proposed.Bindings[i].Feedback = new UiComposerFeedbackPolicy
            {
                Activation = "Click",
                Success = "None",
                Failure = "None"
            };
        }
        File.WriteAllText(
            contractPath,
            JsonSerializer.Serialize(
                proposed,
                new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
            new UTF8Encoding(false));

        UiComposerAuthoringState valid = UiComposerService.ReadState(
            temp.Root,
            UiComposerService.DemoScreenId);
        Require(
            valid.ErrorCount == 0 &&
            valid.ContractHash != legacy.ContractHash,
            "Valid v2 feedback must participate in the contract hash and pass preflight.");
    }

    private static void BlocksInvalidBindingsAndStaleSave()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        UiComposerAuthoringState state = UiComposerService.ReadState(temp.Root, UiComposerService.DemoScreenId);
        UiScreenNode primary = state.Blueprint.Nodes.First(node => node.Name == "btnPrimary");
        primary.BindingSlot = "secondary";

        UiComposerValidationResult invalid = UiComposerService.ValidateBlueprint(temp.Root, state.Screen.ScreenId, state.Blueprint);
        Require(!invalid.Success && invalid.Diagnostics.Any(item => item.Code == "UICOMP004"), "Invalid binding assignment should be blocked by the C# contract.");

        UiComposerOperationPlan stale = UiComposerService.PlanSave(temp.Root, state.Screen.ScreenId, "stale", state.Blueprint);
        Require(stale.Blocked && stale.Diagnostics.Any(item => item.Code == "UICOMP008"), "Stale blueprint hash should block save.");
    }

    private static void QueueApplyWritesOnlyTemporaryRequest()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        UiComposerAuthoringState state = UiComposerService.ReadState(temp.Root, UiComposerService.DemoScreenId);
        UiComposerQueuedOperation operation = UiComposerService.QueueApply(temp.Root, state.Screen.ScreenId, Hashes(state));
        string requestPath = Path.Combine(temp.Root, operation.RequestPath.Replace('/', Path.DirectorySeparatorChar));
        Require(File.Exists(requestPath), "Queue apply should write an agent request.");
        Require(!Directory.Exists(Path.Combine(temp.Root, "FGUIProject", "assets", "MxUiFoundationDemo")), "Authoring Core must not create the FairyGUI package.");
        string request = File.ReadAllText(requestPath);
        Require(request.Contains("\"action\": \"Materialize\"", StringComparison.Ordinal), "Queued request should describe a materialize action.");
    }

    private static void SynchronizesSupportedXmlAndBlocksUnknownAttributes()
    {
        using TempUiComposerProject temp = TempUiComposerProject.Create();
        UiComposerAuthoringState initial = temp.Materialize();
        string panelPath = Path.Combine(temp.Root, "FGUIProject", "assets", "MxUiFoundationDemo", "UiFoundationDemoPanel.xml");
        string panel = File.ReadAllText(panelPath).Replace("xy=\"800,470\"", "xy=\"812,470\"", StringComparison.Ordinal);
        File.WriteAllText(panelPath, panel, new UTF8Encoding(false));

        UiComposerAuthoringState drifted = UiComposerService.ReadState(temp.Root, initial.Screen.ScreenId);
        UiComposerSyncPlan syncPlan = UiComposerService.PlanSync(temp.Root, drifted.Screen.ScreenId, Hashes(drifted));
        Require(!syncPlan.Blocked, "Supported FairyGUI layout changes should be synchronizable.");
        Require(syncPlan.Diffs.Any(item => item.NodeId == "primary"), "Sync plan should report the moved primary button.");
        UiComposerAuthoringState synced = UiComposerService.SyncBlueprint(temp.Root, drifted.Screen.ScreenId, Hashes(drifted));
        Require(synced.Blueprint.Nodes.First(node => node.Name == "btnPrimary").Layout.X == 812, "Supported XY changes should round-trip into the blueprint.");
        Require(!synced.Diagnostics.Any(item => item.Code == "UICOMP108"), "Successful sync should advance the source XML hash.");

        panel = File.ReadAllText(panelPath).Replace("name=\"panel\"", "name=\"panel\" rotation=\"5\"", StringComparison.Ordinal);
        File.WriteAllText(panelPath, panel, new UTF8Encoding(false));
        UiComposerAuthoringState unsupported = UiComposerService.ReadState(temp.Root, synced.Screen.ScreenId);
        UiComposerSyncPlan blocked = UiComposerService.PlanSync(temp.Root, unsupported.Screen.ScreenId, Hashes(unsupported));
        Require(blocked.Blocked && blocked.Diagnostics.Any(item => item.Code == "UICOMP009"), "Unknown FairyGUI attributes must block synchronization.");
    }

    private static UiComposerExpectedHashes Hashes(UiComposerAuthoringState state)
    {
        return new UiComposerExpectedHashes
        {
            BlueprintHash = state.BlueprintHash,
            RecipeCatalogHash = state.RecipeCatalogHash,
            CatalogHash = state.CatalogHash,
            ContractHash = state.ContractHash,
            SourceXmlHash = state.SourceXmlHash,
            MappingHash = state.MappingHash
        };
    }

    private static string HashFile(string path)
    {
        using SHA256 sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(path))).ToLowerInvariant();
    }

    private static string FindTestRepositoryRoot()
    {
        string current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "FGUIProject", "FGUIProject.fairy")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException("Could not locate repository root for UI Composer helper tests.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private sealed class TempUiComposerProject : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        private TempUiComposerProject(string root) { Root = root; }
        public string Root { get; }

        public static TempUiComposerProject Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "mx-ui-composer-" + Guid.NewGuid().ToString("N"));
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/ui-art-resources.json");
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/ui-component-recipes.json");
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/screens/ui_foundation_demo.ui-screen.json");
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/screens/character_test_main_menu.ui-screen.json");
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/screens/character_test_combat_hud.ui-screen.json");
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/screens/character_test_settings.ui-screen.json");
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/screens/character_test_network.ui-screen.json");
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/screens/character_test_ability_binding.ui-screen.json");
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/screens/character_test_behavior_deck.ui-screen.json");
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/screens/player_guidance_package.ui-screen.json");
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/contracts/ui_foundation_demo.ui-composer-binding-contract.json");
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/contracts/character_test_main_menu.ui-composer-binding-contract.json");
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/contracts/character_test_combat_hud.ui-composer-binding-contract.json");
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/contracts/character_test_settings.ui-composer-binding-contract.json");
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/contracts/character_test_network.ui-composer-binding-contract.json");
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/contracts/character_test_ability_binding.ui-composer-binding-contract.json");
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/contracts/character_test_behavior_deck.ui-composer-binding-contract.json");
            CopyProjectFile(root, "ArtSource/UIKits/MxUiFoundation/contracts/player_guidance_package.ui-composer-binding-contract.json");
            CopyProjectDirectory(root, "ArtSource/UIKits/MxUiFoundation/images");
            Directory.CreateDirectory(Path.Combine(root, "FGUIProject", "assets"));
            return new TempUiComposerProject(root);
        }

        public UiComposerAuthoringState Materialize()
        {
            UiComposerAuthoringState state = UiComposerService.ReadState(Root, UiComposerService.DemoScreenId);
            UiComposerOperationPlan plan = UiComposerService.PlanApply(Root, state.Screen.ScreenId, Hashes(state));
            UiComposerMaterializationPayload payload = plan.Materialization;
            string packageRoot = Path.Combine(Root, "FGUIProject", "assets", "MxUiFoundationDemo");
            Directory.CreateDirectory(Path.Combine(packageRoot, "Components"));
            Directory.CreateDirectory(Path.Combine(packageRoot, "Images", "MxUiFoundation", "buttons"));
            const string packageDescriptionId = "mxuifd0";
            const string imageId = "mxuifdi";
            const string buttonId = "mxuifdb";
            const string panelId = "mxuifdp";
            string ReplaceIds(string value) => value
                .Replace("{{PACKAGE_DESCRIPTION_ID}}", packageDescriptionId, StringComparison.Ordinal)
                .Replace(payload.Resources[0].Token, imageId, StringComparison.Ordinal)
                .Replace(payload.Components[0].Token, buttonId, StringComparison.Ordinal)
                .Replace("{{PANEL_COMPONENT_ID}}", panelId, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(packageRoot, "package.xml"), ReplaceIds(payload.PackageXmlTemplate), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(packageRoot, payload.Components[0].ComponentRelativePath.Replace('/', Path.DirectorySeparatorChar)), ReplaceIds(payload.Components[0].XmlTemplate), new UTF8Encoding(false));
            string panelPath = Path.Combine(packageRoot, "UiFoundationDemoPanel.xml");
            File.WriteAllText(panelPath, ReplaceIds(payload.PanelComponentXmlTemplate), new UTF8Encoding(false));
            File.Copy(
                Path.Combine(Root, payload.Resources[0].SourcePngPath.Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(packageRoot, payload.Resources[0].TargetPngRelativePath.Replace('/', Path.DirectorySeparatorChar)),
                true);
            var packageState = new UiComposerPackageState
            {
                SchemaVersion = "mx.uiComposerPackageState.v1",
                Owner = "MxUiComposer",
                ScreenId = payload.ScreenId,
                PackageId = payload.PackageId,
                PackageDescriptionId = packageDescriptionId,
                ImageResourceId = imageId,
                ButtonComponentId = buttonId,
                PanelComponentId = panelId,
                BlueprintHash = payload.BlueprintHash,
                CatalogHash = payload.CatalogHash,
                RecipeCatalogHash = payload.RecipeCatalogHash,
                ContractHash = payload.ContractHash,
                SourceXmlHash = HashFile(panelPath),
                NodeMappings = payload.NodeMappings,
                ResourceMappings = new[] { new UiComposerResourceMapping { ResourceId = payload.Resources[0].ResourceId, FairyGuiResourceId = imageId, TargetRelativePath = payload.Resources[0].TargetPngRelativePath } },
                ComponentMappings = new[] { new UiComposerComponentMapping { ComponentRecipeId = payload.Components[0].ComponentRecipeId, FairyGuiComponentId = buttonId, ComponentName = payload.Components[0].ComponentName, ComponentRelativePath = payload.Components[0].ComponentRelativePath } }
            };
            File.WriteAllText(Path.Combine(packageRoot, "ui-composer-state.json"), JsonSerializer.Serialize(packageState, JsonOptions) + Environment.NewLine, new UTF8Encoding(false));
            return UiComposerService.ReadState(Root, payload.ScreenId);
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, true); } catch { }
        }

        private static void CopyProjectFile(string tempRoot, string relativePath)
        {
            string source = Path.Combine(FindRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
            string target = Path.Combine(tempRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? tempRoot);
            File.Copy(source, target, true);
        }

        private static void CopyProjectDirectory(string tempRoot, string relativePath)
        {
            string sourceRoot = Path.Combine(FindRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
            foreach (string source in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(sourceRoot, source);
                string target = Path.Combine(tempRoot, relativePath.Replace('/', Path.DirectorySeparatorChar), relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? tempRoot);
                File.Copy(source, target, true);
            }
        }

        private static string FindRepositoryRoot()
        {
            string current = Directory.GetCurrentDirectory();
            while (!File.Exists(Path.Combine(current, "AGENTS.md")))
            {
                DirectoryInfo parent = Directory.GetParent(current);
                if (parent == null)
                    throw new InvalidOperationException("Repository root was not found.");
                current = parent.FullName;
            }
            return current;
        }
    }
}
