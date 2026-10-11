using System.Collections.Generic;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestSkillPanelTests
    {
        [Test]
        public void SkillSlotRuntime_DefaultSnapshotContainsNineSlotsAndTwentyOneCandidates()
        {
            CharacterTestSkillSlotRuntime skillSlots = CharacterTestSkillSlotRuntime.CreateDefault();
            AbilityRuntimeModule abilityRuntime = CharacterTestAbilityCatalogSource.CreateRuntimeModule();

            CharacterTestSkillPanelSnapshot snapshot = skillSlots.CreateSnapshot(
                abilityRuntime,
                casterEntityId: 1,
                RuntimeFrame.Zero,
                selectedSlotIndex: 0);

            Assert.AreEqual(9, snapshot.Slots.Count);
            Assert.AreEqual(21, snapshot.Candidates.Count);
            Assert.AreEqual(CharacterTestAbilityIds.FireballInputIntentId, snapshot.Slots[0].InputIntentId);
            Assert.AreEqual(CharacterTestAbilityIds.TornadoInputIntentId, snapshot.Slots[6].InputIntentId);
            Assert.AreEqual(CharacterTestAbilityIds.FireballSwayInputIntentId, snapshot.Slots[7].InputIntentId);
            Assert.AreEqual(CharacterTestAbilityIds.FireballHomingInputIntentId, snapshot.Slots[8].InputIntentId);
            Assert.AreEqual(CharacterTestAbilityIds.FireballBezierInputIntentId, snapshot.Candidates[9].InputIntentId);
            Assert.AreEqual(CharacterTestAbilityIds.DashStrikeInputIntentId, snapshot.Candidates[17].InputIntentId);
            Assert.AreEqual(CharacterTestAbilityIds.HookChainInputIntentId, snapshot.Candidates[18].InputIntentId);
            Assert.AreEqual(CharacterTestAbilityIds.ArcMineInputIntentId, snapshot.Candidates[19].InputIntentId);
            Assert.AreEqual(CharacterTestAbilityIds.RuneTurretInputIntentId, snapshot.Candidates[20].InputIntentId);
        }

        [Test]
        public void SkillSlotRuntime_EquipCandidateUpdatesSelectedSlot()
        {
            CharacterTestSkillSlotRuntime skillSlots = CharacterTestSkillSlotRuntime.CreateDefault();

            Assert.IsTrue(skillSlots.TryEquip(
                8,
                CharacterTestAbilityIds.FireballLobInputIntentId,
                out string message));

            Assert.That(message, Does.Contain("9"));
            Assert.IsTrue(skillSlots.TryResolveSlot(8, out CharacterTestSkillSlotBinding binding));
            Assert.AreEqual(CharacterTestAbilityIds.FireballLobInputIntentId, binding.InputIntentId);
        }

        [Test]
        public void View_BindRendersSlotsCandidatesAndCooldown()
        {
            CharacterTestSkillSlotRuntime skillSlots = CharacterTestSkillSlotRuntime.CreateDefault();
            AbilityRuntimeModule abilityRuntime = CharacterTestAbilityCatalogSource.CreateRuntimeModule();
            Assert.IsTrue(abilityRuntime.Definitions.TryGet(
                CharacterTestAbilityIds.FireballAbilityId,
                out AbilityDefinitionConfig fireball));
            abilityRuntime.CommitActivation(1, fireball, RuntimeFrame.Zero);
            CharacterTestSkillPanelSnapshot snapshot = skillSlots.CreateSnapshot(
                abilityRuntime,
                casterEntityId: 1,
                RuntimeFrame.Zero,
                selectedSlotIndex: 0);
            var host = new VisualElement();
            var view = new CharacterTestSkillPanelView();

            view.Build(host);
            view.Bind(snapshot);

            List<Button> buttons = host.Query<Button>().ToList();
            Assert.AreEqual(30, buttons.Count);
            Assert.That(host.Q<Button>("character-test-skill-slot-1").text, Does.Contain("CD"));
            Assert.That(host.Q<Button>("character-test-skill-slot-7").text, Does.Contain("Tornado"));
            Assert.That(host.Q<Button>("character-test-skill-slot-8").text, Does.Contain("摆动火球"));
            Assert.That(host.Q<Button>("character-test-skill-slot-9").text, Does.Contain("追踪火球"));
            Assert.That(host.Q<Label>(CharacterTestSkillPanelView.MessageName).text, Does.Contain("选择槽位"));
        }
    }
}
