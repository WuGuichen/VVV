using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Human-readable, file-size-based report for a completed Windows Player attempt.</summary>
    internal static class CharacterTestPackageOverviewReport
    {
        internal const string FileName = "CharacterTestPackageOverview.html";
        internal const string ResidueSnapshotFileName = "CharacterTestBundleResidueReport.txt";
        internal const string ConfigurationSnapshotFileName = "CharacterTestBuildConfiguration.json";
        private const string DataDirectoryName = "CharacterTestPerformance_Data";
        private const string ManifestRelativePath = "MxFramework/Resources/global_resource_artifact_set.json";
        private const string BundlePrefix = "MxFramework/Resources/Bundles/";
        private const string ExternalBundlePrefix = "MxFramework/Resources/ExternalBundles/";
        private const string CharacterPrefix = "MxFramework/Generated/CharacterPackages/";
        private const string CatalogPrefix = "MxFramework/CharacterTest/RuntimeCatalogs/";
        internal const string PlayerFiles = "Player 运行文件";
        internal const string PlayerData = "Player 数据";
        internal const string CurrentBundles = "正式资源 Bundle";
        internal const string UnlistedBundles = "清单外 Bundle";
        internal const string ExternalBundles = "外部 Bundle";
        internal const string CharacterPackages = "角色暂存包";
        internal const string RuntimeCatalogs = "运行目录镜像";
        internal const string OtherStreaming = "其他 StreamingAssets";

        private static readonly string[] CategoryOrder =
        {
            PlayerData, PlayerFiles, CurrentBundles, CharacterPackages, ExternalBundles,
            RuntimeCatalogs, UnlistedBundles, OtherStreaming
        };

        internal sealed class FileEntry
        {
            internal string RelativePath;
            internal long Bytes;
            internal string Category;
            internal string BundleGroup;
            internal string Ownership;
            internal bool IsStreaming;
        }

        internal sealed class Inventory
        {
            internal readonly List<FileEntry> Files = new();
            internal long TotalBytes;
            internal long StreamingBytes;
            internal long CurrentBundleBytes;

            internal long CategoryBytes(string category) => Files.Where(file => file.Category == category).Sum(file => file.Bytes);
            internal int CategoryCount(string category) => Files.Count(file => file.Category == category);
        }

        /// <remarks>AllocByDesign: one-time Editor report inventory.</remarks>
        internal static Inventory Analyze(
            string playerDirectory,
            string residueReportPath = null,
            IReadOnlyDictionary<string, string> bundleGroups = null)
        {
            string root = Path.GetFullPath(playerDirectory ?? throw new ArgumentNullException(nameof(playerDirectory)));
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException("Player directory not found: " + root);
            string streamingRoot = Path.Combine(root, DataDirectoryName, "StreamingAssets");
            string manifestPath = Path.Combine(streamingRoot, ManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Player resource manifest not found.", manifestPath);

            JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
            if (!string.Equals((string)manifest["buildTarget"], "StandaloneWindows64", StringComparison.Ordinal))
                throw new InvalidDataException("Player resource manifest has an unexpected build target.");
            var currentBundles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JToken artifact in manifest["artifacts"] as JArray ?? throw new InvalidDataException("Manifest artifacts missing."))
            {
                if (!string.Equals((string)artifact["kind"], "bundle", StringComparison.Ordinal))
                    continue;
                string relativePath = NormalizeManifestPath((string)artifact["relativePath"]);
                if (!relativePath.StartsWith(BundlePrefix, StringComparison.OrdinalIgnoreCase)
                    || !currentBundles.Add(relativePath))
                    throw new InvalidDataException("Invalid or duplicate bundle manifest path: " + relativePath);
            }

            var ownership = ReadResidueStates(residueReportPath);
            var inventory = new Inventory();
            string rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string dataPrefix = DataDirectoryName + "/";
            string streamingPrefix = dataPrefix + "StreamingAssets/";
            foreach (string filePath in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                string relativePath = filePath.Substring(rootWithSeparator.Length).Replace('\\', '/');
                bool isStreaming = relativePath.StartsWith(streamingPrefix, StringComparison.OrdinalIgnoreCase);
                string streamRelative = isStreaming ? relativePath.Substring(streamingPrefix.Length) : null;
                string category = Classify(relativePath, streamRelative, currentBundles);
                long bytes = new FileInfo(filePath).Length;
                if (bytes < 0) throw new InvalidDataException("Negative file size: " + relativePath);
                var entry = new FileEntry
                {
                    RelativePath = relativePath,
                    Bytes = bytes,
                    Category = category,
                    IsStreaming = isStreaming,
                    BundleGroup = category == CurrentBundles
                        ? ResolveBundleGroup(Path.GetFileName(streamRelative), bundleGroups) : null,
                    Ownership = category == UnlistedBundles
                        ? ResolveOwnership(streamRelative, bytes, ownership) : null
                };
                inventory.Files.Add(entry);
                checked
                {
                    inventory.TotalBytes += bytes;
                    if (isStreaming) inventory.StreamingBytes += bytes;
                    if (category == CurrentBundles) inventory.CurrentBundleBytes += bytes;
                }
            }

            foreach (string relativePath in currentBundles)
            {
                if (!inventory.Files.Any(file => file.RelativePath.Equals(
                    streamingPrefix + relativePath, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Manifest bundle missing from Player: " + relativePath);
            }
            if (inventory.Files.Sum(file => file.Bytes) != inventory.TotalBytes
                || CategoryOrder.Sum(inventory.CategoryBytes) != inventory.TotalBytes
                || inventory.Files.Where(file => file.IsStreaming).Sum(file => file.Bytes) != inventory.StreamingBytes)
                throw new InvalidDataException("Player size categories do not reconcile.");
            return inventory;
        }

        /// <remarks>AllocByDesign: report is written once per Editor build attempt.</remarks>
        internal static void Write(
            string reportPath,
            string playerDirectory,
            bool builtThisAttempt,
            string acceptanceStatus,
            string buildResult,
            string issue,
            string editorRunReportPath,
            string inputReceiptPath = null,
            string residueReportPath = null)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            Directory.CreateDirectory(directory);
            string configurationSnapshot = Path.Combine(directory, ConfigurationSnapshotFileName);
            string detailsSnapshot = Path.Combine(directory, CharacterTestPackageDetailEvidence.SnapshotName);
            if (File.Exists(detailsSnapshot)) File.Delete(detailsSnapshot);
            if (builtThisAttempt && !string.IsNullOrWhiteSpace(editorRunReportPath))
            {
                string error = CharacterTestPackageDetailEvidence.ErrorPathFor(editorRunReportPath);
                if (File.Exists(error))
                    throw new InvalidDataException("Package detail capture failed: " + File.ReadAllText(error));
                string source = CharacterTestPackageDetailEvidence.PathFor(editorRunReportPath);
                if (File.Exists(source))
                {
                    JObject details = JObject.Parse(File.ReadAllText(source));
                    CharacterTestPackageDetailEvidence.Validate(details, playerDirectory);
                    File.Copy(source, detailsSnapshot);
                }
            }
            if (File.Exists(configurationSnapshot)) File.Delete(configurationSnapshot);
            if (builtThisAttempt && !string.IsNullOrWhiteSpace(editorRunReportPath))
            {
                string configurationSource = CharacterTestWindowsBuildConfiguration.ReceiptPath(editorRunReportPath);
                if (File.Exists(configurationSource))
                {
                    var configuration = JObject.Parse(File.ReadAllText(configurationSource));
                    string symbols = (string)configuration["symbolsDirectory"];
                    if (!string.IsNullOrWhiteSpace(symbols))
                        configuration["symbolsDirectory"] = Path.GetRelativePath(directory,
                            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(configurationSource), symbols)))
                            .Replace('\\', '/');
                    File.WriteAllText(configurationSnapshot, configuration.ToString());
                }
            }
            string residueSnapshotPath = Path.Combine(directory, ResidueSnapshotFileName);
            bool residueAlreadyLocal = !string.IsNullOrWhiteSpace(residueReportPath)
                && string.Equals(Path.GetFullPath(residueReportPath), Path.GetFullPath(residueSnapshotPath),
                    StringComparison.OrdinalIgnoreCase);
            if (File.Exists(residueSnapshotPath)
                && (!builtThisAttempt || !residueAlreadyLocal))
                File.Delete(residueSnapshotPath);
            if (builtThisAttempt && !string.IsNullOrWhiteSpace(residueReportPath) && File.Exists(residueReportPath))
            {
                if (!residueAlreadyLocal)
                    File.Copy(residueReportPath, residueSnapshotPath);
                residueReportPath = residueSnapshotPath;
            }
            else
                residueReportPath = null;
            Inventory inventory = builtThisAttempt
                ? Analyze(playerDirectory, residueReportPath, LoadBundleGroups()) : null;
            string html = Render(inventory, acceptanceStatus, buildResult, issue,
                editorRunReportPath, inputReceiptPath, residueReportPath, reportPath);
            string pendingPath = reportPath + ".pending";
            try
            {
                File.WriteAllText(pendingPath, html, new UTF8Encoding(false));
                if (File.Exists(reportPath)) File.Delete(reportPath);
                File.Move(pendingPath, reportPath);
            }
            finally
            {
                if (File.Exists(pendingPath)) File.Delete(pendingPath);
            }
        }

        internal static string Render(
            Inventory inventory,
            string acceptanceStatus,
            string buildResult,
            string issue,
            string editorRunReportPath,
            string inputReceiptPath,
            string residueReportPath,
            string reportPath)
        {
            var html = new StringBuilder(16000);
            string configurationPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath)), ConfigurationSnapshotFileName);
            JObject configuration = File.Exists(configurationPath) ? JObject.Parse(File.ReadAllText(configurationPath)) : null;
            string buildLabel = configuration == null ? "Windows64 Player" : "Windows64 "
                + (string)configuration["configuration"] + " · " + (string)configuration["scriptingBackend"];
            html.Append("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">")
                .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
                .Append("<title>CharacterTest 打包概览</title><style>")
                .Append("body{font:15px/1.55 system-ui,'Microsoft YaHei',sans-serif;background:#f4f6fa;color:#192335;margin:0}")
                .Append("main{max-width:1100px;margin:0 auto;padding:28px 20px 60px}h1{margin:0 0 6px;font-size:28px}")
                .Append("h2{font-size:20px;margin:0 0 14px}.muted{color:#627086}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:12px;margin:20px 0}")
                .Append(".card,section{background:white;border:1px solid #e1e6ef;border-radius:12px;padding:18px;margin:14px 0}")
                .Append(".card strong{display:block;font-size:23px}.badge{display:inline-block;border-radius:999px;padding:3px 10px;background:#e5eaf2}")
                .Append(".Passed{background:#d8f5e3;color:#08623a}.Failed{background:#ffe0df;color:#922b2b}.BuildOnly{background:#fff0cb;color:#744d00}")
                .Append(".review-keep{background:#d8f5e3;color:#08623a}.review-fix{background:#ffe0df;color:#922b2b}.review-check{background:#fff0cb;color:#744d00}")
                .Append("table{border-collapse:collapse;width:100%;font-size:14px}th,td{text-align:left;padding:9px 7px;border-bottom:1px solid #e8ebf1}")
                .Append("td.num,th.num{text-align:right;white-space:nowrap}.bar{height:9px;border-radius:9px;background:#e9eef5;min-width:70px}")
                .Append(".bar i{display:block;height:100%;border-radius:9px;background:#4a75d1}.scroll{overflow-x:auto}")
                .Append("code{overflow-wrap:anywhere}.warn{color:#9b4e14}details{margin:8px 0}a{color:#3158ae}")
                .Append("</style></head><body><main><h1>CharacterTest 打包概览</h1>")
                .Append("<p class=\"muted\">").Append(E(buildLabel)).Append(" · 文件实际字节数 · ")
                .Append(E(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)))
                .Append("</p><div class=\"cards\"><div class=\"card\">最终状态<strong><span class=\"badge ")
                .Append(E(acceptanceStatus)).Append("\">").Append(E(acceptanceStatus)).Append("</span></strong></div>")
                .Append("<div class=\"card\">Player 构建<strong>").Append(E(buildResult)).Append("</strong></div>")
                .Append("<div class=\"card\">Player 文件总量<strong>")
                .Append(inventory == null ? "不可用" : FormatSize(inventory.TotalBytes))
                .Append("</strong></div></div>");
            string detailPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath)), CharacterTestPackageDetailEvidence.SnapshotName);
            JObject reviewDetails = inventory != null && File.Exists(detailPath) ? JObject.Parse(File.ReadAllText(detailPath)) : null;
            CharacterTestPackageReview.Append(html, inventory, configuration, reviewDetails, acceptanceStatus, buildResult);
            if (configuration != null)
            {
                html.Append("<section><h2>构建配置</h2><p>")
                    .Append(E((string)configuration["architecture"])).Append(" · C++ ")
                    .Append(E((string)configuration["cppCompilerConfiguration"])).Append(" · ")
                    .Append(E((string)configuration["il2cppCodeGeneration"])).Append(" · 裁剪 ")
                    .Append(E((string)configuration["managedStrippingLevel"])).Append(" · ")
                    .Append(E((string)configuration["compression"])).Append(" · ")
                    .Append(E((string)configuration["graphicsApi"])).Append("</p><p>调试符号已独立归档：")
                    .Append(FormatSize((long?)configuration["symbolBytes"] ?? 0)).Append("，")
                    .Append((int?)configuration["symbolFileCount"] ?? 0).Append(" 个文件；不计入 Player 体积。</p></section>");
            }
            if (!string.IsNullOrWhiteSpace(issue))
                html.Append("<section><h2>需要处理</h2><p class=\"warn\">").Append(E(issue)).Append("</p></section>");
            if (inventory == null)
                html.Append("<section><h2>包体数据</h2><p>本次没有成功构建 Player，因此不展示已有目录的旧数据。</p></section>");
            else
            {
                html.Append("<section><h2>整包构成</h2><p class=\"muted\">占比以全部 Player 文件 ")
                    .Append(inventory.TotalBytes.ToString(CultureInfo.InvariantCulture)).Append(" B 为分母。</p>");
                AppendCategoryTable(html, inventory, CategoryOrder, inventory.TotalBytes);
                html.Append("</section>");
                AppendPlayerDetails(html, inventory, reportPath);
                html.Append("<section><h2>资源文件构成</h2><p class=\"muted\">占比以 StreamingAssets ")
                    .Append(inventory.StreamingBytes.ToString(CultureInfo.InvariantCulture)).Append(" B 为分母。</p>");
                AppendCategoryTable(html, inventory, CategoryOrder.Where(name => name != PlayerData && name != PlayerFiles),
                    inventory.StreamingBytes);
                html.Append("</section><section><h2>正式资源 Bundle</h2><p class=\"muted\">按资源规则名称归类；占比以正式 Bundle 为分母。此处是 Bundle 文件体积，不代表其内部单个素材的大小。</p>");
                AppendBundleGroupTable(html, inventory);
                html.Append("</section><section><h2>最大的资源文件</h2>");
                AppendFileTable(html, inventory.Files.Where(file => file.IsStreaming).OrderByDescending(file => file.Bytes).Take(12));
                html.Append("</section><details><summary>最大的 Player 文件</summary>");
                AppendFileTable(html, inventory.Files.OrderByDescending(file => file.Bytes).Take(12));
                html.Append("</details>");
                var unlisted = inventory.Files.Where(file => file.Category == UnlistedBundles).ToArray();
                if (unlisted.Length > 0)
                {
                    html.Append("<section><h2>清单外 Bundle</h2><p class=\"warn\">仅报告并保留；不会自动删除。</p>");
                    AppendFileTable(html, unlisted);
                    html.Append("</section>");
                }
            }
            html.Append("<details><summary>原始证据与口径</summary><p>大小为文件长度；MiB = 1,048,576 B。百分比四舍五入，下载压缩包和磁盘分配空间可能不同。</p><ul>");
            AppendEvidenceLink(html, "Editor 运行报告", editorRunReportPath, reportPath);
            AppendEvidenceLink(html, "实际构建配置与符号文件清单", configurationPath, reportPath);
            AppendEvidenceLink(html, "代码与内置资源明细快照", Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath)),
                CharacterTestPackageDetailEvidence.SnapshotName), reportPath);
            AppendEvidenceLink(html, "输入回执", inputReceiptPath, reportPath);
            AppendEvidenceLink(html, "Bundle 残留报告", residueReportPath, reportPath);
            string reportDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath)), "Reports");
            if (!Directory.Exists(reportDirectory))
                reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            AppendEvidenceLink(html, "Player 运行报告",
                Path.Combine(reportDirectory, "CharacterTestPerformancePlayerRunReport.txt"), reportPath);
            AppendEvidenceLink(html, "Warm Run 计时报告",
                Path.Combine(reportDirectory, "CharacterTestPerformancePlayerWarmRunReport.txt"), reportPath);
            AppendEvidenceLink(html, "Profiler Recorder 报告",
                Path.Combine(reportDirectory, "CharacterTestPerformancePlayerProfilerRecorderReport.txt"), reportPath);
            AppendEvidenceLink(html, "动画报告",
                Path.Combine(reportDirectory, "CharacterTestPerformancePlayerAnimationReport.txt"), reportPath);
            AppendEvidenceLink(html, "移动、攻击与跳跃逐帧记录",
                Path.Combine(reportDirectory, CharacterTestPlayerGameplayProbe.TraceFileName), reportPath);
            html.Append("</ul></details></main></body></html>");
            return html.ToString();
        }

        private static void AppendPlayerDetails(StringBuilder html, Inventory inventory, string reportPath)
        {
            html.Append("<p><a href=\"#player-runtime\">运行文件明细</a> · <a href=\"#player-data\">Player 数据明细</a> · <a href=\"#native-details\">代码归属</a> · <a href=\"#asset-details\">内置资源来源</a></p>");
            foreach (string category in new[] { PlayerFiles, PlayerData })
            {
                html.Append("<section id=\"").Append(category == PlayerFiles ? "player-runtime" : "player-data").Append("\"><h2>")
                    .Append(E(category)).Append("：具体是什么</h2><p class=\"muted\">按实际文件长度统计；占比以本分类 ")
                    .Append(FormatSize(inventory.CategoryBytes(category))).Append(" 为分母。Player 数据目录也包含原生插件，并非全是美术资源。</p>")
                    .Append("<div class=\"scroll\"><table><thead><tr><th>文件</th><th>用途</th><th class=\"num\">大小</th><th class=\"num\">分类占比</th></tr></thead><tbody>");
                foreach (FileEntry file in inventory.Files.Where(f => f.Category == category).OrderByDescending(f => f.Bytes))
                    html.Append("<tr><td><code>").Append(E(file.RelativePath)).Append("</code></td><td>")
                        .Append(E(DescribePlayerFile(file.RelativePath))).Append("</td><td class=\"num\">").Append(FormatSize(file.Bytes))
                        .Append("</td><td class=\"num\">").Append(Percent(file.Bytes, inventory.CategoryBytes(category))).Append("</td></tr>");
                html.Append("</tbody></table></div></section>");
            }
            string path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath)), CharacterTestPackageDetailEvidence.SnapshotName);
            if (!File.Exists(path))
            {
                html.Append("<section><h2>代码与内置资源归属</h2><p class=\"muted\">这次构建未保存详细快照。文件大小可用，无法据此精确分摊 DLL 或压缩资源容器；不会读取其他构建的日志补数。</p></section>");
                return;
            }
            JObject details = JObject.Parse(File.ReadAllText(path));
            JToken native = details["native"];
            html.Append("<section id=\"native-details\"><h2>GameAssembly.dll：代码与数据归属</h2>");
            if ((string)native?["status"] == "Verified")
            {
                html.Append("<p class=\"muted\">以本次 DLL 的 ").Append(FormatSize((long)native["binaryBytes"]))
                    .Append(" 为分母。已核对 DLL/PDB 身份，按编译单元贡献的文件区间统计代码与数据；重叠归属计入共享区，文件头、对齐与未归因部分单列。不使用源码行数推算。</p>");
                JArray groups = (JArray)native["groups"];
                AppendEvidenceSizeTable(html, groups.Take(10), (long)native["binaryBytes"], NativeLabel);
                if (groups.Count > 10)
                    html.Append("<details><summary>其余原生模块（").Append(FormatSize(groups.Skip(10).Sum(g => (long)g["bytes"])))
                        .Append("）</summary>");
                if (groups.Count > 10) { AppendEvidenceSizeTable(html, groups.Skip(10), (long)native["binaryBytes"], NativeLabel); html.Append("</details>"); }
                html.Append("<p class=\"muted\">共享泛型跨多个程序集使用，不能全部算给游戏业务代码；注册表等生成数据也不等于手写代码。</p>");
            }
            else html.Append("<p class=\"muted\">原生模块归属未取得：").Append(E((string)native?["issue"]))
                .Append("。保留 DLL 实际大小，不用中间程序集大小冒充原生占比。</p>");
            html.Append("</section><section id=\"asset-details\"><h2>内置资源：压缩前构成</h2><p class=\"muted\">来自这次 Unity BuildReport 的序列化资源记录，共 ")
                .Append((int)details["assetRecordCount"]).Append(" 条、").Append(FormatSize((long)details["assetBytes"]))
                .Append("。此表是资源记录的压缩前口径，不与上方整包图相加，也不能等同于删除后的包体节省量。</p>");
            JArray types = (JArray)details["assetTypes"];
            AppendEvidenceSizeTable(html, types.Take(8), (long)details["assetBytes"], AssetTypeLabel);
            if (types.Count > 8)
            {
                html.Append("<details><summary>其余资源类型</summary>");
                AppendEvidenceSizeTable(html, types.Skip(8), (long)details["assetBytes"], AssetTypeLabel);
                html.Append("</details>");
            }
            JArray assets = (JArray)details["assets"];
            html.Append("<h3>按资源来源汇总</h3><p class=\"muted\">与上表使用相同的压缩前分母；角色包、字体和光照单独列出。</p>");
            var sourceGroups = assets.GroupBy(a => AssetSourceGroup((string)a["path"]))
                .Select(g => new JObject { ["name"] = g.Key, ["bytes"] = g.Sum(a => (long)a["bytes"]) })
                .OrderByDescending(g => (long)g["bytes"]);
            AppendEvidenceSizeTable(html, sourceGroups, (long)details["assetBytes"], name => name);
            int overlap = assets.Count(a => a["bundleDeclaration"] != null);
            if (overlap > 0)
                html.Append("<p class=\"warn\">发现 ").Append(overlap)
                    .Append(" 个内置资源路径同时有正式 Bundle 目录声明。这是需要审查的双重发布入口，不等于已经确认可删除相同大小的文件。</p>");
            html.Append("<h3>最大的内置资源及来源</h3><p class=\"muted\">").Append(E((string)details["referenceEvidence"]))
                .Append("。引用链只追查最大的场景资产；未显示链不代表没有引用。Bundle 标记仅表示同时存在目录声明，依赖和场景 Bundle 内部未在此完全展开。</p>")
                .Append("<div class=\"scroll\"><table><thead><tr><th>资源</th><th class=\"num\">压缩前</th><th>来源／引用</th></tr></thead><tbody>");
            foreach (JToken asset in assets.Take(20))
            {
                html.Append("<tr><td><code>").Append(E((string)asset["path"])).Append("</code><br><span class=\"muted\">")
                    .Append(E((string)asset["types"])).Append("</span></td><td class=\"num\">").Append(FormatSize((long)asset["bytes"]))
                    .Append("</td><td>").Append(E(string.Join(" / ", ((JArray)asset["origins"]).Values<string>())));
                if (asset["bundleDeclaration"] != null)
                    html.Append("<br><span class=\"warn\">同时有 Bundle 声明：").Append(E((string)asset["bundleDeclaration"])).Append("</span>");
                html.Append("<details><summary>构建记录与引用链</summary><p>资源容器：")
                    .Append(E(string.Join(", ", (asset["containers"] as JArray ?? new JArray()).Values<string>())))
                    .Append("</p>");
                if (asset["referenceChain"] is JArray chain)
                    html.Append("<code>").Append(E(string.Join(" → ", chain.Values<string>()))).Append("</code>");
                else html.Append("<span class=\"muted\">本次未展示完整引用链。</span>");
                html.Append("</details>");
                html.Append("</td></tr>");
            }
            html.Append("</tbody></table></div><p class=\"muted\">完整资源和原生归属清单见页面末尾的明细快照。未自动删除、裁剪或修改任何资源引用。</p></section>");
        }

        private static void AppendEvidenceSizeTable(StringBuilder html, IEnumerable<JToken> rows, long total, Func<string, string> label)
        {
            html.Append("<table><thead><tr><th>内容</th><th class=\"num\">大小</th><th class=\"num\">占比</th></tr></thead><tbody>");
            foreach (JToken row in rows)
                html.Append("<tr><td>").Append(E(label((string)row["name"]))).Append("</td><td class=\"num\">")
                    .Append(FormatSize((long)row["bytes"])).Append("</td><td class=\"num\">")
                    .Append(Percent((long)row["bytes"], total)).Append("</td></tr>");
            html.Append("</tbody></table>");
        }
        private static string Percent(long bytes, long total) => (total == 0 ? 0 : bytes * 100d / total).ToString("F2", CultureInfo.InvariantCulture) + "%";
        private static string NativeLabel(string name) => name switch
        {
            "Shared generic code" => "共享泛型代码（跨程序集）",
            "IL2CPP registration / generated tables" => "IL2CPP 注册与生成表",
            "Headers / alignment / unattributed" => "文件头、对齐及未归因区间",
            "Shared / folded across modules" => "跨模块共享／折叠区间",
            "IL2CPP runtime" => "IL2CPP 运行库",
            "IL2CPP native dependencies" => "IL2CPP 原生依赖",
            "Native libraries / other" => "系统原生库及其他",
            _ => name
        };
        private static string AssetTypeLabel(string name) => name switch
        {
            "Texture2D" => "贴图（Texture2D）", "Mesh" => "模型网格（Mesh）", "Font" => "字体（Font）",
            "TextAsset" => "文本／二进制资源（TextAsset）", "AnimationClip" => "动画（AnimationClip）",
            "Shader" => "Shader", "MonoScript" => "脚本引用元数据（MonoScript，不是原生代码）", _ => name
        };
        private static string AssetSourceGroup(string path)
        {
            const string characters = "Assets/MxFrameworkGenerated/CharacterPackages/";
            if (path.StartsWith(characters, StringComparison.Ordinal))
                return "角色包 " + path.Substring(characters.Length).Split('/')[0];
            if (path.StartsWith("Assets/Resources/Fonts/", StringComparison.Ordinal)) return "Resources 多语言字体";
            if (path.StartsWith("Assets/Config/MxFramework/Rendering/SceneLighting/", StringComparison.Ordinal)) return "场景光照数据";
            if (path.StartsWith("Assets/Piloto Studio/", StringComparison.Ordinal)) return "Piloto 特效素材";
            if (path.StartsWith("Packages/", StringComparison.Ordinal)) return "Unity / 第三方包内置资源";
            return "其他内置资源与引用数据";
        }
        internal static string DescribePlayerFile(string path)
        {
            string name = Path.GetFileName(path).ToLowerInvariant();
            return name switch
            {
                "gameassembly.dll" => "IL2CPP 编译后的游戏、框架、托管依赖与运行库",
                "unityplayer.dll" => "Unity 原生引擎",
                "data.unity3d" => "场景与内置资源的压缩容器",
                "global-metadata.dat" => "IL2CPP 类型、方法等运行元数据",
                "il2cpp.usym" => "运行时源码行号映射；开发诊断使用",
                "unity default resources" => "Unity 引擎内置资源",
                "fmodstudio.dll" or "fmodstudiol.dll" => "FMOD 音频运行库",
                "lib_burst_generated.dll" => "Burst 编译的原生代码",
                "resonanceaudio.dll" => "空间音频插件",
                "d3d12core.dll" or "d3d12sdklayers.dll" => "随 Unity 分发的图形支持库",
                "dstorage.dll" or "dstoragecore.dll" => "Windows DirectStorage 支持库",
                "baselib.dll" => "Unity 底层平台支持库",
                "unitycrashhandler64.exe" => "崩溃报告程序",
                "charactertestperformance.exe" => "游戏启动程序",
                "boot.config" or "app.info" or "scriptingassemblies.json" or "runtimeinitializeonloads.json" => "启动配置与程序集初始化清单",
                _ => name.EndsWith("-resources.dat", StringComparison.Ordinal) ? "托管库内置资源" : "其他文件（未自动识别用途）"
            };
        }

        private static string Classify(string relativePath, string streamRelative, HashSet<string> currentBundles)
        {
            if (streamRelative == null)
                return relativePath.StartsWith(DataDirectoryName + "/", StringComparison.OrdinalIgnoreCase)
                    ? PlayerData : PlayerFiles;
            if (streamRelative.StartsWith(BundlePrefix, StringComparison.OrdinalIgnoreCase))
                return currentBundles.Contains(streamRelative) ? CurrentBundles : UnlistedBundles;
            if (streamRelative.StartsWith(ExternalBundlePrefix, StringComparison.OrdinalIgnoreCase)) return ExternalBundles;
            if (streamRelative.StartsWith(CharacterPrefix, StringComparison.OrdinalIgnoreCase)) return CharacterPackages;
            if (streamRelative.StartsWith(CatalogPrefix, StringComparison.OrdinalIgnoreCase)) return RuntimeCatalogs;
            return OtherStreaming;
        }

        private static string NormalizeManifestPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value) || value.Contains('\\'))
                throw new InvalidDataException("Unsafe manifest path: " + value);
            string[] parts = value.Split('/');
            if (parts.Any(part => part.Length == 0 || part == "." || part == ".." || part.Contains(':')))
                throw new InvalidDataException("Unsafe manifest path: " + value);
            return value;
        }

        private static Dictionary<string, string> LoadBundleGroups()
        {
            var groups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string profilePath = MxFramework.Editor.GlobalPlayerResourceBuildProfileBuilder.ProfilePath;
            if (!File.Exists(profilePath)) return groups;
            JObject profile = JObject.Parse(File.ReadAllText(profilePath));
            foreach (JToken rule in profile["bundleRules"] as JArray ?? new JArray())
            {
                string name = (string)rule["bundleName"];
                string id = (string)rule["id"];
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id))
                    groups[name] = id;
            }
            return groups;
        }

        private static string ResolveBundleGroup(string name, IReadOnlyDictionary<string, string> groups)
        {
            if (groups == null || !groups.TryGetValue(name, out string id)) return "其他";
            if (id.StartsWith("character.", StringComparison.OrdinalIgnoreCase)) return "角色";
            if (id.StartsWith("ui.", StringComparison.OrdinalIgnoreCase)) return "界面";
            if (id.StartsWith("vfx.", StringComparison.OrdinalIgnoreCase)) return "特效";
            if (id.StartsWith("scene.", StringComparison.OrdinalIgnoreCase)) return "场景";
            if (id.StartsWith("audio.", StringComparison.OrdinalIgnoreCase)) return "音频";
            if (id.StartsWith("common.models", StringComparison.OrdinalIgnoreCase)) return "公共模型";
            if (id.StartsWith("common.textures", StringComparison.OrdinalIgnoreCase)) return "公共贴图";
            return "其他";
        }

        private static Dictionary<string, string> ReadResidueStates(string path)
        {
            var states = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return states;
            foreach (string line in File.ReadAllLines(path))
            {
                if (!line.StartsWith("file: ", StringComparison.Ordinal)) continue;
                int space = line.IndexOf(' ', 6);
                int bytesAt = line.LastIndexOf(" bytes=", StringComparison.Ordinal);
                if (space < 0 || bytesAt <= space) continue;
                string state = line.Substring(6, space - 6);
                string relativePath = line.Substring(space + 1, bytesAt - space - 1);
                string bytes = line.Substring(bytesAt + 7);
                states[relativePath] = state + "|" + bytes;
            }
            return states;
        }

        private static string ResolveOwnership(string relativePath, long bytes, Dictionary<string, string> states)
        {
            if (states.TryGetValue(relativePath, out string stateAndBytes))
            {
                string[] parts = stateAndBytes.Split('|');
                if (parts.Length == 2 && long.TryParse(parts[1], NumberStyles.None,
                    CultureInfo.InvariantCulture, out long recordedBytes) && recordedBytes == bytes)
                    return parts[0];
            }
            return "未核实";
        }

        private static void AppendCategoryTable(StringBuilder html, Inventory inventory, IEnumerable<string> categories, long denominator)
        {
            html.Append("<div class=\"scroll\"><table><thead><tr><th>分类</th><th class=\"num\">文件</th><th class=\"num\">体积</th><th class=\"num\">占比</th><th>比例</th></tr></thead><tbody>");
            foreach (string category in categories)
            {
                long bytes = inventory.CategoryBytes(category);
                if (bytes == 0 && inventory.CategoryCount(category) == 0) continue;
                AppendSizeRow(html, category, inventory.CategoryCount(category), bytes, denominator);
            }
            html.Append("</tbody></table></div>");
        }

        private static void AppendBundleGroupTable(StringBuilder html, Inventory inventory)
        {
            html.Append("<div class=\"scroll\"><table><thead><tr><th>资源类</th><th class=\"num\">Bundle</th><th class=\"num\">体积</th><th class=\"num\">占比</th><th>比例</th></tr></thead><tbody>");
            foreach (IGrouping<string, FileEntry> group in inventory.Files.Where(file => file.Category == CurrentBundles)
                .GroupBy(file => file.BundleGroup).OrderByDescending(group => group.Sum(file => file.Bytes)))
                AppendSizeRow(html, group.Key, group.Count(), group.Sum(file => file.Bytes), inventory.CurrentBundleBytes);
            html.Append("</tbody></table></div>");
        }

        private static void AppendSizeRow(StringBuilder html, string name, int count, long bytes, long denominator)
        {
            double percent = denominator == 0 ? 0 : 100d * bytes / denominator;
            string displayPercent = percent.ToString("F2", CultureInfo.InvariantCulture) + "%";
            html.Append("<tr><td>").Append(E(name)).Append("</td><td class=\"num\">").Append(count)
                .Append("</td><td class=\"num\" title=\"").Append(bytes.ToString(CultureInfo.InvariantCulture))
                .Append(" B\">").Append(FormatSize(bytes)).Append("</td><td class=\"num\">")
                .Append(displayPercent).Append("</td><td><div class=\"bar\"><i style=\"width:")
                .Append(percent.ToString("F2", CultureInfo.InvariantCulture)).Append("%\"></i></div></td></tr>");
        }

        private static void AppendFileTable(StringBuilder html, IEnumerable<FileEntry> files)
        {
            html.Append("<div class=\"scroll\"><table><thead><tr><th>文件</th><th>分类 / 归属</th><th class=\"num\">体积</th></tr></thead><tbody>");
            foreach (FileEntry file in files)
                html.Append("<tr><td><code>").Append(E(file.RelativePath)).Append("</code></td><td>")
                    .Append(E(file.Ownership == null ? file.Category : file.Category + " · " + file.Ownership))
                    .Append("</td><td class=\"num\" title=\"").Append(file.Bytes.ToString(CultureInfo.InvariantCulture))
                    .Append(" B\">").Append(FormatSize(file.Bytes)).Append("</td></tr>");
            html.Append("</tbody></table></div>");
        }

        private static void AppendEvidenceLink(StringBuilder html, string label, string target, string reportPath)
        {
            if (string.IsNullOrWhiteSpace(target) || !File.Exists(target)) return;
            Uri reportDirectory = new Uri(Path.GetDirectoryName(Path.GetFullPath(reportPath)).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar);
            string href = reportDirectory.MakeRelativeUri(new Uri(Path.GetFullPath(target))).ToString();
            html.Append("<li><a href=\"").Append(E(href)).Append("\">").Append(E(label)).Append("</a></li>");
        }

        private static string FormatSize(long bytes) =>
            (bytes / 1048576d).ToString("F2", CultureInfo.InvariantCulture) + " MiB";

        private static string E(string value) => WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
