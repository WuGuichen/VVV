using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using MxFramework.Authoring.FairyGuiArtSlices;

namespace MxFramework.Authoring.UiComposer
{
    /// <summary>Low-frequency authoring service for hash-guarded FairyGUI screen composition.</summary>
    /// <remarks>LowFreqAlloc. This service is not used by runtime UI or gameplay ticks.</remarks>
    internal static class UiComposerLegacyService
    {
        public const string AuthoringApiVersion = "mx.uiComposer.authoring.v1";
        public const string DefaultScreenId = "ui.screen.foundation.demo";
        public const string DefaultBlueprintPath = "ArtSource/UIKits/MxUiFoundation/screens/ui_foundation_demo.ui-screen.json";
        public const string DefaultRecipeCatalogPath = "ArtSource/UIKits/MxUiFoundation/ui-component-recipes.json";
        public const string DefaultLibraryId = "MxUiFoundation";
        public const string TargetPackageId = "MxUiFoundationDemo";
        public const string TargetPackageSourcePath = "FGUIProject/assets/MxUiFoundationDemo";
        public const string TargetComponentName = "UiFoundationDemoPanel";
        public const string TargetComponentPath = "FGUIProject/assets/MxUiFoundationDemo/UiFoundationDemoPanel.xml";
        public const string TargetPackageStatePath = "FGUIProject/assets/MxUiFoundationDemo/ui-composer-state.json";

        private const string BlueprintSchema = "mx.uiScreenBlueprint.v1";
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

        private static readonly UiComposerBindingSlot[] BindingSlots =
        {
            new UiComposerBindingSlot("status", "status", "Text", true, string.Empty),
            new UiComposerBindingSlot("primary", "btnPrimary", "ComponentInstance", true, "ui.foundation.demo.primary"),
            new UiComposerBindingSlot("secondary", "btnSecondary", "ComponentInstance", true, "ui.foundation.demo.secondary"),
            new UiComposerBindingSlot("close", "btnClose", "ComponentInstance", true, "ui.foundation.demo.close")
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
                string relative = ToRelativePath(repositoryRoot, paths[i]);
                try
                {
                    UiScreenBlueprint blueprint = ReadJson<UiScreenBlueprint>(paths[i]);
                    if (blueprint != null && IsSupportedTarget(blueprint.Target))
                    {
                        screens.Add(new UiComposerScreenDescriptor(
                            blueprint.ScreenId,
                            blueprint.DisplayName,
                            blueprint.LibraryId,
                            relative,
                            blueprint.Target.PackageId,
                            blueprint.Target.ComponentName));
                    }
                }
                catch (JsonException)
                {
                }
            }

            screens.Sort((left, right) => string.CompareOrdinal(left.ScreenId, right.ScreenId));
            return screens;
        }

        public static UiComposerAuthoringState ReadState(string repositoryRoot, string screen)
        {
            string blueprintPath = ResolveBlueprintPath(repositoryRoot, screen);
            UiScreenBlueprint blueprint = ReadJson<UiScreenBlueprint>(ResolvePath(repositoryRoot, blueprintPath));
            UiComponentRecipeCatalog recipes = ReadJson<UiComponentRecipeCatalog>(ResolvePath(repositoryRoot, DefaultRecipeCatalogPath));
            UiArtResourceLibraryAuthoringState library = UiArtResourceLibraryService.ReadAuthoringState(repositoryRoot, blueprint.LibraryId);
            UiComposerPackageState packageState = TryReadJson<UiComposerPackageState>(ResolvePath(repositoryRoot, TargetPackageStatePath));
            string blueprintHash = ComputeFileHash(ResolvePath(repositoryRoot, blueprintPath));
            string recipeHash = ComputeFileHash(ResolvePath(repositoryRoot, DefaultRecipeCatalogPath));
            string sourceXmlHash = ComputeFileHash(ResolvePath(repositoryRoot, TargetComponentPath));
            string mappingHash = ComputeFileHash(ResolvePath(repositoryRoot, TargetPackageStatePath));
            IReadOnlyList<UiComposerDiagnostic> diagnostics = Validate(
                repositoryRoot,
                blueprintPath,
                blueprint,
                recipes,
                library,
                packageState,
                sourceXmlHash,
                validateSourceDrift: true);

            return new UiComposerAuthoringState(
                new UiComposerScreenDescriptor(
                    blueprint.ScreenId,
                    blueprint.DisplayName,
                    blueprint.LibraryId,
                    blueprintPath,
                    blueprint.Target.PackageId,
                    blueprint.Target.ComponentName),
                blueprintHash,
                recipeHash,
                library.CatalogHash,
                sourceXmlHash,
                mappingHash,
                blueprint,
                recipes,
                library.Catalog,
                packageState,
                BindingSlots,
                diagnostics);
        }

        public static UiComposerValidationResult ValidateBlueprint(
            string repositoryRoot,
            string screen,
            UiScreenBlueprint proposedBlueprint)
        {
            string blueprintPath = ResolveBlueprintPath(repositoryRoot, screen);
            UiComponentRecipeCatalog recipes = ReadJson<UiComponentRecipeCatalog>(ResolvePath(repositoryRoot, DefaultRecipeCatalogPath));
            UiArtResourceLibraryAuthoringState library = UiArtResourceLibraryService.ReadAuthoringState(repositoryRoot, proposedBlueprint?.LibraryId ?? DefaultLibraryId);
            UiComposerPackageState packageState = TryReadJson<UiComposerPackageState>(ResolvePath(repositoryRoot, TargetPackageStatePath));
            IReadOnlyList<UiComposerDiagnostic> diagnostics = Validate(
                repositoryRoot,
                blueprintPath,
                proposedBlueprint,
                recipes,
                library,
                packageState,
                ComputeFileHash(ResolvePath(repositoryRoot, TargetComponentPath)),
                validateSourceDrift: false);
            return new UiComposerValidationResult(diagnostics);
        }

        public static UiComposerOperationPlan PlanSave(
            string repositoryRoot,
            string screen,
            string expectedBlueprintHash,
            UiScreenBlueprint proposedBlueprint)
        {
            string blueprintPath = ResolveBlueprintPath(repositoryRoot, screen);
            var diagnostics = new List<UiComposerDiagnostic>();
            ValidateExpectedHash(
                expectedBlueprintHash,
                ComputeFileHash(ResolvePath(repositoryRoot, blueprintPath)),
                "UICOMP008",
                "Blueprint changed on disk. Refresh before saving.",
                blueprintPath,
                diagnostics);
            diagnostics.AddRange(ValidateBlueprint(repositoryRoot, screen, proposedBlueprint).Diagnostics);
            SortDiagnostics(diagnostics);
            return new UiComposerOperationPlan(
                "SaveBlueprint",
                HasErrors(diagnostics),
                wouldModifyBlueprint: !HasErrors(diagnostics),
                wouldModifyFairyGuiSource: false,
                wouldPublish: false,
                new[] { blueprintPath },
                diagnostics,
                null,
                Array.Empty<UiComposerDiffEntry>());
        }

        public static UiComposerAuthoringState SaveBlueprint(
            string repositoryRoot,
            string screen,
            string expectedBlueprintHash,
            UiScreenBlueprint proposedBlueprint)
        {
            UiComposerOperationPlan plan = PlanSave(repositoryRoot, screen, expectedBlueprintHash, proposedBlueprint);
            if (plan.Blocked)
                throw new InvalidOperationException("UI Composer blueprint save is blocked.");

            string path = ResolvePath(repositoryRoot, ResolveBlueprintPath(repositoryRoot, screen));
            WriteTextAtomic(path, Serialize(proposedBlueprint));
            return ReadState(repositoryRoot, proposedBlueprint.ScreenId);
        }

        public static UiComposerOperationPlan PlanApply(
            string repositoryRoot,
            string screen,
            UiComposerExpectedHashes expected)
        {
            UiComposerAuthoringState state = ReadState(repositoryRoot, screen);
            var diagnostics = new List<UiComposerDiagnostic>();
            ValidateAllHashes(state, expected, diagnostics, requireSourceAndMapping: state.PackageState != null);
            diagnostics.AddRange(state.Diagnostics);

            if (state.PackageState != null
                && !string.Equals(state.PackageState.Owner, StateOwner, StringComparison.Ordinal))
            {
                diagnostics.Add(UiComposerDiagnostic.Error(
                    "UICOMP010",
                    "Target package is not owned by MxUiComposer.",
                    TargetPackageStatePath));
            }
            if (state.PackageState != null
                && !string.Equals(state.PackageState.SourceXmlHash, state.SourceXmlHash, StringComparison.Ordinal))
            {
                diagnostics.Add(UiComposerDiagnostic.Error(
                    "UICOMP008",
                    "FairyGUI source drifted from the synchronized mapping. Sync before applying.",
                    TargetComponentPath));
            }

            UiComposerMaterializationPayload payload = null;
            if (!HasErrors(diagnostics))
                payload = BuildMaterializationPayload(state);

            SortDiagnostics(diagnostics);
            return new UiComposerOperationPlan(
                "ApplyToFairyGui",
                HasErrors(diagnostics),
                wouldModifyBlueprint: false,
                wouldModifyFairyGuiSource: !HasErrors(diagnostics),
                wouldPublish: false,
                new[]
                {
                    TargetPackageSourcePath + "/package.xml",
                    TargetPackageSourcePath + "/Components/GenericButton.xml",
                    TargetComponentPath,
                    TargetPackageStatePath,
                    TargetPackageSourcePath + "/Images/MxUiFoundation/buttons/button_generic_240x64.png"
                },
                diagnostics,
                payload,
                Array.Empty<UiComposerDiffEntry>());
        }

