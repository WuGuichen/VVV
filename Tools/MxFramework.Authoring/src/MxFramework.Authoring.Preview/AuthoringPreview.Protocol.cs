using System.Collections.Generic;
using System.Text.Json;

namespace MxFramework.Authoring.Preview.Protocol
{
    /// <summary>
    /// 03 子需求权威 Spec 定义的连接描述文件结构。
    /// </summary>
    public sealed class PreviewConnectionDescriptor
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string Endpoint { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Token { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string GameVersion { get; set; } = string.Empty;
        public string StartedAt { get; set; } = string.Empty;
        public List<string> Capabilities { get; set; } = new List<string>();
    }

    public sealed class JsonRpcRequest
    {
        public string Jsonrpc { get; set; } = "2.0";
        public int Id { get; set; }
        public string Method { get; set; } = string.Empty;
        public JsonElement? Params { get; set; }
    }

    public sealed class JsonRpcResponse
    {
        public string Jsonrpc { get; set; } = "2.0";
        public int Id { get; set; }
        public JsonElement? Result { get; set; }
        public JsonRpcError Error { get; set; }
    }

    public sealed class JsonRpcError
    {
        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
        public JsonElement? Data { get; set; }
    }

    public static class PreviewError
    {
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32603;
        public const int NotHandshaked = 1001;
        public const int TokenMismatch = 1002;
        public const int PatchParseFailed = 2001;
        public const int PatchLoadFailed = 2002;
        public const int ApplyBuffFailed = 2003;
        public const int NotInPreviewMode = 2004;
    }

    public sealed class HandshakeParams
    {
        public string ClientName { get; set; } = string.Empty;
        public string ClientVersion { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }

    public sealed class HandshakeResult
    {
        public string ServerName { get; set; } = string.Empty;
        public string GameVersion { get; set; } = string.Empty;
        public string SchemaVersion { get; set; } = "1.0";
        public List<string> Capabilities { get; set; } = new List<string>();
    }

    public sealed class LoadPatchParams
    {
        public string PackageId { get; set; } = string.Empty;
        public string Kind { get; set; } = "Preview";
        public List<JsonElement> Patches { get; set; } = new List<JsonElement>();
        public List<JsonElement> BaseLayers { get; set; } = new List<JsonElement>();
        public List<JsonElement> PatchLayers { get; set; } = new List<JsonElement>();
        public string SchemaVersion { get; set; } = "1.0";
        public bool DiscardPrevious { get; set; } = true;
    }

    public sealed class LoadPatchResult
    {
        public List<string> LoadedPatchIds { get; set; } = new List<string>();
        public List<string> RejectedPatches { get; set; } = new List<string>();
        public List<string> MergeWarnings { get; set; } = new List<string>();
        public RuntimePreviewConfigMetadata ConfigMetadata { get; set; } = new RuntimePreviewConfigMetadata();
        public int ElapsedMs { get; set; }
    }

    public sealed class ApplyBuffParams
    {
        public string BuffId { get; set; } = string.Empty;
        public string CasterId { get; set; } = "TestCaster";
        public string TargetId { get; set; } = "TestTarget";
        public int Stack { get; set; } = 1;
        public int? DurationOverrideMs { get; set; }
        public int WaitTicks { get; set; }
    }

    public sealed class ResetParams
    {
        public bool ReloadBase { get; set; }
    }

    public sealed class ResetResult
    {
        public int ElapsedMs { get; set; }
    }

    public sealed class GetSnapshotParams
    {
        public string TargetId { get; set; } = "TestTarget";
    }

    public sealed class GetLogsParams
    {
        public int AfterSeq { get; set; }
        public int Max { get; set; } = 200;
    }

    public sealed class GetLogsResult
    {
        public List<LogEntry> Logs { get; set; } = new List<LogEntry>();
        public int LastSeq { get; set; }
    }

    public sealed class BuffSnapshot
    {
        public string BuffId { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public int Stack { get; set; }
        public int RemainingMs { get; set; }
        public int TotalMs { get; set; }
        public string CasterId { get; set; } = string.Empty;
        public string AddedAt { get; set; } = string.Empty;
    }

    public sealed class AttributeChange
    {
        public string OwnerId { get; set; } = string.Empty;
        public string Attribute { get; set; } = string.Empty;
        public double Before { get; set; }
        public double After { get; set; }
        public string DeltaSource { get; set; } = string.Empty;
    }

    public sealed class DamageTick
    {
        public string BuffId { get; set; } = string.Empty;
        public int TickIndex { get; set; }
        public double Amount { get; set; }
        public string DamageType { get; set; } = string.Empty;
        public string ElementType { get; set; } = string.Empty;
    }

    public sealed class StatusChange
    {
        public string OwnerId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool Applied { get; set; }
    }

    public sealed class LogEntry
    {
        public int Seq { get; set; }
        public string Level { get; set; } = "info";
        public string Message { get; set; } = string.Empty;
        public int AtMs { get; set; }
    }

    public sealed class PerformanceSample
    {
        public int LoadMs { get; set; }
        public int ApplyMs { get; set; }
        public int TickCount { get; set; }
        public int TotalMs { get; set; }
    }

    public sealed class PreviewResultError
    {
        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string PreviewMode { get; set; } = string.Empty;
        public string BuffId { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
    }

    public sealed class RuntimePreviewConfigMetadata
    {
        public string SourceId { get; set; } = string.Empty;
        public string Layer { get; set; } = string.Empty;
        public List<string> LoadedPatchIds { get; set; } = new List<string>();
        public List<string> ChangedConfigIds { get; set; } = new List<string>();
        public List<string> FailedConfigIds { get; set; } = new List<string>();
        public List<string> MergeWarnings { get; set; } = new List<string>();
    }

    public sealed class RuntimePreviewResult
    {
        public string RequestId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string PreviewMode { get; set; } = string.Empty;
        public List<string> LoadedPatchIds { get; set; } = new List<string>();
        public RuntimePreviewConfigMetadata ConfigMetadata { get; set; } = new RuntimePreviewConfigMetadata();
        public string AppliedBuffId { get; set; } = string.Empty;
        public List<BuffSnapshot> BuffSnapshots { get; set; } = new List<BuffSnapshot>();
        public List<AttributeChange> AttributeChanges { get; set; } = new List<AttributeChange>();
        public List<DamageTick> DamageTicks { get; set; } = new List<DamageTick>();
        public List<StatusChange> StatusChanges { get; set; } = new List<StatusChange>();
        public List<LogEntry> Logs { get; set; } = new List<LogEntry>();
        public List<PreviewResultError> Errors { get; set; } = new List<PreviewResultError>();
        public PerformanceSample Performance { get; set; } = new PerformanceSample();
        public bool Truncated { get; set; }
    }
}
