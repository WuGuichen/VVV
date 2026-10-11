using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.CharacterApplication;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestActionBrowserEntry
    {
        public int ActionId;
        public string Name, LibraryId, SourcePath, ContentHash;
        [JsonConverter(typeof(StringEnumConverter))] public CharacterActionCategory Category;
        public int? DurationFrames;
        public bool WeaponTypeIndependent;
        public string[] RequiredWeaponTypes = Array.Empty<string>();
        public string[] WeaponTypes = Array.Empty<string>();
        public string[] Issues = Array.Empty<string>();
        [JsonIgnore] internal string TypeLabel => CharacterTestActionBrowserCatalog.TypeLabel(Category);
    }

    internal sealed class CharacterTestActionWeaponType
    {
        public string Id, Label, ContentKind, EquipmentCategory, IconResourceKey;
        [JsonIgnore] internal string GroupLabel => ContentKind == "equipment" ? "武器 / " + Label : "用途 / " + Label;
    }

    internal sealed class CharacterTestActionBrowserData
    {
        public string Schema = "mx.character-test.action-browser.v1";
        public string Fingerprint;
        public CharacterTestActionWeaponType[] WeaponTypes = Array.Empty<CharacterTestActionWeaponType>();
        public CharacterTestActionBrowserEntry[] Actions = Array.Empty<CharacterTestActionBrowserEntry>();
        public Dictionary<string, string> SourceHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        public string[] Issues = Array.Empty<string>();
    }

    /// <summary>Read-only authoring projection. Classification never edits action execution data.</summary>
    internal static class CharacterTestActionBrowserCatalog
    {
        internal const string WeaponLibraryPath = "WeaponSource/global_weapon_library.json";
        internal const string ClassificationPath = "CharacterActionSource/action_classification.json";
        internal const string GeneratedPath = "Assets/MxFrameworkGenerated/CharacterActions/action_browser_catalog.json";
        internal const string PlayerCatalogPath = "MxFramework/CharacterTest/action_browser_catalog.json";
        private static CharacterTestActionBrowserData _cached;
        private static string _signature;
        private static long _lastCheck;

        internal static CharacterTestActionBrowserData Get(bool refresh = false)
        {
            long now = DateTime.UtcNow.Ticks;
            if (!refresh && _cached != null && now - _lastCheck < TimeSpan.TicksPerSecond) return _cached;
            _lastCheck = now;
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
#if UNITY_EDITOR
            var files = SourceFiles(root).Append(Path.Combine(root, ClassificationPath)).Append(Path.Combine(root, WeaponLibraryPath)).ToArray();
            string signature = string.Join("|", files.Select(p => p + ":" + (File.Exists(p) ? File.GetLastWriteTimeUtc(p).Ticks + ":" + new FileInfo(p).Length : "missing")));
            if (refresh || _cached == null || _signature != signature)
            { _cached = Build(root); _signature = signature; }
#else
            if (refresh || _cached == null)
                _cached = File.Exists(Path.Combine(UnityEngine.Application.streamingAssetsPath, PlayerCatalogPath))
                    ? JsonConvert.DeserializeObject<CharacterTestActionBrowserData>(File.ReadAllText(Path.Combine(UnityEngine.Application.streamingAssetsPath, PlayerCatalogPath)))
                    : new CharacterTestActionBrowserData { Issues = new[] { "动作浏览目录尚未生成。" } };
#endif
            return _cached;
        }

        internal static string[] SourceFiles(string root)
        {
            var files = new List<string>();
            string common = Path.Combine(root, "CharacterActionSource/Libraries");
            string packages = Path.Combine(root, "Authoring/CharacterPackages");
            if (Directory.Exists(common))
                files.AddRange(Directory.GetDirectories(common).Select(p => Path.Combine(p, "character_actions.json")).Where(File.Exists));
            if (Directory.Exists(packages))
                files.AddRange(Directory.GetDirectories(packages).Select(p => Path.Combine(p, "config/character_actions.json")).Where(File.Exists));
            return files.OrderBy(p => p, StringComparer.Ordinal).ToArray();
        }

        internal static CharacterTestActionBrowserData Build(string root)
        {
            root = Path.GetFullPath(root);
            var data = new CharacterTestActionBrowserData();
            var issues = new List<string>();
            var actions = new Dictionary<int, CharacterTestActionBrowserEntry>();
            var raw = new Dictionary<int, JObject>();
            foreach (string path in SourceFiles(root))
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                try
                {
                    byte[] bytes = File.ReadAllBytes(path); data.SourceHashes[relative] = Hash(bytes);
                    var document = JObject.Parse(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
                    if ((string)document["schemaVersion"] != "0.3.0" || document["actions"] is not JArray array)
                        throw new ArgumentException("作者动作库必须是 schema 0.3.0 且包含 actions。");
                    string library = relative.StartsWith("CharacterActionSource/", StringComparison.Ordinal)
                        ? new DirectoryInfo(Path.GetDirectoryName(path)).Name
                        : new DirectoryInfo(Path.GetDirectoryName(Path.GetDirectoryName(path))).Name.Replace("character-", "");
                    foreach (JObject action in array)
                    {
                        int id = (int?)action["actionId"] ?? 0;
                        if (id <= 0) { issues.Add(relative + "：无效动作 ID。"); continue; }
                        if (actions.TryGetValue(id, out var duplicate))
                        {
                            string issue = "重复动作 ID " + id + "：" + duplicate.SourcePath + " / " + relative;
                            issues.Add(issue); duplicate.Issues = duplicate.Issues.Append(issue).ToArray(); continue;
                        }
                        var entry = new CharacterTestActionBrowserEntry { ActionId = id,
                            Name = (string)action["displayName"] ?? "", LibraryId = library, SourcePath = relative,
                            DurationFrames = (int?)action["durationFrames"],
                            WeaponTypeIndependent = (action["requirements"] as JArray ?? new JArray()).OfType<JObject>()
                                .Any(r => (string)r["kind"] == "WeaponTypeIndependent"),
                            RequiredWeaponTypes = (action["requirements"] as JArray ?? new JArray()).OfType<JObject>()
                                .Where(r => (string)r["kind"] == "WeaponTypeRequired").Select(r => (string)r["stableId"]).ToArray(),
                            ContentHash = Hash(Encoding.UTF8.GetBytes(action.ToString(Formatting.None))) };
                        if (!Enum.TryParse((string)action["category"], out entry.Category) || !Enum.IsDefined(typeof(CharacterActionCategory), entry.Category))
                            entry.Issues = new[] { "未知动作类型：" + (string)action["category"] };
                        if (string.IsNullOrWhiteSpace(entry.Name)) entry.Issues = entry.Issues.Append("动作名称为空。").ToArray();
                        actions.Add(id, entry); raw.Add(id, action);
                    }
                }
                catch (Exception error) when (error is IOException || error is JsonException || error is ArgumentException || error is InvalidCastException || error is FormatException)
                { issues.Add(relative + "：" + error.Message); }
            }
            if (actions.Count == 0) issues.Add("没有发现作者动作数据。");
            try
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(root, ClassificationPath));
                data.SourceHashes[ClassificationPath] = Hash(bytes);
                var document = JObject.Parse(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
                if ((string)document["schema"] != "mx.character-action.classification.v1")
                    throw new ArgumentException("武器分类清单 schema 不受支持。");
                var metadataTypes = document["weaponTypes"]?.ToObject<CharacterTestActionWeaponType[]>() ?? Array.Empty<CharacterTestActionWeaponType>();
                string weaponSourcePath = Path.Combine(root, WeaponLibraryPath);
                if (File.Exists(weaponSourcePath))
                {
                    byte[] weaponBytes = File.ReadAllBytes(weaponSourcePath); data.SourceHashes[WeaponLibraryPath] = Hash(weaponBytes);
                    var weaponSource = JObject.Parse(Encoding.UTF8.GetString(weaponBytes).TrimStart('\uFEFF'));
                    var definitions = weaponSource["weaponTypes"]?.ToObject<WeaponTypeDefinition[]>();
                    _ = new WeaponTypeCatalog(definitions);
                    if (metadataTypes.Any(t => t.ContentKind != "body" && t.ContentKind != "function"))
                        throw new ArgumentException("ClassificationPhysicalTypeMustUseWeaponLibrary");
                    data.WeaponTypes = definitions.Select(d => new CharacterTestActionWeaponType {
                        Id = d.TypeId, Label = d.DisplayName, ContentKind = "equipment", EquipmentCategory = d.EquipmentCategory,
                        IconResourceKey = d.IconResourceKey }).Concat(metadataTypes).ToArray();
                }
                else data.WeaponTypes = metadataTypes; // Standalone framework fixture; formal publication requires the weapon source.
                var types = new HashSet<string>(StringComparer.Ordinal);
                foreach (var type in data.WeaponTypes)
                    if (string.IsNullOrWhiteSpace(type.Id) || string.IsNullOrWhiteSpace(type.Label) || !types.Add(type.Id))
                        issues.Add("武器分类标识为空或重复。");
                var assigned = new HashSet<int>();
                foreach (JObject item in document["entries"] as JArray ?? new JArray())
                {
                    int id = (int?)item["actionId"] ?? 0;
                    if (!assigned.Add(id)) { issues.Add("重复的动作分类：" + id); continue; }
                    if (!actions.TryGetValue(id, out var entry)) { issues.Add("分类引用的动作不存在：" + id); continue; }
                    var weapons = item["weaponTypes"]?.ToObject<string[]>() ?? Array.Empty<string>();
                    var entryIssues = new List<string>();
                    if (weapons.Distinct().Count() != weapons.Length || weapons.Any(w => !types.Contains(w)))
                        entryIssues.Add("武器分类包含未知或重复标签。");
                    string evidenceTag = (string)item["evidenceTag"];
                    string classificationSource = (string)item["classificationSource"];
                    if (!string.IsNullOrEmpty(classificationSource) && classificationSource != "manual" && classificationSource != "authorTag")
                        entryIssues.Add("未知分类来源：" + classificationSource);
                    if (classificationSource != "manual" && (string.IsNullOrWhiteSpace(evidenceTag) || raw[id]["tags"] is not JArray tags
                        || !tags.Values<string>().Contains(evidenceTag, StringComparer.Ordinal)))
                        entryIssues.Add("分类依据的作者标签已失效，请重新核对。");
                    entry.WeaponTypes = entryIssues.Count == 0 ? weapons : Array.Empty<string>();
                    entry.Issues = entry.Issues.Concat(entryIssues).ToArray();
                }
            }
            catch (Exception error) when (error is IOException || error is JsonException || error is ArgumentException)
            { issues.Add("武器分类清单：" + error.Message); }
            data.Actions = actions.Values.OrderBy(a => a.LibraryId, StringComparer.Ordinal).ThenBy(a => a.ActionId).ToArray();
            data.Issues = issues.Concat(data.Actions.SelectMany(a => a.Issues.Select(i => a.ActionId + "：" + i))).ToArray();
            data.Fingerprint = Hash(Encoding.UTF8.GetBytes(string.Join("|", data.SourceHashes.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ":" + p.Value))));
            return data;
        }

        internal static CharacterTestActionBrowserEntry[] Filter(CharacterTestActionBrowserData data, string query, string weapon, string type)
        {
            string text = (query ?? "").Trim();
            return data.Actions.Where(a => (string.IsNullOrEmpty(weapon) || weapon == "unclassified" && a.WeaponTypes.Length == 0 || a.WeaponTypes.Contains(weapon))
                && (string.IsNullOrEmpty(type) || a.Category.ToString() == type)
                && (text.Length == 0 || (a.Name + " " + a.ActionId + " " + a.LibraryId + " " + a.TypeLabel + " " + WeaponLabels(data, a))
                    .IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
        }

        internal static string WeaponLabels(CharacterTestActionBrowserData data, CharacterTestActionBrowserEntry entry)
            => entry.WeaponTypes.Length == 0 ? "未分类" : string.Join("、", entry.WeaponTypes.Select(id => data.WeaponTypes.FirstOrDefault(w => w.Id == id)?.Label ?? id));

        internal static string TypeLabel(CharacterActionCategory type) => type switch {
            CharacterActionCategory.BasicAttack => "攻击", CharacterActionCategory.Skill => "技能", CharacterActionCategory.Dodge => "闪避",
            CharacterActionCategory.Guard => "防御", CharacterActionCategory.Movement => "移动", CharacterActionCategory.Jump => "跳跃",
            CharacterActionCategory.Reaction => "受击反应", CharacterActionCategory.Idle => "待机", CharacterActionCategory.Interaction => "交互",
            CharacterActionCategory.PassiveOverlay => "被动叠加", _ => "未分类" };

        internal static string Hash(byte[] bytes)
        { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    }
}
