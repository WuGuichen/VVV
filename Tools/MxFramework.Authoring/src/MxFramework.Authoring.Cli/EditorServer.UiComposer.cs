using System.Diagnostics;
using System.Net;
using System.Text.Json;
using MxFramework.Authoring.UiComposer;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private static bool TryHandleUiComposerApi(
        HttpListenerContext context,
        string rootPath,
        JsonSerializerOptions jsonOptions,
        string path)
    {
        if (path == "/api/authoring/ui-composer/capabilities" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, new
            {
                apiVersion = UiComposerService.WorkspaceApiVersion,
                supportsBlueprintSave = true,
                supportsHashGuardedApply = true,
                supportsFairyGuiSync = true,
                supportsFairyGuiOperationQueue = true,
                supportsPublish = true,
                supportsWorkspace = true,
                supportsRecipeAuthoring = true
            }, jsonOptions);
            return true;
        }

        if (path == "/api/authoring/ui-composer/screens" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, UiComposerService.ListScreens(rootPath), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/ui-composer/state" && context.Request.HttpMethod == "GET")
        {
            string screen = context.Request.QueryString["screen"] ?? UiComposerService.DefaultScreenId;
            WriteJson(context.Response, UiComposerService.ReadState(rootPath, screen), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/ui-composer/workspace" && context.Request.HttpMethod == "GET")
        {
            string screen = context.Request.QueryString["screen"] ?? UiComposerService.DefaultScreenId;
            WriteJson(context.Response, UiComposerService.ReadWorkspace(rootPath, screen), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/ui-composer/plan-save-recipes" && context.Request.HttpMethod == "POST")
        {
            UiComposerRecipeRequest request = ReadUiComposerRequest<UiComposerRecipeRequest>(context, jsonOptions);
            UiComposerWorkspacePlan plan = UiComposerService.PlanSaveRecipes(rootPath, request.screen, request.expectedRecipeCatalogHash, request.recipes);
            WriteJson(context.Response, new { success = !plan.Blocked, plan }, jsonOptions, plan.Blocked ? 409 : 200);
            return true;
        }

        if (path == "/api/authoring/ui-composer/save-recipes" && context.Request.HttpMethod == "POST")
        {
            UiComposerRecipeRequest request = ReadUiComposerRequest<UiComposerRecipeRequest>(context, jsonOptions);
            UiComposerWorkspacePlan plan = UiComposerService.PlanSaveRecipes(rootPath, request.screen, request.expectedRecipeCatalogHash, request.recipes);
            if (plan.Blocked) { WriteJson(context.Response, new { success = false, plan }, jsonOptions, 409); return true; }
            WriteJson(context.Response, new { success = true, plan, workspace = UiComposerService.SaveRecipes(rootPath, request.screen, request.expectedRecipeCatalogHash, request.recipes) }, jsonOptions);
            return true;
        }

        if (path == "/api/authoring/ui-composer/plan-delete-recipe" && context.Request.HttpMethod == "POST")
        {
            UiComposerDeleteRecipeRequest request = ReadUiComposerRequest<UiComposerDeleteRecipeRequest>(context, jsonOptions);
            UiComposerWorkspacePlan plan = UiComposerService.PlanDeleteRecipe(rootPath, request.screen, request.recipeId);
            WriteJson(context.Response, new { success = !plan.Blocked, plan }, jsonOptions, plan.Blocked ? 409 : 200);
            return true;
        }

        if (path == "/api/authoring/ui-composer/plan-workspace" && context.Request.HttpMethod == "POST")
        {
            UiComposerWorkspaceRequest request = ReadUiComposerRequest<UiComposerWorkspaceRequest>(context, jsonOptions);
            UiComposerWorkspacePlan plan = UiComposerService.PlanWorkspace(rootPath, request.screen, request.expected, request.resources, request.recipes, request.blueprint);
            WriteJson(context.Response, new { success = !plan.Blocked, plan }, jsonOptions, plan.Blocked ? 409 : 200);
            return true;
        }

        if (path == "/api/authoring/ui-composer/apply-workspace" && context.Request.HttpMethod == "POST")
        {
            UiComposerWorkspaceRequest request = ReadUiComposerRequest<UiComposerWorkspaceRequest>(context, jsonOptions);
            UiComposerWorkspacePlan plan = UiComposerService.PlanWorkspace(rootPath, request.screen, request.expected, request.resources, request.recipes, request.blueprint);
            if (plan.Blocked) { WriteJson(context.Response, new { success = false, plan }, jsonOptions, 409); return true; }
            UiComposerQueuedOperation operation = UiComposerService.ApplyWorkspace(rootPath, request.screen, request.expected, request.resources, request.recipes, request.blueprint);
            UiComposerEditorLaunchResult launch = request.launchEditor ? TryLaunchFairyGuiEditor(rootPath) : new UiComposerEditorLaunchResult(false, false, "FairyGUI launch was not requested.");
            WriteJson(context.Response, new { success = true, plan, operation, launch }, jsonOptions, 202);
            return true;
        }

        if (path == "/api/authoring/ui-composer/validate" && context.Request.HttpMethod == "POST")
        {
            UiComposerBlueprintRequest request = ReadUiComposerRequest<UiComposerBlueprintRequest>(context, jsonOptions);
            UiComposerValidationResult result = UiComposerService.ValidateBlueprint(rootPath, request.screen, request.blueprint);
            WriteJson(context.Response, result, jsonOptions, result.Success ? 200 : 409);
            return true;
        }

        if (path == "/api/authoring/ui-composer/save" && context.Request.HttpMethod == "POST")
        {
            UiComposerBlueprintRequest request = ReadUiComposerRequest<UiComposerBlueprintRequest>(context, jsonOptions);
            UiComposerOperationPlan plan = UiComposerService.PlanSave(rootPath, request.screen, request.expectedBlueprintHash, request.expectedContractHash, request.blueprint);
            if (plan.Blocked)
            {
                WriteJson(context.Response, new { success = false, plan }, jsonOptions, 409);
                return true;
            }

            UiComposerAuthoringState state = UiComposerService.SaveBlueprint(rootPath, request.screen, request.expectedBlueprintHash, request.expectedContractHash, request.blueprint);
            WriteJson(context.Response, new { success = true, plan, state }, jsonOptions);
            return true;
        }

        if (path == "/api/authoring/ui-composer/plan-apply" && context.Request.HttpMethod == "POST")
        {
            UiComposerOperationApiRequest request = ReadUiComposerRequest<UiComposerOperationApiRequest>(context, jsonOptions);
            UiComposerOperationPlan plan = UiComposerService.PlanApply(rootPath, request.screen, request.expected);
            WriteJson(context.Response, new { success = !plan.Blocked, plan }, jsonOptions, plan.Blocked ? 409 : 200);
            return true;
        }

        if (path == "/api/authoring/ui-composer/apply" && context.Request.HttpMethod == "POST")
        {
            UiComposerOperationApiRequest request = ReadUiComposerRequest<UiComposerOperationApiRequest>(context, jsonOptions);
            UiComposerOperationPlan plan = UiComposerService.PlanApply(rootPath, request.screen, request.expected);
            if (plan.Blocked)
            {
                WriteJson(context.Response, new { success = false, plan }, jsonOptions, 409);
                return true;
            }

            UiComposerQueuedOperation operation = UiComposerService.QueueApply(rootPath, request.screen, request.expected);
            UiComposerEditorLaunchResult launch = request.launchEditor
                ? TryLaunchFairyGuiEditor(rootPath)
                : new UiComposerEditorLaunchResult(false, false, "FairyGUI launch was not requested.");
            WriteJson(context.Response, new { success = true, plan, operation, launch }, jsonOptions, 202);
            return true;
        }

        if (path == "/api/authoring/ui-composer/plan-sync" && context.Request.HttpMethod == "POST")
        {
            UiComposerOperationApiRequest request = ReadUiComposerRequest<UiComposerOperationApiRequest>(context, jsonOptions);
            UiComposerSyncPlan plan = UiComposerService.PlanSync(rootPath, request.screen, request.expected);
            WriteJson(context.Response, new { success = !plan.Blocked, plan }, jsonOptions, plan.Blocked ? 409 : 200);
            return true;
        }

        if (path == "/api/authoring/ui-composer/sync" && context.Request.HttpMethod == "POST")
        {
            UiComposerOperationApiRequest request = ReadUiComposerRequest<UiComposerOperationApiRequest>(context, jsonOptions);
            UiComposerSyncPlan plan = UiComposerService.PlanSync(rootPath, request.screen, request.expected);
            if (plan.Blocked)
            {
                WriteJson(context.Response, new { success = false, plan }, jsonOptions, 409);
                return true;
            }

            UiComposerAuthoringState state = UiComposerService.SyncBlueprint(rootPath, request.screen, request.expected);
            WriteJson(context.Response, new { success = true, plan, state }, jsonOptions);
            return true;
        }

        if (path == "/api/authoring/ui-composer/operation-status" && context.Request.HttpMethod == "GET")
        {
            string operationId = context.Request.QueryString["operationId"] ?? string.Empty;
            WriteJson(context.Response, UiComposerService.ReadOperationStatus(rootPath, operationId), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/ui-composer/publish" && context.Request.HttpMethod == "POST")
        {
            UiComposerOperationApiRequest request = ReadUiComposerRequest<UiComposerOperationApiRequest>(context, jsonOptions);
            UiComposerOperationPlan plan = UiComposerService.PlanPublish(rootPath, request.screen, request.expected);
            if (plan.Blocked)
            {
                WriteJson(context.Response, new { success = false, plan }, jsonOptions, 409);
                return true;
            }

            UiComposerQueuedOperation operation = UiComposerService.QueuePublish(rootPath, request.screen, request.expected);
            UiComposerEditorLaunchResult launch = request.launchEditor
                ? TryLaunchFairyGuiEditor(rootPath)
                : new UiComposerEditorLaunchResult(false, false, "FairyGUI launch was not requested.");
            WriteJson(context.Response, new { success = true, plan, operation, launch }, jsonOptions, 202);
            return true;
        }

        return false;
    }

    private static T ReadUiComposerRequest<T>(HttpListenerContext context, JsonSerializerOptions jsonOptions)
        where T : new()
    {
        string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
        return string.IsNullOrWhiteSpace(body)
            ? new T()
            : JsonSerializer.Deserialize<T>(body, jsonOptions) ?? new T();
    }

    private static UiComposerEditorLaunchResult TryLaunchFairyGuiEditor(string rootPath)
    {
        string editorPath = Path.GetFullPath(Path.Combine(rootPath, "..", "FairyGUI-Editor", "FairyGUI-Editor.exe"));
        string projectPath = Path.Combine(rootPath, "FGUIProject", "FGUIProject.fairy");
        if (!File.Exists(editorPath))
            return new UiComposerEditorLaunchResult(true, false, "FairyGUI Editor executable was not found: " + editorPath);
        if (!File.Exists(projectPath))
            return new UiComposerEditorLaunchResult(true, false, "FairyGUI project was not found: " + projectPath);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = editorPath,
                Arguments = "\"" + projectPath + "\"",
                WorkingDirectory = rootPath,
                UseShellExecute = true
            });
            return new UiComposerEditorLaunchResult(true, true, "FairyGUI Editor launch requested.");
        }
        catch (Exception ex)
        {
            return new UiComposerEditorLaunchResult(true, false, "FairyGUI Editor launch failed: " + ex.Message);
        }
    }

    private sealed class UiComposerBlueprintRequest
    {
        public string screen { get; set; } = UiComposerService.DefaultScreenId;
        public string expectedBlueprintHash { get; set; } = string.Empty;
        public string expectedContractHash { get; set; } = string.Empty;
        public UiScreenBlueprint blueprint { get; set; }
    }

    private sealed class UiComposerOperationApiRequest
    {
        public string screen { get; set; } = UiComposerService.DefaultScreenId;
        public UiComposerExpectedHashes expected { get; set; } = new UiComposerExpectedHashes();
        public bool launchEditor { get; set; }
    }

    private sealed class UiComposerRecipeRequest
    {
        public string screen { get; set; } = UiComposerService.DefaultScreenId;
        public string expectedRecipeCatalogHash { get; set; } = string.Empty;
        public UiComponentRecipeCatalog recipes { get; set; }
    }

    private sealed class UiComposerDeleteRecipeRequest
    {
        public string screen { get; set; } = UiComposerService.DefaultScreenId;
        public string recipeId { get; set; } = string.Empty;
    }

    private sealed class UiComposerWorkspaceRequest
    {
        public string screen { get; set; } = UiComposerService.DefaultScreenId;
        public UiComposerExpectedHashes expected { get; set; } = new UiComposerExpectedHashes();
        public MxFramework.Authoring.FairyGuiArtSlices.UiArtResourceLibraryCatalog resources { get; set; }
        public UiComponentRecipeCatalog recipes { get; set; }
        public UiScreenBlueprint blueprint { get; set; }
        public bool launchEditor { get; set; }
    }

    private sealed class UiComposerEditorLaunchResult
    {
        public UiComposerEditorLaunchResult(bool requested, bool started, string message)
        {
            this.requested = requested;
            this.started = started;
            this.message = message ?? string.Empty;
        }

        public bool requested { get; }
        public bool started { get; }
        public string message { get; }
    }
}
