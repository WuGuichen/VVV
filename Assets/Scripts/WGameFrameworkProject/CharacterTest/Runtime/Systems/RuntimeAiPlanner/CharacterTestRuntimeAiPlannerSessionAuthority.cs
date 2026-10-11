using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiPlannerSessionAuthorityDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    internal sealed class CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic
    {
        public CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic(
            CharacterTestRuntimeAiPlannerSessionAuthorityDiagnosticSeverity severity,
            string code,
            string path,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public CharacterTestRuntimeAiPlannerSessionAuthorityDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string Path { get; }
        public string Message { get; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerSessionDeckRequest
    {
        public const string Schema = "mx.character-test.runtime-ai-planner.session-deck-request.v2";

        private readonly string[] _stableCardIds;
        private readonly IReadOnlyList<string> _readOnlyStableCardIds;
        private readonly int[] _cardNumericIds;
        private readonly IReadOnlyList<int> _readOnlyCardNumericIds;

        public CharacterTestRuntimeAiPlannerSessionDeckRequest(
            string requesterPeerId,
            string characterStableId,
            int characterNumericId,
            string expectedCatalogId,
            int expectedCatalogRevision,
            int expectedCatalogNumericId,
            long expectedCatalogNumericIdentityHash,
            long expectedCatalogHash,
            IEnumerable<int> cardNumericIds,
            IEnumerable<string> stableCardLabels)
        {
            RequesterPeerId = requesterPeerId?.Trim() ?? string.Empty;
            CharacterStableId = characterStableId?.Trim() ?? string.Empty;
            CharacterNumericId = characterNumericId;
            ExpectedCatalogId = expectedCatalogId?.Trim() ?? string.Empty;
            ExpectedCatalogRevision = expectedCatalogRevision;
            ExpectedCatalogNumericId = expectedCatalogNumericId;
            ExpectedCatalogNumericIdentityHash = expectedCatalogNumericIdentityHash;
            ExpectedCatalogHash = expectedCatalogHash;
            _stableCardIds = CopyIds(stableCardLabels);
            _readOnlyStableCardIds = Array.AsReadOnly(_stableCardIds);
            _cardNumericIds = CopyNumericIds(cardNumericIds);
            _readOnlyCardNumericIds = Array.AsReadOnly(_cardNumericIds);
        }

        public string RequesterPeerId { get; }
        public string CharacterStableId { get; }
        public int CharacterNumericId { get; }
        public string ExpectedCatalogId { get; }
        public int ExpectedCatalogRevision { get; }
        public int ExpectedCatalogNumericId { get; }
        public long ExpectedCatalogNumericIdentityHash { get; }
        public long ExpectedCatalogHash { get; }
        public IReadOnlyList<string> StableCardIds => _readOnlyStableCardIds;
        public IReadOnlyList<string> StableCardLabels => _readOnlyStableCardIds;
        public IReadOnlyList<int> CardNumericIds => _readOnlyCardNumericIds;

        private static string[] CopyIds(IEnumerable<string> source)
        {
            if (source == null)
                return Array.Empty<string>();
            var ids = new List<string>();
            foreach (string id in source)
                ids.Add(id?.Trim() ?? string.Empty);
            return ids.ToArray();
        }

        private static int[] CopyNumericIds(IEnumerable<int> source)
        {
            if (source == null)
                return Array.Empty<int>();
            var ids = new List<int>();
            foreach (int id in source)
                ids.Add(id);
            return ids.ToArray();
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerSessionAuthorityContext
    {
        private readonly string[] _ownedCharacterStableIds;
        private readonly IReadOnlyList<string> _readOnlyOwnedCharacterStableIds;

        public CharacterTestRuntimeAiPlannerSessionAuthorityContext(
            string authorizedRequesterPeerId,
            IEnumerable<string> ownedCharacterStableIds,
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlockedCards,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            int runtimeContractVersion,
            bool selectionLocked = false,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput diagnosticsInput = null)
        {
            AuthorizedRequesterPeerId = authorizedRequesterPeerId?.Trim() ?? string.Empty;
            _ownedCharacterStableIds = CopyAndSort(ownedCharacterStableIds);
            _readOnlyOwnedCharacterStableIds = Array.AsReadOnly(_ownedCharacterStableIds);
            UnlockedCards = unlockedCards;
            Catalog = catalog;
            RuntimeContractVersion = runtimeContractVersion;
            SelectionLocked = selectionLocked;
            DiagnosticsInput = diagnosticsInput;
        }

        public string AuthorizedRequesterPeerId { get; }
        public IReadOnlyList<string> OwnedCharacterStableIds => _readOnlyOwnedCharacterStableIds;
        public CharacterTestRuntimeAiPlannerUnlockedCardCollection UnlockedCards { get; }
        public CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog Catalog { get; }
        public int RuntimeContractVersion { get; }
        public bool SelectionLocked { get; }
        public CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput DiagnosticsInput { get; }

        public bool OwnsCharacter(string characterStableId)
        {
            string normalized = characterStableId?.Trim() ?? string.Empty;
            return Array.BinarySearch(
                _ownedCharacterStableIds,
                normalized,
                StringComparer.Ordinal) >= 0;
        }

        private static string[] CopyAndSort(IEnumerable<string> source)
        {
            if (source == null)
                return Array.Empty<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in source)
            {
                string normalized = value?.Trim() ?? string.Empty;
                if (normalized.Length > 0)
                    unique.Add(normalized);
            }
            string[] result = new string[unique.Count];
            unique.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerPinnedBehaviorCard
    {
        public CharacterTestRuntimeAiPlannerPinnedBehaviorCard(
            string stableCardId,
            int revision,
            long semanticHash,
            long contentHash,
            int cost,
            int numericId = 0)
        {
            StableCardId = stableCardId ?? string.Empty;
            Revision = revision;
            SemanticHash = semanticHash;
            ContentHash = contentHash;
            Cost = cost;
            NumericId = numericId;
        }

        public string StableCardId { get; }
        public int Revision { get; }
        public long SemanticHash { get; }
        public long ContentHash { get; }
        public int Cost { get; }
        public int NumericId { get; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerSessionDeckLock
    {
        public const string Schema = "mx.character-test.runtime-ai-planner.session-deck-lock.v2";

        private readonly CharacterTestRuntimeAiPlannerPinnedBehaviorCard[] _cards;
        private readonly IReadOnlyList<CharacterTestRuntimeAiPlannerPinnedBehaviorCard> _readOnlyCards;

        private CharacterTestRuntimeAiPlannerSessionDeckLock(
            string characterStableId,
            int characterNumericId,
            string catalogId,
            int catalogRevision,
            int catalogNumericId,
            long catalogNumericIdentityHash,
            long catalogHash,
            int runtimeContractVersion,
            CharacterTestRuntimeAiPlannerPinnedBehaviorCard[] cards,
            int totalCost,
            long deckHash,
            long authorityHash)
        {
            CharacterStableId = characterStableId ?? string.Empty;
            CharacterNumericId = characterNumericId;
            CatalogId = catalogId ?? string.Empty;
            CatalogRevision = catalogRevision;
            CatalogNumericId = catalogNumericId;
            CatalogNumericIdentityHash = catalogNumericIdentityHash;
            CatalogHash = catalogHash;
            RuntimeContractVersion = runtimeContractVersion;
            _cards = cards ?? Array.Empty<CharacterTestRuntimeAiPlannerPinnedBehaviorCard>();
            _readOnlyCards = Array.AsReadOnly(_cards);
            TotalCost = totalCost;
            DeckHash = deckHash;
            AuthorityHash = authorityHash;
        }

        public string CharacterStableId { get; }
        public int CharacterNumericId { get; }
        public string CatalogId { get; }
        public int CatalogRevision { get; }
        public int CatalogNumericId { get; }
        public long CatalogNumericIdentityHash { get; }
        public long CatalogHash { get; }
        public int RuntimeContractVersion { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerPinnedBehaviorCard> Cards => _readOnlyCards;
        public int TotalCost { get; }
        public long DeckHash { get; }
        public long AuthorityHash { get; }

        internal static CharacterTestRuntimeAiPlannerSessionDeckLock FromInstalledDeck(
            CharacterTestRuntimeAiPlannerInstalledDeck deck)
        {
            var cards = new CharacterTestRuntimeAiPlannerPinnedBehaviorCard[deck.Cards.Count];
            for (int i = 0; i < cards.Length; i++)
            {
                CharacterTestRuntimeAiPlannerInstalledDeckCard card = deck.Cards[i];
                cards[i] = new CharacterTestRuntimeAiPlannerPinnedBehaviorCard(
                    card.StableCardId,
                    card.Revision,
                    card.SemanticHash,
                    card.ContentHash,
                    card.Cost,
                    card.NumericId);
            }
            return CreateValidated(
                deck.CharacterStableId,
                deck.CharacterNumericId,
                deck.CatalogId,
                deck.CatalogRevision,
                deck.CatalogNumericId,
                deck.CatalogNumericIdentityHash,
                deck.CatalogHash,
                deck.RuntimeContractVersion,
                cards,
                deck.TotalCost,
                deck.DeckHash);
        }

        private static CharacterTestRuntimeAiPlannerSessionDeckLock CreateValidated(
            string characterStableId,
            int characterNumericId,
            string catalogId,
            int catalogRevision,
            int catalogNumericId,
            long catalogNumericIdentityHash,
            long catalogHash,
            int runtimeContractVersion,
            CharacterTestRuntimeAiPlannerPinnedBehaviorCard[] cards,
            int totalCost,
            long deckHash)
        {
            CharacterTestRuntimeAiPlannerPinnedBehaviorCard[] copied =
                CopyCards(cards);
            long authorityHash = ComputeAuthorityHash(
                characterStableId,
                characterNumericId,
                catalogId,
                catalogRevision,
                catalogNumericId,
                catalogNumericIdentityHash,
                catalogHash,
                runtimeContractVersion,
                copied,
                totalCost,
                deckHash);
            return new CharacterTestRuntimeAiPlannerSessionDeckLock(
                characterStableId,
                characterNumericId,
                catalogId,
                catalogRevision,
                catalogNumericId,
                catalogNumericIdentityHash,
                catalogHash,
                runtimeContractVersion,
                copied,
                totalCost,
                deckHash,
                authorityHash);
        }

        internal static long ComputeAuthorityHash(
            string characterStableId,
            int characterNumericId,
            string catalogId,
            int catalogRevision,
            int catalogNumericId,
            long catalogNumericIdentityHash,
            long catalogHash,
            int runtimeContractVersion,
            IReadOnlyList<CharacterTestRuntimeAiPlannerPinnedBehaviorCard> cards,
            int totalCost,
            long deckHash)
        {
            var hash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerSessionAuthority, 2);
            hash.AddInt(1, RuntimeHashSchemaIds.RuntimeAiPlannerSessionAuthority);
            hash.AddInt(2, characterNumericId);
            hash.AddInt(3, catalogNumericId);
            hash.AddLong(17, catalogNumericIdentityHash);
            hash.AddInt(4, catalogRevision);
            hash.AddLong(5, catalogHash);
            hash.AddInt(6, runtimeContractVersion);
            hash.AddLong(7, CharacterTestRuntimeAiPlannerInstalledDeckFallbacks.ContractHash);
            hash.AddInt(8, cards.Count);
            hash.AddInt(9, totalCost);
            hash.AddLong(10, deckHash);
            for (int i = 0; i < cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPinnedBehaviorCard card = cards[i];
                hash.AddInt(11, i);
                hash.AddInt(12, card.NumericId);
                hash.AddInt(13, card.Revision);
                hash.AddLong(14, card.SemanticHash);
                hash.AddLong(15, card.ContentHash);
                hash.AddInt(16, card.Cost);
            }
            return hash.ToHash();
        }

        private static CharacterTestRuntimeAiPlannerPinnedBehaviorCard[] CopyCards(
            CharacterTestRuntimeAiPlannerPinnedBehaviorCard[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<CharacterTestRuntimeAiPlannerPinnedBehaviorCard>();
            var result = new CharacterTestRuntimeAiPlannerPinnedBehaviorCard[source.Length];
            Array.Copy(source, result, source.Length);
            return result;
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerSessionAuthorityResult
    {
        public CharacterTestRuntimeAiPlannerSessionAuthorityResult(
            CharacterTestRuntimeAiPlannerSessionDeckLock sessionLock,
            CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic[] diagnostics)
        {
            SessionLock = sessionLock;
            Diagnostics = Array.AsReadOnly(
                diagnostics ?? Array.Empty<CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic>());
        }

        public CharacterTestRuntimeAiPlannerSessionDeckLock SessionLock { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic> Diagnostics { get; }
        public bool Success => SessionLock != null && !HasErrors(Diagnostics);

        private static bool HasErrors(
            IReadOnlyList<CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == CharacterTestRuntimeAiPlannerSessionAuthorityDiagnosticSeverity.Error)
                    return true;
            }
            return false;
        }
    }

    internal static class CharacterTestRuntimeAiPlannerSessionAuthority
    {
        /// <remarks>LowFreqAlloc: pre-session authority validation only.</remarks>
        public static CharacterTestRuntimeAiPlannerSessionAuthorityResult ValidateAndLock(
            CharacterTestRuntimeAiPlannerSessionDeckRequest request,
            CharacterTestRuntimeAiPlannerSessionAuthorityContext context)
        {
            var diagnostics = new List<CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic>();
            if (request == null)
                Error(diagnostics, "SESSION_DECK_REQUEST_MISSING", "$", "Session deck request is required.");
            if (context == null)
                Error(diagnostics, "SESSION_DECK_AUTHORITY_CONTEXT_MISSING", "$.authority", "Session authority context is required.");
            if (request == null || context == null)
                return Failed(diagnostics);

            if (request.RequesterPeerId.Length == 0)
                Error(diagnostics, "SESSION_DECK_REQUESTER_MISSING", "$.requesterPeerId", "Requester peer id is required.");
            if (context.AuthorizedRequesterPeerId.Length == 0)
                Error(diagnostics, "SESSION_DECK_AUTHORIZED_REQUESTER_MISSING", "$.authority.requesterPeerId", "Authorized requester peer id is required.");
            else if (!string.Equals(request.RequesterPeerId, context.AuthorizedRequesterPeerId, StringComparison.Ordinal))
                Error(diagnostics, "SESSION_DECK_REQUESTER_MISMATCH", "$.requesterPeerId", "Requester peer id does not match the authority context.");
            if (request.CharacterStableId.Length == 0)
                Error(diagnostics, "SESSION_DECK_CHARACTER_ID_MISSING", "$.characterStableId", "Character stable id is required.");
            if (request.CharacterNumericId <= 0)
                Error(diagnostics, "SESSION_DECK_CHARACTER_NUMERIC_ID_INVALID", "$.characterNumericId", "Character numeric id must be positive.");
            if (request.ExpectedCatalogNumericId <= 0
                || !CharacterTestAuthoredContentIds.IsActive(
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.CatalogNumericDomainId,
                    request.ExpectedCatalogNumericId))
                Error(diagnostics, "SESSION_DECK_CATALOG_NUMERIC_ID_INVALID", "$.expectedCatalogNumericId", "Catalog numeric id is unknown or retired.");
            if (context.SelectionLocked)
                Error(diagnostics, "SESSION_DECK_SELECTION_LOCKED", "$", "Behavior deck selection is already locked for this session.");
            if (context.UnlockedCards == null)
                Error(diagnostics, "SESSION_DECK_UNLOCKED_COLLECTION_MISSING", "$.authority.unlockedCards", "Authoritative unlocked behavior cards are required.");
            if (context.Catalog == null)
                Error(diagnostics, "SESSION_DECK_CATALOG_MISSING", "$.authority.catalog", "Authoritative Published Behavior Card catalog is required.");
            if (context.RuntimeContractVersion <= 0)
                Error(diagnostics, "SESSION_DECK_RUNTIME_VERSION_INVALID", "$.authority.runtimeContractVersion", "Runtime contract version must be positive.");
            if (!context.OwnsCharacter(request.CharacterStableId))
                Error(diagnostics, "SESSION_DECK_CHARACTER_NOT_OWNED", "$.characterStableId", "Requester is not authorized to configure this character.");

            ValidateCatalogIdentity(request, context.Catalog, diagnostics);
            ValidateCardNumericIds(request, context.Catalog, context.RuntimeContractVersion, diagnostics);
            ValidateUnlockedCards(request, context.UnlockedCards, diagnostics);
            if (HasErrors(diagnostics))
                return Failed(diagnostics);

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult build =
                CharacterTestRuntimeAiPlannerInstalledDeckRules.ValidateAndCreate(
                    new CharacterTestRuntimeAiPlannerInstalledDeckCandidate(
                        request.CharacterStableId,
                        request.CardNumericIds,
                        request.StableCardIds,
                        request.CharacterNumericId),
                    context.Catalog,
                    context.RuntimeContractVersion,
                    context.DiagnosticsInput);
            CopyDeckDiagnostics(diagnostics, build.Diagnostics);
            if (!build.Success)
                return Failed(diagnostics);

            return new CharacterTestRuntimeAiPlannerSessionAuthorityResult(
                CharacterTestRuntimeAiPlannerSessionDeckLock.FromInstalledDeck(build.Deck),
                diagnostics.ToArray());
        }

        private static void ValidateCatalogIdentity(
            CharacterTestRuntimeAiPlannerSessionDeckRequest request,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            List<CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic> diagnostics)
        {
            if (catalog == null)
                return;
            if (!string.Equals(request.ExpectedCatalogId, catalog.CatalogId, StringComparison.Ordinal))
                Error(diagnostics, "SESSION_DECK_CATALOG_ID_MISMATCH", "$.expectedCatalogId", "Client catalog id does not match authority catalog.");
            if (request.ExpectedCatalogRevision != catalog.Revision)
                Error(diagnostics, "SESSION_DECK_CATALOG_REVISION_MISMATCH", "$.expectedCatalogRevision", "Client catalog revision does not match authority catalog.");
            if (request.ExpectedCatalogNumericId != catalog.CatalogNumericId)
                Error(diagnostics, "SESSION_DECK_CATALOG_NUMERIC_ID_MISMATCH", "$.expectedCatalogNumericId", "Client catalog numeric id does not match authority catalog.");
            if (request.ExpectedCatalogNumericIdentityHash != catalog.NumericIdentityHash)
                Error(diagnostics, "SESSION_DECK_CATALOG_NUMERIC_IDENTITY_HASH_MISMATCH", "$.expectedCatalogNumericIdentityHash", "Client catalog numeric mapping does not match authority catalog.");
            if (request.ExpectedCatalogHash != catalog.CatalogHash)
                Error(diagnostics, "SESSION_DECK_CATALOG_HASH_MISMATCH", "$.expectedCatalogHash", "Client catalog hash does not match authority catalog.");
        }

        private static void ValidateCardNumericIds(
            CharacterTestRuntimeAiPlannerSessionDeckRequest request,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            int runtimeContractVersion,
            List<CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic> diagnostics)
        {
            if (request.CardNumericIds.Count != request.StableCardIds.Count)
            {
                Error(diagnostics, "SESSION_DECK_CARD_NUMERIC_ID_COUNT_MISMATCH", "$.cardNumericIds", "Card numeric ids and stable card labels must have the same slot count.");
                return;
            }
            var unique = new HashSet<int>();
            for (int i = 0; i < request.CardNumericIds.Count; i++)
            {
                int numericId = request.CardNumericIds[i];
                string path = "$.cardNumericIds[" + i + "]";
                if (numericId <= 0 || !CharacterTestAuthoredContentIds.IsActive(CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.CardNumericDomainId, numericId))
                    Error(diagnostics, "SESSION_DECK_CARD_NUMERIC_ID_INVALID", path, "Card numeric id is unknown or retired.");
                else if (!unique.Add(numericId))
                    Error(diagnostics, "SESSION_DECK_CARD_NUMERIC_ID_DUPLICATE", path, "Card numeric ids must be unique.");
                if (catalog.TryResolveLatestCompatible(numericId, runtimeContractVersion, out CharacterTestRuntimeAiPlannerPublishedBehaviorCard card)
                    && request.StableCardIds[i].Length > 0
                    && !string.Equals(request.StableCardIds[i], card.StableCardId, StringComparison.Ordinal))
                    Error(diagnostics, "SESSION_DECK_CARD_LABEL_MISMATCH", "$.stableCardLabels[" + i + "]", "Stable card label does not match its numeric id.");
            }
        }

        private static void ValidateUnlockedCards(
            CharacterTestRuntimeAiPlannerSessionDeckRequest request,
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlockedCards,
            List<CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic> diagnostics)
        {
            if (unlockedCards == null)
                return;
            for (int i = 0; i < request.StableCardIds.Count; i++)
            {
                string stableCardId = request.StableCardIds[i];
                if (stableCardId.Length > 0 && !unlockedCards.Contains(stableCardId))
                {
                    Error(
                        diagnostics,
                        "SESSION_DECK_CARD_NOT_UNLOCKED",
                        "$.stableCardIds[" + i + "]",
                        "Behavior card is not unlocked for this authority account: " + stableCardId);
                }
            }
        }

        private static void CopyDeckDiagnostics(
            List<CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic> target,
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> source)
        {
            for (int i = 0; i < source.Count; i++)
            {
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic diagnostic = source[i];
                target.Add(new CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic(
                    ConvertSeverity(diagnostic.Severity),
                    diagnostic.Code,
                    diagnostic.Path,
                    diagnostic.Message));
            }
        }

        private static CharacterTestRuntimeAiPlannerSessionAuthorityDiagnosticSeverity ConvertSeverity(
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Info:
                    return CharacterTestRuntimeAiPlannerSessionAuthorityDiagnosticSeverity.Info;
                case CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning:
                    return CharacterTestRuntimeAiPlannerSessionAuthorityDiagnosticSeverity.Warning;
                default:
                    return CharacterTestRuntimeAiPlannerSessionAuthorityDiagnosticSeverity.Error;
            }
        }

        private static bool HasErrors(
            IReadOnlyList<CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == CharacterTestRuntimeAiPlannerSessionAuthorityDiagnosticSeverity.Error)
                    return true;
            }
            return false;
        }

        private static CharacterTestRuntimeAiPlannerSessionAuthorityResult Failed(
            List<CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic> diagnostics)
        {
            return new CharacterTestRuntimeAiPlannerSessionAuthorityResult(null, diagnostics.ToArray());
        }

        private static void Error(
            List<CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            diagnostics.Add(new CharacterTestRuntimeAiPlannerSessionAuthorityDiagnostic(
                CharacterTestRuntimeAiPlannerSessionAuthorityDiagnosticSeverity.Error,
                code,
                path,
                message));
        }
    }

    internal static class CharacterTestRuntimeAiPlannerSessionAuthorityHash
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static long CombineGameplayStateHash(long gameplayStateHash, long sessionAuthorityHash)
        {
            return RuntimeFixedLayoutHash64.Hash16(gameplayStateHash, sessionAuthorityHash,
                CharacterTestSessionHashContract.DomainSeed);
        }
    }
}
