using System.Collections.Generic;

namespace MxFramework.Preview
{
    // Plain C# DTOs aligned with AUTHORING_EDITOR_03_RUNTIME_PREVIEW.md.
    // Independent from Tools/MxFramework.Authoring; only the JSON wire format is shared.

    public sealed class PreviewConnectionDescriptor
    {
        public string SchemaVersion = "1.0";
        public string Endpoint;
        public int Port;
        public string Token;
        public int ProcessId;
        public string GameVersion;
        public string StartedAt;
        public List<string> Capabilities;
    }

    public sealed class HandshakeParams
    {
        public string ClientName;
        public string ClientVersion;
        public string Token;
    }

    public sealed class HandshakeResult
    {
        public string ServerName;
        public string GameVersion;
        public string SchemaVersion;
        public List<string> Capabilities;
    }

    public sealed class LoadPatchParams
    {
        public string PackageId;
        public string Kind;
        public string SchemaVersion;
        public bool DiscardPrevious;
        // Raw JSON text of patches[] / baseLayers[] / patchLayers[]; opaque to runtime.
        public string RawSource;
    }

    public sealed class LoadPatchResult
    {
        public List<string> LoadedPatchIds = new List<string>();
        public List<string> RejectedPatches = new List<string>();
        public List<string> MergeWarnings = new List<string>();
        public RuntimePreviewConfigMetadata ConfigMetadata = new RuntimePreviewConfigMetadata();
        public long ElapsedMs;
    }

    public sealed class ApplyBuffParams
    {
        public string BuffId;
        public string CasterId;
        public string TargetId;
        public int Stack;
        public long? DurationOverrideMs;
        public int WaitTicks;
        public string RequestId;
    }

    public sealed class ResetParams
    {
        public bool ReloadBase;
    }

    public sealed class ResetResult
    {
        public long ElapsedMs;
    }

    public sealed class GetSnapshotParams
    {
        public string TargetId;
    }

    public sealed class GetLogsParams
    {
        public long AfterSeq;
        public int Max;
    }

    public sealed class GetLogsResult
    {
        public List<LogEntry> Logs = new List<LogEntry>();
        public long LastSeq;
    }

    public sealed class BuffSnapshot
    {
        public string BuffId;
        public string OwnerId;
        public int Stack;
        public long RemainingMs;
        public long TotalMs;
        public string CasterId;
        public string AddedAt;
    }

    public sealed class AttributeChange
    {
        public string OwnerId;
        public string Attribute;
        public long Before;
        public long After;
        public string DeltaSource;
    }

    public sealed class DamageTick
    {
        public string BuffId;
        public int TickIndex;
        public long Amount;
        // Spec carries damageType / elementType. Framework abstraction does not own these;
        // keep empty placeholders to avoid inventing business fields. TODO: wire when WGame layer adds them.
        public string DamageType = string.Empty;
        public string ElementType = string.Empty;
    }

    public sealed class StatusChange
    {
        public string OwnerId;
        public string Status;
        public bool Applied;
    }

    public sealed class LogEntry
    {
        public long Seq;
        public string Level;
        public string Message;
        public long AtMs;
    }

    public sealed class PerformanceSample
    {
        public long LoadMs;
        public long ApplyMs;
        public int TickCount;
        public long TotalMs;
    }

    public sealed class RuntimePreviewResult
    {
        public string RequestId;
        public bool Success;
        public string PreviewMode;
        public List<string> LoadedPatchIds = new List<string>();
        public RuntimePreviewConfigMetadata ConfigMetadata = new RuntimePreviewConfigMetadata();
        public string AppliedBuffId;
        public List<BuffSnapshot> BuffSnapshots = new List<BuffSnapshot>();
        public List<AttributeChange> AttributeChanges = new List<AttributeChange>();
        public List<DamageTick> DamageTicks = new List<DamageTick>();
        public List<StatusChange> StatusChanges = new List<StatusChange>();
        public List<LogEntry> Logs = new List<LogEntry>();
        public List<RuntimePreviewError> Errors = new List<RuntimePreviewError>();
        public PerformanceSample Performance = new PerformanceSample();
        public bool Truncated;
    }

    public sealed class RuntimePreviewConfigMetadata
    {
        public string SourceId = string.Empty;
        public string Layer = string.Empty;
        public List<string> LoadedPatchIds = new List<string>();
        public List<string> ChangedConfigIds = new List<string>();
        public List<string> FailedConfigIds = new List<string>();
        public List<string> MergeWarnings = new List<string>();

        public bool HasSource => !string.IsNullOrEmpty(SourceId) || LoadedPatchIds.Count > 0;

        public RuntimePreviewConfigMetadata Clone()
        {
            var copy = new RuntimePreviewConfigMetadata
            {
                SourceId = SourceId ?? string.Empty,
                Layer = Layer ?? string.Empty,
            };
            copy.LoadedPatchIds.AddRange(LoadedPatchIds);
            copy.ChangedConfigIds.AddRange(ChangedConfigIds);
            copy.FailedConfigIds.AddRange(FailedConfigIds);
            copy.MergeWarnings.AddRange(MergeWarnings);
            return copy;
        }
    }

    public sealed class RuntimePreviewError
    {
        public int Code;
        public string Message;
        public string Reason;
        public string PreviewMode;
        public string BuffId;
        public string TargetId;
    }
}
