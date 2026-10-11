using System;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Network;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers.NetworkBridge
{
    /// <summary>Atomic protocol-2 client-observation ingress for the existing Runtime command gate.</summary>
    public sealed class GameplayTriggerNetworkAuthorityIngress : INetworkBoundedPacketIngress
    {
        private readonly GameplayTriggerNetworkProfile _profile;
        private readonly IGameplayTriggerNetworkPeerDirectory _peers;
        private readonly IGameplayTriggerNetworkOwnershipProvider _ownership;
        private readonly RuntimeCommandSubmissionGate _externalGate;
        private readonly NetworkBoundedPacketFrontiers _frontiers = new NetworkBoundedPacketFrontiers();
        private readonly RuntimeCommand[] _commands = new RuntimeCommand[GameplayTriggerNetworkCommandCodec.MaximumRecords];

        public GameplayTriggerNetworkAuthorityIngress(
            GameplayTriggerNetworkProfile profile,
            IGameplayTriggerNetworkPeerDirectory peers,
            IGameplayTriggerNetworkOwnershipProvider ownership,
            RuntimeCommandSubmissionGate externalGate)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _peers = peers ?? throw new ArgumentNullException(nameof(peers));
            _ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
            _externalGate = externalGate ?? throw new ArgumentNullException(nameof(externalGate));
            if (!_externalGate.IsTargetPendingCapacityBounded)
                throw new ArgumentException("Network authority ingress requires a hard-bounded Runtime command buffer.", nameof(externalGate));
        }

        public GameplayTriggerNetworkResult LastResult { get; private set; }
        public RuntimeFrame LastAcceptedClientFrame { get; private set; } = RuntimeFrame.Zero;
        public uint LastAcceptedPeerHandle { get; private set; }
        public long ExpectedOuterSequence => _frontiers.ExpectedOuterSequence;
        public long ExpectedClientPacketSequence => _frontiers.GetExpectedSemanticSequence(NetworkBoundedSequenceStream.ClientInput);

        /// <remarks>NoAlloc after initialization. Failure advances no frontier and admits no Runtime command.</remarks>
        public NetworkBoundedResult TryAdmit(ReadOnlySpan<byte> packet, in NetworkBoundedIngressContext context)
        {
            LastResult = default;
            NetworkBoundedResult wire = NetworkBoundedWire.TryDecode(packet, out NetworkBoundedPacketHeader header);
            if (!wire.Success)
                return Fail(GameplayTriggerNetworkErrorCode.InvalidPacket, (int)wire.ErrorCode);
            if (context.LocalRole != NetworkSessionRole.Server
                || context.SessionStatus != NetworkSessionStatus.Connected
                || context.AuthenticatedDirection != NetworkBoundedPacketDirection.ClientToServer
                || header.Kind != NetworkMessageKind.BoundedRuntimeCommands)
            {
                return Fail(GameplayTriggerNetworkErrorCode.UnauthorizedDirection);
            }
            if (!context.AuthenticatedMetadata.IsValid
                || header.SessionHandle != context.AuthenticatedSessionHandle
                || header.SenderPeerHandle != context.AuthenticatedRemotePeerHandle
                || header.ReceiverPeerHandle != context.AuthenticatedLocalPeerHandle)
                return Fail(GameplayTriggerNetworkErrorCode.IdentityMismatch);
            if (!_peers.IsConnected(context.AuthenticatedRemotePeerHandle))
                return Fail(GameplayTriggerNetworkErrorCode.UnknownPeer);
            if (context.ProfileHash != _profile.NetworkProfileHash)
                return Fail(GameplayTriggerNetworkErrorCode.IdentityMismatch);

            GameplayTriggerNetworkResult decode = GameplayTriggerNetworkCommandCodec.TryDecodeRuntimePayload(
                _profile,
                packet.Slice(NetworkBoundedWire.HeaderBytes, header.PayloadBytes),
                _commands,
                out RuntimeFrame clientFrame,
                out long packetSequence,
                out int commandCount);
            if (!decode.Success)
                return Fail(decode.ErrorCode, decode.Detail);
            if (header.Frame != clientFrame.Value)
                return Fail(GameplayTriggerNetworkErrorCode.InvalidPacket);
            NetworkBoundedResult frame = NetworkBoundedFrameWindow.TryValidate(context.ServerFrame.Value, clientFrame.Value);
            if (!frame.Success)
                return Fail(GameplayTriggerNetworkErrorCode.FrameRejected, (int)frame.ErrorCode);
            NetworkBoundedResult staged = _frontiers.TryStage(
                header.OuterSequence,
                NetworkBoundedSequenceStream.ClientInput,
                packetSequence,
                out NetworkBoundedFrontierStage frontierStage);
            if (!staged.Success)
                return Fail(GameplayTriggerNetworkErrorCode.InvalidPacket, (int)staged.ErrorCode);

            for (int i = 0; i < commandCount; i++)
            {
                GameplayTriggerNetworkResult authorization = Authorize(context.AuthenticatedRemotePeerHandle, _commands[i]);
                if (!authorization.Success)
                    return Fail(authorization.ErrorCode, i);
            }
            GameplayTriggerNetworkResult batches = ValidateCompleteBatches(commandCount);
            if (!batches.Success)
                return Fail(batches.ErrorCode, batches.Detail);

            RuntimeCommandBatchSubmissionResult admission = _externalGate.SubmitBatch(_commands.AsSpan(0, commandCount));
            if (!admission.Success)
                return Fail(GameplayTriggerNetworkErrorCode.AdmissionRejected, admission.FailedCommandIndex);
            _frontiers.Commit(frontierStage);
            LastAcceptedClientFrame = clientFrame;
            LastAcceptedPeerHandle = context.AuthenticatedRemotePeerHandle;
            LastResult = GameplayTriggerNetworkResult.Succeeded();
            return NetworkBoundedResult.Succeeded();
        }

        private GameplayTriggerNetworkResult Authorize(uint peerHandle, RuntimeCommand command)
        {
            if (command.Envelope.Provenance != RuntimeCommandProvenanceKind.Exogenous)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.ForgedProvenance);
            if (!_profile.TryGetSourcePolicy(command.SourceId, out GameplayTriggerNetworkSourcePolicy policy))
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.UnauthorizedSource);
            switch (command.CommandId)
            {
                case GameplayTriggerRuntimeCommandIds.SubmitSignalBatch:
                    if ((policy.Observations & GameplayTriggerNetworkObservationMask.Signal) == 0)
                        return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.UnauthorizedCommand);
                    if (!GameplayTriggerSignalCommandCodec.TryDecodeRecord(command, out GameplayTriggerSignalCommandRecord signal, out _))
                        return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidRecord);
                    if (!signal.IsEmptyBatch && (!Owns(peerHandle, signal.Signal.Subject) || !Owns(peerHandle, signal.Signal.Instigator)))
                        return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.OwnershipMismatch);
                    return GameplayTriggerNetworkResult.Succeeded();
                case GameplayTriggerRuntimeCommandIds.SubmitOccupancyBatch:
                    if ((policy.Observations & GameplayTriggerNetworkObservationMask.Occupancy) == 0)
                        return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.UnauthorizedCommand);
                    if (!GameplayTriggerOccupancyCommandCodec.TryDecodeRecord(command, out GameplayTriggerOccupancyCommandRecord occupancy, out _))
                        return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidRecord);
                    if (!occupancy.IsEmptyBatch && !Owns(peerHandle, occupancy.Subject))
                        return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.OwnershipMismatch);
                    return GameplayTriggerNetworkResult.Succeeded();
                case GameplayTriggerRuntimeCommandIds.SubmitScalarBatch:
                    if ((policy.Observations & GameplayTriggerNetworkObservationMask.Scalar) == 0)
                        return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.UnauthorizedCommand);
                    if (!GameplayTriggerScalarCommandCodec.TryDecodeRecord(command, out GameplayTriggerScalarCommandRecord scalar, out _))
                        return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidRecord);
                    if (!scalar.IsEmptyBatch && !Owns(peerHandle, scalar.Sample.Subject))
                        return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.OwnershipMismatch);
                    return GameplayTriggerNetworkResult.Succeeded();
                default:
                    return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.UnauthorizedCommand);
            }
        }

        private GameplayTriggerNetworkResult ValidateCompleteBatches(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryGetBatchIdentity(_commands[i], out long batchId, out int itemIndex, out int itemCount))
                    return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidRecord, i);
                if (itemCount == 0)
                    continue;
                int matching = 0;
                for (int j = 0; j < count; j++)
                {
                    if (_commands[j].CommandId != _commands[i].CommandId || _commands[j].TargetId != _commands[i].TargetId)
                        continue;
                    if (!TryGetBatchIdentity(_commands[j], out long candidateBatch, out int candidateIndex, out int candidateCount)
                        || candidateBatch != batchId || candidateCount != itemCount)
                        continue;
                    if (candidateIndex == itemIndex && j != i)
                        return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidRecord, j);
                    matching++;
                }
                if (matching != itemCount)
                    return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidRecord, i);
            }
            return GameplayTriggerNetworkResult.Succeeded();
        }

        private static bool TryGetBatchIdentity(RuntimeCommand command, out long batchId, out int itemIndex, out int itemCount)
        {
            switch (command.CommandId)
            {
                case GameplayTriggerRuntimeCommandIds.SubmitSignalBatch:
                    if (GameplayTriggerSignalCommandCodec.TryDecodeRecord(command, out GameplayTriggerSignalCommandRecord signal, out _))
                    { batchId = signal.BatchId; itemIndex = signal.ItemIndex; itemCount = signal.ItemCount; return true; }
                    break;
                case GameplayTriggerRuntimeCommandIds.SubmitOccupancyBatch:
                    if (GameplayTriggerOccupancyCommandCodec.TryDecodeRecord(command, out GameplayTriggerOccupancyCommandRecord occupancy, out _))
                    { batchId = occupancy.BatchId; itemIndex = occupancy.ItemIndex; itemCount = occupancy.ItemCount; return true; }
                    break;
                case GameplayTriggerRuntimeCommandIds.SubmitScalarBatch:
                    if (GameplayTriggerScalarCommandCodec.TryDecodeRecord(command, out GameplayTriggerScalarCommandRecord scalar, out _))
                    { batchId = scalar.BatchId; itemIndex = scalar.ItemIndex; itemCount = scalar.ItemCount; return true; }
                    break;
            }
            batchId = 0; itemIndex = 0; itemCount = 0; return false;
        }

        private bool Owns(uint peerHandle, GameplayEntityId entity) => entity == default || _ownership.IsOwnedBy(peerHandle, entity);

        private NetworkBoundedResult Fail(GameplayTriggerNetworkErrorCode code, int detail = 0)
        {
            LastResult = GameplayTriggerNetworkResult.Failed(code, detail);
            return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantValidationFailed, (int)code);
        }
    }
}
