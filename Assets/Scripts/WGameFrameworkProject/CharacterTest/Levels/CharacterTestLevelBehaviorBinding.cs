using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest
{
    // Optional actor extension of characterTest.characters v2. Omitted when cleared,
    // so existing documents retain their canonical representation and content hash.
    [Serializable]
    public sealed class CharacterTestLevelBehaviorBinding
    {
        public string GroupId = string.Empty;
        public int GroupRevision;
        public long GroupContentHash;
        public List<CharacterTestLevelCardDependency> Cards = new List<CharacterTestLevelCardDependency>();
        public List<CharacterTestLevelActionBinding> Actions = new List<CharacterTestLevelActionBinding>();
        public CharacterTestLevelBehaviorBinding Clone() => JsonConvert.DeserializeObject<CharacterTestLevelBehaviorBinding>(JsonConvert.SerializeObject(this));
    }

    [Serializable]
    public sealed class CharacterTestLevelCardDependency
    {
        public string EntryId = string.Empty, CardId = string.Empty;
        public int Revision;
        public long SemanticHash, ContentHash;
        public string InputFingerprint = string.Empty;
    }

    [Serializable]
    public sealed class CharacterTestLevelActionBinding
    {
        public string EntryId = string.Empty, SlotPath = string.Empty;
        public int ActionId;
        public string DependencyFingerprint = string.Empty;
    }

    internal static class CharacterTestLevelBehaviorCompiler
    {
        internal static string[] Slots(CharacterTestTacticalPublishedSource source)
        {
            if (source == null) return Array.Empty<string>();
            if (source.IsLegacy) return source.CloneLegacy().Slots.Where(s => !s.IsFunctional).Select(s => s.SlotId).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            var definitions = source.CloneDefinitions().ToDictionary(d => d.DefinitionId, StringComparer.Ordinal);
            return CharacterTestTacticalCompositionEdits.Nodes(source.CloneComposition().Root).Where(n => n.Kind == "Reference")
                .SelectMany(n => definitions[n.DefinitionId].Source.Slots.Where(s => !s.IsFunctional).Select(s => n.NodeId + "/" + s.SlotId))
                .OrderBy(s => s, StringComparer.Ordinal).ToArray();
        }

        // Explicit editor operation. Selecting/upgrading a group invalidates previous
        // dependency pins; publishing is the only operation that fills action pins.
        internal static CharacterTestLevelBehaviorBinding Select(CharacterTestRuntimeAiPlannerBehaviorGroupRevision group)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            return new CharacterTestLevelBehaviorBinding { GroupId = group.StableGroupId, GroupRevision = group.Revision,
                GroupContentHash = group.ContentHash, Cards = group.Cards.Select(c => new CharacterTestLevelCardDependency {
                    EntryId = c.EntryId, CardId = c.StableCardId, Revision = c.CardRevision,
                    SemanticHash = c.SemanticHash, ContentHash = c.ContentHash }).ToList() };
        }

        internal static CharacterTestRuntimeAiPlannerBehaviorGroupRevision Resolve(string levelId,
            CharacterTestSceneCharacterDefinition actor, CharacterTestRuntimeAiPlannerProjectCatalogState catalog)
        {
            var binding = actor.BehaviorBinding;
            if (binding == null) return null;
            string prefix = levelId + "/" + actor.StableId + "/";
            void Reject(string issue) => throw new InvalidOperationException(prefix + issue);
            if (catalog?.Success != true) Reject("catalog: " + catalog?.Diagnostic);
            if (!catalog.GroupCatalog.TryGetExact(binding.GroupId, binding.GroupRevision, out var group)) Reject("missing-group: " + binding.GroupId + "@" + binding.GroupRevision);
            if (group.ContentHash != binding.GroupContentHash) Reject("group-hash: " + binding.GroupId);
            if (binding.Cards == null || binding.Cards.Count != group.Cards.Count) Reject("card-dependencies: incomplete");
            if (binding.Cards.GroupBy(c => c.EntryId, StringComparer.Ordinal).Any(g => g.Count() != 1)) Reject("card-dependencies: duplicate entry");
            foreach (var card in group.Cards)
            {
                var pin = binding.Cards.SingleOrDefault(c => c.EntryId == card.EntryId);
                if (pin == null || pin.CardId != card.StableCardId || pin.Revision != card.CardRevision
                    || pin.SemanticHash != card.SemanticHash || pin.ContentHash != card.ContentHash) Reject(card.EntryId + "/card-pin");
                if (!catalog.CardCatalog.TryGetExact(card.StableCardId, card.CardRevision, out var published)
                    || !published.MatchesPinnedHash(card.SemanticHash, card.ContentHash)) Reject(card.EntryId + "/card-hash");
                if (!published.IsCompatibleWith(CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion)) Reject(card.EntryId + "/runtime-contract");
            }
            var required = group.Cards.Where(c => c.Enabled).SelectMany(c => {
                catalog.CardCatalog.TryGetExact(c.StableCardId, c.CardRevision, out var published);
                return Slots(published.TacticalSource).Select(s => c.EntryId + "\n" + s);
            }).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            var actions = binding.Actions ?? new List<CharacterTestLevelActionBinding>();
            var actual = actions.Select(a => a.EntryId + "\n" + a.SlotPath).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(required)) Reject("slots: missing, duplicate or unknown entry/slot");
            if (actions.Any(a => a.ActionId <= 0)) Reject("slots: action required");
            if (actor.ControlOwner == CharacterTestCharacterControlOwner.AiControlled && !string.IsNullOrWhiteSpace(actor.RuntimeAiPlannerProfilePath)) Reject("profile: ambiguous formal group and legacy profile");
            return group;
        }

        // Cold authoring/candidate preparation, never called in Tick. No inventory,
        // DraftWorkspace, Lab TestCards, implicit action or latest-version resolution.
        internal static CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest Compile(string levelId,
            CharacterTestSceneCharacterDefinition actor, int slot, CharacterTestRuntimeAiPlannerProjectCatalogState catalog,
            CharacterTestCharacterActionRuntime actions, CharacterTestCharacterEntity entity,
            CharacterTestCharacterPhysicsRuntime physics, bool publishPins, CharacterTestCharacterRuntimeModule runtime)
        {
            var group = Resolve(levelId, actor, catalog);
            if (group == null) throw new InvalidOperationException("Formal group required.");
            string prefix = levelId + "/" + actor.StableId + "/";
            var programs = new List<CharacterTestRuntimeAiLabCompiledLogicProgram>();
            foreach (var card in group.Cards.Where(c => c.Enabled).OrderBy(c => c.Order))
            {
                catalog.CardCatalog.TryGetExact(card.StableCardId, card.CardRevision, out var published);
                if (published.TacticalSource == null)
                {
                    var legacy = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(published.CloneLogic(),
                        new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, runtime.ActionRuntime.Registry));
                    if (!legacy.Success || legacy.Program == null) throw new InvalidOperationException(prefix + card.EntryId + "/legacy compile");
                    string dependency = legacy.Program.SemanticHash.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    foreach (int actionId in legacy.Program.Stages.Where(s => s.CharacterActionId.IsValid).Select(s => s.CharacterActionId.Value).Distinct().OrderBy(id => id))
                    {
                        if (!new TacticalCard.CharacterTacticalBoundCardInputs(runtime, entity.Id.Value,
                            new Dictionary<string,int> { ["fixed"] = actionId }).WithRequirements(CharacterTestTacticalInputRequirements.PrimaryAction).TryPrepare(out var fixedInput, out var fixedIssue))
                            throw new InvalidOperationException(prefix + card.EntryId + "/fixed action: " + fixedIssue);
                        dependency += "/" + actionId + "/" + fixedInput.DependencyFingerprint;
                    }
                    var legacyPin = actor.BehaviorBinding.Cards.Single(c => c.EntryId == card.EntryId);
                    dependency = CharacterTestLevelCatalog.Hash(dependency);
                    if (publishPins) legacyPin.InputFingerprint = dependency;
                    else if (legacyPin.InputFingerprint != dependency) throw new InvalidOperationException(prefix + card.EntryId + "/legacy action dependencies drift");
                    programs.Add(legacy.Program.WithSelectionDefinition(CharacterTestBehaviorCardSelectionDefinition.FromPublished(published)));
                    continue;
                }
                var source = published.TacticalSource;
                var bound = new Dictionary<string, CharacterTestTacticalCardInputs>(StringComparer.Ordinal);
                foreach (var binding in actor.BehaviorBinding.Actions.Where(a => a.EntryId == card.EntryId))
                {
                    var inputSource = new TacticalCard.CharacterTacticalBoundCardInputs(runtime, entity.Id.Value,
                        new Dictionary<string, int> { [binding.SlotPath] = binding.ActionId }).WithRequirements(source.InputRequirements);
                    if (!inputSource.TryPrepare(out var prepared, out var issue))
                        throw new InvalidOperationException(prefix + card.EntryId + "/" + binding.SlotPath + ": " + issue);
                    var input = prepared.SlotInputs(binding.SlotPath);
                    if (publishPins) binding.DependencyFingerprint = input.DependencyFingerprint;
                    else if (string.IsNullOrEmpty(binding.DependencyFingerprint) || binding.DependencyFingerprint != input.DependencyFingerprint)
                        throw new InvalidOperationException(prefix + card.EntryId + "/" + binding.SlotPath + ": action dependency drift; publish explicitly");
                    bound.Add(binding.SlotPath, input);
                }
                CharacterTestTacticalCardInputs inputs;
                if (bound.Count == 0)
                {
                    if (!new TacticalCard.CharacterTacticalBoundCardInputs(runtime, entity.Id.Value,
                        new Dictionary<string, int>()).WithRequirements(source.InputRequirements).UseFunctionalResolvers().TryPrepare(out inputs, out var issue))
                        throw new InvalidOperationException(prefix + card.EntryId + ": " + issue);
                }
                else inputs = CharacterTestTacticalCardInputs.BindSlots(actor.CharacterId, bound);
                if (!source.TryCompile(published.StableCardId, published.Metadata.DisplayName, published.Revision, inputs, out var program, out var problem))
                    throw new InvalidOperationException(prefix + card.EntryId + ": " + problem);
                var cardPin = actor.BehaviorBinding.Cards.Single(c => c.EntryId == card.EntryId);
                if (publishPins) cardPin.InputFingerprint = inputs.DependencyFingerprint;
                else if (cardPin.InputFingerprint != inputs.DependencyFingerprint) throw new InvalidOperationException(prefix + card.EntryId + "/functional/action dependencies drift");
                programs.Add(program.WithSelectionDefinition(CharacterTestBehaviorCardSelectionDefinition.FromPublished(published)));
            }
            return new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest(slot, group.Profile.Clone(), programs, group.ContentHash);
        }
    }
}
