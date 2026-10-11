using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiPlannerResolvedPersistedDeck
    {
        public CharacterTestRuntimeAiPlannerResolvedPersistedDeck(
            string characterStableId,
            CharacterTestRuntimeAiPlannerInstalledDeck deck)
        {
            CharacterStableId = characterStableId ?? string.Empty;
            Deck = deck;
        }

        public string CharacterStableId { get; }
        public CharacterTestRuntimeAiPlannerInstalledDeck Deck { get; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerDeckPersistenceRestoreResult
    {
        private readonly IReadOnlyList<CharacterTestRuntimeAiPlannerResolvedPersistedDeck> _resolvedDecks;

        public CharacterTestRuntimeAiPlannerDeckPersistenceRestoreResult(
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot snapshot,
            CharacterTestRuntimeAiPlannerResolvedPersistedDeck[] resolvedDecks,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] diagnostics)
        {
            Snapshot = snapshot;
            _resolvedDecks = Array.AsReadOnly(resolvedDecks ?? Array.Empty<CharacterTestRuntimeAiPlannerResolvedPersistedDeck>());
            Diagnostics = Array.AsReadOnly(diagnostics ?? Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>());
        }

        public CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot Snapshot { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerResolvedPersistedDeck> ResolvedDecks => _resolvedDecks;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> Diagnostics { get; }
        public bool Success => Snapshot != null && !HasErrors(Diagnostics);
        public bool FullyResolved => Success && ResolvedDecks.Count == Snapshot.Decks.Count;

        public bool TryGetResolvedDeck(string characterStableId, out CharacterTestRuntimeAiPlannerInstalledDeck deck)
        {
            string normalized = characterStableId?.Trim() ?? string.Empty;
            for (int i = 0; i < _resolvedDecks.Count; i++)
            {
                if (string.Equals(_resolvedDecks[i].CharacterStableId, normalized, StringComparison.Ordinal))
                {
                    deck = _resolvedDecks[i].Deck;
                    return true;
                }
            }
            deck = null;
            return false;
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
    }

    internal static class CharacterTestRuntimeAiPlannerDeckPersistenceCodec
    {
        public const string CustomStateTypeId = "character-test.runtime-ai-planner.deck-persistence";

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <remarks>LowFreqAlloc: explicit profile save only.</remarks>
        public static RuntimeCustomState Capture(CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            string payload = JsonConvert.SerializeObject(PersistenceDocument.FromSnapshot(snapshot), Settings);
            return new RuntimeCustomState(
                RuntimeHashSchemaIds.RuntimeAiPlannerDeckPersistence,
                CustomStateTypeId,
                CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.SchemaVersion,
                payload);
        }

        /// <remarks>LowFreqAlloc: profile load, migration and catalog resolution only.</remarks>
        public static CharacterTestRuntimeAiPlannerDeckPersistenceRestoreResult Restore(
            RuntimeCustomState customState,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            int runtimeContractVersion,
            CharacterTestRuntimeAiPlannerStableCardIdMigrationMap migrations)
        {
            var diagnostics = new List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>();
            if (customState == null)
                Error(diagnostics, "DECK_PERSISTENCE_CUSTOM_STATE_MISSING", "$", "Deck persistence custom state is required.");
            else if (customState.NumericTypeId != RuntimeHashSchemaIds.RuntimeAiPlannerDeckPersistence)
                Error(diagnostics, "DECK_PERSISTENCE_CUSTOM_STATE_TYPE_MISMATCH", "$.numericTypeId", "Deck persistence custom state type is unsupported.");
            else if (customState.SchemaVersion != CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.SchemaVersion)
                Error(diagnostics, "DECK_PERSISTENCE_SCHEMA_VERSION_UNSUPPORTED", "$.schemaVersion", "Deck persistence custom state schema version is unsupported.");
            if (catalog == null)
                Error(diagnostics, "DECK_PERSISTENCE_CATALOG_MISSING", "$.catalog", "Published behavior card catalog is required for restore.");
            if (runtimeContractVersion <= 0)
                Error(diagnostics, "DECK_PERSISTENCE_RUNTIME_VERSION_INVALID", "$.runtimeContractVersion", "Runtime contract version must be positive.");
            CharacterTestRuntimeAiPlannerStableCardIdMigrationMap migrationMap = migrations
                ?? CharacterTestRuntimeAiPlannerStableCardIdMigrationMap.Empty;
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] migrationDiagnostics = migrationMap.Validate();
            AddDiagnostics(diagnostics, migrationDiagnostics);
            if (HasErrors(diagnostics))
                return Failed(diagnostics);

            PersistenceDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<PersistenceDocument>(customState.PayloadJson, Settings);
            }
            catch (JsonException exception)
            {
                Error(diagnostics, "DECK_PERSISTENCE_JSON_PARSE", "$.payloadJson", "Deck persistence payload JSON parse failed: " + exception.Message);
                return Failed(diagnostics);
            }
            if (document == null)
            {
                Error(diagnostics, "DECK_PERSISTENCE_DOCUMENT_MISSING", "$.payloadJson", "Deck persistence payload document is missing.");
                return Failed(diagnostics);
            }
            if (!string.Equals(document.Schema, CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.Schema, StringComparison.Ordinal))
                Error(diagnostics, "DECK_PERSISTENCE_SCHEMA_UNSUPPORTED", "$.payloadJson.schema", "Deck persistence payload schema is unsupported.");
            if (!document.PersistenceHash.HasValue)
                Error(diagnostics, "DECK_PERSISTENCE_HASH_MISSING", "$.payloadJson.persistenceHash", "Deck persistence payload hash is required.");
            if (HasErrors(diagnostics))
                return Failed(diagnostics);

            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult originalSnapshot =
                CreateDocumentSnapshot(document, diagnostics);
            if (!originalSnapshot.Success)
                return Failed(diagnostics);
            if (document.PersistenceHash.Value != originalSnapshot.Snapshot.PersistenceHash)
            {
                Error(diagnostics, "DECK_PERSISTENCE_HASH_MISMATCH", "$.payloadJson.persistenceHash", "Deck persistence payload hash does not match its normalized content.");
                return Failed(diagnostics);
            }
            for (int deckIndex = 0; deckIndex < originalSnapshot.Snapshot.Decks.Count; deckIndex++)
            {
                CharacterTestRuntimeAiPlannerPersistedDeck deck = originalSnapshot.Snapshot.Decks[deckIndex];
                if (deck.CatalogNumericId != catalog.CatalogNumericId)
                    Error(diagnostics, "DECK_PERSISTENCE_CATALOG_NUMERIC_ID_MISMATCH", "$.payloadJson.decks[" + deckIndex + "].catalogNumericId", "Persisted deck catalog numeric id does not match the active catalog.");
                if (deck.CatalogNumericIdentityHash != catalog.NumericIdentityHash)
                    Error(diagnostics, "DECK_PERSISTENCE_CATALOG_NUMERIC_IDENTITY_HASH_MISMATCH", "$.payloadJson.decks[" + deckIndex + "].catalogNumericIdentityHash", "Persisted deck catalog numeric mapping does not match the active catalog.");
                for (int cardIndex = 0; cardIndex < deck.CardNumericIds.Count; cardIndex++)
                {
                    if (!CharacterTestAuthoredContentIds.IsActive(
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.CardNumericDomainId,
                        deck.CardNumericIds[cardIndex]))
                        Error(diagnostics, "DECK_PERSISTENCE_CARD_NUMERIC_ID_INVALID", "$.payloadJson.decks[" + deckIndex + "].cardNumericIds[" + cardIndex + "]", "Persisted card numeric id is unknown or retired.");
                }
            }
            if (HasErrors(diagnostics))
                return Failed(diagnostics);

            string[] migratedUnlocked = MigrateIds(
                document.UnlockedCardIds,
                migrationMap,
                diagnostics,
                "$.payloadJson.unlockedCardIds");
            var migratedDecks = new List<CharacterTestRuntimeAiPlannerPersistedDeck>();
            PersistedDeckDocument[] deckDocuments = document.Decks ?? Array.Empty<PersistedDeckDocument>();
            for (int deckIndex = 0; deckIndex < deckDocuments.Length; deckIndex++)
            {
                PersistedDeckDocument deckDocument = deckDocuments[deckIndex];
                if (deckDocument == null)
                {
                    Error(diagnostics, "DECK_PERSISTENCE_DECK_DOCUMENT_MISSING", "$.payloadJson.decks[" + deckIndex + "]", "Persisted deck document is missing.");
                    continue;
                }
                migratedDecks.Add(new CharacterTestRuntimeAiPlannerPersistedDeck(
                    deckDocument.CharacterStableId,
                    deckDocument.CardNumericIds,
                    MigrateIds(
                        deckDocument.StableCardIds,
                        migrationMap,
                        diagnostics,
                        "$.payloadJson.decks[" + deckIndex + "].stableCardIds"),
                    deckDocument.CharacterNumericId,
                    deckDocument.CatalogNumericId,
                    deckDocument.CatalogNumericIdentityHash));
            }
            if (HasErrors(diagnostics))
                return Failed(diagnostics);

            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(migratedUnlocked);
            AddDiagnostics(diagnostics, unlocked.Diagnostics);
            if (!unlocked.Success)
                return Failed(diagnostics);
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult snapshot =
                CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.ValidateAndCreate(
                    unlocked.Collection,
                    migratedDecks);
            AddDiagnostics(diagnostics, snapshot.Diagnostics);
            if (!snapshot.Success)
                return Failed(diagnostics);

            AddUnavailableUnlockWarnings(diagnostics, snapshot.Snapshot.UnlockedCards, catalog, runtimeContractVersion);
            CharacterTestRuntimeAiPlannerResolvedPersistedDeck[] resolved = ResolveDecks(
                diagnostics,
                snapshot.Snapshot,
                catalog,
                runtimeContractVersion);
            return new CharacterTestRuntimeAiPlannerDeckPersistenceRestoreResult(
                snapshot.Snapshot,
                resolved,
                diagnostics.ToArray());
        }

        private static CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult CreateDocumentSnapshot(
            PersistenceDocument document,
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(document.UnlockedCardIds);
            AddDiagnostics(diagnostics, unlocked.Diagnostics);
            if (!unlocked.Success)
                return new CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult(null, null);

            var decks = new List<CharacterTestRuntimeAiPlannerPersistedDeck>();
            PersistedDeckDocument[] deckDocuments = document.Decks ?? Array.Empty<PersistedDeckDocument>();
            for (int deckIndex = 0; deckIndex < deckDocuments.Length; deckIndex++)
            {
                PersistedDeckDocument deckDocument = deckDocuments[deckIndex];
                if (deckDocument == null)
                {
                    Error(diagnostics, "DECK_PERSISTENCE_DECK_DOCUMENT_MISSING", "$.payloadJson.decks[" + deckIndex + "]", "Persisted deck document is missing.");
                    continue;
                }
                decks.Add(new CharacterTestRuntimeAiPlannerPersistedDeck(
                    deckDocument.CharacterStableId,
                    deckDocument.CardNumericIds,
                    deckDocument.StableCardIds,
                    deckDocument.CharacterNumericId,
                    deckDocument.CatalogNumericId,
                    deckDocument.CatalogNumericIdentityHash));
            }
            if (HasErrors(diagnostics))
                return new CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult(null, null);

            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult snapshot =
                CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.ValidateAndCreate(unlocked.Collection, decks);
            AddDiagnostics(diagnostics, snapshot.Diagnostics);
            return snapshot;
        }

        private static CharacterTestRuntimeAiPlannerResolvedPersistedDeck[] ResolveDecks(
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot snapshot,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            int runtimeContractVersion)
        {
            var resolved = new List<CharacterTestRuntimeAiPlannerResolvedPersistedDeck>();
            for (int deckIndex = 0; deckIndex < snapshot.Decks.Count; deckIndex++)
            {
                CharacterTestRuntimeAiPlannerPersistedDeck persisted = snapshot.Decks[deckIndex];
                bool unavailable = false;
                for (int cardIndex = 0; cardIndex < persisted.StableCardIds.Count; cardIndex++)
                {
                    string stableCardId = persisted.StableCardIds[cardIndex];
                    if (catalog.TryResolveLatestCompatible(stableCardId, runtimeContractVersion, out _))
                        continue;
                    Warning(
                        diagnostics,
                        "DECK_PERSISTENCE_DECK_CARD_UNAVAILABLE",
                        "$.decks[" + deckIndex + "].stableCardIds[" + cardIndex + "]",
                        "Persisted deck requires replacement because no compatible published revision is available: " + stableCardId);
                    unavailable = true;
                }
                if (unavailable)
                    continue;

                CharacterTestRuntimeAiPlannerInstalledDeckBuildResult build =
                    CharacterTestRuntimeAiPlannerInstalledDeckRules.ValidateAndCreate(
                        new CharacterTestRuntimeAiPlannerInstalledDeckCandidate(
                            persisted.CharacterStableId,
                            persisted.CardNumericIds,
                            persisted.StableCardIds,
                            persisted.CharacterNumericId),
                        catalog,
                        runtimeContractVersion);
                if (!build.Success)
                {
                    Warning(
                        diagnostics,
                        "DECK_PERSISTENCE_DECK_REQUIRES_REPLACEMENT",
                        "$.decks[" + deckIndex + "]",
                        "Persisted deck no longer satisfies the current published catalog rules and was not activated.");
                    continue;
                }
                AddDeckBuildDiagnostics(diagnostics, build.Diagnostics, deckIndex);
                resolved.Add(new CharacterTestRuntimeAiPlannerResolvedPersistedDeck(
                    persisted.CharacterStableId,
                    build.Deck));
            }
            return resolved.ToArray();
        }

        private static void AddDeckBuildDiagnostics(
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> buildDiagnostics,
            int deckIndex)
        {
            string prefix = "$.decks[" + deckIndex + "]";
            for (int i = 0; i < buildDiagnostics.Count; i++)
            {
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic diagnostic = buildDiagnostics[i];
                string path = diagnostic.Path == "$"
                    ? prefix
                    : prefix + (diagnostic.Path.StartsWith("$.", StringComparison.Ordinal)
                        ? diagnostic.Path.Substring(1)
                        : ".installedDeck");
                diagnostics.Add(new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                    diagnostic.Severity,
                    diagnostic.Code,
                    path,
                    diagnostic.Message));
            }
        }

        private static void AddUnavailableUnlockWarnings(
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlockedCards,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            int runtimeContractVersion)
        {
            for (int i = 0; i < unlockedCards.StableCardIds.Count; i++)
            {
                string stableCardId = unlockedCards.StableCardIds[i];
                if (!catalog.TryResolveLatestCompatible(stableCardId, runtimeContractVersion, out _))
                {
                    Warning(
                        diagnostics,
                        "DECK_PERSISTENCE_UNLOCKED_CARD_UNAVAILABLE",
                        "$.unlockedCardIds[" + i + "]",
                        "Unlocked card ownership was preserved, but no compatible published revision is currently available: " + stableCardId);
                }
            }
        }

        private static string[] MigrateIds(
            string[] source,
            CharacterTestRuntimeAiPlannerStableCardIdMigrationMap migrations,
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            string path)
        {
            string[] ids = source ?? Array.Empty<string>();
            var result = new string[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                string original = ids[i]?.Trim() ?? string.Empty;
                if (!migrations.TryResolve(original, out string resolved, out bool migrated))
                {
                    Error(diagnostics, "DECK_PERSISTENCE_MIGRATION_CYCLE", path + "[" + i + "]", "Stable card id migration contains a cycle.");
                    result[i] = original;
                    continue;
                }
                result[i] = resolved;
                if (migrated)
                {
                    Info(
                        diagnostics,
                        "DECK_PERSISTENCE_STABLE_CARD_ID_MIGRATED",
                        path + "[" + i + "]",
                        "Migrated stable behavior card id from " + original + " to " + resolved + ".");
                }
            }
            return result;
        }

        private static void AddDiagnostics(
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> destination,
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> source)
        {
            for (int i = 0; i < source.Count; i++)
                destination.Add(source[i]);
        }

        private static CharacterTestRuntimeAiPlannerDeckPersistenceRestoreResult Failed(
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            return new CharacterTestRuntimeAiPlannerDeckPersistenceRestoreResult(
                null,
                null,
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

        private static void Info(List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics, string code, string path, string message)
        {
            AddDiagnostic(diagnostics, CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Info, code, path, message);
        }

        private static void Warning(List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics, string code, string path, string message)
        {
            AddDiagnostic(diagnostics, CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning, code, path, message);
        }

        private static void Error(List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics, string code, string path, string message)
        {
            AddDiagnostic(diagnostics, CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error, code, path, message);
        }

        private static void AddDiagnostic(
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity severity,
            string code,
            string path,
            string message)
        {
            diagnostics.Add(new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(severity, code, path, message));
        }

        private sealed class PersistenceDocument
        {
            public string Schema { get; set; }
            public long? PersistenceHash { get; set; }
            public string[] UnlockedCardIds { get; set; }
            public PersistedDeckDocument[] Decks { get; set; }

            public static PersistenceDocument FromSnapshot(
                CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot snapshot)
            {
                var document = new PersistenceDocument
                {
                    Schema = CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.Schema,
                    PersistenceHash = snapshot.PersistenceHash,
                    UnlockedCardIds = new string[snapshot.UnlockedCards.StableCardIds.Count],
                    Decks = new PersistedDeckDocument[snapshot.Decks.Count]
                };
                for (int i = 0; i < document.UnlockedCardIds.Length; i++)
                    document.UnlockedCardIds[i] = snapshot.UnlockedCards.StableCardIds[i];
                for (int i = 0; i < document.Decks.Length; i++)
                    document.Decks[i] = PersistedDeckDocument.FromDeck(snapshot.Decks[i]);
                return document;
            }
        }

        private sealed class PersistedDeckDocument
        {
            public string CharacterStableId { get; set; }
            public int CharacterNumericId { get; set; }
            public int CatalogNumericId { get; set; }
            public long CatalogNumericIdentityHash { get; set; }
            public string[] StableCardIds { get; set; }
            public int[] CardNumericIds { get; set; }

            public static PersistedDeckDocument FromDeck(CharacterTestRuntimeAiPlannerPersistedDeck deck)
            {
                var ids = new string[deck.StableCardIds.Count];
                var numericIds = new int[deck.CardNumericIds.Count];
                for (int i = 0; i < ids.Length; i++)
                    ids[i] = deck.StableCardIds[i];
                for (int i = 0; i < numericIds.Length; i++)
                    numericIds[i] = deck.CardNumericIds[i];
                return new PersistedDeckDocument
                {
                    CharacterStableId = deck.CharacterStableId,
                    CharacterNumericId = deck.CharacterNumericId,
                    CatalogNumericId = deck.CatalogNumericId,
                    CatalogNumericIdentityHash = deck.CatalogNumericIdentityHash,
                    StableCardIds = ids,
                    CardNumericIds = numericIds
                };
            }
        }
    }
}
