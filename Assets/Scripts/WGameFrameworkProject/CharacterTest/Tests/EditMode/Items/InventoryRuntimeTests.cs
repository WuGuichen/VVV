using System.Collections.Generic;
using MxFramework.Items;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Items
{
    public sealed class InventoryRuntimeTests
    {
        private static class SampleOwnerIds { public const int Hero = 1; public const int Other = 2; }

        [Test]
        public void InventoryOperations_AddRemoveMoveSplitMergeAndSwap()
        {
            InventoryService service = CreateService(out InventoryStore store, out ItemDefinition herb, out ItemDefinition ore);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 4, "bag");
            var chest = new InventoryContainer(new InventoryContainerId(2), new InventoryOwnerId(SampleOwnerIds.Hero), 4, "chest");
            store.Register(bag);
            store.Register(chest);

            Assert.IsTrue(service.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 10)).Success);
            Assert.IsTrue(service.SplitStack(bag.ContainerId, 0, bag.ContainerId, 1, 4).Success);
            Assert.AreEqual(6, bag.GetStack(0).Count);
            Assert.AreEqual(4, bag.GetStack(1).Count);

            Assert.IsTrue(service.MergeStack(bag.ContainerId, 1, bag.ContainerId, 0).Success);
            Assert.AreEqual(10, bag.GetStack(0).Count);
            Assert.IsTrue(bag.GetStack(1).IsEmpty);

            Assert.IsTrue(service.MoveStack(bag.ContainerId, 0, chest.ContainerId, 0, 5).Success);
            Assert.AreEqual(5, bag.GetStack(0).Count);
            Assert.AreEqual(5, chest.GetStack(0).Count);

            Assert.IsTrue(service.AddStack(bag.ContainerId, 2, new ItemStack(ore.DefinitionId, 3)).Success);
            Assert.IsTrue(service.SwapSlots(bag.ContainerId, 0, bag.ContainerId, 2).Success);
            Assert.AreEqual(ore.DefinitionId, bag.GetStack(0).DefinitionId);
            Assert.AreEqual(herb.DefinitionId, bag.GetStack(2).DefinitionId);

            Assert.IsTrue(service.RemoveStack(chest.ContainerId, 0, 5).Success);
            Assert.IsTrue(chest.GetStack(0).IsEmpty);
        }

        [Test]
        public void InventoryOperations_FailuresAreAtomic()
        {
            InventoryService service = CreateService(out InventoryStore store, out ItemDefinition herb, out ItemDefinition ore);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 2, "bag");
            store.Register(bag);
            service.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 20));
            service.AddStack(bag.ContainerId, 1, new ItemStack(ore.DefinitionId, 1));

            InventoryOperationResult overflow = service.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 1));
            InventoryOperationResult mismatch = service.MergeStack(bag.ContainerId, 1, bag.ContainerId, 0);

            Assert.AreEqual(InventoryOperationStatus.StackOverflow, overflow.Status);
            Assert.AreEqual(InventoryOperationStatus.ItemMismatch, mismatch.Status);
            Assert.AreEqual(20, bag.GetStack(0).Count);
            Assert.AreEqual(ore.DefinitionId, bag.GetStack(1).DefinitionId);
            Assert.AreEqual(1, bag.GetStack(1).Count);
        }

        [Test]
        public void InventoryOperations_MergeOnlyEquivalentCustomState()
        {
            InventoryService service = CreateService(out InventoryStore store, out ItemDefinition herb, out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 3, "bag");
            store.Register(bag);
            var stateA = new RuntimeCustomState(1, "item.state", 1, "{\"quality\":1}");
            var equivalentStateA = new RuntimeCustomState(1, "item.state", 1, "{\"quality\":1}");
            var stateB = new RuntimeCustomState(1, "item.state", 1, "{\"quality\":2}");

            Assert.IsTrue(service.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 2, customState: stateA)).Success);
            InventoryOperationResult incompatibleAdd = service.AddStack(
                bag.ContainerId,
                0,
                new ItemStack(herb.DefinitionId, 1, customState: stateB));
            Assert.AreEqual(InventoryOperationStatus.ItemMismatch, incompatibleAdd.Status);

            InventoryInsertionPlan plan = InventoryInsertionPlanner.CreatePlan(
                bag,
                herb,
                new ItemStack(herb.DefinitionId, 1, customState: stateB));
            Assert.AreEqual(0, plan.MergeCapacity);
            Assert.AreEqual(40, plan.EmptySlotCapacity);
            Assert.IsTrue(service.TryInsertStack(
                bag.ContainerId,
                new ItemStack(herb.DefinitionId, 1, customState: stateB)).Success);

            InventoryOperationResult incompatibleMove = service.MoveStack(
                bag.ContainerId,
                1,
                bag.ContainerId,
                0,
                1);
            Assert.AreEqual(InventoryOperationStatus.ItemMismatch, incompatibleMove.Status);
            Assert.AreEqual("{\"quality\":1}", bag.GetStack(0).CustomState.PayloadJson);
            Assert.AreEqual("{\"quality\":2}", bag.GetStack(1).CustomState.PayloadJson);

            Assert.IsTrue(service.AddStack(
                bag.ContainerId,
                0,
                new ItemStack(herb.DefinitionId, 1, customState: equivalentStateA)).Success);
            Assert.AreEqual(3, bag.GetStack(0).Count);
            Assert.AreEqual("{\"quality\":1}", bag.GetStack(0).CustomState.PayloadJson);
        }

        [Test]
        public void InventoryInsertion_MergesThenSplitsIntoEmptySlotsAtomically()
        {
            InventoryService service = CreateService(out InventoryStore store, out ItemDefinition herb, out ItemDefinition ore);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 3, "bag");
            store.Register(bag);
            Assert.IsTrue(service.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 18)).Success);
            Assert.IsTrue(service.AddStack(bag.ContainerId, 2, new ItemStack(ore.DefinitionId, 1)).Success);

            InventoryInsertionPlan plan = InventoryInsertionPlanner.CreatePlan(bag, herb, new ItemStack(herb.DefinitionId, 7));
            Assert.IsTrue(plan.CanFit);
            Assert.AreEqual(2, plan.MergeCapacity);
            Assert.AreEqual(20, plan.EmptySlotCapacity);

            InventoryOperationResult inserted = service.TryInsertStack(bag.ContainerId, new ItemStack(herb.DefinitionId, 7));
            Assert.AreEqual(InventoryOperationStatus.Success, inserted.Status);
            Assert.AreEqual(20, bag.GetStack(0).Count);
            Assert.AreEqual(5, bag.GetStack(1).Count);
            Assert.AreEqual(ore.DefinitionId, bag.GetStack(2).DefinitionId);

            InventoryOperationResult insufficient = service.TryInsertStack(bag.ContainerId, new ItemStack(herb.DefinitionId, 16));
            Assert.AreEqual(InventoryOperationStatus.InsufficientCapacity, insufficient.Status);
            Assert.AreEqual(20, bag.GetStack(0).Count);
            Assert.AreEqual(5, bag.GetStack(1).Count);
            Assert.AreEqual(1, bag.GetStack(2).Count);
        }

        [Test]
        public void InventoryInsertion_EquipmentMaxStackOneIsPreservedAcrossSlots()
        {
            var definitions = new ItemDefinitionRegistry();
            ItemDefinition sword = ItemDefinitionTests.CreateDefinition(2001, "item.sword", ItemKind.Equipment, 1);
            Assert.IsTrue(definitions.Register(sword).Success);
            var store = new InventoryStore();
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 2, "bag");
            store.Register(bag);
            var service = new InventoryService(definitions, store);

            InventoryOperationResult result = service.TryInsertStack(bag.ContainerId, new ItemStack(sword.DefinitionId, 2));

            Assert.AreEqual(InventoryOperationStatus.Success, result.Status);
            Assert.AreEqual(1, bag.GetStack(0).Count);
            Assert.AreEqual(1, bag.GetStack(1).Count);
            Assert.AreEqual(sword.DefinitionId, bag.GetStack(0).DefinitionId);
            Assert.AreEqual(sword.DefinitionId, bag.GetStack(1).DefinitionId);
        }

        [Test]
        public void InventoryRuntimeModule_ExecutesRequestsAndRejectsStaleUseItem()
        {
            InventoryService service = CreateService(out InventoryStore store, out ItemDefinition herb, out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 2, "bag");
            store.Register(bag);
            var module = new InventoryRuntimeModule(service);

            InventoryRequestHandle addHandle = module.Requests.Add(new InventoryRequest(
                InventoryRequestKind.AddStack,
                bag.ContainerId,
                0,
                itemDefinitionId: herb.DefinitionId,
                count: 3));
            module.Commands.Enqueue(InventoryRuntimeCommandFactory.Create(RuntimeFrame.Zero, 7, ItemRuntimeCommandIds.AddStack, addHandle, new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_InventoryRuntimeTests.Id_test_add, 0)));
            module.Tick(new RuntimeTickContext(0, 1d / 60d, 0d, RuntimeTickStage.Simulation));

            Assert.AreEqual(3, bag.GetStack(0).Count);

            InventoryRequestHandle useHandle = module.Requests.Add(new InventoryRequest(InventoryRequestKind.UseItem, bag.ContainerId, 0));
            module.Commands.Enqueue(InventoryRuntimeCommandFactory.Create(new RuntimeFrame(1), 7, ItemRuntimeCommandIds.UseItem, useHandle, new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_InventoryRuntimeTests.Id_test_use, 0)));
            module.Tick(new RuntimeTickContext(1, 1d / 60d, 1d / 60d, RuntimeTickStage.Simulation));
            Assert.AreEqual(3, bag.GetStack(0).Count);

            var events = new List<ItemRuntimeEvent>();
            module.DrainEvents(new RuntimeFrame(1), events);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(ItemRuntimeEventType.OperationSucceeded, events[0].EventType);
            Assert.AreEqual(InventoryOperationStatus.UnsupportedUse, events[1].Status);

            var stale = new InventoryRequestHandle(useHandle.Index, useHandle.Generation);
            module.Commands.Enqueue(InventoryRuntimeCommandFactory.Create(new RuntimeFrame(2), 7, ItemRuntimeCommandIds.UseItem, stale, new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_InventoryRuntimeTests.Id_test_stale, 0)));
            module.Tick(new RuntimeTickContext(2, 1d / 60d, 2d / 60d, RuntimeTickStage.Simulation));
            events.Clear();
            module.DrainEvents(new RuntimeFrame(2), events);
            Assert.AreEqual(InventoryOperationStatus.StaleRequest, events[0].Status);
        }

        [Test]
        public void InventorySaveState_RoundTripsAndDefinitionHashMismatchFails()
        {
            InventoryService service = CreateService(out InventoryStore store, out ItemDefinition herb, out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 2, "bag");
            store.Register(bag);
            service.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 8));

            var hashBefore = new RuntimeHashCombiner(new[] { new InventoryHashContributor(store) }).ComputeHash(RuntimeFrame.Zero);
            var provider = new InventorySaveStateProvider(store, service.Definitions, () => 12);
            RuntimeSaveState captured = provider.CaptureSaveState().Value;
            Assert.AreEqual(ItemRuntimeIds.InventoryModule, captured.ModuleStates[0].ModuleId);
            Assert.AreEqual(ItemRuntimeIds.InventoryModule, captured.ModuleStates[0].CustomState.TypeId);
            string json = RuntimeSaveStateJson.SaveToJson(captured);
            RuntimeSaveState loaded = RuntimeSaveStateJson.LoadFromJson(json).Value;

            var restoredStore = new InventoryStore();
            var restorer = new InventorySaveStateProvider(restoredStore, service.Definitions);
            Assert.IsTrue(restorer.RestoreSaveState(loaded).Success);
            var hashAfter = new RuntimeHashCombiner(new[] { new InventoryHashContributor(restoredStore) }).ComputeHash(RuntimeFrame.Zero);
            Assert.AreEqual(hashBefore, hashAfter);

            var changedDefinitions = new ItemDefinitionRegistry();
            changedDefinitions.Register(ItemDefinitionTests.CreateDefinition(1001, "item.herb.changed", ItemKind.Material, 20));
            var mismatch = new InventorySaveStateProvider(new InventoryStore(), changedDefinitions).RestoreSaveState(loaded);
            Assert.IsFalse(mismatch.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.CustomStateMismatch, mismatch.Error.Code);
        }

        [Test]
        public void InventorySaveState_InvalidStackFailsWithoutMutatingLiveStore()
        {
            InventoryService service = CreateService(out InventoryStore store, out ItemDefinition herb, out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 2, "bag");
            store.Register(bag);
            service.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 8));

            string invalidPayload = "{\"schemaVersion\":" + InventorySaveState.CurrentSchemaVersion + ",\"definitionHash\":"
                + service.Definitions.ComputeStableHash()
                + ",\"containers\":[{\"containerId\":1,\"stableId\":\"bag\",\"ownerId\":1,\"slotCount\":2,\"slots\":[{\"slotIndex\":0,\"itemDefinitionId\":1001,\"count\":99,\"itemInstanceId\":0,\"customStateTypeId\":\"\",\"customStateSchemaVersion\":0,\"customStatePayloadJson\":\"\"}]}]}";
            var invalidModule = new RuntimeModuleSaveState(
                ItemRuntimeIds.InventoryModule,
                InventorySaveStateProvider.CustomStateSchemaVersion,
                new RuntimeCustomState(3300, ItemRuntimeIds.InventoryModule,
                    InventorySaveStateProvider.CustomStateSchemaVersion,
                    invalidPayload));
            var invalidSaveState = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                System.DateTime.UtcNow,
                string.Empty,
                string.Empty,
                string.Empty,
                0L,
                null,
                null,
                new[] { invalidModule });

            RuntimeSaveStateResult<bool> result =
                new InventorySaveStateProvider(store, service.Definitions).RestoreSaveState(invalidSaveState);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Error.Code);
            Assert.IsTrue(store.TryGet(bag.ContainerId, out InventoryContainer retainedBag));
            Assert.AreSame(bag, retainedBag);
            Assert.AreEqual(8, retainedBag.GetStack(0).Count);
            Assert.IsTrue(retainedBag.GetStack(1).IsEmpty);
        }

        [Test]
        public void InventoryOperations_IntMaxOverflowIsRejectedWithoutMutation()
        {
            var definitions = new ItemDefinitionRegistry();
            var item = ItemDefinitionTests.CreateDefinition(1001, "item.large", ItemKind.Material, int.MaxValue);
            definitions.Register(item);
            var store = new InventoryStore();
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(1), 2);
            store.Register(bag);
            var service = new InventoryService(definitions, store);
            Assert.IsTrue(service.AddStack(bag.ContainerId, 0, new ItemStack(item.DefinitionId, int.MaxValue)).Success);
            Assert.IsTrue(service.AddStack(bag.ContainerId, 1, new ItemStack(item.DefinitionId, 1)).Success);
            Assert.AreEqual(InventoryOperationStatus.StackOverflow,
                service.AddStack(bag.ContainerId, 0, new ItemStack(item.DefinitionId, 1)).Status);
            Assert.AreEqual(InventoryOperationStatus.StackOverflow, service.MoveStack(bag.ContainerId, 1, bag.ContainerId, 0, 1).Status);
            Assert.AreEqual(InventoryOperationStatus.StackOverflow, service.MergeStack(bag.ContainerId, 1, bag.ContainerId, 0).Status);
            Assert.AreEqual(int.MaxValue, bag.GetStack(0).Count);
            Assert.AreEqual(1, bag.GetStack(1).Count);
        }

        [Test]
        public void InventoryInsertion_LargeCapacityDoesNotRejectFittingRequests()
        {
            var definitions = new ItemDefinitionRegistry();
            var item = ItemDefinitionTests.CreateDefinition(1001, "item.large", ItemKind.Material, int.MaxValue);
            definitions.Register(item);
            var store = new InventoryStore();
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(1), 2);
            store.Register(bag);
            var service = new InventoryService(definitions, store);
            var plan = InventoryInsertionPlanner.CreatePlan(bag, item, new ItemStack(item.DefinitionId, 1));
            Assert.AreEqual(int.MaxValue, plan.TotalCapacity);
            Assert.IsTrue(plan.CanFit);
            Assert.IsTrue(service.TryInsertStack(bag.ContainerId, new ItemStack(item.DefinitionId, 1)).Success);
            Assert.IsTrue(service.TryInsertStack(bag.ContainerId, new ItemStack(item.DefinitionId, int.MaxValue)).Success);
            Assert.AreEqual(int.MaxValue, bag.GetStack(0).Count);
            Assert.AreEqual(1, bag.GetStack(1).Count);
            Assert.AreEqual(int.MaxValue, new InventoryInsertionPlan(1, int.MaxValue, int.MaxValue).TotalCapacity);
        }

        [Test]
        public void Showcase_InvalidWorldRestoreLeavesBothLiveStoresAndCommandsUsable()
        {
            using (var showcase = new MxFramework.Demo.Items.ItemLifecycleShowcase())
            {
                showcase.OpenChest();
                Assert.IsTrue(showcase.Save());
                showcase.PickupNearest();
                var before = showcase.CreateSnapshot();
                var field = typeof(MxFramework.Demo.Items.ItemLifecycleShowcase).GetField("_savedStateJson",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var json = Newtonsoft.Json.Linq.JObject.Parse((string)field.GetValue(showcase));
                foreach (var module in json["moduleStates"])
                    if ((string)module["moduleId"] == ItemRuntimeIds.WorldItemsModule)
                        module["customState"]["payloadJson"] = "{\"schemaVersion\":1,\"definitionHash\":0,\"items\":[]}";
                field.SetValue(showcase, json.ToString());
                Assert.IsFalse(showcase.Load());
                var after = showcase.CreateSnapshot();
                Assert.AreEqual(before.StateHash, after.StateHash);
                Assert.AreEqual(before.WorldItemCount, after.WorldItemCount);
                Assert.AreEqual(before.InventorySlots[0].Count, after.InventorySlots[0].Count);
                showcase.Move(1, 0);
                Assert.AreEqual(before.PlayerX + 1, showcase.CreateSnapshot().PlayerX);
            }
        }

        [Test]
        public void Showcase_LoadPublishesRestoredContainerReferencesAndFuturePickup()
        {
            using (var showcase = new MxFramework.Demo.Items.ItemLifecycleShowcase())
            {
                showcase.OpenChest();
                Assert.IsTrue(showcase.Save());
                var saved = showcase.CreateSnapshot();
                showcase.PickupNearest();
                Assert.IsFalse(showcase.CreateSnapshot().InventorySlots[0].IsEmpty);
                Assert.IsTrue(showcase.Load());
                var restored = showcase.CreateSnapshot();
                Assert.AreEqual(saved.StateHash, restored.StateHash);
                Assert.IsTrue(restored.InventorySlots[0].IsEmpty);
                showcase.PickupNearest();
                Assert.IsFalse(showcase.CreateSnapshot().InventorySlots[0].IsEmpty);
                Assert.AreEqual(saved.WorldItemCount - 1, showcase.CreateSnapshot().WorldItemCount);
            }
        }

        [Test]
        public void InventoryInstances_RejectDuplicatesAcrossContainersAndPreserveIdentityOnMove()
        {
            var service = CreateService(out var store, out var herb, out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(1), 2);
            var equipment = new InventoryContainer(new InventoryContainerId(2), new InventoryOwnerId(1), 1);
            store.Register(bag); store.Register(equipment);
            Assert.IsTrue(store.TryAllocateInstanceId(out var instance));
            Assert.IsTrue(service.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 1, instance)).Success);
            Assert.AreEqual(InventoryOperationStatus.DuplicateInstance,
                service.AddStack(equipment.ContainerId, 0, new ItemStack(herb.DefinitionId, 1, instance)).Status);
            Assert.AreEqual(InventoryOperationStatus.DuplicateInstance,
                service.TryInsertStack(equipment.ContainerId, new ItemStack(herb.DefinitionId, 1, instance)).Status);
            Assert.IsTrue(service.MoveStack(bag.ContainerId, 0, equipment.ContainerId, 0, 1).Success);
            Assert.IsTrue(bag.GetStack(0).IsEmpty);
            Assert.AreEqual(instance, equipment.GetStack(0).InstanceId);
            Assert.AreEqual(2, store.NextItemInstanceId);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ItemStack(herb.DefinitionId, 1, new ItemInstanceId(-1)));
        }

        [Test]
        public void InventoryInstances_AllocatedThenRemovedIdentitiesRemainConsumedAfterReload()
        {
            var service = CreateService(out var store, out var herb, out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(1), 1);
            store.Register(bag);
            Assert.IsTrue(store.TryAllocateInstanceId(out var first));
            Assert.IsTrue(service.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 1, first)).Success);
            Assert.IsTrue(service.RemoveStack(bag.ContainerId, 0, 1).Success);
            Assert.IsTrue(store.TryAllocateInstanceId(out var unplaced));
            var save = new InventorySaveStateProvider(store, service.Definitions).CaptureSaveState().Value;
            var restored = new InventoryStore();
            Assert.IsTrue(new InventorySaveStateProvider(restored, service.Definitions).RestoreSaveState(save).Success);
            Assert.AreEqual(3, restored.NextItemInstanceId);
            Assert.IsTrue(restored.TryAllocateInstanceId(out var next));
            Assert.AreEqual(3, next.Value);
            Assert.AreEqual(2, unplaced.Value);
        }

        [TestCase(2, false)]
        [TestCase(3, true)]
        public void InventoryInstances_LegacyMigrationAndInvalidCounterHaveExplicitBoundaries(int schema, bool fail)
        {
            var service = CreateService(out var store, out var herb, out _);
            var retained = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(1), 1);
            store.Register(retained);
            service.AddStack(retained.ContainerId, 0, new ItemStack(herb.DefinitionId, 2));
            var payload = new InventorySaveState
            {
                SchemaVersion = schema, DefinitionHash = service.Definitions.ComputeStableHash(), NextItemInstanceId = 1,
                Containers = new[] { new InventoryContainerSaveState { ContainerId = 2, OwnerId = 1, SlotCount = 1,
                    Slots = new[] { new InventorySlotSaveState { SlotIndex = 0, ItemDefinitionId = herb.DefinitionId.Value,
                        Count = 1, ItemInstanceId = int.MaxValue } } } }
            };
            var result = new InventorySaveStateProvider(store, service.Definitions).RestoreSaveState(InventoryFixture(payload));
            Assert.AreEqual(!fail, result.Success);
            if (fail)
            {
                Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Error.Code);
                Assert.IsTrue(store.TryGet(retained.ContainerId, out var live));
                Assert.AreSame(retained, live);
                Assert.AreEqual(2, live.GetStack(0).Count);
            }
            else
            {
                Assert.AreEqual((long)int.MaxValue + 1, store.NextItemInstanceId);
                Assert.IsFalse(store.TryAllocateInstanceId(out _));
                Assert.AreEqual(InventorySaveStateProvider.CustomStateSchemaVersion,
                    new InventorySaveStateProvider(store, service.Definitions).CaptureSaveState().Value.ModuleStates[0].SchemaVersion);
            }
        }

        [Test]
        public void InventoryInstances_DuplicateSavedIdentityDoesNotPartiallyRestore()
        {
            var service = CreateService(out var store, out var herb, out _);
            var payload = new InventorySaveState
            {
                SchemaVersion = InventorySaveState.CurrentSchemaVersion,
                DefinitionHash = service.Definitions.ComputeStableHash(), NextItemInstanceId = 8,
                Containers = new[] { new InventoryContainerSaveState { ContainerId = 1, OwnerId = 1, SlotCount = 2,
                    Slots = new[] {
                        new InventorySlotSaveState { SlotIndex = 0, ItemDefinitionId = herb.DefinitionId.Value, Count = 1, ItemInstanceId = 7 },
                        new InventorySlotSaveState { SlotIndex = 1, ItemDefinitionId = herb.DefinitionId.Value, Count = 1, ItemInstanceId = 7 } } } }
            };
            var result = new InventorySaveStateProvider(store, service.Definitions).RestoreSaveState(InventoryFixture(payload));
            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, store.Count);
            Assert.AreEqual(1, store.NextItemInstanceId);
        }

        private static RuntimeSaveState InventoryFixture(InventorySaveState payload)
        {
            var module = new RuntimeModuleSaveState(ItemRuntimeIds.InventoryModule, payload.SchemaVersion,
                new RuntimeCustomState(3300, ItemRuntimeIds.InventoryModule, payload.SchemaVersion,
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload)));
            return new RuntimeSaveState(RuntimeSaveState.CurrentSchemaVersion, System.DateTime.UtcNow,
                string.Empty, string.Empty, string.Empty, 0, null, null, new[] { module });
        }

        internal static InventoryService CreateService(out InventoryStore store, out ItemDefinition herb, out ItemDefinition ore)
        {
            var definitions = new ItemDefinitionRegistry();
            herb = ItemDefinitionTests.CreateDefinition(1001, "item.herb", ItemKind.Material, 20);
            ore = ItemDefinitionTests.CreateDefinition(1002, "item.ore", ItemKind.Material, 10);
            definitions.Register(herb);
            definitions.Register(ore);
            store = new InventoryStore();
            return new InventoryService(definitions, store);
        }
    }

    internal static class FixtureIds_InventoryRuntimeTests
    {
        public const int Id_test_add = 1;
        public const int Id_test_stale = 2;
        public const int Id_test_use = 3;
    }
}
