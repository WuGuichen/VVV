using System.Text.Json;
using System.Security.Cryptography;
using MxFramework.ResourceMapping.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    internal static object ReadResourceMappingState(string rootPath, string buildTarget)
    {
        string profilePath = ResolveResourceMappingProfilePath(rootPath);
        if (!File.Exists(profilePath)) return new { success = false, exists = false, profilePath = ToProjectRelativePath(rootPath, profilePath), errorCode = "ProfileMissing" };
        ResourceMappingProfileFileState state = new ResourceMappingProfileFileAggregator().Read(profilePath);
        if (!state.Success) return new { success = false, exists = true, profilePath = ToProjectRelativePath(rootPath, profilePath), revision = state.Revision, diagnostics = state.Diagnostics };
        GlobalResourceBuildPlanCompilation compilation = new GlobalResourceBuildPlanCompiler().Compile(state.Profile, RequireConcreteBuildTarget(buildTarget));
        return new
        {
            success = compilation.Plan.Success, exists = true, productionActive = true, readOnly = false,
            profilePath = ToProjectRelativePath(rootPath, profilePath), revision = state.Revision, profile = state.Profile,
            effectiveBuildTarget = compilation.Plan.BuildTarget, mappingInputFingerprint = compilation.Plan.MappingInputFingerprint,
            buildPlanFingerprint = compilation.Plan.BuildPlanFingerprint, compilerContractVersion = compilation.Plan.CompilerContractVersion,
            plan = compilation.Plan, diagnostics = compilation.Plan.Diagnostics
        };
    }

    internal static object ReadResourceMappingPlan(string rootPath, string buildTarget)
    {
        string profilePath = ResolveResourceMappingProfilePath(rootPath);
        ResourceMappingProfileFileState state = new ResourceMappingProfileFileAggregator().Read(profilePath);
        if (!state.Success) return new { success = false, profilePath = ToProjectRelativePath(rootPath, profilePath), revision = state.Revision, diagnostics = state.Diagnostics };
        GlobalResourceBuildPlanCompilation compilation = new GlobalResourceBuildPlanCompiler().Compile(state.Profile, RequireConcreteBuildTarget(buildTarget));
        return new { success = compilation.Plan.Success, profilePath = ToProjectRelativePath(rootPath, profilePath), revision = state.Revision, plan = compilation.Plan, diagnostics = compilation.Plan.Diagnostics };
    }

    internal static object SaveResourceMappingManualSnapshot(string rootPath, string expectedRevision, ResourceMappingProducerSnapshot snapshot, string buildTarget, JsonSerializerOptions jsonOptions)
    {
        const string producerId = "resource-manager.manual";
        string target;
        try { target = RequireConcreteBuildTarget(buildTarget); }
        catch (ArgumentException ex) { return new { success = false, errorCode = "RESOURCE_MAPPING_BUILD_TARGET_REQUIRED", message = ex.Message }; }
        if (snapshot == null || !string.Equals(snapshot.ProducerId, producerId, StringComparison.Ordinal))
            return new { success = false, errorCode = "RESOURCE_MAPPING_MANUAL_OWNER_INVALID", message = "Manual save requires one complete resource-manager.manual snapshot." };
        ResourceMappingProfileFileAggregationResult applied = new ResourceMappingProfileFileAggregator().ApplyCompleteSnapshots(
            ResolveResourceMappingProfilePath(rootPath), expectedRevision ?? string.Empty, new[] { snapshot }, target);
        if (!applied.Success) return new { success = false, errorCode = applied.ErrorCode, message = applied.Message, diagnostics = applied.Diagnostics };
        InvalidateAuthoringResourceCache(rootPath);
        return new { success = true, changed = applied.Changed, previousRevision = applied.PreviousRevision, acceptedRevision = applied.AcceptedRevision,
            mappingInputFingerprint = applied.Compilation.Plan.MappingInputFingerprint, buildPlanFingerprint = applied.Compilation.Plan.BuildPlanFingerprint };
    }

    internal static object ReadGlobalResourceBuildStatus(string rootPath, string packageRelative, JsonSerializerOptions jsonOptions, string buildTarget)
    {
        string reportPath = ResolveGlobalResourceBuildReportPath(rootPath);
        JsonElement? report = ReadOptionalJson(rootPath, reportPath);
        string reportedTarget = ReadJsonString(report, "buildTarget");
        string target;
        try { target = ResourceMappingBuildTargetResolver.Resolve(rootPath, string.IsNullOrWhiteSpace(buildTarget) ? reportedTarget : buildTarget); }
        catch (InvalidOperationException ex)
        {
            return new { success = false, state = "needsUnityBuild", hasErrors = true, errorCode = "RESOURCE_MAPPING_BUILD_TARGET_REQUIRED", message = ex.Message };
        }
        string profilePath = ResolveResourceMappingProfilePath(rootPath);
        ResourceMappingProfileFileState state = new ResourceMappingProfileFileAggregator().Read(profilePath);
        GlobalResourceBuildPlanCompilation compilation = state.Success ? new GlobalResourceBuildPlanCompiler().Compile(state.Profile, target) : null;
        string reportFingerprint = ReadJsonString(report, "buildPlanFingerprint");
        bool targetMatches = string.Equals(reportedTarget, target, StringComparison.Ordinal);
        GlobalResourceArtifactSetStatus artifactSet = ValidateGlobalResourceArtifactSet(rootPath);
        bool fingerprintMatches = targetMatches && artifactSet.Valid && compilation != null &&
            string.Equals(ReadJsonString(report, "profileId"), compilation.Plan.ProfileId, StringComparison.Ordinal) &&
            string.Equals(ReadJsonString(report, "catalogId"), compilation.Plan.CatalogId, StringComparison.Ordinal) &&
            string.Equals(ReadJsonString(report, "mappingInputFingerprint"), compilation.Plan.MappingInputFingerprint, StringComparison.Ordinal) &&
            string.Equals(artifactSet.ProfileId, compilation.Plan.ProfileId, StringComparison.Ordinal) &&
            string.Equals(artifactSet.CatalogId, compilation.Plan.CatalogId, StringComparison.Ordinal) &&
            string.Equals(artifactSet.BuildTarget, target, StringComparison.Ordinal) &&
            string.Equals(reportFingerprint, compilation.Plan.BuildPlanFingerprint, StringComparison.Ordinal) &&
            string.Equals(artifactSet.BuildPlanFingerprint, compilation.Plan.BuildPlanFingerprint, StringComparison.Ordinal) &&
            string.Equals(artifactSet.MappingInputFingerprint, compilation.Plan.MappingInputFingerprint, StringComparison.Ordinal) &&
            string.Equals(artifactSet.SourceInputFingerprint, ReadJsonString(report, "sourceInputFingerprint"), StringComparison.Ordinal);
        var artifacts = new List<object>(artifactSet.Artifacts);
        artifacts.Add(new { path = ToProjectRelativePath(rootPath, reportPath), exists = File.Exists(reportPath), required = true, kind = "buildReport" });
        bool missing = !File.Exists(reportPath) || !artifactSet.Valid;
        bool hasErrors = !state.Success || compilation == null || !compilation.Plan.Success;
        string status = hasErrors ? "fixErrors" : missing || !fingerprintMatches ? "needsUnityBuild" : "readyForOfflineVerify";
        return new
        {
            success = !hasErrors, profilePath = ToProjectRelativePath(rootPath, profilePath), buildReportPath = ToProjectRelativePath(rootPath, reportPath),
            revision = state.Revision, requestedBuildTarget = buildTarget ?? string.Empty, reportedBuildTarget = reportedTarget, effectiveBuildTarget = compilation?.Plan.BuildTarget ?? target,
            state = status, stale = !fingerprintMatches, missingRequiredArtifacts = missing, hasErrors,
            artifactSetValid = artifactSet.Valid, artifactSetError = artifactSet.Error, sourceCurrentness = "requiresUnityVerification",
            mappingInputFingerprint = compilation?.Plan.MappingInputFingerprint ?? string.Empty,
            buildPlanFingerprint = compilation?.Plan.BuildPlanFingerprint ?? string.Empty,
            summary = compilation == null ? null : compilation.Plan.Counts, artifacts, plan = compilation?.Plan, buildReport = report, diagnostics = state.Diagnostics
        };
    }

    private static string RequireConcreteBuildTarget(string buildTarget)
    {
        if (string.IsNullOrWhiteSpace(buildTarget) || string.Equals(buildTarget, "ActiveBuildTarget", StringComparison.Ordinal))
            throw new ArgumentException("A concrete Unity build target is required.", nameof(buildTarget));
        return buildTarget;
    }
    private static string ResolveResourceMappingProfilePath(string rootPath) => Path.Combine(rootPath, "Assets", "Config", "MxFramework", "ResourceProfiles", "global_resource_mapping_profile.v2.json");
    private static string ResolveGlobalResourceBuildReportPath(string rootPath) => Path.Combine(rootPath, "Assets", "Config", "MxFramework", "ResourceBuildReports", "global_resource_build_report.json");
    private static string ResolveGlobalArtifactSetPath(string rootPath) => Path.Combine(rootPath, "Assets", "StreamingAssets", "MxFramework", "Resources", "global_resource_artifact_set.json");
    private static string ReadJsonString(JsonElement? element, string name) => element.HasValue && element.Value.ValueKind == JsonValueKind.Object && element.Value.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static GlobalResourceArtifactSetStatus ValidateGlobalResourceArtifactSet(string rootPath)
    {
        var status = new GlobalResourceArtifactSetStatus();
        string manifestPath = ResolveGlobalArtifactSetPath(rootPath);
        status.Artifacts.Add(new { path = ToProjectRelativePath(rootPath, manifestPath), exists = File.Exists(manifestPath), required = true, kind = "artifactSet" });
        if (!File.Exists(manifestPath)) { status.Error = "Artifact-set manifest is missing."; return status; }
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("schemaVersion", out JsonElement schema) || schema.GetInt32() != 1 ||
                !root.TryGetProperty("artifacts", out JsonElement artifacts) || artifacts.ValueKind != JsonValueKind.Array)
            { status.Error = "Artifact-set manifest schema is invalid."; return status; }
            status.ProfileId = root.GetProperty("profileId").GetString() ?? string.Empty;
            status.CatalogId = root.GetProperty("catalogId").GetString() ?? string.Empty;
            status.BuildPlanFingerprint = root.GetProperty("buildPlanFingerprint").GetString() ?? string.Empty;
            status.MappingInputFingerprint = root.GetProperty("mappingInputFingerprint").GetString() ?? string.Empty;
            status.SourceInputFingerprint = root.GetProperty("sourceInputFingerprint").GetString() ?? string.Empty;
            status.BuildTarget = root.GetProperty("buildTarget").GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(status.ProfileId) || string.IsNullOrWhiteSpace(status.CatalogId) ||
                string.IsNullOrWhiteSpace(status.BuildTarget) || !IsSha256(status.MappingInputFingerprint) ||
                !IsSha256(status.BuildPlanFingerprint) || !IsSha256(status.SourceInputFingerprint))
            { status.Error = "Artifact-set identity is invalid."; return status; }
            string streamingRoot = Path.GetFullPath(Path.Combine(rootPath, "Assets", "StreamingAssets")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var kinds = new HashSet<string>(StringComparer.Ordinal);
            var relativePaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement artifact in artifacts.EnumerateArray())
            {
                string kind = artifact.GetProperty("kind").GetString() ?? string.Empty;
                string relative = (artifact.GetProperty("relativePath").GetString() ?? string.Empty).Replace('\\', '/');
                string expected = artifact.GetProperty("sha256").GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) ||
                    relative.StartsWith("../", StringComparison.Ordinal) || relative.Contains("/../", StringComparison.Ordinal))
                { status.Error = "Artifact-set contains an invalid or escaping path."; return status; }
                if (!IsSha256(expected)) { status.Error = "Artifact-set contains an invalid file fingerprint."; return status; }
                if (!relativePaths.Add(relative)) { status.Error = "Artifact-set path is duplicated: " + relative + "."; return status; }
                string fullPath = Path.GetFullPath(Path.Combine(streamingRoot, relative));
                bool exists = fullPath.StartsWith(streamingRoot, StringComparison.Ordinal) && File.Exists(fullPath);
                bool hashMatches = exists && string.Equals(expected, ComputeFileSha256(fullPath), StringComparison.OrdinalIgnoreCase);
                status.Artifacts.Add(new { path = ToProjectRelativePath(rootPath, fullPath), exists, required = true, kind, hashMatches });
                if (!exists || !hashMatches) { status.Error = "Artifact-set file is missing or has a mismatched hash: " + relative + "."; return status; }
                if (kind != "bundle" && !kinds.Add(kind)) { status.Error = "Artifact-set kind is duplicated: " + kind + "."; return status; }
            }
            string[] requiredKinds = { "catalog", "catalogMounts", "preloadGroups", "bundleDependencies" };
            for (int i = 0; i < requiredKinds.Length; i++)
                if (!kinds.Contains(requiredKinds[i])) { status.Error = "Artifact-set kind is missing: " + requiredKinds[i] + "."; return status; }
            status.Valid = true;
            return status;
        }
        catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException || ex is KeyNotFoundException || ex is IOException || ex is UnauthorizedAccessException)
        {
            status.Error = ex.Message;
            return status;
        }
    }

    private static string ComputeFileSha256(string path)
    {
        using SHA256 sha = SHA256.Create(); using FileStream stream = File.OpenRead(path);
        return "sha256:" + Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private static bool IsSha256(string value)
    {
        const string prefix = "sha256:";
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || value.Length != prefix.Length + 64) return false;
        for (int i = prefix.Length; i < value.Length; i++)
        {
            char c = value[i];
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
        }
        return true;
    }

    private sealed class GlobalResourceArtifactSetStatus
    {
        public bool Valid { get; set; }
        public string Error { get; set; } = string.Empty;
        public string ProfileId { get; set; } = string.Empty;
        public string CatalogId { get; set; } = string.Empty;
        public string BuildTarget { get; set; } = string.Empty;
        public string MappingInputFingerprint { get; set; } = string.Empty;
        public string BuildPlanFingerprint { get; set; } = string.Empty;
        public string SourceInputFingerprint { get; set; } = string.Empty;
        public List<object> Artifacts { get; } = new();
    }

    private sealed class ResourceMappingManualSnapshotSaveRequest
    {
        public string expectedRevision { get; set; } = string.Empty;
        public string buildTarget { get; set; } = string.Empty;
        public ResourceMappingProducerSnapshot snapshot { get; set; }
    }
}
