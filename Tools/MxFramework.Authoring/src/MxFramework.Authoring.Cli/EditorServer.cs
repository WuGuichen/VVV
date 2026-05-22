using System.Net;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Preview;
using MxFramework.Authoring.Preview.Protocol;

namespace MxFramework.Authoring.Cli;

internal static class EditorServer
{
    private const string DefaultPackageRelativePath = "Tools/MxFramework.Authoring/samples/buff-preview";
    private const string DefaultCharacterPackageRelativePath = "Tools/MxFramework.Authoring/samples/character-iron-vanguard";
    private const string AnimationAuthoringDocumentRelativePath = "config/animation_authoring.json";
    private const string ManifestRelativePath = "Tools/MxFramework.Authoring/samples/project-manifest/project-authoring-manifest.json";
    private const string SamplesRelativePath = "Tools/MxFramework.Authoring/samples";
    private static string s_rootPath = string.Empty;
    private static string s_defaultPackage = string.Empty;
    private static int s_port;
    private static int s_restartScheduled;

    public static int Serve(string root, int port, JsonSerializerOptions jsonOptions, string defaultPackageRelativePath = DefaultPackageRelativePath)
    {
        string rootPath = Path.GetFullPath(root);
        string defaultPackage = string.IsNullOrEmpty(defaultPackageRelativePath) ? DefaultPackageRelativePath : defaultPackageRelativePath;
        s_rootPath = rootPath;
        s_defaultPackage = defaultPackage;
        s_port = port;
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        Console.WriteLine("MxFramework Authoring Editor");
        Console.WriteLine($"Hub URL: http://127.0.0.1:{port}/Tools/MxFramework.EditorHub/web/");
        Console.WriteLine($"Authoring Editor URL: http://127.0.0.1:{port}/Tools/MxFramework.Authoring.Editor/web/");
        Console.WriteLine($"CharacterStudio URL: http://127.0.0.1:{port}/Tools/MxFramework.CharacterStudio/web/");
        Console.WriteLine($"Default package: {defaultPackage}");

        while (true)
        {
            HttpListenerContext context = listener.GetContext();
            try
            {
                Handle(context, rootPath, defaultPackage, jsonOptions);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("request error: " + ex);
                try
                {
                    WriteJson(context.Response, new { error = ex.Message }, jsonOptions, 500);
                }
                catch
                {
                }
            }
        }
    }

    private static void Handle(HttpListenerContext context, string rootPath, string defaultPackage, JsonSerializerOptions jsonOptions)
    {
        string path = context.Request.Url?.AbsolutePath ?? "/";
        if (path == "/")
        {
            context.Response.Redirect("/Tools/MxFramework.EditorHub/web/");
            context.Response.Close();
            return;
        }

        string packageRelative = ResolvePackageRelative(context, rootPath, defaultPackage);

        if (path == "/api/packages" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, ListPackages(rootPath), jsonOptions);
            return;
        }

        if (path == "/api/editor/restart" && context.Request.HttpMethod == "POST")
        {
            ScheduleEditorRestart(rootPath, port: s_port, defaultPackage);
            WriteJson(context.Response, new
            {
                restarting = true,
                port = s_port,
                package = defaultPackage
            }, jsonOptions, 202);
            return;
        }

