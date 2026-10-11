using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring.FairyGuiArtSlices;

namespace MxFramework.Authoring.UiComposer
{
    public static partial class UiComposerService
    {
        public const string WorkspaceApiVersion = "mx.uiComposer.authoring.v5";

        public static UiComposerWorkspaceState ReadWorkspace(string repositoryRoot, string screen)
        {
            UiComposerAuthoringState state = ReadState(repositoryRoot, screen);
            var references = new List<UiComposerReference>(BuildReferences(state.ResourceCatalog, state.ComponentRecipes, state.Blueprint));
            foreach (UiComposerScreenDescriptor descriptor in ListScreens(repositoryRoot))
            {
                if (descriptor.ScreenId == state.Screen.ScreenId) continue;
                UiScreenBlueprint blueprint = ReadJson<UiScreenBlueprint>(ResolvePath(repositoryRoot, descriptor.BlueprintPath));
                references.AddRange(BuildReferences(null, null, blueprint));
            }
            UiComposerReference[] stable = references.GroupBy(item => item.SourceScreenId + "\n" + item.SourceKind + "\n" + item.SourceId + "\n" + item.TargetKind + "\n" + item.TargetId + "\n" + item.Role, StringComparer.Ordinal)
                .Select(group => group.First()).OrderBy(item => item.TargetKind).ThenBy(item => item.TargetId).ThenBy(item => item.SourceId).ToArray();
            return new UiComposerWorkspaceState(state, stable);
        }

        public static UiComposerWorkspacePlan PlanSaveRecipes(string repositoryRoot, string screen, string expectedRecipeHash, UiComponentRecipeCatalog catalog)
        {
            UiComposerAuthoringState state = ReadState(repositoryRoot, screen);
            var diagnostics = new List<UiComposerDiagnostic>();
            if (!string.Equals(expectedRecipeHash ?? string.Empty, state.RecipeCatalogHash, StringComparison.Ordinal))
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP201", "Component recipe catalog changed on disk. Refresh before saving.", DefaultRecipeCatalogPath));
            diagnostics.AddRange(ValidateRecipes(catalog, state.ResourceCatalog));
            return new UiComposerWorkspacePlan("SaveRecipes", diagnostics, new[] { DefaultRecipeCatalogPath });
        }

        public static UiComposerWorkspaceState SaveRecipes(string repositoryRoot, string screen, string expectedRecipeHash, UiComponentRecipeCatalog catalog)
        {
            UiComposerWorkspacePlan plan = PlanSaveRecipes(repositoryRoot, screen, expectedRecipeHash, catalog);
            if (plan.Blocked) throw new InvalidOperationException("UI Composer recipe save is blocked.");
            WriteWorkspaceTextAtomic(Path.Combine(repositoryRoot, DefaultRecipeCatalogPath), SerializeWorkspace(catalog));
            return ReadWorkspace(repositoryRoot, screen);
        }

        public static UiComposerWorkspacePlan PlanDeleteRecipe(string repositoryRoot, string screen, string recipeId)
        {
            UiComposerWorkspaceState state = ReadWorkspace(repositoryRoot, screen);
            var diagnostics = new List<UiComposerDiagnostic>();
            UiComposerReference[] references = state.References.Where(item => item.TargetKind == "recipe" && item.TargetId == recipeId).ToArray();
            if (references.Length > 0)
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP202", "Recipe is still referenced by " + references.Length + " usage location(s). Replace those references before deleting it.", recipeId));
            if (!state.AuthoringState.ComponentRecipes.Recipes.Any(item => item.ComponentId == recipeId))
                diagnostics.Add(UiComposerDiagnostic.Error("UICOMP203", "Component recipe does not exist.", recipeId));
            return new UiComposerWorkspacePlan("DeleteRecipe", diagnostics, new[] { DefaultRecipeCatalogPath });
        }

