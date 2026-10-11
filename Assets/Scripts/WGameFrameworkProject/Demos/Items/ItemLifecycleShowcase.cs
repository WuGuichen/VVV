using System;
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.Config;
using MxFramework.Items;
using MxFramework.Items.CharacterApplicationBridge;
using MxFramework.Resources;
using MxFramework.Runtime;

namespace MxFramework.Demo.Items
{
    public sealed class ItemLifecycleShowcase : IDisposable
    {
        private static class SampleSocketIds { public const int MainHand = 1; public const int OffHand = 2; }

        private object _hashOwner;
        private RuntimeHashCombiner _hashCombiner;

        private static class SampleOwnerIds { public const int Hero = 1; public const int Other = 2; }

        public const int MoveCommandId = 1003401;
        public const int OpenChestCommandId = 1003402;
        public const int PickupNearestCommandId = 1003403;
        public const int EquipSelectedSlotCommandId = 1003404;
        public const int EquipFirstWeaponCommandId = EquipSelectedSlotCommandId;
        public const int UnequipSelectedSlotCommandId = 1003405;
        public const int SelectEquipmentSlotCommandId = 1003406;

        private const int UiSourceId = 1;
        private const int HerbItemId = 1001;
        private const int OreItemId = 1002;
        private const int SwordItemId = 2001;
        private const double StepSeconds = 1d / 60d;

        private readonly List<RuntimeCommand> _drainedCommands = new List<RuntimeCommand>(8);
        private readonly List<WorldItemRuntimeEvent> _worldEvents = new List<WorldItemRuntimeEvent>(8);
        private readonly List<string> _log = new List<string>(24);
        private readonly List<string> _logSnapshot = new List<string>(24);

        private RuntimeHost _host;
        private RuntimeClock _clock;
        private RuntimeCommandBuffer _commands;
        private DeterministicRandom _random;
        private ItemDefinitionRegistry _definitions;
        private LootResolver _lootResolver;
        private LootTableDefinition _lootTable;
        private InventoryStore _inventoryStore;
        private InventoryService _inventoryService;
        private InventoryContainer _bag;
        private InventoryContainer _equipment;
        private WorldItemStore _worldStore;
        private WorldItemService _worldService;
        private WorldItemRuntimeModule _worldModule;
        private CharacterItemEquipmentProfileRegistry _equipmentProfiles;
        private EquipmentSchemaConfig _equipmentSchema;
        private int _playerX;
        private int _playerZ;
        private int _nextWorldItemId;
        private int _selectedEquipmentSlotIndex;
        private long _savedHash;
        private string _savedStateJson = string.Empty;
        private string _saveStatus = "No save";
        private string _hashStatus = "No comparison";
        private bool _disposed;

        public ItemLifecycleShowcase()
        {
            Reset();
        }

        public bool HasSavedState => !string.IsNullOrEmpty(_savedStateJson);

        public void Reset()
        {
            ThrowIfDisposed();
            DisposeRuntime();

            _clock = new RuntimeClock(RuntimeFrame.Zero);
            _commands = new RuntimeCommandBuffer(initialCapacity: 16);
            _random = new DeterministicRandom(20260708u);
            _definitions = CreateDefinitions();
            _lootResolver = new LootResolver(_definitions);
            _lootTable = CreateLootTable();
            _equipmentProfiles = CreateEquipmentProfiles();
            _equipmentSchema = CreateEquipmentSchema();
            _inventoryStore = new InventoryStore();
            _bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 8, "bag");
            _equipment = new InventoryContainer(new InventoryContainerId(2), new InventoryOwnerId(SampleOwnerIds.Hero), _equipmentSchema.Slots.Length, "equipment");
            _inventoryStore.Register(_bag);
            _inventoryStore.Register(_equipment);
            _inventoryService = new InventoryService(_definitions, _inventoryStore);
            _worldStore = new WorldItemStore();
            _worldService = new WorldItemService(_worldStore, _inventoryService);
            _worldModule = new WorldItemRuntimeModule(_worldService, commandCapacity: 16);
            _playerX = 0;
            _playerZ = 0;
            _nextWorldItemId = 1;
            _selectedEquipmentSlotIndex = 0;
            _savedHash = 0L;
            _savedStateJson = string.Empty;
            _saveStatus = "No save";
            _hashStatus = "No comparison";
            _log.Clear();

            _host = new RuntimeHost();
            _host.RegisterModule(new ShowcaseCommandModule(this));
            _host.RegisterModule(_worldModule);
            _host.Initialize();
            _host.Start();
            AddLog("Showcase reset");
        }

        public void Move(int deltaX, int deltaZ)
        {
            EnqueueAndTick(new RuntimeCommand(
                _clock.CurrentFrame,
                UiSourceId,
                MoveCommandId,
                0,
                Clamp(deltaX, -1, 1),
                Clamp(deltaZ, -1, 1),
                0,
                new RuntimeTraceIdentity(ItemLifecycleShowcaseTraceKinds.K_item_lifecycle_move, 0, 0, 0)));
        }

