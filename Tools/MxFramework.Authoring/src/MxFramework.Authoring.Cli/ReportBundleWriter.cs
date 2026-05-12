using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static class ReportBundleWriter
{
    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        opts.Converters.Add(new JsonStringEnumConverter());
        opts.Converters.Add(new FieldValueJsonConverter());
        return opts;
    }

    public static ReportBundleIndex Write(string outputDirectory, ReportBundle bundle)
    {
        if (string.IsNullOrEmpty(outputDirectory))
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));

        Directory.CreateDirectory(outputDirectory);

        var index = new ReportBundleIndex
        {
            PackageId = bundle.Package.PackageId,
            Status = bundle.Validation.HasErrors ? "blocked" : "ready"
        };

        WriteJson(outputDirectory, "mod.json", bundle.Package, index);
        WriteJson(outputDirectory, "validation_report.json", bundle.Validation, index);
        WriteText(outputDirectory, "validation_report.txt", bundle.Validation.ToText(), index);
        WriteJson(outputDirectory, "merge_preview.json", bundle.MergePreviews, index);
        WriteJson(outputDirectory, "report_index.json", index, index);
        return index;
    }

    private static void WriteJson<T>(string outputDirectory, string fileName, T value, ReportBundleIndex index)
    {
        WriteText(outputDirectory, fileName, JsonSerializer.Serialize(value, JsonOptions), index);
    }

    private static void WriteText(string outputDirectory, string fileName, string text, ReportBundleIndex index)
    {
        string path = Path.Combine(outputDirectory, fileName);
        File.WriteAllText(path, text);
        if (!index.Files.Contains(fileName))
            index.Files.Add(fileName);
    }
}
