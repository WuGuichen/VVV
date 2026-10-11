using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiLabPublishTests
    {
        private const int CharacterId = CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId;

        [Test]
        public void SemanticCopyOnWriteDraft_PublishesSessionRevisionButRestartUsesProjectCatalog()
        {
            string directory = CreateTemporaryDirectory();
            string deckPath = Path.Combine(directory, "deck.json");
            try
            {
                CharacterTestRuntimeAiPlannerLocalPlayerDeckService service = CreateService(deckPath);
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard source = ResolveFirstLatest(service);
                int sourceCatalogRevision = service.Catalog.Revision;
                Assert.That(service.Workbench.TryInstall(source.StableCardId).Success, Is.True);
                CreateIsolatedDraft(source, out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                    out Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft draft);
                BumpSemantic(draft);

                CharacterTestRuntimeAiLabPublishResult result = Publish(service, group, cards, draft);

                Assert.That(result.Success, Is.True, Describe(result.Diagnostics));
                Assert.That(result.PublishedCard.StableCardId, Is.EqualTo(source.StableCardId));
                Assert.That(result.PublishedCard.Revision, Is.EqualTo(source.Revision + 1));
                Assert.That(result.Catalog.Revision, Is.EqualTo(sourceCatalogRevision + 1));
                Assert.That(service.Catalog.CatalogHash, Is.EqualTo(result.Catalog.CatalogHash));
                Assert.That(service.TryResolveLatestPublishedCard(source.StableCardId, out var latest), Is.True);
                Assert.That(latest.Revision, Is.EqualTo(source.Revision + 1));
                Assert.That(service.Workbench, Is.Not.Null);
                Assert.That(
                    service.Snapshot.TryGetDeck(
                        "character.definition." + CharacterId,
                        out CharacterTestRuntimeAiPlannerPersistedDeck persisted),
                    Is.True);
                Assert.That(persisted.StableCardIds.Count, Is.EqualTo(1));
                Assert.That(persisted.StableCardIds[0], Is.EqualTo(source.StableCardId));

                string catalogPath = Path.Combine(directory, "deck.published-behavior-cards.json");
                Assert.That(File.Exists(catalogPath), Is.True);
                Assert.That(
                    CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                        deckPath,
                        CharacterId,
                        out CharacterTestRuntimeAiPlannerLocalPlayerDeckService restored,
                        out string restoreError),
                    Is.True,
                    restoreError);
                Assert.That(
                    restored.Catalog.CatalogId,
                    Is.EqualTo(result.Catalog.CatalogId));
                Assert.That(restored.TryResolveLatestPublishedCard(source.StableCardId, out latest), Is.True);
                Assert.That(latest.Revision, Is.EqualTo(source.Revision));
                Assert.That(restored.Workbench.Deck.SlotCount, Is.EqualTo(1));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void SuccessfulPublish_AdoptsRevisionAndClosesPrePublishUndoHistory()
        {
            string directory = CreateTemporaryDirectory();
            string draftDirectory = Path.Combine(directory, "drafts");
            try
            {
                CharacterTestRuntimeAiPlannerLocalPlayerDeckService service =
                    CreateService(Path.Combine(directory, "deck.json"));
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard source = ResolveFirstLatest(service);
                var persistence = new CharacterTestRuntimeAiLabDraftPersistence(draftDirectory);
                CharacterTestRuntimeAiLabDraftWorkspace workspace =
                    persistence.OpenOrCreate(CharacterTestArenaMatchConfig.CreateDefault());
                Assert.That(
                    workspace.TryOpenPublishedCardForSlot(
                        CharacterTestArenaMatchConfig.Team1Id,
                        0,
                        source,
                        out int entryIndex,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft draft,
                        out _,
                        out string message),
                    Is.True,
                    message);
                CharacterTestRuntimeAiLabBehaviorCardLogicPayload edited = draft.LogicPayload.Clone();
                BumpSemantic(edited);
                Assert.That(
                    workspace.TryReplaceCardLogicPayload(
                        CharacterTestArenaMatchConfig.Team1Id,
                        0,
                        entryIndex,
                        edited,
                        out message),
                    Is.True,
                    message);
                Assert.That(workspace.CanUndo, Is.True);
                Assert.That(
                    workspace.TryGetGroupForSlot(
                        CharacterTestArenaMatchConfig.Team1Id,
                        0,
                        out CharacterTestRuntimeAiLabBehaviorGroupDraft group),
                    Is.True);

                CharacterTestRuntimeAiLabPublishResult result = Publish(
                    service,
                    group,
                    workspace.Cards,
                    draft);

                Assert.That(result.Success, Is.True, Describe(result.Diagnostics));
                Assert.That(
                    workspace.TryAdoptPublishedRevision(
                        CharacterTestArenaMatchConfig.Team1Id,
                        0,
                        entryIndex,
                        result.PublishedCard,
                        out message),
                    Is.True,
                    message);
                Assert.That(draft.SourcePublishedRevision, Is.EqualTo(result.PublishedCard.Revision));
                Assert.That(draft.SourcePublishedSemanticHash, Is.EqualTo(result.PublishedCard.SemanticHash));
                Assert.That(draft.SourcePublishedContentHash, Is.EqualTo(result.PublishedCard.ContentHash));
                Assert.That(workspace.CanUndo, Is.False, "Publish closes the pre-publish undo checkpoint.");
                Assert.That(
                    CharacterTestRuntimeAiLabBehaviorCardLogicCompiler
                        .Compile(draft.LogicPayload).Program.SemanticHash,
                    Is.EqualTo(result.PublishedCard.SemanticHash));

                persistence.MarkDirty();
                Assert.That(persistence.Flush(), Is.True, persistence.LastStatus);
                Assert.That(
                    persistence.TryLoad(workspace.WorkspaceId, out var loaded, out message),
                    Is.True,
                    message);
                Assert.That(
                    loaded.TryGetGroupForSlot(
                        CharacterTestArenaMatchConfig.Team1Id,
                        0,
                        out CharacterTestRuntimeAiLabBehaviorGroupDraft loadedGroup),
                    Is.True);
                CharacterTestRuntimeAiLabBehaviorCardDraft loadedDraft =
                    loaded.Cards[loadedGroup.Entries[0].CardDraftId];
                Assert.That(loadedDraft.SourcePublishedRevision, Is.EqualTo(result.PublishedCard.Revision));
                Assert.That(loadedDraft.SourcePublishedSemanticHash, Is.EqualTo(result.PublishedCard.SemanticHash));
                Assert.That(loadedDraft.SourcePublishedContentHash, Is.EqualTo(result.PublishedCard.ContentHash));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void UnchangedDraft_IsRejectedWithoutCatalogMutation()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                string deckPath = Path.Combine(directory, "deck.json");
                CharacterTestRuntimeAiPlannerLocalPlayerDeckService service = CreateService(deckPath);
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard source = ResolveFirstLatest(service);
                CreateIsolatedDraft(source, out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                    out Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft draft);
                long originalHash = service.Catalog.CatalogHash;

                CharacterTestRuntimeAiLabPublishResult result = Publish(service, group, cards, draft);

                Assert.That(result.Success, Is.False);
                AssertDiagnostic(result.Diagnostics, "LAB_PUBLISH_NO_SEMANTIC_CHANGES");
                Assert.That(service.Catalog.CatalogHash, Is.EqualTo(originalHash));
                Assert.That(File.Exists(Path.Combine(directory, "deck.published-behavior-cards.json")), Is.False);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void NonPlayerLegalComposition_IsRejectedAtomically()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                string deckPath = Path.Combine(directory, "deck.json");
                CharacterTestRuntimeAiPlannerLocalPlayerDeckService service = CreateService(deckPath);
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard source = ResolveFirstLatest(service);
                CreateIsolatedDraft(source, out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                    out Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft draft);
                BumpSemantic(draft);
                group.Entries[0].Disabled = true;
                group.Entries[0].RuntimeEntry.Disabled = true;
                long originalHash = service.Catalog.CatalogHash;

                CharacterTestRuntimeAiLabPublishResult result = Publish(service, group, cards, draft);

                Assert.That(result.Success, Is.False);
                AssertDiagnostic(result.Diagnostics, "LAB_PLAYER_CARD_DISABLED");
                Assert.That(service.Catalog.CatalogHash, Is.EqualTo(originalHash));
                Assert.That(File.Exists(Path.Combine(directory, "deck.published-behavior-cards.json")), Is.False);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void MalformedPersistedCatalog_DoesNotReplaceFoundationAuthority()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                string deckPath = Path.Combine(directory, "deck.json");
                File.WriteAllText(
                    Path.Combine(directory, "deck.published-behavior-cards.json"),
                    "{ malformed catalog");

                bool success = CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                    deckPath,
                    CharacterId,
                    out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
                    out string error);

                Assert.That(success, Is.True, error);
                Assert.That(service, Is.Not.Null);
                Assert.That(service.Catalog.CatalogId, Is.EqualTo(
                    "character-test.runtime-ai-planner.project-behavior-cards"));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void CatalogWriteFailure_DoesNotSwitchInMemoryAuthority()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                string deckPath = Path.Combine(directory, "deck.json");
                CharacterTestRuntimeAiPlannerLocalPlayerDeckService service = CreateService(deckPath);
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard source = ResolveFirstLatest(service);
                CreateIsolatedDraft(source, out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                    out Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft draft);
                BumpSemantic(draft);
                long originalHash = service.Catalog.CatalogHash;
                string catalogPath = Path.Combine(directory, "deck.published-behavior-cards.json");
                Directory.CreateDirectory(catalogPath);

                CharacterTestRuntimeAiLabPublishResult result = Publish(service, group, cards, draft);

                Assert.That(result.Success, Is.False);
                AssertDiagnostic(result.Diagnostics, "LAB_PUBLISH_CATALOG_WRITE_FAILED");
                Assert.That(service.Catalog.CatalogHash, Is.EqualTo(originalHash));
                Assert.That(service.TryResolveLatestPublishedCard(source.StableCardId, out var latest), Is.True);
                Assert.That(latest.Revision, Is.EqualTo(source.Revision));
                Assert.That(service.Workbench, Is.Not.Null);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void RuntimeProfileProjectionDivergence_IsRejected()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                CharacterTestRuntimeAiPlannerLocalPlayerDeckService service =
                    CreateService(Path.Combine(directory, "deck.json"));
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard source = ResolveFirstLatest(service);
                CreateIsolatedDraft(source, out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                    out Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft draft);
                BumpSemantic(draft);
                group.Profile.Entries.Add(new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    enabled: true,
                    order: 20));

                CharacterTestRuntimeAiLabPublishResult result = Publish(service, group, cards, draft);

                Assert.That(result.Success, Is.False);
                AssertDiagnostic(result.Diagnostics, "LAB_PUBLISH_PROFILE_PROJECTION_MISMATCH");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void ForeignButSelfConsistentCatalog_DoesNotReplaceFoundationAuthority()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                string deckPath = Path.Combine(directory, "deck.json");
                CharacterTestRuntimeAiPlannerLocalPlayerDeckService service = CreateService(deckPath);
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult foreign =
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                        "foreign.behavior-cards",
                        service.Catalog.Revision,
                        service.Catalog.Cards,
                        CreateReferences());
                Assert.That(foreign.Success, Is.True, Describe(foreign.Diagnostics));
                File.WriteAllText(
                    Path.Combine(directory, "deck.published-behavior-cards.json"),
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(foreign.Catalog));

                bool success = CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                    deckPath,
                    CharacterId,
                    out CharacterTestRuntimeAiPlannerLocalPlayerDeckService restored,
                    out string error);

                Assert.That(success, Is.True, error);
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.Catalog.CatalogId, Is.EqualTo(
                    "character-test.runtime-ai-planner.project-behavior-cards"));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void RepeatedPublish_RequiresLatestSourceAndAdvancesContiguously()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                string deckPath = Path.Combine(directory, "deck.json");
                CharacterTestRuntimeAiPlannerLocalPlayerDeckService service = CreateService(deckPath);
                int unlockedCount = service.Snapshot.UnlockedCards.StableCardIds.Count;
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard source = ResolveFirstLatest(service);
                int sourceCatalogRevision = service.Catalog.Revision;
                CreateIsolatedDraft(source, out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                    out Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft draft);
                BumpSemantic(draft);
                Assert.That(Publish(service, group, cards, draft).Success, Is.True);

                BumpSemantic(draft);
                CharacterTestRuntimeAiLabPublishResult stale = Publish(service, group, cards, draft);
                Assert.That(stale.Success, Is.False);
                AssertDiagnostic(stale.Diagnostics, "LAB_PLAYER_SOURCE_REVISION_OUTDATED");

                Assert.That(service.TryResolveLatestPublishedCard(source.StableCardId, out var revisionTwo), Is.True);
                CreateIsolatedDraft(revisionTwo, out group, out cards, out draft);
                BumpSemantic(draft);
                CharacterTestRuntimeAiLabPublishResult revisionThree = Publish(service, group, cards, draft);

                Assert.That(revisionThree.Success, Is.True, Describe(revisionThree.Diagnostics));
                Assert.That(revisionThree.PublishedCard.Revision, Is.EqualTo(source.Revision + 2));
                Assert.That(revisionThree.Catalog.Revision, Is.EqualTo(sourceCatalogRevision + 2));
                Assert.That(service.Snapshot.UnlockedCards.StableCardIds.Count, Is.EqualTo(unlockedCount));
                CollectionAssert.Contains(
                    service.Snapshot.UnlockedCards.StableCardIds,
                    source.StableCardId);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void UnknownAbilityReference_IsRejectedBeforeCatalogWrite()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                string deckPath = Path.Combine(directory, "deck.json");
                CharacterTestRuntimeAiPlannerLocalPlayerDeckService service = CreateService(deckPath);
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard source = ResolveFirstLatest(service);
                CreateIsolatedDraft(source, out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                    out Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft draft);
                draft.LogicPayload.Stages[0].Kind = CharacterTestRuntimeAiLabCardStepKind.UseAbility;
                draft.LogicPayload.Stages[0].AbilityId = int.MaxValue;

                CharacterTestRuntimeAiLabPublishResult result = Publish(service, group, cards, draft);

                Assert.That(result.Success, Is.False);
                Assert.That(File.Exists(Path.Combine(directory, "deck.published-behavior-cards.json")), Is.False);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void Publish_RevalidatesAndPreservesEveryCharacterDeck()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                string deckPath = Path.Combine(directory, "deck.json");
                var targets = new[]
                {
                    new CharacterTestRuntimeAiPlannerPlayerDeckTarget(CharacterId, 0),
                    new CharacterTestRuntimeAiPlannerPlayerDeckTarget(909090, 1)
                };
                Assert.That(
                    CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                        deckPath,
                        targets,
                        out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
                        out string error),
                    Is.True,
                    error);
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard source = ResolveFirstLatest(service);
                Assert.That(service.Workbench.TryInstall(source.StableCardId).Success, Is.True);
                Assert.That(service.TrySelectCharacter(1, out error), Is.True, error);
                Assert.That(service.Workbench.TryInstall(source.StableCardId).Success, Is.True);
                Assert.That(service.TrySelectCharacter(0, out error), Is.True, error);
                CreateIsolatedDraft(source, out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                    out Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft draft);
                BumpSemantic(draft);

                CharacterTestRuntimeAiLabPublishResult result = Publish(service, group, cards, draft);

                Assert.That(result.Success, Is.True, Describe(result.Diagnostics));
                Assert.That(service.Snapshot.Decks.Count, Is.EqualTo(2));
                for (int i = 0; i < service.Snapshot.Decks.Count; i++)
                {
                    Assert.That(service.Snapshot.Decks[i].StableCardIds.Count, Is.EqualTo(1));
                    Assert.That(service.Snapshot.Decks[i].StableCardIds[0], Is.EqualTo(source.StableCardId));
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void WorkbenchSessionLock_RejectsPublishBeforeAuthorityLockArrayExists()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                CharacterTestRuntimeAiPlannerLocalPlayerDeckService service =
                    CreateService(Path.Combine(directory, "deck.json"));
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard source = ResolveFirstLatest(service);
                CreateIsolatedDraft(source, out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                    out Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft draft);
                BumpSemantic(draft);
                service.Workbench.LockSession();

                CharacterTestRuntimeAiLabPublishResult result = Publish(service, group, cards, draft);

                Assert.That(result.Success, Is.False);
                AssertDiagnostic(result.Diagnostics, "LAB_PUBLISH_SESSION_LOCKED");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void RuntimeEntryIdentityDivergence_IsRejected()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                CharacterTestRuntimeAiPlannerLocalPlayerDeckService service =
                    CreateService(Path.Combine(directory, "deck.json"));
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard source = ResolveFirstLatest(service);
                CreateIsolatedDraft(source, out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                    out Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft draft);
                BumpSemantic(draft);
                group.Entries[0].RuntimeEntry.EntryId = "entry.runtime-diverged";

                CharacterTestRuntimeAiLabPublishResult result = Publish(service, group, cards, draft);

                Assert.That(result.Success, Is.False);
                AssertDiagnostic(result.Diagnostics, "LAB_PUBLISH_PROFILE_PROJECTION_MISMATCH");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static CharacterTestRuntimeAiPlannerLocalPlayerDeckService CreateService(string deckPath)
        {
            Assert.That(
                CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                    deckPath,
                    CharacterId,
                    out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
                    out string error),
                Is.True,
                error);
            return service;
        }

        private static void BumpSemantic(CharacterTestRuntimeAiLabBehaviorCardDraft draft)
        {
            BumpSemantic(draft.LogicPayload);
        }

        private static void BumpSemantic(CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic)
        {
            Assert.That(logic?.Timelines.Count, Is.GreaterThan(0));
            Assert.That(logic.Timelines[0].Events.Count, Is.GreaterThan(0));
            logic.Timelines[0].Events[0].Priority++;
        }
        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCard ResolveFirstLatest(
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService service)
        {
            string stableCardId = service.Catalog.Cards[0].StableCardId;
            Assert.That(service.TryResolveLatestPublishedCard(stableCardId, out var latest), Is.True);
            return latest;
        }

        private static void AssertLivingTargetRevision(
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
            string stableCardId)
        {
            Assert.That(service.Catalog.TryGetExact(stableCardId, 1, out _), Is.True);
            Assert.That(service.TryResolveLatestPublishedCard(stableCardId, out var latest), Is.True);
            Assert.That(latest.Revision, Is.GreaterThan(1));
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic = latest.CloneLogic();
            bool found = false;
            for (int i = 0; i < logic.ActivationConditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCardCondition condition = logic.ActivationConditions[i];
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDead
                    && condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Equals
                    && !condition.BoolValue)
                {
                    found = true;
                    break;
                }
            }
            Assert.That(found, Is.True);
        }

        private static CharacterTestRuntimeAiLabPublishResult Publish(
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            CharacterTestRuntimeAiLabBehaviorCardDraft draft)
        {
            return ((ICharacterTestRuntimeAiLabPublishSource)service).TryPublish(
                CharacterId,
                group,
                cards,
                draft.CardDraftId);
        }

        private static void CreateIsolatedDraft(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard source,
            out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            out Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            out CharacterTestRuntimeAiLabBehaviorCardDraft draft)
        {
            group = new CharacterTestRuntimeAiLabBehaviorGroupDraft(
                null,
                string.Empty,
                "Publish test",
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile());
            group.Entries.Clear();
            group.Profile.Entries.Clear();
            cards = new Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft>(StringComparer.Ordinal);
            draft = CharacterTestRuntimeAiLabBehaviorCardDraft.CreatePublishedCopy(source);
            cards.Add(draft.CardDraftId, draft);
            var runtimeEntry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                enabled: true,
                order: 10);
            CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ConfigureCanonicalArbitration(
                runtimeEntry,
                source.Metadata);
            var entry = new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                null,
                draft.CardDraftId,
                runtimeEntry.BehaviorId,
                runtimeEntry.Order,
                disabled: false,
                runtimeEntry);
            group.Entries.Add(entry);
            group.Profile.Entries.Add(entry.RuntimeEntry);
        }

        private static void AssertDiagnostic(
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            string code)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (string.Equals(diagnostics[i].Code, code, StringComparison.Ordinal))
                    return;
            }
            Assert.Fail("Expected diagnostic " + code + ". Actual: " + Describe(diagnostics));
        }

        private static string Describe(IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            var values = new string[diagnostics.Count];
            for (int i = 0; i < diagnostics.Count; i++)
                values[i] = diagnostics[i].Code + ": " + diagnostics[i].Message;
            return string.Join(" | ", values);
        }

        private static CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog CreateReferences()
        {
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            return new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                CharacterTestAbilityCatalogSource.CreateRegistry(),
                actionRuntime.Registry);
        }

        private static string CreateTemporaryDirectory()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "mx-runtime-ai-lab-publish-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
    }
}
