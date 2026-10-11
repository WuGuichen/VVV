using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiPlannerLocalPlayerDeckService
    {
        internal CharacterTacticalPlayerService TacticalInventory => CharacterTacticalServices.Player;
        internal void ImportLegacyTacticalProgress(CharacterTacticalPlayerService player)
        {
            var cards = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (System.IO.File.Exists(_filePath) && string.IsNullOrEmpty(LoadWarning))
                foreach (var target in _targets)
                {
                    if (!_participant.CurrentSnapshot.TryGetDeck(target.CharacterStableId, out var deck)) continue;
                    foreach (var character in CharacterTestPlayableCharacterCatalog.Discover())
                        if (character.CharacterId == target.CharacterId && character.TacticalDefaults != null)
                        {
                            var ids = new string[deck.StableCardIds.Count];
                            for (int i = 0; i < ids.Length; i++) ids[i] = deck.StableCardIds[i];
                            cards[character.TacticalDefaults.CharacterStableId] = ids;
                        }
                }
            player.ImportLegacyCards(cards);
        }

        // Legacy installed-deck objects are projections, never a second writer of tactical player progress.
        internal bool SyncTacticalInventory(out string issue)
        {
            issue = string.Empty;
            var player = TacticalInventory;
            if (player == null) { issue = "角色战术清单尚未加载。"; return false; }
            var project = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load(_references);
            if (!project.Success) { issue = project.Diagnostic; return false; }
            _catalog = project.CardCatalog;
            var unlocked = new HashSet<string>(StringComparer.Ordinal);
            var decks = new List<CharacterTestRuntimeAiPlannerPersistedDeck>();
            foreach (var target in _targets)
            {
                string stable = null;
                foreach (var character in CharacterTestPlayableCharacterCatalog.Discover())
                    if (character.CharacterId == target.CharacterId) { stable = character.TacticalDefaults?.CharacterStableId; break; }
                var state = player.Read(stable);
                var cards = new List<string>();
                if (state != null)
                    foreach (var reference in state.Cards)
                    {
                        if (!_catalog.TryGetExact(reference.CardId, reference.Revision, out var card)
                            || card.ContentHash.ToString(CultureInfo.InvariantCulture) != reference.ContentHash) continue;
                        unlocked.Add(reference.CardId);
                        if (!player.TryGetBindings(stable, reference.CardId, out _, out _)) continue;
                        // The compatibility InstalledDeck resolver chooses latest; refuse drift instead of silently upgrading.
                        if (!_catalog.TryResolveLatestCompatible(reference.CardId,
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion, out var latest)
                            || latest.Revision != reference.Revision) continue;
                        cards.Add(reference.CardId);
                    }
                decks.Add(new CharacterTestRuntimeAiPlannerPersistedDeck(target.CharacterStableId, cards.ToArray(), target.CharacterId));
            }
            var collection = CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(unlocked);
            if (!collection.Success) { issue = FirstDiagnostic(collection.Diagnostics); return false; }
            var snapshot = CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.ValidateAndCreate(collection.Collection, decks);
            if (!snapshot.Success) { issue = FirstDiagnostic(snapshot.Diagnostics); return false; }
            _participant = new CharacterTestRuntimeAiPlannerDeckPersistenceParticipant(snapshot.Snapshot, _catalog,
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion);
            return TryBuildWorkbench(Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(), out issue);
        }
    }
}
