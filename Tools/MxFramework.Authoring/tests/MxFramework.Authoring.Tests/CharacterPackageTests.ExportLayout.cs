using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

/// <summary>
/// 导出布局（第一批 A + 第二批 B）的聚焦回归。
/// 覆盖：新布局首次导入、二次导入幂等、孤立资源回收与已声明资源保护、
/// dry-run 零改动、显式诊断导出与仓库内目标拒绝、摘要确定性与有界性。
/// </summary>
internal static partial class CharacterPackageTests
{
    private const string ExportLayoutPackageSegment = "codex_flow_test";

    internal static void RunCharacterExportLayout()
    {
        ExportLayout_FirstImport_WritesSummaryAndDropsSnapshots();
        ExportLayout_SecondImport_IsIdempotentAndKeepsSummaryBytes();
        ExportLayout_OrphanResourceIsRemovedAndDeclaredResourcesAreKept();
        ExportLayout_DryRun_DoesNotModifyPackage();
        ExportLayout_DiagnosticsOut_WritesSnapshotsAndRejectsRepoRoots();
    }

    private static void ExportLayout_FirstImport_WritesSummaryAndDropsSnapshots()
    {
        string root = PrepareExportLayoutProject("export-layout-first");
        int exit = ImportCodexFlowTest(root);
        Require(exit == MxFramework.Authoring.Cli.Program.ExitReady, "export layout first import should be ready.");
        string targetRoot = ExportLayoutTargetRoot(root);

        Require(!File.Exists(Path.Combine(targetRoot, "package_cache", "compile_result.json")),
            "first import must not write package_cache/compile_result.json.");
        Require(!File.Exists(Path.Combine(targetRoot, "package_cache", "animation_compile_result.json")),
            "first import must not write package_cache/animation_compile_result.json.");
        Require(!File.Exists(Path.Combine(targetRoot, "generated", "character_application_config_patch.json")),
            "first import must not write the generated config patch mirror.");
        Require(!File.Exists(Path.Combine(targetRoot, "generated", "character_geometry_binding.json")),
            "first import must not write the generated geometry binding mirror.");
        Require(!File.Exists(Path.Combine(targetRoot, "generated", "character_resource_mapping.json")),
            "first import must not write the generated resource mapping mirror.");

        string summaryPath = Path.Combine(targetRoot, "package_cache", "compile_summary.json");
        Require(File.Exists(summaryPath), "first import must publish the bounded compile summary.");
        long summaryBytes = new FileInfo(summaryPath).Length;
        Require(summaryBytes <= 8192, "compile summary must stay within the 8 KiB budget; actual=" + summaryBytes);

        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(summaryPath));
        JsonElement rootElement = summary.RootElement;
        Require(string.Equals(rootElement.GetProperty("format").GetString(), "mx.characterPackageCompileSummary.v1", StringComparison.Ordinal),
            "summary must declare its format.");
        Require(rootElement.GetProperty("exportLayoutVersion").GetInt32() >= 2,
            "summary must declare an export layout version at or above the batch B layout.");
        Require(!rootElement.TryGetProperty("generatedAt", out _) && !rootElement.TryGetProperty("timestamp", out _),
            "summary must not carry timestamps.");
        Require(rootElement.GetProperty("artifactRefs").GetArrayLength() > 0,
            "summary must reference the formal import artifacts.");

        // 摘要的 writePlanHash 必须等于对最终写入计划重算的 canonical 哈希。
        string persistedPlanHash = rootElement.GetProperty("compilerHashes").GetProperty("writePlanHash").GetString() ?? string.Empty;
        string recomputed = RecomputeWritePlanHash(Path.Combine(targetRoot, "package_cache", "unity_import_write_plan.json"));
        Require(!string.IsNullOrEmpty(persistedPlanHash) && string.Equals(persistedPlanHash, recomputed, StringComparison.Ordinal),
            "persisted writePlanHash must equal the canonical hash recomputed over the final write plan.");

        AssertNoDuplicateWriteTargets(targetRoot);

