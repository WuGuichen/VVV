using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static class Program
{
    public const int ExitReady = AuthoringExitCodes.Ready;
    public const int ExitToolError = AuthoringExitCodes.ToolError;
    public const int ExitValidationBlocked = AuthoringExitCodes.ValidationBlocked;
    public const int ExitSchemaIncompatible = AuthoringExitCodes.SchemaIncompatible;
    public const int ExitPreviewUnavailable = AuthoringExitCodes.PreviewUnavailable;
    public const int ExitWarning = AuthoringExitCodes.Warning;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    internal static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new FieldValueJsonConverter());
        return options;
    }

    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintHelp();
            return ExitReady;
        }

        try
        {
            return Dispatch(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return ExitToolError;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return ExitToolError;
        }
    }

    private static int Dispatch(string[] args)
    {
        if (args.Length >= 2 && args[0] == "character")
            return CharacterPackageCommands.Dispatch(args, JsonOptions);

        if (args.Length >= 2 && args[0] == "preview")
            return PreviewCommands.Dispatch(args, JsonOptions);

        if (args.Length >= 2 && args[0] == "manifest" && args[1] == "export")
            return WriteJson(BuiltInContent.CreateProjectManifest());

        if (args.Length >= 2 && args[0] == "editor" && args[1] == "serve")
        {
            string root = GetOption(args, "--root", Directory.GetCurrentDirectory());
            int port = int.Parse(GetOption(args, "--port", "4873"));
            string defaultPackage = GetOption(args, "--package", "Tools/MxFramework.Authoring/samples/buff-preview");
            return EditorServer.Serve(root, port, JsonOptions, defaultPackage);
        }

        if (args.Length >= 2 && args[0] == "manifest" && args[1] == "inspect")
        {
            string manifestPath = RequireOption(args, "--manifest");
            ProjectAuthoringManifest manifest = ManifestReader.Read(manifestPath);
            var summary = new
            {
                manifest.ProjectId,
                manifest.DisplayName,
                manifest.AuthoringVersion,
                manifest.SchemaVersion,
                schemaCount = manifest.Schemas.Count,
                enumCount = manifest.Enums.Count,
                referenceSourceCount = manifest.References.Count,
                workflowCount = manifest.Workflows.Count,
                localizationCount = manifest.Localization.Count
            };
            return WriteJson(summary);
        }

        if (args.Length >= 2 && args[0] == "schema" && args[1] == "export")
            return WriteJson(BuiltInContent.CreateBuffSchema());

        if (args.Length >= 2 && args[0] == "workflow" && args[1] == "list")
            return WriteJson(BuiltInContent.CreateBuiltInWorkflows());

        if (args.Length >= 2 && args[0] == "workflow" && args[1] == "context")
        {
            string workflowId = GetOption(args, "--workflow", "buff.create");
            string stepId = GetOption(args, "--step", string.Empty);
            AuthoringWorkflow workflow = BuiltInContent.GetWorkflow(workflowId);
            if (workflow == null)
            {
                Console.Error.WriteLine("error: workflow '" + workflowId + "' was not found.");
                return ExitToolError;
            }
            Console.Write(workflow.CreateStepContext(stepId));
            return ExitReady;
        }

        if (args.Length >= 2 && args[0] == "workflow" && args[1] == "ai-context")
        {
            string workflowId = RequireOption(args, "--workflow");
            string stepId = GetOption(args, "--step", string.Empty);
            string packagePath = RequireOption(args, "--package");
            string manifestPath = GetOption(args, "--manifest", string.Empty);
            AuthoringWorkflow workflow = BuiltInContent.GetWorkflow(workflowId);
            if (workflow == null)
            {
                Console.Error.WriteLine("error: workflow '" + workflowId + "' was not found.");
                return ExitToolError;
            }

            PackageReadResult package = PackageReader.Read(packagePath);
            ProjectAuthoringManifest manifest = string.IsNullOrEmpty(manifestPath)
                ? TryReadDefaultManifest()
                : ManifestReader.Read(manifestPath);
            Console.Write(workflow.CreateAiStepContext(stepId, manifest, package.Manifest, package.Patches));
            return ExitReady;
        }

        if (args.Length >= 1 && args[0] == "validate")
        {
            string packagePath = RequireOption(args, "--package");
            string manifestPath = GetOption(args, "--manifest", string.Empty);
            PackageReadResult package = PackageReader.Read(packagePath);
            ProjectAuthoringManifest manifest = string.IsNullOrEmpty(manifestPath)
                ? TryReadDefaultManifest()
                : ManifestReader.Read(manifestPath);
            ValidationReport report = AuthoringValidate.Run(manifest, package.Manifest, package.Patches);
            Console.Write(report.ToText());
            return AuthoringExitCodes.From(report);
        }

        if (args.Length >= 1 && args[0] == "merge-preview")
        {
            string packagePath = GetOption(args, "--package", string.Empty);
            string[] basePaths = GetOptionAll(args, "--base");
            string[] patchPaths = GetOptionAll(args, "--patch");

            if (basePaths.Length == 0 && patchPaths.Length == 0)
            {
                if (string.IsNullOrEmpty(packagePath))
                    throw new ArgumentException("--package or --base/--patch is required.");
                PackageReadResult fallbackPkg = PackageReader.Read(packagePath);
                MergePreview fallback = LayeredMerger.Merge(null, null, fallbackPkg.Patches);
                return WriteJson(fallback);
            }

            List<PatchDocument> baseDocs = ReadPatchPaths(basePaths);
            List<PatchDocument> patchDocs = ReadPatchPaths(patchPaths);
            List<PatchDocument> modDocs = string.IsNullOrEmpty(packagePath)
                ? new List<PatchDocument>()
                : PackageReader.Read(packagePath).Patches;
            MergePreview layered = LayeredMerger.Merge(baseDocs, patchDocs, modDocs);
            return WriteJson(layered);
        }

        if (args.Length >= 1 && args[0] == "precommit")
        {
            string packagePath = RequireOption(args, "--package");
            string manifestPath = GetOption(args, "--manifest", string.Empty);
            string outDir = GetOption(args, "--out", Path.Combine("Temp", "MxFrameworkAuthoring", "precommit"));
            PackageReadResult package = PackageReader.Read(packagePath);
            ProjectAuthoringManifest manifest = string.IsNullOrEmpty(manifestPath)
                ? TryReadDefaultManifest()
                : ManifestReader.Read(manifestPath);
            ValidationReport report = AuthoringValidate.Run(manifest, package.Manifest, package.Patches);
            int exit = AuthoringExitCodes.From(report);
            string status = AuthoringPrecommit.Status(exit);
            Directory.CreateDirectory(outDir);
            string outPath = Path.Combine(outDir, "precommit.txt");
            File.WriteAllText(outPath, AuthoringPrecommit.BuildText(packagePath, status, exit, report));
            Console.WriteLine("precommit=" + outPath);
            Console.WriteLine("status=" + status);
            Console.WriteLine("exit=" + exit);
            return exit;
        }

        if (args.Length >= 1 && args[0] == "report")
        {
            string packagePath = RequireOption(args, "--package");
            string outputPath = GetOption(args, "--out", string.Empty);
            string manifestPath = GetOption(args, "--manifest", string.Empty);
            PackageReadResult package = PackageReader.Read(packagePath);
            ProjectAuthoringManifest manifest = string.IsNullOrEmpty(manifestPath)
                ? TryReadDefaultManifest()
                : ManifestReader.Read(manifestPath);
            ValidationReport report = AuthoringValidate.Run(manifest, package.Manifest, package.Patches);
            var bundle = new ReportBundle
            {
                Package = package.Manifest,
                Validation = report,
                MergePreviews = package.Patches.Select(PatchMerger.Merge).ToList()
            };

            if (!string.IsNullOrEmpty(outputPath))
            {
                ReportBundleIndex index = ReportBundleWriter.Write(outputPath, bundle);
                WriteJson(index);
                return AuthoringExitCodes.From(report);
            }

            WriteJson(bundle);
            return AuthoringExitCodes.From(report);
        }

        if (args.Length >= 2 && args[0] == "package" && args[1] == "validate")
        {
            string packagePath = RequireOption(args, "--package");
            string manifestPath = GetOption(args, "--manifest", string.Empty);

            // Read manifest
            string modJsonPath = Path.Combine(packagePath, "mod.json");
            if (!File.Exists(modJsonPath))
            {
                Console.Error.WriteLine("error: mod.json not found at " + modJsonPath);
                return ExitToolError;
            }

            ModPackageManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<ModPackageManifest>(File.ReadAllText(modJsonPath), JsonOptions) ?? new ModPackageManifest();
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine("error: failed to parse mod.json: " + ex.Message);
                return ExitToolError;
            }

            // Run standard validation
            var report = new ValidationReport { PackageId = manifest.PackageId };
            bool hasBlockingError = false;

            void AddError(string code, string message, string field)
            {
                report.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    Code = code,
                    Message = message,
                    Source = manifest.PackageId ?? "",
                    Field = field ?? ""
                });
                hasBlockingError = true;
            }

            // 1. Check schemaVersion
            if (string.IsNullOrWhiteSpace(manifest.SchemaVersion))
            {
                AddError("manifest.schemaVersion", "schemaVersion is required.", "schemaVersion");
            }
            else if (manifest.SchemaVersion != "1")
            {
                AddError("manifest.schemaVersion",
                    "schemaVersion must be '1', but got '" + manifest.SchemaVersion + "'.", "schemaVersion");
            }

            // 2. Check packageId
            if (string.IsNullOrWhiteSpace(manifest.PackageId))
            {
                AddError("manifest.packageId", "packageId is required.", "packageId");
            }

            // 3. Resolve and validate runtimePatch path
            string resolvedPatchPath = null;
            if (!string.IsNullOrWhiteSpace(manifest.RuntimePatch))
            {
                string rawPath = manifest.RuntimePatch.Trim();

                // ❌ Absolute path
                if (Path.IsPathRooted(rawPath))
                {
                    AddError("package.runtimePatch.absolute",
                        "runtimePatch must be a relative path, but got absolute: '" + rawPath + "'.", "runtimePatch");
                }

                // ❌ Path traversal (..)
                string full = Path.GetFullPath(Path.Combine(packagePath, rawPath));
                string normalizedPackage = Path.GetFullPath(packagePath) + Path.DirectorySeparatorChar;
                if (!full.StartsWith(normalizedPackage, StringComparison.Ordinal))
                {
                    AddError("package.runtimePatch.traversal",
                        "runtimePatch '" + rawPath + "' resolves outside package root.", "runtimePatch");
                    full = null;
                }

                // ❌ File does not exist
                if (full != null && !File.Exists(full))
                {
                    AddError("package.runtimePatch.notFound",
                        "Runtime Patch file not found: '" + rawPath + "' (resolved: " + full + ").", "runtimePatch");
                    full = null;
                }

                if (full != null)
                {
                    resolvedPatchPath = full;

                    // Read runtime patch to validate format and layer
                    try
                    {
                        string patchJson = File.ReadAllText(full);
                        using JsonDocument doc = JsonDocument.Parse(patchJson);
                        JsonElement root = doc.RootElement;

                        string actualFormat = root.TryGetProperty("format", out JsonElement fmtEl) ? fmtEl.GetString() ?? "" : "";
                        string actualLayer = root.TryGetProperty("layer", out JsonElement lyrEl) ? lyrEl.GetString() ?? "" : "";

                        // Validate format
                        if (actualFormat != "mx.runtimeConfigPatch.v1")
                        {
                            AddError("package.runtimePatch.format",
                                "Runtime Patch format must be 'mx.runtimeConfigPatch.v1', but got '" + actualFormat + "'.", "runtimePatch");
                        }

                        // Validate kind-layer match
                        if (manifest.Kind == PackageKind.Mod && actualLayer != "Mod")
                        {
                            AddError("package.kindLayerMismatch",
                                "Package kind is 'Mod' but Runtime Patch layer is '" + actualLayer + "'; expected 'Mod'.", "kind/layer");
                        }

                        if (manifest.Kind == PackageKind.Preview && actualLayer != "Patch")
                        {
                            AddError("package.kindLayerMismatch",
                                "Package kind is 'Preview' but Runtime Patch layer is '" + actualLayer + "'; expected 'Patch'.", "kind/layer");
                        }
                    }
                    catch (JsonException ex)
                    {
                        AddError("package.runtimePatch.invalidJson",
                            "Failed to parse Runtime Patch JSON: " + ex.Message, "runtimePatch");
                    }
                }
            }

            // Output summary
            Console.WriteLine("packageId: " + manifest.PackageId);
            Console.WriteLine("kind: " + manifest.Kind);
            Console.WriteLine("version: " + manifest.Version);

            if (resolvedPatchPath != null)
                Console.WriteLine("runtimePatch: " + resolvedPatchPath);
            else if (!string.IsNullOrWhiteSpace(manifest.RuntimePatch))
                Console.WriteLine("runtimePatch: (invalid)");
            else
                Console.WriteLine("runtimePatch: (not specified)");

            Console.WriteLine();
            Console.Write(report.ToText());

            return hasBlockingError ? ExitValidationBlocked : ExitReady;
        }

        if (args.Length >= 2 && args[0] == "mod" && args[1] == "diagnose")
        {
            var diagnoseArgs = new string[args.Length - 2];
            if (args.Length > 2)
                Array.Copy(args, 2, diagnoseArgs, 0, args.Length - 2);
            return ModDiagnoseCommand.Run(diagnoseArgs);
        }

        if (args.Length >= 2 && args[0] == "runtime-patch" && args[1] == "export")
        {
            string packagePath = RequireOption(args, "--package");
            string outputPath = GetOption(args, "--out", string.Empty);
            string sourceId = GetOption(args, "--source-id", "authoring_export");
            string layerStr = GetOption(args, "--layer", "Patch");
            bool force = HasFlag(args, "--force");

            PackageReadResult package = PackageReader.Read(packagePath);
            AuthoringRuntimePatchExporter.ExportResult exportResult =
                AuthoringRuntimePatchExporter.Export(package.Patches, sourceId, layerStr, package.Manifest?.PackageId);

            if (!exportResult.Success)
            {
                Console.Error.WriteLine("error: runtime-patch export failed (" + exportResult.Errors.Count + " errors)");
                for (int i = 0; i < exportResult.Errors.Count; i++)
                {
                    var err = exportResult.Errors[i];
                    Console.Error.WriteLine("  entry=" + err.EntryId + " field=" + err.Field + " " + err.Message);
                }
                return ExitToolError;
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                Console.WriteLine(exportResult.Json);
                return ExitReady;
            }

            // Check if output file exists and prevent overwrite on failure
            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!force && File.Exists(outputPath))
            {
                Console.Error.WriteLine("error: output file already exists: " + outputPath + " (use --force to overwrite)");
                return ExitToolError;
            }

            File.WriteAllText(outputPath, exportResult.Json);
            Console.WriteLine("runtime-patch exported: " + outputPath);
            Console.WriteLine("  format=mx.runtimeConfigPatch.v1");
            return ExitReady;
        }

        PrintHelp();
        return ExitToolError;
    }

    private static ProjectAuthoringManifest TryReadDefaultManifest()
    {
        string repoRoot = Directory.GetCurrentDirectory();
        string candidate = Path.Combine(repoRoot, "Tools", "MxFramework.Authoring", "samples", "project-manifest", "project-authoring-manifest.json");
        if (File.Exists(candidate))
            return ManifestReader.Read(candidate);
        return null;
    }

    private static List<PatchDocument> ReadPatchPaths(string[] paths)
    {
        var result = new List<PatchDocument>();
        for (int i = 0; i < paths.Length; i++)
        {
            string p = paths[i];
            if (string.IsNullOrEmpty(p) || !File.Exists(p)) continue;
            PatchDocument doc = JsonSerializer.Deserialize<PatchDocument>(File.ReadAllText(p), JsonOptions) ?? new PatchDocument();
            for (int j = 0; j < doc.Entries.Count; j++)
            {
                if (string.IsNullOrEmpty(doc.Entries[j].Source))
                    doc.Entries[j].Source = doc.Source;
            }
            result.Add(doc);
        }
        return result;
    }

    private static int WriteJson<T>(T value)
    {
        Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
        return ExitReady;
    }

    internal static string RequireOption(string[] args, string name)
    {
        string value = GetOption(args, name, string.Empty);
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException(name + " is required.");
        return value;
    }

    internal static string GetOption(string[] args, string name, string defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                return args[i + 1];
        }

        return defaultValue;
    }

    internal static bool HasFlag(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == name) return true;
        }
        return false;
    }

    internal static string[] GetOptionAll(string[] args, string name)
    {
        var list = new List<string>();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                list.Add(args[i + 1]);
        }
        return list.ToArray();
    }

    private static void PrintHelp()
    {
        Console.WriteLine("MxFramework Authoring CLI");
        Console.WriteLine("Commands:");
        Console.WriteLine("  editor serve [--root <repoRoot>] [--port <port>] [--package <relative>]");
        Console.WriteLine("  character inspect --package <path>");
        Console.WriteLine("  character validate --package <path>");
        Console.WriteLine("  character compile --package <path> [--out <dir>] [--check-files] [--check-hashes] [--strict]");
        Console.WriteLine("  character schema");
        Console.WriteLine("  package validate --package <path>");
        Console.WriteLine("  manifest export");
        Console.WriteLine("  manifest inspect --manifest <path>");
        Console.WriteLine("  schema export");
        Console.WriteLine("  workflow list");
        Console.WriteLine("  workflow context --workflow <id> --step <id>");
        Console.WriteLine("  workflow ai-context --workflow <id> --step <id> --package <path> [--manifest <path>]");
        Console.WriteLine("  validate --package <path> [--manifest <path>]");
        Console.WriteLine("  merge-preview --package <path> [--base <patch>]... [--patch <patch>]...");
        Console.WriteLine("  report --package <path> [--out <dir>] [--manifest <path>]");
        Console.WriteLine("  precommit --package <path> [--manifest <path>] [--out <dir>]");
        Console.WriteLine("  preview ping");
        Console.WriteLine("  preview load --package <path> [--manifest <path>]");
        Console.WriteLine("  preview apply --buff <id> [--target <id>] [--caster <id>] [--stack 1] [--wait-ticks 60]");
        Console.WriteLine("  preview reset");
        Console.WriteLine("  preview snapshot --target <id>");
        Console.WriteLine("  preview logs [--after <seq>] [--max 200]");
        Console.WriteLine("  mod diagnose [-c <container>]... [--containers <csv>] [--loadout <path>] [--output <path>] [--pretty] [--include-absolute-paths] [--fail-on-warning]");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0 = ready");
        Console.WriteLine("  1 = tool error (missing args, IO failure)");
        Console.WriteLine("  2 = validation blocked (errors present)");
        Console.WriteLine("  3 = schemaVersion incompatible (requires upgrade)");
        Console.WriteLine("  4 = preview unavailable (no descriptor / handshake / token mismatch)");
        Console.WriteLine("  5 = warning (diagnose completed with warnings; use --fail-on-warning)");
    }
}
