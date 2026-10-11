using System.Text.Json;

namespace MxFramework.Authoring.Cli;

/// <summary>Resolves the concrete target of the latest Unity-owned resource artifact set.</summary>
internal static class ResourceMappingBuildTargetResolver
{
    private const string ReportRelativePath = "Assets/Config/MxFramework/ResourceBuildReports/global_resource_build_report.json";

    /// <remarks>AllocByDesign. Low-frequency authoring and command bootstrap only.</remarks>
    public static string Resolve(string projectRoot, string requestedBuildTarget)
    {
        string requested = (requestedBuildTarget ?? string.Empty).Trim();
        if (requested.Length > 0 && !string.Equals(requested, "ActiveBuildTarget", StringComparison.Ordinal))
            return requested;

        string root = Path.GetFullPath(projectRoot ?? Directory.GetCurrentDirectory());
        string reportPath = Path.Combine(root, ReportRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(reportPath))
            throw new InvalidOperationException("Unity has not published a concrete global resource build target yet. Build Player resources in Unity or pass --build-target explicitly.");

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(reportPath));
            if (document.RootElement.TryGetProperty("schemaVersion", out JsonElement schema) && schema.ValueKind == JsonValueKind.Number && schema.GetInt32() == 2 &&
                document.RootElement.TryGetProperty("buildTarget", out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                string target = (value.GetString() ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(target) && !string.Equals(target, "ActiveBuildTarget", StringComparison.Ordinal))
                    return target;
            }
        }
        catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException || ex is FormatException || ex is OverflowException)
        {
            throw new InvalidOperationException("Unity global resource build report is invalid: " + ex.Message, ex);
        }

        throw new InvalidOperationException("Unity global resource build report is not schema v2 or does not contain a concrete build target.");
    }
}
