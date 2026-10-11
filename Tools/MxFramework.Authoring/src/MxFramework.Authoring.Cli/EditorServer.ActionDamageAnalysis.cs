using System.Net;
using System.Text.Json;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string CharacterActionDamageAnalysisRelativePath =
        "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json";

    private static bool TryHandleActionDamageAnalysisApi(
        HttpListenerContext context,
        string rootPath,
        JsonSerializerOptions jsonOptions,
        string path)
    {
        if (!string.Equals(path, "/api/character-actions/damage-analysis", StringComparison.Ordinal))
            return false;

        if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            WriteJson(context.Response, new { error = "Method not allowed." }, jsonOptions, 405);
            return true;
        }

        string sourcePath = Path.Combine(rootPath, CharacterActionDamageAnalysisRelativePath);
        if (!File.Exists(sourcePath))
        {
            WriteJson(context.Response, new
            {
                sourcePath = CharacterActionDamageAnalysisRelativePath,
                exists = false,
                actions = Array.Empty<object>(),
                diagnostics = new[]
                {
                    new
                    {
                        severity = "Error",
                        code = "ACTION_DAMAGE_SOURCE_MISSING",
                        path = CharacterActionDamageAnalysisRelativePath,
                        message = "Character action JSON was not found."
                    }
                }
            }, jsonOptions);
            return true;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(sourcePath));
            JsonElement root = document.RootElement.Clone();
            JsonElement actions = root.TryGetProperty("actions", out JsonElement actionArray)
                && actionArray.ValueKind == JsonValueKind.Array
                    ? actionArray.Clone()
                    : JsonSerializer.SerializeToElement(Array.Empty<object>(), jsonOptions);

            WriteJson(context.Response, new
            {
                sourcePath = CharacterActionDamageAnalysisRelativePath,
                exists = true,
                actions,
                diagnostics = Array.Empty<object>()
            }, jsonOptions);
        }
        catch (JsonException ex)
        {
            WriteJson(context.Response, new
            {
                sourcePath = CharacterActionDamageAnalysisRelativePath,
                exists = true,
                actions = Array.Empty<object>(),
                diagnostics = new[]
                {
                    new
                    {
                        severity = "Error",
                        code = "ACTION_DAMAGE_JSON_INVALID",
                        path = CharacterActionDamageAnalysisRelativePath,
                        message = ex.Message
                    }
                }
            }, jsonOptions);
        }

        return true;
    }
}
