using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiLabPlayerLegalityTests
    {

        private static CharacterTestRuntimeAiLabPlayerLegalityState Evaluate(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            CharacterTestRuntimeAiPlannerUnlockedCardCollection unlocked,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticsInput diagnosticsInput = null)
        {
            return CharacterTestRuntimeAiLabPlayerLegalityEvaluator.Evaluate(
                "character.definition.1",
                1,
                group,
                cards,
                catalog,
                unlocked,
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                diagnosticsInput);
        }

        private static void CreateGroup(
            out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            out Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards)
        {
            group = new CharacterTestRuntimeAiLabBehaviorGroupDraft(
                null,
                string.Empty,
                "Test deck",
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile());
            group.Entries.Clear();
            group.Profile.Entries.Clear();
            cards = new Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft>(StringComparer.Ordinal);
        }

        private static CharacterTestRuntimeAiLabBehaviorCardDraft AddPublished(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            IDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard published)
        {
            CharacterTestRuntimeAiLabBehaviorCardDraft draft =
                CharacterTestRuntimeAiLabBehaviorCardDraft.CreatePublishedCopy(published);
            cards.Add(draft.CardDraftId, draft);
            AddEntry(group, draft);
            CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ConfigureCanonicalArbitration(
                group.Entries[group.Entries.Count - 1].RuntimeEntry,
                published.Metadata);
            return draft;
        }

        private static void AddEntry(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            CharacterTestRuntimeAiLabBehaviorCardDraft card)
        {
            var runtimeEntry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                enabled: true,
                order: (group.Entries.Count + 1) * 10);
            var entry = new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                null,
                card.CardDraftId,
                runtimeEntry.BehaviorId,
                runtimeEntry.Order,
                disabled: false,
                runtimeEntry);
            group.Entries.Add(entry);
            group.Profile.Entries.Add(entry.RuntimeEntry);
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCard Publish(
            string stableCardId,
            int revision,
            int cost,
            CharacterTestRuntimeAiPlannerBehaviorCapability[] requiredCapabilities = null)
        {
            CharacterTestRuntimeAiLabBehaviorCardPayload payload =
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture();
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                null,
                stableCardId,
                stableCardId,
                "Test",
                payload);
            var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(
                stableCardId,
                "ui." + stableCardId,
                cost,
                "Test card",
                "Test use",
                "Test risk",
                requiredCapabilities: requiredCapabilities);
            var request = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(
                stableCardId,
                revision,
                1,
                1,
                metadata);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult result =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                    draft,
                    request,
                    CreateReferences());
            Assert.That(result.Success, Is.True, Describe(result.Diagnostics));
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
            Assert.That(result.Success, Is.True, Describe(result.Diagnostics));
            return result.Catalog;
        }

        private static CharacterTestRuntimeAiPlannerUnlockedCardCollection CreateUnlocked(
            params string[] ids)
        {
            CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult result =
                CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(ids);
            Assert.That(result.Success, Is.True);
            return result.Collection;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog CreateReferences()
        {
            CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
            return new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                CharacterTestAbilityCatalogSource.CreateRegistry(),
                actionRuntime.Registry);
        }

        private static void AssertReason(
            CharacterTestRuntimeAiLabPlayerLegalityState state,
            CharacterTestRuntimeAiLabPlayerLegalityReasonKind kind)
        {
            for (int i = 0; i < state.Reasons.Count; i++)
            {
                if (state.Reasons[i].Kind == kind)
                    return;
            }
            Assert.Fail("Expected reason " + kind + ". Actual: " + Describe(state));
        }

        private static void AssertReasonCode(
            CharacterTestRuntimeAiLabPlayerLegalityState state,
            string code)
        {
            for (int i = 0; i < state.Reasons.Count; i++)
            {
                if (string.Equals(state.Reasons[i].Code, code, StringComparison.Ordinal))
                    return;
            }
            Assert.Fail("Expected reason code " + code + ". Actual: " + Describe(state));
        }

        private static string Describe(CharacterTestRuntimeAiLabPlayerLegalityState state)
        {
            if (state == null)
                return "null";
            var values = new string[state.Reasons.Count];
            for (int i = 0; i < state.Reasons.Count; i++)
                values[i] = state.Reasons[i].Code + ": " + state.Reasons[i].Message;
            return state.Status + " | " + string.Join(" | ", values);
        }

        private static string Describe(IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            var values = new string[diagnostics.Count];
            for (int i = 0; i < diagnostics.Count; i++)
                values[i] = diagnostics[i].Code + ": " + diagnostics[i].Message;
            return string.Join(" | ", values);
        }
    }
}
