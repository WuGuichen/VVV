using System;
using System.Buffers.Binary;

namespace MxFramework.Gameplay
{
    public enum GameplayComponentNetworkStateErrorCode
    {
        None = 0,
        DestinationTooSmall = 1,
        InvalidHeader = 2,
        UnsupportedSchema = 3,
        CapacityExceeded = 4,
        InvalidEntity = 5,
        DuplicateOrUnorderedEntity = 6,
        ComponentCodecMismatch = 7,
        ComponentValidationFailed = 8
    }

    public readonly struct GameplayComponentNetworkStateResult
    {
        private GameplayComponentNetworkStateResult(bool success, GameplayComponentNetworkStateErrorCode errorCode)
        {
            Success = success;
            ErrorCode = errorCode;
        }

        public bool Success { get; }
        public GameplayComponentNetworkStateErrorCode ErrorCode { get; }
        public static GameplayComponentNetworkStateResult Succeeded() =>
            new GameplayComponentNetworkStateResult(true, GameplayComponentNetworkStateErrorCode.None);
        public static GameplayComponentNetworkStateResult Failed(GameplayComponentNetworkStateErrorCode code) =>
            new GameplayComponentNetworkStateResult(false, code);
    }

    public interface IGameplayComponentNetworkEntityCandidate
    {
        /// <remarks>NoAlloc.</remarks>
        bool Contains(GameplayEntityId entityId);
    }

    /// <summary>Caller-provided bounded binary codec for one or more Gameplay component stores.</summary>
    /// <remarks>
    /// Construction and registry freeze are AllocByDesign. Capture, candidate validation and discard must be
    /// NoAlloc after initialization. Commit must be no-fail after a successful prepare; its allocation class is
    /// codec-specific, while the current aggregate Gameplay lifecycle publication remains LowFreqAlloc.
    /// </remarks>
    public interface IGameplayComponentNetworkStateCodec
    {
        uint TypeId { get; }
        ushort Schema { get; }
        Type ComponentType { get; }
        int MaximumEncodedBytes { get; }
        GameplayComponentNetworkStateResult TryCapture(Span<byte> destination, out int bytesWritten);
        GameplayComponentNetworkStateResult TryPrepare(
            ReadOnlySpan<byte> state,
            IGameplayComponentNetworkEntityCandidate entityCandidate);
        void CommitPrepared();
        void DiscardPrepared();
    }

    /// <summary>Bounded binary candidate for Gameplay entity lifecycle and explicitly registered component authority.</summary>
    /// <remarks>
    /// Construction is AllocByDesign. Capture and candidate validation are NoAlloc. Full-comparable composition
    /// freezes exact component-store coverage and rejects later store/codec drift before live publication.
    /// </remarks>
    public sealed class GameplayComponentWorldNetworkState : IGameplayComponentNetworkEntityCandidate
    {
        public const uint Magic = 0x4D584731U;
        public const ushort Schema = 1;
        public const int HeaderBytes = 32;
        public const int EntityRecordBytes = 8;
        public const int CodecDirectoryRowBytes = 16;

        private readonly GameplayComponentWorld _world;
        private readonly GameplayEntityId[] _capture;
        private readonly GameplayEntityId[] _candidate;
        private readonly IGameplayComponentNetworkStateCodec[] _codecs;
        private readonly bool _requireCompleteComponentCoverage;
        private int _candidateCount;
        private bool _prepared;

        public GameplayComponentWorldNetworkState(GameplayComponentWorld world, int maxEntities = 512)
            : this(world, Array.Empty<IGameplayComponentNetworkStateCodec>(), maxEntities, false)
        {
        }

