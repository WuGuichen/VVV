using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests
{
    /// <summary>
    /// Surface Palette usage 的模型版本一致性（不变量 I-1 / I-3）回归测试。
    ///
    /// 背景：character_palette_usage.json 同时承载两件互相绑定的信息
    ///   - modelHash：模型版本标签，Unity 导入时用导入后 Prefab 的 mesh 文件哈希比对；
    ///   - tokenUsages：从该模型网格统计出的面数分布。
    /// 服务端不具备重算面数的能力（统计只在浏览器端 three.js 中实现），所以一旦两者指向不同
    /// 模型版本，只能拒绝写入，绝不能把正确哈希贴到错误的面数分布上。
    ///
    /// 历史缺陷：全局单槽 state.surfacePalettePreviewUsage 曾被武器预览覆盖，把武器模型哈希
    /// 写进了角色 usage（character-000009 实测 sha256:5e6a0068… 对应武器 000001.fbx），
    /// 导致 Unity 导入报 "Palette usage modelHash does not match the imported model."。
    /// </summary>
    public static class CharacterPaletteUsageAuthorityTests
    {
        private const string BodyModelRelativePath = "resources/models/body_lod0.fbx";

        public static void RunAll()
        {
            RunResolveBodyModel();
            RunValidateUsageModelHash();
            RunUsageDocumentParsing();
            RunBindingEnabledParsing();
            RunEvaluatePaletteUsage();
            RunCompilerGateSurfacesWarning();
            RunCompilerGateRespectsStrictMode();
        }

        // ------------------------------------------------------------------
        // 本体模型解析：唯一入口，多候选必须报错而不是任选
        // ------------------------------------------------------------------

        private static void RunResolveBodyModel()
        {
            string root = CreateTempPackage();
            try
            {
                CharacterResourcePackage package = BuildPackage(
                    BodyEntry("char.test.model.body", BodyModelRelativePath, CharacterPackageResourceUsageIds.CharacterModel),
                    BodyEntry("char.test.model.preview", "previews/body.preview.glb", CharacterPackageResourceUsageIds.PreviewMesh));
                string bodyHash = WriteBodyModel(root, BodyModelRelativePath, "fake-body-model");

                CharacterBodyModelResolution resolved = CharacterPaletteUsageAuthority.ResolveBodyModel(package, root, true);
                Require(resolved.IsResolved, "A single typeId=model + usage=characterModel entry must resolve.");
                Require(resolved.Entry.ResourceKey == "char.test.model.body", "Resolved body model must be the characterModel entry.");
                Require(resolved.RelativePath == BodyModelRelativePath, "Resolved relativePath mismatch.");
                Require(resolved.ModelHash == bodyHash, "Resolved modelHash must equal the source file sha256.");
                Require(resolved.ModelHash.StartsWith("sha256:", StringComparison.Ordinal),
                    "modelHash must carry the sha256: prefix.");
                Require(resolved.CandidateResourceKeys.Count == 1,
                    "previewMesh entries must not be counted as body model candidates.");

                // 未声明本体模型
                CharacterBodyModelResolution undeclared = CharacterPaletteUsageAuthority.ResolveBodyModel(
                    BuildPackage(BodyEntry("char.test.model.preview", "previews/body.preview.glb", CharacterPackageResourceUsageIds.PreviewMesh)),
                    root,
                    true);
                Require(!undeclared.IsResolved && undeclared.Status == CharacterBodyModelResolutionStatus.Undeclared,
                    "A package without a characterModel entry must be reported as Undeclared.");
                Require(undeclared.Code == CharacterPaletteUsageCodes.BodyModelUndeclared, "Undeclared code mismatch.");

                // 多候选：禁止任选其一
                CharacterBodyModelResolution ambiguous = CharacterPaletteUsageAuthority.ResolveBodyModel(
                    BuildPackage(
                        BodyEntry("char.test.model.body", BodyModelRelativePath, CharacterPackageResourceUsageIds.CharacterModel),
                        BodyEntry("char.test.model.body.alt", "resources/models/body_alt.fbx", CharacterPackageResourceUsageIds.CharacterModel)),
                    root,
                    true);
                Require(!ambiguous.IsResolved && ambiguous.Status == CharacterBodyModelResolutionStatus.Ambiguous,
                    "Two characterModel entries must be reported as Ambiguous, never silently picked.");
                Require(ambiguous.Code == CharacterPaletteUsageCodes.BodyModelAmbiguous, "Ambiguous code mismatch.");
                Require(ambiguous.CandidateResourceKeys.Count == 2, "Ambiguous must list every candidate.");
                Require(ambiguous.Entry == null, "Ambiguous must not expose a chosen entry.");

                // 不安全的相对路径
                CharacterBodyModelResolution unsafePath = CharacterPaletteUsageAuthority.ResolveBodyModel(
                    BuildPackage(BodyEntry("char.test.model.body", "../outside/body.fbx", CharacterPackageResourceUsageIds.CharacterModel)),
                    root,
                    true);
                Require(!unsafePath.IsResolved && unsafePath.Status == CharacterBodyModelResolutionStatus.UnsafePath,
                    "A path escaping the package must be reported as UnsafePath.");
                Require(unsafePath.Code == CharacterPaletteUsageCodes.BodyModelPathUnsupported, "UnsafePath code mismatch.");

                CharacterBodyModelResolution rooted = CharacterPaletteUsageAuthority.ResolveBodyModel(
                    BuildPackage(BodyEntry("char.test.model.body", "C:/outside/body.fbx", CharacterPackageResourceUsageIds.CharacterModel)),
                    root,
                    true);
                Require(!rooted.IsResolved && rooted.Status == CharacterBodyModelResolutionStatus.UnsafePath,
                    "A rooted path must be reported as UnsafePath.");

                // 文件缺失
                CharacterBodyModelResolution missing = CharacterPaletteUsageAuthority.ResolveBodyModel(
                    BuildPackage(BodyEntry("char.test.model.body", "resources/models/absent.fbx", CharacterPackageResourceUsageIds.CharacterModel)),
                    root,
                    true);
                Require(!missing.IsResolved && missing.Status == CharacterBodyModelResolutionStatus.FileMissing,
                    "A missing body model file must be reported as FileMissing.");
                Require(missing.Code == CharacterPaletteUsageCodes.BodyModelFileMissing, "FileMissing code mismatch.");

                // 无包
                CharacterBodyModelResolution noPackage = CharacterPaletteUsageAuthority.ResolveBodyModel(null, root, true);
                Require(!noPackage.IsResolved && noPackage.Status == CharacterBodyModelResolutionStatus.PackageMissing,
                    "A missing package must be reported as PackageMissing.");
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        // ------------------------------------------------------------------
        // modelHash 判定：拒绝而不是改写
        // ------------------------------------------------------------------

        private static void RunValidateUsageModelHash()
        {
            string root = CreateTempPackage();
            try
            {
                string bodyHash = WriteBodyModel(root, BodyModelRelativePath, "fake-body-model");

                CharacterBodyModelResolution body = CharacterPaletteUsageAuthority.ResolveBodyModel(
                    BuildPackage(BodyEntry("char.test.model.body", BodyModelRelativePath, CharacterPackageResourceUsageIds.CharacterModel)),
                    root,
                    true);
                Require(body.IsResolved, "Body model fixture must resolve.");

                CharacterPaletteUsageCheckResult ok = CharacterPaletteUsageAuthority.ValidateUsageModelHash(body, bodyHash);
                Require(ok.IsValid, "A matching modelHash must pass.");
                Require(ok.ExpectedModelHash == bodyHash && ok.ActualModelHash == bodyHash, "Matching hashes must be echoed back.");

                CharacterPaletteUsageCheckResult prefixTolerant = CharacterPaletteUsageAuthority.ValidateUsageModelHash(
                    body,
                    bodyHash.Substring("sha256:".Length));
                Require(prefixTolerant.IsValid, "modelHash comparison must tolerate a missing sha256: prefix.");

                CharacterPaletteUsageCheckResult upperCase = CharacterPaletteUsageAuthority.ValidateUsageModelHash(
                    body,
                    "SHA256:" + bodyHash.Substring("sha256:".Length).ToUpperInvariant());
                Require(upperCase.IsValid, "modelHash comparison must be case-insensitive.");

                // 000009 回归：武器模型哈希被写进角色 usage
                CharacterPaletteUsageCheckResult weaponHash = CharacterPaletteUsageAuthority.ValidateUsageModelHash(
                    body,
                    "sha256:5e6a0068b0a1b0e7b1b0e4b0a1b0e7b1b0e4b0a1b0e7b1b0e4b0a1b0e7b1b0e4");
                Require(!weaponHash.IsValid, "A foreign model hash must be rejected.");
                Require(weaponHash.Code == CharacterPaletteUsageCodes.ModelHashMismatch, "Mismatch code mismatch.");
                Require(weaponHash.ExpectedModelHash == bodyHash, "Mismatch must report the expected body hash.");
                Require(!string.IsNullOrWhiteSpace(weaponHash.Message), "Mismatch must carry a diagnostic message.");

                CharacterPaletteUsageCheckResult missing = CharacterPaletteUsageAuthority.ValidateUsageModelHash(body, "");
                Require(!missing.IsValid && missing.Code == CharacterPaletteUsageCodes.ModelHashMissing, "Missing modelHash code mismatch.");

                CharacterPaletteUsageCheckResult whitespace = CharacterPaletteUsageAuthority.ValidateUsageModelHash(body, "   ");
                Require(!whitespace.IsValid && whitespace.Code == CharacterPaletteUsageCodes.ModelHashMissing,
                    "Whitespace-only modelHash must be treated as missing.");

                // 本体模型未解析时，必须把本体侧的失败码原样透出，而不是伪装成 modelHash 不一致
                CharacterBodyModelResolution unresolved = CharacterPaletteUsageAuthority.ResolveBodyModel(null, root, true);
                CharacterPaletteUsageCheckResult propagated = CharacterPaletteUsageAuthority.ValidateUsageModelHash(unresolved, bodyHash);
                Require(!propagated.IsValid && propagated.Code == CharacterPaletteUsageCodes.BodyModelUndeclared,
                    "An unresolved body model must propagate its own code.");
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        // ------------------------------------------------------------------
        // usage 文档解析
        // ------------------------------------------------------------------

        private static void RunUsageDocumentParsing()
        {
            const string valid = "{\"format\":\"mx.characterPaletteUsage.v2\",\"modelHash\":\"sha256:abc\"}";
            Require(CharacterPaletteUsageAuthority.ReadUsageModelHash(valid, out string noError) == "sha256:abc",
                "modelHash must be read from the usage document.");
            Require(string.IsNullOrEmpty(noError), "A valid usage document must not report an error.");

            Require(CharacterPaletteUsageAuthority.ReadUsageModelHash("{\"format\":\"x\"}", out _) == string.Empty,
                "A usage document without modelHash must yield an empty string.");

            Require(CharacterPaletteUsageAuthority.ReadUsageModelHash("{ not json", out string malformedError) == null,
                "A malformed usage document must yield null.");
            Require(!string.IsNullOrEmpty(malformedError), "A malformed usage document must report an error.");

            Require(CharacterPaletteUsageAuthority.ReadUsageModelHash("[1,2]", out string rootError) == null,
                "A non-object usage document must yield null.");
            Require(!string.IsNullOrEmpty(rootError), "A non-object usage document must report an error.");

            Require(CharacterPaletteUsageAuthority.ReadUsageModelHash("", out string emptyError) == null,
                "An empty usage document must yield null.");
            Require(!string.IsNullOrEmpty(emptyError), "An empty usage document must report an error.");
        }

        // ------------------------------------------------------------------
        // 绑定文件解析
        // ------------------------------------------------------------------

        private static void RunBindingEnabledParsing()
        {
            string root = CreateTempPackage();
            try
            {
                Require(CharacterPaletteUsageAuthority.ReadBindingEnabled(root, out _) == null,
                    "A missing binding file must yield null (palette not configured).");

                string bindingPath = Path.Combine(root, CharacterPaletteUsageAuthority.BindingRelativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(bindingPath));
                File.WriteAllText(bindingPath, "{\"enabled\":true}", Encoding.UTF8);
                Require(CharacterPaletteUsageAuthority.ReadBindingEnabled(root, out _) == true, "enabled=true must be read.");

                File.WriteAllText(bindingPath, "{\"enabled\":false}", Encoding.UTF8);
                Require(CharacterPaletteUsageAuthority.ReadBindingEnabled(root, out _) == false, "enabled=false must be read.");

                File.WriteAllText(bindingPath, "{\"format\":\"mx.characterPaletteBinding.v5\"}", Encoding.UTF8);
                Require(CharacterPaletteUsageAuthority.ReadBindingEnabled(root, out _) == false,
                    "A binding without an enabled flag must default to disabled.");

                File.WriteAllText(bindingPath, "{ not json", Encoding.UTF8);
                Require(CharacterPaletteUsageAuthority.ReadBindingEnabled(root, out string malformed) == null,
                    "A malformed binding file must yield null.");
                Require(!string.IsNullOrEmpty(malformed), "A malformed binding file must report an error.");
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        // ------------------------------------------------------------------
        // 编译门禁使用的聚合判定
        // ------------------------------------------------------------------

        private static void RunEvaluatePaletteUsage()
        {
            string root = CreateTempPackage();
            try
            {
                string bodyHash = WriteBodyModel(root, BodyModelRelativePath, "fake-body-model");

                CharacterResourcePackage package = BuildPackage(
                    BodyEntry("char.test.model.body", BodyModelRelativePath, CharacterPackageResourceUsageIds.CharacterModel));

                // 未配置色板：不产生任何问题
                Require(CharacterPaletteUsageAuthority.EvaluatePaletteUsage(package, root).Count == 0,
                    "A package without a palette binding file must not produce palette issues.");

                // 绑定存在但未启用：不产生任何问题
                WriteBinding(root, false);
                Require(CharacterPaletteUsageAuthority.EvaluatePaletteUsage(package, root).Count == 0,
                    "A disabled palette binding must not produce palette issues.");

                // 启用但缺 usage 文件
                WriteBinding(root, true);
                List<CharacterPaletteUsageGateIssue> missingUsage = CharacterPaletteUsageAuthority.EvaluatePaletteUsage(package, root);
                Require(missingUsage.Count == 1 && missingUsage[0].Code == CharacterPaletteUsageCodes.PaletteUsageMissing,
                    "An enabled binding without a usage document must report PALETTE_USAGE_MISSING.");

                // 启用 + 损坏的 usage 文件
                WriteUsage(root, "{ not json");
                List<CharacterPaletteUsageGateIssue> malformed = CharacterPaletteUsageAuthority.EvaluatePaletteUsage(package, root);
                Require(malformed.Count == 1 && malformed[0].Code == CharacterPaletteUsageCodes.PaletteUsageInvalid,
                    "A malformed usage document must report PALETTE_USAGE_INVALID.");

                // 启用 + 缺 modelHash
                WriteUsage(root, "{\"format\":\"mx.characterPaletteUsage.v2\"}");
                List<CharacterPaletteUsageGateIssue> noHash = CharacterPaletteUsageAuthority.EvaluatePaletteUsage(package, root);
                Require(noHash.Count == 1 && noHash[0].Code == CharacterPaletteUsageCodes.ModelHashMissing,
                    "A usage document without modelHash must report MODEL_HASH_MISSING.");

                // 启用 + 外来模型哈希（000009 回归）
                WriteUsage(root, "{\"format\":\"mx.characterPaletteUsage.v2\",\"modelHash\":\"sha256:5e6a0068b0a1b0e7\"}");
                List<CharacterPaletteUsageGateIssue> mismatch = CharacterPaletteUsageAuthority.EvaluatePaletteUsage(package, root);
                Require(mismatch.Count == 1 && mismatch[0].Code == CharacterPaletteUsageCodes.ModelHashMismatch,
                    "A foreign model hash must report MODEL_HASH_MISMATCH.");
                Require(mismatch[0].Field == "modelHash", "The mismatch issue must point at the modelHash field.");
                Require(mismatch[0].ExpectedValue == bodyHash, "The mismatch issue must carry the expected body hash.");
                Require(mismatch[0].SourcePath == CharacterPaletteUsageAuthority.UsageRelativePath,
                    "The mismatch issue must point at the usage document.");

                // 启用 + 匹配哈希：通过
                WriteUsage(root, "{\"format\":\"mx.characterPaletteUsage.v2\",\"modelHash\":\"" + bodyHash + "\"}");
                Require(CharacterPaletteUsageAuthority.EvaluatePaletteUsage(package, root).Count == 0,
                    "A matching modelHash must produce no palette issues.");

                // 本体模型缺失：必须报本体侧错误码，而不是静默跳过
                WriteUsage(root, "{\"format\":\"mx.characterPaletteUsage.v2\",\"modelHash\":\"" + bodyHash + "\"}");
                File.Delete(Path.Combine(root, BodyModelRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                List<CharacterPaletteUsageGateIssue> noBodyFile = CharacterPaletteUsageAuthority.EvaluatePaletteUsage(package, root);
                Require(noBodyFile.Count == 1 && noBodyFile[0].Code == CharacterPaletteUsageCodes.BodyModelFileMissing,
                    "A missing body model file must report BODY_MODEL_FILE_MISSING instead of skipping the check.");
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        // ------------------------------------------------------------------
        // 编译器门禁接线：必须出现在 GateReport 里，且首轮只 warning
        // ------------------------------------------------------------------

        private static void RunCompilerGateSurfacesWarning()
        {
            string root = CreateTempPackage();
            try
            {
                string bodyHash = WriteBodyModel(root, BodyModelRelativePath, "fake-body-model");

                CharacterResourcePackage package = BuildPackage(
                    BodyEntry("char.test.model.body", BodyModelRelativePath, CharacterPackageResourceUsageIds.CharacterModel));
                WriteBinding(root, true);

                // 基准：哈希匹配时不得出现任何 palette 问题。
                WriteUsage(root, "{\"format\":\"mx.characterPaletteUsage.v2\",\"modelHash\":\"" + bodyHash + "\"}");
                CharacterCompilerGateReport baseline = CompileGate(package, root, strict: false, allowWarnings: true);
                Require(!baseline.Issues.Exists(item => item != null && IsPaletteCode(item.Code)),
                    "A matching modelHash must not produce palette issues in the compiler gate.");

                // 篡改：写入外来模型哈希（character-000009 回归场景）。
                WriteUsage(root, "{\"format\":\"mx.characterPaletteUsage.v2\",\"modelHash\":\"sha256:5e6a0068b0a1b0e7\"}");
                CharacterCompilerGateReport gate = CompileGate(package, root, strict: false, allowWarnings: true);

                CharacterAuthoringValidationIssue issue = gate.Issues.Find(item =>
                    item != null && item.Code == CharacterPaletteUsageCodes.ModelHashMismatch);
                Require(issue != null, "The compiler gate must surface MODEL_HASH_MISMATCH.");
                Require(issue.Severity == CharacterAuthoringValidationSeverity.Warning,
                    "The first rollout of the palette gate must be warning-only.");
                Require(issue.Gate == CharacterAuthoringValidationGate.WarningOnly,
                    "The first rollout of the palette gate must use WarningOnly.");
                Require(issue.Field == "modelHash", "The gate issue must point at the modelHash field.");
                Require(issue.SourcePath == CharacterPaletteUsageAuthority.UsageRelativePath,
                    "The gate issue must point at config/character_palette_usage.json.");

                // 首轮只 warning：阻断结论必须与基准完全一致。
                Require(gate.ExportBlocked == baseline.ExportBlocked
                        && gate.ImportBlocked == baseline.ImportBlocked
                        && gate.SpawnBlocked == baseline.SpawnBlocked,
                    "A palette usage mismatch must not change any blocking gate in the first rollout.");
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        private static void RunCompilerGateRespectsStrictMode()
        {
            string root = CreateTempPackage();
            try
            {
                string bodyHash = WriteBodyModel(root, BodyModelRelativePath, "fake-body-model");

                CharacterResourcePackage package = BuildPackage(
                    BodyEntry("char.test.model.body", BodyModelRelativePath, CharacterPackageResourceUsageIds.CharacterModel));
                WriteBinding(root, true);

                // 基准：哈希匹配时不得出现 palette 问题（严格模式下其它既有警告不受影响）。
                WriteUsage(root, "{\"format\":\"mx.characterPaletteUsage.v2\",\"modelHash\":\"" + bodyHash + "\"}");
                CharacterCompilerGateReport baseline = CompileGate(package, root, strict: true, allowWarnings: false);
                Require(!baseline.Issues.Exists(item => item != null && IsPaletteCode(item.Code)),
                    "A matching modelHash must not produce palette issues under strict mode.");

                // --strict / --no-warnings 下必须把 palette 警告升级为阻断：
                // 这依赖 AddPaletteUsageDiagnostics 排在 AddStrictDiagnostics 之前。
                WriteUsage(root, "{\"format\":\"mx.characterPaletteUsage.v2\",\"modelHash\":\"sha256:5e6a0068b0a1b0e7\"}");
                CharacterCompilerGateReport strictGate = CompileGate(package, root, strict: true, allowWarnings: false);

                Require(strictGate.Issues.Exists(item =>
                        item != null && item.Code == CharacterPaletteUsageCodes.ModelHashMismatch),
                    "Strict mode must still surface the palette issue itself.");
                Require(strictGate.Issues.Exists(item =>
                        item != null && item.Code == CharacterAuthoringCompilerValidationCodes.StrictWarningBlocked),
                    "Strict mode must upgrade warnings into CHARPKG_STRICT_WARNING_BLOCKED.");
                Require(strictGate.ImportBlocked,
                    "Strict mode must block Unity import when the palette usage is stale.");
            }
            finally
            {
                DeleteDirectory(root);
            }
        }

        private static bool IsPaletteCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return false;
            return code == CharacterPaletteUsageCodes.BodyModelUndeclared
                || code == CharacterPaletteUsageCodes.BodyModelAmbiguous
                || code == CharacterPaletteUsageCodes.BodyModelPathUnsupported
                || code == CharacterPaletteUsageCodes.BodyModelFileMissing
                || code == CharacterPaletteUsageCodes.ModelHashMissing
                || code == CharacterPaletteUsageCodes.ModelHashMismatch
                || code == CharacterPaletteUsageCodes.PaletteUsageMissing
                || code == CharacterPaletteUsageCodes.PaletteUsageInvalid
                || code == CharacterPaletteUsageCodes.PaletteBindingInvalid;
        }

        // ------------------------------------------------------------------
        // fixtures
        // ------------------------------------------------------------------

        private static CharacterCompilerGateReport CompileGate(
            CharacterResourcePackage package,
            string packageRootPath,
            bool strict,
            bool allowWarnings)
        {
            CharacterAuthoringCompileResult result = CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest
            {
                Package = package,
                PackageRootPath = packageRootPath,
                Options = new CharacterAuthoringCompileOptions
                {
                    Strict = strict,
                    AllowWarnings = allowWarnings,
                    ValidateResourceFiles = false,
                    ValidateResourceHashes = false
                }
            });
            return result.GateReport;
        }

        private static CharacterPackageResourceEntry BodyEntry(string resourceKey, string relativePath, string usage)
        {
            return new CharacterPackageResourceEntry
            {
                ResourceKey = resourceKey,
                LocalId = resourceKey,
                StableId = "charpkg.test.resource." + resourceKey,
                TypeId = CharacterPackageResourceTypeIds.Model,
                Usage = usage,
                SourceFormat = CharacterPackageResourceFormatIds.Fbx,
                PackageId = "test",
                RelativePath = relativePath
            };
        }

        private static CharacterResourcePackage BuildPackage(params CharacterPackageResourceEntry[] entries)
        {
            var package = new CharacterResourcePackage
            {
                Manifest = new CharacterPackageManifest
                {
                    PackageId = "test",
                    StableId = "charpkg.test",
                    Version = "0.1.0",
                    Kind = CharacterResourcePackageKind.Character
                },
                ResourceCatalog = new CharacterPackageResourceCatalog()
            };
            package.ResourceCatalog.Entries.AddRange(entries);
            return package;
        }

        private static string CreateTempPackage()
        {
            string root = Path.Combine(Path.GetTempPath(), "mx-palette-usage-authority-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void WriteBinding(string packageRoot, bool enabled)
        {
            string path = Path.Combine(packageRoot, CharacterPaletteUsageAuthority.BindingRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "{\"format\":\"mx.characterPaletteBinding.v5\",\"enabled\":" + (enabled ? "true" : "false") + "}", Encoding.UTF8);
        }

        private static void WriteUsage(string packageRoot, string json)
        {
            string path = Path.Combine(packageRoot, CharacterPaletteUsageAuthority.UsageRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        /// <summary>写入本体模型文件并返回其 sha256。</summary>
        private static string WriteBodyModel(string packageRoot, string relativePath, string content)
        {
            string path = Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(content));
            return CharacterPackageHashUtility.ComputeFileSha256(path);
        }

        private static void DeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
                // 临时目录清理失败不应让测试失败。
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new Exception("CharacterPaletteUsageAuthorityTests: " + message);
        }
    }
}
