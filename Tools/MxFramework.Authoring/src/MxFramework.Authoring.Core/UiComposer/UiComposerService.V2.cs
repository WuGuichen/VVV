using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using MxFramework.Authoring.FairyGuiArtSlices;

namespace MxFramework.Authoring.UiComposer
{
    /// <summary>Low-frequency authoring service for contract-driven FairyGUI screen composition.</summary>
    /// <remarks>LowFreqAlloc. This service is not used by runtime UI or gameplay ticks.</remarks>
    public static partial class UiComposerService
    {
        public const string AuthoringApiVersion = "mx.uiComposer.authoring.v3";
        public const string DefaultScreenId = "ui.screen.character_test.main_menu";
        public const string DefaultBlueprintPath = "ArtSource/UIKits/MxUiFoundation/screens/character_test_main_menu.ui-screen.json";
        public const string DemoScreenId = "ui.screen.foundation.demo";
        public const string DemoBlueprintPath = "ArtSource/UIKits/MxUiFoundation/screens/ui_foundation_demo.ui-screen.json";
        public const string DefaultRecipeCatalogPath = "ArtSource/UIKits/MxUiFoundation/ui-component-recipes.json";
        public const string DefaultLibraryId = "MxUiFoundation";

        private const string BlueprintSchemaV1 = "mx.uiScreenBlueprint.v1";
        private const string BlueprintSchemaV2 = "mx.uiScreenBlueprint.v2";
        private const string BlueprintSchemaV3 = "mx.uiScreenBlueprint.v3";
        private const string ContractSchemaV1 = "mx.uiComposerBindingContract.v1";
        private const string ContractSchemaV2 = "mx.uiComposerBindingContract.v2";
        private const string RecipeSchema = "mx.uiComponentRecipeCatalog.v1";
        private const string StateOwner = "MxUiComposer";
        private const string AgentDirectory = "FGUIProject/.agent";
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static IReadOnlyList<UiComposerScreenDescriptor> ListScreens(string repositoryRoot)
        {
            EnsureRepositoryRoot(repositoryRoot);
            var screens = new List<UiComposerScreenDescriptor>();
            string kitsRoot = ResolvePath(repositoryRoot, UiArtResourceLibraryService.UiKitsRootPath);
            if (!Directory.Exists(kitsRoot))
                return screens;

            string[] paths = Directory.GetFiles(kitsRoot, "*.ui-screen.json", SearchOption.AllDirectories);
            for (int i = 0; i < paths.Length; i++)
            {
                try
                {
                    string blueprintPath = ToRelativePath(repositoryRoot, paths[i]);
                    UiScreenBlueprint blueprint = ReadJson<UiScreenBlueprint>(paths[i]);
                    string contractPath = ContractPathForBlueprint(blueprintPath);
                    if (!IsSafeTarget(blueprint.Target) || !File.Exists(ResolvePath(repositoryRoot, contractPath)))
                        continue;
                    UiComposerBindingContract contract = ReadBindingContract(
                        ResolvePath(repositoryRoot, contractPath));
                    if (!TargetMatchesContract(blueprint, contract))
                        continue;
                    screens.Add(Describe(blueprint, blueprintPath));
                }
                catch (Exception ex) when (ex is JsonException || ex is IOException || ex is InvalidOperationException)
                {
                }
            }

            screens.Sort((left, right) =>
            {
                if (left.ScreenId == DefaultScreenId) return -1;
                if (right.ScreenId == DefaultScreenId) return 1;
                return string.CompareOrdinal(left.ScreenId, right.ScreenId);
            });
            return screens;
        }

        public static UiComposerAuthoringState ReadState(string repositoryRoot, string screen)
        {
            ScreenContext context = ResolveContext(repositoryRoot, screen);
            UiArtResourceLibraryAuthoringState library = UiArtResourceLibraryService.ReadAuthoringState(repositoryRoot, context.Blueprint.LibraryId);
            UiComposerPackageState packageState = TryReadJson<UiComposerPackageState>(ResolvePath(repositoryRoot, context.StatePath));
            string sourceXmlHash = ComputeFileHash(ResolvePath(repositoryRoot, context.ComponentPath));
            IReadOnlyList<UiComposerDiagnostic> diagnostics = Validate(
                repositoryRoot,
                context,
                context.Blueprint,
                library,
                packageState,
                sourceXmlHash,
                validateSourceDrift: true);

            return new UiComposerAuthoringState(
                Describe(context.Blueprint, context.BlueprintPath),
                ComputeFileHash(ResolvePath(repositoryRoot, context.BlueprintPath)),
                ComputeFileHash(ResolvePath(repositoryRoot, context.RecipePath)),
                library.CatalogHash,
                ComputeFileHash(ResolvePath(repositoryRoot, context.ContractPath)),
                sourceXmlHash,
                ComputeFileHash(ResolvePath(repositoryRoot, context.StatePath)),
                context.Blueprint,
                context.Recipes,
                library.Catalog,
                context.Contract,
                packageState,
                context.Contract.Bindings,
                diagnostics);
        }

        public static UiComposerValidationResult ValidateBlueprint(string repositoryRoot, string screen, UiScreenBlueprint proposedBlueprint)
        {
            ScreenContext context = ResolveContext(repositoryRoot, screen);
            UiArtResourceLibraryAuthoringState library = UiArtResourceLibraryService.ReadAuthoringState(
                repositoryRoot,
                proposedBlueprint?.LibraryId ?? context.Blueprint.LibraryId);
            UiComposerPackageState packageState = TryReadJson<UiComposerPackageState>(ResolvePath(repositoryRoot, context.StatePath));
            return new UiComposerValidationResult(Validate(
                repositoryRoot,
                context,
                proposedBlueprint,
                library,
                packageState,
                ComputeFileHash(ResolvePath(repositoryRoot, context.ComponentPath)),
                validateSourceDrift: false));
        }

        public static UiComposerOperationPlan PlanSave(string repositoryRoot, string screen, string expectedBlueprintHash, UiScreenBlueprint proposedBlueprint)
        {
            ScreenContext context = ResolveContext(repositoryRoot, screen);
            return PlanSave(repositoryRoot, screen, expectedBlueprintHash, ComputeFileHash(ResolvePath(repositoryRoot, context.ContractPath)), proposedBlueprint);
        }

        public static UiComposerOperationPlan PlanSave(string repositoryRoot, string screen, string expectedBlueprintHash, string expectedContractHash, UiScreenBlueprint proposedBlueprint)
        {
            ScreenContext context = ResolveContext(repositoryRoot, screen);
            var diagnostics = new List<UiComposerDiagnostic>();
            ValidateExpectedHash(expectedBlueprintHash, ComputeFileHash(ResolvePath(repositoryRoot, context.BlueprintPath)), "UICOMP008", "Blueprint changed on disk. Refresh before saving.", context.BlueprintPath, diagnostics);
            ValidateExpectedHash(expectedContractHash, ComputeFileHash(ResolvePath(repositoryRoot, context.ContractPath)), "UICOMP008", "Binding contract changed on disk. Refresh before saving.", context.ContractPath, diagnostics);
            diagnostics.AddRange(ValidateBlueprint(repositoryRoot, screen, proposedBlueprint).Diagnostics);
            SortDiagnostics(diagnostics);
            return new UiComposerOperationPlan(
                "SaveBlueprint",
                HasErrors(diagnostics),
                !HasErrors(diagnostics),
                false,
                false,
                new[] { context.BlueprintPath },
                diagnostics,
                null,
                Array.Empty<UiComposerDiffEntry>());
        }

        public static UiComposerAuthoringState SaveBlueprint(string repositoryRoot, string screen, string expectedBlueprintHash, UiScreenBlueprint proposedBlueprint)
        {
            ScreenContext context = ResolveContext(repositoryRoot, screen);
            return SaveBlueprint(repositoryRoot, screen, expectedBlueprintHash, ComputeFileHash(ResolvePath(repositoryRoot, context.ContractPath)), proposedBlueprint);
        }

        public static UiComposerAuthoringState SaveBlueprint(string repositoryRoot, string screen, string expectedBlueprintHash, string expectedContractHash, UiScreenBlueprint proposedBlueprint)
        {
            UiComposerOperationPlan plan = PlanSave(repositoryRoot, screen, expectedBlueprintHash, expectedContractHash, proposedBlueprint);
            if (plan.Blocked)
                throw new InvalidOperationException("UI Composer blueprint save is blocked.");
            ScreenContext context = ResolveContext(repositoryRoot, screen);
            WriteTextAtomic(ResolvePath(repositoryRoot, context.BlueprintPath), Serialize(proposedBlueprint));
            return ReadState(repositoryRoot, proposedBlueprint.ScreenId);
        }

        public static UiComposerOperationPlan PlanApply(string repositoryRoot, string screen, UiComposerExpectedHashes expected)
        {
            ScreenContext context = ResolveContext(repositoryRoot, screen);
            UiComposerAuthoringState state = ReadState(repositoryRoot, screen);
            var diagnostics = new List<UiComposerDiagnostic>();
            ValidateAllHashes(state, context, expected, diagnostics, state.PackageState != null);
            diagnostics.AddRange(state.Diagnostics);
            if (state.PackageState != null && !string.Equals(state.PackageState.Owner, StateOwner, StringComparison.Ordinal))
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP010", "Target package is not owned by MxUiComposer.", context.StatePath));
            if (state.PackageState != null && !string.Equals(state.PackageState.SourceXmlHash, state.SourceXmlHash, StringComparison.Ordinal))
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP008", "FairyGUI source drifted from the synchronized mapping. Sync before applying.", context.ComponentPath));

            UiComposerMaterializationPayload payload = HasErrors(diagnostics) ? null : BuildMaterializationPayload(state, context);
            string[] targets = payload == null
                ? new[] { context.PackageXmlPath, context.ComponentPath, context.StatePath }
                : BuildMaterializationTargetPaths(context, payload);
            SortDiagnostics(diagnostics);
            return new UiComposerOperationPlan("ApplyToFairyGui", HasErrors(diagnostics), false, !HasErrors(diagnostics), false, targets, diagnostics, payload, Array.Empty<UiComposerDiffEntry>());
        }

        public static UiComposerQueuedOperation QueueApply(string repositoryRoot, string screen, UiComposerExpectedHashes expected)
        {
            UiComposerOperationPlan plan = PlanApply(repositoryRoot, screen, expected);
            if (plan.Blocked || plan.Materialization == null)
                throw new InvalidOperationException("UI Composer apply is blocked.");
            return QueueOperation(repositoryRoot, "Materialize", plan.Materialization, plan.Materialization.PackageId);
        }

        public static UiComposerSyncPlan PlanSync(string repositoryRoot, string screen, UiComposerExpectedHashes expected)
        {
            ScreenContext context = ResolveContext(repositoryRoot, screen);
            UiComposerAuthoringState state = ReadState(repositoryRoot, screen);
            var diagnostics = new List<UiComposerDiagnostic>();
            ValidateExpectedHash(expected?.BlueprintHash, state.BlueprintHash, "UICOMP008", "Blueprint changed on disk.", context.BlueprintPath, diagnostics);
            ValidateExpectedHash(expected?.ContractHash, state.ContractHash, "UICOMP008", "Binding contract changed on disk.", context.ContractPath, diagnostics);
            ValidateExpectedHash(expected?.SourceXmlHash, state.SourceXmlHash, "UICOMP008", "FairyGUI component changed again. Refresh before syncing.", context.ComponentPath, diagnostics);
            ValidateExpectedHash(expected?.MappingHash, state.MappingHash, "UICOMP008", "Composer mapping changed on disk.", context.StatePath, diagnostics);
            UiScreenBlueprint proposed = null;
            var diffs = new List<UiComposerDiffEntry>();
            if (state.PackageState == null || string.IsNullOrWhiteSpace(state.SourceXmlHash))
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP011", "No materialized FairyGUI screen is available to synchronize.", context.ComponentPath));
            else if (!HasErrors(diagnostics))
            {
                proposed = ParseSupportedComponentXml(repositoryRoot, state, context, diagnostics);
                if (proposed != null)
                    BuildDiff(state.Blueprint, proposed, diffs);
            }
            SortDiagnostics(diagnostics);
            return new UiComposerSyncPlan(HasErrors(diagnostics), proposed, diffs, diagnostics);
        }

        public static UiComposerAuthoringState SyncBlueprint(string repositoryRoot, string screen, UiComposerExpectedHashes expected)
        {
            ScreenContext context = ResolveContext(repositoryRoot, screen);
            UiComposerSyncPlan plan = PlanSync(repositoryRoot, screen, expected);
            if (plan.Blocked || plan.ProposedBlueprint == null)
                throw new InvalidOperationException("UI Composer synchronization is blocked.");
            string blueprintPath = ResolvePath(repositoryRoot, context.BlueprintPath);
            WriteTextAtomic(blueprintPath, Serialize(plan.ProposedBlueprint));
            UiComposerPackageState packageState = ReadJson<UiComposerPackageState>(ResolvePath(repositoryRoot, context.StatePath));
            packageState.SourceXmlHash = ComputeFileHash(ResolvePath(repositoryRoot, context.ComponentPath));
            packageState.BlueprintHash = ComputeFileHash(blueprintPath);
            packageState.ContractHash = ComputeFileHash(ResolvePath(repositoryRoot, context.ContractPath));
            WriteTextAtomic(ResolvePath(repositoryRoot, context.StatePath), Serialize(packageState));
            return ReadState(repositoryRoot, plan.ProposedBlueprint.ScreenId);
        }

