using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

/// <summary>
/// character palette-usage-audit
///
/// 全量审计：对 Authoring/CharacterPackages 下的每个角色包，用唯一权威入口
/// <see cref="CharacterPaletteUsageAuthority"/> 判定 config/character_palette_usage.json 的
/// modelHash 是否等于该包声明的本体模型源文件哈希。
///
/// 这是「根治」的验收工具：它跑的是保存接口与编译门禁共用的同一份判定逻辑，
/// 因此审计结果与 Unity 导入门禁的结论一致。
///
/// 分类：
///   pass           —— 绑定启用、本体模型已解析、usage.modelHash 匹配。
///   skip           —— 未配置色板（绑定文件缺失或 enabled=false），无需判定。
///   usage-missing  —— 绑定启用但缺少 usage 文档（Unity 导入会失败，属实质缺陷）。
///   mismatch       —— usage.modelHash 与本体模型不一致（属实质缺陷）。
///   body-model-*   —— 本体模型未声明 / 多候选 / 路径不安全 / 文件缺失（属实质缺陷）。
/// </summary>
internal static partial class CharacterPackageCommands
{
    private const string PaletteAuditPass = "pass";
    private const string PaletteAuditSkip = "skip";
    private const string PaletteAuditUsageMissing = "usage-missing";
    private const string PaletteAuditMismatch = "mismatch";
    private const string PaletteAuditBodyModel = "body-model";
    private const string PaletteAuditReadError = "read-error";

    private static int PaletteUsageAudit(string[] args, JsonSerializerOptions options)
    {
        string packagesRoot = Program.GetOption(
            args,
            "--packages-root",
            Path.Combine("Authoring", "CharacterPackages"));
        bool asJson = Program.HasFlag(args, "--json");
        if (!Directory.Exists(packagesRoot))
            throw new DirectoryNotFoundException("Character packages root was not found: " + packagesRoot);

        var rows = new List<PaletteUsageAuditRow>();
        foreach (string packagePath in Directory.GetDirectories(packagesRoot))
        {
            string name = Path.GetFileName(packagePath);
            if (name.StartsWith(".", StringComparison.Ordinal))
                continue;
            rows.Add(AuditSinglePackage(packagePath));
        }

        rows.Sort((left, right) => string.CompareOrdinal(left.Package, right.Package));

        int pass = rows.Count(row => row.Status == PaletteAuditPass);
        int skip = rows.Count(row => row.Status == PaletteAuditSkip);
        int findings = rows.Count - pass - skip;

        if (asJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                packagesRoot,
                pass,
                skip,
                findings,
                rows
            }, options));
        }
        else
        {
            Console.WriteLine("package".PadRight(26) + "status".PadRight(16) + "code".PadRight(30) + "detail");
            foreach (PaletteUsageAuditRow row in rows)
            {
                Console.WriteLine(
                    row.Package.PadRight(26)
                    + row.Status.PadRight(16)
                    + (string.IsNullOrEmpty(row.Code) ? "-" : row.Code).PadRight(30)
                    + row.Detail);
            }
            Console.WriteLine();
            Console.WriteLine($"pass={pass} skip={skip} findings={findings} total={rows.Count}");
        }

        return findings == 0 ? Program.ExitReady : Program.ExitValidationBlocked;
    }

    private static PaletteUsageAuditRow AuditSinglePackage(string packagePath)
    {
        string name = Path.GetFileName(packagePath);
        string usagePath = Path.Combine(
            packagePath,
            CharacterPaletteUsageAuthority.UsageRelativePath.Replace('/', Path.DirectorySeparatorChar));

        bool? bindingEnabled;
        try
        {
            bindingEnabled = CharacterPaletteUsageAuthority.ReadBindingEnabled(packagePath, out string bindingError);
            if (bindingEnabled == null && !string.IsNullOrEmpty(bindingError))
            {
                return new PaletteUsageAuditRow
                {
                    Package = name,
                    Status = PaletteAuditBodyModel,
                    Code = CharacterPaletteUsageCodes.PaletteBindingInvalid,
                    Detail = bindingError,
                    UsagePath = ToDisplayPath(usagePath)
                };
            }
        }
        catch (Exception ex)
        {
            return new PaletteUsageAuditRow
            {
                Package = name,
                Status = PaletteAuditReadError,
                Code = "PALETTE_BINDING_UNREADABLE",
                Detail = ex.Message,
                UsagePath = ToDisplayPath(usagePath)
            };
        }

        if (bindingEnabled == null || bindingEnabled == false)
        {
            return new PaletteUsageAuditRow
            {
                Package = name,
                Status = PaletteAuditSkip,
                Code = string.Empty,
                Detail = bindingEnabled == null ? "no palette binding file" : "palette binding disabled",
                UsagePath = ToDisplayPath(usagePath)
            };
        }

        CharacterResourcePackage package;
        try
        {
            package = ReadPackage(packagePath, Program.CreateJsonOptions());
        }
        catch (Exception ex)
        {
            return new PaletteUsageAuditRow
            {
                Package = name,
                Status = PaletteAuditReadError,
                Code = "PACKAGE_UNREADABLE",
                Detail = ex.Message,
                UsagePath = ToDisplayPath(usagePath)
            };
        }

        List<CharacterPaletteUsageGateIssue> issues = CharacterPaletteUsageAuthority.EvaluatePaletteUsage(package, packagePath);
        if (issues.Count == 0)
        {
            CharacterBodyModelResolution body = CharacterPaletteUsageAuthority.ResolveBodyModel(package, packagePath, true);
            return new PaletteUsageAuditRow
            {
                Package = name,
                Status = PaletteAuditPass,
                Code = string.Empty,
                Detail = body.RelativePath + " " + body.ModelHash,
                UsagePath = ToDisplayPath(usagePath)
            };
        }

        CharacterPaletteUsageGateIssue issue = issues[0];
        return new PaletteUsageAuditRow
        {
            Package = name,
            Status = ResolveStatus(issue.Code),
            Code = issue.Code,
            Detail = issue.Message,
            UsagePath = ToDisplayPath(usagePath),
            ExpectedModelHash = issue.ExpectedValue,
            ActualModelHash = issue.ActualValue
        };
    }

    private static string ResolveStatus(string code)
    {
        if (code == CharacterPaletteUsageCodes.PaletteUsageMissing)
            return PaletteAuditUsageMissing;
        if (code == CharacterPaletteUsageCodes.ModelHashMismatch || code == CharacterPaletteUsageCodes.ModelHashMissing)
            return PaletteAuditMismatch;
        return PaletteAuditBodyModel;
    }

    private static string ToDisplayPath(string path) => path.Replace('\\', '/');

    private sealed class PaletteUsageAuditRow
    {
        public string Package { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string UsagePath { get; set; } = string.Empty;
        public string ExpectedModelHash { get; set; } = string.Empty;
        public string ActualModelHash { get; set; } = string.Empty;
    }
}
