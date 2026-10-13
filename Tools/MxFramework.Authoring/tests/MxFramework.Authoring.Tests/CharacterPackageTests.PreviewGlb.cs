using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

/// <summary>
/// 专用预览 GLB 仅保留在制作侧的聚焦回归。
/// 覆盖：用途分类、全部运行出口的排除、制作侧保留、延期保护。
/// </summary>
internal static partial class CharacterPackageTests
{
    private const string PreviewGlbResourceKey = "char.codex_flow_test.preview.body_mesh";
    private const string PreviewGlbRelativePath = "previews/character_00.preview.glb";

    private static readonly string[] RuntimeExportExitRelativePaths =
    {
        "config/resource_catalog_mapping.json",
        "config/unity_resource_catalog.json",
        "config/runtime_resource_catalog.json",
        "config/character_resource_plan.json",
        "config/character_config_patch.json",
        "config/resolver_verification_plan.json"
    };

    internal static void RunCharacterPreviewGlb()
    {
        PreviewGlb_ClassifiesOnlyPreviewMeshGlb();
        PreviewGlb_IsExcludedFromEveryRuntimeExportExit();
        PreviewGlb_IsProtectedFromOrphanCleanup();
        PreviewGlb_SamePathClaimedTwiceIsProtectedAndReported();
        PreviewGlb_DependencyInspectorRejectsUnsafeUris();
    }