        public static UiComposerWorkspacePlan PlanWorkspace(string repositoryRoot, string screen, UiComposerExpectedHashes expected, UiArtResourceLibraryCatalog resources, UiComponentRecipeCatalog recipes, UiScreenBlueprint blueprint)
        {
            UiComposerAuthoringState current = ReadState(repositoryRoot, screen);
            var diagnostics = new List<UiComposerDiagnostic>();
            ValidateWorkspaceHash(expected?.CatalogHash, current.CatalogHash, "resource catalog", diagnostics);
            ValidateWorkspaceHash(expected?.RecipeCatalogHash, current.RecipeCatalogHash, "recipe catalog", diagnostics);
            ValidateWorkspaceHash(expected?.BlueprintHash, current.BlueprintHash, "blueprint", diagnostics);
            ValidateWorkspaceHash(expected?.ContractHash, current.ContractHash, "binding contract", diagnostics);

            UiArtResourceLibraryValidationResult resourceValidation = ValidateProposedResourceCatalog(repositoryRoot, resources);
            foreach (UiArtResourceDiagnostic item in resourceValidation.Diagnostics)
                diagnostics.Add(item.Severity == "error"
                    ? UiComposerDiagnostic.Error("UICOMP210", item.Message, item.ResourceId)
                    : UiComposerDiagnostic.Warning("UICOMP210", item.Message, item.ResourceId));
            diagnostics.AddRange(ValidateRecipes(recipes, resources));
            diagnostics.AddRange(ValidateWorkspaceReferences(blueprint, recipes, resources));
            diagnostics.AddRange(ValidateWorkspaceBlueprint(
                repositoryRoot,
                screen,
                current,
                blueprint,
                recipes,
                resources,
                resourceValidation));
            SortDiagnostics(diagnostics);
            return new UiComposerWorkspacePlan("WorkspacePreflight", diagnostics, new[] { UiArtResourceLibraryService.DefaultCatalogPath, DefaultRecipeCatalogPath, current.Screen.BlueprintPath });
        }

        public static UiComposerQueuedOperation ApplyWorkspace(string repositoryRoot, string screen, UiComposerExpectedHashes expected, UiArtResourceLibraryCatalog resources, UiComponentRecipeCatalog recipes, UiScreenBlueprint blueprint)
        {
            UiComposerWorkspacePlan plan = PlanWorkspace(repositoryRoot, screen, expected, resources, recipes, blueprint);
            if (plan.Blocked) throw new InvalidOperationException("UI Composer workspace apply is blocked.");

            UiComposerAuthoringState current = ReadState(repositoryRoot, screen);
            UiArtResourceLibraryDescriptor library = UiArtResourceLibraryService.ResolveLibraryDescriptor(
                repositoryRoot,
                resources.LibraryId);
            var writes = new[]
            {
                new WorkspaceWrite(
                    Path.Combine(repositoryRoot, library.CatalogPath),
                    UiArtResourceLibraryService.SerializeCatalog(resources),
                    expected.CatalogHash,
                    "resource catalog"),
                new WorkspaceWrite(
                    Path.Combine(repositoryRoot, DefaultRecipeCatalogPath),
                    SerializeWorkspace(recipes),
                    expected.RecipeCatalogHash,
                    "recipe catalog"),
                new WorkspaceWrite(
                    Path.Combine(repositoryRoot, current.Screen.BlueprintPath),
                    SerializeWorkspace(blueprint),
                    expected.BlueprintHash,
                    "blueprint")
            };
            return ExecuteWorkspaceTransaction(writes, () =>
            {
                UiComposerAuthoringState saved = ReadState(repositoryRoot, screen);
                return QueueApply(repositoryRoot, screen, new UiComposerExpectedHashes
                {
                    BlueprintHash = saved.BlueprintHash,
                    RecipeCatalogHash = saved.RecipeCatalogHash,
                    CatalogHash = saved.CatalogHash,
                    ContractHash = saved.ContractHash,
                    SourceXmlHash = saved.SourceXmlHash,
                    MappingHash = saved.MappingHash
                });
            });
        }

