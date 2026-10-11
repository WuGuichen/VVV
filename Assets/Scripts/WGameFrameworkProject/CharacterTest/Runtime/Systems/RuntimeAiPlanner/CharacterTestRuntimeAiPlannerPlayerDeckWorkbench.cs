using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal interface ICharacterTestRuntimeAiPlannerPlayerDeckPersistenceSink
    {
        bool TryPersist(
            CharacterTestRuntimeAiPlannerPersistedDeck deck,
            out CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic diagnostic);
    }

    /// <summary>
    /// Low-frequency player-facing deck authoring controller. Semantic edits are delegated to the
    /// Installed Deck rules; unresolved persisted references remain visible but never become runtime decks.
    /// </summary>
    internal sealed partial class CharacterTestRuntimeAiPlannerPlayerDeckWorkbench
    {
        private readonly CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog _catalog;
        private readonly CharacterTestRuntimeAiPlannerUnlockedCardCollection _unlockedCards;
        private readonly CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput _diagnosticsInput;
        private readonly string _characterStableId;
        private readonly int _characterNumericId;
        private readonly int _runtimeContractVersion;
        private readonly ICharacterTestRuntimeAiPlannerPlayerDeckPersistenceSink _persistenceSink;
        private string[] _stableCardIds;
        private CharacterTestRuntimeAiPlannerInstalledDeck _deck;
        private CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] _deckDiagnostics;
        private CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView _view;
        private bool _isSessionLocked;

        private CharacterTestRuntimeAiPlannerPlayerDeckWorkbench(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlockedCards,
            string characterStableId,
            int characterNumericId,
            int runtimeContractVersion,
            string[] stableCardIds,
            CharacterTestRuntimeAiPlannerInstalledDeck deck,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput diagnosticsInput,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] deckDiagnostics,
            ICharacterTestRuntimeAiPlannerPlayerDeckPersistenceSink persistenceSink)
        {
            _catalog = catalog;
            _unlockedCards = unlockedCards;
            _characterStableId = characterStableId;
            _characterNumericId = characterNumericId;
            _runtimeContractVersion = runtimeContractVersion;
            _stableCardIds = stableCardIds ?? Array.Empty<string>();
            _deck = deck;
            _diagnosticsInput = diagnosticsInput;
            _persistenceSink = persistenceSink;
            _deckDiagnostics = deckDiagnostics ?? Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>();
            _view = BuildView(Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>());
        }

        public CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView View => _view;
        public CharacterTestRuntimeAiPlannerInstalledDeck Deck => _deck;
        public CharacterTestRuntimeAiPlannerPersistedDeck PersistedDeck =>
            _deck != null
                ? CreatePersistedDeck(_deck.Cards)
                : new CharacterTestRuntimeAiPlannerPersistedDeck(_characterStableId, _stableCardIds, _characterNumericId);

        /// <remarks>LowFreqAlloc: player deck screen setup only.</remarks>
        public static CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult ValidateAndCreate(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlockedCards,
            CharacterTestRuntimeAiPlannerInstalledDeck deck,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput diagnosticsInput = null,
            ICharacterTestRuntimeAiPlannerPlayerDeckPersistenceSink persistenceSink = null)
        {
            var diagnostics = new List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>();
            ValidateCommon(catalog, unlockedCards, diagnostics);
            if (deck == null)
                AddError(diagnostics, "PLAYER_DECK_INSTALLED_DECK_MISSING", "$.deck", "Installed deck is required.");
            if (catalog != null && deck != null && catalog.CatalogHash != deck.CatalogHash)
                AddError(diagnostics, "PLAYER_DECK_CATALOG_MISMATCH", "$.deck.catalogHash", "Installed deck was not built from the active published catalog.");
            if (unlockedCards != null && deck != null)
                ValidateOwnedReferences(unlockedCards, deck.Cards, diagnostics);
            if (diagnostics.Count > 0)
                return FailedCreate(diagnostics);

            string[] stableCardIds = CopyCardIds(deck.Cards);
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult rebuilt = BuildDeck(
                catalog,
                deck.CharacterStableId,
                deck.CharacterNumericId,
                deck.RuntimeContractVersion,
                stableCardIds,
                diagnosticsInput);
            if (!rebuilt.Success || rebuilt.Deck.DeckHash != deck.DeckHash)
            {
                AddDiagnostics(diagnostics, rebuilt.Diagnostics);
                if (rebuilt.Success)
                {
                    AddError(
                        diagnostics,
                        "PLAYER_DECK_HASH_MISMATCH",
                        "$.deck.deckHash",
                        "Installed deck identity does not match the active catalog and player deck rules.");
                }
                return FailedCreate(diagnostics);
            }

            return Create(
                catalog,
                unlockedCards,
                deck.CharacterStableId,
                deck.CharacterNumericId,
                deck.RuntimeContractVersion,
                stableCardIds,
                rebuilt.Deck,
                diagnosticsInput,
                CopyDiagnostics(rebuilt.Diagnostics),
                persistenceSink,
                null);
        }

        /// <remarks>LowFreqAlloc: profile restore and unresolved-deck repair entry only.</remarks>
        public static CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult ValidateAndCreate(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlockedCards,
            CharacterTestRuntimeAiPlannerPersistedDeck persistedDeck,
            int runtimeContractVersion,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput diagnosticsInput = null,
            ICharacterTestRuntimeAiPlannerPlayerDeckPersistenceSink persistenceSink = null,
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> additionalDeckDiagnostics = null)
        {
            var diagnostics = new List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>();
            ValidateCommon(catalog, unlockedCards, diagnostics);
            if (persistedDeck == null)
                AddError(diagnostics, "PLAYER_DECK_PERSISTED_DECK_MISSING", "$.persistedDeck", "Persisted deck is required.");
            if (runtimeContractVersion <= 0)
                AddError(diagnostics, "PLAYER_DECK_RUNTIME_VERSION_INVALID", "$.runtimeContractVersion", "Runtime contract version must be positive.");
            if (diagnostics.Count > 0)
                return FailedCreate(diagnostics);

            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult persistence =
                CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.ValidateAndCreate(
                    unlockedCards,
                    new[] { persistedDeck });
            if (!persistence.Success)
            {
                AddDiagnostics(diagnostics, persistence.Diagnostics);
                return FailedCreate(diagnostics);
            }

            string[] stableCardIds = CopyCardIds(persistedDeck.StableCardIds);
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult rebuilt = BuildDeck(
                catalog,
                persistedDeck.CharacterStableId,
                persistedDeck.CharacterNumericId,
                runtimeContractVersion,
                stableCardIds,
                diagnosticsInput);
            CharacterTestRuntimeAiPlannerInstalledDeck resolvedDeck = rebuilt.Success ? rebuilt.Deck : null;
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] deckDiagnostics = rebuilt.Success
                ? CopyDiagnostics(rebuilt.Diagnostics)
                : BuildUnresolvedDiagnostics(catalog, runtimeContractVersion, stableCardIds, rebuilt.Diagnostics);
            return Create(
                catalog,
                unlockedCards,
                persistedDeck.CharacterStableId,
                persistedDeck.CharacterNumericId,
                runtimeContractVersion,
                stableCardIds,
                resolvedDeck,
                diagnosticsInput,
                deckDiagnostics,
                persistenceSink,
                additionalDeckDiagnostics);
        }

        public CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult TryInstall(string stableCardId)
        {
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult guard = ValidateInstall(stableCardId);
            if (guard != null)
                return guard;

            if (_deck != null)
            {
                return Apply(CharacterTestRuntimeAiPlannerInstalledDeckRules.TryInstall(
                    _deck,
                    stableCardId,
                    _catalog,
                    _diagnosticsInput));
            }

            string[] candidate = new string[_stableCardIds.Length + 1];
            Array.Copy(_stableCardIds, candidate, _stableCardIds.Length);
            candidate[candidate.Length - 1] = stableCardId?.Trim() ?? string.Empty;
            return ApplyPersistedCandidate(candidate);
        }

        public CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult TryRemove(string stableCardId)
        {
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult guard = ValidateReferenceEdit(stableCardId);
            if (guard != null)
                return guard;

            if (_deck != null)
            {
                return Apply(CharacterTestRuntimeAiPlannerInstalledDeckRules.TryRemove(
                    _deck,
                    stableCardId,
                    _catalog,
                    _diagnosticsInput));
            }

            string normalized = stableCardId?.Trim() ?? string.Empty;
            string[] candidate = RemoveReference(_stableCardIds, normalized);
            return ApplyPersistedCandidate(candidate);
        }

        public CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult TryReplace(
            string replacedStableCardId,
            string replacementStableCardId)
        {
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult guard = ValidateReferenceEdit(replacedStableCardId);
            if (guard != null)
                return guard;
            string replacement = replacementStableCardId?.Trim() ?? string.Empty;
            if (!_unlockedCards.Contains(replacement))
                return Failed("PLAYER_DECK_REPLACEMENT_NOT_UNLOCKED", "$.replacementStableCardId", "Replacement behavior card is not unlocked: " + replacement);
            if (ContainsReference(replacement))
                return Failed("PLAYER_DECK_REPLACEMENT_DUPLICATE", "$.replacementStableCardId", "Replacement behavior card is already installed: " + replacement);

            string replaced = replacedStableCardId?.Trim() ?? string.Empty;
            var candidate = new string[_stableCardIds.Length];
            for (int i = 0; i < _stableCardIds.Length; i++)
                candidate[i] = string.Equals(_stableCardIds[i], replaced, StringComparison.Ordinal) ? replacement : _stableCardIds[i];
            return _deck == null
                ? ApplyPersistedCandidate(candidate)
                : Apply(BuildDeck(
                    _catalog,
                    _characterStableId,
                    _characterNumericId,
                    _runtimeContractVersion,
                    candidate,
                    _diagnosticsInput));
        }

        public CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult LockSession()
        {
            _isSessionLocked = true;
            OnSuccessfulRegularMutation();
            _view = BuildView(Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>());
            return new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult(
                _view,
                Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                true);
        }

        private CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult ValidateInstall(string stableCardId)
        {
            if (_isSessionLocked)
                return Failed("PLAYER_DECK_SESSION_LOCKED", "$.session", "Behavior deck editing is locked for the active session.");
            string normalized = stableCardId?.Trim() ?? string.Empty;
            if (!_unlockedCards.Contains(normalized))
                return Failed("PLAYER_DECK_CARD_NOT_UNLOCKED", "$.stableCardId", "Behavior card is not unlocked: " + normalized);
            if (ContainsReference(normalized))
                return Failed("PLAYER_DECK_CARD_ALREADY_INSTALLED", "$.stableCardId", "Behavior card is already installed: " + normalized);
            return null;
        }

        private CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult ValidateReferenceEdit(string stableCardId)
        {
            if (_isSessionLocked)
                return Failed("PLAYER_DECK_SESSION_LOCKED", "$.session", "Behavior deck editing is locked for the active session.");
            string normalized = stableCardId?.Trim() ?? string.Empty;
            if (!ContainsReference(normalized))
                return Failed("PLAYER_DECK_CARD_NOT_INSTALLED", "$.stableCardId", "Behavior card is not installed: " + normalized);
            return null;
        }

        private CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult Apply(
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult result)
        {
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] commandDiagnostics = CopyDiagnostics(result.Diagnostics);
            if (!result.Success)
            {
                _view = BuildView(commandDiagnostics);
                return new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult(_view, commandDiagnostics, false);
            }

            CharacterTestRuntimeAiPlannerPersistedDeck persistedDeck =
                CreatePersistedDeck(result.Deck.Cards);
            if (!TryPersist(persistedDeck, out CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic persistenceDiagnostic))
                return Failed(persistenceDiagnostic);

            _deck = result.Deck;
            _stableCardIds = CopyCardIds(result.Deck.Cards);
            _deckDiagnostics = commandDiagnostics;
            OnSuccessfulRegularMutation();
            _view = BuildView(Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>());
            return new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult(_view, commandDiagnostics, true);
        }

        private CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult ApplyPersistedCandidate(string[] candidate)
        {
            var persisted = new CharacterTestRuntimeAiPlannerPersistedDeck(
                _characterStableId,
                candidate,
                _characterNumericId);
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult persistence =
                CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.ValidateAndCreate(
                    _unlockedCards,
                    new[] { persisted });
            if (!persistence.Success)
            {
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] commandDiagnostics =
                    CopyDiagnostics(persistence.Diagnostics);
                _view = BuildView(commandDiagnostics);
                return new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult(
                    _view,
                    commandDiagnostics,
                    false);
            }

            string[] normalized = CopyCardIds(persisted.StableCardIds);
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult rebuilt = BuildDeck(
                _catalog,
                _characterStableId,
                _characterNumericId,
                _runtimeContractVersion,
                normalized,
                _diagnosticsInput);
            if (rebuilt.Success)
                return Apply(rebuilt);

            if (!TryPersist(persisted, out CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic persistenceDiagnostic))
                return Failed(persistenceDiagnostic);

            _stableCardIds = normalized;
            _deck = null;
            _deckDiagnostics = BuildUnresolvedDiagnostics(
                _catalog,
                _runtimeContractVersion,
                normalized,
                rebuilt.Diagnostics);
            OnSuccessfulRegularMutation();
            _view = BuildView(Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>());
            return new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult(
                _view,
                _deckDiagnostics,
                true);
        }

        private CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult Failed(
            string code,
            string path,
            string message)
        {
            var commandDiagnostics = new[]
            {
                new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                    CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                    code,
                    path,
                    message)
            };
            _view = BuildView(commandDiagnostics);
            return new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult(_view, commandDiagnostics, false);
        }

        private CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult Failed(
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic diagnostic)
        {
            var commandDiagnostics = new[]
            {
                diagnostic ?? new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                    CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                    "PLAYER_DECK_PERSISTENCE_FAILED",
                    "$.persistence",
                    "Behavior deck could not be saved.")
            };
            _view = BuildView(commandDiagnostics);
            return new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult(_view, commandDiagnostics, false);
        }

        private bool TryPersist(
            CharacterTestRuntimeAiPlannerPersistedDeck persistedDeck,
            out CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic diagnostic)
        {
            if (_persistenceSink == null)
            {
                diagnostic = null;
                return true;
            }
            return _persistenceSink.TryPersist(persistedDeck, out diagnostic);
        }

        private CharacterTestRuntimeAiPlannerPersistedDeck CreatePersistedDeck(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckCard> cards)
        {
            var stableCardIds = new string[cards.Count];
            for (int i = 0; i < stableCardIds.Length; i++)
                stableCardIds[i] = cards[i].StableCardId;
            return new CharacterTestRuntimeAiPlannerPersistedDeck(
                _characterStableId,
                CopyCardNumericIds(cards),
                stableCardIds,
                _characterNumericId,
                _deck?.CatalogNumericId ?? CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.DefaultCatalogNumericId,
                _deck?.CatalogNumericIdentityHash ?? 0L);
        }

        private static int[] CopyCardNumericIds(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckCard> cards)
        {
            var ids = new int[cards.Count];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = cards[i].NumericId;
            return ids;
        }

        private CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView BuildView(
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] commandDiagnostics)
        {
            var stableCardIds = new SortedSet<string>(StringComparer.Ordinal);
            IReadOnlyList<CharacterTestRuntimeAiPlannerPublishedBehaviorCard> catalogCards = _catalog.Cards;
            for (int i = 0; i < catalogCards.Count; i++)
                stableCardIds.Add(catalogCards[i].StableCardId);
            for (int i = 0; i < _unlockedCards.StableCardIds.Count; i++)
                stableCardIds.Add(_unlockedCards.StableCardIds[i]);
            for (int i = 0; i < _stableCardIds.Length; i++)
                stableCardIds.Add(_stableCardIds[i]);

            var cards = new CharacterTestRuntimeAiPlannerPlayerDeckCardView[stableCardIds.Count];
            int targetIndex = 0;
            foreach (string stableCardId in stableCardIds)
                cards[targetIndex++] = BuildCardView(stableCardId);
            return new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView(
                _characterStableId,
                _stableCardIds.Length,
                ResolveKnownCost(),
                IsKnownCostComplete(),
                _deck?.DeckHash ?? 0L,
                _deck != null,
                cards,
                _deckDiagnostics,
                commandDiagnostics,
                _isSessionLocked);
        }

        private CharacterTestRuntimeAiPlannerPlayerDeckCardView BuildCardView(string stableCardId)
        {
            bool available = _catalog.TryResolveLatestCompatible(
                stableCardId,
                _runtimeContractVersion,
                out CharacterTestRuntimeAiPlannerPublishedBehaviorCard card);
            CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability availability = available
                ? CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Available
                : HasCatalogRevision(stableCardId)
                    ? CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Incompatible
                    : CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Unavailable;
            bool unlocked = _unlockedCards.Contains(stableCardId);
            bool installed = ContainsReference(stableCardId);
            CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason blockReason = ResolveBlockReason(
                card,
                availability,
                unlocked,
                installed);
            return new CharacterTestRuntimeAiPlannerPlayerDeckCardView(
                stableCardId,
                card,
                availability,
                unlocked,
                installed,
                blockReason == CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.None,
                installed && !_isSessionLocked,
                installed && !_isSessionLocked && availability != CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Available,
                !_isSessionLocked
                    && availability == CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Available
                    && unlocked
                    && !installed,
                blockReason);
        }

        private CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason ResolveBlockReason(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard card,
            CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability availability,
            bool unlocked,
            bool installed)
        {
            if (_isSessionLocked)
                return CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.SessionLocked;
            if (availability == CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Unavailable)
                return CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.Unavailable;
            if (availability == CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Incompatible)
                return CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.Incompatible;
            if (!unlocked)
                return CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.NotUnlocked;
            if (installed)
                return CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.AlreadyInstalled;
            if (_deck == null)
                return CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.DeckUnresolved;
            if (_stableCardIds.Length >= CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumSlots)
                return CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.SlotLimit;
            if (_deck.TotalCost + card.Metadata.Cost > CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumCost)
                return CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.CostLimit;
            return CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.None;
        }

        private int ResolveKnownCost()
        {
            if (_deck != null)
                return _deck.TotalCost;
            int total = 0;
            for (int i = 0; i < _stableCardIds.Length; i++)
            {
                if (_catalog.TryResolveLatestCompatible(_stableCardIds[i], _runtimeContractVersion, out CharacterTestRuntimeAiPlannerPublishedBehaviorCard card))
                    total += card.Metadata.Cost;
            }
            return total;
        }

        private bool IsKnownCostComplete()
        {
            if (_deck != null)
                return true;
            for (int i = 0; i < _stableCardIds.Length; i++)
            {
                if (!_catalog.TryResolveLatestCompatible(_stableCardIds[i], _runtimeContractVersion, out _))
                    return false;
            }
            return true;
        }

        private bool ContainsReference(string stableCardId)
        {
            return Array.BinarySearch(_stableCardIds, stableCardId, StringComparer.Ordinal) >= 0;
        }

        private bool HasCatalogRevision(string stableCardId)
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerPublishedBehaviorCard> cards = _catalog.Cards;
            for (int i = 0; i < cards.Count; i++)
            {
                int comparison = string.Compare(cards[i].StableCardId, stableCardId, StringComparison.Ordinal);
                if (comparison == 0)
                    return true;
                if (comparison > 0)
                    return false;
            }
            return false;
        }

        private static CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult Create(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlockedCards,
            string characterStableId,
            int characterNumericId,
            int runtimeContractVersion,
            string[] stableCardIds,
            CharacterTestRuntimeAiPlannerInstalledDeck deck,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput diagnosticsInput,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] deckDiagnostics,
            ICharacterTestRuntimeAiPlannerPlayerDeckPersistenceSink persistenceSink,
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> additionalDeckDiagnostics)
        {
            Array.Sort(stableCardIds, StringComparer.Ordinal);
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] combinedDiagnostics =
                MergeDiagnostics(deckDiagnostics, additionalDeckDiagnostics);
            var workbench = new CharacterTestRuntimeAiPlannerPlayerDeckWorkbench(
                catalog,
                unlockedCards,
                characterStableId,
                characterNumericId,
                runtimeContractVersion,
                stableCardIds,
                deck,
                diagnosticsInput,
                combinedDiagnostics,
                persistenceSink);
            return new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult(
                workbench,
                combinedDiagnostics);
        }

        private static CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] MergeDiagnostics(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> primary,
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> additional)
        {
            int primaryCount = primary?.Count ?? 0;
            int additionalCount = additional?.Count ?? 0;
            if (additionalCount == 0)
                return CopyDiagnostics(primary);
            var result = new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[primaryCount + additionalCount];
            for (int i = 0; i < primaryCount; i++)
                result[i] = primary[i];
            for (int i = 0; i < additionalCount; i++)
                result[primaryCount + i] = additional[i];
            return result;
        }

        private static CharacterTestRuntimeAiPlannerInstalledDeckBuildResult BuildDeck(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            string characterStableId,
            int characterNumericId,
            int runtimeContractVersion,
            string[] stableCardIds,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput diagnosticsInput)
        {
            return CharacterTestRuntimeAiPlannerInstalledDeckRules.ValidateAndCreate(
                new CharacterTestRuntimeAiPlannerInstalledDeckCandidate(
                    characterStableId,
                    stableCardIds,
                    characterNumericId),
                catalog,
                runtimeContractVersion,
                diagnosticsInput);
        }

        private static CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] BuildUnresolvedDiagnostics(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            int runtimeContractVersion,
            string[] stableCardIds,
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> buildDiagnostics)
        {
            var diagnostics = new List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>();
            for (int i = 0; i < buildDiagnostics.Count; i++)
            {
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic source = buildDiagnostics[i];
                diagnostics.Add(new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                    CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning,
                    source.Code,
                    source.Path,
                    source.Message));
            }
            for (int i = 0; i < stableCardIds.Length; i++)
            {
                if (catalog.TryResolveLatestCompatible(stableCardIds[i], runtimeContractVersion, out _))
                    continue;
                bool hasRevision = false;
                for (int cardIndex = 0; cardIndex < catalog.Cards.Count; cardIndex++)
                {
                    if (string.Equals(catalog.Cards[cardIndex].StableCardId, stableCardIds[i], StringComparison.Ordinal))
                    {
                        hasRevision = true;
                        break;
                    }
                }
                diagnostics.Add(new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                    CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning,
                    hasRevision ? "PLAYER_DECK_CARD_INCOMPATIBLE" : "PLAYER_DECK_CARD_UNAVAILABLE",
                    "$.stableCardIds[" + i + "]",
                    hasRevision
                        ? "An owned behavior card has no compatible published revision."
                        : "An owned behavior card is absent from the current published catalog."));
            }
            diagnostics.Add(new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning,
                "PLAYER_DECK_REQUIRES_REPLACEMENT",
                "$.stableCardIds",
                "Persisted behavior deck is preserved but cannot be activated until unavailable or incompatible cards are replaced."));
            return diagnostics.ToArray();
        }

        private static string[] RemoveReference(string[] source, string removed)
        {
            var result = new string[source.Length - 1];
            int target = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (!string.Equals(source[i], removed, StringComparison.Ordinal))
                    result[target++] = source[i];
            }
            return result;
        }

        private static string[] CopyCardIds(IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckCard> cards)
        {
            var result = new string[cards.Count];
            for (int i = 0; i < cards.Count; i++)
                result[i] = cards[i].StableCardId;
            return result;
        }

        private static string[] CopyCardIds(IReadOnlyList<string> cards)
        {
            var result = new string[cards.Count];
            for (int i = 0; i < cards.Count; i++)
                result[i] = cards[i];
            return result;
        }

        private static CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] CopyDiagnostics(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> source)
        {
            var result = new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[source?.Count ?? 0];
            for (int i = 0; i < result.Length; i++)
                result[i] = source[i];
            return result;
        }

        private static void ValidateCommon(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlockedCards,
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            if (catalog == null)
                AddError(diagnostics, "PLAYER_DECK_CATALOG_MISSING", "$.catalog", "Published behavior card catalog is required.");
            if (unlockedCards == null)
                AddError(diagnostics, "PLAYER_DECK_UNLOCK_COLLECTION_MISSING", "$.unlockedCards", "Unlocked behavior card collection is required.");
        }

        private static void ValidateOwnedReferences(
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlockedCards,
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckCard> cards,
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (!unlockedCards.Contains(cards[i].StableCardId))
                {
                    AddError(
                        diagnostics,
                        "PLAYER_DECK_INSTALLED_CARD_NOT_UNLOCKED",
                        "$.deck.cards[" + i + "]",
                        "Installed behavior card is not unlocked: " + cards[i].StableCardId);
                }
            }
        }

        private static CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult FailedCreate(
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            return new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult(null, diagnostics.ToArray());
        }

        private static void AddDiagnostics(
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> target,
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> source)
        {
            for (int i = 0; i < source.Count; i++)
                target.Add(source[i]);
        }

        private static void AddError(
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            diagnostics.Add(new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                code,
                path,
                message));
        }
    }
}
