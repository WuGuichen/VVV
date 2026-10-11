using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static class BuffPackageCommands
{
    public static int Dispatch(string[] args, JsonSerializerOptions options)
    {
        if (args.Length >= 2 && args[1] == "validate")
        {
            string packagePath = Program.RequireOption(args, "--package");
            BuffAuthoringDocument document = ReadDocument(packagePath, options);
            BuffAuthoringValidationReport report = BuffAuthoringCompiler.Validate(document);
            Console.Write(report.ToText());
            return report.HasBlockingIssues ? Program.ExitValidationBlocked : Program.ExitReady;
        }

        if (args.Length >= 2 && args[1] == "compile")
        {
            string packagePath = Program.RequireOption(args, "--package");
            string outDir = Program.GetOption(args, "--out", string.Empty);
            BuffAuthoringCompileResult result = BuffAuthoringCompiler.Compile(ReadDocument(packagePath, options));

            if (!string.IsNullOrWhiteSpace(outDir))
            {
                WriteCompileOutputs(outDir, result, options);
                Console.WriteLine("buffCompileResult=" + Path.Combine(outDir, "buff_compile_result.json"));
                Console.WriteLine("buffDefinitions=" + Path.Combine(outDir, "buff_definitions.json"));
                Console.WriteLine("buffDefinitionsBinary=" + Path.Combine(outDir, "buff_definitions.bytes"));
                Console.WriteLine("auraBuffDefinitions=" + Path.Combine(outDir, "aura_buff_definitions.json"));
                Console.WriteLine("auraBuffDefinitionsBinary=" + Path.Combine(outDir, "aura_buff_definitions.bytes"));
                Console.WriteLine("buffPresentationDefinitions=" + Path.Combine(outDir, "buff_presentation_definitions.json"));
                Console.WriteLine("buffPresentationDefinitionsBinary=" + Path.Combine(outDir, "buff_presentation_definitions.bytes"));
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
            return ImportUnity(args, options);

        if (args.Length >= 2 && args[1] == "export-runtime-binaries")
            return ExportRuntimeBinaries(args, options);

        Console.Error.WriteLine("error: unknown buff command.");
        return Program.ExitToolError;
    }

    internal static void WriteCompileOutputs(string outDir, BuffAuthoringCompileResult result, JsonSerializerOptions options)
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "buff_compile_result.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result, options)));
        File.WriteAllText(Path.Combine(outDir, "buff_definitions.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.BuffDefinitions, options)));
        File.WriteAllBytes(Path.Combine(outDir, "buff_definitions.bytes"), RuntimeCatalogBinaryArtifacts.WriteBuffDefinitions(result.BuffDefinitions));
        File.WriteAllText(Path.Combine(outDir, "aura_buff_definitions.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.AuraBuffDefinitions, options)));
        File.WriteAllBytes(Path.Combine(outDir, "aura_buff_definitions.bytes"), RuntimeCatalogBinaryArtifacts.WriteAuraBuffDefinitions(result.AuraBuffDefinitions));
        File.WriteAllText(Path.Combine(outDir, "buff_presentation_definitions.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.BuffPresentationDefinitions, options)));
        File.WriteAllBytes(Path.Combine(outDir, "buff_presentation_definitions.bytes"), RuntimeCatalogBinaryArtifacts.WriteBuffPresentations(result.BuffPresentationDefinitions));
    }

    private static int ImportUnity(string[] args, JsonSerializerOptions options)
    {
        string packagePath = Program.RequireOption(args, "--package");
        string projectRoot = Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory());
        string buffRoot = Program.GetOption(args, "--buff-root", "Assets/MxFrameworkGenerated/Gameplay/Buffs");
        string auraRoot = Program.GetOption(args, "--aura-root", "Assets/MxFrameworkGenerated/Combat/Auras");
        string reportOut = Program.GetOption(args, "--report-out", string.Empty);
        bool dryRun = Program.HasFlag(args, "--dry-run");

        BuffAuthoringCompileResult result = BuffAuthoringCompiler.Compile(ReadDocument(packagePath, options));
        var writes = new List<BuffUnityImportWrite>
        {
            new BuffUnityImportWrite(CombineProjectPath(buffRoot, "buff_definitions.json"), JsonSerializer.Serialize(result.BuffDefinitions, options)),
            new BuffUnityImportWrite(CombineProjectPath(buffRoot, "buff_definitions.bytes"), RuntimeCatalogBinaryArtifacts.WriteBuffDefinitions(result.BuffDefinitions)),
            new BuffUnityImportWrite(CombineProjectPath(buffRoot, "buff_presentation_definitions.json"), JsonSerializer.Serialize(result.BuffPresentationDefinitions, options)),
            new BuffUnityImportWrite(CombineProjectPath(buffRoot, "buff_presentation_definitions.bytes"), RuntimeCatalogBinaryArtifacts.WriteBuffPresentations(result.BuffPresentationDefinitions)),
            new BuffUnityImportWrite(CombineProjectPath(auraRoot, "aura_buff_definitions.json"), JsonSerializer.Serialize(result.AuraBuffDefinitions, options)),
            new BuffUnityImportWrite(CombineProjectPath(auraRoot, "aura_buff_definitions.bytes"), RuntimeCatalogBinaryArtifacts.WriteAuraBuffDefinitions(result.AuraBuffDefinitions))
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
            File.WriteAllText(Path.Combine(reportOut, "buff_import_report.json"), EnsureTrailingNewline(reportJson));
        }

        Console.WriteLine(reportJson);
        return result.ValidationReport.HasBlockingIssues ? Program.ExitValidationBlocked : Program.ExitReady;
    }

    private static int ExportRuntimeBinaries(string[] args, JsonSerializerOptions options)
    {
        string projectRoot = Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory());
        string buffRoot = Program.GetOption(args, "--buff-root", "Assets/MxFrameworkGenerated/Gameplay/Buffs");
        string auraRoot = Program.GetOption(args, "--aura-root", "Assets/MxFrameworkGenerated/Combat/Auras");
        string reportOut = Program.GetOption(args, "--report-out", string.Empty);
        bool dryRun = Program.HasFlag(args, "--dry-run");

        var writes = new List<BuffUnityImportWrite>
        {
            new BuffUnityImportWrite(
                CombineProjectPath(buffRoot, "buff_definitions.bytes"),
                RuntimeCatalogBinaryArtifacts.WriteBuffDefinitions(
                    ReadProjectJson<BuffDefinitionCatalogDocument>(projectRoot, CombineProjectPath(buffRoot, "buff_definitions.json"), options))),
            new BuffUnityImportWrite(
                CombineProjectPath(buffRoot, "buff_presentation_definitions.bytes"),
                RuntimeCatalogBinaryArtifacts.WriteBuffPresentations(
                    ReadProjectJson<BuffPresentationDefinitionCatalogDocument>(projectRoot, CombineProjectPath(buffRoot, "buff_presentation_definitions.json"), options))),
            new BuffUnityImportWrite(
                CombineProjectPath(auraRoot, "aura_buff_definitions.bytes"),
                RuntimeCatalogBinaryArtifacts.WriteAuraBuffDefinitions(
                    ReadProjectJson<AuraBuffDefinitionCatalogDocument>(projectRoot, CombineProjectPath(auraRoot, "aura_buff_definitions.json"), options)))
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
            File.WriteAllText(Path.Combine(reportOut, "buff_runtime_binary_export_report.json"), EnsureTrailingNewline(reportJson));
        }

        Console.WriteLine(reportJson);
        return Program.ExitReady;
    }

    private static BuffAuthoringDocument ReadDocument(string packageOrDocumentPath, JsonSerializerOptions options)
    {
        string documentPath = BuffAuthoringCompiler.ResolveDocumentPath(packageOrDocumentPath);
        if (!File.Exists(documentPath))
            throw new FileNotFoundException("Buff authoring document was not found.", documentPath);

        return JsonSerializer.Deserialize<BuffAuthoringDocument>(File.ReadAllText(documentPath), options)
            ?? new BuffAuthoringDocument();
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

    private readonly struct BuffUnityImportWrite
    {
        public BuffUnityImportWrite(string relativePath, string content)
        {
            RelativePath = relativePath ?? string.Empty;
            Content = content ?? string.Empty;
            Bytes = Array.Empty<byte>();
            IsBinary = false;
        }

        public BuffUnityImportWrite(string relativePath, byte[] bytes)
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
