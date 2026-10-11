using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

/// <summary>
/// 动画资源计划运行投影（`mx.animationResourcePlan.v2`）的聚焦回归。
/// 覆盖：五字段投影、完整对象未被污染、确定性、负样本拒绝、摘要布局 3、导入路径唯一 writer。
/// </summary>
internal static partial class CharacterPackageTests
{
    private const string RuntimePlanRelativePath = "config/animation_resource_plan.json";

    internal static void RunCharacterAnimationRuntimePlan()
    {
        AnimationRuntimePlan_ProjectsExactlyFiveFields();
        AnimationRuntimePlan_KeepsFullObjectInDiagnosticsAndIsDeterministic();
        AnimationRuntimePlan_ProjectionRejectsInvalidSource();
        AnimationRuntimePlan_SummaryDeclaresLayoutThree();
    }

    private static void AnimationRuntimePlan_ProjectsExactlyFiveFields()
    {
        string root = PrepareExportLayoutProject("animation-runtime-plan-projection");
        Require(ImportCodexFlowTest(root) == MxFramework.Authoring.Cli.Program.ExitReady,
            "import with the runtime projection should be ready.");

        string planPath = Path.Combine(ExportLayoutTargetRoot(root), RuntimePlanRelativePath);
        Require(File.Exists(planPath), "the runtime projection must be written to config/animation_resource_plan.json.");

        byte[] raw = File.ReadAllBytes(planPath);
        Require(raw.Length <= 512, "runtime projection body must stay within 512 bytes; actual=" + raw.Length);
        Require(!(raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF), "runtime projection must not carry a BOM.");
        Require(raw[raw.Length - 1] == (byte)'\n' && raw[raw.Length - 2] != (byte)'\n',
            "runtime projection must end with exactly one newline.");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(planPath));
        List<string> keys = document.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Require(keys.Count == 5, "runtime projection must contain exactly five top-level keys; actual=" + keys.Count);
        Require(keys.SequenceEqual(new[] { "format", "schemaVersion", "packageId", "stableId", "planHash" }),
            "runtime projection must keep the fixed key order; actual=" + string.Join(",", keys));

        Require(document.RootElement.GetProperty("format").GetString() == "mx.animationResourcePlan.v2",
            "runtime projection must declare mx.animationResourcePlan.v2.");
        Require(document.RootElement.GetProperty("schemaVersion").GetString() == "2.0",
            "runtime projection must declare schemaVersion 2.0.");
        Require(document.RootElement.GetProperty("packageId").GetString() == "animation.codex_flow_test",
            "runtime projection must copy the animation package identity, not the character package id.");
        Require(document.RootElement.GetProperty("stableId").GetString() == "charpkg.codex_flow_test.animation",
            "runtime projection must copy the animation stable id verbatim.");

        string planHash = document.RootElement.GetProperty("planHash").GetString() ?? string.Empty;
        Require(CharacterAnimationRuntimeResourcePlanProjection.IsLowercaseSha256Hash(planHash),
            "runtime projection must carry a lowercase sha256 plan hash; actual=" + planHash);

