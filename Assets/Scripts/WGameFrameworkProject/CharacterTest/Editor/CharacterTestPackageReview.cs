using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Evidence-based package review; does not change the build/run verdict.</summary>
    internal static class CharacterTestPackageReview
    {
        internal enum Kind { Fix, Confirm, Candidate, Keep, Unknown }
        internal sealed class Finding
        {
            internal Kind Status;
            internal string Title, Evidence, Action;
        }

        internal static List<Finding> Evaluate(CharacterTestPackageOverviewReport.Inventory inventory,
            JObject configuration, JObject details, string acceptance, string buildResult)
        {
            var findings = new List<Finding>();
            void Add(Kind kind, string title, string evidence, string action) => findings.Add(new Finding
                { Status = kind, Title = title, Evidence = evidence, Action = action });
            if (acceptance == "Failed" || (acceptance == "Passed" && buildResult != "Succeeded"))
                Add(Kind.Fix, "构建／运行验收", "本次结果失败或与构建状态矛盾。", "先解决原始报告中的失败，不应把该包标为可交付。");
            else if (acceptance == "Passed" && buildResult == "Succeeded")
                Add(Kind.Keep, "已记录的构建／运行验收", "本次入口报告 Passed。此处沿用原验收，不代表重新运行 Player。",
                    "保留这份运行证据；它不证明每项资源都必要，也不代表所有玩法和性能均已验证。");
            else Add(Kind.Unknown, "完整运行验收", "当前不是完整 Passed 结果（可能仅 BuildOnly）。", "实际运行并完成验收后再判断可交付性。");
            if (inventory == null)
            {
                Add(Kind.Unknown, "包体评审", "没有本次成功构建的文件清单。", "不读取旧包作判断。");
                return findings;
            }

            bool Has(string path) => inventory.Files.Any(f => f.RelativePath.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (configuration == null || configuration["scriptingBackend"] == null || configuration["configuration"] == null)
                Add(Kind.Unknown, "构建配置", "缺少本次配置回执。", "不能仅凭文件名判断是否符合发布预设。");
            else
            {
                bool il2cpp = (string)configuration["scriptingBackend"] == "IL2CPP";
                bool release = (string)configuration["configuration"] == "Release";
                bool mismatch = !il2cpp || !Has("GameAssembly.dll")
                    || !Has("CharacterTestPerformance_Data/il2cpp_data/Metadata/global-metadata.dat")
                    || inventory.Files.Any(f => f.RelativePath.StartsWith("MonoBleedingEdge/", StringComparison.OrdinalIgnoreCase))
                    || (release && ((bool?)configuration["developmentBuild"] == true
                        || (bool?)configuration["scriptDebugging"] == true
                        || (bool?)configuration["autoconnectProfiler"] == true
                        || (bool?)configuration["deepProfiling"] == true));
                if (mismatch) Add(Kind.Fix, "发布配置／后端一致性", "配置或实际文件与当前 IL2CPP 发布约定冲突。", "修正配置并重建复验，不能靠删除必要运行文件解决。");
                else Add(Kind.Keep, "运行库与 IL2CPP 元数据", "已找到 GameAssembly 与运行元数据，未发现上述后端冲突。",
                    "属于正常运行构成，应保留。此结论只说明用途正常，不等于其大小已经最优。");
                if (new[] { "developmentBuild", "scriptDebugging", "autoconnectProfiler", "deepProfiling" }
                    .Any(key => configuration[key] == null))
                    Add(Kind.Unknown, "开发／调试标志", "配置回执未提供全部开发／调试标志。", "补齐回执后核验，不能把缺失值当成 false。");
            }
            var offlineSymbols = inventory.Files.Where(f => f.RelativePath.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)
                || f.RelativePath.EndsWith(".mdb", StringComparison.OrdinalIgnoreCase)
                || f.RelativePath.Contains("_BackUpThisFolder_ButDontShipItWithYourGame/")
                || f.RelativePath.Contains("_BurstDebugInformation_DoNotShip/")).ToArray();
            if (offlineSymbols.Length > 0)
                Add(Kind.Fix, "离线调试产物混入 Player", offlineSymbols.Length + " 个文件，共 " + Size(offlineSymbols.Sum(f => f.Bytes)) + "。",
                    "按符号回执移到 Player 外并校验；开发版的 il2cpp.usym 属运行时行号数据，不能混同处理。");
            else Add(Kind.Keep, "离线符号分离", "Player 文件清单内未发现 PDB/MDB 或 DoNotShip 产物。", "分发 Player 文件夹，离线符号另存；不要为减体积删除运行库。");

            var unlisted = inventory.Files.Where(f => f.Category == CharacterTestPackageOverviewReport.UnlistedBundles).ToArray();
            if (unlisted.Length > 0)
                Add(Kind.Confirm, "清单外 Bundle 的归属", unlisted.Length + " 个，共 " + Size(unlisted.Sum(f => f.Bytes)) + "，未列入本次正式 manifest。",
                    "先查历史台账与引用。未知归属继续保留；不能直接判为垃圾或自动删除。");

            if (details?["assets"] is JArray assets)
            {
                JToken[] overlap = assets.Where(a => a["bundleDeclaration"] != null).ToArray();
                if (overlap.Length > 0)
                    Add(Kind.Candidate, "优先 1：内置资源与 Bundle 的双重入口", overlap.Length + " 个源路径既有内置资源记录，又有正式 Bundle 目录声明。",
                        "优先核对是否需要同时发布两条加载路径。分离前确认启动、角色和动画依赖；此证据未证明全部内容重复，不能据此报可节省字节。");
                var previews = assets.Where(a => (a["referenceChain"] as JArray)?.Values<string>()
                    .Any(p => p.EndsWith("_character_preview.prefab", StringComparison.OrdinalIgnoreCase)) == true).ToArray();
                if (previews.Length > 0)
                    Add(Kind.Confirm, "预览 Prefab 是否属于正式场景内容", "已定位 " + previews.Length + " 个资源的场景→预览 Prefab 引用链，相关记录合计 "
                        + Size(previews.Sum(a => (long)a["bytes"])) + "（压缩前）。",
                        "名称含 preview 不能证明无用。若仅供编辑器预览，应隔离出构建场景并复查 Resources；若是正式展示则保留。");
                long fonts = assets.Where(a => ((string)a["path"] ?? "").StartsWith("Assets/Resources/Fonts/", StringComparison.Ordinal))
                    .Sum(a => (long)a["bytes"]);
                if (fonts > 0)
                    Add(Kind.Candidate, "优先 2：字体语言范围", "Resources 字体记录合计 " + Size(fonts) + "（压缩前）。",
                        "按实际支持的语言评估随包、分包或字库子集；支持多语言时保留有理由，默认使用某一种语言不能证明其他字体无用。修改需复验缺字和语言切换。");
                long effects = assets.Where(a => ((string)a["path"] ?? "").StartsWith("Assets/Piloto Studio/", StringComparison.Ordinal))
                    .Sum(a => (long)a["bytes"]);
                if (effects > 0)
                    Add(Kind.Candidate, "优先 3：特效素材的发布规格", "Piloto 素材记录合计 " + Size(effects) + "（压缩前），只是候选规模。",
                        "结合实际使用效果检查贴图分辨率和压缩设置，再以视觉对照和重建差值决定；不能凭来源目录或大小认定多余。");
            }
            else Add(Kind.Unknown, "资源必要性与重复量", "没有本次内置资源明细。", "不能从压缩容器总大小推断无用资源或重复数量。");

            if ((string)details?["native"]?["status"] == "Verified")
            {
                long generic = (details["native"]["groups"] as JArray)?.Where(g => (string)g["name"] == "Shared generic code")
                    .Sum(g => (long)g["bytes"]) ?? 0;
                Add(Kind.Unknown, "代码大小是否合理", "共享泛型贡献 " + Size(generic) + "；没有等功能、等性能的包体对照或预算。",
                    "不能把泛型、生成表或 Demo 命名的程序集直接判为无用。若继续压缩，单独对比代码生成／裁剪设置，并完整复验反射、配置加载和玩法，不直接删 DLL。");
            }
            else Add(Kind.Unknown, "代码归属", "没有经匹配 PDB 验证的原生明细。", "不以源码行数或中间 DLL 大小推断问题。");
            Add(Kind.Unknown, "整包大小是否达标", "本次 " + Size(inventory.TotalBytes) + "；本报告未配置可比较的包体预算。",
                "不能仅凭总 MiB 判过大或过小。所有优化候选都要用重建后的实际文件差值确认收益，压缩前统计不能直接相加当作节省量。");
            return findings;
        }

        internal static void Append(StringBuilder html, CharacterTestPackageOverviewReport.Inventory inventory,
            JObject configuration, JObject details, string acceptance, string buildResult)
        {
            List<Finding> findings = Evaluate(inventory, configuration, details, acceptance, buildResult);
            int fixes = findings.Count(f => f.Status == Kind.Fix);
            html.Append("<section id=\"package-review\"><h2>打包评审：哪些正常，哪些要处理</h2><p><strong>")
                .Append(fixes > 0 ? "发现需要修复的项目" : acceptance == "Passed" && buildResult == "Succeeded"
                    ? "运行验收已通过，包体策略仍有待确认项" : "完整验收尚未确认")
                .Append("</strong></p><p class=\"muted\">本页规则发现需修复 ").Append(fixes)
                .Append(" 项、待确认 ").Append(findings.Count(f => f.Status == Kind.Confirm))
                .Append(" 项、优化候选 ").Append(findings.Count(f => f.Status == Kind.Candidate))
                .Append(" 项。零个已知问题不等于全面无问题；评审不改写原始验收。正常构成不等于体积最优，优化候选不等于已确认浪费。</p>")
                .Append("<div class=\"scroll\"><table><thead><tr><th>判断</th><th>项目／证据</th><th>建议</th></tr></thead><tbody>");
            foreach (Finding finding in findings.OrderBy(f => (int)f.Status))
            {
                string label = finding.Status switch { Kind.Fix => "需要修复", Kind.Confirm => "待确认", Kind.Candidate => "优化候选", Kind.Keep => "正常／保留", _ => "证据不足" };
                string style = finding.Status == Kind.Fix ? "review-fix" : finding.Status == Kind.Keep ? "review-keep"
                    : finding.Status == Kind.Unknown ? "" : "review-check";
                html.Append("<tr><td><span class=\"badge ").Append(style).Append("\" style=\"white-space:nowrap\">")
                    .Append(E(label)).Append("</span></td><td><strong>").Append(E(finding.Title))
                    .Append("</strong><br>").Append(E(finding.Evidence)).Append("</td><td>").Append(E(finding.Action)).Append("</td></tr>");
            }
            html.Append("</tbody></table></div></section>");
        }
        private static string Size(long bytes) => (bytes / 1048576d).ToString("F2", CultureInfo.InvariantCulture) + " MiB";
        private static string E(string text) => WebUtility.HtmlEncode(text ?? string.Empty);
    }
}
