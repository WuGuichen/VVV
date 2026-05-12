using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MxFramework.Authoring.Preview;
using MxFramework.Authoring.Preview.Protocol;

namespace MxFramework.Authoring.Tests;

/// <summary>
/// 进程内 Mock Preview Server，用于在测试里跑 6 个 RPC 的 happy-path。
/// 启动时随机选端口并通过 PreviewConnectionLocator 写入隔离目录。
/// </summary>
internal sealed class MockPreviewServer : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpListener _listener = new HttpListener();
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly string _descriptorDir;
    private readonly string _descriptorPath;
    private readonly List<JsonElement> _loadedPatches = new List<JsonElement>();
    private readonly List<LogEntry> _logs = new List<LogEntry>();

    public PreviewConnectionDescriptor Descriptor { get; }
    public Task ServerTask { get; private set; }
    public bool FailApplyBuff { get; set; }

    public MockPreviewServer(string descriptorDir, string token = null)
    {
        _descriptorDir = descriptorDir;
        int port = PickFreePort();
        Descriptor = new PreviewConnectionDescriptor
        {
            SchemaVersion = "1.0",
            Endpoint = $"ws://127.0.0.1:{port}/preview",
            Port = port,
            Token = token ?? Guid.NewGuid().ToString("N"),
            ProcessId = Process.GetCurrentProcess().Id,
            GameVersion = "0.3.1",
            StartedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Capabilities = new List<string>
            {
                "preview.handshake",
                "preview.loadPatch",
                "preview.applyBuff",
                "preview.reset",
                "preview.getSnapshot",
                "preview.getLogs"
            }
        };
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/preview/");
        _listener.Start();
        _descriptorPath = PreviewConnectionLocator.WriteForTests(_descriptorDir, Descriptor);
        ServerTask = Task.Run(AcceptLoopAsync);

        _logs.Add(new LogEntry { Seq = 1, Level = "info", Message = "preview server ready", AtMs = 0 });
    }

    private static int PickFreePort()
    {
        // 49152..65535 是 Spec 推荐范围；用 IPGlobalProperties 确保不冲突。
        var rng = new Random();
        var props = IPGlobalProperties.GetIPGlobalProperties();
        var used = new HashSet<int>();
        foreach (var info in props.GetActiveTcpListeners()) used.Add(info.Port);
        for (int i = 0; i < 64; i++)
        {
            int candidate = rng.Next(49152, 65535);
            if (!used.Contains(candidate)) return candidate;
        }
        return 49157;
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            if (!ctx.Request.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.Close();
                continue;
            }

            HttpListenerWebSocketContext wsCtx = await ctx.AcceptWebSocketAsync(null).ConfigureAwait(false);
            _ = Task.Run(() => SessionAsync(wsCtx.WebSocket));
        }
    }

    private async Task SessionAsync(WebSocket socket)
    {
        bool handshaked = false;
        var buffer = new byte[64 * 1024];
        var ms = new MemoryStream();
        try
        {
            while (socket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                string text = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
                JsonRpcRequest req = JsonSerializer.Deserialize<JsonRpcRequest>(text, JsonOptions);
                if (req == null) continue;

                JsonRpcResponse resp = await HandleRequestAsync(req, handshaked).ConfigureAwait(false);
                if (req.Method == "preview.handshake" && resp.Error == null) handshaked = true;

                byte[] payload = JsonSerializer.SerializeToUtf8Bytes(resp, JsonOptions);
                await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, _cts.Token).ConfigureAwait(false);
            }
        }
        catch
        {
        }
    }

    private Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest req, bool handshaked)
    {
        var resp = new JsonRpcResponse { Jsonrpc = "2.0", Id = req.Id };

        if (req.Method != "preview.handshake" && !handshaked)
        {
            resp.Error = new JsonRpcError { Code = PreviewError.NotHandshaked, Message = "handshake required" };
            return Task.FromResult(resp);
        }

        switch (req.Method)
        {
            case "preview.handshake":
                {
                    HandshakeParams p = JsonSerializer.Deserialize<HandshakeParams>(req.Params?.GetRawText() ?? "{}", JsonOptions);
                    if (p == null || p.Token != Descriptor.Token)
                    {
                        resp.Error = new JsonRpcError { Code = PreviewError.TokenMismatch, Message = "token mismatch" };
                        return Task.FromResult(resp);
                    }
                    var hr = new HandshakeResult
                    {
                        ServerName = "MxRuntimePreview.Mock",
                        GameVersion = Descriptor.GameVersion,
                        SchemaVersion = "1.0",
                        Capabilities = Descriptor.Capabilities
                    };
                    resp.Result = ToElement(hr);
                    break;
                }
            case "preview.loadPatch":
                {
                    LoadPatchParams p = JsonSerializer.Deserialize<LoadPatchParams>(req.Params?.GetRawText() ?? "{}", JsonOptions);
                    _loadedPatches.Clear();
                    if (p?.Patches != null) _loadedPatches.AddRange(p.Patches);
                    var lr = new LoadPatchResult
                    {
                        LoadedPatchIds = new List<string> { p?.PackageId ?? "mock.preview" },
                        ConfigMetadata = new RuntimePreviewConfigMetadata
                        {
                            SourceId = p?.PackageId ?? "mock.preview",
                            Layer = "Patch",
                            LoadedPatchIds = new List<string> { p?.PackageId ?? "mock.preview" },
                            ChangedConfigIds = new List<string> { "BasicBuffConfig:100001" }
                        },
                        ElapsedMs = 7
                    };
                    AppendLog("info", $"loaded {_loadedPatches.Count} patches");
                    resp.Result = ToElement(lr);
                    break;
                }
            case "preview.applyBuff":
                {
                    ApplyBuffParams p = JsonSerializer.Deserialize<ApplyBuffParams>(req.Params?.GetRawText() ?? "{}", JsonOptions);
                    if (FailApplyBuff)
                    {
                        var failed = new RuntimePreviewResult
                        {
                            Success = false,
                            PreviewMode = "scene",
                            LoadedPatchIds = new List<string> { "mock.preview" },
                            ConfigMetadata = new RuntimePreviewConfigMetadata
                            {
                                SourceId = "mock.preview",
                                Layer = "Patch",
                                LoadedPatchIds = new List<string> { "mock.preview" }
                            },
                            Errors =
                            {
                                new PreviewResultError
                                {
                                    Code = PreviewError.ApplyBuffFailed,
                                    Message = "mock missing target",
                                    Reason = "missing_target",
                                    PreviewMode = "scene",
                                    BuffId = p?.BuffId ?? "100001",
                                    TargetId = p?.TargetId ?? "MissingTarget"
                                }
                            }
                        };
                        resp.Error = new JsonRpcError
                        {
                            Code = PreviewError.ApplyBuffFailed,
                            Message = "mock missing target",
                            Data = ToElement(new
                            {
                                reason = "missing_target",
                                previewMode = "scene",
                                buffId = p?.BuffId ?? "100001",
                                targetId = p?.TargetId ?? "MissingTarget",
                                result = failed
                            })
                        };
                        break;
                    }
                    var rr = BuildSampleResult(p?.BuffId ?? "100001", p?.TargetId ?? "TestTarget", p?.CasterId ?? "TestCaster");
                    AppendLog("info", $"applied buff {rr.AppliedBuffId}");
                    resp.Result = ToElement(rr);
                    break;
                }
            case "preview.reset":
                {
                    _loadedPatches.Clear();
                    _logs.Clear();
                    resp.Result = ToElement(new ResetResult { ElapsedMs = 3 });
                    break;
                }
            case "preview.getSnapshot":
                {
                    GetSnapshotParams p = JsonSerializer.Deserialize<GetSnapshotParams>(req.Params?.GetRawText() ?? "{}", JsonOptions);
                    var rr = BuildSampleResult("100001", p?.TargetId ?? "TestTarget", "TestCaster");
                    rr.AppliedBuffId = string.Empty;
                    resp.Result = ToElement(rr);
                    break;
                }
            case "preview.getLogs":
                {
                    GetLogsParams p = JsonSerializer.Deserialize<GetLogsParams>(req.Params?.GetRawText() ?? "{}", JsonOptions);
                    var filtered = new List<LogEntry>();
                    int last = 0;
                    foreach (var entry in _logs)
                    {
                        if (entry.Seq <= (p?.AfterSeq ?? 0)) continue;
                        filtered.Add(entry);
                        last = entry.Seq;
                        if (filtered.Count >= (p?.Max ?? 200)) break;
                    }
                    resp.Result = ToElement(new GetLogsResult { Logs = filtered, LastSeq = last });
                    break;
                }
            default:
                resp.Error = new JsonRpcError { Code = PreviewError.MethodNotFound, Message = "unknown method " + req.Method };
                break;
        }

        return Task.FromResult(resp);
    }

    private RuntimePreviewResult BuildSampleResult(string buffId, string targetId, string casterId)
    {
        return new RuntimePreviewResult
        {
            RequestId = Guid.NewGuid().ToString("N"),
            Success = true,
            PreviewMode = "dummy",
            LoadedPatchIds = new List<string> { "mock.preview" },
            ConfigMetadata = new RuntimePreviewConfigMetadata
            {
                SourceId = "mock.preview",
                Layer = "Patch",
                LoadedPatchIds = new List<string> { "mock.preview" },
                ChangedConfigIds = new List<string> { "BasicBuffConfig:" + buffId },
                MergeWarnings = new List<string> { "mock warning" }
            },
            AppliedBuffId = buffId,
            BuffSnapshots =
            {
                new BuffSnapshot { BuffId = buffId, OwnerId = targetId, Stack = 1, RemainingMs = 4800, TotalMs = 5000, CasterId = casterId, AddedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") }
            },
            AttributeChanges =
            {
                new AttributeChange { OwnerId = targetId, Attribute = "Hp", Before = 1000, After = 940, DeltaSource = buffId }
            },
            DamageTicks =
            {
                new DamageTick { BuffId = buffId, TickIndex = 0, Amount = 60, DamageType = "Magic", ElementType = "Fire" }
            },
            StatusChanges =
            {
                new StatusChange { OwnerId = targetId, Status = "Burning", Applied = true }
            },
            Logs = new List<LogEntry>(_logs),
            Performance = new PerformanceSample { LoadMs = 7, ApplyMs = 3, TickCount = 60, TotalMs = 80 },
            Truncated = false
        };
    }

    private void AppendLog(string level, string message)
    {
        _logs.Add(new LogEntry { Seq = _logs.Count + 1, Level = level, Message = message, AtMs = (int)Environment.TickCount });
    }

    private static JsonElement ToElement<T>(T value)
    {
        string text = JsonSerializer.Serialize(value, JsonOptions);
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _listener.Stop(); } catch { }
        try { _listener.Close(); } catch { }
        PreviewConnectionLocator.DeleteIfExists(_descriptorPath);
    }
}
