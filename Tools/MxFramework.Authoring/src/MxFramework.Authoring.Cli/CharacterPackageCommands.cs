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
