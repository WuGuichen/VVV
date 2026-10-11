using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    public enum GameplayTriggerOccupancyCommandCodecStatus
    {
        None = 0,
        InvalidArgument = 1,
        DestinationTooSmall = 2,
        WrongCommandId = 3,
        MissingEnvelope = 4,
        WrongEnvelopeType = 5,
        UnsupportedSchema = 6,
        InvalidProvenance = 7,
        InvalidBatch = 8,
        InvalidItem = 9,
        InvalidEntity = 10,
        ReservedSlotNotZero = 11
    }

    /// <summary>Decoded fixed record belonging to one complete occupancy observation.</summary>
    public readonly struct GameplayTriggerOccupancyCommandRecord
    {
        internal GameplayTriggerOccupancyCommandRecord(
            RuntimeFrame frame,
            int sourceId,
            GameplayTriggerSourceBindingId sourceBindingId,
            long batchId,
            int itemIndex,
            int itemCount,
            RuntimeCommandProvenanceKind provenance,
            GameplayEntityId subject)
        {
            Frame = frame;
            SourceId = sourceId;
            SourceBindingId = sourceBindingId;
            BatchId = batchId;
            ItemIndex = itemIndex;
            ItemCount = itemCount;
            Provenance = provenance;
            Subject = subject;
        }

        public RuntimeFrame Frame { get; }
        public int SourceId { get; }
        public GameplayTriggerSourceBindingId SourceBindingId { get; }
        public long BatchId { get; }
        public int ItemIndex { get; }
        public int ItemCount { get; }
        public RuntimeCommandProvenanceKind Provenance { get; }
        public GameplayEntityId Subject { get; }
        public bool IsEmptyBatch => ItemCount == 0;
    }

    /// <summary>Encodes and decodes complete occupancy observations as fixed command records.</summary>
    public static class GameplayTriggerOccupancyCommandCodec
    {
        /// <summary>Writes one record per subject, or one explicit -1/0 item record for an empty batch.</summary>
        /// <remarks>NoAlloc. The destination is not modified when validation fails.</remarks>
        public static bool TryEncodeBatch(
            RuntimeFrame frame,
            int sourceId,
            RuntimeCommandProvenanceKind provenance,
            GameplayTriggerSourceBindingId sourceBindingId,
            long batchId,
            ReadOnlySpan<GameplayEntityId> subjects,
            Span<RuntimeCommand> destination,
            out int written,
            out GameplayTriggerOccupancyCommandCodecStatus status,
            RuntimeTraceIdentity traceId = default)
        {
            written = 0;
            if (sourceId <= 0 || sourceBindingId.Value <= 0 || batchId <= 0L ||
                !GameplayTriggerCommandCodecUtilities.IsSupportedProvenance(provenance))
            {
                status = GameplayTriggerOccupancyCommandCodecStatus.InvalidArgument;
                return false;
            }

            int required = subjects.Length == 0 ? 1 : subjects.Length;
            if (destination.Length < required)
            {
                status = GameplayTriggerOccupancyCommandCodecStatus.DestinationTooSmall;
                return false;
            }

            for (int i = 0; i < subjects.Length; i++)
            {
                if (!subjects[i].IsValid)
                {
                    status = GameplayTriggerOccupancyCommandCodecStatus.InvalidEntity;
                    return false;
                }
            }

            if (subjects.Length == 0)
            {
                destination[0] = CreateRecord(
                    frame,
                    sourceId,
                    provenance,
                    sourceBindingId,
                    batchId,
                    itemIndex: -1,
                    itemCount: 0,
                    default,
                    traceId);
                written = 1;
                status = GameplayTriggerOccupancyCommandCodecStatus.None;
                return true;
            }

            for (int i = 0; i < subjects.Length; i++)
            {
                destination[i] = CreateRecord(
                    frame,
                    sourceId,
                    provenance,
                    sourceBindingId,
                    batchId,
                    i,
                    subjects.Length,
                    subjects[i],
                    traceId);
            }

            written = subjects.Length;
            status = GameplayTriggerOccupancyCommandCodecStatus.None;
            return true;
        }

        /// <remarks>NoAlloc.</remarks>
        public static bool TryDecodeRecord(
            RuntimeCommand command,
            out GameplayTriggerOccupancyCommandRecord record,
            out GameplayTriggerOccupancyCommandCodecStatus status)
        {
            record = default;
            if (command.CommandId != GameplayTriggerRuntimeCommandIds.SubmitOccupancyBatch)
            {
                status = GameplayTriggerOccupancyCommandCodecStatus.WrongCommandId;
                return false;
            }

            RuntimeCommandEnvelope envelope = command.Envelope;
            if (!envelope.IsPresent)
            {
                status = GameplayTriggerOccupancyCommandCodecStatus.MissingEnvelope;
                return false;
            }
            if (envelope.TypeId != GameplayTriggerRuntimeEnvelopeIds.OccupancyBatch)
            {
                status = GameplayTriggerOccupancyCommandCodecStatus.WrongEnvelopeType;
                return false;
            }
            if (envelope.SchemaVersion != GameplayTriggerRuntimeEnvelopeIds.OccupancyBatchSchemaVersion)
            {
                status = GameplayTriggerOccupancyCommandCodecStatus.UnsupportedSchema;
                return false;
            }
            if (!GameplayTriggerCommandCodecUtilities.IsSupportedProvenance(envelope.Provenance))
            {
                status = GameplayTriggerOccupancyCommandCodecStatus.InvalidProvenance;
                return false;
            }
            if (command.SourceId <= 0 || command.TargetId <= 0 || envelope.Data0 <= 0L ||
                command.Payload2 != 0)
            {
                status = GameplayTriggerOccupancyCommandCodecStatus.InvalidBatch;
                return false;
            }

            int itemIndex = command.Payload0;
            int itemCount = command.Payload1;
            if (itemCount < 0 ||
                (itemCount == 0 && itemIndex != -1) ||
                (itemCount > 0 && (itemIndex < 0 || itemIndex >= itemCount)))
            {
                status = GameplayTriggerOccupancyCommandCodecStatus.InvalidItem;
                return false;
            }

            if (envelope.Data2 != 0L || envelope.Data3 != 0L || envelope.Data4 != 0L ||
                envelope.Data5 != 0L || envelope.Data6 != 0L || envelope.Data7 != 0L)
            {
                status = GameplayTriggerOccupancyCommandCodecStatus.ReservedSlotNotZero;
                return false;
            }

            GameplayEntityId subject = default;
            if (itemCount == 0)
            {
                if (envelope.Data1 != 0L)
                {
                    status = GameplayTriggerOccupancyCommandCodecStatus.ReservedSlotNotZero;
                    return false;
                }
            }
            else if (!GameplayTriggerCommandCodecUtilities.TryUnpackEntity(
                         envelope.Data1,
                         allowDefault: false,
                         out subject))
            {
                status = GameplayTriggerOccupancyCommandCodecStatus.InvalidEntity;
                return false;
            }

            record = new GameplayTriggerOccupancyCommandRecord(
                command.Frame,
                command.SourceId,
                new GameplayTriggerSourceBindingId(command.TargetId),
                envelope.Data0,
                itemIndex,
                itemCount,
                envelope.Provenance,
                subject);
            status = GameplayTriggerOccupancyCommandCodecStatus.None;
            return true;
        }

        internal static bool HasRawBatchIdentity(RuntimeCommand command, RuntimeCommand batchCommand)
        {
            return command.CommandId == GameplayTriggerRuntimeCommandIds.SubmitOccupancyBatch &&
                command.Frame == batchCommand.Frame &&
                command.SourceId == batchCommand.SourceId &&
                command.TargetId == batchCommand.TargetId &&
                command.Envelope.Data0 == batchCommand.Envelope.Data0;
        }

        private static RuntimeCommand CreateRecord(
            RuntimeFrame frame,
            int sourceId,
            RuntimeCommandProvenanceKind provenance,
            GameplayTriggerSourceBindingId sourceBindingId,
            long batchId,
            int itemIndex,
            int itemCount,
            GameplayEntityId subject,
            RuntimeTraceIdentity traceId)
        {
            var envelope = new RuntimeCommandEnvelope(
                GameplayTriggerRuntimeEnvelopeIds.OccupancyBatch,
                GameplayTriggerRuntimeEnvelopeIds.OccupancyBatchSchemaVersion,
                provenance,
                batchId,
                GameplayTriggerCommandCodecUtilities.PackEntity(subject));
            return new RuntimeCommand(
                frame,
                sourceId,
                GameplayTriggerRuntimeCommandIds.SubmitOccupancyBatch,
                sourceBindingId.Value,
                itemIndex,
                itemCount,
                payload2: 0,
                traceId: traceId,
                envelope: envelope);
        }
    }

    internal static class GameplayTriggerCommandCodecUtilities
    {
        internal static long PackEntity(GameplayEntityId entity)
        {
            return ((long)(uint)entity.Index << 32) | (uint)entity.Generation;
        }

        internal static bool TryUnpackEntity(
            long packed,
            bool allowDefault,
            out GameplayEntityId entity)
        {
            int index = unchecked((int)(packed >> 32));
            int generation = unchecked((int)packed);
            if (index < 0 || generation < 0 || (index == 0) != (generation == 0) ||
                (!allowDefault && index == 0))
            {
                entity = default;
                return false;
            }

            entity = new GameplayEntityId(index, generation);
            return true;
        }

        internal static bool IsSupportedProvenance(RuntimeCommandProvenanceKind provenance)
        {
            return provenance == RuntimeCommandProvenanceKind.Exogenous ||
                provenance == RuntimeCommandProvenanceKind.Derived;
        }
    }
}
