using System;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    public enum GameplayTriggerScalarCommandCodecStatus
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

    /// <summary>Decoded fixed record belonging to one complete scalar observation.</summary>
    public readonly struct GameplayTriggerScalarCommandRecord
    {
        internal GameplayTriggerScalarCommandRecord(
            RuntimeFrame frame,
            int sourceId,
            GameplayTriggerSourceBindingId sourceBindingId,
            long batchId,
            int itemIndex,
            int itemCount,
            RuntimeCommandProvenanceKind provenance,
            GameplayTriggerScalarSample sample)
        {
            Frame = frame;
            SourceId = sourceId;
            SourceBindingId = sourceBindingId;
            BatchId = batchId;
            ItemIndex = itemIndex;
            ItemCount = itemCount;
            Provenance = provenance;
            Sample = sample;
        }

        public RuntimeFrame Frame { get; }
        public int SourceId { get; }
        public GameplayTriggerSourceBindingId SourceBindingId { get; }
        public long BatchId { get; }
        public int ItemIndex { get; }
        public int ItemCount { get; }
        public RuntimeCommandProvenanceKind Provenance { get; }
        public GameplayTriggerScalarSample Sample { get; }
        public bool IsEmptyBatch => ItemCount == 0;
    }

    /// <summary>Encodes and decodes complete scalar observations as fixed command records.</summary>
    public static class GameplayTriggerScalarCommandCodec
    {
        /// <summary>Writes one record per sample, or one explicit -1/0 item record for an empty batch.</summary>
        /// <remarks>NoAlloc. The destination is not modified when validation fails.</remarks>
        public static bool TryEncodeBatch(
            RuntimeFrame frame,
            int sourceId,
            RuntimeCommandProvenanceKind provenance,
            GameplayTriggerSourceBindingId sourceBindingId,
            long batchId,
            ReadOnlySpan<GameplayTriggerScalarSample> samples,
            Span<RuntimeCommand> destination,
            out int written,
            out GameplayTriggerScalarCommandCodecStatus status,
            RuntimeTraceIdentity traceId = default)
        {
            written = 0;
            if (sourceId <= 0 || sourceBindingId.Value <= 0 || batchId <= 0L ||
                !GameplayTriggerCommandCodecUtilities.IsSupportedProvenance(provenance))
            {
                status = GameplayTriggerScalarCommandCodecStatus.InvalidArgument;
                return false;
            }

            int required = samples.Length == 0 ? 1 : samples.Length;
            if (destination.Length < required)
            {
                status = GameplayTriggerScalarCommandCodecStatus.DestinationTooSmall;
                return false;
            }

            for (int i = 0; i < samples.Length; i++)
            {
                GameplayEntityId subject = samples[i].Subject;
                if (subject != default && !subject.IsValid)
                {
                    status = GameplayTriggerScalarCommandCodecStatus.InvalidEntity;
                    return false;
                }
            }

            if (samples.Length == 0)
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
                status = GameplayTriggerScalarCommandCodecStatus.None;
                return true;
            }

            for (int i = 0; i < samples.Length; i++)
            {
                destination[i] = CreateRecord(
                    frame,
                    sourceId,
                    provenance,
                    sourceBindingId,
                    batchId,
                    i,
                    samples.Length,
                    samples[i],
                    traceId);
            }

            written = samples.Length;
            status = GameplayTriggerScalarCommandCodecStatus.None;
            return true;
        }

        /// <remarks>NoAlloc.</remarks>
        public static bool TryDecodeRecord(
            RuntimeCommand command,
            out GameplayTriggerScalarCommandRecord record,
            out GameplayTriggerScalarCommandCodecStatus status)
        {
            record = default;
            if (command.CommandId != GameplayTriggerRuntimeCommandIds.SubmitScalarBatch)
            {
                status = GameplayTriggerScalarCommandCodecStatus.WrongCommandId;
                return false;
            }

            RuntimeCommandEnvelope envelope = command.Envelope;
            if (!envelope.IsPresent)
            {
                status = GameplayTriggerScalarCommandCodecStatus.MissingEnvelope;
                return false;
            }
            if (envelope.TypeId != GameplayTriggerRuntimeEnvelopeIds.ScalarBatch)
            {
                status = GameplayTriggerScalarCommandCodecStatus.WrongEnvelopeType;
                return false;
            }
            if (envelope.SchemaVersion != GameplayTriggerRuntimeEnvelopeIds.ScalarBatchSchemaVersion)
            {
                status = GameplayTriggerScalarCommandCodecStatus.UnsupportedSchema;
                return false;
            }
            if (!GameplayTriggerCommandCodecUtilities.IsSupportedProvenance(envelope.Provenance))
            {
                status = GameplayTriggerScalarCommandCodecStatus.InvalidProvenance;
                return false;
            }
            if (command.SourceId <= 0 || command.TargetId <= 0 || envelope.Data0 <= 0L ||
                command.Payload2 != 0)
            {
                status = GameplayTriggerScalarCommandCodecStatus.InvalidBatch;
                return false;
            }

            int itemIndex = command.Payload0;
            int itemCount = command.Payload1;
            if (itemCount < 0 ||
                (itemCount == 0 && itemIndex != -1) ||
                (itemCount > 0 && (itemIndex < 0 || itemIndex >= itemCount)))
            {
                status = GameplayTriggerScalarCommandCodecStatus.InvalidItem;
                return false;
            }

            if (envelope.Data3 != 0L || envelope.Data4 != 0L || envelope.Data5 != 0L ||
                envelope.Data6 != 0L || envelope.Data7 != 0L)
            {
                status = GameplayTriggerScalarCommandCodecStatus.ReservedSlotNotZero;
                return false;
            }

            GameplayTriggerScalarSample sample = default;
            if (itemCount == 0)
            {
                if (envelope.Data1 != 0L || envelope.Data2 != 0L)
                {
                    status = GameplayTriggerScalarCommandCodecStatus.ReservedSlotNotZero;
                    return false;
                }
            }
            else if (!GameplayTriggerCommandCodecUtilities.TryUnpackEntity(
                         envelope.Data1,
                         allowDefault: true,
                         out GameplayEntityId subject))
            {
                status = GameplayTriggerScalarCommandCodecStatus.InvalidEntity;
                return false;
            }
            else
            {
                sample = new GameplayTriggerScalarSample(subject, Fix64.FromRaw(envelope.Data2));
            }

            record = new GameplayTriggerScalarCommandRecord(
                command.Frame,
                command.SourceId,
                new GameplayTriggerSourceBindingId(command.TargetId),
                envelope.Data0,
                itemIndex,
                itemCount,
                envelope.Provenance,
                sample);
            status = GameplayTriggerScalarCommandCodecStatus.None;
            return true;
        }

        internal static bool HasRawBatchIdentity(RuntimeCommand command, RuntimeCommand batchCommand)
        {
            return command.CommandId == GameplayTriggerRuntimeCommandIds.SubmitScalarBatch &&
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
            GameplayTriggerScalarSample sample,
            RuntimeTraceIdentity traceId)
        {
            var envelope = new RuntimeCommandEnvelope(
                GameplayTriggerRuntimeEnvelopeIds.ScalarBatch,
                GameplayTriggerRuntimeEnvelopeIds.ScalarBatchSchemaVersion,
                provenance,
                batchId,
                GameplayTriggerCommandCodecUtilities.PackEntity(sample.Subject),
                sample.Value.RawValue);
            return new RuntimeCommand(
                frame,
                sourceId,
                GameplayTriggerRuntimeCommandIds.SubmitScalarBatch,
                sourceBindingId.Value,
                itemIndex,
                itemCount,
                payload2: 0,
                traceId: traceId,
                envelope: envelope);
        }
    }
}