        public static UiComposerQueuedOperation QueueApply(
            string repositoryRoot,
            string screen,
            UiComposerExpectedHashes expected)
        {
            UiComposerOperationPlan plan = PlanApply(repositoryRoot, screen, expected);
            if (plan.Blocked || plan.Materialization == null)
                throw new InvalidOperationException("UI Composer apply is blocked.");

            return QueueOperation(repositoryRoot, "Materialize", plan.Materialization, plan.Materialization.PackageId);
        }

        public static UiComposerSyncPlan PlanSync(
            string repositoryRoot,
            string screen,
            UiComposerExpectedHashes expected)
        {
            UiComposerAuthoringState state = ReadState(repositoryRoot, screen);
            var diagnostics = new List<UiComposerDiagnostic>();
            ValidateExpectedHash(expected?.BlueprintHash, state.BlueprintHash, "UICOMP008", "Blueprint changed on disk.", state.Screen.BlueprintPath, diagnostics);
            ValidateExpectedHash(expected?.SourceXmlHash, state.SourceXmlHash, "UICOMP008", "FairyGUI component changed again. Refresh before syncing.", TargetComponentPath, diagnostics);
            ValidateExpectedHash(expected?.MappingHash, state.MappingHash, "UICOMP008", "Composer mapping changed on disk.", TargetPackageStatePath, diagnostics);

            UiScreenBlueprint proposed = null;
            var diffs = new List<UiComposerDiffEntry>();
            if (state.PackageState == null || string.IsNullOrWhiteSpace(state.SourceXmlHash))
            {
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP011", "No materialized FairyGUI screen is available to synchronize.", TargetComponentPath));
            }
            else if (!HasErrors(diagnostics))
            {
                proposed = ParseSupportedComponentXml(repositoryRoot, state, diagnostics);
                if (proposed != null)
                    BuildDiff(state.Blueprint, proposed, diffs);
            }

            SortDiagnostics(diagnostics);
            return new UiComposerSyncPlan(HasErrors(diagnostics), proposed, diffs, diagnostics);
        }

        public static UiComposerAuthoringState SyncBlueprint(
            string repositoryRoot,
            string screen,
            UiComposerExpectedHashes expected)
        {
            UiComposerSyncPlan plan = PlanSync(repositoryRoot, screen, expected);
            if (plan.Blocked || plan.ProposedBlueprint == null)
                throw new InvalidOperationException("UI Composer synchronization is blocked.");

            string path = ResolvePath(repositoryRoot, ResolveBlueprintPath(repositoryRoot, screen));
            WriteTextAtomic(path, Serialize(plan.ProposedBlueprint));
            UiComposerPackageState packageState = ReadJson<UiComposerPackageState>(ResolvePath(repositoryRoot, TargetPackageStatePath));
            packageState.SourceXmlHash = ComputeFileHash(ResolvePath(repositoryRoot, TargetComponentPath));
            packageState.BlueprintHash = ComputeFileHash(path);
            WriteTextAtomic(ResolvePath(repositoryRoot, TargetPackageStatePath), Serialize(packageState));
            return ReadState(repositoryRoot, plan.ProposedBlueprint.ScreenId);
        }

        public static UiComposerOperationPlan PlanPublish(
            string repositoryRoot,
            string screen,
            UiComposerExpectedHashes expected)
        {
            UiComposerAuthoringState state = ReadState(repositoryRoot, screen);
            var diagnostics = new List<UiComposerDiagnostic>();
            ValidateAllHashes(state, expected, diagnostics, requireSourceAndMapping: true);
            if (state.PackageState == null)
            {
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP011", "Materialize the screen before publishing.", TargetPackageStatePath));
            }
            else
            {
                if (!string.Equals(state.PackageState.BlueprintHash, state.BlueprintHash, StringComparison.Ordinal))
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP008", "Materialized source is older than the current blueprint.", TargetComponentPath));
                if (!string.Equals(state.PackageState.SourceXmlHash, state.SourceXmlHash, StringComparison.Ordinal))
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP008", "FairyGUI source changed after materialization. Sync or apply before publishing.", TargetComponentPath));
            }

            SortDiagnostics(diagnostics);
            return new UiComposerOperationPlan(
                "PublishFairyGui",
                HasErrors(diagnostics),
                false,
                false,
                !HasErrors(diagnostics),
                new[] { "Assets/Bundles/FGUI/MxUiFoundationDemo/MxUiFoundationDemo_fui.bytes" },
                diagnostics,
                null,
                Array.Empty<UiComposerDiffEntry>());
        }

