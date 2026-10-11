using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiPlannerStableCardIdMigration
    {
        public CharacterTestRuntimeAiPlannerStableCardIdMigration(string sourceStableCardId, string targetStableCardId)
        {
            SourceStableCardId = sourceStableCardId?.Trim() ?? string.Empty;
            TargetStableCardId = targetStableCardId?.Trim() ?? string.Empty;
        }

        public string SourceStableCardId { get; }
        public string TargetStableCardId { get; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerStableCardIdMigrationMap
    {
        private readonly CharacterTestRuntimeAiPlannerStableCardIdMigration[] _migrations;

        /// <remarks>LowFreqAlloc: save migration/bootstrap only.</remarks>
        public CharacterTestRuntimeAiPlannerStableCardIdMigrationMap(
            IEnumerable<CharacterTestRuntimeAiPlannerStableCardIdMigration> migrations)
        {
            var items = new List<CharacterTestRuntimeAiPlannerStableCardIdMigration>();
            if (migrations != null)
            {
                foreach (CharacterTestRuntimeAiPlannerStableCardIdMigration migration in migrations)
                {
                    if (migration != null)
                        items.Add(migration);
                }
            }
            _migrations = items.ToArray();
            Array.Sort(_migrations, CompareMigrations);
        }

        public static CharacterTestRuntimeAiPlannerStableCardIdMigrationMap Empty { get; } =
            new CharacterTestRuntimeAiPlannerStableCardIdMigrationMap(null);

        public bool TryResolve(string stableCardId, out string resolvedStableCardId, out bool migrated)
        {
            resolvedStableCardId = stableCardId?.Trim() ?? string.Empty;
            migrated = false;
            for (int depth = 0; depth <= _migrations.Length; depth++)
            {
                int index = FindSource(resolvedStableCardId);
                if (index < 0)
                    return true;
                resolvedStableCardId = _migrations[index].TargetStableCardId;
                migrated = true;
            }
            return false;
        }

        public CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] Validate()
        {
            var diagnostics = new List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>();
            for (int i = 0; i < _migrations.Length; i++)
            {
                CharacterTestRuntimeAiPlannerStableCardIdMigration migration = _migrations[i];
                string path = "$.stableCardIdMigrations[" + i + "]";
                if (!CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.IsStableCardId(
                        migration.SourceStableCardId))
                {
                    Error(diagnostics, "DECK_PERSISTENCE_MIGRATION_SOURCE_INVALID", path + ".sourceStableCardId", "Migration source stable card id is invalid.");
                }
                if (!CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.IsStableCardId(
                        migration.TargetStableCardId))
                {
                    Error(diagnostics, "DECK_PERSISTENCE_MIGRATION_TARGET_INVALID", path + ".targetStableCardId", "Migration target stable card id is invalid.");
                }
                if (string.Equals(migration.SourceStableCardId, migration.TargetStableCardId, StringComparison.Ordinal))
                {
                    Error(diagnostics, "DECK_PERSISTENCE_MIGRATION_SELF_REFERENCE", path, "Stable card id migration cannot target itself.");
                }
                if (i > 0 && string.Equals(
                        _migrations[i - 1].SourceStableCardId,
                        migration.SourceStableCardId,
                        StringComparison.Ordinal))
                {
                    Error(diagnostics, "DECK_PERSISTENCE_MIGRATION_SOURCE_DUPLICATE", path + ".sourceStableCardId", "Stable card id migration source must be unique.");
                }
                if (!TryResolve(migration.SourceStableCardId, out _, out _))
                {
                    Error(diagnostics, "DECK_PERSISTENCE_MIGRATION_CYCLE", path, "Stable card id migrations must not contain a cycle.");
                }
            }
            return diagnostics.ToArray();
        }

        private int FindSource(string stableCardId)
        {
            int low = 0;
            int high = _migrations.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int comparison = string.Compare(
                    _migrations[middle].SourceStableCardId,
                    stableCardId,
                    StringComparison.Ordinal);
                if (comparison == 0)
                    return middle;
                if (comparison < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            return -1;
        }

        private static int CompareMigrations(
            CharacterTestRuntimeAiPlannerStableCardIdMigration left,
            CharacterTestRuntimeAiPlannerStableCardIdMigration right)
        {
            return string.Compare(left.SourceStableCardId, right.SourceStableCardId, StringComparison.Ordinal);
        }

        private static void Error(
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

    internal sealed class CharacterTestRuntimeAiPlannerUnlockedCardCollection
    {
        private readonly string[] _stableCardIds;
        private readonly IReadOnlyList<string> _readOnlyStableCardIds;

        private CharacterTestRuntimeAiPlannerUnlockedCardCollection(string[] stableCardIds)
        {
            _stableCardIds = stableCardIds ?? Array.Empty<string>();
            _readOnlyStableCardIds = Array.AsReadOnly(_stableCardIds);
        }

        public IReadOnlyList<string> StableCardIds => _readOnlyStableCardIds;

        public bool Contains(string stableCardId)
        {
            string normalized = stableCardId?.Trim() ?? string.Empty;
            return Array.BinarySearch(_stableCardIds, normalized, StringComparer.Ordinal) >= 0;
        }

        /// <remarks>LowFreqAlloc: account/profile load only.</remarks>
        public static CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult ValidateAndCreate(
            IEnumerable<string> stableCardIds)
        {
            var diagnostics = new List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>();
            var ids = new List<string>();
            if (stableCardIds != null)
            {
                int index = 0;
                foreach (string stableCardId in stableCardIds)
                {
                    string normalized = stableCardId?.Trim() ?? string.Empty;
                    if (!CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.IsStableCardId(normalized))
                    {
                        Error(diagnostics, "DECK_PERSISTENCE_UNLOCKED_CARD_ID_INVALID", "$.unlockedCardIds[" + index + "]", "Unlocked behavior card stable id is invalid.");
                    }
                    else
                    {
                        ids.Add(normalized);
                    }
                    index++;
                }
            }
            ids.Sort(StringComparer.Ordinal);
            for (int i = 1; i < ids.Count; i++)
            {
                if (string.Equals(ids[i - 1], ids[i], StringComparison.Ordinal))
                    Error(diagnostics, "DECK_PERSISTENCE_UNLOCKED_CARD_DUPLICATE", "$.unlockedCardIds[" + i + "]", "Unlocked behavior card stable ids must be unique.");
            }
            if (HasErrors(diagnostics))
                return new CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult(null, diagnostics.ToArray());
            return new CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult(
                new CharacterTestRuntimeAiPlannerUnlockedCardCollection(ids.ToArray()),
                diagnostics.ToArray());
        }

        /// <remarks>LowFreqAlloc: explicit progression unlock only.</remarks>
        public static CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult TryUnlock(
            CharacterTestRuntimeAiPlannerUnlockedCardCollection source,
            string stableCardId,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            int runtimeContractVersion)
        {
            var diagnostics = new List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>();
            if (source == null)
                Error(diagnostics, "DECK_PERSISTENCE_UNLOCK_COLLECTION_MISSING", "$", "Unlocked behavior card collection is required.");
            if (catalog == null)
                Error(diagnostics, "DECK_PERSISTENCE_CATALOG_MISSING", "$.catalog", "Published behavior card catalog is required.");
            string normalized = stableCardId?.Trim() ?? string.Empty;
            if (!CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.IsStableCardId(normalized))
                Error(diagnostics, "DECK_PERSISTENCE_UNLOCK_CARD_ID_INVALID", "$.stableCardId", "Behavior card stable id is invalid.");
            if (source != null && source.Contains(normalized))
                Error(diagnostics, "DECK_PERSISTENCE_CARD_ALREADY_UNLOCKED", "$.stableCardId", "Behavior card is already unlocked: " + normalized);
            if (catalog != null && runtimeContractVersion > 0
                && !catalog.TryResolveLatestCompatible(normalized, runtimeContractVersion, out _))
            {
                Error(diagnostics, "DECK_PERSISTENCE_UNLOCK_CARD_UNAVAILABLE", "$.stableCardId", "No compatible published behavior card revision can be unlocked: " + normalized);
            }
            if (runtimeContractVersion <= 0)
                Error(diagnostics, "DECK_PERSISTENCE_RUNTIME_VERSION_INVALID", "$.runtimeContractVersion", "Runtime contract version must be positive.");
            if (HasErrors(diagnostics))
                return new CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult(null, diagnostics.ToArray());

            var ids = new string[source._stableCardIds.Length + 1];
            Array.Copy(source._stableCardIds, ids, source._stableCardIds.Length);
            ids[ids.Length - 1] = normalized;
            Array.Sort(ids, StringComparer.Ordinal);
            return new CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult(
                new CharacterTestRuntimeAiPlannerUnlockedCardCollection(ids),
                diagnostics.ToArray());
        }

        private static bool HasErrors(IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error)
                    return true;
            }
            return false;
        }

        private static void Error(
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

    internal sealed class CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult
    {
        public CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult(
            CharacterTestRuntimeAiPlannerUnlockedCardCollection collection,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] diagnostics)
        {
            Collection = collection;
            Diagnostics = Array.AsReadOnly(diagnostics ?? Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>());
        }

        public CharacterTestRuntimeAiPlannerUnlockedCardCollection Collection { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> Diagnostics { get; }
        public bool Success => Collection != null;
    }

    internal sealed class CharacterTestRuntimeAiPlannerPersistedDeck
    {
        private readonly string[] _stableCardIds;
        private readonly IReadOnlyList<string> _readOnlyStableCardIds;
        private readonly int[] _cardNumericIds;
        private readonly IReadOnlyList<int> _readOnlyCardNumericIds;

        public CharacterTestRuntimeAiPlannerPersistedDeck(
            string characterStableId,
            IEnumerable<string> stableCardIds,
            int characterNumericId)
            : this(characterStableId, null, stableCardIds, characterNumericId, CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.DefaultCatalogNumericId, 0L)
        {
        }

        public CharacterTestRuntimeAiPlannerPersistedDeck(
            string characterStableId,
            IEnumerable<int> cardNumericIds,
            IEnumerable<string> stableCardIds,
            int characterNumericId,
            int catalogNumericId,
            long catalogNumericIdentityHash = 0L)
        {
            CharacterStableId = characterStableId?.Trim() ?? string.Empty;
            CharacterNumericId = characterNumericId;
            CatalogNumericId = catalogNumericId;
            CatalogNumericIdentityHash = catalogNumericIdentityHash;
            var ids = new List<string>();
            if (stableCardIds != null)
            {
                foreach (string stableCardId in stableCardIds)
                    ids.Add(stableCardId?.Trim() ?? string.Empty);
            }
            var numericIds = new List<int>();
            if (cardNumericIds != null)
            {
                foreach (int numericId in cardNumericIds)
                    numericIds.Add(numericId);
            }
            _stableCardIds = ids.ToArray();
            _cardNumericIds = numericIds.ToArray();
            for (int left = 0; left < _stableCardIds.Length; left++)
            {
                for (int right = left + 1; right < _stableCardIds.Length; right++)
                {
                    if (string.Compare(_stableCardIds[left], _stableCardIds[right], StringComparison.Ordinal) <= 0)
                        continue;
                    string stable = _stableCardIds[left];
                    _stableCardIds[left] = _stableCardIds[right];
                    _stableCardIds[right] = stable;
                    if (_cardNumericIds.Length == _stableCardIds.Length)
                    {
                        int numeric = _cardNumericIds[left];
                        _cardNumericIds[left] = _cardNumericIds[right];
                        _cardNumericIds[right] = numeric;
                    }
                }
            }
            _readOnlyStableCardIds = Array.AsReadOnly(_stableCardIds);
            _readOnlyCardNumericIds = Array.AsReadOnly(_cardNumericIds);
        }

        public string CharacterStableId { get; }
        public int CharacterNumericId { get; }
        public int CatalogNumericId { get; }
        public long CatalogNumericIdentityHash { get; }
        public IReadOnlyList<string> StableCardIds => _readOnlyStableCardIds;
        public IReadOnlyList<int> CardNumericIds => _readOnlyCardNumericIds;
    }

    internal sealed class CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot
    {
        public const string Schema = "mx.character-test.runtime-ai-planner.deck-persistence.v2";
        public const int SchemaVersion = 2;

        private readonly CharacterTestRuntimeAiPlannerPersistedDeck[] _decks;
        private readonly IReadOnlyList<CharacterTestRuntimeAiPlannerPersistedDeck> _readOnlyDecks;

        private CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot(
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlockedCards,
            CharacterTestRuntimeAiPlannerPersistedDeck[] decks,
            long persistenceHash)
        {
            UnlockedCards = unlockedCards;
            _decks = decks;
            _readOnlyDecks = Array.AsReadOnly(_decks);
            PersistenceHash = persistenceHash;
        }

        public CharacterTestRuntimeAiPlannerUnlockedCardCollection UnlockedCards { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerPersistedDeck> Decks => _readOnlyDecks;
        public long PersistenceHash { get; }

        public bool TryGetDeck(string characterStableId, out CharacterTestRuntimeAiPlannerPersistedDeck deck)
        {
            string normalized = characterStableId?.Trim() ?? string.Empty;
            int low = 0;
            int high = _decks.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int comparison = string.Compare(_decks[middle].CharacterStableId, normalized, StringComparison.Ordinal);
                if (comparison == 0)
                {
                    deck = _decks[middle];
                    return true;
                }
                if (comparison < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            deck = null;
            return false;
        }

        /// <remarks>LowFreqAlloc: account/profile load and explicit deck save only.</remarks>
        public static CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult ValidateAndCreate(
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlockedCards,
            IEnumerable<CharacterTestRuntimeAiPlannerPersistedDeck> decks)
        {
            var diagnostics = new List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>();
            if (unlockedCards == null)
                Error(diagnostics, "DECK_PERSISTENCE_UNLOCK_COLLECTION_MISSING", "$.unlockedCardIds", "Unlocked behavior card collection is required.");

            var normalizedDecks = new List<CharacterTestRuntimeAiPlannerPersistedDeck>();
            if (decks != null)
            {
                foreach (CharacterTestRuntimeAiPlannerPersistedDeck deck in decks)
                {
                    if (deck != null)
                        normalizedDecks.Add(deck);
                }
            }
            normalizedDecks.Sort(CompareDecks);
            for (int deckIndex = 0; deckIndex < normalizedDecks.Count; deckIndex++)
            {
                CharacterTestRuntimeAiPlannerPersistedDeck deck = normalizedDecks[deckIndex];
                string path = "$.decks[" + deckIndex + "]";
                if (deck.CharacterStableId.Length == 0)
                    Error(diagnostics, "DECK_PERSISTENCE_CHARACTER_ID_MISSING", path + ".characterStableId", "Persisted deck character stable id is required.");
                if (deck.CharacterNumericId <= 0)
                    Error(diagnostics, "DECK_PERSISTENCE_CHARACTER_NUMERIC_ID_INVALID", path + ".characterNumericId", "Persisted deck character numeric id must be positive.");
                if (deck.CatalogNumericId <= 0)
                    Error(diagnostics, "DECK_PERSISTENCE_CATALOG_NUMERIC_ID_INVALID", path + ".catalogNumericId", "Persisted deck catalog numeric id must be positive.");
                if (deck.CatalogNumericIdentityHash == 0L)
                    Error(diagnostics, "DECK_PERSISTENCE_CATALOG_NUMERIC_IDENTITY_HASH_MISSING", path + ".catalogNumericIdentityHash", "Persisted deck catalog numeric identity hash is required.");
                if (deckIndex > 0 && string.Equals(normalizedDecks[deckIndex - 1].CharacterStableId, deck.CharacterStableId, StringComparison.Ordinal))
                    Error(diagnostics, "DECK_PERSISTENCE_CHARACTER_DUPLICATE", path + ".characterStableId", "Only one persisted deck is allowed per character.");
                if (deck.StableCardIds.Count > CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumSlots)
                    Error(diagnostics, "DECK_PERSISTENCE_SLOT_LIMIT_EXCEEDED", path + ".stableCardIds", "Persisted deck cannot contain more than 6 stable card ids.");
                if (deck.CardNumericIds.Count != 0 && deck.CardNumericIds.Count != deck.StableCardIds.Count)
                    Error(diagnostics, "DECK_PERSISTENCE_CARD_NUMERIC_ID_COUNT_MISMATCH", path + ".cardNumericIds", "Persisted deck card numeric ids must align with stable card labels.");
                for (int cardIndex = 0; cardIndex < deck.StableCardIds.Count; cardIndex++)
                {
                    string stableCardId = deck.StableCardIds[cardIndex];
                    string cardPath = path + ".stableCardIds[" + cardIndex + "]";
                    if (!CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.IsStableCardId(stableCardId))
                        Error(diagnostics, "DECK_PERSISTENCE_DECK_CARD_ID_INVALID", cardPath, "Persisted deck behavior card stable id is invalid.");
                    if (cardIndex > 0 && string.Equals(deck.StableCardIds[cardIndex - 1], stableCardId, StringComparison.Ordinal))
                        Error(diagnostics, "DECK_PERSISTENCE_DECK_CARD_DUPLICATE", cardPath, "Persisted deck behavior card stable ids must be unique.");
                    if (unlockedCards != null && !unlockedCards.Contains(stableCardId))
                        Error(diagnostics, "DECK_PERSISTENCE_DECK_CARD_LOCKED", cardPath, "Installed behavior card must exist in the unlocked collection: " + stableCardId);
                }
            }
            if (HasErrors(diagnostics))
                return new CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult(null, diagnostics.ToArray());

            CharacterTestRuntimeAiPlannerPersistedDeck[] deckArray = normalizedDecks.ToArray();
            return new CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult(
                new CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot(
                    unlockedCards,
                    deckArray,
                    ComputeHash(unlockedCards, deckArray)),
                diagnostics.ToArray());
        }

        private static long ComputeHash(
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlockedCards,
            IReadOnlyList<CharacterTestRuntimeAiPlannerPersistedDeck> decks)
        {
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("deckPersistence.schema", Schema);
            hash.AddInt("deckPersistence.unlockedCount", unlockedCards.StableCardIds.Count);
            for (int i = 0; i < unlockedCards.StableCardIds.Count; i++)
                hash.AddStringStable("deckPersistence.unlockedStableCardId", unlockedCards.StableCardIds[i]);
            hash.AddInt("deckPersistence.deckCount", decks.Count);
            for (int deckIndex = 0; deckIndex < decks.Count; deckIndex++)
            {
                CharacterTestRuntimeAiPlannerPersistedDeck deck = decks[deckIndex];
                hash.AddStringStable("deckPersistence.characterStableId", deck.CharacterStableId);
                hash.AddInt("deckPersistence.characterNumericId", deck.CharacterNumericId);
                hash.AddInt("deckPersistence.catalogNumericId", deck.CatalogNumericId);
                hash.AddLong("deckPersistence.catalogNumericIdentityHash", deck.CatalogNumericIdentityHash);
                hash.AddInt("deckPersistence.cardCount", deck.StableCardIds.Count);
                for (int cardIndex = 0; cardIndex < deck.StableCardIds.Count; cardIndex++)
                {
                    hash.AddStringStable("deckPersistence.cardStableId", deck.StableCardIds[cardIndex]);
                    hash.AddInt("deckPersistence.cardNumericId", deck.CardNumericIds.Count == deck.StableCardIds.Count ? deck.CardNumericIds[cardIndex] : 0);
                }
            }
            return hash.ToHash();
        }

        private static int CompareDecks(
            CharacterTestRuntimeAiPlannerPersistedDeck left,
            CharacterTestRuntimeAiPlannerPersistedDeck right)
        {
            return string.Compare(left.CharacterStableId, right.CharacterStableId, StringComparison.Ordinal);
        }

        private static bool HasErrors(IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error)
                    return true;
            }
            return false;
        }

        private static void Error(
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

    internal sealed class CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult
    {
        public CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult(
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot snapshot,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] diagnostics)
        {
            Snapshot = snapshot;
            Diagnostics = Array.AsReadOnly(diagnostics ?? Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>());
        }

        public CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot Snapshot { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> Diagnostics { get; }
        public bool Success => Snapshot != null;
    }

    internal static class CharacterTestRuntimeAiPlannerDeckPersistenceRules
    {
        /// <remarks>LowFreqAlloc: explicit player/Lab deck save only.</remarks>
        public static CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult TrySaveInstalledDeck(
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot source,
            CharacterTestRuntimeAiPlannerInstalledDeck installedDeck)
        {
            var diagnostics = new List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>();
            if (source == null)
                Error(diagnostics, "DECK_PERSISTENCE_SOURCE_MISSING", "$", "Deck persistence source snapshot is required.");
            if (installedDeck == null)
                Error(diagnostics, "DECK_PERSISTENCE_INSTALLED_DECK_MISSING", "$.installedDeck", "Validated installed deck is required.");
            if (source == null || installedDeck == null)
                return new CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult(null, diagnostics.ToArray());

            for (int i = 0; i < installedDeck.Cards.Count; i++)
            {
                if (!source.UnlockedCards.Contains(installedDeck.Cards[i].StableCardId))
                {
                    Error(
                        diagnostics,
                        "DECK_PERSISTENCE_DECK_CARD_LOCKED",
                        "$.installedDeck.cards[" + i + "]",
                        "Installed behavior card must be unlocked before the deck can be saved: " + installedDeck.Cards[i].StableCardId);
                }
            }
            if (diagnostics.Count > 0)
                return new CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult(null, diagnostics.ToArray());

            var decks = new List<CharacterTestRuntimeAiPlannerPersistedDeck>(source.Decks.Count + 1);
            bool replaced = false;
            for (int i = 0; i < source.Decks.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPersistedDeck existing = source.Decks[i];
                if (string.Equals(existing.CharacterStableId, installedDeck.CharacterStableId, StringComparison.Ordinal))
                {
                    decks.Add(FromInstalledDeck(installedDeck));
                    replaced = true;
                }
                else
                {
                    decks.Add(existing);
                }
            }
            if (!replaced)
                decks.Add(FromInstalledDeck(installedDeck));
            return CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.ValidateAndCreate(
                source.UnlockedCards,
                decks);
        }

        private static CharacterTestRuntimeAiPlannerPersistedDeck FromInstalledDeck(
            CharacterTestRuntimeAiPlannerInstalledDeck installedDeck)
        {
            var ids = new string[installedDeck.Cards.Count];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = installedDeck.Cards[i].StableCardId;
            return new CharacterTestRuntimeAiPlannerPersistedDeck(
                installedDeck.CharacterStableId,
                CopyCardNumericIds(installedDeck),
                ids,
                installedDeck.CharacterNumericId,
                installedDeck.CatalogNumericId,
                installedDeck.CatalogNumericIdentityHash);
        }

        private static int[] CopyCardNumericIds(CharacterTestRuntimeAiPlannerInstalledDeck installedDeck)
        {
            var ids = new int[installedDeck.Cards.Count];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = installedDeck.Cards[i].NumericId;
            return ids;
        }

        private static void Error(
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