        private static UiArtResourceLibraryValidationResult ValidateProposedResourceCatalog(string repositoryRoot, UiArtResourceLibraryCatalog catalog)
        {
            if (catalog == null)
                return new UiArtResourceLibraryValidationResult(string.Empty, string.Empty, Array.Empty<UiArtResource>(), new[] { new UiArtResourceDiagnostic("UIARTRESAPI010", "Catalog required", "error", string.Empty, string.Empty, string.Empty, "Resource catalog payload is required.") });
            string tempRoot = Path.Combine(Path.GetTempPath(), "mx-ui-workspace-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(tempRoot, "ArtSource", "UIKits", "MxUiFoundation"));
                File.WriteAllText(Path.Combine(tempRoot, UiArtResourceLibraryService.DefaultCatalogPath), UiArtResourceLibraryService.SerializeCatalog(catalog), new UTF8Encoding(false));
                foreach (UiArtResource resource in catalog.Resources ?? Array.Empty<UiArtResource>())
                {
                    string source = Path.Combine(repositoryRoot, catalog.ResourceRoot.Replace('/', Path.DirectorySeparatorChar), resource.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                    string target = Path.Combine(tempRoot, catalog.ResourceRoot.Replace('/', Path.DirectorySeparatorChar), resource.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(source)) { Directory.CreateDirectory(Path.GetDirectoryName(target) ?? tempRoot); File.Copy(source, target); }
                }
                return UiArtResourceLibraryService.ValidateLibrary(tempRoot, UiArtResourceLibraryService.DefaultCatalogPath);
            }
            finally { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); }
        }

