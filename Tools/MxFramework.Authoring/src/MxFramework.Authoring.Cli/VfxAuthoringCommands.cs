using System.Text;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static class VfxAuthoringCommands
{
    public static int Dispatch(string[] args, JsonSerializerOptions jsonOptions)
    {
        if (args.Length < 2)
            throw new ArgumentException("vfx command is required (validate, propose-coverage, plan-save, plan-bake, plan-create, agent-task, validate-receipt, evaluate-candidates, plan-promote, or plan-register-published).", nameof(args));

        return args[1] switch
        {
            "validate" => Validate(args, jsonOptions),
            "propose-coverage" => ProposeCoverage(args),
            "search" => Search(args, jsonOptions),
            "impact" => Impact(args, jsonOptions),
            "plan-save" => PlanSave(args, jsonOptions),
            "plan-bake" => PlanBake(args, jsonOptions),
            "plan-create" => PlanCreate(args, jsonOptions),
            "reference-pack" => ReferencePack(args, jsonOptions),
            "source" => SourceLibrary(args, jsonOptions),
            "agent-task" => AgentTask(args, jsonOptions),
            "validate-receipt" => ValidateReceipt(args, jsonOptions),
            "evaluate-candidates" => EvaluateCandidates(args, jsonOptions),
            "plan-promote" => PlanPromote(args, jsonOptions),
            "plan-register-published" => PlanRegisterPublished(args, jsonOptions),
            _ => throw new ArgumentException("Unknown vfx command: " + args[1])
        };
    }

    private static int SourceLibrary(string[] args, JsonSerializerOptions jsonOptions)
    {
        if (args.Length < 3)
            throw new ArgumentException("vfx source command is required (scan, seal-index, search, plan-extract, validate-extraction, plan-compose, or seal-receipt).", nameof(args));

        string repositoryRoot = Path.GetFullPath(Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory()));
        string defaultIndex = ResolveProjectPath(repositoryRoot, "Temp/MxFramework/VfxSourceLibrary/index.json");
        string defaultRegistry = ResolveProjectPath(repositoryRoot, "Assets/Config/MxFramework/Vfx/vfx_source_extraction_registry.json");
        string command = args[2];
        if (command == "seal-index")
        {
            string input = Program.GetOption(args, "--input", defaultIndex);
            VfxSourceLibraryDocument index = VfxCreativeFlywheelJson.LoadFile<VfxSourceLibraryDocument>(input) ?? new VfxSourceLibraryDocument();
            VfxSourceLibraryValidator.SealIndex(index);
            string json = VfxCreativeFlywheelJson.Serialize(index) + Environment.NewLine;
            string output = Program.GetOption(args, "--output", input);
            WriteText(output, json);
            Console.WriteLine(JsonSerializer.Serialize(new { valid = true, output, index.IndexHash, index.PrefabCount, index.ParticleSystemCount, index.SupportAssetCount }, jsonOptions));
            return AuthoringExitCodes.Ready;
        }
        if (command == "seal-registry")
        {
            string input = Program.GetOption(args, "--input", defaultRegistry);
            VfxSourceExtractionRegistryDocument registry = VfxCreativeFlywheelJson.LoadFile<VfxSourceExtractionRegistryDocument>(input) ?? new VfxSourceExtractionRegistryDocument();
            VfxSourceLibraryValidator.SealRegistry(registry);
            string output = Program.GetOption(args, "--output", input);
            WriteText(output, VfxCreativeFlywheelJson.Serialize(registry) + Environment.NewLine);
            Console.WriteLine(JsonSerializer.Serialize(new { valid = true, output, registry.RegistryHash, count = registry.Entries.Count }, jsonOptions));
            return AuthoringExitCodes.Ready;
        }

        VfxSourceLibraryDocument current = VfxCreativeFlywheelJson.LoadFile<VfxSourceLibraryDocument>(Program.GetOption(args, "--index", defaultIndex));
        VfxSourceExtractionRegistryDocument currentRegistry = File.Exists(Program.GetOption(args, "--registry", defaultRegistry))
            ? VfxCreativeFlywheelJson.LoadFile<VfxSourceExtractionRegistryDocument>(Program.GetOption(args, "--registry", defaultRegistry))
            : new VfxSourceExtractionRegistryDocument { SourceLibraryId = current?.SourceLibraryId ?? "source.piloto.studio", SourceIndexHash = current?.IndexHash ?? string.Empty };
        if (command == "scan")
        {
            VfxSourceValidationReport report = VfxSourceLibraryValidator.ValidateIndex(current);
            EmitPlan(args, JsonSerializer.Serialize(new { report.Valid, report.Diagnostics, current?.SourceLibraryId, current?.IndexHash, current?.PrefabCount, current?.ParticleSystemCount, current?.SupportAssetCount }, jsonOptions));
            return report.Valid ? AuthoringExitCodes.Ready : AuthoringExitCodes.ValidationBlocked;
        }
        if (command == "search")
        {
            VfxSourceValidationReport report = VfxSourceLibraryValidator.ValidateIndex(current);
            if (!report.Valid) { EmitPlan(args, JsonSerializer.Serialize(report, jsonOptions)); return AuthoringExitCodes.ValidationBlocked; }
            var query = new VfxSourceSearchQuery
            {
                Text = Program.GetOption(args, "--text", string.Empty),
                SourcePackageId = Program.GetOption(args, "--package", string.Empty),
                Family = Program.GetOption(args, "--family", string.Empty),
                Role = Program.GetOption(args, "--role", string.Empty),
                Theme = Program.GetOption(args, "--theme", string.Empty),
                Anchor = Program.GetOption(args, "--anchor", string.Empty),
                Shape = Program.GetOption(args, "--shape", string.Empty),
                QualityTier = Program.GetOption(args, "--quality", string.Empty),
                Backend = Program.GetOption(args, "--backend", string.Empty),
                MaxParticles = int.Parse(Program.GetOption(args, "--max-particles", "0")),
                Limit = int.Parse(Program.GetOption(args, "--limit", "50"))
            };
            IReadOnlyList<VfxSourceSearchResult> results = new VfxSourceSearchService().Search(current, query);
            EmitPlan(args, JsonSerializer.Serialize(new { indexHash = current.IndexHash, count = results.Count, results }, jsonOptions));
            return AuthoringExitCodes.Ready;
        }
        if (command == "plan-extract")
        {
            VfxSourceExtractionRequest request = VfxCreativeFlywheelJson.LoadFile<VfxSourceExtractionRequest>(Program.RequireOption(args, "--request"));
            VfxSourceExtractionPlan plan = VfxSourcePlanner.PlanExtraction(current, request, currentRegistry);
            EmitPlan(args, JsonSerializer.Serialize(plan, jsonOptions));
            return plan.Blocked ? AuthoringExitCodes.ValidationBlocked : AuthoringExitCodes.Ready;
        }
        if (command == "validate-registry")
        {
            VfxSourceValidationReport report = VfxSourceLibraryValidator.ValidateRegistry(currentRegistry, current);
            EmitPlan(args, JsonSerializer.Serialize(report, jsonOptions));
            return report.Valid ? AuthoringExitCodes.Ready : AuthoringExitCodes.ValidationBlocked;
        }
        if (command == "validate-extraction")
        {
            VfxSourceExtractionPlan plan = VfxCreativeFlywheelJson.LoadFile<VfxSourceExtractionPlan>(Program.RequireOption(args, "--plan"));
            VfxSourceExtractionReceipt receipt = VfxCreativeFlywheelJson.LoadFile<VfxSourceExtractionReceipt>(Program.RequireOption(args, "--receipt"));
            VfxSourceValidationReport report = VfxSourceLibraryValidator.ValidateExtraction(plan, receipt, current);
            EmitPlan(args, JsonSerializer.Serialize(report, jsonOptions));
            return report.Valid ? AuthoringExitCodes.Ready : AuthoringExitCodes.ValidationBlocked;
        }
        if (command == "seal-receipt")
        {
            string input = Program.RequireOption(args, "--input");
            VfxSourceExtractionReceipt receipt = VfxCreativeFlywheelJson.LoadFile<VfxSourceExtractionReceipt>(input) ?? new VfxSourceExtractionReceipt();
            VfxSourceLibraryValidator.SealReceipt(receipt);
            string output = Program.GetOption(args, "--output", input);
            WriteText(output, VfxCreativeFlywheelJson.Serialize(receipt) + Environment.NewLine);
            Console.WriteLine(JsonSerializer.Serialize(new { valid = true, output, receipt.ReceiptHash }, jsonOptions));
            return AuthoringExitCodes.Ready;
        }
        if (command == "plan-compose")
        {
            string[] primitiveIds = Program.RequireOption(args, "--primitives").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            VfxSourceCompositionPlan plan = VfxSourcePlanner.PlanComposition(
                Program.RequireOption(args, "--recipe"),
                Program.GetOption(args, "--name", string.Empty),
                Program.RequireOption(args, "--output-prefab"),
                Program.RequireOption(args, "--expected-hash"),
                primitiveIds);
            EmitPlan(args, JsonSerializer.Serialize(plan, jsonOptions));
            return plan.Blocked ? AuthoringExitCodes.ValidationBlocked : AuthoringExitCodes.Ready;
        }
        throw new ArgumentException("Unknown vfx source command: " + command);
    }

    private static int ReferencePack(string[] args, JsonSerializerOptions jsonOptions)
    {
        if (args.Length < 3) throw new ArgumentException("vfx reference-pack command is required (register, scan, or validate).", nameof(args));
        string repositoryRoot = Path.GetFullPath(Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory()));
        VfxReferencePackScanOptions scanOptions = CreateReferencePackScanOptions(args);
        if (args[2] == "scan")
        {
            VfxReferencePackScanResult scan = VfxReferencePackScanner.ScanPiloto(Program.RequireOption(args, "--source"), scanOptions);
            Console.WriteLine(JsonSerializer.Serialize(scan, jsonOptions));
            return scan.HasErrors ? AuthoringExitCodes.ValidationBlocked : AuthoringExitCodes.Ready;
        }
        if (args[2] == "register")
        {
            string source = Path.GetFullPath(Program.RequireOption(args, "--source"));
            VfxReferencePackScanResult scan = VfxReferencePackScanner.ScanPiloto(source, scanOptions);
            if (scan.HasErrors || scan.Manifest == null) { Console.WriteLine(JsonSerializer.Serialize(scan, jsonOptions)); return AuthoringExitCodes.ValidationBlocked; }
            string outputDirectory = ResolveProjectPath(repositoryRoot, "Temp/MxFramework/VfxReferencePacks/piloto-arpg-realistic-demon-vs-angels");
            Directory.CreateDirectory(outputDirectory);
            string manifestPath = Path.Combine(outputDirectory, "manifest.json");
            string registrationPath = ResolveReferenceRegistrationPath(repositoryRoot, args);
            var registration = new VfxReferencePackLocalRegistration
            {
                SourceRoot = source,
                ManifestPath = ProjectRelative(repositoryRoot, manifestPath),
                UsagePolicy = scan.Manifest.UsagePolicy,
                SourcePackageId = scan.Manifest.SourcePackageId,
                LicenseId = scan.Manifest.LicenseId,
                PublicRedistributionAllowed = scan.Manifest.PublicRedistributionAllowed
            };
            WriteText(manifestPath, VfxCreativeFlywheelJson.Serialize(scan.Manifest) + Environment.NewLine);
            WriteText(registrationPath, VfxCreativeFlywheelJson.Serialize(registration) + Environment.NewLine);
            Console.WriteLine(JsonSerializer.Serialize(new { registrationPath = ProjectRelative(repositoryRoot, registrationPath), manifestPath = registration.ManifestPath, scan.Manifest.ManifestHash, scan.Manifest.PrefabCount, scan.Manifest.DependencyCount, scan.Diagnostics }, jsonOptions));
            return AuthoringExitCodes.Ready;
        }
        if (args[2] == "validate")
        {
            VfxReferencePackLocalRegistration registration = VfxCreativeFlywheelJson.LoadFile<VfxReferencePackLocalRegistration>(ResolveReferenceRegistrationPath(repositoryRoot, args));
            VfxReferencePackManifest manifest = LoadRegisteredReferenceManifest(repositoryRoot, registration);
            VfxReferencePackScanResult current = VfxReferencePackScanner.ScanPiloto(registration?.SourceRoot ?? string.Empty, RegistrationOptions(registration));
            string message = "Reference pack registration or manifest is missing.";
            bool valid = manifest != null && current.Manifest != null && VfxReferencePackScanner.ValidateManifest(manifest, current.Manifest.ManifestHash, out message);
            Console.WriteLine(JsonSerializer.Serialize(new { valid, message, expectedHash = manifest?.ManifestHash ?? string.Empty, actualHash = current.Manifest?.ManifestHash ?? string.Empty, current.Diagnostics }, jsonOptions));
            return valid && !current.HasErrors ? AuthoringExitCodes.Ready : AuthoringExitCodes.ValidationBlocked;
        }
        throw new ArgumentException("Unknown vfx reference-pack command: " + args[2]);
    }

    private static VfxReferencePackScanOptions CreateReferencePackScanOptions(string[] args)
    {
        bool licensed = Program.HasFlag(args, "--licensed-reusable");
        return new VfxReferencePackScanOptions
        {
            UsagePolicy = licensed ? VfxReferenceUsagePolicies.LicensedReusable : VfxReferenceUsagePolicies.ReferenceOnly,
            SourcePackageId = licensed ? Program.GetOption(args, "--source-package", "piloto.super-realistic-fx-bundle") : string.Empty,
            LicenseId = licensed ? Program.GetOption(args, "--license-id", "operator-confirmed-public-redistribution-2026-07-14") : string.Empty,
            PublicRedistributionAllowed = licensed && Program.HasFlag(args, "--public-redistribution-allowed")
        };
    }

    private static VfxReferencePackScanOptions RegistrationOptions(VfxReferencePackLocalRegistration registration)
    {
        return new VfxReferencePackScanOptions
        {
            UsagePolicy = registration?.UsagePolicy ?? VfxReferenceUsagePolicies.ReferenceOnly,
            SourcePackageId = registration?.SourcePackageId ?? string.Empty,
            LicenseId = registration?.LicenseId ?? string.Empty,
            PublicRedistributionAllowed = registration?.PublicRedistributionAllowed == true
        };
    }

    private static int AgentTask(string[] args, JsonSerializerOptions jsonOptions)
    {
        if (args.Length < 3)
            throw new ArgumentException("vfx agent-task command is required (create, validate, request-cancel, seal-receipt, or validate-receipt).", nameof(args));
        return args[2] switch
        {
            "create" => CreateAgentTask(args, jsonOptions),
            "validate" => ValidateAgentTask(args, jsonOptions),
            "request-cancel" => RequestAgentTaskCancel(args, jsonOptions),
            "set-progress" => SetAgentTaskProgress(args, jsonOptions),
            "seal-receipt" => SealAgentTaskReceipt(args, jsonOptions),
            "validate-receipt" => ValidateAgentTaskReceipt(args, jsonOptions),
            _ => throw new ArgumentException("Unknown vfx agent-task command: " + args[2])
        };
    }

    private static int SetAgentTaskProgress(string[] args, JsonSerializerOptions jsonOptions)
    {
        string taskPath = Program.RequireOption(args, "--task");
        string progressPath = Program.RequireOption(args, "--progress");
        string state = Program.RequireOption(args, "--state");
        VfxDevelopmentAgentTaskDocument task = VfxCreativeFlywheelJson.LoadFile<VfxDevelopmentAgentTaskDocument>(taskPath);
        VfxDevelopmentAgentProgressDocument progress = VfxCreativeFlywheelJson.LoadFile<VfxDevelopmentAgentProgressDocument>(progressPath);
        VfxDevelopmentAgentValidationReport current = VfxDevelopmentAgentTaskValidator.ValidateProgress(task, progress);
        if (!current.Valid) { Console.WriteLine(JsonSerializer.Serialize(current, jsonOptions)); return AuthoringExitCodes.ValidationBlocked; }
        var probe = new VfxDevelopmentAgentProgressDocument { TaskId = task.TaskId, TaskHash = task.TaskHash, State = state };
        probe.ProgressHash = VfxCreativeFlywheelHash.Compute(probe, "progressHash");
        VfxDevelopmentAgentValidationReport next = VfxDevelopmentAgentTaskValidator.ValidateProgress(task, probe);
        if (!next.Valid) { Console.WriteLine(JsonSerializer.Serialize(next, jsonOptions)); return AuthoringExitCodes.ValidationBlocked; }
        progress.State = state;
        progress.AgentId = Program.GetOption(args, "--agent-id", progress.AgentId);
        progress.CurrentCandidateId = Program.GetOption(args, "--candidate", string.Empty);
        if (int.TryParse(Program.GetOption(args, "--completed", progress.CompletedCandidates.ToString()), out int completed)) progress.CompletedCandidates = Math.Clamp(completed, 0, 6);
        progress.Message = Program.GetOption(args, "--message", progress.Message);
        progress.Sequence++;
        progress.ProgressHash = VfxCreativeFlywheelHash.Compute(progress, "progressHash");
        WriteText(Path.GetFullPath(progressPath), VfxCreativeFlywheelJson.Serialize(progress) + Environment.NewLine);
        Console.WriteLine(JsonSerializer.Serialize(new { progress.TaskId, progress.State, progress.Sequence, progress.ProgressHash }, jsonOptions));
        return AuthoringExitCodes.Ready;
    }

    private static int SealAgentTaskReceipt(string[] args, JsonSerializerOptions jsonOptions)
    {
        string repositoryRoot = Path.GetFullPath(Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory()));
        string taskPath = Program.RequireOption(args, "--task");
        string receiptPath = Program.RequireOption(args, "--receipt");
        VfxAuthoringProjectScanResult scan = VfxAuthoringProjectScanner.Scan(repositoryRoot, CreateOptions(args));
        VfxGenerationTemplateCatalogDocument templates = VfxCreativeFlywheelJson.LoadFile<VfxGenerationTemplateCatalogDocument>(ResolveTemplatesPath(args, repositoryRoot));
        VfxDevelopmentAgentTaskDocument task = VfxCreativeFlywheelJson.LoadFile<VfxDevelopmentAgentTaskDocument>(taskPath);
        VfxDevelopmentAgentReceiptDocument receipt = VfxCreativeFlywheelJson.LoadFile<VfxDevelopmentAgentReceiptDocument>(receiptPath);
        receipt.ReceiptHash = VfxCreativeFlywheelHash.Compute(receipt, "receiptHash", "diagnostics");
        VfxDevelopmentAgentValidationReport report = VfxDevelopmentAgentTaskValidator.ValidateReceipt(task, receipt, CreateAgentTaskValidationContext(repositoryRoot, scan.CatalogHash, templates));
        if (!report.Valid)
        {
            Console.WriteLine(JsonSerializer.Serialize(report, jsonOptions));
            return AuthoringExitCodes.ValidationBlocked;
        }
        WriteText(Path.GetFullPath(receiptPath), VfxCreativeFlywheelJson.Serialize(receipt) + Environment.NewLine);
        Console.WriteLine(JsonSerializer.Serialize(new { receipt.TaskId, receipt.FinalState, receipt.ReceiptHash, candidateCount = receipt.Candidates.Count }, jsonOptions));
        return AuthoringExitCodes.Ready;
    }

    private static int RequestAgentTaskCancel(string[] args, JsonSerializerOptions jsonOptions)
    {
        string taskPath = Program.RequireOption(args, "--task");
        string progressPath = Program.RequireOption(args, "--progress");
        VfxDevelopmentAgentTaskDocument task = VfxCreativeFlywheelJson.LoadFile<VfxDevelopmentAgentTaskDocument>(taskPath);
        VfxDevelopmentAgentProgressDocument progress = VfxCreativeFlywheelJson.LoadFile<VfxDevelopmentAgentProgressDocument>(progressPath);
        VfxDevelopmentAgentValidationReport report = VfxDevelopmentAgentTaskValidator.ValidateProgress(task, progress);
        if (!report.Valid)
        {
            Console.WriteLine(JsonSerializer.Serialize(report, jsonOptions));
            return AuthoringExitCodes.ValidationBlocked;
        }
        if (progress.State is VfxDevelopmentAgentTaskStates.Completed or VfxDevelopmentAgentTaskStates.Blocked or VfxDevelopmentAgentTaskStates.Cancelled)
            throw new ArgumentException("Completed, blocked, or cancelled Agent tasks cannot receive a cancellation request.");
        progress.State = VfxDevelopmentAgentTaskStates.CancelRequested;
        progress.Sequence++;
        progress.Message = "User requested cooperative cancellation from the Unity VFX Creation Lab.";
        progress.ProgressHash = VfxCreativeFlywheelHash.Compute(progress, "progressHash");
        WriteText(Path.GetFullPath(progressPath), VfxCreativeFlywheelJson.Serialize(progress) + Environment.NewLine);
        Console.WriteLine(JsonSerializer.Serialize(new { progress.TaskId, progress.State, progress.Sequence, progress.ProgressHash }, jsonOptions));
        return AuthoringExitCodes.Ready;
    }

    private static int CreateAgentTask(string[] args, JsonSerializerOptions jsonOptions)
    {
        string repositoryRoot = Path.GetFullPath(Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory()));
        string taskId = Program.RequireOption(args, "--task-id");
        VfxAuthoringProjectScanResult scan = VfxAuthoringProjectScanner.Scan(repositoryRoot, CreateOptions(args));
        if (scan.Catalog == null || scan.Validation.HasErrors)
        {
            Console.WriteLine(JsonSerializer.Serialize(scan.Report, jsonOptions));
            return AuthoringExitCodes.ValidationBlocked;
        }
        VfxCreativeBrief brief = VfxCreativeFlywheelJson.LoadFile<VfxCreativeBrief>(Program.RequireOption(args, "--brief"));
        VfxGenerationTemplateCatalogDocument templates = VfxCreativeFlywheelJson.LoadFile<VfxGenerationTemplateCatalogDocument>(ResolveTemplatesPath(args, repositoryRoot));
        string templateHash = VfxGenerationTemplateCatalog.ComputeHash(templates);
        VfxReferencePackLocalRegistration referenceRegistration = VfxCreativeFlywheelJson.LoadFile<VfxReferencePackLocalRegistration>(ResolveReferenceRegistrationPath(repositoryRoot, args));
        VfxReferencePackManifest referencePack = LoadRegisteredReferenceManifest(repositoryRoot, referenceRegistration);
        List<VfxAgentAssetInventoryItem> inventory = VfxDevelopmentAgentAssetInventory.Build(repositoryRoot, referencePack);
        VfxReuseDecision reuse = VfxReusePlanner.Plan(scan.Catalog, brief, scan.CatalogHash);
        string targetEffectId = Program.GetOption(args, "--target-effect", "vfx.magic.fireball");
        VfxAuthoringEffect targetEffect = null;
        for (int i = 0; i < scan.Catalog.Effects.Count; i++) if (string.Equals(scan.Catalog.Effects[i]?.EffectId, targetEffectId, StringComparison.Ordinal)) { targetEffect = scan.Catalog.Effects[i]; break; }
        VfxDevelopmentAgentTaskDocument task = VfxDevelopmentAgentTaskPlanner.Plan(taskId, brief, scan.CatalogHash, templateHash, inventory, reuse, targetEffectId, VfxDevelopmentAgentTaskPlanner.ComputeTargetContractHash(targetEffect), referencePack);
        string outputDirectory = ResolveAgentTaskDirectory(repositoryRoot, taskId, Program.GetOption(args, "--output-dir", string.Empty));
        Directory.CreateDirectory(outputDirectory);
        string taskPath = Path.Combine(outputDirectory, "task.json");
        string progressPath = Path.Combine(outputDirectory, "progress.json");
        var progress = new VfxDevelopmentAgentProgressDocument
        {
            TaskId = task.TaskId,
            TaskHash = task.TaskHash,
            State = VfxDevelopmentAgentTaskStates.Created,
            Sequence = 0,
            Message = "Development Agent task package created; no Unity asset has been modified."
        };
        progress.ProgressHash = VfxCreativeFlywheelHash.Compute(progress, "progressHash");
        WriteText(taskPath, VfxCreativeFlywheelJson.Serialize(task) + Environment.NewLine);
        WriteText(progressPath, VfxCreativeFlywheelJson.Serialize(progress) + Environment.NewLine);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            taskPath = ProjectRelative(repositoryRoot, taskPath),
            progressPath = ProjectRelative(repositoryRoot, progressPath),
            task.TaskHash,
            task.AssetInventoryHash,
            candidateCount = task.Candidates.Count,
            task.Blocked,
            task.Diagnostics
        }, jsonOptions));
        return task.Blocked ? AuthoringExitCodes.ValidationBlocked : AuthoringExitCodes.Ready;
    }

    private static int ValidateAgentTask(string[] args, JsonSerializerOptions jsonOptions)
    {
        string repositoryRoot = Path.GetFullPath(Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory()));
        VfxAuthoringProjectScanResult scan = VfxAuthoringProjectScanner.Scan(repositoryRoot, CreateOptions(args));
        VfxGenerationTemplateCatalogDocument templates = VfxCreativeFlywheelJson.LoadFile<VfxGenerationTemplateCatalogDocument>(ResolveTemplatesPath(args, repositoryRoot));
        VfxDevelopmentAgentTaskDocument task = VfxCreativeFlywheelJson.LoadFile<VfxDevelopmentAgentTaskDocument>(Program.RequireOption(args, "--task"));
        VfxDevelopmentAgentValidationReport report = VfxDevelopmentAgentTaskValidator.ValidateTask(task, CreateAgentTaskValidationContext(repositoryRoot, scan.CatalogHash, templates));
        string progressPath = Program.GetOption(args, "--progress", string.Empty);
        if (!string.IsNullOrWhiteSpace(progressPath))
        {
            VfxDevelopmentAgentValidationReport progressReport = VfxDevelopmentAgentTaskValidator.ValidateProgress(task, VfxCreativeFlywheelJson.LoadFile<VfxDevelopmentAgentProgressDocument>(progressPath));
            report.Diagnostics.AddRange(progressReport.Diagnostics);
            report.Valid = report.Valid && progressReport.Valid;
        }
        EmitPlan(args, JsonSerializer.Serialize(report, jsonOptions));
        return report.Valid ? AuthoringExitCodes.Ready : AuthoringExitCodes.ValidationBlocked;
    }

    private static int ValidateAgentTaskReceipt(string[] args, JsonSerializerOptions jsonOptions)
    {
        string repositoryRoot = Path.GetFullPath(Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory()));
        VfxAuthoringProjectScanResult scan = VfxAuthoringProjectScanner.Scan(repositoryRoot, CreateOptions(args));
        VfxGenerationTemplateCatalogDocument templates = VfxCreativeFlywheelJson.LoadFile<VfxGenerationTemplateCatalogDocument>(ResolveTemplatesPath(args, repositoryRoot));
        VfxDevelopmentAgentTaskDocument task = VfxCreativeFlywheelJson.LoadFile<VfxDevelopmentAgentTaskDocument>(Program.RequireOption(args, "--task"));
        VfxDevelopmentAgentReceiptDocument receipt = VfxCreativeFlywheelJson.LoadFile<VfxDevelopmentAgentReceiptDocument>(Program.RequireOption(args, "--receipt"));
        VfxDevelopmentAgentValidationContext context = CreateAgentTaskValidationContext(repositoryRoot, scan.CatalogHash, templates);
        context.VerifyAssetFiles = !Program.HasFlag(args, "--skip-file-check");
        VfxDevelopmentAgentValidationReport report = VfxDevelopmentAgentTaskValidator.ValidateReceipt(task, receipt, context);
        EmitPlan(args, JsonSerializer.Serialize(report, jsonOptions));
        return report.Valid ? AuthoringExitCodes.Ready : AuthoringExitCodes.ValidationBlocked;
    }

    private static VfxDevelopmentAgentValidationContext CreateAgentTaskValidationContext(string repositoryRoot, string catalogHash, VfxGenerationTemplateCatalogDocument templates)
    {
        VfxReferencePackLocalRegistration registration = VfxCreativeFlywheelJson.LoadFile<VfxReferencePackLocalRegistration>(ResolveReferenceRegistrationPath(repositoryRoot, Array.Empty<string>()));
        VfxReferencePackManifest referencePack = LoadRegisteredReferenceManifest(repositoryRoot, registration);
        List<VfxAgentAssetInventoryItem> inventory = VfxDevelopmentAgentAssetInventory.Build(repositoryRoot, referencePack);
        return new VfxDevelopmentAgentValidationContext
        {
            RepositoryRoot = repositoryRoot,
            ActualCatalogHash = catalogHash,
            ActualGenerationTemplateCatalogHash = VfxGenerationTemplateCatalog.ComputeHash(templates),
            ActualAssetInventoryHash = VfxDevelopmentAgentAssetInventory.ComputeHash(inventory),
            ActualReferencePackHash = referencePack?.ManifestHash ?? string.Empty,
            ActualReferencePackUsagePolicy = referencePack?.UsagePolicy ?? VfxReferenceUsagePolicies.ReferenceOnly,
            ActualReferenceSourcePackageId = referencePack?.SourcePackageId ?? string.Empty,
            ActualReferenceLicenseId = referencePack?.LicenseId ?? string.Empty,
            ActualReferencePublicRedistributionAllowed = referencePack?.PublicRedistributionAllowed == true,
            VerifyAssetFiles = true
        };
    }

    private static string ResolveReferenceRegistrationPath(string repositoryRoot, string[] args)
    {
        string requested = Program.GetOption(args, "--reference-registration", "Temp/MxFramework/VfxReferencePacks/local-registration.json");
        return ResolveProjectPath(repositoryRoot, requested);
    }

    private static VfxReferencePackManifest LoadRegisteredReferenceManifest(string repositoryRoot, VfxReferencePackLocalRegistration registration)
    {
        if (registration == null ||
            !(string.Equals(registration.SchemaVersion, VfxReferencePackSchemas.LocalRegistrationV1, StringComparison.Ordinal) || string.Equals(registration.SchemaVersion, VfxReferencePackSchemas.LocalRegistrationV2, StringComparison.Ordinal)) ||
            !string.Equals(registration.ReferencePackId, VfxReferencePackIds.PilotoArpgRealisticDemonVsAngels, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(registration.ManifestPath)) return null;
        return VfxCreativeFlywheelJson.LoadFile<VfxReferencePackManifest>(ResolveProjectPath(repositoryRoot, registration.ManifestPath));
    }

    private static string ResolveAgentTaskDirectory(string repositoryRoot, string taskId, string requested)
    {
        string relative = string.IsNullOrWhiteSpace(requested) ? "Temp/MxFramework/VfxAgentTasks/" + taskId : requested.Replace('\\', '/').TrimEnd('/');
        string expected = "Temp/MxFramework/VfxAgentTasks/" + taskId;
        if (!string.Equals(relative, expected, StringComparison.Ordinal) || Path.IsPathRooted(relative) || relative.Contains(".."))
            throw new ArgumentException("VFX Agent task output must equal " + expected);
        return ResolveProjectPath(repositoryRoot, relative);
    }

    private static string ProjectRelative(string repositoryRoot, string path)
    {
        string prefix = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).Substring(prefix.Length).Replace('\\', '/');
    }

    private static int Search(string[] args, JsonSerializerOptions jsonOptions)
    {
        VfxAuthoringProjectScanResult scan = VfxAuthoringProjectScanner.Scan(Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory()), CreateOptions(args));
        if (scan.Catalog == null)
            return AuthoringExitCodes.ValidationBlocked;
        var query = new VfxAuthoringSearchQuery
        {
            Text = Program.GetOption(args, "--text", string.Empty),
            Role = Program.GetOption(args, "--role", string.Empty),
            Theme = Program.GetOption(args, "--theme", string.Empty),
            Anchor = Program.GetOption(args, "--anchor", string.Empty),
            Backend = Program.GetOption(args, "--backend", string.Empty),
            ReviewStatus = Program.GetOption(args, "--review", string.Empty)
        };
        int limit = int.TryParse(Program.GetOption(args, "--limit", "20"), out int parsed) ? parsed : 20;
        Console.WriteLine(JsonSerializer.Serialize(VfxAuthoringSearchService.Search(scan.Catalog, query, limit), jsonOptions));
        return AuthoringExitCodes.Ready;
    }

    private static int Impact(string[] args, JsonSerializerOptions jsonOptions)
    {
        VfxAuthoringProjectScanResult scan = VfxAuthoringProjectScanner.Scan(Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory()), CreateOptions(args));
        if (scan.Catalog == null)
            return AuthoringExitCodes.ValidationBlocked;
        VfxAuthoringImpactReport report = VfxAuthoringSearchService.AnalyzeImpact(scan.Catalog, Program.RequireOption(args, "--id"));
        Console.WriteLine(JsonSerializer.Serialize(report, jsonOptions));
        return report.Found ? AuthoringExitCodes.Ready : AuthoringExitCodes.Warning;
    }

    private static int ProposeCoverage(string[] args)
    {
        string repositoryRoot = Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory());
        string outputPath = Program.RequireOption(args, "--output");
        VfxAuthoringProjectScanResult scan = VfxAuthoringProjectScanner.Scan(repositoryRoot, CreateOptions(args));
        if (scan.Catalog == null || scan.Validation.HasErrors)
            return AuthoringExitCodes.ValidationBlocked;
        bool enableStrict = Program.HasFlag(args, "--enable-strict");
        VfxAuthoringCatalogDocument proposal = VfxAuthoringCoverageImporter.CreateProposal(scan, enableStrict);
        WriteText(outputPath, VfxAuthoringCatalogJson.Serialize(proposal));
        Console.WriteLine("vfxCoverageProposal=" + Path.GetFullPath(outputPath));
        Console.WriteLine("effectCount=" + proposal.Effects.Count);
        Console.WriteLine("strictCoverage=" + proposal.StrictCoverage.ToString().ToLowerInvariant());
        return AuthoringExitCodes.Ready;
    }

    private static int PlanBake(string[] args, JsonSerializerOptions jsonOptions)
    {
        string repositoryRoot = Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory());
        string effectId = Program.RequireOption(args, "--effect");
        VfxAuthoringProjectScanResult scan = VfxAuthoringProjectScanner.Scan(repositoryRoot, CreateOptions(args));
        if (scan.Catalog == null)
        {
            Console.WriteLine(JsonSerializer.Serialize(scan.Report, jsonOptions));
            return AuthoringExitCodes.ValidationBlocked;
        }
        VfxAuthoringBakePlan plan = VfxAuthoringBakePlanner.Plan(scan.Catalog, effectId, scan.Evidence);
        Console.WriteLine(JsonSerializer.Serialize(plan, jsonOptions));
        return plan.Blocked ? AuthoringExitCodes.ValidationBlocked : AuthoringExitCodes.Ready;
    }

    private static int Validate(string[] args, JsonSerializerOptions jsonOptions)
    {
        string repositoryRoot = Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory());
        VfxAuthoringProjectScanOptions options = CreateOptions(args);
        VfxAuthoringProjectScanResult result = VfxAuthoringProjectScanner.Scan(repositoryRoot, options);

        string jsonReportPath = Program.GetOption(args, "--json-report", string.Empty);
        string markdownReportPath = Program.GetOption(args, "--markdown-report", string.Empty);
        if (!string.IsNullOrWhiteSpace(jsonReportPath))
            WriteText(jsonReportPath, JsonSerializer.Serialize(result.Report, jsonOptions) + Environment.NewLine);
        if (!string.IsNullOrWhiteSpace(markdownReportPath))
            WriteText(markdownReportPath, EnsureTrailingNewline(result.Report.MarkdownReport));

        Console.Write(result.Report.MarkdownReport);
        if (result.Validation.HasErrors)
            return AuthoringExitCodes.ValidationBlocked;
        if (Program.HasFlag(args, "--fail-on-warning") && result.Report.WarningCount > 0)
            return AuthoringExitCodes.Warning;
        return AuthoringExitCodes.Ready;
    }

    private static int PlanSave(string[] args, JsonSerializerOptions jsonOptions)
    {
        string repositoryRoot = Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory());
        string proposedPath = Program.RequireOption(args, "--proposed");
        string expectedHash = Program.RequireOption(args, "--expected-hash");
        VfxAuthoringCatalogLoadResult load = VfxAuthoringCatalogJson.LoadFile(Path.GetFullPath(proposedPath));
        if (!load.Success || load.Catalog == null)
        {
            Console.WriteLine(JsonSerializer.Serialize(load, jsonOptions));
            return AuthoringExitCodes.ValidationBlocked;
        }

        VfxAuthoringOperationPlan plan = VfxAuthoringOperationPlanner.PlanSaveCatalog(repositoryRoot, expectedHash, load.Catalog, CreateOptions(args));
        Console.WriteLine(JsonSerializer.Serialize(plan, jsonOptions));
        return plan.Blocked ? AuthoringExitCodes.ValidationBlocked : AuthoringExitCodes.Ready;
    }

    private static int PlanCreate(string[] args, JsonSerializerOptions jsonOptions)
    {
        string repositoryRoot = Path.GetFullPath(Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory()));
        VfxAuthoringProjectScanResult scan = VfxAuthoringProjectScanner.Scan(repositoryRoot, CreateOptions(args));
        if (scan.Catalog == null || scan.Validation.HasErrors)
        {
            EmitPlan(args, JsonSerializer.Serialize(scan.Report, jsonOptions));
            return AuthoringExitCodes.ValidationBlocked;
        }
        VfxCreativeBrief brief = VfxCreativeFlywheelJson.LoadFile<VfxCreativeBrief>(Program.RequireOption(args, "--brief"));
        VfxGenerationTemplateCatalogDocument templates = VfxCreativeFlywheelJson.LoadFile<VfxGenerationTemplateCatalogDocument>(ResolveTemplatesPath(args, repositoryRoot));
        string templateHash = VfxGenerationTemplateCatalog.ComputeHash(templates);
        VfxGenerationSessionCapabilities capabilities = LoadExternalSessionCapabilities(args, repositoryRoot);
        VfxReuseDecision reuse = VfxReusePlanner.Plan(scan.Catalog, brief, scan.CatalogHash);
        VfxGenerationPlan generation = VfxGenerationPlanner.Plan(brief, reuse, scan.Catalog, templates, scan.CatalogHash, templateHash, capabilities);
        var bundle = new VfxCreativePlanBundle { ReuseDecision = reuse, GenerationPlan = generation };
        EmitPlan(args, JsonSerializer.Serialize(bundle, jsonOptions));
        return reuse.Blocked || generation.Blocked ? AuthoringExitCodes.ValidationBlocked : AuthoringExitCodes.Ready;
    }

    private static VfxGenerationSessionCapabilities LoadExternalSessionCapabilities(string[] args, string repositoryRoot)
    {
        string value = Program.GetOption(args, "--capabilities", string.Empty);
        if (string.IsNullOrWhiteSpace(value)) return null;
        string fullPath = Path.GetFullPath(value);
        string root = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string rootPrefix = root + Path.DirectorySeparatorChar;
        if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase) || fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new VfxGenerationSessionCapabilities
            {
                SchemaVersion = "invalid.repository-local-capability",
                CapabilityId = "repository-local-capability"
            };
        }
        return VfxCreativeFlywheelJson.LoadFile<VfxGenerationSessionCapabilities>(fullPath);
    }

    private static int ValidateReceipt(string[] args, JsonSerializerOptions jsonOptions)
    {
        string repositoryRoot = Path.GetFullPath(Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory()));
        VfxAuthoringProjectScanResult scan = VfxAuthoringProjectScanner.Scan(repositoryRoot, CreateOptions(args));
        string planPath = Program.RequireOption(args, "--plan");
        string receiptPath = Program.RequireOption(args, "--receipt");
        string planJson = File.ReadAllText(planPath);
        string receiptJson = File.ReadAllText(receiptPath);
        string schema = ReadSchema(receiptJson);
        VfxReceiptValidationReport report;
        if (string.Equals(schema, VfxCreativeFlywheelSchemaVersions.CandidateReceiptV1, StringComparison.Ordinal))
        {
            VfxGenerationTemplateCatalogDocument templates = VfxCreativeFlywheelJson.LoadFile<VfxGenerationTemplateCatalogDocument>(ResolveTemplatesPath(args, repositoryRoot));
            var context = CreateReceiptContext(args, repositoryRoot, scan.CatalogHash, templates);
            report = VfxReceiptValidator.ValidateCandidateReceipt(VfxCreativeFlywheelJson.Deserialize<VfxGenerationPlan>(planJson), VfxCreativeFlywheelJson.Deserialize<VfxCandidateGenerationReceipt>(receiptJson), context);
        }
        else if (string.Equals(schema, VfxCreativeFlywheelSchemaVersions.PromotionReceiptV1, StringComparison.Ordinal))
        {
            var context = new VfxReceiptValidationContext
            {
                RepositoryRoot = repositoryRoot,
                ActualCatalogHash = scan.CatalogHash,
                VerifyAssetFiles = !Program.HasFlag(args, "--skip-file-check")
            };
            report = VfxReceiptValidator.ValidatePromotionReceipt(VfxCreativeFlywheelJson.Deserialize<VfxPromotionPlan>(planJson), VfxCreativeFlywheelJson.Deserialize<VfxPromotionReceipt>(receiptJson), context);
        }
        else
        {
            throw new ArgumentException("Unsupported receipt schemaVersion: " + schema);
        }
        EmitPlan(args, JsonSerializer.Serialize(report, jsonOptions));
        return report.Valid ? AuthoringExitCodes.Ready : AuthoringExitCodes.ValidationBlocked;
    }

    private static int EvaluateCandidates(string[] args, JsonSerializerOptions jsonOptions)
    {
        string repositoryRoot = Path.GetFullPath(Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory()));
        VfxAuthoringProjectScanResult scan = VfxAuthoringProjectScanner.Scan(repositoryRoot, CreateOptions(args));
        VfxGenerationPlan plan = VfxCreativeFlywheelJson.LoadFile<VfxGenerationPlan>(Program.RequireOption(args, "--plan"));
        VfxCandidateGenerationReceipt receipt = VfxCreativeFlywheelJson.LoadFile<VfxCandidateGenerationReceipt>(Program.RequireOption(args, "--receipt"));
        VfxCandidateEvaluationInputDocument input = VfxCreativeFlywheelJson.LoadFile<VfxCandidateEvaluationInputDocument>(Program.RequireOption(args, "--input"));
        VfxGenerationTemplateCatalogDocument templates = VfxCreativeFlywheelJson.LoadFile<VfxGenerationTemplateCatalogDocument>(ResolveTemplatesPath(args, repositoryRoot));
        VfxReceiptValidationReport receiptValidation = VfxReceiptValidator.ValidateCandidateReceipt(plan, receipt, CreateReceiptContext(args, repositoryRoot, scan.CatalogHash, templates));
        if (!receiptValidation.Valid)
        {
            EmitPlan(args, JsonSerializer.Serialize(receiptValidation, jsonOptions));
            return AuthoringExitCodes.ValidationBlocked;
        }
        VfxQualityProfile profile = FindQualityProfile(templates, input.QualityProfileId, input.QualityProfileVersion);
        if (profile == null)
            throw new ArgumentException("Quality Profile id/version is missing from generation templates.");
        VfxEvaluationReport report = VfxVisualEvaluationService.Evaluate(input, plan, receipt, profile);
        EmitPlan(args, JsonSerializer.Serialize(report, jsonOptions));
        return report.Blocked ? AuthoringExitCodes.ValidationBlocked : AuthoringExitCodes.Ready;
    }

    private static int PlanPromote(string[] args, JsonSerializerOptions jsonOptions)
    {
        string repositoryRoot = Path.GetFullPath(Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory()));
        VfxAuthoringProjectScanResult scan = VfxAuthoringProjectScanner.Scan(repositoryRoot, CreateOptions(args));
        VfxGenerationPlan generation = VfxCreativeFlywheelJson.LoadFile<VfxGenerationPlan>(Program.RequireOption(args, "--plan"));
        VfxCandidateGenerationReceipt receipt = VfxCreativeFlywheelJson.LoadFile<VfxCandidateGenerationReceipt>(Program.RequireOption(args, "--receipt"));
        VfxEvaluationReport evaluation = VfxCreativeFlywheelJson.LoadFile<VfxEvaluationReport>(Program.RequireOption(args, "--evaluation"));
        VfxPromotionRequest request = VfxCreativeFlywheelJson.LoadFile<VfxPromotionRequest>(Program.RequireOption(args, "--request"));
        VfxGenerationTemplateCatalogDocument templates = VfxCreativeFlywheelJson.LoadFile<VfxGenerationTemplateCatalogDocument>(ResolveTemplatesPath(args, repositoryRoot));
        ISet<string> existing = EnumerateProjectAssets(repositoryRoot, "Assets/VFX");
        VfxPromotionPlan plan = VfxPromotionPlanner.Plan(request, generation, receipt, evaluation, CreateReceiptContext(args, repositoryRoot, scan.CatalogHash, templates), existing);
        EmitPlan(args, JsonSerializer.Serialize(plan, jsonOptions));
        return plan.Blocked ? AuthoringExitCodes.ValidationBlocked : AuthoringExitCodes.Ready;
    }

    private static int PlanRegisterPublished(string[] args, JsonSerializerOptions jsonOptions)
    {
        string repositoryRoot = Path.GetFullPath(Program.GetOption(args, "--project-root", Directory.GetCurrentDirectory()));
        VfxAuthoringProjectScanOptions options = CreateOptions(args);
        VfxAuthoringProjectScanResult scan = VfxAuthoringProjectScanner.Scan(repositoryRoot, options);
        if (scan.Catalog == null)
        {
            Console.WriteLine(JsonSerializer.Serialize(scan.Report, jsonOptions));
            return AuthoringExitCodes.ValidationBlocked;
        }
        VfxPublishedRegistrationRequest request = VfxCreativeFlywheelJson.LoadFile<VfxPublishedRegistrationRequest>(Program.RequireOption(args, "--request"));
        VfxPromotionPlan promotionPlan = VfxCreativeFlywheelJson.LoadFile<VfxPromotionPlan>(Program.RequireOption(args, "--promotion-plan"));
        VfxPromotionReceipt promotionReceipt = VfxCreativeFlywheelJson.LoadFile<VfxPromotionReceipt>(Program.RequireOption(args, "--promotion-receipt"));
        var context = new VfxReceiptValidationContext { RepositoryRoot = repositoryRoot, ActualCatalogHash = scan.CatalogHash, VerifyAssetFiles = !Program.HasFlag(args, "--skip-file-check") };
        VfxReceiptValidationReport receiptValidation = VfxReceiptValidator.ValidatePromotionReceipt(promotionPlan, promotionReceipt, context);
        if (!receiptValidation.Valid)
        {
            EmitPlan(args, JsonSerializer.Serialize(receiptValidation, jsonOptions));
            return AuthoringExitCodes.ValidationBlocked;
        }
        string resourceHash = VfxAuthoringProjectScanner.ComputeFileHash(ResolveProjectPath(repositoryRoot, options.ResourceCatalogPath));
        string definitionHash = VfxAuthoringProjectScanner.ComputeFileHash(ResolveProjectPath(repositoryRoot, options.DefinitionCatalogPath));
        VfxPublishedRegistrationPlan plan = VfxPublishedRegistrationPlanner.Plan(request, promotionPlan, promotionReceipt, scan.Catalog, scan.CatalogHash, resourceHash, definitionHash);
        EmitPlan(args, JsonSerializer.Serialize(plan, jsonOptions));
        return plan.Blocked ? AuthoringExitCodes.ValidationBlocked : AuthoringExitCodes.Ready;
    }

    private static VfxReceiptValidationContext CreateReceiptContext(string[] args, string repositoryRoot, string catalogHash, VfxGenerationTemplateCatalogDocument templates)
    {
        return new VfxReceiptValidationContext
        {
            RepositoryRoot = repositoryRoot,
            ActualCatalogHash = catalogHash,
            ActualGenerationTemplateCatalogHash = VfxGenerationTemplateCatalog.ComputeHash(templates),
            VerifyAssetFiles = !Program.HasFlag(args, "--skip-file-check"),
            GenerationTemplates = templates
        };
    }

    private static string ResolveTemplatesPath(string[] args, string repositoryRoot)
    {
        string value = Program.GetOption(args, "--templates", "Assets/Config/MxFramework/Vfx/vfx_generation_templates.json");
        return Path.IsPathRooted(value) ? Path.GetFullPath(value) : ResolveProjectPath(repositoryRoot, value);
    }

    private static string ResolveProjectPath(string repositoryRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
            throw new ArgumentException("VFX project path must be project-relative: " + (path ?? string.Empty));
        string root = Path.GetFullPath(repositoryRoot);
        string full = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("VFX project path escapes repository root: " + path);
        return full;
    }

    private static string ReadSchema(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("schemaVersion", out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;
    }

    private static VfxQualityProfile FindQualityProfile(VfxGenerationTemplateCatalogDocument templates, string id, string version)
    {
        for (int i = 0; i < templates.QualityProfiles.Count; i++)
            if (templates.QualityProfiles[i] != null && string.Equals(templates.QualityProfiles[i].QualityProfileId, id, StringComparison.Ordinal) && string.Equals(templates.QualityProfiles[i].Version, version, StringComparison.Ordinal))
                return templates.QualityProfiles[i];
        return null;
    }

    private static ISet<string> EnumerateProjectAssets(string repositoryRoot, string assetRoot)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        string absoluteRoot = ResolveProjectPath(repositoryRoot, assetRoot);
        if (!Directory.Exists(absoluteRoot))
            return result;
        string rootPrefix = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (string file in Directory.EnumerateFiles(absoluteRoot, "*", SearchOption.AllDirectories))
            if (!file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                result.Add(file.Substring(rootPrefix.Length).Replace('\\', '/'));
        return result;
    }

    private static void EmitPlan(string[] args, string json)
    {
        string output = Program.GetOption(args, "--output", string.Empty);
        if (!string.IsNullOrWhiteSpace(output))
            WriteText(output, json + Environment.NewLine);
        Console.WriteLine(json);
    }

    private static VfxAuthoringProjectScanOptions CreateOptions(string[] args)
    {
        var options = new VfxAuthoringProjectScanOptions();
        options.CatalogPath = Program.GetOption(args, "--catalog", options.CatalogPath);
        options.ResourceCatalogPath = Program.GetOption(args, "--resource-catalog", options.ResourceCatalogPath);
        options.DefinitionCatalogPath = Program.GetOption(args, "--definitions", options.DefinitionCatalogPath);
        options.VfxAssetRoot = Program.GetOption(args, "--vfx-assets", options.VfxAssetRoot);
        options.PackageManifestPath = Program.GetOption(args, "--package-manifest", options.PackageManifestPath);
        options.BakeEvidenceRoot = Program.GetOption(args, "--bake-evidence", options.BakeEvidenceRoot);
        options.ThumbnailManifestPath = Program.GetOption(args, "--thumbnail-manifest", options.ThumbnailManifestPath);
        return options;
    }

    private static void WriteText(string path, string content)
    {
        string absolutePath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(absolutePath, content, new UTF8Encoding(false));
    }

    private static string EnsureTrailingNewline(string value)
    {
        string text = value ?? string.Empty;
        return text.EndsWith("\n", StringComparison.Ordinal) ? text : text + Environment.NewLine;
    }
}