        public static UiComposerQueuedOperation QueuePublish(
            string repositoryRoot,
            string screen,
            UiComposerExpectedHashes expected)
        {
            UiComposerOperationPlan plan = PlanPublish(repositoryRoot, screen, expected);
            if (plan.Blocked)
                throw new InvalidOperationException("UI Composer publish is blocked.");

            return QueueOperation(repositoryRoot, "Publish", null, TargetPackageId);
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

        private static UiComposerQueuedOperation QueueOperation(
            string repositoryRoot,
            string action,
            UiComposerMaterializationPayload materialization,
            string packageId)
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

        private static UiComposerMaterializationPayload BuildMaterializationPayload(UiComposerAuthoringState state)
        {
            UiArtResource resource = FindResource(state.ResourceCatalog, "ui.button.generic.base");
            UiArtRect scale9 = resource?.RecommendedFairyGuiUsage?.Scale9Grid ?? new UiArtRect();
            var nodeMappings = new List<UiComposerNodeMapping>();
            string panelTemplate = BuildPanelXml(state.Blueprint, nodeMappings);
            string buttonTemplate = BuildButtonXml(state.ComponentRecipes);
            string packageTemplate = BuildPackageXml(scale9);
            return new UiComposerMaterializationPayload
            {
                ScreenId = state.Blueprint.ScreenId,
                LibraryId = state.Blueprint.LibraryId,
                PackageId = TargetPackageId,
                PackageSourcePath = TargetPackageSourcePath,
                ComponentName = TargetComponentName,
                SourcePngPath = "ArtSource/UIKits/MxUiFoundation/" + resource.RelativePath,
                TargetPngRelativePath = "Images/MxUiFoundation/buttons/button_generic_240x64.png",
                PackageXmlTemplate = packageTemplate,
                ButtonComponentXmlTemplate = buttonTemplate,
                PanelComponentXmlTemplate = panelTemplate,
                BlueprintHash = state.BlueprintHash,
                CatalogHash = state.CatalogHash,
                RecipeCatalogHash = state.RecipeCatalogHash,
                NodeMappings = nodeMappings.ToArray()
            };
        }

        private static string BuildPackageXml(UiArtRect scale9)
        {
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
                + "<packageDescription id=\"{{PACKAGE_DESCRIPTION_ID}}\">\n"
                + "  <resources>\n"
                + "    <component id=\"{{PANEL_COMPONENT_ID}}\" name=\"UiFoundationDemoPanel.xml\" path=\"/\" exported=\"true\"/>\n"
                + "    <component id=\"{{BUTTON_COMPONENT_ID}}\" name=\"GenericButton.xml\" path=\"/Components/\"/>\n"
                + "    <image id=\"{{IMAGE_RESOURCE_ID}}\" name=\"button_generic_240x64.png\" path=\"/Images/MxUiFoundation/buttons/\" exported=\"true\" scale=\"9grid\" scale9grid=\""
                + scale9.X.ToString(CultureInfo.InvariantCulture) + ","
                + scale9.Y.ToString(CultureInfo.InvariantCulture) + ","
                + scale9.Width.ToString(CultureInfo.InvariantCulture) + ","
                + scale9.Height.ToString(CultureInfo.InvariantCulture) + "\"/>\n"
                + "  </resources>\n"
                + "  <publish name=\"\"/>\n"
                + "</packageDescription>\n";
        }

        private static string BuildButtonXml(UiComponentRecipeCatalog recipes)
        {
            UiComponentRecipe recipe = recipes.Recipes.First(item => string.Equals(item.ComponentId, "ui.component.button.generic", StringComparison.Ordinal));
            string over = EscapeXml(recipe.StatePresentation?.OverOverlayColor ?? "#1838b7a5");
            string down = EscapeXml(recipe.StatePresentation?.DownOverlayColor ?? "#5210181d");
            string font = recipe.StatePresentation?.TitleFont ?? string.Empty;
            string typography = (string.IsNullOrEmpty(font) ? string.Empty : " font=\"" + EscapeXml(font) + "\"")
                + (recipe.StatePresentation?.TitleBold != false ? " bold=\"true\"" : string.Empty);
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
                + "<component size=\"240,64\" extention=\"Button\" initName=\"btn\">\n"
                + "  <controller name=\"button\" pages=\"0,up,1,down,2,over,3,selectedOver\" selected=\"0\"/>\n"
                + "  <displayList>\n"
                + "    <image id=\"n0_mxucb\" name=\"base\" src=\"{{IMAGE_RESOURCE_ID}}\" fileName=\"Images/MxUiFoundation/buttons/button_generic_240x64.png\" xy=\"0,0\" size=\"240,64\">\n"
                + "      <relation target=\"\" sidePair=\"width-width,height-height\"/>\n"
                + "    </image>\n"
                + "    <graph id=\"n1_mxucb\" name=\"overOverlay\" xy=\"0,0\" size=\"240,64\" type=\"rect\" fillColor=\"" + over + "\" corner=\"6,6,6,6\">\n"
                + "      <gearDisplay controller=\"button\" pages=\"2\"/>\n"
                + "      <relation target=\"\" sidePair=\"width-width,height-height\"/>\n"
                + "    </graph>\n"
                + "    <graph id=\"n2_mxucb\" name=\"downOverlay\" xy=\"0,0\" size=\"240,64\" type=\"rect\" fillColor=\"" + down + "\" corner=\"6,6,6,6\">\n"
                + "      <gearDisplay controller=\"button\" pages=\"1,3\"/>\n"
                + "      <relation target=\"\" sidePair=\"width-width,height-height\"/>\n"
                + "    </graph>\n"
                + "    <text id=\"n3_mxucb\" name=\"title\" xy=\"12,0\" size=\"216,64\" fontSize=\"19\" color=\"#f3f5f4\" align=\"center\" vAlign=\"middle\" autoSize=\"none\" singleLine=\"true\"" + typography + " autoClearText=\"true\" text=\"Button\">\n"
                + "      <relation target=\"\" sidePair=\"width-width,height-height\"/>\n"
                + "    </text>\n"
                + "  </displayList>\n"
                + "  <Button/>\n"
                + "</component>\n";
        }

        private static string BuildPanelXml(UiScreenBlueprint blueprint, List<UiComposerNodeMapping> mappings)
        {
            var builder = new StringBuilder(4096);
            builder.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            builder.Append("<component size=\"").Append(blueprint.DesignResolution.Width).Append(',').Append(blueprint.DesignResolution.Height).Append("\" opaque=\"false\">\n");
            builder.Append("  <displayList>\n");
            UiScreenNode[] nodes = blueprint.Nodes.OrderBy(node => node.Order).ThenBy(node => node.NodeId, StringComparer.Ordinal).ToArray();
            for (int i = 0; i < nodes.Length; i++)
            {
                UiScreenNode node = nodes[i];
                string fguiNodeId = "n" + i.ToString(CultureInfo.InvariantCulture) + "_mxuc";
                mappings.Add(new UiComposerNodeMapping(node.NodeId, fguiNodeId, node.Name));
                AppendNodeXml(builder, node, fguiNodeId);
            }
            builder.Append("  </displayList>\n");
            builder.Append("</component>\n");
            return builder.ToString();
        }

        private static void AppendNodeXml(StringBuilder builder, UiScreenNode node, string fguiNodeId)
        {
            UiNodeLayout layout = node.Layout ?? new UiNodeLayout();
            string common = " id=\"" + EscapeXml(fguiNodeId) + "\" name=\"" + EscapeXml(node.Name) + "\" xy=\""
                + layout.X + "," + layout.Y + "\" size=\"" + layout.Width + "," + layout.Height + "\"";
            if (string.Equals(node.Type, "Graph", StringComparison.Ordinal))
            {
                UiGraphNodeStyle style = node.Graph ?? new UiGraphNodeStyle();
                builder.Append("    <graph").Append(common).Append(" type=\"rect\" fillColor=\"").Append(EscapeXml(style.FillColor)).Append("\"");
                if (style.CornerRadius > 0)
                {
                    builder.Append(" corner=\"").Append(style.CornerRadius).Append(',').Append(style.CornerRadius).Append(',').Append(style.CornerRadius).Append(',').Append(style.CornerRadius).Append("\"");
                }
                AppendNodeCloseAndRelation(builder, layout, "graph");
                return;
            }

            if (string.Equals(node.Type, "Text", StringComparison.Ordinal))
            {
                UiTextNodeStyle style = node.Text ?? new UiTextNodeStyle();
                builder.Append("    <text").Append(common)
                    .Append(" fontSize=\"").Append(style.FontSize).Append("\" color=\"").Append(EscapeXml(style.Color))
                    .Append("\" align=\"").Append(EscapeXml(style.Align)).Append("\" vAlign=\"middle\" autoSize=\"none\"");
                if (!string.IsNullOrEmpty(style.Font)) builder.Append(" font=\"").Append(EscapeXml(style.Font)).Append("\"");
                if (style.SingleLine)
                    builder.Append(" singleLine=\"true\"");
                if (style.Bold)
                    builder.Append(" bold=\"true\"");
                builder.Append(" text=\"").Append(EscapeXml(style.Value)).Append("\"");
                AppendNodeCloseAndRelation(builder, layout, "text");
                return;
            }

            UiComponentInstanceNode component = node.Component ?? new UiComponentInstanceNode();
            builder.Append("    <component").Append(common)
                .Append(" src=\"{{BUTTON_COMPONENT_ID}}\" fileName=\"Components/GenericButton.xml\">\n")
                .Append("      <Button title=\"").Append(EscapeXml(component.Title)).Append("\"/>\n");
            AppendRelation(builder, layout, "      ");
            builder.Append("    </component>\n");
        }

        private static void AppendNodeCloseAndRelation(StringBuilder builder, UiNodeLayout layout, string elementName)
        {
            string relation = BuildRelation(layout);
            if (string.IsNullOrEmpty(relation))
            {
                builder.Append("/>\n");
                return;
            }

            builder.Append(">\n");
            builder.Append("      <relation target=\"\" sidePair=\"").Append(relation).Append("\"/>\n");
            builder.Append("    </").Append(elementName).Append(">\n");
        }

        private static void AppendRelation(StringBuilder builder, UiNodeLayout layout, string indent)
        {
            string relation = BuildRelation(layout);
            if (!string.IsNullOrEmpty(relation))
                builder.Append(indent).Append("<relation target=\"\" sidePair=\"").Append(relation).Append("\"/>\n");
        }

        private static string BuildRelation(UiNodeLayout layout)
        {
            var parts = new List<string>(2);
            switch (layout.HorizontalAnchor)
            {
                case "Center": parts.Add("center-center"); break;
                case "Right": parts.Add("right-right"); break;
                case "Stretch": parts.Add("width-width"); break;
            }
            switch (layout.VerticalAnchor)
            {
                case "Middle": parts.Add("middle-middle"); break;
                case "Bottom": parts.Add("bottom-bottom"); break;
                case "Stretch": parts.Add("height-height"); break;
            }
            return string.Join(",", parts);
        }

        private static UiScreenBlueprint ParseSupportedComponentXml(
            string repositoryRoot,
            UiComposerAuthoringState state,
            List<UiComposerDiagnostic> diagnostics)
        {
            string path = ResolvePath(repositoryRoot, TargetComponentPath);
            XDocument document;
            try
            {
                document = XDocument.Load(path, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            }
            catch (Exception ex) when (ex is IOException || ex is System.Xml.XmlException)
            {
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP009", "Unable to read FairyGUI component XML: " + ex.Message, TargetComponentPath));
                return null;
            }

            XElement root = document.Root;
            if (root == null || root.Name.LocalName != "component" || root.Elements().Any(item => item.Name.LocalName != "displayList"))
            {
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP009", "Only a component with one displayList is supported for round-trip.", TargetComponentPath));
                return null;
            }

            XElement displayList = root.Element("displayList");
            if (displayList == null)
            {
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP009", "FairyGUI component has no displayList.", TargetComponentPath));
                return null;
            }

            var nodes = new List<UiScreenNode>();
            var existingByName = new Dictionary<string, UiScreenNode>(StringComparer.Ordinal);
            for (int i = 0; i < state.Blueprint.Nodes.Length; i++)
                existingByName[state.Blueprint.Nodes[i].Name] = state.Blueprint.Nodes[i];

            int order = 0;
            foreach (XElement element in displayList.Elements())
            {
                string kind = element.Name.LocalName;
                if (kind != "graph" && kind != "text" && kind != "component")
                {
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP009", "Unsupported FairyGUI node type: " + kind + ".", TargetComponentPath));
                    continue;
                }

                if (!ValidateSupportedElementShape(element, kind, diagnostics))
                    continue;

                string name = Attribute(element, "name");
                UiScreenNode previous = existingByName.TryGetValue(name, out UiScreenNode value) ? value : null;
                var node = new UiScreenNode
                {
                    NodeId = previous?.NodeId ?? name,
                    Name = name,
                    Type = kind == "graph" ? "Graph" : kind == "text" ? "Text" : "ComponentInstance",
                    Order = order++,
                    BindingSlot = FindBindingSlotByControlName(name),
                    Layout = ParseLayout(element)
                };
                if (kind == "graph")
                {
                    node.Graph = new UiGraphNodeStyle
                    {
                        FillColor = Attribute(element, "fillColor"),
                        CornerRadius = ParseFirstInt(Attribute(element, "corner"))
                    };
                }
                else if (kind == "text")
                {
                    node.Text = new UiTextNodeStyle
                    {
                        Value = Attribute(element, "text"),
                        Font = Attribute(element, "font"), FontSize = ParseInt(Attribute(element, "fontSize"), 16),
                        Color = Attribute(element, "color"),
                        Align = DefaultIfEmpty(Attribute(element, "align"), "left"),
                        Bold = ParseBool(Attribute(element, "bold")),
                        SingleLine = ParseBool(Attribute(element, "singleLine"))
                    };
                }
                else
                {
                    XElement button = element.Element("Button");
                    node.Component = new UiComponentInstanceNode
                    {
                        ComponentRecipeId = "ui.component.button.generic",
                        Title = button != null ? Attribute(button, "title") : string.Empty
                    };
                }
                nodes.Add(node);
            }

            if (HasErrors(diagnostics))
                return null;

            UiSize design = ParseSize(Attribute(root, "size"));
            return new UiScreenBlueprint
            {
                SchemaVersion = state.Blueprint.SchemaVersion,
                ScreenId = state.Blueprint.ScreenId,
                DisplayName = state.Blueprint.DisplayName,
                LibraryId = state.Blueprint.LibraryId,
                DesignResolution = design,
                Target = state.Blueprint.Target,
                Nodes = nodes.ToArray()
            };
        }

        private static bool ValidateSupportedElementShape(XElement element, string kind, List<UiComposerDiagnostic> diagnostics)
        {
            var allowed = new HashSet<string>(StringComparer.Ordinal) { "id", "name", "xy", "size" };
            if (kind == "graph")
            {
                allowed.UnionWith(new[] { "type", "fillColor", "corner" });
            }
            else if (kind == "text")
            {
                allowed.UnionWith(new[] { "font", "fontSize", "color", "align", "vAlign", "autoSize", "singleLine", "bold", "text" });
            }
            else
            {
                allowed.UnionWith(new[] { "src", "fileName" });
                if (!string.Equals(Attribute(element, "fileName"), "Components/GenericButton.xml", StringComparison.Ordinal))
                {
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP009", "Only GenericButton component instances can round-trip in v1.", TargetComponentPath));
                    return false;
                }
            }

            foreach (XAttribute attribute in element.Attributes())
            {
                if (!allowed.Contains(attribute.Name.LocalName))
                {
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP009", "Unsupported FairyGUI attribute: " + attribute.Name.LocalName + ".", TargetComponentPath));
                    return false;
                }
            }

            foreach (XElement child in element.Elements())
            {
                if (child.Name.LocalName != "relation" && !(kind == "component" && child.Name.LocalName == "Button"))
                {
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP009", "Unsupported FairyGUI child element: " + child.Name.LocalName + ".", TargetComponentPath));
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
                X = position.Width,
                Y = position.Height,
                Width = size.Width,
                Height = size.Height,
                HorizontalAnchor = relation.Contains("width-width", StringComparison.Ordinal) ? "Stretch"
                    : relation.Contains("center-center", StringComparison.Ordinal) ? "Center"
                    : relation.Contains("right-right", StringComparison.Ordinal) ? "Right" : "Left",
                VerticalAnchor = relation.Contains("height-height", StringComparison.Ordinal) ? "Stretch"
                    : relation.Contains("middle-middle", StringComparison.Ordinal) ? "Middle"
                    : relation.Contains("bottom-bottom", StringComparison.Ordinal) ? "Bottom" : "Top"
            };
        }

        private static void BuildDiff(UiScreenBlueprint current, UiScreenBlueprint proposed, List<UiComposerDiffEntry> diffs)
        {
            var currentByName = current.Nodes.ToDictionary(node => node.Name, StringComparer.Ordinal);
            for (int i = 0; i < proposed.Nodes.Length; i++)
            {
                UiScreenNode node = proposed.Nodes[i];
                if (!currentByName.TryGetValue(node.Name, out UiScreenNode old))
                {
                    diffs.Add(new UiComposerDiffEntry(node.NodeId, "Added", string.Empty, node.Name));
                    continue;
                }
                string before = NodeSummary(old);
                string after = NodeSummary(node);
                if (!string.Equals(before, after, StringComparison.Ordinal))
                    diffs.Add(new UiComposerDiffEntry(node.NodeId, "Changed", before, after));
                currentByName.Remove(node.Name);
            }
            foreach (UiScreenNode removed in currentByName.Values.OrderBy(node => node.NodeId, StringComparer.Ordinal))
                diffs.Add(new UiComposerDiffEntry(removed.NodeId, "Removed", removed.Name, string.Empty));
        }

        private static string NodeSummary(UiScreenNode node)
        {
            UiNodeLayout layout = node.Layout ?? new UiNodeLayout();
            string value = node.Text?.Value ?? node.Component?.Title ?? node.Graph?.FillColor ?? string.Empty;
            return node.Name + "|" + node.Type + "|" + node.Order + "|"
                + layout.X + "," + layout.Y + "," + layout.Width + "," + layout.Height + "|"
                + layout.HorizontalAnchor + "," + layout.VerticalAnchor + "|" + value;
        }

        private static IReadOnlyList<UiComposerDiagnostic> Validate(
            string repositoryRoot,
            string blueprintPath,
            UiScreenBlueprint blueprint,
            UiComponentRecipeCatalog recipes,
            UiArtResourceLibraryAuthoringState library,
            UiComposerPackageState packageState,
            string sourceXmlHash,
            bool validateSourceDrift)
        {
            var diagnostics = new List<UiComposerDiagnostic>();
            if (blueprint == null)
            {
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP001", "Screen blueprint is required.", blueprintPath));
                return diagnostics;
            }
            if (!string.Equals(blueprint.SchemaVersion, BlueprintSchema, StringComparison.Ordinal))
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP001", "Unsupported screen blueprint schemaVersion.", blueprintPath));
            if (!string.Equals(blueprint.ScreenId, DefaultScreenId, StringComparison.Ordinal) || !IsSupportedTarget(blueprint.Target))
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP002", "V1 only supports the MxUiFoundationDemo sample screen.", blueprintPath));
            if (blueprint.DesignResolution == null || blueprint.DesignResolution.Width != 1920 || blueprint.DesignResolution.Height != 1080)
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP007", "V1 designResolution must be 1920x1080.", blueprintPath));
            if (recipes == null || !string.Equals(recipes.SchemaVersion, RecipeSchema, StringComparison.Ordinal))
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP001", "Unsupported component recipe catalog schemaVersion.", DefaultRecipeCatalogPath));

            var resourceIds = new HashSet<string>(StringComparer.Ordinal);
            UiArtResource[] resources = library?.Catalog?.Resources ?? Array.Empty<UiArtResource>();
            for (int i = 0; i < resources.Length; i++)
                resourceIds.Add(resources[i].ResourceId);
            var recipeIds = new HashSet<string>(StringComparer.Ordinal);
            UiComponentRecipe[] recipeValues = recipes?.Recipes ?? Array.Empty<UiComponentRecipe>();
            for (int i = 0; i < recipeValues.Length; i++)
            {
                UiComponentRecipe recipe = recipeValues[i];
                if (!recipeIds.Add(recipe.ComponentId))
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Duplicate component recipe id: " + recipe.ComponentId + ".", DefaultRecipeCatalogPath));
                UiComponentResourceSlot[] slots = recipe.ResourceSlots ?? Array.Empty<UiComponentResourceSlot>();
                for (int j = 0; j < slots.Length; j++)
                {
                    if (!resourceIds.Contains(slots[j].ResourceId))
                        diagnostics.Add(UiComposerDiagnostic.Error("UICOMP005", "Component recipe references an unknown resource: " + slots[j].ResourceId + ".", DefaultRecipeCatalogPath));
                }
            }

            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            var nodeNames = new HashSet<string>(StringComparer.Ordinal);
            var assignedBindings = new HashSet<string>(StringComparer.Ordinal);
            UiScreenNode[] nodes = blueprint.Nodes ?? Array.Empty<UiScreenNode>();
            for (int i = 0; i < nodes.Length; i++)
            {
                UiScreenNode node = nodes[i];
                string nodePath = blueprintPath + "#" + (node.NodeId ?? string.Empty);
                if (string.IsNullOrWhiteSpace(node.NodeId) || !nodeIds.Add(node.NodeId))
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP003", "Node id is empty or duplicated.", nodePath));
                if (string.IsNullOrWhiteSpace(node.Name) || !nodeNames.Add(node.Name))
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP003", "Node name is empty or duplicated.", nodePath));
                if (node.Type != "Graph" && node.Type != "Text" && node.Type != "ComponentInstance")
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP003", "Unsupported node type: " + node.Type + ".", nodePath));
                if (node.Layout == null || node.Layout.Width <= 0 || node.Layout.Height <= 0)
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP007", "Node size must be positive.", nodePath));
                else if (!IsAnchor(node.Layout.HorizontalAnchor, true) || !IsAnchor(node.Layout.VerticalAnchor, false))
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP007", "Node anchor is unsupported.", nodePath));
                if (node.Type == "Graph" && node.Graph == null)
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP003", "Graph node style is required.", nodePath));
                if (node.Type == "Text" && node.Text == null)
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP003", "Text node style is required.", nodePath));
                if (node.Type == "ComponentInstance")
                {
                    if (node.Component == null || !recipeIds.Contains(node.Component.ComponentRecipeId))
                        diagnostics.Add(UiComposerDiagnostic.Error("UICOMP006", "Component instance references an unknown recipe.", nodePath));
                }
                if (!string.IsNullOrWhiteSpace(node.BindingSlot))
                {
                    UiComposerBindingSlot binding = FindBindingSlot(node.BindingSlot);
                    if (binding == null)
                    {
                        diagnostics.Add(UiComposerDiagnostic.Error("UICOMP004", "Unknown binding slot: " + node.BindingSlot + ".", nodePath));
                    }
                    else
                    {
                        if (!assignedBindings.Add(node.BindingSlot))
                            diagnostics.Add(UiComposerDiagnostic.Error("UICOMP004", "Binding slot is assigned more than once: " + node.BindingSlot + ".", nodePath));
                        if (!string.Equals(binding.ControlName, node.Name, StringComparison.Ordinal) || !string.Equals(binding.NodeType, node.Type, StringComparison.Ordinal))
                            diagnostics.Add(UiComposerDiagnostic.Error("UICOMP004", "Bound node name or type does not match the C# contract.", nodePath));
                    }
                }
            }
            for (int i = 0; i < BindingSlots.Length; i++)
            {
                if (BindingSlots[i].Required && !assignedBindings.Contains(BindingSlots[i].SlotId))
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP004", "Required binding slot is unassigned: " + BindingSlots[i].SlotId + ".", blueprintPath));
            }

            if (packageState != null)
            {
                if (!string.Equals(packageState.Owner, StateOwner, StringComparison.Ordinal))
                    diagnostics.Add(UiComposerDiagnostic.Error("UICOMP010", "Package state owner is invalid.", TargetPackageStatePath));
                if (validateSourceDrift && !string.Equals(packageState.SourceXmlHash, sourceXmlHash, StringComparison.Ordinal))
                    diagnostics.Add(UiComposerDiagnostic.Warning("UICOMP108", "FairyGUI source differs from the last synchronized source. Sync before applying or publishing.", TargetComponentPath));
            }
            else if (File.Exists(ResolvePath(repositoryRoot, TargetPackageSourcePath + "/package.xml")))
            {
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP010", "Target package exists without MxUiComposer ownership state.", TargetPackageStatePath));
            }

            SortDiagnostics(diagnostics);
            return diagnostics;
        }

        private static void ValidateAllHashes(
            UiComposerAuthoringState state,
            UiComposerExpectedHashes expected,
            List<UiComposerDiagnostic> diagnostics,
            bool requireSourceAndMapping)
        {
            ValidateExpectedHash(expected?.BlueprintHash, state.BlueprintHash, "UICOMP008", "Blueprint hash is stale.", state.Screen.BlueprintPath, diagnostics);
            ValidateExpectedHash(expected?.RecipeCatalogHash, state.RecipeCatalogHash, "UICOMP008", "Component recipe catalog hash is stale.", DefaultRecipeCatalogPath, diagnostics);
            ValidateExpectedHash(expected?.CatalogHash, state.CatalogHash, "UICOMP008", "UI art resource catalog hash is stale.", UiArtResourceLibraryService.DefaultCatalogPath, diagnostics);
            if (requireSourceAndMapping)
            {
                ValidateExpectedHash(expected?.SourceXmlHash, state.SourceXmlHash, "UICOMP008", "FairyGUI source XML hash is stale.", TargetComponentPath, diagnostics);
                ValidateExpectedHash(expected?.MappingHash, state.MappingHash, "UICOMP008", "Composer mapping hash is stale.", TargetPackageStatePath, diagnostics);
            }
        }

        private static void ValidateExpectedHash(string expected, string current, string code, string message, string path, List<UiComposerDiagnostic> diagnostics)
        {
            if (!string.Equals(expected ?? string.Empty, current ?? string.Empty, StringComparison.Ordinal))
                diagnostics.Add(UiComposerDiagnostic.Error(code, message, path));
        }

        private static string ResolveBlueprintPath(string repositoryRoot, string screen)
        {
            string value = NormalizePath(screen);
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, DefaultScreenId, StringComparison.Ordinal) || string.Equals(value, DefaultBlueprintPath, StringComparison.Ordinal))
                return DefaultBlueprintPath;
            IReadOnlyList<UiComposerScreenDescriptor> screens = ListScreens(repositoryRoot);
            for (int i = 0; i < screens.Count; i++)
            {
                if (string.Equals(screens[i].ScreenId, value, StringComparison.Ordinal) || string.Equals(screens[i].BlueprintPath, value, StringComparison.Ordinal))
                    return screens[i].BlueprintPath;
            }
            throw new InvalidOperationException("Unknown UI Composer screen: " + screen + ".");
        }

        private static bool IsSupportedTarget(UiScreenTarget target)
        {
            return target != null
                && string.Equals(target.PackageId, TargetPackageId, StringComparison.Ordinal)
                && string.Equals(target.ComponentName, TargetComponentName, StringComparison.Ordinal)
                && string.Equals(NormalizePath(target.PackageSourcePath), TargetPackageSourcePath, StringComparison.Ordinal);
        }

        private static UiArtResource FindResource(UiArtResourceLibraryCatalog catalog, string resourceId)
        {
            UiArtResource[] resources = catalog?.Resources ?? Array.Empty<UiArtResource>();
            for (int i = 0; i < resources.Length; i++)
            {
                if (string.Equals(resources[i].ResourceId, resourceId, StringComparison.Ordinal))
                    return resources[i];
            }
            throw new InvalidOperationException("UI Composer resource is missing: " + resourceId + ".");
        }

        private static UiComposerBindingSlot FindBindingSlot(string slotId)
        {
            for (int i = 0; i < BindingSlots.Length; i++)
            {
                if (string.Equals(BindingSlots[i].SlotId, slotId, StringComparison.Ordinal))
                    return BindingSlots[i];
            }
            return null;
        }

        private static string FindBindingSlotByControlName(string controlName)
        {
            for (int i = 0; i < BindingSlots.Length; i++)
            {
                if (string.Equals(BindingSlots[i].ControlName, controlName, StringComparison.Ordinal))
                    return BindingSlots[i].SlotId;
            }
            return string.Empty;
        }

        private static bool IsAnchor(string value, bool horizontal)
        {
            if (horizontal)
                return value == "Left" || value == "Center" || value == "Right" || value == "Stretch";
            return value == "Top" || value == "Middle" || value == "Bottom" || value == "Stretch";
        }

        private static bool HasErrors(IReadOnlyList<UiComposerDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Blocking || string.Equals(diagnostics[i].Severity, "error", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void SortDiagnostics(List<UiComposerDiagnostic> diagnostics)
        {
            diagnostics.Sort((left, right) =>
            {
                int code = string.CompareOrdinal(left.Code, right.Code);
                if (code != 0) return code;
                int path = string.CompareOrdinal(left.Path, right.Path);
                if (path != 0) return path;
                return string.CompareOrdinal(left.NodeId, right.NodeId);
            });
        }

        private static T ReadJson<T>(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Required UI Composer file is missing.", path);
            T value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
            if (value == null)
                throw new InvalidOperationException("UI Composer JSON is empty: " + path + ".");
            return value;
        }

        private static T TryReadJson<T>(string path) where T : class
        {
            if (!File.Exists(path))
                return null;
            try { return ReadJson<T>(path); }
            catch (JsonException) { return null; }
        }

        private static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine;
        }

        private static string ComputeFileHash(string path)
        {
            if (!File.Exists(path))
                return string.Empty;
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(File.ReadAllBytes(path));
            var builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static void WriteTextAtomic(string path, string text)
        {
            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            Directory.CreateDirectory(directory);
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }

        private static string ResolvePath(string repositoryRoot, string relativePath)
        {
            EnsureRepositoryRoot(repositoryRoot);
            string root = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, NormalizePath(relativePath).Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("UI Composer path escapes the repository root: " + relativePath + ".");
            return path;
        }

        private static string ToRelativePath(string repositoryRoot, string path)
        {
            string root = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Path is outside repository root.");
            return NormalizePath(full.Substring(root.Length));
        }

        private static void EnsureRepositoryRoot(string repositoryRoot)
        {
            if (string.IsNullOrWhiteSpace(repositoryRoot))
                throw new ArgumentException("Repository root is required.", nameof(repositoryRoot));
        }

        private static string EnsureOperationId(string value)
        {
            string id = value ?? string.Empty;
            if (id.Length == 0 || id.Length > 64 || id.Any(ch => !char.IsLetterOrDigit(ch) && ch != '-'))
                throw new InvalidOperationException("Invalid UI Composer operation id.");
            return id;
        }

        private static string NormalizePath(string value) => (value ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/');
        private static string EscapeXml(string value) => System.Security.SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
        private static string Attribute(XElement element, string name) => element?.Attribute(name)?.Value ?? string.Empty;
        private static string DefaultIfEmpty(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;
        private static bool ParseBool(string value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        private static int ParseInt(string value, int fallback) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : fallback;
        private static int ParseFirstInt(string value) => ParseInt((value ?? string.Empty).Split(',')[0], 0);
        private static UiSize ParseSize(string value)
        {
            string[] parts = (value ?? string.Empty).Split(',');
            return new UiSize
            {
                Width = parts.Length > 0 ? ParseInt(parts[0], 0) : 0,
                Height = parts.Length > 1 ? ParseInt(parts[1], 0) : 0
            };
        }
    }

    public sealed class UiComponentRecipeCatalog
    {
        [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; set; } = string.Empty;
        [JsonPropertyName("libraryId")] public string LibraryId { get; set; } = string.Empty;
        [JsonPropertyName("recipes")] public UiComponentRecipe[] Recipes { get; set; } = Array.Empty<UiComponentRecipe>();
    }

    public sealed class UiComponentRecipe
    {
        [JsonPropertyName("componentId")] public string ComponentId { get; set; } = string.Empty;
        [JsonPropertyName("displayName")] public string DisplayName { get; set; } = string.Empty;
        [JsonPropertyName("componentType")] public string ComponentType { get; set; } = string.Empty;
        [JsonPropertyName("fairyGuiComponentName")] public string FairyGuiComponentName { get; set; } = string.Empty;
        [JsonPropertyName("defaultSize")] public UiSize DefaultSize { get; set; } = new UiSize();
        [JsonPropertyName("resourceSlots")] public UiComponentResourceSlot[] ResourceSlots { get; set; } = Array.Empty<UiComponentResourceSlot>();
        [JsonPropertyName("instanceProperties")] public string[] InstanceProperties { get; set; } = Array.Empty<string>();
        [JsonPropertyName("statePresentation")] public UiButtonStatePresentation StatePresentation { get; set; }
        [JsonPropertyName("template")] public UiComponentTemplate Template { get; set; }
    }

    /// <summary>Authoring-only child tree for a controlled reusable Composite recipe.</summary>
    /// <remarks>LowFreqAlloc. The template is materialized into a flat FairyGUI component XML document.</remarks>
    public sealed class UiComponentTemplate
    {
        [JsonPropertyName("nodes")] public UiScreenNode[] Nodes { get; set; } = Array.Empty<UiScreenNode>();
    }

    public sealed class UiComponentResourceSlot
    {
        [JsonPropertyName("slotId")] public string SlotId { get; set; } = string.Empty;
        [JsonPropertyName("resourceId")] public string ResourceId { get; set; } = string.Empty;
    }

    public sealed class UiButtonStatePresentation
    {
        [JsonPropertyName("controllerName")] public string ControllerName { get; set; } = string.Empty;
        [JsonPropertyName("pages")] public string[] Pages { get; set; } = Array.Empty<string>();
        [JsonPropertyName("overOverlayColor")] public string OverOverlayColor { get; set; } = string.Empty;
        [JsonPropertyName("downOverlayColor")] public string DownOverlayColor { get; set; } = string.Empty;
        [JsonPropertyName("titleColor")] public string TitleColor { get; set; } = "#f3f5f4";
        [JsonPropertyName("titleFontSize")] public int TitleFontSize { get; set; } = 22;
        [JsonPropertyName("titleFont")] public string TitleFont { get; set; } = string.Empty;
        [JsonPropertyName("titleBold")] public bool TitleBold { get; set; } = true;
        [JsonPropertyName("iconColor")] public string IconColor { get; set; } = "#ffffffff";
        [JsonPropertyName("iconOnly")] public bool IconOnly { get; set; }
        [JsonPropertyName("iconSize")] public int IconSize { get; set; }
    }

    public sealed class UiScreenBlueprint
    {
        [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; set; } = string.Empty;
        [JsonPropertyName("screenId")] public string ScreenId { get; set; } = string.Empty;
        [JsonPropertyName("displayName")] public string DisplayName { get; set; } = string.Empty;
        [JsonPropertyName("libraryId")] public string LibraryId { get; set; } = string.Empty;
        [JsonPropertyName("designResolution")] public UiSize DesignResolution { get; set; } = new UiSize();
        [JsonPropertyName("target")] public UiScreenTarget Target { get; set; } = new UiScreenTarget();
        [JsonPropertyName("containers")] public UiLayoutContainer[] Containers { get; set; } = Array.Empty<UiLayoutContainer>();
        [JsonPropertyName("nodes")] public UiScreenNode[] Nodes { get; set; } = Array.Empty<UiScreenNode>();
    }

    public sealed class UiScreenTarget
    {
        [JsonPropertyName("packageId")] public string PackageId { get; set; } = string.Empty;
        [JsonPropertyName("componentName")] public string ComponentName { get; set; } = string.Empty;
        [JsonPropertyName("packageSourcePath")] public string PackageSourcePath { get; set; } = string.Empty;
    }

    public sealed class UiScreenNode
    {
        [JsonPropertyName("nodeId")] public string NodeId { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("order")] public int Order { get; set; }
        [JsonPropertyName("bindingSlot")] public string BindingSlot { get; set; } = string.Empty;
        [JsonPropertyName("parentContainerId")] public string ParentContainerId { get; set; } = string.Empty;
        [JsonPropertyName("visible")] public bool? Visible { get; set; }
        [JsonPropertyName("layout")] public UiNodeLayout Layout { get; set; } = new UiNodeLayout();
        [JsonPropertyName("graph")] public UiGraphNodeStyle Graph { get; set; }
        [JsonPropertyName("text")] public UiTextNodeStyle Text { get; set; }
        [JsonPropertyName("image")] public UiImageNodeStyle Image { get; set; }
        [JsonPropertyName("component")] public UiComponentInstanceNode Component { get; set; }
        [JsonPropertyName("list")] public UiListNodeStyle List { get; set; }
    }

    public sealed class UiNodeLayout
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("horizontalAnchor")] public string HorizontalAnchor { get; set; } = "Left";
        [JsonPropertyName("verticalAnchor")] public string VerticalAnchor { get; set; } = "Top";
    }

    /// <summary>Authoring-only group or automatic layout container.</summary>
    /// <remarks>LowFreqAlloc. Containers are flattened before FairyGUI materialization.</remarks>
    public sealed class UiLayoutContainer
    {
        [JsonPropertyName("containerId")] public string ContainerId { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("parentContainerId")] public string ParentContainerId { get; set; } = string.Empty;
        [JsonPropertyName("order")] public int Order { get; set; }
        [JsonPropertyName("mode")] public string Mode { get; set; } = "Free";
        [JsonPropertyName("layout")] public UiNodeLayout Layout { get; set; } = new UiNodeLayout();
        [JsonPropertyName("padding")] public UiContainerPadding Padding { get; set; } = new UiContainerPadding();
        [JsonPropertyName("gapX")] public int GapX { get; set; }
        [JsonPropertyName("gapY")] public int GapY { get; set; }
        [JsonPropertyName("columns")] public int Columns { get; set; } = 1;
        [JsonPropertyName("mainAxisAlign")] public string MainAxisAlign { get; set; } = "Start";
        [JsonPropertyName("crossAxisAlign")] public string CrossAxisAlign { get; set; } = "Start";
    }

    public sealed class UiContainerPadding
    {
        [JsonPropertyName("left")] public int Left { get; set; }
        [JsonPropertyName("top")] public int Top { get; set; }
        [JsonPropertyName("right")] public int Right { get; set; }
        [JsonPropertyName("bottom")] public int Bottom { get; set; }
    }

    public sealed class UiGraphNodeStyle
    {
        [JsonPropertyName("fillColor")] public string FillColor { get; set; } = "#ffffffff";
        [JsonPropertyName("cornerRadius")] public int CornerRadius { get; set; }
    }

    public sealed class UiTextNodeStyle
    {
        [JsonPropertyName("value")] public string Value { get; set; } = string.Empty;
        [JsonPropertyName("font")] public string Font { get; set; } = string.Empty;
        [JsonPropertyName("fontSize")] public int FontSize { get; set; } = 16;
        [JsonPropertyName("color")] public string Color { get; set; } = "#ffffffff";
        [JsonPropertyName("align")] public string Align { get; set; } = "left";
        [JsonPropertyName("bold")] public bool Bold { get; set; }
        [JsonPropertyName("singleLine")] public bool SingleLine { get; set; }
        [JsonPropertyName("input")] public bool Input { get; set; }
        [JsonPropertyName("restrict")] public string Restrict { get; set; } = string.Empty;
        [JsonPropertyName("maxLength")] public int MaxLength { get; set; }
    }

    public sealed class UiImageNodeStyle
    {
        [JsonPropertyName("resourceId")] public string ResourceId { get; set; } = string.Empty;
        [JsonPropertyName("tint")] public string Tint { get; set; } = "#ffffffff";
    }

    public sealed class UiComponentInstanceNode
    {
        [JsonPropertyName("componentRecipeId")] public string ComponentRecipeId { get; set; } = string.Empty;
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("iconResourceId")] public string IconResourceId { get; set; } = string.Empty;
    }

    /// <summary>Controlled FairyGUI GList authoring properties for a screen node.</summary>
    /// <remarks>Virtualization is runtime intent; FairyGUI source XML stores the list geometry and default item, while a binder must call GList.SetVirtual.</remarks>
    public sealed class UiListNodeStyle
    {
        [JsonPropertyName("itemComponentRecipeId")] public string ItemComponentRecipeId { get; set; } = string.Empty;
        [JsonPropertyName("layout")] public string Layout { get; set; } = "Column";
        [JsonPropertyName("lineGap")] public int LineGap { get; set; }
        [JsonPropertyName("columnGap")] public int ColumnGap { get; set; }
        [JsonPropertyName("selectionMode")] public string SelectionMode { get; set; } = "None";
        [JsonPropertyName("overflow")] public string Overflow { get; set; } = "Scroll";
        [JsonPropertyName("virtualized")] public bool Virtualized { get; set; }
    }

    public sealed class UiSize
    {
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
    }

    public sealed class UiComposerBindingSlot
    {
        public UiComposerBindingSlot()
        {
        }

        public UiComposerBindingSlot(string slotId, string controlName, string nodeType, bool required, string commandId, string bindPath = "")
        {
            SlotId = slotId; ControlName = controlName; NodeType = nodeType; Required = required; CommandId = commandId; BindPath = bindPath;
        }
        [JsonPropertyName("slotId")] public string SlotId { get; set; } = string.Empty;
        [JsonPropertyName("controlName")] public string ControlName { get; set; } = string.Empty;
        [JsonPropertyName("nodeType")] public string NodeType { get; set; } = string.Empty;
        [JsonPropertyName("required")] public bool Required { get; set; }
        [JsonPropertyName("commandId")] public string CommandId { get; set; } = string.Empty;
        [JsonPropertyName("bindPath")] public string BindPath { get; set; } = string.Empty;
        [JsonPropertyName("feedback")] public UiComposerFeedbackPolicy Feedback { get; set; }
    }

    public sealed class UiComposerFeedbackPolicy
    {
        [JsonPropertyName("activation")] public string Activation { get; set; } = string.Empty;
        [JsonPropertyName("success")] public string Success { get; set; } = string.Empty;
        [JsonPropertyName("failure")] public string Failure { get; set; } = string.Empty;
    }

    public sealed class UiComposerBindingContract
    {
        [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; set; } = string.Empty;
        [JsonPropertyName("screenId")] public string ScreenId { get; set; } = string.Empty;
        [JsonPropertyName("viewId")] public string ViewId { get; set; } = string.Empty;
        [JsonPropertyName("target")] public UiComposerBindingTarget Target { get; set; } = new UiComposerBindingTarget();
        [JsonPropertyName("bindings")] public UiComposerBindingSlot[] Bindings { get; set; } = Array.Empty<UiComposerBindingSlot>();
        [JsonPropertyName("componentBindings")] public UiComposerComponentBindingContract[] ComponentBindings { get; set; } = Array.Empty<UiComposerComponentBindingContract>();
        [JsonPropertyName("focusOrder")] public string[] FocusOrder { get; set; } = Array.Empty<string>();
        [JsonPropertyName("defaultFocus")] public string DefaultFocus { get; set; } = string.Empty;
    }

    /// <summary>Authoring-only binding contract for runtime-addressed children of a Composite recipe.</summary>
    /// <remarks>LowFreqAlloc. Used only by UI Composer validation and materialization tooling.</remarks>
    public sealed class UiComposerComponentBindingContract
    {
        [JsonPropertyName("componentRecipeId")] public string ComponentRecipeId { get; set; } = string.Empty;
        [JsonPropertyName("bindings")] public UiComposerBindingSlot[] Bindings { get; set; } = Array.Empty<UiComposerBindingSlot>();
    }

    public sealed class UiComposerBindingTarget
    {
        [JsonPropertyName("packageId")] public string PackageId { get; set; } = string.Empty;
        [JsonPropertyName("packageSourcePath")] public string PackageSourcePath { get; set; } = string.Empty;
        [JsonPropertyName("componentName")] public string ComponentName { get; set; } = string.Empty;
        [JsonPropertyName("publishedPackagePath")] public string PublishedPackagePath { get; set; } = string.Empty;
        [JsonPropertyName("publishedAtlasPath")] public string PublishedAtlasPath { get; set; } = string.Empty;
        [JsonPropertyName("packageBytesResourceId")] public string PackageBytesResourceId { get; set; } = string.Empty;
        [JsonPropertyName("atlasResourceId")] public string AtlasResourceId { get; set; } = string.Empty;
    }

    public sealed class UiComposerScreenDescriptor
    {
        public UiComposerScreenDescriptor(string screenId, string displayName, string libraryId, string blueprintPath, string packageId, string componentName)
        {
            ScreenId = screenId ?? string.Empty; DisplayName = displayName ?? string.Empty; LibraryId = libraryId ?? string.Empty;
            BlueprintPath = blueprintPath ?? string.Empty; PackageId = packageId ?? string.Empty; ComponentName = componentName ?? string.Empty;
        }
        [JsonPropertyName("screenId")] public string ScreenId { get; }
        [JsonPropertyName("displayName")] public string DisplayName { get; }
        [JsonPropertyName("libraryId")] public string LibraryId { get; }
        [JsonPropertyName("blueprintPath")] public string BlueprintPath { get; }
        [JsonPropertyName("packageId")] public string PackageId { get; }
        [JsonPropertyName("componentName")] public string ComponentName { get; }
    }

    public sealed class UiComposerAuthoringState
    {
        public UiComposerAuthoringState(UiComposerScreenDescriptor screen, string blueprintHash, string recipeCatalogHash, string catalogHash, string sourceXmlHash, string mappingHash, UiScreenBlueprint blueprint, UiComponentRecipeCatalog recipes, UiArtResourceLibraryCatalog resourceCatalog, UiComposerPackageState packageState, IReadOnlyList<UiComposerBindingSlot> bindingSlots, IReadOnlyList<UiComposerDiagnostic> diagnostics)
            : this(screen, blueprintHash, recipeCatalogHash, catalogHash, string.Empty, sourceXmlHash, mappingHash, blueprint, recipes, resourceCatalog, new UiComposerBindingContract { Bindings = bindingSlots?.ToArray() ?? Array.Empty<UiComposerBindingSlot>() }, packageState, bindingSlots, diagnostics)
        {
        }

        public UiComposerAuthoringState(UiComposerScreenDescriptor screen, string blueprintHash, string recipeCatalogHash, string catalogHash, string contractHash, string sourceXmlHash, string mappingHash, UiScreenBlueprint blueprint, UiComponentRecipeCatalog recipes, UiArtResourceLibraryCatalog resourceCatalog, UiComposerBindingContract bindingContract, UiComposerPackageState packageState, IReadOnlyList<UiComposerBindingSlot> bindingSlots, IReadOnlyList<UiComposerDiagnostic> diagnostics)
        {
            Screen = screen; BlueprintHash = blueprintHash; RecipeCatalogHash = recipeCatalogHash; CatalogHash = catalogHash;
            ContractHash = contractHash; SourceXmlHash = sourceXmlHash; MappingHash = mappingHash; Blueprint = blueprint; ComponentRecipes = recipes;
            ResourceCatalog = resourceCatalog; BindingContract = bindingContract; PackageState = packageState; BindingSlots = bindingSlots; Diagnostics = diagnostics;
            for (int i = 0; i < diagnostics.Count; i++) { if (diagnostics[i].Severity == "error") ErrorCount++; else if (diagnostics[i].Severity == "warning") WarningCount++; }
        }
        [JsonPropertyName("schemaVersion")] public string SchemaVersion => "mx.uiComposerAuthoringState.v2";
        [JsonPropertyName("screen")] public UiComposerScreenDescriptor Screen { get; }
        [JsonPropertyName("blueprintHash")] public string BlueprintHash { get; }
        [JsonPropertyName("recipeCatalogHash")] public string RecipeCatalogHash { get; }
        [JsonPropertyName("catalogHash")] public string CatalogHash { get; }
        [JsonPropertyName("contractHash")] public string ContractHash { get; }
        [JsonPropertyName("sourceXmlHash")] public string SourceXmlHash { get; }
        [JsonPropertyName("mappingHash")] public string MappingHash { get; }
        [JsonPropertyName("blueprint")] public UiScreenBlueprint Blueprint { get; }
        [JsonPropertyName("componentRecipes")] public UiComponentRecipeCatalog ComponentRecipes { get; }
        [JsonPropertyName("resourceCatalog")] public UiArtResourceLibraryCatalog ResourceCatalog { get; }
        [JsonPropertyName("bindingContract")] public UiComposerBindingContract BindingContract { get; }
        [JsonPropertyName("packageState")] public UiComposerPackageState PackageState { get; }
        [JsonPropertyName("bindingSlots")] public IReadOnlyList<UiComposerBindingSlot> BindingSlots { get; }
        [JsonPropertyName("diagnostics")] public IReadOnlyList<UiComposerDiagnostic> Diagnostics { get; }
        [JsonPropertyName("errorCount")] public int ErrorCount { get; }
        [JsonPropertyName("warningCount")] public int WarningCount { get; }
    }

    public sealed class UiComposerPackageState
    {
        [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; set; } = string.Empty;
        [JsonPropertyName("owner")] public string Owner { get; set; } = string.Empty;
        [JsonPropertyName("screenId")] public string ScreenId { get; set; } = string.Empty;
        [JsonPropertyName("packageId")] public string PackageId { get; set; } = string.Empty;
        [JsonPropertyName("packageDescriptionId")] public string PackageDescriptionId { get; set; } = string.Empty;
        [JsonPropertyName("imageResourceId")] public string ImageResourceId { get; set; } = string.Empty;
        [JsonPropertyName("buttonComponentId")] public string ButtonComponentId { get; set; } = string.Empty;
        [JsonPropertyName("panelComponentId")] public string PanelComponentId { get; set; } = string.Empty;
        [JsonPropertyName("blueprintHash")] public string BlueprintHash { get; set; } = string.Empty;
        [JsonPropertyName("catalogHash")] public string CatalogHash { get; set; } = string.Empty;
        [JsonPropertyName("recipeCatalogHash")] public string RecipeCatalogHash { get; set; } = string.Empty;
        [JsonPropertyName("contractHash")] public string ContractHash { get; set; } = string.Empty;
        [JsonPropertyName("sourceXmlHash")] public string SourceXmlHash { get; set; } = string.Empty;
        [JsonPropertyName("nodeMappings")] public UiComposerNodeMapping[] NodeMappings { get; set; } = Array.Empty<UiComposerNodeMapping>();
        [JsonPropertyName("resourceMappings")] public UiComposerResourceMapping[] ResourceMappings { get; set; } = Array.Empty<UiComposerResourceMapping>();
        [JsonPropertyName("componentMappings")] public UiComposerComponentMapping[] ComponentMappings { get; set; } = Array.Empty<UiComposerComponentMapping>();
    }

    public sealed class UiComposerResourceMapping
    {
        [JsonPropertyName("resourceId")] public string ResourceId { get; set; } = string.Empty;
        [JsonPropertyName("fairyGuiResourceId")] public string FairyGuiResourceId { get; set; } = string.Empty;
        [JsonPropertyName("targetRelativePath")] public string TargetRelativePath { get; set; } = string.Empty;
    }

    public sealed class UiComposerComponentMapping
    {
        [JsonPropertyName("componentRecipeId")] public string ComponentRecipeId { get; set; } = string.Empty;
        [JsonPropertyName("fairyGuiComponentId")] public string FairyGuiComponentId { get; set; } = string.Empty;
        [JsonPropertyName("componentName")] public string ComponentName { get; set; } = string.Empty;
        [JsonPropertyName("componentRelativePath")] public string ComponentRelativePath { get; set; } = string.Empty;
    }

    public sealed class UiComposerNodeMapping
    {
        public UiComposerNodeMapping() { }
        public UiComposerNodeMapping(string semanticNodeId, string fairyGuiNodeId, string controlName) { SemanticNodeId = semanticNodeId; FairyGuiNodeId = fairyGuiNodeId; ControlName = controlName; }
        [JsonPropertyName("semanticNodeId")] public string SemanticNodeId { get; set; } = string.Empty;
        [JsonPropertyName("fairyGuiNodeId")] public string FairyGuiNodeId { get; set; } = string.Empty;
        [JsonPropertyName("controlName")] public string ControlName { get; set; } = string.Empty;
    }

    public sealed class UiComposerExpectedHashes
    {
        [JsonPropertyName("blueprintHash")] public string BlueprintHash { get; set; } = string.Empty;
        [JsonPropertyName("recipeCatalogHash")] public string RecipeCatalogHash { get; set; } = string.Empty;
        [JsonPropertyName("catalogHash")] public string CatalogHash { get; set; } = string.Empty;
        [JsonPropertyName("contractHash")] public string ContractHash { get; set; } = string.Empty;
        [JsonPropertyName("sourceXmlHash")] public string SourceXmlHash { get; set; } = string.Empty;
        [JsonPropertyName("mappingHash")] public string MappingHash { get; set; } = string.Empty;
    }

    public sealed class UiComposerDiagnostic
    {
        private UiComposerDiagnostic(string code, string severity, string message, string path, string nodeId, bool blocking)
        { Code = code; Severity = severity; Message = message; Path = path; NodeId = nodeId; Blocking = blocking; }
        public static UiComposerDiagnostic Error(string code, string message, string path, string nodeId = "") => new UiComposerDiagnostic(code, "error", message, path, nodeId, true);
        public static UiComposerDiagnostic Warning(string code, string message, string path, string nodeId = "") => new UiComposerDiagnostic(code, "warning", message, path, nodeId, false);
        [JsonPropertyName("code")] public string Code { get; }
        [JsonPropertyName("severity")] public string Severity { get; }
        [JsonPropertyName("message")] public string Message { get; }
        [JsonPropertyName("path")] public string Path { get; }
        [JsonPropertyName("nodeId")] public string NodeId { get; }
        [JsonPropertyName("blocking")] public bool Blocking { get; }
    }

    public sealed class UiComposerValidationResult
    {
        public UiComposerValidationResult(IReadOnlyList<UiComposerDiagnostic> diagnostics) { Diagnostics = diagnostics; Success = !diagnostics.Any(item => item.Blocking); }
        [JsonPropertyName("success")] public bool Success { get; }
        [JsonPropertyName("diagnostics")] public IReadOnlyList<UiComposerDiagnostic> Diagnostics { get; }
    }

    public sealed class UiComposerOperationPlan
    {
        public UiComposerOperationPlan(string operation, bool blocked, bool wouldModifyBlueprint, bool wouldModifyFairyGuiSource, bool wouldPublish, IReadOnlyList<string> targetPaths, IReadOnlyList<UiComposerDiagnostic> diagnostics, UiComposerMaterializationPayload materialization, IReadOnlyList<UiComposerDiffEntry> diffs)
        { Operation = operation; Blocked = blocked; WouldModifyBlueprint = wouldModifyBlueprint; WouldModifyFairyGuiSource = wouldModifyFairyGuiSource; WouldPublish = wouldPublish; TargetPaths = targetPaths; Diagnostics = diagnostics; Materialization = materialization; Diffs = diffs; }
        [JsonPropertyName("operation")] public string Operation { get; }
        [JsonPropertyName("blocked")] public bool Blocked { get; }
        [JsonPropertyName("wouldModifyBlueprint")] public bool WouldModifyBlueprint { get; }
        [JsonPropertyName("wouldModifyFairyGuiSource")] public bool WouldModifyFairyGuiSource { get; }
        [JsonPropertyName("wouldPublish")] public bool WouldPublish { get; }
        [JsonPropertyName("targetPaths")] public IReadOnlyList<string> TargetPaths { get; }
        [JsonPropertyName("diagnostics")] public IReadOnlyList<UiComposerDiagnostic> Diagnostics { get; }
        [JsonPropertyName("materialization")] public UiComposerMaterializationPayload Materialization { get; }
        [JsonPropertyName("diffs")] public IReadOnlyList<UiComposerDiffEntry> Diffs { get; }
    }

    public sealed class UiComposerSyncPlan
    {
        public UiComposerSyncPlan(bool blocked, UiScreenBlueprint proposedBlueprint, IReadOnlyList<UiComposerDiffEntry> diffs, IReadOnlyList<UiComposerDiagnostic> diagnostics) { Blocked = blocked; ProposedBlueprint = proposedBlueprint; Diffs = diffs; Diagnostics = diagnostics; }
        [JsonPropertyName("blocked")] public bool Blocked { get; }
        [JsonPropertyName("proposedBlueprint")] public UiScreenBlueprint ProposedBlueprint { get; }
        [JsonPropertyName("diffs")] public IReadOnlyList<UiComposerDiffEntry> Diffs { get; }
        [JsonPropertyName("diagnostics")] public IReadOnlyList<UiComposerDiagnostic> Diagnostics { get; }
    }

    public sealed class UiComposerDiffEntry
    {
        public UiComposerDiffEntry(string nodeId, string change, string before, string after) { NodeId = nodeId; Change = change; Before = before; After = after; }
        [JsonPropertyName("nodeId")] public string NodeId { get; }
        [JsonPropertyName("change")] public string Change { get; }
        [JsonPropertyName("before")] public string Before { get; }
        [JsonPropertyName("after")] public string After { get; }
    }

    public sealed class UiComposerMaterializationPayload
    {
        [JsonPropertyName("screenId")] public string ScreenId { get; set; } = string.Empty;
        [JsonPropertyName("libraryId")] public string LibraryId { get; set; } = string.Empty;
        [JsonPropertyName("packageId")] public string PackageId { get; set; } = string.Empty;
        [JsonPropertyName("packageSourcePath")] public string PackageSourcePath { get; set; } = string.Empty;
        [JsonPropertyName("componentName")] public string ComponentName { get; set; } = string.Empty;
        [JsonPropertyName("sourcePngPath")] public string SourcePngPath { get; set; } = string.Empty;
        [JsonPropertyName("targetPngRelativePath")] public string TargetPngRelativePath { get; set; } = string.Empty;
        [JsonPropertyName("packageXmlTemplate")] public string PackageXmlTemplate { get; set; } = string.Empty;
        [JsonPropertyName("buttonComponentXmlTemplate")] public string ButtonComponentXmlTemplate { get; set; } = string.Empty;
        [JsonPropertyName("panelComponentXmlTemplate")] public string PanelComponentXmlTemplate { get; set; } = string.Empty;
        [JsonPropertyName("blueprintHash")] public string BlueprintHash { get; set; } = string.Empty;
        [JsonPropertyName("catalogHash")] public string CatalogHash { get; set; } = string.Empty;
        [JsonPropertyName("recipeCatalogHash")] public string RecipeCatalogHash { get; set; } = string.Empty;
        [JsonPropertyName("contractHash")] public string ContractHash { get; set; } = string.Empty;
        [JsonPropertyName("nodeMappings")] public UiComposerNodeMapping[] NodeMappings { get; set; } = Array.Empty<UiComposerNodeMapping>();
        [JsonPropertyName("resources")] public UiComposerMaterializedResource[] Resources { get; set; } = Array.Empty<UiComposerMaterializedResource>();
        [JsonPropertyName("components")] public UiComposerMaterializedComponent[] Components { get; set; } = Array.Empty<UiComposerMaterializedComponent>();
    }

    public sealed class UiComposerMaterializedResource
    {
        [JsonPropertyName("resourceId")] public string ResourceId { get; set; } = string.Empty;
        [JsonPropertyName("sourcePngPath")] public string SourcePngPath { get; set; } = string.Empty;
        [JsonPropertyName("targetPngRelativePath")] public string TargetPngRelativePath { get; set; } = string.Empty;
        [JsonPropertyName("token")] public string Token { get; set; } = string.Empty;
    }

    public sealed class UiComposerMaterializedComponent
    {
        [JsonPropertyName("componentRecipeId")] public string ComponentRecipeId { get; set; } = string.Empty;
        [JsonPropertyName("componentName")] public string ComponentName { get; set; } = string.Empty;
        [JsonPropertyName("componentRelativePath")] public string ComponentRelativePath { get; set; } = string.Empty;
        [JsonPropertyName("token")] public string Token { get; set; } = string.Empty;
        [JsonPropertyName("xmlTemplate")] public string XmlTemplate { get; set; } = string.Empty;
    }

    public sealed class UiComposerOperationRequest
    {
        [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; set; } = string.Empty;
        [JsonPropertyName("operationId")] public string OperationId { get; set; } = string.Empty;
        [JsonPropertyName("action")] public string Action { get; set; } = string.Empty;
        [JsonPropertyName("packageId")] public string PackageId { get; set; } = string.Empty;
        [JsonPropertyName("materialization")] public UiComposerMaterializationPayload Materialization { get; set; }
    }

    public sealed class UiComposerQueuedOperation
    {
        public UiComposerQueuedOperation(string operationId, string status, string requestPath) { OperationId = operationId; Status = status; RequestPath = requestPath; }
        [JsonPropertyName("operationId")] public string OperationId { get; }
        [JsonPropertyName("status")] public string Status { get; }
        [JsonPropertyName("requestPath")] public string RequestPath { get; }
    }

    public sealed class UiComposerOperationProgress
    {
        [JsonPropertyName("operationId")] public string OperationId { get; set; } = string.Empty;
        [JsonPropertyName("phase")] public string Phase { get; set; } = string.Empty;
        [JsonPropertyName("details")] public string Details { get; set; } = string.Empty;
    }

    public sealed class UiComposerOperationResult
    {
        [JsonPropertyName("operationId")] public string OperationId { get; set; } = string.Empty;
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("action")] public string Action { get; set; } = string.Empty;
        [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
        [JsonPropertyName("sourceXmlHash")] public string SourceXmlHash { get; set; } = string.Empty;
        [JsonPropertyName("publishedPath")] public string PublishedPath { get; set; } = string.Empty;
        [JsonPropertyName("helperVersion")] public string HelperVersion { get; set; } = string.Empty;
        [JsonPropertyName("projectPath")] public string ProjectPath { get; set; } = string.Empty;
        [JsonPropertyName("activeBranch")] public string ActiveBranch { get; set; } = string.Empty;
        [JsonPropertyName("editorProcessId")] public int EditorProcessId { get; set; }
        [JsonPropertyName("ready")] public bool Ready { get; set; }
    }

    public sealed class UiComposerOperationStatus
    {
        public UiComposerOperationStatus(string operationId, string status, UiComposerOperationProgress progress, UiComposerOperationResult result) { OperationId = operationId; Status = status; Progress = progress; Result = result; }
        [JsonPropertyName("operationId")] public string OperationId { get; }
        [JsonPropertyName("status")] public string Status { get; }
        [JsonPropertyName("progress")] public UiComposerOperationProgress Progress { get; }
        [JsonPropertyName("result")] public UiComposerOperationResult Result { get; }
    }
}
