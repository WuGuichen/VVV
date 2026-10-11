using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal sealed class PackageReadResult
{
    public ModPackageManifest Manifest { get; set; } = new();
    public List<PatchDocument> Patches { get; } = new();
}

internal static class PackageReader
{
    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        opts.Converters.Add(new JsonStringEnumConverter());
        opts.Converters.Add(new FieldValueJsonConverter());
        return opts;
    }

    public static PackageReadResult Read(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Package path is required.", nameof(path));

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException(path);

        var result = new PackageReadResult();
        string manifestPath = Path.Combine(path, "mod.json");
        if (File.Exists(manifestPath))
        {
            result.Manifest = JsonSerializer.Deserialize<ModPackageManifest>(File.ReadAllText(manifestPath), JsonOptions) ?? new ModPackageManifest();
        }

        string patchesPath = Path.Combine(path, "patches");
        if (Directory.Exists(patchesPath))
        {
            foreach (string patchPath in Directory.GetFiles(patchesPath, "*.patch.json").OrderBy(p => p, StringComparer.Ordinal))
            {
                PatchDocument patch = JsonSerializer.Deserialize<PatchDocument>(File.ReadAllText(patchPath), JsonOptions) ?? new PatchDocument();
                NormalizePatch(patch);
                result.Patches.Add(patch);
            }
        }

        return result;
    }

    private static void NormalizePatch(PatchDocument patch)
    {
        for (int i = 0; i < patch.Entries.Count; i++)
        {
            PatchEntry entry = patch.Entries[i];
            if (string.IsNullOrEmpty(entry.Source))
                entry.Source = patch.Source;
        }
    }
}
