using System;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerCapacityPerformanceTests
    {
        private static readonly GameplayTriggerSourceDefinitionId SourceDefinitionId =
            new GameplayTriggerSourceDefinitionId(1);
        private static readonly GameplayTriggerDefinitionId DefinitionId =
            new GameplayTriggerDefinitionId(1);
        private static readonly GameplayTriggerSourceBindingId SourceBindingId =
            new GameplayTriggerSourceBindingId(1);
        private static readonly GameplayTriggerActionKindId ActionKindId =
            new GameplayTriggerActionKindId(1);
        private const int BoundaryCapacity = 2;

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        public void RuntimeOptions_InvalidMinimumIsRejected(int capacityIndex)
        {
            var values = new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            values[capacityIndex] = 0;

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new GameplayTriggerRuntimeOptions(
                    values[0],
                    values[1],
                    values[2],
                    values[3],
                    values[4],
                    values[5],
                    values[6],
                    values[7],
                    values[8],
                    values[9]));
        }

        [TestCase(GameplayTriggerSourceKind.Occupancy, 1)]
        [TestCase(GameplayTriggerSourceKind.Occupancy, 2)]
        [TestCase(GameplayTriggerSourceKind.Occupancy, 3)]
        [TestCase(GameplayTriggerSourceKind.Threshold, 1)]
        [TestCase(GameplayTriggerSourceKind.Threshold, 2)]
        [TestCase(GameplayTriggerSourceKind.Threshold, 3)]
        [TestCase(GameplayTriggerSourceKind.Signal, 1)]
        [TestCase(GameplayTriggerSourceKind.Signal, 2)]
        [TestCase(GameplayTriggerSourceKind.Signal, 3)]
        public void InputItemCapacity_NMinusOne_N_NPlusOne(
            GameplayTriggerSourceKind sourceKind,
            int load)
        {
            GameplayTriggerRuntime runtime = RuntimeForSource(
                sourceKind,
                Options(maxInputItems: BoundaryCapacity));
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);
            var c = new GameplayEntityId(3, 1);

            bool accepted;
            GameplayTriggerResult result;
            switch (sourceKind)
            {
                case GameplayTriggerSourceKind.Occupancy:
                    accepted = runtime.TrySubmitOccupancy(
                        RuntimeFrame.Zero,
                        SourceBindingId,
                        Subjects(load, a, b, c),
                        out result);
                    break;
                case GameplayTriggerSourceKind.Threshold:
                    accepted = runtime.TrySubmitScalarBatch(
                        RuntimeFrame.Zero,
                        SourceBindingId,
                        Scalars(load, a, b, c),
                        out result);
                    break;
                default:
                    accepted = runtime.TrySubmitSignals(
                        RuntimeFrame.Zero,
                        SourceBindingId,
                        GameplayTriggerSignalOrdering.UnorderedMultiset,
                        Signals(load),
                        out result);
                    break;
            }

            Assert.AreEqual(load <= BoundaryCapacity, accepted);
            Assert.AreEqual(
                load <= BoundaryCapacity
                    ? GameplayTriggerErrorCode.None
                    : GameplayTriggerErrorCode.InputCapacityExceeded,
                result.ErrorCode);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void InputBatchCapacity_NMinusOne_N_NPlusOne(int load)
        {
            GameplayTriggerRuntime runtime = MultiBindingSignalRuntime(
                bindingCount: 3,
                Options(maxInputBatches: BoundaryCapacity));
            var signal = new[] { new GameplayTriggerSignal(1) };
            int accepted = 0;
            GameplayTriggerResult last = default;
            for (int i = 0; i < load; i++)
            {
                if (runtime.TrySubmitSignals(
                        RuntimeFrame.Zero,
                        new GameplayTriggerSourceBindingId(i + 1),
                        GameplayTriggerSignalOrdering.UnorderedMultiset,
                        signal,
                        out last))
                {
                    accepted++;
                }
            }

            Assert.AreEqual(Math.Min(load, BoundaryCapacity), accepted);
            Assert.AreEqual(
                load <= BoundaryCapacity
                    ? GameplayTriggerErrorCode.None
                    : GameplayTriggerErrorCode.InputCapacityExceeded,
                last.ErrorCode);
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(accepted, runtime.OccurrenceCount);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void RelationCapacity_NMinusOne_N_NPlusOne(int load)
        {
            GameplayTriggerRuntime runtime = RuntimeForSource(
                GameplayTriggerSourceKind.Occupancy,
                Options(maxRelations: BoundaryCapacity));
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);
            var c = new GameplayEntityId(3, 1);

            Assert.IsTrue(runtime.TrySubmitOccupancy(
                RuntimeFrame.Zero,
                SourceBindingId,
                Subjects(load, a, b, c),
                out _));
            GameplayTriggerResult result = runtime.Evaluate(RuntimeFrame.Zero);

            Assert.AreEqual(load <= BoundaryCapacity, result.Success);
            Assert.AreEqual(
                load <= BoundaryCapacity
                    ? GameplayTriggerErrorCode.None
                    : GameplayTriggerErrorCode.InputCapacityExceeded,
                result.ErrorCode);
            Assert.AreEqual(load <= BoundaryCapacity ? load : 0, runtime.RelationCount);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void OccurrenceCapacity_NMinusOne_N_NPlusOne(int load)
        {
            GameplayTriggerRuntime runtime = SingleRuntime(
                SignalSource(),
                Route(GameplayTriggerOccurrenceKind.Pulsed),
                new CountingSink(ActionKindId),
                Options(maxOccurrences: BoundaryCapacity));

            GameplayTriggerResult result = SubmitSignalsForBoundary(runtime, load);

            Assert.AreEqual(load <= BoundaryCapacity, result.Success);
            Assert.AreEqual(load <= BoundaryCapacity ? load : 0, runtime.OccurrenceCount);
            AssertBoundaryEvaluationError(result, load, GameplayTriggerErrorCode.OccurrenceCapacityExceeded);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void CandidateCapacity_NMinusOne_N_NPlusOne(int load)
        {
            GameplayTriggerRuntime runtime = SingleRuntime(
                SignalSource(),
                Route(GameplayTriggerOccurrenceKind.Pulsed),
                new CountingSink(ActionKindId),
                Options(maxOccurrences: 4, maxCandidates: BoundaryCapacity));

            GameplayTriggerResult result = SubmitSignalsForBoundary(runtime, load);

            Assert.AreEqual(load <= BoundaryCapacity, result.Success);
            Assert.AreEqual(load <= BoundaryCapacity ? load : 0, runtime.OccurrenceCount);
            AssertBoundaryEvaluationError(result, load, GameplayTriggerErrorCode.CandidateCapacityExceeded);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void RouteStateCapacity_NMinusOne_N_NPlusOne(int load)
        {
            var policy = new GameplayTriggerActivationPolicy(
                GameplayTriggerStateScope.PerSubject);
            GameplayTriggerRuntime runtime = SingleRuntime(
                OccupancySource(),
                Route(GameplayTriggerOccurrenceKind.Entered, policy: policy),
                new CountingSink(ActionKindId),
                Options(maxRouteStates: BoundaryCapacity));
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);
            var c = new GameplayEntityId(3, 1);

            SubmitOccupancy(runtime, 0, Subjects(load, a, b, c));

            int accepted = Math.Min(load, BoundaryCapacity);
            Assert.AreEqual(load, runtime.OccurrenceCount);
            Assert.AreEqual(accepted, runtime.ActivationCount);
            Assert.AreEqual(accepted, runtime.RouteStateCount);
            AssertBoundaryDiagnostic(runtime, load, GameplayTriggerErrorCode.RouteStateCapacityExceeded);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ActivationCapacity_NMinusOne_N_NPlusOne(int load)
        {
            GameplayTriggerRuntime runtime = SingleRuntime(
                SignalSource(),
                Route(
                    GameplayTriggerOccurrenceKind.Pulsed,
                    policy: new GameplayTriggerActivationPolicy(
                        GameplayTriggerStateScope.PerSubject)),
                new CountingSink(ActionKindId),
                Options(maxActivations: BoundaryCapacity));

            SubmitSignals(runtime, 0, Signals(load));

            int accepted = Math.Min(load, BoundaryCapacity);
            Assert.AreEqual(load, runtime.OccurrenceCount);
            Assert.AreEqual(accepted, runtime.ActivationCount);
            Assert.AreEqual(accepted, runtime.RouteStateCount);
            AssertBoundaryDiagnostic(runtime, load, GameplayTriggerErrorCode.IntentCapacityExceeded);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void IntentCapacity_NMinusOne_N_NPlusOne(int load)
        {
            var sink = new CountingSink(ActionKindId);
            GameplayTriggerRuntime runtime = SingleRuntime(
                SignalSource(),
                Route(GameplayTriggerOccurrenceKind.Pulsed, actionCount: load),
                sink,
                Options(maxIntents: BoundaryCapacity, maxPreparedActions: 4));

            SubmitSignals(runtime, 0, new[] { new GameplayTriggerSignal(1) });

            Assert.AreEqual(1, runtime.OccurrenceCount);
            Assert.AreEqual(load <= BoundaryCapacity ? 1 : 0, runtime.ActivationCount);
            Assert.AreEqual(load <= BoundaryCapacity ? load : 0, runtime.IntentCount);
            Assert.AreEqual(load <= BoundaryCapacity ? load : 0, sink.PrepareCount);
            AssertBoundaryDiagnostic(runtime, load, GameplayTriggerErrorCode.IntentCapacityExceeded);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void PreparedActionCapacity_NMinusOne_N_NPlusOne(int load)
        {
            var sink = new CountingSink(ActionKindId);
            GameplayTriggerRuntime runtime = SingleRuntime(
                SignalSource(),
                Route(GameplayTriggerOccurrenceKind.Pulsed, actionCount: load),
                sink,
                Options(maxIntents: 4, maxPreparedActions: BoundaryCapacity));

            SubmitSignals(runtime, 0, new[] { new GameplayTriggerSignal(1) });

            Assert.AreEqual(1, runtime.OccurrenceCount);
            Assert.AreEqual(load <= BoundaryCapacity ? 1 : 0, runtime.ActivationCount);
            Assert.AreEqual(load <= BoundaryCapacity ? load : 0, runtime.IntentCount);
            Assert.AreEqual(load <= BoundaryCapacity ? load : 0, sink.PrepareCount);
            AssertBoundaryDiagnostic(runtime, load, GameplayTriggerErrorCode.IntentCapacityExceeded);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ConditionStackCapacity_NMinusOne_N_NPlusOne(int load)
        {
            GameplayTriggerConditionProgram condition = MaximumCondition(load);

            if (load > BoundaryCapacity)
            {
                Assert.Throws<ArgumentException>(() => SingleRuntime(
                    SignalSource(),
                    Route(GameplayTriggerOccurrenceKind.Pulsed, condition: condition),
                    new CountingSink(ActionKindId),
                    Options(maxConditionStackDepth: BoundaryCapacity)));
                return;
            }

            GameplayTriggerRuntime runtime = SingleRuntime(
                SignalSource(),
                Route(GameplayTriggerOccurrenceKind.Pulsed, condition: condition),
                new CountingSink(ActionKindId),
                Options(maxConditionStackDepth: BoundaryCapacity));
            SubmitSignals(runtime, 0, new[] { new GameplayTriggerSignal(1) });
            Assert.AreEqual(1, runtime.ActivationCount);
        }

        [Test]
        public void InputCapacityRejection_DoesNotAllocateAfterWarmup()
        {
            GameplayTriggerRuntime runtime = RuntimeForSource(
                GameplayTriggerSourceKind.Signal,
                Options(maxInputItems: 1));
            var overflow = new[] { new GameplayTriggerSignal(1), new GameplayTriggerSignal(2) };
            runtime.TrySubmitSignals(
                RuntimeFrame.Zero,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                overflow,
                out _);
            _ = GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            int rejected = 0;
            for (int i = 0; i < 128; i++)
            {
                if (!runtime.TrySubmitSignals(
                    RuntimeFrame.Zero,
                    SourceBindingId,
                    GameplayTriggerSignalOrdering.UnorderedMultiset,
                    overflow,
                    out _))
                {
                    rejected++;
                }
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(128, rejected);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void OccurrenceCapacityRejection_DoesNotAllocateAfterWarmup()
        {
            GameplayTriggerRuntime runtime = SingleRuntime(
                SignalSource(),
                Route(GameplayTriggerOccurrenceKind.Pulsed),
                new CountingSink(ActionKindId),
                Options(maxOccurrences: 1));
            var overflow = new[] { new GameplayTriggerSignal(1), new GameplayTriggerSignal(2) };
            SubmitExpectedEvaluationFailure(runtime, 0, overflow);
            _ = GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            int rejected = 0;
            for (int i = 1; i <= 64; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                if (runtime.TrySubmitSignals(
                        frame,
                        SourceBindingId,
                        GameplayTriggerSignalOrdering.UnorderedMultiset,
                        overflow,
                        out _) &&
                    !runtime.Evaluate(frame).Success)
                {
                    rejected++;
                }
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(64, rejected);
            Assert.AreEqual(0L, allocated);
        }

        [TestCase(OverflowPath.Relation)]
        [TestCase(OverflowPath.Candidate)]
        [TestCase(OverflowPath.RouteState)]
        [TestCase(OverflowPath.Activation)]
        [TestCase(OverflowPath.Intent)]
        [TestCase(OverflowPath.PreparedAction)]
        public void RemainingCapacityRejections_DoNotAllocateAfterWarmup(OverflowPath path)
        {
            GameplayTriggerRuntime runtime = OverflowRuntime(path);
            var occupancy = new[]
            {
                new GameplayEntityId(1, 1),
                new GameplayEntityId(2, 1),
                new GameplayEntityId(3, 1)
            };
            GameplayTriggerSignal[] signals = Signals(3);
            for (int i = 0; i < 32; i++)
            {
                RunOverflowPath(runtime, path, i, occupancy, signals);
            }
            _ = GC.GetAllocatedBytesForCurrentThread();

            int observed = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 32; i < 96; i++)
            {
                if (RunOverflowPath(runtime, path, i, occupancy, signals))
                {
                    observed++;
                }
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(64, observed);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void OccupancyEnterExit_DoesNotAllocateAfterWarmup()
        {
            var sink = new CountingSink(ActionKindId);
            GameplayTriggerRuntime runtime = SingleRuntime(
                OccupancySource(),
                Route(GameplayTriggerOccurrenceKind.Entered | GameplayTriggerOccurrenceKind.Exited),
                sink,
                Options());
            var subject = new GameplayEntityId(1, 1);
            var occupied = new[] { subject };
            var empty = Array.Empty<GameplayEntityId>();
            SubmitOccupancy(runtime, 0, occupied);
            SubmitOccupancy(runtime, 1, empty);
            _ = GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            int succeeded = 0;
            for (int i = 2; i < 66; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                GameplayEntityId[] subjects = (i & 1) == 0 ? occupied : empty;
                if (runtime.TrySubmitOccupancy(frame, SourceBindingId, subjects, out _) &&
                    runtime.Evaluate(frame).Success)
                {
                    succeeded++;
                }
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(64, succeeded);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void MaximumConditionDepth_DoesNotAllocateAfterWarmup()
        {
            GameplayTriggerConditionProgram condition = MaximumCondition(depth: 16);
            GameplayTriggerRuntime runtime = SingleRuntime(
                SignalSource(),
                Route(GameplayTriggerOccurrenceKind.Pulsed, condition: condition),
                new CountingSink(ActionKindId),
                Options(maxConditionStackDepth: 16));
            var signals = new[] { new GameplayTriggerSignal(1) };
            SubmitSignals(runtime, 0, signals);
            _ = GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            int succeeded = 0;
            for (int i = 1; i <= 64; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                if (runtime.TrySubmitSignals(
                        frame,
                        SourceBindingId,
                        GameplayTriggerSignalOrdering.UnorderedMultiset,
                        signals,
                        out _) &&
                    runtime.Evaluate(frame).Success)
                {
                    succeeded++;
                }
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(64, succeeded);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void LastActionPrepareFailure_DoesNotAllocateAfterWarmup()
        {
            var sink = new CountingSink(ActionKindId, rejectActionIndex: 2);
            GameplayTriggerRuntime runtime = SingleRuntime(
                SignalSource(),
                Route(GameplayTriggerOccurrenceKind.Pulsed, actionCount: 3),
                sink,
                Options(maxPreparedActions: 3));
            var signals = new[] { new GameplayTriggerSignal(1) };
            SubmitSignals(runtime, 0, signals);
            _ = GC.GetAllocatedBytesForCurrentThread();

            int prepareBefore = sink.PrepareCount;
            int abortBefore = sink.AbortCount;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 1; i <= 64; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                runtime.TrySubmitSignals(
                    frame,
                    SourceBindingId,
                    GameplayTriggerSignalOrdering.UnorderedMultiset,
                    signals,
                    out _);
                runtime.Evaluate(frame);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(64 * 3, sink.PrepareCount - prepareBefore);
            Assert.AreEqual(64 * 2, sink.AbortCount - abortBefore);
            Assert.AreEqual(0, runtime.ActivationCount);
            Assert.AreEqual(0L, allocated);
        }

        private static GameplayTriggerRuntime RuntimeForSource(
            GameplayTriggerSourceKind sourceKind,
            GameplayTriggerRuntimeOptions options)
        {
            GameplayTriggerSourceDefinition source;
            GameplayTriggerOccurrenceKind mask;
            switch (sourceKind)
            {
                case GameplayTriggerSourceKind.Occupancy:
                    source = OccupancySource();
                    mask = GameplayTriggerOccurrenceKind.Entered;
                    break;
                case GameplayTriggerSourceKind.Threshold:
                    source = new GameplayTriggerSourceDefinition(
                        SourceDefinitionId,
                        GameplayTriggerSourceKind.Threshold,
                        enterThreshold: default,
                        exitThreshold: default);
                    mask = GameplayTriggerOccurrenceKind.Entered;
                    break;
                default:
                    source = SignalSource();
                    mask = GameplayTriggerOccurrenceKind.Pulsed;
                    break;
            }
            return SingleRuntime(
                source,
                Route(mask),
                new CountingSink(ActionKindId),
                options);
        }

        private static GameplayTriggerRuntime OverflowRuntime(OverflowPath path)
        {
            switch (path)
            {
                case OverflowPath.Relation:
                    return SingleRuntime(
                        OccupancySource(),
                        Route(GameplayTriggerOccurrenceKind.Entered),
                        new CountingSink(ActionKindId),
                        Options(maxRelations: BoundaryCapacity));
                case OverflowPath.Candidate:
                    return SingleRuntime(
                        SignalSource(),
                        Route(GameplayTriggerOccurrenceKind.Pulsed),
                        new CountingSink(ActionKindId),
                        Options(maxOccurrences: 4, maxCandidates: BoundaryCapacity));
                case OverflowPath.RouteState:
                    return SingleRuntime(
                        OccupancySource(),
                        Route(
                            GameplayTriggerOccurrenceKind.Entered,
                            policy: new GameplayTriggerActivationPolicy(
                                GameplayTriggerStateScope.PerSubject)),
                        new CountingSink(ActionKindId),
                        Options(maxRouteStates: BoundaryCapacity));
                case OverflowPath.Activation:
                    return SingleRuntime(
                        SignalSource(),
                        Route(
                            GameplayTriggerOccurrenceKind.Pulsed,
                            policy: new GameplayTriggerActivationPolicy(
                                GameplayTriggerStateScope.PerSubject)),
                        new CountingSink(ActionKindId),
                        Options(maxActivations: BoundaryCapacity));
                case OverflowPath.Intent:
                    return SingleRuntime(
                        SignalSource(),
                        Route(GameplayTriggerOccurrenceKind.Pulsed, actionCount: 3),
                        new CountingSink(ActionKindId),
                        Options(maxIntents: BoundaryCapacity, maxPreparedActions: 4));
                default:
                    return SingleRuntime(
                        SignalSource(),
                        Route(GameplayTriggerOccurrenceKind.Pulsed, actionCount: 3),
                        new CountingSink(ActionKindId),
                        Options(maxIntents: 4, maxPreparedActions: BoundaryCapacity));
            }
        }

        private static bool RunOverflowPath(
            GameplayTriggerRuntime runtime,
            OverflowPath path,
            long frameValue,
            GameplayEntityId[] occupancy,
            GameplayTriggerSignal[] signals)
        {
            var frame = new RuntimeFrame(frameValue);
            if (path == OverflowPath.Relation || path == OverflowPath.RouteState)
            {
                if (path == OverflowPath.RouteState &&
                    !runtime.TryReset(runtime.GetInstanceId(0), out _))
                {
                    return false;
                }
                if (!runtime.TrySubmitOccupancy(frame, SourceBindingId, occupancy, out _))
                {
                    return false;
                }
            }
            else if (!runtime.TrySubmitSignals(
                         frame,
                         SourceBindingId,
                         GameplayTriggerSignalOrdering.UnorderedMultiset,
                         signals.AsSpan(
                             0,
                             path == OverflowPath.Intent || path == OverflowPath.PreparedAction
                                 ? 1
                                 : signals.Length),
                         out _))
            {
                return false;
            }

            GameplayTriggerResult result = runtime.Evaluate(frame);
            switch (path)
            {
                case OverflowPath.Relation:
                    return !result.Success &&
                           result.ErrorCode == GameplayTriggerErrorCode.InputCapacityExceeded;
                case OverflowPath.Candidate:
                    return !result.Success &&
                           result.ErrorCode == GameplayTriggerErrorCode.CandidateCapacityExceeded;
                case OverflowPath.RouteState:
                    return result.Success && HasDiagnostic(
                        runtime,
                        GameplayTriggerErrorCode.RouteStateCapacityExceeded);
                default:
                    return result.Success && HasDiagnostic(
                        runtime,
                        GameplayTriggerErrorCode.IntentCapacityExceeded);
            }
        }

        private static GameplayTriggerRuntime SingleRuntime(
            GameplayTriggerSourceDefinition source,
            GameplayTriggerRouteDefinition route,
            IGameplayTriggerActionSink sink,
            GameplayTriggerRuntimeOptions options)
        {
            var definition = new GameplayTriggerDefinition(
                DefinitionId,
                1,
                source.SourceDefinitionId,
                new[] { route });
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[] { Binding(1, 1) },
                new[] { sink },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(catalog, options);
        }

        private static GameplayTriggerRuntime MultiBindingSignalRuntime(
            int bindingCount,
            GameplayTriggerRuntimeOptions options)
        {
            GameplayTriggerSourceDefinition source = SignalSource();
            var definition = new GameplayTriggerDefinition(
                DefinitionId,
                1,
                SourceDefinitionId,
                new[] { Route(GameplayTriggerOccurrenceKind.Pulsed) });
            var bindings = new GameplayTriggerBinding[bindingCount];
            for (int i = 0; i < bindings.Length; i++)
            {
                bindings[i] = Binding(i + 1, i + 1);
            }
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                bindings,
                new IGameplayTriggerActionSink[] { new CountingSink(ActionKindId) },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(catalog, options);
        }

        private static GameplayTriggerBinding Binding(int bindingId, int sourceBindingId) =>
            new GameplayTriggerBinding(
                new GameplayTriggerBindingId(bindingId),
                DefinitionId,
                new GameplayTriggerSourceBindingId(sourceBindingId),
                new GameplayEntityId(100 + bindingId, 1));

        private static GameplayTriggerSourceDefinition OccupancySource() =>
            new GameplayTriggerSourceDefinition(
                SourceDefinitionId,
                GameplayTriggerSourceKind.Occupancy);

        private static GameplayTriggerSourceDefinition SignalSource() =>
            new GameplayTriggerSourceDefinition(
                SourceDefinitionId,
                GameplayTriggerSourceKind.Signal);

        private static GameplayTriggerRouteDefinition Route(
            GameplayTriggerOccurrenceKind mask,
            GameplayTriggerConditionProgram condition = null,
            GameplayTriggerActivationPolicy policy = default,
            int actionCount = 1)
        {
            var actions = new GameplayTriggerActionInstruction[actionCount];
            for (int i = 0; i < actions.Length; i++)
            {
                actions[i] = new GameplayTriggerActionInstruction(
                    ActionKindId,
                    GameplayTriggerTargetSelector.Owner,
                    payload0: i);
            }
            return new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                mask,
                condition ?? GameplayTriggerConditionProgram.Always,
                actions,
                policy);
        }

        private static GameplayTriggerConditionProgram MaximumCondition(int depth)
        {
            var instructions = new GameplayTriggerConditionInstruction[(depth * 2) - 1];
            for (int i = 0; i < depth; i++)
            {
                instructions[i] = new GameplayTriggerConditionInstruction(
                    GameplayTriggerConditionOperation.PushTrue);
            }
            for (int i = depth; i < instructions.Length; i++)
            {
                instructions[i] = new GameplayTriggerConditionInstruction(
                    GameplayTriggerConditionOperation.All);
            }
            var program = new GameplayTriggerConditionProgram(instructions);
            Assert.AreEqual(depth, program.MaxStackDepth);
            return program;
        }

        private static GameplayTriggerRuntimeOptions Options(
            int maxInputBatches = 4,
            int maxInputItems = 8,
            int maxRelations = 8,
            int maxOccurrences = 8,
            int maxCandidates = 8,
            int maxRouteStates = 8,
            int maxActivations = 8,
            int maxIntents = 8,
            int maxPreparedActions = 4,
            int maxConditionStackDepth = 16) =>
            new GameplayTriggerRuntimeOptions(
                maxInputBatches,
                maxInputItems,
                maxRelations,
                maxOccurrences,
                maxCandidates,
                maxRouteStates,
                maxActivations,
                maxIntents,
                maxPreparedActions,
                maxConditionStackDepth);

        private static void SubmitOccupancy(
            GameplayTriggerRuntime runtime,
            long frame,
            params GameplayEntityId[] subjects)
        {
            var runtimeFrame = new RuntimeFrame(frame);
            Assert.IsTrue(runtime.TrySubmitOccupancy(
                runtimeFrame,
                SourceBindingId,
                subjects,
                out GameplayTriggerResult submit),
                submit.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(runtimeFrame).Success);
        }

        private static void SubmitSignals(
            GameplayTriggerRuntime runtime,
            long frame,
            GameplayTriggerSignal[] signals)
        {
            var runtimeFrame = new RuntimeFrame(frame);
            Assert.IsTrue(runtime.TrySubmitSignals(
                runtimeFrame,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                signals,
                out GameplayTriggerResult submit),
                submit.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(runtimeFrame).Success);
        }

        private static GameplayTriggerResult SubmitSignalsForBoundary(
            GameplayTriggerRuntime runtime,
            int load)
        {
            var frame = RuntimeFrame.Zero;
            Assert.IsTrue(runtime.TrySubmitSignals(
                frame,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                Signals(load),
                out GameplayTriggerResult submit),
                submit.ErrorCode.ToString());
            return runtime.Evaluate(frame);
        }

        private static GameplayEntityId[] Subjects(
            int load,
            GameplayEntityId a,
            GameplayEntityId b,
            GameplayEntityId c)
        {
            var values = new[] { a, b, c };
            var result = new GameplayEntityId[load];
            Array.Copy(values, result, load);
            return result;
        }

        private static GameplayTriggerScalarSample[] Scalars(
            int load,
            GameplayEntityId a,
            GameplayEntityId b,
            GameplayEntityId c)
        {
            GameplayEntityId[] subjects = Subjects(load, a, b, c);
            var result = new GameplayTriggerScalarSample[load];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new GameplayTriggerScalarSample(subjects[i], default);
            }
            return result;
        }

        private static GameplayTriggerSignal[] Signals(int load)
        {
            var result = new GameplayTriggerSignal[load];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new GameplayTriggerSignal(
                    i + 1,
                    new GameplayEntityId(i + 1, 1));
            }
            return result;
        }

        private static void SubmitExpectedEvaluationFailure(
            GameplayTriggerRuntime runtime,
            long frame,
            GameplayTriggerSignal[] signals)
        {
            var runtimeFrame = new RuntimeFrame(frame);
            Assert.IsTrue(runtime.TrySubmitSignals(
                runtimeFrame,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                signals,
                out _));
            Assert.IsFalse(runtime.Evaluate(runtimeFrame).Success);
        }

        private static void AssertBoundaryEvaluationError(
            GameplayTriggerResult result,
            int load,
            GameplayTriggerErrorCode overflowError)
        {
            Assert.AreEqual(
                load <= BoundaryCapacity ? GameplayTriggerErrorCode.None : overflowError,
                result.ErrorCode);
        }

        private static void AssertBoundaryDiagnostic(
            GameplayTriggerRuntime runtime,
            int load,
            GameplayTriggerErrorCode overflowError)
        {
            if (load <= BoundaryCapacity)
            {
                Assert.AreEqual(0, runtime.DiagnosticCount);
            }
            else
            {
                AssertDiagnostic(runtime, overflowError);
            }
        }

        private static void AssertDiagnostic(
            GameplayTriggerRuntime runtime,
            GameplayTriggerErrorCode errorCode)
        {
            Assert.IsTrue(
                HasDiagnostic(runtime, errorCode),
                "Expected diagnostic " + errorCode);
        }

        private static bool HasDiagnostic(
            GameplayTriggerRuntime runtime,
            GameplayTriggerErrorCode errorCode)
        {
            for (int i = 0; i < runtime.DiagnosticCount; i++)
            {
                if (runtime.GetDiagnostic(i).Code == errorCode)
                {
                    return true;
                }
            }
            return false;
        }

        private sealed class CountingSink : IGameplayTriggerActionSink
        {
            private readonly int _rejectActionIndex;
            private int _nextToken;

            public CountingSink(
                GameplayTriggerActionKindId actionKindId,
                int rejectActionIndex = -1)
            {
                ActionKindId = actionKindId;
                _rejectActionIndex = rejectActionIndex;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;
            public int PrepareCount { get; private set; }
            public int CommitCount { get; private set; }
            public int AbortCount { get; private set; }

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                PrepareCount++;
                token = ++_nextToken;
                return preparation.ActionIndex != _rejectActionIndex;
            }

            public void Commit(int token, in GameplayTriggerActionIntent intent)
            {
                CommitCount++;
            }

            public void Abort(int token)
            {
                AbortCount++;
            }
        }

        public enum OverflowPath
        {
            Relation,
            Candidate,
            RouteState,
            Activation,
            Intent,
            PreparedAction
        }
    }
}
