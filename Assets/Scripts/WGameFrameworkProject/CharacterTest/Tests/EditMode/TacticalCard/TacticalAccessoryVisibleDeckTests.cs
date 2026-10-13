using System.Collections.Generic;
using System.Linq;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    internal sealed class TacticalAccessoryVisibleDeckTests
    {
        private static CharacterTestRuntimeAiLabCardTrialSelection Card(CharacterTacticalCardQuality quality = CharacterTacticalCardQuality.Silver)
            => new CharacterTestRuntimeAiLabCardTrialSelection("fixture.main", "action:17") { FixedQuality = quality };

        [Test]
        public void VisibleOffersUseCurrentActualDefinitionsAndTheirConfiguredFees()
        {
            var offers = CharacterTestRuntimeAiLabFixedAssembly.AvailableDefinitions();
            var authored = TacticalAccessoryThirdBatchPublication.CreateWithPreviousBatches(null).Configuration.Accessories;
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }.Concat(authored.Select(value => (int)value.Execution.Kind)), offers.Select(value => (int)value.Execution.Kind));
            foreach (var definition in offers) Assert.AreEqual(definition.Execution.ConfirmationMomentumCost, CharacterTestTacticalAccessoryDisplay.From(definition).ConfirmationCost);
            Assert.AreEqual(offers.Count, offers.Select(value => value.Reference.AccessoryId).Distinct().Count());
            Assert.AreEqual(authored.Length + 3, offers.Count);
        }

        [TestCase(TacticalAccessoryCombatKind.DuelInvitation)]
        [TestCase(TacticalAccessoryCombatKind.ShatteredFantasy)]
        [TestCase(TacticalAccessoryCombatKind.BlackHolePocket)]
        public void OfferSelectionFeedsExistingAttachWithTheSameDefinitionAndFee(TacticalAccessoryCombatKind kind)
        {
            var offer = CharacterTestRuntimeAiLabFixedAssembly.AvailableDefinitions().Single(value => value.Execution.Kind == kind);
            var card = Card();
            Assert.IsTrue(CharacterTestRuntimeAiLabFixedAssembly.TryEquip(card, offer.Execution.Kind, -1, out var issue));
            Assert.IsNull(issue); Assert.AreEqual(kind, card.AccessorySlots[0]); Assert.IsTrue(card.FixedAssembly);
            var main = TacticalFixedAssemblyManifestTests.Main();
            CharacterTestRuntimeAiLabFixedAssembly.Attach(card, main, 11, new Dictionary<string, int> { { "main.action", 17 } });
            Assert.AreEqual(offer.Execution.ConfirmationMomentumCost, main.FixedAssembly.AccessoryConfirmationCost);
            Assert.AreEqual(offer.Reference.AccessoryId, main.FixedAssembly.Accessories[0].AccessoryId);
            Assert.AreEqual(kind, main.FixedAssembly.Accessories[0].Kind);
        }

        [Test]
        public void FullCardRequiresExplicitReplacementAndDoesNotMutateItsCopy()
        {
            var card = Card();
            Assert.IsTrue(CharacterTestRuntimeAiLabFixedAssembly.TryEquip(card, TacticalAccessoryCombatKind.DuelInvitation, -1, out _));
            var copy = card.Clone();
            Assert.IsFalse(CharacterTestRuntimeAiLabFixedAssembly.TryEquip(card, TacticalAccessoryCombatKind.BlackHolePocket, -1, out var issue));
            Assert.IsNotEmpty(issue); Assert.AreEqual(TacticalAccessoryCombatKind.DuelInvitation, card.AccessorySlots[0]);
            Assert.IsTrue(CharacterTestRuntimeAiLabFixedAssembly.TryEquip(card, TacticalAccessoryCombatKind.BlackHolePocket, 0, out issue));
            Assert.IsNull(issue); Assert.AreEqual(TacticalAccessoryCombatKind.BlackHolePocket, card.AccessorySlots[0]);
            Assert.AreEqual(TacticalAccessoryCombatKind.DuelInvitation, copy.AccessorySlots[0]);
            Assert.AreEqual(2, CharacterTestRuntimeAiLabFixedAssembly.ConfirmationCost(card));
            Assert.AreEqual(1, CharacterTestRuntimeAiLabFixedAssembly.ConfirmationCost(copy));
        }

        [Test]
        public void ClickFindsNextEmptySlotAndDropKeepsItsExplicitSlot()
        {
            var card = Card(CharacterTacticalCardQuality.Rainbow);
            Assert.IsTrue(CharacterTestRuntimeAiLabFixedAssembly.TryEquip(card, TacticalAccessoryCombatKind.BlackHolePocket, 2, out _));
            Assert.IsTrue(CharacterTestRuntimeAiLabFixedAssembly.TryEquip(card, TacticalAccessoryCombatKind.DuelInvitation, -1, out _));
            Assert.IsTrue(CharacterTestRuntimeAiLabFixedAssembly.TryEquip(card, TacticalAccessoryCombatKind.ShatteredFantasy, -1, out _));
            CollectionAssert.AreEqual(new[] { TacticalAccessoryCombatKind.DuelInvitation, TacticalAccessoryCombatKind.ShatteredFantasy,
                TacticalAccessoryCombatKind.BlackHolePocket }, card.AccessorySlots);
            Assert.AreEqual(5, CharacterTestRuntimeAiLabFixedAssembly.ConfirmationCost(card), "Fees must not be inferred from three mounted parts.");
        }

        [Test]
        public void DuplicateAndUnavailableKindsLeaveTheExistingAssemblyIntact()
        {
            var card = Card(CharacterTacticalCardQuality.Gold);
            Assert.IsTrue(CharacterTestRuntimeAiLabFixedAssembly.TryEquip(card, TacticalAccessoryCombatKind.DuelInvitation, 1, out _));
            var before = card.Clone();
            Assert.IsFalse(CharacterTestRuntimeAiLabFixedAssembly.TryEquip(card, TacticalAccessoryCombatKind.DuelInvitation, 0, out _));
            Assert.IsFalse(CharacterTestRuntimeAiLabFixedAssembly.TryEquip(card, (TacticalAccessoryCombatKind)700, 0, out _));
            CollectionAssert.AreEqual(before.AccessorySlots, card.AccessorySlots);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void DropOutsideSilverCapacityDoesNotWriteHiddenSlots(int slot)
        {
            var card = Card();
            Assert.IsFalse(CharacterTestRuntimeAiLabFixedAssembly.TryEquip(card, TacticalAccessoryCombatKind.BlackHolePocket, slot, out _));
            CollectionAssert.AreEqual(new TacticalAccessoryCombatKind[3], card.AccessorySlots);
            Assert.IsFalse(card.FixedAssembly);
        }

        [Test]
        public void UnknownDefinitionKeepsItsIdentityFeeAndGenericVisibleMarker()
        {
            var definition = new CharacterTacticalAccessoryDefinition {
                Reference = new CharacterTacticalAccessoryReference { AccessoryId = "fixture.future.exact-id" },
                Execution = new TacticalAccessoryExecutionDefinition { Kind = (TacticalAccessoryCombatKind)700, ConfirmationMomentumCost = 8 } };
            var display = CharacterTestTacticalAccessoryDisplay.From(definition);
            Assert.AreEqual("fixture.future.exact-id", display.Name); Assert.AreEqual("费用 +8", display.FeeText);
            Assert.AreEqual("配件", display.SlotText);
            Assert.IsFalse(CharacterTestRuntimeAiLabFixedAssembly.AvailableDefinitions().Any(value => value.Execution.Kind == display.Kind));
        }
    }
}