        /// <remarks>AllocByDesign. Copies and freezes codecs in numeric TypeId order.</remarks>
        public GameplayComponentWorldNetworkState(
            GameplayComponentWorld world,
            IGameplayComponentNetworkStateCodec[] codecs,
            int maxEntities = 512,
            bool requireCompleteComponentCoverage = true)
        {
            if (maxEntities < 1) throw new ArgumentOutOfRangeException(nameof(maxEntities));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _capture = new GameplayEntityId[maxEntities];
            _candidate = new GameplayEntityId[maxEntities];
            _codecs = codecs != null
                ? (IGameplayComponentNetworkStateCodec[])codecs.Clone()
                : Array.Empty<IGameplayComponentNetworkStateCodec>();
            Array.Sort(_codecs, CompareCodecs);
            for (int i = 0; i < _codecs.Length; i++)
            {
                IGameplayComponentNetworkStateCodec codec = _codecs[i];
                if (codec == null || codec.TypeId == 0U || codec.Schema == 0
                    || codec.ComponentType == null || codec.MaximumEncodedBytes < 0)
                {
                    throw new ArgumentException("Gameplay component network codec descriptor is invalid.", nameof(codecs));
                }
                if (i > 0 && _codecs[i - 1].TypeId == codec.TypeId)
                    throw new ArgumentException("Duplicate Gameplay component network codec TypeId.", nameof(codecs));
                for (int previous = 0; previous < i; previous++)
                {
                    if (_codecs[previous].ComponentType == codec.ComponentType)
                    {
                        throw new ArgumentException(
                            "Duplicate Gameplay component CLR type codec registration.",
                            nameof(codecs));
                    }
                }
            }
            _requireCompleteComponentCoverage = requireCompleteComponentCoverage;
            if (_requireCompleteComponentCoverage && !HasExactComponentStoreCoverage())
                throw new InvalidOperationException("Full-comparable Gameplay component store coverage is incomplete.");
        }

        public int MaxEntities => _candidate.Length;
        public int CodecCount => _codecs.Length;
        public int MaximumEncodedBytes
        {
            get
            {
                int bytes = HeaderBytes
                    + (_candidate.Length * EntityRecordBytes)
                    + (_codecs.Length * CodecDirectoryRowBytes);
                for (int i = 0; i < _codecs.Length; i++)
                    bytes = checked(bytes + _codecs[i].MaximumEncodedBytes);
                return bytes;
            }
        }
        public int PreparedEntityCount => _prepared ? _candidateCount : 0;

