using System;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Network;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers.NetworkBridge
{
    /// <summary>Atomic Gameplay-first, Trigger-second protocol-2 snapshot authority bundle.</summary>
    public sealed class GameplayTriggerNetworkStateBundle
    {
        public const int SnapshotHeaderBytes = 124;
        public const int DirectoryRowBytes = 24;
        public const int FullParticipantCount = 2;
        public const int FullDirectoryBytes = FullParticipantCount * DirectoryRowBytes;

        private readonly GameplayTriggerNetworkProfile _profile;
        private readonly GameplayStateParticipant _gameplay;
        private readonly TriggerStateParticipant _trigger;
        private long _expectedSnapshotId;
        private long _lastSnapshotId = -1L;
        private long _lastSnapshotFrame = -1L;
        private long _lastSnapshotResultHash;
        private long _lastSnapshotPayloadHash;

        public GameplayTriggerNetworkStateBundle(
            GameplayTriggerNetworkProfile profile,
            GameplayComponentWorld gameplayWorld,
            GameplayTriggerRuntime triggerRuntime,
            int maxGameplayEntities = 512)
            : this(
                profile,
                gameplayWorld,
                triggerRuntime,
                Array.Empty<IGameplayComponentNetworkStateCodec>(),
                maxGameplayEntities)
        {
        }

        /// <remarks>
        /// AllocByDesign. FullComparable profiles require exact coverage of every registered Gameplay component store.
        /// </remarks>
        public GameplayTriggerNetworkStateBundle(
            GameplayTriggerNetworkProfile profile,
            GameplayComponentWorld gameplayWorld,
            GameplayTriggerRuntime triggerRuntime,
            IGameplayComponentNetworkStateCodec[] gameplayComponentCodecs,
            int maxGameplayEntities = 512)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _gameplay = new GameplayStateParticipant(
                gameplayWorld,
                gameplayComponentCodecs,
                maxGameplayEntities,
                profile.Comparability == NetworkHashComparability.FullComparable);
            _trigger = new TriggerStateParticipant(triggerRuntime, _gameplay);
        }

        public INetworkBoundedRollbackParticipant GameplayRollbackParticipant => _gameplay;
        public INetworkBoundedRollbackParticipant TriggerRollbackParticipant => _trigger;
        public long ExpectedSnapshotId => _expectedSnapshotId;
        public long LastAppliedSnapshotId => _lastSnapshotId;
        public RuntimeFrame LastAppliedAuthoritativeFrame => _lastSnapshotFrame >= 0 ? new RuntimeFrame(_lastSnapshotFrame) : RuntimeFrame.Zero;
        public bool CanCompareFullWorldHash => _profile.Comparability == NetworkHashComparability.FullComparable;
        public int MaximumStateBytes => _gameplay.MaxStateBytes + _trigger.MaxStateBytes;
        public int MaximumSnapshotPayloadBytes => SnapshotHeaderBytes + FullDirectoryBytes + MaximumStateBytes;

        /// <remarks>NoAlloc. Both Gameplay lifecycle and Trigger authority write into caller-owned storage.</remarks>
        public GameplayTriggerNetworkResult TryCaptureSnapshotPayload(
            long snapshotId,
            RuntimeFrame authoritativeFrame,
            RuntimeFrame ackedInputFrame,
            long resultHash,
            Span<byte> destination,
            out int bytesWritten)
        {
            bytesWritten = 0;
            if (snapshotId < 0L || snapshotId == long.MaxValue
                || authoritativeFrame.Value < 0L || authoritativeFrame.Value == long.MaxValue
                || ackedInputFrame.Value < 0L || ackedInputFrame.Value == long.MaxValue
                || destination.Length < SnapshotHeaderBytes + FullDirectoryBytes)
            {
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidPacket);
            }
            int stateStart = SnapshotHeaderBytes + FullDirectoryBytes;
            NetworkBoundedResult gameplayCapture = _gameplay.TryCapture(
                destination.Slice(stateStart), out int gameplayBytes, out long gameplayHash);
            if (!gameplayCapture.Success)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.CapacityExceeded, (int)gameplayCapture.ErrorCode);
            NetworkBoundedResult triggerCapture = _trigger.TryCapture(
                destination.Slice(stateStart + gameplayBytes), out int triggerBytes, out long triggerHash);
            if (!triggerCapture.Success)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.CapacityExceeded, (int)triggerCapture.ErrorCode);
            int stateBytes = gameplayBytes + triggerBytes;
            int total = stateStart + stateBytes;
            if (total > NetworkBoundedWire.SnapshotTotalLimit - NetworkBoundedWire.HeaderBytes)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.CapacityExceeded);

            Span<byte> payload = destination.Slice(0, total);
            WriteI64(payload, 0, snapshotId); WriteI64(payload, 8, authoritativeFrame.Value); WriteI64(payload, 16, ackedInputFrame.Value);
            WriteI64(payload, 24, _profile.NetworkProfileHash); WriteI64(payload, 32, _profile.DefinitionCatalogHash);
            WriteI64(payload, 40, _profile.BindingCatalogHash); WriteI64(payload, 48, _profile.GameplayTriggerCatalogHash);
            GameplayTriggerNetworkCommandCodec.WriteOptions(payload.Slice(56, 48), _profile.Options);
            WriteI64(payload, 104, resultHash); payload[112] = (byte)_profile.Comparability; payload[113] = 0;
            WriteU16(payload, 114, FullParticipantCount); WriteU32(payload, 116, FullDirectoryBytes); WriteU32(payload, 120, (uint)stateBytes);
            WriteDirectory(payload.Slice(SnapshotHeaderBytes, DirectoryRowBytes), GameplayTriggerNetworkIds.GameplayParticipantTypeId,
                GameplayTriggerNetworkIds.ParticipantSchema, GameplayTriggerNetworkIds.GameplayRestoreOrder, 0, gameplayBytes, gameplayHash);
            WriteDirectory(payload.Slice(SnapshotHeaderBytes + DirectoryRowBytes, DirectoryRowBytes), GameplayTriggerNetworkIds.TriggerParticipantTypeId,
                GameplayTriggerNetworkIds.ParticipantSchema, GameplayTriggerNetworkIds.TriggerRestoreOrder, gameplayBytes, triggerBytes, triggerHash);
            bytesWritten = total;
            return GameplayTriggerNetworkResult.Succeeded();
        }

        /// <remarks>Candidate validation completes for both participants before Gameplay 100 then Trigger 200 publish.</remarks>
        public GameplayTriggerNetworkResult TryApplySnapshotPayload(
            ReadOnlySpan<byte> payload,
            out RuntimeFrame authoritativeFrame,
            out long resultHash)
        {
            authoritativeFrame = RuntimeFrame.Zero;
            resultHash = 0L;
            if (payload.Length < SnapshotHeaderBytes + FullDirectoryBytes)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidLength);
            long snapshotId = ReadI64(payload, 0); long frame = ReadI64(payload, 8); long ack = ReadI64(payload, 16); long hash = ReadI64(payload, 104);
            if (snapshotId < 0 || snapshotId == long.MaxValue || frame < 0 || frame == long.MaxValue || ack < 0 || ack == long.MaxValue
                || ReadI64(payload, 24) != _profile.NetworkProfileHash || ReadI64(payload, 32) != _profile.DefinitionCatalogHash
                || ReadI64(payload, 40) != _profile.BindingCatalogHash || ReadI64(payload, 48) != _profile.GameplayTriggerCatalogHash
                || !GameplayTriggerNetworkCommandCodec.OptionsMatch(payload.Slice(56, 48), _profile.Options))
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.IdentityMismatch);
            if (payload[112] != (byte)_profile.Comparability || payload[113] != 0
                || ReadU16(payload, 114) != FullParticipantCount || ReadU32(payload, 116) != FullDirectoryBytes)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidPacket);
            uint stateBytes = ReadU32(payload, 120);
            if (payload.Length != SnapshotHeaderBytes + FullDirectoryBytes + (long)stateBytes)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidLength);
            if (snapshotId == _lastSnapshotId && frame == _lastSnapshotFrame && hash == _lastSnapshotResultHash)
            {
                if (Hash(payload) != _lastSnapshotPayloadHash)
                    return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidPacket);
                authoritativeFrame = new RuntimeFrame(frame); resultHash = hash;
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.AlreadyApplied);
            }
            if (snapshotId != _expectedSnapshotId)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidPacket);

            if (!TryReadDirectory(payload.Slice(SnapshotHeaderBytes, DirectoryRowBytes),
                    GameplayTriggerNetworkIds.GameplayParticipantTypeId, GameplayTriggerNetworkIds.ParticipantSchema,
                    GameplayTriggerNetworkIds.GameplayRestoreOrder, expectedOffset: 0, out int gameplayLength, out long gameplayHash)
                || !TryReadDirectory(payload.Slice(SnapshotHeaderBytes + DirectoryRowBytes, DirectoryRowBytes),
                    GameplayTriggerNetworkIds.TriggerParticipantTypeId, GameplayTriggerNetworkIds.ParticipantSchema,
                    GameplayTriggerNetworkIds.TriggerRestoreOrder, expectedOffset: gameplayLength, out int triggerLength, out long triggerHash)
                || gameplayLength < 0 || triggerLength < 0 || gameplayLength + (long)triggerLength != stateBytes)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidPacket);
            int stateStart = SnapshotHeaderBytes + FullDirectoryBytes;
            NetworkBoundedResult gameplayPrepare = _gameplay.TryPrepareRestore(payload.Slice(stateStart, gameplayLength), gameplayHash);
            if (!gameplayPrepare.Success)
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.ResyncRequired, (int)gameplayPrepare.ErrorCode);
            NetworkBoundedResult triggerPrepare = _trigger.TryPrepareRestore(payload.Slice(stateStart + gameplayLength, triggerLength), triggerHash);
            if (!triggerPrepare.Success)
            {
                _gameplay.DiscardPreparedRestore();
                _trigger.DiscardPreparedRestore();
                return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.ResyncRequired, (int)triggerPrepare.ErrorCode);
            }
            _gameplay.CommitPreparedRestore();
            _trigger.CommitPreparedRestore();
            _lastSnapshotId = snapshotId; _lastSnapshotFrame = frame; _lastSnapshotResultHash = hash;
            _lastSnapshotPayloadHash = Hash(payload);
            _expectedSnapshotId++;
            authoritativeFrame = new RuntimeFrame(frame); resultHash = hash;
            return GameplayTriggerNetworkResult.Succeeded();
        }

        public GameplayTriggerNetworkResult TryGetResumeFrame(RuntimeFrame snapshotFrame, out RuntimeFrame resumeFrame)
        {
            if (snapshotFrame.Value < 0L || snapshotFrame.Value == long.MaxValue)
            { resumeFrame = RuntimeFrame.Zero; return GameplayTriggerNetworkResult.Failed(GameplayTriggerNetworkErrorCode.InvalidPacket); }
            resumeFrame = new RuntimeFrame(snapshotFrame.Value + 1L);
            return GameplayTriggerNetworkResult.Succeeded();
        }

        private static void WriteDirectory(Span<byte> row, uint type, ushort schema, ushort order, int offset, int length, long hash)
        { WriteU32(row, 0, type); WriteU16(row, 4, schema); WriteU16(row, 6, order); WriteU32(row, 8, (uint)offset); WriteU32(row, 12, (uint)length); WriteI64(row, 16, hash); }
        private static bool TryReadDirectory(ReadOnlySpan<byte> row, uint type, ushort schema, ushort order, int expectedOffset, out int length, out long hash)
        { length = 0; hash = 0; uint rawLength = ReadU32(row, 12); if (expectedOffset < 0 || ReadU32(row, 0) != type || ReadU16(row, 4) != schema || ReadU16(row, 6) != order || ReadU32(row, 8) != (uint)expectedOffset || rawLength > int.MaxValue) return false; length = (int)rawLength; hash = ReadI64(row, 16); return true; }
        private static long Hash(ReadOnlySpan<byte> bytes) { unchecked { ulong h = 14695981039346656037UL; for (int i = 0; i < bytes.Length; i++) { h ^= bytes[i]; h *= 1099511628211UL; } return (long)h; } }
        private static void WriteU16(Span<byte> s, int o, int v) => GameplayTriggerNetworkCommandCodec.WriteU16(s, o, (ushort)v);
        private static void WriteU32(Span<byte> s, int o, int v) => GameplayTriggerNetworkCommandCodec.WriteU32(s, o, (uint)v);
        private static void WriteU32(Span<byte> s, int o, uint v) => GameplayTriggerNetworkCommandCodec.WriteU32(s, o, v);
        private static void WriteI64(Span<byte> s, int o, long v) => GameplayTriggerNetworkCommandCodec.WriteI64(s, o, v);
        private static ushort ReadU16(ReadOnlySpan<byte> s, int o) => GameplayTriggerNetworkCommandCodec.ReadU16(s, o);
        private static uint ReadU32(ReadOnlySpan<byte> s, int o) => GameplayTriggerNetworkCommandCodec.ReadU32(s, o);
        private static long ReadI64(ReadOnlySpan<byte> s, int o) => GameplayTriggerNetworkCommandCodec.ReadI64(s, o);

        private sealed class GameplayStateParticipant : INetworkBoundedStateParticipant, INetworkBoundedRollbackParticipant, IGameplayTriggerAuthorityEntityValidator
        {
            private readonly GameplayComponentWorldNetworkState _state;
            private readonly GameplayComponentWorld _world;
            public GameplayStateParticipant(
                GameplayComponentWorld world,
                IGameplayComponentNetworkStateCodec[] codecs,
                int maxEntities,
                bool requireCompleteCoverage)
            {
                _world = world;
                _state = new GameplayComponentWorldNetworkState(
                    world,
                    codecs,
                    maxEntities,
                    requireCompleteCoverage);
            }
            public uint TypeId => GameplayTriggerNetworkIds.GameplayParticipantTypeId;
            public ushort Schema => GameplayTriggerNetworkIds.ParticipantSchema;
            public ushort RestoreOrder => GameplayTriggerNetworkIds.GameplayRestoreOrder;
            public int MaxStateBytes => _state.MaximumEncodedBytes;
            public int StateBytes => 4 + MaxStateBytes;
            public bool Contains(GameplayEntityId entityId) => entityId == default || _state.PreparedContains(entityId) || _world.IsAlive(entityId);
            public NetworkBoundedResult TryCapture(Span<byte> destination, out int bytesWritten, out long participantHash)
            { GameplayComponentNetworkStateResult r = _state.TryCapture(destination, out bytesWritten); participantHash = r.Success ? Hash(destination.Slice(0, bytesWritten)) : 0; return r.Success ? NetworkBoundedResult.Succeeded() : NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantCaptureFailed, (int)r.ErrorCode); }
            public NetworkBoundedResult TryPrepareRestore(ReadOnlySpan<byte> state, long expectedParticipantHash)
            { if (Hash(state) != expectedParticipantHash) return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantValidationFailed); GameplayComponentNetworkStateResult r = _state.TryPrepare(state); return r.Success ? NetworkBoundedResult.Succeeded() : NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantValidationFailed, (int)r.ErrorCode); }
            public void CommitPreparedRestore() => _state.CommitPrepared();
            public void DiscardPreparedRestore() => _state.DiscardPrepared();
            public NetworkBoundedResult TryCaptureRollbackState(RuntimeFrame frame, Span<byte> destination)
            { destination.Clear(); NetworkBoundedResult r = TryCapture(destination.Slice(4), out int bytes, out _); if (r.Success) GameplayTriggerNetworkCommandCodec.WriteI32(destination, 0, bytes); return r; }
            public NetworkBoundedResult TryPrepareRollbackState(RuntimeFrame frame, ReadOnlySpan<byte> state)
            { int length = GameplayTriggerNetworkCommandCodec.ReadI32(state, 0); if (length < 0 || length > MaxStateBytes) return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantValidationFailed); ReadOnlySpan<byte> payload = state.Slice(4, length); return TryPrepareRestore(payload, Hash(payload)); }
            public void CommitPreparedRollbackState() => CommitPreparedRestore();
            public void DiscardPreparedRollbackState() => DiscardPreparedRestore();
        }

        private sealed class TriggerStateParticipant : INetworkBoundedStateParticipant, INetworkBoundedRollbackParticipant
        {
            private readonly GameplayTriggerAuthorityBinaryState _state;
            public TriggerStateParticipant(GameplayTriggerRuntime runtime, IGameplayTriggerAuthorityEntityValidator validator) { _state = new GameplayTriggerAuthorityBinaryState(runtime, validator); }
            public uint TypeId => GameplayTriggerNetworkIds.TriggerParticipantTypeId;
            public ushort Schema => GameplayTriggerNetworkIds.ParticipantSchema;
            public ushort RestoreOrder => GameplayTriggerNetworkIds.TriggerRestoreOrder;
            public int MaxStateBytes => _state.MaximumEncodedBytes;
            public int StateBytes => 4 + MaxStateBytes;
            public NetworkBoundedResult TryCapture(Span<byte> destination, out int bytesWritten, out long participantHash)
            { GameplayTriggerAuthorityBinaryResult r = _state.TryCapture(destination, out bytesWritten, out participantHash); return r.Success ? NetworkBoundedResult.Succeeded() : NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantCaptureFailed, (int)r.ErrorCode); }
            public NetworkBoundedResult TryPrepareRestore(ReadOnlySpan<byte> state, long expectedParticipantHash)
            { GameplayTriggerAuthorityBinaryResult r = _state.TryPrepare(state, expectedParticipantHash); return r.Success ? NetworkBoundedResult.Succeeded() : NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantValidationFailed, (int)r.ErrorCode); }
            public void CommitPreparedRestore() => _state.CommitPrepared();
            public void DiscardPreparedRestore() => _state.DiscardPrepared();
            public NetworkBoundedResult TryCaptureRollbackState(RuntimeFrame frame, Span<byte> destination)
            { destination.Clear(); NetworkBoundedResult r = TryCapture(destination.Slice(4), out int bytes, out _); if (r.Success) GameplayTriggerNetworkCommandCodec.WriteI32(destination, 0, bytes); return r; }
            public NetworkBoundedResult TryPrepareRollbackState(RuntimeFrame frame, ReadOnlySpan<byte> state)
            { int length = GameplayTriggerNetworkCommandCodec.ReadI32(state, 0); if (length < 0 || length > MaxStateBytes) return NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantValidationFailed); ReadOnlySpan<byte> payload = state.Slice(4, length); GameplayTriggerAuthorityBinaryResult prepare = _state.TryPrepare(payload, Hash(payload)); return prepare.Success ? NetworkBoundedResult.Succeeded() : NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantValidationFailed, (int)prepare.ErrorCode); }
            public void CommitPreparedRollbackState() => _state.CommitPrepared();
            public void DiscardPreparedRollbackState() => _state.DiscardPrepared();
        }
    }
}
