using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static class AbilityPackageCommands
{
    public static int Dispatch(string[] args, JsonSerializerOptions options)
    {
        if (args.Length >= 2 && args[1] == "validate")
        {
            string packagePath = Program.RequireOption(args, "--package");
            AbilityAuthoringDocument document = ReadDocument(packagePath, options);
            AbilityAuthoringValidationReport report = AbilityAuthoringCompiler.Validate(
                document,
                ReadResolutionVfxDefinitions(Program.GetOption(args, "--vfx-definitions", string.Empty), options));
            Console.Write(report.ToText());
            return report.HasBlockingIssues ? Program.ExitValidationBlocked : Program.ExitReady;
        }

        if (args.Length >= 2 && args[1] == "compile")
        {
            string packagePath = Program.RequireOption(args, "--package");
            string outDir = Program.GetOption(args, "--out", string.Empty);
            AbilityAuthoringCompileResult result = AbilityAuthoringCompiler.Compile(
                ReadDocument(packagePath, options),
                ReadResolutionVfxDefinitions(Program.GetOption(args, "--vfx-definitions", string.Empty), options));

            if (!string.IsNullOrWhiteSpace(outDir))
            {
                WriteCompileOutputs(outDir, result, options);
                Console.WriteLine("abilityCompileResult=" + Path.Combine(outDir, "ability_compile_result.json"));
                Console.WriteLine("abilityCatalog=" + Path.Combine(outDir, "ability_catalog.json"));
                Console.WriteLine("abilityCatalogBinary=" + Path.Combine(outDir, "ability_catalog.bytes"));
                Console.WriteLine("vfxDefinitions=" + Path.Combine(outDir, "vfx_definitions.json"));
                Console.WriteLine("vfxDefinitionsBinary=" + Path.Combine(outDir, "vfx_definitions.bytes"));
                Console.WriteLine("vfxTriggerMaps=" + Path.Combine(outDir, "vfx_trigger_maps.json"));
                Console.WriteLine("vfxTriggerMapsBinary=" + Path.Combine(outDir, "vfx_trigger_maps.bytes"));
                Console.WriteLine("actionReferences=" + Path.Combine(outDir, "ability_action_references.json"));
                Console.WriteLine("effectTimelines=" + Path.Combine(outDir, "ability_effect_timelines.json"));
                Console.WriteLine("effectTimelinesBinary=" + Path.Combine(outDir, "ability_effect_timelines.bytes"));
                Console.WriteLine("projectileDefinitions=" + Path.Combine(outDir, "projectile_definitions.json"));
                Console.WriteLine("projectileDefinitionsBinary=" + Path.Combine(outDir, "projectile_definitions.bytes"));
                Console.WriteLine("projectileSpawnProfiles=" + Path.Combine(outDir, "projectile_spawn_profiles.json"));
                Console.WriteLine("projectileSpawnProfilesBinary=" + Path.Combine(outDir, "projectile_spawn_profiles.bytes"));
                Console.WriteLine("projectilePresentationBindings=" + Path.Combine(outDir, "projectile_presentation_bindings.json"));
                Console.WriteLine("projectilePresentationBindingsBinary=" + Path.Combine(outDir, "projectile_presentation_bindings.bytes"));
                Console.WriteLine("status=" + result.ValidationReport.Status);
            }
            else
            {
                Console.WriteLine(JsonSerializer.Serialize(result, options));
            }

            return result.ValidationReport.HasBlockingIssues
                ? Program.ExitValidationBlocked
                : Program.ExitReady;
        }

        if (args.Length >= 2 && args[1] == "import-unity")
        {
            return ImportUnity(args, options);
        }

        if (args.Length >= 2 && args[1] == "export-runtime-binaries")
        {
            return ExportRuntimeBinaries(args, options);
        }

        Console.Error.WriteLine("error: unknown ability command.");
        return Program.ExitToolError;
    }

    internal static void WriteCompileOutputs(string outDir, AbilityAuthoringCompileResult result, JsonSerializerOptions options)
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "ability_compile_result.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result, options)));
        File.WriteAllText(Path.Combine(outDir, "ability_catalog.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.AbilityCatalog, options)));
        File.WriteAllBytes(Path.Combine(outDir, "ability_catalog.bytes"), RuntimeCatalogBinaryArtifacts.WriteAbilityDefinitions(result.AbilityCatalog));
        File.WriteAllText(Path.Combine(outDir, "vfx_definitions.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.VfxDefinitionCatalog, options)));
        File.WriteAllBytes(Path.Combine(outDir, "vfx_definitions.bytes"), RuntimeCatalogBinaryArtifacts.WriteVfxDefinitions(result.VfxDefinitionCatalog));
        File.WriteAllText(Path.Combine(outDir, "vfx_trigger_maps.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.VfxTriggerMap, options)));
        File.WriteAllBytes(Path.Combine(outDir, "vfx_trigger_maps.bytes"), RuntimeCatalogBinaryArtifacts.WriteVfxTriggerMaps(result.VfxTriggerMap));
        File.WriteAllText(Path.Combine(outDir, "ability_action_references.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.ActionReferenceCatalog, options)));
        File.WriteAllText(Path.Combine(outDir, "ability_effect_timelines.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.EffectTimelineCatalog, options)));
        File.WriteAllBytes(Path.Combine(outDir, "ability_effect_timelines.bytes"), RuntimeCatalogBinaryArtifacts.WriteAbilityEffectTimelines(result.EffectTimelineCatalog));
        File.WriteAllText(Path.Combine(outDir, "projectile_definitions.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.ProjectileDefinitionCatalog, options)));
        File.WriteAllBytes(Path.Combine(outDir, "projectile_definitions.bytes"), RuntimeCatalogBinaryArtifacts.WriteProjectileDefinitions(result.ProjectileDefinitionCatalog));
        File.WriteAllText(Path.Combine(outDir, "projectile_spawn_profiles.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.ProjectileSpawnProfileCatalog, options)));
        File.WriteAllBytes(Path.Combine(outDir, "projectile_spawn_profiles.bytes"), RuntimeCatalogBinaryArtifacts.WriteProjectileSpawnProfiles(result.ProjectileSpawnProfileCatalog));
        File.WriteAllText(Path.Combine(outDir, "projectile_presentation_bindings.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.ProjectilePresentationBindings, options)));
        File.WriteAllBytes(Path.Combine(outDir, "projectile_presentation_bindings.bytes"), RuntimeCatalogBinaryArtifacts.WriteProjectilePresentationBindings(result.ProjectilePresentationBindings));
        File.WriteAllText(Path.Combine(outDir, "character_test_mechanic_tunings.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.CharacterTestMechanicTuningCatalog, options)));
    }

    private static int ImportUnity(string[] args, JsonSerializerOptions options)
    {
        string packagePath = Program.RequireOption(args, "--package");
        string projectRoot = Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory());
        string abilityRoot = Program.GetOption(args, "--ability-root", "Assets/MxFrameworkGenerated/Abilities");
        string vfxRoot = Program.GetOption(args, "--vfx-root", "Assets/MxFrameworkGenerated/Vfx");
        string projectileRoot = Program.GetOption(args, "--projectile-root", "Assets/MxFrameworkGenerated/Combat/Projectiles");
        string reportOut = Program.GetOption(args, "--report-out", string.Empty);
        bool dryRun = Program.HasFlag(args, "--dry-run");

        string vfxDefinitionsPath = Program.GetOption(
            args,
            "--vfx-definitions",
            Path.Combine(projectRoot, CombineProjectPath(vfxRoot, "vfx_definitions.json")));
        AbilityVfxDefinitionCatalogDocument existingVfxDefinitions =
            ReadVfxDefinitionCatalog(vfxDefinitionsPath, options);
        AbilityAuthoringCompileResult result = AbilityAuthoringCompiler.Compile(
            ReadDocument(packagePath, options),
            existingVfxDefinitions.Definitions);
        AbilityVfxDefinitionCatalogDocument mergedVfxDefinitions = MergeVfxDefinitions(
            result.VfxDefinitionCatalog,
            existingVfxDefinitions);
        var writes = new List<AbilityUnityImportWrite>
        {
            new AbilityUnityImportWrite(CombineProjectPath(abilityRoot, "ability_catalog.json"), JsonSerializer.Serialize(result.AbilityCatalog, options)),
            new AbilityUnityImportWrite(CombineProjectPath(abilityRoot, "ability_catalog.bytes"), RuntimeCatalogBinaryArtifacts.WriteAbilityDefinitions(result.AbilityCatalog)),
            new AbilityUnityImportWrite(CombineProjectPath(abilityRoot, "ability_action_references.json"), JsonSerializer.Serialize(result.ActionReferenceCatalog, options)),
            new AbilityUnityImportWrite(CombineProjectPath(abilityRoot, "ability_effect_timelines.json"), JsonSerializer.Serialize(result.EffectTimelineCatalog, options)),
            new AbilityUnityImportWrite(CombineProjectPath(abilityRoot, "ability_effect_timelines.bytes"), RuntimeCatalogBinaryArtifacts.WriteAbilityEffectTimelines(result.EffectTimelineCatalog)),
            new AbilityUnityImportWrite(CombineProjectPath(abilityRoot, "character_test_mechanic_tunings.json"), JsonSerializer.Serialize(result.CharacterTestMechanicTuningCatalog, options)),
            new AbilityUnityImportWrite(CombineProjectPath(vfxRoot, "vfx_definitions.json"), JsonSerializer.Serialize(mergedVfxDefinitions, options)),
            new AbilityUnityImportWrite(CombineProjectPath(vfxRoot, "vfx_definitions.bytes"), RuntimeCatalogBinaryArtifacts.WriteVfxDefinitions(mergedVfxDefinitions)),
            new AbilityUnityImportWrite(CombineProjectPath(vfxRoot, "vfx_trigger_maps.json"), JsonSerializer.Serialize(result.VfxTriggerMap, options)),
            new AbilityUnityImportWrite(CombineProjectPath(vfxRoot, "vfx_trigger_maps.bytes"), RuntimeCatalogBinaryArtifacts.WriteVfxTriggerMaps(result.VfxTriggerMap)),
            new AbilityUnityImportWrite(CombineProjectPath(projectileRoot, "projectile_definitions.json"), JsonSerializer.Serialize(result.ProjectileDefinitionCatalog, options)),
            new AbilityUnityImportWrite(CombineProjectPath(projectileRoot, "projectile_definitions.bytes"), RuntimeCatalogBinaryArtifacts.WriteProjectileDefinitions(result.ProjectileDefinitionCatalog)),
            new AbilityUnityImportWrite(CombineProjectPath(projectileRoot, "projectile_spawn_profiles.json"), JsonSerializer.Serialize(result.ProjectileSpawnProfileCatalog, options)),
            new AbilityUnityImportWrite(CombineProjectPath(projectileRoot, "projectile_spawn_profiles.bytes"), RuntimeCatalogBinaryArtifacts.WriteProjectileSpawnProfiles(result.ProjectileSpawnProfileCatalog)),
            new AbilityUnityImportWrite(CombineProjectPath(projectileRoot, "projectile_presentation_bindings.json"), JsonSerializer.Serialize(result.ProjectilePresentationBindings, options)),
            new AbilityUnityImportWrite(CombineProjectPath(projectileRoot, "projectile_presentation_bindings.bytes"), RuntimeCatalogBinaryArtifacts.WriteProjectilePresentationBindings(result.ProjectilePresentationBindings))
        };

        if (!dryRun && !result.ValidationReport.HasBlockingIssues)
        {
            for (int i = 0; i < writes.Count; i++)
            {
                if (writes[i].IsBinary)
                    WriteProjectBytes(projectRoot, writes[i].RelativePath, writes[i].Bytes);
                else
                    WriteProjectText(projectRoot, writes[i].RelativePath, writes[i].Content);
            }
        }

        var report = new
        {
            packageId = result.PackageId,
            status = result.ValidationReport.Status,
            dryRun,
            validationBlocked = result.ValidationReport.HasBlockingIssues,
            writes = writes.Select(write => write.RelativePath).ToArray()
        };
        string reportJson = JsonSerializer.Serialize(report, options);
        if (!string.IsNullOrWhiteSpace(reportOut))
        {
            Directory.CreateDirectory(reportOut);
            File.WriteAllText(Path.Combine(reportOut, "ability_import_report.json"), EnsureTrailingNewline(reportJson));
        }

        Console.WriteLine(reportJson);
        return result.ValidationReport.HasBlockingIssues ? Program.ExitValidationBlocked : Program.ExitReady;
    }

    private static int ExportRuntimeBinaries(string[] args, JsonSerializerOptions options)
    {
        string projectRoot = Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory());
        string abilityRoot = Program.GetOption(args, "--ability-root", "Assets/MxFrameworkGenerated/Abilities");
        string vfxRoot = Program.GetOption(args, "--vfx-root", "Assets/MxFrameworkGenerated/Vfx");
        string projectileRoot = Program.GetOption(args, "--projectile-root", "Assets/MxFrameworkGenerated/Combat/Projectiles");
        string reportOut = Program.GetOption(args, "--report-out", string.Empty);
        bool dryRun = Program.HasFlag(args, "--dry-run");

        var writes = new List<AbilityUnityImportWrite>
        {
            new AbilityUnityImportWrite(
                CombineProjectPath(abilityRoot, "ability_catalog.bytes"),
                RuntimeCatalogBinaryArtifacts.WriteAbilityDefinitions(
                    ReadProjectJson<AbilityRuntimeCatalogDocument>(projectRoot, CombineProjectPath(abilityRoot, "ability_catalog.json"), options))),
            new AbilityUnityImportWrite(
                CombineProjectPath(abilityRoot, "ability_effect_timelines.bytes"),
                RuntimeCatalogBinaryArtifacts.WriteAbilityEffectTimelines(
                    ReadProjectJson<AbilityEffectTimelineCatalogDocument>(projectRoot, CombineProjectPath(abilityRoot, "ability_effect_timelines.json"), options))),
            new AbilityUnityImportWrite(
                CombineProjectPath(vfxRoot, "vfx_definitions.bytes"),
                RuntimeCatalogBinaryArtifacts.WriteVfxDefinitions(
                    ReadProjectJson<AbilityVfxDefinitionCatalogDocument>(projectRoot, CombineProjectPath(vfxRoot, "vfx_definitions.json"), options))),
            new AbilityUnityImportWrite(
                CombineProjectPath(vfxRoot, "vfx_trigger_maps.bytes"),
                RuntimeCatalogBinaryArtifacts.WriteVfxTriggerMaps(
                    ReadProjectJson<AbilityVfxTriggerMapDocument>(projectRoot, CombineProjectPath(vfxRoot, "vfx_trigger_maps.json"), options))),
            new AbilityUnityImportWrite(
                CombineProjectPath(projectileRoot, "projectile_definitions.bytes"),
                RuntimeCatalogBinaryArtifacts.WriteProjectileDefinitions(
                    ReadProjectJson<AbilityProjectileDefinitionCatalogDocument>(projectRoot, CombineProjectPath(projectileRoot, "projectile_definitions.json"), options))),
            new AbilityUnityImportWrite(
                CombineProjectPath(projectileRoot, "projectile_spawn_profiles.bytes"),
                RuntimeCatalogBinaryArtifacts.WriteProjectileSpawnProfiles(
                    ReadProjectJson<AbilityProjectileSpawnProfileCatalogDocument>(projectRoot, CombineProjectPath(projectileRoot, "projectile_spawn_profiles.json"), options))),
            new AbilityUnityImportWrite(
                CombineProjectPath(projectileRoot, "projectile_presentation_bindings.bytes"),
                RuntimeCatalogBinaryArtifacts.WriteProjectilePresentationBindings(
                    ReadProjectJson<AbilityProjectilePresentationBindingDocument>(projectRoot, CombineProjectPath(projectileRoot, "projectile_presentation_bindings.json"), options)))
        };

        if (!dryRun)
        {
            for (int i = 0; i < writes.Count; i++)
                WriteProjectBytes(projectRoot, writes[i].RelativePath, writes[i].Bytes);
        }

        var report = new
        {
            dryRun,
            writes = writes.Select(write => write.RelativePath).ToArray()
        };
        string reportJson = JsonSerializer.Serialize(report, options);
        if (!string.IsNullOrWhiteSpace(reportOut))
        {
            Directory.CreateDirectory(reportOut);
            File.WriteAllText(Path.Combine(reportOut, "ability_runtime_binary_export_report.json"), EnsureTrailingNewline(reportJson));
        }

        Console.WriteLine(reportJson);
        return Program.ExitReady;
    }

    private static void WriteProjectText(string projectRoot, string relativePath, string content)
    {
        string path = Path.Combine(projectRoot ?? string.Empty, relativePath);
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, EnsureTrailingNewline(content));
    }

    private static void WriteProjectBytes(string projectRoot, string relativePath, byte[] content)
    {
        string path = Path.Combine(projectRoot ?? string.Empty, relativePath);
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, content ?? Array.Empty<byte>());
    }

    private static AbilityAuthoringDocument ReadDocument(string packageOrDocumentPath, JsonSerializerOptions options)
    {
        string documentPath = AbilityAuthoringCompiler.ResolveDocumentPath(packageOrDocumentPath);
        if (!File.Exists(documentPath))
            throw new FileNotFoundException("Ability authoring document was not found.", documentPath);

        return AbilityAuthoringPackageLoader.Load(packageOrDocumentPath, options);
    }

    private static IReadOnlyList<VfxDefinitionConfig> ReadResolutionVfxDefinitions(
        string path,
        JsonSerializerOptions options) =>
        string.IsNullOrWhiteSpace(path)
            ? null
            : ReadVfxDefinitionCatalog(path, options).Definitions;

    private static AbilityVfxDefinitionCatalogDocument ReadVfxDefinitionCatalog(
        string path,
        JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new AbilityVfxDefinitionCatalogDocument();
        AbilityVfxDefinitionCatalogDocument catalog = JsonSerializer.Deserialize<AbilityVfxDefinitionCatalogDocument>(
            File.ReadAllText(path),
            options);
        return catalog ?? new AbilityVfxDefinitionCatalogDocument();
    }

    private static AbilityVfxDefinitionCatalogDocument MergeVfxDefinitions(
        AbilityVfxDefinitionCatalogDocument packageCatalog,
        AbilityVfxDefinitionCatalogDocument existingCatalog)
    {
        var merged = new Dictionary<int, VfxDefinitionConfig>();
        for (int i = 0; existingCatalog?.Definitions != null && i < existingCatalog.Definitions.Count; i++)
        {
            VfxDefinitionConfig definition = existingCatalog.Definitions[i];
            if (definition != null)
                merged[definition.DefinitionId] = definition;
        }
        for (int i = 0; packageCatalog?.Definitions != null && i < packageCatalog.Definitions.Count; i++)
        {
            VfxDefinitionConfig definition = packageCatalog.Definitions[i];
            if (definition != null)
                merged[definition.DefinitionId] = definition;
        }
        return new AbilityVfxDefinitionCatalogDocument
        {
            SchemaVersion = packageCatalog?.SchemaVersion ?? existingCatalog?.SchemaVersion ?? "1",
            PackageId = packageCatalog?.PackageId ?? existingCatalog?.PackageId ?? string.Empty,
            Definitions = merged.Values.OrderBy(value => value.DefinitionId).ToList()
        };
    }

    private static TDocument ReadProjectJson<TDocument>(
        string projectRoot,
        string relativePath,
        JsonSerializerOptions options)
        where TDocument : new()
    {
        string path = Path.Combine(projectRoot ?? string.Empty, relativePath ?? string.Empty);
        if (!File.Exists(path))
            return new TDocument();

        return JsonSerializer.Deserialize<TDocument>(File.ReadAllText(path), options) ?? new TDocument();
    }

    private static string CombineProjectPath(string root, string fileName)
    {
        return Path.Combine(root ?? string.Empty, fileName).Replace('\\', '/');
    }

    private static string EnsureTrailingNewline(string value)
    {
        value ??= string.Empty;
        return value.EndsWith("\n", StringComparison.Ordinal) ? value : value + "\n";
    }

    private readonly struct AbilityUnityImportWrite
    {
        public AbilityUnityImportWrite(string relativePath, string content)
        {
            RelativePath = relativePath ?? string.Empty;
            Content = content ?? string.Empty;
            Bytes = Array.Empty<byte>();
            IsBinary = false;
        }

        public AbilityUnityImportWrite(string relativePath, byte[] bytes)
        {
            RelativePath = relativePath ?? string.Empty;
            Content = string.Empty;
            Bytes = bytes ?? Array.Empty<byte>();
            IsBinary = true;
        }

        public string RelativePath { get; }
        public string Content { get; }
        public byte[] Bytes { get; }
        public bool IsBinary { get; }
    }
}
