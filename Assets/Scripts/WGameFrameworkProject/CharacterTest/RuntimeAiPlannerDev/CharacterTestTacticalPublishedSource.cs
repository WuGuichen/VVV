using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Immutable published authoring source with exact shared dependencies, independent of actor binding.</summary>
    internal sealed class CharacterTestTacticalPublishedSource
    {
        internal const string Schema = "mx.character-test.published-tactical-source.v1";
        private readonly string _json;
        private readonly TacticalCard.TacticalPointMoveRules _pointMove;
        internal TacticalCard.TacticalPositionRecallRules PositionRecall { get; }
        internal TacticalCard.InteractionPreparationPayload InteractionPreparation { get; }
        private CharacterTestTacticalPublishedSource(JObject document)
        {
            _json = document.ToString(Formatting.None);
            _pointMove = document["legacy"] == null ? null
                : CharacterTestTacticalAuthoringJson.Read(document["legacy"].ToString()).ExecutionInput?.Freeze();
            PositionRecall = document["legacy"] == null ? null : CharacterTestTacticalAuthoringJson.Read(document["legacy"].ToString()).PositionRecall?.Freeze();
            InteractionPreparation = document["legacy"] == null ? null : CharacterTestTacticalAuthoringJson.Read(document["legacy"].ToString()).InteractionPreparation?.Freeze();
            var hash = new RuntimeContentHashAccumulator(); hash.AddStringStable("tactical.published.source", _json);
            SemanticHash = hash.ToHash();
        }
        internal long SemanticHash { get; }
        internal JObject ToJson() => JObject.Parse(_json);
        internal bool IsLegacy => ToJson()["legacy"] != null;
        internal CharacterTestTacticalAuthoringSource CloneLegacy() => IsLegacy
            ? CharacterTestTacticalAuthoringJson.Read(ToJson()["legacy"].ToString()) : null;
        internal CharacterTestTacticalCompositionSource CloneComposition() => IsLegacy ? null
            : CharacterTestTacticalCompositionJson.Read(ToJson()["composition"].ToString());
        internal CharacterTestTacticalSharedDefinition[] CloneDefinitions() => IsLegacy ? Array.Empty<CharacterTestTacticalSharedDefinition>()
            : ((JArray)ToJson()["definitions"]).Select(t => CharacterTestTacticalCompositionJson.ReadShared(t.ToString())).ToArray();
        internal TacticalCard.TacticalPointMoveRules PointMove => _pointMove;
        internal TacticalCard.TacticalExecutionInputKind ExecutionInputKind =>
            PointMove == null ? TacticalCard.TacticalExecutionInputKind.None : TacticalCard.TacticalExecutionInputKind.WorldPoint;
        internal bool HasPlayerActionSlot => IsLegacy
            ? CloneLegacy().Slots.Any(slot => !slot.IsFunctional)
            : CloneDefinitions().Any(definition => definition.Source.Slots.Any(slot =>
                !slot.IsFunctional));
        internal bool HasPlayerBindingSlots => HasPlayerActionSlot;

        /// <summary>Cold deployment check. Keep exact published semantics; never rewrite an old timed recipe.</summary>
        internal bool TryValidatePlayerExecution(out string issue)
        {
            var sources = IsLegacy ? new[] { CloneLegacy() }
                : CloneDefinitions().Select(definition => definition.Source).ToArray();
            foreach (var source in sources)
                foreach (var timeline in source.Timelines)
                    foreach (var block in timeline.EventBlocks)
                    {
                        if (block.Completion != "Timed") continue;
                        var events = timeline.Events.Where(evt => block.EventIds.Contains(evt.EventId)).ToArray();
                        if (!events.Any(evt => evt.Operation.Kind == "CharacterAction")
                            || !events.Any(evt => evt.Operation.Kind == "WaitForCondition")) continue;
                        issue = "此版本无法保证接敌后完整执行动作。可从首页新游戏使用新版初始卡，或更换为已持有的兼容卡；当前进度与旧卡保留，不会自动升级。";
                        return false;
                    }
            issue = string.Empty; return true;
        }

        internal CharacterTestTacticalInputRequirements InputRequirements
        {
            get
            {
                if (IsLegacy) return CharacterTestTacticalDependencies.Analyze(CloneLegacy());
                var composition = CloneComposition();
                return CharacterTestTacticalDependencies.Analyze(new CharacterTestTacticalDslSource {
                    Root = composition.Root, ActivationConditionRoot = composition.ActivationConditionRoot,
                    Definitions = CloneDefinitions().ToList() });
            }
        }

        internal static CharacterTestTacticalPublishedSource Capture(CharacterTestTacticalFrozenSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var json = new JObject { ["schema"] = Schema };
            if (source.IsLegacy) json["legacy"] = JObject.Parse(CharacterTestTacticalAuthoringJson.Write(source.CloneLegacySource()));
            else
            {
                var document = source.CloneDocument();
                json["composition"] = JObject.Parse(CharacterTestTacticalCompositionJson.Write(new CharacterTestTacticalCompositionSource {
                    Root = document.Root, ActivationConditionRoot = document.ActivationConditionRoot }));
                json["definitions"] = new JArray(document.Definitions.OrderBy(d => d.DefinitionId, StringComparer.Ordinal)
                    .Select(d => JObject.Parse(CharacterTestTacticalCompositionJson.WriteShared(d))));
            }
            return Read(json);
        }
        internal static CharacterTestTacticalPublishedSource CaptureDraft(CharacterTestRuntimeAiLabDraftWorkspace workspace,
            CharacterTestRuntimeAiLabBehaviorCardDraft card)
        {
            var json = new JObject { ["schema"] = Schema };
            if (card.HasTacticalSource) json["legacy"] = JObject.Parse(card.TacticalSourceJson);
            else
            {
                var document = workspace.ResolveComposition(card);
                var used = new HashSet<string>(Nodes(document.Root).Where(n => n.Kind == "Reference").Select(n => n.DefinitionId), StringComparer.Ordinal);
                json["composition"] = JObject.Parse(card.CompositionSourceJson);
                json["definitions"] = new JArray(document.Definitions.Where(d => used.Contains(d.DefinitionId)).OrderBy(d => d.DefinitionId, StringComparer.Ordinal)
                    .Select(d => JObject.Parse(CharacterTestTacticalCompositionJson.WriteShared(d))));
            }
            return Read(json);
        }

        internal static CharacterTestTacticalPublishedSource Read(JObject json)
        {
            if (json == null || (string)json["schema"] != Schema) throw new JsonSerializationException("Unsupported published tactical source.");
            foreach (var field in json.Properties())
                if (field.Name != "schema" && field.Name != "legacy" && field.Name != "composition" && field.Name != "definitions")
                    throw new JsonSerializationException("Unknown published tactical field: " + field.Name);
            var canonical = new JObject { ["schema"] = Schema };
            if (json["legacy"] != null)
            {
                if (json["composition"] != null || json["definitions"] != null) throw new JsonSerializationException("Published tactical source has two authorities.");
                canonical["legacy"] = JObject.Parse(CharacterTestTacticalAuthoringJson.Write(CharacterTestTacticalAuthoringJson.Read(json["legacy"].ToString())));
            }
            else
            {
                var composition = CharacterTestTacticalCompositionJson.Read(json["composition"]?.ToString());
                if (composition.Root?.Kind != "Sequence" || !(json["definitions"] is JArray entries))
                    throw new JsonSerializationException("Published composition requires a sequence and exact dependencies.");
                var definitions = entries.Select(t => CharacterTestTacticalCompositionJson.ReadShared(t.ToString())).OrderBy(d => d.DefinitionId, StringComparer.Ordinal).ToArray();
                if (definitions.Any(d => d.Source.ExecutionInput != null || d.Source.PositionRecall != null || d.Source.InteractionPreparation != null))
                    throw new JsonSerializationException("WorldPoint input cannot be owned by a shared definition.");
                var required = new HashSet<string>(Nodes(composition.Root).Where(n => n.Kind == "Reference").Select(n => n.DefinitionId), StringComparer.Ordinal);
                if (definitions.Select(d => d.DefinitionId).Distinct(StringComparer.Ordinal).Count() != definitions.Length
                    || !required.SetEquals(definitions.Select(d => d.DefinitionId)))
                    throw new JsonSerializationException("Published shared dependencies are missing, duplicated or unused.");
                canonical["composition"] = JObject.Parse(CharacterTestTacticalCompositionJson.Write(composition));
                canonical["definitions"] = new JArray(definitions.Select(d => JObject.Parse(CharacterTestTacticalCompositionJson.WriteShared(d))));
            }
            return new CharacterTestTacticalPublishedSource(canonical);
        }

        internal bool TryCompile(string stableId, string name, int revision, CharacterTestTacticalCardInputs inputs,
            out CharacterTestRuntimeAiLabCompiledLogicProgram program, out string issue)
        {
            program = null; issue = string.Empty;
            if (InteractionPreparation != null) { issue = "execution:InteractionPreparationAuthorityRequired"; return false; }
            if (PositionRecall != null) { issue = "execution:PositionRecallAuthorityRequired"; return false; }
            if (PointMove != null) { issue = "execution-input:PointRequired"; return false; }
            if (inputs == null) { issue = "当前角色动作摘要尚未准备，请显式重建摘要后重试。"; return false; }
            if (IsLegacy)
            {
                var legacy = CloneLegacy();
                if (!CharacterTestTacticalAuthoringCompiler.TryCompile(legacy, inputs, out var compiled, out var errors))
                { issue = string.Join("; ", errors); return false; }
                var card = new CharacterTestRuntimeAiLabBehaviorCardDraft(stableId, stableId, name, "Published", null);
                card.CurrentRevision = revision;
                card.SetTacticalSource(legacy);
                program = compiled.Program.WithTacticalSource(CharacterTestTacticalFrozenSource.CaptureLegacy(null, card, inputs, compiled));
                return true;
            }
            var source = CloneComposition();
            var document = new CharacterTestTacticalDslSource { CardId = stableId, Name = name, Root = source.Root,
                ActivationConditionRoot = source.ActivationConditionRoot, Definitions = CloneDefinitions().ToList() };
            if (!CharacterTestTacticalDslCompiler.TryCompile(document, inputs, out var result, out issue)) return false;
            program = result.Program.WithTacticalSource(CharacterTestTacticalFrozenSource.CapturePublished(stableId, revision, document, inputs, result));
            return true;
        }

        /// <summary>Selection-only contract; never start this unbound program in an execution evaluator.</summary>
        internal bool TryCompileAutomaticContract(string stableId, string name, int revision, int characterId,
            out CharacterTestRuntimeAiLabCompiledLogicProgram program, out string issue)
        {
            program = null; issue = "execution-input:PointRequired";
            if (PointMove == null || characterId <= 0 || revision <= 0 || string.IsNullOrEmpty(stableId)) return false;
            var source = CloneLegacy();
            var inputs = CharacterTestTacticalCardInputs.Unbound(characterId);
            if (!CharacterTestTacticalAuthoringCompiler.TryCompile(source, inputs, out var compiled, out var diagnostics))
            { issue = string.Join("; ", diagnostics); return false; }
            var card = new CharacterTestRuntimeAiLabBehaviorCardDraft(stableId, stableId, name, "Published", null);
            card.CurrentRevision = revision; card.SetTacticalSource(source);
            program = compiled.Program.WithTacticalSource(CharacterTestTacticalFrozenSource.CaptureLegacy(null, card, inputs, compiled));
            issue = string.Empty; return true;
        }

        private static IEnumerable<CharacterTestTacticalDslNode> Nodes(CharacterTestTacticalDslNode node)
        {
            yield return node;
            foreach (var child in node.Children ?? new List<CharacterTestTacticalDslNode>())
                foreach (var nested in Nodes(child)) yield return nested;
        }
    }
}
