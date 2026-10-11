using System;
using System.Collections.Generic;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    [TestFixture]
    public sealed class CharacterTestRuntimeAiPlannerDeckPersistenceTests
    {
        [Test]
        public void UnlockedCollection_NormalizesSortsAndRemainsImmutable()
        {
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult result =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(new[]
                {
                    " card.zeta ",
                    "card.alpha"
                });

            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            CollectionAssert.AreEqual(new[] { "card.alpha", "card.zeta" }, result.Collection.StableCardIds);
            Assert.IsFalse(result.Collection.StableCardIds is string[]);
        }

        [Test]
        public void TryUnlock_RequiresCompatiblePublishedCardAndDoesNotMutateSource()
        {
            CharacterTestRuntimeAiPlannerUnlockedCardCollection source = CreateUnlocked("card.alpha");
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 1),
                Publish("card.beta", 1, 2));

            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult success =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.TryUnlock(source, "card.beta", catalog, 1);
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unavailable =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.TryUnlock(source, "card.missing", catalog, 1);

            Assert.IsTrue(success.Success, Describe(success.Diagnostics));
            Assert.IsTrue(success.Collection.Contains("card.beta"));
            Assert.IsFalse(source.Contains("card.beta"));
            Assert.IsFalse(unavailable.Success);
            AssertDiagnostic(unavailable.Diagnostics, "DECK_PERSISTENCE_UNLOCK_CARD_UNAVAILABLE");
        }

        [Test]
        public void Snapshot_RejectsInstalledCardThatIsNotUnlocked()
        {
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult result =
                CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.ValidateAndCreate(
                    CreateUnlocked("card.alpha"),
                    new[]
                    {
                        new CharacterTestRuntimeAiPlannerPersistedDeck("character.hero", new[] { "card.beta" }, 1)
                    });

            Assert.IsFalse(result.Success);
            AssertDiagnostic(result.Diagnostics, "DECK_PERSISTENCE_DECK_CARD_LOCKED");
        }

        [Test]
        public void Snapshot_NormalizesDeckOrderAndProducesStableHash()
        {
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlocked = CreateUnlocked("card.alpha", "card.beta");
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult first =
                CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.ValidateAndCreate(
                    unlocked,
                    new[]
                    {
                        new CharacterTestRuntimeAiPlannerPersistedDeck("character.zeta", new[] { "card.beta" }, 1),
                        new CharacterTestRuntimeAiPlannerPersistedDeck("character.alpha", new[] { "card.alpha" }, 1)
                    });
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult second =
                CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.ValidateAndCreate(
                    unlocked,
                    new[]
                    {
                        new CharacterTestRuntimeAiPlannerPersistedDeck("character.alpha", new[] { "card.alpha" }, 1),
                        new CharacterTestRuntimeAiPlannerPersistedDeck("character.zeta", new[] { "card.beta" }, 1)
                    });

            Assert.IsTrue(first.Success, Describe(first.Diagnostics));
            Assert.IsTrue(second.Success, Describe(second.Diagnostics));
            Assert.AreEqual("character.alpha", first.Snapshot.Decks[0].CharacterStableId);
            Assert.AreEqual(first.Snapshot.PersistenceHash, second.Snapshot.PersistenceHash);
        }

        [Test]
        public void Codec_RoundTripResolvesLatestCompatiblePublishedRevision()
        {
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot snapshot = CreateSnapshot(
                new[] { "card.alpha" },
                new CharacterTestRuntimeAiPlannerPersistedDeck("character.hero", new[] { "card.alpha" }, 1));
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 1),
                Publish("card.alpha", 2, 2));

            RuntimeCustomState customState = CharacterTestRuntimeAiPlannerDeckPersistenceCodec.Capture(snapshot);
            CharacterTestRuntimeAiPlannerDeckPersistenceRestoreResult restore =
                CharacterTestRuntimeAiPlannerDeckPersistenceCodec.Restore(customState, catalog, 1, null);

            Assert.IsTrue(restore.Success, Describe(restore.Diagnostics));
            Assert.IsTrue(restore.FullyResolved);
            Assert.IsTrue(restore.TryGetResolvedDeck("character.hero", out CharacterTestRuntimeAiPlannerInstalledDeck deck));
            Assert.AreEqual(2, deck.Cards[0].Revision);
            Assert.AreEqual(snapshot.PersistenceHash, restore.Snapshot.PersistenceHash);
        }

        [Test]
        public void Codec_EmptyDeckRoundTripPreservesFallbackOnlyWarning()
        {
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot snapshot = CreateSnapshot(
                Array.Empty<string>(),
                new CharacterTestRuntimeAiPlannerPersistedDeck(
                    "character.hero",
                    Array.Empty<string>(),
                    1));
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.catalog-anchor", 1, 1));

            CharacterTestRuntimeAiPlannerDeckPersistenceRestoreResult restore =
                CharacterTestRuntimeAiPlannerDeckPersistenceCodec.Restore(
                    CharacterTestRuntimeAiPlannerDeckPersistenceCodec.Capture(snapshot),
                    catalog,
                    1,
                    null);

            Assert.IsTrue(restore.Success, Describe(restore.Diagnostics));
            Assert.IsTrue(restore.FullyResolved);
            Assert.IsTrue(restore.TryGetResolvedDeck("character.hero", out CharacterTestRuntimeAiPlannerInstalledDeck deck));
            Assert.AreEqual(0, deck.Cards.Count);
            AssertDiagnosticSeverity(
                restore.Diagnostics,
                "INSTALLED_DECK_FALLBACK_ONLY",
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning);
        }

        [Test]
        public void RuntimeSaveStateParticipant_RoundTripsThroughFrameworkJson()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(Publish("card.alpha", 1, 1));
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot snapshot = CreateSnapshot(
                new[] { "card.alpha" },
                new CharacterTestRuntimeAiPlannerPersistedDeck("character.hero", new[] { "card.alpha" }, 1));
            var source = new CharacterTestRuntimeAiPlannerDeckPersistenceParticipant(
                snapshot,
                catalog,
                1,
                utcNow: () => new DateTime(2026, 7, 17, 0, 0, 0, DateTimeKind.Utc));

            RuntimeSaveStateResult<RuntimeSaveState> captured = source.CaptureSaveState();
            string json = RuntimeSaveStateJson.SaveToJson(captured.Value);
            RuntimeSaveStateResult<RuntimeSaveState> loaded = RuntimeSaveStateJson.LoadFromJson(json);
            var target = new CharacterTestRuntimeAiPlannerDeckPersistenceParticipant(
                CreateSnapshot(Array.Empty<string>()),
                catalog,
                1);
            RuntimeSaveStateResult<bool> restored = target.RestoreSaveState(loaded.Value);

            Assert.IsTrue(captured.Success);
            Assert.IsTrue(loaded.Success, loaded.Error.ToString());
            Assert.IsTrue(restored.Success, restored.Error.ToString());
            Assert.AreEqual(snapshot.PersistenceHash, target.CurrentSnapshot.PersistenceHash);
            Assert.AreEqual(1, target.ResolvedDecks.Count);
        }

        [Test]
        public void RuntimeSaveStateParticipant_RejectsNullModuleEntryLoadedByFrameworkJson()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 1));
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot snapshot = CreateSnapshot(
                new[] { "card.alpha" });
            var source = new CharacterTestRuntimeAiPlannerDeckPersistenceParticipant(snapshot, catalog, 1);
            RuntimeSaveState captured = source.CaptureSaveState().Value;
            JObject document = JObject.Parse(RuntimeSaveStateJson.SaveToJson(captured));
            ((JArray)document["moduleStates"]).Insert(0, JValue.CreateNull());

            RuntimeSaveStateResult<RuntimeSaveState> loaded = RuntimeSaveStateJson.LoadFromJson(
                document.ToString());
            var target = new CharacterTestRuntimeAiPlannerDeckPersistenceParticipant(snapshot, catalog, 1);
            RuntimeSaveStateResult<bool> restore = target.RestoreSaveState(loaded.Value);

            Assert.IsTrue(loaded.Success, loaded.Error.ToString());
            Assert.IsFalse(restore.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, restore.Error.Code);
            Assert.AreEqual("$.moduleStates[0]", restore.Error.Path);
            Assert.AreEqual(snapshot.PersistenceHash, target.CurrentSnapshot.PersistenceHash);
        }

        [Test]
        public void Restore_MigratesStableIdsInOwnershipAndCharacterDeck()
        {
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot legacy = CreateSnapshot(
                new[] { "card.legacy" },
                new CharacterTestRuntimeAiPlannerPersistedDeck("character.hero", new[] { "card.legacy" }, 1));
            RuntimeCustomState state = CharacterTestRuntimeAiPlannerDeckPersistenceCodec.Capture(legacy);
            var migrations = new CharacterTestRuntimeAiPlannerStableCardIdMigrationMap(new[]
            {
                new CharacterTestRuntimeAiPlannerStableCardIdMigration("card.legacy", "card.current")
            });
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(Publish("card.current", 1, 1));

            CharacterTestRuntimeAiPlannerDeckPersistenceRestoreResult restore =
                CharacterTestRuntimeAiPlannerDeckPersistenceCodec.Restore(state, catalog, 1, migrations);

            Assert.IsTrue(restore.Success, Describe(restore.Diagnostics));
            Assert.IsTrue(restore.FullyResolved);
            Assert.IsTrue(restore.Snapshot.UnlockedCards.Contains("card.current"));
            Assert.IsFalse(restore.Snapshot.UnlockedCards.Contains("card.legacy"));
            AssertDiagnosticSeverity(
                restore.Diagnostics,
                "DECK_PERSISTENCE_STABLE_CARD_ID_MIGRATED",
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Info);
        }

        [Test]
        public void MigrationMap_RejectsCycles()
        {
            var migrations = new CharacterTestRuntimeAiPlannerStableCardIdMigrationMap(new[]
            {
                new CharacterTestRuntimeAiPlannerStableCardIdMigration("card.alpha", "card.beta"),
                new CharacterTestRuntimeAiPlannerStableCardIdMigration("card.beta", "card.alpha")
            });

            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic[] diagnostics = migrations.Validate();

            AssertDiagnostic(diagnostics, "DECK_PERSISTENCE_MIGRATION_CYCLE");
        }

        [Test]
        public void Restore_PreservesMissingOwnershipAndDoesNotSilentlyActivateFallbackDeck()
        {
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot snapshot = CreateSnapshot(
                new[] { "card.missing" },
                new CharacterTestRuntimeAiPlannerPersistedDeck("character.hero", new[] { "card.missing" }, 1));
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(Publish("card.other", 1, 1));

            CharacterTestRuntimeAiPlannerDeckPersistenceRestoreResult restore =
                CharacterTestRuntimeAiPlannerDeckPersistenceCodec.Restore(
                    CharacterTestRuntimeAiPlannerDeckPersistenceCodec.Capture(snapshot),
                    catalog,
                    1,
                    null);

            Assert.IsTrue(restore.Success, Describe(restore.Diagnostics));
            Assert.IsFalse(restore.FullyResolved);
            Assert.IsTrue(restore.Snapshot.UnlockedCards.Contains("card.missing"));
            Assert.IsTrue(restore.Snapshot.TryGetDeck("character.hero", out _));
            Assert.IsFalse(restore.TryGetResolvedDeck("character.hero", out _));
            AssertDiagnosticSeverity(
                restore.Diagnostics,
                "DECK_PERSISTENCE_DECK_CARD_UNAVAILABLE",
                CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Warning);
        }

        [Test]
        public void Restore_TreatsIncompatibleRevisionAsUnavailableWithoutDroppingSaveReference()
        {
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot snapshot = CreateSnapshot(
                new[] { "card.future" },
                new CharacterTestRuntimeAiPlannerPersistedDeck("character.hero", new[] { "card.future" }, 1));
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.future", 1, 1, minimumRuntimeVersion: 2, maximumRuntimeVersion: 2));

            CharacterTestRuntimeAiPlannerDeckPersistenceRestoreResult restore =
                CharacterTestRuntimeAiPlannerDeckPersistenceCodec.Restore(
                    CharacterTestRuntimeAiPlannerDeckPersistenceCodec.Capture(snapshot),
                    catalog,
                    1,
                    null);

            Assert.IsTrue(restore.Success, Describe(restore.Diagnostics));
            Assert.IsFalse(restore.FullyResolved);
            Assert.IsTrue(restore.Snapshot.UnlockedCards.Contains("card.future"));
            AssertDiagnostic(restore.Diagnostics, "DECK_PERSISTENCE_UNLOCKED_CARD_UNAVAILABLE");
        }

        [Test]
        public void Restore_RejectsTamperedPayloadHashEvenWhenMigrationExists()
        {
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot snapshot = CreateSnapshot(new[] { "card.legacy" });
            RuntimeCustomState captured = CharacterTestRuntimeAiPlannerDeckPersistenceCodec.Capture(snapshot);
            JObject payload = JObject.Parse(captured.PayloadJson);
            payload["unlockedCardIds"][0] = "card.tampered";
            var tampered = new RuntimeCustomState(
                captured.NumericTypeId,
                captured.TypeId,
                captured.SchemaVersion,
                payload.ToString());
            var migrations = new CharacterTestRuntimeAiPlannerStableCardIdMigrationMap(new[]
            {
                new CharacterTestRuntimeAiPlannerStableCardIdMigration("card.legacy", "card.current")
            });

            CharacterTestRuntimeAiPlannerDeckPersistenceRestoreResult restore =
                CharacterTestRuntimeAiPlannerDeckPersistenceCodec.Restore(
                    tampered,
                    CreateCatalog(Publish("card.current", 1, 1)),
                    1,
                    migrations);

            Assert.IsFalse(restore.Success);
            AssertDiagnostic(restore.Diagnostics, "DECK_PERSISTENCE_HASH_MISMATCH");
        }

        [Test]
        public void Participant_RestoreFailureIsAtomic()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(Publish("card.alpha", 1, 1));
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot original = CreateSnapshot(new[] { "card.alpha" });
            var participant = new CharacterTestRuntimeAiPlannerDeckPersistenceParticipant(original, catalog, 1);
            var invalidModule = new RuntimeModuleSaveState(
                CharacterTestRuntimeAiPlannerDeckPersistenceParticipant.ModuleId,
                1,
                new RuntimeCustomState(
                    RuntimeHashSchemaIds.RuntimeAiPlannerDeckPersistence,
                    CharacterTestRuntimeAiPlannerDeckPersistenceCodec.CustomStateTypeId,
                    1,
                    "{ invalid"));
            var save = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                null,
                null,
                new[] { invalidModule });

            RuntimeSaveStateResult<bool> restore = participant.RestoreSaveState(save);

            Assert.IsFalse(restore.Success);
            Assert.AreEqual(original.PersistenceHash, participant.CurrentSnapshot.PersistenceHash);
        }

        [Test]
        public void Snapshot_RejectsDuplicateCharacterRecords()
        {
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlocked = CreateUnlocked("card.alpha");
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult result =
                CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.ValidateAndCreate(
                    unlocked,
                    new[]
                    {
                        new CharacterTestRuntimeAiPlannerPersistedDeck("character.hero", new[] { "card.alpha" }, 1),
                        new CharacterTestRuntimeAiPlannerPersistedDeck("character.hero", Array.Empty<string>(), 1)
                    });

            Assert.IsFalse(result.Success);
            AssertDiagnostic(result.Diagnostics, "DECK_PERSISTENCE_CHARACTER_DUPLICATE");
        }

        [Test]
        public void TrySaveInstalledDeck_IsAtomicAndRequiresUnlockedCards()
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog = CreateCatalog(
                Publish("card.alpha", 1, 1),
                Publish("card.beta", 1, 1));
            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult installed =
                CharacterTestRuntimeAiPlannerInstalledDeckRules.ValidateAndCreate(
                    new CharacterTestRuntimeAiPlannerInstalledDeckCandidate(
                        "character.hero",
                        new[] { "card.alpha" },
                        1),
                    catalog,
                    1);
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot source = CreateSnapshot(new[] { "card.alpha" });

            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult saved =
                CharacterTestRuntimeAiPlannerDeckPersistenceRules.TrySaveInstalledDeck(source, installed.Deck);

            Assert.IsTrue(saved.Success, Describe(saved.Diagnostics));
            Assert.IsFalse(source.TryGetDeck("character.hero", out _));
            Assert.IsTrue(saved.Snapshot.TryGetDeck("character.hero", out CharacterTestRuntimeAiPlannerPersistedDeck persisted));
            CollectionAssert.AreEqual(new[] { "card.alpha" }, persisted.StableCardIds);

            CharacterTestRuntimeAiPlannerInstalledDeckBuildResult lockedInstalled =
                CharacterTestRuntimeAiPlannerInstalledDeckRules.ValidateAndCreate(
                    new CharacterTestRuntimeAiPlannerInstalledDeckCandidate(
                        "character.hero",
                        new[] { "card.beta" },
                        1),
                    catalog,
                    1);
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult rejected =
                CharacterTestRuntimeAiPlannerDeckPersistenceRules.TrySaveInstalledDeck(source, lockedInstalled.Deck);
            Assert.IsFalse(rejected.Success);
            AssertDiagnostic(rejected.Diagnostics, "DECK_PERSISTENCE_DECK_CARD_LOCKED");
            Assert.IsFalse(source.TryGetDeck("character.hero", out _));
        }

        private static CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot CreateSnapshot(
            IEnumerable<string> unlockedCardIds,
            params CharacterTestRuntimeAiPlannerPersistedDeck[] decks)
        {
            CharacterTestRuntimeAiPlannerDeckPersistenceSnapshotResult result =
                CharacterTestRuntimeAiPlannerDeckPersistenceSnapshot.ValidateAndCreate(
                    CreateUnlocked(unlockedCardIds),
                    decks);
            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            return result.Snapshot;
        }

        private static CharacterTestRuntimeAiPlannerUnlockedCardCollection CreateUnlocked(
            params string[] stableCardIds)
        {
            return CreateUnlocked((IEnumerable<string>)stableCardIds);
        }

        private static CharacterTestRuntimeAiPlannerUnlockedCardCollection CreateUnlocked(
            IEnumerable<string> stableCardIds)
        {
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult result =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(stableCardIds);
            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            return result.Collection;
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCard Publish(
            string stableCardId,
            int revision,
            int cost,
            int minimumRuntimeVersion = 1,
            int maximumRuntimeVersion = 1)
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload payload =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture();
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                cardDraftId: null,
                publishedStableId: stableCardId,
                displayName: stableCardId,
                category: "Test",
                payload: payload);
            draft.LogicPayload = CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(payload);
            var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(
                stableCardId,
                "ui." + stableCardId,
                cost,
                "Test card",
                "Test use",
                "Test risk");
            var request = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(
                stableCardId,
                revision,
                minimumRuntimeVersion,
                maximumRuntimeVersion,
                metadata);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult result =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                    draft,
                    request,
                    CreateReferences());
            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            return result.Card;
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog CreateCatalog(
            params CharacterTestRuntimeAiPlannerPublishedBehaviorCard[] cards)
        {
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult result =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                    "character-test.behavior-cards",
                    1,
                    cards,
                    CreateReferences());
            Assert.IsTrue(result.Success, Describe(result.Diagnostics));
            return result.Catalog;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog CreateReferences()
        {
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            return new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                CharacterTestAbilityCatalogSource.CreateRegistry(),
                actionRuntime.Registry);
        }

        private static void AssertDiagnostic(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            string code)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (string.Equals(diagnostics[i].Code, code, StringComparison.Ordinal))
                    return;
            }
            Assert.Fail("Expected diagnostic " + code + ". Actual: " + Describe(diagnostics));
        }

        private static void AssertDiagnosticSeverity(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            string code,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity expected)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (!string.Equals(diagnostics[i].Code, code, StringComparison.Ordinal))
                    continue;
                Assert.AreEqual(expected, diagnostics[i].Severity);
                return;
            }
            Assert.Fail("Expected diagnostic " + code + ". Actual: " + Describe(diagnostics));
        }

        private static string Describe(
            IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics)
        {
            var lines = new List<string>();
            for (int i = 0; i < diagnostics.Count; i++)
                lines.Add(diagnostics[i].Severity + " " + diagnostics[i].Code + " " + diagnostics[i].Path + " " + diagnostics[i].Message);
            return string.Join(" | ", lines);
        }

        private static string Describe(IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            var lines = new List<string>();
            for (int i = 0; i < diagnostics.Count; i++)
                lines.Add(diagnostics[i].Code + " " + diagnostics[i].Path + " " + diagnostics[i].Message);
            return string.Join(" | ", lines);
        }
    }
}