        // 四个完整对象不得出现在投影里，也不得以 null / 空对象占位。
        foreach (string removed in new[] { "runtimeResourceCatalog", "characterResourcePlan", "audioCueManifest", "diagnostics" })
        {
            Require(!document.RootElement.TryGetProperty(removed, out _),
                "runtime projection must not contain the removed field: " + removed);
        }
    }

    private static void AnimationRuntimePlan_KeepsFullObjectInDiagnosticsAndIsDeterministic()
    {
        string root = PrepareExportLayoutProject("animation-runtime-plan-diagnostics");
        string diagnosticsOut = Path.Combine(Path.GetTempPath(), "MxFrameworkAnimationRuntimePlanDiagnostics");
        if (Directory.Exists(diagnosticsOut))
            Directory.Delete(diagnosticsOut, recursive: true);

        Require(ImportCodexFlowTest(root, "--diagnostics-out", diagnosticsOut) == MxFramework.Authoring.Cli.Program.ExitReady,
            "import with diagnostics output should be ready.");

        byte[] firstProjection = File.ReadAllBytes(Path.Combine(ExportLayoutTargetRoot(root), RuntimePlanRelativePath));

        // 同一次导入的完整动画快照必须仍是完整 v1（含嵌套 catalog / resource plan / audio manifest / diagnostics）。
        string snapshotPath = Path.Combine(diagnosticsOut, "animation_compile_result.json");
        Require(File.Exists(snapshotPath), "explicit diagnostics must write the full animation compile snapshot.");
        using JsonDocument snapshot = JsonDocument.Parse(File.ReadAllText(snapshotPath));
        JsonElement fullPlan = snapshot.RootElement.GetProperty("animationResourcePlan");
        Require(fullPlan.EnumerateObject().Count() == 9,
            "the diagnostics snapshot must keep the full v1 animation resource plan with nine top-level keys.");
        Require(fullPlan.GetProperty("format").GetString() == "mx.animationResourcePlan.v1",
            "the diagnostics snapshot must keep the full v1 format.");
        Require(fullPlan.GetProperty("runtimeResourceCatalog").GetProperty("entries").GetArrayLength() > 0,
            "the full v1 plan in diagnostics must keep its runtime resource catalog entries.");
        Require(fullPlan.GetProperty("characterResourcePlan").GetProperty("animationWarmup").GetProperty("resources").GetArrayLength() > 0,
            "the full v1 plan in diagnostics must keep its animation warmup group.");

        // 第二次相同导入：投影字节不变，且不复活完整正文。
        Require(ImportCodexFlowTest(root) == MxFramework.Authoring.Cli.Program.ExitReady,
            "second import should be ready.");
        byte[] secondProjection = File.ReadAllBytes(Path.Combine(ExportLayoutTargetRoot(root), RuntimePlanRelativePath));
        Require(firstProjection.SequenceEqual(secondProjection),
            "identical input must produce byte-identical runtime projections.");
    }

    private static void AnimationRuntimePlan_ProjectionRejectsInvalidSource()
    {
        JsonSerializerOptions options = MxFramework.Authoring.Cli.Program.CreateJsonOptions();

        Require(!CharacterAnimationRuntimeResourcePlanProjection.TryProject(null, options, out _, out string nullDiagnostic)
                && !string.IsNullOrEmpty(nullDiagnostic),
            "a missing source object must be rejected with a diagnostic.");

        var valid = new AnimationResourcePlanDocument
        {
            PackageId = "animation.pkg",
            StableId = "charpkg.pkg.animation",
            PlanHash = "sha256:" + new string('a', 64)
        };
        Require(CharacterAnimationRuntimeResourcePlanProjection.TryProject(valid, options, out string json, out _),
            "a well-formed source must project successfully.");
        Require(json.EndsWith("\n", StringComparison.Ordinal)
                && json.Length - json.TrimEnd('\n').Length == 1,
            "projection must end with exactly one newline.");

        var emptyPackage = new AnimationResourcePlanDocument { PackageId = "", StableId = valid.StableId, PlanHash = valid.PlanHash };
        Require(!CharacterAnimationRuntimeResourcePlanProjection.TryProject(emptyPackage, options, out _, out _),
            "an empty packageId must be rejected.");

        var emptyStable = new AnimationResourcePlanDocument { PackageId = valid.PackageId, StableId = " ", PlanHash = valid.PlanHash };
        Require(!CharacterAnimationRuntimeResourcePlanProjection.TryProject(emptyStable, options, out _, out _),
            "an empty stableId must be rejected.");

        foreach (string badHash in new[]
                 {
                     "sha256:" + new string('A', 64),
                     "sha256:" + new string('a', 63),
                     new string('a', 64),
                     "sha1:" + new string('a', 40)
                 })
        {
            var badPlan = new AnimationResourcePlanDocument { PackageId = valid.PackageId, StableId = valid.StableId, PlanHash = badHash };
            Require(!CharacterAnimationRuntimeResourcePlanProjection.TryProject(badPlan, options, out _, out string badDiagnostic)
                    && !string.IsNullOrEmpty(badDiagnostic),
                "an invalid planHash must be rejected: " + badHash);
        }
    }

    private static void AnimationRuntimePlan_SummaryDeclaresLayoutThree()
    {
        string root = PrepareExportLayoutProject("animation-runtime-plan-layout");
        Require(ImportCodexFlowTest(root) == MxFramework.Authoring.Cli.Program.ExitReady, "import should be ready.");

        string summaryPath = Path.Combine(ExportLayoutTargetRoot(root), "package_cache", "compile_summary.json");
        using JsonDocument summary = JsonDocument.Parse(File.ReadAllText(summaryPath));
        Require(summary.RootElement.GetProperty("exportLayoutVersion").GetInt32() >= 3,
            "the compile summary must declare an export layout version at or above the animation runtime plan layout.");
        Require(summary.RootElement.GetProperty("artifactRefs").EnumerateArray()
                .Any(reference => reference.GetProperty("path").GetString() == RuntimePlanRelativePath),
            "the summary must keep referencing the runtime plan path instead of copying its body.");
    }
}
