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
        if (args.Length >= 2 && args[0] == "tactical-card")
            return TacticalCardQueryCommands.Dispatch(args, JsonOptions);

        if (args.Length >= 2 && args[0] == "combat-design")
            return CombatDesignCommands.Dispatch(args, JsonOptions);

        if (args.Length >= 2 && args[0] == "character")
            return CharacterPackageCommands.Dispatch(args, JsonOptions);

        if (args.Length >= 2 && args[0] == "animation")
            return AnimationPackageCommands.Dispatch(args, JsonOptions);

        if (args.Length >= 2 && args[0] == "ability")
            return AbilityPackageCommands.Dispatch(args, JsonOptions);

        if (args.Length >= 2 && args[0] == "projectile")
            return ProjectilePackageCommands.Dispatch(args, JsonOptions);

        if (args.Length >= 2 && args[0] == "buff")
            return BuffPackageCommands.Dispatch(args, JsonOptions);

        if (args.Length >= 2 && args[0] == "vfx")
            return VfxAuthoringCommands.Dispatch(args, JsonOptions);

        if (args.Length >= 2 && args[0] == "ui-composer")
            return UiComposerPublishAllCommands.Dispatch(args, JsonOptions);

        if (args.Length >= 2 && args[0] == "resource-mapping" && args[1] == "sync-character")
            return ResourceMappingCharacterCoordinator.SyncCommand(args, JsonOptions);

        if (args.Length >= 2 && args[0] == "resource-mapping" && args[1] == "sync-first-party")
            return ResourceMappingFirstPartyCoordinator.SyncCommand(args, JsonOptions);

        if (args.Length >= 2 && args[0] == "editor" && args[1] == "serve")
        {
            string root = GetOption(args, "--root", Directory.GetCurrentDirectory());
            string toolsRoot = GetOption(args, "--tools-root", root);
            string projectRoot = GetOption(args, "--project-root", root);
            int port = int.Parse(GetOption(args, "--port", "4873"));
            string defaultPackage = GetOption(args, "--package", "Authoring/CharacterPackages/character-000001");
            return EditorServer.Serve(toolsRoot, projectRoot, port, JsonOptions, defaultPackage);
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

        PrintHelp();
        return ExitToolError;
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
        Console.WriteLine("  editor serve [--root <repoRoot>] [--tools-root <frameworkRoot>] [--project-root <unityProjectRoot>] [--port <port>] [--package <relative>]");
        Console.WriteLine("  ui-composer publish-all --project-root <repoRoot> --screen <screenId> [--fast | --verify-unity | --verify-interaction --interaction-profile runtime-ai-lab] [--json] [--keep-editor]");
        Console.WriteLine("  character inspect --package <path>");
        Console.WriteLine("  character validate --package <path>");
        Console.WriteLine("  character resources --package <path>");
        Console.WriteLine("  character resources plan --package <path> [--out <dir>] [--check-files] [--check-hashes]");
        Console.WriteLine("  character compile --package <path> [--out <dir>] [--check-files] [--check-hashes] [--strict]");
        Console.WriteLine("  character body-part-damage <read|validate|save> --package <path> [--input <json>] [--expected-revision <hash>]");
        Console.WriteLine("  character import-unity --package <path> --project-root <repoRoot> [--unity-root <Assets/...>] [--check-files] [--check-hashes]");
        Console.WriteLine("  character schema");
        Console.WriteLine("  animation compile --package <path> [--out <dir>]");
        Console.WriteLine("  ability validate --package <path> [--vfx-definitions <vfx_definitions.json>]");
        Console.WriteLine("  ability compile --package <path> [--vfx-definitions <vfx_definitions.json>] [--out <dir>]");
        Console.WriteLine("  ability import-unity --package <path> --project-root <repoRoot> [--dry-run]");
        Console.WriteLine("  projectile validate --package <path> [--report-out <dir>]");
        Console.WriteLine("  projectile compile --package <path> --out <dir>");
        Console.WriteLine("  projectile workspace --package <path> [--out <workspace.json>]");
        Console.WriteLine("  projectile mutate --package <path> --request <mutation.json> --draft-out <draft.json> [--generated-root <dir>] [--report-out <dir>]");
        Console.WriteLine("  projectile apply --package <path> --draft <draft.json> --out <generated-dir> [--receipt-out <dir>]");
        Console.WriteLine("  projectile import-unity --package <path> --project-root <repoRoot> [--projectile-root <Assets/...>] [--report-out <dir>] [--dry-run]");
        Console.WriteLine("  buff validate --package <path>");
        Console.WriteLine("  buff compile --package <path> [--out <dir>]");
        Console.WriteLine("  buff import-unity --package <path> --project-root <repoRoot> [--dry-run]");
        Console.WriteLine("  resource-mapping sync-character --package <path> [--root <repoRoot>] [--unity-root <Assets/...>] [--build-target <target>]");
        Console.WriteLine("  resource-mapping sync-first-party [--root <repoRoot>] [--build-target <target>] [--producer <producerId>]");
        Console.WriteLine("  vfx validate [--project-root <repoRoot>] [--catalog <path>] [--json-report <path>] [--markdown-report <path>] [--fail-on-warning]");
        Console.WriteLine("  vfx propose-coverage --project-root <repoRoot> --output <catalog.json> [--enable-strict]");
        Console.WriteLine("  vfx search --project-root <repoRoot> [--text <query>] [--role <role>] [--theme <theme>] [--anchor <anchor>] [--backend <backend>] [--review <status>] [--limit <n>]");
        Console.WriteLine("  vfx impact --project-root <repoRoot> --id <effect|recipe|primitive id>");
        Console.WriteLine("  vfx plan-save --project-root <repoRoot> --proposed <catalog.json> --expected-hash <sha256> [--catalog <path>]");
        Console.WriteLine("  vfx plan-bake --project-root <repoRoot> --effect <effectId> [--catalog <path>]");
        Console.WriteLine("  vfx plan-create --project-root <repoRoot> --brief <brief.json> [--templates <templates.json>] [--capabilities <repo-external-capabilities.json>] [--output <plan.json>]");
        Console.WriteLine("  vfx source scan --project-root <repoRoot> [--index <index.json>] [--output <report.json>]");
        Console.WriteLine("  vfx source search --project-root <repoRoot> [--index <index.json>] [--text <query>] [--package <id>] [--family <family>] [--role <role>] [--theme <theme>] [--anchor <anchor>] [--shape <shape>] [--quality <tier>] [--backend <backend>] [--max-particles <n>] [--limit <n>]");
        Console.WriteLine("  vfx source plan-extract --project-root <repoRoot> --request <request.json> [--index <index.json>] [--output <plan.json>]");
        Console.WriteLine("  vfx source validate-extraction --project-root <repoRoot> --plan <plan.json> --receipt <receipt.json> [--index <index.json>] [--output <report.json>]");
        Console.WriteLine("  vfx source plan-compose --recipe <id> --name <name> --output-prefab <Assets/...prefab> --expected-hash <sha256> --primitives <id,id,...> [--output <plan.json>]");
        Console.WriteLine("  vfx agent-task create --project-root <repoRoot> --task-id <id> --brief <brief.json> [--templates <templates.json>]");
        Console.WriteLine("  vfx agent-task validate --project-root <repoRoot> --task <task.json> [--progress <progress.json>] [--templates <templates.json>] [--output <report.json>]");
        Console.WriteLine("  vfx agent-task request-cancel --task <task.json> --progress <progress.json>");
        Console.WriteLine("  vfx agent-task set-progress --task <task.json> --progress <progress.json> --state <state> [--agent-id <id>] [--candidate <id>] [--completed <0..6>] [--message <text>]");
        Console.WriteLine("  vfx agent-task seal-receipt --project-root <repoRoot> --task <task.json> --receipt <receipt.json> [--templates <templates.json>]");
        Console.WriteLine("  vfx agent-task validate-receipt --project-root <repoRoot> --task <task.json> --receipt <receipt.json> [--templates <templates.json>] [--skip-file-check] [--output <report.json>]");
        Console.WriteLine("  vfx validate-receipt --project-root <repoRoot> --plan <plan.json> --receipt <receipt.json> [--templates <templates.json>] [--skip-file-check] [--output <report.json>]");
        Console.WriteLine("  vfx evaluate-candidates --project-root <repoRoot> --plan <plan.json> --receipt <receipt.json> --input <evaluation-input.json> [--templates <templates.json>] [--output <report.json>]");
        Console.WriteLine("  vfx plan-promote --project-root <repoRoot> --plan <generation-plan.json> --receipt <candidate-receipt.json> --evaluation <evaluation-report.json> --request <promotion-request.json> [--templates <templates.json>] [--output <plan.json>]");
        Console.WriteLine("  vfx plan-register-published --project-root <repoRoot> --request <registration-request.json> --promotion-plan <promotion-plan.json> --promotion-receipt <promotion-receipt.json> [--output <plan.json>]");
        Console.WriteLine("  package validate --package <path>");
        Console.WriteLine("  mod diagnose [-c <container>]... [--containers <csv>] [--loadout <path>] [--output <path>] [--pretty] [--include-absolute-paths] [--fail-on-warning]");
        Console.WriteLine();
        Console.WriteLine("  tactical-card query --name|--contains|--id|--instance-id <value> [--project-root <root>] [--layer disk|generated|live] [--locale zh-CN] [--save-json <decoded.json>]");
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0 = ready");
        Console.WriteLine("  1 = tool error (missing args, IO failure)");
        Console.WriteLine("  2 = validation blocked (errors present)");
        Console.WriteLine("  3 = schemaVersion incompatible (requires upgrade)");
        Console.WriteLine("  5 = warning (diagnose completed with warnings; use --fail-on-warning)");
    }
}
