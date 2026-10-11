using System;
using System.Buffers.Binary;
using MxFramework.Runtime;

namespace MxFramework.Network
{
    public enum NetworkBoundedErrorCode
    {
        None = 0,
        InvalidArgument = 1,
        UnsupportedProtocol = 2,
        CapabilityMismatch = 3,
        ProfileMismatch = 4,
        UnsupportedMessageKind = 5,
        UnsupportedWireSchema = 6,
        InvalidHeader = 7,
        InvalidLength = 8,
        PacketTooLarge = 9,
        ReservedNonZero = 10,
        CrcMismatch = 11,
        DuplicateOrStale = 12,
        OutOfOrder = 13,
        SequenceExhausted = 14,
        FrameExhausted = 15,
        CapacityExceeded = 16,
        ParticipantMismatch = 17,
        ParticipantValidationFailed = 18,
        ParticipantCaptureFailed = 19,
        HistoryMissing = 20,
        AlreadyApplied = 21
    }

    public readonly struct NetworkBoundedResult
    {
        private NetworkBoundedResult(bool success, NetworkBoundedErrorCode errorCode, int detail)
        {
            Success = success;
            ErrorCode = errorCode;
            Detail = detail;
        }

        public bool Success { get; }
        public NetworkBoundedErrorCode ErrorCode { get; }
        public int Detail { get; }

        public static NetworkBoundedResult Succeeded()
        {
            return new NetworkBoundedResult(true, NetworkBoundedErrorCode.None, 0);
        }

        public static NetworkBoundedResult Failed(NetworkBoundedErrorCode errorCode, int detail = 0)
        {
            return new NetworkBoundedResult(false, errorCode, detail);
        }
    }

    public enum NetworkHashComparability : byte
    {
        FullComparable = 1,
        PartialNonComparable = 2
    }

    public enum NetworkBoundedSequenceStream : byte
    {
        ClientInput = 1,
        AuthoritativeCommand = 2,
        Snapshot = 3,
        StateRequest = 4
    }

    /// <summary>Authenticated transport direction for one protocol-2 bounded packet.</summary>
    public enum NetworkBoundedPacketDirection : byte
    {
        ClientToServer = 1,
        ServerToClient = 2
    }

    /// <summary>
    /// Transport-authenticated identity that is carried separately from untrusted packet bytes.
    /// </summary>
    public readonly struct NetworkBoundedAuthenticatedMetadata
    {
        public NetworkBoundedAuthenticatedMetadata(
            ulong sessionHandle,
            uint remotePeerHandle,
            uint localPeerHandle,
            NetworkBoundedPacketDirection direction)
        {
            SessionHandle = sessionHandle;
            RemotePeerHandle = remotePeerHandle;
            LocalPeerHandle = localPeerHandle;
            Direction = direction;
        }

        public ulong SessionHandle { get; }
        public uint RemotePeerHandle { get; }
        public uint LocalPeerHandle { get; }
        public NetworkBoundedPacketDirection Direction { get; }

        /// <remarks>NoAlloc.</remarks>
        public bool IsValid => SessionHandle != 0UL
            && RemotePeerHandle != 0U
            && LocalPeerHandle != 0U
            && (Direction == NetworkBoundedPacketDirection.ClientToServer
                || Direction == NetworkBoundedPacketDirection.ServerToClient);
    }

    /// <summary>Fixed connected-peer set for protocol-2 composition roots.</summary>
    public sealed class NetworkBoundedPeerSet
    {
        public const int MaximumPeers = 8;
        private readonly uint[] _peers = new uint[MaximumPeers];
        private int _count;

        public int Count => _count;

