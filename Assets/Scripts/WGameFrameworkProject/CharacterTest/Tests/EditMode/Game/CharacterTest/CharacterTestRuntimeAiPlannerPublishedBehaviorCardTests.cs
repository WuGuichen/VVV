using System;
using System.Collections.Generic;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiPlannerPublishedBehaviorCardTests
    {
        [Test]
        public void BehaviorContractV6Migration_RebindsLegacyDistanceToHurtboxDistance()
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload =
                CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(
                    CharacterTestRuntimeAiLabBehaviorCardFactory.CreateContinuousApproachAtomic());
            payload.ActivationConditions.Clear();
            payload.ActivationConditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = "legacy.root-distance",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan,
                Value = 2f
            });
            payload.ActivationConditionRoot =
                CharacterTestRuntimeAiLabBehaviorRecipeConditionNode.FromAll(
                    payload.ActivationConditions,
                    "activation");

            CharacterTestRuntimeAiLabTargetDistanceContractMigration
                .UpgradeToHurtboxDistanceReferences(payload);

            Assert.AreEqual(
                CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                payload.ActivationConditions[0].Fact);
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode migratedLeaf =
                payload.ActivationConditionRoot.Children[0];
            if (migratedLeaf.Kind
                == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope)
            {
                migratedLeaf = migratedLeaf.Children[0];
            }
            Assert.AreEqual(
                CharacterTestRuntimeAiLabCardConditionFact.TargetDistance,
                migratedLeaf.Predicate.Fact);
        }

        [Test]
        public void PublishedCard_PublishCatalogRoundtripPreservesLogicAndHashes()
        {
            CharacterTestRuntimeAiLabBehaviorCardDraft draft = CreateDraft();
            draft.ResponsibilityKinds = new[]
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting,
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage
            };
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult publish = Publish(
                draft,
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 2);

            Assert.IsTrue(publish.Success, Describe(publish.Diagnostics));
            Assert.NotNull(publish.Card);
            Assert.NotNull(publish.Program);
            Assert.AreEqual(publish.Program.SemanticHash, publish.Card.SemanticHash);

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                "character-test.behavior-cards",
                1,
                new[] { publish.Card });
            string json = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(catalog);
            StringAssert.Contains("\"responsibilities\"", json);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult loaded =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(
                    json,
                    CreateReferences());

            Assert.IsTrue(loaded.Success, Describe(loaded.Diagnostics));
            Assert.AreEqual(catalog.CatalogHash, loaded.Catalog.CatalogHash);
            Assert.IsTrue(loaded.Catalog.TryGetExact(
                "test.behavior-card.action-chain",
                1,
                out CharacterTestRuntimeAiPlannerPublishedBehaviorCard restored));
            Assert.AreEqual(publish.Card.SemanticHash, restored.SemanticHash);
            Assert.AreEqual(publish.Card.ContentHash, restored.ContentHash);
            Assert.AreEqual(publish.Card.Metadata.DisplayName, restored.Metadata.DisplayName);
            CollectionAssert.AreEqual(
                draft.ResponsibilityKinds,
                restored.Metadata.Responsibilities);
            Assert.AreEqual(publish.Card.CloneLogic().Stages.Count, restored.CloneLogic().Stages.Count);
        }

        [Test]
        public void PublishedCard_AuthenticatedV10MigratesToV11WithoutRewritingSourceJson()
        {
            const string stableCardId = "test.behavior-card.v10-migration";
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic =
                CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession.CreateInitialNativeLogic();
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                null,
                stableCardId,
                "V10 migration",
                "Test",
                new CharacterTestRuntimeAiLabBehaviorCardPayload())
            {
                LogicPayload = logic,
                LogicPayloadMigratedFromV1 = false
            };
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest request = CreateRequest(
                1,
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                stableCardId);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult published =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                    draft,
                    request,
                    CreateReferences());
            Assert.IsTrue(published.Success, Describe(published.Diagnostics));
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                "test.v10-migration",
                1,
                new[] { published.Card });
            JObject document = JObject.Parse(
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(catalog));
            JObject cardDocument = (JObject)((JArray)document["cards"])[0];
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload v10 = published.Card.CloneLogic();
            v10.BehaviorRecipeVersion = 10;
            CharacterTestRuntimeAiLabBehaviorRecipeV10Migration.SynchronizeForWrite(v10);
            long v10SemanticHash = CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash.ComputeV10(v10);
            const int v10Contract = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                .LegacyObjectScopeRuntimeContractVersion;
            long v10ContentHash = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                stableCardId,
                1,
                v10Contract,
                v10Contract,
                request.Metadata,
                v10SemanticHash);
            cardDocument["minimumRuntimeContractVersion"] = v10Contract;
            cardDocument["maximumRuntimeContractVersion"] = v10Contract;
            cardDocument["semanticHash"] = v10SemanticHash;
            cardDocument["contentHash"] = v10ContentHash;
            cardDocument["logicSchema"] = CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV10Schema;
            cardDocument["logic"] = JObject.FromObject(v10);
            document["catalogHash"] = ComputeCatalogHash(document);
            string sourceJson = document.ToString();

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult loaded =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(
                    sourceJson,
                    CreateReferences());

            Assert.IsTrue(loaded.Success, Describe(loaded.Diagnostics));
            Assert.IsTrue(loaded.Catalog.TryGetExact(stableCardId, 1, out var restored));
            Assert.AreEqual(11, restored.CloneLogic().BehaviorRecipeVersion);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                restored.MinimumRuntimeContractVersion);
            Assert.AreEqual(v10SemanticHash, restored.LegacySemanticHash);
            Assert.AreEqual(v10ContentHash, restored.LegacyContentHash);
            StringAssert.Contains("\"BehaviorRecipeVersion\": 10", sourceJson);
        }

        [Test]
        public void PublishedCard_CatalogLoadRejectsLegacyV4SchemaWithoutImplicitMigration()
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload payload =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateContinuousApproachAtomic();
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                null,
                "bc_3qz33zjzk4",
                "持续接敌",
                "Player",
                payload)
            {
                LogicPayload = CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(payload)
            };
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload legacyLogic = draft.LogicPayload.Clone();
            legacyLogic.Stages[0].Kind = CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly;
            legacyLogic.Stages[0].LegacyActionKind = CharacterTestRuntimeAiPlannerActionKind.MoveToTarget;
            legacyLogic.Detectors[0].ActivationDurationFrames = 60;
            ConfirmMigratedV7Intervals(draft.LogicPayload);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult publish =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                    draft,
                    CreateRequest(
                        revision: 1,
                        minimumRuntimeVersion: 2,
                        maximumRuntimeVersion: 2,
                        stableCardId: "bc_3qz33zjzk4"),
                    CreateReferences());
            Assert.IsTrue(publish.Success, Describe(publish.Diagnostics));
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                "character-test.behavior-cards",
                1,
                new[] { publish.Card });

            JObject document = JObject.Parse(
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(catalog));
            JObject card = (JObject)((JArray)document["cards"])[0];
            card["logicSchema"] = CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV4Schema;
            card["logic"] = ToLegacyV4LogicDocument(legacyLogic);
            long legacySemanticHash =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.ComputeLegacyV4SemanticHash(legacyLogic);
            long legacyContentHash =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                    publish.Card.StableCardId,
                    publish.Card.Revision,
                    publish.Card.MinimumRuntimeContractVersion,
                    publish.Card.MaximumRuntimeContractVersion,
                    publish.Card.Metadata,
                    legacySemanticHash);
            card["semanticHash"] = legacySemanticHash;
            card["contentHash"] = legacyContentHash;
            document["catalogHash"] = ComputeCatalogHash(document);

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult loaded =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(
                    document.ToString(),
                    CreateReferences());

            Assert.IsFalse(loaded.Success);
            Assert.IsNull(loaded.Catalog);
            AssertDiagnostic(loaded.Diagnostics, "PUBLISHED_CARD_LOGIC_SCHEMA_UNSUPPORTED");
        }

        [Test]
        public void PublishedCard_ExplicitLegacyV8MigrationConvertsAuthenticatedStringActionIds()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard card = Publish(
                CreateDraft(CharacterTestRuntimeAiLabBehaviorCardFactory.CreateReactiveDodgeAtomic()),
                revision: 1,
                minimumRuntimeVersion:
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                maximumRuntimeVersion:
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion).Card;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                "character-test.behavior-cards",
                1,
                new[] { card });
            string legacyJson = CharacterTestRuntimeAiPlannerLegacyCatalogTestFixture
                .CreateLegacyV8Json(catalog);
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references = CreateReferences();

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult implicitLoad =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(
                    legacyJson,
                    references);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult migrated =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson
                    .LoadForExplicitLegacyMigration(legacyJson, references);

            Assert.IsFalse(implicitLoad.Success);
            AssertDiagnostic(implicitLoad.Diagnostics, "PUBLISHED_CARD_CATALOG_JSON_PARSE");
            Assert.IsTrue(migrated.Success, Describe(migrated.Diagnostics));
            Assert.IsTrue(migrated.Catalog.TryGetExact(
                card.StableCardId,
                card.Revision,
                out CharacterTestRuntimeAiPlannerPublishedBehaviorCard restored));
            Assert.AreEqual(
                CharacterTestRuntimeAiLabBehaviorCardLogicPayload.CurrentBehaviorRecipeVersion,
                restored.CloneLogic().BehaviorRecipeVersion);
            Assert.IsTrue(ContainsCharacterAction(
                restored.CloneLogic(),
                CharacterTestCharacterActionConfigSource.DefaultEvadeActionId.Value));
            StringAssert.DoesNotContain(
                "\"characterActionId\": \"",
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(
                    migrated.Catalog));
        }

        [Test]
        public void PublishedCard_PublishRejectsInvalidEnvelopeAndLogic()
        {
            CharacterTestRuntimeAiLabBehaviorCardDraft draft = CreateDraft();
            draft.LogicPayload.Timelines.Clear();
            var invalidMetadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(
                displayName: string.Empty,
                iconKey: string.Empty,
                cost: 11,
                shortDescription: string.Empty,
                useSummary: string.Empty,
                riskSummary: string.Empty,
                tags: new[] { "Pursuit", "Pursuit" },
                requiredCapabilities: new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCapability.None
                });
            var request = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(
                "Character Test Invalid",
                revision: 0,
                minimumRuntimeContractVersion: 2,
                maximumRuntimeContractVersion: 1,
                invalidMetadata);

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult result =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                    draft,
                    request,
                    CreateReferences());

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Card);
            AssertDiagnostic(result.Diagnostics, "PUBLISHED_CARD_STABLE_ID_INVALID");
            AssertDiagnostic(result.Diagnostics, "PUBLISHED_CARD_COST_INVALID");
            AssertDiagnostic(result.Diagnostics, "PUBLISHED_CARD_ICON_MISSING");
            AssertDiagnostic(result.Diagnostics, "PUBLISHED_CARD_RUNTIME_RANGE_INVALID");

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult invalidLogic = Publish(
                draft,
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1);
            Assert.IsFalse(invalidLogic.Success);
            AssertDiagnostic(invalidLogic.Diagnostics, "CARD_V6_TIMELINE_MISSING");
        }

        [Test]
        public void PublishedCard_ResolverSelectsLatestCompatibleRevision()
        {
            CharacterTestRuntimeAiLabBehaviorCardDraft draft = CreateDraft();
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard revision1 = Publish(
                draft,
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1).Card;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard revision2 = Publish(
                draft,
                revision: 2,
                minimumRuntimeVersion: 2,
                maximumRuntimeVersion: 3).Card;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard revision3 = Publish(
                draft,
                revision: 3,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 3).Card;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                "character-test.behavior-cards",
                3,
                new[] { revision2, revision1, revision3 });

            Assert.IsTrue(catalog.TryResolveLatestCompatible(
                revision1.StableCardId,
                runtimeContractVersion: 1,
                out CharacterTestRuntimeAiPlannerPublishedBehaviorCard selected));
            Assert.AreEqual(3, selected.Revision);
            Assert.IsTrue(catalog.TryResolveLatestCompatible(
                revision1.StableCardId,
                runtimeContractVersion: 2,
                out selected));
            Assert.AreEqual(3, selected.Revision);
            Assert.IsFalse(catalog.TryResolveLatestCompatible(
                revision1.StableCardId,
                runtimeContractVersion: 4,
                out _));
        }

        [Test]
        public void PublishedCard_CatalogAllowsSameStableIdAcrossRevisionsWithStableNumericId()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard revision1 = Publish(
                CreateDraft(),
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1,
                numericId: 1).Card;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard revision2 = Publish(
                CreateDraft(),
                revision: 2,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1,
                numericId: 1).Card;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard otherCard = Publish(
                CreateDraft(),
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1,
                stableCardId: "test.behavior-card.other",
                numericId: 2).Card;

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                "character-test.behavior-cards",
                2,
                new[] { revision1, revision2, otherCard });

            Assert.AreEqual(3, catalog.Cards.Count);
            Assert.AreEqual(1, catalog.Cards[0].NumericId);
            Assert.AreEqual(1, catalog.Cards[1].NumericId);
            Assert.AreEqual(2, catalog.Cards[2].NumericId);
        }

        [Test]
        public void PublishedCard_CatalogRejectsNumericIdReuseAcrossStableIds()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard first = Publish(
                CreateDraft(),
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1,
                numericId: 1).Card;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard second = Publish(
                CreateDraft(),
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1,
                stableCardId: "test.behavior-card.other",
                numericId: 1).Card;

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult result =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                    "character-test.behavior-cards",
                    1,
                    new[] { first, second },
                    CreateReferences());

            Assert.IsFalse(result.Success);
            AssertDiagnostic(result.Diagnostics, "PUBLISHED_CARD_NUMERIC_ID_DUPLICATE");
        }

        [Test]
        public void PublishedCard_CatalogRejectsNumericIdChangeAcrossRevisions()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard revision1 = Publish(
                CreateDraft(),
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1,
                numericId: 1).Card;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard revision2 = Publish(
                CreateDraft(),
                revision: 2,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1,
                numericId: 2).Card;

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult result =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                    "character-test.behavior-cards",
                    2,
                    new[] { revision1, revision2 },
                    CreateReferences());

            Assert.IsFalse(result.Success);
            AssertDiagnostic(result.Diagnostics, "PUBLISHED_CARD_NUMERIC_ID_CHANGED");
        }

        [Test]
        public void PublishedCard_CatalogRejectsUnknownOrRetiredNumericId()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard card = Publish(
                CreateDraft(),
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1,
                numericId: 3).Card;

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult result =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                    "character-test.behavior-cards",
                    1,
                    new[] { card },
                    CreateReferences());

            Assert.IsFalse(result.Success);
            AssertDiagnostic(result.Diagnostics, "PUBLISHED_CARD_NUMERIC_ID_INVALID");
        }

        [Test]
        public void PublishedCard_CatalogLoadRejectsDuplicateRevisionAndTamperedHash()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard card = Publish(
                CreateDraft(),
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1).Card;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                "character-test.behavior-cards",
                1,
                new[] { card });
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult roundtrip =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(catalog),
                    CreateReferences());
            Assert.IsTrue(roundtrip.Success, Describe(roundtrip.Diagnostics));

            JObject duplicateDocument = JObject.Parse(
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(catalog));
            JArray duplicateCards = (JArray)duplicateDocument["cards"];
            duplicateCards.Add(duplicateCards[0].DeepClone());
            duplicateDocument["catalogHash"] = ComputeCatalogHash(duplicateDocument);

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult duplicate =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(
                    duplicateDocument.ToString(),
                    CreateReferences());
            Assert.IsFalse(duplicate.Success);
            AssertDiagnostic(duplicate.Diagnostics, "PUBLISHED_CARD_REVISION_DUPLICATE");

            JObject tamperedDocument = JObject.Parse(
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(catalog));
            JObject firstCard = (JObject)((JArray)tamperedDocument["cards"])[0];
            firstCard["semanticHash"] = firstCard.Value<long>("semanticHash") + 1L;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogLoadResult tampered =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(
                    tamperedDocument.ToString(),
                    CreateReferences());
            Assert.IsFalse(tampered.Success);
            AssertDiagnostic(tampered.Diagnostics, "PUBLISHED_CARD_SEMANTIC_HASH_MISMATCH");
        }

        [Test]
        public void PublishedCard_LayoutAndDraftEditsDoNotMutatePublishedContent()
        {
            CharacterTestRuntimeAiLabBehaviorCardDraft draft = CreateDraft();
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard original = Publish(
                draft,
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1).Card;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload publishedLogic = original.CloneLogic();
            int originalColumn = publishedLogic.Layout.Stages[0].Column;
            publishedLogic.Layout.Stages[0].Column += 3;

            draft.LogicPayload.Layout.Stages[0].Column += 7;
            draft.LogicPayload.Stages[0].Title = "Changed draft title";
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard republished = Publish(
                draft,
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1).Card;

            CharacterTestRuntimeAiLabBehaviorCardLogicPayload unchangedLogic = original.CloneLogic();
            Assert.AreEqual(originalColumn, unchangedLogic.Layout.Stages[0].Column);
            Assert.AreNotEqual("Changed draft title", unchangedLogic.Stages[0].Title);
            Assert.AreEqual(original.SemanticHash, republished.SemanticHash);
            Assert.AreEqual(original.ContentHash, republished.ContentHash);
        }

        [Test]
        public void PublishedCard_RuntimeBehaviorFieldsChangeSemanticAndContentHashes()
        {
            CharacterTestRuntimeAiLabBehaviorCardDraft legacyDraft = CreateDraft(
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateReactiveDodgeAtomic());
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForRead(
                legacyDraft.LogicPayload);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard legacyOriginal = Publish(
                legacyDraft,
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1).Card;
            legacyDraft.LogicPayload.Timelines[0].Events[0].Priority += 1;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard legacyChanged = Publish(
                legacyDraft,
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1).Card;

            CharacterTestRuntimeAiLabBehaviorCardDraft conditionDraft = CreateDraft(
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateContinuousApproachAtomic());
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForRead(
                conditionDraft.LogicPayload);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard conditionOriginal = Publish(
                conditionDraft,
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1).Card;
            conditionDraft.LogicPayload.Timelines[0].Events[0].ConditionRoot =
                CreateFactConditionNode(
                    "hash.condition.target-exists",
                    CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                    abilityId: 0,
                    characterActionId: string.Empty);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard conditionChanged = Publish(
                conditionDraft,
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1).Card;

            Assert.AreNotEqual(legacyOriginal.SemanticHash, legacyChanged.SemanticHash);
            Assert.AreNotEqual(legacyOriginal.ContentHash, legacyChanged.ContentHash);
            Assert.AreNotEqual(conditionOriginal.SemanticHash, conditionChanged.SemanticHash);
            Assert.AreNotEqual(conditionOriginal.ContentHash, conditionChanged.ContentHash);
        }

        [Test]
        public void PublishedCard_PublishRequiresReferencesAndRejectsUnknownStageAndConditionReferences()
        {
            CharacterTestRuntimeAiLabBehaviorCardDraft missingReferencesDraft = CreateDraft();
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult missingReferences =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                    missingReferencesDraft,
                    CreateRequest(1, 1, 1),
                    references: null);
            Assert.IsFalse(missingReferences.Success);
            AssertDiagnostic(missingReferences.Diagnostics, "PUBLISHED_CARD_REFERENCES_MISSING");

            CharacterTestRuntimeAiLabBehaviorCardDraft unknownDraft = CreateDraft();
            CharacterTestRuntimeAiLabBehaviorRecipeEvent unknownAbilityEvent =
                unknownDraft.LogicPayload.Timelines[0].Events[0];
            unknownAbilityEvent.Operation.Kind =
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Ability;
            unknownAbilityEvent.Operation.AbilityId = int.MaxValue;
            unknownDraft.LogicPayload.ActivationConditionRoot =
                CreateFactConditionNode(
                    "unknown.ability",
                    CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable,
                    abilityId: int.MaxValue,
                    characterActionId: string.Empty);
            unknownDraft.LogicPayload.Timelines[0].EntryConditionRoot =
                CreateFactConditionNode(
                    "unknown.action",
                    CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction,
                    abilityId: 0,
                    characterActionId: "missing.action");
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult unknown = Publish(
                unknownDraft,
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1);

            Assert.IsFalse(unknown.Success);
            AssertDiagnostic(unknown.Diagnostics, "CARD_V9_EVENT_ABILITY_REMOVED");
            AssertDiagnostic(unknown.Diagnostics, "CARD_V6_CONDITION_ABILITY_UNKNOWN");
            AssertDiagnostic(unknown.Diagnostics, "CARD_V6_CONDITION_ACTION_UNKNOWN");
        }

        [Test]
        public void PublishedCard_LegacyCastAbilityRejectsNonPositiveAndUnknownAbilityReferences()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult missing = Publish(
                CreateLegacyCastAbilityDraft(0),
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult negative = Publish(
                CreateLegacyCastAbilityDraft(-1),
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult unknown = Publish(
                CreateLegacyCastAbilityDraft(int.MaxValue),
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult known = Publish(
                CreateLegacyCastAbilityDraft(CharacterTestAbilityIds.DashStrikeAbilityId),
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1);

            Assert.IsFalse(missing.Success);
            Assert.IsFalse(negative.Success);
            Assert.IsFalse(unknown.Success);
            AssertDiagnostic(missing.Diagnostics, "CARD_V9_EVENT_ABILITY_REMOVED");
            AssertDiagnostic(negative.Diagnostics, "CARD_V9_EVENT_ABILITY_REMOVED");
            AssertDiagnostic(unknown.Diagnostics, "CARD_V9_EVENT_ABILITY_REMOVED");
            Assert.IsFalse(known.Success);
            AssertDiagnostic(known.Diagnostics, "CARD_V9_EVENT_ABILITY_REMOVED");
        }

        [Test]
        public void PublishedCard_HoldEventIsRejectedAsRemoved()
        {
            CharacterTestRuntimeAiLabBehaviorCardDraft draft = CreateDraft();
            CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = draft.LogicPayload.Timelines[0].Events[0];
            evt.Operation.Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Hold;
            evt.Operation.CharacterActionId = default;

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult result = Publish(
                draft,
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1);

            Assert.IsFalse(result.Success);
            AssertDiagnostic(result.Diagnostics, "CARD_V9_EVENT_HOLD_REMOVED");
        }

        [Test]
        public void PublishedCard_CatalogRevalidatesDirectlyConstructedCards()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard valid = Publish(
                CreateDraft(),
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1).Card;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload forgedLogic = valid.CloneLogic();
            forgedLogic.Timelines[0].Events[0].Operation.Kind =
                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Ability;
            forgedLogic.Timelines[0].Events[0].Operation.AbilityId = int.MaxValue;
            var forged = new CharacterTestRuntimeAiPlannerPublishedBehaviorCard(
                valid.StableCardId,
                valid.Revision,
                valid.MinimumRuntimeContractVersion,
                valid.MaximumRuntimeContractVersion,
                valid.Metadata,
                forgedLogic,
                valid.SemanticHash,
                valid.ContentHash);

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult result =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                    "character-test.behavior-cards",
                    1,
                    new[] { forged },
                    CreateReferences());

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Catalog);
            AssertDiagnostic(result.Diagnostics, "CARD_V9_EVENT_ABILITY_REMOVED");
        }

        [Test]
        public void PublishedCard_InMemoryCatalogRejectsDuplicateRevisionAndDoesNotExposeBackingArray()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard card = Publish(
                CreateDraft(),
                revision: 1,
                minimumRuntimeVersion: 1,
                maximumRuntimeVersion: 1).Card;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult duplicate =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                    "character-test.behavior-cards",
                    1,
                    new[] { card, card },
                    CreateReferences());
            Assert.IsFalse(duplicate.Success);
            AssertDiagnostic(duplicate.Diagnostics, "PUBLISHED_CARD_REVISION_DUPLICATE");

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                "character-test.behavior-cards",
                1,
                new[] { card });
            Assert.IsFalse(catalog.Cards is CharacterTestRuntimeAiPlannerPublishedBehaviorCard[]);
            var mutableView = catalog.Cards as System.Collections.Generic.IList<
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard>;
            Assert.NotNull(mutableView);
            Assert.Throws<NotSupportedException>(() => mutableView[0] = card);
        }

        [Test]
        public void PublishedCard_StableIdTrimsBoundaryWhitespaceAndRejectsBlankOrPunctuationPrefix()
        {
            CharacterTestRuntimeAiLabBehaviorCardDraft draft = CreateDraft();
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult normalized =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                    draft,
                    CreateRequest(1, 1, 1, "  test.behavior-card.action-chain  "),
                    CreateReferences());
            Assert.IsTrue(normalized.Success, Describe(normalized.Diagnostics));
            Assert.AreEqual(
                "test.behavior-card.action-chain",
                normalized.Card.StableCardId);

            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult blank =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                    draft,
                    CreateRequest(1, 1, 1, "   "),
                    CreateReferences());
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult punctuation =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                    draft,
                    CreateRequest(1, 1, 1, ".invalid"),
                    CreateReferences());
            Assert.IsFalse(blank.Success);
            Assert.IsFalse(punctuation.Success);
            AssertDiagnostic(blank.Diagnostics, "PUBLISHED_CARD_STABLE_ID_INVALID");
            AssertDiagnostic(punctuation.Diagnostics, "PUBLISHED_CARD_STABLE_ID_INVALID");
        }

        private static CharacterTestRuntimeAiLabBehaviorCardDraft CreateDraft()
        {
            return CreateDraft(CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture());
        }

        private static CharacterTestRuntimeAiLabBehaviorCardDraft CreateDraft(
            CharacterTestRuntimeAiLabBehaviorCardPayload payload)
        {
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                cardDraftId: null,
                publishedStableId: "test.behavior-card.action-chain",
                displayName: "Pursuit Dash Strike",
                category: "Pursuit",
                payload: payload);
            draft.LogicPayload = CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(draft.Payload);
            ConfirmMigratedV7Intervals(draft.LogicPayload);
            return draft;
        }

        private static void ConfirmMigratedV7Intervals(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForWrite(payload);
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.RebuildLegacyProjection(payload);
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForRead(payload);
            CharacterTestRuntimeAiLabBehaviorRecipeV7Migration.SynchronizeForRead(payload);
            for (int timelineIndex = 0; timelineIndex < payload.Timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline = payload.Timelines[timelineIndex];
                for (int eventIndex = 0;
                     timeline != null && eventIndex < timeline.Events.Count;
                     eventIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = timeline.Events[eventIndex];
                    if (evt == null)
                        continue;
                    evt.DurationFrames = Math.Max(30, evt.DurationFrames);
                    evt.DurationReviewRequired = false;
                }
            }
            CharacterTestRuntimeAiLabBehaviorRecipeV7Migration.SynchronizeForWrite(payload);
        }

        private static CharacterTestRuntimeAiLabBehaviorCardDraft CreateLegacyCastAbilityDraft(
            int abilityId)
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload payload =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateReactiveDodgeAtomic();
            payload.Steps[0].Kind = CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly;
            payload.Steps[0].CharacterActionId = MxFramework.CharacterAction.CharacterActionId.None;
            payload.Steps[0].LegacyActionKind =
                CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot;
            payload.Steps[0].AbilityId = abilityId;
            return CreateDraft(payload);
        }

        private static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode CreateFactConditionNode(
            string nodeId,
            CharacterTestRuntimeAiLabCardConditionFact fact,
            int abilityId,
            string characterActionId)
        {
            return new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode
            {
                NodeId = nodeId,
                Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                Predicate = new CharacterTestRuntimeAiLabCardCondition
                {
                    ConditionId = nodeId,
                    Fact = fact,
                    Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                    BoolValue = true,
                    AbilityId = abilityId,
                    CharacterActionId = CharacterActionLegacyFixtureIds.From(characterActionId)
                }
            };
        }

        private static JObject ToLegacyV4LogicDocument(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            return new JObject
            {
                ["entryStageId"] = payload.EntryStageId,
                ["intent"] = payload.Intent,
                ["riskSummary"] = payload.RiskSummary,
                ["activationConditions"] = JArray.FromObject(payload.ActivationConditions),
                ["stages"] = JArray.FromObject(payload.Stages),
                ["detectors"] = JArray.FromObject(payload.Detectors),
                ["transitions"] = JArray.FromObject(payload.Transitions),
                ["layout"] = JObject.FromObject(payload.Layout)
            };
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult Publish(
            CharacterTestRuntimeAiLabBehaviorCardDraft draft,
            int revision,
            int minimumRuntimeVersion,
            int maximumRuntimeVersion,
            string stableCardId = null,
            int numericId = 1)
        {
            return CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                draft,
                CreateRequest(
                    revision,
                    minimumRuntimeVersion,
                    maximumRuntimeVersion,
                    stableCardId: stableCardId ?? draft.PublishedStableId,
                    responsibilities: draft.ResponsibilityKinds,
                    numericId: numericId),
                CreateReferences());
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest CreateRequest(
            int revision,
            int minimumRuntimeVersion,
            int maximumRuntimeVersion,
            string stableCardId = "test.behavior-card.action-chain",
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] responsibilities = null,
            int numericId = 1)
        {
            var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(
                "Pursuit Dash Strike",
                "ui.behavior-card.action-chain",
                cost: 3,
                shortDescription: "Approach the target and commit to a dash strike when in range.",
                useSummary: "Pursuit and engage",
                riskSummary: "Can be interrupted by higher-priority danger reactions.",
                tags: new[] { "Pursuit", "Engage" },
                requiredCapabilities: new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Targeting,
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Pursuit
                },
                responsibilities: responsibilities);
            var request = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(
                stableCardId,
                revision,
                minimumRuntimeVersion,
                maximumRuntimeVersion,
                metadata,
                numericId);
            return request;
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog CreateCatalog(
            string catalogId,
            int revision,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard[] cards)
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult result =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                    catalogId,
                    revision,
                    cards,
                    CreateReferences());
            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            return result.Catalog;
        }

        private static long ComputeCatalogHash(JObject document)
        {
            JArray cards = (JArray)document["cards"];
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable(
                "publishedCardCatalog.schema",
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.Schema);
            hash.AddStringStable("publishedCardCatalog.catalogId", document.Value<string>("catalogId"));
            hash.AddInt("publishedCardCatalog.revision", document.Value<int>("revision"));
            hash.AddInt("publishedCardCatalog.cardCount", cards.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                JObject card = (JObject)cards[i];
                hash.AddStringStable("publishedCardCatalog.cardStableId", card.Value<string>("stableCardId"));
                hash.AddInt("publishedCardCatalog.cardRevision", card.Value<int>("revision"));
                hash.AddLong("publishedCardCatalog.cardContentHash", card.Value<long>("contentHash"));
            }
            return hash.ToHash();
        }

        private static CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog CreateReferences()
        {
            CharacterTestCharacterActionRuntime actionRuntime =
                CharacterTestCharacterActionConfigSource.Load();
            return new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                CharacterTestAbilityCatalogSource.CreateRegistry(),
                actionRuntime.Registry);
        }

        private static bool ContainsCharacterAction(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            int actionId)
        {
            for (int timelineIndex = 0; timelineIndex < payload.Timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline =
                    payload.Timelines[timelineIndex];
                for (int eventIndex = 0;
                     timeline != null && eventIndex < timeline.Events.Count;
                     eventIndex++)
                {
                    if (timeline.Events[eventIndex]?.Operation?.CharacterActionId.Value == actionId)
                        return true;
                }
            }
            return false;
        }

        private static void AssertDiagnostic(
            System.Collections.Generic.IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            string code)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (string.Equals(diagnostics[i].Code, code, StringComparison.Ordinal))
                    return;
            }
            Assert.Fail("Expected diagnostic " + code + ". Actual: " + Describe(diagnostics));
        }

        private static string Describe(
            System.Collections.Generic.IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (i > 0)
                    builder.Append(" | ");
                builder.Append(diagnostics[i].Code)
                    .Append('@')
                    .Append(diagnostics[i].Path)
                    .Append(':')
                    .Append(diagnostics[i].Message);
            }
            return builder.ToString();
        }
    }

    internal static class CharacterTestRuntimeAiPlannerLegacyCatalogTestFixture
    {
        public static string CreateLegacyV8Json(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            string changedStableCardId = "")
        {
            JObject document = JObject.Parse(
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(catalog));
            JArray cards = (JArray)document["cards"];
            int legacyRuntimeVersion =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion - 1;
            for (int i = 0; i < cards.Count; i++)
            {
                JObject cardDocument = (JObject)cards[i];
                string stableCardId = cardDocument.Value<string>("stableCardId");
                int revision = cardDocument.Value<int>("revision");
                if (!catalog.TryGetExact(
                        stableCardId,
                        revision,
                        out CharacterTestRuntimeAiPlannerPublishedBehaviorCard card))
                {
                    throw new InvalidOperationException(
                        "Legacy catalog fixture could not resolve "
                        + stableCardId
                        + "@"
                        + revision
                        + ".");
                }

                CharacterTestRuntimeAiLabBehaviorCardLogicPayload legacyLogic = card.CloneLogic();
                if (string.Equals(
                        stableCardId,
                        changedStableCardId,
                        StringComparison.Ordinal))
                {
                    MutateFirstEventPriority(
                        legacyLogic,
                        (JObject)cardDocument["logic"]);
                }
                long semanticHash = CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash
                    .ComputeLegacyV8(legacyLogic);
                long contentHash =
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(
                        card.StableCardId,
                        card.Revision,
                        legacyRuntimeVersion,
                        legacyRuntimeVersion,
                        card.Metadata,
                        semanticHash);
                cardDocument["minimumRuntimeContractVersion"] = legacyRuntimeVersion;
                cardDocument["maximumRuntimeContractVersion"] = legacyRuntimeVersion;
                cardDocument["semanticHash"] = semanticHash;
                cardDocument["contentHash"] = contentHash;
                cardDocument["logicSchema"] =
                    CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV8Schema;
                ((JObject)cardDocument["logic"])["behaviorRecipeVersion"] = 8;
            }

            document["catalogHash"] = ComputeCatalogHash(document);
            if (!TryReplaceCharacterActionIds(document, out string error))
                throw new InvalidOperationException(error);
            return document.ToString();
        }

        private static void MutateFirstEventPriority(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic,
            JObject logicDocument)
        {
            JArray timelineDocuments = (JArray)logicDocument["timelines"];
            for (int timelineIndex = 0; timelineIndex < logic.Timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline =
                    logic.Timelines[timelineIndex];
                if (timeline == null || timeline.Events.Count == 0)
                    continue;
                timeline.Events[0].Priority++;
                JObject timelineDocument = (JObject)timelineDocuments[timelineIndex];
                JObject eventDocument = (JObject)((JArray)timelineDocument["events"])[0];
                eventDocument["priority"] = timeline.Events[0].Priority;
                return;
            }
            throw new InvalidOperationException(
                "Legacy catalog fixture requires at least one event to mutate.");
        }

        private static bool TryReplaceCharacterActionIds(JToken token, out string error)
        {
            error = string.Empty;
            if (token is JObject objectValue)
            {
                foreach (JProperty property in objectValue.Properties())
                {
                    if (string.Equals(
                            property.Name,
                            "characterActionId",
                            StringComparison.Ordinal)
                        && property.Value.Type == JTokenType.Integer)
                    {
                        var actionId = new MxFramework.CharacterAction.CharacterActionId(
                            (int)property.Value);
                        if (!CharacterTestRuntimeAiLabBehaviorRecipeV6SemanticHash
                            .TryFormatLegacyActionId(actionId, out string legacyActionId))
                        {
                            error = "Legacy catalog fixture does not support CharacterAction "
                                + actionId.Value
                                + ".";
                            return false;
                        }
                        property.Value = legacyActionId;
                        continue;
                    }

                    if (!TryReplaceCharacterActionIds(property.Value, out error))
                        return false;
                }
                return true;
            }

            if (token is JArray arrayValue)
            {
                for (int i = 0; i < arrayValue.Count; i++)
                {
                    if (!TryReplaceCharacterActionIds(arrayValue[i], out error))
                        return false;
                }
            }
            return true;
        }

        private static long ComputeCatalogHash(JObject document)
        {
            JArray cards = (JArray)document["cards"];
            var ordered = new List<JObject>(cards.Count);
            for (int i = 0; i < cards.Count; i++)
                ordered.Add((JObject)cards[i]);
            ordered.Sort((left, right) =>
            {
                int stableId = string.Compare(
                    left.Value<string>("stableCardId"),
                    right.Value<string>("stableCardId"),
                    StringComparison.Ordinal);
                return stableId != 0
                    ? stableId
                    : left.Value<int>("revision").CompareTo(right.Value<int>("revision"));
            });

            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable(
                "publishedCardCatalog.schema",
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.Schema);
            hash.AddStringStable(
                "publishedCardCatalog.catalogId",
                document.Value<string>("catalogId"));
            hash.AddInt("publishedCardCatalog.revision", document.Value<int>("revision"));
            hash.AddInt("publishedCardCatalog.cardCount", ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                hash.AddStringStable(
                    "publishedCardCatalog.cardStableId",
                    ordered[i].Value<string>("stableCardId"));
                hash.AddInt(
                    "publishedCardCatalog.cardRevision",
                    ordered[i].Value<int>("revision"));
                hash.AddLong(
                    "publishedCardCatalog.cardContentHash",
                    ordered[i].Value<long>("contentHash"));
            }
            return hash.ToHash();
        }
    }
}