    private static void PreviewGlb_DependencyInspectorRejectsUnsafeUris()
    {
        string root = Path.Combine(Path.GetTempPath(), "mx-preview-glb-deps-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            // 合法的相对依赖：存在即通过，并被登记为外部依赖。
            Directory.CreateDirectory(Path.Combine(root, "textures"));
            File.WriteAllText(Path.Combine(root, "textures", "body.png"), "png");

            string embedded = WriteGlb(root, "embedded.glb", "{\"buffers\":[{}],\"images\":[{}]}");
            CharacterGlbDependencyReport embeddedReport = CharacterGlbDependencyInspector.Inspect(embedded, root);
            Require(embeddedReport.Succeeded && embeddedReport.ExternalDependencies.Count == 0,
                "an embedded GLB without uris must pass the dependency check.");

            string dataUri = WriteGlb(root, "data-uri.glb",
                "{\"buffers\":[{\"uri\":\"data:application/octet-stream;base64,AAAA\"}]}");
            Require(CharacterGlbDependencyInspector.Inspect(dataUri, root).Succeeded,
                "an inline data uri must be accepted.");

            string relative = WriteGlb(root, "relative.glb", "{\"images\":[{\"uri\":\"textures/body.png\"}]}");
            CharacterGlbDependencyReport relativeReport = CharacterGlbDependencyInspector.Inspect(relative, root);
            Require(relativeReport.Succeeded && relativeReport.ExternalDependencies.Contains("textures/body.png"),
                "a relative dependency that exists must be accepted and recorded.");

            string missing = WriteGlb(root, "missing.glb", "{\"images\":[{\"uri\":\"textures/absent.png\"}]}");
            Require(!CharacterGlbDependencyInspector.Inspect(missing, root).Succeeded,
                "a missing external dependency must be reported.");

            foreach (string unsafeUri in new[]
                     {
                         "C:/textures/body.png",
                         "C:\\\\textures\\\\body.png",
                         "/absolute/body.png",
                         "\\\\\\\\server\\\\share\\\\body.png",
                         "../outside/body.png",
                         "https://example.com/body.png",
                         "file:///tmp/body.png"
                     })
            {
                string escaped = unsafeUri.Replace("\\", "\\\\");
                string unsafeGlb = WriteGlb(root, "unsafe.glb", "{\"images\":[{\"uri\":\"" + escaped + "\"}]}");
                CharacterGlbDependencyReport unsafeReport = CharacterGlbDependencyInspector.Inspect(unsafeGlb, root);
                Require(!unsafeReport.Succeeded && unsafeReport.Violations.Count > 0,
                    "an unsafe dependency uri must be rejected: " + unsafeUri);
            }

            // 非 GLB 容器必须报告为未通过预检，而不是静默通过。
            string notGlb = Path.Combine(root, "not-glb.glb");
            File.WriteAllText(notGlb, "plain text");
            CharacterGlbDependencyReport notGlbReport = CharacterGlbDependencyInspector.Inspect(notGlb, root);
            Require(!notGlbReport.Inspected && !string.IsNullOrEmpty(notGlbReport.FailureReason),
                "a non-GLB container must fail the inspection instead of passing silently.");
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>写出一个最小合法 GLB 容器，仅含 JSON chunk。</summary>
    private static string WriteGlb(string directory, string fileName, string json)
    {
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
        int padded = (jsonBytes.Length + 3) / 4 * 4;
        int total = 12 + 8 + padded;
        var bytes = new byte[total];

        WriteUInt32(bytes, 0, 0x46546C67);
        WriteUInt32(bytes, 4, 2);
        WriteUInt32(bytes, 8, (uint)total);
        WriteUInt32(bytes, 12, (uint)padded);
        WriteUInt32(bytes, 16, 0x4E4F534A);
        Array.Copy(jsonBytes, 0, bytes, 20, jsonBytes.Length);
        for (int i = 20 + jsonBytes.Length; i < total; i++)
            bytes[i] = (byte)' ';

        string path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value & 0xFF);
        bytes[offset + 1] = (byte)((value >> 8) & 0xFF);
        bytes[offset + 2] = (byte)((value >> 16) & 0xFF);
        bytes[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void PreviewGlb_SamePathClaimedTwiceIsProtectedAndReported()
    {
        var package = new CharacterResourcePackage
        {
            Manifest = new CharacterPackageManifest { PackageId = "preview_glb_conflict" },
            ResourceCatalog = new CharacterPackageResourceCatalog()
        };

        // 同一物理文件同时被"专用预览"和"正式模型"声明。
        package.ResourceCatalog.Entries.Add(new CharacterPackageResourceEntry
        {
            ResourceKey = "char.preview_glb_conflict.preview.body_mesh",
            TypeId = "model",
            Usage = "previewMesh",
            RelativePath = "previews/shared.glb",
            SourceFormat = "glb"
        });
        package.ResourceCatalog.Entries.Add(new CharacterPackageResourceEntry
        {
            ResourceKey = "char.preview_glb_conflict.model.body",
            TypeId = "model",
            Usage = "characterModel",
            RelativePath = "previews/shared.glb",
            SourceFormat = "glb"
        });

        CharacterAuthoringCompileResult result = CharacterAuthoringCompiler.Compile(
            new CharacterAuthoringCompileRequest
            {
                Package = package,
                Options = new CharacterAuthoringCompileOptions()
            });

        Require(result != null && result.GateReport != null && result.GateReport.Issues != null
                && result.GateReport.Issues.Exists(issue =>
                    string.Equals(issue.Code, CharacterPackageResourceExportPolicy.RuntimeReferenceDiagnosticCode, StringComparison.Ordinal)),
            "sharing one physical file between an authoring-only preview and a formal use must report a runtime reference conflict.");

        // 冲突时文件必须继续导出，否则通用孤立扫描会把它当成孤立资源删除。
        Require(result.ResourceMapping != null && result.ResourceMapping.Entries != null
                && result.ResourceMapping.Entries.Exists(entry =>
                    string.Equals(entry.SourceRelativePath, "previews/shared.glb", StringComparison.Ordinal)),
            "the conflicting physical file must stay exported so the orphan scan cannot reclaim it.");
    }

    private static void PreviewGlb_ClassifiesOnlyPreviewMeshGlb()
    {
        Require(CharacterPackageResourceExportPolicy.IsAuthoringPreviewGlb("previewMesh", "glb"),
            "previewMesh + glb must classify as authoring-only preview.");
        Require(CharacterPackageResourceExportPolicy.IsAuthoringPreviewGlb("previewMesh", ".GLB"),
            "classification must be case-insensitive and accept a leading dot.");
        Require(!CharacterPackageResourceExportPolicy.IsAuthoringPreviewGlb("previewMesh", "fbx"),
            "previewMesh FBX must keep its previous export behaviour.");
        Require(!CharacterPackageResourceExportPolicy.IsAuthoringPreviewGlb("previewThumbnail", "glb"),
            "previewThumbnail must not be classified as the authoring-only preview.");
        Require(!CharacterPackageResourceExportPolicy.IsAuthoringPreviewGlb("characterModel", "glb"),
            "a formal character model GLB must not be classified as the authoring-only preview.");
        Require(!CharacterPackageResourceExportPolicy.IsAuthoringPreviewGlb("previewMesh", string.Empty),
            "an empty source format must not classify.");
        Require(!CharacterPackageResourceExportPolicy.IsAuthoringPreviewGlb(string.Empty, "glb"),
            "an empty usage must not classify.");

        // typeId=model 是合法输入：分类只看 usage + 有效源格式。
        var entry = new CharacterPackageResourceEntry
        {
            ResourceKey = PreviewGlbResourceKey,
            TypeId = "model",
            Usage = "previewMesh",
            RelativePath = PreviewGlbRelativePath
        };
        Require(CharacterPackageResourceExportPolicy.IsAuthoringPreviewGlb(entry),
            "typeId=model with usage=previewMesh and a .glb path must classify.");

        // 未显式声明 sourceFormat 时按扩展名取有效格式。
        var extensionOnly = new CharacterPackageResourceEntry
        {
            Usage = "previewMesh",
            RelativePath = "previews/character_00.PREVIEW.GLB",
            SourceFormat = string.Empty
        };
        Require(CharacterPackageResourceExportPolicy.IsAuthoringPreviewGlb(extensionOnly),
            "classification must fall back to the file extension when sourceFormat is absent.");

        Require(!CharacterPackageResourceExportPolicy.IsAuthoringPreviewGlb((CharacterPackageResourceEntry)null),
            "a null entry must not classify.");
    }

    private static void PreviewGlb_IsExcludedFromEveryRuntimeExportExit()
    {
        string root = PrepareExportLayoutProject("preview-glb-exits");
        Require(ImportCodexFlowTest(root) == MxFramework.Authoring.Cli.Program.ExitReady,
            "import with the authoring-only preview policy should be ready.");

        string targetRoot = ExportLayoutTargetRoot(root);

        // 1) 源 catalog 必须保留完整条目（分类不修改源对象）。
        string sourceCatalogPath = Path.Combine(
            FindSamplePath("character-codex-flow-test"), "resource_catalog.json");
        Require(File.ReadAllText(sourceCatalogPath).Contains(PreviewGlbResourceKey, StringComparison.Ordinal),
            "the source resource catalog must keep the preview entry.");

        // 2) 每一个运行出口都不得再出现该运行资源引用。
        foreach (string relative in RuntimeExportExitRelativePaths)
        {
            string path = Path.Combine(targetRoot, relative);
            Require(File.Exists(path), "expected export artifact is missing: " + relative);
            string content = File.ReadAllText(path);
            Require(!content.Contains(PreviewGlbResourceKey, StringComparison.Ordinal),
                "runtime export exit must not reference the authoring-only preview resource key: " + relative);
            Require(!content.Contains(PreviewGlbRelativePath, StringComparison.Ordinal),
                "runtime export exit must not reference the authoring-only preview path: " + relative);
        }

        // 3) 生成副本不再产生；制作侧其它资源继续正常导出。
        Require(!File.Exists(Path.Combine(targetRoot, "previews", "character_00.preview.glb")),
            "the authoring-only preview GLB must not be copied into the generated package.");
        Require(File.Exists(Path.Combine(targetRoot, "previews", "thumbnail.png")),
            "previewThumbnail must keep being exported.");
        Require(File.Exists(Path.Combine(targetRoot, "resources", "models", "character_00.fbx")),
            "the formal character model must keep being exported.");
        Require(File.Exists(Path.Combine(targetRoot, "resources", "textures", "body_albedo.png")),
            "the body texture must keep being exported.");

        // 4) 其余三条 catalog 条目仍应导出（4 项 -> 3 项）。
        using JsonDocument unityCatalog = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(targetRoot, "config", "unity_resource_catalog.json")));
        Require(unityCatalog.RootElement.GetProperty("entries").GetArrayLength() == 3,
            "the Unity resource catalog must drop exactly the authoring-only preview entry.");

        // 5) 导出布局登记为 4（承接动画计划精简的布局 3）。
        using JsonDocument summary = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(targetRoot, "package_cache", "compile_summary.json")));
        Require(summary.RootElement.GetProperty("exportLayoutVersion").GetInt32() == 4,
            "the compile summary must declare export layout version 4 for the authoring-only preview policy.");
    }

    private static void PreviewGlb_IsProtectedFromOrphanCleanup()
    {
        string root = PrepareExportLayoutProject("preview-glb-deferred");
        Require(ImportCodexFlowTest(root) == MxFramework.Authoring.Cli.Program.ExitReady,
            "first import should be ready.");

        string targetRoot = ExportLayoutTargetRoot(root);
        string legacyGlb = Path.Combine(targetRoot, "previews", "character_00.preview.glb");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyGlb) ?? targetRoot);
        File.WriteAllText(legacyGlb, "legacy-generated-preview");
        File.WriteAllText(legacyGlb + ".meta", "fileFormatVersion: 2");

        Require(ImportCodexFlowTest(root) == MxFramework.Authoring.Cli.Program.ExitReady,
            "second import with a legacy generated preview should be ready.");

        Require(File.Exists(legacyGlb),
            "the legacy generated preview GLB must be deferred, not reclaimed by the generic orphan scan.");
        Require(File.Exists(legacyGlb + ".meta"),
            "the deferred preview GLB must keep its meta until Unity performs the targeted deletion.");

        CharacterUnityImportReport report = JsonSerializer.Deserialize<CharacterUnityImportReport>(
            File.ReadAllText(Path.Combine(targetRoot, "package_cache", "import_report.json")), JsonOptions);
        Require(report != null && report.Operations.Exists(operation =>
                operation.Kind == "exportCleanup"
                && operation.Action == "Deferred"
                && operation.SourcePath == PreviewGlbRelativePath),
            "the import report must record the deferred preview GLB.");
    }
}
