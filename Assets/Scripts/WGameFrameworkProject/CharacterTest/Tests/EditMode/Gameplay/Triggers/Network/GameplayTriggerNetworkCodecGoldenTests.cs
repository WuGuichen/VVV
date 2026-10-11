using System;
using System.Buffers.Binary;
using MxFramework.Network;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers.Network
{
    public sealed class GameplayTriggerNetworkCodecGoldenTests
    {
        [Test]
        public void OuterWireUsesFrozenOffsetsAndRejectsCorruptionTrailingAndLimits()
        {
            byte[] payload = { 0x11, 0x22, 0x33, 0x44 };
            var header = new NetworkBoundedPacketHeader(
                NetworkMessageKind.BoundedRuntimeCommands,
                0x0102030405060708UL,
                0x11223344U,
                0x55667788U,
                7L,
                99L,
                payload.Length);
            var packet = new byte[NetworkBoundedWire.HeaderBytes + payload.Length];

            NetworkBoundedResult encoded = NetworkBoundedWire.TryEncode(
                header,
                payload,
                packet,
                out int encodedBytes);

            Assert.IsTrue(encoded.Success, encoded.ErrorCode.ToString());
            Assert.AreEqual(56, encodedBytes);
            Assert.AreEqual(0x4D584E31U, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(0, 4)));
            Assert.AreEqual(1, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(4, 2)));
            Assert.AreEqual(52, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(6, 2)));
            Assert.AreEqual(10, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(8, 2)));
            Assert.AreEqual(0, packet[10]);
            Assert.AreEqual(0, packet[11]);
            Assert.AreEqual(0x0102030405060708UL, BinaryPrimitives.ReadUInt64LittleEndian(packet.AsSpan(12, 8)));
            Assert.AreEqual(0x11223344U, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(20, 4)));
            Assert.AreEqual(0x55667788U, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(24, 4)));
            Assert.AreEqual(7L, BinaryPrimitives.ReadInt64LittleEndian(packet.AsSpan(28, 8)));
            Assert.AreEqual(99L, BinaryPrimitives.ReadInt64LittleEndian(packet.AsSpan(36, 8)));
            Assert.AreEqual(4U, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(44, 4)));
            Assert.AreEqual(0x44332211U, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(52, 4)));
            Assert.AreEqual(0x532E3CA8U, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(48, 4)));

            NetworkBoundedResult decoded = NetworkBoundedWire.TryDecode(packet, out NetworkBoundedPacketHeader decodedHeader);
            Assert.IsTrue(decoded.Success, decoded.ErrorCode.ToString());
            Assert.AreEqual(header.Kind, decodedHeader.Kind);
            Assert.AreEqual(header.SessionHandle, decodedHeader.SessionHandle);
            Assert.AreEqual(header.SenderPeerHandle, decodedHeader.SenderPeerHandle);
            Assert.AreEqual(header.ReceiverPeerHandle, decodedHeader.ReceiverPeerHandle);
            Assert.AreEqual(header.OuterSequence, decodedHeader.OuterSequence);
            Assert.AreEqual(header.Frame, decodedHeader.Frame);
            Assert.AreEqual(header.PayloadBytes, decodedHeader.PayloadBytes);

            byte[] corrupt = (byte[])packet.Clone();
            corrupt[52] ^= 0x80;
            Assert.AreEqual(NetworkBoundedErrorCode.CrcMismatch, NetworkBoundedWire.TryDecode(corrupt, out _).ErrorCode);
            byte[] reserved = (byte[])packet.Clone();
            reserved[11] = 1;
            Assert.AreEqual(NetworkBoundedErrorCode.ReservedNonZero, NetworkBoundedWire.TryDecode(reserved, out _).ErrorCode);
            byte[] trailing = new byte[packet.Length + 1];
            packet.CopyTo(trailing, 0);
            Assert.AreEqual(NetworkBoundedErrorCode.InvalidLength, NetworkBoundedWire.TryDecode(trailing, out _).ErrorCode);

            var oversizedPayload = new byte[NetworkBoundedWire.CommandTotalLimit - NetworkBoundedWire.HeaderBytes + 1];
            var oversizedHeader = new NetworkBoundedPacketHeader(
                NetworkMessageKind.BoundedRuntimeCommands,
                1,
                1,
                2,
                0,
                0,
                oversizedPayload.Length);
            Assert.AreEqual(
                NetworkBoundedErrorCode.PacketTooLarge,
                NetworkBoundedWire.TryEncode(oversizedHeader, oversizedPayload, new byte[8193], out _).ErrorCode);
        }

        [Test]
        public void OuterAndSemanticFrontiersAdvanceAtomicallyAndIndependently()
        {
            var frontiers = new NetworkBoundedPacketFrontiers();
            Assert.IsTrue(frontiers.TryAccept(0, NetworkBoundedSequenceStream.ClientInput, 0).Success);
            Assert.IsTrue(frontiers.TryAccept(1, NetworkBoundedSequenceStream.Snapshot, 0).Success);

            NetworkBoundedResult semanticFailure = frontiers.TryAccept(
                2,
                NetworkBoundedSequenceStream.ClientInput,
                0);
            Assert.AreEqual(NetworkBoundedErrorCode.DuplicateOrStale, semanticFailure.ErrorCode);
            Assert.AreEqual(2L, frontiers.ExpectedOuterSequence, "outer frontier must not advance on semantic failure");
            Assert.AreEqual(1L, frontiers.GetExpectedSemanticSequence(NetworkBoundedSequenceStream.ClientInput));

            Assert.IsTrue(frontiers.TryAccept(2, NetworkBoundedSequenceStream.ClientInput, 1).Success);
            Assert.AreEqual(NetworkBoundedErrorCode.OutOfOrder,
                frontiers.TryAccept(4, NetworkBoundedSequenceStream.StateRequest, 0).ErrorCode);
            Assert.AreEqual(3L, frontiers.ExpectedOuterSequence);
        }

        [Test]
        public void ProtocolTwoNegotiationIsExactAndNeverDowngrades()
        {
            NetworkSessionConfig protocol2 = CreateConfig(2);
            NetworkSessionConfig protocol1 = CreateConfig(1);
            Assert.IsTrue(NetworkBoundedSessionNegotiation.TryNegotiate(
                protocol2, protocol2, 77, 77, true, true).Success);
            Assert.AreEqual(NetworkBoundedErrorCode.UnsupportedProtocol,
                NetworkBoundedSessionNegotiation.TryNegotiate(
                    protocol2, protocol1, 77, 77, true, true).ErrorCode);
            Assert.AreEqual(NetworkBoundedErrorCode.CapabilityMismatch,
                NetworkBoundedSessionNegotiation.TryNegotiate(
                    protocol2, protocol2, 77, 77, true, false).ErrorCode);
            Assert.AreEqual(NetworkBoundedErrorCode.ProfileMismatch,
                NetworkBoundedSessionNegotiation.TryNegotiate(
                    protocol2, protocol2, 77, 78, true, true).ErrorCode);
        }

        [Test]
        public void StateAndRollbackUseExplicitGameplayThenTriggerOrder()
        {
            var restoreLog = new int[2];
            var registry = new NetworkBoundedStateRegistry(2);
            var trigger = new StateParticipant(1002302, 1, 200, restoreLog);
            var gameplay = new StateParticipant(1002301, 1, 100, restoreLog);
            registry.Register(trigger);
            registry.Register(gameplay);
            registry.Seal();
            var state = new byte[] { 11, 22 };
            var slices = new[]
            {
                new NetworkBoundedStateSlice(1002301, 1, 100, 0, 1, 11),
                new NetworkBoundedStateSlice(1002302, 1, 200, 1, 1, 22)
            };
            Assert.IsTrue(registry.TryPrepareAndCommit(slices, state).Success);
            CollectionAssert.AreEqual(new[] { 100, 200 }, restoreLog);

            var rollbackLog = new int[2];
            var history = new NetworkBoundedRollbackHistory(2, 2);
            history.Register(new RollbackParticipant(1002302, 200, rollbackLog));
            history.Register(new RollbackParticipant(1002301, 100, rollbackLog));
            history.Seal();
            Assert.IsTrue(history.TryRecord(new RuntimeFrame(10)).Success);
            Assert.IsTrue(history.TryRestore(new RuntimeFrame(10)).Success);
            CollectionAssert.AreEqual(new[] { 100, 200 }, rollbackLog);
            Assert.AreEqual(NetworkBoundedErrorCode.HistoryMissing,
                history.TryRestore(new RuntimeFrame(9)).ErrorCode);
        }

        [Test]
        public void FullRollbackRingCaptureFailurePreservesOverwrittenFrameExactly()
        {
            var gameplay = new AtomicRollbackParticipant(1002301, 100, initialValue: 11);
            var trigger = new AtomicRollbackParticipant(1002302, 200, initialValue: 22);
            var history = new NetworkBoundedRollbackHistory(1, 2);
            history.Register(gameplay);
            history.Register(trigger);
            history.Seal();
            Assert.IsTrue(history.TryRecord(new RuntimeFrame(10)).Success);

            gameplay.LiveValue = 31;
            trigger.LiveValue = 32;
            trigger.FailCapture = true;
            Assert.AreEqual(
                NetworkBoundedErrorCode.ParticipantCaptureFailed,
                history.TryRecord(new RuntimeFrame(11)).ErrorCode);
            Assert.AreEqual(1, history.Count);

            gameplay.LiveValue = 91;
            trigger.LiveValue = 92;
            trigger.FailCapture = false;
            Assert.IsTrue(history.TryRestore(new RuntimeFrame(10)).Success);
            Assert.AreEqual(11, gameplay.LiveValue);
            Assert.AreEqual(22, trigger.LiveValue);
            Assert.AreEqual(
                NetworkBoundedErrorCode.HistoryMissing,
                history.TryRestore(new RuntimeFrame(11)).ErrorCode);
        }

        [Test]
        public void TriggerPrepareFailureDiscardsGameplayCandidateAndPreservesBothLiveStates()
        {
            var commitLog = new int[2];
            var gameplay = new AtomicRollbackParticipant(1002301, 100, initialValue: 11, commitLog: commitLog);
            var trigger = new AtomicRollbackParticipant(1002302, 200, initialValue: 22, commitLog: commitLog);
            var history = new NetworkBoundedRollbackHistory(2, 2);
            history.Register(trigger);
            history.Register(gameplay);
            history.Seal();
            Assert.IsTrue(history.TryRecord(new RuntimeFrame(10)).Success);

            gameplay.LiveValue = 91;
            trigger.LiveValue = 92;
            trigger.FailPrepare = true;
            Assert.AreEqual(
                NetworkBoundedErrorCode.ParticipantValidationFailed,
                history.TryRestore(new RuntimeFrame(10)).ErrorCode);
            Assert.AreEqual(91, gameplay.LiveValue);
            Assert.AreEqual(92, trigger.LiveValue);
            Assert.AreEqual(0, commitLog[0]);
            Assert.AreEqual(0, commitLog[1]);
            Assert.AreEqual(1, gameplay.DiscardCount);
            Assert.AreEqual(1, trigger.DiscardCount);

            trigger.FailPrepare = false;
            Assert.IsTrue(history.TryRestore(new RuntimeFrame(10)).Success);
            Assert.AreEqual(11, gameplay.LiveValue);
            Assert.AreEqual(22, trigger.LiveValue);
            CollectionAssert.AreEqual(new[] { 100, 200 }, commitLog);
        }

        [Test]
        public void SuccessfulRollbackRecordAndPrepareCommitRestoreAreNoAllocAndOrdered()
        {
            var commitLog = new int[2];
            var gameplay = new AtomicRollbackParticipant(1002301, 100, initialValue: 1, commitLog: commitLog);
            var trigger = new AtomicRollbackParticipant(1002302, 200, initialValue: 2, commitLog: commitLog);
            var history = new NetworkBoundedRollbackHistory(2, 2);
            history.Register(trigger);
            history.Register(gameplay);
            history.Seal();
            Assert.IsTrue(history.TryRecord(RuntimeFrame.Zero).Success);
            gameplay.LiveValue = 9;
            trigger.LiveValue = 9;
            Assert.IsTrue(history.TryRestore(RuntimeFrame.Zero).Success);
            commitLog[0] = 0;
            commitLog[1] = 0;
            _ = GC.GetAllocatedBytesForCurrentThread();

            bool succeeded = true;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 1; i <= 64; i++)
            {
                gameplay.LiveValue = (byte)i;
                trigger.LiveValue = (byte)(i + 1);
                succeeded &= history.TryRecord(new RuntimeFrame(i)).Success;
                gameplay.LiveValue = 0;
                trigger.LiveValue = 0;
                succeeded &= history.TryRestore(new RuntimeFrame(i)).Success;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(succeeded);
            Assert.AreEqual(0L, allocated);
            CollectionAssert.AreEqual(new[] { 100, 200 }, commitLog);
            Assert.AreEqual(64, gameplay.LiveValue);
            Assert.AreEqual(65, trigger.LiveValue);
        }

        [Test]
        public void ExistingCodecLoopbackCarriesBoundedPacketWithoutAllocationAfterConfiguration()
        {
            var transport = new CodecLoopbackNetworkTransport();
            transport.ConfigureBounded(2, 256);
            byte[] payload = { 9, 8, 7 };
            var packet = new byte[NetworkBoundedWire.HeaderBytes + payload.Length];
            var header = new NetworkBoundedPacketHeader(
                NetworkMessageKind.BoundedStateRequest,
                1,
                1,
                2,
                0,
                0,
                payload.Length);
            Assert.IsTrue(NetworkBoundedWire.TryEncode(header, payload, packet, out _).Success);
            var metadata = new NetworkBoundedAuthenticatedMetadata(
                1,
                1,
                2,
                NetworkBoundedPacketDirection.ClientToServer);
            Assert.IsTrue(transport.TrySendBounded(packet, metadata, RuntimeFrame.Zero).Success);
            var destination = new byte[256];
            Assert.IsTrue(transport.TryPollBounded(
                2,
                RuntimeFrame.Zero,
                destination,
                out int bytes,
                out NetworkBoundedAuthenticatedMetadata receivedMetadata).Success);
            Assert.AreEqual(packet.Length, bytes);
            Assert.AreEqual(metadata.SessionHandle, receivedMetadata.SessionHandle);
            Assert.AreEqual(metadata.RemotePeerHandle, receivedMetadata.RemotePeerHandle);
            Assert.AreEqual(metadata.LocalPeerHandle, receivedMetadata.LocalPeerHandle);
            Assert.AreEqual(metadata.Direction, receivedMetadata.Direction);
            CollectionAssert.AreEqual(packet, destination.AsSpan(0, bytes).ToArray());
        }

        [Test]
        public void BoundedLoopbackRoutesByIndependentAuthenticatedMetadataInsteadOfPacketHeader()
        {
            var transport = new CodecLoopbackNetworkTransport();
            transport.ConfigureBounded(1, 256);
            byte[] payload = { 1 };
            var packet = new byte[NetworkBoundedWire.HeaderBytes + payload.Length];
            var forgedHeader = new NetworkBoundedPacketHeader(
                NetworkMessageKind.BoundedStateRequest,
                1,
                7,
                2,
                0,
                0,
                payload.Length);
            Assert.IsTrue(NetworkBoundedWire.TryEncode(forgedHeader, payload, packet, out _).Success);
            var authenticated = new NetworkBoundedAuthenticatedMetadata(
                55,
                8,
                9,
                NetworkBoundedPacketDirection.ClientToServer);
            Assert.IsTrue(transport.TrySendBounded(packet, authenticated, RuntimeFrame.Zero).Success);

            var destination = new byte[256];
            Assert.IsTrue(transport.TryPollBounded(
                2,
                RuntimeFrame.Zero,
                destination,
                out int forgedReceiverBytes,
                out _).Success);
            Assert.AreEqual(0, forgedReceiverBytes, "packet header receiver must not route the transport queue");
            Assert.IsTrue(transport.TryPollBounded(
                9,
                RuntimeFrame.Zero,
                destination,
                out int authenticatedReceiverBytes,
                out NetworkBoundedAuthenticatedMetadata received).Success);
            Assert.AreEqual(packet.Length, authenticatedReceiverBytes);
            Assert.AreEqual(authenticated.SessionHandle, received.SessionHandle);
            Assert.AreEqual(authenticated.RemotePeerHandle, received.RemotePeerHandle);
            Assert.AreEqual(authenticated.LocalPeerHandle, received.LocalPeerHandle);
            Assert.AreEqual(authenticated.Direction, received.Direction);
        }

        private static NetworkSessionConfig CreateConfig(int protocol)
        {
            return new NetworkSessionConfig(protocol, 60, 2, 2, "catalog", "resources");
        }

        private sealed class StateParticipant : INetworkBoundedStateParticipant
        {
            private readonly int[] _log;
            private bool _prepared;

            public StateParticipant(uint typeId, ushort schema, ushort restoreOrder, int[] log)
            {
                TypeId = typeId;
                Schema = schema;
                RestoreOrder = restoreOrder;
                _log = log;
            }

            public uint TypeId { get; }
            public ushort Schema { get; }
            public ushort RestoreOrder { get; }
            public int MaxStateBytes => 1;
            public NetworkBoundedResult TryCapture(Span<byte> destination, out int bytesWritten, out long participantHash)
            {
                destination[0] = (byte)RestoreOrder;
                bytesWritten = 1;
                participantHash = RestoreOrder;
                return NetworkBoundedResult.Succeeded();
            }
            public NetworkBoundedResult TryPrepareRestore(ReadOnlySpan<byte> state, long expectedParticipantHash)
            {
                _prepared = state.Length == 1 && state[0] == expectedParticipantHash;
                return _prepared
                    ? NetworkBoundedResult.Succeeded()
                    : NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantValidationFailed);
            }
            public void CommitPreparedRestore()
            {
                Assert.IsTrue(_prepared);
                int index = _log[0] == 0 ? 0 : 1;
                _log[index] = RestoreOrder;
                _prepared = false;
            }
            public void DiscardPreparedRestore() { _prepared = false; }
        }

        private sealed class RollbackParticipant : INetworkBoundedRollbackParticipant
        {
            private readonly int[] _log;
            public RollbackParticipant(uint typeId, ushort order, int[] log)
            {
                TypeId = typeId;
                RestoreOrder = order;
                _log = log;
            }
            public uint TypeId { get; }
            public ushort RestoreOrder { get; }
            public int StateBytes => 1;
            public NetworkBoundedResult TryCaptureRollbackState(RuntimeFrame frame, Span<byte> destination)
            {
                destination[0] = (byte)frame.Value;
                return NetworkBoundedResult.Succeeded();
            }
            public NetworkBoundedResult TryPrepareRollbackState(RuntimeFrame frame, ReadOnlySpan<byte> state)
            {
                if (state.Length != 1 || state[0] != (byte)frame.Value)
                    return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantValidationFailed);
                return NetworkBoundedResult.Succeeded();
            }
            public void CommitPreparedRollbackState()
            {
                int index = _log[0] == 0 ? 0 : 1;
                _log[index] = RestoreOrder;
            }
            public void DiscardPreparedRollbackState() { }
        }

        private sealed class AtomicRollbackParticipant : INetworkBoundedRollbackParticipant
        {
            private readonly int[] _commitLog;
            private bool _prepared;
            private byte _preparedValue;

            public AtomicRollbackParticipant(
                uint typeId,
                ushort restoreOrder,
                byte initialValue,
                int[] commitLog = null)
            {
                TypeId = typeId;
                RestoreOrder = restoreOrder;
                LiveValue = initialValue;
                _commitLog = commitLog;
            }

            public uint TypeId { get; }
            public ushort RestoreOrder { get; }
            public int StateBytes => 1;
            public byte LiveValue { get; set; }
            public bool FailCapture { get; set; }
            public bool FailPrepare { get; set; }
            public int DiscardCount { get; private set; }

            public NetworkBoundedResult TryCaptureRollbackState(RuntimeFrame frame, Span<byte> destination)
            {
                destination[0] = LiveValue;
                return FailCapture
                    ? NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantCaptureFailed)
                    : NetworkBoundedResult.Succeeded();
            }

            public NetworkBoundedResult TryPrepareRollbackState(RuntimeFrame frame, ReadOnlySpan<byte> state)
            {
                if (state.Length != 1)
                    return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantValidationFailed);
                _preparedValue = state[0];
                _prepared = true;
                return FailPrepare
                    ? NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantValidationFailed)
                    : NetworkBoundedResult.Succeeded();
            }

            public void CommitPreparedRollbackState()
            {
                if (!_prepared)
                    throw new InvalidOperationException("Rollback participant committed without prepare.");
                LiveValue = _preparedValue;
                _prepared = false;
                if (_commitLog == null)
                    return;
                int index = _commitLog[0] == 0 ? 0 : 1;
                _commitLog[index] = RestoreOrder;
            }

            public void DiscardPreparedRollbackState()
            {
                _prepared = false;
                DiscardCount++;
            }
        }
    }
}
