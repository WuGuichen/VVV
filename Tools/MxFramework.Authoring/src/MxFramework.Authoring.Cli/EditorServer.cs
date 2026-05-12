using System.Net;
using System.Text;
using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.Authoring.Preview;
using MxFramework.Authoring.Preview.Protocol;

namespace MxFramework.Authoring.Cli;

internal static class EditorServer
{
    private const string DefaultPackageRelativePath = "Tools/MxFramework.Authoring/samples/buff-preview";
    private const string ManifestRelativePath = "Tools/MxFramework.Authoring/samples/project-manifest/project-authoring-manifest.json";
    private const string SamplesRelativePath = "Tools/MxFramework.Authoring/samples";

    public static int Serve(string root, int port, JsonSerializerOptions jsonOptions, string defaultPackageRelativePath = DefaultPackageRelativePath)
    {
        string rootPath = Path.GetFullPath(root);
        string defaultPackage = string.IsNullOrEmpty(defaultPackageRelativePath) ? DefaultPackageRelativePath : defaultPackageRelativePath;
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        Console.WriteLine("MxFramework Authoring Editor");
        Console.WriteLine($"URL: http://127.0.0.1:{port}/Tools/MxFramework.Authoring.Editor/web/");
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
            context.Response.Redirect("/Tools/MxFramework.Authoring.Editor/web/");
            context.Response.Close();
            return;
        }

        string packageRelative = ResolvePackageRelative(context, rootPath, defaultPackage);

        if (path == "/api/packages" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, ListPackages(rootPath), jsonOptions);
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

    private static string ResolveSafePath(string rootPath, string relative)
    {
        string fullPath = Path.GetFullPath(Path.Combine(rootPath, relative));
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
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
