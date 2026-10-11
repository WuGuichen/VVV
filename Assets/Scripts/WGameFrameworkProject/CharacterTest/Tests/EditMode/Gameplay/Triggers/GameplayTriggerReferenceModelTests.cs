using System;
using System.Collections.Generic;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerReferenceModelTests
    {
        private static readonly GameplayTriggerSourceBindingId SourceBindingId = new GameplayTriggerSourceBindingId(100);
        private static readonly GameplayTriggerActionKindId ActionKind = new GameplayTriggerActionKindId(1000);
        private const int PermutationSeed = 670031;

        [Test]
        public void Occupancy_FixedSeedPermutationsMatchIndependentReferenceTrace()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new GameplayTriggerSourceDefinition(new GameplayTriggerSourceDefinitionId(1), GameplayTriggerSourceKind.Occupancy),
                GameplayTriggerOccurrenceKind.Entered | GameplayTriggerOccurrenceKind.Stayed | GameplayTriggerOccurrenceKind.Exited);
            var reference = new GameplayTriggerReferenceModel(runtime.GetInstanceId(0));
            var random = new Random(PermutationSeed);
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);
            var c = new GameplayEntityId(3, 1);
            GameplayEntityId[][] canonicalFrames =
            {
                new[] { a, b },
                new[] { a, b, c },
                new[] { b, c },
                new[] { c },
                Array.Empty<GameplayEntityId>()
            };

            for (int frame = 0; frame < canonicalFrames.Length; frame++)
            {
                GameplayEntityId[] submitted = AddDuplicatesAndShuffle(canonicalFrames[frame], random);
                reference.ReduceOccupancy(frame, submitted);
                Assert.IsTrue(runtime.TrySubmitOccupancy(new RuntimeFrame(frame), SourceBindingId, submitted, out GameplayTriggerResult submit), Failure(PermutationSeed, frame, submit));
                Assert.IsTrue(runtime.Evaluate(new RuntimeFrame(frame)).Success, "seed=" + PermutationSeed + " frame=" + frame);
                AssertOccurrences(reference.Occurrences, runtime, PermutationSeed, frame);
            }
        }

        [Test]
        public void Occupancy_HistoricalInsertionOrderDoesNotAffectCanonicalExitOrder()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new GameplayTriggerSourceDefinition(new GameplayTriggerSourceDefinitionId(1), GameplayTriggerSourceKind.Occupancy),
                GameplayTriggerOccurrenceKind.Entered | GameplayTriggerOccurrenceKind.Stayed | GameplayTriggerOccurrenceKind.Exited);
            var reference = new GameplayTriggerReferenceModel(runtime.GetInstanceId(0));
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);
            GameplayEntityId[][] frames =
            {
                new[] { b },
                new[] { a, b },
                Array.Empty<GameplayEntityId>()
            };

            for (int frame = 0; frame < frames.Length; frame++)
            {
                reference.ReduceOccupancy(frame, frames[frame]);
                Assert.IsTrue(runtime.TrySubmitOccupancy(new RuntimeFrame(frame), SourceBindingId, frames[frame], out _));
                Assert.IsTrue(runtime.Evaluate(new RuntimeFrame(frame)).Success);
                AssertOccurrences(reference.Occurrences, runtime, 0, frame);
            }

            Assert.AreEqual(a, runtime.GetOccurrence(0).Subject);
            Assert.AreEqual(b, runtime.GetOccurrence(1).Subject);
        }

        [TestCase(GameplayTriggerThresholdDirection.High, GameplayTriggerInitialSamplePolicy.PrimeSilently, GameplayTriggerSubjectDeparturePolicy.EmitExited)]
        [TestCase(GameplayTriggerThresholdDirection.High, GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive, GameplayTriggerSubjectDeparturePolicy.RemoveSilently)]
        [TestCase(GameplayTriggerThresholdDirection.Low, GameplayTriggerInitialSamplePolicy.PrimeSilently, GameplayTriggerSubjectDeparturePolicy.RemoveSilently)]
        [TestCase(GameplayTriggerThresholdDirection.Low, GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive, GameplayTriggerSubjectDeparturePolicy.EmitExited)]
        public void Threshold_RawBoundaryMatrixMatchesIndependentReference(
            GameplayTriggerThresholdDirection direction,
            GameplayTriggerInitialSamplePolicy initialPolicy,
            GameplayTriggerSubjectDeparturePolicy departurePolicy)
        {
            Fix64 enter = direction == GameplayTriggerThresholdDirection.High ? Fix64.FromInt(10) : Fix64.FromInt(8);
            Fix64 exit = direction == GameplayTriggerThresholdDirection.High ? Fix64.FromInt(8) : Fix64.FromInt(10);
            long enterOutsideRaw = direction == GameplayTriggerThresholdDirection.High ? enter.RawValue - 1L : enter.RawValue + 1L;
            long enterInsideRaw = direction == GameplayTriggerThresholdDirection.High ? enter.RawValue : enter.RawValue;
            long exitInsideRaw = direction == GameplayTriggerThresholdDirection.High ? exit.RawValue : exit.RawValue;
            long exitOutsideRaw = direction == GameplayTriggerThresholdDirection.High ? exit.RawValue - 1L : exit.RawValue + 1L;
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Threshold,
                thresholdDirection: direction,
                enterThreshold: enter,
                exitThreshold: exit,
                initialSamplePolicy: initialPolicy,
                departurePolicy: departurePolicy);
            GameplayTriggerRuntime runtime = CreateRuntime(
                source,
                GameplayTriggerOccurrenceKind.Entered | GameplayTriggerOccurrenceKind.Stayed | GameplayTriggerOccurrenceKind.Exited);
            var reference = new GameplayTriggerReferenceModel(runtime.GetInstanceId(0));
            var subject = new GameplayEntityId(1, 1);
            GameplayTriggerScalarSample[][] frames =
            {
                new[] { new GameplayTriggerScalarSample(subject, Fix64.FromRaw(enterOutsideRaw)) },
                new[] { new GameplayTriggerScalarSample(subject, Fix64.FromRaw(enterInsideRaw)) },
                new[] { new GameplayTriggerScalarSample(subject, Fix64.FromRaw(exitInsideRaw)) },
                new[] { new GameplayTriggerScalarSample(subject, Fix64.FromRaw(exitOutsideRaw)) },
                new[] { new GameplayTriggerScalarSample(subject, Fix64.FromRaw(enterInsideRaw)) },
                Array.Empty<GameplayTriggerScalarSample>()
            };

            for (int frame = 0; frame < frames.Length; frame++)
            {
                reference.ReduceThreshold(
                    frame,
                    frames[frame],
                    direction,
                    enter,
                    exit,
                    initialPolicy,
                    departurePolicy);
                Assert.IsTrue(runtime.TrySubmitScalarBatch(new RuntimeFrame(frame), SourceBindingId, frames[frame], out GameplayTriggerResult submit), Failure(0, frame, submit));
                Assert.IsTrue(runtime.Evaluate(new RuntimeFrame(frame)).Success);
                AssertOccurrences(reference.Occurrences, runtime, 0, frame);
            }
        }

        [TestCase(GameplayTriggerThresholdDirection.High, GameplayTriggerInitialSamplePolicy.PrimeSilently, 0)]
        [TestCase(GameplayTriggerThresholdDirection.High, GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive, 1)]
        [TestCase(GameplayTriggerThresholdDirection.Low, GameplayTriggerInitialSamplePolicy.PrimeSilently, 0)]
        [TestCase(GameplayTriggerThresholdDirection.Low, GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive, 1)]
        public void Threshold_InitialActiveSamplePolicyControlsFirstEntered(
            GameplayTriggerThresholdDirection direction,
            GameplayTriggerInitialSamplePolicy initialPolicy,
            int expectedCount)
        {
            Fix64 enter = direction == GameplayTriggerThresholdDirection.High ? Fix64.FromInt(10) : Fix64.FromInt(8);
            Fix64 exit = direction == GameplayTriggerThresholdDirection.High ? Fix64.FromInt(8) : Fix64.FromInt(10);
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Threshold,
                thresholdDirection: direction,
                enterThreshold: enter,
                exitThreshold: exit,
                initialSamplePolicy: initialPolicy);
            GameplayTriggerRuntime runtime = CreateRuntime(source, GameplayTriggerOccurrenceKind.Entered);
            var reference = new GameplayTriggerReferenceModel(runtime.GetInstanceId(0));
            var samples = new[] { new GameplayTriggerScalarSample(new GameplayEntityId(1, 1), enter) };

            reference.ReduceThreshold(0, samples, direction, enter, exit, initialPolicy, GameplayTriggerSubjectDeparturePolicy.RemoveSilently);
            Assert.IsTrue(runtime.TrySubmitScalarBatch(RuntimeFrame.Zero, SourceBindingId, samples, out _));
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(expectedCount, runtime.OccurrenceCount);
            AssertOccurrences(reference.Occurrences, runtime, 0, 0);
        }

        [Test]
        public void UnorderedSignals_AllFixedSeedPermutationsMatchIndependentReferenceAndKeepDuplicates()
        {
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal,
                signalOrdering: GameplayTriggerSignalOrdering.UnorderedMultiset);
            var subject = new GameplayEntityId(7, 1);
            var instigator = new GameplayEntityId(9, 1);
            GameplayTriggerSignal[] canonical =
            {
                new GameplayTriggerSignal(3, subject, instigator, payload0: 20, payload1: 2),
                new GameplayTriggerSignal(2, subject, payload0: 10),
                new GameplayTriggerSignal(3, subject, instigator, payload0: 20, payload1: 2),
                new GameplayTriggerSignal(3, subject, payload0: 5)
            };
            var random = new Random(PermutationSeed);

            for (int iteration = 0; iteration < 32; iteration++)
            {
                GameplayTriggerSignal[] submitted = (GameplayTriggerSignal[])canonical.Clone();
                Shuffle(submitted, random);
                GameplayTriggerRuntime runtime = CreateRuntime(source, GameplayTriggerOccurrenceKind.Pulsed);
                var reference = new GameplayTriggerReferenceModel(runtime.GetInstanceId(0));
                reference.ReduceUnorderedSignals(0, submitted);

                Assert.IsTrue(runtime.TrySubmitSignals(RuntimeFrame.Zero, SourceBindingId, GameplayTriggerSignalOrdering.UnorderedMultiset, submitted, out GameplayTriggerResult submit), Failure(PermutationSeed, iteration, submit));
                Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
                AssertOccurrences(reference.Occurrences, runtime, PermutationSeed, iteration);
                Assert.AreEqual(4, runtime.OccurrenceCount, "duplicate multiplicity lost; seed=" + PermutationSeed + " iteration=" + iteration);
            }
        }

        private static GameplayTriggerRuntime CreateRuntime(
            GameplayTriggerSourceDefinition source,
            GameplayTriggerOccurrenceKind occurrenceMask)
        {
            var route = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                occurrenceMask,
                GameplayTriggerConditionProgram.Always,
                new[] { new GameplayTriggerActionInstruction(ActionKind, GameplayTriggerTargetSelector.Owner) });
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[]
                {
                    new GameplayTriggerDefinition(
                        new GameplayTriggerDefinitionId(1),
                        1,
                        source.SourceDefinitionId,
                        new[] { route })
                },
                new[]
                {
                    new GameplayTriggerBinding(
                        new GameplayTriggerBindingId(1),
                        new GameplayTriggerDefinitionId(1),
                        SourceBindingId,
                        new GameplayEntityId(99, 1))
                },
                new IGameplayTriggerActionSink[] { new ReferenceSink() },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(4, 16, 16, 16, 32, 16, 16, 16, 4, 8));
        }

        private static GameplayEntityId[] AddDuplicatesAndShuffle(GameplayEntityId[] canonical, Random random)
        {
            var values = new List<GameplayEntityId>(canonical.Length * 2);
            for (int i = 0; i < canonical.Length; i++)
            {
                values.Add(canonical[i]);
                if (random.Next(0, 2) == 0) values.Add(canonical[i]);
            }
            GameplayEntityId[] result = values.ToArray();
            Shuffle(result, random);
            return result;
        }

        private static void Shuffle<T>(T[] values, Random random)
        {
            for (int i = values.Length - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                T value = values[i];
                values[i] = values[other];
                values[other] = value;
            }
        }

        private static void AssertOccurrences(
            IReadOnlyList<GameplayTriggerReferenceOccurrence> expected,
            GameplayTriggerRuntime actual,
            int seed,
            int step)
        {
            Assert.AreEqual(expected.Count, actual.OccurrenceCount, "count mismatch; seed=" + seed + " step=" + step);
            for (int i = 0; i < expected.Count; i++)
            {
                GameplayTriggerReferenceOccurrence left = expected[i];
                GameplayTriggerOccurrence right = actual.GetOccurrence(i);
                string context = "seed=" + seed + " step=" + step + " occurrence=" + i;
                Assert.AreEqual(left.Frame, right.Frame.Value, context);
                Assert.AreEqual(left.InstanceId, right.InstanceId, context);
                Assert.AreEqual(left.Kind, right.Kind, context);
                Assert.AreEqual(left.Subject, right.Subject, context);
                Assert.AreEqual(left.Instigator, right.Instigator, context);
                Assert.AreEqual(left.SignalId, right.SignalId, context);
                Assert.AreEqual(left.PreviousScalar, right.PreviousScalar, context);
                Assert.AreEqual(left.CurrentScalar, right.CurrentScalar, context);
                Assert.AreEqual(left.Payload0, right.Payload0, context);
                Assert.AreEqual(left.Payload1, right.Payload1, context);
                Assert.AreEqual(left.Payload2, right.Payload2, context);
                Assert.AreEqual(left.OrderingValue, right.OrderingValue, context);
                Assert.AreEqual(left.PresenceEpoch, right.PresenceEpoch, context);
                Assert.AreEqual(left.PresenceStartFrame, right.PresenceStartFrame, context);
            }
        }

        private static string Failure(int seed, int step, GameplayTriggerResult result) =>
            "seed=" + seed + " step=" + step + " error=" + result.ErrorCode + " details=" + result.Detail0 + "," + result.Detail1;

        private sealed class ReferenceSink : IGameplayTriggerActionSink
        {
            public GameplayTriggerActionKindId ActionKindId => ActionKind;
            public GameplayTriggerDeliveryContract DeliveryContract => GameplayTriggerDeliveryContract.SameFrameReserved;
            public bool TryPrepare(in GameplayTriggerActionPreparation preparation, out int token)
            {
                token = preparation.ActionIndex + 1;
                return true;
            }
            public void Abort(int token) { }
            public void Commit(int token, in GameplayTriggerActionIntent intent) { }
        }
    }
}
