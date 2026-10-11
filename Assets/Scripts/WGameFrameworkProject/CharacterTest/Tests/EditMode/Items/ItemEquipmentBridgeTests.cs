using System;
using MxFramework.CharacterApplication;
using MxFramework.Items;
using MxFramework.Items.CharacterApplicationBridge;
using MxFramework.Resources;
using NUnit.Framework;

namespace MxFramework.Tests.Items
{
    public sealed class ItemEquipmentBridgeTests
    {
        [Test]
        public void BuiltInEquipmentIds_KeepStableWireValues()
        {
            Assert.AreEqual(1, CharacterEquipmentSlotIds.MainHand);
            Assert.AreEqual(2, CharacterEquipmentSlotIds.OffHand);
            Assert.AreEqual("mainHand", CharacterEquipmentSlotIds.MainHandLabel);
            Assert.AreEqual("offHand", CharacterEquipmentSlotIds.OffHandLabel);
            Assert.AreEqual("blade", CharacterWeaponTagIds.Blade);
            Assert.AreEqual("one_hand", CharacterWeaponTagIds.OneHand);
            Assert.AreEqual("1:blade", EquipmentMatchRuleToken.Create(CharacterEquipmentSlotIds.MainHand, CharacterWeaponTagIds.Blade));
        }

        [Test]
        public void Project_WhenEquipmentContainerHasItem_CreatesLegacyWeaponConfigAndLoadout()
        {
            ItemDefinitionRegistry itemRegistry = CreateItemRegistry();
            CharacterItemEquipmentProfileRegistry profileRegistry = CreateProfileRegistry();
            EquipmentSchemaConfig schema = CreateEquipmentSchema();
            var store = new InventoryStore();
            var backpack = new InventoryContainer(new InventoryContainerId(101), new InventoryOwnerId(1), 2, "bag");
            var equipment = new InventoryContainer(new InventoryContainerId(102), new InventoryOwnerId(1), 1, "equipment");
            store.Register(backpack);
            store.Register(equipment);
            var service = new InventoryService(itemRegistry, store);
            ItemStack swordStack = new ItemStack(new ItemDefinitionId(2001), 1);

            Assert.IsTrue(service.AddStack(backpack.ContainerId, 0, swordStack).Success);
            Assert.IsTrue(service.MoveStack(backpack.ContainerId, 0, equipment.ContainerId, 0, 1).Success);

            CharacterItemEquipmentProjectionResult projection = CharacterItemEquipmentProjector.Project(
                equipment,
                schema,
                new EquipmentLoadoutId(760101),
                "test.loadout.item_backed.sword",
                itemRegistry,
                profileRegistry);

            Assert.IsTrue(projection.IsSuccess, DescribeDiagnostics(projection.Diagnostics));
            Assert.AreEqual(1, projection.Weapons.Length);
            Assert.AreEqual(new WeaponConfigId(780101), projection.Weapons[0].WeaponId);
            Assert.AreEqual(1, projection.Loadout.Slots.Length);
            Assert.AreEqual(CharacterEquipmentSlotIds.MainHand, projection.Loadout.Slots[0].SlotId);

            var singleSwordState = new EquipmentStateConfig(
                new EquipmentStateId(770101),
                "test.equipment_state.single_sword",
                schema.EquipmentSchemaId,
                10,
                new[]
                {
                    new EquipmentMatchRule(
                        new[] { CharacterEquipmentSlotIds.MainHand },
                        Array.Empty<int>(),
                        new[] { EquipmentMatchRuleToken.Create(CharacterEquipmentSlotIds.MainHand, WeaponCategory.OneHandMelee.ToString()) },
                        new[] { EquipmentMatchRuleToken.Create(CharacterEquipmentSlotIds.MainHand, CharacterWeaponTagIds.Blade) },
                        Array.Empty<string>())
                },
                new AbilityLoadoutId(0),
                new CombatActionSetId(800101),
                "anim.single_sword",
                Array.Empty<string>());

            EquipmentStateResolveResult resolved = EquipmentStateResolver.Resolve(
                schema,
                projection.Loadout,
                projection.Weapons,
                new[] { singleSwordState });

            Assert.IsTrue(resolved.IsSuccess, DescribeDiagnostics(resolved.Diagnostics));
            Assert.AreEqual(singleSwordState.StateId, resolved.ActiveStateId);
        }

        [Test]
        public void Project_WhenProfileMissing_ReturnsDiagnostic()
        {
            ItemDefinitionRegistry itemRegistry = CreateItemRegistry();
            CharacterItemEquipmentProfileRegistry profileRegistry = new CharacterItemEquipmentProfileRegistry();
            EquipmentSchemaConfig schema = CreateEquipmentSchema();
            var store = new InventoryStore();
            var equipment = new InventoryContainer(new InventoryContainerId(102), new InventoryOwnerId(1), 1, "equipment");
            store.Register(equipment);
            var service = new InventoryService(itemRegistry, store);

            Assert.IsTrue(service.AddStack(equipment.ContainerId, 0, new ItemStack(new ItemDefinitionId(2001), 1)).Success);

            CharacterItemEquipmentProjectionResult projection = CharacterItemEquipmentProjector.Project(
                equipment,
                schema,
                new EquipmentLoadoutId(760101),
                "test.loadout.item_backed.sword",
                itemRegistry,
                profileRegistry);

            Assert.IsFalse(projection.IsSuccess);
            Assert.AreEqual(CharacterItemEquipmentProjectionStatus.ProjectionFailed, projection.Status);
            Assert.AreEqual(1, projection.Diagnostics.Length);
            Assert.AreEqual(CharacterDiagnosticCode.MissingWeaponConfig, projection.Diagnostics[0].Code);
        }

