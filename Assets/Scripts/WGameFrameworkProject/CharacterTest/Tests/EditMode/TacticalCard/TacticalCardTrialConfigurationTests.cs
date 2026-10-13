using System;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.CharacterApplication;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalCardTrialConfigurationTests
    {
        [Test]
        public void ManualPlacementAndAdditionalParticipantAreFrozenFromTheEditedRoster()
        {
            var content = Content(); var draft = Configuration(2);
            draft.UseExplicitDecks = true;
            draft.SetPlacement(0, new UnityEngine.Vector3(-3, 1, 2), 120);
            draft.SetPlacement(1, new UnityEngine.Vector3(4, 1, -2), 300);
            draft.OpponentDeck.Add(new CharacterTestRuntimeAiLabCardTrialSelection("fixture.wait"));
            var additional = new CharacterTestRuntimeAiLabTrialParticipant { CharacterId = 2, Position = new UnityEngine.Vector3(1, 1, 3), Facing = 180 };
            additional.Cards.Add(new CharacterTestRuntimeAiLabCardTrialSelection("fixture.wait"));
            draft.AdditionalParticipants.Add(additional);
            var frozen = draft.Clone(); var deployment = content.Freeze(frozen, 94);
            draft.SetPlacement(0, UnityEngine.Vector3.zero, 0); additional.Position = UnityEngine.Vector3.zero;
            additional.Cards.Clear();
            Assert.AreEqual(3, deployment.Participants.Count);
            Assert.AreEqual(new UnityEngine.Vector3(-3, 1, 2), deployment.Participants[0].Position);
            Assert.AreEqual(120, deployment.Participants[0].YawDegrees);
            Assert.AreEqual(new UnityEngine.Vector3(1, 1, 3), deployment.Participants[2].Position);
            Assert.AreEqual(1, deployment.Participants[2].Cards.Count);
            Assert.IsFalse(draft.HasSameConditions(frozen));
        }

        [Test]
        public void ExplicitEmptyOpponentDeckIsNotSilentlyReplacedByADefaultCard()
        {
            var content = Content(); var draft = Configuration(2); draft.UseExplicitDecks = true;
            var deployment = content.Freeze(draft, 95);
            Assert.AreEqual(0, deployment.Participants[1].Cards.Count);
        }

        [Test]
        public void CloneFreezesHandResourcesOpponentAndSeedAwayFromEditableDraft()
        {
            var draft = Configuration(4); var frozen = draft.Clone();
            draft.InitialMomentum = 0; draft.Seed = 999; draft.OpponentCharacterId = 1;
            draft.InitialHand[1].CardId = "changed"; draft.InitialHand.RemoveAt(0);
            Assert.AreEqual(50, frozen.InitialMomentum); Assert.AreEqual(73, frozen.Seed);
            Assert.AreEqual(2, frozen.OpponentCharacterId); Assert.AreEqual(4, frozen.InitialHand.Count);
            Assert.AreEqual("fixture.wait", frozen.InitialHand[1].CardId);
            Assert.AreEqual(CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId, frozen.InitialHand[0].CardId);
        }

        [Test]
        public void PendingConfigurationTracksResourcesBindingAndOrderWithoutChangingFrozenHand()
        {
            var draft = Configuration(4); var frozen = draft.Clone();
            Assert.IsTrue(draft.HasSameConditions(frozen));
            draft.PreparationCost = 6;
            Assert.IsFalse(draft.HasSameConditions(frozen)); Assert.AreEqual(5, frozen.PreparationCost);
            draft = frozen.Clone(); draft.InitialHand[1].TacticalActionId = "different-action";
            Assert.IsFalse(draft.HasSameConditions(frozen)); Assert.IsTrue(string.IsNullOrEmpty(frozen.InitialHand[1].TacticalActionId));
            draft = frozen.Clone(); var first = draft.InitialHand[0]; draft.InitialHand.RemoveAt(0); draft.InitialHand.Add(first);
            Assert.IsFalse(draft.HasSameConditions(frozen));
            Assert.AreEqual(CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId, frozen.InitialHand[0].CardId);
        }

        [TestCase(1)] [TestCase(4)] [TestCase(7)]
        public void FrozenDeploymentHasIndependentRosterExactHandAndActionOnlyReserve(int count)
        {
            var content = Content(); var draft = Configuration(count);
            var deployment = content.Freeze(draft, 91);
            Assert.AreEqual(count, deployment.CardTrial.HandCapacity); Assert.AreEqual(count, deployment.CardTrial.InitialHandCount);
            Assert.AreEqual(91ul, deployment.CardTrial.StageRunId); Assert.AreEqual(73, deployment.CardTrial.Seed);
            Assert.AreSame(content.Published, deployment.CardTrial.PublishedCatalog);
            Assert.AreEqual(50, deployment.InitialMotion); Assert.IsFalse(deployment.UsesLegacyFunctionalResolverFallback);
            Assert.AreEqual(2, deployment.Participants.Count); Assert.AreEqual(1, deployment.Participants.Count(item => item.IsPlayerControlled));
            Assert.AreNotEqual(deployment.Participants[0].EntityId, deployment.Participants[1].EntityId);
            Assert.AreNotEqual(deployment.Participants[0].TeamId, deployment.Participants[1].TeamId);
            var cards = deployment.PlayerParticipant.Cards;
            Assert.AreEqual(count + count - 1, cards.Count);
            Assert.IsInstanceOf<TestPreparationPayload>(((TacticalUtilityCardDefinition)cards[0]).Payload);
            Assert.AreEqual(CardDestination.Exhaust, cards[0].DestinationAfterUse);
            Assert.IsTrue(cards.Skip(count).All(item => item.Kind == TacticalCardKind.Action));
            var entries = cards.OfType<TacticalActionCardDefinition>().ToArray();
            Assert.AreEqual(entries.Length, entries.Select(item => item.DeckEntryInstanceId).Distinct().Count());
            Assert.IsTrue(entries.All(item => item.PublishedCardId == "fixture.wait" && item.PublishedRevision == 1 && item.PublishedContentHash == "123"));
            Assert.AreEqual(1, deployment.Participants[1].Cards.Count);
            Assert.AreEqual(0, content.Characters[0].TacticalDefaults.Cards.Count, "No trial card is granted to character defaults or a player store.");
        }

        [Test]
        public void ResetFromFrozenConditionsRetainsDeckSeedResourcesAndOpponentWithFreshOwner()
        {
            var content = Content(); var frozen = Configuration(7).Clone();
            var first = content.Freeze(frozen, 91); var reset = content.Freeze(frozen.Clone(), 92);
            Assert.AreNotEqual(first.CardTrial.StageRunId, reset.CardTrial.StageRunId);
            Assert.AreEqual(first.CardTrial.Seed, reset.CardTrial.Seed);
            Assert.AreEqual(first.InitialMotion, reset.InitialMotion);
            Assert.AreEqual(first.OpponentCharacterId, reset.OpponentCharacterId);
            CollectionAssert.AreEqual(first.Cards.Select(item => item.Id), reset.Cards.Select(item => item.Id));
            frozen.InitialHand.Clear(); frozen.InitialMomentum = 0; frozen.Seed = 999;
            Assert.AreEqual(13, first.Cards.Count); Assert.AreEqual(50, first.InitialMotion); Assert.AreEqual(73, first.CardTrial.Seed);
        }

        [TestCase(-1,5)] [TestCase(101,5)] [TestCase(50,-1)]
        public void InvalidInitialConditionsAreRejectedBeforeDeployment(int resources, int cost)
        {
            var draft = Configuration(4); draft.InitialMomentum = resources; draft.PreparationCost = cost;
            Assert.Throws<InvalidOperationException>(() => Content().Freeze(draft, 91));
        }

        [Test]
        public void EmptyHandOrInvalidParticipantCannotCreateTrialSession()
        {
            var content = Content(); var draft = Configuration(4); draft.InitialHand.Clear();
            Assert.Throws<InvalidOperationException>(() => content.Freeze(draft, 91));
            draft = Configuration(4); draft.PlayerCharacterId = 999;
            Assert.Throws<InvalidOperationException>(() => content.Freeze(draft, 91));
        }

        [Test]
        public void MissingOrOtherCharacterActionBindingCannotBeFrozen()
        {
            var content = Content(); content.Catalog.Cards[0].Slots = new[] { "attack" };
            var draft = Configuration(4);
            Assert.Throws<InvalidOperationException>(() => content.Freeze(draft, 91));
            content.Catalog.Actions = new[] { new CharacterTacticalActionDefinition { Id = "other.action", ActionId = 123, CharacterStableIds = new[] { "other" } } };
            draft.InitialHand[1].TacticalActionId = "other.action";
            Assert.Throws<InvalidOperationException>(() => content.Freeze(draft, 91));
        }

        [Test]
        public void AutomaticActionBindingPrefersCompatibleChoiceThenRoleDefaultThenStableFirstCandidate()
        {
            var content = Content(); content.Catalog.Cards[0].Slots = new[] { "attack" };
            content.Catalog.Actions = new[] {
                new CharacterTacticalActionDefinition { Id = "foreign", DisplayName = "A", ActionId = 900, CharacterStableIds = new[] { "other" } },
                new CharacterTacticalActionDefinition { Id = "b", DisplayName = "Same name", ActionId = 302, CharacterStableIds = new[] { "fixture.1" } },
                new CharacterTacticalActionDefinition { Id = "a", DisplayName = "Same name", ActionId = 301, CharacterStableIds = new[] { "fixture.1" } }
            };
            var defaults = content.Characters[0].TacticalDefaults;
            defaults.Bindings.Add(new CharacterTacticalSlotBinding { CardId = "fixture.wait", SlotPath = "attack", TacticalActionId = "b" });
            Assert.AreEqual("b", content.ResolveActionBinding("fixture.wait", 1), "The role's explicit card default precedes the catalog fallback.");
            Assert.AreEqual("a", content.ResolveActionBinding("fixture.wait", 1, "a"), "A compatible manual choice survives replacement.");
            Assert.AreEqual("b", content.ResolveActionBinding("fixture.wait", 1, "foreign"));
            defaults.Bindings.Clear();
            Assert.AreEqual("a", content.ResolveActionBinding("fixture.wait", 1), "Several candidates must still provide a deterministic default.");
            Array.Reverse(content.Catalog.Actions);
            Assert.AreEqual("a", content.ResolveActionBinding("fixture.wait", 1));
            Assert.AreEqual("", content.ResolveActionBinding(CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId, 1, "a"));
        }

        [Test]
        public void ChangingRoleAutomaticallyBindsEligibleActionsWithoutMutatingFrozenDeployment()
        {
            var content = Content(); content.Catalog.Cards[0].Slots = new[] { "attack" };
            content.Catalog.Actions = new[] {
                new CharacterTacticalActionDefinition { Id = "first", DisplayName = "A", ActionId = 301, CharacterStableIds = new[] { "fixture.1" } },
                new CharacterTacticalActionDefinition { Id = "first.alternative", DisplayName = "B", ActionId = 302, CharacterStableIds = new[] { "fixture.1" } },
                new CharacterTacticalActionDefinition { Id = "second", DisplayName = "A", ActionId = 601, CharacterStableIds = new[] { "fixture.2" } },
                new CharacterTacticalActionDefinition { Id = "second.alternative", DisplayName = "B", ActionId = 602, CharacterStableIds = new[] { "fixture.2" } }
            };
            var draft = Configuration(4);
            foreach (var item in draft.InitialHand) item.TacticalActionId = content.ResolveActionBinding(item.CardId, 1);
            var frozen = content.Freeze(draft, 91);
            draft.PlayerCharacterId = 2; draft.OpponentCharacterId = 1;
            foreach (var item in draft.InitialHand) item.TacticalActionId = content.ResolveActionBinding(item.CardId, 2, item.TacticalActionId);
            var next = content.Freeze(draft, 92);
            Assert.IsTrue(frozen.PlayerParticipant.Bindings.Values.Where(item => item.Count > 0).All(item => item["attack"] == 301));
            Assert.IsTrue(next.PlayerParticipant.Bindings.Values.Where(item => item.Count > 0).All(item => item["attack"] == 601));
            Assert.AreEqual("second", draft.InitialHand[1].TacticalActionId);
            content.Catalog.Actions = content.Catalog.Actions.Where(item => item.CharacterStableIds.Contains("fixture.1")).ToArray();
            Assert.AreEqual("", content.ResolveActionBinding("fixture.wait", 2), "No eligible action must remain an explicit invalid configuration.");
        }

        private static CharacterTestRuntimeAiLabCardTrialConfiguration Configuration(int count)
        {
            var config = new CharacterTestRuntimeAiLabCardTrialConfiguration { PlayerCharacterId = 1, OpponentCharacterId = 2 };
            config.InitialHand.Add(new(CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId));
            for (int i = 1; i < count; i++) config.InitialHand.Add(new("fixture.wait"));
            return config;
        }

        private static CharacterTestRuntimeAiLabCardTrialContent Content()
        {
            var source = CharacterTestTacticalPublishedSource.Read(new JObject {
                ["schema"] = CharacterTestTacticalPublishedSource.Schema,
                ["legacy"] = JObject.Parse(CharacterTestTacticalAuthoringJson.Write(CharacterTestTacticalAuthoringTemplates.CreateWaitTwoSeconds())) });
            var catalog = new CharacterTacticalContentCatalog { Cards = new[] { new CharacterTacticalCardDefinition {
                CardId = "fixture.wait", Revision = 1, ContentHash = "123", DisplayName = "Wait fixture", PresentationSource = source } } };
            var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null,
                new CharacterActionConfigRegistry(Array.Empty<CharacterActionSetConfig>(), Array.Empty<CharacterActionConfig>()));
            var published = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate("trial.fixtures", 1,
                Array.Empty<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>(), references);
            Assert.IsTrue(published.Success);
            var state = new CharacterTestRuntimeAiPlannerProjectCatalogState(published.Catalog,
                new CharacterTestRuntimeAiPlannerBehaviorGroupCatalog("trial.fixtures", 1, null), "", "", "", "", "");
            var roles = new[] { 1, 2 }.Select(id => new CharacterTestCharacterDefinition(id, "Fixture " + id, default, id, default,
                CharacterTestCharacterPhysicsComponent.Default, tacticalDefaults: new CharacterTacticalDefaults { CharacterStableId = "fixture." + id })).ToArray();
            return new CharacterTestRuntimeAiLabCardTrialContent(catalog, state, roles);
        }
    }
}
