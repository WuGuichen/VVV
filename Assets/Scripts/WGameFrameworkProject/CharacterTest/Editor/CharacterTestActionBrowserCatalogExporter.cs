using System;
using System.IO;
using System.Linq;
using System.Text;
using MxFramework.Game.CharacterTest;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static class CharacterTestActionBrowserCatalogExporter
    {
        [InitializeOnLoadMethod]
        private static void RegisterClassificationSave() => CharacterTestActionClassificationEditor.AfterSave = Rebuild;

        [MenuItem("MxFramework/CharacterTest/Rebuild Action Browser Catalog")]
        internal static void Rebuild()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var data = CharacterTestActionBrowserCatalog.Build(root);
            var weaponTypes = CharacterTestWeaponTypePublication.Build(root, data); // All validation precedes any write.
            var report = new StringBuilder("# 动作目录整理报告\n\n")
                .AppendLine("由 Rebuild Action Browser Catalog 生成；作者源为唯一正文权威，生成副本不重复计数。")
                .AppendLine().AppendLine("- 动作数：" + data.Actions.Length)
                .AppendLine("- 数据问题：" + data.Issues.Length)
                .AppendLine("- 目录指纹：" + data.Fingerprint).AppendLine()
                .AppendLine("## 分类统计").AppendLine();
            foreach (var group in data.Actions.GroupBy(a => a.LibraryId))
                report.AppendLine("- 动作库 " + group.Key + "：" + group.Count());
            foreach (var group in data.Actions.GroupBy(a => a.Category))
                report.AppendLine("- " + CharacterTestActionBrowserCatalog.TypeLabel(group.Key) + "：" + group.Count());
            foreach (var type in data.WeaponTypes)
                report.AppendLine("- 武器 / " + type.Label + "：" + data.Actions.Count(a => a.WeaponTypes.Contains(type.Id)));
            report.AppendLine("- 武器 / 未分类：" + data.Actions.Count(a => a.WeaponTypes.Length == 0)).AppendLine()
                .AppendLine("武器归属来自作者标签及动作库中的人工分类；人工分类只修改独立浏览元数据。持剑四连击另有 Common/source/SwordAnimationPack/Combo01/README.md 的来源说明。")
                .AppendLine("例如 Physical Shield 的 shield 标签也可能描述技能效果，未据此推断为盾牌动作；未标注武器不等于通用。")
                .AppendLine().AppendLine("## 动作清单").AppendLine()
                .AppendLine("| ID | 名称 | 类型 | 武器归属 | 库 | 帧数 | 作者源 |")
                .AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
            foreach (var action in data.Actions)
                report.AppendLine("| " + action.ActionId + " | " + action.Name.Replace("|", "/") + " | " + action.TypeLabel + " | "
                    + CharacterTestActionBrowserCatalog.WeaponLabels(data, action) + " | " + action.LibraryId + " | "
                    + (action.DurationFrames?.ToString() ?? "未定义") + " | " + action.SourcePath + " |");
            report.AppendLine().AppendLine("## 数据问题").AppendLine();
            if (data.Issues.Length == 0) report.AppendLine("无重复动作 ID、失效分类引用、未知分类或失效依据。");
            else foreach (string issue in data.Issues) report.AppendLine("- " + issue);
            report.AppendLine().AppendLine("## 作者源校验值").AppendLine();
            foreach (var pair in data.SourceHashes) report.AppendLine("- " + pair.Key + "：" + pair.Value);
            string reportPath = Path.Combine(root, "Docs/Tasks/CHARACTER_ACTION_BROWSER_CATALOG_REPORT.md");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));
            if (data.Issues.Length > 0) throw new InvalidOperationException("动作目录未生成，请修复报告中的数据问题。");
            string output = Path.Combine(root, CharacterTestActionBrowserCatalog.GeneratedPath);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonConvert.SerializeObject(data, Formatting.Indented) + "\n", new UTF8Encoding(false));
            string playerOutput = Path.Combine(Application.streamingAssetsPath, CharacterTestActionBrowserCatalog.PlayerCatalogPath);
            Directory.CreateDirectory(Path.GetDirectoryName(playerOutput));
            File.WriteAllText(playerOutput, JsonConvert.SerializeObject(data, Formatting.Indented) + "\n", new UTF8Encoding(false));
            foreach (string typeOutput in new[] { Path.Combine(root, CharacterTestWeaponTypePublication.GeneratedPath),
                Path.Combine(Application.streamingAssetsPath, CharacterTestWeaponTypePublication.PlayerPath) })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(typeOutput));
                File.WriteAllText(typeOutput, weaponTypes.ToString(Formatting.Indented) + "\n", new UTF8Encoding(false));
            }
            CharacterTestWeaponTypeAdmission.Invalidate();
            AssetDatabase.Refresh();
            Debug.Log("动作浏览目录已生成：" + data.Actions.Length + " 条。");
        }
    }
}
