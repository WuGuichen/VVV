using System;
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.Items;
using MxFramework.Items.CharacterApplicationBridge;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Items
{
    public sealed class WorldItemRuntimeTests
    {
        [Test]
        public void WorldItemService_SpawnPickupAddsToInventoryAndRemovesWorldItem()
        {
            InventoryService inventory = InventoryRuntimeTests.CreateService(out InventoryStore inventoryStore, out ItemDefinition herb, out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(1), 2, "bag");
            inventoryStore.Register(bag);
            var worldStore = new WorldItemStore();
            var service = new WorldItemService(worldStore, inventory);

            WorldItemOperationResult spawn = service.Spawn(new WorldItemStack(
                new WorldItemId(1),
                new ItemStack(herb.DefinitionId, 15),
                new WorldItemPosition(2, 0, 3),
                new InventoryOwnerId(1),
                WorldItemAccessPolicy.Public,
                spawnFrame: 0));
            Assert.AreEqual(WorldItemOperationStatus.Success, spawn.Status);
            Assert.AreEqual(1, worldStore.Count);

            WorldItemOperationResult pickup = service.Pickup(
                new WorldItemId(1),
                new InventoryOwnerId(1),
                bag.ContainerId,
                RuntimeFrame.Zero);
            Assert.AreEqual(WorldItemOperationStatus.Success, pickup.Status);
            Assert.AreEqual(0, worldStore.Count);
            Assert.AreEqual(15, bag.GetStack(0).Count);
        }

        [Test]
        public void WorldItemService_PickupFailuresDoNotRemoveWorldItem()
        {
            InventoryService inventory = InventoryRuntimeTests.CreateService(out InventoryStore inventoryStore, out ItemDefinition herb, out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(1), 1, "bag");
            inventoryStore.Register(bag);
            Assert.IsTrue(inventory.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 20)).Success);
            var worldStore = new WorldItemStore();
            var service = new WorldItemService(worldStore, inventory);
            Assert.IsTrue(service.Spawn(new WorldItemStack(
                new WorldItemId(1),
                new ItemStack(herb.DefinitionId, 1),
                new WorldItemPosition(0, 0, 0),
                new InventoryOwnerId(1),
                WorldItemAccessPolicy.Public,
                0)).Success);
            Assert.IsTrue(service.Spawn(new WorldItemStack(
                new WorldItemId(2),
                new ItemStack(herb.DefinitionId, 1),
                new WorldItemPosition(1, 0, 0),
                new InventoryOwnerId(1),
                WorldItemAccessPolicy.OwnerOnly,
                0)).Success);
            Assert.IsTrue(service.Spawn(new WorldItemStack(
                new WorldItemId(3),
                new ItemStack(herb.DefinitionId, 1),
                new WorldItemPosition(2, 0, 0),
                new InventoryOwnerId(1),
                WorldItemAccessPolicy.Public,
                0,
                expireFrame: 1)).Success);

            WorldItemOperationResult full = service.Pickup(new WorldItemId(1), new InventoryOwnerId(1), bag.ContainerId, RuntimeFrame.Zero);
            Assert.AreEqual(WorldItemOperationStatus.InventoryRejected, full.Status);
            Assert.AreEqual(InventoryOperationStatus.InsufficientCapacity, full.InventoryStatus);
            Assert.IsTrue(worldStore.TryGet(new WorldItemId(1), out _));

            WorldItemOperationResult denied = service.Pickup(new WorldItemId(2), new InventoryOwnerId(2), bag.ContainerId, RuntimeFrame.Zero);
            Assert.AreEqual(WorldItemOperationStatus.AccessDenied, denied.Status);
            Assert.IsTrue(worldStore.TryGet(new WorldItemId(2), out _));

            WorldItemOperationResult expired = service.Pickup(new WorldItemId(3), new InventoryOwnerId(1), bag.ContainerId, new RuntimeFrame(1));
            Assert.AreEqual(WorldItemOperationStatus.Expired, expired.Status);
            Assert.IsTrue(worldStore.TryGet(new WorldItemId(3), out _));

            WorldItemOperationResult missing = service.Pickup(new WorldItemId(404), new InventoryOwnerId(1), bag.ContainerId, RuntimeFrame.Zero);
            Assert.AreEqual(WorldItemOperationStatus.MissingWorldItem, missing.Status);
        }

        [Test]
        public void WorldItemRuntimeModule_ExecutesCommandsAndExpiresInStableOrder()
        {
            InventoryService inventory = InventoryRuntimeTests.CreateService(out InventoryStore inventoryStore, out ItemDefinition herb, out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(1), 2, "bag");
            inventoryStore.Register(bag);
            var worldStore = new WorldItemStore();
            var module = new WorldItemRuntimeModule(new WorldItemService(worldStore, inventory));

            WorldItemRequestHandle spawnHandle = module.Requests.Add(new WorldItemRequest(
                WorldItemRequestKind.SpawnWorldItem,
                new WorldItemId(1),
                new ItemStack(herb.DefinitionId, 2),
                new WorldItemPosition(0, 0, 0),
                new InventoryOwnerId(1),
                WorldItemAccessPolicy.Public,
                spawnFrame: 0,
                expireFrame: 1));
            module.Commands.Enqueue(WorldItemRuntimeCommandFactory.Create(
                RuntimeFrame.Zero,
                7,
                WorldItemRuntimeCommandIds.SpawnWorldItem,
                spawnHandle,
                CharacterTestRuntimeTraceIds.TestSpawn));
            module.Tick(new RuntimeTickContext(0, 1d / 60d, 0d, RuntimeTickStage.Simulation));
            Assert.AreEqual(1, worldStore.Count);

            var events = new List<WorldItemRuntimeEvent>();
            module.DrainEvents(RuntimeFrame.Zero, events);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(WorldItemRuntimeEventType.OperationSucceeded, events[0].EventType);
            Assert.AreEqual(new WorldItemId(1), events[0].WorldItemId);

            module.Tick(new RuntimeTickContext(1, 1d / 60d, 1d / 60d, RuntimeTickStage.Simulation));
            Assert.AreEqual(0, worldStore.Count);
            events.Clear();
            module.DrainEvents(new RuntimeFrame(1), events);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(WorldItemRuntimeEventType.ItemExpired, events[0].EventType);
            Assert.AreEqual(new WorldItemId(1), events[0].WorldItemId);

            module.Commands.Enqueue(WorldItemRuntimeCommandFactory.Create(
                new RuntimeFrame(2),
                7,
                WorldItemRuntimeCommandIds.ExpireWorldItem,
                spawnHandle,
                CharacterTestRuntimeTraceIds.TestStale));
            module.Tick(new RuntimeTickContext(2, 1d / 60d, 2d / 60d, RuntimeTickStage.Simulation));
            events.Clear();
            module.DrainEvents(new RuntimeFrame(2), events);
            Assert.AreEqual(WorldItemOperationStatus.StaleRequest, events[0].Status);
        }

        [Test]
        public void WorldItemSaveState_RoundTripsHashAndRejectsDefinitionMismatch()
        {
            InventoryService inventory = InventoryRuntimeTests.CreateService(out _, out ItemDefinition herb, out _);
            var worldStore = new WorldItemStore();
            var service = new WorldItemService(worldStore, inventory);
            Assert.IsTrue(service.Spawn(new WorldItemStack(
                new WorldItemId(1),
                new ItemStack(herb.DefinitionId, 8),
                new WorldItemPosition(4, 0, 5),
                new InventoryOwnerId(1),
                WorldItemAccessPolicy.OwnerOnly,
                3,
                99)).Success);

            long hashBefore = RuntimeHashCombiner.ComputeHash(RuntimeFrame.Zero, new[] { new WorldItemHashContributor(worldStore) });
            var provider = new WorldItemSaveStateProvider(worldStore, inventory.Definitions, () => 12);
            RuntimeSaveState captured = provider.CaptureSaveState().Value;
            Assert.AreEqual(ItemRuntimeIds.WorldItemsModule, captured.ModuleStates[0].ModuleId);
            Assert.AreEqual(ItemRuntimeIds.WorldItemsModule, captured.ModuleStates[0].CustomState.TypeId);
            string json = RuntimeSaveStateJson.SaveToJson(captured);
            RuntimeSaveState loaded = RuntimeSaveStateJson.LoadFromJson(json).Value;

            var restoredStore = new WorldItemStore();
            var restorer = new WorldItemSaveStateProvider(restoredStore, inventory.Definitions);
            Assert.IsTrue(restorer.RestoreSaveState(loaded).Success);
            long hashAfter = RuntimeHashCombiner.ComputeHash(RuntimeFrame.Zero, new[] { new WorldItemHashContributor(restoredStore) });
            Assert.AreEqual(hashBefore, hashAfter);

            var changedDefinitions = new ItemDefinitionRegistry();
            changedDefinitions.Register(ItemDefinitionTests.CreateDefinition(1001, "item.herb.changed", ItemKind.Material, 20));
            RuntimeSaveStateResult<bool> mismatch = new WorldItemSaveStateProvider(new WorldItemStore(), changedDefinitions).RestoreSaveState(loaded);
            Assert.IsFalse(mismatch.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.CustomStateMismatch, mismatch.Error.Code);
        }

        [Test]
        public void LifecycleIntegration_LootWorldPickupInventoryAndEquipmentProjection()
        {
            ItemDefinitionRegistry definitions = CreateLifecycleDefinitions();
            var inventoryStore = new InventoryStore();
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(1), 4, "bag");
            var equipment = new InventoryContainer(new InventoryContainerId(2), new InventoryOwnerId(1), 1, "equipment");
            inventoryStore.Register(bag);
            inventoryStore.Register(equipment);
            var inventory = new InventoryService(definitions, inventoryStore);
            var worldStore = new WorldItemStore();
            var world = new WorldItemService(worldStore, inventory);

            LootResolveResult loot = new LootResolver(definitions).Resolve(
                new LootTableDefinition(
                    new LootTableId(1),
                    "loot.lifecycle.chest",
                    new[]
                    {
                        new LootGroupDefinition(
                            "weapon",
                            1,
                            1,
                            LootContext.BasisPointsScale,
                            0,
                            new[] { new LootEntryDefinition(new ItemDefinitionId(2001), 1, 1, 1) })
                    }),
                new LootContext("chest.lifecycle", 1, new ItemTagSet(null)),
                new DeterministicRandom(42u));
            Assert.IsTrue(loot.Success, loot.Diagnostics.Length == 0 ? string.Empty : loot.Diagnostics[0].Message);
            Assert.AreEqual(1, loot.Items.Length);

            Assert.IsTrue(world.Spawn(new WorldItemStack(
                new WorldItemId(1),
                new ItemStack(loot.Items[0].ItemDefinitionId, loot.Items[0].Count),
                new WorldItemPosition(1, 0, 0),
                new InventoryOwnerId(1),
                WorldItemAccessPolicy.Public,
                0)).Success);
            Assert.IsTrue(world.Pickup(new WorldItemId(1), new InventoryOwnerId(1), bag.ContainerId, RuntimeFrame.Zero).Success);
            Assert.AreEqual(0, worldStore.Count);
            Assert.AreEqual(new ItemDefinitionId(2001), bag.GetStack(0).DefinitionId);

            Assert.IsTrue(inventory.MoveStack(bag.ContainerId, 0, equipment.ContainerId, 0, 1).Success);
            CharacterItemEquipmentProjectionResult projection = CharacterItemEquipmentProjector.Project(
                equipment,
                CreateEquipmentSchema(),
                new EquipmentLoadoutId(760101),
                "test.loadout.lifecycle",
                definitions,
                CreateProfileRegistry());
            Assert.IsTrue(projection.IsSuccess, DescribeDiagnostics(projection.Diagnostics));

            EquipmentStateResolveResult resolved = EquipmentStateResolver.Resolve(
                CreateEquipmentSchema(),
                projection.Loadout,
                projection.Weapons,
                new[] { CreateSingleSwordState() });
            Assert.IsTrue(resolved.IsSuccess, DescribeDiagnostics(resolved.Diagnostics));
        }

        private static ItemDefinitionRegistry CreateLifecycleDefinitions()
        {
            var definitions = new ItemDefinitionRegistry();
            Assert.IsTrue(definitions.Register(ItemDefinitionTests.CreateDefinition(1001, "item.herb", ItemKind.Material, 20)).Success);
            Assert.IsTrue(definitions.Register(ItemDefinitionTests.CreateDefinition(2001, "item.sword", ItemKind.Equipment, 1)).Success);
            return definitions;
        }

        private static CharacterItemEquipmentProfileRegistry CreateProfileRegistry()
        {
            var registry = new CharacterItemEquipmentProfileRegistry();
            Assert.IsTrue(registry.Register(new CharacterItemEquipmentProfileConfig(
                new ItemDefinitionId(2001),
                new WeaponConfigId(780101),
                "test.weapon.lifecycle.sword",
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
                "test.equipment_schema.lifecycle",
                new[] { new EquipmentSlotEntry(CharacterEquipmentSlotIds.MainHand, EquipmentSlotKind.MainHand, "Main Hand", new[] { WeaponCategory.OneHandMelee.ToString() }, new[] { CharacterWeaponTagIds.Blade }) },
                Array.Empty<EquipmentExclusiveGroupEntry>(),
                new[] { new EquipmentStateId(770101) });
        }

        private static EquipmentStateConfig CreateSingleSwordState()
        {
            return new EquipmentStateConfig(
                new EquipmentStateId(770101),
                "test.equipment_state.lifecycle_sword",
                new EquipmentSchemaId(750101),
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
        }

        [Test]
        public void WorldInstances_RejectDuplicateSpawnAndTransferOneIdentityOnPickup()
        {
            var inventory = InventoryRuntimeTests.CreateService(out var store, out var herb, out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(1), 2);
            store.Register(bag);
            var world = new WorldItemStore();
            var service = new WorldItemService(world, inventory);
            var stack = new ItemStack(herb.DefinitionId, 1, new ItemInstanceId(77));
            Assert.IsTrue(service.Spawn(new WorldItemStack(new WorldItemId(1), stack,
                new WorldItemPosition(0, 0, 0), new InventoryOwnerId(1), WorldItemAccessPolicy.Public, 0)).Success);
            Assert.AreEqual(78, store.NextItemInstanceId);
            Assert.AreEqual(WorldItemOperationStatus.DuplicateInstance,
                service.Spawn(new WorldItemStack(new WorldItemId(2), stack,
                    new WorldItemPosition(0, 0, 0), new InventoryOwnerId(1), WorldItemAccessPolicy.Public, 0)).Status);
            Assert.IsTrue(service.Pickup(new WorldItemId(1), new InventoryOwnerId(1), bag.ContainerId, RuntimeFrame.Zero).Success);
            Assert.AreEqual(77, bag.GetStack(0).InstanceId.Value);
            Assert.AreEqual(0, world.Count);
            Assert.AreEqual(WorldItemOperationStatus.DuplicateInstance,
                service.Spawn(new WorldItemStack(new WorldItemId(3), stack,
                    new WorldItemPosition(0, 0, 0), new InventoryOwnerId(1), WorldItemAccessPolicy.Public, 0)).Status);
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