        // B1 只改变目标路径，不得改变正式 config 文件的内容语义（含单个结尾换行）。
        foreach (string relative in new[]
                 {
                     "config/character_config_patch.json",
                     "config/geometry_binding.json",
                     "config/resource_catalog_mapping.json"
                 })
        {
            string content = File.ReadAllText(Path.Combine(targetRoot, relative));
            Require(content.EndsWith("\n", StringComparison.Ordinal),
                "formal config artifact must end with a single newline: " + relative);
            Require(!content.EndsWith("\n\n", StringComparison.Ordinal),
                "formal config artifact must not end with a blank line: " + relative);
        }
    }

    /// <summary>写入计划与导入报告都不得出现重复 TargetPath（方案 §7 B1）。</summary>
    private static void AssertNoDuplicateWriteTargets(string targetRoot)
    {
        using (JsonDocument planDocument = JsonDocument.Parse(
                   File.ReadAllText(Path.Combine(targetRoot, "package_cache", "unity_import_write_plan.json"))))
        {
            List<string> planTargets = planDocument.RootElement.GetProperty("writes").EnumerateArray()
                .Select(write => write.GetProperty("targetPath").GetString() ?? string.Empty)
                .ToList();
            Require(planTargets.Count == planTargets.Distinct(StringComparer.Ordinal).Count(),
                "merged write plan must not contain duplicate TargetPath entries.");
        }

        using (JsonDocument reportDocument = JsonDocument.Parse(
                   File.ReadAllText(Path.Combine(targetRoot, "package_cache", "import_report.json"))))
        {
            List<string> writeTargets = reportDocument.RootElement.GetProperty("operations").EnumerateArray()
                .Where(operation => !string.Equals(operation.GetProperty("kind").GetString(), "exportCleanup", StringComparison.Ordinal))
                .Select(operation => operation.GetProperty("targetPath").GetString() ?? string.Empty)
                .ToList();
            Require(writeTargets.Count == writeTargets.Distinct(StringComparer.Ordinal).Count(),
                "import report must not contain duplicate write targets.");
        }
    }

    private static void ExportLayout_SecondImport_IsIdempotentAndKeepsSummaryBytes()
    {
        string root = PrepareExportLayoutProject("export-layout-second");
        Require(ImportCodexFlowTest(root) == MxFramework.Authoring.Cli.Program.ExitReady, "first import should be ready.");
        string targetRoot = ExportLayoutTargetRoot(root);
        string summaryPath = Path.Combine(targetRoot, "package_cache", "compile_summary.json");
        byte[] firstSummary = File.ReadAllBytes(summaryPath);

        Require(ImportCodexFlowTest(root) == MxFramework.Authoring.Cli.Program.ExitReady, "second import should be ready.");

        Require(FileBytesEqual(firstSummary, File.ReadAllBytes(summaryPath)),
            "identical input must not rewrite the compile summary bytes.");
        Require(!File.Exists(Path.Combine(targetRoot, "package_cache", "compile_result.json")),
            "deprecated snapshots must not be revived by a second import.");
        Require(!File.Exists(Path.Combine(targetRoot, "generated", "character_geometry_binding.json")),
            "deprecated mirrors must not be revived by a second import.");
    }

    private static void ExportLayout_OrphanResourceIsRemovedAndDeclaredResourcesAreKept()
    {
        string root = PrepareExportLayoutProject("export-layout-orphan");
        Require(ImportCodexFlowTest(root) == MxFramework.Authoring.Cli.Program.ExitReady, "first import should be ready.");
        string targetRoot = ExportLayoutTargetRoot(root);

        string declaredModel = Path.Combine(targetRoot, "resources", "models", "character_00.fbx");
        string declaredTexture = Path.Combine(targetRoot, "resources", "textures", "body_albedo.png");
        Require(File.Exists(declaredModel) && File.Exists(declaredTexture),
            "declared resources must exist before the orphan scenario.");

        string orphan = Path.Combine(targetRoot, "resources", "models", "orphan_leftover.fbx");
        File.WriteAllText(orphan, "orphan-payload");
        File.WriteAllText(orphan + ".meta", "fileFormatVersion: 2");

        Require(ImportCodexFlowTest(root) == MxFramework.Authoring.Cli.Program.ExitReady, "orphan cleanup import should be ready.");

        Require(!File.Exists(orphan), "an undeclared resources/ file must be reclaimed.");
        Require(!File.Exists(orphan + ".meta"), "the reclaimed file's meta must be reclaimed in the same operation.");
        Require(File.Exists(declaredModel), "a declared model resource must never be reclaimed.");
        Require(File.Exists(declaredTexture), "a declared texture resource must never be reclaimed.");
        Require(File.Exists(Path.Combine(targetRoot, "previews", "thumbnail.png")),
            "a declared preview resource must never be reclaimed.");

        CharacterUnityImportReport report = JsonSerializer.Deserialize<CharacterUnityImportReport>(
            File.ReadAllText(Path.Combine(targetRoot, "package_cache", "import_report.json")), JsonOptions);
        Require(report != null && report.Operations.Exists(operation =>
                operation.Kind == "exportCleanup"
                && operation.SourcePath == "resources/models/orphan_leftover.fbx"
                && operation.Action == "Deleted"),
            "import report must record the reclaimed orphan resource.");
    }

    private static void ExportLayout_DryRun_DoesNotModifyPackage()
    {
        string root = PrepareExportLayoutProject("export-layout-dry-run");
        Require(ImportCodexFlowTest(root) == MxFramework.Authoring.Cli.Program.ExitReady, "seed import should be ready.");
        string targetRoot = ExportLayoutTargetRoot(root);

        // 制造一个孤立文件与一份旧快照，验证 dry-run 不清理、也不发布摘要变更。
        string orphan = Path.Combine(targetRoot, "resources", "models", "dry_run_orphan.fbx");
        File.WriteAllText(orphan, "orphan-payload");
        string legacySnapshot = Path.Combine(targetRoot, "package_cache", "compile_result.json");
        File.WriteAllText(legacySnapshot, "{\"packageId\":\"codex_flow_test\"}");
        string summaryPath = Path.Combine(targetRoot, "package_cache", "compile_summary.json");
        byte[] summaryBefore = File.ReadAllBytes(summaryPath);

        Require(ImportCodexFlowTest(root, "--dry-run") == MxFramework.Authoring.Cli.Program.ExitReady, "dry-run import should be ready.");

        Require(File.Exists(orphan), "dry-run must not reclaim the orphan resource.");
        Require(File.Exists(legacySnapshot), "dry-run must not delete the legacy snapshot.");
        Require(FileBytesEqual(summaryBefore, File.ReadAllBytes(summaryPath)), "dry-run must not rewrite the summary.");
    }

    private static void ExportLayout_DiagnosticsOut_WritesSnapshotsAndRejectsRepoRoots()
    {
        string root = PrepareExportLayoutProject("export-layout-diagnostics");
        string diagnosticsOut = Path.Combine(Path.GetTempPath(), "MxFrameworkExportLayoutDiagnostics");
        if (Directory.Exists(diagnosticsOut))
            Directory.Delete(diagnosticsOut, recursive: true);

        Require(ImportCodexFlowTest(root, "--diagnostics-out", diagnosticsOut) == MxFramework.Authoring.Cli.Program.ExitReady,
            "import with explicit diagnostics output should be ready.");
        Require(File.Exists(Path.Combine(diagnosticsOut, "compile_result.json")),
            "explicit diagnostics must write the full compile snapshot outside the package.");
        Require(File.Exists(Path.Combine(diagnosticsOut, "animation_compile_result.json")),
            "explicit diagnostics must write the full animation snapshot outside the package.");
        Require(File.Exists(Path.Combine(diagnosticsOut, "diagnostics_manifest.json")),
            "explicit diagnostics must write a stage manifest.");
        Require(!File.Exists(Path.Combine(ExportLayoutTargetRoot(root), "package_cache", "compile_result.json")),
            "explicit diagnostics must not put the snapshot back into the package.");

        string forbidden = Path.Combine(root, "Assets", "MxFrameworkGenerated", "forbidden-diagnostics");
        int blocked = ImportCodexFlowTest(root, "--diagnostics-out", forbidden);
        Require(blocked == MxFramework.Authoring.Cli.Program.ExitValidationBlocked,
            "diagnostics output inside Assets/ must be rejected before any write.");
        Require(!Directory.Exists(forbidden), "rejected diagnostics target must not be created.");
    }

    private static string PrepareExportLayoutProject(string name)
    {
        string root = Path.Combine(FindRepoRoot(), "Temp", "MxFrameworkAuthoringTests", name);
        ResetDirectory(root);
        SeedGlobalResourceMappingProfile(root);
        return root;
    }

    private static string ExportLayoutTargetRoot(string projectRoot)
    {
        return Path.Combine(projectRoot, "Assets", "MxFrameworkGenerated", "CharacterPackages", ExportLayoutPackageSegment);
    }

    private static int ImportCodexFlowTest(string projectRoot, params string[] extraArguments)
    {
        var arguments = new List<string>
        {
            "character",
            "import-unity",
            "--package",
            FindSamplePath("character-codex-flow-test"),
            "--project-root",
            projectRoot,
            "--build-target",
            "StandaloneWindows64",
            "--check-files"
        };
        arguments.AddRange(extraArguments);
        return MxFramework.Authoring.Cli.CharacterPackageCommands.Dispatch(arguments.ToArray(), MxFramework.Authoring.Cli.Program.CreateJsonOptions());
    }

    /// <summary>按 CharacterAuthoringCompiler.BuildWritePlanCanonicalText 的口径重算写入计划哈希。</summary>
    private static string RecomputeWritePlanHash(string writePlanPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(writePlanPath));
        JsonElement plan = document.RootElement;
        var builder = new StringBuilder();
        builder.Append(plan.GetProperty("targetRootPath").GetString())
            .Append('|')
            .Append(plan.GetProperty("canWriteToUnityProject").GetBoolean() ? "True" : "False")
            .Append('|')
            .Append(plan.GetProperty("canSpawnAfterImport").GetBoolean() ? "True" : "False")
            .Append('\n');

        foreach (JsonElement write in plan.GetProperty("writes").EnumerateArray())
        {
            builder.Append(write.GetProperty("kind").GetString()).Append('|')
                .Append(write.GetProperty("sourcePath").GetString()).Append('|')
                .Append(write.GetProperty("targetPath").GetString()).Append('|')
                .Append(write.TryGetProperty("contentHash", out JsonElement hash) ? hash.GetString() ?? string.Empty : string.Empty)
                .Append('\n');
        }

        return CharacterPackageHashUtility.ComputeTextSha256(builder.ToString());
    }

    private static bool FileBytesEqual(byte[] left, byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length)
            return false;
        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
                return false;
        }
        return true;
    }
}