        private static IEnumerable<UiComposerDiagnostic> ValidateRecipes(UiComponentRecipeCatalog catalog, UiArtResourceLibraryCatalog resources)
        {
            var diagnostics = new List<UiComposerDiagnostic>();
            if (catalog == null) { diagnostics.Add(UiComposerDiagnostic.Error("UICOMP204", "Component recipe catalog payload is required.", DefaultRecipeCatalogPath)); return diagnostics; }
            var resourceMap = (resources?.Resources ?? Array.Empty<UiArtResource>()).ToDictionary(item => item.ResourceId, StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            UiComponentRecipe[] recipes = catalog.Recipes ?? Array.Empty<UiComponentRecipe>();
            foreach (UiComponentRecipe recipe in recipes)
            {
                string path = "recipes/" + (recipe.ComponentId ?? string.Empty);
                if (string.IsNullOrWhiteSpace(recipe.ComponentId) || !ids.Add(recipe.ComponentId)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP205", "Recipe id is empty or duplicated.", path));
                if (recipe.ComponentType == "Composite") continue;
                if (recipe.ComponentType != "Button") { diagnostics.Add(UiComposerDiagnostic.Error("UICOMP206", "Only Button and controlled Composite recipes are supported.", path)); continue; }
                if (recipe.DefaultSize == null || recipe.DefaultSize.Width <= 0 || recipe.DefaultSize.Height <= 0) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP207", "Button recipe default size must be positive.", path));
                UiComponentResourceSlot baseSlot = (recipe.ResourceSlots ?? Array.Empty<UiComponentResourceSlot>()).FirstOrDefault(item => item.SlotId == "base");
                if (baseSlot == null || !resourceMap.TryGetValue(baseSlot.ResourceId, out UiArtResource baseResource)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP208", "Button recipe requires a valid base resource slot.", path));
                else if (baseResource.RecommendedFairyGuiUsage == null || (!baseResource.Replacement.AllowStretch && baseResource.RecommendedFairyGuiUsage.Policy != "NineSlice")) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP209", "Button base resource must support stretching or NineSlice.", path));
                UiButtonStatePresentation presentation = recipe.StatePresentation;
                if (presentation == null || presentation.ControllerName != "button" || !(presentation.Pages ?? Array.Empty<string>()).Contains("up") || !(presentation.Pages ?? Array.Empty<string>()).Contains("down")) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP211", "Button recipe must use the standard button controller with up and down pages.", path));
            }
            ValidateComponentRecipes(recipes, new HashSet<string>(resourceMap.Keys, StringComparer.Ordinal), ids, DefaultRecipeCatalogPath, diagnostics);
            return diagnostics;
        }

        private static IEnumerable<UiComposerDiagnostic> ValidateWorkspaceReferences(UiScreenBlueprint blueprint, UiComponentRecipeCatalog recipes, UiArtResourceLibraryCatalog resources)
        {
            var diagnostics = new List<UiComposerDiagnostic>();
            var recipeIds = new HashSet<string>((recipes?.Recipes ?? Array.Empty<UiComponentRecipe>()).Select(item => item.ComponentId), StringComparer.Ordinal);
            var resourceMap = (resources?.Resources ?? Array.Empty<UiArtResource>()).ToDictionary(item => item.ResourceId, StringComparer.Ordinal);
            foreach (UiScreenNode node in blueprint?.Nodes ?? Array.Empty<UiScreenNode>())
            {
                if (node.Component != null && !recipeIds.Contains(node.Component.ComponentRecipeId)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP212", "Node references a missing component recipe.", node.NodeId));
                if (node.List != null && !recipeIds.Contains(node.List.ItemComponentRecipeId)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP212", "List references a missing item component recipe.", node.NodeId));
                if (!string.IsNullOrEmpty(node.Component?.IconResourceId) && (!resourceMap.TryGetValue(node.Component.IconResourceId, out UiArtResource icon) || icon.Category != "Icon")) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP213", "Button icon must reference an Icon resource.", node.NodeId));
                if (node.Image != null && !resourceMap.ContainsKey(node.Image.ResourceId)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP214", "Image node references a missing resource.", node.NodeId));
            }
            return diagnostics;
        }

        private static IEnumerable<UiComposerDiagnostic> ValidateWorkspaceBlueprint(
            string repositoryRoot,
            string screen,
            UiComposerAuthoringState current,
            UiScreenBlueprint blueprint,
            UiComponentRecipeCatalog recipes,
            UiArtResourceLibraryCatalog resources,
            UiArtResourceLibraryValidationResult resourceValidation)
        {
            ScreenContext currentContext = ResolveContext(repositoryRoot, screen);
            var proposedContext = new ScreenContext(
                currentContext.BlueprintPath,
                currentContext.RecipePath,
                currentContext.ContractPath,
                currentContext.Blueprint,
                recipes,
                currentContext.Contract);
            UiArtResourceLibraryDescriptor descriptor = UiArtResourceLibraryService.ResolveLibraryDescriptor(
                repositoryRoot,
                resources?.LibraryId ?? current.Blueprint.LibraryId);
            var proposedLibrary = new UiArtResourceLibraryAuthoringState(
                descriptor,
                current.CatalogHash,
                resources,
                resourceValidation,
                string.Empty);
            UiComposerPackageState packageState = TryReadJson<UiComposerPackageState>(
                ResolvePath(repositoryRoot, proposedContext.StatePath));
            return Validate(
                repositoryRoot,
                proposedContext,
                blueprint,
                proposedLibrary,
                packageState,
                ComputeFileHash(ResolvePath(repositoryRoot, proposedContext.ComponentPath)),
                validateSourceDrift: false);
        }

        private static UiComposerReference[] BuildReferences(UiArtResourceLibraryCatalog resources, UiComponentRecipeCatalog recipes, UiScreenBlueprint blueprint)
        {
            var result = new List<UiComposerReference>();
            foreach (UiComponentRecipe recipe in recipes?.Recipes ?? Array.Empty<UiComponentRecipe>())
            {
                foreach (UiComponentResourceSlot slot in recipe.ResourceSlots ?? Array.Empty<UiComponentResourceSlot>())
                    result.Add(new UiComposerReference("recipe", recipe.ComponentId, "resource", slot.ResourceId, slot.SlotId, string.Empty));
                foreach (UiScreenNode node in recipe.Template?.Nodes ?? Array.Empty<UiScreenNode>())
                {
                    string sourceId = recipe.ComponentId + "/" + node.NodeId;
                    if (node.Image != null) result.Add(new UiComposerReference("recipeNode", sourceId, "resource", node.Image.ResourceId, "image", string.Empty));
                    if (node.Component != null)
                    {
                        result.Add(new UiComposerReference("recipeNode", sourceId, "recipe", node.Component.ComponentRecipeId, "component", string.Empty));
                        if (!string.IsNullOrEmpty(node.Component.IconResourceId)) result.Add(new UiComposerReference("recipeNode", sourceId, "resource", node.Component.IconResourceId, "icon", string.Empty));
                    }
                }
            }
            foreach (UiScreenNode node in blueprint?.Nodes ?? Array.Empty<UiScreenNode>())
            {
                if (node.Image != null) result.Add(new UiComposerReference("node", node.NodeId, "resource", node.Image.ResourceId, "image", blueprint.ScreenId));
                if (node.Component != null)
                {
                    result.Add(new UiComposerReference("node", node.NodeId, "recipe", node.Component.ComponentRecipeId, "component", blueprint.ScreenId));
                    if (!string.IsNullOrEmpty(node.Component.IconResourceId)) result.Add(new UiComposerReference("node", node.NodeId, "resource", node.Component.IconResourceId, "icon", blueprint.ScreenId));
                }
                if (node.List != null) result.Add(new UiComposerReference("node", node.NodeId, "recipe", node.List.ItemComponentRecipeId, "item", blueprint.ScreenId));
            }
            return result.OrderBy(item => item.TargetKind).ThenBy(item => item.TargetId).ThenBy(item => item.SourceId).ToArray();
        }

        private static void ValidateWorkspaceHash(string expected, string actual, string label, List<UiComposerDiagnostic> diagnostics)
        { if (!string.Equals(expected ?? string.Empty, actual ?? string.Empty, StringComparison.Ordinal)) diagnostics.Add(UiComposerDiagnostic.Error("UICOMP201", label + " changed on disk. Refresh before continuing.", label)); }
        private static string SerializeWorkspace<T>(T value) => JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        private static void WriteWorkspaceTextAtomic(string path, string text) { Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty); string temp = path + ".tmp-" + Guid.NewGuid().ToString("N"); File.WriteAllText(temp, text, new UTF8Encoding(false)); if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); }

        private static T ExecuteWorkspaceTransaction<T>(IReadOnlyList<WorkspaceWrite> writes, Func<T> afterCommit)
        {
            if (writes == null) throw new ArgumentNullException(nameof(writes));
            if (afterCommit == null) throw new ArgumentNullException(nameof(afterCommit));

            string transactionId = Guid.NewGuid().ToString("N");
            try
            {
                for (int i = 0; i < writes.Count; i++)
                    writes[i].Stage(transactionId);
                for (int i = 0; i < writes.Count; i++)
                    writes[i].Commit(transactionId);

                T result = afterCommit();
                for (int i = 0; i < writes.Count; i++)
                    writes[i].Complete();
                return result;
            }
            catch (Exception commitError)
            {
                Exception rollbackError = null;
                for (int i = writes.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        writes[i].Rollback();
                    }
                    catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                    {
                        rollbackError = rollbackError == null
                            ? error
                            : new AggregateException(rollbackError, error);
                    }
                }

                if (rollbackError != null)
                    throw new AggregateException("UI Composer workspace transaction and rollback both failed.", commitError, rollbackError);
                throw;
            }
            finally
            {
                for (int i = 0; i < writes.Count; i++)
                    writes[i].CleanupTemporary();
            }
        }

        private sealed class WorkspaceWrite
        {
            private readonly string _targetPath;
            private readonly string _text;
            private readonly string _expectedHash;
            private readonly string _label;
            private string _temporaryPath;
            private string _backupPath;
            private bool _targetExisted;
            private bool _committed;

            public WorkspaceWrite(string targetPath, string text, string expectedHash, string label)
            {
                _targetPath = targetPath ?? throw new ArgumentNullException(nameof(targetPath));
                _text = text ?? throw new ArgumentNullException(nameof(text));
                _expectedHash = expectedHash ?? string.Empty;
                _label = label ?? "workspace file";
            }

            public void Stage(string transactionId)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_targetPath) ?? string.Empty);
                _temporaryPath = _targetPath + ".tmp-" + transactionId;
                File.WriteAllText(_temporaryPath, _text, new UTF8Encoding(false));
            }

            public void Commit(string transactionId)
            {
                string currentHash = ComputeFileHash(_targetPath);
                if (!string.Equals(currentHash, _expectedHash, StringComparison.Ordinal))
                    throw new InvalidOperationException("UI Composer " + _label + " changed during workspace apply. Refresh before continuing.");

                _targetExisted = File.Exists(_targetPath);
                if (_targetExisted)
                {
                    _backupPath = _targetPath + ".bak-" + transactionId;
                    File.Replace(_temporaryPath, _targetPath, _backupPath);
                }
                else
                {
                    File.Move(_temporaryPath, _targetPath);
                }

                _committed = true;
                _temporaryPath = null;
            }

            public void Complete()
            {
                _committed = false;
                TryDeleteFile(_backupPath);
                _backupPath = null;
            }

            public void Rollback()
            {
                if (!_committed)
                    return;

                if (_targetExisted)
                {
                    if (string.IsNullOrEmpty(_backupPath) || !File.Exists(_backupPath))
                        throw new IOException("UI Composer workspace rollback backup is missing for " + _label + ".");
                    if (File.Exists(_targetPath))
                        File.Delete(_targetPath);
                    File.Move(_backupPath, _targetPath);
                    _backupPath = null;
                }
                else if (File.Exists(_targetPath))
                {
                    File.Delete(_targetPath);
                }

                _committed = false;
            }

            public void CleanupTemporary()
            {
                TryDeleteFile(_temporaryPath);
                _temporaryPath = null;
            }

            private static void TryDeleteFile(string path)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return;

                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    public sealed class UiComposerWorkspaceState
    {
        public UiComposerWorkspaceState(UiComposerAuthoringState authoringState, UiComposerReference[] references) { AuthoringState = authoringState; References = references ?? Array.Empty<UiComposerReference>(); }
        [JsonPropertyName("schemaVersion")] public string SchemaVersion => "mx.uiComposerWorkspaceState.v1";
        [JsonPropertyName("authoringState")] public UiComposerAuthoringState AuthoringState { get; }
        [JsonPropertyName("references")] public UiComposerReference[] References { get; }
    }

    public sealed class UiComposerReference
    {
        public UiComposerReference(string sourceKind, string sourceId, string targetKind, string targetId, string role, string sourceScreenId) { SourceKind = sourceKind; SourceId = sourceId; TargetKind = targetKind; TargetId = targetId; Role = role; SourceScreenId = sourceScreenId ?? string.Empty; }
        [JsonPropertyName("sourceKind")] public string SourceKind { get; }
        [JsonPropertyName("sourceId")] public string SourceId { get; }
        [JsonPropertyName("targetKind")] public string TargetKind { get; }
        [JsonPropertyName("targetId")] public string TargetId { get; }
        [JsonPropertyName("role")] public string Role { get; }
        [JsonPropertyName("sourceScreenId")] public string SourceScreenId { get; }
    }

    public sealed class UiComposerWorkspacePlan
    {
        public UiComposerWorkspacePlan(string operation, IReadOnlyList<UiComposerDiagnostic> diagnostics, IReadOnlyList<string> writes) { Operation = operation; Diagnostics = diagnostics ?? Array.Empty<UiComposerDiagnostic>(); Writes = writes ?? Array.Empty<string>(); }
        [JsonPropertyName("operation")] public string Operation { get; }
        [JsonPropertyName("blocked")] public bool Blocked => Diagnostics.Any(item => item.Severity == "error");
        [JsonPropertyName("diagnostics")] public IReadOnlyList<UiComposerDiagnostic> Diagnostics { get; }
        [JsonPropertyName("writes")] public IReadOnlyList<string> Writes { get; }
    }
}