        public static UiComposerOperationPlan PlanPublish(string repositoryRoot, string screen, UiComposerExpectedHashes expected)
        {
            ScreenContext context = ResolveContext(repositoryRoot, screen);
            UiComposerAuthoringState state = ReadState(repositoryRoot, screen);
            var diagnostics = new List<UiComposerDiagnostic>();
            ValidateAllHashes(state, context, expected, diagnostics, true);
            if (state.PackageState == null)
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP011", "Materialize the screen before publishing.", context.StatePath));
            else
            {
                if (!string.Equals(state.PackageState.BlueprintHash, state.BlueprintHash, StringComparison.Ordinal))
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP008", "Materialized source is older than the current blueprint.", context.ComponentPath));
                if (!string.IsNullOrEmpty(state.PackageState.ContractHash) && !string.Equals(state.PackageState.ContractHash, state.ContractHash, StringComparison.Ordinal))
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP008", "Materialized source uses an older binding contract.", context.ContractPath));
                if (!string.Equals(state.PackageState.SourceXmlHash, state.SourceXmlHash, StringComparison.Ordinal))
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP008", "FairyGUI source changed after materialization. Sync or apply before publishing.", context.ComponentPath));
            }
            SortDiagnostics(diagnostics);
            return new UiComposerOperationPlan("PublishFairyGui", HasErrors(diagnostics), false, false, !HasErrors(diagnostics), new[] { context.Contract.Target.PublishedPackagePath }, diagnostics, null, Array.Empty<UiComposerDiffEntry>());
        }

        public static UiComposerQueuedOperation QueuePublish(string repositoryRoot, string screen, UiComposerExpectedHashes expected)
        {
            ScreenContext context = ResolveContext(repositoryRoot, screen);
            UiComposerOperationPlan plan = PlanPublish(repositoryRoot, screen, expected);
            if (plan.Blocked)
                throw new InvalidOperationException("UI Composer publish is blocked.");
            return QueueOperation(repositoryRoot, "Publish", null, context.Blueprint.Target.PackageId);
        }

        /// <summary>Queues a read-only readiness probe for the FairyGUI project helper.</summary>
        /// <remarks>The helper does not inspect or modify package ownership state for this action.</remarks>
        public static UiComposerQueuedOperation QueueProbe(string repositoryRoot, string packageId)
        {
            EnsureRepositoryRoot(repositoryRoot);
            if (string.IsNullOrWhiteSpace(packageId))
                throw new ArgumentException("UI Composer package id is required.", nameof(packageId));
            return QueueOperation(repositoryRoot, "Probe", null, packageId);
        }

        public static UiComposerOperationStatus ReadOperationStatus(string repositoryRoot, string operationId)
        {
            string safeId = EnsureOperationId(operationId);
            string prefix = ResolvePath(repositoryRoot, AgentDirectory + "/ui-composer-" + safeId);
            UiComposerOperationResult result = TryReadJson<UiComposerOperationResult>(prefix + ".result.json");
            UiComposerOperationProgress progress = TryReadJson<UiComposerOperationProgress>(prefix + ".progress.json");
            string status = result != null ? (result.Ok ? "Succeeded" : "Failed")
                : File.Exists(prefix + ".processing.json") ? "Running"
                : File.Exists(prefix + ".request.json") ? "Queued"
                : "Unknown";
            return new UiComposerOperationStatus(safeId, status, progress, result);
        }

        private static UiComposerQueuedOperation QueueOperation(string repositoryRoot, string action, UiComposerMaterializationPayload materialization, string packageId)
        {
            string operationId = Guid.NewGuid().ToString("N");
            string directory = ResolvePath(repositoryRoot, AgentDirectory);
            Directory.CreateDirectory(directory);
            var request = new UiComposerOperationRequest
            {
                SchemaVersion = "mx.uiComposerOperation.v1",
                OperationId = operationId,
                Action = action,
                PackageId = packageId,
                Materialization = materialization
            };
            string relativePath = AgentDirectory + "/ui-composer-" + operationId + ".request.json";
            WriteTextAtomic(ResolvePath(repositoryRoot, relativePath), Serialize(request));
            return new UiComposerQueuedOperation(operationId, "Queued", relativePath);
        }

        private static UiComposerMaterializationPayload BuildMaterializationPayload(UiComposerAuthoringState state, ScreenContext context)
        {
            Dictionary<string, UiArtResource> resourcesById = (state.ResourceCatalog.Resources ?? Array.Empty<UiArtResource>()).ToDictionary(item => item.ResourceId, StringComparer.Ordinal);
            Dictionary<string, UiComponentRecipe> recipesById = (state.ComponentRecipes.Recipes ?? Array.Empty<UiComponentRecipe>()).ToDictionary(item => item.ComponentId, StringComparer.Ordinal);
            var usedResourceIds = new HashSet<string>(StringComparer.Ordinal);
            var usedRecipeIds = new HashSet<string>(StringComparer.Ordinal);
            UiScreenNode[] nodes = state.Blueprint.Nodes ?? Array.Empty<UiScreenNode>();
            for (int i = 0; i < nodes.Length; i++)
            {
                UiScreenNode node = nodes[i];
                if (node.Image != null && !string.IsNullOrWhiteSpace(node.Image.ResourceId))
                    usedResourceIds.Add(node.Image.ResourceId);
                if (node.List != null)
                    CollectRecipeDependencies(node.List.ItemComponentRecipeId, recipesById, usedRecipeIds, usedResourceIds);
                if (node.Component == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(node.Component.IconResourceId))
                    usedResourceIds.Add(node.Component.IconResourceId);
                CollectRecipeDependencies(node.Component.ComponentRecipeId, recipesById, usedRecipeIds, usedResourceIds);
            }

            UiComposerMaterializedResource[] resources = usedResourceIds.OrderBy(value => value, StringComparer.Ordinal)
                .Select(resourceId => BuildMaterializedResource(state.Blueprint.LibraryId, resourcesById[resourceId]))
                .ToArray();
            UiComposerMaterializedComponent[] components = usedRecipeIds.OrderBy(value => value, StringComparer.Ordinal)
                .Select(recipeId => BuildMaterializedComponent(recipesById[recipeId], resourcesById, recipesById))
                .ToArray();
            var nodeMappings = new List<UiComposerNodeMapping>();
            string panelTemplate = BuildPanelXml(state.Blueprint, resourcesById, recipesById, nodeMappings);
            var runtimeAddressedRecipeIds = new HashSet<string>(
                (state.BindingContract.ComponentBindings ?? Array.Empty<UiComposerComponentBindingContract>())
                    .Select(item => item.ComponentRecipeId),
                StringComparer.Ordinal);
            string packageTemplate = BuildPackageXml(
                state.Blueprint.Target.ComponentName,
                resources,
                components,
                resourcesById,
                runtimeAddressedRecipeIds);
            return new UiComposerMaterializationPayload
            {
                ScreenId = state.Blueprint.ScreenId,
                LibraryId = state.Blueprint.LibraryId,
                PackageId = state.Blueprint.Target.PackageId,
                PackageSourcePath = state.Blueprint.Target.PackageSourcePath,
                ComponentName = state.Blueprint.Target.ComponentName,
                PackageXmlTemplate = packageTemplate,
                PanelComponentXmlTemplate = panelTemplate,
                BlueprintHash = state.BlueprintHash,
                CatalogHash = state.CatalogHash,
                RecipeCatalogHash = state.RecipeCatalogHash,
                ContractHash = state.ContractHash,
                NodeMappings = nodeMappings.ToArray(),
                Resources = resources,
                Components = components,
                SourcePngPath = resources.Length == 1 ? resources[0].SourcePngPath : string.Empty,
                TargetPngRelativePath = resources.Length == 1 ? resources[0].TargetPngRelativePath : string.Empty,
                ButtonComponentXmlTemplate = components.Length == 1 ? components[0].XmlTemplate : string.Empty
            };
        }

        private static UiComposerMaterializedResource BuildMaterializedResource(string libraryId, UiArtResource resource)
        {
            return new UiComposerMaterializedResource
            {
                ResourceId = resource.ResourceId,
                SourcePngPath = "ArtSource/UIKits/" + libraryId + "/" + NormalizePath(resource.RelativePath),
                TargetPngRelativePath = TargetPngRelativePath(libraryId, resource.RelativePath),
                Token = ResourceToken(resource.ResourceId)
            };
        }

        private static void CollectRecipeDependencies(
            string recipeId,
            IReadOnlyDictionary<string, UiComponentRecipe> recipesById,
            HashSet<string> usedRecipeIds,
            HashSet<string> usedResourceIds)
        {
            if (string.IsNullOrWhiteSpace(recipeId) || !usedRecipeIds.Add(recipeId))
                return;

            UiComponentRecipe recipe = recipesById[recipeId];
            UiComponentResourceSlot[] slots = recipe.ResourceSlots ?? Array.Empty<UiComponentResourceSlot>();
            for (int i = 0; i < slots.Length; i++)
                usedResourceIds.Add(slots[i].ResourceId);

            UiScreenNode[] templateNodes = recipe.Template?.Nodes ?? Array.Empty<UiScreenNode>();
            for (int i = 0; i < templateNodes.Length; i++)
            {
                UiScreenNode node = templateNodes[i];
                if (node.Image != null && !string.IsNullOrWhiteSpace(node.Image.ResourceId))
                    usedResourceIds.Add(node.Image.ResourceId);
                if (node.List != null)
                    CollectRecipeDependencies(node.List.ItemComponentRecipeId, recipesById, usedRecipeIds, usedResourceIds);
                if (node.Component == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(node.Component.IconResourceId))
                    usedResourceIds.Add(node.Component.IconResourceId);
                CollectRecipeDependencies(node.Component.ComponentRecipeId, recipesById, usedRecipeIds, usedResourceIds);
            }
        }

        private static UiComposerMaterializedComponent BuildMaterializedComponent(
            UiComponentRecipe recipe,
            IReadOnlyDictionary<string, UiArtResource> resourcesById,
            IReadOnlyDictionary<string, UiComponentRecipe> recipesById)
        {
            string componentName = ComponentName(recipe);
            return new UiComposerMaterializedComponent
            {
                ComponentRecipeId = recipe.ComponentId,
                ComponentName = componentName,
                ComponentRelativePath = "Components/" + componentName + ".xml",
                Token = ComponentToken(recipe.ComponentId),
                XmlTemplate = string.Equals(recipe.ComponentType, "Composite", StringComparison.Ordinal)
                    ? BuildCompositeXml(recipe, resourcesById, recipesById)
                    : BuildButtonXml(recipe, resourcesById)
            };
        }

        private static string BuildPackageXml(
            string panelName,
            UiComposerMaterializedResource[] resources,
            UiComposerMaterializedComponent[] components,
            IReadOnlyDictionary<string, UiArtResource> resourcesById,
            HashSet<string> runtimeAddressedRecipeIds)
        {
            var builder = new StringBuilder(4096);
            builder.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<packageDescription id=\"{{PACKAGE_DESCRIPTION_ID}}\">\n  <resources>\n");
            builder.Append("    <component id=\"{{PANEL_COMPONENT_ID}}\" name=\"").Append(EscapeXml(panelName)).Append(".xml\" path=\"/\" exported=\"true\"/>\n");
            for (int i = 0; i < components.Length; i++)
            {
                UiComposerMaterializedComponent component = components[i];
                builder.Append("    <component id=\"").Append(component.Token).Append("\" name=\"").Append(EscapeXml(component.ComponentName)).Append(".xml\" path=\"/Components/\"");
                if (runtimeAddressedRecipeIds.Contains(component.ComponentRecipeId))
                    builder.Append(" exported=\"true\"");
                builder.Append("/>\n");
            }
            for (int i = 0; i < resources.Length; i++)
            {
                UiComposerMaterializedResource materialized = resources[i];
                UiArtResource resource = resourcesById[materialized.ResourceId];
                string path = NormalizePath(materialized.TargetPngRelativePath);
                string fileName = Path.GetFileName(path);
                string directory = NormalizePath(Path.GetDirectoryName(path) ?? string.Empty);
                builder.Append("    <image id=\"").Append(materialized.Token).Append("\" name=\"").Append(EscapeXml(fileName)).Append("\" path=\"/").Append(EscapeXml(directory)).Append("/\" exported=\"true\"");
                UiArtRecommendedFairyGuiUsage usage = resource.RecommendedFairyGuiUsage;
                if (string.Equals(usage?.Policy, "NineSlice", StringComparison.Ordinal) && usage.Scale9Grid != null)
                {
                    builder.Append(" scale=\"9grid\" scale9grid=\"")
                        .Append(usage.Scale9Grid.X).Append(',').Append(usage.Scale9Grid.Y).Append(',')
                        .Append(usage.Scale9Grid.Width).Append(',').Append(usage.Scale9Grid.Height).Append('"');
                }
                builder.Append("/>\n");
            }
            builder.Append("  </resources>\n  <publish name=\"\"/>\n</packageDescription>\n");
            return builder.ToString();
        }

