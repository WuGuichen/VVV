using System.Text;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static class ProjectilePackageCommands
{
    private const string DefinitionsJson = "projectile_definitions.json";
    private const string DefinitionsBytes = "projectile_definitions.bytes";
    private const string SpawnsJson = "projectile_spawn_profiles.json";
    private const string SpawnsBytes = "projectile_spawn_profiles.bytes";
    private const string BindingsJson = "projectile_presentation_bindings.json";
    private const string BindingsBytes = "projectile_presentation_bindings.bytes";
    private const string CompileReport = "projectile_compile_report.json";

    public static int Dispatch(string[] args, JsonSerializerOptions options)
    {
        if (args.Length < 2 || args[1] is "-h" or "--help")
        {
            PrintHelp();
            return Program.ExitReady;
        }
        return args[1] switch
        {
            "validate" => Validate(args, options),
            "compile" => Compile(args, options),
            "workspace" => Workspace(args, options),
            "mutate" => Mutate(args, options),
            "apply" => Apply(args, options, null),
            "import-unity" => ImportUnity(args, options, null),
            _ => Unknown(),
        };
    }

    private static int Workspace(string[] args, JsonSerializerOptions options)
    {
        string package = Program.RequireOption(args, "--package");
        string output = Program.GetOption(args, "--out", string.Empty);
        string generatedRoot = Program.GetOption(args, "--generated-root", string.Empty);
        string vfxDefinitions = Program.GetOption(args, "--vfx-definitions", string.Empty);
        using ProjectileAuthoringWorkspace workspace = ProjectileAuthoringWorkspace.Load(package, generatedRoot, vfxDefinitions, options);
        ProjectileAuthoringCompileResult compiled = workspace.Compile();
        object contract = ProjectileAuthoringWorkspaceProjectionBuilder.Build(workspace, compiled);
        string json = JsonSerializer.Serialize(contract, options) + Environment.NewLine;
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
            File.WriteAllText(output, json, new UTF8Encoding(false));
        }
        Console.Write(json);
        return ExitFor(workspace, compiled);
    }

    private static int Mutate(string[] args, JsonSerializerOptions options)
    {
        string package = Program.RequireOption(args, "--package");
        string requestPath = Program.RequireOption(args, "--request");
        string draftOut = Program.RequireOption(args, "--draft-out");
        string reportOut = Program.GetOption(args, "--report-out", string.Empty);
        string requestJson = File.ReadAllText(requestPath);
        string generatedRoot = Program.GetOption(args, "--generated-root", string.Empty);
        string vfxDefinitions = Program.GetOption(args, "--vfx-definitions", string.Empty);
        using ProjectileAuthoringWorkspace workspace = ProjectileAuthoringWorkspace.Load(package, generatedRoot, vfxDefinitions, options);
        using JsonDocument requestDocument = JsonDocument.Parse(requestJson);
        ProjectileAuthoringMutationResult result;
        if (requestDocument.RootElement.TryGetProperty("commands", out _))
        {
            ProjectileAuthoringMutationSequenceRequest sequence = JsonSerializer.Deserialize<ProjectileAuthoringMutationSequenceRequest>(requestJson, options)
                ?? throw new ArgumentException("Mutation sequence request is empty.");
            result = ProjectileAuthoringMutationSequencePlanner.Plan(workspace, sequence, options);
        }
        else
        {
            ProjectileAuthoringMutationRequest request = JsonSerializer.Deserialize<ProjectileAuthoringMutationRequest>(requestJson, options)
                ?? throw new ArgumentException("Mutation request is empty.");
            result = ProjectileAuthoringMutationSequencePlanner.Plan(workspace, new ProjectileAuthoringMutationSequenceRequest
            {
                Commands = new List<ProjectileAuthoringMutationRequest> { request }
            }, options);
        }
        if (result.Status == "Ready")
        {
            string artifactRoot = string.IsNullOrWhiteSpace(generatedRoot) ? Path.Combine(workspace.PackageRoot, "generated") : generatedRoot;
            PopulateExpectedArtifactHashes(result.Draft, artifactRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(draftOut)) ?? ".");
            File.WriteAllText(draftOut, JsonSerializer.Serialize(result.Draft, options) + Environment.NewLine, new UTF8Encoding(false));
        }
        if (!string.IsNullOrWhiteSpace(reportOut))
        {
            Directory.CreateDirectory(reportOut);
            File.WriteAllText(Path.Combine(reportOut, "projectile_mutation_report.json"), JsonSerializer.Serialize(result, options) + Environment.NewLine, new UTF8Encoding(false));
        }
        Console.WriteLine(JsonSerializer.Serialize(result, options));
        return result.Status == "Ready" ? Program.ExitReady : Program.ExitValidationBlocked;
    }

    private static void PopulateExpectedArtifactHashes(ProjectileAuthoringDraftBundle draft, string generatedRoot)
    {
        string[] names = { DefinitionsJson, DefinitionsBytes, SpawnsJson, SpawnsBytes, BindingsJson, BindingsBytes, CompileReport };
        for (int i = 0; i < names.Length; i++)
        {
            string path = Path.Combine(generatedRoot, names[i]);
            draft.ExpectedArtifactHashes[names[i]] = File.Exists(path)
                ? ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(path))
                : ProjectileAuthoringHashes.Missing;
        }
    }

    private static int Validate(string[] args, JsonSerializerOptions options)
    {
        string package = Program.RequireOption(args, "--package");
        string reportOut = Program.GetOption(args, "--report-out", string.Empty);
        string vfxDefinitions = Program.GetOption(args, "--vfx-definitions", string.Empty);
        using ProjectileAuthoringWorkspace workspace = ProjectileAuthoringWorkspace.Load(package, null, vfxDefinitions, options);
        ProjectileAuthoringCompileResult result = workspace.Compile();
        byte[] report = BuildReport(workspace, result, options);
        if (!string.IsNullOrWhiteSpace(reportOut))
        {
            Directory.CreateDirectory(reportOut);
            File.WriteAllBytes(Path.Combine(reportOut, "projectile_validation_report.json"), report);
        }
        Console.Write(Encoding.UTF8.GetString(report));
        return ExitFor(workspace, result);
    }

    private static int Compile(string[] args, JsonSerializerOptions options)
    {
        string package = Program.RequireOption(args, "--package");
        string output = Program.RequireOption(args, "--out");
        string vfxDefinitions = Program.GetOption(args, "--vfx-definitions", string.Empty);
        using ProjectileAuthoringWorkspace workspace = ProjectileAuthoringWorkspace.Load(package, null, vfxDefinitions, options);
        ProjectileAuthoringCompileResult result = workspace.Compile();
        if (result.HasBlockingIssues)
            return ExitFor(workspace, result);
        Dictionary<string, byte[]> artifacts = BuildArtifacts(workspace, result, options);
        var items = new List<ProjectileAuthoringTransactionItem>();
        foreach (KeyValuePair<string, byte[]> artifact in artifacts)
        {
            string target = Path.Combine(output, artifact.Key);
            items.Add(new ProjectileAuthoringTransactionItem
            {
                ReceiptPath = artifact.Key,
                TargetPath = target,
                ExpectedSha256 = File.Exists(target) ? ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(target)) : ProjectileAuthoringHashes.Missing,
                Content = artifact.Value,
            });
        }
        ProjectileAuthoringTransactionReceipt receipt = ProjectileAuthoringTransaction.Execute(items);
        Console.WriteLine("status=" + receipt.Status);
        Console.WriteLine("output=" + NormalizeRelative(output));
        return receipt.Status == "Committed" ? Program.ExitReady : Program.ExitToolError;
    }

    internal static int ApplyForTests(string[] args, JsonSerializerOptions options, IProjectileAuthoringTransactionFaultInjector faultInjector)
        => Apply(args, options, faultInjector);

    private static int Apply(string[] args, JsonSerializerOptions options, IProjectileAuthoringTransactionFaultInjector faultInjector)
    {
        string package = Program.RequireOption(args, "--package");
        string draftPath = Program.RequireOption(args, "--draft");
        string output = Program.RequireOption(args, "--out");
        string receiptOut = Program.GetOption(args, "--receipt-out", string.Empty);
        string vfxDefinitions = Program.GetOption(args, "--vfx-definitions", string.Empty);
        string projectRoot = Program.GetOption(args, "--project-root", string.Empty);
        string projectileRoot = Program.GetOption(args, "--projectile-root", "Assets/MxFrameworkGenerated/Combat/Projectiles");
        string playerMirrorRoot = Program.GetOption(
            args,
            "--player-mirror-root",
            "Assets/StreamingAssets/MxFramework/CharacterTest/RuntimeCatalogs/Combat/Projectiles");
        bool syncUnityAndPlayer = !string.IsNullOrWhiteSpace(projectRoot);
        string unityRoot = string.Empty;
        string mirrorRoot = string.Empty;
        if (syncUnityAndPlayer)
        {
            string projectFullPath = Path.GetFullPath(projectRoot);
            unityRoot = SafeCombine(projectFullPath, NormalizeRelative(projectileRoot));
            mirrorRoot = SafeCombine(projectFullPath, NormalizeRelative(playerMirrorRoot));
            if (PathsOverlap(unityRoot, mirrorRoot))
            {
                Console.Error.WriteLine(
                    "Projectile generated root and Player mirror root must be distinct, non-overlapping directories.");
                return Program.ExitToolError;
            }
        }
        ProjectileAuthoringDraftBundle draft = JsonSerializer.Deserialize<ProjectileAuthoringDraftBundle>(File.ReadAllText(draftPath), options)
            ?? throw new ArgumentException("Draft is empty.");
        if (!string.Equals(draft.SchemaVersion, "1", StringComparison.Ordinal))
            return Program.ExitSchemaIncompatible;

        using ProjectileAuthoringWorkspace current = ProjectileAuthoringWorkspace.Load(package, null, vfxDefinitions, options);
        var sourceItems = new List<ProjectileAuthoringTransactionItem>();
        string stagedPackage = CreateStagedPackage(current.PackageRoot);
        try
        {
            for (int i = 0; i < draft.Documents.Count; i++)
            {
                ProjectileAuthoringDraftDocument proposal = draft.Documents[i];
                string relative = NormalizeRelative(proposal.SourceRelativePath);
                bool projectileSource = IsProjectileSource(relative);
                bool consumerProposal = string.Equals(proposal.Operation, "UpdateConsumer", StringComparison.Ordinal);
                bool vfxContractProposal = string.Equals(
                        proposal.Operation,
                        "SynchronizeVfxContract",
                        StringComparison.Ordinal)
                    && string.Equals(
                        relative,
                        AbilityAuthoringCompiler.AbilityAuthoringDocumentRelativePath,
                        StringComparison.Ordinal);
                if (!projectileSource
                    && (!consumerProposal || !IsAuthorizedConfigSource(relative))
                    && !vfxContractProposal)
                    throw new ArgumentException("Draft source path must be a package-relative config JSON path: " + relative);
                ProjectileAuthoringSourceDocument source = FindSource(current, relative);
                string currentTarget = SafeCombine(current.PackageRoot, relative);
                bool deleting = string.Equals(proposal.Operation, "Delete", StringComparison.OrdinalIgnoreCase);
                if (deleting)
                {
                    if (!File.Exists(currentTarget)) throw new ArgumentException("Delete source does not exist: " + relative);
                    string stagedDelete = SafeCombine(stagedPackage, relative);
                    File.Delete(stagedDelete);
                    sourceItems.Add(new ProjectileAuthoringTransactionItem { ReceiptPath = relative, TargetPath = SafeCombine(current.PackageRoot, relative), ExpectedSha256 = proposal.ExpectedSha256, Delete = true });
                    continue;
                }
                if (!File.Exists(currentTarget) && !string.Equals(proposal.ExpectedSha256, ProjectileAuthoringHashes.Missing, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("New source must use expected hash 'missing': " + relative);
                byte[] content = Array.Empty<byte>();
                if (projectileSource && source != null && !current.TryBuildLosslessProposal(source, proposal.Json, out content, out ProjectileAuthoringDiagnostic diagnostic))
                {
                    WriteBlockedReceipt(receiptOut, diagnostic.Code, options, current, draft);
                    return Program.ExitValidationBlocked;
                }
                if (!projectileSource || source == null)
                    content = Utf8(proposal.Json.GetRawText());
                string stagedTarget = SafeCombine(stagedPackage, relative);
                File.WriteAllBytes(stagedTarget, content);
                sourceItems.Add(new ProjectileAuthoringTransactionItem
                {
                    ReceiptPath = relative,
                    TargetPath = SafeCombine(current.PackageRoot, relative),
                    ExpectedSha256 = proposal.ExpectedSha256,
                    Content = content,
                });
            }

            using ProjectileAuthoringWorkspace staged = ProjectileAuthoringWorkspace.Load(stagedPackage, null, vfxDefinitions, options);
            ProjectileAuthoringCompileResult result = staged.Compile();
            if (result.HasBlockingIssues)
            {
                Console.Error.WriteLine(JsonSerializer.Serialize(result.Diagnostics, options));
                WriteBlockedReceipt(receiptOut, "PROJAUTH_VALIDATION_BLOCKED", options, current, draft);
                return ExitFor(staged, result);
            }
            Dictionary<string, byte[]> artifacts = BuildArtifacts(staged, result, options);
            var items = new List<ProjectileAuthoringTransactionItem>(sourceItems);
            foreach (KeyValuePair<string, byte[]> artifact in artifacts)
            {
                string target = Path.Combine(output, artifact.Key);
                if (!draft.ExpectedArtifactHashes.TryGetValue(artifact.Key, out string expected))
                {
                    expected = File.Exists(target) ? string.Empty : ProjectileAuthoringHashes.Missing;
                }
                if (string.IsNullOrWhiteSpace(expected))
                {
                    WriteBlockedReceipt(receiptOut, "PROJAUTH006", options, current, draft);
                    return Program.ExitValidationBlocked;
                }
                items.Add(new ProjectileAuthoringTransactionItem
                {
                    ReceiptPath = "generated/" + artifact.Key,
                    TargetPath = target,
                    ExpectedSha256 = expected,
                    Content = artifact.Value,
                });
            }
            if (syncUnityAndPlayer)
            {
                AddRuntimeArtifactTransactionItems(items, artifacts, unityRoot, projectileRoot);
                AddRuntimeArtifactTransactionItems(items, artifacts, mirrorRoot, playerMirrorRoot);
            }

            ProjectileAuthoringTransactionReceipt receipt = faultInjector == null
                ? ProjectileAuthoringTransaction.Execute(items)
                : ProjectileAuthoringTransaction.Execute(items, faultInjector);
            receipt.PlayerMirrorFreshness = syncUnityAndPlayer
                ? receipt.Status == "Committed"
                    ? "UpdatedAtomically"
                    : receipt.RollbackResult == "Succeeded" ? "RolledBackWithGenerated" : "RecoveryRequired"
                : "NotUpdatedByTransaction";
            WriteReceipt(receiptOut, receipt, options);
            Console.WriteLine(JsonSerializer.Serialize(receipt, options));
            return receipt.Status switch
            {
                "Committed" => Program.ExitReady,
                "Blocked" when receipt.DiagnosticCodes.Contains("PROJAUTH006") => Program.ExitValidationBlocked,
                _ => Program.ExitToolError,
            };
        }
        finally
        {
            TryDeleteDirectory(stagedPackage);
        }
    }

    internal static int ImportUnityForTests(
        string[] args,
        JsonSerializerOptions options,
        IProjectileAuthoringTransactionFaultInjector faultInjector)
        => ImportUnity(args, options, faultInjector);

    private static int ImportUnity(
        string[] args,
        JsonSerializerOptions options,
        IProjectileAuthoringTransactionFaultInjector faultInjector)
    {
        string package = Program.RequireOption(args, "--package");
        string projectRoot = Program.RequireOption(args, "--project-root");
        string projectileRoot = Program.GetOption(args, "--projectile-root", "Assets/MxFrameworkGenerated/Combat/Projectiles");
        string playerMirrorRoot = Program.GetOption(
            args,
            "--player-mirror-root",
            "Assets/StreamingAssets/MxFramework/CharacterTest/RuntimeCatalogs/Combat/Projectiles");
        string reportOut = Program.GetOption(args, "--report-out", string.Empty);
        bool dryRun = Program.HasFlag(args, "--dry-run");
        string vfxDefinitions = Program.GetOption(args, "--vfx-definitions", string.Empty);
        string projectFullPath = Path.GetFullPath(projectRoot);
        string root = SafeCombine(projectFullPath, NormalizeRelative(projectileRoot));
        string mirrorRoot = SafeCombine(projectFullPath, NormalizeRelative(playerMirrorRoot));
        if (PathsOverlap(root, mirrorRoot))
        {
            Console.Error.WriteLine(
                "Projectile generated root and Player mirror root must be distinct, non-overlapping directories.");
            return Program.ExitToolError;
        }

        using ProjectileAuthoringWorkspace workspace = ProjectileAuthoringWorkspace.Load(package, null, vfxDefinitions, options);
        ProjectileAuthoringCompileResult result = workspace.Compile();
        if (result.HasBlockingIssues)
            return ExitFor(workspace, result);
        Dictionary<string, byte[]> artifacts = BuildArtifacts(workspace, result, options);
        artifacts.Remove(CompileReport);
        string[] artifactNames = artifacts.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        string[] writes = artifactNames
            .SelectMany(name => new[]
            {
                NormalizeRelative(Path.Combine(projectileRoot, name)),
                NormalizeRelative(Path.Combine(playerMirrorRoot, name)),
            })
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var report = new
        {
            status = dryRun ? "DryRun" : "Ready",
            dryRun,
            transaction = "GeneratedAndPlayerMirrorAtomic",
            writes,
        };
        string reportJson = JsonSerializer.Serialize(report, options) + Environment.NewLine;
        if (!string.IsNullOrWhiteSpace(reportOut))
        {
            Directory.CreateDirectory(reportOut);
            File.WriteAllText(Path.Combine(reportOut, "projectile_import_report.json"), reportJson);
        }
        if (dryRun)
        {
            Console.Write(reportJson);
            return Program.ExitReady;
        }

        var items = new List<ProjectileAuthoringTransactionItem>(artifacts.Count * 2);
        AddArtifactTransactionItems(items, artifacts, root, projectileRoot);
        AddArtifactTransactionItems(items, artifacts, mirrorRoot, playerMirrorRoot);
        ProjectileAuthoringTransactionReceipt receipt = faultInjector == null
            ? ProjectileAuthoringTransaction.Execute(items)
            : ProjectileAuthoringTransaction.Execute(items, faultInjector);
        receipt.PlayerMirrorFreshness = receipt.Status == "Committed"
            ? "UpdatedAtomically"
            : receipt.RollbackResult == "Succeeded" ? "RolledBackWithGenerated" : "RecoveryRequired";
        Console.WriteLine(JsonSerializer.Serialize(receipt, options));
        return receipt.Status == "Committed" ? Program.ExitReady : Program.ExitToolError;
    }

    private static bool PathsOverlap(string left, string right)
    {
        string normalizedLeft = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedRight = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase))
            return true;
        string leftPrefix = normalizedLeft + Path.DirectorySeparatorChar;
        string rightPrefix = normalizedRight + Path.DirectorySeparatorChar;
        return normalizedLeft.StartsWith(rightPrefix, StringComparison.OrdinalIgnoreCase)
            || normalizedRight.StartsWith(leftPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static void AddArtifactTransactionItems(
        List<ProjectileAuthoringTransactionItem> items,
        IReadOnlyDictionary<string, byte[]> artifacts,
        string root,
        string receiptRoot)
    {
        foreach (KeyValuePair<string, byte[]> artifact in artifacts)
        {
            string target = Path.Combine(root, artifact.Key);
            items.Add(new ProjectileAuthoringTransactionItem
            {
                ReceiptPath = NormalizeRelative(Path.Combine(receiptRoot, artifact.Key)),
                TargetPath = target,
                ExpectedSha256 = File.Exists(target) ? ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(target)) : ProjectileAuthoringHashes.Missing,
                Content = artifact.Value,
            });
        }
    }

    private static void AddRuntimeArtifactTransactionItems(
        List<ProjectileAuthoringTransactionItem> items,
        IReadOnlyDictionary<string, byte[]> artifacts,
        string root,
        string receiptRoot)
    {
        foreach (KeyValuePair<string, byte[]> artifact in artifacts)
        {
            if (string.Equals(artifact.Key, CompileReport, StringComparison.Ordinal))
                continue;
            string target = Path.Combine(root, artifact.Key);
            items.Add(new ProjectileAuthoringTransactionItem
            {
                ReceiptPath = NormalizeRelative(Path.Combine(receiptRoot, artifact.Key)),
                TargetPath = target,
                ExpectedSha256 = File.Exists(target)
                    ? ProjectileAuthoringHashes.ComputeSha256(File.ReadAllBytes(target))
                    : ProjectileAuthoringHashes.Missing,
                Content = artifact.Value,
            });
        }
    }

    internal static Dictionary<string, byte[]> BuildArtifacts(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringCompileResult result, JsonSerializerOptions options)
    {
        byte[] definitionsBinary = RuntimeCatalogBinaryArtifacts.WriteProjectileDefinitions(result.Definitions);
        byte[] spawnsBinary = RuntimeCatalogBinaryArtifacts.WriteProjectileSpawnProfiles(result.SpawnProfiles);
        byte[] bindingsBinary = RuntimeCatalogBinaryArtifacts.WriteProjectilePresentationBindings(result.PresentationBindings);
        if (!RuntimeCatalogBinaryArtifacts.ValidateProjectileArtifact(definitionsBinary, RuntimeCatalogBinaryArtifacts.ProjectileDefinitionsKind, "5", result.Definitions.Definitions.Count)
            || !RuntimeCatalogBinaryArtifacts.ValidateProjectileArtifact(spawnsBinary, RuntimeCatalogBinaryArtifacts.ProjectileSpawnProfilesKind, "2", result.SpawnProfiles.Profiles.Count)
            || !RuntimeCatalogBinaryArtifacts.ValidateProjectileArtifact(bindingsBinary, RuntimeCatalogBinaryArtifacts.ProjectilePresentationBindingsKind, result.PresentationBindings.SchemaVersion, result.PresentationBindings.Bindings.Count))
            throw new InvalidDataException("Projectile binary artifact header validation failed.");
        return new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [DefinitionsJson] = JsonBytes(result.Definitions, options),
            [DefinitionsBytes] = definitionsBinary,
            [SpawnsJson] = JsonBytes(result.SpawnProfiles, options),
            [SpawnsBytes] = spawnsBinary,
            [BindingsJson] = JsonBytes(result.PresentationBindings, options),
            [BindingsBytes] = bindingsBinary,
            [CompileReport] = BuildReport(workspace, result, options),
        };
    }

    private static byte[] BuildReport(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringCompileResult result, JsonSerializerOptions options)
    {
        var report = new
        {
            schemaVersion = "1",
            status = result.HasBlockingIssues ? "Blocked" : "Ready",
            sourceCount = workspace.SourceDocuments.Count,
            definitionCount = result.Definitions.Definitions.Count,
            spawnProfileCount = result.SpawnProfiles.Profiles.Count,
            presentationBindingCount = result.PresentationBindings.Bindings.Count,
            diagnostics = result.Diagnostics,
        };
        return JsonBytes(report, options);
    }

    private static int ExitFor(ProjectileAuthoringWorkspace workspace, ProjectileAuthoringCompileResult result)
    {
        for (int i = 0; i < workspace.Diagnostics.Count; i++)
        {
            if (workspace.Diagnostics[i].Code.Contains("SCHEMA", StringComparison.Ordinal))
                return Program.ExitSchemaIncompatible;
        }
        return result.HasBlockingIssues ? Program.ExitValidationBlocked : Program.ExitReady;
    }

    private static ProjectileAuthoringSourceDocument FindSource(ProjectileAuthoringWorkspace workspace, string relative)
    {
        for (int i = 0; i < workspace.SourceDocuments.Count; i++)
        {
            if (string.Equals(workspace.SourceDocuments[i].RelativePath, relative, StringComparison.Ordinal))
                return workspace.SourceDocuments[i];
        }
        return null;
    }

    private static string CreateStagedPackage(string packageRoot)
    {
        string target = Path.Combine(Path.GetTempPath(), "MxFramework.ProjectileAuthoring", Guid.NewGuid().ToString("N"));
        CopyDirectory(packageRoot, target);
        return target;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        string[] files = Directory.GetFiles(source, "*", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < files.Length; i++)
            File.Copy(files[i], Path.Combine(target, Path.GetFileName(files[i])));
        string[] directories = Directory.GetDirectories(source, "*", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < directories.Length; i++)
            CopyDirectory(directories[i], Path.Combine(target, Path.GetFileName(directories[i])));
    }

    private static string SafeCombine(string root, string relative)
    {
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Path escapes the requested root: " + relative);
        return full;
    }

    private static bool IsProjectileSource(string relative)
    {
        return relative.StartsWith("config/projectiles/", StringComparison.Ordinal)
            && relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            && !relative.Contains("../", StringComparison.Ordinal);
    }

    private static bool IsAuthorizedConfigSource(string relative)
    {
        return relative.StartsWith("config/", StringComparison.Ordinal)
            && relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            && !relative.Contains("../", StringComparison.Ordinal);
    }

    private static void WriteBlockedReceipt(
        string directory,
        string code,
        JsonSerializerOptions options,
        ProjectileAuthoringWorkspace workspace,
        ProjectileAuthoringDraftBundle draft)
    {
        var receipt = new ProjectileAuthoringTransactionReceipt { Status = "Blocked" };
        receipt.DiagnosticCodes.Add(code);
        if (workspace != null && draft?.Documents != null)
        {
            for (int i = 0; i < draft.Documents.Count; i++)
            {
                ProjectileAuthoringDraftDocument proposal = draft.Documents[i];
                string relative = NormalizeRelative(proposal.SourceRelativePath);
                ProjectileAuthoringSourceDocument source = FindSource(workspace, relative);
                receipt.TouchedFiles.Add(new ProjectileAuthoringTransactionFileReceipt
                {
                    Path = relative,
                    BeforeSha256 = source?.Sha256 ?? proposal.ExpectedSha256,
                    AfterSha256 = string.Equals(proposal.Operation, "Delete", StringComparison.OrdinalIgnoreCase)
                        ? ProjectileAuthoringHashes.Missing
                        : ProjectileAuthoringHashes.ComputeSha256(Utf8(proposal.Json.GetRawText())),
                });
            }
        }
        WriteReceipt(directory, receipt, options);
        Console.WriteLine(JsonSerializer.Serialize(receipt, options));
    }

    private static void WriteReceipt(string directory, ProjectileAuthoringTransactionReceipt receipt, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return;
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "projectile_apply_receipt.json"), JsonBytes(receipt, options));
    }

    private static byte[] JsonBytes<T>(T value, JsonSerializerOptions options) => Utf8(JsonSerializer.Serialize(value, options) + Environment.NewLine);
    private static byte[] Utf8(string value) => new UTF8Encoding(false).GetBytes(value ?? string.Empty);
    private static string NormalizeRelative(string value) => (value ?? string.Empty).Replace('\\', '/').TrimStart('/');

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // Best-effort cleanup for system temporary staging.
        }
    }

    private static int Unknown()
    {
        Console.Error.WriteLine("error: unknown projectile command.");
        return Program.ExitToolError;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("projectile validate --package <path> [--report-out <dir>]");
        Console.WriteLine("projectile compile --package <path> --out <dir>");
        Console.WriteLine("projectile workspace --package <path> [--generated-root <dir>] [--vfx-definitions <vfx_definitions.json>] [--out <workspace.json>]");
        Console.WriteLine("projectile mutate --package <path> --request <mutation.json> --draft-out <draft.json> [--generated-root <dir>] [--vfx-definitions <vfx_definitions.json>] [--report-out <dir>]");
        Console.WriteLine("projectile apply --package <path> --draft <draft.json> --out <generated-dir> [--project-root <repoRoot>] [--projectile-root <Assets/...>] [--player-mirror-root <Assets/...>] [--receipt-out <dir>]");
        Console.WriteLine("projectile import-unity --package <path> --project-root <repoRoot> [--projectile-root <Assets/...>] [--player-mirror-root <Assets/...>] [--report-out <dir>] [--dry-run]");
    }
}
