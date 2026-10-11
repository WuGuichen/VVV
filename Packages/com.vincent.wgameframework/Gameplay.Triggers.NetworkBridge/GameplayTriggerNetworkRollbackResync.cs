using System;
using MxFramework.Network;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers.NetworkBridge
{
    public enum GameplayTriggerNetworkResyncReason : ushort
    {
        HashMismatch = 1,
        MissingHistory = 2,
        CatalogMismatch = 3,
        CorruptSnapshot = 4,
        UnauthorizedSnapshot = 5,
        RestoreFailure = 6
    }

    public readonly struct GameplayTriggerNetworkStateRequest
    {
        public GameplayTriggerNetworkStateRequest(
            long requestSequence,
            RuntimeFrame requestedFrame,
            long lastSnapshotId,
            long localHash,
            long remoteHash,
            GameplayTriggerNetworkResyncReason reason,
            NetworkHashComparability comparability)
        {
            RequestSequence = requestSequence;
            RequestedFrame = requestedFrame;
            LastSnapshotId = lastSnapshotId;
            LocalHash = localHash;
            RemoteHash = remoteHash;
            Reason = reason;
            Comparability = comparability;
        }
        public long RequestSequence { get; }
        public RuntimeFrame RequestedFrame { get; }
        public long LastSnapshotId { get; }
        public long LocalHash { get; }
        public long RemoteHash { get; }
        public GameplayTriggerNetworkResyncReason Reason { get; }
        public NetworkHashComparability Comparability { get; }
    }

    public static class GameplayTriggerNetworkStateRequestCodec
    {
        public const int PayloadBytes = 56;

        /// <remarks>NoAlloc.</remarks>
        public static GameplayTriggerNetworkResult TryEncode(
            GameplayTriggerNetworkProfile profile,
            in GameplayTriggerNetworkStateRequest request,
            Span<byte> destination)
        {
            if (profile == null || destination.Length < PayloadBytes || request.RequestSequence < 0L || request.RequestSequence == long.MaxValue
                || request.RequestedFrame.Value < 0L || request.RequestedFrame.Value == long.MaxValue || request.LastSnapshotId < -1L
                || (request.Comparability != NetworkHashComparability.FullComparable && request.Comparability != NetworkHashComparability.PartialNonComparable)
                || request.Reason < GameplayTriggerNetworkResyncReason.HashMismatch || request.Reason > GameplayTriggerNetworkResyncReason.RestoreFailure)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidPacket);
            Span<byte> payload = destination.Slice(0, PayloadBytes); payload.Clear();
            WriteI64(payload, 0, request.RequestSequence); WriteI64(payload, 8, request.RequestedFrame.Value); WriteI64(payload, 16, request.LastSnapshotId);
            WriteI64(payload, 24, request.LocalHash); WriteI64(payload, 32, request.RemoteHash); WriteI64(payload, 40, profile.NetworkProfileHash);
            WriteU16(payload, 48, (ushort)request.Reason); payload[50] = (byte)request.Comparability;
            return GameplayTriggerNetworkResult.Succeeded();
        }

        /// <remarks>NoAlloc. Rejects all reserved fields and variable tails.</remarks>
        public static GameplayTriggerNetworkResult TryDecode(
            GameplayTriggerNetworkProfile profile,
            ReadOnlySpan<byte> payload,
            out GameplayTriggerNetworkStateRequest request)
        {
            request = default;
            if (profile == null || payload.Length != PayloadBytes)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidLength);
            long sequence = ReadI64(payload, 0); long frame = ReadI64(payload, 8); long lastId = ReadI64(payload, 16);
            ushort reason = ReadU16(payload, 48); byte comparability = payload[50];
            if (sequence < 0 || sequence == long.MaxValue || frame < 0 || frame == long.MaxValue || lastId < -1
                || ReadI64(payload, 40) != profile.NetworkProfileHash || reason < 1 || reason > 6
                || (comparability != 1 && comparability != 2) || payload[51] != 0 || ReadU32(payload, 52) != 0)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidPacket);
            request = new GameplayTriggerNetworkStateRequest(sequence, new RuntimeFrame(frame), lastId,
                ReadI64(payload, 24), ReadI64(payload, 32), (GameplayTriggerNetworkResyncReason)reason, (NetworkHashComparability)comparability);
            return GameplayTriggerNetworkResult.Succeeded();
        }

        private static void WriteU16(Span<byte> s, int o, ushort v) => GameplayTriggerNetworkCommandCodec.WriteU16(s, o, v);
        private static void WriteI64(Span<byte> s, int o, long v) => GameplayTriggerNetworkCommandCodec.WriteI64(s, o, v);
        private static ushort ReadU16(ReadOnlySpan<byte> s, int o) => GameplayTriggerNetworkCommandCodec.ReadU16(s, o);
        private static uint ReadU32(ReadOnlySpan<byte> s, int o) => GameplayTriggerNetworkCommandCodec.ReadU32(s, o);
        private static long ReadI64(ReadOnlySpan<byte> s, int o) => GameplayTriggerNetworkCommandCodec.ReadI64(s, o);
    }

    /// <summary>Authenticated client-side snapshot ingress with atomic bundle publication and idempotency.</summary>
    public sealed class GameplayTriggerNetworkSnapshotIngress : INetworkBoundedPacketIngress
    {
        private readonly GameplayTriggerNetworkProfile _profile;
        private readonly GameplayTriggerNetworkStateBundle _bundle;
        private readonly uint _serverPeerHandle;
        private readonly NetworkBoundedPacketFrontiers _frontiers = new NetworkBoundedPacketFrontiers();

        public GameplayTriggerNetworkSnapshotIngress(
            GameplayTriggerNetworkProfile profile,
            GameplayTriggerNetworkStateBundle bundle,
            uint serverPeerHandle)
        {
            if (serverPeerHandle == 0U) throw new ArgumentOutOfRangeException(nameof(serverPeerHandle));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _serverPeerHandle = serverPeerHandle;
        }

        public GameplayTriggerNetworkResult LastResult { get; private set; }
        public bool ResyncRequested { get; private set; }
        public GameplayTriggerNetworkResyncReason LastResyncReason { get; private set; }
        public long ExpectedOuterSequence => _frontiers.ExpectedOuterSequence;

        /// <remarks>NoAlloc except low-frequency participant candidate creation in the current restore seam.</remarks>
        public NetworkBoundedResult TryAdmit(ReadOnlySpan<byte> packet, in NetworkBoundedIngressContext context)
        {
            NetworkBoundedResult wire = NetworkBoundedWire.TryDecode(packet, out NetworkBoundedPacketHeader header);
            if (!wire.Success)
                return RequestResync(GameplayTriggerNetworkResyncReason.CorruptSnapshot, GameplayTriggerNetworkErrorCode.InvalidPacket);
            if (context.LocalRole == NetworkSessionRole.Server || context.SessionStatus != NetworkSessionStatus.Connected
                || context.AuthenticatedDirection != NetworkBoundedPacketDirection.ServerToClient
                || header.Kind != NetworkMessageKind.BoundedStateSnapshot)
                return RequestResync(GameplayTriggerNetworkResyncReason.UnauthorizedSnapshot, GameplayTriggerNetworkErrorCode.UnauthorizedDirection);
            if (!context.AuthenticatedMetadata.IsValid
                || header.SessionHandle != context.AuthenticatedSessionHandle
                || header.SenderPeerHandle != context.AuthenticatedRemotePeerHandle
                || header.ReceiverPeerHandle != context.AuthenticatedLocalPeerHandle
                || context.AuthenticatedRemotePeerHandle != _serverPeerHandle)
                return RequestResync(GameplayTriggerNetworkResyncReason.UnauthorizedSnapshot, GameplayTriggerNetworkErrorCode.IdentityMismatch);
            if (context.ProfileHash != _profile.NetworkProfileHash)
                return RequestResync(GameplayTriggerNetworkResyncReason.CatalogMismatch, GameplayTriggerNetworkErrorCode.IdentityMismatch);
            ReadOnlySpan<byte> payload = packet.Slice(NetworkBoundedWire.HeaderBytes, header.PayloadBytes);
            if (payload.Length < GameplayTriggerNetworkStateBundle.SnapshotHeaderBytes)
                return RequestResync(GameplayTriggerNetworkResyncReason.CorruptSnapshot, GameplayTriggerNetworkErrorCode.InvalidLength);
            long snapshotId = GameplayTriggerNetworkCommandCodec.ReadI64(payload, 0);
            long snapshotFrame = GameplayTriggerNetworkCommandCodec.ReadI64(payload, 8);
            long snapshotHash = GameplayTriggerNetworkCommandCodec.ReadI64(payload, 104);
            if (snapshotFrame != header.Frame)
                return RequestResync(GameplayTriggerNetworkResyncReason.CorruptSnapshot, GameplayTriggerNetworkErrorCode.InvalidPacket);

            if (snapshotId == _bundle.LastAppliedSnapshotId && snapshotFrame == _bundle.LastAppliedAuthoritativeFrame.Value)
            {
                NetworkBoundedResult outer = _frontiers.TryStageOuter(header.OuterSequence, out long stagedOuter);
                if (!outer.Success) return outer;
                GameplayTriggerNetworkResult duplicate = _bundle.TryApplySnapshotPayload(payload, out _, out long appliedHash);
                if (duplicate.ErrorCode != GameplayTriggerNetworkErrorCode.AlreadyApplied || appliedHash != snapshotHash)
                    return RequestResync(GameplayTriggerNetworkResyncReason.CorruptSnapshot, GameplayTriggerNetworkErrorCode.InvalidPacket);
                _frontiers.CommitOuter(stagedOuter);
                LastResult = duplicate;
                ResyncRequested = false;
                return NetworkBoundedResult.Succeeded();
            }

            NetworkBoundedResult staged = _frontiers.TryStage(header.OuterSequence, NetworkBoundedSequenceStream.Snapshot,
                snapshotId, out NetworkBoundedFrontierStage stage);
            if (!staged.Success) return staged;
            GameplayTriggerNetworkResult apply = _bundle.TryApplySnapshotPayload(payload, out _, out _);
            if (!apply.Success)
                return RequestResync(GameplayTriggerNetworkResyncReason.RestoreFailure, apply.ErrorCode);
            _frontiers.Commit(stage);
            LastResult = GameplayTriggerNetworkResult.Succeeded();
            ResyncRequested = false;
            return NetworkBoundedResult.Succeeded();
        }

        public GameplayTriggerNetworkResult TryCompareFullHash(long localHash, long authoritativeHash, out bool equal)
        {
            equal = false;
            if (!_bundle.CanCompareFullWorldHash)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.ResyncRequired);
            equal = localHash == authoritativeHash;
            return GameplayTriggerNetworkResult.Succeeded();
        }

        private NetworkBoundedResult RequestResync(GameplayTriggerNetworkResyncReason reason, GameplayTriggerNetworkErrorCode error)
        {
            ResyncRequested = true; LastResyncReason = reason; LastResult = GameplayTriggerNetworkResult.Failed(error);
            return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantValidationFailed, (int)error);
        }
    }

    public delegate GameplayTriggerNetworkResult GameplayTriggerNetworkReplayFrame(RuntimeFrame frame);

    /// <summary>Bounded Trigger-participating rollback/replay facade over the existing Network coordinator.</summary>
    public sealed class GameplayTriggerNetworkRollbackResync
    {
        private readonly NetworkPredictionRollbackCoordinator _coordinator;

        public GameplayTriggerNetworkRollbackResync(
            NetworkPredictionRollbackCoordinator coordinator,
            GameplayTriggerNetworkStateBundle bundle,
            int historyCapacity = NetworkPredictionRollbackCoordinator.DefaultHistoryCapacity)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            _coordinator.ConfigureBoundedHistory(historyCapacity, 2);
            _coordinator.RegisterBoundedParticipant(bundle.TriggerRollbackParticipant);
            _coordinator.RegisterBoundedParticipant(bundle.GameplayRollbackParticipant);
            _coordinator.SealBoundedHistory();
        }

        /// <remarks>NoAlloc.</remarks>
        public int HistoryCount => _coordinator.BoundedHistoryCount;

        /// <remarks>NoAlloc after initialization.</remarks>
        public NetworkBoundedResult TryRecord(RuntimeFrame frame) => _coordinator.TryRecordBoundedFrame(frame);

        /// <remarks>NoAlloc after initialization when the replay callback is NoAlloc.</remarks>
        public GameplayTriggerNetworkResult TryRollbackAndReplay(
            RuntimeFrame rollbackFrame,
            RuntimeFrame currentFrame,
            GameplayTriggerNetworkReplayFrame replay)
        {
            if (replay == null) throw new ArgumentNullException(nameof(replay));
            NetworkBoundedResult restored = _coordinator.TryRestoreBoundedFrame(rollbackFrame);
            if (!restored.Success)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.ResyncRequired, (int)restored.ErrorCode);
            if (rollbackFrame > currentFrame)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidPacket);
            if (rollbackFrame.Value == long.MaxValue)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidPacket);
            for (long frame = rollbackFrame.Value + 1L; frame <= currentFrame.Value; frame++)
            {
                GameplayTriggerNetworkResult result = replay(new RuntimeFrame(frame));
                if (!result.Success) return result;
                if (frame == long.MaxValue) break;
            }
            return GameplayTriggerNetworkResult.Succeeded();
        }
    }
}
