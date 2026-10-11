using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiPlannerInstalledDeckFallbackKind
    {
        TargetNearestEnemy = 0,
        Idle = 1
    }

    internal sealed class CharacterTestRuntimeAiPlannerInstalledDeckFallback
    {
        public CharacterTestRuntimeAiPlannerInstalledDeckFallback(
            CharacterTestRuntimeAiPlannerInstalledDeckFallbackKind kind,
            string stableId)
        {
            Kind = kind;
            StableId = stableId ?? string.Empty;
        }

        public CharacterTestRuntimeAiPlannerInstalledDeckFallbackKind Kind { get; }
        public string StableId { get; }
        public int Cost => 0;
        public bool ConsumesSlot => false;
    }

    internal static class CharacterTestRuntimeAiPlannerInstalledDeckFallbacks
    {
        private static readonly IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckFallback> Items =
            Array.AsReadOnly(new[]
            {
                new CharacterTestRuntimeAiPlannerInstalledDeckFallback(
                    CharacterTestRuntimeAiPlannerInstalledDeckFallbackKind.TargetNearestEnemy,
                    "system.runtime-ai-planner.fallback.target-nearest-enemy"),
                new CharacterTestRuntimeAiPlannerInstalledDeckFallback(
                    CharacterTestRuntimeAiPlannerInstalledDeckFallbackKind.Idle,
                    "system.runtime-ai-planner.fallback.idle")
            });

        public static IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckFallback> All => Items;

        public static long ContractHash => ComputeContractHash(Items);

        internal static long ComputeContractHash(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckFallback> fallbacks)
        {
            var hash = new RuntimeContentHashAccumulator();
            int count = fallbacks != null ? fallbacks.Count : 0;
            hash.AddInt("installedDeck.fallbackCount", count);
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiPlannerInstalledDeckFallback fallback = fallbacks[i];
                hash.AddInt("installedDeck.fallbackIndex", i);
                hash.AddInt("installedDeck.fallbackKind", (int)fallback.Kind);
                hash.AddStringStable("installedDeck.fallbackStableId", fallback.StableId);
                hash.AddInt("installedDeck.fallbackCost", fallback.Cost);
                hash.AddInt("installedDeck.fallbackConsumesSlot", fallback.ConsumesSlot ? 1 : 0);
            }
            return hash.ToHash();
        }
    }

    internal enum CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    internal sealed class CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic
    {
        public CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity severity,
            string code,
            string path,
            string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string Path { get; }
        public string Message { get; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput
    {
        private readonly IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> _characterCapabilities;
        private readonly IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> _providedDeckCapabilities;

        /// <remarks>LowFreqAlloc: explicit deck-diagnostics projection input only.</remarks>
        public CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput(
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorCapability> characterCapabilities,
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorCapability> providedDeckCapabilities)
        {
            _characterCapabilities = Array.AsReadOnly(CopyCapabilities(characterCapabilities));
            _providedDeckCapabilities = Array.AsReadOnly(CopyCapabilities(providedDeckCapabilities));
        }

        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> CharacterCapabilities =>
            _characterCapabilities;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> ProvidedDeckCapabilities =>
            _providedDeckCapabilities;

        public bool CharacterHas(CharacterTestRuntimeAiPlannerBehaviorCapability capability)
        {
            return Contains(_characterCapabilities, capability);
        }

        public bool DeckProvides(CharacterTestRuntimeAiPlannerBehaviorCapability capability)
        {
            return Contains(_providedDeckCapabilities, capability);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCapability[] CopyCapabilities(
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorCapability> source)
        {
            if (source == null)
                return Array.Empty<CharacterTestRuntimeAiPlannerBehaviorCapability>();

            var unique = new HashSet<CharacterTestRuntimeAiPlannerBehaviorCapability>();
            foreach (CharacterTestRuntimeAiPlannerBehaviorCapability capability in source)
            {
                if (capability != CharacterTestRuntimeAiPlannerBehaviorCapability.None)
                    unique.Add(capability);
            }
            CharacterTestRuntimeAiPlannerBehaviorCapability[] result =
                new CharacterTestRuntimeAiPlannerBehaviorCapability[unique.Count];
            unique.CopyTo(result);
            Array.Sort(result);
            return result;
        }

        private static bool Contains(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> capabilities,
            CharacterTestRuntimeAiPlannerBehaviorCapability target)
        {
            for (int i = 0; i < capabilities.Count; i++)
            {
                if (capabilities[i] == target)
                    return true;
            }
            return false;
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerInstalledDeckCandidate
    {
        private readonly IReadOnlyList<string> _stableCardIds;
        private readonly IReadOnlyList<int> _cardNumericIds;

        /// <remarks>LowFreqAlloc: player/Lab deck authoring and bootstrap only.</remarks>
        public CharacterTestRuntimeAiPlannerInstalledDeckCandidate(
            string characterStableId,
            IEnumerable<string> stableCardIds,
            int characterNumericId)
            : this(characterStableId, null, stableCardIds, characterNumericId)
        {
        }

        public CharacterTestRuntimeAiPlannerInstalledDeckCandidate(
            string characterStableId,
            IEnumerable<int> cardNumericIds,
            IEnumerable<string> stableCardIds,
            int characterNumericId)
        {
            CharacterStableId = characterStableId?.Trim() ?? string.Empty;
            CharacterNumericId = characterNumericId;
            _stableCardIds = Array.AsReadOnly(CopyIds(stableCardIds));
            _cardNumericIds = Array.AsReadOnly(CopyNumericIds(cardNumericIds));
        }

        public string CharacterStableId { get; }
        public int CharacterNumericId { get; }
        public IReadOnlyList<string> StableCardIds => _stableCardIds;
        public IReadOnlyList<int> CardNumericIds => _cardNumericIds;

        private static string[] CopyIds(IEnumerable<string> source)
        {
            if (source == null)
                return Array.Empty<string>();

            var result = new List<string>();
            foreach (string value in source)
                result.Add(value?.Trim() ?? string.Empty);
            return result.ToArray();
        }

        private static int[] CopyNumericIds(IEnumerable<int> source)
        {
            if (source == null)
                return Array.Empty<int>();
            var result = new List<int>();
            foreach (int value in source)
                result.Add(value);
            return result.ToArray();
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerInstalledDeckCard
    {
        public CharacterTestRuntimeAiPlannerInstalledDeckCard(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard)
        {
            PublishedCard = publishedCard;
        }

        public CharacterTestRuntimeAiPlannerPublishedBehaviorCard PublishedCard { get; }
        public string StableCardId => PublishedCard.StableCardId;
        public int Revision => PublishedCard.Revision;
        public int Cost => PublishedCard.Metadata.Cost;
        public int NumericId => PublishedCard.NumericId;
        public long SemanticHash => PublishedCard.SemanticHash;
        public long ContentHash => PublishedCard.ContentHash;
    }

    internal sealed class CharacterTestRuntimeAiPlannerInstalledDeck
    {
        public const string Schema = "mx.character-test.runtime-ai-planner.installed-deck.v2";

        private readonly CharacterTestRuntimeAiPlannerInstalledDeckCard[] _cards;
        private readonly IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckCard> _readOnlyCards;

        private CharacterTestRuntimeAiPlannerInstalledDeck(
            string characterStableId,
            int characterNumericId,
            string catalogId,
            int catalogNumericId,
            long catalogNumericIdentityHash,
            int catalogRevision,
            long catalogHash,
            int runtimeContractVersion,
            CharacterTestRuntimeAiPlannerInstalledDeckCard[] cards,
            int totalCost,
            long deckHash)
        {
            CharacterStableId = characterStableId;
            CharacterNumericId = characterNumericId;
            CatalogId = catalogId;
            CatalogNumericId = catalogNumericId;
            CatalogNumericIdentityHash = catalogNumericIdentityHash;
            CatalogRevision = catalogRevision;
            CatalogHash = catalogHash;
            RuntimeContractVersion = runtimeContractVersion;
            _cards = cards ?? Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckCard>();
            _readOnlyCards = Array.AsReadOnly(_cards);
            TotalCost = totalCost;
            DeckHash = deckHash;
        }

        public string CharacterStableId { get; }
        public int CharacterNumericId { get; }
        public string CatalogId { get; }
        public int CatalogNumericId { get; }
        public long CatalogNumericIdentityHash { get; }
        public int CatalogRevision { get; }
        public long CatalogHash { get; }
        public int RuntimeContractVersion { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckCard> Cards => _readOnlyCards;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckFallback> Fallbacks =>
            CharacterTestRuntimeAiPlannerInstalledDeckFallbacks.All;
        public int SlotCount => _cards.Length;
        public int TotalCost { get; }
        public long DeckHash { get; }

        /// <remarks>LowFreqAlloc: player/Lab deck validation and bootstrap only.</remarks>
        internal static CharacterTestRuntimeAiPlannerInstalledDeckBuildResult ValidateAndCreate(
            CharacterTestRuntimeAiPlannerInstalledDeckCandidate candidate,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            int runtimeContractVersion,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput diagnosticsInput)
        {
            var diagnostics = new List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>();
            if (candidate == null)
                Error(diagnostics, "INSTALLED_DECK_CANDIDATE_MISSING", "$", "Installed deck candidate is required.");
            if (catalog == null)
                Error(diagnostics, "INSTALLED_DECK_CATALOG_MISSING", "$.catalog", "Published behavior card catalog is required.");
            if (runtimeContractVersion <= 0)
                Error(diagnostics, "INSTALLED_DECK_RUNTIME_VERSION_INVALID", "$.runtimeContractVersion", "Runtime contract version must be positive.");
            if (candidate == null || catalog == null || runtimeContractVersion <= 0)
                return Failed(diagnostics);

            if (candidate.CharacterStableId.Length == 0)
                Error(diagnostics, "INSTALLED_DECK_CHARACTER_ID_MISSING", "$.characterStableId", "Character stable id is required.");
            if (candidate.CharacterNumericId <= 0)
                Error(diagnostics, "INSTALLED_DECK_CHARACTER_NUMERIC_ID_INVALID", "$.characterNumericId", "Character numeric id must be positive.");
            if (candidate.StableCardIds.Count > CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumSlots)
                Error(diagnostics, "INSTALLED_DECK_SLOT_LIMIT_EXCEEDED", "$.stableCardIds", "Installed deck cannot contain more than 6 cards.");

            bool hasNumericIds = candidate.CardNumericIds.Count > 0;
            if (hasNumericIds && candidate.CardNumericIds.Count != candidate.StableCardIds.Count)
                Error(diagnostics, "INSTALLED_DECK_CARD_NUMERIC_ID_COUNT_MISMATCH", "$.cardNumericIds", "Card numeric ids and stable card labels must have the same slot count.");
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            var uniqueNumericIds = new HashSet<int>();
            var cards = new List<CharacterTestRuntimeAiPlannerInstalledDeckCard>(candidate.StableCardIds.Count);
            int totalCost = 0;
            for (int i = 0; i < candidate.StableCardIds.Count; i++)
            {
                string stableCardId = candidate.StableCardIds[i];
                string path = "$.stableCardIds[" + i + "]";
                int numericCardId = hasNumericIds && i < candidate.CardNumericIds.Count
                    ? candidate.CardNumericIds[i]
                    : 0;
                if (hasNumericIds && numericCardId <= 0)
                {
                    Error(diagnostics, "INSTALLED_DECK_CARD_NUMERIC_ID_INVALID", "$.cardNumericIds[" + i + "]", "Published behavior card numeric id must be positive.");
                    continue;
                }
                if (hasNumericIds && !uniqueNumericIds.Add(numericCardId))
                {
                    Error(diagnostics, "INSTALLED_DECK_CARD_NUMERIC_ID_DUPLICATE", "$.cardNumericIds[" + i + "]", "The same numeric behavior card cannot be installed twice.");
                    continue;
                }
                if (stableCardId.Length == 0)
                {
                    if (!hasNumericIds)
                    {
                        Error(diagnostics, "INSTALLED_DECK_CARD_ID_MISSING", path, "Published behavior card stable id is required.");
                        continue;
                    }
                }
                if (stableCardId.Length > 0 && !uniqueIds.Add(stableCardId))
                {
                    Error(diagnostics, "INSTALLED_DECK_CARD_DUPLICATE", path, "The same stable behavior card cannot be installed twice.");
                    continue;
                }
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard card;
                bool resolved = hasNumericIds
                    ? catalog.TryResolveLatestCompatible(numericCardId, runtimeContractVersion, out card)
                    : catalog.TryResolveLatestCompatible(stableCardId, runtimeContractVersion, out card);
                if (!resolved)
                {
                    Error(diagnostics, "INSTALLED_DECK_CARD_UNAVAILABLE", hasNumericIds ? "$.cardNumericIds[" + i + "]" : path, "No compatible published behavior card revision is available: " + (hasNumericIds ? numericCardId.ToString() : stableCardId));
                    continue;
                }
                if (hasNumericIds && stableCardId.Length > 0 && !string.Equals(stableCardId, card.StableCardId, StringComparison.Ordinal))
                {
                    Error(diagnostics, "INSTALLED_DECK_CARD_LABEL_MISMATCH", path, "Stable behavior card label does not match its numeric identity.");
                    continue;
                }
                cards.Add(new CharacterTestRuntimeAiPlannerInstalledDeckCard(card));
                totalCost += card.Metadata.Cost;
            }

            if (totalCost > CharacterTestRuntimeAiPlannerInstalledDeckRules.MaximumCost)
                Error(diagnostics, "INSTALLED_DECK_COST_LIMIT_EXCEEDED", "$.stableCardIds", "Installed deck cost cannot exceed 10.");
            if (HasErrors(diagnostics))
                return Failed(diagnostics);

            CharacterTestRuntimeAiPlannerInstalledDeckCard[] normalizedCards = cards.ToArray();
            Array.Sort(normalizedCards, CompareCards);
            AddCapabilityDiagnostics(diagnostics, normalizedCards, diagnosticsInput);
            return new CharacterTestRuntimeAiPlannerInstalledDeckBuildResult(
                new CharacterTestRuntimeAiPlannerInstalledDeck(
                    candidate.CharacterStableId,
                    candidate.CharacterNumericId,
                    catalog.CatalogId,
                    catalog.CatalogNumericId,
                    catalog.NumericIdentityHash,
                    catalog.Revision,
                    catalog.CatalogHash,
                    runtimeContractVersion,
                    normalizedCards,
                    totalCost,
                    ComputeHash(
                        candidate.CharacterStableId,
                        candidate.CharacterNumericId,
                        catalog,
                        runtimeContractVersion,
                        normalizedCards,
                        totalCost,
                        CharacterTestRuntimeAiPlannerInstalledDeckFallbacks.ContractHash)),
                diagnostics.ToArray());
        }

        private static int CompareCards(
            CharacterTestRuntimeAiPlannerInstalledDeckCard left,
            CharacterTestRuntimeAiPlannerInstalledDeckCard right)
        {
            int numeric = left.NumericId.CompareTo(right.NumericId);
            return numeric != 0
                ? numeric
                : string.Compare(left.StableCardId, right.StableCardId, StringComparison.Ordinal);
        }

        internal static long ComputeHash(
            string characterStableId,
            int characterNumericId,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            int runtimeContractVersion,
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckCard> cards,
            int totalCost,
            long fallbackContractHash)
        {
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("installedDeck.schema", Schema);
            hash.AddStringStable("installedDeck.characterStableId", characterStableId);
            hash.AddInt("installedDeck.characterNumericId", characterNumericId);
            hash.AddStringStable("installedDeck.catalogId", catalog.CatalogId);
            hash.AddInt("installedDeck.catalogNumericId", catalog.CatalogNumericId);
            hash.AddLong("installedDeck.catalogNumericIdentityHash", catalog.NumericIdentityHash);
            hash.AddInt("installedDeck.catalogRevision", catalog.Revision);
            hash.AddLong("installedDeck.catalogHash", catalog.CatalogHash);
            hash.AddInt("installedDeck.runtimeContractVersion", runtimeContractVersion);
            hash.AddLong("installedDeck.fallbackContractHash", fallbackContractHash);
            hash.AddInt("installedDeck.cardCount", cards.Count);
            hash.AddInt("installedDeck.totalCost", totalCost);
            for (int i = 0; i < cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerInstalledDeckCard card = cards[i];
                hash.AddInt("installedDeck.cardIndex", i);
                hash.AddStringStable("installedDeck.cardStableId", card.StableCardId);
                hash.AddInt("installedDeck.cardNumericId", card.NumericId);
                hash.AddInt("installedDeck.cardRevision", card.Revision);
                hash.AddLong("installedDeck.cardContentHash", card.ContentHash);
                hash.AddInt("installedDeck.cardCost", card.Cost);
            }
            return hash.ToHash();
        }

        private static CharacterTestRuntimeAiPlannerInstalledDeckBuildResult Failed(
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            return new CharacterTestRuntimeAiPlannerInstalledDeckBuildResult(null, diagnostics.ToArray());
        }

        private static void Error(
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            AddDiagnostic(
                diagnostics,
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                code,
                path,
                message);
        }

        private static void Warning(
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            AddDiagnostic(
                diagnostics,
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning,
                code,
                path,
                message);
        }

        private static void AddDiagnostic(
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity severity,
            string code,
            string path,
            string message)
        {
            diagnostics.Add(new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                severity,
                code,
                path,
                message));
        }

        private static bool HasErrors(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error)
                    return true;
            }
            return false;
        }

        private static void AddCapabilityDiagnostics(
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            CharacterTestRuntimeAiPlannerInstalledDeckCard[] cards,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput input)
        {
            if (cards.Length == 0)
            {
                Warning(
                    diagnostics,
                    "INSTALLED_DECK_FALLBACK_ONLY",
                    "$.stableCardIds",
                    "The deck is legal but relies only on the free targeting and idle fallbacks.");
            }

            if (input == null)
                return;

            AddMissingCapabilityWarning(
                diagnostics,
                input,
                CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting,
                "INSTALLED_DECK_TARGETING_MISSING",
                "Targeting");
            AddMissingCapabilityWarning(
                diagnostics,
                input,
                CharacterTestRuntimeAiPlannerBehaviorCapability.Engage,
                "INSTALLED_DECK_ENGAGE_MISSING",
                "engage");
            AddMissingCapabilityWarning(
                diagnostics,
                input,
                CharacterTestRuntimeAiPlannerBehaviorCapability.Damage,
                "INSTALLED_DECK_DAMAGE_MISSING",
                "damage");
            AddMissingCapabilityWarning(
                diagnostics,
                input,
                CharacterTestRuntimeAiPlannerBehaviorCapability.Kite,
                "INSTALLED_DECK_DISENGAGE_MISSING",
                "disengage");
            AddMissingCapabilityWarning(
                diagnostics,
                input,
                CharacterTestRuntimeAiPlannerBehaviorCapability.Defense,
                "INSTALLED_DECK_SURVIVAL_MISSING",
                "survival");

            for (int cardIndex = 0; cardIndex < cards.Length; cardIndex++)
            {
                CharacterTestRuntimeAiPlannerInstalledDeckCard card = cards[cardIndex];
                IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> required =
                    card.PublishedCard.Metadata.RequiredCapabilities;
                for (int capabilityIndex = 0; capabilityIndex < required.Count; capabilityIndex++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorCapability capability = required[capabilityIndex];
                    if (input.CharacterHas(capability))
                        continue;
                    Warning(
                        diagnostics,
                        "INSTALLED_DECK_CHARACTER_CAPABILITY_MISMATCH",
                        "$.cards[" + cardIndex + "].requiredCapabilities[" + capabilityIndex + "]",
                        "Character does not provide the capability required by "
                            + card.StableCardId
                            + ": "
                            + CharacterTestRuntimeAiPlannerBehaviorCapabilityAnalyzer.FormatCapabilityLabel(capability));
                }
            }
        }

        private static void AddMissingCapabilityWarning(
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput input,
            CharacterTestRuntimeAiPlannerBehaviorCapability capability,
            string code,
            string label)
        {
            if (input.DeckProvides(capability))
                return;
            Warning(
                diagnostics,
                code,
                "$.providedDeckCapabilities",
                "The deck is legal but has no declared " + label + " capability.");
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerInstalledDeckBuildResult
    {
        public CharacterTestRuntimeAiPlannerInstalledDeckBuildResult(
            CharacterTestRuntimeAiPlannerInstalledDeck deck,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] diagnostics)
        {
            Deck = deck;
            Diagnostics = Array.AsReadOnly(
                diagnostics ?? Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>());
        }

        public CharacterTestRuntimeAiPlannerInstalledDeck Deck { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> Diagnostics { get; }
        public bool Success => Deck != null && !HasErrors(Diagnostics);

        private static bool HasErrors(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error)
                    return true;
            }
            return false;
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerInstalledDeckPresetDefinition
    {
        private readonly IReadOnlyList<string> _stableCardIds;

        /// <remarks>LowFreqAlloc: preset catalog/bootstrap only.</remarks>
        public CharacterTestRuntimeAiPlannerInstalledDeckPresetDefinition(
            int loadoutId,
            int variantId,
            IEnumerable<string> stableCardIds)
        {
            LoadoutId = loadoutId;
            VariantId = variantId;
            var ids = new List<string>();
            if (stableCardIds != null)
            {
                foreach (string stableCardId in stableCardIds)
                    ids.Add(stableCardId?.Trim() ?? string.Empty);
            }
            _stableCardIds = Array.AsReadOnly(ids.ToArray());
        }

        public int LoadoutId { get; }
        public int VariantId { get; }
        public IReadOnlyList<string> StableCardIds => _stableCardIds;
    }

    internal static class CharacterTestRuntimeAiPlannerInstalledDeckRules
    {
        public const int MaximumSlots = 6;
        public const int MaximumCost = 10;

        /// <remarks>LowFreqAlloc: player/Lab deck validation and bootstrap only.</remarks>
        public static CharacterTestRuntimeAiPlannerInstalledDeckBuildResult ValidateAndCreate(
            CharacterTestRuntimeAiPlannerInstalledDeckCandidate candidate,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            int runtimeContractVersion,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput diagnosticsInput = null)
        {
            return CharacterTestRuntimeAiPlannerInstalledDeck.ValidateAndCreate(
                candidate,
                catalog,
                runtimeContractVersion,
                diagnosticsInput);
        }

        /// <remarks>LowFreqAlloc: explicit single-card deck edit only.</remarks>
        public static CharacterTestRuntimeAiPlannerInstalledDeckBuildResult TryInstall(
            CharacterTestRuntimeAiPlannerInstalledDeck source,
            string stableCardId,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput diagnosticsInput = null)
        {
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult guard = ValidateEditSource(source, catalog);
            if (guard != null)
                return guard;

            var ids = new string[source.Cards.Count + 1];
            for (int i = 0; i < source.Cards.Count; i++)
                ids[i] = source.Cards[i].StableCardId;
            ids[ids.Length - 1] = stableCardId;
            return ValidateAndCreate(
                new CharacterTestRuntimeAiPlannerInstalledDeckCandidate(source.CharacterStableId, ids, source.CharacterNumericId),
                catalog,
                source.RuntimeContractVersion,
                diagnosticsInput);
        }

        /// <remarks>LowFreqAlloc: explicit single-card deck edit only.</remarks>
        public static CharacterTestRuntimeAiPlannerInstalledDeckBuildResult TryRemove(
            CharacterTestRuntimeAiPlannerInstalledDeck source,
            string stableCardId,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput diagnosticsInput = null)
        {
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult guard = ValidateEditSource(source, catalog);
            if (guard != null)
                return guard;

            string normalized = stableCardId?.Trim() ?? string.Empty;
            int removeIndex = -1;
            for (int i = 0; i < source.Cards.Count; i++)
            {
                if (string.Equals(source.Cards[i].StableCardId, normalized, StringComparison.Ordinal))
                {
                    removeIndex = i;
                    break;
                }
            }
            if (removeIndex < 0)
            {
                return Failed(new List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>
                {
                    new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                        CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                        "INSTALLED_DECK_CARD_NOT_INSTALLED",
                        "$.stableCardId",
                        "The behavior card is not installed: " + normalized)
                });
            }

            var ids = new string[source.Cards.Count - 1];
            int target = 0;
            for (int i = 0; i < source.Cards.Count; i++)
            {
                if (i != removeIndex)
                    ids[target++] = source.Cards[i].StableCardId;
            }
            return ValidateAndCreate(
                new CharacterTestRuntimeAiPlannerInstalledDeckCandidate(source.CharacterStableId, ids, source.CharacterNumericId),
                catalog,
                source.RuntimeContractVersion,
                diagnosticsInput);
        }

        /// <remarks>LowFreqAlloc: explicit recommended-preset expansion only.</remarks>
        public static CharacterTestRuntimeAiPlannerInstalledDeckBuildResult ExpandRecommendedPreset(
            string characterStableId,
            int characterNumericId,
            CharacterTestRuntimeAiPlannerInstalledDeckPresetDefinition preset,
            CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog loadoutCatalog,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog publishedCatalog,
            int runtimeContractVersion,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput diagnosticsInput = null)
        {
            var diagnostics = new List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>();
            if (preset == null)
                Error(diagnostics, "INSTALLED_DECK_PRESET_MISSING", "$.preset", "Installed deck preset is required.");
            if (loadoutCatalog == null)
                Error(diagnostics, "INSTALLED_DECK_LOADOUT_CATALOG_MISSING", "$.loadoutCatalog", "Behavior Loadout catalog is required.");
            if (diagnostics.Count > 0)
                return Failed(diagnostics);

            bool loadoutFound = false;
            bool variantFound = false;
            for (int i = 0; i < loadoutCatalog.Loadouts.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition loadout = loadoutCatalog.Loadouts[i];
                if (loadout.LoadoutId != preset.LoadoutId)
                    continue;
                loadoutFound = true;
                for (int j = 0; j < loadout.Variants.Count; j++)
                {
                    if (loadout.Variants[j].VariantId == preset.VariantId)
                    {
                        variantFound = true;
                        break;
                    }
                }
                break;
            }
            if (!loadoutFound)
                Error(diagnostics, "INSTALLED_DECK_PRESET_LOADOUT_UNKNOWN", "$.preset.loadoutId", "Recommended Behavior Loadout does not exist.");
            else if (!variantFound)
                Error(diagnostics, "INSTALLED_DECK_PRESET_VARIANT_UNKNOWN", "$.preset.variantId", "Recommended Behavior Loadout variant does not exist.");
            if (diagnostics.Count > 0)
                return Failed(diagnostics);

            return ValidateAndCreate(
                new CharacterTestRuntimeAiPlannerInstalledDeckCandidate(characterStableId, preset.StableCardIds, characterNumericId),
                publishedCatalog,
                runtimeContractVersion,
                diagnosticsInput);
        }

        private static CharacterTestRuntimeAiPlannerInstalledDeckBuildResult ValidateEditSource(
            CharacterTestRuntimeAiPlannerInstalledDeck source,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog)
        {
            if (source == null)
            {
                return Failed(new List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>
                {
                    new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                        CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                        "INSTALLED_DECK_SOURCE_MISSING",
                        "$",
                        "Installed deck source is required.")
                });
            }
            if (catalog == null || source.CatalogHash != catalog.CatalogHash)
            {
                return Failed(new List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>
                {
                    new CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic(
                        CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error,
                        "INSTALLED_DECK_CATALOG_MISMATCH",
                        "$.catalog",
                        "Single-card edits require the same published behavior card catalog revision.")
                });
            }
            return null;
        }

        private static CharacterTestRuntimeAiPlannerInstalledDeckBuildResult Failed(
            List<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            return new CharacterTestRuntimeAiPlannerInstalledDeckBuildResult(null, diagnostics.ToArray());
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
