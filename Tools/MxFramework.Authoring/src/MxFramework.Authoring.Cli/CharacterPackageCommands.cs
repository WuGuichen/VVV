using System.Text;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static class CharacterPackageCommands
{
    public static int Dispatch(string[] args, JsonSerializerOptions options)
    {
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
            CharacterAuthoringCompileResult result = CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest
            {
                Package = package,
                PackageRootPath = packagePath,
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
        string outDir = Program.GetOption(args, "--out", string.Empty);
        bool checkHashes = Program.HasFlag(args, "--check-hashes");
        CharacterResourcePackage package = ReadPackage(packagePath, options);
        CharacterResourcePlanCompileResult result = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = packagePath,
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
        bool checkHashes = Program.HasFlag(args, "--check-hashes");

        CharacterResourcePackage package = ReadPackage(packagePath, options);
        CharacterAuthoringCompileResult result = CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest
        {
            Package = package,
            PackageRootPath = packagePath,
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

        CharacterUnityImportReport report = CharacterUnityImportBridge.Execute(new CharacterUnityImportBridgeRequest
        {
            CompileResult = result,
            PackageRootPath = packagePath,
            ProjectRootPath = projectRoot,
            SourcePackageVersion = package.Manifest != null ? package.Manifest.Version : string.Empty,
            DryRun = Program.HasFlag(args, "--dry-run"),
            GeneratedArtifacts = CreateGeneratedArtifacts(result, options),
            AdditionalWrites = CreateUnityImportAdditionalWrites(packagePath, package, result, options)
        });

        string projectReportPath = CombineProjectPath(result.UnityImportWritePlan.TargetRootPath, "package_cache/import_report.json");
        report.ReportPath = projectReportPath;
        string reportJson = JsonSerializer.Serialize(report, options);
        string reportText = CreateUnityImportReportText(report);

        if (report.CanWriteToUnityProject && !report.DryRun && report.ErrorCount == 0 && report.ConflictCount == 0)
        {
            WriteProjectText(projectRoot, projectReportPath, reportJson);
            WriteProjectText(projectRoot, CombineProjectPath(result.UnityImportWritePlan.TargetRootPath, "package_cache/import_report.txt"), reportText);
        }

        if (!string.IsNullOrWhiteSpace(reportOut))
        {
            Directory.CreateDirectory(reportOut);
            File.WriteAllText(Path.Combine(reportOut, "import_report.json"), reportJson);
            File.WriteAllText(Path.Combine(reportOut, "import_report.txt"), reportText);
        }

        Console.Write(reportText);
        return report.Status == "Failed" || result.GateReport.ExportBlocked || result.GateReport.ImportBlocked
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
            Manifest = ReadRequired<CharacterPackageManifest>(packagePath, "manifest.json", options),
            ResourceCatalog = ReadOptional<CharacterPackageResourceCatalog>(packagePath, "resource_catalog.json", options) ?? new CharacterPackageResourceCatalog(),
            Geometry = ReadGeometry(packagePath, options),
            ApplicationConfig = ReadOptional<CharacterApplicationAuthoringSummary>(packagePath, Path.Combine("config", "character_application.json"), options) ?? new CharacterApplicationAuthoringSummary(),
            ValidationReport = ReadOptional<CharacterAuthoringValidationReport>(packagePath, Path.Combine("validation", "last_report.json"), options) ?? new CharacterAuthoringValidationReport()
        };

        return package;
    }

    private static void WriteCompileOutputs(string outDir, CharacterAuthoringCompileResult result, JsonSerializerOptions options)
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "compile_result.json"), JsonSerializer.Serialize(result, options));
        File.WriteAllText(Path.Combine(outDir, "generated_config_patch.json"), JsonSerializer.Serialize(result.GeneratedConfigPatch, options));
        File.WriteAllText(Path.Combine(outDir, "geometry_binding.json"), JsonSerializer.Serialize(result.GeometryBinding, options));
        File.WriteAllText(Path.Combine(outDir, "resource_mapping.json"), JsonSerializer.Serialize(result.ResourceMapping, options));
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

        artifacts.Add(CreateGeneratedArtifact(
            "compiler/generated_config_patch.json",
            JsonSerializer.Serialize(result.GeneratedConfigPatch, options)));
        artifacts.Add(CreateGeneratedArtifact(
            "compiler/geometry_binding.json",
            JsonSerializer.Serialize(result.GeometryBinding, options)));
        artifacts.Add(CreateGeneratedArtifact(
            "compiler/resource_mapping.json",
            JsonSerializer.Serialize(result.ResourceMapping, options)));
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

    private static List<CharacterUnityImportWriteInput> CreateUnityImportAdditionalWrites(
        string packagePath,
        CharacterResourcePackage package,
        CharacterAuthoringCompileResult result,
        JsonSerializerOptions options)
    {
        var writes = new List<CharacterUnityImportWriteInput>();
        if (result == null || result.UnityImportWritePlan == null)
            return writes;

        string targetRoot = result.UnityImportWritePlan.TargetRootPath;
        string configPatch = EnsureTrailingNewline(JsonSerializer.Serialize(result.GeneratedConfigPatch, options));
        string geometryBinding = EnsureTrailingNewline(JsonSerializer.Serialize(result.GeometryBinding, options));
        string resourceMapping = EnsureTrailingNewline(JsonSerializer.Serialize(result.ResourceMapping, options));
        string resolverPlan = EnsureTrailingNewline(JsonSerializer.Serialize(result.ResolverVerificationPlan, options));
        string unityCatalog = CreateUnityResourceCatalogJson(packagePath, package, result, options);
        CharacterResourcePlanCompileResult resourcePlanArtifacts = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = packagePath,
            AuthoringCompileResult = result
        });
        string runtimeResourceCatalog = EnsureTrailingNewline(JsonSerializer.Serialize(resourcePlanArtifacts.RuntimeResourceCatalog, options));
        string characterResourcePlan = EnsureTrailingNewline(JsonSerializer.Serialize(resourcePlanArtifacts.CharacterResourcePlan, options));
        string audioCueManifest = EnsureTrailingNewline(JsonSerializer.Serialize(resourcePlanArtifacts.AudioCueManifest, options));
        string resourceValidationReport = EnsureTrailingNewline(JsonSerializer.Serialize(resourcePlanArtifacts.ResourceValidationReport, options));
        string compileResult = EnsureTrailingNewline(JsonSerializer.Serialize(result, options));
        string writePlan = EnsureTrailingNewline(JsonSerializer.Serialize(result.UnityImportWritePlan, options));
        string resourceHashReport = EnsureTrailingNewline(JsonSerializer.Serialize(result.ResourceHashReport, options));
        string dependencyGraph = EnsureTrailingNewline(JsonSerializer.Serialize(result.DependencyGraph, options));
        string gateReportText = GateReportToText(result.GateReport);

        AddContentWrite(writes, "packageCache", "package/manifest.json", CombineProjectPath(targetRoot, "package_cache/manifest.json"), ReadPackageFile(packagePath, "manifest.json"), "Recreate");
        AddContentWrite(writes, "packageCache", "package/resource_catalog.json", CombineProjectPath(targetRoot, "package_cache/resource_catalog.json"), ReadPackageFile(packagePath, "resource_catalog.json"), "Recreate");
        AddContentWrite(writes, "packageCache", "compiler/compile_result.json", CombineProjectPath(targetRoot, "package_cache/compile_result.json"), compileResult, "Recreate");
        AddContentWrite(writes, "packageCache", "compiler/unity_import_write_plan.json", CombineProjectPath(targetRoot, "package_cache/unity_import_write_plan.json"), writePlan, "Recreate");
        AddContentWrite(writes, "packageCache", "compiler/resource_hash_report.json", CombineProjectPath(targetRoot, "package_cache/resource_hash_report.json"), resourceHashReport, "Recreate");
        AddContentWrite(writes, "packageCache", "compiler/dependency_graph.json", CombineProjectPath(targetRoot, "package_cache/dependency_graph.json"), dependencyGraph, "Recreate");
        AddContentWrite(writes, "packageCache", "compiler/gate_report.txt", CombineProjectPath(targetRoot, "package_cache/gate_report.txt"), gateReportText, "Recreate");

        AddContentWrite(writes, CharacterAuthoringCompilerWriteKinds.GeneratedConfigPatch, "compiler/generated_config_patch.json", CombineProjectPath(targetRoot, "config/character_config_patch.json"), configPatch, "Recreate");
        AddContentWrite(writes, CharacterAuthoringCompilerWriteKinds.GeometryBinding, "compiler/geometry_binding.json", CombineProjectPath(targetRoot, "config/geometry_binding.json"), geometryBinding, "Recreate");
        AddContentWrite(writes, CharacterAuthoringCompilerWriteKinds.ResourceMapping, "compiler/resource_mapping.json", CombineProjectPath(targetRoot, "config/resource_catalog_mapping.json"), resourceMapping, "Recreate");
        AddContentWrite(writes, "resolverVerificationPlan", "compiler/resolver_verification_plan.json", CombineProjectPath(targetRoot, "config/resolver_verification_plan.json"), resolverPlan, "Recreate");
        AddContentWrite(writes, "unityResourceCatalog", "compiler/unity_resource_catalog.json", CombineProjectPath(targetRoot, "config/unity_resource_catalog.json"), unityCatalog, "Recreate");
        AddContentWrite(writes, "runtimeResourceCatalog", "compiler/runtime_resource_catalog.json", CombineProjectPath(targetRoot, "config/runtime_resource_catalog.json"), runtimeResourceCatalog, "Recreate");
        AddContentWrite(writes, "characterResourcePlan", "compiler/character_resource_plan.json", CombineProjectPath(targetRoot, "config/character_resource_plan.json"), characterResourcePlan, "Recreate");
        AddContentWrite(writes, "audioCueManifest", "compiler/audio_cue_manifest.json", CombineProjectPath(targetRoot, "config/audio_cue_manifest.json"), audioCueManifest, "Recreate");
        AddContentWrite(writes, "resourceValidationReport", "compiler/resource_validation_report.json", CombineProjectPath(targetRoot, "config/resource_validation_report.json"), resourceValidationReport, "Recreate");
        return writes;
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
                entries.Add(CreateUnityResourceCatalogEntry(packagePath, result, sourceEntry, entry, mappingByPackageKey, hashEntry));
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
        CharacterAuthoringCompileResult result,
        CharacterPackageResourceEntry sourceEntry,
        CharacterPackageResourceMappingEntry mapping,
        Dictionary<string, CharacterPackageResourceMappingEntry> mappingByPackageKey,
        CharacterPackageResourceHashEntry hashEntry)
    {
        string packageId = result != null ? result.PackageId : string.Empty;
        string typeId = MapUnityResourceTypeId(mapping.TypeId);
        string importerKind = GuessUnityImporterKind(mapping.SourceFormat, mapping.ImportTargetPath);
        string declaredContentHash = !string.IsNullOrWhiteSpace(mapping.DeclaredContentHash)
            ? mapping.DeclaredContentHash
            : hashEntry != null ? hashEntry.DeclaredContentHash : string.Empty;
        string contentHash = hashEntry != null && !string.IsNullOrWhiteSpace(hashEntry.ComputedContentHash)
            ? hashEntry.ComputedContentHash
            : declaredContentHash;
        var diagnostics = BuildUnityResourceDiagnostics(mapping, hashEntry);
        var providerData = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["assetPath"] = mapping.ImportTargetPath,
            ["unityAssetPath"] = mapping.ImportTargetPath,
            ["unityAssetGuid"] = string.Empty,
            ["unityMainObjectType"] = string.Empty,
            ["importerKind"] = importerKind,
            ["importStatus"] = "PendingUnityImport",
            ["packageResourceKey"] = mapping.PackageResourceKey,
            ["stableId"] = mapping.StableId,
            ["sourceRelativePath"] = mapping.SourceRelativePath,
            ["sourceFormat"] = mapping.SourceFormat,
            ["usage"] = mapping.Usage,
            ["declaredContentHash"] = declaredContentHash,
            ["contentHash"] = contentHash,
            ["requestedProviderId"] = mapping.ProviderId,
            ["importHash"] = mapping.ImportHash,
            ["dependencyHash"] = mapping.DependencyHash,
            ["diagnosticCount"] = diagnostics.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["generatedBy"] = "CharacterUnityImportBridge",
            ["sourcePackageHash"] = result != null && result.Hashes != null ? result.Hashes.SourcePackageHash : string.Empty
        };

        return new UnityResourceCatalogEntryDto
        {
            id = mapping.ProjectResourceKey,
            type = typeId,
            variant = sourceEntry != null ? sourceEntry.Variant : string.Empty,
            packageId = packageId,
            provider = "memory",
            address = mapping.ImportTargetPath,
            packageResourceKey = mapping.PackageResourceKey,
            stableId = mapping.StableId,
            usage = mapping.Usage,
            sourceRelativePath = mapping.SourceRelativePath,
            sourceFormat = mapping.SourceFormat,
            declaredContentHash = declaredContentHash,
            contentHash = contentHash,
            importHash = mapping.ImportHash,
            dependencyHash = mapping.DependencyHash,
            unityAssetPath = mapping.ImportTargetPath,
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
                .Append(" hash=").Append(operation.ContentHash)
                .AppendLine();
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
        if (aggregate != null)
            return aggregate;

        var geometry = new CharacterAuthoringGeometry
        {
            BodyProfile = ReadOptional<CharacterBodyGeometryProfile>(packagePath, Path.Combine("geometry", "body_geometry.json"), options) ?? new CharacterBodyGeometryProfile()
        };

        CharacterBodyPartsDocument bodyParts = ReadOptional<CharacterBodyPartsDocument>(packagePath, Path.Combine("geometry", "body_parts.json"), options);
        if (bodyParts != null && bodyParts.BodyParts != null)
            geometry.BodyParts.AddRange(bodyParts.BodyParts);

        CharacterBodyCollidersDocument colliders = ReadOptional<CharacterBodyCollidersDocument>(packagePath, Path.Combine("geometry", "body_colliders.json"), options);
        if (colliders != null && colliders.Colliders != null)
            geometry.Colliders.AddRange(colliders.Colliders);

        CharacterSocketsDocument sockets = ReadOptional<CharacterSocketsDocument>(packagePath, Path.Combine("geometry", "sockets.json"), options);
        if (sockets != null && sockets.Sockets != null)
            geometry.Sockets.AddRange(sockets.Sockets);

        WeaponAttachmentsDocument attachments = ReadOptional<WeaponAttachmentsDocument>(packagePath, Path.Combine("geometry", "weapon_attachments.json"), options);
        if (attachments != null && attachments.Attachments != null)
            geometry.WeaponAttachments.AddRange(attachments.Attachments);

        WeaponTracesDocument traces = ReadOptional<WeaponTracesDocument>(packagePath, Path.Combine("geometry", "traces.json"), options);
        if (traces != null && traces.Traces != null)
            geometry.Traces.AddRange(traces.Traces);

        return geometry;
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

    private sealed class WeaponTracesDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public List<WeaponTraceProfile> Traces { get; set; } = new();
    }
}
