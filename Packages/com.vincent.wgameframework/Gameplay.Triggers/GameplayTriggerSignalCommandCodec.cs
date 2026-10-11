using System;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    public static class GameplayTriggerRuntimeEnvelopeIds
    {
        public const int SignalBatch = 1002101;
        public const int SignalBatchSchemaVersion = 1;
        public const int OccupancyBatch = 1002102;
        public const int OccupancyBatchSchemaVersion = 1;
        public const int ScalarBatch = 1002103;
        public const int ScalarBatchSchemaVersion = 1;
    }

    public enum GameplayTriggerSignalCommandCodecStatus
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
        InvalidEntity = 10
    }

    /// <summary>Decoded fixed record belonging to one replayable Signal batch.</summary>
    public readonly struct GameplayTriggerSignalCommandRecord
    {
        internal GameplayTriggerSignalCommandRecord(
            RuntimeFrame frame,
            int sourceId,
            GameplayTriggerSourceBindingId sourceBindingId,
            long batchId,
            GameplayTriggerSignalOrdering ordering,
            int itemIndex,
            int itemCount,
            RuntimeCommandProvenanceKind provenance,
            GameplayTriggerSignal signal)
        {
            Frame = frame;
            SourceId = sourceId;
            SourceBindingId = sourceBindingId;
            BatchId = batchId;
            Ordering = ordering;
            ItemIndex = itemIndex;
            ItemCount = itemCount;
            Provenance = provenance;
            Signal = signal;
        }

        public RuntimeFrame Frame { get; }
        public int SourceId { get; }
        public GameplayTriggerSourceBindingId SourceBindingId { get; }
        public long BatchId { get; }
        public GameplayTriggerSignalOrdering Ordering { get; }
        public int ItemIndex { get; }
        public int ItemCount { get; }
        public RuntimeCommandProvenanceKind Provenance { get; }
        public GameplayTriggerSignal Signal { get; }
        public bool IsEmptyBatch => ItemCount == 0;
    }

    /// <summary>Encodes and decodes replayable fixed Signal-batch command records.</summary>
    public static class GameplayTriggerSignalCommandCodec
    {
        /// <summary>Writes one record per signal, or one explicit record for an empty batch.</summary>
        /// <remarks>NoAlloc. Destination and trace text are caller-owned.</remarks>
        public static bool TryEncodeBatch(
            RuntimeFrame frame,
            int sourceId,
            RuntimeCommandProvenanceKind provenance,
            GameplayTriggerSourceBindingId sourceBindingId,
            GameplayTriggerSignalOrdering ordering,
            long batchId,
            ReadOnlySpan<GameplayTriggerSignal> signals,
            Span<RuntimeCommand> destination,
            out int written,
            out GameplayTriggerSignalCommandCodecStatus status,
            RuntimeTraceIdentity traceId = default)
        {
            written = 0;
            if (sourceId <= 0 ||
                sourceBindingId.Value <= 0 ||
                batchId <= 0L ||
                !IsSupportedOrdering(ordering) ||
                !IsSupportedProvenance(provenance))
            {
                status = GameplayTriggerSignalCommandCodecStatus.InvalidArgument;
                return false;
            }

            int required = signals.Length == 0 ? 1 : signals.Length;
            if (destination.Length < required)
            {
                status = GameplayTriggerSignalCommandCodecStatus.DestinationTooSmall;
                return false;
            }

            long previousSequence = -1L;
            for (int i = 0; i < signals.Length; i++)
            {
                GameplayTriggerSignal signal = signals[i];
                if (signal.SignalId <= 0 || signal.Sequence < 0L ||
                    (ordering == GameplayTriggerSignalOrdering.Ordered &&
                     signal.Sequence <= previousSequence))
                {
                    status = GameplayTriggerSignalCommandCodecStatus.InvalidItem;
                    return false;
                }

                previousSequence = signal.Sequence;
            }

            if (signals.Length == 0)
            {
                destination[0] = CreateRecord(
                    frame,
                    sourceId,
                    provenance,
                    sourceBindingId,
                    ordering,
                    batchId,
                    itemIndex: -1,
                    itemCount: 0,
                    default,
                    traceId);
                written = 1;
                status = GameplayTriggerSignalCommandCodecStatus.None;
                return true;
            }

            for (int i = 0; i < signals.Length; i++)
            {
                destination[i] = CreateRecord(
                    frame,
                    sourceId,
                    provenance,
                    sourceBindingId,
                    ordering,
                    batchId,
                    i,
                    signals.Length,
                    signals[i],
                    traceId);
            }

            written = signals.Length;
            status = GameplayTriggerSignalCommandCodecStatus.None;
            return true;
        }

        /// <remarks>NoAlloc.</remarks>
        public static bool TryDecodeRecord(
            RuntimeCommand command,
            out GameplayTriggerSignalCommandRecord record,
            out GameplayTriggerSignalCommandCodecStatus status)
        {
            record = default;
            if (command.CommandId != GameplayTriggerRuntimeCommandIds.SubmitSignalBatch)
            {
                status = GameplayTriggerSignalCommandCodecStatus.WrongCommandId;
                return false;
            }

            RuntimeCommandEnvelope envelope = command.Envelope;
            if (!envelope.IsPresent)
            {
                status = GameplayTriggerSignalCommandCodecStatus.MissingEnvelope;
                return false;
            }
            if (envelope.TypeId != GameplayTriggerRuntimeEnvelopeIds.SignalBatch)
            {
                status = GameplayTriggerSignalCommandCodecStatus.WrongEnvelopeType;
                return false;
            }
            if (envelope.SchemaVersion != GameplayTriggerRuntimeEnvelopeIds.SignalBatchSchemaVersion)
            {
                status = GameplayTriggerSignalCommandCodecStatus.UnsupportedSchema;
                return false;
            }
            if (!IsSupportedProvenance(envelope.Provenance))
            {
                status = GameplayTriggerSignalCommandCodecStatus.InvalidProvenance;
                return false;
            }
            if (command.SourceId <= 0 || command.TargetId <= 0 || envelope.Data0 <= 0L ||
                !IsSupportedOrderingValue(command.Payload0))
            {
                status = GameplayTriggerSignalCommandCodecStatus.InvalidBatch;
                return false;
            }

            int itemIndex = command.Payload1;
            int itemCount = command.Payload2;
            if (itemCount < 0 ||
                (itemCount == 0 && itemIndex != -1) ||
                (itemCount > 0 && (itemIndex < 0 || itemIndex >= itemCount)))
            {
                status = GameplayTriggerSignalCommandCodecStatus.InvalidItem;
                return false;
            }

            GameplayTriggerSignal signal = default;
            if (itemCount == 0)
            {
                if (envelope.Data1 != 0L || envelope.Data2 != 0L ||
                    envelope.Data3 != 0L || envelope.Data4 != 0L ||
                    envelope.Data5 != 0L || envelope.Data6 != 0L ||
                    envelope.Data7 != 0L)
                {
                    status = GameplayTriggerSignalCommandCodecStatus.InvalidItem;
                    return false;
                }
            }
            else
            {
                if (envelope.Data1 <= 0L || envelope.Data1 > int.MaxValue || envelope.Data7 < 0L ||
                    !TryUnpackEntity(envelope.Data2, out GameplayEntityId subject) ||
                    !TryUnpackEntity(envelope.Data3, out GameplayEntityId instigator))
                {
                    status = GameplayTriggerSignalCommandCodecStatus.InvalidEntity;
                    return false;
                }

                signal = new GameplayTriggerSignal(
                    (int)envelope.Data1,
                    subject,
                    instigator,
                    envelope.Data4,
                    envelope.Data5,
                    envelope.Data6,
                    envelope.Data7);
            }

            record = new GameplayTriggerSignalCommandRecord(
                command.Frame,
                command.SourceId,
                new GameplayTriggerSourceBindingId(command.TargetId),
                envelope.Data0,
                (GameplayTriggerSignalOrdering)command.Payload0,
                itemIndex,
                itemCount,
                envelope.Provenance,
                signal);
            status = GameplayTriggerSignalCommandCodecStatus.None;
            return true;
        }

        internal static bool HasRawBatchIdentity(
            RuntimeCommand command,
            RuntimeCommand batchCommand)
        {
            return command.CommandId == GameplayTriggerRuntimeCommandIds.SubmitSignalBatch
                && command.Frame == batchCommand.Frame
                && command.SourceId == batchCommand.SourceId
                && command.TargetId == batchCommand.TargetId
                && command.Envelope.Data0 == batchCommand.Envelope.Data0;
        }

        private static RuntimeCommand CreateRecord(
            RuntimeFrame frame,
            int sourceId,
            RuntimeCommandProvenanceKind provenance,
            GameplayTriggerSourceBindingId sourceBindingId,
            GameplayTriggerSignalOrdering ordering,
            long batchId,
            int itemIndex,
            int itemCount,
            GameplayTriggerSignal signal,
            RuntimeTraceIdentity traceId)
        {
            var envelope = new RuntimeCommandEnvelope(
                GameplayTriggerRuntimeEnvelopeIds.SignalBatch,
                GameplayTriggerRuntimeEnvelopeIds.SignalBatchSchemaVersion,
                provenance,
                batchId,
                signal.SignalId,
                GameplayTriggerCommandCodecUtilities.PackEntity(signal.Subject),
                GameplayTriggerCommandCodecUtilities.PackEntity(signal.Instigator),
                signal.Payload0,
                signal.Payload1,
                signal.Payload2,
                signal.Sequence);
            return new RuntimeCommand(
                frame,
                sourceId,
                GameplayTriggerRuntimeCommandIds.SubmitSignalBatch,
                sourceBindingId.Value,
                (int)ordering,
                itemIndex,
                itemCount,
                traceId,
                envelope: envelope);
        }

        private static bool TryUnpackEntity(long packed, out GameplayEntityId entity)
        {
            return GameplayTriggerCommandCodecUtilities.TryUnpackEntity(
                packed,
                allowDefault: true,
                out entity);
        }

        private static bool IsSupportedOrdering(GameplayTriggerSignalOrdering ordering)
        {
            return ordering == GameplayTriggerSignalOrdering.Ordered ||
                ordering == GameplayTriggerSignalOrdering.UnorderedMultiset;
        }

        private static bool IsSupportedOrderingValue(int ordering)
        {
            return ordering == (int)GameplayTriggerSignalOrdering.Ordered ||
                ordering == (int)GameplayTriggerSignalOrdering.UnorderedMultiset;
        }

        private static bool IsSupportedProvenance(RuntimeCommandProvenanceKind provenance)
        {
            return GameplayTriggerCommandCodecUtilities.IsSupportedProvenance(provenance);
        }
    }
}
