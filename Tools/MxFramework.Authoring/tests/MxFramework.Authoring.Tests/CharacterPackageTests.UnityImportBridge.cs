using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    private static void UnityImportBridge_PropagatesPaletteBindingAndHashesIt()
    {
        string root = Path.Combine(
            FindRepoRoot(),
            "Temp",
            "MxFrameworkAuthoringTests",
            "palette-binding-import");
        ResetDirectory(root);
        string packageCopy = Path.Combine(root, "package-copy");
        CopyDirectory(FindSamplePath("character-codex-flow-test"), packageCopy);
        SeedGlobalResourceMappingProfile(root);
        string paletteRelative = "config/character_palette_binding.json";
        string paletteUsageRelative = "config/character_palette_usage.json";
        string manifestPath = Path.Combine(packageCopy, "manifest.json");
        CharacterPackageManifest manifest = JsonSerializer.Deserialize<CharacterPackageManifest>(
            File.ReadAllText(manifestPath),
            JsonOptions) ?? new CharacterPackageManifest();
        if (!manifest.ConfigFiles.Contains(paletteRelative, StringComparer.Ordinal))
            manifest.ConfigFiles.Add(paletteRelative);
        if (!manifest.ConfigFiles.Contains(paletteUsageRelative, StringComparer.Ordinal))
            manifest.ConfigFiles.Add(paletteUsageRelative);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
        string palettePath = Path.Combine(packageCopy, "config", "character_palette_binding.json");
        string paletteBindingV4 =
            "{\"format\":\"mx.characterPaletteBinding.v5\",\"schemaVersion\":\"5.0\"," 
            + "\"enabled\":true,\"colorAtlasResourceKey\":\"wgame.character.color_palette.shared\","
            + "\"addressingMode\":\"colorUvSurfaceProfileVertexAlpha\",\"atlasWidth\":256,\"atlasHeight\":256,"
            + "\"surfaceProfileSource\":\"paletteDataAlpha16\",\"surfaceProfileCount\":16,\"faceDetailEnabled\":false,\"cullMode\":\"Back\"}";
        File.WriteAllText(palettePath, paletteBindingV4);
        string paletteUsagePath = Path.Combine(packageCopy, "config", "character_palette_usage.json");
        string paletteUsage = "{\"format\":\"mx.characterPaletteUsage.v2\",\"schemaVersion\":\"2.0\","
            + "\"packageId\":\"codex_flow_test\",\"catalogHash\":\"sha256:catalog\","
            + "\"modelHash\":\"sha256:model\",\"paletteRepaintRequired\":true,\"invalidFaceCount\":1,\"tokenUsages\":[]}";
        File.WriteAllText(paletteUsagePath, paletteUsage);

        int firstExit = CharacterPackageCommands.Dispatch(new[]
        {
            "character",
            "import-unity",
            "--package",
            packageCopy,
            "--project-root",
            root,
            "--build-target",
            "StandaloneWindows64",
            "--check-files"
        }, JsonOptions);
        string targetRoot = Path.Combine(
            root,
            "Assets",
            "MxFrameworkGenerated",
            "CharacterPackages",
            "codex_flow_test");
        string generatedPalettePath = Path.Combine(
            targetRoot,
            "config",
            "character_palette_binding.json");
        string generatedPaletteUsagePath = Path.Combine(
            targetRoot,
            "config",
            "character_palette_usage.json");
        Require(firstExit == MxFramework.Authoring.Cli.Program.ExitReady,
            "palette-enabled Unity import should be ready.");
        Require(File.Exists(generatedPalettePath)
            && string.Equals(
                File.ReadAllText(generatedPalettePath).TrimEnd(),
                paletteBindingV4,
                StringComparison.Ordinal),
            "Unity import should propagate the package-local palette binding to generated config.");
        Require(File.Exists(generatedPaletteUsagePath)
            && string.Equals(File.ReadAllText(generatedPaletteUsagePath).TrimEnd(), paletteUsage, StringComparison.Ordinal),
            "Unity import should propagate the versioned palette usage sidecar.");
        string firstSourceHash = ReadCompileSummaryHash(
            Path.Combine(targetRoot, "package_cache", "compile_summary.json"),
            "sourcePackageHash");
        Require(!File.Exists(Path.Combine(targetRoot, "package_cache", "compile_result.json")),
            "Unity import must not write the deprecated full compile snapshot.");
        Require(File.Exists(Path.Combine(targetRoot, "package_cache", "compile_summary.json")),
            "Unity import must publish the bounded compile summary.");

        string updatedPaletteBindingV5 = paletteBindingV4.Replace("\"cullMode\":\"Back\"", "\"cullMode\":\"Off\"");
        File.WriteAllText(palettePath, updatedPaletteBindingV5);
        int secondExit = CharacterPackageCommands.Dispatch(new[]
        {
            "character",
            "import-unity",
            "--package",
            packageCopy,
            "--project-root",
            root,
            "--build-target",
            "StandaloneWindows64",
            "--check-files"
        }, JsonOptions);
        string secondSourceHash = ReadCompileSummaryHash(
            Path.Combine(targetRoot, "package_cache", "compile_summary.json"),
            "sourcePackageHash");
        Require(secondExit == MxFramework.Authoring.Cli.Program.ExitReady,
            "updated palette binding Unity import should remain ready.");
        Require(string.Equals(
                File.ReadAllText(generatedPalettePath).TrimEnd(),
                updatedPaletteBindingV5,
                StringComparison.Ordinal),
            "Unity import should update generated palette binding content.");
        Require(!string.IsNullOrWhiteSpace(firstSourceHash)
            && !string.Equals(firstSourceHash, secondSourceHash, StringComparison.Ordinal),
            "palette binding changes should invalidate the source package compiler hash.");
    }

    /// <summary>从轻量摘要读取 compilerHashes 中的指定字段；摘要缺失时返回空串。</summary>
    private static string ReadCompileSummaryHash(string summaryPath, string hashField)
    {
        if (!File.Exists(summaryPath))
            return string.Empty;

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(summaryPath));
        if (!document.RootElement.TryGetProperty("compilerHashes", out JsonElement hashes))
            return string.Empty;

        return hashes.TryGetProperty(hashField, out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;
    }

    private static void UnityImportBridge_ImportsCodexFlowTestAndSkipsRepeat()
    {
        string root = Path.Combine(FindRepoRoot(), "Temp", "MxFrameworkAuthoringTests", "issue222-import-ready");
        ResetDirectory(root);
        SeedGlobalResourceMappingProfile(root);

        int firstExit = CharacterPackageCommands.Dispatch(new[]
        {
            "character",
            "import-unity",
            "--package",
            FindSamplePath("character-codex-flow-test"),
            "--project-root",
            root,
            "--build-target",
            "StandaloneWindows64",
            "--check-files",
            "--check-hashes"
        }, MxFramework.Authoring.Cli.Program.CreateJsonOptions());

        string targetRoot = Path.Combine(root, "Assets", "MxFrameworkGenerated", "CharacterPackages", "codex_flow_test");
        string reportPath = Path.Combine(targetRoot, "package_cache", "import_report.json");
        string catalogPath = Path.Combine(targetRoot, "config", "unity_resource_catalog.json");
        string targetModelPath = Path.Combine(targetRoot, "resources", "models", "character_00.fbx");
        Require(firstExit == MxFramework.Authoring.Cli.Program.ExitReady, "first Unity import should be ready.");
        Require(File.Exists(targetModelPath), "Unity import should copy model resource.");
        Require(File.Exists(Path.Combine(targetRoot, "config", "character_config_patch.json")), "Unity import should write the single formal config patch target.");
        Require(!File.Exists(Path.Combine(targetRoot, "generated", "character_application_config_patch.json")), "Unity import must not write the deprecated generated config patch mirror.");
        Require(File.Exists(Path.Combine(targetRoot, "config", "geometry_binding.json")), "Unity import should write readable config geometry binding alias.");
        Require(!File.Exists(Path.Combine(targetRoot, "generated", "character_geometry_binding.json")), "Unity import must not write the deprecated generated geometry binding mirror.");
        Require(!File.Exists(Path.Combine(targetRoot, "generated", "character_resource_mapping.json")), "Unity import must not write the deprecated generated resource mapping mirror.");
        Require(File.Exists(catalogPath), "Unity import should write project ResourceCatalog JSON.");
        Require(File.Exists(Path.Combine(targetRoot, "config", "animation_set_definition.json")), "Unity import should write runtime animation set definition.");
        Require(File.Exists(Path.Combine(targetRoot, "config", "animation_clip_registry.json")), "Unity import should write runtime animation clip registry.");
        Require(File.Exists(Path.Combine(targetRoot, "config", "animation_resource_plan.json")), "Unity import should write runtime animation resource plan.");
        Require(File.Exists(Path.Combine(targetRoot, "config", "character_resource_plan.json")), "Unity import should write character resource plan.");
        Require(File.Exists(Path.Combine(targetRoot, "package_cache", "animation_package_expectation.json")), "Unity import should preserve animation package expectation in package cache.");
        Require(File.Exists(Path.Combine(targetRoot, "package_cache", "animation_validation_report.json")), "Unity import should preserve animation validation report in package cache.");
        Require(File.Exists(Path.Combine(targetRoot, "package_cache", "compile_summary.json")), "Unity import should publish the bounded compile summary.");
        Require(!File.Exists(Path.Combine(targetRoot, "package_cache", "animation_compile_result.json")), "Unity import must not write the deprecated animation compile snapshot.");
        Require(!File.Exists(Path.Combine(targetRoot, "package_cache", "compile_result.json")), "Unity import must not write the deprecated compile snapshot.");

        CharacterUnityImportReport firstReport = JsonSerializer.Deserialize<CharacterUnityImportReport>(File.ReadAllText(reportPath), JsonOptions);
        Require(firstReport != null && firstReport.Status == CharacterAuthoringCompilerStatus.Ready.ToString(), "first import report should be Ready.");
        Require(firstReport.AddedCount > 0, "first import should add files.");
        CharacterUnityImportOperation modelOperation = firstReport.Operations.Find(operation => operation.SourcePath == "resources/models/character_00.fbx");
        Require(modelOperation != null && modelOperation.ArtifactStatus == "SourceUnchanged", "FBX model import should be tracked as an unchanged source artifact.");
        Require(firstReport.Operations.Exists(operation => operation.TargetPath.EndsWith("config/animation_set_definition.json", StringComparison.Ordinal)), "import report should include animation set definition write.");
        Require(firstReport.Operations.Exists(operation => operation.TargetPath.EndsWith("config/animation_clip_registry.json", StringComparison.Ordinal)), "import report should include animation clip registry write.");
        Require(firstReport.Operations.Exists(operation => operation.TargetPath.EndsWith("config/animation_resource_plan.json", StringComparison.Ordinal)), "import report should include animation resource plan write.");
        string catalogJson = File.ReadAllText(catalogPath);
        Require(catalogJson.Contains("\"format\": \"mx.characterUnityResourceCatalog.v1\""), "generated Unity ResourceCatalog should declare C2 catalog format.");
        Require(catalogJson.Contains("\"packageResourceKey\": \"char.codex_flow_test.model.body\""), "generated Unity ResourceCatalog should expose package resource key as a top-level ledger field.");
        Require(catalogJson.Contains("\"stableId\": \"charpkg.codex_flow_test.resource.model.body\""), "generated Unity ResourceCatalog should expose stable id as a top-level ledger field.");
        Require(catalogJson.Contains("\"sourceRelativePath\": \"resources/models/character_00.fbx\""), "generated Unity ResourceCatalog should expose source relative path.");
        Require(catalogJson.Contains("\"contentHash\": \"sha256:"), "generated Unity ResourceCatalog should expose resolved source content hash.");
        Require(catalogJson.Contains("\"sourceHash\": \"sha256:"), "generated Unity ResourceCatalog should expose source hash separately from artifact hash.");
        Require(catalogJson.Contains("\"artifactHash\": \"sha256:"), "generated Unity ResourceCatalog should expose Unity import artifact hash.");
        Require(catalogJson.Contains("\"artifactTransforms\": ["), "generated Unity ResourceCatalog should expose artifact transform ledger.");
        Require(catalogJson.Contains("\"importHash\": \"sha256:"), "generated Unity ResourceCatalog should expose import hash.");
        Require(catalogJson.Contains("\"diagnostics\": []"), "clean import-ready resources should have empty structured diagnostics.");
        Require(catalogJson.Contains("\"orphanedUnityAssets\": []"), "generated Unity ResourceCatalog should expose deterministic orphan list.");
        Require(catalogJson.Contains("\"provider\": \"memory\""), "generated Unity ResourceCatalog should use v1 memory provider bridge.");
        Require(catalogJson.Contains("\"assetPath\": \"Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test/resources/models/character_00.fbx\""), "generated ResourceCatalog should preserve Unity assetPath mapping.");
        Require(catalogJson.Contains("\"unityAssetPath\": \"Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test/resources/models/character_00.fbx\""), "generated ResourceCatalog should expose Unity asset path for editor resolution.");
        Require(catalogJson.Contains("\"unityAssetGuid\": \"\""), "CLI-generated ResourceCatalog should leave Unity GUID empty until AssetDatabase enrichment.");
        Require(catalogJson.Contains("\"unityMainObjectType\": \"\""), "CLI-generated ResourceCatalog should leave Unity main object type empty until AssetDatabase enrichment.");
        Require(catalogJson.Contains("\"importerKind\": \"unity-fbx\""), "generated ResourceCatalog should record expected Unity importer kind.");
        Require(catalogJson.Contains("\"importStatus\": \"PendingUnityImport\""), "CLI-generated ResourceCatalog should mark entries pending Unity AssetDatabase import.");
        Require(catalogJson.Contains("\"placeholderResource\": \"false\""), "generated ResourceCatalog should preserve package import metadata for Unity Editor consumers.");

        int secondExit = CharacterPackageCommands.Dispatch(new[]
        {
            "character",
            "import-unity",
            "--package",
            FindSamplePath("character-codex-flow-test"),
            "--project-root",
            root,
            "--build-target",
            "StandaloneWindows64",
            "--check-files",
            "--check-hashes"
        }, MxFramework.Authoring.Cli.Program.CreateJsonOptions());

        CharacterUnityImportReport secondReport = JsonSerializer.Deserialize<CharacterUnityImportReport>(File.ReadAllText(reportPath), JsonOptions);
        Require(secondExit == MxFramework.Authoring.Cli.Program.ExitReady, "second Unity import should be ready.");
        Require(secondReport != null && secondReport.SkippedCount >= firstReport.AddedCount, "repeat import should skip unchanged write plan targets.");
        Require(secondReport.AddedCount == 0 && secondReport.UpdatedCount == 0, "repeat import should not rewrite unchanged targets.");
    }

    private static void UnityImportBridge_ImportBlockedDoesNotWriteProjectTarget()
    {
        string root = Path.Combine(FindRepoRoot(), "Temp", "MxFrameworkAuthoringTests", "issue222-import-blocked");
        ResetDirectory(root);
        string packageCopy = Path.Combine(root, "package-copy");
        CopyDirectory(FindSamplePath("character-codex-flow-test"), packageCopy);
        File.Delete(Path.Combine(packageCopy, "resources", "models", "character_00.fbx"));

        string reportOut = Path.Combine(root, "report-out");
        int exit = CharacterPackageCommands.Dispatch(new[]
        {
            "character",
            "import-unity",
            "--package",
            packageCopy,
            "--project-root",
            root,
            "--check-files",
            "--check-hashes",
            "--report-out",
            reportOut
        }, MxFramework.Authoring.Cli.Program.CreateJsonOptions());

        string targetRoot = Path.Combine(root, "Assets", "MxFrameworkGenerated", "CharacterPackages", "codex_flow_test");
        Require(exit == MxFramework.Authoring.Cli.Program.ExitValidationBlocked, "ImportBlocked package should return validation-blocked exit.");
        Require(!Directory.Exists(targetRoot), "ImportBlocked package must not write Unity project target root.");
        CharacterUnityImportReport report = JsonSerializer.Deserialize<CharacterUnityImportReport>(File.ReadAllText(Path.Combine(reportOut, "import_report.json")), JsonOptions);
        Require(report != null && report.Status == CharacterAuthoringCompilerStatus.ImportBlocked.ToString(), "blocked import report should preserve ImportBlocked status.");
        Require(report.Issues.Exists(issue => issue.Code == CharacterAuthoringValidationCodes.MissingResourceFile), "blocked import report should include missing resource diagnostic.");
    }

    private static void UnityImportBridge_SpawnBlockedWritesButMarksNotSpawnable()
    {
        string root = Path.Combine(FindRepoRoot(), "Temp", "MxFrameworkAuthoringTests", "issue222-import-spawn-blocked");
        ResetDirectory(root);
        SeedGlobalResourceMappingProfile(root);
        string packageCopy = Path.Combine(root, "package-copy");
        CopyDirectory(FindSamplePath("character-codex-flow-test"), packageCopy);
        string weaponSlotsPath = Path.Combine(packageCopy, "geometry", "weapon_slots.json");
        File.WriteAllText(weaponSlotsPath, File.ReadAllText(weaponSlotsPath).Replace("\"attachSocketId\": \"mainHand\"", "\"attachSocketId\": \"missing.socket\""));

        int exit = CharacterPackageCommands.Dispatch(new[]
        {
            "character",
            "import-unity",
            "--package",
            packageCopy,
            "--project-root",
            root,
            "--build-target",
            "StandaloneWindows64",
            "--check-files",
            "--check-hashes"
        }, MxFramework.Authoring.Cli.Program.CreateJsonOptions());

        string targetRoot = Path.Combine(root, "Assets", "MxFrameworkGenerated", "CharacterPackages", "codex_flow_test");
        string reportPath = Path.Combine(targetRoot, "package_cache", "import_report.json");
        Require(exit == MxFramework.Authoring.Cli.Program.ExitReady, "SpawnBlocked import should still complete project writes.");
        Require(File.Exists(Path.Combine(targetRoot, "config", "geometry_binding.json")), "SpawnBlocked import should write geometry binding for inspection.");
        CharacterUnityImportReport report = JsonSerializer.Deserialize<CharacterUnityImportReport>(File.ReadAllText(reportPath), JsonOptions);
        Require(report != null && report.Status == CharacterAuthoringCompilerStatus.SpawnBlocked.ToString(), "SpawnBlocked import report should preserve status.");
        Require(!report.CanSpawnAfterImport, "SpawnBlocked import must mark runtime spawn unavailable.");
        Require(report.Issues.Exists(issue => issue.Code == CharacterAuthoringValidationCodes.CharacterWeaponSlotSocketMissing), "SpawnBlocked report should include character weapon-slot socket diagnostic.");
    }

    private static void SeedGlobalResourceMappingProfile(string projectRoot)
    {
        string profileRelative = Path.Combine(
            "Assets",
            "Config",
            "MxFramework",
            "ResourceProfiles",
            "global_resource_mapping_profile.v2.json");
        string profileTarget = Path.Combine(projectRoot, profileRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(profileTarget)!);
        File.Copy(Path.Combine(FindRepoRoot(), profileRelative), profileTarget, true);
    }
}
