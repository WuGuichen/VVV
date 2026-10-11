using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private static string ComputeObjectSha256<T>(T value, JsonSerializerOptions options)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, options);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static AudioGenerationJob DeserializeGenerationJob(string body, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<AudioGenerationJob>(body, options) ?? new AudioGenerationJob();
    }

    private static AudioGenerationCandidate DeserializeGenerationCandidate(string body, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<AudioGenerationCandidate>(body, options) ?? new AudioGenerationCandidate();
    }

    private static AudioGenerationHttpResult SendAudioGeneration(HttpMethod method, string path, string body)
    {
        string endpoint = ResolveAudioGenerationEndpoint();
        string token = Environment.GetEnvironmentVariable("MX_AUDIO_GENERATION_TOKEN") ?? string.Empty;
        using var request = new HttpRequestMessage(method, endpoint + path);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        try
        {
            using HttpResponseMessage response = AudioGenerationClient.Send(request);
            string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return new AudioGenerationHttpResult((int)response.StatusCode, responseBody, null);
        }
        catch (TaskCanceledException exception)
        {
            return AudioGenerationHttpResult.Failed(504, "AUDIO_GENERATION_HOST_TIMEOUT", "SFX 生成服务响应超时", exception.Message);
        }
        catch (HttpRequestException exception)
        {
            return AudioGenerationHttpResult.Failed(503, "AUDIO_GENERATION_HOST_UNAVAILABLE", "SFX 生成服务未连接", exception.Message);
        }
    }

    private static byte[] SendAudioGenerationBytes(string path)
    {
        string endpoint = ResolveAudioGenerationEndpoint();
        string token = Environment.GetEnvironmentVariable("MX_AUDIO_GENERATION_TOKEN") ?? string.Empty;
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using HttpResponseMessage response = AudioGenerationClient.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                int status = response.StatusCode == HttpStatusCode.Unauthorized ? 502 : (int)response.StatusCode;
                throw AudioGenerationTransportException.Provider(status, ExtractProviderError(responseBody));
            }
            return response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        }
        catch (TaskCanceledException exception)
        {
            throw AudioGenerationTransportException.Timeout(exception.Message);
        }
        catch (HttpRequestException exception)
        {
            throw AudioGenerationTransportException.Unavailable(exception.Message);
        }
    }

    private static void WriteAudioGenerationJson(HttpListenerContext context, AudioGenerationHttpResult result, JsonSerializerOptions options, int successStatus = 200)
    {
        if (result.failure != null)
        {
            WriteJson(context.Response, result.failure, options, result.status);
            return;
        }
        if (result.status < 200 || result.status >= 300)
        {
            WriteJson(context.Response, new { error = "AUDIO_GENERATION_PROVIDER_ERROR", message = ExtractProviderError(result.body) }, options, result.status == 401 ? 502 : result.status);
            return;
        }
        object value = JsonSerializer.Deserialize<object>(result.body, options) ?? new { };
        WriteJson(context.Response, value, options, successStatus);
    }

    private static string ResolveAudioGenerationEndpoint()
    {
        string endpoint = (Environment.GetEnvironmentVariable("MX_AUDIO_GENERATION_ENDPOINT") ?? "http://192.168.1.200:10188").TrimEnd('/');
        return string.Equals(endpoint, "http://192.168.1.200:9890", StringComparison.OrdinalIgnoreCase)
            ? "http://192.168.1.200:10188"
            : endpoint;
    }

    private static void ProxyAudioCandidate(HttpListenerResponse response, string jobId, string candidateId)
    {
        byte[] bytes = SendAudioGenerationBytes("/v1/sfx/jobs/" + jobId + "/candidates/" + candidateId);
        response.StatusCode = 200;
        response.ContentType = "audio/wav";
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.Close();
    }

    private static string ExtractProviderError(string body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("detail", out JsonElement detail))
                return detail.ToString();
        }
        catch (JsonException) { }
        return string.IsNullOrWhiteSpace(body) ? "Audio generation provider is unavailable." : body;
    }

    private static void EnsureSafeGenerationId(string value)
    {
        if (!IsSafeAudioSegment(value) || value.Length > 64)
            throw new ArgumentException("generation id is invalid");
    }

    private static bool IsSafeAudioSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        for (int i = 0; i < value.Length; i++)
            if (!char.IsLetterOrDigit(value[i]) && value[i] != '-' && value[i] != '_')
                return false;
        return true;
    }

    private static string SafeAudioSegment(string value)
    {
        if (!IsSafeAudioSegment(value))
            throw new ArgumentException("audio path segment is invalid: " + value);
        return value;
    }

    internal static string SafeAudioProjectSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            throw new ArgumentException("audio project segment is invalid");
        var result = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (char.IsLetterOrDigit(character) || character == '-' || character == '_')
                result.Append(character);
            else if (character == '.')
                result.Append('_');
            else
                throw new ArgumentException("audio project segment contains an unsupported character");
        }
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).Substring(0, 8).ToLowerInvariant();
        return result.Append("__").Append(hash).ToString();
    }

    private readonly record struct AudioGenerationHttpResult(int status, string body, AudioGenerationFailure failure)
    {
        public static AudioGenerationHttpResult Failed(int status, string code, string message, string detail)
        {
            return new AudioGenerationHttpResult(status, string.Empty, new AudioGenerationFailure
            {
                code = code,
                message = message,
                detail = detail,
                retryable = true
            });
        }
    }

    internal class AudioGenerationFailure
    {
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public string detail { get; set; } = string.Empty;
        public bool retryable { get; set; }
    }

    internal sealed class AudioGenerationHealthStatus : AudioGenerationFailure
    {
        public string status { get; set; } = "offline";
        public bool startupAvailable { get; set; }
        public AudioGenerationBootstrapStatus bootstrapStatus { get; set; }
    }

    private sealed class AudioGenerationTransportException : Exception
    {
        private AudioGenerationTransportException(int statusCode, AudioGenerationFailure failure) : base(failure.message)
        {
            StatusCode = statusCode;
            Failure = failure;
        }

        public int StatusCode { get; }
        public AudioGenerationFailure Failure { get; }

        public static AudioGenerationTransportException Timeout(string detail) =>
            new AudioGenerationTransportException(504, AudioGenerationHttpResult.Failed(504, "AUDIO_GENERATION_HOST_TIMEOUT", "SFX 生成服务响应超时", detail).failure);

        public static AudioGenerationTransportException Unavailable(string detail) =>
            new AudioGenerationTransportException(503, AudioGenerationHttpResult.Failed(503, "AUDIO_GENERATION_HOST_UNAVAILABLE", "SFX 生成服务未连接", detail).failure);

        public static AudioGenerationTransportException Provider(int statusCode, string detail) =>
            new AudioGenerationTransportException(statusCode, new AudioGenerationFailure
            {
                code = "AUDIO_GENERATION_PROVIDER_ERROR",
                message = "SFX 生成服务返回错误",
                detail = detail,
                retryable = statusCode >= 500
            });
    }
}
