using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MxFramework.Authoring;
using MxFramework.ResourceMapping.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class CharacterPackageCommands
{
    private const string CharacterPaletteBindingRelativePath =
        "config/character_palette_binding.json";
    private const string CharacterPaletteUsageRelativePath =
        "config/character_palette_usage.json";
    private const string CharacterMagicaClothRelativePath =
        "config/character_cloth_magica.json";

    public static int Dispatch(string[] args, JsonSerializerOptions options)
    {
        if (args.Length >= 2 && args[1] == "body-presence")
            return DispatchBodyPresence(args, options);
        if (args.Length >= 2 && args[1] == "body-part-damage")
            return DispatchBodyPartDamage(args, options);

        if (args.Length >= 2 && args[1] == "project-common")
        {
            string packagePath = Program.RequireOption(args, "--package");
            string sourcePath = Program.RequireOption(args, "--source");
            string outputPath = Program.RequireOption(args, "--out");
            JsonNode source = JsonNode.Parse(File.ReadAllText(sourcePath))
                ?? throw new InvalidDataException("Common action source is empty.");
            string projection = ProjectCharacterActionRuntimeIdentities(
                source,
                TryLoadIdentityCatalog(null, packagePath),
                LoadAudioCueIds(packagePath),
                LoadAbilityIds(packagePath),
                LoadVfxDefinitionIds(packagePath),
                options);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
            File.WriteAllText(outputPath, projection);
            Console.WriteLine("projection=" + Path.GetFullPath(outputPath));
            return Program.ExitReady;
        }

        if (args.Length >= 2 && args[1] == "inspect")
        {
            string packagePath = Program.RequireOption(args, "--package");
            CharacterResourcePackage package = ReadPackage(packagePath, options);
            var summary = new
            {
                package.Manifest.PackageId,
                package.Manifest.StableId,
                package.Manifest.Version,
                package.Manifest.Kind,
                package.Manifest.PackageSchemaVersion,
                package.Manifest.AuthoringSchemaVersion,
                resourceCount = package.ResourceCatalog.Entries.Count,
                bodyPartCount = package.Geometry.BodyParts.Count,
                colliderCount = package.Geometry.Colliders.Count,
                socketCount = package.Geometry.Sockets.Count,
                weaponAttachmentCount = package.Geometry.WeaponAttachments.Count,
                traceCount = package.Geometry.Traces.Count
            };
            Console.WriteLine(JsonSerializer.Serialize(summary, options));
            return Program.ExitReady;
        }

        if (args.Length >= 2 && args[1] == "validate")
        {
            string packagePath = Program.RequireOption(args, "--package");
            CharacterResourcePackage package = ReadPackage(packagePath, options);
            bool checkHashes = Program.HasFlag(args, "--check-hashes");
            var validationOptions = new CharacterResourcePackageValidationOptions
            {
                PackageRootPath = packagePath,
                ValidateResourceFiles = Program.HasFlag(args, "--check-files") || checkHashes,
                ValidateResourceHashes = checkHashes
            };
            CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package, validationOptions);
            Console.Write(report.ToText());
            return report.HasBlockingIssues ? Program.ExitValidationBlocked : Program.ExitReady;
        }

        if (args.Length >= 2 && args[1] == "resources")
        {
            if (args.Length >= 3 && args[2] == "plan")
                return ResourcesPlan(args, options);

            string packagePath = Program.RequireOption(args, "--package");
            CharacterResourcePackage package = ReadPackage(packagePath, options);
            CharacterPackageDependencyGraph graph = CharacterPackageResourcePipeline.BuildDependencyGraph(package.ResourceCatalog);
            var summary = new
            {
                package.Manifest.PackageId,
                package.Manifest.StableId,
                package.Manifest.Version,
                resourceCount = package.ResourceCatalog.Entries.Count,
                graph,
                entries = package.ResourceCatalog.Entries.Select(entry => new
                {
                    entry.ResourceKey,
                    entry.LocalId,
                    entry.StableId,
                    entry.TypeId,
                    entry.Usage,
                    entry.SourceFormat,
                    entry.RelativePath,
                    declaredContentHash = CharacterPackageResourcePipeline.GetDeclaredContentHash(entry),
                    importHash = CharacterPackageResourcePipeline.ComputeImportHash(entry),
                    dependencyHash = CharacterPackageResourcePipeline.ComputeDependencyHash(entry, package.ResourceCatalog)
                }).ToList()
            };
            Console.WriteLine(JsonSerializer.Serialize(summary, options));
            return Program.ExitReady;
        }

        if (args.Length >= 2 && args[1] == "hash")
        {
            string packagePath = Program.RequireOption(args, "--package");
            CharacterResourcePackage package = ReadPackage(packagePath, options);
            CharacterPackageResourceHashReport report = CharacterPackageResourcePipeline.BuildHashReport(package, packagePath);
            Console.WriteLine(JsonSerializer.Serialize(report, options));
            return report.HasBlockingIssues ? Program.ExitValidationBlocked : Program.ExitReady;
        }

        if (args.Length >= 2 && args[1] == "compile")
        {
            string packagePath = Program.RequireOption(args, "--package");
            string outDir = Program.GetOption(args, "--out", string.Empty);
            bool checkHashes = Program.HasFlag(args, "--check-hashes");
            CharacterResourcePackage package = ReadPackage(packagePath, options);
            CharacterContentIdentityCatalogSnapshot catalog = TryLoadIdentityCatalog(null, packagePath);
            CharacterAuthoringCompileResult result = CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest
            {
                Package = package,
                PackageRootPath = packagePath,
                IdentityCatalog = catalog,
                Options = new CharacterAuthoringCompileOptions
                {
                    Strict = Program.HasFlag(args, "--strict"),
                    AllowWarnings = !Program.HasFlag(args, "--no-warnings"),
                    ValidateResourceFiles = Program.HasFlag(args, "--check-files") || checkHashes,
                    ValidateResourceHashes = checkHashes,
                    GeneratedRootPath = Program.GetOption(args, "--unity-root", "Assets/MxFrameworkGenerated/CharacterPackages"),
                    TargetUnityPathPolicy = Program.GetOption(args, "--target-path-policy", CharacterPackageImportTargetPathPolicies.GeneratedCharacterPackage)
                }
            });

            if (!string.IsNullOrWhiteSpace(outDir))
            {
                WriteCompileOutputs(outDir, result, options);
                Console.WriteLine("compileResult=" + Path.Combine(outDir, "compile_result.json"));
                Console.WriteLine("status=" + result.Status);
            }
            else
            {
                Console.WriteLine(JsonSerializer.Serialize(result, options));
            }

            return result.GateReport.ExportBlocked || result.GateReport.ImportBlocked
                ? Program.ExitValidationBlocked
                : Program.ExitReady;
        }

        if (args.Length >= 2 && args[1] == "import-unity")
        {
            return ImportUnity(args, options);
        }

        if (args.Length >= 2 && args[1] == "palette-usage-audit")
        {
            return PaletteUsageAudit(args, options);
        }

        if (args.Length >= 2 && args[1] == "schema")
        {
            var schema = new
            {
                schemas = CharacterResourcePackageSchemas.CreateAll(),
                enums = CharacterResourcePackageSchemas.CreateEnumDomains()
            };
            Console.WriteLine(JsonSerializer.Serialize(schema, options));
            return Program.ExitReady;
        }

        Console.Error.WriteLine("error: unknown character command.");
        return Program.ExitToolError;
    }

    private static int ResourcesPlan(string[] args, JsonSerializerOptions options)
    {
        string packagePath = Program.RequireOption(args, "--package");
        string projectRoot = Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory());
        string outDir = Program.GetOption(args, "--out", string.Empty);
        bool checkHashes = Program.HasFlag(args, "--check-hashes");
        CharacterResourcePackage package = ReadPackage(packagePath, options);
        AuthoringResourceCollection authoringResources = BuildCharacterPackageAuthoringResources(projectRoot, packagePath, package, options);
        CharacterResourcePlanCompileResult result = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = packagePath,
            AuthoringResources = authoringResources,
            ValidateResourceFiles = Program.HasFlag(args, "--check-files") || checkHashes,
            ValidateResourceHashes = checkHashes
        });

        if (!string.IsNullOrWhiteSpace(outDir))
        {
            WriteResourcePlanOutputs(outDir, result, options);
            Console.WriteLine("runtimeResourceCatalog=" + Path.Combine(outDir, "runtime_resource_catalog.json"));
            Console.WriteLine("characterResourcePlan=" + Path.Combine(outDir, "character_resource_plan.json"));
            Console.WriteLine("audioCueManifest=" + Path.Combine(outDir, "audio_cue_manifest.json"));
            Console.WriteLine("resourceValidationReport=" + Path.Combine(outDir, "resource_validation_report.json"));
            Console.WriteLine("status=" + result.ResourceValidationReport.Status);
            Console.WriteLine("planHash=" + result.CharacterResourcePlan.PlanHash);
        }
        else
        {
            Console.WriteLine(JsonSerializer.Serialize(result, options));
        }

        return result.ResourceValidationReport.HasErrors
            ? Program.ExitValidationBlocked
            : Program.ExitReady;
    }

    private static int ImportUnity(string[] args, JsonSerializerOptions options)
    {
        string packagePath = Program.RequireOption(args, "--package");
        string projectRoot = Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory());
        string generatedRoot = Program.GetOption(args, "--unity-root", "Assets/MxFrameworkGenerated/CharacterPackages");
        string reportOut = Program.GetOption(args, "--report-out", string.Empty);
        string diagnosticsOut = Program.GetOption(args, "--diagnostics-out", string.Empty);
        string migrationBackup = Program.GetOption(args, "--migration-backup", string.Empty);
        bool checkHashes = Program.HasFlag(args, "--check-hashes");

        if (!string.IsNullOrWhiteSpace(diagnosticsOut))
        {
            string diagnosticsError = ValidateDiagnosticsTarget(diagnosticsOut, projectRoot);
            if (!string.IsNullOrEmpty(diagnosticsError))
            {
                Console.Error.WriteLine("diagnosticsOut=" + diagnosticsOut);
                Console.Error.WriteLine("error=" + diagnosticsError);
                return Program.ExitValidationBlocked;
            }
        }

        CharacterResourcePackage package = ReadPackage(packagePath, options);
        AuthoringResourceCollection authoringResources = BuildCharacterPackageAuthoringResources(projectRoot, packagePath, package, options);
        CharacterContentIdentityCatalogSnapshot catalog = TryLoadIdentityCatalog(projectRoot, packagePath);
        CharacterAuthoringCompileResult result = CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest
        {
            Package = package,
            PackageRootPath = packagePath,
            IdentityCatalog = catalog,
            Options = new CharacterAuthoringCompileOptions
            {
                Strict = Program.HasFlag(args, "--strict"),
                AllowWarnings = !Program.HasFlag(args, "--no-warnings"),
                ValidateResourceFiles = Program.HasFlag(args, "--check-files") || checkHashes,
                ValidateResourceHashes = checkHashes,
                GeneratedRootPath = generatedRoot,
                TargetUnityPathPolicy = Program.GetOption(args, "--target-path-policy", CharacterPackageImportTargetPathPolicies.GeneratedCharacterPackage)
            }
        });

        var payloads = new CharacterPackageImportPayloads();
        List<CharacterUnityImportWriteInput> additionalWrites = CreateUnityImportAdditionalWrites(
            packagePath,
            package,
            result,
            authoringResources,
            options,
            identityCatalog: catalog,
            projectRoot: projectRoot,
            includeFullSnapshots: !string.IsNullOrWhiteSpace(diagnosticsOut),
            payloads: payloads);

        // 投影/版本校验必须早于桥接写任何项目文件；失败时旧文件与 .meta 原样保留。
        if (!string.IsNullOrEmpty(payloads.AnimationRuntimePlanDiagnostic))
        {
            Console.Error.WriteLine("animationRuntimeResourcePlan=" + CharacterAnimationRuntimeResourcePlanDiagnostics.Invalid);
            Console.Error.WriteLine("error=" + payloads.AnimationRuntimePlanDiagnostic);
            Console.Error.WriteLine("note=No project file was written; the previous animation resource plan and its meta were preserved.");
            return Program.ExitValidationBlocked;
        }

        CharacterUnityImportReport report = CharacterUnityImportBridge.Execute(new CharacterUnityImportBridgeRequest
        {
            CompileResult = result,
            PackageRootPath = packagePath,
            ProjectRootPath = projectRoot,
            SourcePackageVersion = package.Manifest != null ? package.Manifest.Version : string.Empty,
            DryRun = Program.HasFlag(args, "--dry-run"),
            GeneratedArtifacts = CreateGeneratedArtifacts(result, options),
            AdditionalWrites = additionalWrites
        });

        string projectReportPath = CombineProjectPath(result.UnityImportWritePlan.TargetRootPath, "package_cache/import_report.json");
        report.ReportPath = projectReportPath;
        string reportJson = JsonSerializer.Serialize(report, options);
        string reportText = CreateUnityImportReportText(report);

        if (report.CanWriteToUnityProject && !report.DryRun && report.ErrorCount == 0 && report.ConflictCount == 0)
        {
            PerformWriteAfterVerify(projectRoot, result.UnityImportWritePlan, report);
            reportJson = JsonSerializer.Serialize(report, options);
            reportText = CreateUnityImportReportText(report);
            WriteProjectText(projectRoot, projectReportPath, reportJson);
            WriteProjectText(projectRoot, CombineProjectPath(result.UnityImportWritePlan.TargetRootPath, "package_cache/import_report.txt"), reportText);
        }

        ResourceMappingCharacterSyncResult resourceMappingSync = null;
        if (!report.DryRun && report.ErrorCount == 0 && report.ConflictCount == 0 &&
            !result.GateReport.ExportBlocked && !result.GateReport.ImportBlocked)
        {
            resourceMappingSync = ResourceMappingCharacterCoordinator.ApplyFromCompileResult(
                projectRoot,
                result,
                Program.GetOption(args, "--build-target", string.Empty));
            Console.WriteLine("resourceMapping=" + JsonSerializer.Serialize(resourceMappingSync, options));
        }

        bool importCompleted = !report.DryRun
            && report.CanWriteToUnityProject
            && report.ErrorCount == 0
            && report.ConflictCount == 0
            && !result.GateReport.ExportBlocked
            && !result.GateReport.ImportBlocked
            && (resourceMappingSync == null || resourceMappingSync.Success);

        CharacterPackageExportCleanupResult cleanup = null;
        bool summaryFailed = false;
        if (importCompleted)
        {
            string targetRoot = result.UnityImportWritePlan.TargetRootPath;
            cleanup = CharacterPackageExportCleanup.Run(
                Path.GetFullPath(CombineProjectPath(projectRoot, targetRoot)),
                result.PackageId,
                targetRoot,
                report,
                dryRun: false,
                backupRoot: migrationBackup,
                runId: DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture),
                protectedRelativePaths: CollectDeferredPreviewPaths(package));

            bool cleanupSucceeded = ApplyCleanupToReport(report, cleanup, targetRoot);
            reportJson = JsonSerializer.Serialize(report, options);
            reportText = CreateUnityImportReportText(report);
            WriteProjectText(projectRoot, projectReportPath, reportJson);
            WriteProjectText(projectRoot, CombineProjectPath(targetRoot, "package_cache/import_report.txt"), reportText);

            int deferredCount = cleanup.CountOf(CharacterPackageExportCleanupResults.Deferred);
            Console.WriteLine("exportCleanup=deleted:" + cleanup.CountOf(CharacterPackageExportCleanupResults.Deleted)
                + " missing:" + cleanup.CountOf(CharacterPackageExportCleanupResults.Missing)
                + " deferred:" + deferredCount
                + " conflict:" + cleanup.CountOf(CharacterPackageExportCleanupResults.Conflict)
                + " failed:" + cleanup.CountOf(CharacterPackageExportCleanupResults.Failed)
                + " backup:" + cleanup.BackupRoot
                + (cleanup.BackupIsVolatile ? " (volatile Temp path)" : string.Empty));

            if (deferredCount > 0)
            {
                Console.WriteLine("exportCleanupPending=" + deferredCount
                    + " (Unity reference verification and targeted deletion are still required; CLI file stage only)");
            }

            summaryFailed = !TryWriteCompileSummary(projectRoot, targetRoot, payloads.Summary, options, out _);

            if (!cleanupSucceeded)
                Console.Error.WriteLine("exportCleanup=Failed; migration was not completed. Retry after inspecting the backup.");
        }
        else if (!report.DryRun)
        {
            Console.WriteLine("exportCleanup=Skipped (import did not complete successfully)");
        }

        if (!string.IsNullOrWhiteSpace(diagnosticsOut) && importCompleted)
        {
            WriteCharacterPackageDiagnostics(diagnosticsOut, package, result, payloads, reportJson, reportText, options);
        }

        if (!string.IsNullOrWhiteSpace(reportOut))
        {
            Directory.CreateDirectory(reportOut);
            File.WriteAllText(Path.Combine(reportOut, "import_report.json"), reportJson);
            File.WriteAllText(Path.Combine(reportOut, "import_report.txt"), reportText);
        }

        Console.Write(reportText);
        return report.Status == "Failed" || result.GateReport.ExportBlocked || result.GateReport.ImportBlocked || (resourceMappingSync != null && !resourceMappingSync.Success) || summaryFailed
            ? Program.ExitValidationBlocked
            : Program.ExitReady;
    }

    internal static CharacterResourcePackage ReadPackage(string packagePath, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new ArgumentException("--package is required.");
        if (!Directory.Exists(packagePath))
            throw new DirectoryNotFoundException("Character package directory was not found: " + packagePath);

        var package = new CharacterResourcePackage
        {
            PackagePath = Path.GetFullPath(packagePath),
            Manifest = ReadRequired<CharacterPackageManifest>(packagePath, "manifest.json", options),
            ResourceCatalog = ReadOptional<CharacterPackageResourceCatalog>(packagePath, "resource_catalog.json", options) ?? new CharacterPackageResourceCatalog(),
            Geometry = ReadGeometry(packagePath, options),
            ApplicationConfig = ReadOptional<CharacterApplicationAuthoringSummary>(packagePath, Path.Combine("config", "character_application.json"), options) ?? new CharacterApplicationAuthoringSummary(),
            BaseAttributes = ReadOptional<CharacterBaseAttributesAuthoring>(packagePath, CharacterBaseAttributesAuthoring.RelativePath, options),
            TacticalDefaults = ReadOptional<MxFramework.CharacterApplication.CharacterTacticalDefaults>(packagePath, MxFramework.CharacterApplication.CharacterTacticalDefaults.RelativePath, options),
            AnimationAuthoring = ReadOptional<AnimationAuthoringPackage>(packagePath, Path.Combine("config", "animation_authoring.json"), options) ?? new AnimationAuthoringPackage(),
            ValidationReport = ReadOptional<CharacterAuthoringValidationReport>(packagePath, Path.Combine("validation", "last_report.json"), options) ?? new CharacterAuthoringValidationReport(),
            GlobalWeaponLibrary = ReadGlobalWeaponLibrary(packagePath, options),
            CharacterActionLibraryPath = Path.Combine(packagePath, "config", "character_actions.json"),
            CharacterActionLibrary = ReadOptional<CharacterActionSourceLibrary>(
                packagePath,
                Path.Combine("config", "character_actions.json"),
                options) ?? new CharacterActionSourceLibrary(),
        };
        package.PackageConfigFileContents = ReadDeclaredPackageConfigFiles(
            packagePath,
            package.Manifest);

        package.CommonCharacterActionLibraryPath = FindCommonCharacterActionLibraryPath(packagePath);
        package.CommonCharacterActionLibrary = ReadCharacterActionLibrary(
            package.CommonCharacterActionLibraryPath,
            options);

        GlobalWeaponConfigurationResolver.HydrateCombinedWeapons(package);

        return package;
    }

    private static Dictionary<string, string> ReadDeclaredPackageConfigFiles(
        string packagePath,
        CharacterPackageManifest manifest)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        string packageRoot = Path.GetFullPath(packagePath);
        if (!packageRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            packageRoot += Path.DirectorySeparatorChar;

        if (manifest?.ConfigFiles != null)
        {
            for (int index = 0; index < manifest.ConfigFiles.Count; index++)
            {
                string relative = (manifest.ConfigFiles[index] ?? string.Empty)
                    .Replace('\\', '/')
                    .Trim()
                    .TrimStart('/');
                if (string.IsNullOrWhiteSpace(relative))
                    continue;
                string fullPath = Path.GetFullPath(Path.Combine(packageRoot, relative));
                if (!fullPath.StartsWith(packageRoot, StringComparison.Ordinal)
                    || !File.Exists(fullPath))
                    continue;
                result[relative] = File.ReadAllText(fullPath);
            }
        }

        string fallbackUsagePath = Path.GetFullPath(Path.Combine(packageRoot, CharacterPaletteUsageRelativePath));
        if (!result.ContainsKey(CharacterPaletteUsageRelativePath) && File.Exists(fallbackUsagePath))
        {
            result[CharacterPaletteUsageRelativePath] = File.ReadAllText(fallbackUsagePath);
        }

        return result;
    }

    internal static string FindGlobalWeaponLibraryPath(string packagePath)
    {
        string path = FindGlobalWeaponLibraryPathFrom(packagePath);
        if (!string.IsNullOrWhiteSpace(path))
            return path;
        return FindGlobalWeaponLibraryPathFrom(Directory.GetCurrentDirectory());
    }

    internal static GlobalWeaponLibrary ReadGlobalWeaponLibrary(
        string packagePath,
        JsonSerializerOptions options)
    {
        string path = FindGlobalWeaponLibraryPath(packagePath);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        return JsonSerializer.Deserialize<GlobalWeaponLibrary>(File.ReadAllText(path), options);
    }

    internal static string FindCommonCharacterActionLibraryPath(string startPath)
    {
        string current = string.IsNullOrWhiteSpace(startPath)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(startPath);
        if (File.Exists(current))
            current = Path.GetDirectoryName(current) ?? string.Empty;

        while (!string.IsNullOrWhiteSpace(current))
        {
            string candidate = Path.Combine(
                current,
                "CharacterActionSource",
                "Libraries",
                "Common",
                "character_actions.json");
            if (File.Exists(candidate))
                return candidate;
            current = Directory.GetParent(current)?.FullName;
        }

        return string.Empty;
    }

    private static CharacterActionSourceLibrary ReadCharacterActionLibrary(
        string path,
        JsonSerializerOptions options)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path)
            ? JsonSerializer.Deserialize<CharacterActionSourceLibrary>(File.ReadAllText(path), options)
                ?? new CharacterActionSourceLibrary()
            : new CharacterActionSourceLibrary();
    }

    private static string FindGlobalWeaponLibraryPathFrom(string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
            return string.Empty;
        string current = Path.GetFullPath(startPath);
        if (File.Exists(current))
            current = Path.GetDirectoryName(current) ?? string.Empty;
        while (!string.IsNullOrWhiteSpace(current))
        {
            string candidate = Path.Combine(current, "WeaponSource", "global_weapon_library.json");
            if (File.Exists(candidate))
                return candidate;
            current = Directory.GetParent(current)?.FullName;
        }
        return string.Empty;
    }

    private static void WriteCompileOutputs(string outDir, CharacterAuthoringCompileResult result, JsonSerializerOptions options)
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "compile_result.json"), JsonSerializer.Serialize(result, options));
        File.WriteAllText(Path.Combine(outDir, "generated_config_patch.json"), JsonSerializer.Serialize(result.GeneratedConfigPatch, options));
        File.WriteAllText(Path.Combine(outDir, "geometry_binding.json"), JsonSerializer.Serialize(result.GeometryBinding, options));
        File.WriteAllText(Path.Combine(outDir, "resource_mapping.json"), JsonSerializer.Serialize(result.ResourceMapping, options));
        File.WriteAllText(Path.Combine(outDir, "resource_mapping_v2_snapshot.json"), JsonSerializer.Serialize(result.ResourceMappingV2Snapshot, options));
        File.WriteAllText(Path.Combine(outDir, "unity_import_write_plan.json"), JsonSerializer.Serialize(result.UnityImportWritePlan, options));
        File.WriteAllText(Path.Combine(outDir, "gate_report.txt"), GateReportToText(result.GateReport));
    }

    private static void WriteResourcePlanOutputs(string outDir, CharacterResourcePlanCompileResult result, JsonSerializerOptions options)
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "runtime_resource_catalog.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.RuntimeResourceCatalog, options)));
        File.WriteAllText(Path.Combine(outDir, "character_resource_plan.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.CharacterResourcePlan, options)));
        File.WriteAllText(Path.Combine(outDir, "audio_cue_manifest.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.AudioCueManifest, options)));
        File.WriteAllText(Path.Combine(outDir, "resource_validation_report.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.ResourceValidationReport, options)));
    }

    private static List<CharacterUnityImportGeneratedArtifact> CreateGeneratedArtifacts(
        CharacterAuthoringCompileResult result,
        JsonSerializerOptions options)
    {
        var artifacts = new List<CharacterUnityImportGeneratedArtifact>();
        if (result == null)
            return artifacts;

        // 这三份载荷现在是 config/ 下正式文件的唯一来源（见 CreateUnityImportAdditionalWrites），
        // 必须保持与迁移前一致的内容语义：与其它正式 JSON 产物相同，以单个换行结尾。
        artifacts.Add(CreateGeneratedArtifact(
            "compiler/generated_config_patch.json",
            EnsureTrailingNewline(JsonSerializer.Serialize(result.GeneratedConfigPatch, options))));
        artifacts.Add(CreateGeneratedArtifact(
            "compiler/geometry_binding.json",
            EnsureTrailingNewline(JsonSerializer.Serialize(result.GeometryBinding, options))));
        artifacts.Add(CreateGeneratedArtifact(
            "compiler/resource_mapping.json",
            EnsureTrailingNewline(JsonSerializer.Serialize(result.ResourceMapping, options))));
        return artifacts;
    }

    private static CharacterUnityImportGeneratedArtifact CreateGeneratedArtifact(string sourcePath, string content)
    {
        content ??= string.Empty;
        return new CharacterUnityImportGeneratedArtifact
        {
            SourcePath = sourcePath,
            Content = content,
            ContentHash = CharacterPackageHashUtility.ComputeTextSha256(content)
        };
    }

    internal static List<CharacterUnityImportWriteInput> CreateUnityImportAdditionalWrites(
        string packagePath,
        CharacterResourcePackage package,
        CharacterAuthoringCompileResult result,
        AuthoringResourceCollection authoringResources,
        JsonSerializerOptions options,
        CharacterContentIdentityCatalogSnapshot identityCatalog = null,
        string projectRoot = null,
        bool includeFullSnapshots = false,
        CharacterPackageImportPayloads payloads = null)
    {
        var writes = new List<CharacterUnityImportWriteInput>();
        if (result == null || result.UnityImportWritePlan == null)
            return writes;

        string resolvedProjectRoot = ResolveProjectRootFromPackageOrCurrent(projectRoot, packagePath);
        identityCatalog ??= TryLoadIdentityCatalog(resolvedProjectRoot, packagePath);
        if (identityCatalog == null && CharacterContentIdentityCatalogSnapshot.DefaultAllowAutoRegisterForTests)
        {
            identityCatalog = new CharacterContentIdentityCatalogSnapshot { AllowAutoRegisterForTests = true };
        }

        string targetRoot = result.UnityImportWritePlan.TargetRootPath;
        string resolverPlan = EnsureTrailingNewline(JsonSerializer.Serialize(result.ResolverVerificationPlan, options));
        string unityCatalog = PreserveUnityCatalogEnrichment(
            CreateUnityResourceCatalogJson(packagePath, package, result, options),
            Path.Combine(resolvedProjectRoot, targetRoot, "config", "unity_resource_catalog.json"),
            resolvedProjectRoot,
            options);
        AnimationAuthoringCompileResult animationArtifacts = result.AnimationCompileResult ??
            AnimationPackageCommands.Compile(
                packagePath,
                package.AnimationAuthoring,
                package.ResourceCatalog,
                options);
        CharacterResourcePlanCompileResult resourcePlanArtifacts = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = packagePath,
            AuthoringCompileResult = result,
            AuthoringResources = authoringResources
        });
        PopulatePreparedClipBindings(
            resolvedProjectRoot,
            package,
            animationArtifacts?.AnimationSetDefinition,
            identityCatalog);
        string animationSetDefinition = EnsureTrailingNewline(JsonSerializer.Serialize(animationArtifacts.AnimationSetDefinition, options));
        string animationClipRegistry = EnsureTrailingNewline(JsonSerializer.Serialize(animationArtifacts.AnimationClipRegistry, options));
        // 正式 config/animation_resource_plan.json 只写五字段运行投影（mx.animationResourcePlan.v2）。
        // 完整 v1 对象继续保留在编译结果、制作 API 与显式诊断输出中。
        // 投影校验必须早于任何写盘；失败时由调用方在 CharacterUnityImportBridge.Execute 之前中止。
        bool animationRuntimePlanProjected = CharacterAnimationRuntimeResourcePlanProjection.TryProject(
            animationArtifacts?.AnimationResourcePlan,
            options,
            out string animationResourcePlan,
            out string animationRuntimePlanDiagnostic);
        if (!animationRuntimePlanProjected)
        {
            animationResourcePlan = string.Empty;
            if (payloads != null)
                payloads.AnimationRuntimePlanDiagnostic = animationRuntimePlanDiagnostic;
        }
        if (includeFullSnapshots && payloads != null)
        {
            payloads.AnimationCompileResultSnapshotJson = EnsureTrailingNewline(JsonSerializer.Serialize(animationArtifacts, options));
        }
        string animationPackageExpectation = EnsureTrailingNewline(JsonSerializer.Serialize(animationArtifacts.AnimationPackageExpectation, options));
        string animationValidationReport = EnsureTrailingNewline(JsonSerializer.Serialize(animationArtifacts.AnimationValidationReport, options));
        string runtimeResourceCatalog = EnsureTrailingNewline(JsonSerializer.Serialize(resourcePlanArtifacts.RuntimeResourceCatalog, options));
        string characterResourcePlan = EnsureTrailingNewline(JsonSerializer.Serialize(resourcePlanArtifacts.CharacterResourcePlan, options));
        string audioCueManifest = EnsureTrailingNewline(JsonSerializer.Serialize(resourcePlanArtifacts.AudioCueManifest, options));
        string resourceValidationReport = EnsureTrailingNewline(JsonSerializer.Serialize(resourcePlanArtifacts.ResourceValidationReport, options));
        if (includeFullSnapshots && payloads != null)
        {
            payloads.CompileResultSnapshotJson = EnsureTrailingNewline(JsonSerializer.Serialize(result, options));
        }
        string writePlan = EnsureTrailingNewline(JsonSerializer.Serialize(result.UnityImportWritePlan, options));
        string resourceHashReport = EnsureTrailingNewline(JsonSerializer.Serialize(result.ResourceHashReport, options));
        string dependencyGraph = EnsureTrailingNewline(JsonSerializer.Serialize(result.DependencyGraph, options));
        string gateReportText = GateReportToText(result.GateReport);

        AddContentWrite(writes, "packageCache", "package/manifest.json", CombineProjectPath(targetRoot, "package_cache/manifest.json"), ReadPackageFile(packagePath, "manifest.json"), "Recreate");
        if (result.TacticalDefaults != null)
            AddContentWrite(writes, "packageConfig", "compiler/character_tactical_defaults.json",
                CombineProjectPath(targetRoot, MxFramework.CharacterApplication.CharacterTacticalDefaults.RelativePath),
                EnsureTrailingNewline(JsonSerializer.Serialize(result.TacticalDefaults, options)), "Recreate");
        AddContentWrite(writes, "packageCache", "package/resource_catalog.json", CombineProjectPath(targetRoot, "package_cache/resource_catalog.json"), ReadPackageFile(packagePath, "resource_catalog.json"), "Recreate");
        // package_cache/character_application.json 由 CharacterAuthoringCompiler.BuildWritePlan 唯一拥有（kind=resourceFile），
        // 此处不再重复注册，避免合并后的写入计划出现重复 TargetPath。
        if (package.PackageConfigFileContents.TryGetValue(
            CharacterPaletteBindingRelativePath,
            out string paletteBindingContent))
        {
            AddContentWrite(
                writes,
                "packageConfig",
                CharacterPaletteBindingRelativePath,
                CombineProjectPath(targetRoot, CharacterPaletteBindingRelativePath),
                paletteBindingContent,
                "Recreate");
        }
        if (package.PackageConfigFileContents.TryGetValue(
            CharacterPaletteUsageRelativePath,
            out string paletteUsageContent))
        {
            AddContentWrite(
                writes,
                "packageConfig",
                CharacterPaletteUsageRelativePath,
                CombineProjectPath(targetRoot, CharacterPaletteUsageRelativePath),
                paletteUsageContent,
                "Recreate");
        }
        if (package.PackageConfigFileContents.TryGetValue(
            CharacterMagicaClothRelativePath,
            out string magicaClothContent))
        {
            AddContentWrite(
                writes,
                "packageConfig",
                CharacterMagicaClothRelativePath,
                CombineProjectPath(targetRoot, CharacterMagicaClothRelativePath),
                magicaClothContent,
                "Recreate");
        }
        AddContentWrite(writes, "packageCache", "compiler/unity_import_write_plan.json", CombineProjectPath(targetRoot, "package_cache/unity_import_write_plan.json"), writePlan, "Recreate");
        AddContentWrite(writes, "packageCache", "compiler/resource_hash_report.json", CombineProjectPath(targetRoot, "package_cache/resource_hash_report.json"), resourceHashReport, "Recreate");
        AddContentWrite(writes, "packageCache", "compiler/dependency_graph.json", CombineProjectPath(targetRoot, "package_cache/dependency_graph.json"), dependencyGraph, "Recreate");
        AddContentWrite(writes, "packageCache", "compiler/gate_report.txt", CombineProjectPath(targetRoot, "package_cache/gate_report.txt"), gateReportText, "Recreate");
        AddContentWrite(writes, "packageCache", "animation/animation_package_expectation.json", CombineProjectPath(targetRoot, "package_cache/animation_package_expectation.json"), animationPackageExpectation, "Recreate");
        AddContentWrite(writes, "packageCache", "animation/animation_validation_report.json", CombineProjectPath(targetRoot, "package_cache/animation_validation_report.json"), animationValidationReport, "Recreate");

        AddContentWrite(writes, "resolverVerificationPlan", "compiler/resolver_verification_plan.json", CombineProjectPath(targetRoot, "config/resolver_verification_plan.json"), resolverPlan, "Recreate");
        AddContentWrite(writes, "unityResourceCatalog", "compiler/unity_resource_catalog.json", CombineProjectPath(targetRoot, "config/unity_resource_catalog.json"), unityCatalog, "Recreate");
        AddContentWrite(writes, "animationSetDefinition", "animation/animation_set_definition.json", CombineProjectPath(targetRoot, "config/animation_set_definition.json"), animationSetDefinition, "Recreate");
        AddContentWrite(writes, "animationClipRegistry", "animation/animation_clip_registry.json", CombineProjectPath(targetRoot, "config/animation_clip_registry.json"), animationClipRegistry, "Recreate");
        if (animationRuntimePlanProjected)
        {
            AddContentWrite(writes, "animationResourcePlan", "animation/animation_resource_plan.json", CombineProjectPath(targetRoot, "config/animation_resource_plan.json"), animationResourcePlan, "Recreate");
        }
        AddContentWrite(writes, "runtimeResourceCatalog", "compiler/runtime_resource_catalog.json", CombineProjectPath(targetRoot, "config/runtime_resource_catalog.json"), runtimeResourceCatalog, "Recreate");
        AddContentWrite(writes, "characterResourcePlan", "compiler/character_resource_plan.json", CombineProjectPath(targetRoot, "config/character_resource_plan.json"), characterResourcePlan, "Recreate");
        AddContentWrite(writes, "audioCueManifest", "compiler/audio_cue_manifest.json", CombineProjectPath(targetRoot, "config/audio_cue_manifest.json"), audioCueManifest, "Recreate");
        AddContentWrite(writes, "resourceValidationReport", "compiler/resource_validation_report.json", CombineProjectPath(targetRoot, "config/resource_validation_report.json"), resourceValidationReport, "Recreate");
        AddCharacterActionProjectionWrites(writes, packagePath, package, options, identityCatalog);
        if (CharacterBakedSurfaceAuthority.Exists(packagePath))
        {
            string relative = CharacterBakedSurfaceAuthority.RelativePath;
            string content = File.ReadAllText(Path.Combine(packagePath, relative));
            AddContentWrite(writes, "packageConfig", relative, CombineProjectPath(targetRoot, relative), content, "Recreate");
        }
        AddHumanoidAvatarSourceWrites(writes, packagePath, package, result, targetRoot);
        if (payloads != null)
        {
            payloads.Summary = CharacterPackageCompileSummaryBuilder.Build(
                package,
                result,
                animationArtifacts,
                resourcePlanArtifacts,
                package?.Manifest?.Version,
                CharacterPackageCompileSummaryBuilder.LayoutVersionAuthoringOnlyPreviewGlb);
        }

        return writes;
    }

    private static void AddCharacterActionProjectionWrites(
        List<CharacterUnityImportWriteInput> writes,
        string packagePath,
        CharacterResourcePackage package,
        JsonSerializerOptions options,
        CharacterContentIdentityCatalogSnapshot identityCatalog)
    {
        List<CombinedWeaponConfigurationAuthoringSummary> configurations =
            package?.ApplicationConfig?.CombinedWeaponConfigurations;
        if (configurations == null || configurations.Count == 0)
            return;
        var ordered = new List<CombinedWeaponConfigurationAuthoringSummary>(configurations);
        ordered.Sort((left, right) => string.CompareOrdinal(
            left?.CombinedWeaponConfigurationId,
            right?.CombinedWeaponConfigurationId));
        string packageId = package.Manifest?.PackageId ?? "character";
        string packageSegment = CharacterPackageResourceKeyGenerator.NormalizeSegment(packageId);
        string actionSetRoot = "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/" + packageSegment;
        string actionLibraryRoot = "Assets/MxFrameworkGenerated/CharacterActions/Libraries/" + packageSegment;
        var actionSets = new JsonArray();
        var reactionProfiles = new JsonArray();
        var actions = new JsonArray();
        var actionIds = new HashSet<int>();
        var commonActionIds = IndexCharacterActionIds(package.CommonCharacterActionLibrary);
        var characterActionIds = IndexCharacterActionIds(package.CharacterActionLibrary);
        Dictionary<string, int> audioCueIds = LoadAudioCueIds(packagePath);
        Dictionary<string, int> abilityIds = LoadAbilityIds(packagePath);
        Dictionary<string, int> vfxDefinitionIds = LoadVfxDefinitionIds(packagePath);

        for (int configurationIndex = 0; configurationIndex < ordered.Count; configurationIndex++)
        {
            CombinedWeaponConfigurationAuthoringSummary combined = ordered[configurationIndex];
            if (combined == null)
                continue;
            CombinedWeaponActionConfiguration authored = combined.ActionConfiguration ??
                new CombinedWeaponActionConfiguration();
            GlobalAbilityConfiguration ability = GlobalWeaponConfigurationResolver.FindAbility(
                package.GlobalWeaponLibrary,
                combined.AbilityConfigurationId);
            GlobalReactionConfiguration reaction = GlobalWeaponConfigurationResolver.FindReaction(
                package.GlobalWeaponLibrary,
                combined.ReactionConfigurationId);
            string combinedSegment = CharacterPackageResourceKeyGenerator.NormalizeSegment(
                GlobalWeaponConfigurationResolver.GetCharacterScopedCombinationIdentity(
                    package.Manifest?.PackageId,
                    combined.CombinedWeaponConfigurationId));
            string reactionProfileId = "reaction." + packageSegment + "." + combinedSegment;
            JsonObject actionSet = JsonObjectFrom(authored.ActionSet) ?? new JsonObject();
            actionSet["id"] = 2 + configurationIndex;
            string actionSetStableId = packageSegment + "." + CharacterPackageResourceKeyGenerator.NormalizeSegment(
                GlobalWeaponConfigurationResolver.GetCharacterScopedCombinationIdentity(
                    package.Manifest?.PackageId,
                    combined.CombinedWeaponConfigurationId));
            actionSet["stableId"] = actionSetStableId;
            actionSet["displayName"] = combined.DisplayName;
            actionSet["characterStableId"] = package.ApplicationConfig.CharacterStableId;
            actionSet["equipmentStateStableId"] = "equip_state." + packageSegment + "." + combinedSegment;
            actionSet["movementProfileId"] = "movement.default";
            actionSet["reactionProfileId"] = reactionProfileId;
            actionSet["defaultActionId"] = 0;
            actionSet["defaultActionSlotMask"] = 415;
            actionSet["commandBindings"] = BuildCommandBindings(ability, authored);
            actionSet["commandSequences"] = BuildCommandSequences(authored);
            actionSet["abilityBindings"] = BuildAbilityBindings(authored);
            int defaultReactionActionId = ResolveDefaultReactionActionId(reaction, authored);
            actionSet["reactionBindings"] = defaultReactionActionId <= 0
                ? new JsonArray()
                : new JsonArray(new JsonObject
                {
                    ["reactionProfileId"] = reactionProfileId,
                    ["defaultActionId"] = defaultReactionActionId,
                    ["requiredTags"] = new JsonArray(),
                    ["forbiddenTags"] = new JsonArray()
                });
            actionSets.Add(actionSet);

            var reactionProfile = new JsonObject
            {
                ["stableId"] = reactionProfileId,
                ["displayName"] = (reaction?.DisplayName ?? combined.DisplayName) + " / " + combined.DisplayName,
                ["defaultActionId"] = defaultReactionActionId,
                ["rules"] = BuildReactionRules(reaction, authored)
            };
            reactionProfiles.Add(reactionProfile);

            ValidateReferencedActions(
                authored,
                commonActionIds,
                characterActionIds,
                combined.CombinedWeaponConfigurationId,
                package.ApplicationConfig.AdditionalActionLibraryIds != null
                    && package.ApplicationConfig.AdditionalActionLibraryIds.Count > 0);
        }

        AddSourceLibraryActions(actions, actionIds, package.CharacterActionLibrary);

        string actionSetsJson = ProjectCharacterActionRuntimeIdentities(new JsonObject
        {
            ["schemaVersion"] = "0.4.0",
            ["actionSets"] = actionSets
        }, identityCatalog, audioCueIds, abilityIds, vfxDefinitionIds, options);
        string reactionProfilesJson = ProjectCharacterActionRuntimeIdentities(new JsonObject
        {
            ["schemaVersion"] = "0.4.0",
            ["reactionProfiles"] = reactionProfiles
        }, identityCatalog, audioCueIds, abilityIds, vfxDefinitionIds, options);
        string actionsJson = ProjectCharacterActionRuntimeIdentities(new JsonObject
        {
            ["schemaVersion"] = CharacterActionSourceLibrary.CurrentSchemaVersion,
            ["animationSources"] = JsonNode.Parse(JsonSerializer.Serialize(
                package.CharacterActionLibrary.AnimationSources,
                options)),
            ["actions"] = actions
        }, identityCatalog, audioCueIds, abilityIds, vfxDefinitionIds, options);
        var actionSetIds = new JsonArray();
        for (int i = 0; i < ordered.Count; i++)
            if (ordered[i] != null)
                actionSetIds.Add(packageSegment + "." + CharacterPackageResourceKeyGenerator.NormalizeSegment(
                    GlobalWeaponConfigurationResolver.GetCharacterScopedCombinationIdentity(
                        package.Manifest?.PackageId,
                        ordered[i].CombinedWeaponConfigurationId)));
        var actionLibraryIds = new JsonArray(packageSegment, "Common");
        var additionalLibraryIds = package.ApplicationConfig.AdditionalActionLibraryIds ?? new List<string>();
        additionalLibraryIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < additionalLibraryIds.Count; i++)
        {
            string libraryId = additionalLibraryIds[i]?.Trim() ?? string.Empty;
            if (libraryId.Length > 0 && !string.Equals(libraryId, packageSegment, StringComparison.Ordinal)
                && !string.Equals(libraryId, "Common", StringComparison.Ordinal))
            {
                actionLibraryIds.Add(libraryId);
            }
        }
        string profileJson = ProjectCharacterActionRuntimeIdentities(new JsonObject
        {
            ["schemaVersion"] = "0.4.0",
            ["profiles"] = new JsonArray(new JsonObject
            {
                ["id"] = 1,
                ["stableId"] = packageSegment + ".default",
                ["displayName"] = (package.Manifest?.DisplayName ?? packageId) + " Default",
                ["actionSetIds"] = actionSetIds,
                ["actionLibraryIds"] = actionLibraryIds
            })
        }, identityCatalog, audioCueIds, abilityIds, vfxDefinitionIds, options);
        AddContentWrite(writes, "characterActionSet", "compiler/character_action_sets.json", actionSetRoot + "/character_action_sets.json", actionSetsJson, "Recreate");
        AddContentWrite(writes, "characterReactionProfile", "compiler/character_reaction_profiles.json", actionSetRoot + "/character_reaction_profiles.json", reactionProfilesJson, "Recreate");
        AddContentWrite(writes, "characterActionLibrary", "compiler/character_actions.json", actionLibraryRoot + "/character_actions.json", actionsJson, "Recreate");
        AddContentWrite(writes, "characterActionProfile", "compiler/character_action_profile.json", "Assets/MxFrameworkGenerated/CharacterActions/Profiles/" + packageSegment + "/character_action_profile.json", profileJson, "Recreate");
        if (package.CommonCharacterActionLibrary != null)
        {
            JsonNode commonDocument = JsonNode.Parse(JsonSerializer.Serialize(package.CommonCharacterActionLibrary, options));
            string commonJson = ProjectCharacterActionRuntimeIdentities(commonDocument, identityCatalog, audioCueIds, abilityIds, vfxDefinitionIds, options);
            AddContentWrite(
                writes,
                "commonCharacterActionLibrary",
                "CharacterActionSource/Libraries/Common/character_actions.json",
                "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json",
                commonJson,
                "Recreate");
        }
    }

    private static string ProjectCharacterActionRuntimeIdentities(
        JsonNode document,
        CharacterContentIdentityCatalogSnapshot identityCatalog,
        Dictionary<string, int> audioCueIds,
        Dictionary<string, int> abilityIds,
        Dictionary<string, int> vfxDefinitionIds,
        JsonSerializerOptions options)
    {
        if (document == null)
            return "{}\n";
        if (identityCatalog == null)
            throw new InvalidDataException("CharacterAction runtime identity projection requires the frozen identity catalog.");

        var eventOrders = new Dictionary<int, int>();
        CharacterContentIdentityDomain eventDomain = identityCatalog.Domains?.Find(item =>
            item != null && item.DomainId == CharacterContentIdentityDomains.Event);
        List<CharacterContentIdentityEntry> orderedEvents = eventDomain?.Entries != null
            ? new List<CharacterContentIdentityEntry>(eventDomain.Entries)
            : new List<CharacterContentIdentityEntry>();
        orderedEvents.RemoveAll(item => item == null);
        orderedEvents.Sort((left, right) => string.CompareOrdinal(left.SourceKey, right.SourceKey));
        for (int i = 0; i < orderedEvents.Count; i++)
            if (string.Equals(orderedEvents[i].Status, "active", StringComparison.OrdinalIgnoreCase))
                eventOrders[orderedEvents[i].Id] = i + 1;

        var bladeTrailDocument = Newtonsoft.Json.Linq.JObject.Parse(document.ToJsonString());
        MxFramework.CharacterAction.CharacterActionBladeTrailJsonProjection.SynchronizeDocument(
            bladeTrailDocument,
            (domain, value) => value == null || string.IsNullOrWhiteSpace(value.ToString()) ? 0
                : value.Type == Newtonsoft.Json.Linq.JTokenType.Integer ? (int)value
                : ResolveRequiredRuntimeIdentity(identityCatalog, domain, value.ToString(), "bladeTrail"),
            key => vfxDefinitionIds != null && vfxDefinitionIds.TryGetValue(key, out int id) ? id : 0);
        document = JsonNode.Parse(bladeTrailDocument.ToString(Newtonsoft.Json.Formatting.None));
        ProjectCharacterActionNode(document, identityCatalog, audioCueIds, abilityIds, vfxDefinitionIds, eventOrders);
        ProjectCharacterActionEventOrders(document, eventOrders);
        if (document is JsonObject root)
            root["schemaVersion"] = root["actions"] != null ? "0.5.0" : "0.6.0";
        return EnsureTrailingNewline(document.ToJsonString(options));
    }

    private static void ProjectCharacterActionEventOrders(JsonNode node, Dictionary<int, int> orders)
    {
        if (node is JsonArray array)
        {
            foreach (JsonNode child in array) ProjectCharacterActionEventOrders(child, orders);
        }
        else if (node is JsonObject obj)
        {
            if (obj["kind"] != null && obj["stableEventId"] is JsonValue value && value.TryGetValue(out int id))
                obj["stableEventOrder"] = orders.TryGetValue(id, out int order) ? order : 0;
            foreach (KeyValuePair<string, JsonNode> property in obj)
                ProjectCharacterActionEventOrders(property.Value, orders);
        }
    }

    private static void ProjectCharacterActionNode(
        JsonNode node,
        CharacterContentIdentityCatalogSnapshot identityCatalog,
        Dictionary<string, int> audioCueIds,
        Dictionary<string, int> abilityIds,
        Dictionary<string, int> vfxDefinitionIds,
        Dictionary<int, int> eventOrders)
    {
        if (node is JsonArray array)
        {
            for (int i = 0; i < array.Count; i++)
                ProjectCharacterActionNode(array[i], identityCatalog, audioCueIds, abilityIds, vfxDefinitionIds, eventOrders);
            return;
        }
        if (node is not JsonObject obj)
            return;

        bool isDamagePayload = obj["damageTypeId"] != null
            || obj["formulaId"] != null
            || obj["baseDamage"] != null;
        if (string.Equals(obj["kind"]?.GetValue<string>(), "SpawnVisualCue", StringComparison.Ordinal))
        {
            string resourceKey = obj["resourceKey"]?.GetValue<string>() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(resourceKey)
                || vfxDefinitionIds == null
                || !vfxDefinitionIds.TryGetValue(resourceKey, out int vfxDefinitionId)
                || vfxDefinitionId <= 0)
                throw new InvalidDataException("VFX definition identity is missing from the generated VFX catalog: " + resourceKey);
            obj["vfxDefinitionId"] = vfxDefinitionId;

            JsonObject attachment = obj["vfxAttachment"] as JsonObject;
            string mode = attachment?["mode"]?.GetValue<string>() ?? string.Empty;
            int numericSocketId = 0;
            if (string.Equals(mode, "FollowSocket", StringComparison.Ordinal))
            {
                string socketLabel = attachment?["socketId"]?.GetValue<string>() ?? string.Empty;
                numericSocketId = ResolveRequiredRuntimeIdentity(
                    identityCatalog,
                    CharacterContentIdentityDomains.Pose,
                    socketLabel,
                    "numericSocketId");
            }
            obj["numericSocketId"] = numericSocketId;
        }
        var names = new List<string>();
        foreach (KeyValuePair<string, JsonNode> property in obj)
            names.Add(property.Key);
        for (int i = 0; i < names.Count; i++)
        {
            string name = names[i];
            JsonNode value = obj[name];
            if (value is JsonValue scalar && scalar.TryGetValue(out string label))
            {
                int domain = CharacterActionIdentityDomain(name);
                int numericId;
                if (string.Equals(name, "abilityStableId", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(label))
                        numericId = 0;
                    else if (abilityIds == null || !abilityIds.TryGetValue(label, out numericId) || numericId <= 0)
                        throw new InvalidDataException("Ability identity is missing from the generated ability catalog: " + label);
                }
                else if (string.Equals(name, "cueId", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(obj["kind"]?.GetValue<string>(), "PlayAudioCue", StringComparison.Ordinal))
                    {
                        if (string.IsNullOrWhiteSpace(label))
                            numericId = 0;
                        else if (audioCueIds == null || !audioCueIds.TryGetValue(label, out numericId) || numericId <= 0)
                            throw new InvalidDataException("Audio cue identity is missing from the generated audio runtime catalog: " + label);
                    }
                    else
                    {
                        numericId = ResolveRequiredRuntimeIdentity(
                            identityCatalog,
                            CharacterContentIdentityDomains.Event,
                            label,
                            name);
                    }
                }
                else if (domain != 0)
                {
                    numericId = ResolveRequiredRuntimeIdentity(identityCatalog, domain, label, name);
                }
                else if (string.Equals(name, "reactionGroupId", StringComparison.OrdinalIgnoreCase))
                {
                    numericId = new CharacterContentIdentityResolver(identityCatalog).ResolveReactionGroupId(label);
                }
                else if (string.Equals(name, "formulaId", StringComparison.OrdinalIgnoreCase))
                {
                    numericId = new CharacterContentIdentityResolver(identityCatalog).ResolveFormulaId(label);
                }
                else
                {
                    ProjectCharacterActionNode(value, identityCatalog, audioCueIds, abilityIds, vfxDefinitionIds, eventOrders);
                    continue;
                }

                obj[IdentityLabelName(name)] = label;
                obj[name] = numericId;
                if (string.Equals(name, "stableEventId", StringComparison.OrdinalIgnoreCase)
                    && obj["kind"] != null)
                {
                    obj["stableEventOrder"] = numericId > 0 && eventOrders.TryGetValue(numericId, out int order)
                        ? order
                        : 0;
                }
                continue;
            }

            if (value is JsonArray identities && CharacterActionIdentityArrayDomain(name, isDamagePayload) is int arrayDomain && arrayDomain != 0)
            {
                var labels = new JsonArray();
                var numbers = new JsonArray();
                for (int itemIndex = 0; itemIndex < identities.Count; itemIndex++)
                {
                    if (identities[itemIndex] is JsonValue item && item.TryGetValue(out string itemLabel))
                    {
                        labels.Add(itemLabel);
                        numbers.Add(ResolveRequiredRuntimeIdentity(identityCatalog, arrayDomain, itemLabel, name));
                    }
                    else
                    {
                        numbers.Add(identities[itemIndex]?.DeepClone());
                    }
                }
                obj[IdentityLabelName(name)] = labels;
                obj[name] = numbers;
                continue;
            }

            ProjectCharacterActionNode(value, identityCatalog, audioCueIds, abilityIds, vfxDefinitionIds, eventOrders);
        }
    }

    private static Dictionary<string, int> LoadAudioCueIds(string packagePath)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        DirectoryInfo directory = new DirectoryInfo(Path.GetFullPath(packagePath));
        while (directory != null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "Assets",
                "MxFrameworkGenerated",
                "Audio",
                "audio_runtime_catalog.json");
            if (File.Exists(candidate))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(candidate));
                if (document.RootElement.TryGetProperty("cues", out JsonElement cues)
                    && cues.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement cue in cues.EnumerateArray())
                    {
                        string label = cue.TryGetProperty("audioCueId", out JsonElement labelNode)
                            ? labelNode.GetString() ?? string.Empty
                            : string.Empty;
                        int id = cue.TryGetProperty("id", out JsonElement idNode) && idNode.TryGetInt32(out int value)
                            ? value
                            : 0;
                        if (!string.IsNullOrWhiteSpace(label) && id > 0)
                            result[label] = id;
                    }
                }
                return result;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Generated audio runtime catalog was not found for CharacterAction projection.");
    }

    private static Dictionary<string, int> LoadAbilityIds(string packagePath)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        DirectoryInfo directory = new DirectoryInfo(Path.GetFullPath(packagePath));
        while (directory != null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "Assets",
                "MxFrameworkGenerated",
                "Abilities",
                "ability_catalog.json");
            if (File.Exists(candidate))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(candidate));
                if (document.RootElement.TryGetProperty("abilities", out JsonElement abilities)
                    && abilities.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement ability in abilities.EnumerateArray())
                    {
                        string label = ability.TryGetProperty("abilityStableId", out JsonElement labelNode)
                            ? labelNode.GetString() ?? string.Empty
                            : string.Empty;
                        int id = ability.TryGetProperty("abilityId", out JsonElement idNode) && idNode.TryGetInt32(out int value)
                            ? value
                            : 0;
                        if (!string.IsNullOrWhiteSpace(label) && id > 0)
                            result[label] = id;
                    }
                }
                return result;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Generated ability catalog was not found for CharacterAction projection.");
    }

    private static Dictionary<string, int> LoadVfxDefinitionIds(string packagePath)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        DirectoryInfo directory = new DirectoryInfo(Path.GetFullPath(packagePath));
        while (directory != null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "Assets",
                "MxFrameworkGenerated",
                "Vfx",
                "vfx_definitions.json");
            if (File.Exists(candidate))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(candidate));
                if (document.RootElement.TryGetProperty("definitions", out JsonElement definitions)
                    && definitions.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement definition in definitions.EnumerateArray())
                    {
                        int id = definition.TryGetProperty("definitionId", out JsonElement idNode)
                            && idNode.TryGetInt32(out int value)
                            ? value
                            : 0;
                        if (id <= 0)
                            continue;
                        string stableId = definition.TryGetProperty("stableId", out JsonElement stableNode)
                            ? stableNode.GetString() ?? string.Empty
                            : string.Empty;
                        if (!string.IsNullOrWhiteSpace(stableId))
                            result[stableId] = id;
                        if (definition.TryGetProperty("prefabResource", out JsonElement resource))
                        {
                            string key = resource.TryGetProperty("id", out JsonElement keyNode)
                                ? keyNode.GetString() ?? string.Empty
                                : string.Empty;
                            string typeId = resource.TryGetProperty("typeId", out JsonElement typeNode)
                                ? typeNode.GetString() ?? string.Empty
                                : string.Empty;
                            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(typeId))
                            {
                                string reference = key + ":" + typeId;
                                string variant = resource.TryGetProperty("variant", out JsonElement variantNode)
                                    ? variantNode.GetString() ?? string.Empty
                                    : string.Empty;
                                string packageId = resource.TryGetProperty("packageId", out JsonElement packageNode)
                                    ? packageNode.GetString() ?? string.Empty
                                    : string.Empty;
                                if (!string.IsNullOrWhiteSpace(variant)) reference += "#" + variant;
                                if (!string.IsNullOrWhiteSpace(packageId)) reference += "@" + packageId;
                                result[reference] = id;
                            }
                        }
                    }
                }
                return result;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Generated VFX definition catalog was not found for CharacterAction projection.");
    }

    private static int CharacterActionIdentityDomain(string name)
    {
        if (string.Equals(name, "stableEventId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "bladeTrailSourceEventId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "sourceAnimationEventId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "eventId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "requestId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "targetTimelineId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "effectTimelineId", StringComparison.OrdinalIgnoreCase))
            return CharacterContentIdentityDomains.Event;
        if (string.Equals(name, "collisionProfileId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "movementProfileId", StringComparison.OrdinalIgnoreCase))
            return CharacterContentIdentityDomains.Profile;
        if (string.Equals(name, "traceId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "traceProfileId", StringComparison.OrdinalIgnoreCase))
            return CharacterContentIdentityDomains.Trace;
        if (string.Equals(name, "intentId", StringComparison.OrdinalIgnoreCase))
            return CharacterContentIdentityDomains.Intent;
        if (string.Equals(name, "slotId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "equipmentSlotId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "equipSlot", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "mountedSlot", StringComparison.OrdinalIgnoreCase))
            return CharacterContentIdentityDomains.Slot;
        if (string.Equals(name, "targetPolicyId", StringComparison.OrdinalIgnoreCase))
            return CharacterContentIdentityDomains.TargetPolicy;
        if (string.Equals(name, "presentationProfileId", StringComparison.OrdinalIgnoreCase))
            return CharacterContentIdentityDomains.Presentation;
        if (string.Equals(name, "damageTypeId", StringComparison.OrdinalIgnoreCase))
            return CharacterContentIdentityDomains.DamageType;
        if (string.Equals(name, "analysisCategory", StringComparison.OrdinalIgnoreCase))
            return CharacterContentIdentityDomains.Analysis;
        if (string.Equals(name, "combatActionId", StringComparison.OrdinalIgnoreCase))
            return CharacterContentIdentityDomains.CombatAction;
        if (string.Equals(name, "requiredPartStateId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "startSocketId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "endSocketId", StringComparison.OrdinalIgnoreCase))
            return CharacterContentIdentityDomains.Pose;
        return 0;
    }

    private static int CharacterActionIdentityArrayDomain(string name, bool isDamagePayload)
    {
        if (isDamagePayload && string.Equals(name, "tags", StringComparison.OrdinalIgnoreCase))
            return CharacterContentIdentityDomains.Tag;
        if (string.Equals(name, "allowedPostureIds", StringComparison.OrdinalIgnoreCase))
            return CharacterContentIdentityDomains.Profile;
        return string.Equals(name, "occupiesSlots", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "slotIds", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "requiredFilledSlots", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "requiredEmptySlots", StringComparison.OrdinalIgnoreCase)
            ? CharacterContentIdentityDomains.Slot
            : 0;
    }

    private static int ResolveRequiredRuntimeIdentity(
        CharacterContentIdentityCatalogSnapshot catalog,
        int domain,
        string label,
        string field)
    {
        if (string.IsNullOrWhiteSpace(label))
            return 0;
        if (catalog.TryResolve(domain, label, out int numericId, out string status)
            && numericId > 0
            && string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            return numericId;
        throw new InvalidDataException($"Runtime identity '{label}' for {field} is missing or inactive in domain {domain}.");
    }

    private static string IdentityLabelName(string name)
    {
        if (name.EndsWith("Ids", StringComparison.Ordinal))
            return name.Substring(0, name.Length - 3) + "Labels";
        if (name.EndsWith("Id", StringComparison.Ordinal))
            return name.Substring(0, name.Length - 2) + "Label";
        return name + "Label";
    }

    private static HashSet<int> IndexCharacterActionIds(CharacterActionSourceLibrary library)
    {
        var result = new HashSet<int>();
        List<JsonElement> source = library?.Actions;
        for (int i = 0; source != null && i < source.Count; i++)
        {
            int actionId = CharacterActionSourceLibrary.GetActionId(source[i]);
            if (actionId > 0 && !result.Add(actionId))
                throw new InvalidDataException("Duplicate CharacterAction id '" + actionId + "'.");
        }
        return result;
    }

    private static void AddSourceLibraryActions(
        JsonArray target,
        HashSet<int> actionIds,
        CharacterActionSourceLibrary library)
    {
        List<JsonElement> source = library?.Actions;
        for (int i = 0; source != null && i < source.Count; i++)
        {
            int actionId = CharacterActionSourceLibrary.GetActionId(source[i]);
            if (actionId <= 0 || !actionIds.Add(actionId))
                continue;
            target.Add(JsonNode.Parse(source[i].GetRawText()));
        }
    }

    private static void ValidateReferencedActions(
        CombinedWeaponActionConfiguration configuration,
        HashSet<int> commonActionIds,
        HashSet<int> characterActionIds,
        string combinationId,
        bool allowExternalLibraries)
    {
        List<CombinedWeaponActionEntry> entries = configuration?.Actions;
        for (int i = 0; entries != null && i < entries.Count; i++)
        {
            int actionId = entries[i]?.ActionId ?? 0;
            if (actionId <= 0 ||
                (!commonActionIds.Contains(actionId) && !characterActionIds.Contains(actionId)))
            {
                if (actionId > 0 && allowExternalLibraries)
                    continue;
                throw new InvalidDataException(
                    "Combined weapon '" + combinationId + "' action role '" +
                    (entries[i]?.ActionRole ?? string.Empty) + "' references missing action '" + actionId + "'.");
            }
        }
    }

    private static JsonObject JsonObjectFrom(JsonElement? element)
    {
        return element.HasValue && element.Value.ValueKind == JsonValueKind.Object
            ? JsonNode.Parse(element.Value.GetRawText()) as JsonObject
            : null;
    }

    private static JsonArray BuildCommandBindings(
        GlobalAbilityConfiguration ability,
        CombinedWeaponActionConfiguration actions)
    {
        var result = new JsonArray();
        for (int i = 0; ability?.Abilities != null && i < ability.Abilities.Count; i++)
        {
            GlobalAbilityEntry slot = ability.Abilities[i];
            int actionId = ResolveAbilityActionId(slot?.AbilityId ?? 0, slot?.ActionRole, actions);
            if (slot == null || !string.Equals(slot.TriggerMode, GlobalAbilityTriggerModes.Input, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(slot.InputIntentId) || actionId <= 0)
                continue;
            result.Add(new JsonObject
            {
                ["intentId"] = slot.InputIntentId,
                ["actionId"] = actionId,
                ["priority"] = 10,
                ["allowQueue"] = false,
                ["queueWindowFrames"] = 0
            });
        }

        var actionIdsByRole = new Dictionary<string, int>(StringComparer.Ordinal);
        List<CombinedWeaponActionEntry> actionEntries = actions?.Actions;
        for (int i = 0; actionEntries != null && i < actionEntries.Count; i++)
        {
            CombinedWeaponActionEntry entry = actionEntries[i];
            if (entry != null && !string.IsNullOrWhiteSpace(entry.ActionRole))
                actionIdsByRole[entry.ActionRole] = entry.ActionId;
        }
        List<CombinedWeaponCommandBinding> authoredBindings = actions?.CommandBindings;
        for (int i = 0; authoredBindings != null && i < authoredBindings.Count; i++)
        {
            CombinedWeaponCommandBinding binding = authoredBindings[i];
            if (binding == null || string.IsNullOrWhiteSpace(binding.IntentId))
                continue;
            int actionId = binding.ActionId;
            if (actionId <= 0 && !actionIdsByRole.TryGetValue(binding.ActionRole ?? string.Empty, out actionId))
                throw new InvalidDataException(
                    "Command binding '" + binding.IntentId + "' references unresolved action role '" +
                    (binding.ActionRole ?? string.Empty) + "'.");
            bool duplicate = false;
            for (int resultIndex = 0; resultIndex < result.Count; resultIndex++)
            {
                if (string.Equals((string)result[resultIndex]?["intentId"], binding.IntentId, StringComparison.Ordinal))
                {
                    result[resultIndex] = new JsonObject
                    {
                        ["intentId"] = binding.IntentId,
                        ["actionId"] = actionId,
                        ["priority"] = binding.Priority,
                        ["allowQueue"] = binding.AllowQueue,
                        ["queueWindowFrames"] = binding.QueueWindowFrames
                    };
                    duplicate = true;
                    break;
                }
            }
            if (duplicate)
                continue;
            result.Add(new JsonObject
            {
                ["intentId"] = binding.IntentId,
                ["actionId"] = actionId,
                ["priority"] = binding.Priority,
                ["allowQueue"] = binding.AllowQueue,
                ["queueWindowFrames"] = binding.QueueWindowFrames
            });
        }
        return result;
    }

    private static JsonArray BuildCommandSequences(CombinedWeaponActionConfiguration configuration)
    {
        var result = new JsonArray();
        var actionIdsByRole = new Dictionary<string, int>(StringComparer.Ordinal);
        List<CombinedWeaponActionEntry> actions = configuration?.Actions;
        for (int i = 0; actions != null && i < actions.Count; i++)
        {
            CombinedWeaponActionEntry action = actions[i];
            if (action != null && !string.IsNullOrWhiteSpace(action.ActionRole))
                actionIdsByRole[action.ActionRole] = action.ActionId;
        }

        List<CombinedWeaponCommandSequence> sequences = configuration?.CommandSequences;
        for (int i = 0; sequences != null && i < sequences.Count; i++)
        {
            CombinedWeaponCommandSequence sequence = sequences[i];
            if (sequence == null || string.IsNullOrWhiteSpace(sequence.IntentId))
                continue;
            var actionIds = new JsonArray();
            for (int roleIndex = 0; sequence.ActionRoles != null && roleIndex < sequence.ActionRoles.Count; roleIndex++)
            {
                string role = sequence.ActionRoles[roleIndex];
                if (!actionIdsByRole.TryGetValue(role ?? string.Empty, out int actionId) ||
                    actionId <= 0)
                    throw new InvalidDataException(
                        "Command sequence '" + sequence.IntentId + "' references unresolved action role '" + role + "'.");
                actionIds.Add(actionId);
            }
            result.Add(new JsonObject
            {
                ["intentId"] = sequence.IntentId,
                ["actionIds"] = actionIds,
                ["wrapAfterLast"] = sequence.WrapAfterLast,
                ["priority"] = sequence.Priority,
                ["allowQueue"] = sequence.AllowQueue,
                ["queueWindowFrames"] = sequence.QueueWindowFrames,
            });
        }
        return result;
    }

    private static JsonArray BuildAbilityBindings(CombinedWeaponActionConfiguration actions)
    {
        var result = new JsonArray();
        List<CombinedWeaponAbilityActionBinding> bindings = actions?.AbilityActionBindings;
        for (int i = 0; bindings != null && i < bindings.Count; i++)
        {
            CombinedWeaponAbilityActionBinding binding = bindings[i];
            if (binding == null)
                continue;
            result.Add(new JsonObject
            {
                ["abilityId"] = binding.AbilityId,
                ["actionId"] = binding.ActionId,
                ["requiredTags"] = new JsonArray(),
                ["forbiddenTags"] = new JsonArray()
            });
        }
        return result;
    }

    private static JsonArray BuildReactionRules(
        GlobalReactionConfiguration reaction,
        CombinedWeaponActionConfiguration actions)
    {
        var result = new JsonArray();
        for (int i = 0; reaction?.Slots != null && i < reaction.Slots.Count; i++)
        {
            GlobalReactionSlotConfiguration slot = reaction.Slots[i];
            int actionId = ResolveReactionActionId(slot?.SlotId, slot?.ActionRole, actions);
            if (slot == null || actionId <= 0)
                continue;
            result.Add(new JsonObject
            {
                ["actionId"] = actionId,
                ["trigger"] = slot.Trigger,
                ["currentScatterPressureBand"] = slot.PressureBand,
                ["priority"] = slot.Priority,
                ["minImpactForce"] = slot.MinImpactForce,
                ["maxImpactForce"] = slot.MaxImpactForce,
                ["reactionGroupId"] = slot.ReactionGroupId,
                ["requiresImpactForce"] = slot.MinImpactForce.HasValue || slot.MaxImpactForce.HasValue
            });
        }
        return result;
    }

    private static int ResolveDefaultReactionActionId(
        GlobalReactionConfiguration reaction,
        CombinedWeaponActionConfiguration actions)
    {
        GlobalReactionSlotConfiguration slot = reaction?.Slots?.Find(item =>
            item != null && string.Equals(item.SlotId, reaction.DefaultSlotId, StringComparison.Ordinal))
            ?? (reaction?.Slots != null && reaction.Slots.Count > 0 ? reaction.Slots[0] : null);
        return ResolveReactionActionId(slot?.SlotId, slot?.ActionRole, actions);
    }

    private static int ResolveAbilityActionId(
        int abilityId,
        string actionRole,
        CombinedWeaponActionConfiguration actions)
    {
        return actions?.AbilityActionBindings?.Find(item => item != null && item.AbilityId == abilityId &&
            string.Equals(item.ActionRole, actionRole, StringComparison.Ordinal))?.ActionId ?? 0;
    }

    private static int ResolveReactionActionId(
        string slotId,
        string actionRole,
        CombinedWeaponActionConfiguration actions)
    {
        return actions?.ReactionActionBindings?.Find(item => item != null &&
            string.Equals(item.ReactionSlotId, slotId, StringComparison.Ordinal) &&
            string.Equals(item.ActionRole, actionRole, StringComparison.Ordinal))?.ActionId ?? 0;
    }

    private static int ResolveActionDurationFrames(string actionRole, GlobalReactionConfiguration reaction)
    {
        GlobalReactionSlotConfiguration slot = reaction?.Slots?.Find(item => item != null &&
            string.Equals(item.ActionRole, actionRole, StringComparison.Ordinal));
        return Math.Max(1, slot?.DurationFrames ?? 1);
    }

    private static JsonObject BuildDefaultCharacterAction(
        CombinedWeaponActionEntry source,
        int id,
        int durationFrames)
    {
        JsonNode animationTrack = null;
        if (!string.IsNullOrWhiteSpace(source.AnimationClipId))
        {
            animationTrack = new JsonObject
            {
                ["events"] = new JsonArray(new JsonObject
                {
                    ["frame"] = 0,
                    ["endFrame"] = durationFrames - 1,
                    ["playbackSpeed"] = 1f,
                    ["kind"] = "CrossFadeAnimation",
                    ["animationActionKey"] = source.ActionRole,
                    ["animationClipResourceKey"] = source.AnimationClipId,
                    ["animationStartOffsetFrames"] = 0,
                    ["animationEndOffsetFrames"] = 0,
                    ["transitionSeconds"] = 0.08f,
                    ["stableEventId"] = source.TimelineId
                })
            };
        }
        return new JsonObject
        {
            ["id"] = id,
            ["stableId"] = source.ActionId,
            ["displayName"] = source.ActionRole,
            ["category"] = ResolveCharacterActionCategory(source.ActionRole),
            ["timelineAuthority"] = "CharacterAuthored",
            ["tags"] = new JsonArray(source.ActionRole),
            ["priority"] = 0,
            ["durationFrames"] = durationFrames,
            ["requirements"] = new JsonArray(),
            ["phases"] = new JsonArray(new JsonObject
            {
                ["kind"] = "Active",
                ["startFrame"] = 0,
                ["endFrame"] = durationFrames - 1
            }),
            ["cancelRules"] = new JsonArray(),
            ["interruptRules"] = new JsonArray(),
            ["cohesionSupport"] = null,
            ["motionTrack"] = null,
            ["combatTrack"] = null,
            ["gameplayTrack"] = null,
            ["animationTrack"] = animationTrack,
            ["presentationTrack"] = null,
            ["debugTrack"] = null
        };
    }

    private static string ResolveCharacterActionCategory(string actionRole)
    {
        if (actionRole.StartsWith("reaction.", StringComparison.Ordinal))
            return "Reaction";
        return actionRole.IndexOf("dodge", StringComparison.OrdinalIgnoreCase) >= 0 ||
            actionRole.IndexOf("evade", StringComparison.OrdinalIgnoreCase) >= 0
            ? "Dodge"
            : "Skill";
    }

    private static void AddContentWrite(
        List<CharacterUnityImportWriteInput> writes,
        string kind,
        string sourcePath,
        string targetPath,
        string content,
        string writePolicy)
    {
        content = EnsureTrailingNewline(content);
        writes.Add(new CharacterUnityImportWriteInput
        {
            Kind = kind,
            Owner = CharacterAuthoringCompilerOwnerKinds.UnityImporter,
            SourcePath = sourcePath,
            TargetPath = targetPath,
            WritePolicy = writePolicy,
            Content = content,
            ContentHash = CharacterPackageHashUtility.ComputeTextSha256(content)
        });
    }

    private static void AddHumanoidAvatarSourceWrites(
        List<CharacterUnityImportWriteInput> writes,
        string packagePath,
        CharacterResourcePackage package,
        CharacterAuthoringCompileResult result,
        string targetRoot)
    {
        if (writes == null || result?.ResourceMapping == null)
            return;

        var existingTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < writes.Count; i++)
        {
            string target = NormalizeProjectRelativePath(writes[i]?.TargetPath);
            if (!string.IsNullOrWhiteSpace(target))
                existingTargets.Add(target);
        }

        for (int i = 0; i < result.ResourceMapping.Entries.Count; i++)
        {
            CharacterPackageResourceMappingEntry mapping = result.ResourceMapping.Entries[i];
            HumanoidAvatarSourceInfo avatarSource = ResolveRigAvatarSource(
                packagePath,
                package,
                result,
                mapping,
                BuildMappingLookup(result.ResourceMapping));
            if (avatarSource == null || !avatarSource.Available || avatarSource.SourceIsPrimaryResource)
                continue;

            string targetPath = avatarSource.UnityAssetPath;
            if (string.IsNullOrWhiteSpace(targetPath))
                targetPath = CombineProjectPath(targetRoot, avatarSource.RelativePath);
            targetPath = NormalizeProjectRelativePath(targetPath);
            if (!existingTargets.Add(targetPath))
                continue;

            writes.Add(new CharacterUnityImportWriteInput
            {
                Kind = "avatarSource",
                Owner = CharacterAuthoringCompilerOwnerKinds.UnityImporter,
                SourcePath = avatarSource.RelativePath,
                SourceFilePath = avatarSource.FullPath,
                TargetPath = targetPath,
                WritePolicy = "CopyIfHashChanged",
                ContentHash = avatarSource.ContentHash
            });
        }
    }

    private static string CreateUnityResourceCatalogJson(
        string packagePath,
        CharacterResourcePackage package,
        CharacterAuthoringCompileResult result,
        JsonSerializerOptions options)
    {
        CharacterPackageResourceCatalog packageCatalog = package != null ? package.ResourceCatalog : null;
        CharacterPackageResourceMapping mapping = result != null ? result.ResourceMapping : null;
        var sourceByKey = new Dictionary<string, CharacterPackageResourceEntry>(StringComparer.Ordinal);
        if (packageCatalog != null)
        {
            for (int i = 0; i < packageCatalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = packageCatalog.Entries[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.ResourceKey))
                    sourceByKey[entry.ResourceKey] = entry;
            }
        }

        var mappingByPackageKey = new Dictionary<string, CharacterPackageResourceMappingEntry>(StringComparer.Ordinal);
        var hashByPackageKey = BuildHashEntryLookup(result != null ? result.ResourceHashReport : null);
        var entries = new List<UnityResourceCatalogEntryDto>();
        if (mapping != null)
        {
            for (int i = 0; i < mapping.Entries.Count; i++)
            {
                CharacterPackageResourceMappingEntry entry = mapping.Entries[i];
                if (entry == null)
                    continue;

                mappingByPackageKey[entry.PackageResourceKey] = entry;
            }

            for (int i = 0; i < mapping.Entries.Count; i++)
            {
                CharacterPackageResourceMappingEntry entry = mapping.Entries[i];
                if (entry == null)
                    continue;

                CharacterPackageResourceEntry sourceEntry;
                sourceByKey.TryGetValue(entry.PackageResourceKey, out sourceEntry);
                CharacterPackageResourceHashEntry hashEntry;
                hashByPackageKey.TryGetValue(entry.PackageResourceKey, out hashEntry);
                entries.Add(CreateUnityResourceCatalogEntry(packagePath, package, result, sourceEntry, entry, mappingByPackageKey, hashEntry));
            }
        }

        var dto = new UnityResourceCatalogDto
        {
            format = "mx.characterUnityResourceCatalog.v1",
            schemaVersion = 1,
            catalogId = "character.package." + (result != null ? result.PackageId : string.Empty),
            packageId = result != null ? result.PackageId : string.Empty,
            generatedAtUtc = string.Empty,
            entries = entries.ToArray()
        };
        return EnsureTrailingNewline(JsonSerializer.Serialize(dto, options));
    }

    private static Dictionary<string, CharacterPackageResourceHashEntry> BuildHashEntryLookup(CharacterPackageResourceHashReport report)
    {
        var lookup = new Dictionary<string, CharacterPackageResourceHashEntry>(StringComparer.Ordinal);
        if (report == null || report.Entries == null)
            return lookup;

        for (int i = 0; i < report.Entries.Count; i++)
        {
            CharacterPackageResourceHashEntry entry = report.Entries[i];
            if (entry != null && !string.IsNullOrWhiteSpace(entry.ResourceKey))
                lookup[entry.ResourceKey] = entry;
        }

        return lookup;
    }

    private static UnityResourceCatalogEntryDto CreateUnityResourceCatalogEntry(
        string packagePath,
        CharacterResourcePackage package,
        CharacterAuthoringCompileResult result,
        CharacterPackageResourceEntry sourceEntry,
        CharacterPackageResourceMappingEntry mapping,
        Dictionary<string, CharacterPackageResourceMappingEntry> mappingByPackageKey,
        CharacterPackageResourceHashEntry hashEntry)
    {
        string packageId = result != null ? result.PackageId : string.Empty;
        string typeId = MapUnityResourceTypeId(mapping.TypeId);
        bool extractsAnimationClip = string.Equals(mapping.TypeId, CharacterPackageResourceTypeIds.Animation, StringComparison.OrdinalIgnoreCase)
            && string.Equals(mapping.SourceFormat, CharacterPackageResourceFormatIds.Fbx, StringComparison.OrdinalIgnoreCase);
        string sourceAssetPath = mapping.ImportTargetPath ?? string.Empty;
        string runtimeAssetPath = extractsAnimationClip
            ? CharacterGeneratedAnimationClipAssetPaths.Resolve(
                packageId,
                !string.IsNullOrWhiteSpace(mapping.ProjectResourceKey)
                    ? mapping.ProjectResourceKey
                    : mapping.PackageResourceKey)
            : sourceAssetPath;
        string runtimeSourceFormat = extractsAnimationClip
            ? CharacterPackageResourceFormatIds.Anim
            : mapping.SourceFormat;
        string importerKind = GuessUnityImporterKind(runtimeSourceFormat, runtimeAssetPath);
        string declaredContentHash = !string.IsNullOrWhiteSpace(mapping.DeclaredContentHash)
            ? mapping.DeclaredContentHash
            : hashEntry != null ? hashEntry.DeclaredContentHash : string.Empty;
        string contentHash = hashEntry != null && !string.IsNullOrWhiteSpace(hashEntry.ComputedContentHash)
            ? hashEntry.ComputedContentHash
            : declaredContentHash;
        CharacterUnityImportArtifactInfo artifactInfo = AnalyzeUnityImportArtifact(packagePath, mapping, contentHash);
        string rigType = ResolveCompiledRigType(package, result);
        HumanoidAvatarSourceInfo avatarSource = ResolveRigAvatarSource(
            packagePath,
            package,
            result,
            mapping,
            mappingByPackageKey);
        AnimationUnityImportSettings animationImport = ResolveAnimationUnityImportSettings(result, mapping);
        string sourceHash = CharacterPackageHashUtility.NormalizeSha256(artifactInfo.SourceHash);
        if (string.IsNullOrWhiteSpace(sourceHash))
            sourceHash = CharacterPackageHashUtility.NormalizeSha256(contentHash);
        string artifactHash = CharacterPackageHashUtility.NormalizeSha256(artifactInfo.ArtifactHash);
        var diagnostics = BuildUnityResourceDiagnostics(mapping, hashEntry);
        var providerData = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["assetPath"] = runtimeAssetPath,
            ["unityAssetPath"] = runtimeAssetPath,
            ["unityAssetGuid"] = string.Empty,
            ["unityMainObjectType"] = string.Empty,
            ["importerKind"] = importerKind,
            ["importStatus"] = "PendingUnityImport",
            ["packageResourceKey"] = mapping.PackageResourceKey,
            ["stableId"] = mapping.StableId,
            ["sourceRelativePath"] = mapping.SourceRelativePath,
            ["sourceFormat"] = runtimeSourceFormat,
            ["authoringSourceFormat"] = mapping.SourceFormat,
            ["sourceFbxAssetPath"] = extractsAnimationClip ? sourceAssetPath : string.Empty,
            ["usage"] = mapping.Usage,
            ["declaredContentHash"] = declaredContentHash,
            ["contentHash"] = contentHash,
            ["sourceHash"] = sourceHash,
            ["artifactHash"] = artifactHash,
            ["artifactStatus"] = artifactInfo.ArtifactStatus ?? string.Empty,
            ["artifactValidationStatus"] = artifactInfo.ValidationStatus ?? string.Empty,
            ["artifactTransforms"] = string.Join(",", artifactInfo.Transforms ?? new List<string>()),
            ["rigType"] = rigType,
            ["avatarSourceStatus"] = avatarSource != null ? avatarSource.Status : string.Empty,
            ["avatarSourceRelativePath"] = avatarSource != null ? avatarSource.RelativePath : string.Empty,
            ["avatarSourceAssetPath"] = avatarSource != null ? avatarSource.UnityAssetPath : string.Empty,
            ["avatarSourceContentHash"] = avatarSource != null ? avatarSource.ContentHash : string.Empty,
            ["avatarSourceMessage"] = avatarSource != null ? avatarSource.Message : string.Empty,
            ["avatarSourceRigStatus"] = string.Empty,
            ["animationTakeName"] = animationImport.TakeName,
            ["animationClipName"] = animationImport.ClipName,
            ["animationLoopTime"] = animationImport.LoopTime ? "true" : "false",
            ["animationLoopBlend"] = animationImport.LoopBlend ? "true" : "false",
            ["rootMotionPolicy"] = animationImport.RootMotionPolicy,
            ["requestedProviderId"] = mapping.ProviderId,
            ["importHash"] = mapping.ImportHash,
            ["dependencyHash"] = mapping.DependencyHash,
            ["diagnosticCount"] = diagnostics.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["generatedBy"] = "CharacterUnityImportBridge",
            ["sourcePackageHash"] = result != null && result.Hashes != null ? result.Hashes.SourcePackageHash : string.Empty
        };
        CopyUnityResourceProviderMetadata(sourceEntry, providerData);

        return new UnityResourceCatalogEntryDto
        {
            id = mapping.ProjectResourceKey,
            type = typeId,
            variant = sourceEntry != null ? sourceEntry.Variant : string.Empty,
            packageId = packageId,
            provider = "memory",
            address = runtimeAssetPath,
            packageResourceKey = mapping.PackageResourceKey,
            stableId = mapping.StableId,
            usage = mapping.Usage,
            sourceRelativePath = mapping.SourceRelativePath,
            sourceFormat = runtimeSourceFormat,
            declaredContentHash = declaredContentHash,
            contentHash = contentHash,
            sourceHash = sourceHash,
            artifactHash = artifactHash,
            artifactStatus = artifactInfo.ArtifactStatus ?? string.Empty,
            artifactValidationStatus = artifactInfo.ValidationStatus ?? string.Empty,
            artifactTransforms = artifactInfo.Transforms != null ? artifactInfo.Transforms.ToArray() : Array.Empty<string>(),
            rigType = rigType,
            avatarSourceStatus = avatarSource != null ? avatarSource.Status : string.Empty,
            avatarSourceRelativePath = avatarSource != null ? avatarSource.RelativePath : string.Empty,
            avatarSourceAssetPath = avatarSource != null ? avatarSource.UnityAssetPath : string.Empty,
            avatarSourceContentHash = avatarSource != null ? avatarSource.ContentHash : string.Empty,
            avatarSourceMessage = avatarSource != null ? avatarSource.Message : string.Empty,
            importHash = mapping.ImportHash,
            dependencyHash = mapping.DependencyHash,
            unityAssetPath = runtimeAssetPath,
            unityAssetGuid = string.Empty,
            unityMainObjectType = string.Empty,
            importerKind = importerKind,
            importStatus = "PendingUnityImport",
            diagnostics = diagnostics,
            labels = BuildUnityResourceLabels(packageId, sourceEntry, mapping),
            dependencies = BuildUnityResourceDependencies(packageId, sourceEntry, mappingByPackageKey),
            hash = declaredContentHash,
            size = GetPackageResourceSize(packagePath, mapping.SourceRelativePath),
            allowOverride = false,
            providerData = providerData
        };
    }

    private static void CopyUnityResourceProviderMetadata(
        CharacterPackageResourceEntry sourceEntry,
        Dictionary<string, string> providerData)
    {
        Dictionary<string, string> metadata = sourceEntry?.ImportHints?.Metadata;
        if (metadata == null || providerData == null)
            return;

        foreach (KeyValuePair<string, string> pair in metadata)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key))
                providerData[pair.Key] = pair.Value ?? string.Empty;
        }
    }

    private static HumanoidAvatarSourceInfo ResolveRigAvatarSource(
        string packagePath,
        CharacterResourcePackage package,
        CharacterAuthoringCompileResult result,
        CharacterPackageResourceMappingEntry mapping,
        Dictionary<string, CharacterPackageResourceMappingEntry> mappingByPackageKey)
    {
        string rigType = ResolveCompiledRigType(package, result);
        if (mapping != null
            && string.Equals(mapping.Usage, CharacterPackageResourceUsageIds.AnimationClipGroup, StringComparison.OrdinalIgnoreCase)
            && string.Equals(rigType, GlobalAnimationRigTypes.Generic, StringComparison.Ordinal))
        {
            CharacterPackageResourceMappingEntry body = FindCharacterModelMapping(mappingByPackageKey);
            if (body == null)
            {
                return new HumanoidAvatarSourceInfo
                {
                    Status = "Missing",
                    Message = "Generic animation FBX requires the package character model Avatar source."
                };
            }
            HumanoidAvatarSourceInfo bodySource = ResolveBodyAvatarSource(packagePath, body, rigType);
            if (bodySource != null)
                bodySource.SourceIsPrimaryResource = false;
            return bodySource;
        }

        if (!IsCharacterModelMapping(mapping))
            return null;

        return ResolveBodyAvatarSource(packagePath, mapping, rigType);
    }

    private static HumanoidAvatarSourceInfo ResolveBodyAvatarSource(
        string packagePath,
        CharacterPackageResourceMappingEntry mapping,
        string rigType)
    {
        if (string.IsNullOrWhiteSpace(rigType))
        {
            return new HumanoidAvatarSourceInfo
            {
                Status = "RigTypeUnspecified",
                Message = "Skeletal character model must explicitly declare rigType; Generic is not inferred."
            };
        }

        if (string.IsNullOrWhiteSpace(packagePath) || string.IsNullOrWhiteSpace(mapping.SourceRelativePath))
        {
            return new HumanoidAvatarSourceInfo
            {
                Status = "Missing",
                Message = rigType + " character model did not declare a source path for Avatar generation."
            };
        }

        string format = (mapping.SourceFormat ?? string.Empty).Trim().ToLowerInvariant();
        if (format == CharacterPackageResourceFormatIds.Fbx
            || mapping.SourceRelativePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
        {
            string normalized = NormalizeProjectRelativePath(mapping.SourceRelativePath);
            string fullPath = ResolvePackageFilePath(packagePath, normalized);
            bool exists = !string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath);
            return new HumanoidAvatarSourceInfo
            {
                Status = exists ? "Ready" : "Missing",
                RelativePath = normalized,
                UnityAssetPath = NormalizeProjectRelativePath(mapping.ImportTargetPath),
                FullPath = exists ? fullPath : string.Empty,
                ContentHash = exists ? CharacterPackageHashUtility.ComputeSha256(File.ReadAllBytes(fullPath)) : string.Empty,
                Message = exists
                    ? rigType + " Avatar will be generated from this FBX body resource."
                    : rigType + " body resource references an FBX Avatar source that is missing.",
                SourceIsPrimaryResource = true
            };
        }

        if (string.Equals(rigType, GlobalAnimationRigTypes.Generic, StringComparison.Ordinal))
        {
            return new HumanoidAvatarSourceInfo
            {
                Status = "Missing",
                Message = "Generic character models must use an FBX body resource so Unity can create the package Avatar."
            };
        }

        List<string> candidates = BuildHumanoidAvatarSourceCandidates(mapping.SourceRelativePath);
        for (int i = 0; i < candidates.Count; i++)
        {
            string relativePath = candidates[i];
            string fullPath = ResolvePackageFilePath(packagePath, relativePath);
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
                continue;

            return new HumanoidAvatarSourceInfo
            {
                Status = "Ready",
                RelativePath = relativePath,
                UnityAssetPath = BuildCompanionAvatarUnityAssetPath(mapping.ImportTargetPath, relativePath),
                FullPath = fullPath,
                ContentHash = CharacterPackageHashUtility.ComputeSha256(File.ReadAllBytes(fullPath)),
                Message = "Humanoid Avatar will be generated from companion FBX source.",
                SourceIsPrimaryResource = false
            };
        }

        return new HumanoidAvatarSourceInfo
        {
            Status = "Missing",
            Message = "Humanoid character model uses a GLB/Gltf Unity artifact but no same-package FBX Avatar source was found."
        };
    }

    private static bool IsCharacterModelMapping(CharacterPackageResourceMappingEntry mapping)
    {
        return mapping != null
            && string.Equals(mapping.Usage, "characterModel", StringComparison.OrdinalIgnoreCase)
            && string.Equals(mapping.TypeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, CharacterPackageResourceMappingEntry> BuildMappingLookup(
        CharacterPackageResourceMapping mapping)
    {
        var lookup = new Dictionary<string, CharacterPackageResourceMappingEntry>(StringComparer.Ordinal);
        for (int i = 0; mapping?.Entries != null && i < mapping.Entries.Count; i++)
        {
            CharacterPackageResourceMappingEntry entry = mapping.Entries[i];
            if (entry != null && !string.IsNullOrWhiteSpace(entry.PackageResourceKey))
                lookup[entry.PackageResourceKey] = entry;
        }
        return lookup;
    }

    private static CharacterPackageResourceMappingEntry FindCharacterModelMapping(
        Dictionary<string, CharacterPackageResourceMappingEntry> mappings)
    {
        if (mappings == null)
            return null;
        foreach (CharacterPackageResourceMappingEntry mapping in mappings.Values)
            if (IsCharacterModelMapping(mapping))
                return mapping;
        return null;
    }

    private static string ResolveCompiledRigType(
        CharacterResourcePackage package,
        CharacterAuthoringCompileResult result)
    {
        string compiled = GlobalAnimationRigTypes.Normalize(
            result?.AnimationCompileResult?.AnimationSetDefinition?.RigType);
        return !string.IsNullOrWhiteSpace(compiled)
            ? compiled
            : GlobalWeaponConfigurationResolver.ResolveCharacterRigType(package);
    }

    private static AnimationUnityImportSettings ResolveAnimationUnityImportSettings(
        CharacterAuthoringCompileResult result,
        CharacterPackageResourceMappingEntry mapping)
    {
        var settings = new AnimationUnityImportSettings();
        if (mapping == null
            || !string.Equals(mapping.Usage, CharacterPackageResourceUsageIds.AnimationClipGroup, StringComparison.OrdinalIgnoreCase))
            return settings;

        AnimationAuthoringCompileResult compiled = result?.AnimationCompileResult;
        List<AnimationClipRegistryEntry> clips = compiled?.AnimationClipRegistry?.Clips;
        for (int i = 0; clips != null && i < clips.Count; i++)
        {
            AnimationClipRegistryEntry clip = clips[i];
            AuthoringResourceSelectionRef selection = clip?.SourceSelection;
            bool matches = string.Equals(selection?.PackageResourceKey, mapping.PackageResourceKey, StringComparison.Ordinal)
                || string.Equals(selection?.ProviderResourceKey, mapping.PackageResourceKey, StringComparison.Ordinal)
                || string.Equals(clip?.RuntimeResourceKey, mapping.PackageResourceKey, StringComparison.Ordinal)
                || string.Equals(clip?.RuntimeResourceKey, mapping.ProjectResourceKey, StringComparison.Ordinal);
            if (!matches)
                continue;

            settings.TakeName = clip.SourceSubClipId ?? string.Empty;
            settings.ClipName = !string.IsNullOrWhiteSpace(clip.SourceClipName)
                ? clip.SourceClipName
                : clip.ClipId ?? string.Empty;
            AnimationSetDefinitionClipRef definition = FindAnimationSetDefinitionClip(
                compiled?.AnimationSetDefinition,
                clip.SetId,
                clip.GroupId,
                clip.ClipId);
            if (definition != null)
            {
                settings.LoopTime = definition.Loop;
                settings.LoopBlend = definition.LoopBlend;
                settings.RootMotionPolicy = definition.RootMotionPolicy ?? string.Empty;
            }
            break;
        }
        return settings;
    }

    private static AnimationSetDefinitionClipRef FindAnimationSetDefinitionClip(
        AnimationSetDefinitionDocument document,
        string setId,
        string groupId,
        string clipId)
    {
        for (int setIndex = 0; document?.Sets != null && setIndex < document.Sets.Count; setIndex++)
        {
            AnimationSetDefinitionSet set = document.Sets[setIndex];
            if (!string.Equals(set?.SetId, setId, StringComparison.Ordinal))
                continue;
            for (int groupIndex = 0; set.Groups != null && groupIndex < set.Groups.Count; groupIndex++)
            {
                AnimationSetDefinitionGroup group = set.Groups[groupIndex];
                if (!string.Equals(group?.GroupId, groupId, StringComparison.Ordinal))
                    continue;
                for (int clipIndex = 0; group.Clips != null && clipIndex < group.Clips.Count; clipIndex++)
                    if (string.Equals(group.Clips[clipIndex]?.ClipId, clipId, StringComparison.Ordinal))
                        return group.Clips[clipIndex];
            }
        }
        return null;
    }

    private static List<string> BuildHumanoidAvatarSourceCandidates(string sourceRelativePath)
    {
        var candidates = new List<string>();
        string normalized = NormalizeProjectRelativePath(sourceRelativePath);
        if (string.IsNullOrWhiteSpace(normalized))
            return candidates;

        string directory = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
        string baseName = Path.GetFileNameWithoutExtension(normalized);
        AddAvatarSourceCandidate(candidates, CombineProjectPath(directory, baseName + ".fbx"));
        const string lightSuffix = "_light";
        if (!string.IsNullOrWhiteSpace(baseName)
            && baseName.EndsWith(lightSuffix, StringComparison.OrdinalIgnoreCase))
        {
            AddAvatarSourceCandidate(
                candidates,
                CombineProjectPath(directory, baseName.Substring(0, baseName.Length - lightSuffix.Length) + ".fbx"));
        }

        return candidates;
    }

    private static void AddAvatarSourceCandidate(List<string> candidates, string relativePath)
    {
        relativePath = NormalizeProjectRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(relativePath)
            || !relativePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
            || candidates.Exists(candidate => string.Equals(candidate, relativePath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        candidates.Add(relativePath);
    }

    private static string BuildCompanionAvatarUnityAssetPath(string bodyImportTargetPath, string avatarSourceRelativePath)
    {
        string targetDirectory = Path.GetDirectoryName(bodyImportTargetPath ?? string.Empty)?.Replace('\\', '/');
        string fileName = Path.GetFileName(avatarSourceRelativePath ?? string.Empty);
        if (string.IsNullOrWhiteSpace(targetDirectory) || string.IsNullOrWhiteSpace(fileName))
            return string.Empty;
        return CombineProjectPath(targetDirectory, fileName);
    }

    private static string ResolvePackageFilePath(string packagePath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || string.IsNullOrWhiteSpace(relativePath))
            return string.Empty;

        string root = Path.GetFullPath(packagePath);
        if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            root += Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return fullPath.StartsWith(root, StringComparison.Ordinal) ? fullPath : string.Empty;
    }

    private static string NormalizeProjectRelativePath(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/');
    }

    private static CharacterUnityImportArtifactInfo AnalyzeUnityImportArtifact(
        string packagePath,
        CharacterPackageResourceMappingEntry mapping,
        string contentHash)
    {
        string sourceHash = CharacterPackageHashUtility.NormalizeSha256(contentHash);
        var fallback = new CharacterUnityImportArtifactInfo
        {
            SourceHash = sourceHash,
            ArtifactHash = sourceHash,
            ArtifactStatus = "SourceUnchanged",
            ValidationStatus = "NotRequired",
            Message = "source bytes are used as the Unity import artifact."
        };

        if (mapping == null || string.IsNullOrWhiteSpace(packagePath) || string.IsNullOrWhiteSpace(mapping.SourceRelativePath))
            return fallback;

        string sourcePath = Path.GetFullPath(Path.Combine(packagePath, mapping.SourceRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        string packageRoot = Path.GetFullPath(packagePath);
        if (!packageRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            packageRoot += Path.DirectorySeparatorChar;
        if (!sourcePath.StartsWith(packageRoot, StringComparison.Ordinal) || !File.Exists(sourcePath))
            return fallback;

        byte[] bytes = File.ReadAllBytes(sourcePath);
        if (string.IsNullOrWhiteSpace(sourceHash))
            sourceHash = CharacterPackageHashUtility.ComputeSha256(bytes);

        return CharacterUnityImportGlbCompatibility.AnalyzeResourceArtifact(
            mapping.SourceRelativePath,
            mapping.ImportTargetPath,
            bytes,
            sourceHash);
    }

    private static UnityResourceCatalogDiagnosticDto[] BuildUnityResourceDiagnostics(
        CharacterPackageResourceMappingEntry mapping,
        CharacterPackageResourceHashEntry hashEntry)
    {
        var diagnostics = new List<UnityResourceCatalogDiagnosticDto>();
        if (mapping == null)
            return diagnostics.ToArray();

        if (hashEntry == null)
        {
            diagnostics.Add(new UnityResourceCatalogDiagnosticDto
            {
                severity = "Warning",
                code = "CHARPKG_UNITY_HASH_ENTRY_MISSING",
                message = "Resource hash entry was not available when generating Unity resource catalog.",
                sourcePath = mapping.SourceRelativePath,
                field = "contentHash"
            });
        }
        else if (!hashEntry.Exists)
        {
            diagnostics.Add(new UnityResourceCatalogDiagnosticDto
            {
                severity = "Error",
                code = "CHARPKG_UNITY_SOURCE_MISSING",
                message = "Source resource file was missing when generating Unity resource catalog.",
                sourcePath = mapping.SourceRelativePath,
                field = "sourceRelativePath"
            });
        }

        diagnostics.Sort((a, b) => string.CompareOrdinal(a.code, b.code));
        return diagnostics.ToArray();
    }

    private static string[] BuildUnityResourceLabels(
        string packageId,
        CharacterPackageResourceEntry sourceEntry,
        CharacterPackageResourceMappingEntry mapping)
    {
        var labels = new SortedSet<string>(StringComparer.Ordinal);
        labels.Add("package." + packageId);
        labels.Add("character.resourcePackage");
        if (!string.IsNullOrWhiteSpace(mapping.Usage))
            labels.Add("character.usage." + NormalizeLabelSegment(mapping.Usage));
        if (!string.IsNullOrWhiteSpace(mapping.TypeId))
            labels.Add("character.type." + NormalizeLabelSegment(mapping.TypeId));

        if (sourceEntry != null)
        {
            if (sourceEntry.ImportHints != null && sourceEntry.ImportHints.Labels != null)
            {
                for (int i = 0; i < sourceEntry.ImportHints.Labels.Count; i++)
                    labels.Add(sourceEntry.ImportHints.Labels[i]);
            }

            if (sourceEntry.Tags != null)
            {
                for (int i = 0; i < sourceEntry.Tags.Count; i++)
                    labels.Add("tag." + NormalizeLabelSegment(sourceEntry.Tags[i]));
            }
        }

        var result = new string[labels.Count];
        labels.CopyTo(result);
        return result;
    }

    private static UnityResourceKeyDto[] BuildUnityResourceDependencies(
        string packageId,
        CharacterPackageResourceEntry sourceEntry,
        Dictionary<string, CharacterPackageResourceMappingEntry> mappingByPackageKey)
    {
        if (sourceEntry == null || sourceEntry.Dependencies == null || sourceEntry.Dependencies.Count == 0)
            return Array.Empty<UnityResourceKeyDto>();

        var dependencies = new List<UnityResourceKeyDto>();
        for (int i = 0; i < sourceEntry.Dependencies.Count; i++)
        {
            CharacterPackageResourceDependency dependency = sourceEntry.Dependencies[i];
            if (dependency == null || string.IsNullOrWhiteSpace(dependency.ResourceKey))
                continue;

            CharacterPackageResourceMappingEntry target;
            if (!mappingByPackageKey.TryGetValue(dependency.ResourceKey, out target))
                continue;

            dependencies.Add(new UnityResourceKeyDto
            {
                id = target.ProjectResourceKey,
                type = MapUnityResourceTypeId(target.TypeId),
                variant = string.Empty,
                packageId = packageId
            });
        }

        return dependencies.ToArray();
    }

    private static long GetPackageResourceSize(string packagePath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || string.IsNullOrWhiteSpace(relativePath))
            return 0;

        string fullPath = Path.GetFullPath(Path.Combine(packagePath, relativePath));
        string root = Path.GetFullPath(packagePath);
        if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            root += Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.Ordinal) || !File.Exists(fullPath))
            return 0;
        return new FileInfo(fullPath).Length;
    }

    private static string MapUnityResourceTypeId(string packageTypeId)
    {
        switch (packageTypeId)
        {
            case CharacterPackageResourceTypeIds.Model:
            case CharacterPackageResourceTypeIds.Vfx:
                return "GameObject";
            case CharacterPackageResourceTypeIds.Texture:
            case CharacterPackageResourceTypeIds.Preview:
                return "Texture2D";
            case CharacterPackageResourceTypeIds.Material:
                return "Material";
            case CharacterPackageResourceTypeIds.Animation:
                return "AnimationClip";
            case CharacterPackageResourceTypeIds.Audio:
                return "AudioClip";
            case CharacterPackageResourceTypeIds.Config:
            case CharacterPackageResourceTypeIds.Geometry:
                return "TextAsset";
            default:
                return "Object";
        }
    }

    private static string GuessUnityImporterKind(string sourceFormat, string importTargetPath)
    {
        string format = string.IsNullOrWhiteSpace(sourceFormat)
            ? Path.GetExtension(importTargetPath ?? string.Empty).TrimStart('.')
            : sourceFormat;
        format = (format ?? string.Empty).ToLowerInvariant();

        switch (format)
        {
            case CharacterPackageResourceFormatIds.Glb:
            case CharacterPackageResourceFormatIds.Gltf:
                return "unity-gltf";
            case CharacterPackageResourceFormatIds.Fbx:
                return "unity-fbx";
            case CharacterPackageResourceFormatIds.Png:
            case CharacterPackageResourceFormatIds.Jpg:
            case CharacterPackageResourceFormatIds.Jpeg:
            case CharacterPackageResourceFormatIds.Tga:
                return "unity-texture";
            default:
                return string.IsNullOrWhiteSpace(format) ? "unity-asset" : "unity-" + format;
        }
    }

    private static string NormalizeLabelSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = char.ToLowerInvariant(value[i]);
            bool valid = (c >= 'a' && c <= 'z')
                || (c >= '0' && c <= '9')
                || c == '.'
                || c == '_'
                || c == '-';
            builder.Append(valid ? c : '.');
        }

        return builder.ToString().Trim('.');
    }

    private static string ReadPackageFile(string packagePath, string relativePath)
    {
        string path = Path.Combine(packagePath, relativePath);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static string EnsureTrailingNewline(string content)
    {
        if (string.IsNullOrEmpty(content))
            return "\n";
        return content.EndsWith("\n", StringComparison.Ordinal) ? content : content + "\n";
    }

    private static void WriteProjectText(string projectRoot, string projectRelativePath, string content)
    {
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath));
        string root = Path.GetFullPath(projectRoot);
        if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            root += Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.Ordinal))
            throw new InvalidOperationException("target path escapes Unity project root: " + projectRelativePath);

        string directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, EnsureTrailingNewline(content));
    }

    private static string CombineProjectPath(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
            return (right ?? string.Empty).Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(right))
            return left.Replace('\\', '/');
        return left.TrimEnd('/', '\\').Replace('\\', '/') + "/" + right.TrimStart('/', '\\').Replace('\\', '/');
    }

    internal static AuthoringResourceCollection BuildCharacterPackageAuthoringResources(
        string projectRoot,
        string packagePath,
        CharacterResourcePackage package,
        JsonSerializerOptions options)
    {
        string resolvedRoot = ResolveProjectRoot(projectRoot, packagePath);
        string packageFullPath = Path.GetFullPath(packagePath ?? string.Empty);
        string packageId = package != null && package.Manifest != null ? package.Manifest.PackageId ?? string.Empty : string.Empty;
        string scopeId = "characterPackage:" + packageId;
        string generatedRoot = Path.Combine(resolvedRoot, "Assets", "MxFrameworkGenerated", "CharacterPackages", packageId);
        string unityResourceCatalogPath = Path.Combine(generatedRoot, "config", "unity_resource_catalog.json");
        string globalRuntimeCatalogPath = Path.Combine(resolvedRoot, "Assets", "StreamingAssets", "MxFramework", "Resources", "global_runtime_catalog.json");
        string resourceMappingProfilePath = Path.Combine(resolvedRoot, "Assets", "Config", "MxFramework", "ResourceProfiles", "global_resource_mapping_profile.v2.json");
        string globalPreloadGroupsPath = Path.Combine(resolvedRoot, "Assets", "StreamingAssets", "MxFramework", "Resources", "global_preload_groups.json");
        string globalBundleDependenciesPath = Path.Combine(resolvedRoot, "Assets", "StreamingAssets", "MxFramework", "Resources", "global_bundle_dependencies.json");
        string globalResourceBuildReportPath = Path.Combine(resolvedRoot, "Assets", "Config", "MxFramework", "ResourceBuildReports", "global_resource_build_report.json");

        var context = new AuthoringResourceProviderContext
        {
            ScopeId = scopeId,
            PackageId = packageId,
            PackagePath = ToProjectRelativePath(resolvedRoot, packageFullPath),
            ProjectRootPath = resolvedRoot,
            PackageResourceCatalog = package != null ? package.ResourceCatalog : null,
            UnityResourceCatalog = ReadOptionalProjectJson<AuthoringUnityResourceCatalogDocument>(resolvedRoot, unityResourceCatalogPath, options),
            UnityResourceCatalogPath = ToProjectRelativePath(resolvedRoot, unityResourceCatalogPath),
            RuntimeResourceCatalogPath = ToProjectRelativePath(resolvedRoot, Path.Combine(generatedRoot, "config", "runtime_resource_catalog.json")),
            ResourceMappingProfile = ReadResourceMappingProfile(resolvedRoot, resourceMappingProfilePath),
            ResourceMappingProfilePath = ToProjectRelativePath(resolvedRoot, resourceMappingProfilePath),
            GlobalRuntimeCatalogPath = ToProjectRelativePath(resolvedRoot, globalRuntimeCatalogPath),
            GlobalPreloadGroupsPath = ToProjectRelativePath(resolvedRoot, globalPreloadGroupsPath),
            GlobalBundleDependenciesPath = ToProjectRelativePath(resolvedRoot, globalBundleDependenciesPath),
            GlobalResourceBuildReportPath = ToProjectRelativePath(resolvedRoot, globalResourceBuildReportPath)
        };

        AuthoringResourceCollection collection = AuthoringResourceCollectionMerger.Merge(
            new CharacterPackageAuthoringResourceProvider().BuildResourceCollection(context),
            new CharacterActionAuthoringResourceProvider().BuildResourceCollection(context),
            new ResourceMappingAuthoringResourceProvider().BuildResourceCollection(context),
            new RuntimeCatalogAuthoringResourceProvider().BuildResourceCollection(context));
        collection.ScopeId = scopeId;
        collection.ReferenceGraph = AuthoringResourceReferenceGraphBuilder.FromCharacterPackage(package, collection);
        if (collection.ReferenceGraph != null && collection.ReferenceGraph.Diagnostics != null)
            collection.Diagnostics.AddRange(collection.ReferenceGraph.Diagnostics);
        return collection;
    }

    private static ResourceMappingProfileV2 ReadResourceMappingProfile(string rootPath, string path)
    {
        if (!File.Exists(path)) return null;
        ResourceMappingProfileReadResult read = ResourceMappingProfileJson.ReadStrict(File.ReadAllText(path));
        return read.Success ? read.Profile : null;
    }

    private static string ResolveProjectRoot(string projectRoot, string packagePath)
    {
        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            string explicitRoot = Path.GetFullPath(projectRoot);
            if (Directory.Exists(explicitRoot))
                return explicitRoot;
        }

        string start = Path.GetFullPath(packagePath ?? Directory.GetCurrentDirectory());
        string directory = Directory.Exists(start) ? start : Path.GetDirectoryName(start);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "ProjectSettings", "ProjectVersion.txt")))
                return directory;

            DirectoryInfo parent = Directory.GetParent(directory);
            directory = parent != null ? parent.FullName : string.Empty;
        }

        return Path.GetFullPath(Directory.GetCurrentDirectory());
    }

    private static T ReadOptionalProjectJson<T>(string projectRoot, string path, JsonSerializerOptions options)
        where T : class
    {
        string fullPath = Path.GetFullPath(path ?? string.Empty);
        if (!IsPathAtOrUnderRoot(projectRoot, fullPath) || !File.Exists(fullPath))
            return null;

        return JsonSerializer.Deserialize<T>(File.ReadAllText(fullPath), options);
    }

    private static string ToProjectRelativePath(string projectRoot, string path)
    {
        string root = NormalizeDirectoryRoot(projectRoot);
        string fullPath = Path.GetFullPath(path ?? string.Empty);
        if (!IsPathAtOrUnderRoot(root, fullPath))
            return fullPath.Replace('\\', '/');

        return Path.GetRelativePath(root, fullPath).Replace('\\', '/');
    }

    private static bool IsPathAtOrUnderRoot(string projectRoot, string fullPath)
    {
        string root = NormalizeDirectoryRoot(projectRoot);
        string candidate = Path.GetFullPath(fullPath ?? string.Empty);
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectoryRoot(string path)
    {
        string root = Path.GetFullPath(string.IsNullOrWhiteSpace(path) ? Directory.GetCurrentDirectory() : path);
        if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            root += Path.DirectorySeparatorChar;
        return root;
    }

    private static string CreateUnityImportReportText(CharacterUnityImportReport report)
    {
        if (report == null)
            return string.Empty;

        var builder = new StringBuilder();
        builder.AppendLine("MxFramework Character Unity Import Report");
        builder.Append("package=").Append(report.PackageId).AppendLine();
        builder.Append("status=").Append(report.Status).AppendLine();
        builder.Append("targetRoot=").Append(report.TargetRootPath).AppendLine();
        builder.Append("canWrite=").Append(report.CanWriteToUnityProject).AppendLine();
        builder.Append("canSpawn=").Append(report.CanSpawnAfterImport).AppendLine();
        builder.Append("added=").Append(report.AddedCount)
            .Append(" updated=").Append(report.UpdatedCount)
            .Append(" skipped=").Append(report.SkippedCount)
            .Append(" conflicts=").Append(report.ConflictCount)
            .Append(" errors=").Append(report.ErrorCount)
            .Append(" artifacts=").Append(report.ArtifactCount)
            .Append(" artifactTransforms=").Append(report.ArtifactTransformCount)
            .AppendLine();
        builder.Append("sourcePackageHash=").Append(report.SourcePackageHash).AppendLine();
        builder.Append("resourceMappingHash=").Append(report.ResourceMappingHash).AppendLine();
        builder.AppendLine("issues:");
        if (report.Issues.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                CharacterAuthoringValidationIssue issue = report.Issues[i];
                builder.Append("- ")
                    .Append(issue.Severity)
                    .Append(" gate=").Append(issue.Gate)
                    .Append(" code=").Append(issue.Code)
                    .Append(" sourcePath=").Append(issue.SourcePath)
                    .Append(" object=").Append(issue.SourceObjectPath)
                    .Append(" field=").Append(issue.Field)
                    .Append(" message=").Append(issue.Message)
                    .AppendLine();
            }
        }

        builder.AppendLine("operations:");
        for (int i = 0; i < report.Operations.Count; i++)
        {
            CharacterUnityImportOperation operation = report.Operations[i];
            builder.Append("- ")
                .Append(operation.Action)
                .Append(" kind=").Append(operation.Kind)
                .Append(" source=").Append(operation.SourcePath)
                .Append(" target=").Append(operation.TargetPath)
                .Append(" hash=").Append(operation.ContentHash);
            if (!string.IsNullOrWhiteSpace(operation.SourceHash))
                builder.Append(" sourceHash=").Append(operation.SourceHash);
            if (!string.IsNullOrWhiteSpace(operation.ArtifactHash))
                builder.Append(" artifactHash=").Append(operation.ArtifactHash);
            if (!string.IsNullOrWhiteSpace(operation.ArtifactStatus))
                builder.Append(" artifactStatus=").Append(operation.ArtifactStatus);
            if (!string.IsNullOrWhiteSpace(operation.ArtifactValidationStatus))
                builder.Append(" artifactValidation=").Append(operation.ArtifactValidationStatus);
            if (operation.ArtifactTransforms != null && operation.ArtifactTransforms.Count > 0)
                builder.Append(" artifactTransforms=").Append(string.Join(",", operation.ArtifactTransforms));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string GateReportToText(CharacterCompilerGateReport report)
    {
        if (report == null)
            return string.Empty;

        using var writer = new StringWriter();
        writer.WriteLine("MxFramework Character Authoring Compiler Gate Report");
        writer.WriteLine("package=" + report.PackageId);
        writer.WriteLine("status=" + report.Status);
        for (int i = 0; i < report.Issues.Count; i++)
        {
            CharacterAuthoringValidationIssue issue = report.Issues[i];
            writer.Write(issue.Severity);
            writer.Write(" gate=");
            writer.Write(issue.Gate);
            writer.Write(" code=");
            writer.Write(issue.Code);
            writer.Write(" sourcePath=");
            writer.Write(issue.SourcePath);
            writer.Write(" object=");
            writer.Write(issue.SourceObjectPath);
            writer.Write(" field=");
            writer.Write(issue.Field);
            writer.Write(" message=");
            writer.WriteLine(issue.Message);
        }

        return writer.ToString();
    }

    private static CharacterAuthoringGeometry ReadGeometry(string packagePath, JsonSerializerOptions options)
    {
        CharacterAuthoringGeometry aggregate = ReadOptional<CharacterAuthoringGeometry>(packagePath, Path.Combine("geometry", "authoring_geometry.json"), options);
        var geometry = aggregate ?? new CharacterAuthoringGeometry
        {
            BodyProfile = ReadOptional<CharacterBodyGeometryProfile>(packagePath, Path.Combine("geometry", "body_geometry.json"), options) ?? new CharacterBodyGeometryProfile()
        };

        CharacterBodyPartsDocument bodyParts = aggregate == null
            ? ReadOptional<CharacterBodyPartsDocument>(packagePath, Path.Combine("geometry", "body_parts.json"), options)
            : null;
        if (bodyParts != null && bodyParts.BodyParts != null)
            geometry.BodyParts.AddRange(bodyParts.BodyParts);

        CharacterBodyCollidersDocument colliders = aggregate == null
            ? ReadOptional<CharacterBodyCollidersDocument>(packagePath, Path.Combine("geometry", "body_colliders.json"), options)
            : null;
        if (colliders != null && colliders.Colliders != null)
            geometry.Colliders.AddRange(colliders.Colliders);

        CharacterSocketsDocument sockets = aggregate == null
            ? ReadOptional<CharacterSocketsDocument>(packagePath, Path.Combine("geometry", "sockets.json"), options)
            : null;
        if (sockets != null && sockets.Sockets != null)
            geometry.Sockets.AddRange(sockets.Sockets);

        WeaponSlotsDocument weaponSlots = aggregate == null
            ? ReadOptional<WeaponSlotsDocument>(packagePath, Path.Combine("geometry", "weapon_slots.json"), options)
            : null;
        if (weaponSlots != null && weaponSlots.WeaponSlots != null)
        {
            geometry.WeaponSlotsSchemaVersion = weaponSlots.SchemaVersion;
            geometry.WeaponSlots.AddRange(weaponSlots.WeaponSlots);
        }

        // Schema 5.0 packages use weapon_slots.json as the only geometry fact source.
        // Legacy attachment/trace documents are loaded only when no canonical slot document exists,
        // so migration can inspect them without leaking their ownership into normal compilation.
        if (aggregate == null && weaponSlots == null)
        {
            WeaponAttachmentsDocument attachments = ReadOptional<WeaponAttachmentsDocument>(packagePath, Path.Combine("geometry", "weapon_attachments.json"), options);
            if (attachments != null && attachments.Attachments != null)
            {
                geometry.WeaponAttachmentsSchemaVersion = attachments.SchemaVersion;
                geometry.WeaponAttachments.AddRange(attachments.Attachments);
            }

            WeaponTracesDocument traces = ReadOptional<WeaponTracesDocument>(packagePath, Path.Combine("geometry", "traces.json"), options);
            if (traces != null && traces.Traces != null)
                geometry.Traces.AddRange(traces.Traces);
        }

        CharacterBodyPartContactPointsDocument contactPoints = aggregate == null
            ? ReadOptional<CharacterBodyPartContactPointsDocument>(packagePath, Path.Combine("geometry", "body_part_contact_points.json"), options)
            : null;
        if (contactPoints != null && contactPoints.ContactPoints != null)
            geometry.BodyPartContactPoints.AddRange(contactPoints.ContactPoints);

        ReadDamageZones(packagePath, options, geometry);
        ReadEngagementAnchors(packagePath, options, geometry);
        ReadBodyPresence(packagePath, options, geometry);
        ReadBodyPartDamage(packagePath, options, geometry);
        ReadAuthorityPoseRig(packagePath, options, geometry);
        return geometry;
    }

    private static void ReadDamageZones(
        string packagePath,
        JsonSerializerOptions options,
        CharacterAuthoringGeometry geometry)
    {
        string path = Path.Combine(packagePath, "geometry", "damage_zones.json");
        if (!File.Exists(path))
        {
            geometry.DamageZones = null;
            geometry.DamageZonesDocumentState = CharacterDamageZonesDocumentState.Missing;
            geometry.DamageZonesRawPayload = string.Empty;
            return;
        }

        string payload = File.ReadAllText(path);
        geometry.DamageZonesRawPayload = payload;
        try
        {
            CharacterDamageZonesDocument document =
                JsonSerializer.Deserialize<CharacterDamageZonesDocument>(payload, options);
            geometry.DamageZones = document;
            if (document != null
                && string.Equals(document.Format, CharacterDamageZonesDocument.CurrentFormat, StringComparison.Ordinal)
                && string.Equals(document.SchemaVersion, CharacterDamageZonesDocument.CurrentSchemaVersion, StringComparison.Ordinal))
            {
                geometry.DamageZonesDocumentState = CharacterDamageZonesDocumentState.Current;
            }
            else
            {
                geometry.DamageZonesDocumentState = CharacterDamageZonesDocumentState.UnsupportedSchema;
            }
        }
        catch (JsonException)
        {
            geometry.DamageZones = null;
            geometry.DamageZonesDocumentState = CharacterDamageZonesDocumentState.Invalid;
        }
    }

    private static void ReadEngagementAnchors(
        string packagePath,
        JsonSerializerOptions options,
        CharacterAuthoringGeometry geometry)
    {
        string path = Path.Combine(packagePath, "geometry", "engagement_anchors.json");
        if (!File.Exists(path))
        {
            geometry.EngagementAnchors = null;
            geometry.EngagementAnchorsDocumentState = CharacterEngagementAnchorsDocumentState.Missing;
            geometry.EngagementAnchorsRawPayload = string.Empty;
            return;
        }

        string payload = File.ReadAllText(path);
        geometry.EngagementAnchorsRawPayload = payload;
        try
        {
            CharacterEngagementAnchorsDocument document =
                JsonSerializer.Deserialize<CharacterEngagementAnchorsDocument>(payload, options);
            geometry.EngagementAnchors = document;
            geometry.EngagementAnchorsDocumentState = document != null
                && string.Equals(
                    document.Format,
                    CharacterEngagementAnchorsDocument.CurrentFormat,
                    StringComparison.Ordinal)
                && string.Equals(
                    document.SchemaVersion,
                    CharacterEngagementAnchorsDocument.CurrentSchemaVersion,
                    StringComparison.Ordinal)
                    ? CharacterEngagementAnchorsDocumentState.Current
                    : CharacterEngagementAnchorsDocumentState.UnsupportedSchema;
        }
        catch (JsonException)
        {
            geometry.EngagementAnchors = null;
            geometry.EngagementAnchorsDocumentState = CharacterEngagementAnchorsDocumentState.Invalid;
        }
    }

    private static T ReadRequired<T>(string packagePath, string relativePath, JsonSerializerOptions options) where T : new()
    {
        string path = Path.Combine(packagePath, relativePath);
        if (!File.Exists(path))
            throw new FileNotFoundException("Required character package file is missing: " + path);
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), options) ?? new T();
    }

    private static T ReadOptional<T>(string packagePath, string relativePath, JsonSerializerOptions options) where T : class, new()
    {
        string path = Path.Combine(packagePath, relativePath);
        if (!File.Exists(path))
            return null;
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), options) ?? new T();
    }

    private sealed class UnityResourceCatalogDto
    {
        public string format { get; set; } = "mx.characterUnityResourceCatalog.v1";
        public int schemaVersion { get; set; } = 1;
        public string catalogId { get; set; } = string.Empty;
        public string packageId { get; set; } = string.Empty;
        public string generatedAtUtc { get; set; } = string.Empty;
        public UnityResourceCatalogOrphanDto[] orphanedUnityAssets { get; set; } = Array.Empty<UnityResourceCatalogOrphanDto>();
        public UnityResourceCatalogEntryDto[] entries { get; set; } = Array.Empty<UnityResourceCatalogEntryDto>();
    }

    private sealed class UnityResourceCatalogEntryDto
    {
        public string id { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
        public string variant { get; set; } = string.Empty;
        public string packageId { get; set; } = string.Empty;
        public string provider { get; set; } = string.Empty;
        public string address { get; set; } = string.Empty;
        public string packageResourceKey { get; set; } = string.Empty;
        public string stableId { get; set; } = string.Empty;
        public string usage { get; set; } = string.Empty;
        public string sourceRelativePath { get; set; } = string.Empty;
        public string sourceFormat { get; set; } = string.Empty;
        public string declaredContentHash { get; set; } = string.Empty;
        public string contentHash { get; set; } = string.Empty;
        public string sourceHash { get; set; } = string.Empty;
        public string artifactHash { get; set; } = string.Empty;
        public string artifactStatus { get; set; } = string.Empty;
        public string artifactValidationStatus { get; set; } = string.Empty;
        public string[] artifactTransforms { get; set; } = Array.Empty<string>();
        public string rigType { get; set; } = string.Empty;
        public string avatarSourceStatus { get; set; } = string.Empty;
        public string avatarSourceRelativePath { get; set; } = string.Empty;
        public string avatarSourceAssetPath { get; set; } = string.Empty;
        public string avatarSourceContentHash { get; set; } = string.Empty;
        public string avatarSourceGuid { get; set; } = string.Empty;
        public string avatarSourceHumanoidStatus { get; set; } = string.Empty;
        public string avatarSourceRigStatus { get; set; } = string.Empty;
        public string avatarSourceMessage { get; set; } = string.Empty;
        public string importHash { get; set; } = string.Empty;
        public string dependencyHash { get; set; } = string.Empty;
        public string unityAssetPath { get; set; } = string.Empty;
        public string unityAssetGuid { get; set; } = string.Empty;
        public string unityMainObjectType { get; set; } = string.Empty;
        public string importerKind { get; set; } = string.Empty;
        public string importStatus { get; set; } = string.Empty;
        public UnityResourceCatalogDiagnosticDto[] diagnostics { get; set; } = Array.Empty<UnityResourceCatalogDiagnosticDto>();
        public string[] labels { get; set; } = Array.Empty<string>();
        public UnityResourceKeyDto[] dependencies { get; set; } = Array.Empty<UnityResourceKeyDto>();
        public string hash { get; set; } = string.Empty;
        public long size { get; set; }
        public bool allowOverride { get; set; }
        public Dictionary<string, string> providerData { get; set; } = new();
    }

    private sealed class AnimationUnityImportSettings
    {
        public string TakeName { get; set; } = string.Empty;
        public string ClipName { get; set; } = string.Empty;
        public bool LoopTime { get; set; }
        public bool LoopBlend { get; set; }
        public string RootMotionPolicy { get; set; } = string.Empty;
    }

    private sealed class UnityResourceCatalogDiagnosticDto
    {
        public string severity { get; set; } = string.Empty;
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public string sourcePath { get; set; } = string.Empty;
        public string field { get; set; } = string.Empty;
    }

    private sealed class UnityResourceCatalogOrphanDto
    {
        public string unityAssetPath { get; set; } = string.Empty;
        public string importStatus { get; set; } = "OrphanedUnityAsset";
        public string message { get; set; } = string.Empty;
    }

    private sealed class HumanoidAvatarSourceInfo
    {
        public bool Available => string.Equals(Status, "Ready", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(RelativePath);

        public string Status { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string UnityAssetPath { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool SourceIsPrimaryResource { get; set; }
    }

    private sealed class UnityResourceKeyDto
    {
        public string id { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
        public string variant { get; set; } = string.Empty;
        public string packageId { get; set; } = string.Empty;
    }

    private sealed class CharacterBodyPartsDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<CharacterBodyPartAuthoring> BodyParts { get; set; } = new();
    }

    private sealed class CharacterBodyCollidersDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<CharacterBodyColliderProfile> Colliders { get; set; } = new();
    }

    private sealed class CharacterSocketsDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<CharacterSocketProfile> Sockets { get; set; } = new();
    }

    private sealed class WeaponAttachmentsDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<WeaponAttachmentProfile> Attachments { get; set; } = new();
    }

    private sealed class WeaponSlotsDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<CharacterWeaponSlotProfile> WeaponSlots { get; set; } = new();
    }

    private sealed class WeaponTracesDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<WeaponTraceProfile> Traces { get; set; } = new();
    }

    private sealed class CharacterBodyPartContactPointsDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<CharacterBodyPartContactPointProfile> ContactPoints { get; set; } = new();
    }

    private static void PerformWriteAfterVerify(
        string projectRoot,
        CharacterUnityImportWritePlan writePlan,
        CharacterUnityImportReport report)
    {
        if (writePlan?.Writes == null || report == null)
            return;

        for (int i = 0; i < writePlan.Writes.Count; i++)
        {
            CharacterUnityImportWriteEntry write = writePlan.Writes[i];
            if (write == null || string.IsNullOrWhiteSpace(write.ContentHash))
                continue;

            string diskPath = Path.IsPathRooted(write.TargetPath)
                ? write.TargetPath
                : Path.Combine(projectRoot, write.TargetPath);

            if (!File.Exists(diskPath))
            {
                report.Issues.Add(new CharacterAuthoringValidationIssue
                {
                    Code = "CHARPKG_IMPORT_VERIFY_FILE_MISSING",
                    Gate = CharacterAuthoringValidationGate.ImportBlocked,
                    Severity = CharacterAuthoringValidationSeverity.Error,
                    SourcePath = write.SourcePath,
                    Message = $"Write-after-verify failed: target file does not exist on disk: {write.TargetPath}"
                });
                report.CanSpawnAfterImport = false;
                continue;
            }

            if (string.Equals(write.Kind, CharacterAuthoringCompilerWriteKinds.GeneratedConfigPatch, StringComparison.Ordinal) ||
                string.Equals(write.Kind, CharacterAuthoringCompilerWriteKinds.GeometryBinding, StringComparison.Ordinal) ||
                string.Equals(write.Kind, CharacterAuthoringCompilerWriteKinds.ResourceMapping, StringComparison.Ordinal))
            {
                continue;
            }

            string actualHash = CharacterPackageHashUtility.ComputeFileSha256(diskPath);
            if (!string.Equals(actualHash, write.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                report.Issues.Add(new CharacterAuthoringValidationIssue
                {
                    Code = "CHARPKG_IMPORT_VERIFY_HASH_MISMATCH",
                    Gate = CharacterAuthoringValidationGate.ImportBlocked,
                    Severity = CharacterAuthoringValidationSeverity.Error,
                    SourcePath = write.SourcePath,
                    Message = $"Write-after-verify SHA-256 mismatch for {write.TargetPath}. Expected: {write.ContentHash}, actual: {actualHash}"
                });
                report.CanSpawnAfterImport = false;
            }
        }
    }

    private static CharacterContentIdentityCatalogSnapshot TryLoadIdentityCatalog(string projectRoot, string packagePath = null)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(projectRoot))
            candidates.Add(Path.Combine(projectRoot, "Tools", "RuntimeHash", "character_definition_ids.json"));

        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "Tools", "RuntimeHash", "character_definition_ids.json"));
        candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "Tools", "RuntimeHash", "character_definition_ids.json"));
        candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "..", "Tools", "RuntimeHash", "character_definition_ids.json"));

        string seed = !string.IsNullOrWhiteSpace(packagePath) ? packagePath : Directory.GetCurrentDirectory();
        for (string cur = seed; !string.IsNullOrEmpty(cur); cur = Path.GetDirectoryName(cur))
        {
            candidates.Add(Path.Combine(cur, "Tools", "RuntimeHash", "character_definition_ids.json"));
            string parent = Path.GetDirectoryName(cur);
            if (string.Equals(parent, cur, StringComparison.Ordinal)) break;
        }

        for (string cur = AppDomain.CurrentDomain.BaseDirectory; !string.IsNullOrEmpty(cur); cur = Path.GetDirectoryName(cur))
        {
            candidates.Add(Path.Combine(cur, "Tools", "RuntimeHash", "character_definition_ids.json"));
            string parent = Path.GetDirectoryName(cur);
            if (string.Equals(parent, cur, StringComparison.Ordinal)) break;
        }

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            string fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                try
                {
                    return CharacterContentIdentityCatalogSnapshot.LoadFromFile(fullPath);
                }
                catch
                {
                    // Fallback to next candidate
                }
            }
        }
        return null;
    }

    private static string ResolveProjectRootFromPackageOrCurrent(string projectRoot, string packagePath)
    {
        if (!string.IsNullOrWhiteSpace(projectRoot))
            return projectRoot;

        string seed = !string.IsNullOrWhiteSpace(packagePath) ? packagePath : Directory.GetCurrentDirectory();
        for (string cur = Path.GetFullPath(seed); !string.IsNullOrEmpty(cur); cur = Path.GetDirectoryName(cur))
        {
            if (Directory.Exists(Path.Combine(cur, "Assets")))
                return cur;
            string parent = Path.GetDirectoryName(cur);
            if (string.Equals(parent, cur, StringComparison.Ordinal)) break;
        }

        return Directory.GetCurrentDirectory();
    }

    private static void PopulatePreparedClipBindings(
        string projectRoot,
        CharacterResourcePackage package,
        AnimationSetDefinitionDocument document,
        CharacterContentIdentityCatalogSnapshot identityCatalog)
    {
        if (document?.Sets == null || document.Sets.Count == 0)
            return;

        HashSet<string> keys = CollectCharacterActionAnimationKeys(projectRoot, package);
        if (keys.Count == 0)
            return;

        for (int setIndex = 0; setIndex < document.Sets.Count; setIndex++)
        {
            AnimationSetDefinitionSet set = document.Sets[setIndex];
            if (set == null) continue;

            var existing = new HashSet<string>(StringComparer.Ordinal);
            if (set.ActionBindings != null)
            {
                for (int i = 0; i < set.ActionBindings.Count; i++)
                {
                    var ab = set.ActionBindings[i];
                    if (!string.IsNullOrEmpty(ab?.ActionId))
                        existing.Add(ab.ActionId);
                }
            }

            var prepared = new List<AnimationSetDefinitionPreparedClipBinding>();
            var sortedKeys = new List<string>(keys);
            sortedKeys.Sort(StringComparer.Ordinal);

            for (int k = 0; k < sortedKeys.Count; k++)
            {
                string key = sortedKeys[k];
                if (existing.Contains(key)) continue;

                string label = "runtime.clip." + key;
                int bindingId = 0;
                if (identityCatalog != null && identityCatalog.TryResolve(CharacterContentIdentityDomains.Pose, label, out int resolvedId, out _))
                {
                    bindingId = resolvedId;
                }
                else if (CharacterContentIdentityCatalogSnapshot.DefaultAllowAutoRegisterForTests)
                {
                    bindingId = Math.Abs(label.GetHashCode()) % 100000 + 10000;
                }

                if (bindingId > 0)
                {
                    prepared.Add(new AnimationSetDefinitionPreparedClipBinding
                    {
                        BindingId = bindingId,
                        BindingLabel = label,
                        ClipKey = key
                    });
                }
            }

            set.PreparedClipBindings = prepared;
        }
    }

    private static HashSet<string> CollectCharacterActionAnimationKeys(string projectRoot, CharacterResourcePackage package)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        // 1. In-memory package action libraries
        if (package?.CharacterActionLibrary?.Actions != null)
        {
            for (int i = 0; i < package.CharacterActionLibrary.Actions.Count; i++)
                ExtractAnimationKeysFromElement(package.CharacterActionLibrary.Actions[i], keys);
        }
        if (package?.CommonCharacterActionLibrary?.Actions != null)
        {
            for (int i = 0; i < package.CommonCharacterActionLibrary.Actions.Count; i++)
                ExtractAnimationKeysFromElement(package.CommonCharacterActionLibrary.Actions[i], keys);
        }

        // 2. Project actions on disk (if projectRoot has Assets)
        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            string actionDirectory = Path.Combine(projectRoot, "Assets", "MxFrameworkGenerated", "CharacterActions");
            if (Directory.Exists(actionDirectory))
            {
                string[] jsonFiles = Directory.GetFiles(actionDirectory, "*.json", SearchOption.AllDirectories);
                for (int i = 0; i < jsonFiles.Length; i++)
                {
                    try
                    {
                        using var stream = File.OpenRead(jsonFiles[i]);
                        using var doc = JsonDocument.Parse(stream);
                        ExtractAnimationKeysFromElement(doc.RootElement, keys);
                    }
                    catch
                    {
                        // Ignore corrupted or unparseable JSON files
                    }
                }
            }

            string embeddedPath = Path.Combine(
                projectRoot,
                "Assets", "Scripts", "WGameFrameworkProject", "CharacterTest", "Combat",
                "CharacterTestCharacterActionConfigSource.EmbeddedJson.cs");
            if (File.Exists(embeddedPath))
            {
                try
                {
                    string text = File.ReadAllText(embeddedPath);
                    var regex = new System.Text.RegularExpressions.Regex(
                        @"private const string (\w+Json) = @""((?:[^""]|"""")*)"";",
                        System.Text.RegularExpressions.RegexOptions.Singleline);
                    foreach (System.Text.RegularExpressions.Match match in regex.Matches(text))
                    {
                        string rawJson = match.Groups[2].Value.Replace("\"\"", "\"");
                        try
                        {
                            using var doc = JsonDocument.Parse(rawJson);
                            ExtractAnimationKeysFromElement(doc.RootElement, keys);
                        }
                        catch
                        {
                            // Ignore unparseable embedded JSON
                        }
                    }
                }
                catch
                {
                    // Ignore errors reading embedded file
                }
            }
        }

        return keys;
    }

    private static void ExtractAnimationKeysFromElement(JsonElement element, HashSet<string> keys)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    ExtractAnimationKeysFromElement(item, keys);
                }
                break;

            case JsonValueKind.Object:
                if (element.TryGetProperty("kind", out var kindEl) && kindEl.ValueKind == JsonValueKind.String)
                {
                    string kind = kindEl.GetString();
                    if (string.Equals(kind, "PlayAnimation", StringComparison.Ordinal) ||
                        string.Equals(kind, "CrossFadeAnimation", StringComparison.Ordinal) ||
                        string.Equals(kind, "SetAnimationBlend", StringComparison.Ordinal))
                    {
                        string key = null;
                        if (element.TryGetProperty("animationClipResourceKey", out var clipEl) && clipEl.ValueKind == JsonValueKind.String)
                            key = clipEl.GetString();
                        if (string.IsNullOrWhiteSpace(key) && element.TryGetProperty("animationActionKey", out var actionEl) && actionEl.ValueKind == JsonValueKind.String)
                            key = actionEl.GetString();

                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            keys.Add(key.Trim());
                        }
                    }
                }

                foreach (var prop in element.EnumerateObject())
                {
                    ExtractAnimationKeysFromElement(prop.Value, keys);
                }
                break;
        }
    }
}
