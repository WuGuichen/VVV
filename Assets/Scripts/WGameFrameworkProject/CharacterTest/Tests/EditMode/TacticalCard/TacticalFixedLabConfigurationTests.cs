using System;
using System.Linq;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalFixedLabConfigurationTests
    {
        private static CharacterTestRuntimeAiLabCardTrialConfiguration Draft(CharacterTestRuntimeAiLabCardTrialContent content)
        {
            var draft = content.CreateDefault(); draft.InitialHand.Clear();
            var card = content.DefaultActionCard(draft.PlayerCharacterId);
            card.FixedAssembly = true; card.FixedQuality = CharacterTacticalCardQuality.Rainbow;
            card.AccessorySlots[0] = TacticalAccessoryCombatKind.BorrowMomentum;
            card.AccessorySlots[1] = TacticalAccessoryCombatKind.PushAway;
            card.AccessorySlots[2] = TacticalAccessoryCombatKind.BreakMomentum;
            draft.InitialHand.Add(card); return draft;
        }
        [Test] public void LabFreezeProducesExactThreeAccessoryCombinationAndIndependentReserveCopies()
        {
            var content = CharacterTestRuntimeAiLabCardTrialContent.Load(); var draft = Draft(content);
            var deployment = content.Freeze(draft, 707); var cards = deployment.PlayerParticipant.Cards;
            Assert.AreEqual(2, cards.Count); var first = cards[0].FixedAssembly; var second = cards[1].FixedAssembly;
            Assert.NotNull(first); Assert.NotNull(second); Assert.AreEqual(3, first.SlotCount);
            Assert.AreEqual(3, first.Accessories.Count); Assert.AreEqual(3, first.AccessoryConfirmationCost);
            foreach (var item in first.Accessories) Assert.AreEqual(1, item.ConfirmationCost);
            Assert.AreEqual(2, first.Accessories.Single(a => a.Kind == TacticalAccessoryCombatKind.BorrowMomentum).MomentumAmount);
            Assert.AreEqual(2, first.Accessories.Single(a => a.Kind == TacticalAccessoryCombatKind.BreakMomentum).MomentumAmount);
            Assert.AreNotEqual(first.HeldCardInstance, second.HeldCardInstance);
            Assert.IsFalse(first.Accessories.Select(a => a.ItemInstance).Intersect(second.Accessories.Select(a => a.ItemInstance)).Any());
            draft.InitialHand[0].AccessorySlots[0] = 0;
            Assert.AreEqual(3, first.Accessories.Count);
        }
        [Test] public void CloneAndConditionComparisonIncludeSlotValuesWithoutAliasing()
        {
            var content = CharacterTestRuntimeAiLabCardTrialContent.Load(); var draft = Draft(content); var frozen = draft.Clone();
            Assert.IsTrue(draft.HasSameConditions(frozen)); draft.InitialHand[0].AccessorySlots[0] = 0;
            Assert.IsFalse(draft.HasSameConditions(frozen)); Assert.AreEqual(TacticalAccessoryCombatKind.BorrowMomentum, frozen.InitialHand[0].AccessorySlots[0]);
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] public void EveryTestQualityAllowsAnEmptyFixedCombination(int quality)
        {
            var content = CharacterTestRuntimeAiLabCardTrialContent.Load(); var draft = Draft(content);
            var card = draft.InitialHand[0]; card.FixedQuality = (CharacterTacticalCardQuality)quality;
            Array.Clear(card.AccessorySlots, 0, 3);
            var assembly = content.Freeze(draft, 708).PlayerParticipant.Cards[0].FixedAssembly;
            Assert.AreEqual(quality, assembly.SlotCount); Assert.AreEqual(0, assembly.Accessories.Count);
        }
        [Test] public void FixedModeRejectsLegacyIndependentPreparationWithoutChangingDraft()
        {
            var content = CharacterTestRuntimeAiLabCardTrialContent.Load(); var draft = Draft(content);
            draft.InitialHand.Add(new CharacterTestRuntimeAiLabCardTrialSelection(CharacterTestRuntimeAiLabCardTrialSelection.TestPreparationId));
            var before = draft.Clone(); Assert.Throws<InvalidOperationException>(() => content.Freeze(draft, 709));
            Assert.IsTrue(draft.HasSameConditions(before));
        }
    }
}
