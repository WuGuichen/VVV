using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private static AudioPromptOptimizeResponse OptimizeAudioPrompt(
        string rootPath,
        AudioPromptOptimizeRequest request,
        JsonSerializerOptions options)
    {
        AudioGenerationRecipeV2 fallback = request?.recipe ?? AudioGenerationBriefBuilder.BuildRecipe(
            request?.cue, request?.audioEvent, AudioAuthoringManifestV2.ComputeBindingHash(request?.cue, request?.audioEvent));
        fallback.AudioCueId = request?.cue?.AudioCueId ?? fallback.AudioCueId;
        fallback.EventDefinitionId = request?.audioEvent?.Id ?? fallback.EventDefinitionId;
        fallback.BindingHash = AudioAuthoringManifestV2.ComputeBindingHash(request?.cue, request?.audioEvent);
        AudioGenerationBriefBuilder.RebuildRecipePrompts(fallback);
        string tempRoot = Path.Combine(rootPath, "Temp", "MxFrameworkAuthoring", "Audio", "PromptOptimize", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string schemaPath = Path.Combine(tempRoot, "schema.json");
        string outputPath = Path.Combine(tempRoot, "result.json");
        try
        {
            File.WriteAllText(schemaPath, AudioPromptSchemaJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            var start = new ProcessStartInfo
            {
                FileName = "codex",
                WorkingDirectory = tempRoot,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (string argument in new[] { "exec", "--ephemeral", "--ignore-rules", "--skip-git-repo-check", "--sandbox", "read-only", "-c", "model_reasoning_effort=\"low\"", "--output-schema", schemaPath, "--output-last-message", outputPath, "--color", "never", "-C", tempRoot })
                start.ArgumentList.Add(argument);
            string model = Environment.GetEnvironmentVariable("MX_AUDIO_PROMPT_MODEL") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(model))
                model = "gpt-5.6-luna";
            start.ArgumentList.Add("--model");
            start.ArgumentList.Add(model);
            start.ArgumentList.Add("-");
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start Codex CLI.");
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            process.StandardInput.Write(BuildAudioPromptOptimizerInstruction(request, fallback, options));
            process.StandardInput.Close();
            if (!process.WaitForExit(180_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                return AudioPromptOptimizeResponse.Fallback(fallback, "AUTH_AUDIO_PROMPT_LLM_TIMEOUT", "Codex CLI 优化超时，已使用规则生成。", model);
            }
            _ = standardOutput.GetAwaiter().GetResult();
            string error = standardError.GetAwaiter().GetResult();
            if (process.ExitCode != 0 || !File.Exists(outputPath))
                return AudioPromptOptimizeResponse.Fallback(fallback, "AUTH_AUDIO_PROMPT_LLM_FAILED", "Codex CLI 不可用，已使用规则生成。", model, SanitizeAudioBootstrapDetail(error));
            AudioPromptOptimizerResult result = JsonSerializer.Deserialize<AudioPromptOptimizerResult>(File.ReadAllText(outputPath), options);
            if (result == null || string.IsNullOrWhiteSpace(result.positivePrompt) || result.layers == null)
                return AudioPromptOptimizeResponse.Fallback(fallback, "AUTH_AUDIO_PROMPT_SCHEMA_INVALID", "Codex CLI 返回无效结构，已使用规则生成。", model);
            fallback.PositivePrompt = result.positivePrompt.Trim();
            fallback.NegativePrompt = string.IsNullOrWhiteSpace(result.negativePrompt) ? fallback.NegativePrompt : result.negativePrompt.Trim();
            for (int i = 0; i < fallback.Layers.Count; i++)
            {
                AudioPromptOptimizerLayer resultLayer = result.layers.Find(item => string.Equals(item.name, fallback.Layers[i].Name, StringComparison.OrdinalIgnoreCase));
                if (resultLayer != null && !string.IsNullOrWhiteSpace(resultLayer.prompt))
                    fallback.Layers[i].Prompt = resultLayer.prompt.Trim();
            }
            fallback.DerivationProvider = "CodexCli";
            fallback.DerivationModel = model;
            fallback.PromptModified = false;
            fallback.UpdatedAtUtc = DateTime.UtcNow.ToString("O");
            return new AudioPromptOptimizeResponse { success = true, provider = "CodexCli", model = fallback.DerivationModel, summary = result.summary ?? string.Empty, recipe = fallback };
        }
        catch (Exception exception)
        {
            return AudioPromptOptimizeResponse.Fallback(fallback, "AUTH_AUDIO_PROMPT_LLM_FAILED", "Codex CLI 优化失败，已使用规则生成。", string.Empty, SanitizeAudioBootstrapDetail(exception.Message));
        }
        finally
        {
            try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    private static string BuildAudioPromptOptimizerInstruction(AudioPromptOptimizeRequest request, AudioGenerationRecipeV2 recipe, JsonSerializerOptions options)
    {
        return "You are an audio authoring prompt editor. Return JSON only. Preserve Cue identity, collision profile values, layer names/counts, spatial and loop facts. "
            + "For contact sounds, describe contact mode, impactor material/mass/construction/contact area, receiver material/mass/construction/contact area, angle/speed/intensity, then layer behavior. "
            + "For other sounds, write concise English SFX prompts in this order: source/action, material, weight/intensity, layer behavior, perspective/environment, sound direction, technical constraints. "
            + "Require dry, close-mic, isolated, game-ready, clear transient and controlled decay. Exclude music, speech, voice, ambience, noise, clipping, distortion, rhythmic repetition and baked large-room reverb.\nINPUT:\n"
            + JsonSerializer.Serialize(new { cue = request?.cue, audioEvent = request?.audioEvent, recipe }, options);
    }

    private const string AudioPromptSchemaJson = """
    {"type":"object","additionalProperties":false,"required":["positivePrompt","negativePrompt","summary","layers"],"properties":{"positivePrompt":{"type":"string"},"negativePrompt":{"type":"string"},"summary":{"type":"string"},"layers":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["name","prompt"],"properties":{"name":{"type":"string"},"prompt":{"type":"string"}}}}}}
    """;

    internal sealed class AudioPromptOptimizeRequest
    {
        public AudioCueAuthoringDefinition cue { get; set; }
        public AudioEventAuthoringDefinition audioEvent { get; set; }
        public AudioGenerationRecipeV2 recipe { get; set; }
    }
    internal sealed class AudioPromptOptimizeResponse
    {
        public bool success { get; set; }
        public bool fallback { get; set; }
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public string detail { get; set; } = string.Empty;
        public string provider { get; set; } = string.Empty;
        public string model { get; set; } = string.Empty;
        public string summary { get; set; } = string.Empty;
        public AudioGenerationRecipeV2 recipe { get; set; }
        public static AudioPromptOptimizeResponse Fallback(AudioGenerationRecipeV2 recipe, string code, string message, string model, string detail = "") => new AudioPromptOptimizeResponse
        {
            success = false, fallback = true, code = code, message = message, detail = detail,
            provider = "RuleFallback", model = model ?? string.Empty, recipe = recipe
        };
    }
    private sealed class AudioPromptOptimizerResult
    {
        public string positivePrompt { get; set; } = string.Empty;
        public string negativePrompt { get; set; } = string.Empty;
        public string summary { get; set; } = string.Empty;
        public List<AudioPromptOptimizerLayer> layers { get; set; } = new List<AudioPromptOptimizerLayer>();
    }
    private sealed class AudioPromptOptimizerLayer
    {
        public string name { get; set; } = string.Empty;
        public string prompt { get; set; } = string.Empty;
    }
}
