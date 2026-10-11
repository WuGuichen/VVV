using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static class ManifestReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static ProjectAuthoringManifest Read(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Manifest path is required.", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException(path);

        return JsonSerializer.Deserialize<ProjectAuthoringManifest>(File.ReadAllText(path), JsonOptions)
               ?? new ProjectAuthoringManifest();
    }
}
