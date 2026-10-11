using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Edits only global browsing metadata, preserving all action authoring files.</summary>
    internal static class CharacterTestActionClassificationEditor
    {
        internal static Action AfterSave;
        internal static bool CanEdit => UnityEngine.Application.isEditor;

        internal static bool TrySave(int actionId, string[] weapons, string fingerprint, out string issue)
        {
            issue = string.Empty;
            if (!CanEdit) { issue = "请在 Unity 编辑器中的动作库修改分类。"; return false; }
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            if (!TrySaveAt(root, actionId, weapons, fingerprint, out bool changed, out issue)) return false;
            CharacterTestActionBrowserCatalog.Get(refresh: true);
            if (changed && AfterSave != null)
            {
                try { AfterSave(); }
                catch (Exception error) { issue = "分类已保存，但目录报告更新失败：" + error.Message; }
            }
            return true;
        }

        internal static bool TrySaveAt(string root, int actionId, string[] weapons, string fingerprint,
            out bool changed, out string issue)
        {
            changed = false; issue = string.Empty;
            string temporary = null;
            try
            {
                var data = CharacterTestActionBrowserCatalog.Build(root);
                if (data.Fingerprint != fingerprint)
                { issue = "动作或分类已被其他编辑更新，请取消后刷新目录，再重新分类。"; return false; }
                if (data.Issues.Length > 0)
                { issue = "目录数据存在问题，请先修复：" + data.Issues[0]; return false; }
                var entry = data.Actions.SingleOrDefault(a => a.ActionId == actionId);
                if (entry == null) { issue = "动作已不存在，请刷新目录。"; return false; }
                var ids = (weapons ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
                if (ids.Any(id => !data.WeaponTypes.Any(w => w.Id == id)))
                { issue = "选择中含有未知武器分类，请刷新目录。"; return false; }
                if (entry.WeaponTypes.OrderBy(w => w, StringComparer.Ordinal).SequenceEqual(ids.OrderBy(w => w, StringComparer.Ordinal))) return true;

                string path = Path.Combine(root, CharacterTestActionBrowserCatalog.ClassificationPath);
                byte[] original = File.ReadAllBytes(path);
                if (CharacterTestActionBrowserCatalog.Hash(original) != data.SourceHashes[CharacterTestActionBrowserCatalog.ClassificationPath])
                { issue = "分类文件已变化，请取消后刷新目录。"; return false; }
                var document = JObject.Parse(Encoding.UTF8.GetString(original).TrimStart('\uFEFF'));
                var entries = document["entries"] as JArray ?? new JArray();
                document["entries"] = entries;
                var item = entries.OfType<JObject>().SingleOrDefault(e => (int?)e["actionId"] == actionId);
                if (item == null) { item = new JObject { ["actionId"] = actionId }; entries.Add(item); }
                item["weaponTypes"] = new JArray(data.WeaponTypes.Where(w => ids.Contains(w.Id)).Select(w => w.Id));
                item["classificationSource"] = "manual";
                item.Remove("evidenceTag");
                item["note"] = "动作库界面人工分类；仅用于浏览。";
                temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temporary, document.ToString(Formatting.Indented) + "\n", new UTF8Encoding(false));
                // Refuse a stale dialog instead of overwriting a newer authoring file.
                if (CharacterTestActionBrowserCatalog.Hash(File.ReadAllBytes(path)) != data.SourceHashes[CharacterTestActionBrowserCatalog.ClassificationPath])
                { issue = "分类文件已变化，请取消后刷新目录。"; return false; }
                File.Replace(temporary, path, null);
                changed = true;
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException || error is ArgumentException)
            { issue = "分类保存失败：" + error.Message; return false; }
            finally { if (temporary != null && File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