        public void OpenChest()
        {
            EnqueueAndTick(new RuntimeCommand(_clock.CurrentFrame, UiSourceId, OpenChestCommandId, 0, traceId: new RuntimeTraceIdentity(ItemLifecycleShowcaseTraceKinds.K_item_lifecycle_open, 0, 0, 0)));
        }

        public void PickupNearest()
        {
            EnqueueAndTick(new RuntimeCommand(_clock.CurrentFrame, UiSourceId, PickupNearestCommandId, 0, traceId: new RuntimeTraceIdentity(ItemLifecycleShowcaseTraceKinds.K_item_lifecycle_pickup, 0, 0, 0)));
        }

        public void EquipFirstWeapon()
        {
            EnqueueAndTick(new RuntimeCommand(_clock.CurrentFrame, UiSourceId, EquipSelectedSlotCommandId, 0, traceId: new RuntimeTraceIdentity(ItemLifecycleShowcaseTraceKinds.K_item_lifecycle_equip, 0, 0, 0)));
        }

        /// <remarks>Allocation: LowFreqAlloc command submission from UI/test code.</remarks>
        public void UnequipSelectedSlot()
        {
            EnqueueAndTick(new RuntimeCommand(_clock.CurrentFrame, UiSourceId, UnequipSelectedSlotCommandId, 0, traceId: new RuntimeTraceIdentity(ItemLifecycleShowcaseTraceKinds.K_item_lifecycle_unequip, 0, 0, 0)));
        }

        /// <remarks>Allocation: LowFreqAlloc command submission from UI/test code.</remarks>
        public void SelectEquipmentSlot(int slotIndex)
        {
            EnqueueAndTick(new RuntimeCommand(
                _clock.CurrentFrame,
                UiSourceId,
                SelectEquipmentSlotCommandId,
                0,
                slotIndex,
                0,
                0,
                new RuntimeTraceIdentity(ItemLifecycleShowcaseTraceKinds.K_item_lifecycle_select_equipment_slot, 0, 0, 0)));
        }

        public void TickIdle()
        {
            Tick();
        }

