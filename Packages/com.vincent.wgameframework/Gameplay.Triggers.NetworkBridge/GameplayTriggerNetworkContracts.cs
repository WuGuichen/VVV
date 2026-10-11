using System;
using System.Buffers.Binary;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Network;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers.NetworkBridge
{
    public static class GameplayTriggerNetworkIds
    {
        public const uint GameplayParticipantTypeId = 1002301;
        public const uint TriggerParticipantTypeId = 1002302;
        public const ushort ParticipantSchema = 1;
        public const ushort GameplayRestoreOrder = 100;
        public const ushort TriggerRestoreOrder = 200;
        public const int ProfileTypeId = 1002201;
        public const int ProfileSchema = 1;
    }

    [Flags]
    public enum GameplayTriggerNetworkObservationMask
    {
        None = 0,
        Signal = 1,
        Occupancy = 2,
        Scalar = 4
    }

    public readonly struct GameplayTriggerNetworkSourcePolicy
    {
        public GameplayTriggerNetworkSourcePolicy(int sourceId, GameplayTriggerNetworkObservationMask observations)
        {
            if (sourceId <= 0) throw new ArgumentOutOfRangeException(nameof(sourceId));
            SourceId = sourceId;
            Observations = observations;
        }

        public int SourceId { get; }
        public GameplayTriggerNetworkObservationMask Observations { get; }
    }

    public interface IGameplayTriggerNetworkPeerDirectory
    {
        bool IsConnected(uint peerHandle);
    }

    public interface IGameplayTriggerNetworkOwnershipProvider
    {
        long IdentityHash { get; }
        bool IsOwnedBy(uint peerHandle, GameplayEntityId entityId);
    }

    public sealed class GameplayTriggerNetworkProfile
    {
        private readonly GameplayTriggerNetworkSourcePolicy[] _sourcePolicies;

        public GameplayTriggerNetworkProfile(
            long definitionCatalogHash,
            long bindingCatalogHash,
            long gameplayTriggerCatalogHash,
            GameplayTriggerRuntimeOptions options,
            long networkProfileHash,
            NetworkHashComparability comparability,
            GameplayTriggerNetworkSourcePolicy[] sourcePolicies)
        {
            DefinitionCatalogHash = definitionCatalogHash;
            BindingCatalogHash = bindingCatalogHash;
            GameplayTriggerCatalogHash = gameplayTriggerCatalogHash;
            Options = options;
            NetworkProfileHash = networkProfileHash;
            Comparability = comparability;
            _sourcePolicies = sourcePolicies != null
                ? (GameplayTriggerNetworkSourcePolicy[])sourcePolicies.Clone()
                : Array.Empty<GameplayTriggerNetworkSourcePolicy>();
            Array.Sort(_sourcePolicies, CompareSourcePolicies);
        }

        public long DefinitionCatalogHash { get; }
        public long BindingCatalogHash { get; }
        public long GameplayTriggerCatalogHash { get; }
        public GameplayTriggerRuntimeOptions Options { get; }
        public long NetworkProfileHash { get; }
        public NetworkHashComparability Comparability { get; }

        /// <remarks>NoAlloc.</remarks>
        public bool TryGetSourcePolicy(int sourceId, out GameplayTriggerNetworkSourcePolicy policy)
        {
            for (int i = 0; i < _sourcePolicies.Length; i++)
            {
                if (_sourcePolicies[i].SourceId == sourceId)
                {
                    policy = _sourcePolicies[i];
                    return true;
                }
            }
            policy = default;
            return false;
        }

        private static int CompareSourcePolicies(GameplayTriggerNetworkSourcePolicy left, GameplayTriggerNetworkSourcePolicy right) =>
            left.SourceId.CompareTo(right.SourceId);
    }

    public enum GameplayTriggerNetworkErrorCode
    {
        None = 0,
        InvalidPacket = 1,
        InvalidLength = 2,
        InvalidRecord = 3,
        TraceIdNotSupported = 4,
        IdentityMismatch = 5,
        UnknownPeer = 6,
        UnauthorizedDirection = 7,
        UnauthorizedSource = 8,
        UnauthorizedCommand = 9,
        ForgedProvenance = 10,
        OwnershipMismatch = 11,
        FrameRejected = 12,
        CapacityExceeded = 13,
        AdmissionRejected = 14,
        ReservedNonZero = 15,
        AlreadyApplied = 16,
        ResyncRequired = 17
    }

    public readonly struct GameplayTriggerNetworkResult
    {
        private GameplayTriggerNetworkResult(bool success, GameplayTriggerNetworkErrorCode errorCode, int detail)
        {
            Success = success;
            ErrorCode = errorCode;
            Detail = detail;
        }
        public bool Success { get; }
        public GameplayTriggerNetworkErrorCode ErrorCode { get; }
        public int Detail { get; }
        public static GameplayTriggerNetworkResult Succeeded() => new GameplayTriggerNetworkResult(true, GameplayTriggerNetworkErrorCode.None, 0);
        public static GameplayTriggerNetworkResult Failed(GameplayTriggerNetworkErrorCode code, int detail = 0) => new GameplayTriggerNetworkResult(false, code, detail);
    }

    public static class GameplayTriggerNetworkCommandCodec
    {
        public const int RecordSchema = 1;
        public const int RecordBytes = 116;
        public const int MaximumRecords = 64;
        public const int RuntimeHeaderBytes = 104;
        public const int AuthoritativeHeaderBytes = 120;
        public const int MaximumRuntimePayloadBytes = RuntimeHeaderBytes + (MaximumRecords * RecordBytes);
        public const int MaximumAuthoritativePayloadBytes = AuthoritativeHeaderBytes + (MaximumRecords * RecordBytes);

        /// <remarks>NoAlloc. Non-empty TraceId is rejected because it is not part of record schema 1.</remarks>
        public static GameplayTriggerNetworkResult TryEncodeRecord(RuntimeCommand command, Span<byte> destination)
        {
            if (destination.Length < RecordBytes)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.CapacityExceeded);
            if (!command.TraceId.IsNone)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.TraceIdNotSupported);
            RuntimeCommandEnvelope envelope = command.Envelope;
            if (command.Frame.Value < 0L || command.Frame.Value == long.MaxValue || command.Sequence < 0L || command.Sequence == long.MaxValue
                || command.SourceId <= 0 || command.CommandId <= 0 || !envelope.IsPresent)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidRecord);
            destination.Slice(0, RecordBytes).Clear();
            WriteI64(destination, 0, command.Frame.Value); WriteI32(destination, 8, command.SourceId); WriteI32(destination, 12, command.CommandId);
            WriteI32(destination, 16, command.TargetId); WriteI32(destination, 20, command.Payload0); WriteI32(destination, 24, command.Payload1); WriteI32(destination, 28, command.Payload2);
            WriteI64(destination, 32, command.Sequence); WriteI32(destination, 40, envelope.TypeId); WriteI32(destination, 44, envelope.SchemaVersion); WriteI32(destination, 48, (int)envelope.Provenance);
            WriteI64(destination, 52, envelope.Data0); WriteI64(destination, 60, envelope.Data1); WriteI64(destination, 68, envelope.Data2); WriteI64(destination, 76, envelope.Data3);
            WriteI64(destination, 84, envelope.Data4); WriteI64(destination, 92, envelope.Data5); WriteI64(destination, 100, envelope.Data6); WriteI64(destination, 108, envelope.Data7);
            return GameplayTriggerNetworkResult.Succeeded();
        }

        /// <remarks>NoAlloc. Constructs the canonical empty TraceId.</remarks>
        public static GameplayTriggerNetworkResult TryDecodeRecord(ReadOnlySpan<byte> source, out RuntimeCommand command)
        {
            command = default;
            if (source.Length != RecordBytes)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidLength);
            long frame = ReadI64(source, 0); long sequence = ReadI64(source, 32);
            int sourceId = ReadI32(source, 8); int commandId = ReadI32(source, 12); int typeId = ReadI32(source, 40); int schema = ReadI32(source, 44); int provenance = ReadI32(source, 48);
            if (frame < 0L || frame == long.MaxValue || sequence < 0L || sequence == long.MaxValue || sourceId <= 0 || commandId <= 0 || typeId <= 0 || schema <= 0
                || provenance < (int)RuntimeCommandProvenanceKind.Unspecified || provenance > (int)RuntimeCommandProvenanceKind.Derived)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidRecord);
            command = new RuntimeCommand(new RuntimeFrame(frame), sourceId, commandId, ReadI32(source, 16), ReadI32(source, 20), ReadI32(source, 24), ReadI32(source, 28),
                default, sequence, new RuntimeCommandEnvelope(typeId, schema, (RuntimeCommandProvenanceKind)provenance,
                    ReadI64(source, 52), ReadI64(source, 60), ReadI64(source, 68), ReadI64(source, 76), ReadI64(source, 84), ReadI64(source, 92), ReadI64(source, 100), ReadI64(source, 108)));
            return GameplayTriggerNetworkResult.Succeeded();
        }

        /// <remarks>NoAlloc. Encodes the frozen kind-10 payload into caller storage.</remarks>
        public static GameplayTriggerNetworkResult TryEncodeRuntimePayload(
            GameplayTriggerNetworkProfile profile,
            RuntimeFrame clientFrame,
            long packetSequence,
            ReadOnlySpan<RuntimeCommand> commands,
            Span<byte> destination,
            out int bytesWritten)
        {
            bytesWritten = 0;
            if (profile == null || clientFrame.Value < 0L || clientFrame.Value == long.MaxValue || packetSequence < 0L || packetSequence == long.MaxValue
                || commands.Length < 1 || commands.Length > MaximumRecords)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidPacket);
            int required = RuntimeHeaderBytes + (commands.Length * RecordBytes);
            if (destination.Length < required)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.CapacityExceeded);
            Span<byte> payload = destination.Slice(0, required); payload.Clear();
            WriteI64(payload, 0, clientFrame.Value); WriteI64(payload, 8, packetSequence); WriteProfile(payload, 16, profile);
            WriteU32(payload, 96, (uint)commands.Length); WriteU32(payload, 100, (uint)(commands.Length * RecordBytes));
            int offset = RuntimeHeaderBytes;
            for (int i = 0; i < commands.Length; i++)
            {
                GameplayTriggerNetworkResult result = TryEncodeRecord(commands[i], payload.Slice(offset, RecordBytes));
                if (!result.Success) return result;
                offset += RecordBytes;
            }
            bytesWritten = required;
            return GameplayTriggerNetworkResult.Succeeded();
        }

        /// <remarks>NoAlloc. Performs exact identity/count/length preflight before decoding any record.</remarks>
        public static GameplayTriggerNetworkResult TryDecodeRuntimePayload(
            GameplayTriggerNetworkProfile profile,
            ReadOnlySpan<byte> payload,
            Span<RuntimeCommand> destination,
            out RuntimeFrame clientFrame,
            out long packetSequence,
            out int recordCount)
        {
            clientFrame = RuntimeFrame.Zero; packetSequence = 0; recordCount = 0;
            if (profile == null || payload.Length < RuntimeHeaderBytes)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidLength);
            long frame = ReadI64(payload, 0); long sequence = ReadI64(payload, 8);
            if (frame < 0 || frame == long.MaxValue || sequence < 0 || sequence == long.MaxValue)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidPacket);
            if (!ProfileMatches(payload, 16, profile))
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.IdentityMismatch);
            uint count = ReadU32(payload, 96); uint recordByteCount = ReadU32(payload, 100);
            if (count < 1 || count > MaximumRecords || recordByteCount != count * RecordBytes
                || payload.Length != RuntimeHeaderBytes + (long)recordByteCount)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidLength);
            if (destination.Length < count)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.CapacityExceeded);
            int offset = RuntimeHeaderBytes;
            for (int i = 0; i < (int)count; i++)
            {
                GameplayTriggerNetworkResult result = TryDecodeRecord(payload.Slice(offset, RecordBytes), out destination[i]);
                if (!result.Success) return result;
                offset += RecordBytes;
            }
            clientFrame = new RuntimeFrame(frame); packetSequence = sequence; recordCount = (int)count;
            return GameplayTriggerNetworkResult.Succeeded();
        }

        /// <remarks>NoAlloc. Encodes the frozen kind-11 authoritative payload.</remarks>
        public static GameplayTriggerNetworkResult TryEncodeAuthoritativePayload(
            GameplayTriggerNetworkProfile profile,
            RuntimeFrame serverFrame,
            RuntimeFrame ackedInputFrame,
            uint inputPeerHandle,
            long packetSequence,
            ReadOnlySpan<RuntimeCommand> commands,
            Span<byte> destination,
            out int bytesWritten)
        {
            bytesWritten = 0;
            if (profile == null || inputPeerHandle == 0U || serverFrame.Value < 0L || serverFrame.Value == long.MaxValue
                || ackedInputFrame.Value < 0L || ackedInputFrame.Value == long.MaxValue || packetSequence < 0L || packetSequence == long.MaxValue
                || commands.Length < 1 || commands.Length > MaximumRecords)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidPacket);
            int required = AuthoritativeHeaderBytes + (commands.Length * RecordBytes);
            if (destination.Length < required)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.CapacityExceeded);
            Span<byte> payload = destination.Slice(0, required); payload.Clear();
            WriteI64(payload, 0, serverFrame.Value); WriteI64(payload, 8, ackedInputFrame.Value); WriteU32(payload, 16, inputPeerHandle);
            WriteI64(payload, 24, packetSequence); WriteProfile(payload, 32, profile);
            WriteU32(payload, 112, (uint)commands.Length); WriteU32(payload, 116, (uint)(commands.Length * RecordBytes));
            int offset = AuthoritativeHeaderBytes;
            for (int i = 0; i < commands.Length; i++)
            {
                GameplayTriggerNetworkResult result = TryEncodeRecord(commands[i], payload.Slice(offset, RecordBytes));
                if (!result.Success) return result;
                offset += RecordBytes;
            }
            bytesWritten = required;
            return GameplayTriggerNetworkResult.Succeeded();
        }

        /// <remarks>NoAlloc. Validates reserved offset 20 before record decode.</remarks>
        public static GameplayTriggerNetworkResult TryDecodeAuthoritativePayload(
            GameplayTriggerNetworkProfile profile,
            ReadOnlySpan<byte> payload,
            Span<RuntimeCommand> destination,
            out RuntimeFrame serverFrame,
            out RuntimeFrame ackedInputFrame,
            out uint inputPeerHandle,
            out long packetSequence,
            out int recordCount)
        {
            serverFrame = RuntimeFrame.Zero; ackedInputFrame = RuntimeFrame.Zero; inputPeerHandle = 0; packetSequence = 0; recordCount = 0;
            if (profile == null || payload.Length < AuthoritativeHeaderBytes)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidLength);
            long server = ReadI64(payload, 0); long ack = ReadI64(payload, 8); uint peer = ReadU32(payload, 16); long sequence = ReadI64(payload, 24);
            if (server < 0 || server == long.MaxValue || ack < 0 || ack == long.MaxValue || peer == 0 || ReadU32(payload, 20) != 0
                || sequence < 0 || sequence == long.MaxValue || !ProfileMatches(payload, 32, profile))
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidPacket);
            uint count = ReadU32(payload, 112); uint bytes = ReadU32(payload, 116);
            if (count < 1 || count > MaximumRecords || bytes != count * RecordBytes
                || payload.Length != AuthoritativeHeaderBytes + (long)bytes || destination.Length < count)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidLength);
            int offset = AuthoritativeHeaderBytes;
            for (int i = 0; i < (int)count; i++)
            {
                GameplayTriggerNetworkResult result = TryDecodeRecord(payload.Slice(offset, RecordBytes), out destination[i]);
                if (!result.Success) return result;
                offset += RecordBytes;
            }
            serverFrame = new RuntimeFrame(server); ackedInputFrame = new RuntimeFrame(ack); inputPeerHandle = peer; packetSequence = sequence; recordCount = (int)count;
            return GameplayTriggerNetworkResult.Succeeded();
        }

        internal static void WriteProfile(Span<byte> payload, int offset, GameplayTriggerNetworkProfile profile)
        {
            WriteI64(payload, offset, profile.DefinitionCatalogHash); WriteI64(payload, offset + 8, profile.BindingCatalogHash);
            WriteI64(payload, offset + 16, profile.GameplayTriggerCatalogHash); WriteI64(payload, offset + 24, profile.NetworkProfileHash);
            WriteOptions(payload.Slice(offset + 32, 48), profile.Options);
        }

        internal static bool ProfileMatches(ReadOnlySpan<byte> payload, int offset, GameplayTriggerNetworkProfile profile)
        {
            return ReadI64(payload, offset) == profile.DefinitionCatalogHash && ReadI64(payload, offset + 8) == profile.BindingCatalogHash
                && ReadI64(payload, offset + 16) == profile.GameplayTriggerCatalogHash && ReadI64(payload, offset + 24) == profile.NetworkProfileHash
                && OptionsMatch(payload.Slice(offset + 32, 48), profile.Options);
        }

        internal static void WriteOptions(Span<byte> s, GameplayTriggerRuntimeOptions o)
        { WriteI32(s, 0, o.MaxInputBatches); WriteI32(s, 4, o.MaxInputItems); WriteI32(s, 8, o.MaxRelations); WriteI32(s, 12, o.MaxOccurrences); WriteI32(s, 16, o.MaxCandidates); WriteI32(s, 20, o.MaxRouteStates); WriteI32(s, 24, o.MaxActivations); WriteI32(s, 28, o.MaxIntents); WriteI32(s, 32, o.MaxPreparedActions); WriteI32(s, 36, o.MaxConditionStackDepth); WriteI32(s, 40, o.MaxDeferredRecords); WriteI32(s, 44, o.MaxReliableOutboxRecords); }
        internal static bool OptionsMatch(ReadOnlySpan<byte> s, GameplayTriggerRuntimeOptions o)
        { return ReadI32(s, 0) == o.MaxInputBatches && ReadI32(s, 4) == o.MaxInputItems && ReadI32(s, 8) == o.MaxRelations && ReadI32(s, 12) == o.MaxOccurrences && ReadI32(s, 16) == o.MaxCandidates && ReadI32(s, 20) == o.MaxRouteStates && ReadI32(s, 24) == o.MaxActivations && ReadI32(s, 28) == o.MaxIntents && ReadI32(s, 32) == o.MaxPreparedActions && ReadI32(s, 36) == o.MaxConditionStackDepth && ReadI32(s, 40) == o.MaxDeferredRecords && ReadI32(s, 44) == o.MaxReliableOutboxRecords; }
        internal static void WriteU16(Span<byte> s, int o, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(s.Slice(o, 2), v);
        internal static void WriteU32(Span<byte> s, int o, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(s.Slice(o, 4), v);
        internal static void WriteI32(Span<byte> s, int o, int v) => BinaryPrimitives.WriteInt32LittleEndian(s.Slice(o, 4), v);
        internal static void WriteI64(Span<byte> s, int o, long v) => BinaryPrimitives.WriteInt64LittleEndian(s.Slice(o, 8), v);
        internal static ushort ReadU16(ReadOnlySpan<byte> s, int o) => BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(o, 2));
        internal static uint ReadU32(ReadOnlySpan<byte> s, int o) => BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(o, 4));
        internal static int ReadI32(ReadOnlySpan<byte> s, int o) => BinaryPrimitives.ReadInt32LittleEndian(s.Slice(o, 4));
        internal static long ReadI64(ReadOnlySpan<byte> s, int o) => BinaryPrimitives.ReadInt64LittleEndian(s.Slice(o, 8));
    }
}
