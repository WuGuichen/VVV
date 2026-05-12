using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MxFramework.Authoring.Preview.Protocol;

namespace MxFramework.Authoring.Preview;

/// <summary>
/// 基于 ClientWebSocket 的 Authoring -> Game 预览客户端，遵循 03 子需求 JSON-RPC 2.0 协议。
/// </summary>
public sealed class WebSocketPreviewClient : IPreviewClient, IAsyncDisposable, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Uri _endpoint;
    private readonly string _token;
    private readonly TimeSpan _defaultTimeout;
    private readonly ClientWebSocket _socket = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonRpcResponse>> _pending = new();
    private readonly CancellationTokenSource _readLoopCts = new();
    private int _nextId;
    private Task _readLoopTask;
    private bool _connected;

    public WebSocketPreviewClient(Uri endpoint, string token, TimeSpan? defaultTimeout = null)
    {
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        _token = token ?? string.Empty;
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(5);
    }

    public bool IsHandshaked { get; private set; }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_connected) return;
        try
        {
            await _socket.ConnectAsync(_endpoint, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new PreviewConnectionException($"failed to connect to preview server at {_endpoint}", ex);
        }
        _connected = true;
        _readLoopTask = Task.Run(ReadLoopAsync);
    }

    public async Task<HandshakeResult> HandshakeAsync(string clientName, string clientVersion, CancellationToken cancellationToken = default)
    {
        if (!_connected) await ConnectAsync(cancellationToken).ConfigureAwait(false);
        var parameters = new HandshakeParams
        {
            ClientName = clientName ?? "MxAuthoringEditor",
            ClientVersion = clientVersion ?? "0.3.0",
            Token = _token
        };
        HandshakeResult result = await CallAsync<HandshakeResult>("preview.handshake", parameters, cancellationToken, allowBeforeHandshake: true).ConfigureAwait(false);
        IsHandshaked = true;
        return result;
    }

    public Task<LoadPatchResult> LoadPatchAsync(LoadPatchParams parameters, CancellationToken cancellationToken = default)
        => CallAsync<LoadPatchResult>("preview.loadPatch", parameters, cancellationToken);

    public Task<RuntimePreviewResult> ApplyBuffAsync(ApplyBuffParams parameters, CancellationToken cancellationToken = default)
        => CallAsync<RuntimePreviewResult>("preview.applyBuff", parameters, cancellationToken);

    public Task<ResetResult> ResetAsync(ResetParams parameters, CancellationToken cancellationToken = default)
        => CallAsync<ResetResult>("preview.reset", parameters, cancellationToken);

    public Task<RuntimePreviewResult> GetSnapshotAsync(GetSnapshotParams parameters, CancellationToken cancellationToken = default)
        => CallAsync<RuntimePreviewResult>("preview.getSnapshot", parameters, cancellationToken);

    public Task<GetLogsResult> GetLogsAsync(GetLogsParams parameters, CancellationToken cancellationToken = default)
        => CallAsync<GetLogsResult>("preview.getLogs", parameters, cancellationToken);

    private async Task<TResult> CallAsync<TResult>(string method, object parameters, CancellationToken cancellationToken, bool allowBeforeHandshake = false)
    {
        if (!allowBeforeHandshake && !IsHandshaked)
            throw new PreviewNotHandshakedException();

        if (!_connected)
            throw new PreviewConnectionException("preview client is not connected");

        int id = Interlocked.Increment(ref _nextId);
        var request = new JsonRpcRequest
        {
            Jsonrpc = "2.0",
            Id = id,
            Method = method,
            Params = SerializeParams(parameters)
        };

        var tcs = new TaskCompletionSource<JsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);
        try
        {
            await _socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _pending.TryRemove(id, out _);
            throw new PreviewConnectionException($"failed to send '{method}'", ex);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_defaultTimeout);

        JsonRpcResponse response;
        try
        {
            using (timeoutCts.Token.Register(() =>
            {
                if (_pending.TryRemove(id, out TaskCompletionSource<JsonRpcResponse> waiter))
                    waiter.TrySetException(new PreviewTimeoutException(method));
            }))
            {
                response = await tcs.Task.ConfigureAwait(false);
            }
        }
        catch (PreviewProtocolException)
        {
            throw;
        }

        if (response.Error != null)
        {
            int code = response.Error.Code;
            string message = response.Error.Message ?? string.Empty;
            JsonElement? data = response.Error.Data;
            throw code switch
            {
                Protocol.PreviewError.NotHandshaked => new PreviewNotHandshakedException(message),
                Protocol.PreviewError.TokenMismatch => new PreviewTokenMismatchException(message),
                _ => new PreviewProtocolException(code, message, data)
            };
        }

        if (response.Result == null)
            return default;

        return JsonSerializer.Deserialize<TResult>(response.Result.Value.GetRawText(), JsonOptions);
    }

    private static JsonElement SerializeParams(object parameters)
    {
        if (parameters == null)
            return JsonDocument.Parse("{}").RootElement;
        string text = JsonSerializer.Serialize(parameters, parameters.GetType(), JsonOptions);
        return JsonDocument.Parse(text).RootElement;
    }

    private async Task ReadLoopAsync()
    {
        var buffer = new byte[16 * 1024];
        var ms = new System.IO.MemoryStream();
        try
        {
            while (!_readLoopCts.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _readLoopCts.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        FailAllPending(new PreviewConnectionException("preview server closed connection"));
                        return;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                string text = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
                if (string.IsNullOrEmpty(text)) continue;
                Dispatch(text);
            }
        }
        catch (Exception ex)
        {
            FailAllPending(new PreviewConnectionException("preview read loop terminated", ex));
        }
    }

    private void Dispatch(string text)
    {
        try
        {
            JsonRpcResponse response = JsonSerializer.Deserialize<JsonRpcResponse>(text, JsonOptions);
            if (response == null) return;
            if (_pending.TryRemove(response.Id, out TaskCompletionSource<JsonRpcResponse> tcs))
                tcs.TrySetResult(response);
        }
        catch
        {
        }
    }

    private void FailAllPending(Exception ex)
    {
        foreach (var kv in _pending)
        {
            if (_pending.TryRemove(kv.Key, out TaskCompletionSource<JsonRpcResponse> tcs))
                tcs.TrySetException(ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _readLoopCts.Cancel();
            if (_socket.State == WebSocketState.Open)
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client disposed", CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
        }
        _socket.Dispose();
        _readLoopCts.Dispose();
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
