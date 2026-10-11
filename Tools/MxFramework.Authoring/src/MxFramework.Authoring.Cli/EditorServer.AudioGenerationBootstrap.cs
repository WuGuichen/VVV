using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private static readonly object AudioBootstrapGate = new object();
    private static readonly ConcurrentDictionary<string, AudioGenerationBootstrapStatus> AudioBootstraps = new(StringComparer.Ordinal);
    private static string ActiveAudioBootstrapId = string.Empty;

    private static AudioGenerationBootstrapStatus EnsureAudioGenerationHost(string rootPath, JsonSerializerOptions options)
    {
        lock (AudioBootstrapGate)
        {
            if (!string.IsNullOrWhiteSpace(ActiveAudioBootstrapId)
                && AudioBootstraps.TryGetValue(ActiveAudioBootstrapId, out AudioGenerationBootstrapStatus active)
                && !active.IsTerminal)
                return active;

            string id = Guid.NewGuid().ToString("N");
            var status = new AudioGenerationBootstrapStatus
            {
                bootstrapId = id,
                status = "Checking",
                progress = 1,
                message = "正在检查 wg200 SFX Host",
                startedAtUtc = DateTime.UtcNow.ToString("O")
            };
            AudioBootstraps[id] = status;
            ActiveAudioBootstrapId = id;
            _ = Task.Run(() => RunAudioGenerationBootstrap(rootPath, status, options));
            return status;
        }
    }

    private static AudioGenerationBootstrapStatus GetAudioGenerationBootstrap(string id)
    {
        EnsureSafeGenerationId(id);
        if (!AudioBootstraps.TryGetValue(id, out AudioGenerationBootstrapStatus status))
            return new AudioGenerationBootstrapStatus
            {
                bootstrapId = id,
                status = "Failed",
                progress = 100,
                code = "AUDIO_HOST_BOOTSTRAP_NOT_FOUND",
                message = "找不到 SFX Host 启动任务",
                retryable = true,
                completedAtUtc = DateTime.UtcNow.ToString("O")
            };
        return status;
    }

    private static AudioGenerationBootstrapStatus GetActiveAudioGenerationBootstrap()
    {
        if (!string.IsNullOrWhiteSpace(ActiveAudioBootstrapId)
            && AudioBootstraps.TryGetValue(ActiveAudioBootstrapId, out AudioGenerationBootstrapStatus status))
            return status;
        return null;
    }

    private static void RunAudioGenerationBootstrap(string rootPath, AudioGenerationBootstrapStatus status, JsonSerializerOptions options)
    {
        string script = Path.Combine(rootPath, "Tools", "MxFramework.AudioGenerationHost", "tools", "manage_wg200.py");
        try
        {
            if (!File.Exists(script))
                throw new FileNotFoundException("SFX Host operator script is missing.", script);
            var start = new ProcessStartInfo
            {
                FileName = "python3",
                WorkingDirectory = rootPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            start.ArgumentList.Add(script);
            start.ArgumentList.Add("ensure");
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start SFX Host operator.");
            string line;
            while ((line = process.StandardOutput.ReadLine()) != null)
            {
                AudioGenerationBootstrapStatus update;
                try
                {
                    update = JsonSerializer.Deserialize<AudioGenerationBootstrapStatus>(line, options);
                }
                catch (JsonException)
                {
                    continue;
                }
                if (update == null)
                    continue;
                lock (status)
                {
                    status.status = update.status;
                    status.progress = update.progress;
                    status.message = update.message;
                    status.code = update.code;
                    status.detail = SanitizeAudioBootstrapDetail(update.detail);
                    status.retryable = update.retryable;
                    status.startupSeconds = update.startupSeconds;
                }
            }
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0 && !status.IsTerminal)
            {
                status.status = "Failed";
                status.progress = 100;
                status.code = "AUDIO_HOST_OPERATOR_FAILED";
                status.message = "SFX Host 冷启动失败";
                status.detail = SanitizeAudioBootstrapDetail(standardError);
                status.retryable = true;
            }
            else if (process.ExitCode == 0 && !string.Equals(status.status, "Ready", StringComparison.Ordinal))
            {
                status.status = "Ready";
                status.progress = 100;
                status.message = "SFX Host 已就绪";
            }
        }
        catch (Exception exception)
        {
            status.status = "Failed";
            status.progress = 100;
            status.code = "AUDIO_HOST_BOOTSTRAP_FAILED";
            status.message = "无法启动 SFX Host 管理任务";
            status.detail = SanitizeAudioBootstrapDetail(exception.Message);
            status.retryable = true;
        }
        finally
        {
            status.completedAtUtc = DateTime.UtcNow.ToString("O");
        }
    }

    private static string SanitizeAudioBootstrapDetail(string detail)
    {
        string value = (detail ?? string.Empty).Trim();
        if (value.Length > 1600)
            value = value.Substring(value.Length - 1600);
        string token = Environment.GetEnvironmentVariable("MX_AUDIO_GENERATION_TOKEN") ?? string.Empty;
        string hfToken = Environment.GetEnvironmentVariable("HF_TOKEN") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(token))
            value = value.Replace(token, "***", StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(hfToken))
            value = value.Replace(hfToken, "***", StringComparison.Ordinal);
        return value;
    }

    internal sealed class AudioGenerationBootstrapStatus
    {
        public string bootstrapId { get; set; } = string.Empty;
        public string status { get; set; } = "Checking";
        public int progress { get; set; }
        public string message { get; set; } = string.Empty;
        public string code { get; set; } = string.Empty;
        public string detail { get; set; } = string.Empty;
        public bool retryable { get; set; }
        public double startupSeconds { get; set; }
        public string startedAtUtc { get; set; } = string.Empty;
        public string completedAtUtc { get; set; } = string.Empty;
        [JsonIgnore]
        public bool IsTerminal => string.Equals(status, "Ready", StringComparison.Ordinal)
            || string.Equals(status, "Failed", StringComparison.Ordinal);
    }
}