        private static string BuildButtonXml(UiComponentRecipe recipe, IReadOnlyDictionary<string, UiArtResource> resourcesById)
        {
            if (!string.Equals(recipe.ComponentType, "Button", StringComparison.Ordinal))
                throw new InvalidOperationException("UI Composer only materializes Button recipes in v1: " + recipe.ComponentId + ".");
            UiComponentResourceSlot baseSlot = (recipe.ResourceSlots ?? Array.Empty<UiComponentResourceSlot>()).First(item => item.SlotId == "base");
            UiArtResource baseResource = resourcesById[baseSlot.ResourceId];
            int width = recipe.DefaultSize?.Width > 0 ? recipe.DefaultSize.Width : baseResource.RecommendedFairyGuiUsage.DesignSize.Width;
            int height = recipe.DefaultSize?.Height > 0 ? recipe.DefaultSize.Height : baseResource.RecommendedFairyGuiUsage.DesignSize.Height;
            bool supportsIcon = (recipe.InstanceProperties ?? Array.Empty<string>()).Contains("iconResourceId", StringComparer.Ordinal);
            UiButtonStatePresentation state = recipe.StatePresentation ?? new UiButtonStatePresentation();
            int automaticIconSize = Math.Min(32, Math.Max(1, height - 20));
            int iconSize = state.IconSize > 0
                ? Math.Min(state.IconSize, Math.Min(width, height))
                : automaticIconSize;
            int iconX = state.IconOnly ? (width - iconSize) / 2 : 24;
            int iconY = (height - iconSize) / 2;
            // Component instances are frequently narrower than the recipe default.
            // Keep the title centered with symmetric margins so resizing never
            // collapses the label to a single glyph. An optional icon overlays the
            // leading margin and does not reserve title width.
            int titleX = 12;
            int titleWidth = Math.Max(40, width - 24);
            UiComponentResourceSlot overSlot = (recipe.ResourceSlots ?? Array.Empty<UiComponentResourceSlot>()).FirstOrDefault(item => item.SlotId == "over");
            UiComponentResourceSlot downSlot = (recipe.ResourceSlots ?? Array.Empty<UiComponentResourceSlot>()).FirstOrDefault(item => item.SlotId == "down");
            bool hasStateImages = overSlot != null && downSlot != null;
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
                + "<component size=\"" + width + "," + height + "\" extention=\"Button\" initName=\"btn\">\n"
                + "  <controller name=\"button\" pages=\"0,up,1,down,2,over,3,selectedOver\" selected=\"0\"/>\n"
                + "  <displayList>\n"
                + ButtonStateImageXml("n0_mxucb", "base", baseSlot, resourcesById, width, height, hasStateImages ? "0" : string.Empty)
                + (hasStateImages ? ButtonStateImageXml("n5_mxucb", "overBase", overSlot, resourcesById, width, height, "2,3")
                    + ButtonStateImageXml("n6_mxucb", "downBase", downSlot, resourcesById, width, height, "1") : string.Empty)
                + OverlayXml("n1_mxucb", "overOverlay", width, height, state.OverOverlayColor, "2")
                + OverlayXml("n2_mxucb", "downOverlay", width, height, state.DownOverlayColor, "1,3")
                + (supportsIcon
                    ? "    <loader id=\"n3_mxucb\" name=\"icon\" xy=\"" + iconX + "," + iconY + "\" size=\"" + iconSize + "," + iconSize + "\" align=\"center\" vAlign=\"middle\" fill=\"scale\" color=\"" + EscapeXml(state.IconColor) + "\">\n"
                        + (state.IconOnly ? "      <relation target=\"\" sidePair=\"center-center,middle-middle\"/>\n" : string.Empty)
                        + "    </loader>\n"
                    : string.Empty)
                + "    <text id=\"n4_mxucb\" name=\"title\"" + (string.IsNullOrEmpty(state.TitleFont) ? string.Empty : " font=\"" + EscapeXml(state.TitleFont) + "\"") + " xy=\"" + titleX + ",0\" size=\"" + titleWidth + "," + height + "\" fontSize=\"" + Math.Max(10, state.TitleFontSize) + "\" color=\"" + EscapeXml(state.TitleColor) + "\" align=\"center\" vAlign=\"middle\" autoSize=\"none\" singleLine=\"true\"" + (state.TitleBold ? " bold=\"true\"" : string.Empty) + " autoClearText=\"true\" text=\"Button\">\n"
                + "      <relation target=\"\" sidePair=\"width-width,height-height\"/>\n"
                + "    </text>\n"
                + "  </displayList>\n  <Button/>\n</component>\n";
        }

        private static string ButtonStateImageXml(string id, string name, UiComponentResourceSlot slot,
            IReadOnlyDictionary<string, UiArtResource> resourcesById, int width, int height, string pages)
        {
            return "    <image id=\"" + id + "\" name=\"" + name + "\" src=\"" + ResourceToken(slot.ResourceId)
                + "\" fileName=\"" + EscapeXml(TargetPngRelativePath("MxUiFoundation", resourcesById[slot.ResourceId].RelativePath))
                + "\" xy=\"0,0\" size=\"" + width + "," + height + "\">\n"
                + (pages.Length == 0 ? string.Empty : "      <gearDisplay controller=\"button\" pages=\"" + pages + "\"/>\n")
                + "      <relation target=\"\" sidePair=\"width-width,height-height\"/>\n    </image>\n";
        }

