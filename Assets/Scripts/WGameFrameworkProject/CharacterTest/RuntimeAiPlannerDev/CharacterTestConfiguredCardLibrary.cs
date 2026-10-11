using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestConfiguredCard
    {
        public string Schema = "mx.character-test.configured-card.v1";
        public string Id, Name, OwnerCharacter = "", BaseKey, BaseRevision, BaseFingerprint;
        public int Revision;
        public JObject Source;
        public CharacterTestConfiguredCardMetadata Metadata = new();
        public Dictionary<string, int> Actions = new(StringComparer.Ordinal);
        [JsonIgnore] public string FileHash;
        [JsonIgnore] public bool Shared => string.IsNullOrEmpty(OwnerCharacter);
        internal CharacterTestConfiguredCard Clone() => JsonConvert.DeserializeObject<CharacterTestConfiguredCard>(JsonConvert.SerializeObject(this));
    }

    internal sealed class CharacterTestConfiguredCardMetadata
    {
        public string Category = "Tactical", IconKey = "", ShortDescription = "", UseSummary = "", RiskSummary = "", TagsText = "";
        public int Cost;
        public float CooldownSeconds;
        public CharacterTestRuntimeAiPlannerBehaviorCapability[] RequiredCapabilities = Array.Empty<CharacterTestRuntimeAiPlannerBehaviorCapability>();
        public CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] ResponsibilityKinds = Array.Empty<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind>();
        internal static CharacterTestConfiguredCardMetadata From(CharacterTestRuntimeAiLabBehaviorCardDraft card) => new() {
            Category = card.Category, IconKey = card.IconKey, Cost = card.Cost, CooldownSeconds = card.CooldownSeconds, ShortDescription = card.ShortDescription,
            UseSummary = card.UseSummary, RiskSummary = card.RiskSummary, TagsText = card.TagsText,
            RequiredCapabilities = card.RequiredCapabilities.ToArray(), ResponsibilityKinds = card.ResponsibilityKinds.ToArray()
        };
        internal void Apply(CharacterTestRuntimeAiLabBehaviorCardDraft card)
        {
            card.Category = Category; card.IconKey = IconKey; card.Cost = Cost; card.CooldownSeconds = CooldownSeconds; card.ShortDescription = ShortDescription;
            card.UseSummary = UseSummary; card.RiskSummary = RiskSummary; card.TagsText = TagsText;
            card.RequiredCapabilities = RequiredCapabilities.ToArray(); card.ResponsibilityKinds = ResponsibilityKinds.ToArray();
        }
    }

    internal sealed class CharacterTestConfiguredCardRecovery
    {
        public CharacterTestConfiguredCard Card;
        public string ExpectedHash;
        public int ExpectedRevision;
    }

    /// <summary>Project test-card records and role-local recovery. Does not touch player ownership or base definitions.</summary>
    internal sealed class CharacterTestConfiguredCardLibrary
    {
        internal static void FillMissingActions(CharacterTestConfiguredCard card, Func<bool, int> chooseDefault)
        {
            var snapshot = CharacterTestTacticalPublishedSource.Read(card.Source);
            void Fill(CharacterTestTacticalAuthoringSource source, string prefix)
            {
                bool spatial = (CharacterTestTacticalDependencies.Analyze(source) & CharacterTestTacticalInputRequirements.SpatialSummary) != 0;
                foreach (var slot in source.Slots.Where(s => !s.IsFunctional))
                {
                    string path = prefix + slot.SlotId;
                    if (card.Actions.TryGetValue(path, out int existing) && existing > 0) continue;
                    int selected = chooseDefault(spatial);
                    if (selected <= 0) throw new InvalidOperationException("当前角色没有可填入的默认动作，请先配置动作资源或准备摘要。未创建空配置卡。");
                    card.Actions[path] = selected;
                }
            }
            if (snapshot.IsLegacy) Fill(snapshot.CloneLegacy(), "");
            else
            {
                var definitions = snapshot.CloneDefinitions().ToDictionary(d => d.DefinitionId, StringComparer.Ordinal);
                foreach (var node in CharacterTestTacticalCompositionEdits.Nodes(snapshot.CloneComposition().Root).Where(n => n.Kind == "Reference"))
                    Fill(definitions[node.DefinitionId].Source, node.NodeId + "/");
            }
        }

        private readonly string _root, _recovery;
        internal string Issue { get; private set; } = "";
        internal CharacterTestConfiguredCardLibrary(string root = "Authoring/RuntimeAiLab/TestCards",
            string recovery = "UserSettings/RuntimeAiLab/TestCards")
        { _root = Path.GetFullPath(root); _recovery = Path.GetFullPath(recovery); }

        private static string Segment(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value == "." || value == ".."
                || value.Any(c => !char.IsLetterOrDigit(c) && c != '.' && c != '-' && c != '_'))
                throw new InvalidDataException("测试卡路径身份无效。");
            return value;
        }
        private static string Id(string value)
        { if (!Guid.TryParseExact(value, "N", out _)) throw new InvalidDataException("测试卡 ID 无效。"); return value; }
        private string Folder(string owner) => string.IsNullOrEmpty(owner) ? Path.Combine(_root, "Shared") : Path.Combine(_root, "Characters", Segment(owner));
        private string FilePath(CharacterTestConfiguredCard card) => Path.Combine(Folder(card.OwnerCharacter), Id(card.Id) + ".json");
        private string RecoveryPath(string character, string id) => Path.Combine(_recovery, Segment(character), Id(id) + ".json");
        private static void Validate(CharacterTestConfiguredCard card)
        {
            if (card == null || card.Schema != "mx.character-test.configured-card.v1" || string.IsNullOrWhiteSpace(card.Name)
                || card.Name.Length > 128 || card.Revision < 0 || card.Source == null || card.Actions == null || card.Metadata == null
                || card.Metadata.Cost < 0 || card.Metadata.RequiredCapabilities == null || card.Metadata.ResponsibilityKinds == null
                || card.Actions.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value < 0)) throw new InvalidDataException("测试卡数据无效。");
            Id(card.Id); if (!card.Shared) Segment(card.OwnerCharacter);
            CharacterTestTacticalPublishedSource.Read(card.Source);
        }

        internal CharacterTestConfiguredCard[] List(string character)
        {
            Issue = ""; var cards = new List<CharacterTestConfiguredCard>();
            foreach (var folder in new[] { Folder(character), Folder("") }.Distinct())
            {
                if (!Directory.Exists(folder)) continue;
                foreach (string path in Directory.GetFiles(folder, "*.json"))
                    try
                    {
                        string json = File.ReadAllText(path); var card = JsonConvert.DeserializeObject<CharacterTestConfiguredCard>(json); Validate(card);
                        if (!string.Equals(FilePath(card), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("库归属与路径不一致。");
                        card.FileHash = CharacterTestActionBrowserCatalog.Hash(Encoding.UTF8.GetBytes(json)); cards.Add(card);
                    }
                    catch (Exception error) when (error is IOException || error is JsonException || error is ArgumentException)
                    { Issue += Path.GetFileName(path) + "：" + error.Message + "\n"; }
            }
            if (cards.Select(c => c.Id).Distinct().Count() != cards.Count) { Issue += "存在重复测试卡 ID。"; return Array.Empty<CharacterTestConfiguredCard>(); }
            return cards.OrderBy(c => c.Shared).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Id, StringComparer.Ordinal).ToArray();
        }

        internal bool Save(CharacterTestConfiguredCard card, int expectedRevision, string expectedHash, out string issue)
        {
            issue = "";
            try
            {
                Validate(card); string path = FilePath(card);
                string old = File.Exists(path) ? File.ReadAllText(path) : null;
                if (expectedRevision == 0 ? old != null : old == null
                    || JsonConvert.DeserializeObject<CharacterTestConfiguredCard>(old).Revision != expectedRevision
                    || CharacterTestActionBrowserCatalog.Hash(Encoding.UTF8.GetBytes(old)) != expectedHash)
                { issue = "卡库版本已变化或卡已删除，保留当前草稿。请重新载入或另存。"; return false; }
                var next = card.Clone(); next.Name = next.Name.Trim(); next.Revision = checked(expectedRevision + 1);
                string json = JsonConvert.SerializeObject(next, Formatting.Indented) + "\n";
                AtomicWrite(path, json, old);
                card.Revision = next.Revision; card.FileHash = CharacterTestActionBrowserCatalog.Hash(Encoding.UTF8.GetBytes(json));
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException || error is ArgumentException)
            { issue = "测试卡保存失败：" + error.Message; return false; }
        }

        internal bool Delete(CharacterTestConfiguredCard card, out string issue)
        {
            issue = "";
            try
            {
                string path = FilePath(card);
                if (!File.Exists(path) || CharacterTestActionBrowserCatalog.Hash(Encoding.UTF8.GetBytes(File.ReadAllText(path))) != card.FileHash)
                { issue = "卡库版本已变化，请刷新后再删除。"; return false; }
                File.Delete(path); return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { issue = error.Message; return false; }
        }

        internal void Recover(string character, CharacterTestConfiguredCard card, int revision, string hash)
        {
            Validate(card);
            AtomicWrite(RecoveryPath(character, card.Id), JsonConvert.SerializeObject(new CharacterTestConfiguredCardRecovery {
                Card = card, ExpectedRevision = revision, ExpectedHash = hash }, Formatting.Indented), checkPrevious: false);
        }
        internal CharacterTestConfiguredCardRecovery ReadRecovery(string character, string id)
        {
            string path = RecoveryPath(character, id);
            if (!File.Exists(path)) return null;
            var value = JsonConvert.DeserializeObject<CharacterTestConfiguredCardRecovery>(File.ReadAllText(path));
            Validate(value?.Card);
            if (value.Card.Id != id || !value.Card.Shared && value.Card.OwnerCharacter != character) throw new InvalidDataException("恢复草稿归属不匹配。");
            return value;
        }
        internal CharacterTestConfiguredCard[] NewRecoveries(string character)
        {
            string folder = Path.Combine(_recovery, Segment(character));
            if (!Directory.Exists(folder)) return Array.Empty<CharacterTestConfiguredCard>();
            var result = new List<CharacterTestConfiguredCard>();
            foreach (string path in Directory.GetFiles(folder, "*.json"))
                try { var recovery = ReadRecovery(character, Path.GetFileNameWithoutExtension(path)); if (recovery.ExpectedRevision == 0) result.Add(recovery.Card); }
                catch (Exception error) when (error is IOException || error is JsonException || error is ArgumentException) { Issue += error.Message; }
            return result.ToArray();
        }
        internal void ClearRecovery(string character, string id) { string path = RecoveryPath(character, id); if (File.Exists(path)) File.Delete(path); }

        private static void AtomicWrite(string path, string text, string previous = null, bool checkPrevious = true)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)); string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, text, new UTF8Encoding(false));
                if (checkPrevious && (File.Exists(path) ? File.ReadAllText(path) : null) != previous) throw new IOException("保存期间文件已被其它编辑修改。");
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
