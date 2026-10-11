using System.Net;
using System.Text.Json;
using MxFramework.Authoring.FairyGuiArtSlices;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private static bool TryHandleFairyGuiArtSlicesApi(
        HttpListenerContext context,
        string rootPath,
        JsonSerializerOptions jsonOptions,
        string path)
    {
        if (path == "/api/authoring/ui-art/resources/capabilities" && context.Request.HttpMethod == "GET")
        {
            WriteJson(
                context.Response,
                new
                {
                    apiVersion = UiArtResourceLibraryService.AuthoringApiVersion,
                    supportsCatalogValidation = true,
                    supportsSafeLibraryRoots = true,
                    supportsConfigurableCreatePolicy = true,
                    supportsFairyGuiIntegrationPlan = true
                },
                jsonOptions);
            return true;
        }

        if (path == "/api/authoring/ui-art/resources/libraries" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, UiArtResourceLibraryService.ListLibraries(rootPath), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/ui-art/resources/state" && context.Request.HttpMethod == "GET")
        {
            string library = context.Request.QueryString["library"] ?? string.Empty;
            WriteJson(context.Response, UiArtResourceLibraryService.ReadAuthoringState(rootPath, library), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/ui-art/resources/fairygui-packages" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, UiArtResourceFairyGuiIntegrationService.ListTargetPackages(rootPath), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/ui-art/resources/plan-fairygui-import" && context.Request.HttpMethod == "POST")
        {
            UiArtFairyGuiIntegrationRequest request = ReadFairyGuiArtSliceRequest<UiArtFairyGuiIntegrationRequest>(context, jsonOptions);
            UiArtFairyGuiIntegrationPlan plan = UiArtResourceFairyGuiIntegrationService.PlanIntegration(
                rootPath,
                request.library,
                request.resourceId,
                request.targetPackage);
            WriteJson(
                context.Response,
                new
                {
                    success = !plan.Blocked,
                    plan,
                    jsonRecipe = UiArtResourceFairyGuiIntegrationService.GenerateJsonRecipe(plan),
                    markdownRecipe = UiArtResourceFairyGuiIntegrationService.GenerateMarkdownRecipe(plan)
                },
                jsonOptions,
                plan.Blocked ? 409 : 200);
            return true;
        }

        if (path == "/api/authoring/ui-art/resources/save" && context.Request.HttpMethod == "POST")
        {
            UiArtResourceSaveRequest request = ReadFairyGuiArtSliceRequest<UiArtResourceSaveRequest>(context, jsonOptions);
            FairyGuiArtSliceOperationPlan plan = UiArtResourceLibraryService.PlanSaveCatalogSemantics(
                rootPath,
                request.library,
                request.expectedCatalogHash,
                request.catalog);

            if (plan.Blocked)
            {
                WriteJson(context.Response, new { success = false, plan }, jsonOptions, 409);
                return true;
            }

            UiArtResourceLibraryService.SaveCatalogSemantics(
                rootPath,
                request.library,
                request.expectedCatalogHash,
                request.catalog);
            UiArtResourceLibraryDescriptor descriptor = UiArtResourceLibraryService.ResolveLibraryDescriptor(rootPath, request.library);
            WriteJson(
                context.Response,
                new
                {
                    success = true,
                    plan,
                    state = UiArtResourceLibraryService.ReadAuthoringState(rootPath, descriptor.LibraryId)
                },
                jsonOptions);
            return true;
        }

        if (path == "/api/authoring/ui-art/resources/plan-replace" && context.Request.HttpMethod == "POST")
        {
            UiArtResourceReplaceRequest request = ReadFairyGuiArtSliceRequest<UiArtResourceReplaceRequest>(context, jsonOptions);
            byte[] bytes = DecodeBase64Bytes(request.bytesBase64);
            FairyGuiArtSliceOperationPlan plan = UiArtResourceLibraryService.PlanReplaceExistingPng(
                rootPath,
                request.library,
                request.expectedCatalogHash,
                request.resourceId,
                bytes);

            WriteJson(context.Response, new { success = !plan.Blocked, plan }, jsonOptions, plan.Blocked ? 409 : 200);
            return true;
        }

        if (path == "/api/authoring/ui-art/resources/replace-png" && context.Request.HttpMethod == "POST")
        {
            UiArtResourceReplaceRequest request = ReadFairyGuiArtSliceRequest<UiArtResourceReplaceRequest>(context, jsonOptions);
            byte[] bytes = DecodeBase64Bytes(request.bytesBase64);
            FairyGuiArtSliceOperationPlan plan = UiArtResourceLibraryService.PlanReplaceExistingPng(
                rootPath,
                request.library,
                request.expectedCatalogHash,
                request.resourceId,
                bytes);

            if (plan.Blocked)
            {
                WriteJson(context.Response, new { success = false, plan }, jsonOptions, 409);
                return true;
            }

            UiArtResourceLibraryService.ReplaceExistingPng(rootPath, request.library, request.expectedCatalogHash, request.resourceId, bytes);
            UiArtResourceLibraryDescriptor descriptor = UiArtResourceLibraryService.ResolveLibraryDescriptor(rootPath, request.library);
            WriteJson(
                context.Response,
                new
                {
                    success = true,
                    plan,
                    state = UiArtResourceLibraryService.ReadAuthoringState(rootPath, descriptor.LibraryId)
                },
                jsonOptions);
            return true;
        }

        if (path == "/api/authoring/ui-art/resources/plan-create" && context.Request.HttpMethod == "POST")
        {
            UiArtResourceCreateRequest request = ReadFairyGuiArtSliceRequest<UiArtResourceCreateRequest>(context, jsonOptions);
            byte[] bytes = DecodeBase64Bytes(request.bytesBase64);
            FairyGuiArtSliceOperationPlan plan = UiArtResourceLibraryService.PlanCreateResource(
                rootPath,
                request.library,
                request.expectedCatalogHash,
                request.resource,
                bytes);

            WriteJson(context.Response, new { success = !plan.Blocked, plan }, jsonOptions, plan.Blocked ? 409 : 200);
            return true;
        }

        if (path == "/api/authoring/ui-art/resources/create" && context.Request.HttpMethod == "POST")
        {
            UiArtResourceCreateRequest request = ReadFairyGuiArtSliceRequest<UiArtResourceCreateRequest>(context, jsonOptions);
            byte[] bytes = DecodeBase64Bytes(request.bytesBase64);
            FairyGuiArtSliceOperationPlan plan = UiArtResourceLibraryService.PlanCreateResource(
                rootPath,
                request.library,
                request.expectedCatalogHash,
                request.resource,
                bytes);

            if (plan.Blocked)
            {
                WriteJson(context.Response, new { success = false, plan }, jsonOptions, 409);
                return true;
            }

            UiArtResourceLibraryService.CreateResource(rootPath, request.library, request.expectedCatalogHash, request.resource, bytes);
            UiArtResourceLibraryDescriptor descriptor = UiArtResourceLibraryService.ResolveLibraryDescriptor(rootPath, request.library);
            WriteJson(
                context.Response,
                new
                {
                    success = true,
                    plan,
                    state = UiArtResourceLibraryService.ReadAuthoringState(rootPath, descriptor.LibraryId)
                },
                jsonOptions);
            return true;
        }

        if (path == "/api/authoring/ui-art/resources/plan-draft" && context.Request.HttpMethod == "POST")
        {
            UiArtResourceDraftRequest request = ReadFairyGuiArtSliceRequest<UiArtResourceDraftRequest>(context, jsonOptions);
            FairyGuiArtSliceOperationPlan plan = UiArtResourceLibraryService.PlanRegisterDraftResource(
                rootPath,
                request.library,
                request.expectedCatalogHash,
                request.resourceId,
                request.relativePath);
            WriteJson(context.Response, new { success = !plan.Blocked, plan }, jsonOptions, plan.Blocked ? 409 : 200);
            return true;
        }

        if (path == "/api/authoring/fairygui/art-slices/packages" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, FairyGuiArtSliceCatalogService.ListPackages(rootPath), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/fairygui/art-slices/state" && context.Request.HttpMethod == "GET")
        {
            string package = context.Request.QueryString["package"] ?? string.Empty;
            WriteJson(context.Response, FairyGuiArtSliceCatalogService.ReadAuthoringState(rootPath, package), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/fairygui/art-slices/save" && context.Request.HttpMethod == "POST")
        {
            FairyGuiArtSliceSaveRequest request = ReadFairyGuiArtSliceRequest<FairyGuiArtSliceSaveRequest>(context, jsonOptions);
            FairyGuiArtSliceOperationPlan plan = FairyGuiArtSliceCatalogService.PlanSaveCatalogSemantics(
                rootPath,
                request.package,
                request.expectedCatalogHash,
                request.catalog);

            if (plan.Blocked)
            {
                WriteJson(context.Response, new { success = false, plan }, jsonOptions, 409);
                return true;
            }

            FairyGuiArtSlicePackageDescriptor descriptor = FairyGuiArtSliceCatalogService.ResolvePackageDescriptor(rootPath, request.package);
            FairyGuiArtSliceCatalogService.WriteCatalogFile(rootPath, descriptor.CatalogPath, request.catalog);
            WriteJson(
                context.Response,
                new
                {
                    success = true,
                    plan,
                    state = FairyGuiArtSliceCatalogService.ReadAuthoringState(rootPath, descriptor.PackageId)
                },
                jsonOptions);
            return true;
        }

        if (path == "/api/authoring/fairygui/art-slices/plan-replace" && context.Request.HttpMethod == "POST")
        {
            FairyGuiArtSliceReplaceRequest request = ReadFairyGuiArtSliceRequest<FairyGuiArtSliceReplaceRequest>(context, jsonOptions);
            byte[] bytes = DecodeBase64Bytes(request.bytesBase64);
            FairyGuiArtSliceOperationPlan plan = FairyGuiArtSliceCatalogService.PlanReplaceExistingPng(
                rootPath,
                request.package,
                request.expectedCatalogHash,
                request.sliceId,
                bytes);

            WriteJson(context.Response, new { success = !plan.Blocked, plan }, jsonOptions, plan.Blocked ? 409 : 200);
            return true;
        }

        if (path == "/api/authoring/fairygui/art-slices/replace-png" && context.Request.HttpMethod == "POST")
        {
            FairyGuiArtSliceReplaceRequest request = ReadFairyGuiArtSliceRequest<FairyGuiArtSliceReplaceRequest>(context, jsonOptions);
            byte[] bytes = DecodeBase64Bytes(request.bytesBase64);
            FairyGuiArtSliceOperationPlan plan = FairyGuiArtSliceCatalogService.PlanReplaceExistingPng(
                rootPath,
                request.package,
                request.expectedCatalogHash,
                request.sliceId,
                bytes);

            if (plan.Blocked)
            {
                WriteJson(context.Response, new { success = false, plan }, jsonOptions, 409);
                return true;
            }

            FairyGuiArtSliceCatalogService.ReplaceExistingPng(rootPath, request.package, request.expectedCatalogHash, request.sliceId, bytes);
            FairyGuiArtSlicePackageDescriptor descriptor = FairyGuiArtSliceCatalogService.ResolvePackageDescriptor(rootPath, request.package);
            WriteJson(
                context.Response,
                new
                {
                    success = true,
                    plan,
                    state = FairyGuiArtSliceCatalogService.ReadAuthoringState(rootPath, descriptor.PackageId)
                },
                jsonOptions);
            return true;
        }

        if (path == "/api/authoring/fairygui/art-slices/plan-draft" && context.Request.HttpMethod == "POST")
        {
            FairyGuiArtSliceDraftRequest request = ReadFairyGuiArtSliceRequest<FairyGuiArtSliceDraftRequest>(context, jsonOptions);
            FairyGuiArtSliceOperationPlan plan = FairyGuiArtSliceCatalogService.PlanRegisterDraftSlice(
                rootPath,
                request.package,
                request.expectedCatalogHash,
                request.sliceId,
                request.expectedSourcePath);
            WriteJson(context.Response, new { success = !plan.Blocked, plan }, jsonOptions, plan.Blocked ? 409 : 200);
            return true;
        }

        return false;
    }

    private static T ReadFairyGuiArtSliceRequest<T>(HttpListenerContext context, JsonSerializerOptions jsonOptions)
        where T : new()
    {
        string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
        return string.IsNullOrWhiteSpace(body)
            ? new T()
            : (JsonSerializer.Deserialize<T>(body, jsonOptions) ?? new T());
    }

    private static byte[] DecodeBase64Bytes(string value)
    {
        string text = value ?? string.Empty;
        int comma = text.IndexOf(',');
        if (comma >= 0)
            text = text.Substring(comma + 1);

        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<byte>();

        return Convert.FromBase64String(text);
    }

    private sealed class FairyGuiArtSliceSaveRequest
    {
        public string package { get; set; } = string.Empty;
        public string expectedCatalogHash { get; set; } = string.Empty;
        public FairyGuiArtSliceCatalog catalog { get; set; }
    }

    private sealed class FairyGuiArtSliceReplaceRequest
    {
        public string package { get; set; } = string.Empty;
        public string expectedCatalogHash { get; set; } = string.Empty;
        public string sliceId { get; set; } = string.Empty;
        public string bytesBase64 { get; set; } = string.Empty;
    }

    private sealed class FairyGuiArtSliceDraftRequest
    {
        public string package { get; set; } = string.Empty;
        public string expectedCatalogHash { get; set; } = string.Empty;
        public string sliceId { get; set; } = string.Empty;
        public string expectedSourcePath { get; set; } = string.Empty;
    }

    private sealed class UiArtResourceSaveRequest
    {
        public string library { get; set; } = string.Empty;
        public string expectedCatalogHash { get; set; } = string.Empty;
        public UiArtResourceLibraryCatalog catalog { get; set; }
    }

    private sealed class UiArtResourceReplaceRequest
    {
        public string library { get; set; } = string.Empty;
        public string expectedCatalogHash { get; set; } = string.Empty;
        public string resourceId { get; set; } = string.Empty;
        public string bytesBase64 { get; set; } = string.Empty;
    }

    private sealed class UiArtResourceCreateRequest
    {
        public string library { get; set; } = string.Empty;
        public string expectedCatalogHash { get; set; } = string.Empty;
        public UiArtResource resource { get; set; }
        public string bytesBase64 { get; set; } = string.Empty;
    }

    private sealed class UiArtResourceDraftRequest
    {
        public string library { get; set; } = string.Empty;
        public string expectedCatalogHash { get; set; } = string.Empty;
        public string resourceId { get; set; } = string.Empty;
        public string relativePath { get; set; } = string.Empty;
    }

    private sealed class UiArtFairyGuiIntegrationRequest
    {
        public string library { get; set; } = string.Empty;
        public string resourceId { get; set; } = string.Empty;
        public string targetPackage { get; set; } = string.Empty;
    }
}