        private static string BuildCompositeXml(
            UiComponentRecipe recipe,
            IReadOnlyDictionary<string, UiArtResource> resourcesById,
            IReadOnlyDictionary<string, UiComponentRecipe> recipesById)
        {
            UiScreenNode[] nodes = recipe.Template?.Nodes ?? Array.Empty<UiScreenNode>();
            var builder = new StringBuilder(8192);
            builder.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<component size=\"")
                .Append(recipe.DefaultSize.Width).Append(',').Append(recipe.DefaultSize.Height)
                .Append("\" opaque=\"false\">\n  <displayList>\n");
            UiScreenNode[] ordered = nodes.OrderBy(node => node.Order).ThenBy(node => node.NodeId, StringComparer.Ordinal).ToArray();
            for (int i = 0; i < ordered.Length; i++)
                AppendNodeXml(builder, ordered[i], "c" + i.ToString(CultureInfo.InvariantCulture) + "_mxucc", resourcesById, recipesById);
            builder.Append("  </displayList>\n</component>\n");
            return builder.ToString();
        }

        private static string OverlayXml(string id, string name, int width, int height, string color, string pages)
        {
            return "    <graph id=\"" + id + "\" name=\"" + name + "\" xy=\"0,0\" size=\"" + width + "," + height + "\" type=\"rect\" fillColor=\"" + EscapeXml(color) + "\">\n"
                + "      <gearDisplay controller=\"button\" pages=\"" + pages + "\"/>\n"
                + "      <relation target=\"\" sidePair=\"width-width,height-height\"/>\n"
                + "    </graph>\n";
        }

        private static string BuildPanelXml(UiScreenBlueprint blueprint, IReadOnlyDictionary<string, UiArtResource> resourcesById, IReadOnlyDictionary<string, UiComponentRecipe> recipesById, List<UiComposerNodeMapping> mappings)
        {
            var builder = new StringBuilder(8192);
            builder.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<component size=\"").Append(blueprint.DesignResolution.Width).Append(',').Append(blueprint.DesignResolution.Height).Append("\" opaque=\"false\">\n  <displayList>\n");
            UiScreenNode[] nodes = BuildMaterializedNodes(blueprint);
            for (int i = 0; i < nodes.Length; i++)
            {
                UiScreenNode node = nodes[i];
                string fguiNodeId = "n" + i.ToString(CultureInfo.InvariantCulture) + "_mxuc";
                mappings.Add(new UiComposerNodeMapping(node.NodeId, fguiNodeId, node.Name));
                AppendNodeXml(builder, node, fguiNodeId, resourcesById, recipesById);
            }
            builder.Append("  </displayList>\n</component>\n");
            return builder.ToString();
        }

        private static void AppendNodeXml(StringBuilder builder, UiScreenNode node, string fguiNodeId, IReadOnlyDictionary<string, UiArtResource> resourcesById, IReadOnlyDictionary<string, UiComponentRecipe> recipesById)
        {
            UiNodeLayout layout = node.Layout ?? new UiNodeLayout();
            string common = " id=\"" + EscapeXml(fguiNodeId) + "\" name=\"" + EscapeXml(node.Name) + "\" xy=\"" + layout.X + "," + layout.Y + "\" size=\"" + layout.Width + "," + layout.Height + "\""
                + (node.Visible == false ? " visible=\"false\"" : string.Empty);
            if (node.Type == "Graph")
            {
                UiGraphNodeStyle style = node.Graph ?? new UiGraphNodeStyle();
                builder.Append("    <graph").Append(common).Append(" type=\"rect\" fillColor=\"").Append(EscapeXml(style.FillColor)).Append('"');
                if (style.CornerRadius > 0) builder.Append(" corner=\"").Append(style.CornerRadius).Append(',').Append(style.CornerRadius).Append(',').Append(style.CornerRadius).Append(',').Append(style.CornerRadius).Append('"');
                AppendNodeCloseAndRelation(builder, layout, "graph");
                return;
            }
            if (node.Type == "Text")
            {
                UiTextNodeStyle style = node.Text ?? new UiTextNodeStyle();
                builder.Append("    <text").Append(common).Append(" fontSize=\"").Append(style.FontSize).Append("\" color=\"").Append(EscapeXml(style.Color)).Append("\" align=\"").Append(EscapeXml(style.Align)).Append("\" vAlign=\"middle\" autoSize=\"none\"");
                if (!string.IsNullOrEmpty(style.Font)) builder.Append(" font=\"").Append(EscapeXml(style.Font)).Append("\"");
                if (style.SingleLine) builder.Append(" singleLine=\"true\"");
                if (style.Bold) builder.Append(" bold=\"true\"");
                if (style.Input) builder.Append(" input=\"true\"");
                if (!string.IsNullOrWhiteSpace(style.Restrict)) builder.Append(" restrict=\"").Append(EscapeXml(style.Restrict)).Append('"');
                if (style.MaxLength > 0) builder.Append(" maxLength=\"").Append(style.MaxLength).Append('"');
                builder.Append(" text=\"").Append(EscapeXml(style.Value)).Append('"');
                AppendNodeCloseAndRelation(builder, layout, "text");
                return;
            }
            if (node.Type == "Image")
            {
                UiImageNodeStyle style = node.Image ?? new UiImageNodeStyle();
                UiArtResource resource = resourcesById[style.ResourceId];
                builder.Append("    <image").Append(common).Append(" src=\"").Append(ResourceToken(style.ResourceId)).Append("\" fileName=\"").Append(EscapeXml(TargetPngRelativePath("MxUiFoundation", resource.RelativePath))).Append('"');
                if (resource.RecommendedFairyGuiUsage?.PreserveAspect == true || resource.RecommendedFairyGuiUsage?.Policy == "PreserveAspect") builder.Append(" aspect=\"true\"");
                if (!string.IsNullOrWhiteSpace(style.Tint) && style.Tint != "#ffffffff") builder.Append(" color=\"").Append(EscapeXml(style.Tint)).Append('"');
                AppendNodeCloseAndRelation(builder, layout, "image");
                return;
            }
            if (node.Type == "List")
            {
                UiListNodeStyle style = node.List ?? new UiListNodeStyle();
                UiComponentRecipe itemRecipe = recipesById[style.ItemComponentRecipeId];
                builder.Append("    <list").Append(common);
                if (style.Layout == "Row") builder.Append(" layout=\"row\"");
                if (style.SelectionMode != "Single") builder.Append(" selectionMode=\"").Append(EscapeXml(ListSelectionModeToXml(style.SelectionMode))).Append('"');
                builder.Append(" overflow=\"scroll\"");
                if (style.Layout == "Row") builder.Append(" scroll=\"horizontal\"");
                if (style.LineGap != 0) builder.Append(" lineGap=\"").Append(style.LineGap).Append('"');
                if (style.ColumnGap != 0) builder.Append(" colGap=\"").Append(style.ColumnGap).Append('"');
                builder.Append(" defaultItem=\"ui://{{PACKAGE_DESCRIPTION_ID}}").Append(ComponentToken(itemRecipe.ComponentId)).Append('"');
                AppendNodeCloseAndRelation(builder, layout, "list");
                return;
            }
            UiComponentInstanceNode component = node.Component ?? new UiComponentInstanceNode();
            UiComponentRecipe recipe = recipesById[component.ComponentRecipeId];
            builder.Append("    <component").Append(common).Append(" src=\"").Append(ComponentToken(recipe.ComponentId)).Append("\" fileName=\"Components/").Append(EscapeXml(ComponentName(recipe))).Append(".xml\">");
            if (string.Equals(recipe.ComponentType, "Button", StringComparison.Ordinal))
            {
                builder.Append("\n      <Button title=\"").Append(EscapeXml(component.Title)).Append('"');
                if (!string.IsNullOrWhiteSpace(component.IconResourceId))
                    builder.Append(" icon=\"ui://{{PACKAGE_DESCRIPTION_ID}}").Append(ResourceToken(component.IconResourceId)).Append('"');
                builder.Append("/>");
            }
            builder.Append('\n');
            AppendRelation(builder, layout, "      ");
            builder.Append("    </component>\n");
        }

        private static UiScreenNode[] BuildMaterializedNodes(UiScreenBlueprint blueprint)
        {
            UiScreenNode[] source = blueprint.Nodes ?? Array.Empty<UiScreenNode>();
            UiLayoutContainer[] containers = blueprint.Containers ?? Array.Empty<UiLayoutContainer>();
            if (containers.Length == 0)
                return source.OrderBy(node => node.Order).ThenBy(node => node.NodeId, StringComparer.Ordinal).ToArray();

            var containersById = containers.ToDictionary(item => item.ContainerId, StringComparer.Ordinal);
            var output = new List<UiScreenNode>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(source[i].ParentContainerId))
                    output.Add(CloneNodeWithLayout(source[i], CloneLayout(source[i].Layout)));
            }

            UiLayoutContainer[] roots = containers
                .Where(item => string.IsNullOrWhiteSpace(item.ParentContainerId))
                .OrderBy(item => item.Order)
                .ThenBy(item => item.ContainerId, StringComparer.Ordinal)
                .ToArray();
            for (int i = 0; i < roots.Length; i++)
                MaterializeContainer(roots[i], 0, 0, roots[i].Layout.HorizontalAnchor, roots[i].Layout.VerticalAnchor, source, containers, containersById, output, null, null);
            return output.OrderBy(node => node.Order).ThenBy(node => node.NodeId, StringComparer.Ordinal).ToArray();
        }

        private static void MaterializeContainer(
            UiLayoutContainer container,
            int parentX,
            int parentY,
            string inheritedHorizontalAnchor,
            string inheritedVerticalAnchor,
            UiScreenNode[] nodes,
            UiLayoutContainer[] containers,
            IReadOnlyDictionary<string, UiLayoutContainer> containersById,
            List<UiScreenNode> output,
            int? derivedX,
            int? derivedY)
        {
            UiNodeLayout containerLayout = container.Layout ?? new UiNodeLayout();
            int originX = parentX + (derivedX ?? containerLayout.X);
            int originY = parentY + (derivedY ?? containerLayout.Y);
            string horizontalAnchor = string.IsNullOrWhiteSpace(container.ParentContainerId) ? containerLayout.HorizontalAnchor : inheritedHorizontalAnchor;
            string verticalAnchor = string.IsNullOrWhiteSpace(container.ParentContainerId) ? containerLayout.VerticalAnchor : inheritedVerticalAnchor;
            var children = new List<ContainerChild>();
            for (int i = 0; i < nodes.Length; i++)
            {
                if (string.Equals(nodes[i].ParentContainerId, container.ContainerId, StringComparison.Ordinal))
                    children.Add(ContainerChild.ForNode(nodes[i]));
            }
            for (int i = 0; i < containers.Length; i++)
            {
                if (string.Equals(containers[i].ParentContainerId, container.ContainerId, StringComparison.Ordinal))
                    children.Add(ContainerChild.ForContainer(containers[i]));
            }
            children.Sort((left, right) => left.Order != right.Order ? left.Order.CompareTo(right.Order) : string.CompareOrdinal(left.Id, right.Id));
            PositionContainerChildren(container, children);

            for (int i = 0; i < children.Count; i++)
            {
                ContainerChild child = children[i];
                if (child.Node != null)
                {
                    UiNodeLayout layout = CloneLayout(child.Node.Layout);
                    layout.X = originX + child.X;
                    layout.Y = originY + child.Y;
                    layout.HorizontalAnchor = horizontalAnchor;
                    layout.VerticalAnchor = verticalAnchor;
                    output.Add(CloneNodeWithLayout(child.Node, layout));
                }
                else if (child.Container != null && containersById.ContainsKey(child.Container.ContainerId))
                {
                    MaterializeContainer(child.Container, originX, originY, horizontalAnchor, verticalAnchor, nodes, containers, containersById, output, child.X, child.Y);
                }
            }
        }

        private static void PositionContainerChildren(UiLayoutContainer container, List<ContainerChild> children)
        {
            UiContainerPadding padding = container.Padding ?? new UiContainerPadding();
            UiNodeLayout layout = container.Layout ?? new UiNodeLayout();
            int availableWidth = Math.Max(0, layout.Width - padding.Left - padding.Right);
            int availableHeight = Math.Max(0, layout.Height - padding.Top - padding.Bottom);
            string mode = container.Mode ?? "Free";
            if (mode == "Free")
                return;
            if (mode == "Horizontal")
            {
                int used = children.Sum(item => item.Width) + Math.Max(0, children.Count - 1) * container.GapX;
                int cursor = padding.Left + AlignmentOffset(container.MainAxisAlign, availableWidth, used);
                for (int i = 0; i < children.Count; i++)
                {
                    children[i].X = cursor;
                    children[i].Y = padding.Top + AlignmentOffset(container.CrossAxisAlign, availableHeight, children[i].Height);
                    cursor += children[i].Width + container.GapX;
                }
                return;
            }
            if (mode == "Vertical")
            {
                int used = children.Sum(item => item.Height) + Math.Max(0, children.Count - 1) * container.GapY;
                int cursor = padding.Top + AlignmentOffset(container.MainAxisAlign, availableHeight, used);
                for (int i = 0; i < children.Count; i++)
                {
                    children[i].X = padding.Left + AlignmentOffset(container.CrossAxisAlign, availableWidth, children[i].Width);
                    children[i].Y = cursor;
                    cursor += children[i].Height + container.GapY;
                }
                return;
            }

            int columns = Math.Max(1, container.Columns);
            int cellWidth = children.Count == 0 ? 0 : children.Max(item => item.Width);
            int cellHeight = children.Count == 0 ? 0 : children.Max(item => item.Height);
            int rows = (children.Count + columns - 1) / columns;
            int usedWidth = Math.Min(columns, children.Count) * cellWidth + Math.Max(0, Math.Min(columns, children.Count) - 1) * container.GapX;
            int usedHeight = rows * cellHeight + Math.Max(0, rows - 1) * container.GapY;
            int startX = padding.Left + AlignmentOffset(container.MainAxisAlign, availableWidth, usedWidth);
            int startY = padding.Top + AlignmentOffset(container.CrossAxisAlign, availableHeight, usedHeight);
            for (int i = 0; i < children.Count; i++)
            {
                children[i].X = startX + (i % columns) * (cellWidth + container.GapX);
                children[i].Y = startY + (i / columns) * (cellHeight + container.GapY);
            }
        }

        private static int AlignmentOffset(string alignment, int available, int used)
        {
            if (string.Equals(alignment, "Center", StringComparison.Ordinal)) return (available - used) / 2;
            if (string.Equals(alignment, "End", StringComparison.Ordinal)) return available - used;
            return 0;
        }

        private static UiScreenNode CloneNodeWithLayout(UiScreenNode source, UiNodeLayout layout)
        {
            return new UiScreenNode
            {
                NodeId = source.NodeId,
                Name = source.Name,
                Type = source.Type,
                Order = source.Order,
                BindingSlot = source.BindingSlot,
                ParentContainerId = source.ParentContainerId,
                Visible = source.Visible,
                Layout = layout,
                Graph = source.Graph,
                Text = source.Text,
                Image = source.Image,
                Component = source.Component,
                List = source.List
            };
        }

        private static UiNodeLayout CloneLayout(UiNodeLayout source)
        {
            source = source ?? new UiNodeLayout();
            return new UiNodeLayout { X = source.X, Y = source.Y, Width = source.Width, Height = source.Height, HorizontalAnchor = source.HorizontalAnchor, VerticalAnchor = source.VerticalAnchor };
        }

        private static void RestoreContainerLayouts(UiScreenBlueprint blueprint, List<UiScreenNode> parsedNodes, IReadOnlyDictionary<string, UiScreenNode> materializedByName, string path, List<UiComposerDiagnostic> diagnostics)
        {
            UiLayoutContainer[] containers = blueprint.Containers ?? Array.Empty<UiLayoutContainer>();
            if (containers.Length == 0) return;
            var containersById = containers.ToDictionary(item => item.ContainerId, StringComparer.Ordinal);
            var previousByName = (blueprint.Nodes ?? Array.Empty<UiScreenNode>()).ToDictionary(item => item.Name, StringComparer.Ordinal);
            for (int i = 0; i < parsedNodes.Count; i++)
            {
                UiScreenNode parsed = parsedNodes[i];
                if (!previousByName.TryGetValue(parsed.Name, out UiScreenNode previous) || string.IsNullOrWhiteSpace(previous.ParentContainerId)) continue;
                parsed.ParentContainerId = previous.ParentContainerId;
                if (!containersById.TryGetValue(previous.ParentContainerId, out UiLayoutContainer container)) continue;
                UiScreenNode expected = materializedByName[parsed.Name];
                if (!string.Equals(container.Mode, "Free", StringComparison.Ordinal))
                {
                    if (!LayoutsEqual(parsed.Layout, expected.Layout)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP012", "FairyGUI changed a node controlled by an automatic layout container. Edit the container in UI Composer or ungroup it before syncing.", path + "#" + parsed.Name));
                    parsed.Layout = CloneLayout(previous.Layout);
                    continue;
                }
                parsed.Layout.X -= expected.Layout.X - previous.Layout.X;
                parsed.Layout.Y -= expected.Layout.Y - previous.Layout.Y;
                parsed.Layout.HorizontalAnchor = previous.Layout.HorizontalAnchor;
                parsed.Layout.VerticalAnchor = previous.Layout.VerticalAnchor;
            }
        }

        private static bool LayoutsEqual(UiNodeLayout left, UiNodeLayout right)
        {
            return left != null && right != null && left.X == right.X && left.Y == right.Y && left.Width == right.Width && left.Height == right.Height;
        }

        private sealed class ContainerChild
        {
            public string Id { get; private set; }
            public int Order { get; private set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; private set; }
            public int Height { get; private set; }
            public UiScreenNode Node { get; private set; }
            public UiLayoutContainer Container { get; private set; }

            public static ContainerChild ForNode(UiScreenNode node) => new ContainerChild { Id = node.NodeId, Order = node.Order, X = node.Layout.X, Y = node.Layout.Y, Width = node.Layout.Width, Height = node.Layout.Height, Node = node };
            public static ContainerChild ForContainer(UiLayoutContainer container) => new ContainerChild { Id = container.ContainerId, Order = container.Order, X = container.Layout.X, Y = container.Layout.Y, Width = container.Layout.Width, Height = container.Layout.Height, Container = container };
        }

        private static void ValidateContainers(UiScreenBlueprint blueprint, string blueprintPath, bool isV1, List<UiComposerDiagnostic> diagnostics)
        {
            UiLayoutContainer[] containers = blueprint.Containers ?? Array.Empty<UiLayoutContainer>();
            if (isV1 && containers.Length > 0)
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP001", "V1 blueprints cannot declare layout containers.", blueprintPath));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < containers.Length; i++)
            {
                UiLayoutContainer container = containers[i];
                string path = blueprintPath + "#" + (container.ContainerId ?? string.Empty);
                if (string.IsNullOrWhiteSpace(container.ContainerId) || !ids.Add(container.ContainerId)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP013", "Container id is empty or duplicated.", path));
                if (string.IsNullOrWhiteSpace(container.Name) || !names.Add(container.Name)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP013", "Container name is empty or duplicated.", path));
                if (container.Layout == null || container.Layout.Width <= 0 || container.Layout.Height <= 0) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP014", "Container size must be positive.", path));
                else if (!IsAnchor(container.Layout.HorizontalAnchor, true) || !IsAnchor(container.Layout.VerticalAnchor, false) || container.Layout.HorizontalAnchor == "Stretch" || container.Layout.VerticalAnchor == "Stretch") diagnostics.Add(UiComposerDiagnostic.Error("UICOMP014", "Containers support fixed Left/Center/Right and Top/Middle/Bottom anchors only.", path));
                if (container.Mode != "Free" && container.Mode != "Horizontal" && container.Mode != "Vertical" && container.Mode != "Grid") diagnostics.Add(UiComposerDiagnostic.Error("UICOMP014", "Unsupported container mode: " + container.Mode + ".", path));
                if (container.GapX < 0 || container.GapY < 0) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP014", "Container gaps cannot be negative.", path));
                if (container.Mode == "Grid" && container.Columns <= 0) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP014", "Grid containers require at least one column.", path));
                UiContainerPadding padding = container.Padding ?? new UiContainerPadding();
                if (padding.Left < 0 || padding.Top < 0 || padding.Right < 0 || padding.Bottom < 0) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP014", "Container padding cannot be negative.", path));
                if (!IsContainerAlignment(container.MainAxisAlign) || !IsContainerAlignment(container.CrossAxisAlign)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP014", "Container alignment must be Start, Center, or End.", path));
            }
            for (int i = 0; i < containers.Length; i++)
            {
                UiLayoutContainer container = containers[i];
                if (!string.IsNullOrWhiteSpace(container.ParentContainerId) && !ids.Contains(container.ParentContainerId)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP013", "Container references an unknown parent.", blueprintPath + "#" + container.ContainerId));
            }
            var byId = containers.Where(item => !string.IsNullOrWhiteSpace(item.ContainerId)).GroupBy(item => item.ContainerId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            for (int i = 0; i < containers.Length; i++)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal);
                UiLayoutContainer current = containers[i];
                while (current != null && !string.IsNullOrWhiteSpace(current.ParentContainerId))
                {
                    if (!visited.Add(current.ContainerId))
                    {
                        diagnostics.Add(UiComposerDiagnostic.Error("UICOMP013", "Layout container hierarchy contains a cycle.", blueprintPath + "#" + containers[i].ContainerId));
                        break;
                    }
                    byId.TryGetValue(current.ParentContainerId, out current);
                }
            }
            ValidateContainerOverflow(blueprint, diagnostics, blueprintPath);
        }

        private static void ValidateContainerOverflow(UiScreenBlueprint blueprint, List<UiComposerDiagnostic> diagnostics, string blueprintPath)
        {
            UiLayoutContainer[] containers = blueprint.Containers ?? Array.Empty<UiLayoutContainer>();
            UiScreenNode[] nodes = blueprint.Nodes ?? Array.Empty<UiScreenNode>();
            for (int i = 0; i < containers.Length; i++)
            {
                UiLayoutContainer container = containers[i];
                if (container.Mode == "Free" || container.Layout == null) continue;
                var children = new List<ContainerChild>();
                for (int j = 0; j < nodes.Length; j++) if (nodes[j].ParentContainerId == container.ContainerId) children.Add(ContainerChild.ForNode(nodes[j]));
                for (int j = 0; j < containers.Length; j++) if (containers[j].ParentContainerId == container.ContainerId) children.Add(ContainerChild.ForContainer(containers[j]));
                children.Sort((left, right) => left.Order != right.Order ? left.Order.CompareTo(right.Order) : string.CompareOrdinal(left.Id, right.Id));
                PositionContainerChildren(container, children);
                UiContainerPadding padding = container.Padding ?? new UiContainerPadding();
                bool overflow = children.Any(child => child.X < padding.Left || child.Y < padding.Top || child.X + child.Width > container.Layout.Width - padding.Right || child.Y + child.Height > container.Layout.Height - padding.Bottom);
                if (overflow) diagnostics.Add(UiComposerDiagnostic.Warning("UICOMP109", "Automatic layout children overflow the container bounds.", blueprintPath + "#" + container.ContainerId));
            }
        }

        private static bool IsContainerAlignment(string value) => value == "Start" || value == "Center" || value == "End";

        private static void ValidateComponentRecipes(
            UiComponentRecipe[] recipes,
            HashSet<string> resourceIds,
            HashSet<string> recipeIds,
            string recipePath,
            List<UiComposerDiagnostic> diagnostics)
        {
            var recipesById = recipes
                .Where(recipe => !string.IsNullOrWhiteSpace(recipe.ComponentId))
                .GroupBy(recipe => recipe.ComponentId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            for (int i = 0; i < recipes.Length; i++)
            {
                UiComponentRecipe recipe = recipes[i];
                string path = recipePath + "#" + recipe.ComponentId;
                if (recipe.DefaultSize == null || recipe.DefaultSize.Width <= 0 || recipe.DefaultSize.Height <= 0)
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Component recipe size must be positive.", path));

                if (string.Equals(recipe.ComponentType, "Button", StringComparison.Ordinal))
                {
                    bool hasBase = (recipe.ResourceSlots ?? Array.Empty<UiComponentResourceSlot>())
                        .Any(slot => slot.SlotId == "base" && resourceIds.Contains(slot.ResourceId));
                    if (!hasBase)
                        diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Button recipe requires a valid base resource slot.", path));
                    continue;
                }

                if (!string.Equals(recipe.ComponentType, "Composite", StringComparison.Ordinal))
                {
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Unsupported component recipe type: " + recipe.ComponentType + ".", path));
                    continue;
                }

                UiScreenNode[] templateNodes = recipe.Template?.Nodes ?? Array.Empty<UiScreenNode>();
                if (templateNodes.Length == 0)
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Composite recipe requires template nodes.", path));
                var nodeIds = new HashSet<string>(StringComparer.Ordinal);
                var nodeNames = new HashSet<string>(StringComparer.Ordinal);
                for (int j = 0; j < templateNodes.Length; j++)
                    ValidateCompositeNode(templateNodes[j], path, resourceIds, recipeIds, nodeIds, nodeNames, diagnostics);
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string recipeId in recipesById.Keys)
                ValidateCompositeCycle(recipeId, recipesById, new HashSet<string>(StringComparer.Ordinal), visited, recipePath, diagnostics);
        }

        private static void ValidateComponentBindingContracts(
            UiComposerComponentBindingContract[] contracts,
            UiComponentRecipe[] recipes,
            string contractPath,
            List<UiComposerDiagnostic> diagnostics)
        {
            UiComposerComponentBindingContract[] safeContracts = contracts
                ?? Array.Empty<UiComposerComponentBindingContract>();
            var claimedRecipes = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < safeContracts.Length; i++)
            {
                UiComposerComponentBindingContract componentContract = safeContracts[i];
                string recipeId = componentContract?.ComponentRecipeId ?? string.Empty;
                string path = contractPath + "#" + recipeId;
                if (string.IsNullOrWhiteSpace(recipeId) || !claimedRecipes.Add(recipeId))
                {
                    diagnostics.Add(UiComposerDiagnostic.Error(
                        "UICOMP004",
                        "Composite binding contract recipe id is empty or duplicated.",
                        path));
                    continue;
                }
                UiComponentRecipe recipe = null;
                for (int recipeIndex = 0; recipeIndex < recipes.Length; recipeIndex++)
                {
                    if (string.Equals(recipes[recipeIndex].ComponentId, recipeId, StringComparison.Ordinal))
                    {
                        recipe = recipes[recipeIndex];
                        break;
                    }
                }
                if (recipe == null || !string.Equals(recipe.ComponentType, "Composite", StringComparison.Ordinal))
                {
                    diagnostics.Add(UiComposerDiagnostic.Error(
                        "UICOMP004",
                        "Composite binding contract references an unknown Composite recipe.",
                        path));
                    continue;
                }

                UiScreenNode[] nodes = recipe.Template?.Nodes ?? Array.Empty<UiScreenNode>();
                UiComposerBindingSlot[] bindings = componentContract.Bindings
                    ?? Array.Empty<UiComposerBindingSlot>();
                var slotIds = new HashSet<string>(StringComparer.Ordinal);
                var controlNames = new HashSet<string>(StringComparer.Ordinal);
                for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
                {
                    UiComposerBindingSlot binding = bindings[bindingIndex];
                    string slotPath = path + "/" + (binding?.SlotId ?? string.Empty);
                    if (binding == null
                        || string.IsNullOrWhiteSpace(binding.SlotId)
                        || !slotIds.Add(binding.SlotId)
                        || string.IsNullOrWhiteSpace(binding.ControlName)
                        || !controlNames.Add(binding.ControlName))
                    {
                        diagnostics.Add(UiComposerDiagnostic.Error(
                            "UICOMP004",
                            "Composite binding slot id and control name must be non-empty and unique.",
                            slotPath));
                        continue;
                    }
                    UiScreenNode node = null;
                    for (int nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
                    {
                        if (string.Equals(nodes[nodeIndex].Name, binding.ControlName, StringComparison.Ordinal))
                        {
                            node = nodes[nodeIndex];
                            break;
                        }
                    }
                    if (node == null)
                    {
                        if (binding.Required)
                            diagnostics.Add(UiComposerDiagnostic.Error(
                                "UICOMP004",
                                "Required Composite binding control is missing: " + binding.ControlName + ".",
                                slotPath));
                        continue;
                    }
                    if (!string.Equals(node.Type, binding.NodeType, StringComparison.Ordinal))
                        diagnostics.Add(UiComposerDiagnostic.Error(
                            "UICOMP004",
                            "Composite binding control type does not match the contract.",
                            slotPath));
                }
            }
        }

        private static void ValidateCompositeNode(
            UiScreenNode node,
            string recipePath,
            HashSet<string> resourceIds,
            HashSet<string> recipeIds,
            HashSet<string> nodeIds,
            HashSet<string> nodeNames,
            List<UiComposerDiagnostic> diagnostics)
        {
            string path = recipePath + "/" + node.NodeId;
            if (string.IsNullOrWhiteSpace(node.NodeId) || !nodeIds.Add(node.NodeId) || string.IsNullOrWhiteSpace(node.Name) || !nodeNames.Add(node.Name))
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Composite node id and name must be non-empty and unique.", path));
            if (node.Type != "Graph" && node.Type != "Text" && node.Type != "Image" && node.Type != "ComponentInstance" && node.Type != "List")
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Composite node type is unsupported: " + node.Type + ".", path));
            if (node.Layout == null || node.Layout.Width <= 0 || node.Layout.Height <= 0)
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Composite node size must be positive.", path));
            if (!string.IsNullOrWhiteSpace(node.ParentContainerId) || !string.IsNullOrWhiteSpace(node.BindingSlot))
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Composite template nodes cannot use containers or screen binding slots.", path));
            if (node.Type == "Graph" && node.Graph == null)
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Composite Graph node requires graph style.", path));
            if (node.Type == "Text" && node.Text == null)
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Composite Text node requires text style.", path));
            if (node.Type == "Image" && (node.Image == null || !resourceIds.Contains(node.Image.ResourceId)))
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP005", "Composite Image node references an unknown semantic resource.", path));
            if (node.Type == "ComponentInstance" && (node.Component == null || !recipeIds.Contains(node.Component.ComponentRecipeId)))
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Composite instance references an unknown recipe.", path));
            if (node.Type == "List" && (node.List == null || !recipeIds.Contains(node.List.ItemComponentRecipeId)))
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Composite list references an unknown item component recipe.", path));
            if (!string.IsNullOrWhiteSpace(node.Component?.IconResourceId) && !resourceIds.Contains(node.Component.IconResourceId))
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP005", "Composite button icon references an unknown semantic resource.", path));
        }

        private static void ValidateCompositeCycle(
            string recipeId,
            IReadOnlyDictionary<string, UiComponentRecipe> recipesById,
            HashSet<string> visiting,
            HashSet<string> visited,
            string recipePath,
            List<UiComposerDiagnostic> diagnostics)
        {
            if (visited.Contains(recipeId))
                return;
            if (!visiting.Add(recipeId))
            {
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Composite recipe dependency cycle detected at " + recipeId + ".", recipePath + "#" + recipeId));
                return;
            }

            UiScreenNode[] nodes = recipesById[recipeId].Template?.Nodes ?? Array.Empty<UiScreenNode>();
            for (int i = 0; i < nodes.Length; i++)
            {
                string dependency = nodes[i].Component?.ComponentRecipeId;
                if (!string.IsNullOrWhiteSpace(dependency) && recipesById.ContainsKey(dependency))
                    ValidateCompositeCycle(dependency, recipesById, visiting, visited, recipePath, diagnostics);
                string listDependency = nodes[i].List?.ItemComponentRecipeId;
                if (!string.IsNullOrWhiteSpace(listDependency) && recipesById.ContainsKey(listDependency))
                    ValidateCompositeCycle(listDependency, recipesById, visiting, visited, recipePath, diagnostics);
            }
            visiting.Remove(recipeId);
            visited.Add(recipeId);
        }

        private static UiScreenBlueprint ParseSupportedComponentXml(string repositoryRoot, UiComposerAuthoringState state, ScreenContext context, List<UiComposerDiagnostic> diagnostics)
        {
            XDocument document;
            try { document = XDocument.Load(ResolvePath(repositoryRoot, context.ComponentPath), LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo); }
            catch (Exception ex) when (ex is IOException || ex is System.Xml.XmlException)
            {
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP009", "Unable to read FairyGUI component XML: " + ex.Message, context.ComponentPath));
                return null;
            }
            XElement root = document.Root;
            XElement displayList = root?.Element("displayList");
            if (root == null || root.Name.LocalName != "component" || displayList == null || root.Elements().Any(item => item.Name.LocalName != "displayList"))
            {
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP009", "Only a component with one displayList is supported for round-trip.", context.ComponentPath));
                return null;
            }
            var existingByName = (state.Blueprint.Nodes ?? Array.Empty<UiScreenNode>()).ToDictionary(item => item.Name, StringComparer.Ordinal);
            Dictionary<string, UiScreenNode> materializedByName = BuildMaterializedNodes(state.Blueprint).ToDictionary(item => item.Name, StringComparer.Ordinal);
            var resourcesByPath = ResourceMappings(state.PackageState).ToDictionary(item => NormalizePath(item.TargetRelativePath), item => item.ResourceId, StringComparer.Ordinal);
            var resourcesByFguiId = ResourceMappings(state.PackageState).ToDictionary(item => item.FairyGuiResourceId, item => item.ResourceId, StringComparer.Ordinal);
            var recipesByPath = ComponentMappings(state.PackageState).ToDictionary(item => NormalizePath(item.ComponentRelativePath), item => item.ComponentRecipeId, StringComparer.Ordinal);
            UiComposerComponentMapping[] componentMappings = ComponentMappings(state.PackageState).ToArray();
            var nodes = new List<UiScreenNode>();
            int order = 0;
            foreach (XElement element in displayList.Elements())
            {
                string kind = element.Name.LocalName;
                if (kind != "graph" && kind != "text" && kind != "image" && kind != "component" && kind != "list")
                {
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP009", "Unsupported FairyGUI node type: " + kind + ".", context.ComponentPath));
                    continue;
                }
                if (!ValidateSupportedElementShape(element, kind, context.ComponentPath, diagnostics)) continue;
                string name = Attribute(element, "name");
                existingByName.TryGetValue(name, out UiScreenNode previous);
                var node = new UiScreenNode
                {
                    NodeId = previous?.NodeId ?? name,
                    Name = name,
                    Type = kind == "graph" ? "Graph" : kind == "text" ? "Text" : kind == "image" ? "Image" : kind == "list" ? "List" : "ComponentInstance",
                    Order = order++,
                    BindingSlot = FindBindingSlotByControlName(context.Contract.Bindings, name),
                    ParentContainerId = previous?.ParentContainerId ?? string.Empty,
                    Visible = ParseOptionalBool(Attribute(element, "visible")),
                    Layout = ParseLayout(element)
                };
                if (kind == "graph") node.Graph = new UiGraphNodeStyle { FillColor = Attribute(element, "fillColor"), CornerRadius = ParseFirstInt(Attribute(element, "corner")) };
                else if (kind == "text") node.Text = new UiTextNodeStyle { Value = Attribute(element, "text"), Font = Attribute(element, "font"), FontSize = ParseInt(Attribute(element, "fontSize"), 16), Color = Attribute(element, "color"), Align = DefaultIfEmpty(Attribute(element, "align"), "left"), Bold = ParseBool(Attribute(element, "bold")), SingleLine = ParseBool(Attribute(element, "singleLine")), Input = ParseBool(Attribute(element, "input")), Restrict = Attribute(element, "restrict"), MaxLength = ParseInt(Attribute(element, "maxLength"), 0) };
                else if (kind == "image")
                {
                    string fileName = NormalizePath(Attribute(element, "fileName"));
                    if (!resourcesByPath.TryGetValue(fileName, out string resourceId)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP009", "Image is not mapped to a semantic resource: " + fileName + ".", context.ComponentPath));
                    node.Image = new UiImageNodeStyle { ResourceId = resourceId ?? string.Empty, Tint = DefaultIfEmpty(Attribute(element, "color"), "#ffffffff") };
                }
                else if (kind == "list")
                {
                    string defaultItem = Attribute(element, "defaultItem");
                    UiComposerComponentMapping itemMapping = componentMappings.FirstOrDefault(item =>
                        !string.IsNullOrWhiteSpace(item.FairyGuiComponentId) &&
                        (string.IsNullOrWhiteSpace(state.PackageState?.PackageDescriptionId)
                            ? defaultItem.EndsWith(item.FairyGuiComponentId, StringComparison.Ordinal)
                            : string.Equals(defaultItem, "ui://" + state.PackageState.PackageDescriptionId + item.FairyGuiComponentId, StringComparison.Ordinal)));
                    if (itemMapping == null)
                        diagnostics.Add(UiComposerDiagnostic.Error("UICOMP009", "List default item is not mapped to a component recipe.", context.ComponentPath));
                    node.List = new UiListNodeStyle
                    {
                        ItemComponentRecipeId = itemMapping?.ComponentRecipeId ?? string.Empty,
                        Layout = ListLayoutFromXml(Attribute(element, "layout")),
                        LineGap = ParseInt(Attribute(element, "lineGap"), 0),
                        ColumnGap = ParseInt(Attribute(element, "colGap"), 0),
                        SelectionMode = ListSelectionModeFromXml(Attribute(element, "selectionMode")),
                        Overflow = "Scroll",
                        Virtualized = previous?.List?.Virtualized ?? false
                    };
                }
                else
                {
                    string fileName = NormalizePath(Attribute(element, "fileName"));
                    if (!recipesByPath.TryGetValue(fileName, out string recipeId)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP009", "Component is not mapped to a recipe: " + fileName + ".", context.ComponentPath));
                    XElement button = element.Element("Button");
                    string iconUrl = button == null ? string.Empty : Attribute(button, "icon");
                    string iconId = iconUrl.Length >= 8 ? iconUrl.Substring(Math.Max(0, iconUrl.Length - 8)) : string.Empty;
                    resourcesByFguiId.TryGetValue(iconId, out string iconResourceId);
                    node.Component = new UiComponentInstanceNode { ComponentRecipeId = recipeId ?? string.Empty, Title = button == null ? string.Empty : Attribute(button, "title"), IconResourceId = iconResourceId ?? string.Empty };
                }
                nodes.Add(node);
            }
            if (HasErrors(diagnostics)) return null;
            RestoreContainerLayouts(state.Blueprint, nodes, materializedByName, context.ComponentPath, diagnostics);
            if (HasErrors(diagnostics)) return null;
            UiSize design = ParseSize(Attribute(root, "size"));
            return new UiScreenBlueprint { SchemaVersion = state.Blueprint.SchemaVersion, ScreenId = state.Blueprint.ScreenId, DisplayName = state.Blueprint.DisplayName, LibraryId = state.Blueprint.LibraryId, DesignResolution = design, Target = state.Blueprint.Target, Containers = state.Blueprint.Containers ?? Array.Empty<UiLayoutContainer>(), Nodes = nodes.ToArray() };
        }

        private static IReadOnlyList<UiComposerDiagnostic> Validate(string repositoryRoot, ScreenContext context, UiScreenBlueprint blueprint, UiArtResourceLibraryAuthoringState library, UiComposerPackageState packageState, string sourceXmlHash, bool validateSourceDrift)
        {
            var diagnostics = new List<UiComposerDiagnostic>();
            if (blueprint == null)
            {
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP001", "Screen blueprint is required.", context.BlueprintPath));
                return diagnostics;
            }
            bool isV1 = string.Equals(blueprint.SchemaVersion, BlueprintSchemaV1, StringComparison.Ordinal);
            bool isV2 = string.Equals(blueprint.SchemaVersion, BlueprintSchemaV2, StringComparison.Ordinal);
            bool isV3 = string.Equals(blueprint.SchemaVersion, BlueprintSchemaV3, StringComparison.Ordinal);
            if (!isV1 && !isV2 && !isV3) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP001", "Unsupported screen blueprint schemaVersion.", context.BlueprintPath));
            bool isContractV1 = string.Equals(
                context.Contract.SchemaVersion,
                ContractSchemaV1,
                StringComparison.Ordinal);
            bool isContractV2 = string.Equals(
                context.Contract.SchemaVersion,
                ContractSchemaV2,
                StringComparison.Ordinal);
            if (!isContractV1 && !isContractV2)
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP001", "Unsupported binding contract schemaVersion.", context.ContractPath));
            ValidateFeedbackPolicies(
                context.Contract,
                context.ContractPath,
                isContractV2,
                diagnostics);
            if (!TargetMatchesContract(blueprint, context.Contract)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP002", "Blueprint target does not match its generated binding contract.", context.BlueprintPath));
            if (blueprint.DesignResolution == null || blueprint.DesignResolution.Width != 1920 || blueprint.DesignResolution.Height != 1080) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP007", "UI Composer designResolution must be 1920x1080.", context.BlueprintPath));
            if (context.Recipes == null || context.Recipes.SchemaVersion != RecipeSchema) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP001", "Unsupported component recipe catalog schemaVersion.", context.RecipePath));

            var resourceIds = new HashSet<string>((library?.Catalog?.Resources ?? Array.Empty<UiArtResource>()).Select(item => item.ResourceId), StringComparer.Ordinal);
            var recipeIds = new HashSet<string>(StringComparer.Ordinal);
            UiComponentRecipe[] recipes = context.Recipes?.Recipes ?? Array.Empty<UiComponentRecipe>();
            for (int i = 0; i < recipes.Length; i++)
            {
                UiComponentRecipe recipe = recipes[i];
                if (!recipeIds.Add(recipe.ComponentId)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Duplicate component recipe id: " + recipe.ComponentId + ".", context.RecipePath));
                if (string.IsNullOrWhiteSpace(ComponentName(recipe))) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Component recipe requires a FairyGUI component name.", context.RecipePath));
                UiComponentResourceSlot[] slots = recipe.ResourceSlots ?? Array.Empty<UiComponentResourceSlot>();
                for (int j = 0; j < slots.Length; j++) if (!resourceIds.Contains(slots[j].ResourceId)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP005", "Component recipe references an unknown resource: " + slots[j].ResourceId + ".", context.RecipePath));
            }
            ValidateComponentRecipes(recipes, resourceIds, recipeIds, context.RecipePath, diagnostics);
            ValidateComponentBindingContracts(
                context.Contract.ComponentBindings,
                recipes,
                context.ContractPath,
                diagnostics);

            ValidateContainers(blueprint, context.BlueprintPath, isV1, diagnostics);
            var containerIds = new HashSet<string>((blueprint.Containers ?? Array.Empty<UiLayoutContainer>()).Select(item => item.ContainerId), StringComparer.Ordinal);
            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            var nodeNames = new HashSet<string>(StringComparer.Ordinal);
            var assignedBindings = new HashSet<string>(StringComparer.Ordinal);
            UiScreenNode[] nodes = blueprint.Nodes ?? Array.Empty<UiScreenNode>();
            for (int i = 0; i < nodes.Length; i++)
            {
                UiScreenNode node = nodes[i];
                string nodePath = context.BlueprintPath + "#" + (node.NodeId ?? string.Empty);
                if (string.IsNullOrWhiteSpace(node.NodeId) || !nodeIds.Add(node.NodeId)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP003", "Node id is empty or duplicated.", nodePath));
                if (string.IsNullOrWhiteSpace(node.Name) || !nodeNames.Add(node.Name)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP003", "Node name is empty or duplicated.", nodePath));
                if (node.Type != "Graph" && node.Type != "Text" && node.Type != "Image" && node.Type != "ComponentInstance" && node.Type != "List") diagnostics.Add(UiComposerDiagnostic.Error("UICOMP003", "Unsupported node type: " + node.Type + ".", nodePath));
                if (node.Layout == null || node.Layout.Width <= 0 || node.Layout.Height <= 0) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP007", "Node size must be positive.", nodePath));
                else if (!IsAnchor(node.Layout.HorizontalAnchor, true) || !IsAnchor(node.Layout.VerticalAnchor, false)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP007", "Node anchor is unsupported.", nodePath));
                if (!string.IsNullOrWhiteSpace(node.ParentContainerId) && !containerIds.Contains(node.ParentContainerId)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP013", "Node references an unknown layout container.", nodePath));
                if (isV1 && !string.IsNullOrWhiteSpace(node.ParentContainerId)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP001", "V1 blueprints cannot assign nodes to layout containers.", nodePath));
                if (node.Type == "Graph" && node.Graph == null) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP003", "Graph node style is required.", nodePath));
                if (node.Type == "Text" && node.Text == null) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP003", "Text node style is required.", nodePath));
                if (node.Type == "Image" && (node.Image == null || !resourceIds.Contains(node.Image.ResourceId))) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP005", "Image node references an unknown semantic resource.", nodePath));
                if (node.Type == "ComponentInstance")
                {
                    if (node.Component == null || !recipeIds.Contains(node.Component.ComponentRecipeId)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Component instance references an unknown recipe.", nodePath));
                    if (!string.IsNullOrWhiteSpace(node.Component?.IconResourceId) && !resourceIds.Contains(node.Component.IconResourceId)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP005", "Button icon references an unknown semantic resource.", nodePath));
                }
                if (node.Type == "List")
                {
                    if (!isV3) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP001", "List nodes require mx.uiScreenBlueprint.v3.", nodePath));
                    if (node.List == null || !recipeIds.Contains(node.List.ItemComponentRecipeId)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "List references an unknown item component recipe.", nodePath));
                    if (node.List != null && (!IsListLayout(node.List.Layout) || !IsListSelectionMode(node.List.SelectionMode) || node.List.Overflow != "Scroll" || node.List.LineGap < 0 || node.List.ColumnGap < 0))
                        diagnostics.Add(UiComposerDiagnostic.Error("UICOMP016", "List supports Column/Row layout, non-negative gaps, Scroll overflow, and controlled selection modes only.", nodePath));
                }
                if (!string.IsNullOrWhiteSpace(node.BindingSlot))
                {
                    UiComposerBindingSlot binding = FindBindingSlot(context.Contract.Bindings, node.BindingSlot);
                    if (binding == null) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP004", "Unknown binding slot: " + node.BindingSlot + ".", nodePath));
                    else
                    {
                        if (!assignedBindings.Add(node.BindingSlot)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP004", "Binding slot is assigned more than once: " + node.BindingSlot + ".", nodePath));
                        if (binding.ControlName != node.Name || binding.NodeType != node.Type) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP004", "Bound node name or type does not match the C# contract.", nodePath));
                    }
                }
            }
            UiComposerBindingSlot[] bindings = context.Contract.Bindings ?? Array.Empty<UiComposerBindingSlot>();
            for (int i = 0; i < bindings.Length; i++) if (bindings[i].Required && !assignedBindings.Contains(bindings[i].SlotId)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP004", "Required binding slot is unassigned: " + bindings[i].SlotId + ".", context.BlueprintPath));

            if (packageState != null)
            {
                if (packageState.Owner != StateOwner || packageState.PackageId != blueprint.Target.PackageId || packageState.ScreenId != blueprint.ScreenId) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP010", "Package state ownership or target is invalid.", context.StatePath));
                if (validateSourceDrift && packageState.SourceXmlHash != sourceXmlHash) diagnostics.Add(UiComposerDiagnostic.Warning("UICOMP108", "FairyGUI source differs from the last synchronized source. Sync before applying or publishing.", context.ComponentPath));
            }
            else if (File.Exists(ResolvePath(repositoryRoot, context.PackageXmlPath))) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP010", "Target package exists without MxUiComposer ownership state.", context.StatePath));
            SortDiagnostics(diagnostics);
            return diagnostics;
        }

        private static void ValidateAllHashes(UiComposerAuthoringState state, ScreenContext context, UiComposerExpectedHashes expected, List<UiComposerDiagnostic> diagnostics, bool requireSourceAndMapping)
        {
            ValidateExpectedHash(expected?.BlueprintHash, state.BlueprintHash, "UICOMP008", "Blueprint hash is stale.", context.BlueprintPath, diagnostics);
            ValidateExpectedHash(expected?.RecipeCatalogHash, state.RecipeCatalogHash, "UICOMP008", "Component recipe catalog hash is stale.", context.RecipePath, diagnostics);
            ValidateExpectedHash(expected?.CatalogHash, state.CatalogHash, "UICOMP008", "UI art resource catalog hash is stale.", UiArtResourceLibraryService.DefaultCatalogPath, diagnostics);
            ValidateExpectedHash(expected?.ContractHash, state.ContractHash, "UICOMP008", "Binding contract hash is stale.", context.ContractPath, diagnostics);
            if (requireSourceAndMapping)
            {
                ValidateExpectedHash(expected?.SourceXmlHash, state.SourceXmlHash, "UICOMP008", "FairyGUI source XML hash is stale.", context.ComponentPath, diagnostics);
                ValidateExpectedHash(expected?.MappingHash, state.MappingHash, "UICOMP008", "Composer mapping hash is stale.", context.StatePath, diagnostics);
            }
        }

        private static ScreenContext ResolveContext(string repositoryRoot, string screen)
        {
            string blueprintPath = ResolveBlueprintPath(repositoryRoot, screen);
            UiScreenBlueprint blueprint = ReadJson<UiScreenBlueprint>(ResolvePath(repositoryRoot, blueprintPath));
            string libraryRoot = "ArtSource/UIKits/" + blueprint.LibraryId;
            string recipePath = libraryRoot + "/ui-component-recipes.json";
            string contractPath = ContractPathForBlueprint(blueprintPath);
            return new ScreenContext(
                blueprintPath,
                recipePath,
                contractPath,
                blueprint,
                ReadJson<UiComponentRecipeCatalog>(ResolvePath(repositoryRoot, recipePath)),
                ReadBindingContract(ResolvePath(repositoryRoot, contractPath)));
        }

        private static string ResolveBlueprintPath(string repositoryRoot, string screen)
        {
            string value = NormalizePath(screen);
            if (string.IsNullOrWhiteSpace(value) || value == DefaultScreenId || value == DefaultBlueprintPath) return DefaultBlueprintPath;
            if (value == DemoScreenId || value == DemoBlueprintPath) return DemoBlueprintPath;
            IReadOnlyList<UiComposerScreenDescriptor> screens = ListScreens(repositoryRoot);
            for (int i = 0; i < screens.Count; i++) if (screens[i].ScreenId == value || screens[i].BlueprintPath == value) return screens[i].BlueprintPath;
            throw new InvalidOperationException("Unknown UI Composer screen: " + screen + ".");
        }

        private static string ContractPathForBlueprint(string blueprintPath)
        {
            string normalized = NormalizePath(blueprintPath);
            string screensDirectory = NormalizePath(Path.GetDirectoryName(normalized) ?? string.Empty);
            string libraryRoot = NormalizePath(Path.GetDirectoryName(screensDirectory) ?? string.Empty);
            string fileName = Path.GetFileName(normalized).Replace(".ui-screen.json", ".ui-composer-binding-contract.json", StringComparison.Ordinal);
            return libraryRoot + "/contracts/" + fileName;
        }

        private static UiComposerScreenDescriptor Describe(UiScreenBlueprint blueprint, string blueprintPath) => new UiComposerScreenDescriptor(blueprint.ScreenId, blueprint.DisplayName, blueprint.LibraryId, blueprintPath, blueprint.Target.PackageId, blueprint.Target.ComponentName);

        private static bool IsSafeTarget(UiScreenTarget target)
        {
            if (target == null || !IsSafeIdentifier(target.PackageId) || !IsSafeIdentifier(target.ComponentName)) return false;
            return NormalizePath(target.PackageSourcePath) == "FGUIProject/assets/" + target.PackageId;
        }

        private static bool TargetMatchesContract(UiScreenBlueprint blueprint, UiComposerBindingContract contract)
        {
            return blueprint != null && contract != null && IsSafeTarget(blueprint.Target)
                && blueprint.ScreenId == contract.ScreenId
                && blueprint.Target.PackageId == contract.Target?.PackageId
                && blueprint.Target.ComponentName == contract.Target?.ComponentName
                && NormalizePath(blueprint.Target.PackageSourcePath) == NormalizePath(contract.Target?.PackageSourcePath);
        }

        private static UiComposerBindingContract ReadBindingContract(string path)
        {
            UiComposerBindingContract contract = ReadJson<UiComposerBindingContract>(path);
            if (!string.Equals(
                    contract.SchemaVersion,
                    ContractSchemaV1,
                    StringComparison.Ordinal))
            {
                return contract;
            }

            ProjectV1BindingsAsSilent(contract.Bindings);
            UiComposerComponentBindingContract[] components = contract.ComponentBindings
                ?? Array.Empty<UiComposerComponentBindingContract>();
            for (int i = 0; i < components.Length; i++)
                ProjectV1BindingsAsSilent(components[i]?.Bindings);
            return contract;
        }

        private static void ProjectV1BindingsAsSilent(UiComposerBindingSlot[] bindings)
        {
            UiComposerBindingSlot[] values = bindings ?? Array.Empty<UiComposerBindingSlot>();
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null || string.IsNullOrWhiteSpace(values[i].CommandId))
                    continue;

                values[i].Feedback = new UiComposerFeedbackPolicy
                {
                    Activation = "None",
                    Success = "None",
                    Failure = "None"
                };
            }
        }

        private static void ValidateFeedbackPolicies(
            UiComposerBindingContract contract,
            string contractPath,
            bool requireExplicitFeedback,
            List<UiComposerDiagnostic> diagnostics)
        {
            ValidateFeedbackBindings(
                contract?.Bindings,
                contractPath,
                requireExplicitFeedback,
                diagnostics);
            UiComposerComponentBindingContract[] components = contract?.ComponentBindings
                ?? Array.Empty<UiComposerComponentBindingContract>();
            for (int i = 0; i < components.Length; i++)
            {
                ValidateFeedbackBindings(
                    components[i]?.Bindings,
                    contractPath + "#" + (components[i]?.ComponentRecipeId ?? string.Empty),
                    requireExplicitFeedback,
                    diagnostics);
            }
        }

        private static void ValidateFeedbackBindings(
            UiComposerBindingSlot[] bindings,
            string path,
            bool requireExplicitFeedback,
            List<UiComposerDiagnostic> diagnostics)
        {
            UiComposerBindingSlot[] values = bindings ?? Array.Empty<UiComposerBindingSlot>();
            for (int i = 0; i < values.Length; i++)
            {
                UiComposerBindingSlot binding = values[i];
                if (binding == null || string.IsNullOrWhiteSpace(binding.CommandId))
                    continue;

                if (!requireExplicitFeedback)
                    continue;

                UiComposerFeedbackPolicy feedback = binding.Feedback;
                if (feedback == null ||
                    !IsFeedbackKind(feedback.Activation) ||
                    !IsFeedbackKind(feedback.Success) ||
                    !IsFeedbackKind(feedback.Failure))
                {
                    diagnostics.Add(UiComposerDiagnostic.Error(
                        "UICOMP015",
                        "V2 command binding requires explicit activation, success, and failure feedback.",
                        path + "#" + (binding.SlotId ?? string.Empty)));
                }
            }
        }

        private static bool IsFeedbackKind(string value)
        {
            return value == "None" ||
                   value == "Click" ||
                   value == "Confirm" ||
                   value == "Back" ||
                   value == "Error";
        }

        private static bool IsSafeIdentifier(string value) => !string.IsNullOrWhiteSpace(value) && value.All(ch => char.IsLetterOrDigit(ch) || ch == '_');

        private static string[] BuildMaterializationTargetPaths(ScreenContext context, UiComposerMaterializationPayload payload)
        {
            var paths = new List<string> { context.PackageXmlPath, context.ComponentPath, context.StatePath };
            paths.AddRange(payload.Components.Select(item => context.Blueprint.Target.PackageSourcePath + "/" + item.ComponentRelativePath));
            paths.AddRange(payload.Resources.Select(item => context.Blueprint.Target.PackageSourcePath + "/" + item.TargetPngRelativePath));
            return paths.ToArray();
        }

        private static IEnumerable<UiComposerResourceMapping> ResourceMappings(UiComposerPackageState state)
        {
            if (state?.ResourceMappings?.Length > 0) return state.ResourceMappings;
            if (!string.IsNullOrWhiteSpace(state?.ImageResourceId)) return new[] { new UiComposerResourceMapping { ResourceId = "ui.button.generic.base", FairyGuiResourceId = state.ImageResourceId, TargetRelativePath = "Images/MxUiFoundation/buttons/button_generic_240x64.png" } };
            return Array.Empty<UiComposerResourceMapping>();
        }

        private static IEnumerable<UiComposerComponentMapping> ComponentMappings(UiComposerPackageState state)
        {
            if (state?.ComponentMappings?.Length > 0) return state.ComponentMappings;
            if (!string.IsNullOrWhiteSpace(state?.ButtonComponentId)) return new[] { new UiComposerComponentMapping { ComponentRecipeId = "ui.component.button.generic", FairyGuiComponentId = state.ButtonComponentId, ComponentName = "GenericButton", ComponentRelativePath = "Components/GenericButton.xml" } };
            return Array.Empty<UiComposerComponentMapping>();
        }

        private static bool ValidateSupportedElementShape(XElement element, string kind, string path, List<UiComposerDiagnostic> diagnostics)
        {
            var allowed = new HashSet<string>(StringComparer.Ordinal) { "id", "name", "xy", "size", "visible" };
            if (kind == "graph") allowed.UnionWith(new[] { "type", "fillColor", "corner" });
            else if (kind == "text") allowed.UnionWith(new[] { "font", "fontSize", "color", "align", "vAlign", "autoSize", "singleLine", "bold", "input", "restrict", "maxLength", "text" });
            else if (kind == "image") allowed.UnionWith(new[] { "src", "fileName", "aspect", "color" });
            else if (kind == "list") allowed.UnionWith(new[] { "layout", "selectionMode", "overflow", "scroll", "lineGap", "colGap", "defaultItem" });
            else allowed.UnionWith(new[] { "src", "fileName" });
            foreach (XAttribute attribute in element.Attributes())
            {
                if (!allowed.Contains(attribute.Name.LocalName))
                {
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP009", "Unsupported FairyGUI attribute: " + attribute.Name.LocalName + ".", path));
                    return false;
                }
            }
            if (kind == "list")
            {
                string layout = Attribute(element, "layout");
                string selection = Attribute(element, "selectionMode");
                string overflow = Attribute(element, "overflow");
                string scroll = Attribute(element, "scroll");
                string lineGap = Attribute(element, "lineGap");
                string columnGap = Attribute(element, "colGap");
                bool supportedLayout = string.IsNullOrEmpty(layout) || layout == "column" || layout == "row";
                bool supportedSelection = string.IsNullOrEmpty(selection) || selection == "single" || selection == "multiple" || selection == "multipleSingleClick" || selection == "none";
                bool supportedScroll = layout == "row" ? scroll == "horizontal" : string.IsNullOrEmpty(scroll) || scroll == "vertical";
                bool validLineGap = string.IsNullOrEmpty(lineGap) || int.TryParse(lineGap, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedLineGap) && parsedLineGap >= 0;
                bool validColumnGap = string.IsNullOrEmpty(columnGap) || int.TryParse(columnGap, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedColumnGap) && parsedColumnGap >= 0;
                if (!supportedLayout || !supportedSelection || overflow != "scroll" || !supportedScroll || !validLineGap || !validColumnGap || !Attribute(element, "defaultItem").StartsWith("ui://", StringComparison.Ordinal))
                {
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP009", "FairyGUI list uses properties outside the controlled UI Composer subset.", path));
                    return false;
                }
            }
            foreach (XElement child in element.Elements())
            {
                if (child.Name.LocalName != "relation" && !(kind == "component" && child.Name.LocalName == "Button"))
                {
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP009", "Unsupported FairyGUI child element: " + child.Name.LocalName + ".", path));
                    return false;
                }
                if (child.Name.LocalName == "Button" && child.Attributes().Any(item => item.Name.LocalName != "title" && item.Name.LocalName != "icon"))
                {
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP009", "Unsupported FairyGUI Button property.", path));
                    return false;
                }
            }
            return true;
        }

        private static UiNodeLayout ParseLayout(XElement element)
        {
            UiSize position = ParseSize(Attribute(element, "xy"));
            UiSize size = ParseSize(Attribute(element, "size"));
            string relation = element.Element("relation") != null ? Attribute(element.Element("relation"), "sidePair") : string.Empty;
            return new UiNodeLayout
            {
                X = position.Width, Y = position.Height, Width = size.Width, Height = size.Height,
                HorizontalAnchor = relation.Contains("width-width", StringComparison.Ordinal) ? "Stretch" : relation.Contains("center-center", StringComparison.Ordinal) ? "Center" : relation.Contains("right-right", StringComparison.Ordinal) ? "Right" : "Left",
                VerticalAnchor = relation.Contains("height-height", StringComparison.Ordinal) ? "Stretch" : relation.Contains("middle-middle", StringComparison.Ordinal) ? "Middle" : relation.Contains("bottom-bottom", StringComparison.Ordinal) ? "Bottom" : "Top"
            };
        }

        private static void BuildDiff(UiScreenBlueprint current, UiScreenBlueprint proposed, List<UiComposerDiffEntry> diffs)
        {
            var currentByName = current.Nodes.ToDictionary(node => node.Name, StringComparer.Ordinal);
            for (int i = 0; i < proposed.Nodes.Length; i++)
            {
                UiScreenNode node = proposed.Nodes[i];
                if (!currentByName.TryGetValue(node.Name, out UiScreenNode old)) { diffs.Add(new UiComposerDiffEntry(node.NodeId, "Added", string.Empty, node.Name)); continue; }
                string before = NodeSummary(old); string after = NodeSummary(node);
                if (before != after) diffs.Add(new UiComposerDiffEntry(node.NodeId, "Changed", before, after));
                currentByName.Remove(node.Name);
            }
            foreach (UiScreenNode removed in currentByName.Values.OrderBy(node => node.NodeId, StringComparer.Ordinal)) diffs.Add(new UiComposerDiffEntry(removed.NodeId, "Removed", removed.Name, string.Empty));
        }

        private static string NodeSummary(UiScreenNode node)
        {
            UiNodeLayout layout = node.Layout ?? new UiNodeLayout();
            string value = node.Text != null
                ? node.Text.Value + "|" + node.Text.Input + "|" + node.Text.Restrict + "|" + node.Text.MaxLength
                : node.Component?.Title ?? node.Image?.ResourceId ?? node.Graph?.FillColor
                    ?? (node.List == null ? string.Empty : node.List.ItemComponentRecipeId + "|" + node.List.Layout + "|" + node.List.LineGap + "|" + node.List.ColumnGap + "|" + node.List.SelectionMode + "|" + node.List.Overflow + "|" + node.List.Virtualized);
            string icon = node.Component?.IconResourceId ?? string.Empty;
            return node.Name + "|" + node.Type + "|" + node.Order + "|" + layout.X + "," + layout.Y + "," + layout.Width + "," + layout.Height + "|" + layout.HorizontalAnchor + "," + layout.VerticalAnchor + "|" + value + "|" + icon + "|" + node.Visible;
        }

        private static UiComposerBindingSlot FindBindingSlot(UiComposerBindingSlot[] bindings, string slotId) => (bindings ?? Array.Empty<UiComposerBindingSlot>()).FirstOrDefault(item => item.SlotId == slotId);
        private static string FindBindingSlotByControlName(UiComposerBindingSlot[] bindings, string controlName) => (bindings ?? Array.Empty<UiComposerBindingSlot>()).FirstOrDefault(item => item.ControlName == controlName)?.SlotId ?? string.Empty;
        private static string ComponentName(UiComponentRecipe recipe) => string.IsNullOrWhiteSpace(recipe.FairyGuiComponentName) ? recipe.ComponentId.Split('.').Last() : recipe.FairyGuiComponentName;
        private static string TargetPngRelativePath(string libraryId, string relativePath)
        {
            string normalized = NormalizePath(relativePath);
            if (normalized.StartsWith("images/", StringComparison.Ordinal)) normalized = normalized.Substring("images/".Length);
            return "Images/" + libraryId + "/" + normalized;
        }
        private static string ResourceToken(string resourceId) => "{{RESOURCE_" + TokenPart(resourceId) + "}}";
        private static string ComponentToken(string recipeId) => "{{COMPONENT_" + TokenPart(recipeId) + "}}";
        private static string TokenPart(string value) => new string((value ?? string.Empty).Select(ch => char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '_').ToArray());

        private static void AppendNodeCloseAndRelation(StringBuilder builder, UiNodeLayout layout, string elementName)
        {
            string relation = BuildRelation(layout);
            if (string.IsNullOrEmpty(relation)) { builder.Append("/>\n"); return; }
            builder.Append(">\n      <relation target=\"\" sidePair=\"").Append(relation).Append("\"/>\n    </").Append(elementName).Append(">\n");
        }

        private static void AppendRelation(StringBuilder builder, UiNodeLayout layout, string indent)
        {
            string relation = BuildRelation(layout);
            if (!string.IsNullOrEmpty(relation)) builder.Append(indent).Append("<relation target=\"\" sidePair=\"").Append(relation).Append("\"/>\n");
        }

        private static string BuildRelation(UiNodeLayout layout)
        {
            var parts = new List<string>(2);
            if (layout.HorizontalAnchor == "Center") parts.Add("center-center"); else if (layout.HorizontalAnchor == "Right") parts.Add("right-right"); else if (layout.HorizontalAnchor == "Stretch") parts.Add("width-width");
            if (layout.VerticalAnchor == "Middle") parts.Add("middle-middle"); else if (layout.VerticalAnchor == "Bottom") parts.Add("bottom-bottom"); else if (layout.VerticalAnchor == "Stretch") parts.Add("height-height");
            return string.Join(",", parts);
        }

        private static bool IsListLayout(string value) => value == "Column" || value == "Row";
        private static bool IsListSelectionMode(string value) => value == "Single" || value == "Multiple" || value == "MultipleSingleClick" || value == "None";
        private static string ListLayoutFromXml(string value) => value == "row" ? "Row" : "Column";
        private static string ListSelectionModeFromXml(string value) => value == "multiple" ? "Multiple" : value == "multipleSingleClick" ? "MultipleSingleClick" : value == "none" ? "None" : "Single";
        private static string ListSelectionModeToXml(string value) => value == "Multiple" ? "multiple" : value == "MultipleSingleClick" ? "multipleSingleClick" : value == "None" ? "none" : "single";
        private static bool IsAnchor(string value, bool horizontal) => horizontal ? value == "Left" || value == "Center" || value == "Right" || value == "Stretch" : value == "Top" || value == "Middle" || value == "Bottom" || value == "Stretch";
        private static bool HasErrors(IReadOnlyList<UiComposerDiagnostic> diagnostics) => diagnostics.Any(item => item.Blocking || item.Severity == "error");
        private static void SortDiagnostics(List<UiComposerDiagnostic> diagnostics) => diagnostics.Sort((left, right) => { int code = string.CompareOrdinal(left.Code, right.Code); if (code != 0) return code; int path = string.CompareOrdinal(left.Path, right.Path); return path != 0 ? path : string.CompareOrdinal(left.NodeId, right.NodeId); });
        private static void ValidateExpectedHash(string expected, string current, string code, string message, string path, List<UiComposerDiagnostic> diagnostics) { if ((expected ?? string.Empty) != (current ?? string.Empty)) diagnostics.Add(UiComposerDiagnostic.Error(code, message, path)); }
        private static T ReadJson<T>(string path) { if (!File.Exists(path)) throw new FileNotFoundException("Required UI Composer file is missing.", path); T value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions); return value == null ? throw new InvalidOperationException("UI Composer JSON is empty: " + path + ".") : value; }
        private static T TryReadJson<T>(string path) where T : class { if (!File.Exists(path)) return null; try { return ReadJson<T>(path); } catch (JsonException) { return null; } }
        private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine;
        private static string ComputeFileHash(string path) { if (!File.Exists(path)) return string.Empty; using SHA256 sha = SHA256.Create(); byte[] hash = sha.ComputeHash(File.ReadAllBytes(path)); return string.Concat(hash.Select(item => item.ToString("x2", CultureInfo.InvariantCulture))); }
        private static void WriteTextAtomic(string path, string text) { Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty); string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N"); File.WriteAllText(temporary, text, new UTF8Encoding(false)); if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path); }
        private static string ResolvePath(string repositoryRoot, string relativePath) { EnsureRepositoryRoot(repositoryRoot); string root = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar; string path = Path.GetFullPath(Path.Combine(root, NormalizePath(relativePath).Replace('/', Path.DirectorySeparatorChar))); if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("UI Composer path escapes the repository root: " + relativePath + "."); return path; }
        private static string ToRelativePath(string repositoryRoot, string path) { string root = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar; string full = Path.GetFullPath(path); if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Path is outside repository root."); return NormalizePath(full.Substring(root.Length)); }
        private static void EnsureRepositoryRoot(string repositoryRoot) { if (string.IsNullOrWhiteSpace(repositoryRoot)) throw new ArgumentException("Repository root is required.", nameof(repositoryRoot)); }
        private static string EnsureOperationId(string value) { string id = value ?? string.Empty; if (id.Length == 0 || id.Length > 64 || id.Any(ch => !char.IsLetterOrDigit(ch) && ch != '-')) throw new InvalidOperationException("Invalid UI Composer operation id."); return id; }
        private static string NormalizePath(string value) => (value ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/');
        private static string EscapeXml(string value) => System.Security.SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
        private static string Attribute(XElement element, string name) => element?.Attribute(name)?.Value ?? string.Empty;
        private static string DefaultIfEmpty(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;
        private static bool ParseBool(string value) => value.Equals("true", StringComparison.OrdinalIgnoreCase);
        private static bool? ParseOptionalBool(string value) => string.IsNullOrWhiteSpace(value) ? null : ParseBool(value);
        private static int ParseInt(string value, int fallback) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : fallback;
        private static int ParseFirstInt(string value) => ParseInt((value ?? string.Empty).Split(',')[0], 0);
        private static UiSize ParseSize(string value) { string[] parts = (value ?? string.Empty).Split(','); return new UiSize { Width = parts.Length > 0 ? ParseInt(parts[0], 0) : 0, Height = parts.Length > 1 ? ParseInt(parts[1], 0) : 0 }; }

        private sealed class ScreenContext
        {
            public ScreenContext(string blueprintPath, string recipePath, string contractPath, UiScreenBlueprint blueprint, UiComponentRecipeCatalog recipes, UiComposerBindingContract contract)
            {
                BlueprintPath = blueprintPath; RecipePath = recipePath; ContractPath = contractPath; Blueprint = blueprint; Recipes = recipes; Contract = contract;
                StatePath = NormalizePath(blueprint.Target.PackageSourcePath) + "/ui-composer-state.json";
                PackageXmlPath = NormalizePath(blueprint.Target.PackageSourcePath) + "/package.xml";
                ComponentPath = NormalizePath(blueprint.Target.PackageSourcePath) + "/" + blueprint.Target.ComponentName + ".xml";
            }
            public string BlueprintPath { get; }
            public string RecipePath { get; }
            public string ContractPath { get; }
            public UiScreenBlueprint Blueprint { get; }
            public UiComponentRecipeCatalog Recipes { get; }
            public UiComposerBindingContract Contract { get; }
            public string StatePath { get; }
            public string PackageXmlPath { get; }
            public string ComponentPath { get; }
        }
    }
}
