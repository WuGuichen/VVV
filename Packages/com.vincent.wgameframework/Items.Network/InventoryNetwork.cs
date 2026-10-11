using System;
using System.Collections.Generic;
using MxFramework.Network;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Items.Network
{
    /// <summary>Network-safe inventory runtime command ids.</summary>
    /// <remarks>Allocation: constants only; wire shape remains compact integer command and payload fields.</remarks>
    public static class ItemNetworkCommandIds
    {
        public const int AddStack = 1003201;
        public const int RemoveStack = 1003202;
        public const int MoveStack = 1003203;
        public const int SplitStack = 1003204;
        public const int MergeStack = 1003205;
        public const int SwapSlots = 1003206;
        public const int UseItem = 1003207;

        public static bool IsInventoryNetworkCommand(int commandId)
        {
            return commandId >= AddStack && commandId <= UseItem;
        }
    }

    /// <summary>Inventory operation kind carried by compact network commands.</summary>
    public enum InventoryNetworkCommandKind
    {
        AddStack = 0,
        RemoveStack = 1,
        MoveStack = 2,
        SplitStack = 3,
        MergeStack = 4,
        SwapSlots = 5,
        UseItem = 6
    }

    /// <summary>Decode result for compact inventory network commands.</summary>
    public enum InventoryNetworkCommandDecodeStatus
    {
        Success = 0,
        UnsupportedCommand = 1,
        InvalidPayload = 2
    }

    /// <summary>Decoded network-safe inventory operation.</summary>
    /// <remarks>Allocation: value type only; it carries numeric ids and slot indices, not request handles or content catalog data.</remarks>
    public readonly struct InventoryNetworkCommand
    {
        public InventoryNetworkCommand(
            InventoryNetworkCommandKind kind,
            InventoryContainerId sourceContainerId,
            int sourceSlotIndex,
            InventoryContainerId destinationContainerId = default,
            int destinationSlotIndex = -1,
            ItemDefinitionId itemDefinitionId = default,
            int count = 0)
        {
            Kind = kind;
            SourceContainerId = sourceContainerId;
            SourceSlotIndex = sourceSlotIndex;
            DestinationContainerId = destinationContainerId;
            DestinationSlotIndex = destinationSlotIndex;
            ItemDefinitionId = itemDefinitionId;
            Count = count;
        }

        public InventoryNetworkCommandKind Kind { get; }
        public InventoryContainerId SourceContainerId { get; }
        public int SourceSlotIndex { get; }
        public InventoryContainerId DestinationContainerId { get; }
        public int DestinationSlotIndex { get; }
        public ItemDefinitionId ItemDefinitionId { get; }
        public int Count { get; }
    }

    /// <summary>Encodes inventory operations into network-safe <see cref="RuntimeCommand"/> payloads.</summary>
    /// <remarks>
    /// Allocation: encode/decode/execute are NoAlloc after initialization. Snapshot and request-store handles are not embedded in network commands.
    /// </remarks>
    public static class InventoryNetworkCommandCodec
    {
        private const int MaxSlotIndex = ushort.MaxValue;
        private const int SlotMask = ushort.MaxValue;

        public static RuntimeCommand CreateAddStack(
            RuntimeFrame frame,
            int sourceId,
            InventoryContainerId containerId,
            int slotIndex,
            ItemDefinitionId itemDefinitionId,
            int count,
            RuntimeTraceIdentity traceId = default,
            long sequence = 0L)
        {
            ValidateContainer(containerId, nameof(containerId));
            ValidateSlot(slotIndex, nameof(slotIndex));
            ValidateItem(itemDefinitionId, nameof(itemDefinitionId));
            ValidateCount(count, nameof(count));
            return new RuntimeCommand(
                frame,
                sourceId,
                ItemNetworkCommandIds.AddStack,
                containerId.Value,
                slotIndex,
                itemDefinitionId.Value,
                count,
                traceId,
                sequence);
        }

        public static RuntimeCommand CreateRemoveStack(
            RuntimeFrame frame,
            int sourceId,
            InventoryContainerId containerId,
            int slotIndex,
            int count,
            RuntimeTraceIdentity traceId = default,
            long sequence = 0L)
        {
            ValidateContainer(containerId, nameof(containerId));
            ValidateSlot(slotIndex, nameof(slotIndex));
            ValidateCount(count, nameof(count));
            return new RuntimeCommand(
                frame,
                sourceId,
                ItemNetworkCommandIds.RemoveStack,
                containerId.Value,
                slotIndex,
                count,
                0,
                traceId,
                sequence);
        }

        public static RuntimeCommand CreateMoveStack(
            RuntimeFrame frame,
            int sourceId,
            InventoryContainerId sourceContainerId,
            int sourceSlotIndex,
            InventoryContainerId destinationContainerId,
            int destinationSlotIndex,
            int count,
            RuntimeTraceIdentity traceId = default,
            long sequence = 0L)
        {
            return CreateTwoSlotCommand(
                frame,
                sourceId,
                ItemNetworkCommandIds.MoveStack,
                sourceContainerId,
                sourceSlotIndex,
                destinationContainerId,
                destinationSlotIndex,
                count,
                traceId,
                sequence);
        }

        public static RuntimeCommand CreateSplitStack(
            RuntimeFrame frame,
            int sourceId,
            InventoryContainerId sourceContainerId,
            int sourceSlotIndex,
            InventoryContainerId destinationContainerId,
            int destinationSlotIndex,
            int count,
            RuntimeTraceIdentity traceId = default,
            long sequence = 0L)
        {
            return CreateTwoSlotCommand(
                frame,
                sourceId,
                ItemNetworkCommandIds.SplitStack,
                sourceContainerId,
                sourceSlotIndex,
                destinationContainerId,
                destinationSlotIndex,
                count,
                traceId,
                sequence);
        }

        public static RuntimeCommand CreateMergeStack(
            RuntimeFrame frame,
            int sourceId,
            InventoryContainerId sourceContainerId,
            int sourceSlotIndex,
            InventoryContainerId destinationContainerId,
            int destinationSlotIndex,
            RuntimeTraceIdentity traceId = default,
            long sequence = 0L)
        {
            return CreateTwoSlotCommand(
                frame,
                sourceId,
                ItemNetworkCommandIds.MergeStack,
                sourceContainerId,
                sourceSlotIndex,
                destinationContainerId,
                destinationSlotIndex,
                0,
                traceId,
                sequence);
        }

        public static RuntimeCommand CreateSwapSlots(
            RuntimeFrame frame,
            int sourceId,
            InventoryContainerId leftContainerId,
            int leftSlotIndex,
            InventoryContainerId rightContainerId,
            int rightSlotIndex,
            RuntimeTraceIdentity traceId = default,
            long sequence = 0L)
        {
            return CreateTwoSlotCommand(
                frame,
                sourceId,
                ItemNetworkCommandIds.SwapSlots,
                leftContainerId,
                leftSlotIndex,
                rightContainerId,
                rightSlotIndex,
                0,
                traceId,
                sequence);
        }

        public static RuntimeCommand CreateUseItem(
            RuntimeFrame frame,
            int sourceId,
            InventoryContainerId containerId,
            int slotIndex,
            RuntimeTraceIdentity traceId = default,
            long sequence = 0L)
        {
            ValidateContainer(containerId, nameof(containerId));
            ValidateSlot(slotIndex, nameof(slotIndex));
            return new RuntimeCommand(
                frame,
                sourceId,
                ItemNetworkCommandIds.UseItem,
                containerId.Value,
                slotIndex,
                0,
                0,
                traceId,
                sequence);
        }

        public static bool TryDecode(RuntimeCommand command, out InventoryNetworkCommand request)
        {
            return TryDecode(command, out request, out InventoryNetworkCommandDecodeStatus status)
                && status == InventoryNetworkCommandDecodeStatus.Success;
        }

        public static bool TryDecode(
            RuntimeCommand command,
            out InventoryNetworkCommand request,
            out InventoryNetworkCommandDecodeStatus status)
        {
            request = default;
            status = InventoryNetworkCommandDecodeStatus.UnsupportedCommand;
            switch (command.CommandId)
            {
                case ItemNetworkCommandIds.AddStack:
                    if (command.TargetId <= 0 || !IsEncodableSlot(command.Payload0) || command.Payload1 <= 0 || command.Payload2 <= 0)
                    {
                        status = InventoryNetworkCommandDecodeStatus.InvalidPayload;
                        return false;
                    }

                    request = new InventoryNetworkCommand(
                        InventoryNetworkCommandKind.AddStack,
                        new InventoryContainerId(command.TargetId),
                        command.Payload0,
                        itemDefinitionId: new ItemDefinitionId(command.Payload1),
                        count: command.Payload2);
                    status = InventoryNetworkCommandDecodeStatus.Success;
                    return true;

                case ItemNetworkCommandIds.RemoveStack:
                    if (command.TargetId <= 0 || !IsEncodableSlot(command.Payload0) || command.Payload1 <= 0 || command.Payload2 != 0)
                    {
                        status = InventoryNetworkCommandDecodeStatus.InvalidPayload;
                        return false;
                    }

                    request = new InventoryNetworkCommand(
                        InventoryNetworkCommandKind.RemoveStack,
                        new InventoryContainerId(command.TargetId),
                        command.Payload0,
                        count: command.Payload1);
                    status = InventoryNetworkCommandDecodeStatus.Success;
                    return true;

                case ItemNetworkCommandIds.MoveStack:
                    return TryDecodeTwoSlotCommand(command, InventoryNetworkCommandKind.MoveStack, requiresCount: true, out request, out status);
                case ItemNetworkCommandIds.SplitStack:
                    return TryDecodeTwoSlotCommand(command, InventoryNetworkCommandKind.SplitStack, requiresCount: true, out request, out status);
                case ItemNetworkCommandIds.MergeStack:
                    return TryDecodeTwoSlotCommand(command, InventoryNetworkCommandKind.MergeStack, requiresCount: false, out request, out status);
                case ItemNetworkCommandIds.SwapSlots:
                    return TryDecodeTwoSlotCommand(command, InventoryNetworkCommandKind.SwapSlots, requiresCount: false, out request, out status);

                case ItemNetworkCommandIds.UseItem:
                    if (command.TargetId <= 0 || !IsEncodableSlot(command.Payload0) || command.Payload1 != 0 || command.Payload2 != 0)
                    {
                        status = InventoryNetworkCommandDecodeStatus.InvalidPayload;
                        return false;
                    }

                    request = new InventoryNetworkCommand(
                        InventoryNetworkCommandKind.UseItem,
                        new InventoryContainerId(command.TargetId),
                        command.Payload0);
                    status = InventoryNetworkCommandDecodeStatus.Success;
                    return true;
            }

            return false;
        }

        /// <remarks>NoAlloc after initialization; invalid null service calls may allocate exception diagnostics.</remarks>
        public static InventoryOperationResult Execute(InventoryService service, InventoryNetworkCommand request)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            switch (request.Kind)
            {
                case InventoryNetworkCommandKind.AddStack:
                    return service.AddStack(
                        request.SourceContainerId,
                        request.SourceSlotIndex,
                        new ItemStack(request.ItemDefinitionId, request.Count));
                case InventoryNetworkCommandKind.RemoveStack:
                    return service.RemoveStack(request.SourceContainerId, request.SourceSlotIndex, request.Count);
                case InventoryNetworkCommandKind.MoveStack:
                    return service.MoveStack(
                        request.SourceContainerId,
                        request.SourceSlotIndex,
                        request.DestinationContainerId,
                        request.DestinationSlotIndex,
                        request.Count);
                case InventoryNetworkCommandKind.SplitStack:
                    return service.SplitStack(
                        request.SourceContainerId,
                        request.SourceSlotIndex,
                        request.DestinationContainerId,
                        request.DestinationSlotIndex,
                        request.Count);
                case InventoryNetworkCommandKind.MergeStack:
                    return service.MergeStack(
                        request.SourceContainerId,
                        request.SourceSlotIndex,
                        request.DestinationContainerId,
                        request.DestinationSlotIndex);
                case InventoryNetworkCommandKind.SwapSlots:
                    return service.SwapSlots(
                        request.SourceContainerId,
                        request.SourceSlotIndex,
                        request.DestinationContainerId,
                        request.DestinationSlotIndex);
                case InventoryNetworkCommandKind.UseItem:
                    return service.UseItem(request.SourceContainerId, request.SourceSlotIndex);
                default:
                    return InventoryOperationResult.Failed(InventoryOperationStatus.StaleRequest);
            }
        }

        public static bool TryExecute(
            InventoryService service,
            RuntimeCommand command,
            out InventoryOperationResult result,
            out InventoryNetworkCommandDecodeStatus decodeStatus)
        {
            if (!TryDecode(command, out InventoryNetworkCommand request, out decodeStatus))
            {
                result = InventoryOperationResult.Failed(
                    decodeStatus == InventoryNetworkCommandDecodeStatus.InvalidPayload
                        ? InventoryOperationStatus.InvalidItem
                        : InventoryOperationStatus.StaleRequest);
                return false;
            }

            result = Execute(service, request);
            return true;
        }

        private static RuntimeCommand CreateTwoSlotCommand(
            RuntimeFrame frame,
            int sourceId,
            int commandId,
            InventoryContainerId sourceContainerId,
            int sourceSlotIndex,
            InventoryContainerId destinationContainerId,
            int destinationSlotIndex,
            int count,
            RuntimeTraceIdentity traceId,
            long sequence)
        {
            ValidateContainer(sourceContainerId, nameof(sourceContainerId));
            ValidateContainer(destinationContainerId, nameof(destinationContainerId));
            ValidateSlot(sourceSlotIndex, nameof(sourceSlotIndex));
            ValidateSlot(destinationSlotIndex, nameof(destinationSlotIndex));
            if ((commandId == ItemNetworkCommandIds.MoveStack || commandId == ItemNetworkCommandIds.SplitStack) && count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Inventory network command count must be positive.");
            if ((commandId == ItemNetworkCommandIds.MergeStack || commandId == ItemNetworkCommandIds.SwapSlots) && count != 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Inventory network command count must be zero.");

            return new RuntimeCommand(
                frame,
                sourceId,
                commandId,
                sourceContainerId.Value,
                PackSlotPair(sourceSlotIndex, destinationSlotIndex),
                destinationContainerId.Value,
                count,
                traceId,
                sequence);
        }

        private static bool TryDecodeTwoSlotCommand(
            RuntimeCommand command,
            InventoryNetworkCommandKind kind,
            bool requiresCount,
            out InventoryNetworkCommand request,
            out InventoryNetworkCommandDecodeStatus status)
        {
            request = default;
            if (command.TargetId <= 0 || command.Payload1 <= 0)
            {
                status = InventoryNetworkCommandDecodeStatus.InvalidPayload;
                return false;
            }

            if (requiresCount && command.Payload2 <= 0)
            {
                status = InventoryNetworkCommandDecodeStatus.InvalidPayload;
                return false;
            }

            if (!requiresCount && command.Payload2 != 0)
            {
                status = InventoryNetworkCommandDecodeStatus.InvalidPayload;
                return false;
            }

            if (!TryUnpackSlotPair(command.Payload0, out int sourceSlotIndex, out int destinationSlotIndex))
            {
                status = InventoryNetworkCommandDecodeStatus.InvalidPayload;
                return false;
            }

            request = new InventoryNetworkCommand(
                kind,
                new InventoryContainerId(command.TargetId),
                sourceSlotIndex,
                new InventoryContainerId(command.Payload1),
                destinationSlotIndex,
                count: command.Payload2);
            status = InventoryNetworkCommandDecodeStatus.Success;
            return true;
        }

        private static int PackSlotPair(int sourceSlotIndex, int destinationSlotIndex)
        {
            return sourceSlotIndex | (destinationSlotIndex << 16);
        }

        private static bool TryUnpackSlotPair(int packed, out int sourceSlotIndex, out int destinationSlotIndex)
        {
            sourceSlotIndex = packed & SlotMask;
            destinationSlotIndex = (packed >> 16) & SlotMask;
            return IsEncodableSlot(sourceSlotIndex) && IsEncodableSlot(destinationSlotIndex);
        }

        private static bool IsEncodableSlot(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex <= MaxSlotIndex;
        }

        private static void ValidateContainer(InventoryContainerId containerId, string parameterName)
        {
            if (!containerId.IsValid)
                throw new ArgumentOutOfRangeException(parameterName, "Inventory container id must be positive.");
        }

        private static void ValidateSlot(int slotIndex, string parameterName)
        {
            if (!IsEncodableSlot(slotIndex))
                throw new ArgumentOutOfRangeException(parameterName, "Inventory network slot index must fit in 16 bits.");
        }

        private static void ValidateItem(ItemDefinitionId itemDefinitionId, string parameterName)
        {
            if (!itemDefinitionId.IsValid)
                throw new ArgumentOutOfRangeException(parameterName, "Item definition id must be positive.");
        }

        private static void ValidateCount(int count, string parameterName)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(parameterName, "Inventory stack count must be positive.");
        }
    }

    /// <summary>Inventory network ownership and mutation authority check.</summary>
    /// <remarks>Allocation: implementations should make <see cref="CanAccessContainer"/> and <see cref="CanCreateOrDestroyStacks"/> NoAlloc after setup.</remarks>
    public interface IInventoryNetworkAuthority
    {
        bool CanAccessContainer(string peerId, InventoryContainerId containerId);
        bool CanCreateOrDestroyStacks(string peerId);
    }

    /// <summary>Default owner-based inventory network authority.</summary>
    /// <remarks>Allocation: peer registration may allocate; authorization checks are NoAlloc after dictionary/hash-set capacity is established.</remarks>
    public sealed class InventoryOwnerNetworkAuthority : IInventoryNetworkAuthority
    {
        private readonly InventoryStore _store;
        private readonly Dictionary<string, InventoryOwnerId> _peerOwners =
            new Dictionary<string, InventoryOwnerId>(StringComparer.Ordinal);
        private readonly HashSet<string> _stackMutationPeers = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _allContainerPeers = new HashSet<string>(StringComparer.Ordinal);

        public InventoryOwnerNetworkAuthority(InventoryStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public void SetOwnerForPeer(string peerId, InventoryOwnerId ownerId)
        {
            if (string.IsNullOrEmpty(peerId))
                throw new ArgumentException("Peer id cannot be null or empty.", nameof(peerId));
            if (!ownerId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(ownerId), "Inventory owner id must be valid.");

            _peerOwners[peerId] = ownerId;
        }

        public bool RemovePeer(string peerId)
        {
            bool removed = _peerOwners.Remove(peerId ?? string.Empty);
            _stackMutationPeers.Remove(peerId ?? string.Empty);
            _allContainerPeers.Remove(peerId ?? string.Empty);
            return removed;
        }

        public void GrantStackMutationAuthority(string peerId)
        {
            AddPeerId(_stackMutationPeers, peerId);
        }

        public void RevokeStackMutationAuthority(string peerId)
        {
            _stackMutationPeers.Remove(peerId ?? string.Empty);
        }

        public void GrantAllContainerAccess(string peerId)
        {
            AddPeerId(_allContainerPeers, peerId);
        }

        public void RevokeAllContainerAccess(string peerId)
        {
            _allContainerPeers.Remove(peerId ?? string.Empty);
        }

        public bool CanAccessContainer(string peerId, InventoryContainerId containerId)
        {
            peerId = peerId ?? string.Empty;
            if (!containerId.IsValid)
                return false;
            if (_allContainerPeers.Contains(peerId))
                return _store.TryGet(containerId, out _);
            if (!_peerOwners.TryGetValue(peerId, out InventoryOwnerId ownerId))
                return false;
            return _store.TryGet(containerId, out InventoryContainer container)
                && container.OwnerId.Equals(ownerId);
        }

        public bool CanCreateOrDestroyStacks(string peerId)
        {
            return _stackMutationPeers.Contains(peerId ?? string.Empty);
        }

        private static void AddPeerId(HashSet<string> set, string peerId)
        {
            if (string.IsNullOrEmpty(peerId))
                throw new ArgumentException("Peer id cannot be null or empty.", nameof(peerId));

            set.Add(peerId);
        }
    }

    /// <summary>Adapts inventory authority rules to the Network command-authorizer hook.</summary>
    /// <remarks>Allocation: authorization is NoAlloc after authority setup; rejected results reuse constant reason strings.</remarks>
    public sealed class InventoryNetworkCommandAuthorizer
    {
        private const string InvalidPayloadReason = "invalid inventory network command payload";
        private const string AddRemoveDeniedReason = "inventory add/remove requires server authority";
        private const string SourceDeniedReason = "inventory source container is not owned by peer";
        private const string DestinationDeniedReason = "inventory destination container is not owned by peer";

        private readonly IInventoryNetworkAuthority _authority;

        public InventoryNetworkCommandAuthorizer(IInventoryNetworkAuthority authority)
        {
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        }

        public NetworkCommandAuthorizationResult Authorize(string peerId, RuntimeCommand command)
        {
            if (!ItemNetworkCommandIds.IsInventoryNetworkCommand(command.CommandId))
                return NetworkCommandAuthorizationResult.Accept();

            if (!InventoryNetworkCommandCodec.TryDecode(command, out InventoryNetworkCommand request, out InventoryNetworkCommandDecodeStatus status))
            {
                return status == InventoryNetworkCommandDecodeStatus.UnsupportedCommand
                    ? NetworkCommandAuthorizationResult.Accept()
                    : NetworkCommandAuthorizationResult.Reject(InvalidPayloadReason);
            }

            switch (request.Kind)
            {
                case InventoryNetworkCommandKind.AddStack:
                case InventoryNetworkCommandKind.RemoveStack:
                    if (!_authority.CanCreateOrDestroyStacks(peerId))
                        return NetworkCommandAuthorizationResult.Reject(AddRemoveDeniedReason);
                    if (!_authority.CanAccessContainer(peerId, request.SourceContainerId))
                        return NetworkCommandAuthorizationResult.Reject(SourceDeniedReason);
                    return NetworkCommandAuthorizationResult.Accept();

                case InventoryNetworkCommandKind.MoveStack:
                case InventoryNetworkCommandKind.SplitStack:
                case InventoryNetworkCommandKind.MergeStack:
                case InventoryNetworkCommandKind.SwapSlots:
                    if (!_authority.CanAccessContainer(peerId, request.SourceContainerId))
                        return NetworkCommandAuthorizationResult.Reject(SourceDeniedReason);
                    if (!_authority.CanAccessContainer(peerId, request.DestinationContainerId))
                        return NetworkCommandAuthorizationResult.Reject(DestinationDeniedReason);
                    return NetworkCommandAuthorizationResult.Accept();

                case InventoryNetworkCommandKind.UseItem:
                    return _authority.CanAccessContainer(peerId, request.SourceContainerId)
                        ? NetworkCommandAuthorizationResult.Accept()
                        : NetworkCommandAuthorizationResult.Reject(SourceDeniedReason);

                default:
                    return NetworkCommandAuthorizationResult.Reject(InvalidPayloadReason);
            }
        }
    }

    /// <summary>Full inventory snapshot participant for network bootstrap and resync.</summary>
    /// <remarks>Allocation: capture/apply are AllocByDesign because they build DTO arrays and JSON payloads; do not use as routine high-frequency sync.</remarks>
    public sealed class InventoryNetworkSnapshotParticipant : INetworkSnapshotParticipant
    {
        public const int CustomStateSchemaVersion = InventoryNetworkSnapshotState.CurrentSchemaVersion;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include
        };

        private readonly InventoryStore _store;
        private readonly ItemDefinitionRegistry _definitions;

        public InventoryNetworkSnapshotParticipant(
            InventoryStore store,
            ItemDefinitionRegistry definitions,
            string participantId = ItemRuntimeIds.InventoryModule)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            ParticipantId = participantId ?? string.Empty;
        }

        public string ParticipantId { get; }

        public RuntimeCustomState CaptureSnapshot(RuntimeFrame frame)
        {
            InventoryNetworkSnapshotState state = InventoryNetworkStateCodec.Capture(_store, _definitions);
            string payloadJson = JsonConvert.SerializeObject(state, JsonSettings);
            return new RuntimeCustomState(3302, ParticipantId, CustomStateSchemaVersion, payloadJson);
        }

        public NetworkSnapshotApplyResult ApplySnapshot(RuntimeFrame frame, RuntimeCustomState snapshot)
        {
            if (snapshot == null)
                return NetworkSnapshotApplyResult.Failed("Inventory network snapshot is null.");
            if (!string.Equals(snapshot.TypeId, ParticipantId, StringComparison.Ordinal))
                return NetworkSnapshotApplyResult.Failed("Inventory network snapshot participant mismatch.");
            if (snapshot.SchemaVersion != CustomStateSchemaVersion && snapshot.SchemaVersion != 2)
                return NetworkSnapshotApplyResult.Failed("Unsupported inventory network snapshot schema.");

            InventoryNetworkSnapshotState state;
            try
            {
                state = JsonConvert.DeserializeObject<InventoryNetworkSnapshotState>(snapshot.PayloadJson, JsonSettings);
            }
            catch (Exception ex)
            {
                return NetworkSnapshotApplyResult.Failed("Inventory network snapshot payload could not be parsed: " + ex.Message);
            }

            if (state != null && state.SchemaVersion != snapshot.SchemaVersion)
                return NetworkSnapshotApplyResult.Failed("Inventory snapshot envelope/payload schema mismatch.");
            return InventoryNetworkStateCodec.Restore(_store, _definitions, state, out string failure)
                ? NetworkSnapshotApplyResult.Succeeded()
                : NetworkSnapshotApplyResult.Failed(failure);
        }
    }

    /// <summary>Inventory rollback participant used by predicted clients.</summary>
    /// <remarks>
    /// Allocation: capture is NoAlloc after warmup while the container/slot layout fits the configured state capacity.
    /// Restore is AllocByDesign because it rebuilds inventory containers through public runtime APIs.
    /// </remarks>
    public sealed class InventoryRollbackParticipant : INetworkRollbackParticipant
    {
        private readonly InventoryStore _store;
        private readonly ItemDefinitionRegistry _definitions;
        private readonly InventoryRollbackState[] _states;
        private readonly long _definitionHash;
        private int _nextStateIndex;

        public InventoryRollbackParticipant(
            InventoryStore store,
            ItemDefinitionRegistry definitions,
            string participantId = ItemRuntimeIds.InventoryModule,
            int historyCapacity = 128)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _definitionHash = _definitions.ComputeStableHash();
            ParticipantId = participantId ?? string.Empty;
            if (historyCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(historyCapacity), "Inventory rollback history capacity must be positive.");

            int containerCapacity;
            int slotCapacity;
            InventoryNetworkStateCodec.CountStoreShape(_store, out containerCapacity, out slotCapacity);
            _states = new InventoryRollbackState[historyCapacity];
            for (int i = 0; i < _states.Length; i++)
                _states[i] = new InventoryRollbackState(containerCapacity, slotCapacity);
        }

        public string ParticipantId { get; }

        public int HistoryCapacity => _states.Length;

        public object CaptureRollbackState(RuntimeFrame frame)
        {
            InventoryRollbackState state = _states[_nextStateIndex];
            _nextStateIndex = (_nextStateIndex + 1) % _states.Length;
            InventoryNetworkStateCodec.Capture(_store, _definitionHash, state);
            return state;
        }

        public bool RestoreRollbackState(RuntimeFrame frame, object state, out string failure)
        {
            if (!(state is InventoryRollbackState snapshot))
            {
                failure = "Inventory rollback state type mismatch.";
                return false;
            }

            return InventoryNetworkStateCodec.Restore(_store, _definitions, snapshot, out failure);
        }
    }

    /// <summary>Reusable in-memory inventory rollback state.</summary>
    /// <remarks>
    /// Allocation: constructed with fixed capacities; capture is NoAlloc while container and slot counts fit those capacities.
    /// If the inventory shape grows, capacity growth is LowFreqAlloc and should be avoided by sizing from the expected rollback history shape.
    /// </remarks>
    public sealed class InventoryRollbackState
    {
        private InventoryRollbackContainerState[] _containers;
        private InventoryRollbackSlotState[] _slots;

        public InventoryRollbackState(int containerCapacity, int slotCapacity)
        {
            if (containerCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(containerCapacity));
            if (slotCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(slotCapacity));

            _containers = containerCapacity == 0
                ? Array.Empty<InventoryRollbackContainerState>()
                : new InventoryRollbackContainerState[containerCapacity];
            _slots = slotCapacity == 0
                ? Array.Empty<InventoryRollbackSlotState>()
                : new InventoryRollbackSlotState[slotCapacity];
        }

        public int SchemaVersion { get; internal set; }
        public long DefinitionHash { get; internal set; }
        public long NextItemInstanceId { get; internal set; }
        public int ContainerCount { get; internal set; }
        public int SlotCount { get; internal set; }
        public int ContainerCapacity => _containers.Length;
        public int SlotCapacity => _slots.Length;

        /// <remarks>NoAlloc for valid indices; invalid indices may allocate exception diagnostics.</remarks>
        public InventoryRollbackContainerState GetContainer(int index)
        {
            if (index < 0 || index >= ContainerCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _containers[index];
        }

        /// <remarks>NoAlloc for valid indices; invalid indices may allocate exception diagnostics.</remarks>
        public InventoryRollbackSlotState GetSlot(int index)
        {
            if (index < 0 || index >= SlotCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _slots[index];
        }

        internal InventoryRollbackContainerState GetContainerUnchecked(int index)
        {
            return _containers[index];
        }

        internal InventoryRollbackSlotState GetSlotUnchecked(int index)
        {
            return _slots[index];
        }

        internal void EnsureCapacity(int containerCapacity, int slotCapacity)
        {
            if (_containers.Length < containerCapacity)
                Array.Resize(ref _containers, containerCapacity);
            if (_slots.Length < slotCapacity)
                Array.Resize(ref _slots, slotCapacity);
        }

        internal void SetContainer(int index, InventoryRollbackContainerState container)
        {
            _containers[index] = container;
        }

        internal void SetSlot(int index, InventoryRollbackSlotState slot)
        {
            _slots[index] = slot;
        }
    }

    /// <summary>Container entry in a reusable inventory rollback state.</summary>
    /// <remarks>NoAlloc value type; strings are stable id references copied from runtime state.</remarks>
    public readonly struct InventoryRollbackContainerState
    {
        public InventoryRollbackContainerState(
            int containerId,
            string stableId,
            int ownerId,
            int slotCount,
            int firstStackIndex,
            int stackCount)
        {
            ContainerId = containerId;
            StableId = stableId ?? string.Empty;
            OwnerId = ownerId;
            SlotCount = slotCount;
            FirstStackIndex = firstStackIndex;
            StackCount = stackCount;
        }

        public int ContainerId { get; }
        public string StableId { get; }
        public int OwnerId { get; }
        public int SlotCount { get; }
        public int FirstStackIndex { get; }
        public int StackCount { get; }
    }

    /// <summary>Non-empty slot entry in a reusable inventory rollback state.</summary>
    /// <remarks>NoAlloc value type; custom state envelope fields are copied as references.</remarks>
    public readonly struct InventoryRollbackSlotState
    {
        public InventoryRollbackSlotState(
            int slotIndex,
            int itemDefinitionId,
            int count,
            int itemInstanceId,
            int customStateNumericTypeId,
            string customStateTypeId,
            int customStateSchemaVersion,
            string customStatePayloadJson)
        {
            SlotIndex = slotIndex;
            ItemDefinitionId = itemDefinitionId;
            Count = count;
            ItemInstanceId = itemInstanceId;
            CustomStateNumericTypeId = customStateNumericTypeId;
            CustomStateTypeId = customStateTypeId ?? string.Empty;
            CustomStateSchemaVersion = customStateSchemaVersion;
            CustomStatePayloadJson = customStatePayloadJson ?? string.Empty;
        }

        public int SlotIndex { get; }
        public int ItemDefinitionId { get; }
        public int Count { get; }
        public int ItemInstanceId { get; }
        public int CustomStateNumericTypeId { get; }
        public string CustomStateTypeId { get; }
        public int CustomStateSchemaVersion { get; }
        public string CustomStatePayloadJson { get; }
    }

    /// <summary>DTO carried inside the inventory network full snapshot custom state.</summary>
    /// <remarks>Allocation: DTO is AllocByDesign for bootstrap/resync snapshots and rollback memory capture.</remarks>
    public sealed class InventoryNetworkSnapshotState
    {
        public const int CurrentSchemaVersion = 3;
        public int SchemaVersion { get; set; }
        public long DefinitionHash { get; set; }
        public long NextItemInstanceId { get; set; }
        public InventoryNetworkContainerSnapshot[] Containers { get; set; }
    }

    /// <summary>DTO for one inventory container in a network full snapshot.</summary>
    public sealed class InventoryNetworkContainerSnapshot
    {
        public int ContainerId { get; set; }
        public string StableId { get; set; }
        public int OwnerId { get; set; }
        public int SlotCount { get; set; }
        public InventoryNetworkSlotSnapshot[] Slots { get; set; }
    }

    /// <summary>DTO for one non-empty inventory slot in a network full snapshot.</summary>
    public sealed class InventoryNetworkSlotSnapshot
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

    internal static class InventoryNetworkStateCodec
    {
        public static void CountStoreShape(InventoryStore store, out int containerCount, out int slotCapacity)
        {
            containerCount = store.Count;
            slotCapacity = 0;
            for (int i = 0; i < store.Count; i++)
                slotCapacity += store.GetContainerAtSortedIndex(i).SlotCount;
        }

        public static InventoryNetworkSnapshotState Capture(InventoryStore store, ItemDefinitionRegistry definitions)
        {
            var containers = new InventoryNetworkContainerSnapshot[store.Count];
            for (int i = 0; i < store.Count; i++)
            {
                InventoryContainer container = store.GetContainerAtSortedIndex(i);
                int nonEmptyCount = 0;
                for (int slotIndex = 0; slotIndex < container.SlotCount; slotIndex++)
                {
                    if (!container.GetStack(slotIndex).IsEmpty)
                        nonEmptyCount++;
                }

                var slots = new InventoryNetworkSlotSnapshot[nonEmptyCount];
                int slotWriteIndex = 0;
                for (int slotIndex = 0; slotIndex < container.SlotCount; slotIndex++)
                {
                    ItemStack stack = container.GetStack(slotIndex);
                    if (stack.IsEmpty)
                        continue;

                    slots[slotWriteIndex++] = new InventoryNetworkSlotSnapshot
                    {
                        SlotIndex = slotIndex,
                        ItemDefinitionId = stack.DefinitionId.Value,
                        Count = stack.Count,
                        ItemInstanceId = stack.InstanceId.Value,
                        CustomStateNumericTypeId = stack.CustomState == null ? 0 : stack.CustomState.NumericTypeId,
                        CustomStateTypeId = stack.CustomState == null ? string.Empty : stack.CustomState.TypeId,
                        CustomStateSchemaVersion = stack.CustomState == null ? 0 : stack.CustomState.SchemaVersion,
                        CustomStatePayloadJson = stack.CustomState == null ? string.Empty : stack.CustomState.PayloadJson
                    };
                }

                containers[i] = new InventoryNetworkContainerSnapshot
                {
                    ContainerId = container.ContainerId.Value,
                    StableId = container.StableId,
                    OwnerId = container.OwnerId.Value,
                    SlotCount = container.SlotCount,
                    Slots = slots
                };
            }

            return new InventoryNetworkSnapshotState
            {
                SchemaVersion = InventoryNetworkSnapshotState.CurrentSchemaVersion,
                DefinitionHash = definitions.ComputeStableHash(),
                NextItemInstanceId = store.NextItemInstanceId,
                Containers = containers
            };
        }

        public static void Capture(InventoryStore store, long definitionHash, InventoryRollbackState target)
        {
            CountStoreShape(store, out int containerCount, out int slotCapacity);
            target.EnsureCapacity(containerCount, slotCapacity);
            target.SchemaVersion = InventoryNetworkSnapshotState.CurrentSchemaVersion;
            target.DefinitionHash = definitionHash;
            target.NextItemInstanceId = store.NextItemInstanceId;
            target.ContainerCount = containerCount;

            int stackWriteIndex = 0;
            for (int i = 0; i < store.Count; i++)
            {
                InventoryContainer container = store.GetContainerAtSortedIndex(i);
                int firstStackIndex = stackWriteIndex;
                for (int slotIndex = 0; slotIndex < container.SlotCount; slotIndex++)
                {
                    ItemStack stack = container.GetStack(slotIndex);
                    if (stack.IsEmpty)
                        continue;

                    target.SetSlot(
                        stackWriteIndex++,
                        new InventoryRollbackSlotState(
                            slotIndex,
                            stack.DefinitionId.Value,
                            stack.Count,
                            stack.InstanceId.Value,
                            stack.CustomState == null ? 0 : stack.CustomState.NumericTypeId,
                            stack.CustomState == null ? string.Empty : stack.CustomState.TypeId,
                            stack.CustomState == null ? 0 : stack.CustomState.SchemaVersion,
                            stack.CustomState == null ? string.Empty : stack.CustomState.PayloadJson));
                }

                target.SetContainer(
                    i,
                    new InventoryRollbackContainerState(
                        container.ContainerId.Value,
                        container.StableId,
                        container.OwnerId.Value,
                        container.SlotCount,
                        firstStackIndex,
                        stackWriteIndex - firstStackIndex));
            }

            target.SlotCount = stackWriteIndex;
        }

        public static bool Restore(
            InventoryStore store,
            ItemDefinitionRegistry definitions,
            InventoryNetworkSnapshotState state,
            out string failure)
        {
            if (!Validate(definitions, state, out failure))
                return false;

            var restoredStore = new InventoryStore();
            InventoryNetworkContainerSnapshot[] containers = state.Containers ?? Array.Empty<InventoryNetworkContainerSnapshot>();
            for (int i = 0; i < containers.Length; i++)
            {
                InventoryNetworkContainerSnapshot source = containers[i];
                var container = new InventoryContainer(
                    new InventoryContainerId(source.ContainerId),
                    new InventoryOwnerId(source.OwnerId),
                    source.SlotCount,
                    source.StableId);
                if (!restoredStore.Register(container))
                {
                    failure = "Inventory network snapshot duplicate container id.";
                    return false;
                }
            }

            var service = new InventoryService(definitions, restoredStore);
            for (int i = 0; i < containers.Length; i++)
            {
                InventoryNetworkContainerSnapshot source = containers[i];
                InventoryNetworkSlotSnapshot[] slots = source.Slots ?? Array.Empty<InventoryNetworkSlotSnapshot>();
                for (int j = 0; j < slots.Length; j++)
                {
                    InventoryNetworkSlotSnapshot slot = slots[j];
                    RuntimeCustomState customState = string.IsNullOrEmpty(slot.CustomStateTypeId)
                        ? null
                        : new RuntimeCustomState(
                            slot.CustomStateNumericTypeId,
                            slot.CustomStateTypeId,
                            slot.CustomStateSchemaVersion,
                            slot.CustomStatePayloadJson);
                    InventoryOperationResult result = service.AddStack(
                        new InventoryContainerId(source.ContainerId),
                        slot.SlotIndex,
                        new ItemStack(
                            new ItemDefinitionId(slot.ItemDefinitionId),
                            slot.Count,
                            new ItemInstanceId(slot.ItemInstanceId),
                            customState));
                    if (!result.Success)
                    {
                        failure = "Inventory network snapshot restore failed: " + result.Status;
                        return false;
                    }
                }
            }

            if (state.SchemaVersion == 3 && !restoredStore.TryRestoreInstanceCounter(state.NextItemInstanceId))
            {
                failure = "Inventory instance counter is invalid.";
                return false;
            }
            store.Clear();
            for (int i = 0; i < restoredStore.Count; i++)
                store.Register(restoredStore.GetContainerAtSortedIndex(i));
            store.TryRestoreInstanceCounter(restoredStore.NextItemInstanceId);

            failure = string.Empty;
            return true;
        }

        public static bool Restore(
            InventoryStore store,
            ItemDefinitionRegistry definitions,
            InventoryRollbackState state,
            out string failure)
        {
            if (!Validate(definitions, state, out failure))
                return false;

            var restoredStore = new InventoryStore();
            for (int i = 0; i < state.ContainerCount; i++)
            {
                InventoryRollbackContainerState source = state.GetContainerUnchecked(i);
                var container = new InventoryContainer(
                    new InventoryContainerId(source.ContainerId),
                    new InventoryOwnerId(source.OwnerId),
                    source.SlotCount,
                    source.StableId);
                if (!restoredStore.Register(container))
                {
                    failure = "Inventory rollback state duplicate container id.";
                    return false;
                }
            }

            var service = new InventoryService(definitions, restoredStore);
            for (int i = 0; i < state.ContainerCount; i++)
            {
                InventoryRollbackContainerState source = state.GetContainerUnchecked(i);
                int endSlotIndex = source.FirstStackIndex + source.StackCount;
                for (int j = source.FirstStackIndex; j < endSlotIndex; j++)
                {
                    InventoryRollbackSlotState slot = state.GetSlotUnchecked(j);
                    RuntimeCustomState customState = string.IsNullOrEmpty(slot.CustomStateTypeId)
                        ? null
                        : new RuntimeCustomState(
                            slot.CustomStateNumericTypeId,
                            slot.CustomStateTypeId,
                            slot.CustomStateSchemaVersion,
                            slot.CustomStatePayloadJson);
                    InventoryOperationResult result = service.AddStack(
                        new InventoryContainerId(source.ContainerId),
                        slot.SlotIndex,
                        new ItemStack(
                            new ItemDefinitionId(slot.ItemDefinitionId),
                            slot.Count,
                            new ItemInstanceId(slot.ItemInstanceId),
                            customState));
                    if (!result.Success)
                    {
                        failure = "Inventory rollback state restore failed: " + result.Status;
                        return false;
                    }
                }
            }

            if (!restoredStore.TryRestoreInstanceCounter(state.NextItemInstanceId))
            {
                failure = "Inventory rollback instance counter is invalid.";
                return false;
            }
            store.Clear();
            for (int i = 0; i < restoredStore.Count; i++)
                store.Register(restoredStore.GetContainerAtSortedIndex(i));
            store.TryRestoreInstanceCounter(restoredStore.NextItemInstanceId);

            failure = string.Empty;
            return true;
        }

        private static bool Validate(
            ItemDefinitionRegistry definitions,
            InventoryNetworkSnapshotState state,
            out string failure)
        {
            if (state == null)
            {
                failure = "Inventory network snapshot payload is empty.";
                return false;
            }

            if (state.SchemaVersion != InventoryNetworkSnapshotState.CurrentSchemaVersion && state.SchemaVersion != 2)
            {
                failure = "Unsupported inventory network snapshot payload schema.";
                return false;
            }

            if (state.DefinitionHash != definitions.ComputeStableHash())
            {
                failure = "Inventory definition hash mismatch.";
                return false;
            }

            var containerIds = new HashSet<int>();
            InventoryNetworkContainerSnapshot[] containers = state.Containers ?? Array.Empty<InventoryNetworkContainerSnapshot>();
            for (int i = 0; i < containers.Length; i++)
            {
                InventoryNetworkContainerSnapshot container = containers[i];
                if (container == null || container.ContainerId <= 0)
                {
                    failure = "Inventory network snapshot container id is invalid.";
                    return false;
                }

                if (container.SlotCount < 0)
                {
                    failure = "Inventory network snapshot slot count is invalid.";
                    return false;
                }

                if (!containerIds.Add(container.ContainerId))
                {
                    failure = "Inventory network snapshot duplicate container id.";
                    return false;
                }

                var slotIndices = new HashSet<int>();
                InventoryNetworkSlotSnapshot[] slots = container.Slots ?? Array.Empty<InventoryNetworkSlotSnapshot>();
                for (int j = 0; j < slots.Length; j++)
                {
                    InventoryNetworkSlotSnapshot slot = slots[j];
                    if (slot == null)
                    {
                        failure = "Inventory network snapshot slot is null.";
                        return false;
                    }

                    if (slot.SlotIndex < 0 || slot.SlotIndex >= container.SlotCount)
                    {
                        failure = "Inventory network snapshot slot index is invalid.";
                        return false;
                    }

                    if (!slotIndices.Add(slot.SlotIndex))
                    {
                        failure = "Inventory network snapshot duplicate slot index.";
                        return false;
                    }

                    if (slot.ItemDefinitionId <= 0 || slot.Count <= 0)
                    {
                        failure = "Inventory network snapshot stack is invalid.";
                        return false;
                    }

                    if (!definitions.TryGet(new ItemDefinitionId(slot.ItemDefinitionId), out ItemDefinition definition))
                    {
                        failure = "Inventory network snapshot item definition is missing.";
                        return false;
                    }

                    if (slot.Count > definition.MaxStackCount)
                    {
                        failure = "Inventory network snapshot stack overflows definition.";
                        return false;
                    }

                    if (slot.ItemInstanceId > 0 && slot.Count != 1)
                    {
                        failure = "Inventory network snapshot instance stack count is invalid.";
                        return false;
                    }
                }
            }

            failure = string.Empty;
            return true;
        }

        private static bool Validate(
            ItemDefinitionRegistry definitions,
            InventoryRollbackState state,
            out string failure)
        {
            if (state == null)
            {
                failure = "Inventory rollback state is null.";
                return false;
            }

            if (state.SchemaVersion != InventoryNetworkSnapshotState.CurrentSchemaVersion)
            {
                failure = "Unsupported inventory rollback state schema.";
                return false;
            }

            if (state.DefinitionHash != definitions.ComputeStableHash())
            {
                failure = "Inventory definition hash mismatch.";
                return false;
            }

            if (state.ContainerCount < 0 || state.ContainerCount > state.ContainerCapacity)
            {
                failure = "Inventory rollback container count is invalid.";
                return false;
            }

            if (state.SlotCount < 0 || state.SlotCount > state.SlotCapacity)
            {
                failure = "Inventory rollback slot count is invalid.";
                return false;
            }

            var containerIds = new HashSet<int>();
            for (int i = 0; i < state.ContainerCount; i++)
            {
                InventoryRollbackContainerState container = state.GetContainerUnchecked(i);
                if (container.ContainerId <= 0)
                {
                    failure = "Inventory rollback container id is invalid.";
                    return false;
                }

                if (container.SlotCount < 0)
                {
                    failure = "Inventory rollback slot count is invalid.";
                    return false;
                }

                if (container.FirstStackIndex < 0 || container.StackCount < 0 || container.FirstStackIndex + container.StackCount > state.SlotCount)
                {
                    failure = "Inventory rollback stack range is invalid.";
                    return false;
                }

                if (!containerIds.Add(container.ContainerId))
                {
                    failure = "Inventory rollback duplicate container id.";
                    return false;
                }

                var slotIndices = new HashSet<int>();
                int endSlotIndex = container.FirstStackIndex + container.StackCount;
                for (int j = container.FirstStackIndex; j < endSlotIndex; j++)
                {
                    InventoryRollbackSlotState slot = state.GetSlotUnchecked(j);
                    if (slot.SlotIndex < 0 || slot.SlotIndex >= container.SlotCount)
                    {
                        failure = "Inventory rollback slot index is invalid.";
                        return false;
                    }

                    if (!slotIndices.Add(slot.SlotIndex))
                    {
                        failure = "Inventory rollback duplicate slot index.";
                        return false;
                    }

                    if (slot.ItemDefinitionId <= 0 || slot.Count <= 0)
                    {
                        failure = "Inventory rollback stack is invalid.";
                        return false;
                    }

                    if (!definitions.TryGet(new ItemDefinitionId(slot.ItemDefinitionId), out ItemDefinition definition))
                    {
                        failure = "Inventory rollback item definition is missing.";
                        return false;
                    }

                    if (slot.Count > definition.MaxStackCount)
                    {
                        failure = "Inventory rollback stack overflows definition.";
                        return false;
                    }

                    if (slot.ItemInstanceId > 0 && slot.Count != 1)
                    {
                        failure = "Inventory rollback instance stack count is invalid.";
                        return false;
                    }
                }
            }

            failure = string.Empty;
            return true;
        }
    }
}
