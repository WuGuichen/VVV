using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace MxFramework.ResourceMapping.Authoring
{
    public sealed class ResourceMappingProfileFileState
    {
        public bool Success { get; set; }
        public string ProfilePath { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;
        public ResourceMappingProfileV2 Profile { get; set; }
        public List<ResourceMappingDiagnostic> Diagnostics { get; set; } = new List<ResourceMappingDiagnostic>();
    }

    public sealed class ResourceMappingProfileFileAggregationResult
    {
        public bool Success { get; set; }
        public bool Changed { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string PreviousRevision { get; set; } = string.Empty;
        public string AcceptedRevision { get; set; } = string.Empty;
        public ResourceMappingProfileV2 Profile { get; set; }
        public GlobalResourceBuildPlanCompilation Compilation { get; set; }
        public List<ResourceMappingDiagnostic> Diagnostics { get; set; } = new List<ResourceMappingDiagnostic>();
    }

    public sealed class ResourceMappingProfileFileAggregator
    {
        /// <remarks>AllocByDesign. Reads one low-frequency authoring Profile with a canonical optimistic revision.</remarks>
        public ResourceMappingProfileFileState Read(string profilePath)
        {
            var state = new ResourceMappingProfileFileState { ProfilePath = profilePath ?? string.Empty };
            if (string.IsNullOrWhiteSpace(profilePath) || !File.Exists(profilePath))
            {
                state.Diagnostics.Add(Error("ProfileMissing", "Resource Mapping Profile was not found.", "$"));
                return state;
            }

            ResourceMappingProfileReadResult read = ResourceMappingProfileJson.ReadStrict(File.ReadAllText(profilePath));
            state.Diagnostics.AddRange(read.Diagnostics);
            if (!read.Success)
                return state;
            state.Profile = read.Profile;
            state.Revision = new GlobalResourceBuildPlanCompiler().ComputeProfileRevision(read.Profile);
            state.Success = true;
            return state;
        }

        /// <remarks>AllocByDesign. Applies complete producer snapshots and atomically persists one compiler-valid Profile.</remarks>
        public ResourceMappingProfileFileAggregationResult ApplyCompleteSnapshots(
            string profilePath,
            string expectedRevision,
            IList<ResourceMappingProducerSnapshot> snapshots,
            string buildTarget)
        {
            ResourceMappingProfileFileState state = Read(profilePath);
            if (!state.Success)
                return Failure("RESOURCE_MAPPING_PROFILE_READ_FAILED", "Profile could not be read.", state);
            if (!string.Equals(state.Revision, expectedRevision ?? string.Empty, StringComparison.Ordinal))
                return Failure(ResourceMappingAggregationErrorCodes.RevisionConflict, "Expected revision does not match the saved Profile.", state);
            if (snapshots == null || snapshots.Count == 0)
                return Failure("RESOURCE_MAPPING_SNAPSHOT_MISSING", "At least one complete producer snapshot is required.", state);

            ResourceMappingProfileV2 candidate = state.Profile;
            string revision = state.Revision;
            var aggregator = new ResourceMappingProfileAggregator();
            for (int i = 0; i < snapshots.Count; i++)
            {
                ResourceMappingAggregationResult applied = aggregator.ApplyCompleteSnapshot(candidate, revision, revision, snapshots[i]);
                if (!applied.Success)
                {
                    var failed = new ResourceMappingProfileFileAggregationResult
                    {
                        Success = false,
                        Changed = false,
                        ErrorCode = applied.ErrorCode,
                        Message = applied.Message,
                        PreviousRevision = state.Revision,
                        AcceptedRevision = state.Revision,
                        Profile = state.Profile
                    };
                    if (applied.Validation != null)
                        failed.Diagnostics.AddRange(applied.Validation.Diagnostics);
                    return failed;
                }
                candidate = applied.Profile;
                revision = applied.AcceptedRevision;
            }

            GlobalResourceBuildPlanCompilation compilation = new GlobalResourceBuildPlanCompiler().Compile(candidate, buildTarget ?? string.Empty);
            if (!compilation.Plan.Success)
            {
                return new ResourceMappingProfileFileAggregationResult
                {
                    Success = false,
                    Changed = false,
                    ErrorCode = "RESOURCE_MAPPING_COMPILE_FAILED",
                    Message = "Aggregated Profile did not compile.",
                    PreviousRevision = state.Revision,
                    AcceptedRevision = state.Revision,
                    Profile = state.Profile,
                    Compilation = compilation,
                    Diagnostics = new List<ResourceMappingDiagnostic>(compilation.Plan.Diagnostics)
                };
            }

            string canonical = new GlobalResourceBuildPlanCompiler().SerializeCanonicalProfile(candidate);
            string candidateRevision = ResourceMappingCanonicalJson.ComputeFingerprint(canonical);
            if (string.Equals(candidateRevision, state.Revision, StringComparison.Ordinal))
            {
                return new ResourceMappingProfileFileAggregationResult
                {
                    Success = true,
                    Changed = false,
                    PreviousRevision = state.Revision,
                    AcceptedRevision = candidateRevision,
                    Profile = candidate,
                    Compilation = compilation
                };
            }

            using (FileStream writeLock = AcquireWriteLock(profilePath))
            {
                ResourceMappingProfileFileAggregationResult final = VerifyAndWrite(profilePath, expectedRevision, canonical);
                if (final != null) return final;
            }
            return new ResourceMappingProfileFileAggregationResult
            {
                Success = true,
                Changed = true,
                PreviousRevision = state.Revision,
                AcceptedRevision = candidateRevision,
                Profile = candidate,
                Compilation = compilation
            };
        }

        private ResourceMappingProfileFileAggregationResult VerifyAndWrite(string profilePath, string expectedRevision, string canonical)
        {
            ResourceMappingProfileFileState beforeWrite = Read(profilePath);
            if (!beforeWrite.Success || !string.Equals(beforeWrite.Revision, expectedRevision ?? string.Empty, StringComparison.Ordinal))
                return Failure(ResourceMappingAggregationErrorCodes.RevisionConflict, "Saved Profile changed before atomic replacement.", beforeWrite);
            WriteAtomic(profilePath, canonical);
            return null;
        }

        private static FileStream AcquireWriteLock(string profilePath)
        {
            string identity = ResourceMappingCanonicalJson.ComputeFingerprint(Path.GetFullPath(profilePath)).Substring("sha256:".Length);
            string directory = Path.Combine(Path.GetTempPath(), "MxFramework.ResourceMapping.Locks");
            Directory.CreateDirectory(directory);
            string lockPath = Path.Combine(directory, identity + ".lock");
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (true)
            {
                try
                {
                    return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    if (DateTime.UtcNow >= deadline)
                        throw new IOException("Timed out waiting for the Resource Mapping Profile write lock: " + profilePath + ".");
                    Thread.Sleep(25);
                }
            }
        }

        private static void WriteAtomic(string path, string content)
        {
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("Profile path has no parent directory.");
            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(temporaryPath, content);
            try
            {
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        if (File.Exists(fullPath))
                            File.Replace(temporaryPath, fullPath, null);
                        else
                            File.Move(temporaryPath, fullPath);
                        return;
                    }
                    catch (IOException) when (attempt < 4)
                    {
                        System.Threading.Thread.Sleep(10 * (attempt + 1));
                    }
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static ResourceMappingProfileFileAggregationResult Failure(string code, string message, ResourceMappingProfileFileState state)
        {
            return new ResourceMappingProfileFileAggregationResult
            {
                Success = false,
                Changed = false,
                ErrorCode = code,
                Message = message,
                PreviousRevision = state != null ? state.Revision : string.Empty,
                AcceptedRevision = state != null ? state.Revision : string.Empty,
                Profile = state != null ? state.Profile : null,
                Diagnostics = state != null ? new List<ResourceMappingDiagnostic>(state.Diagnostics) : new List<ResourceMappingDiagnostic>()
            };
        }

        private static ResourceMappingDiagnostic Error(string code, string message, string path)
        {
            return new ResourceMappingDiagnostic { Severity = ResourceMappingDiagnosticSeverity.Error, Code = code, Message = message, Path = path };
        }
    }
}
