using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static class AnimationPackageCommands
{
    private const string AnimationAuthoringDocumentRelativePath = "config/animation_authoring.json";

    public static int Dispatch(string[] args, JsonSerializerOptions options)
    {
        if (args.Length >= 2 && args[1] == "compile")
        {
            string packagePath = Program.RequireOption(args, "--package");
            string outDir = Program.GetOption(args, "--out", string.Empty);
            AnimationAuthoringCompileResult result = Compile(packagePath, options);

            if (!string.IsNullOrWhiteSpace(outDir))
            {
                WriteCompileOutputs(outDir, result, options);
                Console.WriteLine("animationCompileResult=" + Path.Combine(outDir, "animation_compile_result.json"));
                Console.WriteLine("animationSetDefinition=" + Path.Combine(outDir, "animation_set_definition.json"));
                Console.WriteLine("animationPackageExpectation=" + Path.Combine(outDir, "animation_package_expectation.json"));
                Console.WriteLine("animationResourcePlan=" + Path.Combine(outDir, "animation_resource_plan.json"));
                Console.WriteLine("animationClipRegistry=" + Path.Combine(outDir, "animation_clip_registry.json"));
                Console.WriteLine("animationValidationReport=" + Path.Combine(outDir, "animation_validation_report.json"));
                Console.WriteLine("runtimeResourceCatalog=" + Path.Combine(outDir, "runtime_resource_catalog.json"));
                Console.WriteLine("characterResourcePlan=" + Path.Combine(outDir, "character_resource_plan.json"));
                Console.WriteLine("audioCueManifest=" + Path.Combine(outDir, "audio_cue_manifest.json"));
            }
            else
            {
                Console.WriteLine(JsonSerializer.Serialize(result, options));
            }

            return result.AnimationValidationReport.HasBlockingIssues
                ? Program.ExitValidationBlocked
                : Program.ExitReady;
        }

        Console.Error.WriteLine("error: unknown animation command.");
        return Program.ExitToolError;
    }

    internal static AnimationAuthoringCompileResult Compile(string packageOrDocumentPath, JsonSerializerOptions options)
    {
        string documentPath = ResolveAnimationDocumentPath(packageOrDocumentPath);
        string packageRoot = ResolvePackageRootPath(documentPath);
        AnimationAuthoringPackage package = File.Exists(documentPath)
            ? JsonSerializer.Deserialize<AnimationAuthoringPackage>(File.ReadAllText(documentPath), options) ?? new AnimationAuthoringPackage()
            : CreatePackageFromManifest(packageRoot, options);

        CharacterPackageResourceCatalog catalog = new CharacterPackageResourceCatalog();
        string resourceCatalogPath = Path.Combine(packageRoot, "resource_catalog.json");
        if (File.Exists(resourceCatalogPath))
            catalog = JsonSerializer.Deserialize<CharacterPackageResourceCatalog>(File.ReadAllText(resourceCatalogPath), options) ?? new CharacterPackageResourceCatalog();

        return AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest
        {
            Package = package,
            PackageRootPath = packageRoot,
            ResourceCatalog = catalog
        });
    }

    internal static void WriteCompileOutputs(string outDir, AnimationAuthoringCompileResult result, JsonSerializerOptions options)
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "animation_compile_result.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result, options)));
        File.WriteAllText(Path.Combine(outDir, "animation_set_definition.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.AnimationSetDefinition, options)));
        File.WriteAllText(Path.Combine(outDir, "animation_package_expectation.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.AnimationPackageExpectation, options)));
        File.WriteAllText(Path.Combine(outDir, "animation_resource_plan.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.AnimationResourcePlan, options)));
        File.WriteAllText(Path.Combine(outDir, "animation_clip_registry.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.AnimationClipRegistry, options)));
        File.WriteAllText(Path.Combine(outDir, "animation_validation_report.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.AnimationValidationReport, options)));
        File.WriteAllText(Path.Combine(outDir, "runtime_resource_catalog.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.AnimationResourcePlan.RuntimeResourceCatalog, options)));
        File.WriteAllText(Path.Combine(outDir, "character_resource_plan.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.AnimationResourcePlan.CharacterResourcePlan, options)));
        File.WriteAllText(Path.Combine(outDir, "audio_cue_manifest.json"), EnsureTrailingNewline(JsonSerializer.Serialize(result.AnimationResourcePlan.AudioCueManifest, options)));
    }

    private static string ResolveAnimationDocumentPath(string packageOrDocumentPath)
    {
        string resolved = Path.GetFullPath(packageOrDocumentPath);
        if (File.Exists(resolved) || string.Equals(Path.GetExtension(resolved), ".json", StringComparison.OrdinalIgnoreCase))
            return resolved;
        return Path.Combine(resolved, AnimationAuthoringDocumentRelativePath);
    }

    private static string ResolvePackageRootPath(string documentPath)
    {
        string full = Path.GetFullPath(documentPath);
        if (string.Equals(Path.GetFileName(full), Path.GetFileName(AnimationAuthoringDocumentRelativePath), StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(Path.GetDirectoryName(full)!)!;
        return Path.GetDirectoryName(full)!;
    }

    private static AnimationAuthoringPackage CreatePackageFromManifest(string packageRoot, JsonSerializerOptions options)
    {
        string manifestPath = Path.Combine(packageRoot, "manifest.json");
        if (!File.Exists(manifestPath))
            return new AnimationAuthoringPackage();

        CharacterPackageManifest manifest = JsonSerializer.Deserialize<CharacterPackageManifest>(File.ReadAllText(manifestPath), options) ?? new CharacterPackageManifest();
        string packageId = manifest.PackageId ?? string.Empty;
        string stableId = manifest.StableId ?? string.Empty;
        string displayName = manifest.DisplayName ?? string.Empty;
        return new AnimationAuthoringPackage
        {
            PackageId = string.IsNullOrWhiteSpace(packageId) ? string.Empty : "animation." + packageId,
            StableId = string.IsNullOrWhiteSpace(stableId) ? string.Empty : stableId + ".animation",
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Animation Authoring" : displayName + " Animation"
        };
    }

    private static string EnsureTrailingNewline(string value)
    {
        value ??= string.Empty;
        return value.EndsWith("\n", StringComparison.Ordinal) ? value : value + "\n";
    }
}