        [Test]
        public void Project_WhenEquippedStackCountIsNotOne_ReturnsDiagnostic()
        {
            var itemRegistry = new ItemDefinitionRegistry();
            Assert.IsTrue(itemRegistry.Register(ItemDefinitionTests.CreateDefinition(2001, "test.item.stackable_sword", ItemKind.Equipment, 2)).Success);
            CharacterItemEquipmentProfileRegistry profileRegistry = CreateProfileRegistry();
            EquipmentSchemaConfig schema = CreateEquipmentSchema();
            var store = new InventoryStore();
            var equipment = new InventoryContainer(new InventoryContainerId(102), new InventoryOwnerId(1), 1, "equipment");
            store.Register(equipment);
            var service = new InventoryService(itemRegistry, store);

            Assert.IsTrue(service.AddStack(equipment.ContainerId, 0, new ItemStack(new ItemDefinitionId(2001), 2)).Success);

            CharacterItemEquipmentProjectionResult projection = CharacterItemEquipmentProjector.Project(
                equipment,
                schema,
                new EquipmentLoadoutId(760101),
                "test.loadout.item_backed.sword",
                itemRegistry,
                profileRegistry);

            Assert.IsFalse(projection.IsSuccess);
            Assert.AreEqual(CharacterDiagnosticCode.InvalidEquipmentLoadout, projection.Diagnostics[0].Code);
        }

        [Test]
        public void Project_WhenSlotRejectsProfileCategory_ReturnsDiagnostic()
        {
            ItemDefinitionRegistry itemRegistry = CreateItemRegistry();
            CharacterItemEquipmentProfileRegistry profileRegistry = CreateProfileRegistry();
            var schema = new EquipmentSchemaConfig(
                new EquipmentSchemaId(750102),
                "test.equipment_schema.offhand_only",
                new[] { new EquipmentSlotEntry(CharacterEquipmentSlotIds.OffHand, EquipmentSlotKind.OffHand, "Off Hand", new[] { WeaponCategory.Shield.ToString() }) },
                Array.Empty<EquipmentExclusiveGroupEntry>(),
                Array.Empty<EquipmentStateId>());
            var store = new InventoryStore();
            var equipment = new InventoryContainer(new InventoryContainerId(102), new InventoryOwnerId(1), 1, "equipment");
            store.Register(equipment);
            var service = new InventoryService(itemRegistry, store);

            Assert.IsTrue(service.AddStack(equipment.ContainerId, 0, new ItemStack(new ItemDefinitionId(2001), 1)).Success);

            CharacterItemEquipmentProjectionResult projection = CharacterItemEquipmentProjector.Project(
                equipment,
                schema,
                new EquipmentLoadoutId(760101),
                "test.loadout.item_backed.sword",
                itemRegistry,
                profileRegistry);

            Assert.IsFalse(projection.IsSuccess);
            Assert.AreEqual(CharacterDiagnosticCode.CategoryNotAllowed, projection.Diagnostics[0].Code);
        }

        private static ItemDefinitionRegistry CreateItemRegistry()
        {
            var registry = new ItemDefinitionRegistry();
            Assert.IsTrue(registry.Register(ItemDefinitionTests.CreateDefinition(2001, "test.item.sword", ItemKind.Equipment, 1)).Success);
            return registry;
        }

        private static CharacterItemEquipmentProfileRegistry CreateProfileRegistry()
        {
            var registry = new CharacterItemEquipmentProfileRegistry();
            Assert.IsTrue(registry.Register(new CharacterItemEquipmentProfileConfig(
                new ItemDefinitionId(2001),
                new WeaponConfigId(780101),
                "test.weapon.item_backed.sword",
                WeaponCategory.OneHandMelee,
                new[] { CharacterWeaponTagIds.Blade, CharacterWeaponTagIds.OneHand },
                new[] { CharacterEquipmentSlotIds.MainHand },
                new AbilityLoadoutId(0),
                "presentation.weapon.sword",
                "combat.weapon.sword",
                "resource.weapon.sword",
                new[] { new CharacterResourceKeyEntry("test.weapon.sword.prefab", ResourceTypeIds.GameObject, CharacterResourceUsageKind.WeaponModel) },
                new[] { new CharacterTraceBindingEntry("trace.test.sword", CharacterEquipmentSlotIds.MainHand, 1, 1.2f, 0.1f) },
                Array.Empty<CharacterModifierGrantEntry>())));
            return registry;
        }

        private static EquipmentSchemaConfig CreateEquipmentSchema()
        {
            return new EquipmentSchemaConfig(
                new EquipmentSchemaId(750101),
                "test.equipment_schema.hands",
                new[] { new EquipmentSlotEntry(CharacterEquipmentSlotIds.MainHand, EquipmentSlotKind.MainHand, "Main Hand", new[] { WeaponCategory.OneHandMelee.ToString() }, new[] { CharacterWeaponTagIds.Blade }) },
                Array.Empty<EquipmentExclusiveGroupEntry>(),
                new[] { new EquipmentStateId(770101) });
        }

        private static string DescribeDiagnostics(CharacterDiagnostic[] diagnostics)
        {
            if (diagnostics == null || diagnostics.Length == 0)
                return string.Empty;

            string text = string.Empty;
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (i > 0)
                    text += "; ";
                text += diagnostics[i].Code + ":" + diagnostics[i].Message;
            }

            return text;
        }
    }
}
