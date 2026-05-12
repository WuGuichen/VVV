using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MxFramework.Config.Runtime;
using UnityEngine;

namespace MxFramework.Preview
{
    /// <summary>
    /// Unity-side WebSocket + JSON-RPC 2.0 preview server. Listens on 127.0.0.1 only,
    /// auto-picks a port in [49152, 65535], writes a connection descriptor, and dispatches
    /// the six preview.* methods. Buff operations are marshaled to the Unity main thread
    /// via <see cref="PreviewMainThreadDispatcher"/>.
    /// </summary>
    public sealed class PreviewRpcServer : IDisposable
    {
        public const string ServerName = "MxRuntimePreview";
        public const string SchemaVersion = "1.0";
        private const int MaxInlineResultLogs = 32;
        private const int MaxResultPayloadBytes = 1024 * 1024;

        private static readonly string[] s_capabilities =
        {
            "preview.handshake", "preview.loadPatch", "preview.applyBuff",
            "preview.reset", "preview.getSnapshot", "preview.getLogs",
        };

        private readonly IPreviewWorld _world;
        private readonly RuntimePreviewAdapter _adapter;
        private readonly IBuffPatchLoader _patchLoader;
        private readonly PreviewMainThreadDispatcher _dispatcher;
        private readonly PreviewLogBuffer _logs;
        private readonly string _gameVersion;

        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private Task _acceptLoop;
        private string _token;
        private int _port;
        private string _descriptorPath;
        private List<string> _loadedPatchIds = new List<string>();

        public bool IsRunning => _listener != null && _listener.Server != null && _listener.Server.IsBound;
        public int Port => _port;
        public string Token => _token;
        public string DescriptorPath => _descriptorPath;
        public PreviewLogBuffer Logs => _logs;

        public PreviewRpcServer(
            IPreviewWorld world,
            IBuffPatchLoader patchLoader,
            PreviewMainThreadDispatcher dispatcher,
            string gameVersion = "0.3.1",
            PreviewLogBuffer logs = null)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _patchLoader = patchLoader ?? throw new ArgumentNullException(nameof(patchLoader));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _gameVersion = gameVersion ?? "0.0.0";
            _logs = logs ?? new PreviewLogBuffer();
            _adapter = new RuntimePreviewAdapter(_world, _logs);
        }