        if (path == "/api/character/packages" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, ListCharacterPackages(rootPath, defaultPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/authoring/animation/packages" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, ListAnimationPackages(rootPath, defaultPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/authoring/animation/load" && context.Request.HttpMethod == "GET")
        {
            string animationPackage = context.Request.QueryString["package"] ?? string.Empty;
            WriteJson(context.Response, ReadAnimationPackageState(rootPath, animationPackage, defaultPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/authoring/animation/save" && context.Request.HttpMethod == "POST")
        {
            AnimationAuthoringSaveRequest request = ReadAnimationAuthoringSaveRequest(context, jsonOptions);
            string animationPackage = !string.IsNullOrWhiteSpace(request.package)
                ? request.package
                : context.Request.QueryString["package"] ?? string.Empty;
            WriteJson(context.Response, SaveAnimationPackage(rootPath, animationPackage, defaultPackage, request.animation, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/authoring/animation/validate" && context.Request.HttpMethod == "POST")
        {
            AnimationAuthoringSaveRequest request = ReadAnimationAuthoringSaveRequest(context, jsonOptions);
            AnimationAuthoringPackage package = request.animation;
            if (package == null)
            {
                string animationPackage = !string.IsNullOrWhiteSpace(request.package)
                    ? request.package
                    : context.Request.QueryString["package"] ?? string.Empty;
                package = ReadAnimationPackage(rootPath, ResolveAnimationDocumentPath(rootPath, animationPackage, defaultPackage, jsonOptions), jsonOptions);
            }
            WriteJson(context.Response, ValidateAnimationPackage(package), jsonOptions);
            return;
        }

        if (path == "/api/authoring/animation/compile" && context.Request.HttpMethod == "POST")
        {
            AnimationAuthoringSaveRequest request = ReadAnimationAuthoringSaveRequest(context, jsonOptions);
            string animationPackage = !string.IsNullOrWhiteSpace(request.package)
                ? request.package
                : context.Request.QueryString["package"] ?? string.Empty;
            WriteJson(context.Response, CompileAnimationPackage(rootPath, animationPackage, defaultPackage, request.animation, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/authoring/animation/preview" && (context.Request.HttpMethod == "GET" || context.Request.HttpMethod == "POST"))
        {
            AnimationAuthoringSaveRequest request = context.Request.HttpMethod == "POST"
                ? ReadAnimationAuthoringSaveRequest(context, jsonOptions)
                : new AnimationAuthoringSaveRequest();
            string animationPackage = !string.IsNullOrWhiteSpace(request.package)
                ? request.package
                : context.Request.QueryString["package"] ?? string.Empty;
            WriteJson(context.Response, ReadAnimationPreview(rootPath, animationPackage, defaultPackage, request.animation, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/state" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadCharacterState(rootPath, characterPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/resources" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadCharacterResources(rootPath, characterPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/resources/inspect" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            string id = context.Request.QueryString["id"] ?? string.Empty;
            object result = ReadCharacterResourceInspect(rootPath, characterPackage, id, jsonOptions);
            if (result == null)
            {
                WriteJson(context.Response, new
                {
                    error = "RESOURCE_LIBRARY_ITEM_NOT_FOUND",
                    message = "Resource library item was not found.",
                    id
                }, jsonOptions, 404);
                return;
            }

            WriteJson(context.Response, result, jsonOptions);
            return;
        }

        if (path == "/api/authoring/resources" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadAuthoringResources(rootPath, characterPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/authoring/resources/inspect" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            string id = context.Request.QueryString["id"] ?? string.Empty;
            object result = ReadAuthoringResourceInspect(rootPath, characterPackage, id, jsonOptions);
            if (result == null)
            {
                WriteJson(context.Response, new
                {
                    error = "AUTH_RES_ITEM_MISSING",
                    message = "Authoring resource item was not found.",
                    id
                }, jsonOptions, 404);
                return;
            }

            WriteJson(context.Response, result, jsonOptions);
            return;
        }

        if (path == "/api/authoring/resources/references" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadAuthoringResources(rootPath, characterPackage, jsonOptions).ReferenceGraph, jsonOptions);
            return;
        }

        if (path == "/api/authoring/resources/providers" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadAuthoringResources(rootPath, characterPackage, jsonOptions).Providers, jsonOptions);
            return;
        }

        if (path == "/api/authoring/resources/stage-import" && context.Request.HttpMethod == "POST")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            ExternalImportStageRequest request = ReadExternalImportStageRequest(context, jsonOptions);
            if (!string.IsNullOrWhiteSpace(request.package))
                characterPackage = request.package;
            WriteJson(context.Response, StageExternalImport(rootPath, characterPackage, request, jsonOptions), jsonOptions);
            return;
        }

        if ((path == "/api/authoring/resources/pick" || path == "/api/authoring/resources/resolve-selection") && context.Request.HttpMethod == "POST")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            AuthoringResourceSelectionRequest request = JsonSerializer.Deserialize<AuthoringResourceSelectionRequest>(body, jsonOptions) ?? new AuthoringResourceSelectionRequest();
            if (!string.IsNullOrWhiteSpace(request.package))
                characterPackage = request.package;
            AuthoringResourceCollection resources = ReadAuthoringResources(rootPath, characterPackage, jsonOptions);
            var service = new AuthoringResourceSelectionService();
            object result = path == "/api/authoring/resources/pick"
                ? service.Query(resources, request.fieldSpec, request.context)
                : service.Resolve(resources, request.fieldSpec, request.context, request.selection);
            WriteJson(context.Response, result, jsonOptions);
            return;
        }

        if ((path == "/api/authoring/resources/import" || path == "/api/character/resources/import") && context.Request.HttpMethod == "POST")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            ResourceLibraryWriteRequest request = ReadResourceLibraryWriteRequest(context, jsonOptions);
            if (!string.IsNullOrWhiteSpace(request.package))
                characterPackage = request.package;
            try
            {
                WriteJson(context.Response, ImportCharacterResource(rootPath, characterPackage, request, jsonOptions), jsonOptions);
            }
            catch (ResourceLibraryWriteException ex)
            {
                WriteJson(context.Response, BuildResourceLibraryWriteError(ex.Code, ex.Message), jsonOptions, ex.StatusCode);
            }
            catch (ArgumentException ex)
            {
                WriteJson(context.Response, BuildResourceLibraryWriteError("RESOURCE_LIBRARY_WRITE_INVALID_REQUEST", ex.Message), jsonOptions, 400);
            }
            return;
        }

        if ((path == "/api/authoring/resources/reimport" || path == "/api/character/resources/reimport") && context.Request.HttpMethod == "POST")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            ResourceLibraryWriteRequest request = ReadResourceLibraryWriteRequest(context, jsonOptions);
            if (!string.IsNullOrWhiteSpace(request.package))
                characterPackage = request.package;
            try
            {
                WriteJson(context.Response, ReimportCharacterResource(rootPath, characterPackage, request, jsonOptions), jsonOptions);
            }
            catch (ResourceLibraryWriteException ex)
            {
                WriteJson(context.Response, BuildResourceLibraryWriteError(ex.Code, ex.Message), jsonOptions, ex.StatusCode);
            }
            catch (ArgumentException ex)
            {
                WriteJson(context.Response, BuildResourceLibraryWriteError("RESOURCE_LIBRARY_WRITE_INVALID_REQUEST", ex.Message), jsonOptions, 400);
            }
            return;
        }

        if ((path == "/api/authoring/resources/replace-source" || path == "/api/character/resources/replace-source") && context.Request.HttpMethod == "POST")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            ResourceLibraryWriteRequest request = ReadResourceLibraryWriteRequest(context, jsonOptions);
            if (!string.IsNullOrWhiteSpace(request.package))
                characterPackage = request.package;
            try
            {
                WriteJson(context.Response, ReplaceCharacterResourceSource(rootPath, characterPackage, request, jsonOptions), jsonOptions);
            }
            catch (ResourceLibraryWriteException ex)
            {
                WriteJson(context.Response, BuildResourceLibraryWriteError(ex.Code, ex.Message), jsonOptions, ex.StatusCode);
            }
            catch (ArgumentException ex)
            {
                WriteJson(context.Response, BuildResourceLibraryWriteError("RESOURCE_LIBRARY_WRITE_INVALID_REQUEST", ex.Message), jsonOptions, 400);
            }
            return;
        }

        if ((path == "/api/authoring/resources/resource-plan" || path == "/api/character/resource-plan") && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            bool checkHashes = string.Equals(context.Request.QueryString["checkHashes"], "true", StringComparison.OrdinalIgnoreCase);
            WriteJson(context.Response, ReadCharacterResourcePlan(rootPath, characterPackage, checkFiles: true, checkHashes: checkHashes, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/create" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterStudioCreateRequest request = JsonSerializer.Deserialize<CharacterStudioCreateRequest>(body, jsonOptions) ?? new CharacterStudioCreateRequest();
            object created = CreateCharacterPackage(rootPath, request, jsonOptions);
            WriteJson(context.Response, created, jsonOptions, 201);
            return;
        }

        if (path == "/api/character/save" && context.Request.HttpMethod == "POST")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterStudioSaveRequest request = JsonSerializer.Deserialize<CharacterStudioSaveRequest>(body, jsonOptions) ?? new CharacterStudioSaveRequest();
            if (request.package == null)
                throw new ArgumentException("package is required.");
            SaveCharacterPackage(rootPath, characterPackage, request.package, jsonOptions);
            WriteJson(context.Response, ReadCharacterState(rootPath, characterPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/import-model" && context.Request.HttpMethod == "POST")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterStudioModelImportRequest request = JsonSerializer.Deserialize<CharacterStudioModelImportRequest>(body, jsonOptions) ?? new CharacterStudioModelImportRequest();
            ImportCharacterModel(rootPath, characterPackage, request, jsonOptions);
            WriteJson(context.Response, ReadCharacterState(rootPath, characterPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/compile" && (context.Request.HttpMethod == "GET" || context.Request.HttpMethod == "POST"))
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            bool checkHashes = string.Equals(context.Request.QueryString["checkHashes"], "true", StringComparison.OrdinalIgnoreCase);
            WriteJson(context.Response, CompileCharacterPackage(rootPath, characterPackage, checkFiles: true, checkHashes: checkHashes, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/import-unity" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CharacterStudioImportRequest request = string.IsNullOrWhiteSpace(body)
                ? new CharacterStudioImportRequest()
                : (JsonSerializer.Deserialize<CharacterStudioImportRequest>(body, jsonOptions) ?? new CharacterStudioImportRequest());
            string characterPackage = string.IsNullOrWhiteSpace(request.package)
                ? ResolveCharacterPackageRelative(context, rootPath, defaultPackage)
                : request.package;
            ResolveSafePath(rootPath, characterPackage);
            WriteJson(context.Response, RunCharacterImport(rootPath, characterPackage, request, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/state" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, ReadState(rootPath, packageRelative), jsonOptions);
            return;
        }

        if (path == "/api/patch" && context.Request.HttpMethod == "POST")
        {
            PatchDocument patch = JsonSerializer.Deserialize<PatchDocument>(
                new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd(),
                jsonOptions) ?? new PatchDocument();
            SavePatch(rootPath, packageRelative, patch, jsonOptions);
            WriteJson(context.Response, ReadState(rootPath, packageRelative), jsonOptions);
            return;
        }

        if (path == "/api/report" && context.Request.HttpMethod == "POST")
        {
            WriteReport(rootPath, packageRelative);
            WriteJson(context.Response, ReadState(rootPath, packageRelative), jsonOptions);
            return;
        }

        if (path == "/api/preview/status" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, ReadPreviewStatus(jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/preview/run" && context.Request.HttpMethod == "POST")
        {
            string buffId = context.Request.QueryString["buff"] ?? string.Empty;
            string targetId = context.Request.QueryString["target"] ?? "TestTarget";
            string casterId = context.Request.QueryString["caster"] ?? "TestCaster";
            int stack = int.TryParse(context.Request.QueryString["stack"], out int parsedStack) ? parsedStack : 1;
            int waitTicks = int.TryParse(context.Request.QueryString["waitTicks"], out int parsedTicks) ? parsedTicks : 60;
            WriteJson(context.Response, RunPreview(rootPath, packageRelative, buffId, casterId, targetId, stack, waitTicks, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/mod/diagnose" && (context.Request.HttpMethod == "GET" || context.Request.HttpMethod == "POST"))
        {
            ModDiagnoseRequest request = ReadDiagnoseRequest(context, rootPath);
            if (request.containers.Count == 0)
                request.containers.Add("Assets/StreamingAssets/MxFramework/Demo");
            ModDiagnosticSnapshotDto snapshot = ModDiagnosticService.BuildSnapshot(
                request.containers.ToArray(),
                request.loadout,
                request.includeAbsolutePaths);
            WriteJson(context.Response, snapshot, jsonOptions);
            return;
        }

        if (path == "/api/runtime-patch/export" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            ExportRequest exportReq;

            try
            {
                exportReq = JsonSerializer.Deserialize<ExportRequest>(body, jsonOptions) ?? new ExportRequest();
            }
            catch
            {
                exportReq = new ExportRequest();
            }

            string effectivePackage = ResolvePackageRelative(context, rootPath, defaultPackage);
            string effectiveSource = string.IsNullOrEmpty(exportReq.sourceId) ? "authoring_export" : exportReq.sourceId;
            string effectiveLayer = string.IsNullOrEmpty(exportReq.layer) ? "Patch" : exportReq.layer;

            PackageReadResult package = PackageReader.Read(ResolveSafePath(rootPath, effectivePackage));
            AuthoringRuntimePatchExporter.ExportResult exportResult =
                AuthoringRuntimePatchExporter.Export(package.Patches, effectiveSource, effectiveLayer, package.Manifest?.PackageId);

            if (!exportResult.Success)
            {
                WriteJson(context.Response, new
                {
                    success = false,
                    errors = exportResult.Errors,
                    outputPath = ""
                }, jsonOptions);
                return;
            }

            string outputPath = Path.Combine(rootPath, "Assets", "StreamingAssets", "MxFramework", "Demo", "runtime_config_patch.json");
            string dir = Path.GetDirectoryName(outputPath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(outputPath, exportResult.Json);

            WriteJson(context.Response, new
            {
                success = true,
                errors = new object[0],
                outputPath = "Assets/StreamingAssets/MxFramework/Demo/runtime_config_patch.json",
                format = "mx.runtimeConfigPatch.v1",
                buffCount = CountBuffs(exportResult.Json),
                modCount = CountMods(exportResult.Json)
            }, jsonOptions);
            return;
        }

        if (path == "/api/validate-draft" && context.Request.HttpMethod == "POST")
        {
            PatchDocument draft = JsonSerializer.Deserialize<PatchDocument>(
                new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd(),
                jsonOptions) ?? new PatchDocument();
            ValidationReport issues = ValidateDraft(rootPath, packageRelative, draft);
            WriteJson(context.Response, new { issues = issues.Issues }, jsonOptions);
            return;
        }

        if (path == "/api/ai-context" && context.Request.HttpMethod == "GET")
        {
            string workflowId = context.Request.QueryString["workflow"] ?? "buff.create";
            string stepId = context.Request.QueryString["step"] ?? string.Empty;
            string mode = NormalizeMode(context.Request.QueryString["mode"]);
            AuthoringWorkflow workflow = BuiltInContent.GetWorkflow(workflowId);
            if (workflow == null)
            {
                WriteText(context.Response, "workflow not found", "text/plain; charset=utf-8", 404);
                return;
            }
            string manifestPath = Path.Combine(rootPath, ManifestRelativePath);
            string packagePath = ResolveSafePath(rootPath, packageRelative);
            ProjectAuthoringManifest manifest = File.Exists(manifestPath) ? ManifestReader.Read(manifestPath) : null;
            PackageReadResult package = PackageReader.Read(packagePath);
            string text = "uiMode=" + mode + "\n" + workflow.CreateAiStepContext(stepId, manifest, package.Manifest, package.Patches);
            WriteText(context.Response, text, "text/plain; charset=utf-8", 200);
            return;
        }

        if (path == "/api/localization" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, ReadLocalizationPatch(rootPath, packageRelative, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/localization" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            string mode = NormalizeMode(context.Request.QueryString["mode"]);
            try
            {
                SaveLocalizationPatch(rootPath, packageRelative, body, mode, jsonOptions);
            }
            catch (ArgumentException ex)
            {
                WriteJson(context.Response, new { error = ex.Message }, jsonOptions, 400);
                return;
            }
            WriteJson(context.Response, ReadLocalizationPatch(rootPath, packageRelative, jsonOptions), jsonOptions);
            return;
        }

        ServeStatic(context.Response, rootPath, path);
    }

    private static string NormalizeMode(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Mod";
        return raw.Equals("Developer", StringComparison.OrdinalIgnoreCase) ? "Developer" : "Mod";
    }

    private static object ReadLocalizationPatch(string rootPath, string packageRelative, JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        string filePath = Path.Combine(packagePath, "patches", "localization.patch.json");
        if (!File.Exists(filePath))
            return new { schemaVersion = "1.0", source = "Localization", entries = new object[0] };
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(filePath));
        return doc.RootElement.Clone();
    }

    private static void SaveLocalizationPatch(string rootPath, string packageRelative, string body, string mode, JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        string modJsonPath = Path.Combine(packagePath, "mod.json");
        string packageId = string.Empty;
        if (File.Exists(modJsonPath))
        {
            ModPackageManifest m = JsonSerializer.Deserialize<ModPackageManifest>(File.ReadAllText(modJsonPath), jsonOptions) ?? new ModPackageManifest();
            packageId = m.PackageId ?? string.Empty;
        }

        using JsonDocument input = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        if (!input.RootElement.TryGetProperty("entries", out JsonElement entriesEl) || entriesEl.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("entries array is required.");

        string requiredPrefix = string.IsNullOrEmpty(packageId) ? null : ("mod." + packageId + ".");
        var sanitized = new List<object>();
        foreach (JsonElement entry in entriesEl.EnumerateArray())
        {
            string key = entry.TryGetProperty("key", out JsonElement keyEl) ? (keyEl.GetString() ?? string.Empty) : string.Empty;
            string zh = entry.TryGetProperty("zhCN", out JsonElement zhEl) ? (zhEl.GetString() ?? string.Empty) : string.Empty;
            string en = entry.TryGetProperty("enUS", out JsonElement enEl) ? (enEl.GetString() ?? string.Empty) : string.Empty;
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("localization key must not be empty.");
            if (string.Equals(mode, "Mod", StringComparison.OrdinalIgnoreCase) && requiredPrefix != null && !key.StartsWith(requiredPrefix, StringComparison.Ordinal))
                throw new ArgumentException("Mod 模式下 key 必须以 '" + requiredPrefix + "' 开头：" + key);
            sanitized.Add(new { key, zhCN = zh, enUS = en });
        }

        var payload = new
        {
            schemaVersion = "1.0",
            source = "Localization",
            entries = sanitized
        };
        string outPath = Path.Combine(packagePath, "patches", "localization.patch.json");
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, JsonSerializer.Serialize(payload, jsonOptions));
    }

    private static string ResolvePackageRelative(HttpListenerContext context, string rootPath, string defaultPackage)
    {
        string requested = context.Request.QueryString["package"];
        if (string.IsNullOrEmpty(requested))
            return defaultPackage;
        ResolveSafePath(rootPath, requested);
        return requested;
    }

    private static string ResolveCharacterPackageRelative(HttpListenerContext context, string rootPath, string defaultPackage)
    {
        string requested = context.Request.QueryString["package"];
        if (string.IsNullOrEmpty(requested))
            return IsCharacterPackage(rootPath, defaultPackage) ? defaultPackage : DefaultCharacterPackageRelativePath;
        ResolveSafePath(rootPath, requested);
        return requested;
    }

    private static string ResolveSafePath(string rootPath, string relative)
    {
        string normalizedRoot = Path.GetFullPath(rootPath);
        string rootPrefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(normalizedRoot, relative));
        if (!string.Equals(fullPath, normalizedRoot, StringComparison.OrdinalIgnoreCase)
            && !fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("path escapes root: " + relative);
        return fullPath;
    }

    private static ModDiagnoseRequest ReadDiagnoseRequest(HttpListenerContext context, string rootPath)
    {
        var request = new ModDiagnoseRequest();

        string[] queryContainers = context.Request.QueryString.GetValues("container") ?? Array.Empty<string>();
        foreach (string value in queryContainers)
        {
            if (!string.IsNullOrWhiteSpace(value))
                request.containers.Add(value.Trim());
        }
        string containersCsv = context.Request.QueryString["containers"] ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(containersCsv))
        {
            foreach (string part in containersCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    request.containers.Add(trimmed);
            }
        }

        string queryLoadout = context.Request.QueryString["loadout"] ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(queryLoadout))
            request.loadout = queryLoadout.Trim();

        string includeAbsolute = context.Request.QueryString["includeAbsolutePaths"] ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(includeAbsolute) && bool.TryParse(includeAbsolute, out bool parsedInclude))
            request.includeAbsolutePaths = parsedInclude;

        if (context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            if (!string.IsNullOrWhiteSpace(body))
            {
                ModDiagnoseRequest post = JsonSerializer.Deserialize<ModDiagnoseRequest>(body) ?? new ModDiagnoseRequest();
                if (post.containers != null && post.containers.Count > 0)
                    request.containers = post.containers;
                if (!string.IsNullOrWhiteSpace(post.loadout))
                    request.loadout = post.loadout;
                request.includeAbsolutePaths = post.includeAbsolutePaths;
            }
        }

        var normalizedContainers = new List<string>();
        foreach (string container in request.containers)
        {
            if (string.IsNullOrWhiteSpace(container))
                continue;
            normalizedContainers.Add(NormalizePathInput(rootPath, container.Trim()));
        }
        request.containers = normalizedContainers;
        request.loadout = string.IsNullOrWhiteSpace(request.loadout) ? string.Empty : NormalizePathInput(rootPath, request.loadout.Trim());

        return request;
    }

    private static string NormalizePathInput(string rootPath, string path)
    {
        string fullPath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(rootPath, path));
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("path escapes root: " + path);
        return fullPath;
    }

    private static List<object> ListPackages(string rootPath)
    {
        var result = new List<object>();
        string samplesDir = Path.Combine(rootPath, SamplesRelativePath);
        if (!Directory.Exists(samplesDir)) return result;
        foreach (string dir in Directory.GetDirectories(samplesDir).OrderBy(p => p, StringComparer.Ordinal))
        {
            string modJson = Path.Combine(dir, "mod.json");
            if (!File.Exists(modJson)) continue;
            try
            {
                ModPackageManifest m = JsonSerializer.Deserialize<ModPackageManifest>(File.ReadAllText(modJson), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                }) ?? new ModPackageManifest();
                string relative = Path.GetRelativePath(rootPath, dir).Replace('\\', '/');
                result.Add(new { relative, packageId = m.PackageId, kind = m.Kind.ToString() });
            }
            catch
            {
            }
        }
        return result;
    }

    private static List<object> ListCharacterPackages(string rootPath, string defaultPackage, JsonSerializerOptions jsonOptions)
    {
        var result = new List<object>();
        AddCharacterPackageListItem(result, rootPath, defaultPackage, jsonOptions);

        string samplesDir = Path.Combine(rootPath, SamplesRelativePath);
        if (!Directory.Exists(samplesDir))
            return result;

        foreach (string dir in Directory.GetDirectories(samplesDir).OrderBy(p => p, StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(rootPath, dir).Replace('\\', '/');
            AddCharacterPackageListItem(result, rootPath, relative, jsonOptions);
        }

        return result;
    }

    private static void AddCharacterPackageListItem(List<object> result, string rootPath, string relative, JsonSerializerOptions jsonOptions)
    {
        if (string.IsNullOrWhiteSpace(relative))
            return;
        string packagePath;
        try
        {
            packagePath = ResolveSafePath(rootPath, relative);
        }
        catch
        {
            return;
        }

        string manifestPath = Path.Combine(packagePath, "manifest.json");
        string resourceCatalogPath = Path.Combine(packagePath, "resource_catalog.json");
        if (!File.Exists(manifestPath) || !File.Exists(resourceCatalogPath))
            return;

        try
        {
            CharacterPackageManifest manifest = JsonSerializer.Deserialize<CharacterPackageManifest>(File.ReadAllText(manifestPath), jsonOptions) ?? new CharacterPackageManifest();
            if (manifest.Kind != CharacterResourcePackageKind.Character)
                return;

            string normalized = Path.GetRelativePath(rootPath, packagePath).Replace('\\', '/');
            if (result.Any(item => string.Equals(GetAnonymousProperty(item, "relative"), normalized, StringComparison.Ordinal)))
                return;
            result.Add(new
            {
                relative = normalized,
                packageId = manifest.PackageId,
                stableId = manifest.StableId,
                version = manifest.Version,
                kind = manifest.Kind
            });
        }
        catch
        {
        }
    }

    private static bool IsCharacterPackage(string rootPath, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
            return false;
        try
        {
            string packagePath = ResolveSafePath(rootPath, relative);
            return File.Exists(Path.Combine(packagePath, "manifest.json")) && File.Exists(Path.Combine(packagePath, "resource_catalog.json"));
        }
        catch
        {
            return false;
        }
    }

    internal static List<object> ListAnimationPackages(string rootPath, string defaultPackage, JsonSerializerOptions jsonOptions)
    {
        var result = new List<object>();
        AddAnimationPackageListItem(result, rootPath, IsCharacterPackage(rootPath, defaultPackage) ? defaultPackage : DefaultCharacterPackageRelativePath, jsonOptions);

        string samplesDir = Path.Combine(rootPath, SamplesRelativePath);
        if (!Directory.Exists(samplesDir))
            return result;

        foreach (string dir in Directory.GetDirectories(samplesDir).OrderBy(p => p, StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(rootPath, dir).Replace('\\', '/');
            AddAnimationPackageListItem(result, rootPath, relative, jsonOptions);
        }

        return result;
    }

    private static void AddAnimationPackageListItem(List<object> result, string rootPath, string relative, JsonSerializerOptions jsonOptions)
    {
        if (!IsCharacterPackage(rootPath, relative))
            return;

        string packagePath = ResolveSafePath(rootPath, relative);
        string documentPath = Path.Combine(packagePath, AnimationAuthoringDocumentRelativePath);
        CharacterPackageManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<CharacterPackageManifest>(File.ReadAllText(Path.Combine(packagePath, "manifest.json")), jsonOptions) ?? new CharacterPackageManifest();
        }
        catch
        {
            return;
        }

        string normalized = Path.GetRelativePath(rootPath, packagePath).Replace('\\', '/');
        if (result.Any(item => string.Equals(GetAnonymousProperty(item, "relative"), normalized, StringComparison.Ordinal)))
            return;

        AnimationAuthoringPackage animation = File.Exists(documentPath)
            ? ReadAnimationPackage(rootPath, documentPath, jsonOptions)
            : CreateAnimationPackageFromCharacterManifest(manifest);

        result.Add(new
        {
            relative = normalized,
            packageId = string.IsNullOrWhiteSpace(animation.PackageId) ? manifest.PackageId : animation.PackageId,
            characterPackageId = manifest.PackageId,
            stableId = string.IsNullOrWhiteSpace(animation.StableId) ? manifest.StableId : animation.StableId,
            displayName = string.IsNullOrWhiteSpace(animation.DisplayName) ? manifest.DisplayName : animation.DisplayName,
            document = ToProjectRelativePath(rootPath, documentPath),
            exists = File.Exists(documentPath),
            kind = "AnimationAuthoring"
        });
    }

    internal static object ReadAnimationPackageState(string rootPath, string packageOrPath, string defaultPackage, JsonSerializerOptions jsonOptions)
    {
        string documentPath = ResolveAnimationDocumentPath(rootPath, packageOrPath, defaultPackage, jsonOptions);
        AnimationAuthoringPackage package = ReadAnimationPackage(rootPath, documentPath, jsonOptions);
        return new
        {
            packageRelative = ToAnimationPackageRelative(rootPath, documentPath),
            documentRelative = ToProjectRelativePath(rootPath, documentPath),
            exists = File.Exists(documentPath),
            canWrite = true,
            package,
            fieldSpecs = CreateAnimationFieldSpecs(),
            validation = ValidateAnimationPackage(package)
        };
    }

    internal static object SaveAnimationPackage(
        string rootPath,
        string packageOrPath,
        string defaultPackage,
        AnimationAuthoringPackage package,
        JsonSerializerOptions jsonOptions)
    {
        if (package == null)
            throw new ArgumentException("animation package is required.");

        string documentPath = ResolveAnimationDocumentPath(rootPath, packageOrPath, defaultPackage, jsonOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(documentPath)!);
        WriteJsonFileAtomic(documentPath, package, jsonOptions);
        return ReadAnimationPackageState(rootPath, ToProjectRelativePath(rootPath, documentPath), defaultPackage, jsonOptions);
    }

    internal static CharacterAuthoringValidationReport ValidateAnimationPackage(AnimationAuthoringPackage package)
    {
        var report = new CharacterAuthoringValidationReport
        {
            PackageId = package != null ? package.PackageId ?? string.Empty : string.Empty
        };
        if (package == null)
        {
            report.Issues.Add(CreateAnimationValidationIssue("ANIM_PACKAGE_MISSING", "package", string.Empty, "Animation authoring package is missing.", CharacterAuthoringValidationSeverity.Error));
            return report;
        }

        var setIds = new HashSet<string>(StringComparer.Ordinal);
        for (int setIndex = 0; package.Sets != null && setIndex < package.Sets.Count; setIndex++)
        {
            AnimationAuthoringSet set = package.Sets[setIndex];
            if (set == null)
                continue;
            AddDuplicateIssue(report, setIds, set.SetId, "ANIM_DUPLICATE_SET_ID", "sets/" + setIndex, "setId");

            var layerIds = new HashSet<string>(StringComparer.Ordinal);
            for (int layerIndex = 0; set.Layers != null && layerIndex < set.Layers.Count; layerIndex++)
            {
                AnimationLayerAuthoring layer = set.Layers[layerIndex];
                if (layer != null)
                    AddDuplicateIssue(report, layerIds, layer.LayerId, "ANIM_DUPLICATE_LAYER_ID", "sets/" + setIndex + "/layers/" + layerIndex, "layerId");
            }

            var groupIds = new HashSet<string>(StringComparer.Ordinal);
            for (int groupIndex = 0; set.Groups != null && groupIndex < set.Groups.Count; groupIndex++)
            {
                AnimationGroupAuthoring group = set.Groups[groupIndex];
                if (group == null)
                    continue;
                AddDuplicateIssue(report, groupIds, group.GroupId, "ANIM_DUPLICATE_GROUP_ID", "sets/" + setIndex + "/groups/" + groupIndex, "groupId");

                var clipIds = new HashSet<string>(StringComparer.Ordinal);
                for (int clipIndex = 0; group.Clips != null && clipIndex < group.Clips.Count; clipIndex++)
                {
                    AnimationClipMappingAuthoring clip = group.Clips[clipIndex];
                    if (clip == null)
                        continue;
                    string path = "sets/" + setIndex + "/groups/" + groupIndex + "/clips/" + clipIndex;
                    AddDuplicateIssue(report, clipIds, clip.ClipId, "ANIM_DUPLICATE_CLIP_ID", path, "clipId");
                    if (IsSelectionEmpty(clip.SourceSelection))
                    {
                        report.Issues.Add(CreateAnimationValidationIssue(
                            "ANIM_MISSING_SOURCE_SELECTION",
                            path,
                            "sourceSelection",
                            "Animation clip mapping is missing a source ResourceSelectionRef.",
                            CharacterAuthoringValidationSeverity.Error));
                    }
                }

                var blend1DIds = new HashSet<string>(StringComparer.Ordinal);
                for (int blendIndex = 0; group.Blend1D != null && blendIndex < group.Blend1D.Count; blendIndex++)
                {
                    AnimationBlend1DAuthoring blend = group.Blend1D[blendIndex];
                    if (blend != null)
                        AddDuplicateIssue(report, blend1DIds, blend.BlendId, "ANIM_DUPLICATE_BLEND_ID", "sets/" + setIndex + "/groups/" + groupIndex + "/blend1D/" + blendIndex, "blendId");
                }

                var blend2DIds = new HashSet<string>(StringComparer.Ordinal);
                for (int blendIndex = 0; group.Blend2D != null && blendIndex < group.Blend2D.Count; blendIndex++)
                {
                    AnimationBlend2DAuthoring blend = group.Blend2D[blendIndex];
                    if (blend != null)
                        AddDuplicateIssue(report, blend2DIds, blend.BlendId, "ANIM_DUPLICATE_BLEND_ID", "sets/" + setIndex + "/groups/" + groupIndex + "/blend2D/" + blendIndex, "blendId");
                }

                var timelineIds = new HashSet<string>(StringComparer.Ordinal);
                for (int timelineIndex = 0; group.Timelines != null && timelineIndex < group.Timelines.Count; timelineIndex++)
                {
                    AnimationTimelineAuthoring timeline = group.Timelines[timelineIndex];
                    if (timeline == null)
                        continue;
                    AddDuplicateIssue(report, timelineIds, timeline.TimelineId, "ANIM_DUPLICATE_TIMELINE_ID", "sets/" + setIndex + "/groups/" + groupIndex + "/timelines/" + timelineIndex, "timelineId");

                    var eventIds = new HashSet<string>(StringComparer.Ordinal);
                    for (int eventIndex = 0; timeline.Events != null && eventIndex < timeline.Events.Count; eventIndex++)
                    {
                        AnimationTimelineEventAuthoring timelineEvent = timeline.Events[eventIndex];
                        if (timelineEvent != null)
                            AddDuplicateIssue(report, eventIds, timelineEvent.EventId, "ANIM_DUPLICATE_EVENT_ID", "sets/" + setIndex + "/groups/" + groupIndex + "/timelines/" + timelineIndex + "/events/" + eventIndex, "eventId");
                    }
                }
            }
        }

        var profileIds = new HashSet<string>(StringComparer.Ordinal);
        for (int profileIndex = 0; package.Profiles != null && profileIndex < package.Profiles.Count; profileIndex++)
        {
            AnimationAuthoringProfile profile = package.Profiles[profileIndex];
            if (profile == null)
                continue;
            AddDuplicateIssue(report, profileIds, profile.ProfileId, "ANIM_DUPLICATE_PROFILE_ID", "profiles/" + profileIndex, "profileId");
        }

        return report;
    }

    internal static AnimationAuthoringCompileResult CompileAnimationPackage(
        string rootPath,
        string packageOrPath,
        string defaultPackage,
        AnimationAuthoringPackage package,
        JsonSerializerOptions jsonOptions)
    {
        string documentPath = ResolveAnimationDocumentPath(rootPath, packageOrPath, defaultPackage, jsonOptions);
        AnimationAuthoringPackage animation = package ?? ReadAnimationPackage(rootPath, documentPath, jsonOptions);
        string packageRoot = ResolveAnimationPackageRootPath(documentPath);
        CharacterPackageResourceCatalog catalog = ReadAnimationResourceCatalog(packageRoot, jsonOptions);
        return AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest
        {
            Package = animation,
            PackageRootPath = packageRoot,
            ResourceCatalog = catalog,
            RuntimeResourceCatalog = AnimationPackageCommands.ReadProjectRuntimeResourceCatalogs(rootPath, jsonOptions)
        });
    }

    internal static object ReadAnimationPreview(
        string rootPath,
        string packageOrPath,
        string defaultPackage,
        AnimationAuthoringPackage package,
        JsonSerializerOptions jsonOptions)
    {
        string documentPath = ResolveAnimationDocumentPath(rootPath, packageOrPath, defaultPackage, jsonOptions);
        string packageRoot = ResolveAnimationPackageRootPath(documentPath);
        string packageRelative = ToAnimationPackageRelative(rootPath, documentPath);
        AnimationAuthoringCompileResult compile = CompileAnimationPackage(rootPath, packageOrPath, defaultPackage, package, jsonOptions);
        CharacterPackageResourceCatalog catalog = ReadAnimationResourceCatalog(packageRoot, jsonOptions);
        AuthoringResourceCollection authoringResources = TryReadAuthoringResources(rootPath, packageRelative, jsonOptions);

        return new
        {
            package = packageRelative,
            packageRoot = ToProjectRelativePath(rootPath, packageRoot),
            serviceStatus = "ready",
            compileResult = compile,
            animationSetDefinition = compile.AnimationSetDefinition,
            animationClipRegistry = compile.AnimationClipRegistry,
            animationResourcePlan = compile.AnimationResourcePlan,
            animationValidationReport = compile.AnimationValidationReport,
            previewResources = BuildAnimationPreviewResources(rootPath, packageRoot, catalog, compile.AnimationClipRegistry, compile.AnimationSetDefinition),
            unityPreviewReport = BuildAnimationUnityPreviewReport(rootPath, packageRoot, catalog, compile.AnimationClipRegistry, compile.AnimationSetDefinition, authoringResources),
            diagnostics = compile.AnimationValidationReport?.Issues ?? new List<CharacterAuthoringValidationIssue>()
        };
    }

    private static AuthoringResourceCollection TryReadAuthoringResources(string rootPath, string packageRelative, JsonSerializerOptions jsonOptions)
    {
        try
        {
            return ReadAuthoringResources(rootPath, packageRelative, jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static object BuildAnimationPreviewResources(
        string rootPath,
        string packageRoot,
        CharacterPackageResourceCatalog catalog,
        AnimationClipRegistryDocument clipRegistry,
        AnimationSetDefinitionDocument setDefinition)
    {
        var resourcesByKey = new Dictionary<string, object>(StringComparer.Ordinal);
        var entriesByKey = new Dictionary<string, CharacterPackageResourceEntry>(StringComparer.Ordinal);
        for (int i = 0; catalog != null && catalog.Entries != null && i < catalog.Entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = catalog.Entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.ResourceKey))
                continue;

            string fullPath = CharacterPackageResourcePipeline.ResolvePackagePath(packageRoot, entry.RelativePath);
            string projectRelative = !string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath)
                ? ToProjectRelativePath(rootPath, fullPath)
                : string.Empty;
            string url = string.IsNullOrWhiteSpace(projectRelative)
                ? string.Empty
                : "/" + projectRelative.Replace('\\', '/');
            resourcesByKey[entry.ResourceKey] = new
            {
                resourceKey = entry.ResourceKey,
                stableId = entry.StableId,
                kind = entry.TypeId,
                usage = entry.Usage,
                relativePath = entry.RelativePath,
                projectRelativePath = projectRelative,
                url,
                exists = !string.IsNullOrWhiteSpace(projectRelative),
                tags = entry.Tags ?? new List<string>()
            };
            entriesByKey[entry.ResourceKey] = entry;
        }

        var animationClips = new List<object>();
        for (int i = 0; clipRegistry != null && clipRegistry.Clips != null && i < clipRegistry.Clips.Count; i++)
        {
            AnimationClipRegistryEntry clip = clipRegistry.Clips[i];
            resourcesByKey.TryGetValue(clip.RuntimeResourceKey ?? string.Empty, out object resource);
            object previewModelResource = null;
            string previewModelResourceKey = string.Empty;
            if (entriesByKey.TryGetValue(clip.RuntimeResourceKey ?? string.Empty, out CharacterPackageResourceEntry clipResourceEntry))
                previewModelResourceKey = clipResourceEntry.Preview?.PreviewMeshResourceKey ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(previewModelResourceKey))
                resourcesByKey.TryGetValue(previewModelResourceKey, out previewModelResource);
            AnimationSetDefinitionClipRef clipRef = FindAnimationSetDefinitionClip(setDefinition, clip);
            animationClips.Add(new
            {
                setId = clip.SetId,
                groupId = clip.GroupId,
                clipId = clip.ClipId,
                displayName = clip.DisplayName,
                sourceClipName = clip.SourceClipName,
                sourceSubClipId = clip.SourceSubClipId,
                runtimeResourceKey = clip.RuntimeResourceKey,
                loop = clipRef != null && clipRef.Loop,
                speed = clipRef != null ? clipRef.Speed : 1f,
                rootMotionPolicy = clipRef != null ? clipRef.RootMotionPolicy : string.Empty,
                resource,
                animationResource = resource,
                previewModelResourceKey,
                previewModelResource
            });
        }

        return new
        {
            resources = resourcesByKey.Values.ToArray(),
            animationClips
        };
    }

    private static object BuildAnimationUnityPreviewReport(
        string rootPath,
        string packageRoot,
        CharacterPackageResourceCatalog catalog,
        AnimationClipRegistryDocument clipRegistry,
        AnimationSetDefinitionDocument setDefinition,
        AuthoringResourceCollection authoringResources)
    {
        var clipReports = new List<object>();
        for (int i = 0; clipRegistry != null && clipRegistry.Clips != null && i < clipRegistry.Clips.Count; i++)
        {
            AnimationClipRegistryEntry clip = clipRegistry.Clips[i];
            AnimationSetDefinitionClipRef clipRef = FindAnimationSetDefinitionClip(setDefinition, clip);
            CharacterPackageResourceEntry packageEntry = FindPackageResourceEntryByKey(catalog, clip.RuntimeResourceKey);
            CharacterPackageResourceEntry previewModelEntry = FindPackageResourceEntryByKey(catalog, packageEntry?.Preview?.PreviewMeshResourceKey);
            if (previewModelEntry == null)
                previewModelEntry = FindDefaultAnimationPreviewModel(catalog);
            AuthoringResourceItem sourceItem = FindAnimationSourceResource(authoringResources, clip.SourceSelection, clip.RuntimeResourceKey);
            AuthoringResourceProviderBinding unityBinding = GetAuthoringUnityBinding(sourceItem);
            AuthoringResourceProviderBinding runtimeBinding = GetAuthoringRuntimeBinding(sourceItem);
            string unityAssetPath = FirstNonEmpty(
                clip.SourceSelection != null ? clip.SourceSelection.UnityAssetPath : string.Empty,
                unityBinding != null ? unityBinding.UnityAssetPath : string.Empty);
            string unityGuid = FirstNonEmpty(
                clip.SourceSelection != null ? clip.SourceSelection.UnityGuid : string.Empty,
                unityBinding != null ? unityBinding.UnityGuid : string.Empty);
            string runtimeResourceKey = FirstNonEmpty(
                clip.RuntimeResourceKey,
                clip.SourceSelection != null ? clip.SourceSelection.RuntimeResourceKey : string.Empty,
                runtimeBinding != null ? runtimeBinding.RuntimeResourceKey : string.Empty);
            string subClipId = FirstNonEmpty(
                clip.SourceSubClipId,
                GetAuthoringResourceMetadata(sourceItem, "subClipId"),
                GetAuthoringResourceMetadata(sourceItem, "subClipName"),
                GetAuthoringResourceMetadata(sourceItem, "clipName"),
                clip.SourceClipName);
            bool unityAssetExists = IsUnityAssetPathPresent(rootPath, unityAssetPath);
            bool canPreviewInUnity = !string.IsNullOrWhiteSpace(unityAssetPath) && unityAssetExists && IsAnimationClipPreviewCandidate(sourceItem, packageEntry);
            bool canPreviewInWeb = IsWebAnimationPreviewCandidate(packageEntry, packageRoot);
            List<object> diagnostics = BuildAnimationUnityPreviewDiagnostics(clip, sourceItem, packageEntry, previewModelEntry, unityAssetPath, unityAssetExists, canPreviewInUnity, canPreviewInWeb);

            clipReports.Add(new
            {
                setId = clip.SetId,
                groupId = clip.GroupId,
                clipId = clip.ClipId,
                displayName = clip.DisplayName,
                runtimeResourceKey,
                sourceStableId = clip.SourceSelection != null ? clip.SourceSelection.ResourceStableId : string.Empty,
                sourceProviderId = clip.SourceSelection != null ? clip.SourceSelection.SourceProviderId : string.Empty,
                sourceBindingKind = clip.SourceSelection != null ? clip.SourceSelection.BindingKind.ToString() : AuthoringResourceBindingKind.None.ToString(),
                sourceSubClipId = subClipId,
                sourceClipName = FirstNonEmpty(clip.SourceClipName, subClipId),
                loop = clipRef != null && clipRef.Loop,
                speed = clipRef != null ? clipRef.Speed : 1f,
                rootMotionPolicy = clipRef != null ? clipRef.RootMotionPolicy : string.Empty,
                canPreviewInUnity,
                canPreviewInWeb,
                previewAuthority = canPreviewInUnity ? "UnityPreview" : canPreviewInWeb ? "WebPreviewArtifact" : "Unavailable",
                unity = new
                {
                    unityGuid,
                    unityAssetPath,
                    unityAssetExists,
                    subClipId,
                    assetType = FirstNonEmpty(unityBinding != null ? unityBinding.AssetType : string.Empty, GetAuthoringResourceMetadata(sourceItem, "assetType"))
                },
                web = new
                {
                    resourceKey = packageEntry != null ? packageEntry.ResourceKey : string.Empty,
                    relativePath = packageEntry != null ? packageEntry.RelativePath : string.Empty,
                    previewModelResourceKey = previewModelEntry != null ? previewModelEntry.ResourceKey : string.Empty,
                    previewModelRelativePath = previewModelEntry != null ? previewModelEntry.RelativePath : string.Empty
                },
                diagnostics
            });
        }

        return new
        {
            mode = "UnityAuthoritativePreview",
            description = "Unity preview is authoritative when canPreviewInUnity is true. Web preview artifacts are approximate editor aids.",
            clips = clipReports
        };
    }

    private static CharacterPackageResourceEntry FindDefaultAnimationPreviewModel(CharacterPackageResourceCatalog catalog)
    {
        for (int i = 0; catalog != null && catalog.Entries != null && i < catalog.Entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = catalog.Entries[i];
            if (entry != null &&
                string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.Usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    private static List<object> BuildAnimationUnityPreviewDiagnostics(
        AnimationClipRegistryEntry clip,
        AuthoringResourceItem sourceItem,
        CharacterPackageResourceEntry packageEntry,
        CharacterPackageResourceEntry previewModelEntry,
        string unityAssetPath,
        bool unityAssetExists,
        bool canPreviewInUnity,
        bool canPreviewInWeb)
    {
        var diagnostics = new List<object>();
        if (sourceItem == null && string.IsNullOrWhiteSpace(clip.RuntimeResourceKey))
        {
            diagnostics.Add(CreateAnimationPreviewDiagnostic(
                "Error",
                "ANIM_UNITY_PREVIEW_SOURCE_MISSING",
                "Animation clip has no resolvable source selection or runtime resource key.",
                "Select a Unity AnimationClip or runtime-ready animation resource in Animation Editor."));
        }

        if (!string.IsNullOrWhiteSpace(unityAssetPath) && !unityAssetExists)
        {
            diagnostics.Add(CreateAnimationPreviewDiagnostic(
                "Error",
                "ANIM_UNITY_PREVIEW_ASSET_MISSING",
                "Unity preview source asset does not exist: " + unityAssetPath,
                "Refresh Unity resource sync or reimport the animation source."));
        }

        if (string.IsNullOrWhiteSpace(clip.SourceSubClipId) && string.Equals(packageEntry != null ? packageEntry.Usage : string.Empty, CharacterPackageResourceUsageIds.AnimationClipGroup, StringComparison.Ordinal))
        {
            diagnostics.Add(CreateAnimationPreviewDiagnostic(
                "Warning",
                "ANIM_UNITY_PREVIEW_SUBCLIP_UNRESOLVED",
                "Animation source is a clip group but no source sub-clip id is configured.",
                "Choose a concrete sub-clip from the Animation.SourceClip picker."));
        }

        if (!canPreviewInUnity && canPreviewInWeb)
        {
            diagnostics.Add(CreateAnimationPreviewDiagnostic(
                "Info",
                "ANIM_WEB_PREVIEW_ARTIFACT_ONLY",
                "Only web preview artifact is currently available; this is not authoritative Unity playback.",
                "Use a Unity AnimationClip source for authoritative preview."));
        }

        if (previewModelEntry == null)
        {
            diagnostics.Add(CreateAnimationPreviewDiagnostic(
                "Warning",
                "ANIM_UNITY_PREVIEW_TARGET_MISSING",
                "No preview model resource is linked for this animation clip.",
                "Link a skeleton or character model preview target before running Unity preview."));
        }

        return diagnostics;
    }

    private static object CreateAnimationPreviewDiagnostic(string severity, string code, string message, string suggestedFix)
    {
        return new
        {
            severity,
            code,
            message,
            suggestedFix
        };
    }

    private static CharacterPackageResourceEntry FindPackageResourceEntryByKey(CharacterPackageResourceCatalog catalog, string resourceKey)
    {
        if (catalog == null || catalog.Entries == null || string.IsNullOrWhiteSpace(resourceKey))
            return null;

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = catalog.Entries[i];
            if (entry != null && string.Equals(entry.ResourceKey, resourceKey, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }

    private static AuthoringResourceItem FindAnimationSourceResource(AuthoringResourceCollection collection, AuthoringResourceSelectionRef selection, string runtimeResourceKey)
    {
        if (collection == null || collection.Items == null)
            return null;

        for (int i = 0; i < collection.Items.Count; i++)
        {
            AuthoringResourceItem item = collection.Items[i];
            if (item == null)
                continue;

            if (selection != null &&
                (AuthoringResourceMatches(selection.ResourceStableId, item, string.Empty) ||
                 AuthoringResourceMatches(selection.RuntimeResourceKey, item, string.Empty) ||
                 AuthoringResourceMatches(selection.UnityGuid, item, string.Empty) ||
                 AuthoringResourceMatches(selection.UnityAssetPath, item, string.Empty) ||
                 AuthoringResourceMatches(selection.ProviderResourceKey, item, string.Empty) ||
                 AuthoringResourceMatches(selection.PackageResourceKey, item, string.Empty)))
                return item;

            if (AuthoringResourceMatches(runtimeResourceKey, item, string.Empty))
                return item;
        }

        return null;
    }

    private static bool IsUnityAssetPathPresent(string rootPath, string unityAssetPath)
    {
        if (string.IsNullOrWhiteSpace(unityAssetPath))
            return false;

        string normalized = unityAssetPath.Replace('\\', '/');
        if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            return false;

        return File.Exists(Path.Combine(rootPath, normalized));
    }

    private static bool IsAnimationClipPreviewCandidate(AuthoringResourceItem item, CharacterPackageResourceEntry packageEntry)
    {
        string usage = FirstNonEmpty(item != null ? item.Usage : string.Empty, packageEntry != null ? packageEntry.Usage : string.Empty);
        string kind = FirstNonEmpty(item != null ? item.Kind : string.Empty, packageEntry != null ? packageEntry.TypeId : string.Empty);
        return string.Equals(kind, CharacterPackageResourceTypeIds.Animation, StringComparison.Ordinal) &&
            (string.Equals(usage, AnimationAuthoringResourceUsages.AnimationClip, StringComparison.Ordinal) ||
             string.Equals(usage, CharacterPackageResourceUsageIds.AnimationClipGroup, StringComparison.Ordinal));
    }

    private static bool IsWebAnimationPreviewCandidate(CharacterPackageResourceEntry packageEntry, string packageRoot)
    {
        if (packageEntry == null || string.IsNullOrWhiteSpace(packageEntry.RelativePath))
            return false;

        string extension = Path.GetExtension(packageEntry.RelativePath);
        if (!string.Equals(extension, ".glb", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".gltf", StringComparison.OrdinalIgnoreCase))
            return false;

        string fullPath = CharacterPackageResourcePipeline.ResolvePackagePath(packageRoot, packageEntry.RelativePath);
        return !string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath);
    }

    private static AnimationSetDefinitionClipRef FindAnimationSetDefinitionClip(
        AnimationSetDefinitionDocument setDefinition,
        AnimationClipRegistryEntry clip)
    {
        for (int setIndex = 0; setDefinition != null && setDefinition.Sets != null && setIndex < setDefinition.Sets.Count; setIndex++)
        {
            AnimationSetDefinitionSet set = setDefinition.Sets[setIndex];
            if (set == null || !string.Equals(set.SetId, clip.SetId, StringComparison.Ordinal))
                continue;

            for (int groupIndex = 0; set.Groups != null && groupIndex < set.Groups.Count; groupIndex++)
            {
                AnimationSetDefinitionGroup group = set.Groups[groupIndex];
                if (group == null || !string.Equals(group.GroupId, clip.GroupId, StringComparison.Ordinal))
                    continue;

                for (int clipIndex = 0; group.Clips != null && clipIndex < group.Clips.Count; clipIndex++)
                {
                    AnimationSetDefinitionClipRef item = group.Clips[clipIndex];
                    if (item != null && string.Equals(item.ClipId, clip.ClipId, StringComparison.Ordinal))
                        return item;
                }
            }
        }

        return null;
    }

    private static object CreateAnimationFieldSpecs()
    {
        return new
        {
            sourceClip = AnimationAuthoringResourceFieldSpecs.CreateSourceClip(),
            avatarMask = AnimationAuthoringResourceFieldSpecs.CreateAvatarMask(),
            bakeArtifact = AnimationAuthoringResourceFieldSpecs.CreateBakeArtifact(),
            compatibilityProfile = AnimationAuthoringResourceFieldSpecs.CreateCompatibilityProfile(),
            eventVfx = AnimationAuthoringResourceFieldSpecs.CreateEventVfx(),
            eventAudioCue = AnimationAuthoringResourceFieldSpecs.CreateEventAudioCue(),
            eventAudioDefinition = AnimationAuthoringResourceFieldSpecs.CreateEventAudioCue(AuthoringResourceSelectionOutputKind.AudioEventDefinitionId)
        };
    }

    private static void AddDuplicateIssue(
        CharacterAuthoringValidationReport report,
        HashSet<string> ids,
        string id,
        string code,
        string sourceObjectPath,
        string field)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;
        if (ids.Add(id))
            return;

        report.Issues.Add(CreateAnimationValidationIssue(
            code,
            sourceObjectPath,
            field,
            "Animation authoring id is duplicated: " + id,
            CharacterAuthoringValidationSeverity.Error));
    }

    private static CharacterAuthoringValidationIssue CreateAnimationValidationIssue(
        string code,
        string sourceObjectPath,
        string field,
        string message,
        CharacterAuthoringValidationSeverity severity)
    {
        return new CharacterAuthoringValidationIssue
        {
            Severity = severity,
            Gate = severity == CharacterAuthoringValidationSeverity.Error ? CharacterAuthoringValidationGate.ExportBlocked : CharacterAuthoringValidationGate.WarningOnly,
            Code = code,
            SourcePath = AnimationAuthoringDocumentRelativePath,
            SourceObjectPath = sourceObjectPath,
            Field = field,
            Message = message,
            SuggestedFix = "Fix the animation authoring draft, then run validation again."
        };
    }

    private static bool IsSelectionEmpty(AuthoringResourceSelectionRef selection)
    {
        return selection == null ||
            (string.IsNullOrWhiteSpace(selection.ResourceStableId) &&
             string.IsNullOrWhiteSpace(selection.ProviderResourceKey) &&
             string.IsNullOrWhiteSpace(selection.PackageResourceKey) &&
             string.IsNullOrWhiteSpace(selection.RuntimeResourceKey) &&
             string.IsNullOrWhiteSpace(selection.UnityGuid) &&
             string.IsNullOrWhiteSpace(selection.UnityAssetPath) &&
             string.IsNullOrWhiteSpace(selection.AudioCueId) &&
             string.IsNullOrWhiteSpace(selection.AudioEventDefinitionId));
    }

    private static string ResolveAnimationDocumentPath(string rootPath, string packageOrPath, string defaultPackage, JsonSerializerOptions jsonOptions)
    {
        string requested = packageOrPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(requested))
            requested = IsCharacterPackage(rootPath, defaultPackage) ? defaultPackage : DefaultCharacterPackageRelativePath;

        string resolved = ResolveSafePath(rootPath, requested);
        if (File.Exists(resolved) || string.Equals(Path.GetExtension(resolved), ".json", StringComparison.OrdinalIgnoreCase))
            return resolved;
        if (Directory.Exists(resolved) || requested.Contains('/') || requested.Contains('\\'))
            return Path.Combine(resolved, AnimationAuthoringDocumentRelativePath);

        string matched = FindAnimationPackageRelativeById(rootPath, requested, defaultPackage, jsonOptions);
        if (!string.IsNullOrWhiteSpace(matched))
            return Path.Combine(ResolveSafePath(rootPath, matched), AnimationAuthoringDocumentRelativePath);

        return Path.Combine(resolved, AnimationAuthoringDocumentRelativePath);
    }

    private static string FindAnimationPackageRelativeById(string rootPath, string id, string defaultPackage, JsonSerializerOptions jsonOptions)
    {
        foreach (object item in ListAnimationPackages(rootPath, defaultPackage, jsonOptions))
        {
            string relative = GetAnonymousProperty(item, "relative");
            string packageId = GetAnonymousProperty(item, "packageId");
            string characterPackageId = GetAnonymousProperty(item, "characterPackageId");
            string stableId = GetAnonymousProperty(item, "stableId");
            if (string.Equals(id, packageId, StringComparison.Ordinal) ||
                string.Equals(id, characterPackageId, StringComparison.Ordinal) ||
                string.Equals(id, stableId, StringComparison.Ordinal))
                return relative;
        }

        return string.Empty;
    }

    private static string ToAnimationPackageRelative(string rootPath, string documentPath)
    {
        string normalized = Path.GetFullPath(documentPath);
        string fileName = Path.GetFileName(normalized);
        if (string.Equals(fileName, Path.GetFileName(AnimationAuthoringDocumentRelativePath), StringComparison.OrdinalIgnoreCase))
        {
            string packagePath = Path.GetDirectoryName(Path.GetDirectoryName(normalized)!)!;
            return ToProjectRelativePath(rootPath, packagePath);
        }

        return ToProjectRelativePath(rootPath, normalized);
    }

    private static string ResolveAnimationPackageRootPath(string documentPath)
    {
        string normalized = Path.GetFullPath(documentPath);
        string fileName = Path.GetFileName(normalized);
        if (string.Equals(fileName, Path.GetFileName(AnimationAuthoringDocumentRelativePath), StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(Path.GetDirectoryName(normalized)!)!;

        return Path.GetDirectoryName(normalized)!;
    }

    private static CharacterPackageResourceCatalog ReadAnimationResourceCatalog(string packageRoot, JsonSerializerOptions jsonOptions)
    {
        string path = Path.Combine(packageRoot, "resource_catalog.json");
        if (!File.Exists(path))
            return new CharacterPackageResourceCatalog();

        return JsonSerializer.Deserialize<CharacterPackageResourceCatalog>(File.ReadAllText(path), jsonOptions) ?? new CharacterPackageResourceCatalog();
    }

    private static AnimationAuthoringPackage ReadAnimationPackage(string rootPath, string documentPath, JsonSerializerOptions jsonOptions)
    {
        if (File.Exists(documentPath))
            return JsonSerializer.Deserialize<AnimationAuthoringPackage>(File.ReadAllText(documentPath), jsonOptions) ?? new AnimationAuthoringPackage();

        string packagePath = Path.GetDirectoryName(Path.GetDirectoryName(documentPath)!)!;
        string manifestPath = Path.Combine(packagePath, "manifest.json");
        if (File.Exists(manifestPath))
        {
            CharacterPackageManifest manifest = JsonSerializer.Deserialize<CharacterPackageManifest>(File.ReadAllText(manifestPath), jsonOptions) ?? new CharacterPackageManifest();
            return CreateAnimationPackageFromCharacterManifest(manifest);
        }

        return new AnimationAuthoringPackage();
    }

    private static AnimationAuthoringPackage CreateAnimationPackageFromCharacterManifest(CharacterPackageManifest manifest)
    {
        string packageId = manifest != null ? manifest.PackageId ?? string.Empty : string.Empty;
        string stableId = manifest != null ? manifest.StableId ?? string.Empty : string.Empty;
        string displayName = manifest != null ? manifest.DisplayName ?? string.Empty : string.Empty;
        return new AnimationAuthoringPackage
        {
            PackageId = string.IsNullOrWhiteSpace(packageId) ? string.Empty : "animation." + packageId,
            StableId = string.IsNullOrWhiteSpace(stableId) ? string.Empty : stableId + ".animation",
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Animation Authoring" : displayName + " Animation"
        };
    }

    private static string GetAnonymousProperty(object value, string propertyName)
    {
        return value?.GetType().GetProperty(propertyName)?.GetValue(value)?.ToString() ?? string.Empty;
    }

    private static object ReadCharacterState(string rootPath, string packageRelative, JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterAuthoringValidationReport validation = CharacterResourcePackageValidator.Validate(package, new CharacterResourcePackageValidationOptions
        {
            PackageRootPath = packagePath,
            ValidateResourceFiles = true,
            ValidateResourceHashes = false
        });

        string generatedRoot = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterPackages", package.Manifest.PackageId);
        string importReportPath = Path.Combine(generatedRoot, "package_cache", "import_report.json");
        string unityResourceCatalogPath = Path.Combine(generatedRoot, "config", "unity_resource_catalog.json");

        return new
        {
            packageRelative,
            canWrite = true,
            package,
            validation,
            importReport = File.Exists(importReportPath) ? ReadOptionalJson(rootPath, importReportPath) : null,
            unityResourceCatalogPath = ToProjectRelativePath(rootPath, unityResourceCatalogPath),
            unityResourceCatalog = File.Exists(unityResourceCatalogPath) ? ReadOptionalJson(rootPath, unityResourceCatalogPath) : null
        };
    }

    private static object CreateCharacterPackage(string rootPath, CharacterStudioCreateRequest request, JsonSerializerOptions jsonOptions)
    {
        if (request == null)
            throw new ArgumentException("create request is required.");

        string packageId = NormalizeCharacterPackageId(request.packageId);
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentException("packageId is required.");

        string displayName = string.IsNullOrWhiteSpace(request.displayName)
            ? BuildDefaultCharacterDisplayName(packageId)
            : request.displayName.Trim();
        string templateRelative = ResolveCharacterTemplateRelative(request.template);
        string directoryName = NormalizeCharacterPackageDirectoryName(request.directoryName, packageId);
        string packageRelative = SamplesRelativePath + "/" + directoryName;
        string templatePath = ResolveSafePath(rootPath, templateRelative);
        string packagePath = ResolveSafePath(rootPath, packageRelative);

        if (!Directory.Exists(templatePath))
            throw new DirectoryNotFoundException("Character template directory was not found: " + templateRelative);
        if (Directory.Exists(packagePath))
            throw new IOException("Character package directory already exists: " + packageRelative);

        CopyDirectoryRecursive(templatePath, packagePath);

        try
        {
            CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
            RewriteCharacterTemplatePackage(package, packageId, displayName, jsonOptions);
            SaveCharacterPackage(rootPath, packageRelative, package, jsonOptions);

            string animationDocumentPath = Path.Combine(packagePath, AnimationAuthoringDocumentRelativePath);
            if (File.Exists(animationDocumentPath))
            {
                AnimationAuthoringPackage animation = ReadAnimationPackage(rootPath, animationDocumentPath, jsonOptions);
                animation = RewriteAnimationTemplatePackage(animation, oldPackageId: GetTemplatePackageId(templatePath, jsonOptions), oldDisplayName: GetTemplateDisplayName(templatePath, jsonOptions), newPackageId: packageId, newDisplayName: displayName, package, jsonOptions);
                WriteJsonFileAtomic(animationDocumentPath, animation, jsonOptions);
            }
        }
        catch
        {
            try
            {
                Directory.Delete(packagePath, recursive: true);
            }
            catch
            {
            }

            throw;
        }

        return new
        {
            packageRelative,
            state = ReadCharacterState(rootPath, packageRelative, jsonOptions)
        };
    }

    private static CharacterResourceLibrary ReadCharacterResources(string rootPath, string packageRelative, JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterResourceLibrary library = CharacterResourceLibraryBuilder.FromPackageResourceCatalog(package.ResourceCatalog);
        library.PackageId = package.Manifest != null ? package.Manifest.PackageId : library.PackageId;
        library.ReferenceGraph = BuildCharacterResourceReferenceGraph(package);
        ApplyCharacterResourceRuntimeState(rootPath, packagePath, package, library, jsonOptions);
        library.Diagnostics.Clear();
        library.Diagnostics.AddRange(CharacterResourceLibraryBuilder.ValidateLibrary(library));
        return library;
    }

    private static AuthoringResourceCollection ReadAuthoringResources(string rootPath, string packageRelative, JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        string packageId = package.Manifest != null ? package.Manifest.PackageId : string.Empty;
        string scopeId = "characterPackage:" + packageId;
        string generatedRoot = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterPackages", packageId);
        string unityResourceCatalogPath = Path.Combine(generatedRoot, "config", "unity_resource_catalog.json");
        string runtimeResourceCatalogPath = Path.Combine(generatedRoot, "config", "runtime_resource_catalog.json");
        string fmodAudioLibrarySnapshotPath = ResolveFmodAudioLibrarySnapshotPath(rootPath, packageId);
        CharacterResourcePlanCompileResult plan = ReadCharacterResourcePlan(rootPath, packageRelative, checkFiles: false, checkHashes: false, jsonOptions);
        RuntimeResourceCatalogDocument projectRuntimeCatalog = AnimationPackageCommands.ReadProjectRuntimeResourceCatalogs(rootPath, jsonOptions);
        RuntimeResourceCatalogDocument runtimeCatalog = AnimationPackageCommands.MergeRuntimeResourceCatalogs(projectRuntimeCatalog, plan != null ? plan.RuntimeResourceCatalog : null);
        var context = new AuthoringResourceProviderContext
        {
            ScopeId = scopeId,
            PackageId = packageId,
            PackagePath = packageRelative,
            ProjectRootPath = rootPath,
            PackageResourceCatalog = package.ResourceCatalog,
            UnityResourceCatalog = ReadOptionalJsonFile<AuthoringUnityResourceCatalogDocument>(rootPath, unityResourceCatalogPath, jsonOptions),
            RuntimeResourceCatalog = runtimeCatalog,
            FmodAudioLibrarySnapshot = ReadOptionalJsonFile<AuthoringFmodAudioLibrarySnapshotDocument>(rootPath, fmodAudioLibrarySnapshotPath, jsonOptions),
            UnityResourceCatalogPath = ToProjectRelativePath(rootPath, unityResourceCatalogPath),
            RuntimeResourceCatalogPath = ToProjectRelativePath(rootPath, runtimeResourceCatalogPath),
            FmodAudioLibrarySnapshotPath = ToProjectRelativePath(rootPath, fmodAudioLibrarySnapshotPath)
        };
        context.Metadata["externalImportStaging"] = "empty";
        AuthoringResourceCollection collection = AuthoringResourceCollectionMerger.Merge(
            new CharacterPackageAuthoringResourceProvider().BuildResourceCollection(context),
            new UnityAssetDatabaseAuthoringResourceProvider().BuildResourceCollection(context),
            new UnityProjectAssetAuthoringResourceProvider().BuildResourceCollection(context),
            new RuntimeCatalogAuthoringResourceProvider().BuildResourceCollection(context),
            new FmodAudioLibraryAuthoringResourceProvider().BuildResourceCollection(context),
            new ExternalImportStagingAuthoringResourceProvider().BuildResourceCollection(context));
        collection.ScopeId = scopeId;
        collection.ReferenceGraph = AuthoringResourceReferenceGraphBuilder.FromCharacterPackage(package, collection);
        collection.Diagnostics.AddRange(collection.ReferenceGraph.Diagnostics);
        return collection;
    }

    private static string ResolveFmodAudioLibrarySnapshotPath(string rootPath, string packageId)
    {
        string packageSnapshot = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterPackages", packageId ?? string.Empty, "config", "fmod_audio_library_snapshot.json");
        if (File.Exists(packageSnapshot))
            return packageSnapshot;

        string globalSnapshot = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "Audio", "fmod_audio_library_snapshot.json");
        if (File.Exists(globalSnapshot))
            return globalSnapshot;

        string legacyGlobalSnapshot = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "Audio", "fmod_audio_library.json");
        if (File.Exists(legacyGlobalSnapshot))
            return legacyGlobalSnapshot;

        return packageSnapshot;
    }

    private static object ReadAuthoringResourceInspect(string rootPath, string packageRelative, string id, JsonSerializerOptions jsonOptions)
    {
        AuthoringResourceCollection collection = ReadAuthoringResources(rootPath, packageRelative, jsonOptions);
        AuthoringResourceItem item = FindAuthoringResourceItem(collection, id);
        if (item == null)
            return null;

        CharacterResourcePlanCompileResult plan = ReadCharacterResourcePlan(rootPath, packageRelative, checkFiles: true, checkHashes: false, jsonOptions);
        List<AuthoringResourceReferenceEdge> references = collection.ReferenceGraph != null
            ? collection.ReferenceGraph.FindReferencesToResource(item)
            : new List<AuthoringResourceReferenceEdge>();
        List<object> plans = BuildAuthoringResourceInspectPlans(item, plan);

        return new
        {
            packageId = GetAuthoringResourceMetadata(item, "packageId"),
            item,
            authoring = BuildAuthoringResourceInspectAuthoring(item),
            unity = BuildAuthoringResourceInspectUnity(item),
            runtime = BuildAuthoringResourceInspectRuntime(item, plans),
            references,
            referenceImpact = new
            {
                referenceCount = references.Count,
                blocksDelete = references.Count > 0,
                destructiveDeleteAllowed = references.Count == 0
            },
            plans,
            diagnostics = BuildAuthoringResourceInspectDiagnostics(item, collection, plan),
            collection = new
            {
                scopeId = collection.ScopeId,
                providers = collection.Providers,
                diagnostics = collection.Diagnostics
            }
        };
    }

    private static AuthoringResourceItem FindAuthoringResourceItem(AuthoringResourceCollection collection, string id)
    {
        if (collection == null || collection.Items == null || string.IsNullOrWhiteSpace(id))
            return null;

        for (int i = 0; i < collection.Items.Count; i++)
        {
            AuthoringResourceItem item = collection.Items[i];
            if (AuthoringResourceMatches(id, item, string.Empty))
                return item;
        }

        return null;
    }

    private static object BuildAuthoringResourceInspectAuthoring(AuthoringResourceItem item)
    {
        return new
        {
            resourceId = item.ResourceId,
            stableId = item.StableId,
            sourceProviderId = item.SourceProviderId,
            sourceKind = item.SourceKind,
            bindingKind = item.BindingKind,
            providerBindings = item.ProviderBindings,
            sourcePath = FirstNonEmpty(GetAuthoringResourceMetadata(item, "relativePath"), GetAuthoringResourceExternalSourcePath(item)),
            tags = item.Tags,
            metadata = item.Metadata,
            diagnostics = item.Diagnostics
        };
    }

    private static object BuildAuthoringResourceInspectUnity(AuthoringResourceItem item)
    {
        AuthoringResourceProviderBinding binding = GetAuthoringUnityBinding(item);
        return new
        {
            unityAssetGuid = binding != null ? binding.UnityGuid : string.Empty,
            unityAssetPath = binding != null ? binding.UnityAssetPath : string.Empty,
            importStatus = item.ImportStatus.ToString(),
            diagnostics = item.Diagnostics != null ? item.Diagnostics.FindAll(diagnostic => diagnostic != null && (diagnostic.Code ?? string.Empty).IndexOf("UNITY", StringComparison.OrdinalIgnoreCase) >= 0) : new List<AuthoringResourceDiagnostic>(),
            binding
        };
    }

    private static object BuildAuthoringResourceInspectRuntime(AuthoringResourceItem item, List<object> plans)
    {
        AuthoringResourceProviderBinding binding = GetAuthoringRuntimeBinding(item) ?? GetAuthoringPrimaryBinding(item);
        List<string> groupNames = GetPlanGroupNames(plans);
        return new
        {
            runtimeBindingKind = item.BindingKind,
            runtimeAvailability = item.RuntimeAvailability,
            resourceKey = binding != null ? FirstNonEmpty(binding.RuntimeResourceKey, binding.PackageResourceKey, binding.ProviderResourceKey) : string.Empty,
            runtimeResourceKey = binding != null ? binding.RuntimeResourceKey : string.Empty,
            providerResourceKey = binding != null ? binding.ProviderResourceKey : string.Empty,
            packageResourceKey = binding != null ? binding.PackageResourceKey : string.Empty,
            providerId = binding != null ? binding.ProviderId : item.SourceProviderId,
            address = binding != null ? binding.Address : string.Empty,
            assetType = binding != null ? FirstNonEmpty(binding.AssetType, item.Kind) : item.Kind,
            hash = binding != null ? FirstNonEmpty(binding.Hash, GetAuthoringResourceMetadata(item, "contentHash")) : GetAuthoringResourceMetadata(item, "contentHash"),
            preloadPolicy = groupNames.Count > 0 ? groupNames[0] : AuthoringResourcePreloadPolicies.None,
            includedRuntimePlanGroups = groupNames,
            providerData = binding != null ? binding.ProviderData : new Dictionary<string, string>()
        };
    }

    private static List<object> BuildAuthoringResourceInspectPlans(AuthoringResourceItem item, CharacterResourcePlanCompileResult result)
    {
        var plans = new List<object>();
        if (item == null || result == null || result.CharacterResourcePlan == null)
            return plans;

        AddAuthoringPlanGroupIfContains(plans, CharacterResourcePlanGroups.SpawnCritical, result.CharacterResourcePlan.SpawnCritical, item);
        AddAuthoringPlanGroupIfContains(plans, CharacterResourcePlanGroups.PresentationCritical, result.CharacterResourcePlan.PresentationCritical, item);
        AddAuthoringPlanGroupIfContains(plans, CharacterResourcePlanGroups.EquipmentInitial, result.CharacterResourcePlan.EquipmentInitial, item);
        AddAuthoringPlanGroupIfContains(plans, CharacterResourcePlanGroups.AnimationWarmup, result.CharacterResourcePlan.AnimationWarmup, item);
        AddAuthoringPlanGroupIfContains(plans, CharacterResourcePlanGroups.VfxWarmup, result.CharacterResourcePlan.VfxWarmup, item);
        AddAuthoringPlanGroupIfContains(plans, CharacterResourcePlanGroups.UiDeferred, result.CharacterResourcePlan.UiDeferred, item);

        AudioCueManifestEntry cue = FindAuthoringAudioCue(result.AudioCueManifest, item);
        if (cue != null)
        {
            plans.Add(new
            {
                groupName = CharacterResourcePlanGroups.Audio,
                required = result.CharacterResourcePlan.Audio != null && result.CharacterResourcePlan.Audio.Required,
                failurePolicy = result.CharacterResourcePlan.Audio != null ? result.CharacterResourcePlan.Audio.FailurePolicy : string.Empty,
                resource = cue
            });
        }

        return plans;
    }

    private static void AddAuthoringPlanGroupIfContains(List<object> plans, string groupName, CharacterResourcePlanGroup group, AuthoringResourceItem item)
    {
        if (plans == null || group == null || group.Resources == null || item == null)
            return;

        for (int i = 0; i < group.Resources.Count; i++)
        {
            CharacterResourcePlanResourceRef resource = group.Resources[i];
            if (resource == null)
                continue;

            if (!AuthoringResourceMatches(resource.ResourceKey, item, string.Empty) &&
                !AuthoringResourceMatches(resource.StableId, item, string.Empty))
                continue;

            plans.Add(new
            {
                groupName,
                required = group.Required,
                failurePolicy = group.FailurePolicy,
                resource
            });
        }
    }

    private static List<object> BuildAuthoringResourceInspectDiagnostics(
        AuthoringResourceItem item,
        AuthoringResourceCollection collection,
        CharacterResourcePlanCompileResult plan)
    {
        var diagnostics = new List<object>();
        if (item == null)
            return diagnostics;

        if (item.Diagnostics != null)
        {
            for (int i = 0; i < item.Diagnostics.Count; i++)
                AddAuthoringResourceDiagnostic(diagnostics, "item", item.Diagnostics[i], item);
        }

        if (collection != null && collection.Diagnostics != null)
        {
            for (int i = 0; i < collection.Diagnostics.Count; i++)
                AddAuthoringResourceDiagnostic(diagnostics, "collection", collection.Diagnostics[i], item);
        }

        if (plan != null && plan.CharacterResourcePlan != null && plan.CharacterResourcePlan.Diagnostics != null)
        {
            for (int i = 0; i < plan.CharacterResourcePlan.Diagnostics.Count; i++)
                AddAuthoringResourcePlanDiagnostic(diagnostics, "resourcePlan", plan.CharacterResourcePlan.Diagnostics[i], item);
        }

        if (plan != null && plan.ResourceValidationReport != null && plan.ResourceValidationReport.Diagnostics != null)
        {
            for (int i = 0; i < plan.ResourceValidationReport.Diagnostics.Count; i++)
                AddAuthoringResourcePlanDiagnostic(diagnostics, "validationReport", plan.ResourceValidationReport.Diagnostics[i], item);
        }

        return diagnostics;
    }

    private static void AddAuthoringResourceDiagnostic(List<object> diagnostics, string source, AuthoringResourceDiagnostic diagnostic, AuthoringResourceItem item)
    {
        if (diagnostics == null || diagnostic == null || item == null)
            return;

        if (!AuthoringResourceMatches(diagnostic.ResourceId, item, string.Empty) &&
            !AuthoringResourceMatches(diagnostic.ResourceStableId, item, string.Empty) &&
            !AuthoringResourceMatches(diagnostic.RuntimeResourceKey, item, string.Empty))
            return;

        diagnostics.Add(new
        {
            source,
            severity = diagnostic.Severity.ToString(),
            code = diagnostic.Code,
            message = diagnostic.Message,
            suggestedFix = diagnostic.SuggestedFix,
            resourceId = diagnostic.ResourceId,
            resourceStableId = diagnostic.ResourceStableId,
            runtimeResourceKey = diagnostic.RuntimeResourceKey,
            providerId = diagnostic.ProviderId,
            sourceConfigKind = diagnostic.SourceConfigKind,
            sourceStableId = diagnostic.SourceStableId,
            sourceField = diagnostic.SourceField
        });
    }

    private static void AddAuthoringResourcePlanDiagnostic(List<object> diagnostics, string source, CharacterResourcePlanDiagnostic diagnostic, AuthoringResourceItem item)
    {
        if (diagnostics == null || diagnostic == null || item == null)
            return;

        if (!AuthoringResourceMatches(diagnostic.LibraryItemStableId, item, string.Empty) &&
            !AuthoringResourceMatches(diagnostic.ResourceKey, item, string.Empty))
            return;

        diagnostics.Add(new
        {
            source,
            severity = diagnostic.Severity,
            code = diagnostic.Code,
            message = diagnostic.Message,
            suggestedFix = diagnostic.SuggestedFix,
            libraryItemStableId = diagnostic.LibraryItemStableId,
            resourceStableId = diagnostic.LibraryItemStableId,
            resourceKey = diagnostic.ResourceKey,
            sourceConfigKind = diagnostic.SourceConfigKind,
            sourceField = diagnostic.SourceField
        });
    }

    private static AudioCueManifestEntry FindAuthoringAudioCue(AudioCueManifestDocument manifest, AuthoringResourceItem item)
    {
        if (manifest == null || manifest.Cues == null || item == null)
            return null;

        for (int i = 0; i < manifest.Cues.Count; i++)
        {
            AudioCueManifestEntry cue = manifest.Cues[i];
            if (cue == null)
                continue;

            if (AuthoringResourceMatches(cue.CueId, item, string.Empty) ||
                AuthoringResourceMatches(cue.StableId, item, string.Empty) ||
                AuthoringResourceMatches(cue.ResourceKey, item, string.Empty))
                return cue;
        }

        return null;
    }

    private static bool AuthoringResourceMatches(string value, AuthoringResourceItem item, string id)
    {
        if (item == null)
            return false;

        string candidate = FirstNonEmpty(value, id);
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        if (string.Equals(candidate, item.ResourceId, StringComparison.Ordinal) ||
            string.Equals(candidate, item.StableId, StringComparison.Ordinal) ||
            string.Equals(candidate, item.DisplayName, StringComparison.Ordinal))
            return true;

        if (item.Metadata != null)
        {
            string metadataValue;
            if ((item.Metadata.TryGetValue("localId", out metadataValue) && string.Equals(candidate, metadataValue, StringComparison.Ordinal)) ||
                (item.Metadata.TryGetValue("relativePath", out metadataValue) && string.Equals(candidate, metadataValue, StringComparison.Ordinal)))
                return true;
        }

        if (item.ProviderBindings == null)
            return false;

        for (int i = 0; i < item.ProviderBindings.Count; i++)
        {
            AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
            if (binding == null)
                continue;

            if (string.Equals(candidate, binding.ProviderResourceKey, StringComparison.Ordinal) ||
                string.Equals(candidate, binding.PackageResourceKey, StringComparison.Ordinal) ||
                string.Equals(candidate, binding.RuntimeResourceKey, StringComparison.Ordinal) ||
                string.Equals(candidate, binding.UnityGuid, StringComparison.Ordinal) ||
                string.Equals(candidate, binding.UnityAssetPath, StringComparison.Ordinal) ||
                string.Equals(candidate, binding.FmodEventGuid, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate, binding.FmodEventPath, StringComparison.Ordinal) ||
                string.Equals(candidate, binding.ExternalSourcePath, StringComparison.Ordinal) ||
                string.Equals(candidate, binding.DisplayValue, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static AuthoringResourceProviderBinding GetAuthoringPrimaryBinding(AuthoringResourceItem item)
    {
        if (item == null || item.ProviderBindings == null || item.ProviderBindings.Count == 0)
            return null;

        for (int i = 0; i < item.ProviderBindings.Count; i++)
        {
            AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
            if (binding != null && binding.IsPrimary)
                return binding;
        }

        return item.ProviderBindings[0];
    }

    private static AuthoringResourceProviderBinding GetAuthoringRuntimeBinding(AuthoringResourceItem item)
    {
        if (item == null || item.ProviderBindings == null)
            return null;

        for (int i = 0; i < item.ProviderBindings.Count; i++)
        {
            AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
            if (binding != null && !string.IsNullOrWhiteSpace(binding.RuntimeResourceKey))
                return binding;
        }

        return null;
    }

    private static AuthoringResourceProviderBinding GetAuthoringUnityBinding(AuthoringResourceItem item)
    {
        if (item == null || item.ProviderBindings == null)
            return null;

        for (int i = 0; i < item.ProviderBindings.Count; i++)
        {
            AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
            if (binding != null && (!string.IsNullOrWhiteSpace(binding.UnityGuid) || !string.IsNullOrWhiteSpace(binding.UnityAssetPath)))
                return binding;
        }

        return null;
    }

    private static string GetAuthoringResourceExternalSourcePath(AuthoringResourceItem item)
    {
        if (item == null || item.ProviderBindings == null)
            return string.Empty;

        for (int i = 0; i < item.ProviderBindings.Count; i++)
        {
            AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
            if (binding != null && !string.IsNullOrWhiteSpace(binding.ExternalSourcePath))
                return binding.ExternalSourcePath;
        }

        return string.Empty;
    }

    private static string GetAuthoringResourceMetadata(AuthoringResourceItem item, string key)
    {
        if (item == null || item.Metadata == null || string.IsNullOrWhiteSpace(key))
            return string.Empty;

        string value;
        return item.Metadata.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
    }

    private static CharacterResourcePlanCompileResult ReadCharacterResourcePlan(
        string rootPath,
        string packageRelative,
        bool checkFiles,
        bool checkHashes,
        JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        return CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = packagePath,
            ValidateResourceFiles = checkFiles || checkHashes,
            ValidateResourceHashes = checkHashes
        });
    }

    private static object ReadCharacterResourceInspect(string rootPath, string packageRelative, string id, JsonSerializerOptions jsonOptions)
    {
        CharacterResourceLibrary library = ReadCharacterResources(rootPath, packageRelative, jsonOptions);
        ResourceLibraryItem item = FindCharacterResourceLibraryItem(library, id);
        if (item == null)
            return null;

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterPackageResourceEntry authoringEntry = FindCharacterPackageResourceEntry(package.ResourceCatalog, item, id);
        CharacterResourcePlanCompileResult plan = ReadCharacterResourcePlan(rootPath, packageRelative, checkFiles: true, checkHashes: false, jsonOptions);
        string generatedRoot = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterPackages", library.PackageId);
        string unityCatalogPath = Path.Combine(generatedRoot, "config", "unity_resource_catalog.json");
        string importReportPath = Path.Combine(generatedRoot, "package_cache", "import_report.json");
        JsonElement? unityEntry = FindResourceJsonEntry(rootPath, unityCatalogPath, item);
        JsonElement? importOperation = FindImportReportOperation(rootPath, importReportPath, item, authoringEntry, unityEntry);
        RuntimeResourceCatalogEntryDocument runtimeEntry = FindRuntimeCatalogEntry(plan.RuntimeResourceCatalog, item);
        AudioCueManifestEntry audioCue = FindAudioCue(plan.AudioCueManifest, item);
        List<object> plans = BuildCharacterResourceInspectPlans(item, plan);
        List<ResourceReferenceEdge> references = FindCharacterResourceReferences(library.ReferenceGraph, item);

        return new
        {
            packageId = library.PackageId,
            item,
            authoring = BuildCharacterResourceInspectAuthoring(packagePath, packageRelative, authoringEntry, item),
            unity = BuildCharacterResourceInspectUnity(rootPath, unityCatalogPath, importReportPath, unityEntry, importOperation, item),
            runtime = BuildCharacterResourceInspectRuntime(item, runtimeEntry, audioCue, plans),
            references,
            plans,
            diagnostics = BuildCharacterResourceInspectDiagnostics(item, library, plan, unityEntry)
        };
    }

    private static ResourceLibraryWriteRequest ReadResourceLibraryWriteRequest(HttpListenerContext context, JsonSerializerOptions jsonOptions)
    {
        string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
        return string.IsNullOrWhiteSpace(body)
            ? new ResourceLibraryWriteRequest()
            : (JsonSerializer.Deserialize<ResourceLibraryWriteRequest>(body, jsonOptions) ?? new ResourceLibraryWriteRequest());
    }

    private static ExternalImportStageRequest ReadExternalImportStageRequest(HttpListenerContext context, JsonSerializerOptions jsonOptions)
    {
        string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
        return string.IsNullOrWhiteSpace(body)
            ? new ExternalImportStageRequest()
            : (JsonSerializer.Deserialize<ExternalImportStageRequest>(body, jsonOptions) ?? new ExternalImportStageRequest());
    }

    private static AnimationAuthoringSaveRequest ReadAnimationAuthoringSaveRequest(HttpListenerContext context, JsonSerializerOptions jsonOptions)
    {
        string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
        if (string.IsNullOrWhiteSpace(body))
            return new AnimationAuthoringSaveRequest();

        AnimationAuthoringSaveRequest request = JsonSerializer.Deserialize<AnimationAuthoringSaveRequest>(body, jsonOptions);
        if (request != null && (request.animation != null || !string.IsNullOrWhiteSpace(request.package)))
            return request;

        AnimationAuthoringPackage package = JsonSerializer.Deserialize<AnimationAuthoringPackage>(body, jsonOptions);
        return new AnimationAuthoringSaveRequest { animation = package };
    }

    private static AuthoringResourceCollection StageExternalImport(
        string rootPath,
        string packageRelative,
        ExternalImportStageRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        string packageId = package.Manifest != null ? package.Manifest.PackageId : string.Empty;
        var context = new AuthoringResourceProviderContext
        {
            ScopeId = "externalImportStaging:" + packageId,
            PackageId = packageId,
            PackagePath = packageRelative,
            ProjectRootPath = rootPath,
            PackageResourceCatalog = package.ResourceCatalog
        };
        var document = new AuthoringExternalImportStagingDocument
        {
            ScopeId = context.ScopeId,
            SourceRootLabel = request.sourceRootLabel ?? string.Empty,
            Files = request.files ?? new List<AuthoringExternalImportStagingFile>(),
            MaxFileSizeBytes = request.maxFileSizeBytes
        };
        return ExternalImportStagingAuthoringResourceProvider.FromStagingDocument(document, context);
    }

    private static object ImportCharacterResource(
        string rootPath,
        string packageRelative,
        ResourceLibraryWriteRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string extension = ValidateResourceFileName(request.fileName);
        string typeId = NormalizeResourceTypeId(request.kind, extension);
        CharacterPackageResourceEntry entry = string.Equals(typeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase)
            ? ImportCharacterModel(rootPath, packageRelative, new CharacterStudioModelImportRequest
            {
                fileName = request.fileName,
                role = string.IsNullOrWhiteSpace(request.role) ? "preview" : request.role,
                resourceKey = request.resourceKey,
                bytesBase64 = request.bytesBase64
            }, jsonOptions)
            : ImportGenericCharacterResource(rootPath, packageRelative, request, existingEntry: null, preserveExistingPath: false, jsonOptions);

        return BuildCharacterResourceWriteResponse(rootPath, packageRelative, "import", entry, null, jsonOptions);
    }

    private static object ReimportCharacterResource(
        string rootPath,
        string packageRelative,
        ResourceLibraryWriteRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string id = GetResourceLibraryWriteRequestId(request);
        if (string.IsNullOrWhiteSpace(id))
            throw new ResourceLibraryWriteException("RESOURCE_LIBRARY_WRITE_TARGET_REQUIRED", "id, resourceKey, stableId, or libraryItemId is required.", 400);

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterPackageResourceEntry entry = FindCharacterPackageResourceEntryById(package.ResourceCatalog, id);
        if (entry == null)
            throw new ResourceLibraryWriteException("RESOURCE_LIBRARY_ITEM_NOT_FOUND", "Resource library item was not found: " + id, 404);

        string sourcePath = CharacterPackageResourcePipeline.ResolvePackagePath(packagePath, entry.RelativePath);
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new ResourceLibraryWriteException("RESOURCE_LIBRARY_SOURCE_MISSING", "Resource source file was not found: " + entry.RelativePath, 409);

        ResourceIdentitySnapshot before = ResourceIdentitySnapshot.FromEntry(entry);
        entry.Hashes ??= new CharacterPackageResourceHashes();
        entry.Hashes.Algorithm = "sha256";
        entry.Hash = CharacterPackageHashUtility.ComputeFileSha256(sourcePath);
        entry.Hashes.ContentHash = entry.Hash;
        entry.Hashes.ImportHash = CharacterPackageResourcePipeline.ComputeImportHash(entry);
        entry.Hashes.DependencyHash = CharacterPackageResourcePipeline.ComputeDependencyHash(entry, package.ResourceCatalog);
        TouchResourceProvenance(entry, "ResourceLibraryEditor/Reimport", entry.Provenance?.SourceFile);

        SaveCharacterPackage(rootPath, packageRelative, package, jsonOptions);
        return BuildCharacterResourceWriteResponse(rootPath, packageRelative, "reimport", entry, before, jsonOptions);
    }

    private static object ReplaceCharacterResourceSource(
        string rootPath,
        string packageRelative,
        ResourceLibraryWriteRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string id = GetResourceLibraryWriteRequestId(request);
        if (string.IsNullOrWhiteSpace(id))
            throw new ResourceLibraryWriteException("RESOURCE_LIBRARY_WRITE_TARGET_REQUIRED", "id, resourceKey, stableId, or libraryItemId is required.", 400);

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterPackageResourceEntry entry = FindCharacterPackageResourceEntryById(package.ResourceCatalog, id);
        if (entry == null)
            throw new ResourceLibraryWriteException("RESOURCE_LIBRARY_ITEM_NOT_FOUND", "Resource library item was not found: " + id, 404);

        ResourceIdentitySnapshot before = ResourceIdentitySnapshot.FromEntry(entry);
        CharacterPackageResourceEntry updated;
        if (string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(entry.ResourceKey))
                throw new ResourceLibraryWriteException("RESOURCE_LIBRARY_RESOURCE_KEY_MISSING", "Model resource cannot be replaced because resourceKey is empty.", 409);
            ValidateModelReplaceFormatChange(entry, request);
            updated = ImportCharacterModel(rootPath, packageRelative, new CharacterStudioModelImportRequest
            {
                fileName = request.fileName,
                role = string.IsNullOrWhiteSpace(request.role) ? GetModelImportRole(package, entry) : request.role,
                resourceKey = entry.ResourceKey,
                bytesBase64 = request.bytesBase64
            }, jsonOptions);
        }
        else
        {
            updated = ImportGenericCharacterResource(rootPath, packageRelative, request, entry, preserveExistingPath: true, jsonOptions);
        }

        return BuildCharacterResourceWriteResponse(rootPath, packageRelative, "replace-source", updated, before, jsonOptions);
    }

    private static void ValidateModelReplaceFormatChange(CharacterPackageResourceEntry entry, ResourceLibraryWriteRequest request)
    {
        string sourceExtension = ValidateResourceFileName(request.fileName);
        string targetExtension = string.Equals(sourceExtension, ".fbx", StringComparison.OrdinalIgnoreCase) ? ".glb" : sourceExtension;
        string currentExtension = Path.GetExtension(entry.RelativePath ?? string.Empty);
        if (!request.allowFormatChange && !string.IsNullOrWhiteSpace(currentExtension) &&
            !string.Equals(currentExtension, targetExtension, StringComparison.OrdinalIgnoreCase))
            throw new ResourceLibraryWriteException(
                "RESOURCE_LIBRARY_FORMAT_CHANGE_REQUIRES_CONFIRMATION",
                "Replacing this model changes the package format from " + currentExtension + " to " + targetExtension + ". Set allowFormatChange to true to confirm.",
                409);
    }

    private static CharacterPackageResourceEntry ImportGenericCharacterResource(
        string rootPath,
        string packageRelative,
        ResourceLibraryWriteRequest request,
        CharacterPackageResourceEntry existingEntry,
        bool preserveExistingPath,
        JsonSerializerOptions jsonOptions)
    {
        string extension = ValidateResourceFileName(request.fileName);
        byte[] bytes = DecodeResourceWriteBytes(request);
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterPackageResourceCatalog catalog = package.ResourceCatalog ?? new CharacterPackageResourceCatalog();
        package.ResourceCatalog = catalog;

        CharacterPackageResourceEntry entry = existingEntry;
        if (entry == null && !string.IsNullOrWhiteSpace(request.resourceKey))
            entry = CharacterPackageResourcePipeline.FindByKey(catalog, request.resourceKey);
        if (entry == null)
        {
            entry = new CharacterPackageResourceEntry();
            catalog.Entries.Add(entry);
        }

        string packageId = package.Manifest?.PackageId ?? string.Empty;
        string typeId = NormalizeResourceTypeId(!string.IsNullOrWhiteSpace(request.kind) ? request.kind : entry.TypeId, extension);
        string format = NormalizeSourceFormat(extension);
        string targetExtension = extension;
        byte[] packageResourceBytes = bytes;
        string fileStem = CharacterPackageResourceKeyGenerator.NormalizeSegment(Path.GetFileNameWithoutExtension(request.fileName)).Replace('.', '_');
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Animation, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(format, CharacterPackageResourceFormatIds.Fbx, StringComparison.OrdinalIgnoreCase))
        {
            packageResourceBytes = ConvertFbxToGlb(rootPath, fileStem, request.fileName, bytes);
            targetExtension = ".glb";
            format = CharacterPackageResourceFormatIds.Glb;
        }

        string usage = !string.IsNullOrWhiteSpace(request.usage) ? request.usage.Trim() : (!string.IsNullOrWhiteSpace(entry.Usage) ? entry.Usage : GetDefaultResourceUsage(typeId));
        if (!CharacterPackageResourcePipeline.IsSupportedV1Format(new CharacterPackageResourceEntry { TypeId = typeId, SourceFormat = format }))
            throw new ResourceLibraryWriteException("RESOURCE_LIBRARY_UNSUPPORTED_FORMAT", "Resource type '" + typeId + "' does not support ." + format + " files.", 400);
        if (preserveExistingPath && !request.allowFormatChange && !string.IsNullOrWhiteSpace(entry.SourceFormat) &&
            !string.Equals(entry.SourceFormat, format, StringComparison.OrdinalIgnoreCase))
            throw new ResourceLibraryWriteException(
                "RESOURCE_LIBRARY_FORMAT_CHANGE_REQUIRES_CONFIRMATION",
                "Replacing this resource changes the source format from ." + entry.SourceFormat + " to ." + format + ". Set allowFormatChange to true to confirm.",
                409);

        string localId = !string.IsNullOrWhiteSpace(request.localId)
            ? CharacterPackageResourceKeyGenerator.NormalizeSegment(request.localId)
            : (!string.IsNullOrWhiteSpace(entry.LocalId) ? entry.LocalId : typeId + "." + fileStem);
        string targetRelativePath = ResolveResourceWriteTargetRelativePath(request, entry, preserveExistingPath, typeId, fileStem, targetExtension);
        string outputPath = Path.Combine(packagePath, targetRelativePath);
        string contentHash = CharacterPackageHashUtility.ComputeSha256(packageResourceBytes);
        if (!File.Exists(outputPath) || !string.Equals(CharacterPackageHashUtility.ComputeFileSha256(outputPath), contentHash, StringComparison.OrdinalIgnoreCase))
            WriteBytesAtomic(outputPath, packageResourceBytes);

        if (string.IsNullOrWhiteSpace(entry.ResourceKey))
            entry.ResourceKey = CharacterPackageResourceKeyGenerator.Generate(packageId, typeId, localId);
        if (string.IsNullOrWhiteSpace(entry.LocalId))
            entry.LocalId = localId;
        if (string.IsNullOrWhiteSpace(entry.StableId))
            entry.StableId = "charpkg." + CharacterPackageResourceKeyGenerator.NormalizeSegment(packageId) + ".resource." + CharacterPackageResourceKeyGenerator.NormalizeSegment(entry.LocalId);

        entry.TypeId = typeId;
        entry.Variant = string.IsNullOrWhiteSpace(entry.Variant) ? "default" : entry.Variant;
        entry.Usage = usage;
        entry.SourceFormat = format;
        entry.PackageId = packageId;
        entry.RelativePath = targetRelativePath;
        entry.ImportHints ??= new CharacterPackageImportHint();
        entry.ImportHints.TargetPathPolicy = CharacterPackageImportTargetPathPolicies.GeneratedCharacterPackage;
        entry.ImportHints.TargetRelativePath = targetRelativePath;
        entry.ImportHints.ProviderId = string.IsNullOrWhiteSpace(entry.ImportHints.ProviderId) ? "unityAsset" : entry.ImportHints.ProviderId;
        entry.Tags ??= new List<string>();
        AddTag(entry.Tags, "resourcelibrary-import");
        if (request.tags != null)
        {
            for (int i = 0; i < request.tags.Count; i++)
                AddTag(entry.Tags, request.tags[i]);
        }

        entry.Hash = contentHash;
        entry.Hashes ??= new CharacterPackageResourceHashes();
        entry.Hashes.Algorithm = "sha256";
        entry.Hashes.ContentHash = entry.Hash;
        entry.Hashes.ImportHash = CharacterPackageResourcePipeline.ComputeImportHash(entry);
        entry.Hashes.DependencyHash = CharacterPackageResourcePipeline.ComputeDependencyHash(entry, catalog);
        TouchResourceProvenance(entry, "ResourceLibraryEditor", request.fileName);

        SaveCharacterPackage(rootPath, packageRelative, package, jsonOptions);
        return entry;
    }

    private static string ResolveResourceWriteTargetRelativePath(
        ResourceLibraryWriteRequest request,
        CharacterPackageResourceEntry entry,
        bool preserveExistingPath,
        string typeId,
        string fileStem,
        string extension)
    {
        if (!string.IsNullOrWhiteSpace(request.targetRelativePath))
        {
            if (!CharacterPackageResourcePipeline.IsSafePackageRelativePath(request.targetRelativePath))
                throw new ResourceLibraryWriteException("RESOURCE_LIBRARY_UNSAFE_TARGET_PATH", "targetRelativePath must stay inside the character package.", 400);
            return request.targetRelativePath.Replace('\\', '/').TrimStart('/');
        }

        if (preserveExistingPath && entry != null && !string.IsNullOrWhiteSpace(entry.RelativePath))
        {
            string existingExtension = Path.GetExtension(entry.RelativePath);
            if (string.Equals(existingExtension, extension, StringComparison.OrdinalIgnoreCase))
                return entry.RelativePath.Replace('\\', '/').TrimStart('/');
        }

        return (GetResourceTypeDirectory(typeId) + "/" + fileStem + extension).Replace('\\', '/');
    }

    private static string GetResourceTypeDirectory(string typeId)
    {
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase))
            return "resources/models";
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Texture, StringComparison.OrdinalIgnoreCase))
            return "resources/textures";
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Material, StringComparison.OrdinalIgnoreCase))
            return "resources/materials";
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Animation, StringComparison.OrdinalIgnoreCase))
            return "resources/animations";
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Audio, StringComparison.OrdinalIgnoreCase))
            return "resources/audio";
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Vfx, StringComparison.OrdinalIgnoreCase))
            return "resources/vfx";
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Geometry, StringComparison.OrdinalIgnoreCase))
            return "resources/geometry";
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Preview, StringComparison.OrdinalIgnoreCase))
            return "resources/previews";
        return "resources/config";
    }

    private static string NormalizeResourceTypeId(string typeId, string extension)
    {
        if (!string.IsNullOrWhiteSpace(typeId))
            return typeId.Trim();
        string format = NormalizeSourceFormat(extension);
        if (format == CharacterPackageResourceFormatIds.Glb || format == CharacterPackageResourceFormatIds.Gltf || format == CharacterPackageResourceFormatIds.Fbx)
            return CharacterPackageResourceTypeIds.Model;
        if (format == CharacterPackageResourceFormatIds.Png || format == CharacterPackageResourceFormatIds.Jpg || format == CharacterPackageResourceFormatIds.Jpeg || format == CharacterPackageResourceFormatIds.Tga)
            return CharacterPackageResourceTypeIds.Texture;
        if (format == CharacterPackageResourceFormatIds.Wav || format == CharacterPackageResourceFormatIds.Ogg)
            return CharacterPackageResourceTypeIds.Audio;
        return CharacterPackageResourceTypeIds.Config;
    }

    private static string GetDefaultResourceUsage(string typeId)
    {
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceUsageIds.PreviewMesh;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Texture, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceUsageIds.Texture;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Material, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceUsageIds.Material;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Animation, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceUsageIds.AnimationClipGroup;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Audio, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceUsageIds.AudioCue;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Vfx, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceUsageIds.VfxCue;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Geometry, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceUsageIds.GeometryAuthoring;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Preview, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceUsageIds.PreviewThumbnail;
        return CharacterPackageResourceUsageIds.CharacterConfig;
    }

    private static string NormalizeSourceFormat(string extension)
    {
        return extension.TrimStart('.').Trim().ToLowerInvariant();
    }

    private static string ValidateResourceFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("fileName is required.");
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("fileName must include an extension.");
        return extension;
    }

