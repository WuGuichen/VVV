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

        if (path == "/api/character/packages" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, ListCharacterPackages(rootPath, defaultPackage, jsonOptions), jsonOptions);
            return;
        }

        if (path == "/api/character/state" && context.Request.HttpMethod == "GET")
        {
            string characterPackage = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadCharacterState(rootPath, characterPackage, jsonOptions), jsonOptions);
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

        string importReportPath = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterPackages", package.Manifest.PackageId, "package_cache", "import_report.json");

        return new
        {
            packageRelative,
            canWrite = true,
            package,
            validation,
            importReport = File.Exists(importReportPath) ? ReadOptionalJson(rootPath, importReportPath) : null
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

    private static void ImportCharacterModel(
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

        string relativePath = ("resources/models/" + fileStem + targetExtension).Replace('\\', '/');
        string outputPath = Path.Combine(packagePath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllBytes(outputPath, packageModelBytes);

        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterPackageResourceEntry entry = ResolveModelImportEntry(package, request.role, fileStem);
        ApplyModelImportEntry(package, entry, request, relativePath, outputPath, targetExtension, convertedFromFbx);
        SaveCharacterPackage(rootPath, packageRelative, package, jsonOptions);
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

    private static CharacterPackageResourceEntry ResolveModelImportEntry(CharacterResourcePackage package, string role, string fileStem)
    {
        CharacterPackageResourceCatalog catalog = package.ResourceCatalog ?? new CharacterPackageResourceCatalog();
        package.ResourceCatalog = catalog;
        string normalizedRole = string.IsNullOrWhiteSpace(role) ? "preview" : role.Trim();

        CharacterPackageResourceEntry entry = null;
        if (normalizedRole.Equals("body", StringComparison.OrdinalIgnoreCase))
        {
            entry = catalog.Entries.FirstOrDefault(item =>
                item != null && (string.Equals(item.Usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.LocalId, "model.body", StringComparison.OrdinalIgnoreCase)));
        }
        else if (normalizedRole.Equals("mainHand", StringComparison.OrdinalIgnoreCase) || normalizedRole.Equals("offHand", StringComparison.OrdinalIgnoreCase))
        {
            WeaponAttachmentProfile attachment = package.Geometry?.WeaponAttachments?.FirstOrDefault(item =>
                item != null && string.Equals(item.EquipSlot, normalizedRole, StringComparison.OrdinalIgnoreCase));
            if (attachment != null && !string.IsNullOrWhiteSpace(attachment.PreviewResourceKey))
                entry = CharacterPackageResourcePipeline.FindByKey(catalog, attachment.PreviewResourceKey);
        }

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
        string outputPath,
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

        entry.Hash = CharacterPackageHashUtility.ComputeFileSha256(outputPath);
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

    private sealed class CharacterStudioSaveRequest
    {
        public CharacterResourcePackage package { get; set; }
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