        public bool Save()
        {
            RuntimeSaveStateResult<RuntimeSaveState> inventoryState =
                new InventorySaveStateProvider(_inventoryStore, _definitions, () => _clock.CurrentFrame.Value).CaptureSaveState();
            RuntimeSaveStateResult<RuntimeSaveState> worldState =
                new WorldItemSaveStateProvider(_worldStore, _definitions, () => _clock.CurrentFrame.Value).CaptureSaveState();
            if (!inventoryState.Success || !worldState.Success)
            {
                _saveStatus = "Save failed";
                AddLog(_saveStatus);
                return false;
            }

            var moduleStates = new List<RuntimeModuleSaveState>();
            Append(moduleStates, inventoryState.Value.ModuleStates);
            Append(moduleStates, worldState.Value.ModuleStates);
            var combined = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                "item-lifecycle-showcase-v0",
                string.Empty,
                string.Empty,
                _clock.CurrentFrame.Value,
                null,
                null,
                moduleStates);
            _savedStateJson = RuntimeSaveStateJson.SaveToJson(combined);
            _savedHash = ComputeStateHash();
            _saveStatus = "Saved hash " + _savedHash;
            _hashStatus = "Saved == current";
            AddLog(_saveStatus);
            return true;
        }

        public bool Load()
        {
            if (string.IsNullOrEmpty(_savedStateJson))
            {
                _saveStatus = "Load skipped: no save";
                AddLog(_saveStatus);
                return false;
            }

            RuntimeSaveStateResult<RuntimeSaveState> load = RuntimeSaveStateJson.LoadFromJson(_savedStateJson);
            if (!load.Success)
            {
                _saveStatus = "Load failed: " + load.Error.Message;
                AddLog(_saveStatus);
                return false;
            }

            // Prepare a complete detached candidate. A later participant's failure must not
            // publish an earlier participant, and live container references must follow it.
            var candidateInventory = new InventoryStore();
            var candidateWorld = new WorldItemStore();
            RuntimeSaveStateResult<bool> inventoryRestore =
                new InventorySaveStateProvider(candidateInventory, _definitions).RestoreSaveState(load.Value);
            RuntimeSaveStateResult<bool> worldRestore =
                new WorldItemSaveStateProvider(candidateWorld, _definitions).RestoreSaveState(load.Value);
            if (!inventoryRestore.Success || !worldRestore.Success)
            {
                _saveStatus = "Load failed";
                AddLog(_saveStatus);
                return false;
            }

            if (!candidateInventory.TryGet(_bag.ContainerId, out InventoryContainer candidateBag)
                || !candidateInventory.TryGet(_equipment.ContainerId, out InventoryContainer candidateEquipment)
                || candidateBag.SlotCount != _bag.SlotCount
                || candidateEquipment.SlotCount != _equipmentSchema.Slots.Length
                || !candidateBag.OwnerId.Equals(_bag.OwnerId)
                || !candidateEquipment.OwnerId.Equals(_equipment.OwnerId)
                || !CharacterItemEquipmentProjector.Project(candidateEquipment, _equipmentSchema,
                    new EquipmentLoadoutId(760101), "showcase.item_lifecycle.loadout",
                    _definitions, _equipmentProfiles).IsSuccess)
            {
                _saveStatus = "Load failed: invalid inventory layout or equipment";
                AddLog(_saveStatus);
                return false;
            }

            bool legacyInventory = false;
            for (int i = 0; i < load.Value.ModuleStates.Count; i++)
                if (load.Value.ModuleStates[i].ModuleId == ItemRuntimeIds.InventoryModule)
                    legacyInventory = load.Value.ModuleStates[i].SchemaVersion == 2;
            for (int i = 0; i < candidateWorld.Count; i++)
            {
                ItemInstanceId id = candidateWorld.GetAtSortedIndex(i).Stack.InstanceId;
                if (!id.IsValid) continue;
                if (candidateInventory.ContainsInstance(id)
                    || (!legacyInventory && id.Value >= candidateInventory.NextItemInstanceId))
                {
                    _saveStatus = "Load failed: duplicate instance or invalid instance counter";
                    AddLog(_saveStatus);
                    return false;
                }
                if (legacyInventory)
                    candidateInventory.TryRestoreInstanceCounter(Math.Max(candidateInventory.NextItemInstanceId,
                        (long)id.Value + 1));
            }
            var candidateService = new InventoryService(_definitions, candidateInventory);
            var candidateWorldService = new WorldItemService(candidateWorld, candidateService);
            var candidateWorldModule = new WorldItemRuntimeModule(candidateWorldService, commandCapacity: 16);
            var candidateHost = new RuntimeHost();
            candidateHost.RegisterModule(new ShowcaseCommandModule(this));
            candidateHost.RegisterModule(candidateWorldModule);
            candidateHost.Initialize();
            candidateHost.Start();

            RuntimeHost previousHost = _host;
            _inventoryStore = candidateInventory;
            _worldStore = candidateWorld;
            _bag = candidateBag;
            _equipment = candidateEquipment;
            _inventoryService = candidateService;
            _worldService = candidateWorldService;
            _worldModule = candidateWorldModule;
            _host = candidateHost;
            previousHost.Dispose();

            _saveStatus = "Loaded hash " + ComputeStateHash();
            _hashStatus = ComputeStateHash() == _savedHash ? "Saved == current" : "Saved != current";
            AddLog(_saveStatus);
            return true;
        }

        public long CompareSavedHash()
        {
            long current = ComputeStateHash();
            _hashStatus = HasSavedState && current == _savedHash
                ? "Saved == current"
                : "Saved != current";
            AddLog("Hash compare: " + _hashStatus + " current " + current);
            return current;
        }

        public long ComputeStateHash()
        {
            if (!ReferenceEquals(_hashOwner, _inventoryStore))
            {
                _hashOwner = _inventoryStore;
                _hashCombiner = new RuntimeHashCombiner(new IRuntimeHashContributor[] { new InventoryHashContributor(_inventoryStore), new WorldItemHashContributor(_worldStore) });
            }
            return _hashCombiner.ComputeHash(RuntimeFrame.Zero);
        }

        public ItemLifecycleShowcaseSnapshot CreateSnapshot()
        {
            _logSnapshot.Clear();
            for (int i = 0; i < _log.Count; i++)
                _logSnapshot.Add(_log[i]);

            return new ItemLifecycleShowcaseSnapshot(
                _clock.CurrentFrame,
                _playerX,
                _playerZ,
                _worldStore.Count,
                CreateWorldSnapshots(),
                CreateSlotSnapshots(_bag, null, -1),
                CreateSlotSnapshots(_equipment, _equipmentSchema, _selectedEquipmentSlotIndex),
                CreateEquipmentText(),
                _selectedEquipmentSlotIndex,
                GetEquipmentSlotLabel(_selectedEquipmentSlotIndex),
                ComputeStateHash(),
                _saveStatus,
                _hashStatus,
                _logSnapshot.ToArray());
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            DisposeRuntime();
            _disposed = true;
        }

        private void EnqueueAndTick(RuntimeCommand command)
        {
            _commands.Enqueue(command);
            Tick();
        }

        private void Tick()
        {
            RuntimeFrame frame = _clock.CurrentFrame;
            _host.Tick(frame.Value, StepSeconds, frame.Value * StepSeconds);
            DrainWorldEvents(frame);
            _clock.Step();
        }

        private void ProcessCommands(RuntimeTickContext context)
        {
            _drainedCommands.Clear();
            RuntimeFrame frame = new RuntimeFrame(context.FrameIndex);
            _commands.DrainForFrame(frame, _drainedCommands);
            for (int i = 0; i < _drainedCommands.Count; i++)
                ProcessCommand(frame, _drainedCommands[i]);
            _drainedCommands.Clear();
        }

        private void ProcessCommand(RuntimeFrame frame, RuntimeCommand command)
        {
            switch (command.CommandId)
            {
                case MoveCommandId:
                    int oldX = _playerX;
                    int oldZ = _playerZ;
                    _playerX = Clamp(_playerX + command.Payload0, -4, 4);
                    _playerZ = Clamp(_playerZ + command.Payload1, -4, 4);
                    AddLog("Move " + command.Payload0 + "," + command.Payload1 + ": " + oldX + "," + oldZ + " -> " + _playerX + "," + _playerZ);
                    break;
                case OpenChestCommandId:
                    OpenLoot(frame);
                    break;
                case PickupNearestCommandId:
                    PickupNearestWorldItem(frame);
                    break;
                case EquipSelectedSlotCommandId:
                    EquipFirstWeaponFromBag();
                    break;
                case UnequipSelectedSlotCommandId:
                    UnequipSelectedEquipmentSlot();
                    break;
                case SelectEquipmentSlotCommandId:
                    SelectEquipmentSlotInternal(command.Payload0);
                    break;
            }
        }

        private void OpenLoot(RuntimeFrame frame)
        {
            LootResolveResult result = _lootResolver.Resolve(
                _lootTable,
                new LootContext("showcase.chest", 5, new ItemTagSet(new[] { 42 })),
                _random);
            if (!result.Success)
            {
                AddLog("Loot failed: " + (result.Diagnostics.Length > 0 ? result.Diagnostics[0].Message : result.Status.ToString()));
                return;
            }

            if (result.Items.Length == 0)
            {
                AddLog("Loot rolled empty");
                return;
            }

            for (int i = 0; i < result.Items.Length; i++)
            {
                LootItemQuantity item = result.Items[i];
                WorldItemId worldItemId = new WorldItemId(_nextWorldItemId++);
                WorldItemPosition position = new WorldItemPosition(_playerX + i + 1, 0, _playerZ);
                WorldItemRequestHandle handle = _worldModule.Requests.Add(new WorldItemRequest(
                    WorldItemRequestKind.SpawnWorldItem,
                    worldItemId,
                    new ItemStack(item.ItemDefinitionId, item.Count),
                    position,
                    new InventoryOwnerId(SampleOwnerIds.Hero),
                    WorldItemAccessPolicy.Public,
                    frame.Value,
                    expireFrame: frame.Value + 600));
                _worldModule.Commands.Enqueue(WorldItemRuntimeCommandFactory.Create(
                    frame,
                    UiSourceId,
                    WorldItemRuntimeCommandIds.SpawnWorldItem,
                    handle,
                    new RuntimeTraceIdentity(ItemLifecycleShowcaseTraceKinds.K_item_lifecycle_spawn_world_item, 0, 0, 0)));
                AddLog("Loot: " + GetItemName(item.ItemDefinitionId) + " x" + item.Count + " -> world #" + worldItemId.Value + " @ " + position.X + "," + position.Z);
            }
        }

        private void PickupNearestWorldItem(RuntimeFrame frame)
        {
            if (!TryFindClosestWorldItem(out WorldItemStack item, out int distance))
            {
                AddLog("Pickup skipped: no world items");
                return;
            }

            if (distance > 1)
            {
                AddLog("Pickup skipped: nearest #" + item.WorldItemId.Value + " " + GetItemName(item.Stack.DefinitionId)
                    + " @ " + item.Position.X + "," + item.Position.Z + " d=" + distance + " range=1");
                return;
            }

            AddLog("Pickup target: #" + item.WorldItemId.Value + " " + GetItemName(item.Stack.DefinitionId)
                + " @ " + item.Position.X + "," + item.Position.Z + " d=" + distance);
            WorldItemRequestHandle handle = _worldModule.Requests.Add(new WorldItemRequest(
                WorldItemRequestKind.PickupWorldItem,
                item.WorldItemId,
                requesterOwnerId: new InventoryOwnerId(SampleOwnerIds.Hero),
                destinationContainerId: _bag.ContainerId));
            _worldModule.Commands.Enqueue(WorldItemRuntimeCommandFactory.Create(
                frame,
                UiSourceId,
                WorldItemRuntimeCommandIds.PickupWorldItem,
                handle,
                new RuntimeTraceIdentity(ItemLifecycleShowcaseTraceKinds.K_item_lifecycle_pickup_world_item, 0, 0, 0)));
        }

        private void EquipFirstWeaponFromBag()
        {
            if (!IsEquipmentSlotIndexValid(_selectedEquipmentSlotIndex))
            {
                AddLog("Equip skipped: invalid equipment slot " + _selectedEquipmentSlotIndex);
                return;
            }

            string equipmentSlotName = GetEquipmentSlotLabel(_selectedEquipmentSlotIndex);
            if (!_equipment.GetStack(_selectedEquipmentSlotIndex).IsEmpty)
            {
                AddLog("Equip skipped: " + equipmentSlotName + " occupied");
                return;
            }

            for (int slot = 0; slot < _bag.SlotCount; slot++)
            {
                ItemStack stack = _bag.GetStack(slot);
                if (stack.IsEmpty || !_definitions.TryGet(stack.DefinitionId, out ItemDefinition definition) || definition.Kind != ItemKind.Equipment)
                    continue;

                InventoryOperationResult result = _inventoryService.MoveStack(_bag.ContainerId, slot, _equipment.ContainerId, _selectedEquipmentSlotIndex, 1);
                AddLog(result.Success
                    ? "Equipped " + GetItemName(stack.DefinitionId) + " -> " + equipmentSlotName
                    : "Equip failed: " + result.Status);
                return;
            }

            AddLog("Equip skipped: no weapon in bag");
        }

        private void UnequipSelectedEquipmentSlot()
        {
            if (!IsEquipmentSlotIndexValid(_selectedEquipmentSlotIndex))
            {
                AddLog("Unequip skipped: invalid equipment slot " + _selectedEquipmentSlotIndex);
                return;
            }

            string equipmentSlotName = GetEquipmentSlotLabel(_selectedEquipmentSlotIndex);
            ItemStack stack = _equipment.GetStack(_selectedEquipmentSlotIndex);
            if (stack.IsEmpty)
            {
                AddLog("Unequip skipped: " + equipmentSlotName + " empty");
                return;
            }

            if (!TryFindFirstEmptyBagSlot(out int bagSlot))
            {
                AddLog("Unequip failed: bag full");
                return;
            }

            InventoryOperationResult result = _inventoryService.MoveStack(
                _equipment.ContainerId,
                _selectedEquipmentSlotIndex,
                _bag.ContainerId,
                bagSlot,
                stack.Count);
            AddLog(result.Success
                ? "Unequipped " + GetItemName(stack.DefinitionId) + " -> bag slot " + bagSlot
                : "Unequip failed: " + result.Status);
        }

        private void SelectEquipmentSlotInternal(int slotIndex)
        {
            if (!IsEquipmentSlotIndexValid(slotIndex))
            {
                AddLog("Select slot skipped: invalid equipment slot " + slotIndex);
                return;
            }

            _selectedEquipmentSlotIndex = slotIndex;
            AddLog("Target slot: " + GetEquipmentSlotLabel(slotIndex));
        }

        private bool TryFindClosestWorldItem(out WorldItemStack nearest, out int bestDistance)
        {
            nearest = null;
            bestDistance = int.MaxValue;
            for (int i = 0; i < _worldStore.Count; i++)
            {
                WorldItemStack item = _worldStore.GetAtSortedIndex(i);
                int distance = Math.Abs(item.Position.X - _playerX) + Math.Abs(item.Position.Z - _playerZ);
                if (distance > bestDistance)
                    continue;

                if (distance == bestDistance && nearest != null && item.WorldItemId.Value > nearest.WorldItemId.Value)
                    continue;

                bestDistance = distance;
                nearest = item;
            }

            return nearest != null;
        }

        private void DrainWorldEvents(RuntimeFrame frame)
        {
            _worldEvents.Clear();
            _worldModule.DrainEvents(frame, _worldEvents);
            for (int i = 0; i < _worldEvents.Count; i++)
            {
                WorldItemRuntimeEvent worldEvent = _worldEvents[i];
                if (worldEvent.EventType == WorldItemRuntimeEventType.OperationSucceeded)
                {
                    if (worldEvent.RequestKind == WorldItemRequestKind.PickupWorldItem)
                        AddLog("Picked up world item " + worldEvent.WorldItemId.Value);
                    else if (worldEvent.RequestKind == WorldItemRequestKind.SpawnWorldItem)
                        AddLog("World item spawned " + worldEvent.WorldItemId.Value);
                }
                else if (worldEvent.EventType == WorldItemRuntimeEventType.ItemExpired)
                {
                    AddLog("World item expired " + worldEvent.WorldItemId.Value);
                }
                else
                {
                    AddLog("World item rejected: " + worldEvent.Status + " / " + worldEvent.InventoryStatus);
                }
            }
        }

        private ItemLifecycleWorldItemSnapshot[] CreateWorldSnapshots()
        {
            var items = new ItemLifecycleWorldItemSnapshot[_worldStore.Count];
            for (int i = 0; i < _worldStore.Count; i++)
            {
                WorldItemStack item = _worldStore.GetAtSortedIndex(i);
                int distance = Math.Abs(item.Position.X - _playerX) + Math.Abs(item.Position.Z - _playerZ);
                items[i] = new ItemLifecycleWorldItemSnapshot(
                    item.WorldItemId.Value,
                    GetItemName(item.Stack.DefinitionId),
                    item.Stack.Count,
                    CreateIconSnapshot(item.Stack.DefinitionId),
                    item.Position.X,
                    item.Position.Z,
                    distance,
                    distance <= 1);
            }

            return items;
        }

        private ItemLifecycleSlotSnapshot[] CreateSlotSnapshots(
            InventoryContainer container,
            EquipmentSchemaConfig equipmentSchema,
            int selectedSlotIndex)
        {
            var slots = new ItemLifecycleSlotSnapshot[container.SlotCount];
            for (int i = 0; i < container.SlotCount; i++)
            {
                ItemStack stack = container.GetStack(i);
                string slotName = equipmentSchema == null ? "Slot " + i : GetEquipmentSlotLabel(i);
                bool selected = equipmentSchema != null && i == selectedSlotIndex;
                slots[i] = stack.IsEmpty
                    ? new ItemLifecycleSlotSnapshot(i, slotName, string.Empty, 0, ItemLifecycleIconSnapshot.Empty, selected)
                    : new ItemLifecycleSlotSnapshot(i, slotName, GetItemName(stack.DefinitionId), stack.Count, CreateIconSnapshot(stack.DefinitionId), selected);
            }

            return slots;
        }

        private ItemLifecycleIconSnapshot CreateIconSnapshot(ItemDefinitionId itemDefinitionId)
        {
            if (!_definitions.TryGet(itemDefinitionId, out ItemDefinition definition))
                return ItemLifecycleIconSnapshot.Empty;

            ItemResourceRef[] resourceRefs = definition.ResourceRefs;
            for (int i = 0; i < resourceRefs.Length; i++)
            {
                ItemResourceRef resourceRef = resourceRefs[i];
                if (!resourceRef.IsValid)
                    continue;
                if (!string.Equals(resourceRef.UsageId, ItemResourceUsageIds.Icon, StringComparison.Ordinal))
                    continue;
                if (!string.Equals(resourceRef.Key.TypeId, ResourceTypeIds.Sprite, StringComparison.Ordinal))
                    continue;

                return new ItemLifecycleIconSnapshot(resourceRef.Key.Id, resourceRef.Key.TypeId);
            }

            return ItemLifecycleIconSnapshot.Empty;
        }

        private string CreateEquipmentText()
        {
            CharacterItemEquipmentProjectionResult projection = CharacterItemEquipmentProjector.Project(
                _equipment,
                _equipmentSchema,
                new EquipmentLoadoutId(760101),
                "showcase.item_lifecycle.loadout",
                _definitions,
                _equipmentProfiles);
            if (!projection.IsSuccess)
                return projection.Diagnostics.Length > 0 ? "Invalid: " + projection.Status : "Invalid";

            string summary = string.Empty;
            for (int i = 0; i < _equipment.SlotCount; i++)
            {
                ItemStack stack = _equipment.GetStack(i);
                if (stack.IsEmpty)
                    continue;

                if (summary.Length > 0)
                    summary += ", ";
                summary += GetEquipmentSlotLabel(i) + ": " + GetItemName(stack.DefinitionId);
            }

            return summary.Length > 0 ? summary : "None";
        }

        private bool IsEquipmentSlotIndexValid(int slotIndex)
        {
            return slotIndex >= 0 && _equipment != null && slotIndex < _equipment.SlotCount;
        }

        private string GetEquipmentSlotLabel(int slotIndex)
        {
            if (_equipmentSchema == null || _equipmentSchema.Slots == null || slotIndex < 0 || slotIndex >= _equipmentSchema.Slots.Length)
                return "Slot " + slotIndex;

            EquipmentSlotEntry slot = _equipmentSchema.Slots[slotIndex];
            return string.IsNullOrEmpty(slot.DisplayName) ? "Slot " + slot.SlotId : slot.DisplayName;
        }

        private bool TryFindFirstEmptyBagSlot(out int slotIndex)
        {
            for (int i = 0; i < _bag.SlotCount; i++)
            {
                if (_bag.GetStack(i).IsEmpty)
                {
                    slotIndex = i;
                    return true;
                }
            }

            slotIndex = -1;
            return false;
        }

        private string GetItemName(ItemDefinitionId itemDefinitionId)
        {
            if (!_definitions.TryGet(itemDefinitionId, out ItemDefinition definition))
                return "Item " + itemDefinitionId.Value;
            if (definition.DefinitionId.Value == HerbItemId)
                return "Herb";
            if (definition.DefinitionId.Value == OreItemId)
                return "Ore";
            if (definition.DefinitionId.Value == SwordItemId)
                return "Iron Sword";
            return definition.StableId;
        }

        private void AddLog(string message)
        {
            if (_log.Count >= 24)
                _log.RemoveAt(0);
            _log.Add(message ?? string.Empty);
        }

        private void DisposeRuntime()
        {
            if (_host != null)
            {
                _host.Stop();
                _host.Dispose();
                _host = null;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ItemLifecycleShowcase));
        }

        private static ItemDefinitionRegistry CreateDefinitions()
        {
            var registry = new ItemDefinitionRegistry();
            registry.Register(CreateDefinition(HerbItemId, "showcase.item.herb", ItemKind.Material, 20));
            registry.Register(CreateDefinition(OreItemId, "showcase.item.ore", ItemKind.Material, 10));
            registry.Register(CreateDefinition(SwordItemId, "showcase.item.iron_sword", ItemKind.Equipment, 1));
            return registry;
        }

        private static ItemDefinition CreateDefinition(int id, string stableId, ItemKind kind, int maxStack)
        {
            return new ItemDefinition(
                new ItemDefinitionId(id),
                stableId,
                new LocalizedTextKey(stableId + ".name"),
                new LocalizedTextKey(stableId + ".desc"),
                kind,
                new ItemTagSet(new[] { 42 }),
                ItemStackPolicy.ForKind(kind, maxStack),
                new[] { new ItemResourceRef(new ResourceKey(stableId + ".icon", ResourceTypeIds.Sprite), ItemResourceUsageIds.Icon) },
                new[] { new ItemProfileRef(kind == ItemKind.Equipment ? ItemProfileTypeIds.CharacterEquipment : ItemProfileTypeIds.Generic, stableId + ".profile") });
        }

        private static LootTableDefinition CreateLootTable()
        {
            return new LootTableDefinition(
                new LootTableId(1),
                "showcase.loot.lifecycle_chest",
                new[]
                {
                    new LootGroupDefinition(
                        "materials",
                        1,
                        1,
                        LootContext.BasisPointsScale,
                        0,
                        new[]
                        {
                            new LootEntryDefinition(new ItemDefinitionId(HerbItemId), 60, 4, 6),
                            new LootEntryDefinition(new ItemDefinitionId(OreItemId), 40, 2, 4)
                        }),
                    new LootGroupDefinition(
                        "weapon",
                        1,
                        1,
                        LootContext.BasisPointsScale,
                        0,
                        new[] { new LootEntryDefinition(new ItemDefinitionId(SwordItemId), 1, 1, 1, requiredContextTags: new ItemTagSet(new[] { 42 })) })
                });
        }

        private static CharacterItemEquipmentProfileRegistry CreateEquipmentProfiles()
        {
            var registry = new CharacterItemEquipmentProfileRegistry();
            registry.Register(new CharacterItemEquipmentProfileConfig(
                new ItemDefinitionId(SwordItemId),
                new WeaponConfigId(780101),
                "showcase.weapon.iron_sword",
                WeaponCategory.OneHandMelee,
                new[] { CharacterWeaponTagIds.Blade, CharacterWeaponTagIds.OneHand },
                Array.Empty<int>(),
                new AbilityLoadoutId(0),
                "presentation.weapon.iron_sword",
                "combat.weapon.iron_sword",
                "resource.weapon.iron_sword",
                new[] { new CharacterResourceKeyEntry("showcase.weapon.iron_sword.prefab", ResourceTypeIds.GameObject, CharacterResourceUsageKind.WeaponModel) },
                new[]
                {
                    new CharacterTraceBindingEntry("showcase.trace.iron_sword.main", CharacterEquipmentSlotIds.MainHand, SampleSocketIds.MainHand, 1.2f, 0.1f),
                    new CharacterTraceBindingEntry("showcase.trace.iron_sword.off", CharacterEquipmentSlotIds.OffHand, SampleSocketIds.OffHand, 1.2f, 0.1f)
                },
                Array.Empty<CharacterModifierGrantEntry>()));
            return registry;
        }

        private static EquipmentSchemaConfig CreateEquipmentSchema()
        {
            return new EquipmentSchemaConfig(
                new EquipmentSchemaId(750101),
                "showcase.equipment_schema.hands",
                new[]
                {
                    new EquipmentSlotEntry(CharacterEquipmentSlotIds.MainHand, EquipmentSlotKind.MainHand, "Main Hand", new[] { WeaponCategory.OneHandMelee.ToString() }, new[] { CharacterWeaponTagIds.Blade }),
                    new EquipmentSlotEntry(CharacterEquipmentSlotIds.OffHand, EquipmentSlotKind.OffHand, "Off Hand", new[] { WeaponCategory.OneHandMelee.ToString() }, new[] { CharacterWeaponTagIds.Blade })
                },
                Array.Empty<EquipmentExclusiveGroupEntry>(),
                new[] { new EquipmentStateId(770101) });
        }

        private static void Append<T>(List<T> target, IReadOnlyList<T> source)
        {
            if (source == null)
                return;
            for (int i = 0; i < source.Count; i++)
                target.Add(source[i]);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private sealed class ShowcaseCommandModule : RuntimeModule
        {
            private readonly ItemLifecycleShowcase _showcase;

            public ShowcaseCommandModule(ItemLifecycleShowcase showcase)
                : base("mxframework.demo.items.lifecycle", RuntimeTickStage.Simulation, priority: 90)
            {
                _showcase = showcase;
            }

            /// <remarks>NoAlloc after initialization while the command drain list stays within configured capacity.</remarks>
            public override void Tick(RuntimeTickContext context)
            {
                _showcase.ProcessCommands(context);
            }
        }
    }

    public readonly struct ItemLifecycleSlotSnapshot
    {
        public ItemLifecycleSlotSnapshot(
            int slotIndex,
            string slotName,
            string itemName,
            int count,
            ItemLifecycleIconSnapshot icon,
            bool isSelected)
        {
            SlotIndex = slotIndex;
            SlotName = string.IsNullOrEmpty(slotName) ? "Slot " + slotIndex : slotName;
            ItemName = itemName ?? string.Empty;
            Count = count;
            Icon = icon.IsValid ? icon : ItemLifecycleIconSnapshot.Empty;
            IsSelected = isSelected;
        }

        public int SlotIndex { get; }
        public string SlotName { get; }
        public string ItemName { get; }
        public int Count { get; }
        public ItemLifecycleIconSnapshot Icon { get; }
        public bool IsSelected { get; }
        public bool IsEmpty => string.IsNullOrEmpty(ItemName) || Count <= 0;
    }

    public readonly struct ItemLifecycleWorldItemSnapshot
    {
        public ItemLifecycleWorldItemSnapshot(
            int worldItemId,
            string itemName,
            int count,
            ItemLifecycleIconSnapshot icon,
            int x,
            int z,
            int distance,
            bool inRange)
        {
            WorldItemId = worldItemId;
            ItemName = itemName ?? string.Empty;
            Count = count;
            Icon = icon.IsValid ? icon : ItemLifecycleIconSnapshot.Empty;
            X = x;
            Z = z;
            Distance = distance;
            InRange = inRange;
        }

        public int WorldItemId { get; }
        public string ItemName { get; }
        public int Count { get; }
        public ItemLifecycleIconSnapshot Icon { get; }
        public int X { get; }
        public int Z { get; }
        public int Distance { get; }
        public bool InRange { get; }
    }

    public readonly struct ItemLifecycleIconSnapshot
    {
        public static readonly ItemLifecycleIconSnapshot Empty = new ItemLifecycleIconSnapshot(string.Empty, string.Empty);

        public ItemLifecycleIconSnapshot(string resourceId, string resourceTypeId)
        {
            ResourceId = resourceId ?? string.Empty;
            ResourceTypeId = resourceTypeId ?? string.Empty;
        }

        public string ResourceId { get; }
        public string ResourceTypeId { get; }
        public bool IsValid => !string.IsNullOrEmpty(ResourceId) && !string.IsNullOrEmpty(ResourceTypeId);

        public ResourceKey ToResourceKey()
        {
            return new ResourceKey(ResourceId, ResourceTypeId);
        }
    }

    public sealed class ItemLifecycleShowcaseSnapshot
    {
        public ItemLifecycleShowcaseSnapshot(
            RuntimeFrame frame,
            int playerX,
            int playerZ,
            int worldItemCount,
            ItemLifecycleWorldItemSnapshot[] worldItems,
            ItemLifecycleSlotSnapshot[] inventorySlots,
            ItemLifecycleSlotSnapshot[] equipmentSlots,
            string equipmentProfile,
            int selectedEquipmentSlotIndex,
            string selectedEquipmentSlotName,
            long stateHash,
            string saveStatus,
            string hashStatus,
            string[] log)
        {
            Frame = frame;
            PlayerX = playerX;
            PlayerZ = playerZ;
            WorldItemCount = worldItemCount;
            WorldItems = worldItems ?? Array.Empty<ItemLifecycleWorldItemSnapshot>();
            InventorySlots = inventorySlots ?? Array.Empty<ItemLifecycleSlotSnapshot>();
            EquipmentSlots = equipmentSlots ?? Array.Empty<ItemLifecycleSlotSnapshot>();
            EquipmentProfile = equipmentProfile ?? string.Empty;
            SelectedEquipmentSlotIndex = selectedEquipmentSlotIndex;
            SelectedEquipmentSlotName = selectedEquipmentSlotName ?? string.Empty;
            StateHash = stateHash;
            SaveStatus = saveStatus ?? string.Empty;
            HashStatus = hashStatus ?? string.Empty;
            Log = log ?? Array.Empty<string>();
        }

        public RuntimeFrame Frame { get; }
        public int PlayerX { get; }
        public int PlayerZ { get; }
        public int WorldItemCount { get; }
        public ItemLifecycleWorldItemSnapshot[] WorldItems { get; }
        public ItemLifecycleSlotSnapshot[] InventorySlots { get; }
        public ItemLifecycleSlotSnapshot[] EquipmentSlots { get; }
        public string EquipmentProfile { get; }
        public int SelectedEquipmentSlotIndex { get; }
        public string SelectedEquipmentSlotName { get; }
        public long StateHash { get; }
        public string SaveStatus { get; }
        public string HashStatus { get; }
        public string[] Log { get; }
    }

    // Source-defined trace categories. Never renumber or reuse retired values.
    internal static class ItemLifecycleShowcaseTraceKinds
    {
        public const int K_item_lifecycle_move = 222;
        public const int K_item_lifecycle_open = 223;
        public const int K_item_lifecycle_pickup = 224;
        public const int K_item_lifecycle_equip = 226;
        public const int K_item_lifecycle_unequip = 227;
        public const int K_item_lifecycle_select_equipment_slot = 228;
        public const int K_item_lifecycle_spawn_world_item = 230;
        public const int K_item_lifecycle_pickup_world_item = 231;
    }
}