        public void Start()
        {
            if (IsRunning) return;

            _token = GenerateToken();
            _logs.Reset();

            _listener = BindAvailablePort(out _port);

            _cts = new CancellationTokenSource();
            _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));

            PreviewConnectionDescriptor desc = new PreviewConnectionDescriptor
            {
                Endpoint = $"ws://127.0.0.1:{_port}/preview",
                Port = _port,
                Token = _token,
                ProcessId = Process.GetCurrentProcess().Id,
                GameVersion = _gameVersion,
                StartedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                Capabilities = new List<string>(s_capabilities),
            };
            _descriptorPath = PreviewConnectionDescriptorWriter.Write(desc);

            _logs.Append("info", $"PreviewRpcServer listening on ws://127.0.0.1:{_port}/preview");
            UnityEngine.Debug.Log($"[MxPreview] PreviewRpcServer started. desc={_descriptorPath}");
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            PreviewConnectionDescriptorWriter.Delete();
            _listener = null;
            _logs.Append("info", "PreviewRpcServer stopped.");
        }

        private static TcpListener BindAvailablePort(out int selectedPort)
        {
            for (int port = 49152; port <= 65535; port++)
            {
                TcpListener candidate = null;
                try
                {
                    candidate = new TcpListener(IPAddress.Loopback, port);
                    candidate.Start();
                    selectedPort = port;
                    return candidate;
                }
                catch (SocketException)
                {
                    try { candidate?.Stop(); } catch { }
                }
            }
            selectedPort = 0;
            throw new InvalidOperationException("No free port in 49152-65535 range.");
        }

        private static string GenerateToken()
        {
            byte[] buf = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(buf);
            return Convert.ToBase64String(buf);
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener != null)
            {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false); }
                catch (Exception) { break; }

                _ = Task.Run(() => HandleClientAsync(client, ct));
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            bool handshaked = false;
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    if (!await AcceptWebSocketHandshake(stream, ct).ConfigureAwait(false))
                        return;

                    while (!ct.IsCancellationRequested && client.Connected)
                    {
                        string request = await ReadWebSocketText(stream, ct).ConfigureAwait(false);
                        if (request == null)
                            return;
                        string response = ProcessRequest(request, ref handshaked);
                        if (response == null) return;
                        await WriteWebSocketText(stream, response, ct).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logs.Append("error", "WebSocket loop error: " + ex.Message);
            }
        }

        private static async Task<bool> AcceptWebSocketHandshake(NetworkStream stream, CancellationToken ct)
        {
            string header = await ReadHttpHeader(stream, ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(header))
                return false;

            string key = null;
            string[] lines = header.Split(new[] { "\r\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon <= 0) continue;
                string name = lines[i].Substring(0, colon).Trim();
                if (string.Equals(name, "Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase))
                    key = lines[i].Substring(colon + 1).Trim();
            }

            if (string.IsNullOrEmpty(key))
            {
                byte[] bad = Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\n\r\n");
                await stream.WriteAsync(bad, 0, bad.Length, ct).ConfigureAwait(false);
                return false;
            }

            string accept;
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));
                accept = Convert.ToBase64String(hash);
            }

            string response =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                "Sec-WebSocket-Accept: " + accept + "\r\n\r\n";
            byte[] bytes = Encoding.ASCII.GetBytes(response);
            await stream.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
            return true;
        }

        private static async Task<string> ReadHttpHeader(NetworkStream stream, CancellationToken ct)
        {
            byte[] buffer = new byte[1];
            List<byte> bytes = new List<byte>();
            while (bytes.Count < 16 * 1024)
            {
                int read = await stream.ReadAsync(buffer, 0, 1, ct).ConfigureAwait(false);
                if (read <= 0) return null;
                bytes.Add(buffer[0]);
                int n = bytes.Count;
                if (n >= 4 && bytes[n - 4] == '\r' && bytes[n - 3] == '\n' && bytes[n - 2] == '\r' && bytes[n - 1] == '\n')
                    return Encoding.ASCII.GetString(bytes.ToArray());
            }

            return null;
        }

        private static async Task<string> ReadWebSocketText(NetworkStream stream, CancellationToken ct)
        {
            int b0 = await ReadByte(stream, ct).ConfigureAwait(false);
            if (b0 < 0) return null;
            int b1 = await ReadByte(stream, ct).ConfigureAwait(false);
            if (b1 < 0) return null;

            int opcode = b0 & 0x0f;
            bool masked = (b1 & 0x80) != 0;
            ulong length = (ulong)(b1 & 0x7f);
            if (length == 126)
            {
                byte[] ext = await ReadExact(stream, 2, ct).ConfigureAwait(false);
                length = (ulong)((ext[0] << 8) | ext[1]);
            }
            else if (length == 127)
            {
                byte[] ext = await ReadExact(stream, 8, ct).ConfigureAwait(false);
                length = 0;
                for (int i = 0; i < 8; i++) length = (length << 8) | ext[i];
            }

            byte[] mask = masked ? await ReadExact(stream, 4, ct).ConfigureAwait(false) : null;
            byte[] payload = await ReadExact(stream, checked((int)length), ct).ConfigureAwait(false);
            if (masked)
            {
                for (int i = 0; i < payload.Length; i++)
                    payload[i] = (byte)(payload[i] ^ mask[i % 4]);
            }

            if (opcode == 8) return null;
            if (opcode != 1) return string.Empty;
            return Encoding.UTF8.GetString(payload);
        }

        private static async Task WriteWebSocketText(NetworkStream stream, string text, CancellationToken ct)
        {
            byte[] payload = Encoding.UTF8.GetBytes(text ?? string.Empty);
            List<byte> frame = new List<byte>();
            frame.Add(0x81);
            if (payload.Length <= 125)
            {
                frame.Add((byte)payload.Length);
            }
            else if (payload.Length <= ushort.MaxValue)
            {
                frame.Add(126);
                frame.Add((byte)((payload.Length >> 8) & 0xff));
                frame.Add((byte)(payload.Length & 0xff));
            }
            else
            {
                frame.Add(127);
                ulong len = (ulong)payload.Length;
                for (int i = 7; i >= 0; i--)
                    frame.Add((byte)((len >> (8 * i)) & 0xff));
            }
            frame.AddRange(payload);
            byte[] bytes = frame.ToArray();
            await stream.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
        }

        private static async Task<int> ReadByte(NetworkStream stream, CancellationToken ct)
        {
            byte[] b = await ReadExact(stream, 1, ct).ConfigureAwait(false);
            return b == null || b.Length == 0 ? -1 : b[0];
        }

        private static async Task<byte[]> ReadExact(NetworkStream stream, int length, CancellationToken ct)
        {
            byte[] bytes = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                int read = await stream.ReadAsync(bytes, offset, length - offset, ct).ConfigureAwait(false);
                if (read <= 0) throw new InvalidOperationException("WebSocket stream closed.");
                offset += read;
            }
            return bytes;
        }

        // Parses a JSON-RPC envelope and dispatches. Returns null if the connection should close.
        private string ProcessRequest(string raw, ref bool handshaked)
        {
            JsonValue env;
            try { env = PreviewJson.Parse(raw); }
            catch (Exception ex) { return BuildError(null, PreviewError.InvalidRequest, "Parse error: " + ex.Message); }

            if (env == null || env.Kind != JsonKind.Object)
                return BuildError(null, PreviewError.InvalidRequest, "Envelope must be an object.");

            string idText = SerializeId(env.GetField("id"));
            string method = env.GetString("method");
            JsonValue paramsVal = env.GetField("params");

            if (string.IsNullOrEmpty(method))
                return BuildError(idText, PreviewError.InvalidRequest, "Missing 'method'.");

            try
            {
                switch (method)
                {
                    case "preview.handshake":
                        return HandleHandshake(idText, paramsVal, ref handshaked);
                    case "preview.loadPatch":
                    case "preview.applyBuff":
                    case "preview.reset":
                    case "preview.getSnapshot":
                    case "preview.getLogs":
                        if (!handshaked)
                            return BuildError(idText, PreviewError.NotHandshaked, "Handshake required.");
                        if (method == "preview.loadPatch") return HandleLoadPatch(idText, raw, paramsVal);
                        if (method == "preview.applyBuff") return HandleApplyBuff(idText, paramsVal);
                        if (method == "preview.reset") return HandleReset(idText, paramsVal);
                        if (method == "preview.getSnapshot") return HandleGetSnapshot(idText, paramsVal);
                        return HandleGetLogs(idText, paramsVal);
                    default:
                        return BuildError(idText, PreviewError.UnknownMethod, "Unknown method: " + method);
                }
            }
            catch (Exception ex)
            {
                _logs.Append("error", method + " failed: " + ex.Message);
                return BuildError(idText, PreviewError.InternalError, ex.Message);
            }
        }

        // ---------- handshake ----------
        private string HandleHandshake(string idText, JsonValue p, ref bool handshaked)
        {
            string token = p?.GetString("token");
            if (token != _token)
            {
                return BuildError(idText, PreviewError.TokenMismatch, "Token mismatch.");
            }
            handshaked = true;
            _logs.Append("info", "Handshake ok: " + (p?.GetString("clientName") ?? "unknown"));

            PreviewJson.Writer w = new PreviewJson.Writer().Begin();
            w.ObjStart()
                .KeyStr("jsonrpc", "2.0")
                .Key("id").Raw(idText)
                .Key("result").ObjStart()
                    .KeyStr("serverName", ServerName)
                    .KeyStr("gameVersion", _gameVersion)
                    .KeyStr("schemaVersion", SchemaVersion)
                    .Key("capabilities").ArrStart();
            for (int i = 0; i < s_capabilities.Length; i++) w.Str(s_capabilities[i]);
            w.ArrEnd().ObjEnd().ObjEnd();
            return w.ToString();
        }

        // ---------- loadPatch ----------
        private string HandleLoadPatch(string idText, string rawEnvelope, JsonValue p)
        {
            if (p == null || p.Kind != JsonKind.Object)
                return BuildError(idText, PreviewError.InvalidParams, "Missing params.");

            Stopwatch sw = Stopwatch.StartNew();
            string source = SerializeJsonValue(p);
            bool discardPrevious = p.GetBool("discardPrevious", true);

            IReadOnlyList<string> ids;
            Exception parseError = null;
            Exception loadError = null;
            IReadOnlyList<string> captured = null;
            RuntimePreviewConfigMetadata configMetadata = new RuntimePreviewConfigMetadata();
            _dispatcher.Invoke(() =>
            {
                try
                {
                    if (discardPrevious) _patchLoader.Clear();
                    captured = _patchLoader.LoadPatch(source);
                    _world.LoadPreviewPatch(source);
                    configMetadata = _adapter.ConfigMetadata;
                }
                catch (RuntimeConfigPatchParseException ex) { parseError = ex; }
                catch (FormatException fx) { parseError = fx; }
                catch (Exception ex) { loadError = ex; }
            });
            if (parseError != null)
                return BuildError(idText, PreviewError.PatchParseFailed, "Patch parse failed: " + parseError.Message);
            if (loadError != null)
                return BuildError(idText, PreviewError.PatchLoadRejected, "Patch load rejected: " + loadError.Message);

            var loadedIds = new List<string>();
            AddUniqueRange(loadedIds, captured);
            AddUniqueRange(loadedIds, configMetadata.LoadedPatchIds);
            ids = loadedIds;
            sw.Stop();

            _loadedPatchIds = new List<string>(ids);
            _logs.Append("info", $"loadPatch ok: {ids.Count} ids in {sw.ElapsedMilliseconds}ms previewMode={_adapter.PreviewMode}");

            PreviewJson.Writer w = new PreviewJson.Writer().Begin();
            w.ObjStart()
                .KeyStr("jsonrpc", "2.0")
                .Key("id").Raw(idText)
                .Key("result").ObjStart()
                    .Key("loadedPatchIds").ArrStart();
            for (int i = 0; i < ids.Count; i++) w.Str(ids[i]);
            w.ArrEnd()
                    .Key("rejectedPatches").ArrStart().ArrEnd()
                    .Key("mergeWarnings").ArrStart();
            for (int i = 0; i < configMetadata.MergeWarnings.Count; i++) w.Str(configMetadata.MergeWarnings[i]);
            w.ArrEnd()
                    .Key("configMetadata");
            WriteConfigMetadata(w, configMetadata);
            w
                    .KeyNum("elapsedMs", sw.ElapsedMilliseconds)
                .ObjEnd()
            .ObjEnd();
            return w.ToString();
        }

        // ---------- applyBuff ----------
        private string HandleApplyBuff(string idText, JsonValue p)
        {
            if (p == null || p.Kind != JsonKind.Object)
                return BuildError(idText, PreviewError.InvalidParams, "Missing params.");

            string buffId = p.GetString("buffId");
            string casterId = p.GetString("casterId") ?? "TestCaster";
            string targetId = p.GetString("targetId") ?? "TestTarget";
            int stack = (int)p.GetLong("stack", 1);
            long? duration = p.TryGetLong("durationOverrideMs", out long d) ? d : (long?)null;
            int waitTicks = (int)p.GetLong("waitTicks", 0);
            string requestId = p.GetString("requestId");

            Stopwatch totalSw = Stopwatch.StartNew();
            Stopwatch applySw = Stopwatch.StartNew();
            RuntimePreviewAdapterResult applyResult = null;
            Exception applyError = null;
            _dispatcher.Invoke(() =>
            {
                try
                {
                    applyResult = _adapter.ApplyBuff(new RuntimePreviewApplyBuffRequest
                    {
                        BuffId = buffId,
                        CasterId = casterId,
                        TargetId = targetId,
                        Stack = stack,
                        DurationOverrideMs = duration,
                        WaitTicks = waitTicks,
                    });
                }
                catch (Exception ex) { applyError = ex; }
            });
            applySw.Stop();
            if (applyError != null)
                return BuildError(idText, PreviewError.ApplyBuffFailed, applyError.Message);

            if (applyResult == null || !applyResult.Success)
            {
                RuntimePreviewAdapterResult failure = applyResult ?? RuntimePreviewAdapterResult.Fail(
                    PreviewError.ApplyBuffFailed,
                    "adapter_no_result",
                    "Runtime preview adapter did not return a result.",
                    _adapter.PreviewMode,
                    buffId,
                    targetId);
                totalSw.Stop();
                RuntimePreviewResult failureResult = BuildFailureResultFromAdapter(
                    requestId,
                    failure,
                    applySw.ElapsedMilliseconds,
                    waitTicks > 0 ? waitTicks : 0,
                    totalSw.ElapsedMilliseconds);
                return BuildError(idText, failure.ErrorCode, failure.ErrorMessage, failure, failureResult);
            }

            int tickCount = waitTicks > 0 ? waitTicks : 0;
            RuntimePreviewResult result = BuildResultFromSnapshot(requestId, buffId, true, applyResult.Snapshot);
            totalSw.Stop();
            result.Performance.LoadMs = 0;
            result.Performance.ApplyMs = applySw.ElapsedMilliseconds;
            result.Performance.TickCount = tickCount;
            result.Performance.TotalMs = totalSw.ElapsedMilliseconds;

            _logs.Append("info", $"applyBuff {buffId} -> {targetId} stack={stack} waitTicks={waitTicks}");
            return WrapResult(idText, result);
        }

        // ---------- reset ----------
        private string HandleReset(string idText, JsonValue p)
        {
            bool reload = p != null && p.GetBool("reloadBase", false);
            Stopwatch sw = Stopwatch.StartNew();
            _dispatcher.Invoke(() =>
            {
                _adapter.Reset(reload);
                _patchLoader.Clear();
            });
            _loadedPatchIds.Clear();
            _logs.Reset();
            sw.Stop();

            PreviewJson.Writer w = new PreviewJson.Writer().Begin();
            w.ObjStart()
                .KeyStr("jsonrpc", "2.0")
                .Key("id").Raw(idText)
                .Key("result").ObjStart().KeyNum("elapsedMs", sw.ElapsedMilliseconds).ObjEnd()
            .ObjEnd();
            return w.ToString();
        }

        // ---------- getSnapshot ----------
        private string HandleGetSnapshot(string idText, JsonValue p)
        {
            string targetId = p?.GetString("targetId") ?? "TestTarget";
            string requestId = p?.GetString("requestId");
            RuntimePreviewSnapshot snapshot = null;
            _dispatcher.Invoke(() => snapshot = _adapter.Snapshot(targetId));
            RuntimePreviewResult result = BuildResultFromSnapshot(requestId, null, true, snapshot);
            return WrapResult(idText, result);
        }

        // ---------- getLogs ----------
        private string HandleGetLogs(string idText, JsonValue p)
        {
            long after = p != null ? p.GetLong("afterSeq", 0) : 0;
            int max = p != null ? (int)p.GetLong("max", 200) : 200;
            IReadOnlyList<LogEntry> logs = _logs.GetSince(after, max, out long lastSeq);

            PreviewJson.Writer w = new PreviewJson.Writer().Begin();
            w.ObjStart()
                .KeyStr("jsonrpc", "2.0")
                .Key("id").Raw(idText)
                .Key("result").ObjStart()
                    .Key("logs").ArrStart();
            for (int i = 0; i < logs.Count; i++)
            {
                LogEntry e = logs[i];
                w.ObjStart()
                    .KeyNum("seq", e.Seq)
                    .KeyStr("level", e.Level)
                    .KeyStr("message", e.Message)
                    .KeyNum("atMs", e.AtMs)
                .ObjEnd();
            }
            w.ArrEnd().KeyNum("lastSeq", lastSeq).ObjEnd().ObjEnd();
            return w.ToString();
        }

        // ---------- helpers ----------
        private RuntimePreviewResult BuildResultFromSnapshot(
            string requestId,
            string appliedBuffId,
            bool success,
            RuntimePreviewSnapshot snapshot)
        {
            snapshot = snapshot ?? RuntimePreviewSnapshot.Empty(_adapter.PreviewMode);
            RuntimePreviewResult result = new RuntimePreviewResult
            {
                RequestId = requestId ?? string.Empty,
                Success = success,
                PreviewMode = snapshot.PreviewMode ?? string.Empty,
                AppliedBuffId = appliedBuffId ?? string.Empty,
            };
            result.LoadedPatchIds.AddRange(_loadedPatchIds);
            result.ConfigMetadata = snapshot.ConfigMetadata != null
                ? snapshot.ConfigMetadata.Clone()
                : _adapter.ConfigMetadata;
            AddUniqueRange(result.LoadedPatchIds, result.ConfigMetadata.LoadedPatchIds);

            result.BuffSnapshots.AddRange(snapshot.BuffSnapshots);
            result.AttributeChanges.AddRange(snapshot.AttributeChanges);
            result.DamageTicks.AddRange(snapshot.DamageTicks);
            result.StatusChanges.AddRange(snapshot.StatusChanges);
            result.Truncated = _logs.DroppedOldest;

            AppendMappingExplanationLogs(result);

            // Attach a tail of recent logs for convenience.
            IReadOnlyList<LogEntry> recent = _logs.GetSince(Math.Max(0, _logs.GetAll().Count - MaxInlineResultLogs), MaxInlineResultLogs, out long _ignored);
            for (int i = 0; i < recent.Count; i++) result.Logs.Add(recent[i]);

            return result;
        }

        private RuntimePreviewResult BuildFailureResultFromAdapter(
            string requestId,
            RuntimePreviewAdapterResult failure,
            long applyMs,
            int tickCount,
            long totalMs)
        {
            failure = failure ?? RuntimePreviewAdapterResult.Fail(
                PreviewError.ApplyBuffFailed,
                "adapter_no_result",
                "Runtime preview adapter did not return a result.",
                _adapter.PreviewMode,
                string.Empty,
                string.Empty);

            RuntimePreviewResult result = new RuntimePreviewResult
            {
                RequestId = requestId ?? string.Empty,
                Success = false,
                PreviewMode = failure.PreviewMode ?? _adapter.PreviewMode,
                AppliedBuffId = failure.BuffId ?? string.Empty,
                ConfigMetadata = _adapter.ConfigMetadata,
                Truncated = _logs.DroppedOldest,
            };
            result.LoadedPatchIds.AddRange(_loadedPatchIds);
            AddUniqueRange(result.LoadedPatchIds, result.ConfigMetadata.LoadedPatchIds);
            result.Errors.Add(new RuntimePreviewError
            {
                Code = failure.ErrorCode,
                Message = failure.ErrorMessage,
                Reason = failure.ErrorReason,
                PreviewMode = failure.PreviewMode,
                BuffId = failure.BuffId,
                TargetId = failure.TargetId,
            });
            result.Performance.ApplyMs = applyMs;
            result.Performance.TickCount = tickCount;
            result.Performance.TotalMs = totalMs;

            IReadOnlyList<LogEntry> recent = _logs.GetSince(Math.Max(0, _logs.GetAll().Count - MaxInlineResultLogs), MaxInlineResultLogs, out long _ignored);
            for (int i = 0; i < recent.Count; i++) result.Logs.Add(recent[i]);
            return result;
        }

        private void AppendMappingExplanationLogs(RuntimePreviewResult result)
        {
            if (result == null || !result.Success || string.IsNullOrEmpty(result.AppliedBuffId))
                return;

            if (result.BuffSnapshots.Count == 0)
            {
                _logs.Append("warn", $"RuntimePreviewResult: no buffSnapshots captured for appliedBuffId={result.AppliedBuffId}; target may not expose active buff diagnostics.");
                result.Truncated = result.Truncated || _logs.DroppedOldest;
            }

            if (result.AttributeChanges.Count == 0)
            {
                _logs.Append("info", $"RuntimePreviewResult: no attributeChanges captured for appliedBuffId={result.AppliedBuffId}; buff may be state-only or delta=0.");
                result.Truncated = result.Truncated || _logs.DroppedOldest;
            }

            if (result.DamageTicks.Count == 0)
            {
                _logs.Append("info", $"RuntimePreviewResult: no damageTicks captured for appliedBuffId={result.AppliedBuffId}; current framework config maps this buff through attributes/modifiers.");
                result.Truncated = result.Truncated || _logs.DroppedOldest;
            }
        }

        private static string WrapResult(string idText, RuntimePreviewResult r)
        {
            string json = BuildResultJson(idText, r);
            if (Encoding.UTF8.GetByteCount(json) <= MaxResultPayloadBytes)
                return json;

            r.Truncated = true;
            json = BuildResultJson(idText, r);
            while (r.Logs.Count > 0 && Encoding.UTF8.GetByteCount(json) > MaxResultPayloadBytes)
            {
                r.Logs.RemoveAt(0);
                json = BuildResultJson(idText, r);
            }

            return json;
        }

        private static string BuildResultJson(string idText, RuntimePreviewResult r)
        {
            PreviewJson.Writer w = new PreviewJson.Writer().Begin();
            w.ObjStart()
                .KeyStr("jsonrpc", "2.0")
                .Key("id").Raw(idText)
                .Key("result");
            WriteRuntimePreviewResult(w, r);
            w.ObjEnd();
            return w.ToString();
        }

        private static void WriteRuntimePreviewResult(PreviewJson.Writer w, RuntimePreviewResult r)
        {
            r = r ?? new RuntimePreviewResult();
            w.ObjStart()
                .KeyStr("requestId", r.RequestId)
                .KeyBool("success", r.Success)
                .KeyStr("previewMode", r.PreviewMode)
                .Key("loadedPatchIds").ArrStart();
            for (int i = 0; i < r.LoadedPatchIds.Count; i++) w.Str(r.LoadedPatchIds[i]);
            w.ArrEnd()
                .Key("configMetadata");
            WriteConfigMetadata(w, r.ConfigMetadata);
            w
                .KeyStr("appliedBuffId", r.AppliedBuffId)
                .Key("buffSnapshots").ArrStart();
            for (int i = 0; i < r.BuffSnapshots.Count; i++)
            {
                BuffSnapshot s = r.BuffSnapshots[i];
                w.ObjStart()
                    .KeyStr("buffId", s.BuffId)
                    .KeyStr("ownerId", s.OwnerId)
                    .KeyNum("stack", s.Stack)
                    .KeyNum("remainingMs", s.RemainingMs)
                    .KeyNum("totalMs", s.TotalMs)
                    .KeyStr("casterId", s.CasterId)
                    .KeyStr("addedAt", s.AddedAt)
                .ObjEnd();
            }
            w.ArrEnd()
                .Key("attributeChanges").ArrStart();
            for (int i = 0; i < r.AttributeChanges.Count; i++)
            {
                AttributeChange c = r.AttributeChanges[i];
                w.ObjStart()
                    .KeyStr("ownerId", c.OwnerId)
                    .KeyStr("attribute", c.Attribute)
                    .KeyNum("before", c.Before)
                    .KeyNum("after", c.After)
                    .KeyStr("deltaSource", c.DeltaSource)
                .ObjEnd();
            }
            w.ArrEnd()
                .Key("damageTicks").ArrStart();
            for (int i = 0; i < r.DamageTicks.Count; i++)
            {
                DamageTick d = r.DamageTicks[i];
                w.ObjStart()
                    .KeyStr("buffId", d.BuffId)
                    .KeyNum("tickIndex", d.TickIndex)
                    .KeyNum("amount", d.Amount)
                    .KeyStr("damageType", d.DamageType)
                    .KeyStr("elementType", d.ElementType)
                .ObjEnd();
            }
            w.ArrEnd()
                .Key("statusChanges").ArrStart();
            for (int i = 0; i < r.StatusChanges.Count; i++)
            {
                StatusChange s = r.StatusChanges[i];
                w.ObjStart()
                    .KeyStr("ownerId", s.OwnerId)
                    .KeyStr("status", s.Status)
                    .KeyBool("applied", s.Applied)
                .ObjEnd();
            }
            w.ArrEnd()
                .Key("logs").ArrStart();
            for (int i = 0; i < r.Logs.Count; i++)
            {
                LogEntry e = r.Logs[i];
                w.ObjStart()
                    .KeyNum("seq", e.Seq)
                    .KeyStr("level", e.Level)
                    .KeyStr("message", e.Message)
                    .KeyNum("atMs", e.AtMs)
                .ObjEnd();
            }
            w.ArrEnd()
                .Key("errors").ArrStart();
            for (int i = 0; i < r.Errors.Count; i++)
            {
                RuntimePreviewError er = r.Errors[i];
                w.ObjStart()
                    .KeyNum("code", er.Code)
                    .KeyStr("message", er.Message)
                    .KeyStr("reason", er.Reason)
                    .KeyStr("previewMode", er.PreviewMode)
                    .KeyStr("buffId", er.BuffId)
                    .KeyStr("targetId", er.TargetId)
                .ObjEnd();
            }
            w.ArrEnd()
                .Key("performance").ObjStart()
                    .KeyNum("loadMs", r.Performance.LoadMs)
                    .KeyNum("applyMs", r.Performance.ApplyMs)
                    .KeyNum("tickCount", r.Performance.TickCount)
                    .KeyNum("totalMs", r.Performance.TotalMs)
                .ObjEnd()
                .KeyBool("truncated", r.Truncated)
            .ObjEnd();
        }

        private static void WriteConfigMetadata(PreviewJson.Writer w, RuntimePreviewConfigMetadata metadata)
        {
            metadata = metadata ?? new RuntimePreviewConfigMetadata();
            w.ObjStart()
                .KeyStr("sourceId", metadata.SourceId)
                .KeyStr("layer", metadata.Layer)
                .Key("loadedPatchIds").ArrStart();
            for (int i = 0; i < metadata.LoadedPatchIds.Count; i++) w.Str(metadata.LoadedPatchIds[i]);
            w.ArrEnd()
                .Key("changedConfigIds").ArrStart();
            for (int i = 0; i < metadata.ChangedConfigIds.Count; i++) w.Str(metadata.ChangedConfigIds[i]);
            w.ArrEnd()
                .Key("failedConfigIds").ArrStart();
            for (int i = 0; i < metadata.FailedConfigIds.Count; i++) w.Str(metadata.FailedConfigIds[i]);
            w.ArrEnd()
                .Key("mergeWarnings").ArrStart();
            for (int i = 0; i < metadata.MergeWarnings.Count; i++) w.Str(metadata.MergeWarnings[i]);
            w.ArrEnd()
            .ObjEnd();
        }

        private static void AddUniqueRange(List<string> target, IReadOnlyList<string> source)
        {
            if (target == null || source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                string value = source[i];
                if (!string.IsNullOrEmpty(value) && !target.Contains(value))
                    target.Add(value);
            }
        }

        private static string BuildError(string idText, int code, string message)
        {
            return BuildError(idText, code, message, null);
        }

        private static string BuildError(
            string idText,
            int code,
            string message,
            RuntimePreviewAdapterResult adapterFailure)
        {
            return BuildError(idText, code, message, adapterFailure, null);
        }

        private static string BuildError(
            string idText,
            int code,
            string message,
            RuntimePreviewAdapterResult adapterFailure,
            RuntimePreviewResult result)
        {
            PreviewJson.Writer w = new PreviewJson.Writer().Begin();
            w.ObjStart()
                .KeyStr("jsonrpc", "2.0");
            if (string.IsNullOrEmpty(idText)) w.KeyNull("id"); else w.Key("id").Raw(idText);
            w.Key("error").ObjStart()
                .KeyNum("code", code)
                .KeyStr("message", message ?? string.Empty);
            if (adapterFailure != null)
            {
                w.Key("data").ObjStart()
                    .KeyStr("reason", adapterFailure.ErrorReason)
                    .KeyStr("previewMode", adapterFailure.PreviewMode)
                    .KeyStr("buffId", adapterFailure.BuffId)
                    .KeyStr("targetId", adapterFailure.TargetId);
                if (result != null)
                {
                    w.Key("result");
                    WriteRuntimePreviewResult(w, result);
                }
                w.ObjEnd();
            }
            w.ObjEnd().ObjEnd();
            return w.ToString();
        }

        // Re-serializes a JsonValue to JSON text (used to feed loadPatch raw source).
        private static string SerializeJsonValue(JsonValue v)
        {
            StringBuilder sb = new StringBuilder();
            SerializeJsonValueTo(sb, v);
            return sb.ToString();
        }

        private static void SerializeJsonValueTo(StringBuilder sb, JsonValue v)
        {
            if (v == null || v.Kind == JsonKind.Null) { sb.Append("null"); return; }
            switch (v.Kind)
            {
                case JsonKind.Bool: sb.Append(v.Bool ? "true" : "false"); break;
                case JsonKind.Number: sb.Append(v.Number.ToString("R", CultureInfo.InvariantCulture)); break;
                case JsonKind.String: AppendEscaped(sb, v.String); break;
                case JsonKind.Array:
                    sb.Append('[');
                    for (int i = 0; i < v.Array.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        SerializeJsonValueTo(sb, v.Array[i]);
                    }
                    sb.Append(']');
                    break;
                case JsonKind.Object:
                    sb.Append('{');
                    bool first = true;
                    foreach (KeyValuePair<string, JsonValue> kv in v.Object)
                    {
                        if (!first) sb.Append(','); first = false;
                        AppendEscaped(sb, kv.Key);
                        sb.Append(':');
                        SerializeJsonValueTo(sb, kv.Value);
                    }
                    sb.Append('}');
                    break;
            }
        }

        private static void AppendEscaped(StringBuilder sb, string s)
        {
            sb.Append('"');
            if (s == null) { sb.Append('"'); return; }
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // Re-serialize the JSON-RPC id field as-is (string or number, default null).
        private static string SerializeId(JsonValue v)
        {
            if (v == null || v.Kind == JsonKind.Null) return "null";
            if (v.Kind == JsonKind.Number) return ((long)v.Number).ToString(CultureInfo.InvariantCulture);
            if (v.Kind == JsonKind.String)
            {
                StringBuilder sb = new StringBuilder();
                AppendEscaped(sb, v.String);
                return sb.ToString();
            }
            return "null";
        }
    }

}
