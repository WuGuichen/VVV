using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Reversible trial owner; never writes the formal AI Lab workspace or catalog.</summary>
    internal sealed class CharacterTestTacticalDslTrialWorkspace
    {
        internal sealed class SharedChange
        {
            internal int Version;
            internal string DefinitionId, Json;
            internal string[] Impact;
        }
        private readonly List<string> _undo = new List<string>(), _redo = new List<string>();
        private List<CharacterTestTacticalDslSource> _cards;
        internal int Version { get; private set; }
        internal IReadOnlyList<CharacterTestTacticalDslSource> Cards => _cards.Select(Clone).ToArray();

        internal CharacterTestTacticalDslTrialWorkspace()
        {
            var first = CharacterTestTacticalDslTrialTemplates.Create();
            var second = Clone(first); second.CardId = "trial.second"; second.Name = "另一张引用卡";
            second.Definitions = first.Definitions;
            _cards = new List<CharacterTestTacticalDslSource> { first, second };
        }
        private static CharacterTestTacticalDslSource Clone(CharacterTestTacticalDslSource source)
            => CharacterTestTacticalDslJson.Read(CharacterTestTacticalDslJson.Write(source));
        internal CharacterTestTacticalDslSource Card(string id) => Clone(_cards.Single(c => c.CardId == id));
        internal static IEnumerable<CharacterTestTacticalDslNode> Nodes(CharacterTestTacticalDslNode root)
        {
            yield return root;
            foreach (var child in root.Children ?? new List<CharacterTestTacticalDslNode>())
                foreach (var node in Nodes(child)) yield return node;
        }
        internal SharedChange PrepareShared(string id, string json)
        {
            _cards[0].Definitions.Single(d => d.DefinitionId == id);
            var source = CharacterTestTacticalAuthoringJson.ReadSharedTrial(json);
            return new SharedChange { Version = Version, DefinitionId = id, Json = CharacterTestTacticalAuthoringJson.Write(source),
                Impact = _cards.Select(c => new { c.Name, Count = Nodes(c.Root).Count(n => n.DefinitionId == id) })
                    .Where(x => x.Count > 0).Select(x => x.Name + "：" + x.Count + " 处引用").ToArray() };
        }
        internal void ApplyShared(SharedChange change)
        {
            if (change == null) throw new ArgumentNullException(nameof(change));
            var source = CharacterTestTacticalAuthoringJson.ReadSharedTrial(change.Json);
            Transaction(change.Version, () => {
                var definition = _cards[0].Definitions.Single(d => d.DefinitionId == change.DefinitionId);
                definition.Source = source; definition.Revision = checked(definition.Revision + 1);
            });
        }
        internal void DuplicateDefinitionAndReplace(int version, string cardId, string referenceId, string newDefinitionId)
        {
            if (string.IsNullOrWhiteSpace(newDefinitionId) || newDefinitionId.Length > 128)
                throw new ArgumentException("新封装身份无效。");
            Transaction(version, () => {
                var card = _cards.Single(c => c.CardId == cardId);
                var node = Nodes(card.Root).Single(n => n.NodeId == referenceId && n.Kind == "Reference");
                if (card.Definitions.Any(d => d.DefinitionId == newDefinitionId)) throw new ArgumentException("封装身份已存在。");
                var source = card.Definitions.Single(d => d.DefinitionId == node.DefinitionId).Source.Clone();
                card.Definitions.Add(new CharacterTestTacticalSharedDefinition { DefinitionId = newDefinitionId, Source = source });
                node.DefinitionId = newDefinitionId;
            });
        }
        internal void DuplicateCard(int version, string cardId, string newCardId)
        {
            if (string.IsNullOrWhiteSpace(newCardId) || newCardId.Length > 128) throw new ArgumentException("新卡身份无效。");
            Transaction(version, () => {
                if (_cards.Any(c => c.CardId == newCardId)) throw new ArgumentException("卡身份已存在。");
                var copy = Clone(_cards.Single(c => c.CardId == cardId));
                copy.CardId = newCardId; copy.Name += "（副本）"; copy.Definitions = _cards[0].Definitions; _cards.Add(copy);
            });
        }
        internal void SetEndCondition(int version, string cardId, string nodeId, CharacterTestRuntimeAiLabBehaviorRecipeConditionNode condition)
            => Transaction(version, () => Nodes(_cards.Single(c => c.CardId == cardId).Root).Single(n => n.NodeId == nodeId).EndCondition = condition?.Clone());
        internal void MoveSharedAttack(int version, string definitionId, int frames)
        {
            var source = _cards[0].Definitions.Single(d => d.DefinitionId == definitionId).Source.Clone();
            CharacterTestTacticalAuthoringEdits.Move(source, source.EntryTimelineId,
                source.Timelines[0].Events.Select(e => e.EventId).ToArray(), frames);
            var change = PrepareShared(definitionId, CharacterTestTacticalAuthoringJson.Write(source));
            change.Version = version; ApplyShared(change);
        }
        private void Transaction(int version, Action action)
        {
            if (version != Version) throw new InvalidOperationException("试用版本已变化，请重新查看影响。");
            string before = Capture();
            try
            {
                action();
                string after = Capture();
                Restore(after); // strict round trip also detaches caller-owned trees.
                if (before == after) return;
                _undo.Add(before); _redo.Clear(); Version++;
            }
            catch { Restore(before); throw; }
        }
        internal bool Undo(bool redo = false)
        {
            var from = redo ? _redo : _undo; var to = redo ? _undo : _redo;
            if (from.Count == 0) return false;
            to.Add(Capture()); string snapshot = from[from.Count - 1]; from.RemoveAt(from.Count - 1);
            Restore(snapshot); Version++; return true;
        }
        internal string Capture()
        {
            var cards = new JArray();
            foreach (var card in _cards)
            { var token = JObject.Parse(CharacterTestTacticalDslJson.Write(card)); token.Remove("definitions"); cards.Add(token); }
            return new JObject { ["schema"] = "mx.character-test.tactical-workspace.trial.v1", ["cards"] = cards,
                ["definitions"] = JObject.Parse(CharacterTestTacticalDslJson.Write(_cards[0]))["definitions"] }.ToString(Formatting.Indented);
        }
        private void Restore(string json)
        {
            var snapshot = JObject.Parse(json); var cards = new List<CharacterTestTacticalDslSource>();
            foreach (JObject token in snapshot["cards"])
            { token["definitions"] = snapshot["definitions"].DeepClone(); cards.Add(CharacterTestTacticalDslJson.Read(token.ToString())); }
            foreach (var card in cards) card.Definitions = cards[0].Definitions;
            _cards = cards;
        }
    }
}