        /// <remarks>NoAlloc. Uses caller-owned state storage and construction-time scratch.</remarks>
        public GameplayComponentNetworkStateResult TryCapture(Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            // Schema 1 cannot encode dead generations or reuse order. Never advertise a lossy capture as restorable.
            if (_world.Registry.Lifecycle.CountAllocated != _world.Registry.CountAlive)
                return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.UnsupportedSchema);
            if (_requireCompleteComponentCoverage && !HasExactComponentStoreCoverage())
                return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.ComponentCodecMismatch);
            if (!_world.TryCopyEntitySnapshot(_capture, out int entityCount))
                return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.CapacityExceeded);
            int entityBytes = entityCount * EntityRecordBytes;
            int directoryBytes = _codecs.Length * CodecDirectoryRowBytes;
            int componentStart = HeaderBytes + entityBytes + directoryBytes;
            if (destination.Length < componentStart)
                return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.DestinationTooSmall);
            destination.Slice(0, componentStart).Clear();

            int componentBytes = 0;
            for (int i = 0; i < _codecs.Length; i++)
            {
                IGameplayComponentNetworkStateCodec codec = _codecs[i];
                int available = destination.Length - componentStart - componentBytes;
                if (available < 0)
                    return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.DestinationTooSmall);
                GameplayComponentNetworkStateResult capture = codec.TryCapture(
                    destination.Slice(componentStart + componentBytes, available),
                    out int codecBytes);
                if (!capture.Success)
                    return capture;
                if (codecBytes < 0 || codecBytes > codec.MaximumEncodedBytes || codecBytes > available)
                    return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.ComponentCodecMismatch);
                WriteDirectoryRow(
                    destination.Slice(HeaderBytes + entityBytes + (i * CodecDirectoryRowBytes), CodecDirectoryRowBytes),
                    codec,
                    componentBytes,
                    codecBytes);
                componentBytes += codecBytes;
            }

            int required = componentStart + componentBytes;
            Span<byte> state = destination.Slice(0, required);
            BinaryPrimitives.WriteUInt32LittleEndian(state.Slice(0, 4), Magic);
            BinaryPrimitives.WriteUInt16LittleEndian(state.Slice(4, 2), Schema);
            BinaryPrimitives.WriteUInt16LittleEndian(state.Slice(6, 2), HeaderBytes);
            BinaryPrimitives.WriteUInt32LittleEndian(state.Slice(8, 4), (uint)entityCount);
            BinaryPrimitives.WriteUInt16LittleEndian(state.Slice(12, 2), checked((ushort)_codecs.Length));
            BinaryPrimitives.WriteUInt16LittleEndian(state.Slice(14, 2), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(state.Slice(16, 4), (uint)directoryBytes);
            BinaryPrimitives.WriteUInt32LittleEndian(state.Slice(20, 4), (uint)componentBytes);
            BinaryPrimitives.WriteUInt32LittleEndian(state.Slice(24, 4), (uint)required);
            BinaryPrimitives.WriteUInt32LittleEndian(state.Slice(28, 4), 0U);
            int entityOffset = HeaderBytes;
            for (int i = 0; i < entityCount; i++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(state.Slice(entityOffset, 4), _capture[i].Index);
                BinaryPrimitives.WriteInt32LittleEndian(state.Slice(entityOffset + 4, 4), _capture[i].Generation);
                entityOffset += EntityRecordBytes;
            }
            bytesWritten = required;
            return GameplayComponentNetworkStateResult.Succeeded();
        }

        /// <remarks>NoAlloc. All entity and component candidates validate before live publication.</remarks>
        public GameplayComponentNetworkStateResult TryPrepare(ReadOnlySpan<byte> state)
        {
            DiscardPrepared();
            if (state.Length < HeaderBytes
                || BinaryPrimitives.ReadUInt32LittleEndian(state.Slice(0, 4)) != Magic
                || BinaryPrimitives.ReadUInt16LittleEndian(state.Slice(6, 2)) != HeaderBytes
                || BinaryPrimitives.ReadUInt16LittleEndian(state.Slice(14, 2)) != 0
                || BinaryPrimitives.ReadUInt32LittleEndian(state.Slice(28, 4)) != 0U)
                return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.InvalidHeader);
            if (BinaryPrimitives.ReadUInt16LittleEndian(state.Slice(4, 2)) != Schema)
                return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.UnsupportedSchema);
            uint entityCount = BinaryPrimitives.ReadUInt32LittleEndian(state.Slice(8, 4));
            ushort codecCount = BinaryPrimitives.ReadUInt16LittleEndian(state.Slice(12, 2));
            uint directoryBytes = BinaryPrimitives.ReadUInt32LittleEndian(state.Slice(16, 4));
            uint componentBytes = BinaryPrimitives.ReadUInt32LittleEndian(state.Slice(20, 4));
            uint totalBytes = BinaryPrimitives.ReadUInt32LittleEndian(state.Slice(24, 4));
            if (entityCount > (uint)_candidate.Length || codecCount != _codecs.Length
                || componentBytes > int.MaxValue
                || directoryBytes != (uint)(codecCount * CodecDirectoryRowBytes) || totalBytes != (uint)state.Length
                || HeaderBytes + ((long)entityCount * EntityRecordBytes) + directoryBytes + componentBytes != state.Length)
                return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.ComponentCodecMismatch);

            int entityOffset = HeaderBytes;
            int previousIndex = 0;
            for (int i = 0; i < (int)entityCount; i++)
            {
                int index = BinaryPrimitives.ReadInt32LittleEndian(state.Slice(entityOffset, 4));
                int generation = BinaryPrimitives.ReadInt32LittleEndian(state.Slice(entityOffset + 4, 4));
                if (index <= 0 || generation <= 0)
                    return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.InvalidEntity);
                if (index <= previousIndex)
                    return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.DuplicateOrUnorderedEntity);
                if (index != i + 1)
                    return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.UnsupportedSchema);
                _candidate[i] = new GameplayEntityId(index, generation);
                previousIndex = index;
                entityOffset += EntityRecordBytes;
            }
            _candidateCount = (int)entityCount;
            _prepared = true;

            int directoryStart = HeaderBytes + (_candidateCount * EntityRecordBytes);
            int componentStart = directoryStart + (int)directoryBytes;
            int expectedOffset = 0;
            for (int i = 0; i < _codecs.Length; i++)
            {
                ReadOnlySpan<byte> row = state.Slice(
                    directoryStart + (i * CodecDirectoryRowBytes),
                    CodecDirectoryRowBytes);
                IGameplayComponentNetworkStateCodec codec = _codecs[i];
                int offset = BinaryPrimitives.ReadInt32LittleEndian(row.Slice(8, 4));
                int length = BinaryPrimitives.ReadInt32LittleEndian(row.Slice(12, 4));
                if (BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(0, 4)) != codec.TypeId
                    || BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(4, 2)) != codec.Schema
                    || BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(6, 2)) != 0
                    || offset != expectedOffset || length < 0 || length > codec.MaximumEncodedBytes
                    || offset > (int)componentBytes - length)
                {
                    DiscardPrepared();
                    return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.ComponentCodecMismatch);
                }
                GameplayComponentNetworkStateResult prepare = codec.TryPrepare(
                    state.Slice(componentStart + offset, length),
                    this);
                if (!prepare.Success)
                {
                    DiscardPrepared();
                    return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.ComponentValidationFailed);
                }
                expectedOffset += length;
            }
            if (expectedOffset != (int)componentBytes)
            {
                DiscardPrepared();
                return GameplayComponentNetworkStateResult.Failed(GameplayComponentNetworkStateErrorCode.ComponentCodecMismatch);
            }
            return GameplayComponentNetworkStateResult.Succeeded();
        }

        /// <remarks>NoAlloc lookup against the prepared entity candidate.</remarks>
        public bool PreparedContains(GameplayEntityId entityId)
        {
            if (!_prepared || entityId == default)
                return entityId == default;
            for (int i = 0; i < _candidateCount; i++)
            {
                if (_candidate[i] == entityId)
                    return true;
            }
            return false;
        }

        bool IGameplayComponentNetworkEntityCandidate.Contains(GameplayEntityId entityId) =>
            PreparedContains(entityId);

        /// <remarks>LowFreqAlloc through the existing Gameplay registry restore implementation.</remarks>
        public void CommitPrepared()
        {
            if (!_prepared) throw new InvalidOperationException("Gameplay network state candidate is not prepared.");
            _world.Clear();
            _world.Registry.RestoreEntities(new ArraySegment<GameplayEntityId>(_candidate, 0, _candidateCount));
            for (int i = 0; i < _codecs.Length; i++)
                _codecs[i].CommitPrepared();
            _prepared = false;
        }

        public void DiscardPrepared()
        {
            for (int i = 0; i < _codecs.Length; i++)
                _codecs[i].DiscardPrepared();
            _candidateCount = 0;
            _prepared = false;
        }

        private static int CompareCodecs(IGameplayComponentNetworkStateCodec left, IGameplayComponentNetworkStateCodec right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            return left.TypeId.CompareTo(right.TypeId);
        }

        private bool HasExactComponentStoreCoverage()
        {
            if (_world.StoreCount != _codecs.Length)
                return false;
            for (int storeIndex = 0; storeIndex < _world.StoreCount; storeIndex++)
            {
                Type componentType = _world.GetStoreComponentType(storeIndex);
                bool found = false;
                for (int codecIndex = 0; codecIndex < _codecs.Length; codecIndex++)
                {
                    if (_codecs[codecIndex].ComponentType == componentType)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }
            return true;
        }

        private static void WriteDirectoryRow(
            Span<byte> row,
            IGameplayComponentNetworkStateCodec codec,
            int offset,
            int length)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(0, 4), codec.TypeId);
            BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(4, 2), codec.Schema);
            BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(6, 2), 0);
            BinaryPrimitives.WriteInt32LittleEndian(row.Slice(8, 4), offset);
            BinaryPrimitives.WriteInt32LittleEndian(row.Slice(12, 4), length);
        }
    }
}
