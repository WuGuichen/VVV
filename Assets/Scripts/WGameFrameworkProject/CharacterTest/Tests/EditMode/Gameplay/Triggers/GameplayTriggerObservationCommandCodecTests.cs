using System;
using System.Collections.Generic;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerObservationCommandCodecTests
    {
        private static readonly GameplayTriggerSourceDefinitionId SourceDefinitionId =
            new GameplayTriggerSourceDefinitionId(1);
        private static readonly GameplayTriggerDefinitionId DefinitionId =
            new GameplayTriggerDefinitionId(1);
        private static readonly GameplayTriggerSourceBindingId SourceBindingId =
            new GameplayTriggerSourceBindingId(1);
        private static readonly GameplayTriggerActionKindId ActionKindId =
            new GameplayTriggerActionKindId(1);

        [Test]
        public void ObservationCommandIdsAndEnvelopeIds_AreStableAndAdditive()
        {
            Assert.AreEqual(1002001, GameplayTriggerRuntimeCommandIds.SetEnabled);
            Assert.AreEqual(1002002, GameplayTriggerRuntimeCommandIds.Reset);
            Assert.AreEqual(1002003, GameplayTriggerRuntimeCommandIds.SubmitSignalBatch);
            Assert.AreEqual(1002004, GameplayTriggerRuntimeCommandIds.SubmitOccupancyBatch);
            Assert.AreEqual(1002005, GameplayTriggerRuntimeCommandIds.SubmitScalarBatch);
            Assert.AreEqual(1002101, GameplayTriggerRuntimeEnvelopeIds.SignalBatch);
            Assert.AreEqual(1002102, GameplayTriggerRuntimeEnvelopeIds.OccupancyBatch);
            Assert.AreEqual(1002103, GameplayTriggerRuntimeEnvelopeIds.ScalarBatch);
            Assert.AreEqual(1, GameplayTriggerRuntimeEnvelopeIds.OccupancyBatchSchemaVersion);
            Assert.AreEqual(1, GameplayTriggerRuntimeEnvelopeIds.ScalarBatchSchemaVersion);
        }

        [Test]
        public void OccupancyCodec_RoundTripsNonEmptyAndExplicitEmptyBatch()
        {
            var subjects = new[]
            {
                new GameplayEntityId(3, 4),
                new GameplayEntityId(int.MaxValue, int.MaxValue)
            };
            var commands = new RuntimeCommand[2];

            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                new RuntimeFrame(7),
                21,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                99L,
                subjects,
                commands,
                out int written,
                out GameplayTriggerOccupancyCommandCodecStatus status,
                new RuntimeTraceIdentity(981001)));
            Assert.AreEqual(GameplayTriggerOccupancyCommandCodecStatus.None, status);
            Assert.AreEqual(2, written);
            for (int i = 0; i < written; i++)
            {
                RuntimeCommand command = commands[i];
                Assert.AreEqual(i, command.Payload0);
                Assert.AreEqual(2, command.Payload1);
                Assert.AreEqual(0, command.Payload2);
                Assert.AreEqual(99L, command.Envelope.Data0);
                Assert.AreEqual(0L, command.Envelope.Data2);
                Assert.AreEqual(0L, command.Envelope.Data7);
                Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryDecodeRecord(
                    command,
                    out GameplayTriggerOccupancyCommandRecord decoded,
                    out status));
                Assert.AreEqual(subjects[i], decoded.Subject);
                Assert.AreEqual(i, decoded.ItemIndex);
                Assert.AreEqual(2, decoded.ItemCount);
                Assert.AreEqual(99L, decoded.BatchId);
                Assert.AreEqual(RuntimeCommandProvenanceKind.Exogenous, decoded.Provenance);
            }

            var empty = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero,
                22,
                RuntimeCommandProvenanceKind.Derived,
                SourceBindingId,
                100L,
                ReadOnlySpan<GameplayEntityId>.Empty,
                empty,
                out written,
                out status));
            Assert.AreEqual(1, written);
            Assert.AreEqual(-1, empty[0].Payload0);
            Assert.AreEqual(0, empty[0].Payload1);
            Assert.AreEqual(0, empty[0].Payload2);
            AssertEnvelopeDataIsZero(empty[0].Envelope, 1);
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryDecodeRecord(
                empty[0],
                out GameplayTriggerOccupancyCommandRecord emptyRecord,
                out status));
            Assert.IsTrue(emptyRecord.IsEmptyBatch);
            Assert.AreEqual(-1, emptyRecord.ItemIndex);
            Assert.AreEqual(default(GameplayEntityId), emptyRecord.Subject);
        }

        [Test]
        public void ScalarCodec_RoundTripsOptionalSubjectsAndRawLongBoundaries()
        {
            var samples = new[]
            {
                new GameplayTriggerScalarSample(default, Fix64.FromRaw(long.MinValue)),
                new GameplayTriggerScalarSample(
                    new GameplayEntityId(int.MaxValue, int.MaxValue),
                    Fix64.FromRaw(long.MaxValue))
            };
            var commands = new RuntimeCommand[2];

            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                new RuntimeFrame(8),
                31,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                101L,
                samples,
                commands,
                out int written,
                out GameplayTriggerScalarCommandCodecStatus status));
            Assert.AreEqual(2, written);
            for (int i = 0; i < written; i++)
            {
                RuntimeCommand command = commands[i];
                Assert.AreEqual(i, command.Payload0);
                Assert.AreEqual(2, command.Payload1);
                Assert.AreEqual(0, command.Payload2);
                Assert.AreEqual(samples[i].Value.RawValue, command.Envelope.Data2);
                AssertEnvelopeDataIsZero(command.Envelope, 3);
                Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryDecodeRecord(
                    command,
                    out GameplayTriggerScalarCommandRecord decoded,
                    out status));
                Assert.AreEqual(samples[i].Subject, decoded.Sample.Subject);
                Assert.AreEqual(samples[i].Value.RawValue, decoded.Sample.Value.RawValue);
            }

            var empty = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero,
                32,
                RuntimeCommandProvenanceKind.Derived,
                SourceBindingId,
                102L,
                ReadOnlySpan<GameplayTriggerScalarSample>.Empty,
                empty,
                out written,
                out status));
            Assert.AreEqual(1, written);
            Assert.AreEqual(-1, empty[0].Payload0);
            Assert.AreEqual(0, empty[0].Payload1);
            Assert.AreEqual(0, empty[0].Payload2);
            AssertEnvelopeDataIsZero(empty[0].Envelope, 1);
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryDecodeRecord(
                empty[0],
                out GameplayTriggerScalarCommandRecord emptyRecord,
                out status));
            Assert.IsTrue(emptyRecord.IsEmptyBatch);
            Assert.AreEqual(-1, emptyRecord.ItemIndex);
            Assert.AreEqual(0L, emptyRecord.Sample.Value.RawValue);
        }

        [Test]
        public void ObservationCodecs_PrevalidateBeforeWritingDestination()
        {
            RuntimeCommand sentinel = new RuntimeCommand(
                new RuntimeFrame(9),
                sourceId: 77,
                commandId: 88,
                targetId: 99,
                payload0: 1,
                payload1: 2,
                payload2: 3,
                traceId: CharacterTestRuntimeTraceIds.Sentinel);
            var occupancyDestination = new[] { sentinel, sentinel };
            var invalidSubjects = new[]
            {
                new GameplayEntityId(1, 1),
                default(GameplayEntityId)
            };

            Assert.IsFalse(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero,
                1,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                1L,
                invalidSubjects,
                occupancyDestination,
                out int written,
                out GameplayTriggerOccupancyCommandCodecStatus occupancyStatus));
            Assert.AreEqual(GameplayTriggerOccupancyCommandCodecStatus.InvalidEntity, occupancyStatus);
            Assert.AreEqual(0, written);
            Assert.AreEqual(sentinel, occupancyDestination[0]);
            Assert.AreEqual(sentinel, occupancyDestination[1]);

            var scalarDestination = new[] { sentinel };
            var samples = new[]
            {
                new GameplayTriggerScalarSample(default, Fix64.MinValue),
                new GameplayTriggerScalarSample(new GameplayEntityId(1, 1), Fix64.MaxValue)
            };
            Assert.IsFalse(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero,
                1,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                2L,
                samples,
                scalarDestination,
                out written,
                out GameplayTriggerScalarCommandCodecStatus scalarStatus));
            Assert.AreEqual(GameplayTriggerScalarCommandCodecStatus.DestinationTooSmall, scalarStatus);
            Assert.AreEqual(0, written);
            Assert.AreEqual(sentinel, scalarDestination[0]);
        }

        [Test]
        public void ObservationCodecs_RejectReservedSlotsAndInvalidPackedEntities()
        {
            var occupancy = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero,
                1,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                1L,
                new[] { new GameplayEntityId(1, 1) },
                occupancy,
                out _,
                out _));
            RuntimeCommand occupancyReserved = WithEnvelope(
                occupancy[0],
                CopyEnvelope(occupancy[0].Envelope, data3: 1L));
            Assert.IsFalse(GameplayTriggerOccupancyCommandCodec.TryDecodeRecord(
                occupancyReserved,
                out _,
                out GameplayTriggerOccupancyCommandCodecStatus occupancyStatus));
            Assert.AreEqual(
                GameplayTriggerOccupancyCommandCodecStatus.ReservedSlotNotZero,
                occupancyStatus);

            var scalar = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero,
                1,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                2L,
                new[] { new GameplayTriggerScalarSample(default, Fix64.One) },
                scalar,
                out _,
                out _));
            RuntimeCommand invalidPackedEntity = WithEnvelope(
                scalar[0],
                CopyEnvelope(scalar[0].Envelope, data1: 1L));
            Assert.IsFalse(GameplayTriggerScalarCommandCodec.TryDecodeRecord(
                invalidPackedEntity,
                out _,
                out GameplayTriggerScalarCommandCodecStatus scalarStatus));
            Assert.AreEqual(GameplayTriggerScalarCommandCodecStatus.InvalidEntity, scalarStatus);
        }

        [Test]
        public void OccupancyDecoder_RejectsCommandEnvelopeSchemaAndProvenanceMismatches()
        {
            var commands = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L, new[] { new GameplayEntityId(1, 1) },
                commands, out _, out _));
            RuntimeCommand valid = commands[0];

            Assert.IsFalse(GameplayTriggerOccupancyCommandCodec.TryDecodeRecord(
                WithCommandId(valid, GameplayTriggerRuntimeCommandIds.SubmitScalarBatch),
                out _,
                out GameplayTriggerOccupancyCommandCodecStatus status));
            Assert.AreEqual(GameplayTriggerOccupancyCommandCodecStatus.WrongCommandId, status);
            Assert.IsFalse(GameplayTriggerOccupancyCommandCodec.TryDecodeRecord(
                WithEnvelope(valid, CopyEnvelope(valid.Envelope,
                    typeId: GameplayTriggerRuntimeEnvelopeIds.ScalarBatch)),
                out _,
                out status));
            Assert.AreEqual(GameplayTriggerOccupancyCommandCodecStatus.WrongEnvelopeType, status);
            Assert.IsFalse(GameplayTriggerOccupancyCommandCodec.TryDecodeRecord(
                WithEnvelope(valid, CopyEnvelope(valid.Envelope, schemaVersion: 2)),
                out _,
                out status));
            Assert.AreEqual(GameplayTriggerOccupancyCommandCodecStatus.UnsupportedSchema, status);
            Assert.IsFalse(GameplayTriggerOccupancyCommandCodec.TryDecodeRecord(
                WithEnvelope(valid, CopyEnvelope(valid.Envelope,
                    provenance: RuntimeCommandProvenanceKind.Unspecified)),
                out _,
                out status));
            Assert.AreEqual(GameplayTriggerOccupancyCommandCodecStatus.InvalidProvenance, status);
        }

        [Test]
        public void ScalarDecoder_RejectsCommandEnvelopeSchemaAndProvenanceMismatches()
        {
            var commands = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L,
                new[] { new GameplayTriggerScalarSample(default, Fix64.One) },
                commands, out _, out _));
            RuntimeCommand valid = commands[0];

            Assert.IsFalse(GameplayTriggerScalarCommandCodec.TryDecodeRecord(
                WithCommandId(valid, GameplayTriggerRuntimeCommandIds.SubmitOccupancyBatch),
                out _,
                out GameplayTriggerScalarCommandCodecStatus status));
            Assert.AreEqual(GameplayTriggerScalarCommandCodecStatus.WrongCommandId, status);
            Assert.IsFalse(GameplayTriggerScalarCommandCodec.TryDecodeRecord(
                WithEnvelope(valid, CopyEnvelope(valid.Envelope,
                    typeId: GameplayTriggerRuntimeEnvelopeIds.OccupancyBatch)),
                out _,
                out status));
            Assert.AreEqual(GameplayTriggerScalarCommandCodecStatus.WrongEnvelopeType, status);
            Assert.IsFalse(GameplayTriggerScalarCommandCodec.TryDecodeRecord(
                WithEnvelope(valid, CopyEnvelope(valid.Envelope, schemaVersion: 2)),
                out _,
                out status));
            Assert.AreEqual(GameplayTriggerScalarCommandCodecStatus.UnsupportedSchema, status);
            Assert.IsFalse(GameplayTriggerScalarCommandCodec.TryDecodeRecord(
                WithEnvelope(valid, CopyEnvelope(valid.Envelope,
                    provenance: RuntimeCommandProvenanceKind.Unspecified)),
                out _,
                out status));
            Assert.AreEqual(GameplayTriggerScalarCommandCodecStatus.InvalidProvenance, status);
        }

        [Test]
        public void OccupancyEncoder_EnforcesDestinationNMinusOneNAndNPlusOneAtomically()
        {
            RuntimeCommand sentinel = new RuntimeCommand(RuntimeFrame.Zero, 7, 8, 9);
            var subjects = new[] { new GameplayEntityId(1, 1), new GameplayEntityId(2, 1) };
            var tooSmall = new[] { sentinel };
            Assert.IsFalse(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L, subjects, tooSmall, out int written,
                out GameplayTriggerOccupancyCommandCodecStatus status));
            Assert.AreEqual(GameplayTriggerOccupancyCommandCodecStatus.DestinationTooSmall, status);
            Assert.AreEqual(0, written);
            Assert.AreEqual(sentinel, tooSmall[0]);

            var exact = new[] { sentinel, sentinel };
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L, subjects, exact, out written, out status));
            Assert.AreEqual(2, written);

            var larger = new[] { sentinel, sentinel, sentinel };
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L, subjects, larger, out written, out status));
            Assert.AreEqual(2, written);
            Assert.AreEqual(sentinel, larger[2]);
        }

        [Test]
        public void ScalarEncoder_EnforcesDestinationNMinusOneNAndNPlusOneAtomically()
        {
            RuntimeCommand sentinel = new RuntimeCommand(RuntimeFrame.Zero, 7, 8, 9);
            var samples = new[]
            {
                new GameplayTriggerScalarSample(default, Fix64.MinValue),
                new GameplayTriggerScalarSample(new GameplayEntityId(2, 1), Fix64.MaxValue)
            };
            var tooSmall = new[] { sentinel };
            Assert.IsFalse(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L, samples, tooSmall, out int written,
                out GameplayTriggerScalarCommandCodecStatus status));
            Assert.AreEqual(GameplayTriggerScalarCommandCodecStatus.DestinationTooSmall, status);
            Assert.AreEqual(0, written);
            Assert.AreEqual(sentinel, tooSmall[0]);

            var exact = new[] { sentinel, sentinel };
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L, samples, exact, out written, out status));
            Assert.AreEqual(2, written);

            var larger = new[] { sentinel, sentinel, sentinel };
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L, samples, larger, out written, out status));
            Assert.AreEqual(2, written);
            Assert.AreEqual(sentinel, larger[2]);
        }

        [Test]
        public void ObservationEntityPacking_MatchesSignalGenerationSafeLayout()
        {
            var entity = new GameplayEntityId(123, 456);
            var occupancy = new RuntimeCommand[1];
            var scalar = new RuntimeCommand[1];
            var signal = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L, new[] { entity }, occupancy, out _, out _));
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 2L,
                new[] { new GameplayTriggerScalarSample(entity, Fix64.Zero) },
                scalar, out _, out _));
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, GameplayTriggerSignalOrdering.UnorderedMultiset,
                3L, new[] { new GameplayTriggerSignal(1, entity) },
                signal, out _, out _));

            Assert.AreEqual(signal[0].Envelope.Data2, occupancy[0].Envelope.Data1);
            Assert.AreEqual(signal[0].Envelope.Data2, scalar[0].Envelope.Data1);
        }

        [Test]
        public void CommandSystem_PreservesFourParameterConstructorAndAddsExplicitCapacities()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(GameplayTriggerSourceKind.Occupancy);
            var compatible = new GameplayTriggerCommandSystem(
                runtime,
                "compatible",
                901,
                3);
            var explicitCapacities = new GameplayTriggerCommandSystem(
                runtime,
                "explicit",
                902,
                maxSignalCommandItems: 3,
                maxOccupancyCommandItems: 4,
                maxScalarCommandItems: 5);

            Assert.AreEqual("compatible", compatible.SystemId);
            Assert.AreEqual(901, compatible.Priority);
            Assert.AreEqual("explicit", explicitCapacities.SystemId);
            Assert.AreEqual(902, explicitCapacities.Priority);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new GameplayTriggerCommandSystem(runtime, "invalid", 0, 1, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new GameplayTriggerCommandSystem(runtime, "invalid", 0, 1, 1, 0));
        }

        [Test]
        public void EmptyObservationCommands_SubmitCompleteEmptyBatches()
        {
            GameplayTriggerRuntime occupancyRuntime = CreateRuntime(GameplayTriggerSourceKind.Occupancy);
            var occupancy = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L, ReadOnlySpan<GameplayEntityId>.Empty,
                occupancy, out _, out _));
            GameplayTriggerCommandSystem occupancySystem = RunCommandSystem(
                occupancyRuntime,
                occupancy,
                RuntimeFrame.Zero,
                maxOccupancyItems: 1,
                maxScalarItems: 1);
            Assert.IsTrue(occupancySystem.LastResult.Result.Success);
            Assert.AreEqual(1, occupancySystem.HandledCommandCount);
            Assert.IsTrue(occupancyRuntime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(0, occupancyRuntime.RelationCount);

            GameplayTriggerRuntime scalarRuntime = CreateRuntime(GameplayTriggerSourceKind.Threshold);
            var scalar = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 2L, ReadOnlySpan<GameplayTriggerScalarSample>.Empty,
                scalar, out _, out _));
            GameplayTriggerCommandSystem scalarSystem = RunCommandSystem(
                scalarRuntime,
                scalar,
                RuntimeFrame.Zero,
                maxOccupancyItems: 1,
                maxScalarItems: 1);
            Assert.IsTrue(scalarSystem.LastResult.Result.Success);
            Assert.AreEqual(1, scalarSystem.HandledCommandCount);
            Assert.IsTrue(scalarRuntime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(0, scalarRuntime.RelationCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OccupancyCommandSystem_ReconstructsByItemIndexAcrossTransportOrder(
            bool reverse)
        {
            GameplayTriggerRuntime runtime = CreateRuntime(GameplayTriggerSourceKind.Occupancy);
            var expected = new[]
            {
                new GameplayEntityId(8, 1),
                new GameplayEntityId(3, 2)
            };
            var encoded = new RuntimeCommand[2];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 2, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 10L, expected, encoded, out _, out _));
            RuntimeCommand[] transport = reverse
                ? new[] { encoded[1], encoded[0] }
                : encoded;

            GameplayTriggerCommandSystem system = RunCommandSystem(
                runtime,
                transport,
                RuntimeFrame.Zero,
                maxOccupancyItems: 2,
                maxScalarItems: 2);
            GameplayTriggerResult evaluation = runtime.Evaluate(RuntimeFrame.Zero);

            Assert.IsTrue(system.LastResult.Result.Success);
            Assert.AreEqual(GameplayTriggerRuntimeCommandKind.SubmitOccupancyBatch,
                system.LastResult.RuntimeCommandKind);
            Assert.IsTrue(evaluation.Success);
            Assert.AreEqual(2, runtime.OccurrenceCount);
            Assert.AreEqual(new GameplayEntityId(3, 2), runtime.GetOccurrence(0).Subject);
            Assert.AreEqual(new GameplayEntityId(8, 1), runtime.GetOccurrence(1).Subject);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ScalarCommandSystem_ReconstructsByItemIndexAndPreservesRawValues(
            bool reverse)
        {
            GameplayTriggerRuntime runtime = CreateRuntime(GameplayTriggerSourceKind.Threshold);
            var samples = new[]
            {
                new GameplayTriggerScalarSample(new GameplayEntityId(8, 1), Fix64.MaxValue),
                new GameplayTriggerScalarSample(new GameplayEntityId(3, 2), Fix64.MinValue)
            };
            var encoded = new RuntimeCommand[2];
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 2, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 11L, samples, encoded, out _, out _));
            RuntimeCommand[] transport = reverse
                ? new[] { encoded[1], encoded[0] }
                : encoded;

            GameplayTriggerCommandSystem system = RunCommandSystem(
                runtime,
                transport,
                RuntimeFrame.Zero,
                maxOccupancyItems: 2,
                maxScalarItems: 2);
            GameplayTriggerResult evaluation = runtime.Evaluate(RuntimeFrame.Zero);

            Assert.IsTrue(system.LastResult.Result.Success);
            Assert.AreEqual(GameplayTriggerRuntimeCommandKind.SubmitScalarBatch,
                system.LastResult.RuntimeCommandKind);
            Assert.IsTrue(evaluation.Success);
            Assert.AreEqual(1, runtime.OccurrenceCount);
            Assert.AreEqual(new GameplayEntityId(8, 1), runtime.GetOccurrence(0).Subject);
            Assert.AreEqual(long.MaxValue, runtime.GetOccurrence(0).CurrentScalar.RawValue);
        }

        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        public void OccupancyCommandSystem_EnforcesNMinusOneNAndNPlusOneCapacity(
            int itemCount,
            bool expectedSuccess)
        {
            GameplayTriggerRuntime runtime = CreateRuntime(GameplayTriggerSourceKind.Occupancy);
            var subjects = new GameplayEntityId[itemCount];
            for (int i = 0; i < subjects.Length; i++)
                subjects[i] = new GameplayEntityId(i + 1, 1);
            var commands = new RuntimeCommand[itemCount];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L, subjects, commands, out _, out _));

            GameplayTriggerCommandSystem system = RunCommandSystem(
                runtime,
                commands,
                RuntimeFrame.Zero,
                maxOccupancyItems: 2,
                maxScalarItems: 2);

            Assert.AreEqual(expectedSuccess, system.LastResult.Result.Success);
            if (expectedSuccess)
            {
                Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
                Assert.AreEqual(itemCount, runtime.RelationCount);
            }
            else
            {
                Assert.AreEqual(GameplayTriggerErrorCode.InputCapacityExceeded,
                    system.LastResult.Result.ErrorCode);
                Assert.AreEqual(0, runtime.RelationCount);
                Assert.AreEqual(itemCount, system.RejectedCommandCount);
            }
        }

        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        public void ScalarCommandSystem_EnforcesNMinusOneNAndNPlusOneCapacity(
            int itemCount,
            bool expectedSuccess)
        {
            GameplayTriggerRuntime runtime = CreateRuntime(GameplayTriggerSourceKind.Threshold);
            var samples = new GameplayTriggerScalarSample[itemCount];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = new GameplayTriggerScalarSample(
                    new GameplayEntityId(i + 1, 1),
                    Fix64.FromRaw(i + 2));
            }
            var commands = new RuntimeCommand[itemCount];
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L, samples, commands, out _, out _));

            GameplayTriggerCommandSystem system = RunCommandSystem(
                runtime,
                commands,
                RuntimeFrame.Zero,
                maxOccupancyItems: 2,
                maxScalarItems: 2);

            Assert.AreEqual(expectedSuccess, system.LastResult.Result.Success);
            if (expectedSuccess)
            {
                Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
                Assert.AreEqual(itemCount, runtime.RelationCount);
            }
            else
            {
                Assert.AreEqual(GameplayTriggerErrorCode.InputCapacityExceeded,
                    system.LastResult.Result.ErrorCode);
                Assert.AreEqual(0, runtime.RelationCount);
                Assert.AreEqual(itemCount, system.RejectedCommandCount);
            }
        }

        [Test]
        public void OccupancyIncompleteBatch_IsAtomicAndDoesNotCreateMassExit()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(GameplayTriggerSourceKind.Occupancy);
            var initial = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L, new[] { new GameplayEntityId(1, 1) },
                initial, out _, out _));
            GameplayTriggerCommandSystem initialSystem = RunCommandSystem(
                runtime, initial, RuntimeFrame.Zero, 2, 2);
            Assert.IsTrue(initialSystem.LastResult.Result.Success);
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(1, runtime.RelationCount);

            var next = new RuntimeCommand[2];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                new RuntimeFrame(1), 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 2L,
                new[] { new GameplayEntityId(2, 1), new GameplayEntityId(3, 1) },
                next, out _, out _));
            GameplayTriggerCommandSystem rejected = RunCommandSystem(
                runtime,
                new[] { next[0] },
                new RuntimeFrame(1),
                2,
                2);

            Assert.IsFalse(rejected.LastResult.Result.Success);
            Assert.AreEqual(GameplayTriggerErrorCode.InvalidArgument,
                rejected.LastResult.Result.ErrorCode);
            Assert.IsTrue(runtime.Evaluate(new RuntimeFrame(1)).Success);
            Assert.AreEqual(1, runtime.RelationCount);
            Assert.AreEqual(0, runtime.OccurrenceCount);
        }

        [Test]
        public void ObservationMalformedOrDuplicateItems_RejectWholeRawBatchAndMarkAllHandled()
        {
            GameplayTriggerRuntime occupancyRuntime = CreateRuntime(GameplayTriggerSourceKind.Occupancy);
            var occupancy = new RuntimeCommand[2];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L,
                new[] { new GameplayEntityId(1, 1), new GameplayEntityId(2, 1) },
                occupancy, out _, out _));
            occupancy[1] = WithPayload(occupancy[1], payload0: 0);
            GameplayTriggerCommandSystem occupancySystem = RunCommandSystem(
                occupancyRuntime, occupancy, RuntimeFrame.Zero, 2, 2);
            Assert.IsFalse(occupancySystem.LastResult.Result.Success);
            Assert.AreEqual(2, occupancySystem.HandledCommandCount);
            Assert.AreEqual(2, occupancySystem.RejectedCommandCount);
            Assert.IsTrue(occupancyRuntime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(0, occupancyRuntime.RelationCount);

            GameplayTriggerRuntime scalarRuntime = CreateRuntime(GameplayTriggerSourceKind.Threshold);
            var scalar = new RuntimeCommand[2];
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 2L,
                new[]
                {
                    new GameplayTriggerScalarSample(new GameplayEntityId(1, 1), Fix64.One),
                    new GameplayTriggerScalarSample(new GameplayEntityId(2, 1), Fix64.One)
                },
                scalar, out _, out _));
            scalar[1] = WithPayload(scalar[1], payload1: 3);
            GameplayTriggerCommandSystem scalarSystem = RunCommandSystem(
                scalarRuntime, scalar, RuntimeFrame.Zero, 2, 2);
            Assert.IsFalse(scalarSystem.LastResult.Result.Success);
            Assert.AreEqual(2, scalarSystem.HandledCommandCount);
            Assert.AreEqual(2, scalarSystem.RejectedCommandCount);
            Assert.IsTrue(scalarRuntime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(0, scalarRuntime.RelationCount);
        }

        [Test]
        public void CommandIdSeparatesOccupancyAndScalarWithOtherwiseIdenticalRawBatchIdentity()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(GameplayTriggerSourceKind.Occupancy);
            var occupancy = new RuntimeCommand[1];
            var scalar = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 77L, new[] { new GameplayEntityId(1, 1) },
                occupancy, out _, out _));
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 77L,
                new[] { new GameplayTriggerScalarSample(default, Fix64.One) },
                scalar, out _, out _));

            GameplayTriggerCommandSystem system = RunCommandSystem(
                runtime,
                new[] { occupancy[0], scalar[0] },
                RuntimeFrame.Zero,
                1,
                1);

            Assert.AreEqual(2, system.HandledCommandCount);
            Assert.AreEqual(1, system.RejectedCommandCount);
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(1, runtime.RelationCount);
            Assert.AreEqual(new GameplayEntityId(1, 1), runtime.GetOccurrence(0).Subject);
        }

        [Test]
        public void ObservationProvenanceMismatch_RejectsEveryRecordInRawBatch()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(GameplayTriggerSourceKind.Occupancy);
            var commands = new RuntimeCommand[2];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L,
                new[] { new GameplayEntityId(1, 1), new GameplayEntityId(2, 1) },
                commands, out _, out _));
            commands[1] = WithEnvelope(
                commands[1],
                CopyEnvelope(commands[1].Envelope,
                    provenance: RuntimeCommandProvenanceKind.Derived));

            GameplayTriggerCommandSystem system = RunCommandSystem(
                runtime, commands, RuntimeFrame.Zero, 2, 2);

            Assert.IsFalse(system.LastResult.Result.Success);
            Assert.AreEqual(GameplayTriggerErrorCode.InvalidArgument,
                system.LastResult.Result.ErrorCode);
            Assert.AreEqual(2, system.HandledCommandCount);
            Assert.AreEqual(2, system.RejectedCommandCount);
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(0, runtime.RelationCount);
        }

        [Test]
        public void ScalarDuplicateSubject_PreservesExistingAuthorityAsWholeBatchReject()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(GameplayTriggerSourceKind.Threshold);
            var initial = new RuntimeCommand[1];
            GameplayEntityId existing = new GameplayEntityId(1, 1);
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L,
                new[] { new GameplayTriggerScalarSample(existing, Fix64.FromRaw(2)) },
                initial, out _, out _));
            Assert.IsTrue(RunCommandSystem(runtime, initial, RuntimeFrame.Zero, 2, 2)
                .LastResult.Result.Success);
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(1, runtime.RelationCount);

            GameplayEntityId duplicate = new GameplayEntityId(2, 1);
            var duplicateBatch = new RuntimeCommand[2];
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                new RuntimeFrame(1), 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 2L,
                new[]
                {
                    new GameplayTriggerScalarSample(duplicate, Fix64.FromRaw(3)),
                    new GameplayTriggerScalarSample(duplicate, Fix64.FromRaw(4))
                },
                duplicateBatch, out _, out _));
            GameplayTriggerCommandSystem commandSystem = RunCommandSystem(
                runtime, duplicateBatch, new RuntimeFrame(1), 2, 2);
            Assert.IsTrue(commandSystem.LastResult.Result.Success);

            GameplayTriggerResult result = runtime.Evaluate(new RuntimeFrame(1));
            Assert.IsFalse(result.Success);
            Assert.AreEqual(GameplayTriggerErrorCode.DuplicateScalarSubject, result.ErrorCode);
            Assert.AreEqual(1, runtime.RelationCount);
            Assert.AreEqual(0, runtime.OccurrenceCount);
        }

        [Test]
        public void SourceGateAndExogenousReplay_AcceptObservationCommandsAndExcludeDerived()
        {
            const int externalSource = 21;
            const int derivedSource = 22;
            int[] commandIds =
            {
                GameplayTriggerRuntimeCommandIds.SubmitSignalBatch,
                GameplayTriggerRuntimeCommandIds.SubmitOccupancyBatch,
                GameplayTriggerRuntimeCommandIds.SubmitScalarBatch
            };
            var registry = new RuntimeCommandSourceRegistry(initialCapacity: 2);
            Assert.IsTrue(registry.TryRegister(
                externalSource,
                RuntimeCommandSourceKind.Exogenous,
                commandIds,
                out _));
            Assert.IsTrue(registry.TryRegister(
                derivedSource,
                RuntimeCommandSourceKind.Derived,
                commandIds,
                out _));
            var buffer = new RuntimeCommandBuffer(initialCapacity: 2);
            var gate = new RuntimeCommandSubmissionGate(
                registry,
                buffer,
                RuntimeCommandIngressKind.External);
            var occupancy = new RuntimeCommand[1];
            var scalar = new RuntimeCommand[1];
            var derived = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, externalSource, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L, new[] { new GameplayEntityId(1, 1) },
                occupancy, out _, out _));
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, externalSource, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 2L,
                new[] { new GameplayTriggerScalarSample(default, Fix64.One) },
                scalar, out _, out _));
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, derivedSource, RuntimeCommandProvenanceKind.Derived,
                SourceBindingId, 3L, new[] { new GameplayEntityId(2, 1) },
                derived, out _, out _));

            Assert.IsTrue(gate.Submit(occupancy[0]).Success);
            Assert.IsTrue(gate.Submit(scalar[0]).Success);
            RuntimeCommand rawLifecycle = GameplayTriggerRuntimeCommandFactory.SetEnabled(
                RuntimeFrame.Zero,
                new GameplayTriggerInstanceId(1, 1),
                enabled: true,
                sourceId: externalSource);
            Assert.AreEqual(RuntimeCommandErrorCode.ProvenanceMismatch,
                gate.Submit(rawLifecycle).Error.Code);
            Assert.AreEqual(RuntimeCommandErrorCode.UnauthorizedSource,
                gate.Submit(derived[0]).Error.Code);

            RuntimeReplayRecorder recorder = RuntimeReplayRecorder.CreateExogenousStream(
                new RuntimeReplayHeader(1, "0.1", "config", "resources", RuntimeFrame.Zero),
                registry);
            RuntimeReplayFrameRecord record = recorder.RecordFrame(
                RuntimeFrame.Zero,
                new[] { derived[0], occupancy[0], scalar[0] },
                resultHash: 1L);
            Assert.AreEqual(2, record.Commands.Count);
            Assert.AreEqual(GameplayTriggerRuntimeCommandIds.SubmitOccupancyBatch,
                record.Commands[0].CommandId);
            Assert.AreEqual(GameplayTriggerRuntimeCommandIds.SubmitScalarBatch,
                record.Commands[1].CommandId);
        }

        [Test]
        public void ExogenousReplayStream_DeepCopiesObservationRecordsBeforeCallerMutation()
        {
            const int externalSource = 21;
            var registry = new RuntimeCommandSourceRegistry(initialCapacity: 1);
            Assert.IsTrue(registry.TryRegister(
                externalSource,
                RuntimeCommandSourceKind.Exogenous,
                new[]
                {
                    GameplayTriggerRuntimeCommandIds.SubmitOccupancyBatch,
                    GameplayTriggerRuntimeCommandIds.SubmitScalarBatch
                },
                out _));
            var commands = new RuntimeCommand[2];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, externalSource, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L, new[] { new GameplayEntityId(1, 2) },
                commands.AsSpan(0, 1), out _, out _));
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, externalSource, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 2L,
                new[] { new GameplayTriggerScalarSample(default, Fix64.MinValue) },
                commands.AsSpan(1, 1), out _, out _));
            RuntimeCommand expectedOccupancy = commands[0];
            RuntimeCommand expectedScalar = commands[1];
            RuntimeReplayRecorder recorder = RuntimeReplayRecorder.CreateExogenousStream(
                new RuntimeReplayHeader(1, "0.1", "config", "resources", RuntimeFrame.Zero),
                registry);

            RuntimeReplayFrameRecord record = recorder.RecordFrame(
                RuntimeFrame.Zero,
                commands,
                resultHash: 1L);
            commands[0] = default;
            commands[1] = default;
            RuntimeReplaySnapshot snapshot = recorder.CreateSnapshot();

            Assert.AreEqual(expectedOccupancy, record.Commands[0]);
            Assert.AreEqual(expectedScalar, record.Commands[1]);
            Assert.AreEqual(expectedOccupancy, snapshot.Records[0].Commands[0]);
            Assert.AreEqual(expectedScalar, snapshot.Records[0].Commands[1]);
        }

        [Test]
        public void ObservationEnvelopes_SurviveBufferSnapshotReplayAndCallerMutation()
        {
            var commands = new RuntimeCommand[2];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L, new[] { new GameplayEntityId(1, 2) },
                commands.AsSpan(0, 1), out _, out _));
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 2L,
                new[] { new GameplayTriggerScalarSample(default, Fix64.MinValue) },
                commands.AsSpan(1, 1), out _, out _));
            RuntimeCommand occupancy = commands[0];
            RuntimeCommand scalar = commands[1];
            var buffer = new RuntimeCommandBuffer(initialCapacity: 2);
            Assert.IsTrue(buffer.Enqueue(occupancy).Success);
            Assert.IsTrue(buffer.Enqueue(scalar).Success);
            RuntimeCommandBufferSnapshot snapshot = buffer.CaptureSnapshot();
            var replay = new RuntimeReplayFrameRecord(
                RuntimeFrame.Zero,
                commands,
                resultHash: 1L,
                diagnosticsSummary: string.Empty);

            commands[0] = default;
            commands[1] = default;

            Assert.AreEqual(occupancy.Envelope, snapshot.PendingCommands[0].Envelope);
            Assert.AreEqual(scalar.Envelope, snapshot.PendingCommands[1].Envelope);
            Assert.AreEqual(occupancy.Envelope, replay.Commands[0].Envelope);
            Assert.AreEqual(scalar.Envelope, replay.Commands[1].Envelope);
        }

        [Test]
        public void ObservationCodecs_DoNotAllocateAfterWarmup()
        {
            var subjects = new[] { new GameplayEntityId(1, 1), new GameplayEntityId(2, 1) };
            var samples = new[]
            {
                new GameplayTriggerScalarSample(default, Fix64.MinValue),
                new GameplayTriggerScalarSample(new GameplayEntityId(2, 1), Fix64.MaxValue)
            };
            var occupancy = new RuntimeCommand[2];
            var scalar = new RuntimeCommand[2];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L, subjects, occupancy, out _, out _));
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 2L, samples, scalar, out _, out _));
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryDecodeRecord(
                occupancy[0], out _, out _));
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryDecodeRecord(
                scalar[0], out _, out _));
            _ = GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                    RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                    SourceBindingId, 1L, subjects, occupancy, out _, out _);
                GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                    RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                    SourceBindingId, 2L, samples, scalar, out _, out _);
                GameplayTriggerOccupancyCommandCodec.TryDecodeRecord(
                    occupancy[1], out _, out _);
                GameplayTriggerScalarCommandCodec.TryDecodeRecord(
                    scalar[1], out _, out _);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void ObservationCommandSystems_SuccessAndOverflowPathsDoNotAllocateAfterWarmup()
        {
            GameplayTriggerRuntime occupancyRuntime = CreateRuntime(GameplayTriggerSourceKind.Occupancy);
            var occupancy = new RuntimeCommand[2];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 1L,
                new[] { new GameplayEntityId(1, 1), new GameplayEntityId(2, 1) },
                occupancy, out _, out _));
            var successSystem = new GameplayTriggerCommandSystem(
                occupancyRuntime,
                "success",
                GameplayTriggerCommandSystem.DefaultPriority,
                1,
                2,
                2);
            var successState = new GameplayCommandExecutionState();
            GameplayComponentWorld successWorld = new GameplayComponentWorld();
            GameplaySystemContext successContext = CreateContext(
                RuntimeFrame.Zero, occupancy, successState, successWorld);
            successSystem.Tick(successContext);
            occupancyRuntime.Evaluate(RuntimeFrame.Zero);

            GameplayTriggerRuntime scalarRuntime = CreateRuntime(GameplayTriggerSourceKind.Threshold);
            var scalar = new RuntimeCommand[3];
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero, 1, RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId, 2L,
                new[]
                {
                    new GameplayTriggerScalarSample(new GameplayEntityId(1, 1), Fix64.One),
                    new GameplayTriggerScalarSample(new GameplayEntityId(2, 1), Fix64.One),
                    new GameplayTriggerScalarSample(new GameplayEntityId(3, 1), Fix64.One)
                },
                scalar, out _, out _));
            var overflowSystem = new GameplayTriggerCommandSystem(
                scalarRuntime,
                "overflow",
                GameplayTriggerCommandSystem.DefaultPriority,
                1,
                2,
                2);
            var overflowState = new GameplayCommandExecutionState();
            GameplayComponentWorld overflowWorld = new GameplayComponentWorld();
            GameplaySystemContext overflowContext = CreateContext(
                RuntimeFrame.Zero, scalar, overflowState, overflowWorld);
            overflowSystem.Tick(overflowContext);
            _ = GC.GetAllocatedBytesForCurrentThread();

            long successAllocated = 0L;
            long overflowAllocated = 0L;
            for (int i = 1; i <= 64; i++)
            {
                successState.Clear();
                successContext = CreateContext(
                    new RuntimeFrame(i), occupancy, successState, successWorld);
                long beforeSuccess = GC.GetAllocatedBytesForCurrentThread();
                successSystem.Tick(successContext);
                occupancyRuntime.Evaluate(new RuntimeFrame(i));
                successAllocated += GC.GetAllocatedBytesForCurrentThread() - beforeSuccess;

                overflowState.Clear();
                long beforeOverflow = GC.GetAllocatedBytesForCurrentThread();
                overflowSystem.Tick(overflowContext);
                overflowAllocated += GC.GetAllocatedBytesForCurrentThread() - beforeOverflow;
            }

            Assert.AreEqual(0L, successAllocated);
            Assert.AreEqual(0L, overflowAllocated);
            Assert.IsTrue(successSystem.LastResult.Result.Success);
            Assert.AreEqual(GameplayTriggerErrorCode.InputCapacityExceeded,
                overflowSystem.LastResult.Result.ErrorCode);
        }

        private static GameplayTriggerCommandSystem RunCommandSystem(
            GameplayTriggerRuntime runtime,
            IReadOnlyList<RuntimeCommand> commands,
            RuntimeFrame frame,
            int maxOccupancyItems,
            int maxScalarItems)
        {
            var system = new GameplayTriggerCommandSystem(
                runtime,
                "test.observation.command",
                GameplayTriggerCommandSystem.DefaultPriority,
                maxSignalCommandItems: 1,
                maxOccupancyCommandItems: maxOccupancyItems,
                maxScalarCommandItems: maxScalarItems);
            var state = new GameplayCommandExecutionState();
            var componentWorld = new GameplayComponentWorld();
            system.Tick(CreateContext(frame, commands, state, componentWorld));
            return system;
        }

        private static GameplaySystemContext CreateContext(
            RuntimeFrame frame,
            IReadOnlyList<RuntimeCommand> commands,
            GameplayCommandExecutionState commandState,
            GameplayComponentWorld componentWorld)
        {
            return new GameplaySystemContext(
                frame,
                0d,
                0d,
                new GameplayWorld(),
                commands,
                componentWorld.Events,
                commandState,
                componentWorld);
        }

        private static GameplayTriggerRuntime CreateRuntime(
            GameplayTriggerSourceKind sourceKind)
        {
            GameplayTriggerSourceDefinition source = sourceKind == GameplayTriggerSourceKind.Threshold
                ? new GameplayTriggerSourceDefinition(
                    SourceDefinitionId,
                    sourceKind,
                    thresholdDirection: GameplayTriggerThresholdDirection.High,
                    enterThreshold: Fix64.FromRaw(1L),
                    exitThreshold: Fix64.Zero,
                    initialSamplePolicy: GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive)
                : new GameplayTriggerSourceDefinition(SourceDefinitionId, sourceKind);
            var action = new GameplayTriggerActionInstruction(
                ActionKindId,
                GameplayTriggerTargetSelector.Owner);
            var route = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                GameplayTriggerOccurrenceKind.Entered |
                GameplayTriggerOccurrenceKind.Stayed |
                GameplayTriggerOccurrenceKind.Exited,
                GameplayTriggerConditionProgram.Always,
                new[] { action });
            var definition = new GameplayTriggerDefinition(
                DefinitionId,
                1,
                SourceDefinitionId,
                new[] { route });
            var binding = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(1),
                DefinitionId,
                SourceBindingId,
                new GameplayEntityId(10, 1));
            var sink = new CountingSink();
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[] { binding },
                new IGameplayTriggerActionSink[] { sink },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(
                    8,
                    16,
                    16,
                    16,
                    16,
                    16,
                    16,
                    16,
                    8,
                    8));
        }

        private static RuntimeCommand WithEnvelope(
            RuntimeCommand command,
            RuntimeCommandEnvelope envelope)
        {
            return new RuntimeCommand(
                command.Frame,
                command.SourceId,
                command.CommandId,
                command.TargetId,
                command.Payload0,
                command.Payload1,
                command.Payload2,
                command.TraceId,
                command.Sequence,
                envelope);
        }

        private static RuntimeCommand WithCommandId(
            RuntimeCommand command,
            int commandId)
        {
            return new RuntimeCommand(
                command.Frame,
                command.SourceId,
                commandId,
                command.TargetId,
                command.Payload0,
                command.Payload1,
                command.Payload2,
                command.TraceId,
                command.Sequence,
                command.Envelope);
        }

        private static RuntimeCommand WithPayload(
            RuntimeCommand command,
            int? payload0 = null,
            int? payload1 = null,
            int? payload2 = null)
        {
            return new RuntimeCommand(
                command.Frame,
                command.SourceId,
                command.CommandId,
                command.TargetId,
                payload0 ?? command.Payload0,
                payload1 ?? command.Payload1,
                payload2 ?? command.Payload2,
                command.TraceId,
                command.Sequence,
                command.Envelope);
        }

        private static RuntimeCommandEnvelope CopyEnvelope(
            RuntimeCommandEnvelope envelope,
            int? typeId = null,
            int? schemaVersion = null,
            RuntimeCommandProvenanceKind? provenance = null,
            long? data1 = null,
            long? data3 = null)
        {
            return new RuntimeCommandEnvelope(
                typeId ?? envelope.TypeId,
                schemaVersion ?? envelope.SchemaVersion,
                provenance ?? envelope.Provenance,
                envelope.Data0,
                data1 ?? envelope.Data1,
                envelope.Data2,
                data3 ?? envelope.Data3,
                envelope.Data4,
                envelope.Data5,
                envelope.Data6,
                envelope.Data7);
        }

        private static void AssertEnvelopeDataIsZero(
            RuntimeCommandEnvelope envelope,
            int firstIndex)
        {
            for (int i = firstIndex; i < 8; i++)
            {
                long value = i == 0 ? envelope.Data0
                    : i == 1 ? envelope.Data1
                    : i == 2 ? envelope.Data2
                    : i == 3 ? envelope.Data3
                    : i == 4 ? envelope.Data4
                    : i == 5 ? envelope.Data5
                    : i == 6 ? envelope.Data6
                    : envelope.Data7;
                Assert.AreEqual(0L, value, "Envelope Data" + i + " must be reserved zero.");
            }
        }

        private sealed class CountingSink : IGameplayTriggerActionSink
        {
            private int _nextToken;

            public GameplayTriggerActionKindId ActionKindId =>
                GameplayTriggerObservationCommandCodecTests.ActionKindId;
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                token = ++_nextToken;
                return true;
            }

            public void Commit(int token, in GameplayTriggerActionIntent intent)
            {
            }

            public void Abort(int token)
            {
            }
        }
    }
}