    private static byte[] DecodeResourceWriteBytes(ResourceLibraryWriteRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.bytesBase64))
            throw new ArgumentException("bytesBase64 is required.");
        try
        {
            return Convert.FromBase64String(request.bytesBase64);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("bytesBase64 is not valid base64.", ex);
        }
    }

    private static string GetResourceLibraryWriteRequestId(ResourceLibraryWriteRequest request)
    {
        if (request == null)
            return string.Empty;
        return FirstNonEmpty(request.id, request.libraryItemId, request.stableId, request.resourceKey);
    }

    private static CharacterPackageResourceEntry FindCharacterPackageResourceEntryById(CharacterPackageResourceCatalog catalog, string id)
    {
        if (catalog == null || catalog.Entries == null || string.IsNullOrWhiteSpace(id))
            return null;

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = catalog.Entries[i];
            if (entry == null)
                continue;
            if (string.Equals(entry.ResourceKey, id, StringComparison.Ordinal) ||
                string.Equals(entry.StableId, id, StringComparison.Ordinal) ||
                string.Equals(entry.LocalId, id, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }

    private static string GetModelImportRole(CharacterResourcePackage package, CharacterPackageResourceEntry entry)
    {
        if (entry == null)
            return "preview";
        if (string.Equals(entry.Usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.LocalId, "model.body", StringComparison.OrdinalIgnoreCase))
            return "body";
        if (package?.Geometry?.WeaponAttachments != null)
        {
            WeaponAttachmentProfile attachment = package.Geometry.WeaponAttachments.FirstOrDefault(item =>
                item != null && string.Equals(item.PreviewResourceKey, entry.ResourceKey, StringComparison.Ordinal));
            if (attachment != null && !string.IsNullOrWhiteSpace(attachment.EquipSlot))
                return attachment.EquipSlot;
        }
        if (entry.Tags != null)
        {
            if (entry.Tags.Contains("mainHand", StringComparer.OrdinalIgnoreCase))
                return "mainHand";
            if (entry.Tags.Contains("offHand", StringComparer.OrdinalIgnoreCase))
                return "offHand";
        }
        return "preview";
    }

    private static object BuildCharacterResourceWriteResponse(
        string rootPath,
        string packageRelative,
        string action,
        CharacterPackageResourceEntry entry,
        ResourceIdentitySnapshot before,
        JsonSerializerOptions jsonOptions)
    {
        string selectedId = FirstNonEmpty(entry?.ResourceKey, entry?.StableId, entry?.LocalId);
        CharacterResourceLibrary resources = ReadCharacterResources(rootPath, packageRelative, jsonOptions);
        ResourceLibraryItem item = FindCharacterResourceLibraryItem(resources, selectedId);
        List<ResourceReferenceEdge> references = item != null ? FindCharacterResourceReferences(resources.ReferenceGraph, item) : new List<ResourceReferenceEdge>();
        CharacterResourcePlanCompileResult resourcePlan = ReadCharacterResourcePlan(rootPath, packageRelative, checkFiles: true, checkHashes: false, jsonOptions);
        object inspect = string.IsNullOrWhiteSpace(selectedId) ? null : ReadCharacterResourceInspect(rootPath, packageRelative, selectedId, jsonOptions);

        return new
        {
            success = true,
            action,
            packageRelative,
            selectedId,
            selectedResourceKey = entry?.ResourceKey ?? string.Empty,
            item,
            inspect,
            resources,
            resourcePlan,
            referenceImpact = new
            {
                preservesStableId = before == null || string.Equals(before.StableId, entry?.StableId, StringComparison.Ordinal),
                preservesResourceKey = before == null || string.Equals(before.ResourceKey, entry?.ResourceKey, StringComparison.Ordinal),
                affectedReferenceCount = references.Count,
                changedFields = before == null ? Array.Empty<string>() : before.GetChangedFields(entry)
            },
            diagnostics = item?.Diagnostics ?? new List<ResourceLibraryDiagnostic>()
        };
    }

    private static object BuildResourceLibraryWriteError(string code, string message)
    {
        return new
        {
            error = string.IsNullOrWhiteSpace(code) ? "RESOURCE_LIBRARY_WRITE_FAILED" : code,
            message = string.IsNullOrWhiteSpace(message) ? "Resource library write failed." : message
        };
    }

    private static CharacterAuthoringCompileResult CompileCharacterPackage(
        string rootPath,
        string packageRelative,
        bool checkFiles,
        bool checkHashes,
        JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        return CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest
        {
            Package = package,
            PackageRootPath = packagePath,
            Options = new CharacterAuthoringCompileOptions
            {
                ValidateResourceFiles = checkFiles || checkHashes,
                ValidateResourceHashes = checkHashes
            }
        });
    }

    private static ResourceLibraryItem FindCharacterResourceLibraryItem(CharacterResourceLibrary library, string id)
    {
        if (library == null || library.Items == null || string.IsNullOrWhiteSpace(id))
            return null;

        ResourceLibraryItem fallback = null;
        for (int i = 0; i < library.Items.Count; i++)
        {
            ResourceLibraryItem item = library.Items[i];
            if (item == null)
                continue;

            if (string.Equals(item.LibraryItemId, id, StringComparison.Ordinal) ||
                string.Equals(item.StableId, id, StringComparison.Ordinal) ||
                string.Equals(item.ResourceKey, id, StringComparison.Ordinal))
                return item;

            if (fallback == null && string.Equals(item.DisplayName, id, StringComparison.Ordinal))
                fallback = item;
        }

        if (fallback != null)
            return fallback;

        for (int i = 0; i < library.Items.Count; i++)
        {
            ResourceLibraryItem item = library.Items[i];
            if (item != null && string.Equals(item.DisplayName, id, StringComparison.OrdinalIgnoreCase))
                return item;
        }

        return null;
    }

    private static CharacterPackageResourceEntry FindCharacterPackageResourceEntry(CharacterPackageResourceCatalog catalog, ResourceLibraryItem item, string id)
    {
        if (catalog == null || catalog.Entries == null || item == null)
            return null;

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = catalog.Entries[i];
            if (entry == null)
                continue;

            if (ResourceMatches(entry.ResourceKey, item, id) ||
                ResourceMatches(entry.StableId, item, id) ||
                ResourceMatches(entry.LocalId, item, id))
                return entry;
        }

        return null;
    }

    private static bool ResourceMatches(string value, ResourceLibraryItem item, string id)
    {
        if (string.IsNullOrWhiteSpace(value) || item == null)
            return false;

        return string.Equals(value, item.ResourceKey, StringComparison.Ordinal) ||
            string.Equals(value, item.StableId, StringComparison.Ordinal) ||
            string.Equals(value, item.LibraryItemId, StringComparison.Ordinal) ||
            string.Equals(value, item.DisplayName, StringComparison.Ordinal) ||
            string.Equals(value, id, StringComparison.Ordinal);
    }

    private static object BuildCharacterResourceInspectAuthoring(
        string packagePath,
        string packageRelative,
        CharacterPackageResourceEntry entry,
        ResourceLibraryItem item)
    {
        string sourcePath = entry != null ? entry.RelativePath : item.SourcePath;
        string sourceFullPath = CharacterPackageResourcePipeline.ResolvePackagePath(packagePath, sourcePath);
        bool sourceExists = !string.IsNullOrWhiteSpace(sourceFullPath) && File.Exists(sourceFullPath);
        long sourceSize = sourceExists ? new FileInfo(sourceFullPath).Length : 0;

        return new
        {
            packageRelative,
            resourceKey = entry != null ? entry.ResourceKey : item.ResourceKey,
            localId = entry != null ? entry.LocalId : item.DisplayName,
            stableId = entry != null ? entry.StableId : item.StableId,
            typeId = entry != null ? entry.TypeId : item.Kind,
            variant = entry != null ? entry.Variant : string.Empty,
            usage = entry != null ? entry.Usage : item.Usage,
            sourceFormat = entry != null ? entry.SourceFormat : string.Empty,
            sourcePath,
            sourceExists,
            sourceSize,
            hash = entry != null ? CharacterPackageResourcePipeline.GetDeclaredContentHash(entry) : item.Hash,
            hashes = entry != null ? entry.Hashes : null,
            importHints = entry != null ? entry.ImportHints : null,
            dependencies = entry != null && entry.Dependencies != null ? entry.Dependencies : new List<CharacterPackageResourceDependency>(),
            tags = entry != null ? entry.Tags : item.Tags,
            conflictPolicy = entry != null ? entry.ConflictPolicy : null,
            preview = entry != null ? entry.Preview : null,
            provenance = entry != null ? entry.Provenance : null,
            entry
        };
    }

    private static object BuildCharacterResourceInspectUnity(
        string rootPath,
        string unityCatalogPath,
        string importReportPath,
        JsonElement? unityEntry,
        JsonElement? importOperation,
        ResourceLibraryItem item)
    {
        return new
        {
            unityResourceCatalogPath = ToProjectRelativePath(rootPath, unityCatalogPath),
            importReportPath = ToProjectRelativePath(rootPath, importReportPath),
            unityAssetGuid = GetJsonString(unityEntry, "unityAssetGuid"),
            unityAssetPath = FirstNonEmpty(GetJsonString(unityEntry, "unityAssetPath"), item.UnityAssetPath),
            importerKind = GetJsonString(unityEntry, "importerKind"),
            mainObjectType = GetJsonString(unityEntry, "unityMainObjectType"),
            subAssets = Array.Empty<object>(),
            importStatus = FirstNonEmpty(GetJsonString(unityEntry, "importStatus"), item.ImportStatus.ToString()),
            lastImportOperation = importOperation,
            diagnostics = GetJsonArray(unityEntry, "diagnostics"),
            catalogEntry = unityEntry
        };
    }

    private static object BuildCharacterResourceInspectRuntime(
        ResourceLibraryItem item,
        RuntimeResourceCatalogEntryDocument runtimeEntry,
        AudioCueManifestEntry audioCue,
        List<object> plans)
    {
        List<string> groupNames = GetPlanGroupNames(plans);
        return new
        {
            runtimeBindingKind = item.RuntimeBindingKind,
            runtimeAvailability = item.RuntimeAvailability,
            resourceKey = item.ResourceKey,
            providerId = FirstNonEmpty(runtimeEntry != null ? runtimeEntry.Provider : string.Empty, item.ProviderId),
            address = FirstNonEmpty(runtimeEntry != null ? runtimeEntry.Address : string.Empty, item.UnityAssetPath, item.SourcePath),
            assetType = FirstNonEmpty(runtimeEntry != null ? runtimeEntry.Type : string.Empty, item.Kind),
            hash = FirstNonEmpty(runtimeEntry != null ? runtimeEntry.Hash : string.Empty, item.Hash),
            preloadPolicy = groupNames.Count > 0 ? groupNames[0] : ResourceLibraryPreloadPolicies.None,
            includedRuntimePlanGroups = groupNames,
            runtimeCatalogEntry = runtimeEntry,
            audioCue
        };
    }

    private static List<object> BuildCharacterResourceInspectPlans(ResourceLibraryItem item, CharacterResourcePlanCompileResult result)
    {
        var plans = new List<object>();
        if (item == null || result == null || result.CharacterResourcePlan == null)
            return plans;

        AddPlanGroupIfContains(plans, CharacterResourcePlanGroups.SpawnCritical, result.CharacterResourcePlan.SpawnCritical, item);
        AddPlanGroupIfContains(plans, CharacterResourcePlanGroups.PresentationCritical, result.CharacterResourcePlan.PresentationCritical, item);
        AddPlanGroupIfContains(plans, CharacterResourcePlanGroups.EquipmentInitial, result.CharacterResourcePlan.EquipmentInitial, item);
        AddPlanGroupIfContains(plans, CharacterResourcePlanGroups.AnimationWarmup, result.CharacterResourcePlan.AnimationWarmup, item);
        AddPlanGroupIfContains(plans, CharacterResourcePlanGroups.VfxWarmup, result.CharacterResourcePlan.VfxWarmup, item);
        AddPlanGroupIfContains(plans, CharacterResourcePlanGroups.UiDeferred, result.CharacterResourcePlan.UiDeferred, item);

        AudioCueManifestEntry cue = FindAudioCue(result.AudioCueManifest, item);
        if (cue != null)
        {
            plans.Add(new
            {
                groupName = CharacterResourcePlanGroups.Audio,
                required = result.CharacterResourcePlan.Audio != null && result.CharacterResourcePlan.Audio.Required,
                failurePolicy = result.CharacterResourcePlan.Audio != null ? result.CharacterResourcePlan.Audio.FailurePolicy : string.Empty,
                resource = cue
            });
        }

        return plans;
    }

    private static void AddPlanGroupIfContains(List<object> plans, string groupName, CharacterResourcePlanGroup group, ResourceLibraryItem item)
    {
        if (plans == null || group == null || group.Resources == null || item == null)
            return;

        for (int i = 0; i < group.Resources.Count; i++)
        {
            CharacterResourcePlanResourceRef resource = group.Resources[i];
            if (resource == null)
                continue;

            if (!ResourceMatches(resource.ResourceKey, item, string.Empty) &&
                !ResourceMatches(resource.StableId, item, string.Empty))
                continue;

            plans.Add(new
            {
                groupName,
                required = group.Required,
                failurePolicy = group.FailurePolicy,
                resource
            });
        }
    }

    private static List<string> GetPlanGroupNames(List<object> plans)
    {
        var result = new List<string>();
        if (plans == null)
            return result;

        for (int i = 0; i < plans.Count; i++)
        {
            string groupName = GetAnonymousProperty(plans[i], "groupName");
            if (!string.IsNullOrWhiteSpace(groupName) && !result.Contains(groupName))
                result.Add(groupName);
        }

        return result;
    }

    private static List<ResourceReferenceEdge> FindCharacterResourceReferences(ResourceReferenceGraph graph, ResourceLibraryItem item)
    {
        var references = new List<ResourceReferenceEdge>();
        if (graph == null || graph.Edges == null || item == null)
            return references;

        for (int i = 0; i < graph.Edges.Count; i++)
        {
            ResourceReferenceEdge edge = graph.Edges[i];
            if (edge == null)
                continue;

            if (ResourceMatches(edge.TargetLibraryItemStableId, item, string.Empty) ||
                ResourceMatches(edge.TargetResourceKey, item, string.Empty))
                references.Add(edge);
        }

        return references;
    }

    private static RuntimeResourceCatalogEntryDocument FindRuntimeCatalogEntry(RuntimeResourceCatalogDocument catalog, ResourceLibraryItem item)
    {
        if (catalog == null || catalog.Entries == null || item == null)
            return null;

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            RuntimeResourceCatalogEntryDocument entry = catalog.Entries[i];
            if (entry == null)
                continue;

            if (ResourceMatches(entry.Id, item, string.Empty) ||
                (entry.ProviderData != null && ResourceMatches(GetProviderData(entry.ProviderData, "stableId"), item, string.Empty)) ||
                (entry.ProviderData != null && ResourceMatches(GetProviderData(entry.ProviderData, "packageResourceKey"), item, string.Empty)))
                return entry;
        }

        return null;
    }

    private static AudioCueManifestEntry FindAudioCue(AudioCueManifestDocument manifest, ResourceLibraryItem item)
    {
        if (manifest == null || manifest.Cues == null || item == null)
            return null;

        for (int i = 0; i < manifest.Cues.Count; i++)
        {
            AudioCueManifestEntry cue = manifest.Cues[i];
            if (cue == null)
                continue;

            if (ResourceMatches(cue.CueId, item, string.Empty) ||
                ResourceMatches(cue.StableId, item, string.Empty) ||
                ResourceMatches(cue.ResourceKey, item, string.Empty))
                return cue;
        }

        return null;
    }

    private static string GetProviderData(Dictionary<string, string> providerData, string key)
    {
        if (providerData == null || string.IsNullOrWhiteSpace(key))
            return string.Empty;
        string value;
        return providerData.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
    }

    private static List<object> BuildCharacterResourceInspectDiagnostics(
        ResourceLibraryItem item,
        CharacterResourceLibrary library,
        CharacterResourcePlanCompileResult plan,
        JsonElement? unityEntry)
    {
        var diagnostics = new List<object>();
        if (item == null)
            return diagnostics;

        if (item.Diagnostics != null)
        {
            for (int i = 0; i < item.Diagnostics.Count; i++)
                AddResourceLibraryDiagnostic(diagnostics, "item", item.Diagnostics[i], item);
        }

        if (library != null && library.Diagnostics != null)
        {
            for (int i = 0; i < library.Diagnostics.Count; i++)
                AddResourceLibraryDiagnostic(diagnostics, "library", library.Diagnostics[i], item);
        }

        if (plan != null && plan.CharacterResourcePlan != null && plan.CharacterResourcePlan.Diagnostics != null)
        {
            for (int i = 0; i < plan.CharacterResourcePlan.Diagnostics.Count; i++)
                AddResourcePlanDiagnostic(diagnostics, "resourcePlan", plan.CharacterResourcePlan.Diagnostics[i], item);
        }

        if (plan != null && plan.ResourceValidationReport != null && plan.ResourceValidationReport.Diagnostics != null)
        {
            for (int i = 0; i < plan.ResourceValidationReport.Diagnostics.Count; i++)
                AddResourcePlanDiagnostic(diagnostics, "validationReport", plan.ResourceValidationReport.Diagnostics[i], item);
        }

        if (unityEntry.HasValue)
        {
            JsonElement[] unityDiagnostics = GetJsonArray(unityEntry, "diagnostics");
            for (int i = 0; i < unityDiagnostics.Length; i++)
                AddUnityDiagnostic(diagnostics, unityDiagnostics[i], item);
        }

        return diagnostics;
    }

    private static void AddResourceLibraryDiagnostic(List<object> diagnostics, string source, ResourceLibraryDiagnostic diagnostic, ResourceLibraryItem item)
    {
        if (diagnostics == null || diagnostic == null || item == null)
            return;

        if (!ResourceMatches(diagnostic.LibraryItemStableId, item, string.Empty) &&
            !ResourceMatches(diagnostic.ResourceKey, item, string.Empty))
            return;

        diagnostics.Add(new
        {
            source,
            severity = diagnostic.Severity.ToString(),
            code = diagnostic.Code,
            message = diagnostic.Message,
            suggestedFix = diagnostic.SuggestedFix,
            libraryItemStableId = diagnostic.LibraryItemStableId,
            resourceKey = diagnostic.ResourceKey,
            sourceConfigKind = diagnostic.SourceConfigKind,
            sourceStableId = diagnostic.SourceStableId,
            sourceField = diagnostic.SourceField
        });
    }

    private static void AddResourcePlanDiagnostic(List<object> diagnostics, string source, CharacterResourcePlanDiagnostic diagnostic, ResourceLibraryItem item)
    {
        if (diagnostics == null || diagnostic == null || item == null)
            return;

        if (!ResourceMatches(diagnostic.LibraryItemStableId, item, string.Empty) &&
            !ResourceMatches(diagnostic.ResourceKey, item, string.Empty))
            return;

        diagnostics.Add(new
        {
            source,
            severity = diagnostic.Severity,
            code = diagnostic.Code,
            message = diagnostic.Message,
            suggestedFix = diagnostic.SuggestedFix,
            libraryItemStableId = diagnostic.LibraryItemStableId,
            resourceKey = diagnostic.ResourceKey,
            sourceConfigKind = diagnostic.SourceConfigKind,
            sourceField = diagnostic.SourceField
        });
    }

    private static void AddUnityDiagnostic(List<object> diagnostics, JsonElement diagnostic, ResourceLibraryItem item)
    {
        if (diagnostics == null || item == null || diagnostic.ValueKind != JsonValueKind.Object)
            return;

        diagnostics.Add(new
        {
            source = "unity",
            severity = TryGetString(diagnostic, "severity"),
            code = TryGetString(diagnostic, "code"),
            message = TryGetString(diagnostic, "message"),
            suggestedFix = string.Empty,
            libraryItemStableId = item.StableId,
            resourceKey = item.ResourceKey,
            sourcePath = TryGetString(diagnostic, "sourcePath"),
            sourceField = TryGetString(diagnostic, "field")
        });
    }

    private static JsonElement? FindResourceJsonEntry(string rootPath, string path, ResourceLibraryItem item)
    {
        if (item == null)
            return null;

        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            return null;

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(fullPath));
        if (!document.RootElement.TryGetProperty("entries", out JsonElement entries) || entries.ValueKind != JsonValueKind.Array)
            return null;

        foreach (JsonElement entry in entries.EnumerateArray())
        {
            if (JsonResourceMatches(entry, item))
                return entry.Clone();
        }

        return null;
    }

    private static JsonElement? FindImportReportOperation(
        string rootPath,
        string path,
        ResourceLibraryItem item,
        CharacterPackageResourceEntry authoringEntry,
        JsonElement? unityEntry)
    {
        string fullPath = Path.GetFullPath(path);
        if (item == null || !fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            return null;

        string sourcePath = authoringEntry != null ? authoringEntry.RelativePath : item.SourcePath;
        string unityAssetPath = GetJsonString(unityEntry, "unityAssetPath");
        string address = GetJsonString(unityEntry, "address");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(fullPath));
        if (!document.RootElement.TryGetProperty("operations", out JsonElement operations) || operations.ValueKind != JsonValueKind.Array)
            return null;

        foreach (JsonElement operation in operations.EnumerateArray())
        {
            if (string.Equals(TryGetString(operation, "sourcePath"), sourcePath, StringComparison.Ordinal) ||
                string.Equals(TryGetString(operation, "targetPath"), unityAssetPath, StringComparison.Ordinal) ||
                string.Equals(TryGetString(operation, "targetPath"), address, StringComparison.Ordinal) ||
                string.Equals(TryGetString(operation, "contentHash"), item.Hash, StringComparison.Ordinal))
                return operation.Clone();
        }

        return null;
    }

    private static bool JsonResourceMatches(JsonElement entry, ResourceLibraryItem item)
    {
        if (entry.ValueKind != JsonValueKind.Object || item == null)
            return false;

        return ResourceMatches(TryGetString(entry, "id"), item, string.Empty) ||
            ResourceMatches(TryGetString(entry, "packageResourceKey"), item, string.Empty) ||
            ResourceMatches(TryGetString(entry, "stableId"), item, string.Empty);
    }

    private static string GetJsonString(JsonElement? element, string propertyName)
    {
        if (!element.HasValue)
            return string.Empty;
        return TryGetString(element.Value, propertyName);
    }

    private static JsonElement[] GetJsonArray(JsonElement? element, string propertyName)
    {
        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Object)
            return Array.Empty<JsonElement>();
        if (!element.Value.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.Array)
            return Array.Empty<JsonElement>();

        var result = new List<JsonElement>();
        foreach (JsonElement item in property.EnumerateArray())
            result.Add(item.Clone());
        return result.ToArray();
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return string.Empty;

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i];
        }

        return string.Empty;
    }

    private static ResourceReferenceGraph BuildCharacterResourceReferenceGraph(CharacterResourcePackage package)
    {
        var graph = new ResourceReferenceGraph();
        if (package == null || package.ResourceCatalog == null)
            return graph;

        Dictionary<string, CharacterPackageResourceEntry> byKey = BuildPackageResourceEntryLookup(package.ResourceCatalog);
        CharacterApplicationAuthoringSummary application = package.ApplicationConfig;
        if (application != null && application.ResourceKeys != null)
        {
            string sourceStableId = !string.IsNullOrWhiteSpace(application.CharacterStableId)
                ? application.CharacterStableId
                : package.Manifest != null ? package.Manifest.StableId : string.Empty;
            for (int i = 0; i < application.ResourceKeys.Count; i++)
            {
                string resourceKey = application.ResourceKeys[i];
                AddResourceReferenceEdge(
                    graph,
                    byKey,
                    "character",
                    sourceStableId,
                    "resourceKeys/" + i.ToString(CultureInfo.InvariantCulture),
                    resourceKey,
                    ResourceLibraryPreloadPolicies.SpawnCritical,
                    isRequiredAtRuntime: true);
            }
        }

        CharacterAuthoringGeometry geometry = package.Geometry;
        if (geometry != null && geometry.WeaponAttachments != null)
        {
            for (int i = 0; i < geometry.WeaponAttachments.Count; i++)
            {
                WeaponAttachmentProfile attachment = geometry.WeaponAttachments[i];
                if (attachment == null)
                    continue;

                AddResourceReferenceEdge(
                    graph,
                    byKey,
                    "weapon",
                    attachment.WeaponId,
                    "weaponAttachments/" + i.ToString(CultureInfo.InvariantCulture) + "/previewResourceKey",
                    attachment.PreviewResourceKey,
                    ResourceLibraryPreloadPolicies.EquipmentInitial,
                    isRequiredAtRuntime: true);
            }
        }

        return graph;
    }

    private static Dictionary<string, CharacterPackageResourceEntry> BuildPackageResourceEntryLookup(CharacterPackageResourceCatalog catalog)
    {
        var lookup = new Dictionary<string, CharacterPackageResourceEntry>(StringComparer.Ordinal);
        if (catalog == null || catalog.Entries == null)
            return lookup;

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = catalog.Entries[i];
            if (entry != null && !string.IsNullOrWhiteSpace(entry.ResourceKey))
                lookup[entry.ResourceKey] = entry;
        }

        return lookup;
    }

    private static void AddResourceReferenceEdge(
        ResourceReferenceGraph graph,
        Dictionary<string, CharacterPackageResourceEntry> byKey,
        string sourceConfigKind,
        string sourceStableId,
        string sourceField,
        string resourceKey,
        string preloadPolicy,
        bool isRequiredAtRuntime)
    {
        if (graph == null || string.IsNullOrWhiteSpace(resourceKey))
            return;

        CharacterPackageResourceEntry target;
        byKey.TryGetValue(resourceKey, out target);
        graph.Edges.Add(new ResourceReferenceEdge
        {
            SourceConfigKind = sourceConfigKind ?? string.Empty,
            SourceStableId = sourceStableId ?? string.Empty,
            SourceField = sourceField ?? string.Empty,
            TargetLibraryItemStableId = target != null ? target.StableId : string.Empty,
            TargetResourceKey = resourceKey,
            BindingKind = RuntimeBindingKind.ResourceManagerAsset,
            IsRequiredAtRuntime = isRequiredAtRuntime,
            PreloadPolicy = preloadPolicy ?? ResourceLibraryPreloadPolicies.None
        });
    }

    private static void ApplyCharacterResourceRuntimeState(
        string rootPath,
        string packagePath,
        CharacterResourcePackage package,
        CharacterResourceLibrary library,
        JsonSerializerOptions jsonOptions)
    {
        if (package == null || package.Manifest == null || library == null)
            return;

        string generatedRoot = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterPackages", package.Manifest.PackageId);
        string unityCatalogPath = Path.Combine(generatedRoot, "config", "unity_resource_catalog.json");
        string runtimeCatalogPath = Path.Combine(generatedRoot, "config", "runtime_resource_catalog.json");
        HashSet<string> unityResourceKeys = ReadResourceKeySetFromCatalog(unityCatalogPath, jsonOptions);
        HashSet<string> runtimeResourceKeys = ReadResourceKeySetFromCatalog(runtimeCatalogPath, jsonOptions);
        AddCompiledRuntimeResourceKeys(packagePath, package, runtimeResourceKeys);

        for (int i = 0; i < library.Items.Count; i++)
        {
            ResourceLibraryItem item = library.Items[i];
            if (item == null)
                continue;

            if (item.RuntimeBindingKind == RuntimeBindingKind.ResourceManagerAsset)
            {
                item.ImportStatus = unityResourceKeys.Contains(item.ResourceKey)
                    ? ResourceImportStatus.Clean
                    : ResourceImportStatus.UnityMissing;
                item.RuntimeAvailability = runtimeResourceKeys.Contains(item.ResourceKey)
                    ? ResourceRuntimeAvailability.RuntimeReady
                    : ResourceRuntimeAvailability.RuntimeMissing;
            }
            else if (item.RuntimeBindingKind == RuntimeBindingKind.GeneratedPreviewOnly)
            {
                item.ImportStatus = ResourceImportStatus.Clean;
                item.RuntimeAvailability = ResourceRuntimeAvailability.PreviewOnly;
            }
        }
    }

    private static void AddCompiledRuntimeResourceKeys(
        string packagePath,
        CharacterResourcePackage package,
        HashSet<string> runtimeResourceKeys)
    {
        if (package == null || runtimeResourceKeys == null)
            return;

        CharacterResourcePlanCompileResult result = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = packagePath,
            ValidateResourceFiles = false,
            ValidateResourceHashes = false
        });
        if (result == null || result.RuntimeResourceCatalog == null || result.RuntimeResourceCatalog.Entries == null)
            return;

        for (int i = 0; i < result.RuntimeResourceCatalog.Entries.Count; i++)
        {
            RuntimeResourceCatalogEntryDocument entry = result.RuntimeResourceCatalog.Entries[i];
            if (entry != null && !string.IsNullOrWhiteSpace(entry.Id))
                runtimeResourceKeys.Add(entry.Id);
        }
    }

    private static HashSet<string> ReadResourceKeySetFromCatalog(string path, JsonSerializerOptions jsonOptions)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return keys;

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("entries", out JsonElement entries) || entries.ValueKind != JsonValueKind.Array)
                return keys;

            foreach (JsonElement entry in entries.EnumerateArray())
            {
                string id = TryGetString(entry, "id");
                if (!string.IsNullOrWhiteSpace(id))
                    keys.Add(id);
                string packageResourceKey = TryGetString(entry, "packageResourceKey");
                if (!string.IsNullOrWhiteSpace(packageResourceKey))
                    keys.Add(packageResourceKey);
            }
        }
        catch
        {
        }

        return keys;
    }

    private static string TryGetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(propertyName))
            return string.Empty;
        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
            return string.Empty;
        return property.GetString() ?? string.Empty;
    }

    private static void SaveCharacterPackage(
        string rootPath,
        string packageRelative,
        CharacterResourcePackage package,
        JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        if (!Directory.Exists(packagePath))
            throw new DirectoryNotFoundException("Character package directory was not found: " + packageRelative);

        RefreshCharacterResourceHashes(packagePath, package);

        CharacterAuthoringValidationReport validation = CharacterResourcePackageValidator.Validate(package, new CharacterResourcePackageValidationOptions
        {
            PackageRootPath = packagePath,
            ValidateResourceFiles = true,
            ValidateResourceHashes = false
        });
        if (HasValidationGate(validation, CharacterAuthoringValidationGate.ExportBlocked))
            throw new ArgumentException("ExportBlocked package draft was not saved: " + validation.ToText());

        WriteJsonFileAtomic(Path.Combine(packagePath, "manifest.json"), package.Manifest, jsonOptions);
        WriteJsonFileAtomic(Path.Combine(packagePath, "resource_catalog.json"), package.ResourceCatalog, jsonOptions);
        WriteJsonFileAtomic(Path.Combine(packagePath, "config", "character_application.json"), package.ApplicationConfig, jsonOptions);
        WriteJsonFileAtomic(Path.Combine(packagePath, "geometry", "body_geometry.json"), package.Geometry.BodyProfile, jsonOptions);
        WriteJsonFileAtomic(Path.Combine(packagePath, "geometry", "body_parts.json"), new CharacterBodyPartsFile
        {
            schemaVersion = package.Geometry.SchemaVersion,
            bodyParts = package.Geometry.BodyParts
        }, jsonOptions);
        WriteJsonFileAtomic(Path.Combine(packagePath, "geometry", "body_colliders.json"), new CharacterBodyCollidersFile
        {
            schemaVersion = package.Geometry.SchemaVersion,
            colliders = package.Geometry.Colliders
        }, jsonOptions);
        WriteJsonFileAtomic(Path.Combine(packagePath, "geometry", "sockets.json"), new CharacterSocketsFile
        {
            schemaVersion = package.Geometry.SchemaVersion,
            sockets = package.Geometry.Sockets
        }, jsonOptions);
        WriteJsonFileAtomic(Path.Combine(packagePath, "geometry", "weapon_attachments.json"), new CharacterWeaponAttachmentsFile
        {
            schemaVersion = package.Geometry.SchemaVersion,
            attachments = package.Geometry.WeaponAttachments
        }, jsonOptions);
        WriteJsonFileAtomic(Path.Combine(packagePath, "geometry", "traces.json"), new CharacterTracesFile
        {
            schemaVersion = package.Geometry.SchemaVersion,
            traces = package.Geometry.Traces
        }, jsonOptions);
        WriteJsonFileAtomic(Path.Combine(packagePath, "validation", "last_report.json"), validation, jsonOptions);
    }

    private static void RewriteCharacterTemplatePackage(
        CharacterResourcePackage package,
        string packageId,
        string displayName,
        JsonSerializerOptions jsonOptions)
    {
        if (package == null)
            throw new ArgumentException("character package template is required.");

        package.Manifest ??= new CharacterPackageManifest();
        package.ResourceCatalog ??= new CharacterPackageResourceCatalog();
        package.Geometry ??= new CharacterAuthoringGeometry();
        package.ApplicationConfig ??= new CharacterApplicationAuthoringSummary();

        string oldPackageId = package.Manifest.PackageId ?? string.Empty;
        string oldDisplayName = package.Manifest.DisplayName ?? string.Empty;

        var resourceKeyMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var stableIdMap = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (CharacterPackageResourceEntry entry in package.ResourceCatalog.Entries)
        {
            if (entry == null)
                continue;

            string oldResourceKey = entry.ResourceKey ?? string.Empty;
            string oldStableId = entry.StableId ?? string.Empty;
            string newResourceKey = BuildCharacterResourceKey(oldResourceKey, oldPackageId, packageId, entry.TypeId, entry.LocalId, entry.Variant);
            string newStableId = BuildCharacterResourceStableId(packageId, entry.LocalId);
            if (!string.IsNullOrWhiteSpace(oldResourceKey))
                resourceKeyMap[oldResourceKey] = newResourceKey;
            if (!string.IsNullOrWhiteSpace(oldStableId))
                stableIdMap[oldStableId] = newStableId;

            entry.PackageId = packageId;
            entry.ResourceKey = newResourceKey;
            entry.StableId = newStableId;
        }

        foreach (CharacterPackageResourceEntry entry in package.ResourceCatalog.Entries)
        {
            if (entry == null)
                continue;

            for (int dependencyIndex = 0; dependencyIndex < entry.Dependencies.Count; dependencyIndex++)
            {
                CharacterPackageResourceDependency dependency = entry.Dependencies[dependencyIndex];
                if (dependency == null)
                    continue;
                dependency.ResourceKey = ReplaceMappedValue(dependency.ResourceKey, resourceKeyMap);
            }

            entry.ImportHints ??= new CharacterPackageImportHint();
            for (int labelIndex = 0; labelIndex < entry.ImportHints.Labels.Count; labelIndex++)
            {
                string label = entry.ImportHints.Labels[labelIndex] ?? string.Empty;
                if (string.Equals(label, "package." + oldPackageId, StringComparison.Ordinal))
                    entry.ImportHints.Labels[labelIndex] = "package." + packageId;
            }

            entry.Preview ??= new CharacterPackagePreviewMetadata();
            entry.Preview.ThumbnailResourceKey = ReplaceMappedValue(entry.Preview.ThumbnailResourceKey, resourceKeyMap);
            entry.Preview.PreviewMeshResourceKey = ReplaceMappedValue(entry.Preview.PreviewMeshResourceKey, resourceKeyMap);
            entry.Preview.PlaceholderResourceKey = ReplaceMappedValue(entry.Preview.PlaceholderResourceKey, resourceKeyMap);

            entry.ResourceSelection = RewriteSelection(entry.ResourceSelection, resourceKeyMap, stableIdMap);
        }

        package.Manifest.PackageId = packageId;
        package.Manifest.StableId = "charpkg." + packageId;
        package.Manifest.DisplayName = displayName;
        package.Manifest.Kind = CharacterResourcePackageKind.Character;

        var replacementMap = new Dictionary<string, string>(StringComparer.Ordinal);
        AddReplacement(replacementMap, oldPackageId, packageId);
        AddReplacement(replacementMap, oldDisplayName, displayName);
        AddReplacement(replacementMap, "charpkg." + oldPackageId, "charpkg." + packageId);
        AddReplacement(replacementMap, "char." + oldPackageId, "char." + packageId);
        AddReplacement(replacementMap, "body." + oldPackageId, "body." + packageId);
        AddReplacement(replacementMap, "attr." + oldPackageId, "attr." + packageId);
        AddReplacement(replacementMap, "equip_loadout." + oldPackageId, "equip_loadout." + packageId);
        AddReplacement(replacementMap, "geometry." + oldPackageId, "geometry." + packageId);
        AddReplacement(replacementMap, "animation." + oldPackageId, "animation." + packageId);
        AddReplacement(replacementMap, "skeleton." + oldPackageId, "skeleton." + packageId);
        AddReplacement(replacementMap, "avatar." + oldPackageId, "avatar." + packageId);

        foreach (KeyValuePair<string, string> pair in resourceKeyMap)
            AddReplacement(replacementMap, pair.Key, pair.Value);
        foreach (KeyValuePair<string, string> pair in stableIdMap)
            AddReplacement(replacementMap, pair.Key, pair.Value);

        package.ApplicationConfig = RewriteJsonObject(package.ApplicationConfig, jsonOptions, replacementMap);
        package.Geometry = RewriteJsonObject(package.Geometry, jsonOptions, replacementMap);
        package.ApplicationConfig.ResourceKeys = package.ResourceCatalog.Entries
            .Where(static entry => entry != null && !string.IsNullOrWhiteSpace(entry.ResourceKey))
            .Select(static entry => entry.ResourceKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (WeaponAttachmentProfile attachment in package.Geometry.WeaponAttachments)
        {
            if (attachment == null)
                continue;
            attachment.PreviewResourceKey = ReplaceMappedValue(attachment.PreviewResourceKey, resourceKeyMap);
            attachment.PreviewResourceSelection = RewriteSelection(attachment.PreviewResourceSelection, resourceKeyMap, stableIdMap);
        }
    }

    private static AnimationAuthoringPackage RewriteAnimationTemplatePackage(
        AnimationAuthoringPackage animation,
        string oldPackageId,
        string oldDisplayName,
        string newPackageId,
        string newDisplayName,
        CharacterResourcePackage package,
        JsonSerializerOptions jsonOptions)
    {
        if (animation == null)
            return new AnimationAuthoringPackage();

        var replacementMap = new Dictionary<string, string>(StringComparer.Ordinal);
        AddReplacement(replacementMap, oldPackageId, newPackageId);
        AddReplacement(replacementMap, oldDisplayName, newDisplayName);
        AddReplacement(replacementMap, "charpkg." + oldPackageId, "charpkg." + newPackageId);
        AddReplacement(replacementMap, "animation." + oldPackageId, "animation." + newPackageId);
        AddReplacement(replacementMap, "skeleton." + oldPackageId, "skeleton." + newPackageId);
        AddReplacement(replacementMap, "avatar." + oldPackageId, "avatar." + newPackageId);

        if (package?.ResourceCatalog?.Entries != null)
        {
            foreach (CharacterPackageResourceEntry entry in package.ResourceCatalog.Entries)
            {
                if (entry == null)
                    continue;
                AddReplacement(replacementMap, "charpkg." + oldPackageId + ".resource." + CharacterPackageResourceKeyGenerator.NormalizeSegment(entry.LocalId), entry.StableId);
                AddReplacement(replacementMap, "char." + oldPackageId, "char." + newPackageId);
            }
        }

        animation = RewriteJsonObject(animation, jsonOptions, replacementMap);
        animation.PackageId = "animation." + newPackageId;
        animation.StableId = "charpkg." + newPackageId + ".animation";
        animation.DisplayName = newDisplayName + " Animation";
        if (!string.IsNullOrWhiteSpace(animation.SkeletonProfileId))
            animation.SkeletonProfileId = animation.SkeletonProfileId.Replace(oldPackageId, newPackageId, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(animation.AvatarProfileId))
            animation.AvatarProfileId = animation.AvatarProfileId.Replace(oldPackageId, newPackageId, StringComparison.Ordinal);
        return animation;
    }

    private static T RewriteJsonObject<T>(T value, JsonSerializerOptions jsonOptions, IReadOnlyDictionary<string, string> replacements)
    {
        if (value == null || replacements == null || replacements.Count == 0)
            return value;

        string json = JsonSerializer.Serialize(value, jsonOptions);
        foreach (KeyValuePair<string, string> pair in replacements.OrderByDescending(static item => item.Key.Length))
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.Equals(pair.Key, pair.Value, StringComparison.Ordinal))
                continue;
            json = json.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
        }

        return JsonSerializer.Deserialize<T>(json, jsonOptions) ?? value;
    }

    private static AuthoringResourceSelectionRef RewriteSelection(
        AuthoringResourceSelectionRef selection,
        IReadOnlyDictionary<string, string> resourceKeyMap,
        IReadOnlyDictionary<string, string> stableIdMap)
    {
        selection ??= new AuthoringResourceSelectionRef();
        selection.PackageResourceKey = ReplaceMappedValue(selection.PackageResourceKey, resourceKeyMap);
        selection.ResourceStableId = ReplaceMappedValue(selection.ResourceStableId, stableIdMap);
        return selection;
    }

    private static string ReplaceMappedValue(string value, IReadOnlyDictionary<string, string> map)
    {
        if (string.IsNullOrWhiteSpace(value) || map == null)
            return value ?? string.Empty;
        return map.TryGetValue(value, out string replacement) ? replacement : value;
    }

    private static void AddReplacement(IDictionary<string, string> replacements, string from, string to)
    {
        if (string.IsNullOrWhiteSpace(from) || string.Equals(from, to, StringComparison.Ordinal))
            return;
        replacements[from] = to ?? string.Empty;
    }

    private static string BuildCharacterResourceStableId(string packageId, string localId)
    {
        return "charpkg." + packageId + ".resource." + CharacterPackageResourceKeyGenerator.NormalizeSegment(localId);
    }

    private static string BuildCharacterResourceKey(string oldResourceKey, string oldPackageId, string newPackageId, string typeId, string localId, string variant)
    {
        string oldPrefix = "char." + oldPackageId + ".";
        if (!string.IsNullOrWhiteSpace(oldPackageId)
            && !string.IsNullOrWhiteSpace(oldResourceKey)
            && oldResourceKey.StartsWith(oldPrefix, StringComparison.Ordinal))
        {
            return "char." + newPackageId + "." + oldResourceKey.Substring(oldPrefix.Length);
        }

        return CharacterPackageResourceKeyGenerator.Generate(newPackageId, typeId, localId, variant);
    }

    private static string NormalizeCharacterPackageId(string packageId)
    {
        string normalized = CharacterPackageResourceKeyGenerator.NormalizeSegment(packageId).Replace('.', '_');
        return string.Equals(normalized, "unknown", StringComparison.Ordinal) ? string.Empty : normalized;
    }

    private static string NormalizeCharacterPackageDirectoryName(string directoryName, string packageId)
    {
        string slug = CharacterPackageResourceKeyGenerator.NormalizeSegment(directoryName);
        if (string.IsNullOrWhiteSpace(slug) || string.Equals(slug, "unknown", StringComparison.Ordinal))
            slug = CharacterPackageResourceKeyGenerator.NormalizeSegment(packageId).Replace('.', '-').Replace('_', '-');
        if (!slug.StartsWith("character-", StringComparison.Ordinal))
            slug = "character-" + slug;
        return slug;
    }

    private static string BuildDefaultCharacterDisplayName(string packageId)
    {
        TextInfo textInfo = CultureInfo.InvariantCulture.TextInfo;
        string[] parts = packageId.Split(new[] { '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "New Character";
        return string.Join(" ", parts.Select(part => textInfo.ToTitleCase(part)));
    }

    private static string ResolveCharacterTemplateRelative(string template)
    {
        return string.Equals(template, "creature", StringComparison.OrdinalIgnoreCase)
            ? "Tools/MxFramework.Authoring/samples/character-slime"
            : "Tools/MxFramework.Authoring/samples/character-iron-vanguard";
    }

    private static void CopyDirectoryRecursive(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);
        foreach (string directory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourcePath, directory);
            Directory.CreateDirectory(Path.Combine(destinationPath, relative));
        }

        foreach (string file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourcePath, file);
            string targetFile = Path.Combine(destinationPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(file, targetFile, overwrite: false);
        }
    }

    private static string GetTemplatePackageId(string templatePath, JsonSerializerOptions jsonOptions)
    {
        string manifestPath = Path.Combine(templatePath, "manifest.json");
        if (!File.Exists(manifestPath))
            return string.Empty;
        CharacterPackageManifest manifest = JsonSerializer.Deserialize<CharacterPackageManifest>(File.ReadAllText(manifestPath), jsonOptions) ?? new CharacterPackageManifest();
        return manifest.PackageId ?? string.Empty;
    }

    private static string GetTemplateDisplayName(string templatePath, JsonSerializerOptions jsonOptions)
    {
        string manifestPath = Path.Combine(templatePath, "manifest.json");
        if (!File.Exists(manifestPath))
            return string.Empty;
        CharacterPackageManifest manifest = JsonSerializer.Deserialize<CharacterPackageManifest>(File.ReadAllText(manifestPath), jsonOptions) ?? new CharacterPackageManifest();
        return manifest.DisplayName ?? string.Empty;
    }

    private static void RefreshCharacterResourceHashes(string packagePath, CharacterResourcePackage package)
    {
        CharacterPackageResourceCatalog catalog = package.ResourceCatalog ?? new CharacterPackageResourceCatalog();
        package.ResourceCatalog = catalog;

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = catalog.Entries[i];
            if (entry == null)
                continue;

            entry.Hashes ??= new CharacterPackageResourceHashes();
            entry.Hashes.Algorithm = "sha256";

            string fullPath = CharacterPackageResourcePipeline.ResolvePackagePath(packagePath, entry.RelativePath);
            if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
            {
                entry.Hash = CharacterPackageHashUtility.ComputeFileSha256(fullPath);
                entry.Hashes.ContentHash = entry.Hash;
            }

            if (string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase))
            {
                entry.ImportHints ??= new CharacterPackageImportHint();
                entry.ImportHints.ModelWrapperPose ??= new CharacterAuthoringLocalPose();
            }

            entry.Hashes.ImportHash = CharacterPackageResourcePipeline.ComputeImportHash(entry);
        }

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = catalog.Entries[i];
            if (entry == null)
                continue;

            entry.Hashes ??= new CharacterPackageResourceHashes();
            entry.Hashes.DependencyHash = CharacterPackageResourcePipeline.ComputeDependencyHash(entry, catalog);
        }
    }

    private static CharacterPackageResourceEntry ImportCharacterModel(
        string rootPath,
        string packageRelative,
        CharacterStudioModelImportRequest request,
        JsonSerializerOptions jsonOptions)
    {
        if (string.IsNullOrWhiteSpace(request.fileName))
            throw new ArgumentException("fileName is required.");
        if (string.IsNullOrWhiteSpace(request.bytesBase64))
            throw new ArgumentException("bytesBase64 is required.");

        string sourceExtension = Path.GetExtension(request.fileName).ToLowerInvariant();
        if (sourceExtension != ".glb" && sourceExtension != ".gltf" && sourceExtension != ".fbx")
            throw new ArgumentException("Only .glb, .gltf, and .fbx model files are supported.");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(request.bytesBase64);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("bytesBase64 is not valid base64.", ex);
        }

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        if (!Directory.Exists(packagePath))
            throw new DirectoryNotFoundException("Character package directory was not found: " + packageRelative);

        string fileStem = CharacterPackageResourceKeyGenerator.NormalizeSegment(Path.GetFileNameWithoutExtension(request.fileName)).Replace('.', '_');
        string targetExtension = sourceExtension;
        byte[] packageModelBytes = bytes;
        bool convertedFromFbx = sourceExtension == ".fbx";
        if (convertedFromFbx)
        {
            packageModelBytes = ConvertFbxToGlb(rootPath, fileStem, request.fileName, bytes);
            targetExtension = ".glb";
        }

        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        string contentHash = CharacterPackageHashUtility.ComputeSha256(packageModelBytes);
        string defaultRelativePath = ("resources/models/" + fileStem + targetExtension).Replace('\\', '/');
        CharacterPackageResourceEntry entry = ResolveModelImportEntry(package, request.role, fileStem, contentHash, defaultRelativePath, request.resourceKey);
        string existingExtension = Path.GetExtension(entry.RelativePath ?? string.Empty);
        string relativePath = !string.IsNullOrWhiteSpace(request.resourceKey)
            && !string.IsNullOrWhiteSpace(entry.RelativePath)
            && string.Equals(existingExtension, targetExtension, StringComparison.OrdinalIgnoreCase)
            ? entry.RelativePath
            : defaultRelativePath;
        string outputPath = Path.Combine(packagePath, relativePath);
        bool shouldWriteFile = !File.Exists(outputPath)
            || !string.Equals(CharacterPackageHashUtility.ComputeFileSha256(outputPath), contentHash, StringComparison.OrdinalIgnoreCase);
        if (shouldWriteFile)
            WriteBytesAtomic(outputPath, packageModelBytes);

        ApplyModelImportEntry(package, entry, request, relativePath, contentHash, targetExtension, convertedFromFbx);
        SaveCharacterPackage(rootPath, packageRelative, package, jsonOptions);
        return entry;
    }

    private static byte[] ConvertFbxToGlb(string rootPath, string fileStem, string originalFileName, byte[] bytes)
    {
        string converterPath = ResolveFbx2GltfPath(rootPath);
        string conversionRoot = Path.Combine(rootPath, "Temp", "MxFrameworkAuthoring", "CharacterStudio", "fbx-imports", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(conversionRoot);

        string sourceName = string.IsNullOrWhiteSpace(fileStem) ? "model" : fileStem;
        string sourcePath = Path.Combine(conversionRoot, sourceName + ".fbx");
        string outputBasePath = Path.Combine(conversionRoot, sourceName);
        string outputPath = outputBasePath + ".glb";
        File.WriteAllBytes(sourcePath, bytes);

        var startInfo = new ProcessStartInfo(converterPath)
        {
            WorkingDirectory = conversionRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--input");
        startInfo.ArgumentList.Add(sourcePath);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputBasePath);
        startInfo.ArgumentList.Add("--binary");

        using Process process = Process.Start(startInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to launch FBX2glTF converter.");

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(120000);
        if (!exited)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            try { process.WaitForExit(); } catch { }
            throw new InvalidOperationException("FBX to GLB conversion timed out: " + originalFileName);
        }

        string stdout = stdoutTask.GetAwaiter().GetResult();
        string stderr = stderrTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0 || !File.Exists(outputPath))
        {
            string detail = string.Join("\n", new[] { stdout, stderr }.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (string.IsNullOrWhiteSpace(detail))
                detail = "FBX2glTF did not produce a GLB output.";
            throw new InvalidOperationException("FBX to GLB conversion failed for " + originalFileName + ": " + detail);
        }

        return File.ReadAllBytes(outputPath);
    }

    private static string ResolveFbx2GltfPath(string rootPath)
    {
        string overridePath = Environment.GetEnvironmentVariable("MXFRAMEWORK_FBX2GLTF") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            string fullOverridePath = Path.GetFullPath(overridePath);
            if (File.Exists(fullOverridePath))
                return fullOverridePath;
            throw new FileNotFoundException("MXFRAMEWORK_FBX2GLTF points to a missing converter executable.", fullOverridePath);
        }

        string platformDirectory;
        string executableName = "FBX2glTF";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            platformDirectory = "Darwin";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            platformDirectory = "Windows_NT";
            executableName = "FBX2glTF.exe";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            platformDirectory = "Linux";
        else
            throw new PlatformNotSupportedException("FBX conversion is only wired for macOS, Linux, and Windows.");

        string packageConverterPath = Path.Combine(rootPath, "Tools", "MxFramework.CharacterStudio", "node_modules", "fbx2gltf", "bin", platformDirectory, executableName);
        if (File.Exists(packageConverterPath))
            return packageConverterPath;

        throw new FileNotFoundException("FBX conversion requires FBX2glTF. Run `npm --prefix Tools/MxFramework.CharacterStudio install`, or set MXFRAMEWORK_FBX2GLTF to the converter executable.", packageConverterPath);
    }

    private static CharacterPackageResourceEntry ResolveModelImportEntry(
        CharacterResourcePackage package,
        string role,
        string fileStem,
        string contentHash,
        string relativePath,
        string requestedResourceKey)
    {
        CharacterPackageResourceCatalog catalog = package.ResourceCatalog ?? new CharacterPackageResourceCatalog();
        package.ResourceCatalog = catalog;
        string normalizedRole = string.IsNullOrWhiteSpace(role) ? "preview" : role.Trim();

        CharacterPackageResourceEntry entry = null;
        if (!string.IsNullOrWhiteSpace(requestedResourceKey))
            entry = CharacterPackageResourcePipeline.FindByKey(catalog, requestedResourceKey);

        if (normalizedRole.Equals("body", StringComparison.OrdinalIgnoreCase))
        {
            entry ??= catalog.Entries.FirstOrDefault(item =>
                item != null && (string.Equals(item.Usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.LocalId, "model.body", StringComparison.OrdinalIgnoreCase)));
        }
        else if (normalizedRole.Equals("mainHand", StringComparison.OrdinalIgnoreCase) || normalizedRole.Equals("offHand", StringComparison.OrdinalIgnoreCase))
        {
            WeaponAttachmentProfile attachment = package.Geometry?.WeaponAttachments?.FirstOrDefault(item =>
                item != null && string.Equals(item.EquipSlot, normalizedRole, StringComparison.OrdinalIgnoreCase));
            if (attachment != null && !string.IsNullOrWhiteSpace(attachment.PreviewResourceKey))
                entry ??= CharacterPackageResourcePipeline.FindByKey(catalog, attachment.PreviewResourceKey);
        }

        entry ??= FindModelResourceByContentHash(catalog, contentHash);
        entry ??= catalog.Entries.FirstOrDefault(item =>
            item != null
            && string.Equals(item.TypeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizePackageRelativePath(item.RelativePath), NormalizePackageRelativePath(relativePath), StringComparison.Ordinal));

        if (entry == null)
        {
            entry = new CharacterPackageResourceEntry();
            catalog.Entries.Add(entry);
        }

        return entry;
    }

    private static void ApplyModelImportEntry(
        CharacterResourcePackage package,
        CharacterPackageResourceEntry entry,
        CharacterStudioModelImportRequest request,
        string relativePath,
        string contentHash,
        string extension,
        bool convertedFromFbx)
    {
        string packageId = package.Manifest?.PackageId ?? string.Empty;
        string normalizedRole = string.IsNullOrWhiteSpace(request.role) ? "preview" : request.role.Trim();
        bool isBody = normalizedRole.Equals("body", StringComparison.OrdinalIgnoreCase);
        bool isWeapon = normalizedRole.Equals("mainHand", StringComparison.OrdinalIgnoreCase) || normalizedRole.Equals("offHand", StringComparison.OrdinalIgnoreCase);
        string fileStem = CharacterPackageResourceKeyGenerator.NormalizeSegment(Path.GetFileNameWithoutExtension(request.fileName));
        string localId = isBody ? "model.body" : (isWeapon ? ("weapon." + normalizedRole + ".model") : ("model." + fileStem));

        if (string.IsNullOrWhiteSpace(entry.ResourceKey))
            entry.ResourceKey = isBody
                ? CharacterPackageResourceKeyGenerator.Generate(packageId, CharacterPackageResourceTypeIds.Model, "body")
                : CharacterPackageResourceKeyGenerator.Generate(packageId, CharacterPackageResourceTypeIds.Model, localId);
        if (string.IsNullOrWhiteSpace(entry.LocalId))
            entry.LocalId = localId;
        if (string.IsNullOrWhiteSpace(entry.StableId))
            entry.StableId = "charpkg." + CharacterPackageResourceKeyGenerator.NormalizeSegment(packageId) + ".resource." + CharacterPackageResourceKeyGenerator.NormalizeSegment(entry.LocalId);

        entry.TypeId = CharacterPackageResourceTypeIds.Model;
        entry.Variant = string.IsNullOrWhiteSpace(entry.Variant) ? "default" : entry.Variant;
        entry.Usage = isBody
            ? CharacterPackageResourceUsageIds.CharacterModel
            : (isWeapon ? CharacterPackageResourceUsageIds.WeaponModel : CharacterPackageResourceUsageIds.PreviewMesh);
        entry.SourceFormat = extension.TrimStart('.');
        entry.PackageId = packageId;
        entry.RelativePath = relativePath;
        entry.ImportHints ??= new CharacterPackageImportHint();
        entry.ImportHints.TargetPathPolicy = CharacterPackageImportTargetPathPolicies.GeneratedCharacterPackage;
        entry.ImportHints.TargetRelativePath = relativePath;
        entry.ImportHints.Scale = entry.ImportHints.Scale <= 0 ? 1f : entry.ImportHints.Scale;
        entry.ImportHints.ModelWrapperPose ??= new CharacterAuthoringLocalPose();
        entry.ImportHints.ProviderId = string.IsNullOrWhiteSpace(entry.ImportHints.ProviderId) ? "unityAsset" : entry.ImportHints.ProviderId;
        entry.ImportHints.MaterialPolicy = string.IsNullOrWhiteSpace(entry.ImportHints.MaterialPolicy) ? "importEmbeddedOrPackageMaterials" : entry.ImportHints.MaterialPolicy;
        entry.ImportHints.CollisionPolicy = string.IsNullOrWhiteSpace(entry.ImportHints.CollisionPolicy) ? "authoringGeometryOnly" : entry.ImportHints.CollisionPolicy;
        entry.ImportHints.PhysicsDataPolicy = string.IsNullOrWhiteSpace(entry.ImportHints.PhysicsDataPolicy) ? "separateGeometryBinding" : entry.ImportHints.PhysicsDataPolicy;
        entry.ImportHints.UpAxis = string.IsNullOrWhiteSpace(entry.ImportHints.UpAxis) ? "Y+" : entry.ImportHints.UpAxis;
        entry.ImportHints.ForwardAxis = string.IsNullOrWhiteSpace(entry.ImportHints.ForwardAxis) ? "Z+" : entry.ImportHints.ForwardAxis;
        entry.Tags ??= new List<string>();
        AddTag(entry.Tags, "characterstudio-import");
        AddTag(entry.Tags, normalizedRole);
        if (convertedFromFbx)
            AddTag(entry.Tags, "converted-from-fbx");

        entry.Hash = contentHash;
        entry.Hashes ??= new CharacterPackageResourceHashes();
        entry.Hashes.Algorithm = "sha256";
        entry.Hashes.ContentHash = entry.Hash;
        entry.Hashes.ImportHash = CharacterPackageResourcePipeline.ComputeImportHash(entry);
        entry.Hashes.DependencyHash = CharacterPackageResourcePipeline.ComputeDependencyHash(entry, package.ResourceCatalog);

        entry.Provenance ??= new CharacterPackageResourceProvenance();
        entry.Provenance.SourceTool = convertedFromFbx ? "CharacterStudio/FBX2glTF" : "CharacterStudio";
        entry.Provenance.SourceFile = request.fileName;
        string now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(entry.Provenance.CreatedUtc))
            entry.Provenance.CreatedUtc = now;
        entry.Provenance.ModifiedUtc = now;

        if (isWeapon && package.Geometry?.WeaponAttachments != null)
        {
            WeaponAttachmentProfile attachment = package.Geometry.WeaponAttachments.FirstOrDefault(item =>
                item != null && string.Equals(item.EquipSlot, normalizedRole, StringComparison.OrdinalIgnoreCase));
            if (attachment != null)
                attachment.PreviewResourceKey = entry.ResourceKey;
        }
    }

    private static void TouchResourceProvenance(CharacterPackageResourceEntry entry, string sourceTool, string sourceFile)
    {
        if (entry == null)
            return;

        entry.Provenance ??= new CharacterPackageResourceProvenance();
        if (!string.IsNullOrWhiteSpace(sourceTool))
            entry.Provenance.SourceTool = sourceTool;
        if (!string.IsNullOrWhiteSpace(sourceFile))
            entry.Provenance.SourceFile = sourceFile;
        string now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(entry.Provenance.CreatedUtc))
            entry.Provenance.CreatedUtc = now;
        entry.Provenance.ModifiedUtc = now;
    }

    private static CharacterPackageResourceEntry FindModelResourceByContentHash(CharacterPackageResourceCatalog catalog, string contentHash)
    {
        string normalizedHash = CharacterPackageHashUtility.NormalizeSha256(contentHash);
        if (catalog == null || string.IsNullOrWhiteSpace(normalizedHash))
            return null;

        return catalog.Entries.FirstOrDefault(item =>
            item != null
            && string.Equals(item.TypeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase)
            && string.Equals(CharacterPackageHashUtility.NormalizeSha256(CharacterPackageResourcePipeline.GetDeclaredContentHash(item)), normalizedHash, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePackageRelativePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').TrimStart('/');
    }

    private static void AddTag(List<string> tags, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return;
        if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            tags.Add(tag);
    }

    private static object RunCharacterImport(
        string rootPath,
        string packageRelative,
        CharacterStudioImportRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        string cliProject = Path.Combine(rootPath, "Tools", "MxFramework.Authoring", "src", "MxFramework.Authoring.Cli", "MxFramework.Authoring.Cli.csproj");
        string reportOut = Path.Combine(rootPath, "Temp", "MxFrameworkAuthoring", "CharacterStudio", "import-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(reportOut);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = rootPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(cliProject);
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("character");
        startInfo.ArgumentList.Add("import-unity");
        startInfo.ArgumentList.Add("--package");
        startInfo.ArgumentList.Add(packagePath);
        startInfo.ArgumentList.Add("--project-root");
        startInfo.ArgumentList.Add(rootPath);
        startInfo.ArgumentList.Add("--unity-root");
        startInfo.ArgumentList.Add(string.IsNullOrWhiteSpace(request.unityRoot) ? "Assets/MxFrameworkGenerated/CharacterPackages" : request.unityRoot);
        startInfo.ArgumentList.Add("--report-out");
        startInfo.ArgumentList.Add(reportOut);
        startInfo.ArgumentList.Add("--check-files");
        if (request.checkHashes)
            startInfo.ArgumentList.Add("--check-hashes");
        if (request.dryRun)
            startInfo.ArgumentList.Add("--dry-run");

        using Process process = Process.Start(startInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to launch character import command.");

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(120000);
        if (!exited)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            try { process.WaitForExit(); } catch { }
            string timedOutStdout = stdoutTask.GetAwaiter().GetResult();
            string timedOutStderr = stderrTask.GetAwaiter().GetResult();
            return new { success = false, timedOut = true, exitCode = -1, stdout = timedOutStdout, stderr = timedOutStderr, reportOut };
        }

        string stdout = stdoutTask.GetAwaiter().GetResult();
        string stderr = stderrTask.GetAwaiter().GetResult();
        string reportPath = Path.Combine(reportOut, "import_report.json");
        return new
        {
            success = process.ExitCode == 0,
            timedOut = false,
            exitCode = process.ExitCode,
            stdout,
            stderr,
            reportOut = Path.GetRelativePath(rootPath, reportOut).Replace('\\', '/'),
            report = File.Exists(reportPath) ? ReadOptionalJson(rootPath, reportPath) : null
        };
    }

    private static void WriteJsonFileAtomic<T>(string path, T value, JsonSerializerOptions jsonOptions)
    {
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, jsonOptions) + Environment.NewLine);
        if (File.Exists(path))
            File.Replace(temp, path, null);
        else
            File.Move(temp, path);
    }

    private static void WriteBytesAtomic(string path, byte[] bytes)
    {
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        string temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        if (File.Exists(path))
            File.Replace(temp, path, null);
        else
            File.Move(temp, path);
    }

    private static bool HasValidationGate(CharacterAuthoringValidationReport report, CharacterAuthoringValidationGate gate)
    {
        if (report == null || report.Issues == null)
            return false;
        for (int i = 0; i < report.Issues.Count; i++)
        {
            if (report.Issues[i] != null && report.Issues[i].Gate == gate)
                return true;
        }

        return false;
    }

    private static object ReadState(string rootPath, string packageRelative)
    {
        string manifestPath = Path.Combine(rootPath, ManifestRelativePath);
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        PackageReadResult package = PackageReader.Read(packagePath);
        string reportsPath = Path.Combine(packagePath, "reports");

        return new
        {
            manifest = ManifestReader.Read(manifestPath),
            mod = package.Manifest,
            patch = package.Patches.Count > 0 ? package.Patches[0] : new PatchDocument { Source = "BuffFactoryData" },
            validation = ReadOptionalJson(rootPath, Path.Combine(reportsPath, "validation_report.json")),
            mergePreview = ReadOptionalJson(rootPath, Path.Combine(reportsPath, "merge_preview.json")),
            reportIndex = ReadOptionalJson(rootPath, Path.Combine(reportsPath, "report_index.json")),
            packageRelative,
            canWrite = true
        };
    }

    private static void SavePatch(string rootPath, string packageRelative, PatchDocument patch, JsonSerializerOptions jsonOptions)
    {
        if (string.IsNullOrWhiteSpace(patch.SchemaVersion))
            patch.SchemaVersion = "1.0";
        if (string.IsNullOrWhiteSpace(patch.Source))
            patch.Source = "BuffFactoryData";
        for (int i = 0; i < patch.Entries.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(patch.Entries[i].Source))
                patch.Entries[i].Source = patch.Source;
            if (string.IsNullOrWhiteSpace(patch.Entries[i].Layer))
                patch.Entries[i].Layer = "Mod";
        }

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        string patchPath = Path.Combine(packagePath, "patches", "buff.patch.json");
        Directory.CreateDirectory(Path.GetDirectoryName(patchPath)!);
        File.WriteAllText(patchPath, JsonSerializer.Serialize(patch, jsonOptions));
    }

    private static void WriteReport(string rootPath, string packageRelative)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        string manifestPath = Path.Combine(rootPath, ManifestRelativePath);
        ProjectAuthoringManifest manifest = File.Exists(manifestPath) ? ManifestReader.Read(manifestPath) : null;
        PackageReadResult package = PackageReader.Read(packagePath);
        ValidationReport validation = AuthoringValidate.Run(manifest, package.Manifest, package.Patches);
        var bundle = new ReportBundle
        {
            Package = package.Manifest,
            Validation = validation,
            MergePreviews = package.Patches.Select(PatchMerger.Merge).ToList()
        };
        ReportBundleWriter.Write(Path.Combine(packagePath, "reports"), bundle);
    }

    private static ValidationReport ValidateDraft(string rootPath, string packageRelative, PatchDocument draft)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        string manifestPath = Path.Combine(rootPath, ManifestRelativePath);
        ProjectAuthoringManifest manifest = File.Exists(manifestPath) ? ManifestReader.Read(manifestPath) : null;
        PackageReadResult package = PackageReader.Read(packagePath);
        if (string.IsNullOrWhiteSpace(draft.Source))
            draft.Source = "BuffFactoryData";
        for (int i = 0; i < draft.Entries.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(draft.Entries[i].Source))
                draft.Entries[i].Source = draft.Source;
            if (string.IsNullOrWhiteSpace(draft.Entries[i].Layer))
                draft.Entries[i].Layer = "Mod";
        }
        return AuthoringValidate.Run(manifest, package.Manifest, new[] { draft });
    }

    internal static object ReadPreviewStatus(JsonSerializerOptions jsonOptions)
    {
        PreviewConnectionDescriptor descriptor = PreviewConnectionLocator.TryRead();
        if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Endpoint))
        {
            return new
            {
                connected = false,
                status = "unavailable",
                message = "未发现运行中的 Unity Preview Server。请在 Unity 中执行 MxFramework/Runtime Preview/Start Server。"
            };
        }

        try
        {
            using var client = new WebSocketPreviewClient(new Uri(descriptor.Endpoint), descriptor.Token);
            HandshakeResult handshake = client.HandshakeAsync("MxAuthoringEditor", "0.3.0").GetAwaiter().GetResult();
            return new
            {
                connected = true,
                status = "connected",
                descriptor.Endpoint,
                descriptor.Port,
                descriptor.ProcessId,
                descriptor.GameVersion,
                descriptor.StartedAt,
                handshake
            };
        }
        catch (PreviewProtocolException ex)
        {
            string status = IsUnavailablePreviewException(ex) ? "unavailable" : "error";
            return new
            {
                connected = false,
                status,
                message = ex.Message,
                code = ex.ErrorCode,
                descriptor.Endpoint,
                descriptor.Port
            };
        }
        catch (Exception ex)
        {
            return new
            {
                connected = false,
                status = "unavailable",
                message = "无法连接 Unity Preview Server：" + ex.Message,
                descriptor.Endpoint,
                descriptor.Port
            };
        }
    }

    internal static object RunPreview(string rootPath, string packageRelative, string buffId, string casterId, string targetId, int stack, int waitTicks, JsonSerializerOptions jsonOptions)
    {
        PreviewConnectionDescriptor descriptor = PreviewConnectionLocator.TryRead();
        if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Endpoint))
        {
            return new
            {
                connected = false,
                success = false,
                status = "unavailable",
                message = "未发现运行中的 Unity Preview Server。请在 Unity 中执行 MxFramework/Runtime Preview/Start Server。"
            };
        }

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        PackageReadResult package = PackageReader.Read(packagePath);
        string effectiveBuffId = string.IsNullOrWhiteSpace(buffId) ? FirstBuffId(package) : buffId;
        if (string.IsNullOrWhiteSpace(effectiveBuffId))
        {
            return new { connected = true, success = false, status = "blocked", message = "当前包没有可预览的 Buff。", descriptor.Endpoint };
        }

        try
        {
            using var client = new WebSocketPreviewClient(new Uri(descriptor.Endpoint), descriptor.Token);
            HandshakeResult handshake = client.HandshakeAsync("MxAuthoringEditor", "0.3.0").GetAwaiter().GetResult();
            client.ResetAsync(new ResetParams { ReloadBase = false }).GetAwaiter().GetResult();
            LoadPatchResult load = client.LoadPatchAsync(CreateLoadParams(package, jsonOptions)).GetAwaiter().GetResult();
            RuntimePreviewResult apply = client.ApplyBuffAsync(new ApplyBuffParams
            {
                BuffId = effectiveBuffId,
                CasterId = casterId,
                TargetId = targetId,
                Stack = stack,
                WaitTicks = waitTicks
            }).GetAwaiter().GetResult();
            GetLogsResult logs = client.GetLogsAsync(new GetLogsParams { AfterSeq = 0, Max = 100 }).GetAwaiter().GetResult();

            return new
            {
                connected = true,
                success = apply.Success,
                status = apply.Success ? "ready" : "failed",
                previewMode = apply.PreviewMode,
                descriptor.Endpoint,
                descriptor.Port,
                handshake,
                load,
                result = apply,
                logs
            };
        }
        catch (PreviewProtocolException ex)
        {
            RuntimePreviewResult failureResult = TryReadRuntimePreviewResult(ex.ErrorData, jsonOptions);
            string previewMode = ReadStringProperty(ex.ErrorData, "previewMode");
            string reason = ReadStringProperty(ex.ErrorData, "reason");
            bool unavailable = IsUnavailablePreviewException(ex);
            return new
            {
                connected = !unavailable,
                success = false,
                status = unavailable ? "unavailable" : "failed",
                message = ex.Message,
                code = ex.ErrorCode,
                reason,
                previewMode = !string.IsNullOrWhiteSpace(failureResult?.PreviewMode) ? failureResult.PreviewMode : previewMode,
                descriptor.Endpoint,
                descriptor.Port,
                error = new
                {
                    code = ex.ErrorCode,
                    message = ex.Message,
                    reason,
                    previewMode,
                    data = ex.ErrorData
                },
                result = failureResult
            };
        }
        catch (Exception ex)
        {
            return new
            {
                connected = false,
                success = false,
                status = "unavailable",
                message = "无法连接 Unity Preview Server：" + ex.Message,
                descriptor.Endpoint,
                descriptor.Port
            };
        }
    }

    private static bool IsUnavailablePreviewException(PreviewProtocolException ex)
    {
        return ex is PreviewConnectionException ||
            ex is PreviewTimeoutException;
    }

    private static RuntimePreviewResult TryReadRuntimePreviewResult(JsonElement? data, JsonSerializerOptions jsonOptions)
    {
        if (data == null || data.Value.ValueKind != JsonValueKind.Object)
            return null;
        if (!data.Value.TryGetProperty("result", out JsonElement result) || result.ValueKind != JsonValueKind.Object)
            return null;
        try
        {
            return JsonSerializer.Deserialize<RuntimePreviewResult>(result.GetRawText(), jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string ReadStringProperty(JsonElement? data, string propertyName)
    {
        if (data == null || data.Value.ValueKind != JsonValueKind.Object)
            return string.Empty;
        if (!data.Value.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            return string.Empty;
        return value.GetString() ?? string.Empty;
    }

    private static LoadPatchParams CreateLoadParams(PackageReadResult package, JsonSerializerOptions jsonOptions)
    {
        var patches = new List<JsonElement>();
        for (int i = 0; i < package.Patches.Count; i++)
        {
            string text = JsonSerializer.Serialize(package.Patches[i], jsonOptions);
            patches.Add(JsonDocument.Parse(text).RootElement.Clone());
        }

        return new LoadPatchParams
        {
            PackageId = package.Manifest?.PackageId ?? string.Empty,
            Kind = package.Manifest?.Kind.ToString() ?? "Preview",
            SchemaVersion = package.Manifest?.SchemaVersion ?? "1.0",
            Patches = patches,
            DiscardPrevious = true
        };
    }

    private static string FirstBuffId(PackageReadResult package)
    {
        for (int i = 0; i < package.Patches.Count; i++)
        {
            for (int j = 0; j < package.Patches[i].Entries.Count; j++)
            {
                PatchEntry entry = package.Patches[i].Entries[j];
                if (!string.IsNullOrWhiteSpace(entry.Id))
                    return entry.Id;
            }
        }

        return string.Empty;
    }

    private static JsonElement? ReadOptionalJson(string rootPath, string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            return null;
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(fullPath));
        return doc.RootElement.Clone();
    }

    private static T ReadOptionalJsonFile<T>(string rootPath, string path, JsonSerializerOptions jsonOptions)
        where T : class
    {
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            return null;

        return JsonSerializer.Deserialize<T>(File.ReadAllText(fullPath), jsonOptions);
    }

    private static string ToProjectRelativePath(string rootPath, string path)
    {
        string fullRoot = Path.GetFullPath(rootPath);
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            return fullPath.Replace('\\', '/');
        return Path.GetRelativePath(fullRoot, fullPath).Replace('\\', '/');
    }

    private static void ServeStatic(HttpListenerResponse response, string rootPath, string requestPath)
    {
        string relative = Uri.UnescapeDataString(requestPath.TrimStart('/')).Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(rootPath, relative));
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            WriteText(response, "Forbidden", "text/plain", 403);
            return;
        }

        if (Directory.Exists(fullPath))
            fullPath = Path.Combine(fullPath, "index.html");

        if (!File.Exists(fullPath))
        {
            WriteText(response, "Not Found", "text/plain", 404);
            return;
        }

        byte[] bytes = File.ReadAllBytes(fullPath);
        response.StatusCode = 200;
        response.ContentType = GetContentType(fullPath);
        response.ContentLength64 = bytes.Length;
        response.Headers["X-Mx-Authoring"] = "1";
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.Close();
    }

    private static void WriteJson<T>(HttpListenerResponse response, T value, JsonSerializerOptions jsonOptions, int statusCode = 200)
    {
        string text = JsonSerializer.Serialize(value, jsonOptions);
        WriteText(response, text, "application/json; charset=utf-8", statusCode);
    }

    private static void ScheduleEditorRestart(string rootPath, int port, string defaultPackage)
    {
        if (Interlocked.Exchange(ref s_restartScheduled, 1) != 0)
            return;

        ProcessStartInfo restart = BuildEditorRestartProcessStartInfo(rootPath, port, defaultPackage);
        Process.Start(restart);

        _ = Task.Run(async () =>
        {
            await Task.Delay(300);
            Environment.Exit(0);
        });
    }

    private static ProcessStartInfo BuildEditorRestartProcessStartInfo(string rootPath, int port, string defaultPackage)
    {
        string restartScriptPath = Path.Combine(rootPath, "Tools", "MxFramework.EditorHub", "start-editor-hub.bat");
        if (File.Exists(restartScriptPath))
        {
            string command = "set MXFRAMEWORK_EDITOR_HUB_OPEN_BROWSER=0"
                + " && ping 127.0.0.1 -n 3 >nul"
                + " && start \"\" \"" + restartScriptPath + "\" " + port.ToString(CultureInfo.InvariantCulture) + " \"" + defaultPackage.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
            return new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c " + command,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
        }

        string processPath = Environment.ProcessPath ?? string.Empty;
        string entryAssemblyPath = System.Reflection.Assembly.GetEntryAssembly()?.Location ?? string.Empty;
        string executablePath = !string.IsNullOrWhiteSpace(processPath) ? processPath : entryAssemblyPath;
        if (string.IsNullOrWhiteSpace(executablePath))
            throw new InvalidOperationException("Unable to determine authoring executable path for restart.");

        string fallbackCommand = "ping 127.0.0.1 -n 3 >nul && start \"\" \"" + executablePath + "\" editor serve --root \"" + rootPath.Replace("\"", "\"\"", StringComparison.Ordinal) + "\" --port " + port.ToString(CultureInfo.InvariantCulture) + " --package \"" + defaultPackage.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        return new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c " + fallbackCommand,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
    }

    private static void WriteText(HttpListenerResponse response, string text, string contentType, int statusCode)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        response.StatusCode = statusCode;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        response.Headers["X-Mx-Authoring"] = "1";
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.Close();
    }

    private static string GetContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            _ => "application/octet-stream"
        };
    }

    private sealed class ResourceLibraryWriteRequest
    {
        public string package { get; set; } = string.Empty;
        public string id { get; set; } = string.Empty;
        public string libraryItemId { get; set; } = string.Empty;
        public string stableId { get; set; } = string.Empty;
        public string resourceKey { get; set; } = string.Empty;
        public string fileName { get; set; } = string.Empty;
        public string role { get; set; } = string.Empty;
        public string kind { get; set; } = string.Empty;
        public string usage { get; set; } = string.Empty;
        public string localId { get; set; } = string.Empty;
        public string targetRelativePath { get; set; } = string.Empty;
        public string bytesBase64 { get; set; } = string.Empty;
        public List<string> tags { get; set; } = new();
        public bool allowFormatChange { get; set; }
        public bool dryRun { get; set; }
        public bool importUnity { get; set; }
        public bool checkHashes { get; set; }
    }

    private sealed class ExternalImportStageRequest
    {
        public string package { get; set; } = string.Empty;
        public string sourceRootLabel { get; set; } = string.Empty;
        public long maxFileSizeBytes { get; set; } = 512L * 1024L * 1024L;
        public List<AuthoringExternalImportStagingFile> files { get; set; } = new();
    }

    private sealed class AuthoringResourceSelectionRequest
    {
        public string package { get; set; } = string.Empty;
        public AuthoringResourceFieldSpec fieldSpec { get; set; } = new AuthoringResourceFieldSpec();
        public AuthoringResourceConsumerContext context { get; set; } = new AuthoringResourceConsumerContext();
        public AuthoringResourceSelectionRef selection { get; set; } = new AuthoringResourceSelectionRef();
    }

    private sealed class AnimationAuthoringSaveRequest
    {
        public string package { get; set; } = string.Empty;
        public AnimationAuthoringPackage animation { get; set; }
    }

    private sealed class ResourceLibraryWriteException : Exception
    {
        public ResourceLibraryWriteException(string code, string message, int statusCode)
            : base(message)
        {
            Code = code;
            StatusCode = statusCode;
        }

        public string Code { get; }
        public int StatusCode { get; }
    }

    private sealed class ResourceIdentitySnapshot
    {
        public string ResourceKey { get; private init; } = string.Empty;
        public string StableId { get; private init; } = string.Empty;
        public string LocalId { get; private init; } = string.Empty;
        public string RelativePath { get; private init; } = string.Empty;
        public string Hash { get; private init; } = string.Empty;
        public string SourceFormat { get; private init; } = string.Empty;
        public string TypeId { get; private init; } = string.Empty;
        public string Usage { get; private init; } = string.Empty;

        public static ResourceIdentitySnapshot FromEntry(CharacterPackageResourceEntry entry)
        {
            return new ResourceIdentitySnapshot
            {
                ResourceKey = entry?.ResourceKey ?? string.Empty,
                StableId = entry?.StableId ?? string.Empty,
                LocalId = entry?.LocalId ?? string.Empty,
                RelativePath = entry?.RelativePath ?? string.Empty,
                Hash = entry?.Hash ?? string.Empty,
                SourceFormat = entry?.SourceFormat ?? string.Empty,
                TypeId = entry?.TypeId ?? string.Empty,
                Usage = entry?.Usage ?? string.Empty
            };
        }

        public string[] GetChangedFields(CharacterPackageResourceEntry entry)
        {
            var changed = new List<string>();
            AddIfChanged(changed, nameof(ResourceKey), ResourceKey, entry?.ResourceKey);
            AddIfChanged(changed, nameof(StableId), StableId, entry?.StableId);
            AddIfChanged(changed, nameof(LocalId), LocalId, entry?.LocalId);
            AddIfChanged(changed, nameof(RelativePath), RelativePath, entry?.RelativePath);
            AddIfChanged(changed, nameof(Hash), Hash, entry?.Hash);
            AddIfChanged(changed, nameof(SourceFormat), SourceFormat, entry?.SourceFormat);
            AddIfChanged(changed, nameof(TypeId), TypeId, entry?.TypeId);
            AddIfChanged(changed, nameof(Usage), Usage, entry?.Usage);
            return changed.ToArray();
        }

        private static void AddIfChanged(List<string> changed, string name, string before, string after)
        {
            if (!string.Equals(before ?? string.Empty, after ?? string.Empty, StringComparison.Ordinal))
                changed.Add(name);
        }
    }

    private sealed class CharacterStudioSaveRequest
    {
        public CharacterResourcePackage package { get; set; }
    }

    private sealed class CharacterStudioCreateRequest
    {
        public string packageId { get; set; } = string.Empty;
        public string displayName { get; set; } = string.Empty;
        public string directoryName { get; set; } = string.Empty;
        public string template { get; set; } = "humanoid";
    }

    private sealed class CharacterStudioImportRequest
    {
        public string package { get; set; } = string.Empty;
        public string unityRoot { get; set; } = string.Empty;
        public bool checkHashes { get; set; }
        public bool dryRun { get; set; }
    }

    private sealed class CharacterStudioModelImportRequest
    {
        public string fileName { get; set; } = string.Empty;
        public string role { get; set; } = "body";
        public string resourceKey { get; set; } = string.Empty;
        public string bytesBase64 { get; set; } = string.Empty;
    }

    private sealed class CharacterBodyPartsFile
    {
        public string schemaVersion { get; set; } = "1.0";
        public List<CharacterBodyPartAuthoring> bodyParts { get; set; } = new();
    }

    private sealed class CharacterBodyCollidersFile
    {
        public string schemaVersion { get; set; } = "1.0";
        public List<CharacterBodyColliderProfile> colliders { get; set; } = new();
    }

    private sealed class CharacterSocketsFile
    {
        public string schemaVersion { get; set; } = "1.0";
        public List<CharacterSocketProfile> sockets { get; set; } = new();
    }

    private sealed class CharacterWeaponAttachmentsFile
    {
        public string schemaVersion { get; set; } = "1.0";
        public List<WeaponAttachmentProfile> attachments { get; set; } = new();
    }

    private sealed class CharacterTracesFile
    {
        public string schemaVersion { get; set; } = "1.0";
        public List<WeaponTraceProfile> traces { get; set; } = new();
    }

    private sealed class ExportRequest
    {
        public string package { get; set; } = string.Empty;
        public string sourceId { get; set; } = string.Empty;
        public string layer { get; set; } = string.Empty;
    }

    private sealed class ModDiagnoseRequest
    {
        public List<string> containers { get; set; } = new();
        public string loadout { get; set; } = string.Empty;
        public bool includeAbsolutePaths { get; set; }
    }

    private static int CountBuffs(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("buffs", out var arr))
                return arr.GetArrayLength();
        }
        catch { }
        return 0;
    }

    private static int CountMods(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("modifiers", out var arr))
                return arr.GetArrayLength();
        }
        catch { }
        return 0;
    }
}
