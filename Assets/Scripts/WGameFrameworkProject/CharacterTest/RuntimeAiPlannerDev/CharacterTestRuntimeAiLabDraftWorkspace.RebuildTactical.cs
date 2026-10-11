using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabDraftWorkspace
    {
        /// <summary>Explicit destructive content reset; never called by loading or normal editing.</summary>
        internal int RebuildTacticalDrafts(CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog published = null)
        {
            if (IsReadOnly) throw new InvalidOperationException("只读工作区不能重建。");
            var oldCards = _cards.Values.Where(c => c.HasAuthoredSource).ToArray();
            if (oldCards.Length == 0) return 0;
            var replacement = new List<(CharacterTestRuntimeAiLabBehaviorCardDraft card, CharacterTestTacticalSharedDefinition definition)>();
            foreach (var old in oldCards)
            {
                var source = CharacterTestTacticalAuthoringTemplates.CreateVisualCard(old.DisplayName);
                var oldActivation = old.HasCompositionSource ? old.CloneCompositionSource().ActivationConditionRoot : old.CloneTacticalSource().ActivationConditionRoot;
                // Preserve the card's explicit use condition (e.g. low health), rebuild all internal events.
                var activation = oldActivation?.Clone() ?? source.ActivationConditionRoot;
                source.ActivationConditionRoot = null;
                string definitionId = "tactical.visual." + old.PublishedStableId;
                var definition = new CharacterTestTacticalSharedDefinition { DefinitionId = definitionId, Source = source };
                var root = CharacterTestTacticalCompositionEdits.NewNode("Sequence");
                root.Children.Add(CharacterTestTacticalCompositionEdits.NewNode("Reference", definitionId));
                var card = new CharacterTestRuntimeAiLabBehaviorCardDraft(old.CardDraftId, old.PublishedStableId, old.DisplayName, old.Category, null) {
                    IconKey = old.IconKey, Cost = old.Cost, TagsText = old.TagsText,
                    RequiredCapabilities = old.RequiredCapabilities, ResponsibilityKinds = old.ResponsibilityKinds,
                    SourcePublishedRevision = 0, LastCompileStatus = "VisualTimelineRebuilt" };
                card.SetCompositionSource(new CharacterTestTacticalCompositionSource { Root = root, ActivationConditionRoot = activation });
                var baseline = published?.Cards.FirstOrDefault(c => c.StableCardId == card.PublishedStableId);
                if (baseline != null)
                {
                    card.SourcePublishedRevision = baseline.Revision;
                    card.SourcePublishedSemanticHash = baseline.SemanticHash; card.SourcePublishedContentHash = baseline.ContentHash;
                }
                CharacterTestTacticalAuthoringJson.Read(CharacterTestTacticalAuthoringJson.Write(source));
                replacement.Add((card, definition));
            }
            foreach (var old in oldCards) _cards.Remove(old.CardDraftId);
            _tacticalDefinitions.Clear();
            foreach (var item in replacement) { _cards.Add(item.card.CardDraftId, item.card); _tacticalDefinitions.Add(item.definition.DefinitionId, item.definition); }
            // Card envelope IDs keep group references connected to the newly constructed content.
            // Clear history so undo cannot resurrect the deleted source corpus.
            _undo.Clear(); _redo.Clear(); TouchWorkspace(); LastExplicitSaveRevision = CurrentRevision;
            return replacement.Count;
        }
    }
}
