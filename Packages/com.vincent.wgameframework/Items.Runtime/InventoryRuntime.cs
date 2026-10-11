using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Items
{
    public readonly struct ItemInstanceId : IEquatable<ItemInstanceId>, IFormattable
    {
        public ItemInstanceId(int value)
        {
            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(ItemInstanceId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is ItemInstanceId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public override string ToString() { return Value.ToString(); }
        public string ToString(string format, IFormatProvider formatProvider) { return Value.ToString(format, formatProvider); }
    }

    public readonly struct InventoryContainerId : IEquatable<InventoryContainerId>, IFormattable
    {
        public InventoryContainerId(int value)
        {
            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(InventoryContainerId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is InventoryContainerId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public override string ToString() { return Value.ToString(); }
        public string ToString(string format, IFormatProvider formatProvider) { return Value.ToString(format, formatProvider); }
    }

    /// <summary>Numeric owner instance identity supplied by the owning simulation.</summary>
    public readonly struct InventoryOwnerId : IEquatable<InventoryOwnerId>
    {
        public InventoryOwnerId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(InventoryOwnerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is InventoryOwnerId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public readonly struct ItemStack : IEquatable<ItemStack>
    {
        public ItemStack(ItemDefinitionId definitionId, int count, ItemInstanceId instanceId = default, RuntimeCustomState customState = null)
        {
            if (!definitionId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(definitionId), "Item stack definition id must be positive.");
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Item stack count must be positive.");
            if (instanceId.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(instanceId), "Item instance id cannot be negative.");

            DefinitionId = definitionId;
            Count = count;
            InstanceId = instanceId;
            CustomState = customState?.PreparePayloadDigest();
        }

        public ItemDefinitionId DefinitionId { get; }
        public int Count { get; }
        public ItemInstanceId InstanceId { get; }
        public RuntimeCustomState CustomState { get; }
        public bool IsEmpty => !DefinitionId.IsValid || Count <= 0;
        public static ItemStack Empty => default;

        public ItemStack WithCount(int count)
        {
            return new ItemStack(DefinitionId, count, InstanceId, CustomState);
        }

        internal bool CanMergeWith(ItemStack other)
        {
            return DefinitionId.Equals(other.DefinitionId)
                && InstanceId.Equals(other.InstanceId)
                && !InstanceId.IsValid
                && SameCustomState(CustomState, other.CustomState);
        }

        public bool Equals(ItemStack other)
        {
            return DefinitionId.Equals(other.DefinitionId)
                && Count == other.Count
                && InstanceId.Equals(other.InstanceId)
                && SameCustomState(CustomState, other.CustomState);
        }

        public override bool Equals(object obj)
        {
            return obj is ItemStack other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = DefinitionId.GetHashCode();
                hash = (hash * 397) ^ Count;
                hash = (hash * 397) ^ InstanceId.GetHashCode();
                return hash;
            }
        }

        private static bool SameCustomState(RuntimeCustomState left, RuntimeCustomState right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;
            return left.NumericTypeId == right.NumericTypeId
                && left.SchemaVersion == right.SchemaVersion
                && string.Equals(left.PayloadJson, right.PayloadJson, StringComparison.Ordinal);
        }
    }

    public readonly struct InventorySlot
    {
        public InventorySlot(int index, ItemStack stack)
        {
            Index = index;
            Stack = stack;
        }

        public int Index { get; }
        public ItemStack Stack { get; }
        public bool IsEmpty => Stack.IsEmpty;
    }

    public sealed class InventoryContainer
    {
        private readonly ItemStack[] _slots;

        public InventoryContainer(InventoryContainerId containerId, InventoryOwnerId ownerId, int slotCount, string stableId = "")
        {
            if (!containerId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(containerId), "Inventory container id must be positive.");
            if (slotCount < 0)
                throw new ArgumentOutOfRangeException(nameof(slotCount));

            ContainerId = containerId;
            OwnerId = ownerId;
            StableId = stableId ?? string.Empty;
            StableLabelDigest = RuntimeHashDigest.ComputeContentDigest(StableId);
            _slots = new ItemStack[slotCount];
        }

        public InventoryContainerId ContainerId { get; }
        public InventoryOwnerId OwnerId { get; }
        public string StableId { get; }
        public RuntimeHashDigest StableLabelDigest { get; }
        public int SlotCount => _slots.Length;

        public bool TryGetSlot(int slotIndex, out InventorySlot slot)
        {
            if (!IsValidSlot(slotIndex))
            {
                slot = default;
                return false;
            }

            slot = new InventorySlot(slotIndex, _slots[slotIndex]);
            return true;
        }

        public ItemStack GetStack(int slotIndex)
        {
            if (!IsValidSlot(slotIndex))
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            return _slots[slotIndex];
        }

        internal bool IsValidSlot(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < _slots.Length;
        }

        internal void SetStack(int slotIndex, ItemStack stack)
        {
            _slots[slotIndex] = stack;
        }

        internal void ClearSlot(int slotIndex)
        {
            _slots[slotIndex] = ItemStack.Empty;
        }

        internal InventorySlot[] CreateSlotSnapshot()
        {
            var slots = new InventorySlot[_slots.Length];
            for (int i = 0; i < _slots.Length; i++)
                slots[i] = new InventorySlot(i, _slots[i]);
            return slots;
        }
    }

    public sealed class InventoryStore
    {
        private readonly Dictionary<int, InventoryContainer> _containers = new Dictionary<int, InventoryContainer>();
        private readonly List<InventoryContainer> _sortedContainers = new List<InventoryContainer>();
        private long _nextItemInstanceId = 1;

        public int Count => _containers.Count;
        public long NextItemInstanceId => _nextItemInstanceId;

        /// <summary>Allocates one monotonic identity on the owning simulation's command boundary.</summary>
        /// <remarks>NoAlloc. Persist the counter with inventory; no wall clock or random source is used.</remarks>
        public bool TryAllocateInstanceId(out ItemInstanceId instanceId)
        {
            if (_nextItemInstanceId > int.MaxValue)
            {
                instanceId = default;
                return false;
            }
            instanceId = new ItemInstanceId((int)_nextItemInstanceId++);
            return true;
        }

        public bool ContainsInstance(ItemInstanceId instanceId)
        {
            if (!instanceId.IsValid) return false;
            for (int i = 0; i < _sortedContainers.Count; i++)
                for (int slot = 0; slot < _sortedContainers[i].SlotCount; slot++)
                    if (_sortedContainers[i].GetStack(slot).InstanceId.Equals(instanceId)) return true;
            return false;
        }

        /// <summary>Restores a validated saved counter after rebuilding a detached candidate store.</summary>
        public bool TryRestoreInstanceCounter(long nextItemInstanceId)
        {
            if (nextItemInstanceId < _nextItemInstanceId || nextItemInstanceId > (long)int.MaxValue + 1)
                return false;
            _nextItemInstanceId = nextItemInstanceId;
            return true;
        }

        internal void ObserveInstance(ItemInstanceId instanceId)
        {
            if (instanceId.IsValid && instanceId.Value >= _nextItemInstanceId)
                _nextItemInstanceId = (long)instanceId.Value + 1;
        }

        public bool Register(InventoryContainer container)
        {
            if (container == null || _containers.ContainsKey(container.ContainerId.Value))
                return false;

            for (int slot = 0; slot < container.SlotCount; slot++)
            {
                ItemInstanceId id = container.GetStack(slot).InstanceId;
                if (!id.IsValid) continue;
                if (ContainsInstance(id)) return false;
                for (int previous = 0; previous < slot; previous++)
                    if (container.GetStack(previous).InstanceId.Equals(id)) return false;
            }

            _containers.Add(container.ContainerId.Value, container);
            _sortedContainers.Add(container);
            _sortedContainers.Sort(CompareContainers);
            for (int slot = 0; slot < container.SlotCount; slot++)
                ObserveInstance(container.GetStack(slot).InstanceId);
            return true;
        }

        public bool TryGet(InventoryContainerId containerId, out InventoryContainer container)
        {
            return _containers.TryGetValue(containerId.Value, out container);
        }

        public IReadOnlyList<InventoryContainer> CreateContainerSnapshot()
        {
            return _sortedContainers.ToArray();
        }

        public InventoryContainer GetContainerAtSortedIndex(int index)
        {
            return _sortedContainers[index];
        }

        public void Clear()
        {
            _containers.Clear();
            _sortedContainers.Clear();
            _nextItemInstanceId = 1;
        }

        private static int CompareContainers(InventoryContainer left, InventoryContainer right)
        {
            return left.ContainerId.Value.CompareTo(right.ContainerId.Value);
        }
    }

    public enum InventoryOperationStatus
    {
        Success = 0,
        InvalidContainer = 1,
        InvalidSlot = 2,
        InvalidItem = 3,
        DefinitionMissing = 4,
        InvalidCount = 5,
        SlotEmpty = 6,
        SlotOccupied = 7,
        ItemMismatch = 8,
        StackOverflow = 9,
        InstanceItemNotStackable = 10,
        StaleRequest = 11,
        UnsupportedUse = 12,
        InsufficientCapacity = 13,
        DuplicateInstance = 14
    }

    public readonly struct InventoryOperationResult
    {
        public InventoryOperationResult(InventoryOperationStatus status)
        {
            Status = status;
        }

        public InventoryOperationStatus Status { get; }
        public bool Success => Status == InventoryOperationStatus.Success;
        public static InventoryOperationResult Succeeded() { return new InventoryOperationResult(InventoryOperationStatus.Success); }
        public static InventoryOperationResult Failed(InventoryOperationStatus status) { return new InventoryOperationResult(status); }
    }

    /// <summary>Slot-based inventory mutation service.</summary>
    /// <remarks>Allocation: public operations are NoAlloc after initialization when definitions and containers are pre-registered.</remarks>
    public sealed class InventoryService
    {
        private readonly ItemDefinitionRegistry _definitions;
        private readonly InventoryStore _store;

        public InventoryService(ItemDefinitionRegistry definitions, InventoryStore store)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public ItemDefinitionRegistry Definitions => _definitions;
        public InventoryStore Store => _store;

        public InventoryOperationResult AddStack(InventoryContainerId containerId, int slotIndex, ItemStack stack)
        {
            if (!_store.TryGet(containerId, out InventoryContainer container))
                return InventoryOperationResult.Failed(InventoryOperationStatus.InvalidContainer);
            if (!container.IsValidSlot(slotIndex))
                return InventoryOperationResult.Failed(InventoryOperationStatus.InvalidSlot);
            if (!ValidateNewStack(stack, out ItemDefinition definition, out InventoryOperationStatus status))
                return InventoryOperationResult.Failed(status);

            ItemStack current = container.GetStack(slotIndex);
            if (current.IsEmpty)
            {
                if (stack.Count > definition.MaxStackCount)
                    return InventoryOperationResult.Failed(InventoryOperationStatus.StackOverflow);
                container.SetStack(slotIndex, stack);
                _store.ObserveInstance(stack.InstanceId);
                return InventoryOperationResult.Succeeded();
            }

            if (!CanMerge(current, stack))
                return InventoryOperationResult.Failed(InventoryOperationStatus.ItemMismatch);
            if (stack.Count > definition.MaxStackCount - current.Count)
                return InventoryOperationResult.Failed(InventoryOperationStatus.StackOverflow);

            container.SetStack(slotIndex, current.WithCount(current.Count + stack.Count));
            return InventoryOperationResult.Succeeded();
        }

        public InventoryOperationResult RemoveStack(InventoryContainerId containerId, int slotIndex, int count)
        {
            if (count <= 0)
                return InventoryOperationResult.Failed(InventoryOperationStatus.InvalidCount);
            if (!_store.TryGet(containerId, out InventoryContainer container))
                return InventoryOperationResult.Failed(InventoryOperationStatus.InvalidContainer);
            if (!container.IsValidSlot(slotIndex))
                return InventoryOperationResult.Failed(InventoryOperationStatus.InvalidSlot);

            ItemStack current = container.GetStack(slotIndex);
            if (current.IsEmpty)
                return InventoryOperationResult.Failed(InventoryOperationStatus.SlotEmpty);
            if (count > current.Count)
                return InventoryOperationResult.Failed(InventoryOperationStatus.InvalidCount);

            if (count == current.Count)
                container.ClearSlot(slotIndex);
            else
                container.SetStack(slotIndex, current.WithCount(current.Count - count));
            return InventoryOperationResult.Succeeded();
        }

        public InventoryOperationResult MoveStack(
            InventoryContainerId sourceContainerId,
            int sourceSlotIndex,
            InventoryContainerId destinationContainerId,
            int destinationSlotIndex,
            int count)
        {
            if (!TryGetMoveEndpoints(sourceContainerId, sourceSlotIndex, destinationContainerId, destinationSlotIndex, count, out InventoryContainer source, out ItemStack sourceStack, out InventoryContainer destination, out ItemStack destinationStack, out InventoryOperationStatus status))
                return InventoryOperationResult.Failed(status);

            if (!_definitions.TryGet(sourceStack.DefinitionId, out ItemDefinition definition))
                return InventoryOperationResult.Failed(InventoryOperationStatus.DefinitionMissing);

            if (destinationStack.IsEmpty)
            {
                ItemStack moving = sourceStack.WithCount(count);
                source.SetStack(sourceSlotIndex, sourceStack.Count == count ? ItemStack.Empty : sourceStack.WithCount(sourceStack.Count - count));
                destination.SetStack(destinationSlotIndex, moving);
                return InventoryOperationResult.Succeeded();
            }

            if (!CanMerge(sourceStack, destinationStack))
                return InventoryOperationResult.Failed(InventoryOperationStatus.ItemMismatch);
            if (count > definition.MaxStackCount - destinationStack.Count)
                return InventoryOperationResult.Failed(InventoryOperationStatus.StackOverflow);

            source.SetStack(sourceSlotIndex, sourceStack.Count == count ? ItemStack.Empty : sourceStack.WithCount(sourceStack.Count - count));
            destination.SetStack(destinationSlotIndex, destinationStack.WithCount(destinationStack.Count + count));
            return InventoryOperationResult.Succeeded();
        }

        public InventoryOperationResult SplitStack(
            InventoryContainerId sourceContainerId,
            int sourceSlotIndex,
            InventoryContainerId destinationContainerId,
            int destinationSlotIndex,
            int count)
        {
            if (!TryGetMoveEndpoints(sourceContainerId, sourceSlotIndex, destinationContainerId, destinationSlotIndex, count, out InventoryContainer source, out ItemStack sourceStack, out InventoryContainer destination, out ItemStack destinationStack, out InventoryOperationStatus status))
                return InventoryOperationResult.Failed(status);
            if (!destinationStack.IsEmpty)
                return InventoryOperationResult.Failed(InventoryOperationStatus.SlotOccupied);
            if (count >= sourceStack.Count)
                return InventoryOperationResult.Failed(InventoryOperationStatus.InvalidCount);

            destination.SetStack(destinationSlotIndex, sourceStack.WithCount(count));
            source.SetStack(sourceSlotIndex, sourceStack.WithCount(sourceStack.Count - count));
            return InventoryOperationResult.Succeeded();
        }

        public InventoryOperationResult MergeStack(
            InventoryContainerId sourceContainerId,
            int sourceSlotIndex,
            InventoryContainerId destinationContainerId,
            int destinationSlotIndex)
        {
            if (!TryGetMoveEndpoints(sourceContainerId, sourceSlotIndex, destinationContainerId, destinationSlotIndex, 1, out InventoryContainer source, out ItemStack sourceStack, out InventoryContainer destination, out ItemStack destinationStack, out InventoryOperationStatus status))
                return InventoryOperationResult.Failed(status);
            if (destinationStack.IsEmpty)
                return InventoryOperationResult.Failed(InventoryOperationStatus.SlotEmpty);
            if (!CanMerge(sourceStack, destinationStack))
                return InventoryOperationResult.Failed(InventoryOperationStatus.ItemMismatch);
            if (!_definitions.TryGet(sourceStack.DefinitionId, out ItemDefinition definition))
                return InventoryOperationResult.Failed(InventoryOperationStatus.DefinitionMissing);
            if (sourceStack.Count > definition.MaxStackCount - destinationStack.Count)
                return InventoryOperationResult.Failed(InventoryOperationStatus.StackOverflow);

            destination.SetStack(destinationSlotIndex, destinationStack.WithCount(destinationStack.Count + sourceStack.Count));
            source.ClearSlot(sourceSlotIndex);
            return InventoryOperationResult.Succeeded();
        }

        public InventoryOperationResult SwapSlots(
            InventoryContainerId leftContainerId,
            int leftSlotIndex,
            InventoryContainerId rightContainerId,
            int rightSlotIndex)
        {
            if (!_store.TryGet(leftContainerId, out InventoryContainer left))
                return InventoryOperationResult.Failed(InventoryOperationStatus.InvalidContainer);
            if (!_store.TryGet(rightContainerId, out InventoryContainer right))
                return InventoryOperationResult.Failed(InventoryOperationStatus.InvalidContainer);
            if (!left.IsValidSlot(leftSlotIndex) || !right.IsValidSlot(rightSlotIndex))
                return InventoryOperationResult.Failed(InventoryOperationStatus.InvalidSlot);

            ItemStack leftStack = left.GetStack(leftSlotIndex);
            ItemStack rightStack = right.GetStack(rightSlotIndex);
            left.SetStack(leftSlotIndex, rightStack);
            right.SetStack(rightSlotIndex, leftStack);
            return InventoryOperationResult.Succeeded();
        }

        public InventoryOperationResult UseItem(InventoryContainerId containerId, int slotIndex)
        {
            if (!_store.TryGet(containerId, out InventoryContainer container))
                return InventoryOperationResult.Failed(InventoryOperationStatus.InvalidContainer);
            if (!container.IsValidSlot(slotIndex))
                return InventoryOperationResult.Failed(InventoryOperationStatus.InvalidSlot);
            if (container.GetStack(slotIndex).IsEmpty)
                return InventoryOperationResult.Failed(InventoryOperationStatus.SlotEmpty);
            return InventoryOperationResult.Failed(InventoryOperationStatus.UnsupportedUse);
        }

        public InventoryOperationResult TryInsertStack(InventoryContainerId containerId, ItemStack stack)
        {
            if (!_store.TryGet(containerId, out InventoryContainer container))
                return InventoryOperationResult.Failed(InventoryOperationStatus.InvalidContainer);
            if (!ValidateNewStack(stack, out ItemDefinition definition, out InventoryOperationStatus status))
                return InventoryOperationResult.Failed(status);

            InventoryInsertionPlan plan = InventoryInsertionPlanner.CreatePlan(container, definition, stack);
            if (!plan.CanFit)
                return InventoryOperationResult.Failed(InventoryOperationStatus.InsufficientCapacity);

            int remaining = stack.Count;
            for (int slotIndex = 0; slotIndex < container.SlotCount && remaining > 0; slotIndex++)
            {
                ItemStack current = container.GetStack(slotIndex);
                if (current.IsEmpty || !CanMerge(current, stack))
                    continue;

                int available = definition.MaxStackCount - current.Count;
                if (available <= 0)
                    continue;

                int moved = remaining < available ? remaining : available;
                container.SetStack(slotIndex, current.WithCount(current.Count + moved));
                remaining -= moved;
            }

            for (int slotIndex = 0; slotIndex < container.SlotCount && remaining > 0; slotIndex++)
            {
                if (!container.GetStack(slotIndex).IsEmpty)
                    continue;

                int moved = remaining < definition.MaxStackCount ? remaining : definition.MaxStackCount;
                container.SetStack(slotIndex, stack.WithCount(moved));
                remaining -= moved;
            }

            _store.ObserveInstance(stack.InstanceId);
            return InventoryOperationResult.Succeeded();
        }

        private bool TryGetMoveEndpoints(
            InventoryContainerId sourceContainerId,
            int sourceSlotIndex,
            InventoryContainerId destinationContainerId,
            int destinationSlotIndex,
            int count,
            out InventoryContainer source,
            out ItemStack sourceStack,
            out InventoryContainer destination,
            out ItemStack destinationStack,
            out InventoryOperationStatus status)
        {
            source = null;
            destination = null;
            sourceStack = default;
            destinationStack = default;
            status = InventoryOperationStatus.Success;

            if (count <= 0)
            {
                status = InventoryOperationStatus.InvalidCount;
                return false;
            }

            if (!_store.TryGet(sourceContainerId, out source) || !_store.TryGet(destinationContainerId, out destination))
            {
                status = InventoryOperationStatus.InvalidContainer;
                return false;
            }

            if (!source.IsValidSlot(sourceSlotIndex) || !destination.IsValidSlot(destinationSlotIndex))
            {
                status = InventoryOperationStatus.InvalidSlot;
                return false;
            }

            if (sourceContainerId.Equals(destinationContainerId) && sourceSlotIndex == destinationSlotIndex)
            {
                status = InventoryOperationStatus.InvalidSlot;
                return false;
            }

            sourceStack = source.GetStack(sourceSlotIndex);
            destinationStack = destination.GetStack(destinationSlotIndex);
            if (sourceStack.IsEmpty)
            {
                status = InventoryOperationStatus.SlotEmpty;
                return false;
            }

            if (count > sourceStack.Count)
            {
                status = InventoryOperationStatus.InvalidCount;
                return false;
            }

            return true;
        }

        private bool ValidateNewStack(ItemStack stack, out ItemDefinition definition, out InventoryOperationStatus status)
        {
            definition = null;
            status = InventoryOperationStatus.Success;
            if (stack.IsEmpty)
            {
                status = InventoryOperationStatus.InvalidItem;
                return false;
            }

            if (!_definitions.TryGet(stack.DefinitionId, out definition))
            {
                status = InventoryOperationStatus.DefinitionMissing;
                return false;
            }

            if (stack.InstanceId.IsValid && stack.Count != 1)
            {
                status = InventoryOperationStatus.InstanceItemNotStackable;
                return false;
            }

            if (stack.InstanceId.IsValid && _store.ContainsInstance(stack.InstanceId))
            {
                status = InventoryOperationStatus.DuplicateInstance;
                return false;
            }

            return true;
        }

        private static bool CanMerge(ItemStack left, ItemStack right)
        {
            return left.CanMergeWith(right);
        }
    }

    public enum InventoryRequestKind
    {
        AddStack = 0,
        RemoveStack = 1,
        MoveStack = 2,
        SplitStack = 3,
        MergeStack = 4,
        SwapSlots = 5,
        UseItem = 6
    }

    public sealed class InventoryRequest
    {
        public InventoryRequest(
            InventoryRequestKind kind,
            InventoryContainerId sourceContainerId,
            int sourceSlotIndex,
            InventoryContainerId destinationContainerId = default,
            int destinationSlotIndex = -1,
            ItemDefinitionId itemDefinitionId = default,
            int count = 0,
            ItemInstanceId itemInstanceId = default,
            RuntimeCustomState customState = null)
        {
            Kind = kind;
            SourceContainerId = sourceContainerId;
            SourceSlotIndex = sourceSlotIndex;
            DestinationContainerId = destinationContainerId;
            DestinationSlotIndex = destinationSlotIndex;
            ItemDefinitionId = itemDefinitionId;
            Count = count;
            ItemInstanceId = itemInstanceId;
            CustomState = customState?.PreparePayloadDigest();
        }

        public InventoryRequestKind Kind { get; }
        public InventoryContainerId SourceContainerId { get; }
        public int SourceSlotIndex { get; }
        public InventoryContainerId DestinationContainerId { get; }
        public int DestinationSlotIndex { get; }
        public ItemDefinitionId ItemDefinitionId { get; }
        public int Count { get; }
        public ItemInstanceId ItemInstanceId { get; }
        public RuntimeCustomState CustomState { get; }
    }

    public readonly struct InventoryRequestHandle
    {
        public InventoryRequestHandle(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index > 0 && Generation > 0;
    }

    /// <summary>Generation-checked request store used by runtime commands.</summary>
    /// <remarks>Allocation: request creation and dictionary growth are LowFreqAlloc; command execution removes handles without allocating when capacity is established.</remarks>
    public sealed class InventoryRequestStore
    {
        private readonly Dictionary<int, InventoryRequest> _requests = new Dictionary<int, InventoryRequest>();
        private readonly Dictionary<int, int> _generations = new Dictionary<int, int>();
        private readonly Queue<int> _freeIndices = new Queue<int>();
        private int _nextIndex = 1;

        public int Count => _requests.Count;

        public InventoryRequestHandle Add(InventoryRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            int index = _freeIndices.Count > 0 ? _freeIndices.Dequeue() : _nextIndex++;
            int generation = _generations.TryGetValue(index, out int currentGeneration) ? currentGeneration + 1 : 1;
            _generations[index] = generation;
            _requests[index] = request;
            return new InventoryRequestHandle(index, generation);
        }

        public bool TryGet(InventoryRequestHandle handle, out InventoryRequest request)
        {
            if (!handle.IsValid ||
                !_generations.TryGetValue(handle.Index, out int generation) ||
                generation != handle.Generation ||
                !_requests.TryGetValue(handle.Index, out request))
            {
                request = null;
                return false;
            }

            return true;
        }

        public bool Remove(InventoryRequestHandle handle)
        {
            if (!handle.IsValid ||
                !_generations.TryGetValue(handle.Index, out int generation) ||
                generation != handle.Generation ||
                !_requests.Remove(handle.Index))
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

    public static class ItemRuntimeCommandIds
    {
        public const int AddStack = 1003001;
        public const int RemoveStack = 1003002;
        public const int MoveStack = 1003003;
        public const int SplitStack = 1003004;
        public const int MergeStack = 1003005;
        public const int SwapSlots = 1003006;
        public const int UseItem = 1003007;
    }

    public static class InventoryRuntimeCommandFactory
    {
        public static RuntimeCommand Create(RuntimeFrame frame, int sourceId, int commandId, InventoryRequestHandle handle, RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(frame, sourceId, commandId, handle.Index, handle.Generation, 0, 0, traceId);
        }
    }

    public enum ItemRuntimeEventType
    {
        OperationSucceeded = 0,
        OperationRejected = 1
    }

    public readonly struct ItemRuntimeEvent
    {
        public ItemRuntimeEvent(
            ItemRuntimeEventType eventType,
            RuntimeCommand command,
            InventoryRequestKind requestKind,
            InventoryOperationStatus status)
        {
            EventType = eventType;
            Command = command;
            RequestKind = requestKind;
            Status = status;
        }

        public ItemRuntimeEventType EventType { get; }
        public RuntimeCommand Command { get; }
        public InventoryRequestKind RequestKind { get; }
        public InventoryOperationStatus Status { get; }
    }

    public sealed class InventoryRuntimeModule : RuntimeModule
    {
        public const string DefaultModuleId = ItemRuntimeIds.InventoryModule;
        private readonly InventoryService _service;
        private readonly InventoryRequestStore _requests;
        private readonly RuntimeCommandBuffer _commands;
        private readonly RuntimeEventQueue<ItemRuntimeEvent> _events;
        private readonly List<RuntimeCommand> _drainedCommands;

        public InventoryRuntimeModule(
            InventoryService service,
            InventoryRequestStore requests = null,
            RuntimeCommandBuffer commands = null,
            RuntimeEventQueue<ItemRuntimeEvent> events = null,
            string moduleId = DefaultModuleId,
            int priority = 120,
            int commandCapacity = 16)
            : base(moduleId, RuntimeTickStage.Simulation, priority)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _requests = requests ?? new InventoryRequestStore();
            _commands = commands ?? new RuntimeCommandBuffer(commandCapacity);
            _events = events ?? new RuntimeEventQueue<ItemRuntimeEvent>(commandCapacity);
            _drainedCommands = new List<RuntimeCommand>(commandCapacity);
        }

        public InventoryService Service => _service;
        public InventoryRequestStore Requests => _requests;
        public RuntimeCommandBuffer Commands => _commands;
        public RuntimeEventQueue<ItemRuntimeEvent> Events => _events;

        /// <remarks>NoAlloc after initialization while command/event buffers stay within configured capacities; rejected diagnostics may allocate through caller trace strings.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            _drainedCommands.Clear();
            var frame = new RuntimeFrame(context.FrameIndex);
            _commands.DrainForFrame(frame, _drainedCommands);
            for (int i = 0; i < _drainedCommands.Count; i++)
                Execute(context, _drainedCommands[i]);
            _drainedCommands.Clear();
        }

        public int DrainEvents(RuntimeFrame frame, List<ItemRuntimeEvent> output)
        {
            return _events.Drain(frame, output);
        }

        private void Execute(RuntimeTickContext context, RuntimeCommand command)
        {
            InventoryRequestHandle handle = new InventoryRequestHandle(command.TargetId, command.Payload0);
            if (!_requests.TryGet(handle, out InventoryRequest request))
            {
                Enqueue(context, command, InventoryRequestKind.AddStack, InventoryOperationStatus.StaleRequest);
                return;
            }

            _requests.Remove(handle);
            InventoryOperationResult result = ExecuteRequest(command, request);
            Enqueue(context, command, request.Kind, result.Status);
        }

        private InventoryOperationResult ExecuteRequest(RuntimeCommand command, InventoryRequest request)
        {
            if (!MatchesCommand(command.CommandId, request.Kind))
                return InventoryOperationResult.Failed(InventoryOperationStatus.StaleRequest);

            switch (request.Kind)
            {
                case InventoryRequestKind.AddStack:
                    return _service.AddStack(
                        request.SourceContainerId,
                        request.SourceSlotIndex,
                        new ItemStack(request.ItemDefinitionId, request.Count, request.ItemInstanceId, request.CustomState));
                case InventoryRequestKind.RemoveStack:
                    return _service.RemoveStack(request.SourceContainerId, request.SourceSlotIndex, request.Count);
                case InventoryRequestKind.MoveStack:
                    return _service.MoveStack(request.SourceContainerId, request.SourceSlotIndex, request.DestinationContainerId, request.DestinationSlotIndex, request.Count);
                case InventoryRequestKind.SplitStack:
                    return _service.SplitStack(request.SourceContainerId, request.SourceSlotIndex, request.DestinationContainerId, request.DestinationSlotIndex, request.Count);
                case InventoryRequestKind.MergeStack:
                    return _service.MergeStack(request.SourceContainerId, request.SourceSlotIndex, request.DestinationContainerId, request.DestinationSlotIndex);
                case InventoryRequestKind.SwapSlots:
                    return _service.SwapSlots(request.SourceContainerId, request.SourceSlotIndex, request.DestinationContainerId, request.DestinationSlotIndex);
                case InventoryRequestKind.UseItem:
                    return _service.UseItem(request.SourceContainerId, request.SourceSlotIndex);
                default:
                    return InventoryOperationResult.Failed(InventoryOperationStatus.StaleRequest);
            }
        }

        private void Enqueue(RuntimeTickContext context, RuntimeCommand command, InventoryRequestKind requestKind, InventoryOperationStatus status)
        {
            ItemRuntimeEventType eventType = status == InventoryOperationStatus.Success
                ? ItemRuntimeEventType.OperationSucceeded
                : ItemRuntimeEventType.OperationRejected;
            _events.Enqueue(new RuntimeFrame(context.FrameIndex), new ItemRuntimeEvent(eventType, command, requestKind, status));
        }

        private static bool MatchesCommand(int commandId, InventoryRequestKind kind)
        {
            switch (kind)
            {
                case InventoryRequestKind.AddStack:
                    return commandId == ItemRuntimeCommandIds.AddStack;
                case InventoryRequestKind.RemoveStack:
                    return commandId == ItemRuntimeCommandIds.RemoveStack;
                case InventoryRequestKind.MoveStack:
                    return commandId == ItemRuntimeCommandIds.MoveStack;
                case InventoryRequestKind.SplitStack:
                    return commandId == ItemRuntimeCommandIds.SplitStack;
                case InventoryRequestKind.MergeStack:
                    return commandId == ItemRuntimeCommandIds.MergeStack;
                case InventoryRequestKind.SwapSlots:
                    return commandId == ItemRuntimeCommandIds.SwapSlots;
                case InventoryRequestKind.UseItem:
                    return commandId == ItemRuntimeCommandIds.UseItem;
                default:
                    return false;
            }
        }
    }

    /// <summary>Deterministic inventory hash contributor.</summary>
    /// <remarks>Allocation: NoAlloc after initialization; it iterates sorted containers directly and uses constant hash keys.</remarks>
    public sealed class InventoryHashContributor : IRuntimeHashContributor
    {
        private const int F_items_container_count = 1;
        private const int F_items_container_index = 2;
        private const int F_items_container_id = 3;
        private const int F_items_container_owner = 4;
        private const int F_items_container_stable = 5;
        private const int F_items_container_slot_count = 6;
        private const int F_items_slot_index = 7;
        private const int F_items_slot_item = 8;
        private const int F_items_slot_count = 9;
        private const int F_items_slot_instance = 10;
        private const int F_items_slot_custom_type = 11;
        private const int F_items_slot_custom_schema = 12;
        private const int F_items_slot_custom_payload = 13;
        private const int F_items_next_instance_id = 14;

        private readonly InventoryStore _store;

        public InventoryHashContributor(InventoryStore store, int contributorId = RuntimeHashContributorIds.ItemInventory)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            if (contributorId <= 0) throw new ArgumentOutOfRangeException(nameof(contributorId));
            ContributorId = contributorId;
        }

        public int HashSchemaId => RuntimeHashSchemaIds.ItemInventory;
        public int HashSchemaVersion => 2;
        public int ContributorId { get; }

        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));

            accumulator.AddLong(F_items_next_instance_id, _store.NextItemInstanceId);
            accumulator.BeginArray(F_items_container_count, _store.Count);
            for (int i = 0; i < _store.Count; i++)
            {
                accumulator.BeginElement(i);
                InventoryContainer container = _store.GetContainerAtSortedIndex(i);
                accumulator.AddInt(F_items_container_index, i);
                accumulator.AddInt(F_items_container_id, container.ContainerId.Value);
                accumulator.AddInt(F_items_container_owner, container.OwnerId.Value);
                accumulator.AddDigest(F_items_container_stable, container.StableLabelDigest);
                accumulator.BeginArray(F_items_container_slot_count, container.SlotCount);
                for (int slot = 0; slot < container.SlotCount; slot++)
                {
                    accumulator.BeginElement(slot);
                    ItemStack stack = container.GetStack(slot);
                    accumulator.AddInt(F_items_slot_index, slot);
                    accumulator.AddInt(F_items_slot_item, stack.DefinitionId.Value);
                    accumulator.AddInt(F_items_slot_count, stack.Count);
                    accumulator.AddInt(F_items_slot_instance, stack.InstanceId.Value);
                    accumulator.AddInt(F_items_slot_custom_type, stack.CustomState == null ? 0 : stack.CustomState.NumericTypeId);
                    accumulator.AddInt(F_items_slot_custom_schema, stack.CustomState == null ? 0 : stack.CustomState.SchemaVersion);
                    accumulator.AddDigest(F_items_slot_custom_payload, stack.CustomState == null ? default : stack.CustomState.PreparedPayloadDigest);
                    accumulator.EndElement();
                }
                accumulator.EndArray();
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }
    }

    /// <summary>Inventory Runtime SaveState adapter.</summary>
    /// <remarks>Allocation: CaptureSaveState and RestoreSaveState are AllocByDesign because they build DTO arrays and JSON payloads.</remarks>
    public sealed class InventorySaveStateProvider :
        IRuntimeSaveStateProvider,
        IRuntimeSaveStateRestorer
    {
        public const string ModuleId = ItemRuntimeIds.InventoryModule;
        public const int CustomStateSchemaVersion = InventorySaveState.CurrentSchemaVersion;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include
        };

        private readonly InventoryStore _store;
        private readonly ItemDefinitionRegistry _definitions;
        private readonly Func<long> _frameProvider;

        public InventorySaveStateProvider(InventoryStore store, ItemDefinitionRegistry definitions, Func<long> frameProvider = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _frameProvider = frameProvider;
        }

        public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
        {
            InventorySaveState state = CaptureInventoryState();
            string payloadJson = JsonConvert.SerializeObject(state, JsonSettings);
            var moduleState = new RuntimeModuleSaveState(
                ModuleId,
                CustomStateSchemaVersion,
                new RuntimeCustomState(3300, ModuleId, CustomStateSchemaVersion, payloadJson));
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
            RuntimeSaveStateResult<InventorySaveState> loadResult = LoadInventoryState(saveState);
            if (!loadResult.Success)
                return RuntimeSaveStateResult<bool>.Failed(loadResult.Error);

            InventorySaveState state = loadResult.Value;
            if (state.DefinitionHash != _definitions.ComputeStableHash())
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.CustomStateMismatch,
                    "$.moduleStates['" + ModuleId + "'].customState.definitionHash",
                    "Inventory definition hash mismatch."));
            }

            var restoredStore = new InventoryStore();
            var restoredService = new InventoryService(_definitions, restoredStore);
            InventoryContainerSaveState[] containers = state.Containers ?? Array.Empty<InventoryContainerSaveState>();
            var seenContainerIds = new HashSet<int>();
            for (int i = 0; i < containers.Length; i++)
            {
                InventoryContainerSaveState source = containers[i];
                string containerPath = "$.moduleStates['" + ModuleId + "'].customState.payloadJson.containers[" + i + "]";
                if (source == null)
                    return FailedRestore(containerPath, "Inventory container entry is null.");
                if (source.ContainerId <= 0)
                    return FailedRestore(containerPath + ".containerId", "Inventory container id must be positive.");
                if (!seenContainerIds.Add(source.ContainerId))
                    return FailedRestore(containerPath + ".containerId", "Duplicate inventory container id.");
                if (source.SlotCount < 0)
                    return FailedRestore(containerPath + ".slotCount", "Inventory slot count must be non-negative.");

                var container = new InventoryContainer(
                    new InventoryContainerId(source.ContainerId),
                    new InventoryOwnerId(source.OwnerId),
                    source.SlotCount,
                    source.StableId);
                if (!restoredStore.Register(container))
                    return FailedRestore(containerPath + ".containerId", "Inventory container could not be registered.");

                InventorySlotSaveState[] slots = source.Slots ?? Array.Empty<InventorySlotSaveState>();
                var seenSlotIndices = new HashSet<int>();
                for (int j = 0; j < slots.Length; j++)
                {
                    InventorySlotSaveState slot = slots[j];
                    string slotPath = containerPath + ".slots[" + j + "]";
                    if (slot == null)
                        return FailedRestore(slotPath, "Inventory slot entry is null.");
                    if (slot.SlotIndex < 0 || slot.SlotIndex >= source.SlotCount)
                        return FailedRestore(slotPath + ".slotIndex", "Inventory slot index is invalid.");
                    if (!seenSlotIndices.Add(slot.SlotIndex))
                        return FailedRestore(slotPath + ".slotIndex", "Duplicate inventory slot index.");
                    if (slot.ItemDefinitionId <= 0)
                        return FailedRestore(slotPath + ".itemDefinitionId", "Item definition id must be positive.");
                    if (slot.Count <= 0)
                        return FailedRestore(slotPath + ".count", "Inventory stack count must be positive.");
                    if (slot.ItemInstanceId < 0)
                        return FailedRestore(slotPath + ".itemInstanceId", "Item instance id must be non-negative.");

                    RuntimeCustomState customState = string.IsNullOrEmpty(slot.CustomStateTypeId)
                        ? null
                        : new RuntimeCustomState(slot.CustomStateNumericTypeId, slot.CustomStateTypeId, slot.CustomStateSchemaVersion, slot.CustomStatePayloadJson);
                    InventoryOperationResult addResult = restoredService.AddStack(
                        container.ContainerId,
                        slot.SlotIndex,
                        new ItemStack(new ItemDefinitionId(slot.ItemDefinitionId), slot.Count, new ItemInstanceId(slot.ItemInstanceId), customState));
                    if (!addResult.Success)
                        return FailedRestore(slotPath, "Inventory stack is invalid: " + addResult.Status + ".");
                }
            }

            if (state.SchemaVersion == 3 && !restoredStore.TryRestoreInstanceCounter(state.NextItemInstanceId))
                return FailedRestore("$.nextItemInstanceId", "Instance counter must follow all allocated identities and fit the Int32 identity range.");

            _store.Clear();
            for (int i = 0; i < restoredStore.Count; i++)
                _store.Register(restoredStore.GetContainerAtSortedIndex(i));
            _store.TryRestoreInstanceCounter(restoredStore.NextItemInstanceId);

            return RuntimeSaveStateResult<bool>.Succeeded(true);
        }

        private static RuntimeSaveStateResult<bool> FailedRestore(string path, string message)
        {
            return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                RuntimeSaveStateErrorCode.InvalidDocument,
                path,
                message));
        }

        private InventorySaveState CaptureInventoryState()
        {
            IReadOnlyList<InventoryContainer> containers = _store.CreateContainerSnapshot();
            var states = new InventoryContainerSaveState[containers.Count];
            for (int i = 0; i < containers.Count; i++)
            {
                InventoryContainer container = containers[i];
                var slots = new List<InventorySlotSaveState>();
                for (int slotIndex = 0; slotIndex < container.SlotCount; slotIndex++)
                {
                    ItemStack stack = container.GetStack(slotIndex);
                    if (stack.IsEmpty)
                        continue;

                    slots.Add(new InventorySlotSaveState
                    {
                        SlotIndex = slotIndex,
                        ItemDefinitionId = stack.DefinitionId.Value,
                        Count = stack.Count,
                        ItemInstanceId = stack.InstanceId.Value,
                        CustomStateNumericTypeId = stack.CustomState == null ? 0 : stack.CustomState.NumericTypeId,
                        CustomStateTypeId = stack.CustomState == null ? string.Empty : stack.CustomState.TypeId,
                        CustomStateSchemaVersion = stack.CustomState == null ? 0 : stack.CustomState.SchemaVersion,
                        CustomStatePayloadJson = stack.CustomState == null ? string.Empty : stack.CustomState.PayloadJson
                    });
                }

                states[i] = new InventoryContainerSaveState
                {
                    ContainerId = container.ContainerId.Value,
                    StableId = container.StableId,
                    OwnerId = container.OwnerId.Value,
                    SlotCount = container.SlotCount,
                    Slots = slots.ToArray()
                };
            }

            return new InventorySaveState
            {
                SchemaVersion = InventorySaveState.CurrentSchemaVersion,
                DefinitionHash = _definitions.ComputeStableHash(),
                NextItemInstanceId = _store.NextItemInstanceId,
                Containers = states
            };
        }

        private static RuntimeSaveStateResult<InventorySaveState> LoadInventoryState(RuntimeSaveState saveState)
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
                return FailedState("$.moduleStates['" + ModuleId + "']", "Inventory module state is missing.");
            if ((module.SchemaVersion != CustomStateSchemaVersion && module.SchemaVersion != 2)
                || module.CustomState.SchemaVersion != module.SchemaVersion)
                return RuntimeSaveStateResult<InventorySaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.UnsupportedVersion,
                    "$.moduleStates['" + ModuleId + "'].schemaVersion",
                    "Unsupported Inventory schema.",
                    module.SchemaVersion,
                    CustomStateSchemaVersion));
            if (!string.Equals(module.CustomState.TypeId, ModuleId, StringComparison.Ordinal))
                return FailedState("$.moduleStates['" + ModuleId + "'].customState.typeId", "Inventory custom state type mismatch.");

            try
            {
                InventorySaveState state = JsonConvert.DeserializeObject<InventorySaveState>(module.CustomState.PayloadJson, JsonSettings);
                if (state == null)
                    return FailedState("$.moduleStates['" + ModuleId + "'].customState.payloadJson", "Inventory payload is empty.");
                if (state.SchemaVersion != module.SchemaVersion)
                {
                    return RuntimeSaveStateResult<InventorySaveState>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.UnsupportedVersion,
                        "$.moduleStates['" + ModuleId + "'].customState.payloadJson.schemaVersion",
                        "Unsupported Inventory payload schema.",
                        state.SchemaVersion,
                        InventorySaveState.CurrentSchemaVersion));
                }

                return RuntimeSaveStateResult<InventorySaveState>.Succeeded(state);
            }
            catch (Exception ex)
            {
                return RuntimeSaveStateResult<InventorySaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates['" + ModuleId + "'].customState.payloadJson",
                    "Inventory payload could not be parsed: " + ex.Message,
                    exception: ex));
            }
        }

        private static RuntimeSaveStateResult<InventorySaveState> FailedState(string path, string message)
        {
            return RuntimeSaveStateResult<InventorySaveState>.Failed(new RuntimeSaveStateError(RuntimeSaveStateErrorCode.InvalidDocument, path, message));
        }
    }

    public sealed class InventorySaveState
    {
        public const int CurrentSchemaVersion = 3;
        public int SchemaVersion { get; set; }
        public long DefinitionHash { get; set; }
        public long NextItemInstanceId { get; set; }
        public InventoryContainerSaveState[] Containers { get; set; }
    }

    public sealed class InventoryContainerSaveState
    {
        public int ContainerId { get; set; }
        public string StableId { get; set; }
        public int OwnerId { get; set; }
        public int SlotCount { get; set; }
        public InventorySlotSaveState[] Slots { get; set; }
    }

    public sealed class InventorySlotSaveState
    {
        public int SlotIndex { get; set; }
        public int ItemDefinitionId { get; set; }
        public int Count { get; set; }
        public int ItemInstanceId { get; set; }
        public int CustomStateNumericTypeId { get; set; }
        public string CustomStateTypeId { get; set; }
        public int CustomStateSchemaVersion { get; set; }
        public string CustomStatePayloadJson { get; set; }
    }
}
