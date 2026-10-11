using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Items
{
    public readonly struct ItemAcquisitionRequestId : IEquatable<ItemAcquisitionRequestId>, IFormattable
    {
        public ItemAcquisitionRequestId(long value)
        {
            Value = value;
        }

        public long Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(ItemAcquisitionRequestId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is ItemAcquisitionRequestId other && Equals(other); }
        public override int GetHashCode() { return Value.GetHashCode(); }
        public override string ToString() { return Value.ToString(); }
        public string ToString(string format, IFormatProvider formatProvider) { return Value.ToString(format, formatProvider); }
    }

    public enum ItemAcquisitionSourceKind
    {
        Custom = 0,
        DirectGrant = 1,
        ShopPurchase = 2,
        Reward = 3,
        Mail = 4,
        Debug = 5
    }

    public readonly struct ItemGrantLine
    {
        public ItemGrantLine(InventoryContainerId targetContainerId, ItemDefinitionId itemDefinitionId, int count)
        {
            TargetContainerId = targetContainerId;
            ItemDefinitionId = itemDefinitionId;
            Count = count;
        }

        public InventoryContainerId TargetContainerId { get; }
        public ItemDefinitionId ItemDefinitionId { get; }
        public int Count { get; }
    }

    public readonly struct ItemCostLine
    {
        public ItemCostLine(InventoryContainerId sourceContainerId, ItemDefinitionId itemDefinitionId, int count)
        {
            SourceContainerId = sourceContainerId;
            ItemDefinitionId = itemDefinitionId;
            Count = count;
        }

        public InventoryContainerId SourceContainerId { get; }
        public ItemDefinitionId ItemDefinitionId { get; }
        public int Count { get; }
    }

    /// <summary>Low-frequency item acquisition transaction request.</summary>
    /// <remarks>Allocation: request construction stores caller-provided line arrays; execution is LowFreqAlloc because it builds a dry-run plan and rollback snapshot.</remarks>
    public sealed class ItemAcquisitionRequest
    {
        public ItemAcquisitionRequest(
            ItemAcquisitionRequestId requestId,
            ItemAcquisitionSourceKind sourceKind,
            ItemCostLine[] costs,
            ItemGrantLine[] grants)
        {
            RequestId = requestId;
            SourceKind = sourceKind;
            Costs = costs ?? Array.Empty<ItemCostLine>();
            Grants = grants ?? Array.Empty<ItemGrantLine>();
        }

        public ItemAcquisitionRequestId RequestId { get; }
        public ItemAcquisitionSourceKind SourceKind { get; }
        public ItemCostLine[] Costs { get; }
        public ItemGrantLine[] Grants { get; }
    }

    public enum ItemAcquisitionStatus
    {
        Success = 0,
        InvalidRequest = 1,
        AlreadyCommitted = 2,
        EmptyTransaction = 3,
        InvalidContainer = 4,
        InvalidItem = 5,
        DefinitionMissing = 6,
        InvalidCount = 7,
        InsufficientCost = 8,
        InsufficientCapacity = 9,
        CommitFailed = 10,
        StaleRequest = 11
    }

    public readonly struct ItemAcquisitionResult
    {
        public ItemAcquisitionResult(
            ItemAcquisitionStatus status,
            int costLineIndex = -1,
            int grantLineIndex = -1,
            InventoryOperationStatus inventoryStatus = InventoryOperationStatus.Success)
        {
            Status = status;
            CostLineIndex = costLineIndex;
            GrantLineIndex = grantLineIndex;
            InventoryStatus = inventoryStatus;
        }

        public ItemAcquisitionStatus Status { get; }
        public int CostLineIndex { get; }
        public int GrantLineIndex { get; }
        public InventoryOperationStatus InventoryStatus { get; }
        public bool Success => Status == ItemAcquisitionStatus.Success;

        public static ItemAcquisitionResult Succeeded()
        {
            return new ItemAcquisitionResult(ItemAcquisitionStatus.Success);
        }

        public static ItemAcquisitionResult Failed(
            ItemAcquisitionStatus status,
            int costLineIndex = -1,
            int grantLineIndex = -1,
            InventoryOperationStatus inventoryStatus = InventoryOperationStatus.Success)
        {
            return new ItemAcquisitionResult(status, costLineIndex, grantLineIndex, inventoryStatus);
        }
    }

    /// <summary>Idempotency ledger for successful item acquisition requests.</summary>
    /// <remarks>Allocation: ledger mutation is LowFreqAlloc; snapshot/save APIs are AllocByDesign.</remarks>
    public sealed class ItemAcquisitionLedger
    {
        private readonly HashSet<long> _committed = new HashSet<long>();
        private readonly List<long> _sortedRequestIds = new List<long>();

        public int Count => _sortedRequestIds.Count;

        public bool Contains(ItemAcquisitionRequestId requestId)
        {
            return requestId.IsValid && _committed.Contains(requestId.Value);
        }

        public bool TryCommit(ItemAcquisitionRequestId requestId)
        {
            if (!requestId.IsValid || !_committed.Add(requestId.Value))
                return false;

            _sortedRequestIds.Add(requestId.Value);
            _sortedRequestIds.Sort();
            return true;
        }

        public ItemAcquisitionRequestId GetRequestIdAtSortedIndex(int index)
        {
            return new ItemAcquisitionRequestId(_sortedRequestIds[index]);
        }

        public ItemAcquisitionRequestId[] CreateSnapshot()
        {
            var snapshot = new ItemAcquisitionRequestId[_sortedRequestIds.Count];
            for (int i = 0; i < _sortedRequestIds.Count; i++)
                snapshot[i] = new ItemAcquisitionRequestId(_sortedRequestIds[i]);
            return snapshot;
        }

        public void Clear()
        {
            _committed.Clear();
            _sortedRequestIds.Clear();
        }
    }

    /// <summary>Low-frequency transaction service for direct grants and purchase-like inventory commits.</summary>
    /// <remarks>Allocation: Execute is LowFreqAlloc because it creates a dry-run working copy and rollback snapshot; it is not intended for per-frame Tick paths.</remarks>
    public sealed class InventoryTransactionService
    {
        private readonly InventoryService _inventory;
        private readonly ItemAcquisitionLedger _ledger;

        public InventoryTransactionService(InventoryService inventory, ItemAcquisitionLedger ledger)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        }

        public InventoryService Inventory => _inventory;
        public ItemAcquisitionLedger Ledger => _ledger;

        /// <remarks>LowFreqAlloc: builds dry-run and rollback snapshots, then commits through InventoryService.</remarks>
        public ItemAcquisitionResult Execute(ItemAcquisitionRequest request)
        {
            return Execute(request, false);
        }

        /// <summary>Executes inventory costs/grants as one component of a staged outer transaction.</summary>
        /// <remarks>LowFreqAlloc. Set hasExternalGrant only after validating a non-inventory grant in the
        /// same detached candidate. The outer owner must publish that grant and this ledger together.
        /// This overload does not publish or persist the external grant itself.</remarks>
        public ItemAcquisitionResult Execute(ItemAcquisitionRequest request, bool hasExternalGrant)
        {
            if (request == null || !request.RequestId.IsValid)
                return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidRequest);
            if (_ledger.Contains(request.RequestId))
                return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.AlreadyCommitted);
            if (request.Grants.Length == 0 && !hasExternalGrant)
                return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.EmptyTransaction);

            InventorySnapshot working = InventorySnapshot.Capture(_inventory.Store);
            ItemAcquisitionResult planResult = ValidateAndApplyToWorkingCopy(request, working);
            if (!planResult.Success)
                return planResult;

            InventorySnapshot beforeCommit = InventorySnapshot.Capture(_inventory.Store);
            ItemAcquisitionResult commitResult = Commit(request);
            if (!commitResult.Success)
            {
                if (!Restore(beforeCommit))
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.CommitFailed);
                return commitResult;
            }

            if (!_ledger.TryCommit(request.RequestId))
            {
                if (!Restore(beforeCommit))
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.CommitFailed);
                return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.AlreadyCommitted);
            }

            return ItemAcquisitionResult.Succeeded();
        }

        private ItemAcquisitionResult ValidateAndApplyToWorkingCopy(ItemAcquisitionRequest request, InventorySnapshot working)
        {
            for (int i = 0; i < request.Costs.Length; i++)
            {
                ItemCostLine cost = request.Costs[i];
                ItemAcquisitionResult validation = ValidateItem(cost.ItemDefinitionId, cost.Count, i, -1);
                if (!validation.Success)
                    return validation;
                if (!working.TryGet(cost.SourceContainerId, out InventoryContainerSnapshot container))
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidContainer, i);
                if (!RemoveCostFromWorkingContainer(container, cost.ItemDefinitionId, cost.Count))
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InsufficientCost, i);
            }

            for (int i = 0; i < request.Grants.Length; i++)
            {
                ItemGrantLine grant = request.Grants[i];
                ItemAcquisitionResult validation = ValidateItem(grant.ItemDefinitionId, grant.Count, -1, i);
                if (!validation.Success)
                    return validation;
                if (!working.TryGet(grant.TargetContainerId, out InventoryContainerSnapshot container))
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidContainer, grantLineIndex: i);
                if (!_inventory.Definitions.TryGet(grant.ItemDefinitionId, out ItemDefinition definition))
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.DefinitionMissing, grantLineIndex: i);
                if (!InsertGrantIntoWorkingContainer(container, definition, grant))
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InsufficientCapacity, grantLineIndex: i);
            }

            return ItemAcquisitionResult.Succeeded();
        }

        private ItemAcquisitionResult ValidateItem(ItemDefinitionId itemDefinitionId, int count, int costLineIndex, int grantLineIndex)
        {
            if (!itemDefinitionId.IsValid)
                return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidItem, costLineIndex, grantLineIndex);
            if (count <= 0)
                return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.InvalidCount, costLineIndex, grantLineIndex);
            if (!_inventory.Definitions.TryGet(itemDefinitionId, out _))
                return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.DefinitionMissing, costLineIndex, grantLineIndex);
            return ItemAcquisitionResult.Succeeded();
        }

        private static bool RemoveCostFromWorkingContainer(InventoryContainerSnapshot container, ItemDefinitionId itemDefinitionId, int count)
        {
            int remaining = count;
            for (int slotIndex = 0; slotIndex < container.Slots.Length && remaining > 0; slotIndex++)
            {
                ItemStack stack = container.Slots[slotIndex];
                if (!CanConsumeAsCost(stack, itemDefinitionId))
                    continue;

                int removed = stack.Count < remaining ? stack.Count : remaining;
                remaining -= removed;
                container.Slots[slotIndex] = stack.Count == removed
                    ? ItemStack.Empty
                    : stack.WithCount(stack.Count - removed);
            }

            return remaining == 0;
        }

        private static bool InsertGrantIntoWorkingContainer(InventoryContainerSnapshot container, ItemDefinition definition, ItemGrantLine grant)
        {
            int remaining = grant.Count;
            for (int slotIndex = 0; slotIndex < container.Slots.Length && remaining > 0; slotIndex++)
            {
                ItemStack current = container.Slots[slotIndex];
                if (current.IsEmpty || !CanMerge(current, grant.ItemDefinitionId))
                    continue;

                int available = definition.MaxStackCount - current.Count;
                if (available <= 0)
                    continue;

                int moved = remaining < available ? remaining : available;
                container.Slots[slotIndex] = current.WithCount(current.Count + moved);
                remaining -= moved;
            }

            for (int slotIndex = 0; slotIndex < container.Slots.Length && remaining > 0; slotIndex++)
            {
                if (!container.Slots[slotIndex].IsEmpty)
                    continue;

                int moved = remaining < definition.MaxStackCount ? remaining : definition.MaxStackCount;
                container.Slots[slotIndex] = new ItemStack(grant.ItemDefinitionId, moved);
                remaining -= moved;
            }

            return remaining == 0;
        }

        private ItemAcquisitionResult Commit(ItemAcquisitionRequest request)
        {
            for (int i = 0; i < request.Costs.Length; i++)
            {
                ItemCostLine cost = request.Costs[i];
                InventoryOperationStatus status = RemoveCostFromInventory(cost);
                if (status != InventoryOperationStatus.Success)
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.CommitFailed, i, -1, status);
            }

            for (int i = 0; i < request.Grants.Length; i++)
            {
                ItemGrantLine grant = request.Grants[i];
                InventoryOperationResult result = _inventory.TryInsertStack(
                    grant.TargetContainerId,
                    new ItemStack(grant.ItemDefinitionId, grant.Count));
                if (!result.Success)
                    return ItemAcquisitionResult.Failed(ItemAcquisitionStatus.CommitFailed, -1, i, result.Status);
            }

            return ItemAcquisitionResult.Succeeded();
        }

        private InventoryOperationStatus RemoveCostFromInventory(ItemCostLine cost)
        {
            if (!_inventory.Store.TryGet(cost.SourceContainerId, out InventoryContainer container))
                return InventoryOperationStatus.InvalidContainer;

            int remaining = cost.Count;
            for (int slotIndex = 0; slotIndex < container.SlotCount && remaining > 0; slotIndex++)
            {
                ItemStack stack = container.GetStack(slotIndex);
                if (!CanConsumeAsCost(stack, cost.ItemDefinitionId))
                    continue;

                int removed = stack.Count < remaining ? stack.Count : remaining;
                InventoryOperationResult result = _inventory.RemoveStack(cost.SourceContainerId, slotIndex, removed);
                if (!result.Success)
                    return result.Status;
                remaining -= removed;
            }

            return remaining == 0 ? InventoryOperationStatus.Success : InventoryOperationStatus.InvalidCount;
        }

        private bool Restore(InventorySnapshot snapshot)
        {
            _inventory.Store.Clear();
            for (int i = 0; i < snapshot.Containers.Length; i++)
            {
                InventoryContainerSnapshot source = snapshot.Containers[i];
                var container = new InventoryContainer(
                    source.ContainerId,
                    source.OwnerId,
                    source.Slots.Length,
                    source.StableId);
                if (!_inventory.Store.Register(container))
                    return false;

                for (int slotIndex = 0; slotIndex < source.Slots.Length; slotIndex++)
                {
                    ItemStack stack = source.Slots[slotIndex];
                    if (stack.IsEmpty)
                        continue;

                    InventoryOperationResult result = _inventory.AddStack(source.ContainerId, slotIndex, stack);
                    if (!result.Success)
                        return false;
                }
            }

            return _inventory.Store.TryRestoreInstanceCounter(snapshot.NextItemInstanceId);
        }

        private static bool CanConsumeAsCost(ItemStack stack, ItemDefinitionId itemDefinitionId)
        {
            return !stack.IsEmpty
                && stack.DefinitionId.Equals(itemDefinitionId)
                && !stack.InstanceId.IsValid
                && stack.CustomState == null;
        }

        private static bool CanMerge(ItemStack stack, ItemDefinitionId itemDefinitionId)
        {
            return !stack.IsEmpty
                && stack.DefinitionId.Equals(itemDefinitionId)
                && !stack.InstanceId.IsValid
                && stack.CustomState == null;
        }
    }

    public readonly struct ItemAcquisitionRequestHandle
    {
        public ItemAcquisitionRequestHandle(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public int Index { get; }
        public int Generation { get; }
        public bool IsValid => Index > 0 && Generation > 0;
    }

    /// <summary>Generation-checked request store for low-frequency acquisition commands.</summary>
    /// <remarks>Allocation: request creation and dictionary growth are LowFreqAlloc; command execution removes established handles without allocation.</remarks>
    public sealed class ItemAcquisitionRequestStore
    {
        private readonly Dictionary<int, ItemAcquisitionRequest> _requests = new Dictionary<int, ItemAcquisitionRequest>();
        private readonly Dictionary<int, int> _generations = new Dictionary<int, int>();
        private readonly Queue<int> _freeIndices = new Queue<int>();
        private int _nextIndex = 1;

        public int Count => _requests.Count;

        public ItemAcquisitionRequestHandle Add(ItemAcquisitionRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            int index = _freeIndices.Count > 0 ? _freeIndices.Dequeue() : _nextIndex++;
            int generation = _generations.TryGetValue(index, out int currentGeneration) ? currentGeneration + 1 : 1;
            _generations[index] = generation;
            _requests[index] = request;
            return new ItemAcquisitionRequestHandle(index, generation);
        }

        public bool TryGet(ItemAcquisitionRequestHandle handle, out ItemAcquisitionRequest request)
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

        public bool Remove(ItemAcquisitionRequestHandle handle)
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

    public static class ItemAcquisitionCommandIds
    {
        public const int Commit = 1003301;
    }

    public static class ItemAcquisitionRuntimeCommandFactory
    {
        public static RuntimeCommand Create(RuntimeFrame frame, int sourceId, ItemAcquisitionRequestHandle handle, RuntimeTraceIdentity traceId = default)
        {
            return new RuntimeCommand(frame, sourceId, ItemAcquisitionCommandIds.Commit, handle.Index, handle.Generation, 0, 0, traceId);
        }
    }

    public enum ItemAcquisitionRuntimeEventType
    {
        Committed = 0,
        Rejected = 1
    }

    public readonly struct ItemAcquisitionRuntimeEvent
    {
        public ItemAcquisitionRuntimeEvent(ItemAcquisitionRuntimeEventType eventType, RuntimeCommand command, ItemAcquisitionResult result)
        {
            EventType = eventType;
            Command = command;
            Result = result;
        }

        public ItemAcquisitionRuntimeEventType EventType { get; }
        public RuntimeCommand Command { get; }
        public ItemAcquisitionResult Result { get; }
    }

    public sealed class ItemAcquisitionRuntimeModule : RuntimeModule
    {
        public const string DefaultModuleId = ItemRuntimeIds.AcquisitionModule;

        private readonly InventoryTransactionService _service;
        private readonly ItemAcquisitionRequestStore _requests;
        private readonly RuntimeCommandBuffer _commands;
        private readonly RuntimeEventQueue<ItemAcquisitionRuntimeEvent> _events;
        private readonly List<RuntimeCommand> _drainedCommands;

        public ItemAcquisitionRuntimeModule(
            InventoryTransactionService service,
            ItemAcquisitionRequestStore requests = null,
            RuntimeCommandBuffer commands = null,
            RuntimeEventQueue<ItemAcquisitionRuntimeEvent> events = null,
            string moduleId = DefaultModuleId,
            int priority = 125,
            int commandCapacity = 16)
            : base(moduleId, RuntimeTickStage.Simulation, priority)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _requests = requests ?? new ItemAcquisitionRequestStore();
            _commands = commands ?? new RuntimeCommandBuffer(commandCapacity);
            _events = events ?? new RuntimeEventQueue<ItemAcquisitionRuntimeEvent>(commandCapacity);
            _drainedCommands = new List<RuntimeCommand>(commandCapacity);
        }

        public InventoryTransactionService Service => _service;
        public ItemAcquisitionRequestStore Requests => _requests;
        public RuntimeCommandBuffer Commands => _commands;
        public RuntimeEventQueue<ItemAcquisitionRuntimeEvent> Events => _events;

        /// <remarks>LowFreqAlloc when acquisition requests execute; command/event buffers are NoAlloc after initialization within configured capacities.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            _drainedCommands.Clear();
            var frame = new RuntimeFrame(context.FrameIndex);
            _commands.DrainForFrame(frame, _drainedCommands);
            for (int i = 0; i < _drainedCommands.Count; i++)
                Execute(context, _drainedCommands[i]);
            _drainedCommands.Clear();
        }

        public int DrainEvents(RuntimeFrame frame, List<ItemAcquisitionRuntimeEvent> output)
        {
            return _events.Drain(frame, output);
        }

        private void Execute(RuntimeTickContext context, RuntimeCommand command)
        {
            if (command.CommandId != ItemAcquisitionCommandIds.Commit)
            {
                Enqueue(context, command, ItemAcquisitionResult.Failed(ItemAcquisitionStatus.StaleRequest));
                return;
            }

            var handle = new ItemAcquisitionRequestHandle(command.TargetId, command.Payload0);
            if (!_requests.TryGet(handle, out ItemAcquisitionRequest request))
            {
                Enqueue(context, command, ItemAcquisitionResult.Failed(ItemAcquisitionStatus.StaleRequest));
                return;
            }

            _requests.Remove(handle);
            Enqueue(context, command, _service.Execute(request));
        }

        private void Enqueue(RuntimeTickContext context, RuntimeCommand command, ItemAcquisitionResult result)
        {
            ItemAcquisitionRuntimeEventType eventType = result.Success
                ? ItemAcquisitionRuntimeEventType.Committed
                : ItemAcquisitionRuntimeEventType.Rejected;
            _events.Enqueue(new RuntimeFrame(context.FrameIndex), new ItemAcquisitionRuntimeEvent(eventType, command, result));
        }
    }

    /// <summary>Deterministic acquisition ledger hash contributor.</summary>
    /// <remarks>NoAlloc after initialization; iterates the sorted ledger and uses constant hash keys.</remarks>
    public sealed class ItemAcquisitionHashContributor : IRuntimeHashContributor
    {
        private const int EntriesField = 1, RequestField = 2;

        private readonly ItemAcquisitionLedger _ledger;

        public ItemAcquisitionHashContributor(ItemAcquisitionLedger ledger, int contributorId = RuntimeHashContributorIds.ItemAcquisition)
        {
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            if (contributorId <= 0) throw new ArgumentOutOfRangeException(nameof(contributorId));
            ContributorId = contributorId;
        }

        public int HashSchemaId => RuntimeHashSchemaIds.ItemAcquisition;
        public int HashSchemaVersion => 1;
        public int ContributorId { get; }

        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));

            accumulator.BeginArray(EntriesField, _ledger.Count);
            for (int i = 0; i < _ledger.Count; i++)
            {
                accumulator.BeginElement(i);
                accumulator.AddLong(RequestField, _ledger.GetRequestIdAtSortedIndex(i).Value);
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }
    }

    /// <summary>SaveState adapter for acquisition idempotency ledger.</summary>
    /// <remarks>Allocation: CaptureSaveState and RestoreSaveState are AllocByDesign because they build DTO arrays and JSON payloads.</remarks>
    public sealed class ItemAcquisitionSaveStateProvider :
        IRuntimeSaveStateProvider,
        IRuntimeSaveStateRestorer
    {
        public const string ModuleId = ItemRuntimeIds.AcquisitionModule;
        public const int CustomStateSchemaVersion = ItemAcquisitionSaveState.CurrentSchemaVersion;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include
        };

        private readonly ItemAcquisitionLedger _ledger;
        private readonly Func<long> _frameProvider;

        public ItemAcquisitionSaveStateProvider(ItemAcquisitionLedger ledger, Func<long> frameProvider = null)
        {
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _frameProvider = frameProvider;
        }

        public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
        {
            ItemAcquisitionSaveState state = CaptureLedgerState();
            string payloadJson = JsonConvert.SerializeObject(state, JsonSettings);
            var moduleState = new RuntimeModuleSaveState(
                ModuleId,
                CustomStateSchemaVersion,
                new RuntimeCustomState(3304, ModuleId, CustomStateSchemaVersion, payloadJson));
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
            RuntimeSaveStateResult<ItemAcquisitionSaveState> loadResult = LoadLedgerState(saveState);
            if (!loadResult.Success)
                return RuntimeSaveStateResult<bool>.Failed(loadResult.Error);

            ItemAcquisitionSaveState state = loadResult.Value;
            long[] ids = state.CommittedRequestIds ?? Array.Empty<long>();
            var seen = new HashSet<long>();
            for (int i = 0; i < ids.Length; i++)
            {
                var requestId = new ItemAcquisitionRequestId(ids[i]);
                if (!requestId.IsValid || !seen.Add(requestId.Value))
                {
                    return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.InvalidDocument,
                        "$.moduleStates['" + ModuleId + "'].customState.payloadJson.committedRequestIds[" + i + "]",
                        "Invalid or duplicate acquisition request id."));
                }
            }

            _ledger.Clear();
            for (int i = 0; i < ids.Length; i++)
                _ledger.TryCommit(new ItemAcquisitionRequestId(ids[i]));

            return RuntimeSaveStateResult<bool>.Succeeded(true);
        }

        private ItemAcquisitionSaveState CaptureLedgerState()
        {
            ItemAcquisitionRequestId[] committed = _ledger.CreateSnapshot();
            var ids = new long[committed.Length];
            for (int i = 0; i < committed.Length; i++)
                ids[i] = committed[i].Value;

            return new ItemAcquisitionSaveState
            {
                SchemaVersion = ItemAcquisitionSaveState.CurrentSchemaVersion,
                CommittedRequestIds = ids
            };
        }

        private static RuntimeSaveStateResult<ItemAcquisitionSaveState> LoadLedgerState(RuntimeSaveState saveState)
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
                return FailedState("$.moduleStates['" + ModuleId + "']", "Acquisition module state is missing.");
            if (module.SchemaVersion != CustomStateSchemaVersion || module.CustomState.SchemaVersion != CustomStateSchemaVersion)
                return RuntimeSaveStateResult<ItemAcquisitionSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.UnsupportedVersion,
                    "$.moduleStates['" + ModuleId + "'].schemaVersion",
                    "Unsupported Acquisition schema.",
                    module.SchemaVersion,
                    CustomStateSchemaVersion));
            if (!string.Equals(module.CustomState.TypeId, ModuleId, StringComparison.Ordinal))
                return FailedState("$.moduleStates['" + ModuleId + "'].customState.typeId", "Acquisition custom state type mismatch.");

            try
            {
                ItemAcquisitionSaveState state = JsonConvert.DeserializeObject<ItemAcquisitionSaveState>(module.CustomState.PayloadJson, JsonSettings);
                if (state == null)
                    return FailedState("$.moduleStates['" + ModuleId + "'].customState.payloadJson", "Acquisition payload is empty.");
                if (state.SchemaVersion != ItemAcquisitionSaveState.CurrentSchemaVersion)
                {
                    return RuntimeSaveStateResult<ItemAcquisitionSaveState>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.UnsupportedVersion,
                        "$.moduleStates['" + ModuleId + "'].customState.payloadJson.schemaVersion",
                        "Unsupported Acquisition payload schema.",
                        state.SchemaVersion,
                        ItemAcquisitionSaveState.CurrentSchemaVersion));
                }

                return RuntimeSaveStateResult<ItemAcquisitionSaveState>.Succeeded(state);
            }
            catch (Exception ex)
            {
                return RuntimeSaveStateResult<ItemAcquisitionSaveState>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates['" + ModuleId + "'].customState.payloadJson",
                    "Acquisition payload could not be parsed: " + ex.Message,
                    exception: ex));
            }
        }

        private static RuntimeSaveStateResult<ItemAcquisitionSaveState> FailedState(string path, string message)
        {
            return RuntimeSaveStateResult<ItemAcquisitionSaveState>.Failed(new RuntimeSaveStateError(RuntimeSaveStateErrorCode.InvalidDocument, path, message));
        }
    }

    public sealed class ItemAcquisitionSaveState
    {
        public const int CurrentSchemaVersion = 1;
        public int SchemaVersion { get; set; }
        public long[] CommittedRequestIds { get; set; }
    }

    internal sealed class InventorySnapshot
    {
        private InventorySnapshot(InventoryContainerSnapshot[] containers, long nextItemInstanceId)
        {
            Containers = containers ?? Array.Empty<InventoryContainerSnapshot>();
            NextItemInstanceId = nextItemInstanceId;
        }

        public InventoryContainerSnapshot[] Containers { get; }
        public long NextItemInstanceId { get; }

        public static InventorySnapshot Capture(InventoryStore store)
        {
            var containers = new InventoryContainerSnapshot[store.Count];
            for (int i = 0; i < store.Count; i++)
            {
                InventoryContainer container = store.GetContainerAtSortedIndex(i);
                var slots = new ItemStack[container.SlotCount];
                for (int slotIndex = 0; slotIndex < container.SlotCount; slotIndex++)
                    slots[slotIndex] = container.GetStack(slotIndex);
                containers[i] = new InventoryContainerSnapshot(container.ContainerId, container.OwnerId, container.StableId, slots);
            }

            return new InventorySnapshot(containers, store.NextItemInstanceId);
        }

        public bool TryGet(InventoryContainerId containerId, out InventoryContainerSnapshot container)
        {
            for (int i = 0; i < Containers.Length; i++)
            {
                if (Containers[i].ContainerId.Equals(containerId))
                {
                    container = Containers[i];
                    return true;
                }
            }

            container = null;
            return false;
        }
    }

    internal sealed class InventoryContainerSnapshot
    {
        public InventoryContainerSnapshot(InventoryContainerId containerId, InventoryOwnerId ownerId, string stableId, ItemStack[] slots)
        {
            ContainerId = containerId;
            OwnerId = ownerId;
            StableId = stableId ?? string.Empty;
            Slots = slots ?? Array.Empty<ItemStack>();
        }

        public InventoryContainerId ContainerId { get; }
        public InventoryOwnerId OwnerId { get; }
        public string StableId { get; }
        public ItemStack[] Slots { get; }
    }
}
