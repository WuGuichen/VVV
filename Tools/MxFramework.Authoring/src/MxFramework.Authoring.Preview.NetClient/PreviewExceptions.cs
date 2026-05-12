using System;
using System.Text.Json;

namespace MxFramework.Authoring.Preview;

public class PreviewProtocolException : Exception
{
    public int ErrorCode { get; }
    public JsonElement? ErrorData { get; }

    public PreviewProtocolException(int errorCode, string message, JsonElement? data = null, Exception inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
        ErrorData = data;
    }
}

public sealed class PreviewNotHandshakedException : PreviewProtocolException
{
    public PreviewNotHandshakedException(string message = "preview client has not completed handshake")
        : base(Protocol.PreviewError.NotHandshaked, message)
    {
    }
}

public sealed class PreviewTokenMismatchException : PreviewProtocolException
{
    public PreviewTokenMismatchException(string message = "preview server rejected token")
        : base(Protocol.PreviewError.TokenMismatch, message)
    {
    }
}

public sealed class PreviewConnectionException : PreviewProtocolException
{
    public PreviewConnectionException(string message, Exception inner = null)
        : base(Protocol.PreviewError.InternalError, message, null, inner)
    {
    }
}

public sealed class PreviewTimeoutException : PreviewProtocolException
{
    public PreviewTimeoutException(string method)
        : base(Protocol.PreviewError.InternalError, $"preview rpc '{method}' timed out")
    {
    }
}
