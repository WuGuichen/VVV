using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult
    {
        public CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            CharacterTestRuntimeAiLabCardDiagnostic[] diagnostics)
        {
            Catalog = catalog;
            Diagnostics = Array.AsReadOnly(
                diagnostics ?? Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>());
        }

        public CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog Catalog { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> Diagnostics { get; }
        public bool Success => Catalog != null
            && !CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(Diagnostics);
    }

    internal static class CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson
    {
        internal static bool ExplicitLegacyMigrationEnabled { get; set; }

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Auto
        };

        /// <remarks>LowFreqAlloc: production catalog authoring/export only.</remarks>
        public static string ToJson(CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            return JsonConvert.SerializeObject(CatalogDocument.FromCatalog(catalog), Settings);
        }

        /// <remarks>LowFreqAlloc: session/bootstrap catalog load only; never called from Tick.</remarks>
        public static CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult Load(
            string json,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            return LoadInternal(json, references, ExplicitLegacyMigrationEnabled);
        }

        internal static CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult
            LoadForExplicitLegacyMigration(
                string json,
                CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            return LoadInternal(json, references, explicitLegacyMigration: true);
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult LoadInternal(
            string json,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            bool explicitLegacyMigration)
        {
            var diagnostics = new List<CharacterTestRuntimeAiLabCardDiagnostic>();
            if (string.IsNullOrWhiteSpace(json))
            {
                Error(diagnostics, "PUBLISHED_CARD_CATALOG_JSON_EMPTY", "$", "Published behavior card catalog JSON is required.");
                return Failed(diagnostics);
            }
            if (references == null)
            {
                Error(
                    diagnostics,
                    "PUBLISHED_CARD_REFERENCES_MISSING",
                    "$.references",
                    "Authoritative Ability and CharacterAction references are required for catalog load.");
                return Failed(diagnostics);
            }

            if (explicitLegacyMigration
                && !TryNormalizeLegacyCharacterActionIds(
                    json,
                    out json,
                    out string legacyActionPath,
                    out string legacyActionError))
            {
                Error(
                    diagnostics,
                    "PUBLISHED_CARD_LEGACY_CHARACTER_ACTION_ID_UNSUPPORTED",
                    legacyActionPath,
                    legacyActionError);
                return Failed(diagnostics);
            }

            CatalogDocument document;
            try
            {
                var rawCards = JObject.Parse(json)["cards"] as JArray;
                if (rawCards != null)
                    foreach (JToken rawCard in rawCards)
                    {
                        var cooldown = ((rawCard as JObject)?["metadata"] as JObject)?["cooldownSeconds"];
                        if (cooldown != null && cooldown.Type != JTokenType.Integer && cooldown.Type != JTokenType.Float)
                            throw new JsonSerializationException("Cooldown must be a JSON number.");
                    }
                document = JsonConvert.DeserializeObject<CatalogDocument>(json, Settings);
            }
            catch (JsonException exception)
            {
                Error(diagnostics, "PUBLISHED_CARD_CATALOG_JSON_PARSE", "$", "Published behavior card catalog JSON parse failed: " + exception.Message);
                return Failed(diagnostics);
            }

            if (document == null)
            {
                Error(diagnostics, "PUBLISHED_CARD_CATALOG_DOCUMENT_MISSING", "$", "Published behavior card catalog document is missing.");
                return Failed(diagnostics);
            }
            if (!string.Equals(
                    document.Schema,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.Schema,
                    StringComparison.Ordinal))
            {
                Error(diagnostics, "PUBLISHED_CARD_CATALOG_SCHEMA_UNSUPPORTED", "$.schema", "Published behavior card catalog schema is unsupported.");
            }
            if (string.IsNullOrWhiteSpace(document.CatalogId))
                Error(diagnostics, "PUBLISHED_CARD_CATALOG_ID_MISSING", "$.catalogId", "Published behavior card catalog id is required.");
            if (document.Revision <= 0)
                Error(diagnostics, "PUBLISHED_CARD_CATALOG_REVISION_INVALID", "$.revision", "Published behavior card catalog revision must be positive.");
            if (document.CatalogNumericId <= 0)
                Error(diagnostics, "PUBLISHED_CARD_CATALOG_NUMERIC_ID_INVALID", "$.catalogNumericId", "Published behavior card catalog numeric identity must be positive.");
            if (document.CatalogNumericDomainId != CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.CatalogNumericDomainId)
                Error(diagnostics, "PUBLISHED_CARD_CATALOG_NUMERIC_DOMAIN_INVALID", "$.catalogNumericDomainId", "Published behavior card catalog numeric domain is not the authoring catalog domain.");
            if (document.CardNumericDomainId != CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.CardNumericDomainId)
                Error(diagnostics, "PUBLISHED_CARD_NUMERIC_DOMAIN_INVALID", "$.cardNumericDomainId", "Published behavior card numeric domain is not the behavior-card domain.");
            if (document.Cards == null)
                Error(diagnostics, "PUBLISHED_CARD_CATALOG_CARDS_MISSING", "$.cards", "Published behavior card catalog cards are required.");
            if (CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(diagnostics))
                return Failed(diagnostics);

            if (!TryComputeStoredCatalogHash(document, out long storedCatalogHash)
                || storedCatalogHash != document.CatalogHash)
            {
                Error(
                    diagnostics,
                    "PUBLISHED_CARD_CATALOG_HASH_MISMATCH",
                    "$.catalogHash",
                    "Published behavior card catalog hash does not match its stored card revisions.");
                return Failed(diagnostics);
            }

            var cards = new List<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>(document.Cards.Length);
            bool migratedLegacyCards = false;
            for (int i = 0; i < document.Cards.Length; i++)
            {
                CardDocument cardDocument = document.Cards[i];
                string path = "$.cards[" + i + "]";
                if (cardDocument == null)
                {
                    Error(diagnostics, "PUBLISHED_CARD_DOCUMENT_MISSING", path, "Published behavior card entry is missing.");
                    continue;
                }
                if (cardDocument.NumericId <= 0)
                    Error(diagnostics, "PUBLISHED_CARD_NUMERIC_ID_INVALID", path + ".numericId", "Published behavior card numeric identity must be positive.");
                if (cardDocument.NumericDomainId != CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.CardNumericDomainId)
                    Error(diagnostics, "PUBLISHED_CARD_NUMERIC_DOMAIN_INVALID", path + ".numericDomainId", "Published behavior card numeric domain is not the behavior-card domain.");

                bool missingResponsibilities = cardDocument.Metadata?.Responsibilities == null;
                if (cardDocument.LogicSchema == CharacterTestTacticalPublishedSource.Schema)
                {
                    try
                    {
                        if (cardDocument.Logic != null) throw new JsonSerializationException("Published tactical content cannot also contain flattened logic.");
                        var tactical = CharacterTestTacticalPublishedSource.Read(cardDocument.TacticalSource);
                        var restored = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.Restore(
                            cardDocument.StableCardId, cardDocument.Revision, cardDocument.MinimumRuntimeContractVersion,
                            cardDocument.MaximumRuntimeContractVersion, cardDocument.Metadata?.ToMetadata(), null,
                            cardDocument.SemanticHash, cardDocument.ContentHash, references, tactical, cardDocument.NumericId);
                        AddDiagnostics(diagnostics, restored.Diagnostics, path);
                        if (restored.Success) cards.Add(restored.Card);
                    }
                    catch (Exception exception) when (exception is JsonException || exception is ArgumentException || exception is InvalidOperationException)
                    { Error(diagnostics, "PUBLISHED_TACTICAL_SOURCE_INVALID", path, exception.Message); }
                    continue;
                }
                if (cardDocument.TacticalSource != null)
                { Error(diagnostics, "PUBLISHED_TACTICAL_SCHEMA_MISMATCH", path, "Tactical source requires its explicit schema."); continue; }
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata metadata =
                    cardDocument.Metadata?.ToMetadata();
                long expectedSemanticHash = cardDocument.SemanticHash;
                long expectedContentHash = cardDocument.ContentHash;
                long legacySemanticHash = 0L;
                long migratedLegacyContentHash = 0L;
                if (metadata != null)
                {
                    string logicSchema = string.IsNullOrWhiteSpace(cardDocument.LogicSchema)
                        ? CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV3Schema
                        : cardDocument.LogicSchema;
                    if (explicitLegacyMigration && string.Equals(
                        logicSchema,
                        CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV3Schema,
                        StringComparison.Ordinal))
                    {
                        long storedSemanticHash =
                            CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeLegacyV3SemanticHash(
                                cardDocument.Logic);
                        long storedContentHash =
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                                cardDocument.StableCardId,
                                cardDocument.Revision,
                                cardDocument.MinimumRuntimeContractVersion,
                                cardDocument.MaximumRuntimeContractVersion,
                                metadata,
                                storedSemanticHash);
                        if (storedSemanticHash != cardDocument.SemanticHash)
                        {
                            Error(
                                diagnostics,
                                "PUBLISHED_CARD_SEMANTIC_HASH_MISMATCH",
                                path + ".semanticHash",
                                "Published behavior card semantic hash does not match its authenticated v3 payload.");
                            continue;
                        }
                        if (storedContentHash != cardDocument.ContentHash)
                        {
                            Error(
                                diagnostics,
                                "PUBLISHED_CARD_CONTENT_HASH_MISMATCH",
                                path + ".contentHash",
                                "Published behavior card content hash does not match its authenticated v3 content.");
                            continue;
                        }

                        CharacterTestRuntimeAiLabBehaviorCardLogicJson.MigrateV3MovementStrategies(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildV5FromLegacy(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.UpgradeFromV5(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV7Migration.UpgradeFromV6(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.UpgradeFromV7(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV10Migration.UpgradeFromV9(cardDocument.Logic);
                        cardDocument.MinimumRuntimeContractVersion =
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
                        cardDocument.MaximumRuntimeContractVersion =
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
                        expectedSemanticHash =
                            CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(
                                cardDocument.Logic);
                        expectedContentHash =
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                                cardDocument.StableCardId,
                                cardDocument.Revision,
                                cardDocument.MinimumRuntimeContractVersion,
                                cardDocument.MaximumRuntimeContractVersion,
                                metadata,
                                expectedSemanticHash);
                        migratedLegacyCards = true;
                    }
                    else if (explicitLegacyMigration && string.Equals(
                        logicSchema,
                        CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV4Schema,
                        StringComparison.Ordinal))
                    {
                        long storedSemanticHash =
                            CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeLegacyV4SemanticHash(
                                cardDocument.Logic);
                        long storedContentHash =
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                                cardDocument.StableCardId,
                                cardDocument.Revision,
                                cardDocument.MinimumRuntimeContractVersion,
                                cardDocument.MaximumRuntimeContractVersion,
                                metadata,
                                storedSemanticHash);
                        if (storedSemanticHash != cardDocument.SemanticHash
                            || storedContentHash != cardDocument.ContentHash)
                        {
                            Error(
                                diagnostics,
                                "PUBLISHED_CARD_V4_AUTHENTICATION_FAILED",
                                path,
                                "Published v4 behavior card failed semantic/content authentication before migration.");
                            continue;
                        }

                        CharacterTestRuntimeAiLabBehaviorCardLogicJson.MigrateLegacyStages(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildV5FromLegacy(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.UpgradeFromV5(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV7Migration.UpgradeFromV6(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.UpgradeFromV7(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV10Migration.UpgradeFromV9(cardDocument.Logic);
                        cardDocument.MinimumRuntimeContractVersion =
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
                        cardDocument.MaximumRuntimeContractVersion =
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
                        expectedSemanticHash =
                            CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(
                                cardDocument.Logic);
                        expectedContentHash =
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                                cardDocument.StableCardId,
                                cardDocument.Revision,
                                cardDocument.MinimumRuntimeContractVersion,
                                cardDocument.MaximumRuntimeContractVersion,
                                metadata,
                                expectedSemanticHash);
                        migratedLegacyCards = true;
                    }
                    else if (explicitLegacyMigration && string.Equals(
                        logicSchema,
                        CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV5Schema,
                        StringComparison.Ordinal))
                    {
                        CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForRead(cardDocument.Logic);
                        long storedSemanticHash = CharacterTestRuntimeAiLabBehaviorRecipeV5SemanticHash.Compute(cardDocument.Logic);
                        long storedContentHash = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                            cardDocument.StableCardId,
                            cardDocument.Revision,
                            cardDocument.MinimumRuntimeContractVersion,
                            cardDocument.MaximumRuntimeContractVersion,
                            metadata,
                            storedSemanticHash);
                        if (storedSemanticHash != cardDocument.SemanticHash || storedContentHash != cardDocument.ContentHash)
                        {
                            Error(diagnostics, "PUBLISHED_CARD_V5_AUTHENTICATION_FAILED", path, "Published v5 behavior card failed semantic/content authentication before migration.");
                            continue;
                        }
                        CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.UpgradeFromV5(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV7Migration.UpgradeFromV6(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.UpgradeFromV7(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV10Migration.UpgradeFromV9(cardDocument.Logic);
                        cardDocument.MinimumRuntimeContractVersion = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
                        cardDocument.MaximumRuntimeContractVersion = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
                        expectedSemanticHash = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(cardDocument.Logic);
                        expectedContentHash = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                            cardDocument.StableCardId,
                            cardDocument.Revision,
                            cardDocument.MinimumRuntimeContractVersion,
                            cardDocument.MaximumRuntimeContractVersion,
                            metadata,
                            expectedSemanticHash);
                        migratedLegacyCards = true;
                    }
                    else if (explicitLegacyMigration && string.Equals(
                        logicSchema,
                        CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV6Schema,
                        StringComparison.Ordinal))
                    {
                        CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.SynchronizeForRead(cardDocument.Logic);
                        long storedSemanticHash = CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.Compute(cardDocument.Logic);
                        long storedContentHash = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                            cardDocument.StableCardId,
                            cardDocument.Revision,
                            cardDocument.MinimumRuntimeContractVersion,
                            cardDocument.MaximumRuntimeContractVersion,
                            metadata,
                            storedSemanticHash);
                        if (storedSemanticHash != cardDocument.SemanticHash || storedContentHash != cardDocument.ContentHash)
                        {
                            Error(diagnostics, "PUBLISHED_CARD_V6_AUTHENTICATION_FAILED", path, "Published v6 behavior card failed semantic/content authentication before migration.");
                            continue;
                        }
                        CharacterTestRuntimeAiLabBehaviorRecipeV7Migration.UpgradeFromV6(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.UpgradeFromV7(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV10Migration.UpgradeFromV9(cardDocument.Logic);
                        cardDocument.MinimumRuntimeContractVersion = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
                        cardDocument.MaximumRuntimeContractVersion = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
                        expectedSemanticHash = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(cardDocument.Logic);
                        expectedContentHash = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                            cardDocument.StableCardId,
                            cardDocument.Revision,
                            cardDocument.MinimumRuntimeContractVersion,
                            cardDocument.MaximumRuntimeContractVersion,
                            metadata,
                            expectedSemanticHash);
                        migratedLegacyCards = true;
                    }
                    else if (explicitLegacyMigration && string.Equals(
                        logicSchema,
                        CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV7Schema,
                        StringComparison.Ordinal))
                    {
                        CharacterTestRuntimeAiLabBehaviorRecipeV7Migration.SynchronizeForRead(cardDocument.Logic);
                        long storedSemanticHash = CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.ComputeV7(cardDocument.Logic);
                        long storedContentHash = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                            cardDocument.StableCardId,
                            cardDocument.Revision,
                            cardDocument.MinimumRuntimeContractVersion,
                            cardDocument.MaximumRuntimeContractVersion,
                            metadata,
                            storedSemanticHash);
                        if (storedSemanticHash != cardDocument.SemanticHash || storedContentHash != cardDocument.ContentHash)
                        {
                            Error(diagnostics, "PUBLISHED_CARD_V7_AUTHENTICATION_FAILED", path, "Published v7 behavior card failed semantic/content authentication before migration.");
                            continue;
                        }
                        CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.UpgradeFromV7(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV10Migration.UpgradeFromV9(cardDocument.Logic);
                        cardDocument.MinimumRuntimeContractVersion = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
                        cardDocument.MaximumRuntimeContractVersion = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
                        expectedSemanticHash = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(cardDocument.Logic);
                        expectedContentHash = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                            cardDocument.StableCardId,
                            cardDocument.Revision,
                            cardDocument.MinimumRuntimeContractVersion,
                            cardDocument.MaximumRuntimeContractVersion,
                            metadata,
                            expectedSemanticHash);
                        migratedLegacyCards = true;
                    }
                    else if (explicitLegacyMigration && string.Equals(
                        logicSchema,
                        CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV8Schema,
                        StringComparison.Ordinal))
                    {
                        CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.SynchronizeForRead(cardDocument.Logic);
                        long storedSemanticHash = CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash
                            .ComputeLegacyV8(cardDocument.Logic);
                        long storedContentHash = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                            cardDocument.StableCardId,
                            cardDocument.Revision,
                            cardDocument.MinimumRuntimeContractVersion,
                            cardDocument.MaximumRuntimeContractVersion,
                            metadata,
                            storedSemanticHash);
                        if (storedSemanticHash != cardDocument.SemanticHash
                            || storedContentHash != cardDocument.ContentHash)
                        {
                            Error(
                                diagnostics,
                                "PUBLISHED_CARD_V8_AUTHENTICATION_FAILED",
                                path,
                                "Published v8 behavior card failed semantic/content authentication before numeric-ID migration.");
                            continue;
                        }
                        cardDocument.Logic.BehaviorRecipeVersion = 9;
                        CharacterTestRuntimeAiLabBehaviorRecipeV10Migration.UpgradeFromV9(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.UpgradeFromV10(cardDocument.Logic);
                        cardDocument.MinimumRuntimeContractVersion =
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
                        cardDocument.MaximumRuntimeContractVersion =
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
                        expectedSemanticHash =
                            CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(cardDocument.Logic);
                        expectedContentHash = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                            cardDocument.StableCardId,
                            cardDocument.Revision,
                            cardDocument.MinimumRuntimeContractVersion,
                            cardDocument.MaximumRuntimeContractVersion,
                            metadata,
                            expectedSemanticHash);
                        migratedLegacyCards = true;
                    }
                    else if (string.Equals(
                        logicSchema,
                        CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV9Schema,
                        StringComparison.Ordinal))
                    {
                        CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.SynchronizeForRead(cardDocument.Logic);
                        long storedSemanticHash = CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash
                            .ComputeV9(cardDocument.Logic);
                        long storedContentHash = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                            cardDocument.StableCardId,
                            cardDocument.Revision,
                            cardDocument.MinimumRuntimeContractVersion,
                            cardDocument.MaximumRuntimeContractVersion,
                            metadata,
                            storedSemanticHash);
                        if (storedSemanticHash != cardDocument.SemanticHash
                            || storedContentHash != cardDocument.ContentHash)
                        {
                            Error(diagnostics, "PUBLISHED_CARD_V9_AUTHENTICATION_FAILED", path,
                                "Published v9 behavior card failed semantic/content authentication before SubjectScope migration.");
                            continue;
                        }
                        legacySemanticHash = storedSemanticHash;
                        migratedLegacyContentHash = storedContentHash;
                        CharacterTestRuntimeAiLabBehaviorRecipeV10Migration.UpgradeFromV9(cardDocument.Logic);
                        CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.UpgradeFromV10(cardDocument.Logic);
                        cardDocument.MinimumRuntimeContractVersion =
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
                        cardDocument.MaximumRuntimeContractVersion =
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
                        expectedSemanticHash = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(cardDocument.Logic);
                        expectedContentHash = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                            cardDocument.StableCardId,
                            cardDocument.Revision,
                            cardDocument.MinimumRuntimeContractVersion,
                            cardDocument.MaximumRuntimeContractVersion,
                            metadata,
                            expectedSemanticHash);
                        migratedLegacyCards = true;
                    }
                    else if (string.Equals(
                        logicSchema,
                        CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV10Schema,
                        StringComparison.Ordinal))
                    {
                        CharacterTestRuntimeAiLabBehaviorRecipeV10Migration.SynchronizeForRead(cardDocument.Logic);
                        long storedSemanticHash = CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash
                            .ComputeV10(cardDocument.Logic);
                        long storedContentHash = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                            cardDocument.StableCardId,
                            cardDocument.Revision,
                            cardDocument.MinimumRuntimeContractVersion,
                            cardDocument.MaximumRuntimeContractVersion,
                            metadata,
                            storedSemanticHash);
                        if (storedSemanticHash != cardDocument.SemanticHash
                            || storedContentHash != cardDocument.ContentHash)
                        {
                            Error(diagnostics, "PUBLISHED_CARD_V10_AUTHENTICATION_FAILED", path,
                                "Published v10 behavior card failed semantic/content authentication before TargetState migration.");
                            continue;
                        }
                        legacySemanticHash = storedSemanticHash;
                        migratedLegacyContentHash = storedContentHash;
                        CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.UpgradeFromV10(cardDocument.Logic);
                        cardDocument.MinimumRuntimeContractVersion =
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
                        cardDocument.MaximumRuntimeContractVersion =
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
                        expectedSemanticHash = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(cardDocument.Logic);
                        expectedContentHash = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                            cardDocument.StableCardId,
                            cardDocument.Revision,
                            cardDocument.MinimumRuntimeContractVersion,
                            cardDocument.MaximumRuntimeContractVersion,
                            metadata,
                            expectedSemanticHash);
                        migratedLegacyCards = true;
                    }
                    else if (string.Equals(
                        logicSchema,
                        CharacterTestRuntimeAiLabBehaviorCardLogicPayload.Schema,
                        StringComparison.Ordinal))
                    {
                        CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForRead(
                            cardDocument.Logic);
                        long currentSemanticHash =
                            CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeSemanticHash(
                                cardDocument.Logic);
                        if (currentSemanticHash != cardDocument.SemanticHash)
                        {
                            Error(
                                diagnostics,
                                "PUBLISHED_CARD_SEMANTIC_HASH_MISMATCH",
                                path + ".semanticHash",
                                "Published behavior card semantic hash does not match its authenticated v11 payload.");
                            continue;
                        }
                        if (missingResponsibilities)
                        {
                            long legacyContentHash =
                                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                                    .ComputeContentHashWithoutResponsibilities(
                                        cardDocument.StableCardId,
                                        cardDocument.Revision,
                                        cardDocument.MinimumRuntimeContractVersion,
                                        cardDocument.MaximumRuntimeContractVersion,
                                        metadata,
                                        currentSemanticHash);
                            if (legacyContentHash != cardDocument.ContentHash)
                            {
                                Error(
                                    diagnostics,
                                    "PUBLISHED_CARD_CONTENT_HASH_MISMATCH",
                                    path + ".contentHash",
                                    "Published behavior card content hash does not match its authenticated content.");
                                continue;
                            }
                            expectedContentHash =
                                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                                    cardDocument.StableCardId,
                                    cardDocument.Revision,
                                    cardDocument.MinimumRuntimeContractVersion,
                                    cardDocument.MaximumRuntimeContractVersion,
                                    metadata,
                                    currentSemanticHash);
                            migratedLegacyCards = true;
                        }
                        else if (explicitLegacyMigration
                            && cardDocument.MinimumRuntimeContractVersion
                                == CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                                    .LegacyRootDistanceRuntimeContractVersion
                            && cardDocument.MaximumRuntimeContractVersion
                                == CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                                    .LegacyRootDistanceRuntimeContractVersion)
                        {
                            long authenticatedContentHash =
                                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                                    .ComputeContentHash(
                                        cardDocument.StableCardId,
                                        cardDocument.Revision,
                                        cardDocument.MinimumRuntimeContractVersion,
                                        cardDocument.MaximumRuntimeContractVersion,
                                        metadata,
                                        currentSemanticHash);
                            if (authenticatedContentHash != cardDocument.ContentHash)
                            {
                                Error(
                                    diagnostics,
                                    "PUBLISHED_CARD_CONTENT_HASH_MISMATCH",
                                    path + ".contentHash",
                                    "Published behavior card content hash does not match its authenticated v6 contract.");
                                continue;
                            }
                            cardDocument.MinimumRuntimeContractVersion =
                                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                                    .CurrentRuntimeContractVersion;
                            cardDocument.MaximumRuntimeContractVersion =
                                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                                    .CurrentRuntimeContractVersion;
                            expectedContentHash =
                                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                                    .ComputeContentHash(
                                        cardDocument.StableCardId,
                                        cardDocument.Revision,
                                        cardDocument.MinimumRuntimeContractVersion,
                                        cardDocument.MaximumRuntimeContractVersion,
                                        metadata,
                                        currentSemanticHash);
                            migratedLegacyCards = true;
                        }
                        else if (!explicitLegacyMigration
                            && cardDocument.MinimumRuntimeContractVersion
                                == CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                                    .LegacyRootDistanceRuntimeContractVersion
                            && cardDocument.MaximumRuntimeContractVersion
                                == CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                                    .LegacyRootDistanceRuntimeContractVersion)
                        {
                            long authenticatedContentHash =
                                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                                    .ComputeContentHash(
                                        cardDocument.StableCardId,
                                        cardDocument.Revision,
                                        cardDocument.MinimumRuntimeContractVersion,
                                        cardDocument.MaximumRuntimeContractVersion,
                                        metadata,
                                        currentSemanticHash);
                            if (authenticatedContentHash != cardDocument.ContentHash)
                            {
                                Error(
                                    diagnostics,
                                    "PUBLISHED_CARD_CONTENT_HASH_MISMATCH",
                                    path + ".contentHash",
                                    "Published behavior card content hash does not match its authenticated v6 contract.");
                                continue;
                            }
                            CharacterTestRuntimeAiLabTargetDistanceContractMigration
                                .UpgradeToHurtboxDistanceReferences(cardDocument.Logic);
                            cardDocument.MinimumRuntimeContractVersion =
                                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                                    .CurrentRuntimeContractVersion;
                            cardDocument.MaximumRuntimeContractVersion =
                                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                                    .CurrentRuntimeContractVersion;
                            expectedSemanticHash =
                                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler
                                    .ComputeSemanticHash(cardDocument.Logic);
                            expectedContentHash =
                                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                                    .ComputeContentHash(
                                        cardDocument.StableCardId,
                                        cardDocument.Revision,
                                        cardDocument.MinimumRuntimeContractVersion,
                                        cardDocument.MaximumRuntimeContractVersion,
                                        metadata,
                                        expectedSemanticHash);
                            migratedLegacyCards = true;
                        }
                    }
                    else
                    {
                        Error(
                            diagnostics,
                            "PUBLISHED_CARD_LOGIC_SCHEMA_UNSUPPORTED",
                            path + ".logicSchema",
                            "Published behavior card logic schema is unsupported.");
                        continue;
                    }
                }
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult restore =
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.Restore(
                        cardDocument.StableCardId,
                        cardDocument.Revision,
                        cardDocument.MinimumRuntimeContractVersion,
                        cardDocument.MaximumRuntimeContractVersion,
                        metadata,
                        cardDocument.Logic,
                        expectedSemanticHash,
                        expectedContentHash,
                        references,
                        numericId: cardDocument.NumericId);
                AddDiagnostics(diagnostics, restore.Diagnostics, path);
                if (restore.Success)
                {
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCard card = restore.Card;
                    if (legacySemanticHash != 0L)
                    {
                        card = new CharacterTestRuntimeAiPlannerPublishedBehaviorCard(
                            card.StableCardId,
                            card.Revision,
                            card.MinimumRuntimeContractVersion,
                            card.MaximumRuntimeContractVersion,
                            card.Metadata,
                            card.CloneLogic(),
                            card.SemanticHash,
                            card.ContentHash,
                            legacySemanticHash,
                            migratedLegacyContentHash,
                            numericId: card.NumericId);
                    }
                    cards.Add(card);
                }
            }

            if (CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(diagnostics))
                return Failed(diagnostics);

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult create =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                    document.CatalogId,
                    document.Revision,
                    cards,
                    references,
                    document.CatalogNumericId);
            AddDiagnostics(diagnostics, create.Diagnostics, "$");
            if (!create.Success)
                return Failed(diagnostics);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = create.Catalog;
            if (!migratedLegacyCards && document.CatalogHash != catalog.CatalogHash)
            {
                Error(diagnostics, "PUBLISHED_CARD_CATALOG_HASH_MISMATCH", "$.catalogHash", "Published behavior card catalog hash does not match its normalized card revisions.");
                return Failed(diagnostics);
            }
            if (document.NumericIdentityHash != catalog.NumericIdentityHash)
            {
                Error(diagnostics, "PUBLISHED_CARD_CATALOG_NUMERIC_IDENTITY_HASH_MISMATCH", "$.numericIdentityHash", "Published behavior card catalog numeric identity mapping does not match its declared integer IDs.");
                return Failed(diagnostics);
            }
            return new CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult(
                catalog,
                diagnostics.ToArray());
        }

        private static bool TryNormalizeLegacyCharacterActionIds(
            string json,
            out string normalizedJson,
            out string failurePath,
            out string error)
        {
            normalizedJson = json;
            failurePath = "$";
            error = string.Empty;
            JToken root;
            try
            {
                root = JToken.Parse(json ?? string.Empty);
            }
            catch (JsonException)
            {
                // Preserve the normal catalog parse diagnostic for malformed JSON.
                return true;
            }

            if (!TryNormalizeLegacyCharacterActionIds(root, out failurePath, out error))
                return false;
            normalizedJson = root.ToString(Formatting.None);
            return true;
        }

        private static bool TryNormalizeLegacyCharacterActionIds(
            JToken token,
            out string failurePath,
            out string error)
        {
            failurePath = "$";
            error = string.Empty;
            if (token is JObject objectValue)
            {
                foreach (JProperty property in objectValue.Properties())
                {
                    if (string.Equals(
                            property.Name,
                            "characterActionId",
                            StringComparison.Ordinal)
                        && property.Value.Type == JTokenType.String)
                    {
                        string legacyId = (string)property.Value ?? string.Empty;
                        if (!CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash
                            .TryResolveLegacyActionId(legacyId, out var actionId))
                        {
                            failurePath = "$" + (property.Path.StartsWith("[", StringComparison.Ordinal)
                                ? property.Path
                                : "." + property.Path);
                            error = "Legacy CharacterAction id is unsupported: " + legacyId + ".";
                            return false;
                        }
                        property.Value = actionId.Value;
                        continue;
                    }

                    if (!TryNormalizeLegacyCharacterActionIds(
                            property.Value,
                            out failurePath,
                            out error))
                    {
                        return false;
                    }
                }
                return true;
            }

            if (token is JArray arrayValue)
            {
                for (int i = 0; i < arrayValue.Count; i++)
                {
                    if (!TryNormalizeLegacyCharacterActionIds(
                            arrayValue[i],
                            out failurePath,
                            out error))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool TryComputeStoredCatalogHash(
            CatalogDocument document,
            out long catalogHash)
        {
            catalogHash = 0L;
            if (document?.Cards == null)
                return false;

            CardDocument[] cards = (CardDocument[])document.Cards.Clone();
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null)
                    return false;
            }
            Array.Sort(cards, CompareCardDocuments);

            var hash = new MxFramework.Runtime.RuntimeContentHashAccumulator();
            hash.AddStringStable(
                "publishedCardCatalog.schema",
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.Schema);
            hash.AddStringStable("publishedCardCatalog.catalogId", document.CatalogId);
            hash.AddInt("publishedCardCatalog.revision", document.Revision);
            hash.AddInt("publishedCardCatalog.cardCount", cards.Length);
            for (int i = 0; i < cards.Length; i++)
            {
                hash.AddStringStable("publishedCardCatalog.cardStableId", cards[i].StableCardId);
                hash.AddInt("publishedCardCatalog.cardRevision", cards[i].Revision);
                hash.AddLong("publishedCardCatalog.cardContentHash", cards[i].ContentHash);
            }
            catalogHash = hash.ToHash();
            return true;
        }

        private static int CompareCardDocuments(CardDocument left, CardDocument right)
        {
            int stableId = string.Compare(
                left.StableCardId,
                right.StableCardId,
                StringComparison.Ordinal);
            return stableId != 0 ? stableId : left.Revision.CompareTo(right.Revision);
        }

        private static void AddDiagnostics(
            List<CharacterTestRuntimeAiLabCardDiagnostic> destination,
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> source,
            string prefix)
        {
            for (int i = 0; i < source.Count; i++)
            {
                CharacterTestRuntimeAiLabCardDiagnostic item = source[i];
                string path = item.Path == "$"
                    ? prefix
                    : prefix + (item.Path.StartsWith("$.", StringComparison.Ordinal) ? item.Path.Substring(1) : ".logic");
                destination.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                    item.Severity,
                    item.Code,
                    path,
                    item.Message));
            }
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult Failed(
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            return new CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult(
                null,
                diagnostics.ToArray());
        }

        private static void Error(
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            diagnostics.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                code,
                path,
                message));
        }

        private sealed class CatalogDocument
        {
            public string Schema { get; set; }
            public string CatalogId { get; set; }
            public int Revision { get; set; }
            public int CatalogNumericId { get; set; }
            public int CardNumericDomainId { get; set; }
            public int CatalogNumericDomainId { get; set; }
            public long CatalogHash { get; set; }
            public long NumericIdentityHash { get; set; }
            public CardDocument[] Cards { get; set; }

            public static CatalogDocument FromCatalog(
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog)
            {
                var document = new CatalogDocument
                {
                    Schema = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.Schema,
                    CatalogId = catalog.CatalogId,
                    Revision = catalog.Revision,
                    CatalogNumericId = catalog.CatalogNumericId,
                    CardNumericDomainId = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.CardNumericDomainId,
                    CatalogNumericDomainId = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.CatalogNumericDomainId,
                    CatalogHash = catalog.CatalogHash,
                    NumericIdentityHash = catalog.NumericIdentityHash,
                    Cards = new CardDocument[catalog.Cards.Count]
                };
                for (int i = 0; i < catalog.Cards.Count; i++)
                    document.Cards[i] = CardDocument.FromCard(catalog.Cards[i]);
                return document;
            }
        }

        private sealed class CardDocument
        {
            public string StableCardId { get; set; }
            public int NumericId { get; set; }
            public int NumericDomainId { get; set; }
            public int Revision { get; set; }
            public int MinimumRuntimeContractVersion { get; set; }
            public int MaximumRuntimeContractVersion { get; set; }
            public long SemanticHash { get; set; }
            public long ContentHash { get; set; }
            public string LogicSchema { get; set; }
            public MetadataDocument Metadata { get; set; }
            public CharacterTestRuntimeAiLabBehaviorCardLogicPayload Logic { get; set; }
            public JObject TacticalSource { get; set; }

            public static CardDocument FromCard(
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard card)
            {
                return new CardDocument
                {
                    StableCardId = card.StableCardId,
                    NumericId = card.NumericId,
                    NumericDomainId = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.CardNumericDomainId,
                    Revision = card.Revision,
                    MinimumRuntimeContractVersion = card.MinimumRuntimeContractVersion,
                    MaximumRuntimeContractVersion = card.MaximumRuntimeContractVersion,
                    SemanticHash = card.SemanticHash,
                    ContentHash = card.ContentHash,
                    LogicSchema = card.LogicSchema,
                    Metadata = MetadataDocument.FromMetadata(card.Metadata),
                    Logic = card.CloneLogic(),
                    TacticalSource = card.TacticalSource?.ToJson()
                };
            }
        }

        private sealed class MetadataDocument
        {
            public string DisplayName { get; set; }
            public string IconKey { get; set; }
            public int Cost { get; set; }
            public float CooldownSeconds { get; set; }
            public bool ShouldSerializeCooldownSeconds() => CooldownSeconds != 0f;
            public string ShortDescription { get; set; }
            public string UseSummary { get; set; }
            public string RiskSummary { get; set; }
            public string[] Tags { get; set; }
            public CharacterTestRuntimeAiPlannerBehaviorCapability[] RequiredCapabilities { get; set; }
            public CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] Responsibilities { get; set; }

            public CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata ToMetadata()
            {
                return new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(
                    DisplayName,
                    IconKey,
                    Cost,
                    ShortDescription,
                    UseSummary,
                    RiskSummary,
                    Tags,
                    RequiredCapabilities,
                    Responsibilities, CooldownSeconds);
            }

            public static MetadataDocument FromMetadata(
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata metadata)
            {
                var tags = new string[metadata.Tags.Count];
                for (int i = 0; i < tags.Length; i++)
                    tags[i] = metadata.Tags[i];
                var capabilities = new CharacterTestRuntimeAiPlannerBehaviorCapability[
                    metadata.RequiredCapabilities.Count];
                for (int i = 0; i < capabilities.Length; i++)
                    capabilities[i] = metadata.RequiredCapabilities[i];
                var responsibilities = new CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[
                    metadata.Responsibilities.Count];
                for (int i = 0; i < responsibilities.Length; i++)
                    responsibilities[i] = metadata.Responsibilities[i];
                return new MetadataDocument
                {
                    DisplayName = metadata.DisplayName,
                    IconKey = metadata.IconKey,
                    Cost = metadata.Cost, CooldownSeconds = metadata.CooldownSeconds,
                    ShortDescription = metadata.ShortDescription,
                    UseSummary = metadata.UseSummary,
                    RiskSummary = metadata.RiskSummary,
                    Tags = tags,
                    RequiredCapabilities = capabilities,
                    Responsibilities = responsibilities
                };
            }
        }
    }
}