        /// <remarks>NoAlloc. Duplicate handles are idempotent; zero and N+1 fail without mutation.</remarks>
        public NetworkBoundedResult TryAdd(uint peerHandle)
        {
            if (peerHandle == 0U)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidArgument);
            for (int i = 0; i < _count; i++)
            {
                if (_peers[i] == peerHandle)
                    return NetworkBoundedResult.Succeeded();
            }
            if (_count >= _peers.Length)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.CapacityExceeded);
            _peers[_count++] = peerHandle;
            return NetworkBoundedResult.Succeeded();
        }

        /// <remarks>NoAlloc.</remarks>
        public bool Contains(uint peerHandle)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_peers[i] == peerHandle)
                    return true;
            }
            return false;
        }

        /// <remarks>NoAlloc.</remarks>
        public bool Remove(uint peerHandle)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_peers[i] != peerHandle)
                    continue;
                for (int j = i + 1; j < _count; j++)
                    _peers[j - 1] = _peers[j];
                _peers[--_count] = 0U;
                return true;
            }
            return false;
        }
    }

    public readonly struct NetworkBoundedPacketHeader
    {
        public NetworkBoundedPacketHeader(
            NetworkMessageKind kind,
            ulong sessionHandle,
            uint senderPeerHandle,
            uint receiverPeerHandle,
            long outerSequence,
            long frame,
            int payloadBytes)
        {
            Kind = kind;
            SessionHandle = sessionHandle;
            SenderPeerHandle = senderPeerHandle;
            ReceiverPeerHandle = receiverPeerHandle;
            OuterSequence = outerSequence;
            Frame = frame;
            PayloadBytes = payloadBytes;
        }

        public NetworkMessageKind Kind { get; }
        public ulong SessionHandle { get; }
        public uint SenderPeerHandle { get; }
        public uint ReceiverPeerHandle { get; }
        public long OuterSequence { get; }
        public long Frame { get; }
        public int PayloadBytes { get; }
    }

    /// <summary>Encodes and validates the protocol-2 bounded outer wire envelope.</summary>
    public static class NetworkBoundedWire
    {
        public const uint Magic = 0x4D584E31U;
        public const ushort WireSchema = 1;
        public const int HeaderBytes = 52;
        public const int CommandTotalLimit = 8192;
        public const int SnapshotTotalLimit = 524288;
        public const int StateRequestTotalLimit = 256;

        /// <remarks>NoAlloc. Writes into caller-owned storage.</remarks>
        public static NetworkBoundedResult TryEncode(
            in NetworkBoundedPacketHeader header,
            ReadOnlySpan<byte> payload,
            Span<byte> destination,
            out int encodedBytes)
        {
            encodedBytes = 0;
            int totalLimit = GetTotalLimit(header.Kind);
            if (totalLimit == 0)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.UnsupportedMessageKind);
            if (header.SessionHandle == 0UL
                || header.SenderPeerHandle == 0U
                || header.ReceiverPeerHandle == 0U
                || header.OuterSequence < 0L
                || header.OuterSequence == long.MaxValue
                || header.Frame < 0L
                || header.Frame == long.MaxValue)
            {
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidHeader);
            }
            if (header.PayloadBytes != payload.Length)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidLength);
            if (payload.Length > totalLimit - HeaderBytes)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.PacketTooLarge);
            int totalBytes = HeaderBytes + payload.Length;
            if (destination.Length < totalBytes)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.CapacityExceeded);

            Span<byte> packet = destination.Slice(0, totalBytes);
            packet.Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(packet.Slice(0, 4), Magic);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.Slice(4, 2), WireSchema);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.Slice(6, 2), HeaderBytes);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.Slice(8, 2), (ushort)header.Kind);
            packet[10] = (byte)NetworkChannel.ReliableOrdered;
            packet[11] = 0;
            BinaryPrimitives.WriteUInt64LittleEndian(packet.Slice(12, 8), header.SessionHandle);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.Slice(20, 4), header.SenderPeerHandle);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.Slice(24, 4), header.ReceiverPeerHandle);
            BinaryPrimitives.WriteInt64LittleEndian(packet.Slice(28, 8), header.OuterSequence);
            BinaryPrimitives.WriteInt64LittleEndian(packet.Slice(36, 8), header.Frame);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.Slice(44, 4), (uint)payload.Length);
            payload.CopyTo(packet.Slice(HeaderBytes));
            uint crc = CalculateCrc32WithZeroedChecksum(packet);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.Slice(48, 4), crc);
            encodedBytes = totalBytes;
            return NetworkBoundedResult.Succeeded();
        }

        /// <remarks>NoAlloc. Performs all envelope and exact-length checks before exposing the payload offset.</remarks>
        public static NetworkBoundedResult TryDecode(
            ReadOnlySpan<byte> packet,
            out NetworkBoundedPacketHeader header)
        {
            header = default;
            if (packet.Length < HeaderBytes)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidLength);
            if (BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(0, 4)) != Magic)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidHeader);
            if (BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(4, 2)) != WireSchema)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.UnsupportedWireSchema);
            if (BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(6, 2)) != HeaderBytes)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidHeader);

            var kind = (NetworkMessageKind)BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(8, 2));
            int totalLimit = GetTotalLimit(kind);
            if (totalLimit == 0)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.UnsupportedMessageKind);
            if (packet[10] != (byte)NetworkChannel.ReliableOrdered)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidHeader);
            if (packet[11] != 0)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ReservedNonZero);

            ulong session = BinaryPrimitives.ReadUInt64LittleEndian(packet.Slice(12, 8));
            uint sender = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(20, 4));
            uint receiver = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(24, 4));
            long sequence = BinaryPrimitives.ReadInt64LittleEndian(packet.Slice(28, 8));
            long frame = BinaryPrimitives.ReadInt64LittleEndian(packet.Slice(36, 8));
            uint payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(44, 4));
            if (session == 0UL || sender == 0U || receiver == 0U || sequence < 0L || frame < 0L)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidHeader);
            if (sequence == long.MaxValue)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.SequenceExhausted);
            if (frame == long.MaxValue)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.FrameExhausted);
            if (payloadLength > (uint)(totalLimit - HeaderBytes))
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.PacketTooLarge);
            long expectedLength = HeaderBytes + (long)payloadLength;
            if (expectedLength != packet.Length)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidLength);

            uint expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(48, 4));
            if (expectedCrc != CalculateCrc32WithZeroedChecksum(packet))
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.CrcMismatch);

            header = new NetworkBoundedPacketHeader(
                kind,
                session,
                sender,
                receiver,
                sequence,
                frame,
                (int)payloadLength);
            return NetworkBoundedResult.Succeeded();
        }

        public static int GetTotalLimit(NetworkMessageKind kind)
        {
            switch (kind)
            {
                case NetworkMessageKind.BoundedRuntimeCommands:
                case NetworkMessageKind.BoundedAuthoritativeCommands:
                    return CommandTotalLimit;
                case NetworkMessageKind.BoundedStateSnapshot:
                    return SnapshotTotalLimit;
                case NetworkMessageKind.BoundedStateRequest:
                    return StateRequestTotalLimit;
                default:
                    return 0;
            }
        }

        private static uint CalculateCrc32WithZeroedChecksum(ReadOnlySpan<byte> packet)
        {
            uint crc = uint.MaxValue;
            for (int i = 0; i < packet.Length; i++)
            {
                byte value = i >= 48 && i < 52 ? (byte)0 : packet[i];
                crc ^= value;
                for (int bit = 0; bit < 8; bit++)
                {
                    uint mask = (uint)-(int)(crc & 1U);
                    crc = (crc >> 1) ^ (0xEDB88320U & mask);
                }
            }

            return ~crc;
        }
    }

    public static class NetworkBoundedFrameWindow
    {
        public const int PastFrames = 8;
        public const int FutureFrames = 2;

        /// <remarks>NoAlloc. Uses guarded signed arithmetic and never wraps.</remarks>
        public static NetworkBoundedResult TryValidate(long serverFrame, long candidateFrame)
        {
            if (serverFrame < 0L || candidateFrame < 0L)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidArgument);
            if (serverFrame > long.MaxValue - (FutureFrames + 1L))
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.FrameExhausted);
            long minimum = serverFrame > PastFrames ? serverFrame - PastFrames : 0L;
            long maximum = serverFrame + FutureFrames;
            if (candidateFrame < minimum)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.DuplicateOrStale);
            if (candidateFrame > maximum)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.OutOfOrder);
            return NetworkBoundedResult.Succeeded();
        }
    }

    /// <summary>Tracks an exact signed sequence frontier without mutation during validation.</summary>
    public sealed class NetworkBoundedSequenceFrontier
    {
        private long _expected;

        public long Expected => _expected;
        public long LastAccepted => _expected - 1L;

        /// <remarks>NoAlloc. Does not mutate the frontier.</remarks>
        public NetworkBoundedResult TryStage(long sequence, out long stagedExpected)
        {
            stagedExpected = _expected;
            if (_expected == long.MaxValue || sequence == long.MaxValue)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.SequenceExhausted);
            if (sequence < _expected)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.DuplicateOrStale);
            if (sequence > _expected)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.OutOfOrder);
            stagedExpected = _expected + 1L;
            return NetworkBoundedResult.Succeeded();
        }

        /// <remarks>NoAlloc. Commits a value returned by <see cref="TryStage"/>.</remarks>
        public void Commit(long stagedExpected)
        {
            if (stagedExpected != _expected + 1L)
                throw new ArgumentOutOfRangeException(nameof(stagedExpected));
            _expected = stagedExpected;
        }
    }

    /// <summary>Owns the total outer frontier and four independent semantic-stream frontiers.</summary>
    public sealed class NetworkBoundedPacketFrontiers
    {
        private readonly NetworkBoundedSequenceFrontier _outer = new NetworkBoundedSequenceFrontier();
        private readonly NetworkBoundedSequenceFrontier[] _semantic =
            new NetworkBoundedSequenceFrontier[4]
            {
                new NetworkBoundedSequenceFrontier(),
                new NetworkBoundedSequenceFrontier(),
                new NetworkBoundedSequenceFrontier(),
                new NetworkBoundedSequenceFrontier()
            };

        public long ExpectedOuterSequence => _outer.Expected;

        public long GetExpectedSemanticSequence(NetworkBoundedSequenceStream stream)
        {
            return GetSemantic(stream).Expected;
        }

        /// <remarks>NoAlloc. Used for a fully validated idempotent semantic duplicate with a new outer sequence.</remarks>
        public NetworkBoundedResult TryStageOuter(long outerSequence, out long stagedOuter)
        {
            return _outer.TryStage(outerSequence, out stagedOuter);
        }

        /// <remarks>NoAlloc.</remarks>
        public void CommitOuter(long stagedOuter)
        {
            _outer.Commit(stagedOuter);
        }

        /// <remarks>NoAlloc. Advances both applicable frontiers only after both validate.</remarks>
        public NetworkBoundedResult TryAccept(
            long outerSequence,
            NetworkBoundedSequenceStream stream,
            long semanticSequence)
        {
            NetworkBoundedResult result = TryStage(
                outerSequence,
                stream,
                semanticSequence,
                out NetworkBoundedFrontierStage stage);
            if (!result.Success)
                return result;
            Commit(stage);
            return NetworkBoundedResult.Succeeded();
        }

        /// <remarks>NoAlloc. Validates without mutation so authority admission can be atomic.</remarks>
        public NetworkBoundedResult TryStage(
            long outerSequence,
            NetworkBoundedSequenceStream stream,
            long semanticSequence,
            out NetworkBoundedFrontierStage stage)
        {
            stage = default;
            NetworkBoundedResult outer = _outer.TryStage(outerSequence, out long stagedOuter);
            if (!outer.Success)
                return outer;
            NetworkBoundedSequenceFrontier semantic = GetSemantic(stream);
            NetworkBoundedResult inner = semantic.TryStage(semanticSequence, out long stagedSemantic);
            if (!inner.Success)
                return inner;
            stage = new NetworkBoundedFrontierStage(
                stream,
                _outer.Expected,
                semantic.Expected,
                stagedOuter,
                stagedSemantic);
            return NetworkBoundedResult.Succeeded();
        }

        /// <remarks>NoAlloc. Commits only a still-current stage returned by <see cref="TryStage"/>.</remarks>
        public void Commit(in NetworkBoundedFrontierStage stage)
        {
            NetworkBoundedSequenceFrontier semantic = GetSemantic(stage.Stream);
            if (_outer.Expected != stage.OuterExpectedBefore
                || semantic.Expected != stage.SemanticExpectedBefore)
            {
                throw new InvalidOperationException("Bounded frontier stage is stale.");
            }
            _outer.Commit(stage.OuterExpectedAfter);
            semantic.Commit(stage.SemanticExpectedAfter);
        }

        private NetworkBoundedSequenceFrontier GetSemantic(NetworkBoundedSequenceStream stream)
        {
            int index = (int)stream - 1;
            if ((uint)index >= (uint)_semantic.Length)
                throw new ArgumentOutOfRangeException(nameof(stream));
            return _semantic[index];
        }
    }

    public readonly struct NetworkBoundedFrontierStage
    {
        internal NetworkBoundedFrontierStage(
            NetworkBoundedSequenceStream stream,
            long outerExpectedBefore,
            long semanticExpectedBefore,
            long outerExpectedAfter,
            long semanticExpectedAfter)
        {
            Stream = stream;
            OuterExpectedBefore = outerExpectedBefore;
            SemanticExpectedBefore = semanticExpectedBefore;
            OuterExpectedAfter = outerExpectedAfter;
            SemanticExpectedAfter = semanticExpectedAfter;
        }

        public NetworkBoundedSequenceStream Stream { get; }
        internal long OuterExpectedBefore { get; }
        internal long SemanticExpectedBefore { get; }
        internal long OuterExpectedAfter { get; }
        internal long SemanticExpectedAfter { get; }
    }

    public readonly struct NetworkBoundedParticipantDescriptor
    {
        public NetworkBoundedParticipantDescriptor(uint typeId, ushort schema, ushort restoreOrder)
        {
            if (typeId == 0U) throw new ArgumentOutOfRangeException(nameof(typeId));
            if (schema == 0) throw new ArgumentOutOfRangeException(nameof(schema));
            TypeId = typeId;
            Schema = schema;
            RestoreOrder = restoreOrder;
        }

        public uint TypeId { get; }
        public ushort Schema { get; }
        public ushort RestoreOrder { get; }
    }

    public readonly struct NetworkBoundedSourceAuthorizationRow
    {
        public NetworkBoundedSourceAuthorizationRow(
            int sourceId,
            int allowedCommandMask,
            int observationMask,
            int ownershipPolicy)
        {
            SourceId = sourceId;
            AllowedCommandMask = allowedCommandMask;
            ObservationMask = observationMask;
            OwnershipPolicy = ownershipPolicy;
        }

        public int SourceId { get; }
        public int AllowedCommandMask { get; }
        public int ObservationMask { get; }
        public int OwnershipPolicy { get; }
    }

    /// <summary>Builds the canonical protocol-2 authority profile identity.</summary>
    public static class NetworkBoundedProfileHash
    {
        public const int Schema = 1;

        private static class HashFields
        {
            public const int ProfileSchema = 1;
            public const int Protocol = 2;
            public const int WireSchema = 3;
            public const int WireMagic = 4;
            public const int KindRuntime = 5;
            public const int KindAuthoritative = 6;
            public const int KindSnapshot = 7;
            public const int KindRequest = 8;
            public const int Channel = 9;
            public const int LimitCommand = 10;
            public const int LimitSnapshot = 11;
            public const int LimitRequest = 12;
            public const int RecordSchema = 13;
            public const int RecordBytes = 14;
            public const int RecordMax = 15;
            public const int PeersMax = 16;
            public const int PendingMax = 17;
            public const int HistoryMax = 18;
            public const int ParticipantsMax = 19;
            public const int FramePast = 20;
            public const int FrameFuture = 21;
            public const int AuthMode = 22;
            public const int OwnershipIdentity = 23;
            public const int Comparability = 24;
            public const int SourceRows = 25;
            public const int Participants = 26;
        }

        private static class SourceHashFields
        {
            public const int SourceId = 1;
            public const int CommandMask = 2;
            public const int ObservationMask = 3;
            public const int Ownership = 4;
        }

        private static class ParticipantHashFields
        {
            public const int Type = 1;
            public const int Schema = 2;
            public const int Order = 3;
        }

        /// <remarks>LowFreqAlloc. Intended for immutable session-profile construction.</remarks>
        public static long Compute(
            long ownershipProviderIdentityHash,
            NetworkHashComparability comparability,
            ReadOnlySpan<NetworkBoundedSourceAuthorizationRow> sourceRows,
            ReadOnlySpan<NetworkBoundedParticipantDescriptor> participants)
        {
            var hash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkBoundedState, Schema);
            hash.AddInt(HashFields.ProfileSchema, Schema);
            hash.AddInt(HashFields.Protocol, 2);
            hash.AddInt(HashFields.WireSchema, NetworkBoundedWire.WireSchema);
            hash.AddLong(HashFields.WireMagic, NetworkBoundedWire.Magic);
            hash.AddInt(HashFields.KindRuntime, 10);
            hash.AddInt(HashFields.KindAuthoritative, 11);
            hash.AddInt(HashFields.KindSnapshot, 12);
            hash.AddInt(HashFields.KindRequest, 13);
            hash.AddInt(HashFields.Channel, (int)NetworkChannel.ReliableOrdered);
            hash.AddInt(HashFields.LimitCommand, NetworkBoundedWire.CommandTotalLimit);
            hash.AddInt(HashFields.LimitSnapshot, NetworkBoundedWire.SnapshotTotalLimit);
            hash.AddInt(HashFields.LimitRequest, NetworkBoundedWire.StateRequestTotalLimit);
            hash.AddInt(HashFields.RecordSchema, 1);
            hash.AddInt(HashFields.RecordBytes, 116);
            hash.AddInt(HashFields.RecordMax, 64);
            hash.AddInt(HashFields.PeersMax, NetworkBoundedPeerSet.MaximumPeers);
            hash.AddInt(HashFields.PendingMax, 128);
            hash.AddInt(HashFields.HistoryMax, 128);
            hash.AddInt(HashFields.ParticipantsMax, 8);
            hash.AddInt(HashFields.FramePast, 8);
            hash.AddInt(HashFields.FrameFuture, 2);
            hash.AddInt(HashFields.AuthMode, 1);
            hash.AddLong(HashFields.OwnershipIdentity, ownershipProviderIdentityHash);
            hash.AddInt(HashFields.Comparability, (int)comparability);
            hash.BeginArray(HashFields.SourceRows, sourceRows.Length);
            for (int i = 0; i < sourceRows.Length; i++)
            {
                NetworkBoundedSourceAuthorizationRow row = sourceRows[i];
                hash.BeginElement(i);
                hash.AddInt(SourceHashFields.SourceId, row.SourceId);
                hash.AddInt(SourceHashFields.CommandMask, row.AllowedCommandMask);
                hash.AddInt(SourceHashFields.ObservationMask, row.ObservationMask);
                hash.AddInt(SourceHashFields.Ownership, row.OwnershipPolicy);
                hash.EndElement();
            }
            hash.EndArray();
            hash.BeginArray(HashFields.Participants, participants.Length);
            for (int i = 0; i < participants.Length; i++)
            {
                NetworkBoundedParticipantDescriptor participant = participants[i];
                hash.BeginElement(i);
                hash.AddLong(ParticipantHashFields.Type, participant.TypeId);
                hash.AddInt(ParticipantHashFields.Schema, participant.Schema);
                hash.AddInt(ParticipantHashFields.Order, participant.RestoreOrder);
                hash.EndElement();
            }
            hash.EndArray();
            return hash.ToHash();
        }
    }

    public static class NetworkBoundedSessionNegotiation
    {
        public const int RequiredProtocolVersion = 2;

        /// <remarks>NoAlloc. Protocol 1 remains on the legacy path and cannot negotiate bounded kinds.</remarks>
        public static NetworkBoundedResult TryNegotiate(
            in NetworkSessionConfig local,
            in NetworkSessionConfig remote,
            long localProfileHash,
            long remoteProfileHash,
            bool localCapability,
            bool remoteCapability)
        {
            if (local.ProtocolVersion != RequiredProtocolVersion
                || remote.ProtocolVersion != RequiredProtocolVersion)
            {
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.UnsupportedProtocol);
            }
            if (!localCapability || !remoteCapability)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.CapabilityMismatch);
            if (localProfileHash != remoteProfileHash)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ProfileMismatch);
            return NetworkBoundedResult.Succeeded();
        }
    }

    public interface INetworkBoundedStateParticipant
    {
        uint TypeId { get; }
        ushort Schema { get; }
        ushort RestoreOrder { get; }
        int MaxStateBytes { get; }
        NetworkBoundedResult TryCapture(Span<byte> destination, out int bytesWritten, out long participantHash);
        NetworkBoundedResult TryPrepareRestore(ReadOnlySpan<byte> state, long expectedParticipantHash);
        void CommitPreparedRestore();
        void DiscardPreparedRestore();
    }

    /// <summary>Coordinates candidate validation and explicit ordered publication for bounded state participants.</summary>
    public sealed class NetworkBoundedStateRegistry
    {
        public const int MaximumParticipants = 8;
        private readonly INetworkBoundedStateParticipant[] _participants;
        private int _count;
        private bool _sealed;

        public NetworkBoundedStateRegistry(int capacity = MaximumParticipants)
        {
            if (capacity < 1 || capacity > MaximumParticipants)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _participants = new INetworkBoundedStateParticipant[capacity];
        }

        public int ParticipantCount => _count;

        /// <remarks>AllocByDesign initialization operation.</remarks>
        public void Register(INetworkBoundedStateParticipant participant)
        {
            if (_sealed) throw new InvalidOperationException("Bounded state registry is sealed.");
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            if (_count >= _participants.Length) throw new InvalidOperationException("Bounded state participant capacity exceeded.");
            for (int i = 0; i < _count; i++)
            {
                if (_participants[i].TypeId == participant.TypeId)
                    throw new ArgumentException("Duplicate bounded participant type id.", nameof(participant));
            }
            _participants[_count++] = participant;
        }

        /// <remarks>NoAlloc. Freezes deterministic RestoreOrder,TypeId order.</remarks>
        public void Seal()
        {
            for (int i = 1; i < _count; i++)
            {
                INetworkBoundedStateParticipant value = _participants[i];
                int j = i - 1;
                while (j >= 0 && Compare(_participants[j], value) > 0)
                {
                    _participants[j + 1] = _participants[j];
                    j--;
                }
                _participants[j + 1] = value;
            }
            _sealed = true;
        }

        /// <remarks>NoAlloc. Every participant validates before any participant publishes.</remarks>
        public NetworkBoundedResult TryPrepareAndCommit(
            ReadOnlySpan<NetworkBoundedStateSlice> slices,
            ReadOnlySpan<byte> stateBytes)
        {
            if (!_sealed || slices.Length != _count)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantMismatch);
            int prepared = 0;
            int expectedOffset = 0;
            for (int i = 0; i < _count; i++)
            {
                INetworkBoundedStateParticipant participant = _participants[i];
                NetworkBoundedStateSlice slice = slices[i];
                if (participant.TypeId != slice.TypeId
                    || participant.Schema != slice.Schema
                    || participant.RestoreOrder != slice.RestoreOrder
                    || slice.Offset != expectedOffset
                    || slice.Length < 0
                    || slice.Offset > stateBytes.Length - slice.Length)
                {
                    DiscardPrepared(prepared);
                    return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantMismatch, i);
                }
                NetworkBoundedResult result = participant.TryPrepareRestore(
                    stateBytes.Slice(slice.Offset, slice.Length),
                    slice.ParticipantHash);
                if (!result.Success)
                {
                    DiscardPrepared(prepared);
                    return result;
                }
                prepared++;
                expectedOffset += slice.Length;
            }

            if (expectedOffset != stateBytes.Length)
            {
                DiscardPrepared(prepared);
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantMismatch);
            }

            for (int i = 0; i < _count; i++)
                _participants[i].CommitPreparedRestore();
            return NetworkBoundedResult.Succeeded();
        }

        private void DiscardPrepared(int count)
        {
            for (int i = 0; i < count; i++)
                _participants[i].DiscardPreparedRestore();
        }

        private static int Compare(INetworkBoundedStateParticipant left, INetworkBoundedStateParticipant right)
        {
            int order = left.RestoreOrder.CompareTo(right.RestoreOrder);
            return order != 0 ? order : left.TypeId.CompareTo(right.TypeId);
        }
    }

    public readonly struct NetworkBoundedStateSlice
    {
        public NetworkBoundedStateSlice(
            uint typeId,
            ushort schema,
            ushort restoreOrder,
            int offset,
            int length,
            long participantHash)
        {
            TypeId = typeId;
            Schema = schema;
            RestoreOrder = restoreOrder;
            Offset = offset;
            Length = length;
            ParticipantHash = participantHash;
        }

        public uint TypeId { get; }
        public ushort Schema { get; }
        public ushort RestoreOrder { get; }
        public int Offset { get; }
        public int Length { get; }
        public long ParticipantHash { get; }
    }

    public interface INetworkBoundedRollbackParticipant
    {
        uint TypeId { get; }
        ushort RestoreOrder { get; }
        int StateBytes { get; }

        /// <remarks>NoAlloc. Failure must not mutate the participant's live state.</remarks>
        NetworkBoundedResult TryCaptureRollbackState(RuntimeFrame frame, Span<byte> destination);

        /// <remarks>NoAlloc. Prepares a candidate without mutating live state.</remarks>
        NetworkBoundedResult TryPrepareRollbackState(RuntimeFrame frame, ReadOnlySpan<byte> state);

        /// <remarks>NoAlloc and no-fail after a successful prepare.</remarks>
        void CommitPreparedRollbackState();

        /// <remarks>NoAlloc and idempotent, including when no candidate is prepared.</remarks>
        void DiscardPreparedRollbackState();
    }

    /// <summary>Preallocated, participant-ordered rollback history for protocol-2 sessions.</summary>
    public sealed class NetworkBoundedRollbackHistory
    {
        private readonly int _capacity;
        private readonly INetworkBoundedRollbackParticipant[] _participants;
        private readonly int[] _participantOffsets;
        private readonly RuntimeFrame[] _frames;
        private INetworkBoundedRollbackParticipant[] _sealedParticipants;
        private byte[] _states;
        private byte[] _recordScratch;
        private int _participantCount;
        private int _stateBytesPerFrame;
        private int _next;
        private int _count;
        private bool _sealed;

        public NetworkBoundedRollbackHistory(int historyCapacity = 128, int participantCapacity = 8)
        {
            if (historyCapacity < 1) throw new ArgumentOutOfRangeException(nameof(historyCapacity));
            if (participantCapacity < 1 || participantCapacity > 8) throw new ArgumentOutOfRangeException(nameof(participantCapacity));
            _capacity = historyCapacity;
            _participants = new INetworkBoundedRollbackParticipant[participantCapacity];
            _participantOffsets = new int[participantCapacity];
            _frames = new RuntimeFrame[historyCapacity];
        }

        public int Capacity => _capacity;
        public int Count => _count;
        public int StateBytesPerFrame => _stateBytesPerFrame;

        /// <remarks>AllocByDesign initialization operation.</remarks>
        public void Register(INetworkBoundedRollbackParticipant participant)
        {
            if (_sealed) throw new InvalidOperationException("Bounded rollback history is sealed.");
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            if (participant.StateBytes < 1) throw new ArgumentOutOfRangeException(nameof(participant));
            if (_participantCount >= _participants.Length) throw new InvalidOperationException("Bounded rollback participant capacity exceeded.");
            _participants[_participantCount++] = participant;
        }

        /// <remarks>AllocByDesign. Allocates the complete history slab once.</remarks>
        public void Seal()
        {
            if (_sealed) return;
            for (int i = 1; i < _participantCount; i++)
            {
                INetworkBoundedRollbackParticipant value = _participants[i];
                int j = i - 1;
                while (j >= 0 && Compare(_participants[j], value) > 0)
                {
                    _participants[j + 1] = _participants[j];
                    j--;
                }
                _participants[j + 1] = value;
            }
            _sealedParticipants = new INetworkBoundedRollbackParticipant[_participantCount];
            Array.Copy(_participants, _sealedParticipants, _participantCount);
            int offset = 0;
            for (int i = 0; i < _participantCount; i++)
            {
                _participantOffsets[i] = offset;
                offset = checked(offset + _sealedParticipants[i].StateBytes);
            }
            _stateBytesPerFrame = offset;
            _states = new byte[checked(_capacity * _stateBytesPerFrame)];
            _recordScratch = new byte[_stateBytesPerFrame];
            _sealed = true;
        }

        /// <remarks>NoAlloc after <see cref="Seal"/>.</remarks>
        public NetworkBoundedResult TryRecord(RuntimeFrame frame)
        {
            if (!_sealed || frame.Value < 0L || frame.Value == long.MaxValue)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidArgument);
            for (int i = 0; i < _sealedParticipants.Length; i++)
            {
                INetworkBoundedRollbackParticipant participant = _sealedParticipants[i];
                Span<byte> destination = _recordScratch.AsSpan(
                    _participantOffsets[i],
                    participant.StateBytes);
                NetworkBoundedResult result = participant.TryCaptureRollbackState(frame, destination);
                if (!result.Success)
                    return result;
            }
            _recordScratch.AsSpan().CopyTo(
                _states.AsSpan(_next * _stateBytesPerFrame, _stateBytesPerFrame));
            _frames[_next] = frame;
            _next = (_next + 1) % _capacity;
            if (_count < _capacity) _count++;
            return NetworkBoundedResult.Succeeded();
        }

        /// <remarks>NoAlloc. Prepares every participant before ordered no-fail commit.</remarks>
        public NetworkBoundedResult TryRestore(RuntimeFrame frame)
        {
            if (!_sealed)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.InvalidArgument);
            int slot = FindFrame(frame);
            if (slot < 0)
                return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.HistoryMissing);
            int frameOffset = slot * _stateBytesPerFrame;
            for (int i = 0; i < _sealedParticipants.Length; i++)
            {
                INetworkBoundedRollbackParticipant participant = _sealedParticipants[i];
                NetworkBoundedResult result = participant.TryPrepareRollbackState(
                    frame,
                    _states.AsSpan(frameOffset + _participantOffsets[i], participant.StateBytes));
                if (!result.Success)
                {
                    DiscardAllPrepared();
                    return result;
                }
            }
            for (int i = 0; i < _sealedParticipants.Length; i++)
                _sealedParticipants[i].CommitPreparedRollbackState();
            return NetworkBoundedResult.Succeeded();
        }

        private void DiscardAllPrepared()
        {
            for (int i = 0; i < _sealedParticipants.Length; i++)
                _sealedParticipants[i].DiscardPreparedRollbackState();
        }

        private int FindFrame(RuntimeFrame frame)
        {
            for (int i = 0; i < _count; i++)
            {
                int slot = (_next - 1 - i + _capacity) % _capacity;
                if (_frames[slot] == frame)
                    return slot;
            }
            return -1;
        }

        private static int Compare(INetworkBoundedRollbackParticipant left, INetworkBoundedRollbackParticipant right)
        {
            int order = left.RestoreOrder.CompareTo(right.RestoreOrder);
            return order != 0 ? order : left.TypeId.CompareTo(right.TypeId);
        }
    }

    public interface INetworkBoundedPacketTransport
    {
        /// <remarks>
        /// NoAlloc after transport initialization. The authenticated metadata must originate from
        /// the transport/session seam and must never be inferred from <paramref name="packet"/>.
        /// </remarks>
        NetworkBoundedResult TrySendBounded(
            ReadOnlySpan<byte> packet,
            in NetworkBoundedAuthenticatedMetadata authenticatedMetadata,
            RuntimeFrame deliveryFrame);

        /// <remarks>
        /// NoAlloc after transport initialization. Returns authenticated identity independently
        /// from the packet bytes copied into <paramref name="destination"/>.
        /// </remarks>
        NetworkBoundedResult TryPollBounded(
            uint receiverPeerHandle,
            RuntimeFrame frame,
            Span<byte> destination,
            out int packetBytes,
            out NetworkBoundedAuthenticatedMetadata authenticatedMetadata);
    }

    public readonly struct NetworkBoundedIngressContext
    {
        public NetworkBoundedIngressContext(
            in NetworkBoundedAuthenticatedMetadata authenticatedMetadata,
            NetworkSessionRole localRole,
            NetworkSessionStatus sessionStatus,
            RuntimeFrame serverFrame,
            long profileHash)
        {
            AuthenticatedMetadata = authenticatedMetadata;
            LocalRole = localRole;
            SessionStatus = sessionStatus;
            ServerFrame = serverFrame;
            ProfileHash = profileHash;
        }

        public NetworkBoundedAuthenticatedMetadata AuthenticatedMetadata { get; }
        public ulong AuthenticatedSessionHandle => AuthenticatedMetadata.SessionHandle;
        public uint AuthenticatedRemotePeerHandle => AuthenticatedMetadata.RemotePeerHandle;
        public uint AuthenticatedLocalPeerHandle => AuthenticatedMetadata.LocalPeerHandle;
        public NetworkBoundedPacketDirection AuthenticatedDirection => AuthenticatedMetadata.Direction;
        public NetworkSessionRole LocalRole { get; }
        public NetworkSessionStatus SessionStatus { get; }
        public RuntimeFrame ServerFrame { get; }
        public long ProfileHash { get; }
    }

    public interface INetworkBoundedPacketIngress
    {
        /// <remarks>NoAlloc. The packet is valid only for the duration of the call.</remarks>
        NetworkBoundedResult TryAdmit(
            ReadOnlySpan<byte> packet,
            in NetworkBoundedIngressContext context);
    }
}
