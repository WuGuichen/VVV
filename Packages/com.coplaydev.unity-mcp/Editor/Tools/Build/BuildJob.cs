using System;
using System.Collections.Generic;
using UnityEditor;

namespace MCPForUnity.Editor.Tools.Build
{
    public enum BuildJobState
    {
        Pending,
        Building,
        Succeeded,
        Failed,
        Cancelled,
        Skipped
    }

    public class BuildJob
    {
        public string JobId { get; }
        public BuildJobState State { get; set; } = BuildJobState.Pending;
        public BuildTarget Target { get; set; }
        public string OutputPath { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string ErrorMessage { get; set; }

        public double TotalSizeMb { get; set; }
        public int TotalErrors { get; set; }
        public int TotalWarnings { get; set; }

        public BuildJob(string jobId, BuildTarget target, string outputPath)
        {
            JobId = jobId;
            Target = target;
            OutputPath = outputPath;
        }

        public object ToStatusResponse()
        {
            var data = new Dictionary<string, object>
            {
                ["job_id"] = JobId,
                ["result"] = State.ToString().ToLowerInvariant(),
                ["platform"] = Target.ToString(),
                ["output_path"] = OutputPath
            };

            if (StartedAt != default)
                data["started_at"] = StartedAt.ToString("O");

            if (CompletedAt.HasValue)
            {
                data["duration_seconds"] = (CompletedAt.Value - StartedAt).TotalSeconds;
                data["completed_at"] = CompletedAt.Value.ToString("O");
            }

            if (State == BuildJobState.Succeeded || State == BuildJobState.Failed)
            {
                data["total_size_mb"] = TotalSizeMb;
                data["errors"] = TotalErrors;
                data["warnings"] = TotalWarnings;
            }

            if (!string.IsNullOrEmpty(ErrorMessage))
                data["error"] = ErrorMessage;

            return data;
        }
    }

    public class BatchJob
    {
        public string JobId { get; }
        public BuildJobState State { get; set; } = BuildJobState.Pending;
        public List<BuildJob> Children { get; } = new();
        public int CurrentIndex { get; set; } = -1;

        public BatchJob(string jobId)
        {
            JobId = jobId;
        }

        public object ToStatusResponse()
        {
            int completed = 0;
            string currentBuild = null;
            var builds = new List<object>();

            foreach (var child in Children)
            {
                if (child.State == BuildJobState.Succeeded || child.State == BuildJobState.Failed ||
                    child.State == BuildJobState.Skipped || child.State == BuildJobState.Cancelled)
                    completed++;
                if (child.State == BuildJobState.Building)
                    currentBuild = child.JobId;
                builds.Add(child.ToStatusResponse());
            }

            return new Dictionary<string, object>
            {
                ["job_id"] = JobId,
                ["result"] = State.ToString().ToLowerInvariant(),
                ["completed"] = completed,
                ["total"] = Children.Count,
                ["current_build"] = currentBuild,
                ["builds"] = builds
            };
        }
    }

    public static class BuildJobStore
    {
        private static readonly Dictionary<string, BuildJob> BuildJobs = new();
        private static readonly Dictionary<string, BatchJob> BatchJobs = new();
        private static BuildJob lastCompletedJob;

        public static string CreateJobId() => $"build-{Guid.NewGuid():N}".Substring(0, 16);
        public static string CreateBatchId() => $"batch-{Guid.NewGuid():N}".Substring(0, 16);

        public static void AddBuildJob(BuildJob job) => BuildJobs[job.JobId] = job;
        public static void AddBatchJob(BatchJob job) => BatchJobs[job.JobId] = job;

        public static BuildJob GetBuildJob(string jobId)
        {
            BuildJobs.TryGetValue(jobId, out var job);
            return job;
        }

        public static BatchJob GetBatchJob(string jobId)
        {
            BatchJobs.TryGetValue(jobId, out var job);
            return job;
        }

        public static BuildJob LastCompletedJob => lastCompletedJob;

        public static void SetLastCompleted(BuildJob job)
        {
            lastCompletedJob = job;
            PruneOldJobs();
        }

        private const int MaxRetainedJobs = 50;

        private static void PruneOldJobs()
        {
            if (BuildJobs.Count <= MaxRetainedJobs)
                return;

            var toRemove = new List<string>();
            foreach (var kvp in BuildJobs)
            {
                if (kvp.Value.State != BuildJobState.Building && kvp.Value.State != BuildJobState.Pending &&
                    kvp.Value != lastCompletedJob)
                    toRemove.Add(kvp.Key);
            }

            foreach (var key in toRemove)
            {
                BuildJobs.Remove(key);
                if (BuildJobs.Count <= MaxRetainedJobs / 2)
                    break;
            }

            var batchesToRemove = new List<string>();
            foreach (var kvp in BatchJobs)
            {
                var batch = kvp.Value;
                if (batch.State == BuildJobState.Building || batch.State == BuildJobState.Pending)
                    continue;

                batch.Children.RemoveAll(c => !BuildJobs.ContainsKey(c.JobId));
                if (batch.Children.Count == 0)
                    batchesToRemove.Add(kvp.Key);
            }

            foreach (var key in batchesToRemove)
                BatchJobs.Remove(key);
        }
    }
}
