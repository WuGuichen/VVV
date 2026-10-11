using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Items
{
    public readonly struct WorldItemId : IEquatable<WorldItemId>, IFormattable
    {
        public WorldItemId(int value)
        {
            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(WorldItemId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is WorldItemId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public override string ToString() { return Value.ToString(); }
        public string ToString(string format, IFormatProvider formatProvider) { return Value.ToString(format, formatProvider); }
    }

    public readonly struct WorldItemPosition : IEquatable<WorldItemPosition>
    {
        public WorldItemPosition(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public bool Equals(WorldItemPosition other) { return X == other.X && Y == other.Y && Z == other.Z; }
        public override bool Equals(object obj) { return obj is WorldItemPosition other && Equals(other); }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X;
                hash = (hash * 397) ^ Y;
                hash = (hash * 397) ^ Z;
                return hash;
            }
        }
    }

    public enum WorldItemAccessPolicy
    {
        Public = 0,
        OwnerOnly = 1
    }

    public sealed class WorldItemStack
    {
        public WorldItemStack(
            WorldItemId worldItemId,
            ItemStack stack,
            WorldItemPosition position,
            InventoryOwnerId ownerId,
            WorldItemAccessPolicy accessPolicy,
            long spawnFrame,
            long expireFrame = -1L)
        {
            if (!worldItemId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(worldItemId));
            if (stack.IsEmpty)
                throw new ArgumentOutOfRangeException(nameof(stack));
            if (spawnFrame < 0L)
                throw new ArgumentOutOfRangeException(nameof(spawnFrame));
            if (expireFrame >= 0L && expireFrame < spawnFrame)
                throw new ArgumentOutOfRangeException(nameof(expireFrame));

            WorldItemId = worldItemId;
            Stack = stack;
            Position = position;
            OwnerId = ownerId;
            AccessPolicy = accessPolicy;
            SpawnFrame = spawnFrame;
            ExpireFrame = expireFrame;
        }

        public WorldItemId WorldItemId { get; }
        public ItemStack Stack { get; }
        public WorldItemPosition Position { get; }
        public InventoryOwnerId OwnerId { get; }
        public WorldItemAccessPolicy AccessPolicy { get; }
        public long SpawnFrame { get; }
        public long ExpireFrame { get; }

        public bool IsExpired(RuntimeFrame frame)
        {
            return ExpireFrame >= 0L && frame.Value >= ExpireFrame;
        }
    }

    public sealed class WorldItemStore
    {
        private readonly Dictionary<int, WorldItemStack> _items = new Dictionary<int, WorldItemStack>();
        private readonly List<WorldItemStack> _sortedItems = new List<WorldItemStack>();

        public int Count => _items.Count;

        public bool ContainsInstance(ItemInstanceId instanceId)
        {
            if (!instanceId.IsValid) return false;
            for (int i = 0; i < _sortedItems.Count; i++)
                if (_sortedItems[i].Stack.InstanceId.Equals(instanceId)) return true;
            return false;
        }

        public bool Register(WorldItemStack item)
        {
            if (item == null || _items.ContainsKey(item.WorldItemId.Value))
                return false;
            if (ContainsInstance(item.Stack.InstanceId)) return false;

            _items.Add(item.WorldItemId.Value, item);
            _sortedItems.Add(item);
            _sortedItems.Sort(CompareItems);
            return true;
        }

        public bool TryGet(WorldItemId worldItemId, out WorldItemStack item)
        {
            return _items.TryGetValue(worldItemId.Value, out item);
        }

        public bool Remove(WorldItemId worldItemId)
        {
            if (!_items.TryGetValue(worldItemId.Value, out WorldItemStack item))
                return false;

            _items.Remove(worldItemId.Value);
            _sortedItems.Remove(item);
            return true;
        }

        public WorldItemStack GetAtSortedIndex(int index)
        {
            return _sortedItems[index];
        }

        public IReadOnlyList<WorldItemStack> CreateSnapshot()
        {
            return _sortedItems.ToArray();
        }

        public void Clear()
        {
            _items.Clear();
            _sortedItems.Clear();
        }

        private static int CompareItems(WorldItemStack left, WorldItemStack right)
        {
            return left.WorldItemId.Value.CompareTo(right.WorldItemId.Value);
        }
    }

    public enum WorldItemOperationStatus
    {
        Success = 0,
        InvalidWorldItem = 1,
        DuplicateWorldItem = 2,
        InvalidStack = 3,
        DefinitionMissing = 4,
        MissingWorldItem = 5,
        Expired = 6,
        AccessDenied = 7,
        InvalidDestination = 8,
        InventoryRejected = 9,
        StaleRequest = 10,
        DuplicateInstance = 11
    }

    public readonly struct WorldItemOperationResult
    {
        public WorldItemOperationResult(WorldItemOperationStatus status, InventoryOperationStatus inventoryStatus = InventoryOperationStatus.Success)
        {
            Status = status;
            InventoryStatus = inventoryStatus;
        }

        public WorldItemOperationStatus Status { get; }
        public InventoryOperationStatus InventoryStatus { get; }
        public bool Success => Status == WorldItemOperationStatus.Success;
        public static WorldItemOperationResult Succeeded() { return new WorldItemOperationResult(WorldItemOperationStatus.Success); }
        public static WorldItemOperationResult Failed(WorldItemOperationStatus status, InventoryOperationStatus inventoryStatus = InventoryOperationStatus.Success)
        {
            return new WorldItemOperationResult(status, inventoryStatus);
        }
    }

    /// <summary>Authority service for world item spawn, pickup, and expiration.</summary>
    /// <remarks>NoAlloc after initialization for command-time operations when caller-owned temporary lists have enough capacity.</remarks>
    public sealed class WorldItemService
    {
        private readonly WorldItemStore _store;
        private readonly InventoryService _inventory;

        public WorldItemService(WorldItemStore store, InventoryService inventory)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        public WorldItemStore Store => _store;
        public InventoryService Inventory => _inventory;

        public WorldItemOperationResult Spawn(WorldItemStack item)
        {
            if (item == null || !item.WorldItemId.IsValid)
                return WorldItemOperationResult.Failed(WorldItemOperationStatus.InvalidWorldItem);
            if (!ValidateStack(item.Stack, out WorldItemOperationStatus status))
                return WorldItemOperationResult.Failed(status);
            if (item.Stack.InstanceId.IsValid && (_inventory.Store.ContainsInstance(item.Stack.InstanceId)
                || _store.ContainsInstance(item.Stack.InstanceId)))
                return WorldItemOperationResult.Failed(WorldItemOperationStatus.DuplicateInstance);
            if (!_store.Register(item))
                return WorldItemOperationResult.Failed(WorldItemOperationStatus.DuplicateWorldItem);
            if (item.Stack.InstanceId.IsValid)
                _inventory.Store.TryRestoreInstanceCounter(Math.Max(_inventory.Store.NextItemInstanceId,
                    (long)item.Stack.InstanceId.Value + 1));
            return WorldItemOperationResult.Succeeded();
        }

        public WorldItemOperationResult Pickup(
            WorldItemId worldItemId,
            InventoryOwnerId requesterOwnerId,
            InventoryContainerId destinationContainerId,
            RuntimeFrame frame)
        {
            if (!_store.TryGet(worldItemId, out WorldItemStack item))
                return WorldItemOperationResult.Failed(WorldItemOperationStatus.MissingWorldItem);
            if (item.IsExpired(frame))
                return WorldItemOperationResult.Failed(WorldItemOperationStatus.Expired);
            if (item.AccessPolicy == WorldItemAccessPolicy.OwnerOnly && !item.OwnerId.Equals(requesterOwnerId))
                return WorldItemOperationResult.Failed(WorldItemOperationStatus.AccessDenied);
            if (!_inventory.Store.TryGet(destinationContainerId, out _))
                return WorldItemOperationResult.Failed(WorldItemOperationStatus.InvalidDestination);

            InventoryOperationResult insert = _inventory.TryInsertStack(destinationContainerId, item.Stack);
            if (!insert.Success)
                return WorldItemOperationResult.Failed(WorldItemOperationStatus.InventoryRejected, insert.Status);

            _store.Remove(worldItemId);
            return WorldItemOperationResult.Succeeded();
        }

        public WorldItemOperationResult Expire(WorldItemId worldItemId)
        {
            return _store.Remove(worldItemId)
                ? WorldItemOperationResult.Succeeded()
                : WorldItemOperationResult.Failed(WorldItemOperationStatus.MissingWorldItem);
        }

        public int ExpireDue(RuntimeFrame frame, List<WorldItemId> expiredItems)
        {
            if (expiredItems == null)
                throw new ArgumentNullException(nameof(expiredItems));

            expiredItems.Clear();
            for (int i = 0; i < _store.Count; i++)
            {
                WorldItemStack item = _store.GetAtSortedIndex(i);
                if (item.IsExpired(frame))
                    expiredItems.Add(item.WorldItemId);
            }

            for (int i = 0; i < expiredItems.Count; i++)
                _store.Remove(expiredItems[i]);
            return expiredItems.Count;
        }

        private bool ValidateStack(ItemStack stack, out WorldItemOperationStatus status)
        {
            status = WorldItemOperationStatus.Success;
            if (stack.IsEmpty)
            {
                status = WorldItemOperationStatus.InvalidStack;
                return false;
            }

            if (!_inventory.Definitions.TryGet(stack.DefinitionId, out _))
            {
                status = WorldItemOperationStatus.DefinitionMissing;
                return false;
            }

            if (stack.InstanceId.IsValid && stack.Count != 1)
            {
                status = WorldItemOperationStatus.InvalidStack;
                return false;
            }

            return true;
        }
    }

    public enum WorldItemRequestKind
    {
        SpawnWorldItem = 0,
        PickupWorldItem = 1,
        ExpireWorldItem = 2
    }

    public sealed class WorldItemRequest
    {
        public WorldItemRequest(
            WorldItemRequestKind kind,
            WorldItemId worldItemId,
            ItemStack stack = default,
            WorldItemPosition position = default,
            InventoryOwnerId ownerId = default,
            WorldItemAccessPolicy accessPolicy = WorldItemAccessPolicy.Public,
            long spawnFrame = 0L,
            long expireFrame = -1L,
            InventoryOwnerId requesterOwnerId = default,
            InventoryContainerId destinationContainerId = default)
        {
            Kind = kind;
            WorldItemId = worldItemId;
            Stack = stack;
            Position = position;
            OwnerId = ownerId;
            AccessPolicy = accessPolicy;
            SpawnFrame = spawnFrame;
            ExpireFrame = expireFrame;
            RequesterOwnerId = requesterOwnerId;
            DestinationContainerId = destinationContainerId;
        }

        public WorldItemRequestKind Kind { get; }
        public WorldItemId WorldItemId { get; }
        public ItemStack Stack { get; }
        public WorldItemPosition Position { get; }
        public InventoryOwnerId OwnerId { get; }
        public WorldItemAccessPolicy AccessPolicy { get; }
        public long SpawnFrame { get; }
        public long ExpireFrame { get; }
        public InventoryOwnerId RequesterOwnerId { get; }
        public InventoryContainerId DestinationContainerId { get; }
    }

    public readonly struct WorldItemRequestHandle
    {
        public WorldItemRequestHandle(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index > 0 && Generation > 0;
    }

    /// <remarks>Allocation: request creation and dictionary growth are LowFreqAlloc.</remarks>
    public sealed class WorldItemRequestStore
    {
        private readonly Dictionary<int, WorldItemRequest> _requests = new Dictionary<int, WorldItemRequest>();
        private readonly Dictionary<int, int> _generations = new Dictionary<int, int>();
        private readonly Queue<int> _freeIndices = new Queue<int>();
        private int _nextIndex = 1;

        public int Count => _requests.Count;

        public WorldItemRequestHandle Add(WorldItemRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            int index = _freeIndices.Count > 0 ? _freeIndices.Dequeue() : _nextIndex++;
            int generation = _generations.TryGetValue(index, out int currentGeneration) ? currentGeneration + 1 : 1;
            _generations[index] = generation;
            _requests[index] = request;
            return new WorldItemRequestHandle(index, generation);
        }

        public bool TryGet(WorldItemRequestHandle handle, out WorldItemRequest request)
        {
            if (!handle.IsValid
                || !_generations.TryGetValue(handle.Index, out int generation)
                || generation != handle.Generation
                || !_requests.TryGetValue(handle.Index, out request))
            {
                request = null;
                return false;
            }

            return true;
        }

        public bool Remove(WorldItemRequestHandle handle)
        {
            if (!handle.IsValid
                || !_generations.TryGetValue(handle.Index, out int generation)
                || generation != handle.Generation
                || !_requests.Remove(handle.Index))
                return false;

            _freeIndices.Enqueue(handle.Index);
            return true;
        }

        public void Clear()
        {
            _requests.Clear();
            _freeIndices.Clear();
        }
    }

    public static class WorldItemRuntimeCommandIds
    {
        public const int SpawnWorldItem = 1003101;
        public const int PickupWorldItem = 1003102;
        public const int ExpireWorldItem = 1003103;
    }

    public static class WorldItemRuntimeCommandFactory
    {
        public static RuntimeCommand Create(RuntimeFrame frame, int sourceId, int commandId, WorldItemRequestHandle handle, RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(frame, sourceId, commandId, handle.Index, handle.Generation, 0, 0, traceId);
        }
    }

    public enum WorldItemRuntimeEventType
    {
        OperationSucceeded = 0,
        OperationRejected = 1,
        ItemExpired = 2
    }

    public readonly struct WorldItemRuntimeEvent
    {
        public WorldItemRuntimeEvent(
            WorldItemRuntimeEventType eventType,
            RuntimeCommand command,
            WorldItemRequestKind requestKind,
            WorldItemId worldItemId,
            WorldItemOperationStatus status,
            InventoryOperationStatus inventoryStatus)
        {
            EventType = eventType;
            Command = command;
            RequestKind = requestKind;
            WorldItemId = worldItemId;
            Status = status;
            InventoryStatus = inventoryStatus;
        }

        public WorldItemRuntimeEventType EventType { get; }
        public RuntimeCommand Command { get; }
        public WorldItemRequestKind RequestKind { get; }
        public WorldItemId WorldItemId { get; }
        public WorldItemOperationStatus Status { get; }
        public InventoryOperationStatus InventoryStatus { get; }
    }

    public sealed class WorldItemRuntimeModule : RuntimeModule
    {
        public const string DefaultModuleId = ItemRuntimeIds.WorldItemsModule;

        private readonly WorldItemService _service;
        private readonly WorldItemRequestStore _requests;
        private readonly RuntimeCommandBuffer _commands;
        private readonly RuntimeEventQueue<WorldItemRuntimeEvent> _events;
        private readonly List<RuntimeCommand> _drainedCommands;
        private readonly List<WorldItemId> _expiredItems;

        public WorldItemRuntimeModule(
            WorldItemService service,
            WorldItemRequestStore requests = null,
            RuntimeCommandBuffer commands = null,
            RuntimeEventQueue<WorldItemRuntimeEvent> events = null,
            string moduleId = DefaultModuleId,
            int priority = 125,
            int commandCapacity = 16)
            : base(moduleId, RuntimeTickStage.Simulation, priority)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _requests = requests ?? new WorldItemRequestStore();
            _commands = commands ?? new RuntimeCommandBuffer(commandCapacity);
            _events = events ?? new RuntimeEventQueue<WorldItemRuntimeEvent>(commandCapacity);
            _drainedCommands = new List<RuntimeCommand>(commandCapacity);
            _expiredItems = new List<WorldItemId>(commandCapacity);
        }

        public WorldItemService Service => _service;
        public WorldItemRequestStore Requests => _requests;
        public RuntimeCommandBuffer Commands => _commands;
        public RuntimeEventQueue<WorldItemRuntimeEvent> Events => _events;

        /// <remarks>NoAlloc after initialization while command/event buffers stay within configured capacities.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            RuntimeFrame frame = new RuntimeFrame(context.FrameIndex);
            _drainedCommands.Clear();
            _commands.DrainForFrame(frame, _drainedCommands);
            for (int i = 0; i < _drainedCommands.Count; i++)
                Execute(context, _drainedCommands[i]);
            _drainedCommands.Clear();

            int expiredCount = _service.ExpireDue(frame, _expiredItems);
            for (int i = 0; i < expiredCount; i++)
            {
                _events.Enqueue(
                    frame,
                    new WorldItemRuntimeEvent(
                        WorldItemRuntimeEventType.ItemExpired,
                        default,
                        WorldItemRequestKind.ExpireWorldItem,
                        _expiredItems[i],
                        WorldItemOperationStatus.Success,
                        InventoryOperationStatus.Success));
            }

            _expiredItems.Clear();
        }

        public int DrainEvents(RuntimeFrame frame, List<WorldItemRuntimeEvent> output)
        {
            return _events.Drain(frame, output);
        }

        private void Execute(RuntimeTickContext context, RuntimeCommand command)
        {
            WorldItemRequestHandle handle = new WorldItemRequestHandle(command.TargetId, command.Payload0);
            if (!_requests.TryGet(handle, out WorldItemRequest request))
            {
                Enqueue(context, command, WorldItemRequestKind.SpawnWorldItem, default, WorldItemOperationResult.Failed(WorldItemOperationStatus.StaleRequest));
                return;
            }

            _requests.Remove(handle);
            WorldItemOperationResult result = ExecuteRequest(command, request);
            Enqueue(context, command, request.Kind, request.WorldItemId, result);
        }

        private WorldItemOperationResult ExecuteRequest(RuntimeCommand command, WorldItemRequest request)
        {
            if (!MatchesCommand(command.CommandId, request.Kind))
                return WorldItemOperationResult.Failed(WorldItemOperationStatus.StaleRequest);

            switch (request.Kind)
            {
                case WorldItemRequestKind.SpawnWorldItem:
                    if (!request.WorldItemId.IsValid || request.SpawnFrame < 0L || (request.ExpireFrame >= 0L && request.ExpireFrame < request.SpawnFrame))
                        return WorldItemOperationResult.Failed(WorldItemOperationStatus.InvalidWorldItem);
                    if (request.Stack.IsEmpty)
                        return WorldItemOperationResult.Failed(WorldItemOperationStatus.InvalidStack);
                    return _service.Spawn(new WorldItemStack(
                        request.WorldItemId,
                        request.Stack,
                        request.Position,
                        request.OwnerId,
                        request.AccessPolicy,
                        request.SpawnFrame,
                        request.ExpireFrame));
                case WorldItemRequestKind.PickupWorldItem:
                    return _service.Pickup(
                        request.WorldItemId,
                        request.RequesterOwnerId,
                        request.DestinationContainerId,
                        new RuntimeFrame(command.Frame.Value));
                case WorldItemRequestKind.ExpireWorldItem:
                    return _service.Expire(request.WorldItemId);
                default:
                    return WorldItemOperationResult.Failed(WorldItemOperationStatus.StaleRequest);
            }
        }

        private void Enqueue(
            RuntimeTickContext context,
            RuntimeCommand command,
            WorldItemRequestKind requestKind,
            WorldItemId worldItemId,
            WorldItemOperationResult result)
        {
            WorldItemRuntimeEventType eventType = result.Success
                ? WorldItemRuntimeEventType.OperationSucceeded
                : WorldItemRuntimeEventType.OperationRejected;
            _events.Enqueue(
                new RuntimeFrame(context.FrameIndex),
                new WorldItemRuntimeEvent(eventType, command, requestKind, worldItemId, result.Status, result.InventoryStatus));
        }

        private static bool MatchesCommand(int commandId, WorldItemRequestKind kind)
        {
            switch (kind)
            {
                case WorldItemRequestKind.SpawnWorldItem:
                    return commandId == WorldItemRuntimeCommandIds.SpawnWorldItem;
                case WorldItemRequestKind.PickupWorldItem:
                    return commandId == WorldItemRuntimeCommandIds.PickupWorldItem;
                case WorldItemRequestKind.ExpireWorldItem:
                    return commandId == WorldItemRuntimeCommandIds.ExpireWorldItem;
                default:
                    return false;
            }
        }
    }

    /// <summary>Deterministic world item hash contributor.</summary>
    /// <remarks>NoAlloc after initialization.</remarks>
    public sealed class WorldItemHashContributor : IRuntimeHashContributor
    {
        private const int F_items_world_count = 1;
        private const int F_items_world_index = 2;
        private const int F_items_world_id = 3;
        private const int F_items_world_item = 4;
        private const int F_items_world_count_stack = 5;
        private const int F_items_world_instance = 6;
        private const int F_items_world_custom_type = 7;
        private const int F_items_world_custom_schema = 8;
        private const int F_items_world_custom_payload = 9;
        private const int F_items_world_x = 10;
        private const int F_items_world_y = 11;
        private const int F_items_world_z = 12;
        private const int F_items_world_owner = 13;
        private const int F_items_world_access = 14;
        private const int F_items_world_spawnFrame = 15;
        private const int F_items_world_expireFrame = 16;

        private readonly WorldItemStore _store;

        public WorldItemHashContributor(WorldItemStore store, int contributorId = RuntimeHashContributorIds.ItemWorld)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            if (contributorId <= 0) throw new ArgumentOutOfRangeException(nameof(contributorId));
            ContributorId = contributorId;
        }

        public int HashSchemaId => RuntimeHashSchemaIds.ItemWorld;
        public int HashSchemaVersion => 1;
        public int ContributorId { get; }

        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));

            accumulator.BeginArray(F_items_world_count, _store.Count);
            for (int i = 0; i < _store.Count; i++)
            {
                accumulator.BeginElement(i);
                WorldItemStack item = _store.GetAtSortedIndex(i);
                accumulator.AddInt(F_items_world_index, i);
                accumulator.AddInt(F_items_world_id, item.WorldItemId.Value);
                accumulator.AddInt(F_items_world_item, item.Stack.DefinitionId.Value);
                accumulator.AddInt(F_items_world_count_stack, item.Stack.Count);
                accumulator.AddInt(F_items_world_instance, item.Stack.InstanceId.Value);
                accumulator.AddInt(F_items_world_custom_type, item.Stack.CustomState == null ? 0 : item.Stack.CustomState.NumericTypeId);
                accumulator.AddInt(F_items_world_custom_schema, item.Stack.CustomState == null ? 0 : item.Stack.CustomState.SchemaVersion);
                accumulator.AddDigest(F_items_world_custom_payload, item.Stack.CustomState == null ? default : item.Stack.CustomState.PreparedPayloadDigest);
                accumulator.AddInt(F_items_world_x, item.Position.X);
                accumulator.AddInt(F_items_world_y, item.Position.Y);
                accumulator.AddInt(F_items_world_z, item.Position.Z);
                accumulator.AddInt(F_items_world_owner, item.OwnerId.Value);
                accumulator.AddInt(F_items_world_access, (int)item.AccessPolicy);
                accumulator.AddLong(F_items_world_spawnFrame, item.SpawnFrame);
                accumulator.AddLong(F_items_world_expireFrame, item.ExpireFrame);
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }
    }

    /// <summary>World item Runtime SaveState adapter.</summary>
    /// <remarks>Allocation: CaptureSaveState and RestoreSaveState are AllocByDesign because they build DTO arrays and JSON payloads.</remarks>
    public sealed class WorldItemSaveStateProvider :
        IRuntimeSaveStateProvider,
        IRuntimeSaveStateRestorer
    {
        public const string ModuleId = ItemRuntimeIds.WorldItemsModule;
        public const int CustomStateSchemaVersion = WorldItemSaveState.CurrentSchemaVersion;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include
        };

        private readonly WorldItemStore _store;
        private readonly ItemDefinitionRegistry _definitions;
        private readonly Func<long> _frameProvider;

        public WorldItemSaveStateProvider(WorldItemStore store, ItemDefinitionRegistry definitions, Func<long> frameProvider = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _frameProvider = frameProvider;
        }

        public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
        {
            WorldItemSaveState state = CaptureWorldState();
            string payloadJson = JsonConvert.SerializeObject(state, JsonSettings);
            var moduleState = new RuntimeModuleSaveState(
                ModuleId,
                CustomStateSchemaVersion,
                new RuntimeCustomState(3301, ModuleId, CustomStateSchemaVersion, payloadJson));
            var saveState = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                string.Empty,
                string.Empty,
                string.Empty,
                _frameProvider != null ? _frameProvider() : 0L,
                null,
                null,
                new[] { moduleState });
            return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(saveState);
        }

        public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState)
        {
            RuntimeSaveStateResult<WorldItemSaveState> loadResult = LoadWorldState(saveState);
            if (!loadResult.Success)
                return RuntimeSaveStateResult<bool>.Failed(loadResult.Error);

            WorldItemSaveState state = loadResult.Value;
            if (state.DefinitionHash != _definitions.ComputeStableHash())
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.CustomStateMismatch,
                    "$.moduleStates['" + ModuleId + "'].customState.definitionHash",
                    "World item definition hash mismatch."));
            }

            WorldItemStackSaveState[] items = state.Items ?? Array.Empty<WorldItemStackSaveState>();
            var restoredItems = new List<WorldItemStack>(items.Length);
            var seenWorldItemIds = new HashSet<int>();
            var seenInstanceIds = new HashSet<int>();
            for (int i = 0; i < items.Length; i++)
            {
                WorldItemStackSaveState source = items[i];
                RuntimeSaveStateResult<WorldItemStack> restoreItem = RestoreWorldItem(source, i, seenWorldItemIds, seenInstanceIds);
                if (!restoreItem.Success)
                    return RuntimeSaveStateResult<bool>.Failed(restoreItem.Error);

                restoredItems.Add(restoreItem.Value);
            }

            _store.Clear();
            for (int i = 0; i < restoredItems.Count; i++)
                _store.Register(restoredItems[i]);

            return RuntimeSaveStateResult<bool>.Succeeded(true);
        }

        private RuntimeSaveStateResult<WorldItemStack> RestoreWorldItem(
            WorldItemStackSaveState source,
            int index,
            HashSet<int> seenWorldItemIds,
            HashSet<int> seenInstanceIds)
        {
            string path = "$.moduleStates['" + ModuleId + "'].customState.payloadJson.items[" + index + "]";
            if (source == null)
                return FailedItem(path, "World item entry is null.");
            if (source.WorldItemId <= 0)
                return FailedItem(path + ".worldItemId", "World item id must be positive.");
            if (!seenWorldItemIds.Add(source.WorldItemId))
                return FailedItem(path + ".worldItemId", "Duplicate world item id.");
            if (source.ItemDefinitionId <= 0)
                return FailedItem(path + ".itemDefinitionId", "Item definition id must be positive.");
            if (!_definitions.TryGet(new ItemDefinitionId(source.ItemDefinitionId), out _))
                return FailedItem(path + ".itemDefinitionId", "Item definition is missing.");
            if (source.Count <= 0)
                return FailedItem(path + ".count", "Stack count must be positive.");
            if (source.ItemInstanceId < 0)
                return FailedItem(path + ".itemInstanceId", "Item instance id must be non-negative.");
            if (source.ItemInstanceId > 0 && !seenInstanceIds.Add(source.ItemInstanceId))
                return FailedItem(path + ".itemInstanceId", "Duplicate item instance id.");
            if (source.ItemInstanceId > 0 && source.Count != 1)
                return FailedItem(path + ".itemInstanceId", "Instance-backed world item stacks must have count 1.");
            if (source.AccessPolicy != (int)WorldItemAccessPolicy.Public && source.AccessPolicy != (int)WorldItemAccessPolicy.OwnerOnly)
                return FailedItem(path + ".accessPolicy", "Unsupported world item access policy.");
            if (source.SpawnFrame < 0L)
                return FailedItem(path + ".spawnFrame", "Spawn frame must be non-negative.");
            if (source.ExpireFrame >= 0L && source.ExpireFrame < source.SpawnFrame)
                return FailedItem(path + ".expireFrame", "Expire frame cannot be earlier than spawn frame.");

            RuntimeCustomState customState = string.IsNullOrEmpty(source.CustomStateTypeId)
                ? null
                : new RuntimeCustomState(source.CustomStateNumericTypeId, source.CustomStateTypeId, source.CustomStateSchemaVersion, source.CustomStatePayloadJson);
            var stack = new ItemStack(
                new ItemDefinitionId(source.ItemDefinitionId),
                source.Count,
                new ItemInstanceId(source.ItemInstanceId),
                customState);
            return RuntimeSaveStateResult<WorldItemStack>.Succeeded(new WorldItemStack(
                new WorldItemId(source.WorldItemId),
                stack,
                new WorldItemPosition(source.X, source.Y, source.Z),
                new InventoryOwnerId(source.OwnerId),
                (WorldItemAccessPolicy)source.AccessPolicy,
                source.SpawnFrame,
                source.ExpireFrame));
        }

        private WorldItemSaveState CaptureWorldState()
        {
            var items = new WorldItemStackSaveState[_store.Count];
            for (int i = 0; i < _store.Count; i++)
            {
                WorldItemStack item = _store.GetAtSortedIndex(i);
                items[i] = new WorldItemStackSaveState
                {
                    WorldItemId = item.WorldItemId.Value,
                    ItemDefinitionId = item.Stack.DefinitionId.Value,
                    Count = item.Stack.Count,
                    ItemInstanceId = item.Stack.InstanceId.Value,
                    CustomStateNumericTypeId = item.Stack.CustomState == null ? 0 : item.Stack.CustomState.NumericTypeId,
                    CustomStateTypeId = item.Stack.CustomState == null ? string.Empty : item.Stack.CustomState.TypeId,
                    CustomStateSchemaVersion = item.Stack.CustomState == null ? 0 : item.Stack.CustomState.SchemaVersion,
                    CustomStatePayloadJson = item.Stack.CustomState == null ? string.Empty : item.Stack.CustomState.PayloadJson,
                    X = item.Position.X,
                    Y = item.Position.Y,
                    Z = item.Position.Z,
                    OwnerId = item.OwnerId.Value,
                    AccessPolicy = (int)item.AccessPolicy,
                    SpawnFrame = item.SpawnFrame,
                    ExpireFrame = item.ExpireFrame
                };
            }

            return new WorldItemSaveState
            {
                SchemaVersion = WorldItemSaveState.CurrentSchemaVersion,
                DefinitionHash = _definitions.ComputeStableHash(),
                Items = items
            };
        }

        private static RuntimeSaveStateResult<WorldItemSaveState> LoadWorldState(RuntimeSaveState saveState)
        {
            if (saveState == null)
                return FailedState("$", "Save state is null.");

            RuntimeModuleSaveState module = null;
            for (int i = 0; i < saveState.ModuleStates.Count; i++)
            {
                RuntimeModuleSaveState candidate = saveState.ModuleStates[i];
                if (candidate != null && string.Equals(candidate.ModuleId, ModuleId, StringComparison.Ordinal))
                {
                    module = candidate;
                    break;
                }
            }

            if (module == null || module.CustomState == null)
                return FailedState("$.moduleStates['" + ModuleId + "']", "World item module state is missing.");
            if (module.SchemaVersion != CustomStateSchemaVersion || module.CustomState.SchemaVersion != CustomStateSchemaVersion)
                return RuntimeSaveStateResult<WorldItemSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.UnsupportedVersion,
                    "$.moduleStates['" + ModuleId + "'].schemaVersion",
                    "Unsupported World Item schema.",
                    module.SchemaVersion,
                    CustomStateSchemaVersion));
            if (!string.Equals(module.CustomState.TypeId, ModuleId, StringComparison.Ordinal))
                return FailedState("$.moduleStates['" + ModuleId + "'].customState.typeId", "World item custom state type mismatch.");

            try
            {
                WorldItemSaveState state = JsonConvert.DeserializeObject<WorldItemSaveState>(module.CustomState.PayloadJson, JsonSettings);
                if (state == null)
                    return FailedState("$.moduleStates['" + ModuleId + "'].customState.payloadJson", "World item payload is empty.");
                if (state.SchemaVersion != WorldItemSaveState.CurrentSchemaVersion)
                {
                    return RuntimeSaveStateResult<WorldItemSaveState>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.UnsupportedVersion,
                        "$.moduleStates['" + ModuleId + "'].customState.payloadJson.schemaVersion",
                        "Unsupported World Item payload schema.",
                        state.SchemaVersion,
                        WorldItemSaveState.CurrentSchemaVersion));
                }

                return RuntimeSaveStateResult<WorldItemSaveState>.Succeeded(state);
            }
            catch (Exception ex)
            {
                return RuntimeSaveStateResult<WorldItemSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates['" + ModuleId + "'].customState.payloadJson",
                    "World item payload could not be parsed: " + ex.Message,
                    exception: ex));
            }
        }

        private static RuntimeSaveStateResult<WorldItemSaveState> FailedState(string path, string message)
        {
            return RuntimeSaveStateResult<WorldItemSaveState>.Failed(new RuntimeSaveStateError(RuntimeSaveStateErrorCode.InvalidDocument, path, message));
        }

        private static RuntimeSaveStateResult<WorldItemStack> FailedItem(string path, string message)
        {
            return RuntimeSaveStateResult<WorldItemStack>.Failed(new RuntimeSaveStateError(RuntimeSaveStateErrorCode.InvalidDocument, path, message));
        }
    }

    public sealed class WorldItemSaveState
    {
        public const int CurrentSchemaVersion = 2;
        public int SchemaVersion { get; set; }
        public long DefinitionHash { get; set; }
        public WorldItemStackSaveState[] Items { get; set; }
    }

    public sealed class WorldItemStackSaveState
    {
        public int WorldItemId { get; set; }
        public int ItemDefinitionId { get; set; }
        public int Count { get; set; }
        public int ItemInstanceId { get; set; }
        public int CustomStateNumericTypeId { get; set; }
        public string CustomStateTypeId { get; set; }
        public int CustomStateSchemaVersion { get; set; }
        public string CustomStatePayloadJson { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int OwnerId { get; set; }
        public int AccessPolicy { get; set; }
        public long SpawnFrame { get; set; }
        public long ExpireFrame { get; set; }
    }
}
