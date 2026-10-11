using System.Text.Json;
using MxFramework.ResourceMapping.Authoring;

namespace MxFramework.Authoring.Cli;

internal sealed class ResourceMappingFirstPartySyncResult
{
    public bool Success { get; set; }
    public bool Changed { get; set; }
    public int EntryCount { get; set; }
    public int InternalCount { get; set; }
    public int ExternalCount { get; set; }
    public int EditorOnlyCount { get; set; }
    public int ExcludedCount { get; set; }
    public int ProducerCount { get; set; }
    public int CatalogMountCount { get; set; }
    public string PreviousRevision { get; set; } = string.Empty;
    public string AcceptedRevision { get; set; } = string.Empty;
    public string MappingInputFingerprint { get; set; } = string.Empty;
    public string BuildPlanFingerprint { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<ResourceMappingDiagnostic> Diagnostics { get; set; } = new();
}

/// <summary>Runs all checked first-party producer adapters through the sole Profile file Aggregator.</summary>
internal static class ResourceMappingFirstPartyCoordinator
{
    private const string ProfileRelativePath = "Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json";

    /// <remarks>AllocByDesign. This is a low-frequency authoring synchronization operation.</remarks>
    public static ResourceMappingFirstPartySyncResult Apply(string projectRoot, JsonSerializerOptions options, string buildTarget = "")
    {
        return Apply(projectRoot, options, projectRoot, buildTarget);
    }

    internal static ResourceMappingFirstPartySyncResult Apply(string projectRoot, JsonSerializerOptions options, string producerSourceRoot, string buildTarget)
    {
        return Apply(projectRoot, producerSourceRoot, buildTarget, ResourceMappingFirstPartyProducerAdapters.CreateAll());
    }

    /// <remarks>AllocByDesign. Runs one named first-party owner as a complete producer snapshot.</remarks>
    public static ResourceMappingFirstPartySyncResult ApplyProducer(
        string projectRoot,
        JsonSerializerOptions options,
        string producerId,
        string buildTarget = "")
    {
        IReadOnlyList<IResourceMappingFirstPartyProducerAdapter> all = ResourceMappingFirstPartyProducerAdapters.CreateAll();
        for (int i = 0; i < all.Count; i++)
        {
            if (string.Equals(all[i].ProducerId, producerId, StringComparison.Ordinal))
                return Apply(projectRoot, projectRoot, buildTarget, new[] { all[i] });
        }
        return Failure(new ResourceMappingFirstPartySyncResult(), "RESOURCE_MAPPING_PRODUCER_UNKNOWN", "Unknown first-party producer: " + producerId + ".");
    }

    private static ResourceMappingFirstPartySyncResult Apply(
        string projectRoot,
        string producerSourceRoot,
        string buildTarget,
        IReadOnlyList<IResourceMappingFirstPartyProducerAdapter> adapters)
    {
        string root = Path.GetFullPath(projectRoot ?? Directory.GetCurrentDirectory());
        string sourceRoot = Path.GetFullPath(producerSourceRoot ?? root);
        string profilePath = Path.Combine(root, ProfileRelativePath);
        var result = new ResourceMappingFirstPartySyncResult();
        try { buildTarget = ResourceMappingBuildTargetResolver.Resolve(root, buildTarget); }
        catch (InvalidOperationException ex) { return Failure(result, "RESOURCE_MAPPING_BUILD_TARGET_REQUIRED", ex.Message); }
        if (!File.Exists(profilePath))
            return Failure(result, "RESOURCE_MAPPING_PROFILE_MISSING", "Production resource mapping Profile is missing.");

        var fileAggregator = new ResourceMappingProfileFileAggregator();
        ResourceMappingProfileFileState state = fileAggregator.Read(profilePath);
        result.Diagnostics.AddRange(state.Diagnostics);
        if (!state.Success)
            return Failure(result, "RESOURCE_MAPPING_PROFILE_READ_FAILED", "Production resource mapping Profile could not be read.");

        var snapshots = new List<ResourceMappingProducerSnapshot>();
        for (int i = 0; i < adapters.Count; i++)
        {
            ResourceMappingProducerSnapshotResult materialized = adapters[i].BuildSnapshot(sourceRoot, state.Profile);
            result.Diagnostics.AddRange(materialized.Diagnostics);
            if (!materialized.Success || materialized.Snapshot == null)
                return Failure(result, "RESOURCE_MAPPING_PRODUCER_MATERIALIZATION_FAILED", "First-party producer failed: " + adapters[i].ProducerId + ".");
            snapshots.Add(materialized.Snapshot);
            Count(materialized.Snapshot, result);
        }
        result.ProducerCount = adapters.Count;

        ResourceMappingProfileFileAggregationResult applied = fileAggregator.ApplyCompleteSnapshots(
            profilePath, state.Revision, snapshots, buildTarget);
        result.Success = applied.Success;
        result.Changed = applied.Changed;
        result.PreviousRevision = applied.PreviousRevision;
        result.AcceptedRevision = applied.AcceptedRevision;
        result.ErrorCode = applied.ErrorCode;
        result.Message = applied.Message;
        result.Diagnostics.AddRange(applied.Diagnostics);
        if (applied.Compilation != null)
        {
            result.EntryCount = applied.Compilation.Plan.Counts.Internal + applied.Compilation.Plan.Counts.External +
                applied.Compilation.Plan.Counts.EditorOnly + applied.Compilation.Plan.Counts.Excluded;
            result.InternalCount = applied.Compilation.Plan.Counts.Internal;
            result.ExternalCount = applied.Compilation.Plan.Counts.External;
            result.EditorOnlyCount = applied.Compilation.Plan.Counts.EditorOnly;
            result.ExcludedCount = applied.Compilation.Plan.Counts.Excluded;
            result.MappingInputFingerprint = applied.Compilation.Plan.MappingInputFingerprint;
            result.BuildPlanFingerprint = applied.Compilation.Plan.BuildPlanFingerprint;
            result.CatalogMountCount = applied.Compilation.Plan.CatalogMounts.Count;
        }
        return result;
    }

    public static int SyncCommand(string[] args, JsonSerializerOptions options)
    {
        string root = Program.GetOption(args, "--root", Directory.GetCurrentDirectory());
        string buildTarget = Program.GetOption(args, "--build-target", string.Empty);
        string producerId = Program.GetOption(args, "--producer", string.Empty);
        ResourceMappingFirstPartySyncResult result = string.IsNullOrWhiteSpace(producerId)
            ? Apply(root, options, buildTarget)
            : ApplyProducer(root, options, producerId, buildTarget);
        Console.WriteLine(JsonSerializer.Serialize(result, options));
        return result.Success ? Program.ExitReady : Program.ExitValidationBlocked;
    }

    private static void Count(ResourceMappingProducerSnapshot snapshot, ResourceMappingFirstPartySyncResult result)
    {
        for (int i = 0; i < snapshot.Entries.Count; i++)
        {
            result.EntryCount++;
            switch (snapshot.Entries[i].DeliveryIntent)
            {
                case ResourceMappingDeliveryIntent.Internal: result.InternalCount++; break;
                case ResourceMappingDeliveryIntent.External: result.ExternalCount++; break;
                case ResourceMappingDeliveryIntent.EditorOnly: result.EditorOnlyCount++; break;
                case ResourceMappingDeliveryIntent.Excluded: result.ExcludedCount++; break;
            }
        }
    }

    private static ResourceMappingFirstPartySyncResult Failure(ResourceMappingFirstPartySyncResult result, string code, string message)
    {
        result.Success = false;
        result.ErrorCode = code;
        result.Message = message;
        return result;
    }
}
