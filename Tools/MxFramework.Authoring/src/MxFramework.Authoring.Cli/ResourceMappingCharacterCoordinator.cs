using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.ResourceMapping.Authoring;

namespace MxFramework.Authoring.Cli;

internal sealed class ResourceMappingCharacterSyncResult
{
    public bool Success { get; set; }
    public bool Changed { get; set; }
    public string ProfilePath { get; set; } = string.Empty;
    public string ProducerId { get; set; } = string.Empty;
    public string PreviousRevision { get; set; } = string.Empty;
    public string AcceptedRevision { get; set; } = string.Empty;
    public string MappingInputFingerprint { get; set; } = string.Empty;
    public string BuildPlanFingerprint { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<ResourceMappingDiagnostic> Diagnostics { get; set; } = new();
    public List<CharacterAuthoringValidationIssue> CompileDiagnostics { get; set; } = new();
}

internal static class ResourceMappingCharacterCoordinator
{
    private const string ProfileRelativePath = "Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json";

    public static ResourceMappingCharacterSyncResult ApplyFromCompileResult(
        string projectRoot,
        CharacterAuthoringCompileResult compileResult,
        string buildTarget = "")
    {
        string root = Path.GetFullPath(projectRoot ?? Directory.GetCurrentDirectory());
        string profilePath = Path.Combine(root, ProfileRelativePath);
        var result = new ResourceMappingCharacterSyncResult
        {
            ProfilePath = ProfileRelativePath,
            ProducerId = compileResult?.ResourceMappingV2Snapshot?.ProducerId ?? string.Empty
        };
        if (!File.Exists(profilePath))
        {
            return Failure(result, "RESOURCE_MAPPING_PROFILE_MISSING", "Production resource mapping Profile is not present in this project root.");
        }
        if (compileResult == null || compileResult.ResourceMappingV2Snapshot == null)
            return Failure(result, "RESOURCE_MAPPING_CHARACTER_SNAPSHOT_MISSING", "Character compiler did not produce a v2 snapshot.");
        if (compileResult.GateReport == null)
            return Failure(result, "RESOURCE_MAPPING_CHARACTER_COMPILE_GATE_MISSING", "Character compiler did not produce a gate report.");
        if (compileResult.GateReport.Issues != null)
            result.CompileDiagnostics.AddRange(compileResult.GateReport.Issues);
        if (compileResult.GateReport.ExportBlocked || compileResult.GateReport.ImportBlocked)
            return Failure(result, "RESOURCE_MAPPING_CHARACTER_COMPILE_BLOCKED", "Character compiler blocked export or Unity import; resource mapping was not published.");
        try { buildTarget = ResourceMappingBuildTargetResolver.Resolve(root, buildTarget); }
        catch (InvalidOperationException ex) { return Failure(result, "RESOURCE_MAPPING_BUILD_TARGET_REQUIRED", ex.Message); }
        if (compileResult.ResourceMappingV2Diagnostics != null)
            result.Diagnostics.AddRange(compileResult.ResourceMappingV2Diagnostics);
        if (HasErrors(result.Diagnostics))
            return Failure(result, "RESOURCE_MAPPING_CHARACTER_MATERIALIZATION_FAILED", "Character v2 materialization has blocking diagnostics.");

        var fileAggregator = new ResourceMappingProfileFileAggregator();
        ResourceMappingProfileFileState state = fileAggregator.Read(profilePath);
        result.Diagnostics.AddRange(state.Diagnostics);
        if (!state.Success)
            return Failure(result, "RESOURCE_MAPPING_PROFILE_READ_FAILED", "Production resource mapping Profile could not be read.");

        ResourceMappingProducerSnapshotResult policyResult = new ResourceMappingProfilePolicyProducerAdapter().BuildSnapshot(root);
        if (!policyResult.Success || policyResult.Snapshot == null)
            return Failure(result, "RESOURCE_MAPPING_POLICY_MATERIALIZATION_FAILED", "Central resource mapping policy could not be rebuilt.");
        ResourceMappingProducerSnapshot policy = policyResult.Snapshot;
        ResourceMappingProfileFileAggregationResult applied = fileAggregator.ApplyCompleteSnapshots(
            profilePath,
            state.Revision,
            new[] { policy, compileResult.ResourceMappingV2Snapshot },
            buildTarget);
        result.Success = applied.Success;
        result.Changed = applied.Changed;
        result.PreviousRevision = applied.PreviousRevision;
        result.AcceptedRevision = applied.AcceptedRevision;
        result.ErrorCode = applied.ErrorCode;
        result.Message = applied.Message;
        result.Diagnostics.AddRange(applied.Diagnostics);
        if (applied.Compilation != null)
        {
            result.MappingInputFingerprint = applied.Compilation.Plan.MappingInputFingerprint;
            result.BuildPlanFingerprint = applied.Compilation.Plan.BuildPlanFingerprint;
        }
        return result;
    }

    public static int SyncCommand(string[] args, JsonSerializerOptions options)
    {
        string projectRoot = Program.GetOption(args, "--root", Directory.GetCurrentDirectory());
        string packagePath = Program.RequireOption(args, "--package");
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, options);
        CharacterAuthoringCompileResult compileResult = CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest
        {
            Package = package,
            PackageRootPath = packagePath,
            Options = new CharacterAuthoringCompileOptions
            {
                GeneratedRootPath = Program.GetOption(args, "--unity-root", "Assets/MxFrameworkGenerated/CharacterPackages"),
                TargetUnityPathPolicy = CharacterPackageImportTargetPathPolicies.GeneratedCharacterPackage
            }
        });
        ResourceMappingCharacterSyncResult result = ApplyFromCompileResult(projectRoot, compileResult,
            Program.GetOption(args, "--build-target", string.Empty));
        Console.WriteLine(JsonSerializer.Serialize(result, options));
        return result.Success ? Program.ExitReady : Program.ExitValidationBlocked;
    }

    private static bool HasErrors(IList<ResourceMappingDiagnostic> diagnostics)
    {
        for (int i = 0; i < diagnostics.Count; i++)
            if (diagnostics[i].Severity == ResourceMappingDiagnosticSeverity.Error) return true;
        return false;
    }

    private static ResourceMappingCharacterSyncResult Failure(ResourceMappingCharacterSyncResult result, string code, string message)
    {
        result.Success = false;
        result.ErrorCode = code;
        result.Message = message;
        return result;
    }
}
