using System;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerRuntimeTests
    {
        private static readonly GameplayTriggerSourceBindingId SourceBindingId = new GameplayTriggerSourceBindingId(100);
        private static readonly GameplayTriggerActionKindId ActionKind = new GameplayTriggerActionKindId(1000);

        [Test]
        public void Catalog_RejectsUnknownActionKind()
        {
            var sink = new FixedSink(ActionKind);
            GameplayTriggerRouteDefinition route = Route(
                1,
                GameplayTriggerOccurrenceKind.Pulsed,
                new GameplayTriggerActionKindId(9999));

            bool success = GameplayTriggerCatalog.TryCreate(
                new[] { SignalSource() },
                new[] { Definition(route) },
                new[] { Binding() },
                new IGameplayTriggerActionSink[] { sink },
                out _,
                out GameplayTriggerCatalogValidationResult result);

            Assert.IsFalse(success);
            Assert.AreEqual(GameplayTriggerErrorCode.UnknownActionKind, result.ErrorCode);
            Assert.AreEqual(9999, result.PrimaryId);
        }

        [Test]
        public void Catalog_RejectsNonAdjacentDuplicateSourceBindingIds()
        {
            bool success = GameplayTriggerCatalog.TryCreate(
                new[] { SignalSource() },
                new[] { Definition(Route(1, GameplayTriggerOccurrenceKind.Pulsed, ActionKind)) },
                new[]
                {
                    new GameplayTriggerBinding(new GameplayTriggerBindingId(1), new GameplayTriggerDefinitionId(1), new GameplayTriggerSourceBindingId(100), new GameplayEntityId(1, 1)),
                    new GameplayTriggerBinding(new GameplayTriggerBindingId(2), new GameplayTriggerDefinitionId(1), new GameplayTriggerSourceBindingId(101), new GameplayEntityId(2, 1)),
                    new GameplayTriggerBinding(new GameplayTriggerBindingId(3), new GameplayTriggerDefinitionId(1), new GameplayTriggerSourceBindingId(100), new GameplayEntityId(3, 1))
                },
                new IGameplayTriggerActionSink[] { new FixedSink(ActionKind) },
                out _,
                out GameplayTriggerCatalogValidationResult result);

            Assert.IsFalse(success);
            Assert.AreEqual(GameplayTriggerErrorCode.InvalidCatalog, result.ErrorCode);
            Assert.AreEqual(100, result.PrimaryId);
        }

        [Test]
        public void RejectedInputBatch_DoesNotConsumeInputCapacity()
        {
            GameplayTriggerRuntime occupancy = Runtime(
                OccupancySource(),
                new[] { Route(1, GameplayTriggerOccurrenceKind.Entered, ActionKind) },
                new IGameplayTriggerActionSink[] { new FixedSink(ActionKind) },
                Options(maxInputItems: 2));
            var valid = new GameplayEntityId(1, 1);

            Assert.IsFalse(occupancy.TrySubmitOccupancy(
                RuntimeFrame.Zero,
                SourceBindingId,
                new[] { valid, default(GameplayEntityId) },
                out GameplayTriggerResult invalidOccupancy));
            Assert.AreEqual(GameplayTriggerErrorCode.InvalidArgument, invalidOccupancy.ErrorCode);
            Assert.IsTrue(occupancy.TrySubmitOccupancy(RuntimeFrame.Zero, SourceBindingId, new[] { valid, valid }, out _));

            GameplayTriggerRuntime orderedSignals = Runtime(
                SignalSource(GameplayTriggerSignalOrdering.Ordered),
                new[] { Route(1, GameplayTriggerOccurrenceKind.Pulsed, ActionKind) },
                new IGameplayTriggerActionSink[] { new FixedSink(ActionKind) },
                Options(maxInputItems: 2));
            var invalid = new[] { new GameplayTriggerSignal(1, sequence: 2), new GameplayTriggerSignal(1, sequence: 1) };
            var retry = new[] { new GameplayTriggerSignal(1, sequence: 3), new GameplayTriggerSignal(1, sequence: 4) };

            Assert.IsFalse(orderedSignals.TrySubmitSignals(RuntimeFrame.Zero, SourceBindingId, GameplayTriggerSignalOrdering.Ordered, invalid, out _));
            Assert.IsTrue(orderedSignals.TrySubmitSignals(RuntimeFrame.Zero, SourceBindingId, GameplayTriggerSignalOrdering.Ordered, retry, out _));
        }

        [Test]
        public void Evaluate_RejectsRepeatedOrRegressedFrameWithoutReopeningSubmission()
        {
            GameplayTriggerRuntime runtime = Runtime(
                SignalSource(),
                new[] { Route(1, GameplayTriggerOccurrenceKind.Pulsed, ActionKind) },
                new IGameplayTriggerActionSink[] { new FixedSink(ActionKind) });

            Assert.IsTrue(runtime.TrySubmitSignals(new RuntimeFrame(5), SourceBindingId, GameplayTriggerSignalOrdering.UnorderedMultiset, new[] { new GameplayTriggerSignal(1) }, out _));
            Assert.IsTrue(runtime.Evaluate(new RuntimeFrame(5)).Success);
            Assert.AreEqual(GameplayTriggerErrorCode.InvalidFrame, runtime.Evaluate(new RuntimeFrame(5)).ErrorCode);
            Assert.AreEqual(GameplayTriggerErrorCode.InvalidFrame, runtime.Evaluate(new RuntimeFrame(4)).ErrorCode);
            Assert.IsFalse(runtime.TrySubmitSignals(new RuntimeFrame(5), SourceBindingId, GameplayTriggerSignalOrdering.UnorderedMultiset, new[] { new GameplayTriggerSignal(1) }, out GameplayTriggerResult result));
            Assert.AreEqual(GameplayTriggerErrorCode.InvalidFrame, result.ErrorCode);
        }

        [Test]
        public void Runtime_RejectsConditionStackCapacitySmallerThanCatalogRequirement()
        {
            var condition = new GameplayTriggerConditionProgram(new[]
            {
                new GameplayTriggerConditionInstruction(GameplayTriggerConditionOperation.PushTrue),
                new GameplayTriggerConditionInstruction(GameplayTriggerConditionOperation.PushFalse),
                new GameplayTriggerConditionInstruction(GameplayTriggerConditionOperation.Any)
            });
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { SignalSource() },
                new[] { Definition(Route(1, GameplayTriggerOccurrenceKind.Pulsed, ActionKind, condition)) },
                new[] { Binding() },
                new IGameplayTriggerActionSink[] { new FixedSink(ActionKind) },
                out GameplayTriggerCatalog catalog,
                out _));

            Assert.Throws<ArgumentException>(() => new GameplayTriggerRuntime(catalog, Options(maxConditionStackDepth: 1)));
        }

        [Test]
        public void Catalog_RejectsMixedArbitrationScopesWithinExclusiveGroup()
        {
            GameplayTriggerRouteDefinition global = Route(
                1,
                GameplayTriggerOccurrenceKind.Pulsed,
                ActionKind,
                exclusiveGroupId: 7,
                arbitrationScope: GameplayTriggerArbitrationScope.Global);
            GameplayTriggerRouteDefinition perSubject = Route(
                2,
                GameplayTriggerOccurrenceKind.Pulsed,
                ActionKind,
                exclusiveGroupId: 7,
                arbitrationScope: GameplayTriggerArbitrationScope.PerSubject);

            bool success = GameplayTriggerCatalog.TryCreate(
                new[] { SignalSource() },
                new[] { Definition(global, perSubject) },
                new[] { Binding() },
                new IGameplayTriggerActionSink[] { new FixedSink(ActionKind) },
                out _,
                out GameplayTriggerCatalogValidationResult result);

            Assert.IsFalse(success);
            Assert.AreEqual(GameplayTriggerErrorCode.InvalidCatalog, result.ErrorCode);
            Assert.AreEqual(7, result.PrimaryId);
        }

        [Test]
        public void ActionPreparationAndIntent_ExposeOwnerAndResolvedExplicitTarget()
        {
            var sink = new FixedSink(ActionKind);
            var owner = new GameplayEntityId(10, 1);
            var explicitTarget = new GameplayEntityId(20, 1);
            var route = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                GameplayTriggerOccurrenceKind.Pulsed,
                GameplayTriggerConditionProgram.Always,
                new[]
                {
                    new GameplayTriggerActionInstruction(ActionKind, GameplayTriggerTargetSelector.Owner),
                    new GameplayTriggerActionInstruction(ActionKind, GameplayTriggerTargetSelector.ExplicitBindingTarget, targetSlotId: 3)
                });
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { SignalSource() },
                new[] { Definition(route) },
                new[]
                {
                    new GameplayTriggerBinding(
                        new GameplayTriggerBindingId(1),
                        new GameplayTriggerDefinitionId(1),
                        SourceBindingId,
                        owner,
                        explicitTargets: new[]
                        {
                            new GameplayTriggerBindingTarget(
                                3,
                                new GameplayTriggerTargetRef(7, explicitTarget.Index, explicitTarget.Generation))
                        })
                },
                new IGameplayTriggerActionSink[] { sink },
                out GameplayTriggerCatalog catalog,
                out _));
            var runtime = new GameplayTriggerRuntime(catalog, Options());

            Assert.IsTrue(runtime.TrySubmitSignals(RuntimeFrame.Zero, SourceBindingId, GameplayTriggerSignalOrdering.UnorderedMultiset, new[] { new GameplayTriggerSignal(1) }, out _));
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);

            Assert.AreEqual(owner, sink.GetPreparation(0).Owner);
            Assert.AreEqual(GameplayTriggerTargetRef.FromGameplayEntity(owner), sink.GetPreparation(0).Target);
            Assert.AreEqual(new GameplayTriggerTargetRef(7, explicitTarget.Index, explicitTarget.Generation), sink.GetPreparation(1).Target);
            Assert.AreEqual(owner, runtime.GetIntent(0).Owner);
            Assert.AreEqual(new GameplayTriggerTargetRef(7, explicitTarget.Index, explicitTarget.Generation), runtime.GetIntent(1).Target);
        }

        [Test]
        public void OccupancyReducer_IsPermutationIndependentAndProducesEnterStayExit()
        {
            var firstSink = new FixedSink(ActionKind);
            var secondSink = new FixedSink(ActionKind);
            GameplayTriggerRuntime first = Runtime(
                OccupancySource(),
                new[] { Route(1, GameplayTriggerOccurrenceKind.Entered | GameplayTriggerOccurrenceKind.Stayed | GameplayTriggerOccurrenceKind.Exited, ActionKind) },
                new IGameplayTriggerActionSink[] { firstSink });
            GameplayTriggerRuntime second = Runtime(
                OccupancySource(),
                new[] { Route(1, GameplayTriggerOccurrenceKind.Entered | GameplayTriggerOccurrenceKind.Stayed | GameplayTriggerOccurrenceKind.Exited, ActionKind) },
                new IGameplayTriggerActionSink[] { secondSink });
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);

            Assert.IsTrue(first.TrySubmitOccupancy(RuntimeFrame.Zero, SourceBindingId, new[] { b, a, a }, out _));
            Assert.IsTrue(second.TrySubmitOccupancy(RuntimeFrame.Zero, SourceBindingId, new[] { a, b }, out _));
            Assert.IsTrue(first.Evaluate(RuntimeFrame.Zero).Success);
            Assert.IsTrue(second.Evaluate(RuntimeFrame.Zero).Success);

            Assert.AreEqual(2, first.OccurrenceCount);
            Assert.AreEqual(2, first.ActivationCount);
            Assert.AreEqual(a, first.GetOccurrence(0).Subject);
            Assert.AreEqual(b, first.GetOccurrence(1).Subject);
            Assert.AreEqual(first.GetOccurrence(0).Subject, second.GetOccurrence(0).Subject);
            Assert.AreEqual(first.GetOccurrence(1).Subject, second.GetOccurrence(1).Subject);

            Assert.IsTrue(first.TrySubmitOccupancy(new RuntimeFrame(1), SourceBindingId, new[] { b, a }, out _));
            Assert.IsTrue(first.Evaluate(new RuntimeFrame(1)).Success);
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Stayed, first.GetOccurrence(0).Kind);
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Stayed, first.GetOccurrence(1).Kind);

            Assert.IsTrue(first.TrySubmitOccupancy(new RuntimeFrame(2), SourceBindingId, Array.Empty<GameplayEntityId>(), out _));
            Assert.IsTrue(first.Evaluate(new RuntimeFrame(2)).Success);
            Assert.AreEqual(2, first.OccurrenceCount);
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Exited, first.GetOccurrence(0).Kind);
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Exited, first.GetOccurrence(1).Kind);
        }

        [Test]
        public void ThresholdReducer_UsesExactHighHysteresisBoundaries()
        {
            var sink = new FixedSink(ActionKind);
            GameplayTriggerRuntime runtime = Runtime(
                ThresholdSource(
                    enter: Fix64.FromInt(10),
                    exit: Fix64.FromInt(8),
                    initial: GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive),
                new[] { Route(1, GameplayTriggerOccurrenceKind.Entered | GameplayTriggerOccurrenceKind.Stayed | GameplayTriggerOccurrenceKind.Exited, ActionKind) },
                new IGameplayTriggerActionSink[] { sink });
            var subject = new GameplayEntityId(1, 1);

            SubmitScalar(runtime, 0, subject, Fix64.FromInt(10));
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Entered, runtime.GetOccurrence(0).Kind);
            SubmitScalar(runtime, 1, subject, Fix64.FromInt(8));
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Stayed, runtime.GetOccurrence(0).Kind);
            SubmitScalar(runtime, 2, subject, Fix64.FromRaw(Fix64.FromInt(8).RawValue - 1L));
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Exited, runtime.GetOccurrence(0).Kind);
        }

        [Test]
        public void PrimeSilently_CreatesPresenceEpochAndOncePerPresenceRearmsAfterExit()
        {
            var sink = new FixedSink(ActionKind);
            var policy = new GameplayTriggerActivationPolicy(
                GameplayTriggerStateScope.PerSubject,
                presenceLimit: GameplayTriggerPresenceLimit.OncePerPresence);
            GameplayTriggerRuntime runtime = Runtime(
                ThresholdSource(Fix64.FromInt(10), Fix64.FromInt(8), GameplayTriggerInitialSamplePolicy.PrimeSilently),
                new[] { Route(1, GameplayTriggerOccurrenceKind.Stayed, ActionKind, policy: policy) },
                new IGameplayTriggerActionSink[] { sink });
            var subject = new GameplayEntityId(1, 1);

            SubmitScalar(runtime, 0, subject, Fix64.FromInt(10));
            Assert.AreEqual(0, runtime.ActivationCount);
            SubmitScalar(runtime, 1, subject, Fix64.FromInt(10));
            Assert.AreEqual(1, runtime.ActivationCount);
            SubmitScalar(runtime, 2, subject, Fix64.FromInt(10));
            Assert.AreEqual(0, runtime.ActivationCount);
            SubmitScalar(runtime, 3, subject, Fix64.FromInt(7));
            SubmitScalar(runtime, 4, subject, Fix64.FromInt(10));
            SubmitScalar(runtime, 5, subject, Fix64.FromInt(10));
            Assert.AreEqual(1, runtime.ActivationCount);
        }

        [Test]
        public void UnorderedSignals_KeepMultiplicityButIgnoreInputPermutation()
        {
            var first = Runtime(SignalSource(), new[] { Route(1, GameplayTriggerOccurrenceKind.Pulsed, ActionKind) }, new IGameplayTriggerActionSink[] { new FixedSink(ActionKind) });
            var second = Runtime(SignalSource(), new[] { Route(1, GameplayTriggerOccurrenceKind.Pulsed, ActionKind) }, new IGameplayTriggerActionSink[] { new FixedSink(ActionKind) });
            var subject = new GameplayEntityId(1, 1);
            var low = new GameplayTriggerSignal(3, subject, payload0: 10);
            var high = new GameplayTriggerSignal(3, subject, payload0: 20);

            Assert.IsTrue(first.TrySubmitSignals(RuntimeFrame.Zero, SourceBindingId, GameplayTriggerSignalOrdering.UnorderedMultiset, new[] { high, low, low }, out _));
            Assert.IsTrue(second.TrySubmitSignals(RuntimeFrame.Zero, SourceBindingId, GameplayTriggerSignalOrdering.UnorderedMultiset, new[] { low, high, low }, out _));
            first.Evaluate(RuntimeFrame.Zero);
            second.Evaluate(RuntimeFrame.Zero);

            Assert.AreEqual(3, first.ActivationCount);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(first.GetOccurrence(i).Payload0, second.GetOccurrence(i).Payload0);
                Assert.AreEqual(first.GetOccurrence(i).OrderingValue, second.GetOccurrence(i).OrderingValue);
            }
            Assert.AreEqual(10L, first.GetOccurrence(0).Payload0);
            Assert.AreEqual(10L, first.GetOccurrence(1).Payload0);
            Assert.AreEqual(20L, first.GetOccurrence(2).Payload0);
        }

        [Test]
        public void OrderedSignals_RequireStrictSequenceAndPreserveIt()
        {
            var runtime = Runtime(
                SignalSource(GameplayTriggerSignalOrdering.Ordered),
                new[] { Route(1, GameplayTriggerOccurrenceKind.Pulsed, ActionKind) },
                new IGameplayTriggerActionSink[] { new FixedSink(ActionKind) });
            var invalid = new[] { new GameplayTriggerSignal(1, sequence: 2), new GameplayTriggerSignal(1, sequence: 1) };

            Assert.IsFalse(runtime.TrySubmitSignals(RuntimeFrame.Zero, SourceBindingId, GameplayTriggerSignalOrdering.Ordered, invalid, out GameplayTriggerResult invalidResult));
            Assert.AreEqual(GameplayTriggerErrorCode.InvalidArgument, invalidResult.ErrorCode);

            var valid = new[] { new GameplayTriggerSignal(1, payload0: 20, sequence: 2), new GameplayTriggerSignal(1, payload0: 10, sequence: 3) };
            Assert.IsTrue(runtime.TrySubmitSignals(RuntimeFrame.Zero, SourceBindingId, GameplayTriggerSignalOrdering.Ordered, valid, out _));
            runtime.Evaluate(RuntimeFrame.Zero);
            Assert.AreEqual(2L, runtime.GetOccurrence(0).OrderingValue);
            Assert.AreEqual(20L, runtime.GetOccurrence(0).Payload0);
            Assert.AreEqual(3L, runtime.GetOccurrence(1).OrderingValue);
            Assert.AreEqual(10L, runtime.GetOccurrence(1).Payload0);
        }

        [Test]
        public void ConditionProgram_IsReadOnlyAndUsesPostfixSemantics()
        {
            var condition = new GameplayTriggerConditionProgram(new[]
            {
                new GameplayTriggerConditionInstruction(GameplayTriggerConditionOperation.HasSubject, 1),
                new GameplayTriggerConditionInstruction(GameplayTriggerConditionOperation.Payload0, 5, GameplayTriggerComparison.Greater),
                new GameplayTriggerConditionInstruction(GameplayTriggerConditionOperation.All)
            });
            GameplayTriggerRouteDefinition route = Route(1, GameplayTriggerOccurrenceKind.Pulsed, ActionKind, condition);
            GameplayTriggerRuntime runtime = Runtime(SignalSource(), new[] { route }, new IGameplayTriggerActionSink[] { new FixedSink(ActionKind) });
            var subject = new GameplayEntityId(1, 1);

            SubmitSignal(runtime, 0, new GameplayTriggerSignal(1, subject, payload0: 4));
            Assert.AreEqual(0, runtime.ActivationCount);
            SubmitSignal(runtime, 1, new GameplayTriggerSignal(1, subject, payload0: 6));
            Assert.AreEqual(1, runtime.ActivationCount);
        }

        [Test]
        public void Policy_ConsumesCooldownAndCountOnlyOnAcceptedActivation()
        {
            var sink = new FixedSink(ActionKind);
            var policy = new GameplayTriggerActivationPolicy(GameplayTriggerStateScope.PerSubject, maxActivations: 2, cooldownFrames: 2);
            GameplayTriggerRuntime runtime = Runtime(SignalSource(), new[] { Route(1, GameplayTriggerOccurrenceKind.Pulsed, ActionKind, policy: policy) }, new IGameplayTriggerActionSink[] { sink });
            var subject = new GameplayEntityId(1, 1);

            SubmitSignal(runtime, 0, new GameplayTriggerSignal(1, subject));
            Assert.AreEqual(1, runtime.ActivationCount);
            SubmitSignal(runtime, 1, new GameplayTriggerSignal(1, subject));
            Assert.AreEqual(0, runtime.ActivationCount);
            SubmitSignal(runtime, 2, new GameplayTriggerSignal(1, subject));
            Assert.AreEqual(1, runtime.ActivationCount);
            SubmitSignal(runtime, 4, new GameplayTriggerSignal(1, subject));
            Assert.AreEqual(0, runtime.ActivationCount);
        }

        [Test]
        public void ExclusiveArbitration_UsesWinnerKeyNotExecutionOrder()
        {
            var sink = new FixedSink(ActionKind);
            GameplayTriggerRouteDefinition highExecution = Route(1, GameplayTriggerOccurrenceKind.Pulsed, ActionKind, executionOrder: 100, exclusiveGroupId: 7);
            GameplayTriggerRouteDefinition lowExecution = Route(2, GameplayTriggerOccurrenceKind.Pulsed, ActionKind, executionOrder: -100, exclusiveGroupId: 7);
            GameplayTriggerRuntime runtime = Runtime(SignalSource(), new[] { lowExecution, highExecution }, new IGameplayTriggerActionSink[] { sink });

            SubmitSignal(runtime, 0, new GameplayTriggerSignal(1));

            Assert.AreEqual(1, runtime.ActivationCount);
            Assert.AreEqual(new GameplayTriggerRouteId(1), runtime.GetActivation(0).RouteId);
            AssertDiagnostic(runtime, GameplayTriggerErrorCode.ArbitrationSuppressed);
        }

        [Test]
        public void PrepareFailure_AbortsEarlierTokensAndDoesNotRecreateEnter()
        {
            var firstKind = new GameplayTriggerActionKindId(1001);
            var secondKind = new GameplayTriggerActionKindId(1002);
            var firstSink = new FixedSink(firstKind);
            var secondSink = new FixedSink(secondKind) { RejectPrepare = true };
            GameplayTriggerRouteDefinition route = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                GameplayTriggerOccurrenceKind.Entered,
                GameplayTriggerConditionProgram.Always,
                new[]
                {
                    new GameplayTriggerActionInstruction(firstKind, GameplayTriggerTargetSelector.Subject),
                    new GameplayTriggerActionInstruction(secondKind, GameplayTriggerTargetSelector.Subject)
                });
            GameplayTriggerRuntime runtime = Runtime(OccupancySource(), new[] { route }, new IGameplayTriggerActionSink[] { secondSink, firstSink });
            var subject = new GameplayEntityId(1, 1);

            Assert.IsTrue(runtime.TrySubmitOccupancy(RuntimeFrame.Zero, SourceBindingId, new[] { subject }, out _));
            runtime.Evaluate(RuntimeFrame.Zero);

            Assert.AreEqual(0, runtime.ActivationCount);
            Assert.AreEqual(0, runtime.IntentCount);
            Assert.AreEqual(1L, runtime.NextActivationId);
            Assert.AreEqual(1, firstSink.AbortCount);
            Assert.AreEqual(0, firstSink.CommitCount);

            secondSink.RejectPrepare = false;
            Assert.IsTrue(runtime.TrySubmitOccupancy(new RuntimeFrame(1), SourceBindingId, new[] { subject }, out _));
            runtime.Evaluate(new RuntimeFrame(1));
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Stayed, runtime.GetOccurrence(0).Kind);
            Assert.AreEqual(0, runtime.ActivationCount);
        }

        [Test]
        public void OccurrenceCapacityFailure_PreservesSourceTruth()
        {
            GameplayTriggerRuntime runtime = Runtime(
                OccupancySource(),
                new[] { Route(1, GameplayTriggerOccurrenceKind.Entered, ActionKind) },
                new IGameplayTriggerActionSink[] { new FixedSink(ActionKind) },
                Options(maxOccurrences: 1));
            var a = new GameplayEntityId(1, 1);
            var b = new GameplayEntityId(2, 1);

            Assert.IsTrue(runtime.TrySubmitOccupancy(RuntimeFrame.Zero, SourceBindingId, new[] { a, b }, out _));
            GameplayTriggerResult failed = runtime.Evaluate(RuntimeFrame.Zero);
            Assert.IsFalse(failed.Success);
            Assert.AreEqual(GameplayTriggerErrorCode.OccurrenceCapacityExceeded, failed.ErrorCode);
            Assert.AreEqual(0, runtime.RelationCount);

            Assert.IsTrue(runtime.TrySubmitOccupancy(new RuntimeFrame(1), SourceBindingId, new[] { a }, out _));
            Assert.IsTrue(runtime.Evaluate(new RuntimeFrame(1)).Success);
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Entered, runtime.GetOccurrence(0).Kind);
        }

        [Test]
        public void OccurrenceCapacity_AllowsSingleSubjectSteadyStateAtCapacityOne()
        {
            GameplayTriggerRuntime runtime = Runtime(
                OccupancySource(),
                new[] { Route(1, GameplayTriggerOccurrenceKind.Entered | GameplayTriggerOccurrenceKind.Stayed, ActionKind) },
                new IGameplayTriggerActionSink[] { new FixedSink(ActionKind) },
                Options(maxOccurrences: 1));
            var subject = new GameplayEntityId(1, 1);

            Assert.IsTrue(runtime.TrySubmitOccupancy(RuntimeFrame.Zero, SourceBindingId, new[] { subject }, out _));
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.IsTrue(runtime.TrySubmitOccupancy(new RuntimeFrame(1), SourceBindingId, new[] { subject }, out _));
            Assert.IsTrue(runtime.Evaluate(new RuntimeFrame(1)).Success);
            Assert.AreEqual(1, runtime.OccurrenceCount);
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Stayed, runtime.GetOccurrence(0).Kind);
        }

        [Test]
        public void CandidateCapacity_IsReservedAcrossMultipleSourceBatches()
        {
            var route = Route(1, GameplayTriggerOccurrenceKind.Pulsed, ActionKind);
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { SignalSource() },
                new[] { Definition(route) },
                new[]
                {
                    new GameplayTriggerBinding(new GameplayTriggerBindingId(1), new GameplayTriggerDefinitionId(1), new GameplayTriggerSourceBindingId(100), new GameplayEntityId(1, 1)),
                    new GameplayTriggerBinding(new GameplayTriggerBindingId(2), new GameplayTriggerDefinitionId(1), new GameplayTriggerSourceBindingId(101), new GameplayEntityId(2, 1))
                },
                new IGameplayTriggerActionSink[] { new FixedSink(ActionKind) },
                out GameplayTriggerCatalog catalog,
                out _));
            var runtime = new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(4, 4, 4, 4, 1, 4, 4, 4, 2, 4));

            Assert.IsTrue(runtime.TrySubmitSignals(RuntimeFrame.Zero, new GameplayTriggerSourceBindingId(100), GameplayTriggerSignalOrdering.UnorderedMultiset, new[] { new GameplayTriggerSignal(1) }, out _));
            Assert.IsTrue(runtime.TrySubmitSignals(RuntimeFrame.Zero, new GameplayTriggerSourceBindingId(101), GameplayTriggerSignalOrdering.UnorderedMultiset, new[] { new GameplayTriggerSignal(1) }, out _));
            GameplayTriggerResult result = runtime.Evaluate(RuntimeFrame.Zero);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(GameplayTriggerErrorCode.CandidateCapacityExceeded, result.ErrorCode);
            Assert.AreEqual(1, runtime.OccurrenceCount);
            Assert.AreEqual(1, runtime.ActivationCount);
        }

        [Test]
        public void CandidateCapacity_UsesExactThresholdAndSignalCountsAcrossSources()
        {
            var thresholdSourceId = new GameplayTriggerSourceDefinitionId(1);
            var signalSourceId = new GameplayTriggerSourceDefinitionId(2);
            var thresholdBindingId = new GameplayTriggerSourceBindingId(100);
            var signalBindingId = new GameplayTriggerSourceBindingId(101);
            var thresholdSource = new GameplayTriggerSourceDefinition(
                thresholdSourceId,
                GameplayTriggerSourceKind.Threshold,
                thresholdDirection: GameplayTriggerThresholdDirection.High,
                enterThreshold: Fix64.FromInt(10),
                exitThreshold: Fix64.FromInt(8),
                initialSamplePolicy: GameplayTriggerInitialSamplePolicy.PrimeSilently);
            var signalSource = new GameplayTriggerSourceDefinition(signalSourceId, GameplayTriggerSourceKind.Signal);
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { thresholdSource, signalSource },
                new[]
                {
                    new GameplayTriggerDefinition(
                        new GameplayTriggerDefinitionId(1),
                        1,
                        thresholdSourceId,
                        new[] { Route(1, GameplayTriggerOccurrenceKind.Stayed, ActionKind) }),
                    new GameplayTriggerDefinition(
                        new GameplayTriggerDefinitionId(2),
                        1,
                        signalSourceId,
                        new[] { Route(2, GameplayTriggerOccurrenceKind.Pulsed, ActionKind) })
                },
                new[]
                {
                    new GameplayTriggerBinding(new GameplayTriggerBindingId(1), new GameplayTriggerDefinitionId(1), thresholdBindingId, new GameplayEntityId(1, 1)),
                    new GameplayTriggerBinding(new GameplayTriggerBindingId(2), new GameplayTriggerDefinitionId(2), signalBindingId, new GameplayEntityId(2, 1))
                },
                new IGameplayTriggerActionSink[] { new FixedSink(ActionKind) },
                out GameplayTriggerCatalog catalog,
                out _));
            var runtime = new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(4, 4, 4, 4, 2, 4, 4, 4, 2, 4));
            var subject = new GameplayEntityId(10, 1);

            Assert.IsTrue(runtime.TrySubmitScalarBatch(
                RuntimeFrame.Zero,
                thresholdBindingId,
                new[] { new GameplayTriggerScalarSample(subject, Fix64.FromInt(10)) },
                out _));
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(0, runtime.OccurrenceCount);

            var frame = new RuntimeFrame(1);
            Assert.IsTrue(runtime.TrySubmitScalarBatch(
                frame,
                thresholdBindingId,
                new[] { new GameplayTriggerScalarSample(subject, Fix64.FromInt(10)) },
                out _));
            Assert.IsTrue(runtime.TrySubmitSignals(
                frame,
                signalBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[] { new GameplayTriggerSignal(1) },
                out _));
            GameplayTriggerResult result = runtime.Evaluate(frame);

            Assert.IsTrue(result.Success, result.ErrorCode.ToString());
            Assert.AreEqual(2, runtime.OccurrenceCount);
            Assert.AreEqual(2, runtime.ActivationCount);
        }

        [Test]
        public void DisableClearAndReenable_ProducesNewPresenceWithoutResettingRuntime()
        {
            var policy = new GameplayTriggerActivationPolicy(GameplayTriggerStateScope.PerSubject, presenceLimit: GameplayTriggerPresenceLimit.OncePerPresence);
            GameplayTriggerRuntime runtime = Runtime(OccupancySource(), new[] { Route(1, GameplayTriggerOccurrenceKind.Entered, ActionKind, policy: policy) }, new IGameplayTriggerActionSink[] { new FixedSink(ActionKind) });
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var subject = new GameplayEntityId(1, 1);

            Assert.IsTrue(runtime.TrySubmitOccupancy(RuntimeFrame.Zero, SourceBindingId, new[] { subject }, out _));
            runtime.Evaluate(RuntimeFrame.Zero);
            Assert.AreEqual(1, runtime.ActivationCount);
            Assert.IsTrue(runtime.TrySetEnabled(instanceId, false, out _));
            Assert.IsTrue(runtime.TrySetEnabled(instanceId, true, out _));
            Assert.IsTrue(runtime.TrySubmitOccupancy(new RuntimeFrame(1), SourceBindingId, new[] { subject }, out _));
            runtime.Evaluate(new RuntimeFrame(1));
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Entered, runtime.GetOccurrence(0).Kind);
            Assert.AreEqual(1, runtime.ActivationCount);
        }

        [Test]
        public void SignalSteadyState_DoesNotAllocateAfterWarmup()
        {
            var sink = new FixedSink(ActionKind, 256);
            GameplayTriggerRuntime runtime = Runtime(
                SignalSource(),
                new[] { Route(1, GameplayTriggerOccurrenceKind.Pulsed, ActionKind) },
                new IGameplayTriggerActionSink[] { sink },
                Options(maxActivations: 4, maxIntents: 4));
            var signals = new[] { new GameplayTriggerSignal(1, payload0: 10) };

            Assert.IsTrue(runtime.TrySubmitSignals(RuntimeFrame.Zero, SourceBindingId, GameplayTriggerSignalOrdering.UnorderedMultiset, signals, out _));
            runtime.Evaluate(RuntimeFrame.Zero);
            _ = GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 1; i <= 64; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                runtime.TrySubmitSignals(frame, SourceBindingId, GameplayTriggerSignalOrdering.UnorderedMultiset, signals, out _);
                runtime.Evaluate(frame);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
        }

        private static void SubmitScalar(GameplayTriggerRuntime runtime, long frame, GameplayEntityId subject, Fix64 value)
        {
            RuntimeFrame runtimeFrame = new RuntimeFrame(frame);
            Assert.IsTrue(runtime.TrySubmitScalarBatch(runtimeFrame, SourceBindingId, new[] { new GameplayTriggerScalarSample(subject, value) }, out GameplayTriggerResult submit), submit.ErrorCode.ToString());
            GameplayTriggerResult evaluated = runtime.Evaluate(runtimeFrame);
            Assert.IsTrue(evaluated.Success, evaluated.ErrorCode.ToString());
        }

        private static void SubmitSignal(GameplayTriggerRuntime runtime, long frame, GameplayTriggerSignal signal)
        {
            RuntimeFrame runtimeFrame = new RuntimeFrame(frame);
            Assert.IsTrue(runtime.TrySubmitSignals(runtimeFrame, SourceBindingId, GameplayTriggerSignalOrdering.UnorderedMultiset, new[] { signal }, out GameplayTriggerResult submit), submit.ErrorCode.ToString());
            GameplayTriggerResult evaluated = runtime.Evaluate(runtimeFrame);
            Assert.IsTrue(evaluated.Success, evaluated.ErrorCode.ToString());
        }

        private static void AssertDiagnostic(GameplayTriggerRuntime runtime, GameplayTriggerErrorCode code)
        {
            for (int i = 0; i < runtime.DiagnosticCount; i++) if (runtime.GetDiagnostic(i).Code == code) return;
            Assert.Fail("Missing diagnostic " + code);
        }

        private static GameplayTriggerRuntime Runtime(GameplayTriggerSourceDefinition source, GameplayTriggerRouteDefinition[] routes, IGameplayTriggerActionSink[] sinks, GameplayTriggerRuntimeOptions? options = null)
        {
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { new GameplayTriggerDefinition(new GameplayTriggerDefinitionId(1), 1, source.SourceDefinitionId, routes) },
                new[] { Binding() },
                sinks,
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation), validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(catalog, options ?? Options());
        }

        private static GameplayTriggerSourceDefinition SignalSource(GameplayTriggerSignalOrdering ordering = GameplayTriggerSignalOrdering.UnorderedMultiset) =>
            new GameplayTriggerSourceDefinition(new GameplayTriggerSourceDefinitionId(1), GameplayTriggerSourceKind.Signal, signalOrdering: ordering);

        private static GameplayTriggerSourceDefinition OccupancySource() =>
            new GameplayTriggerSourceDefinition(new GameplayTriggerSourceDefinitionId(1), GameplayTriggerSourceKind.Occupancy);

        private static GameplayTriggerSourceDefinition ThresholdSource(Fix64 enter, Fix64 exit, GameplayTriggerInitialSamplePolicy initial) =>
            new GameplayTriggerSourceDefinition(new GameplayTriggerSourceDefinitionId(1), GameplayTriggerSourceKind.Threshold, thresholdDirection: GameplayTriggerThresholdDirection.High, enterThreshold: enter, exitThreshold: exit, initialSamplePolicy: initial);

        private static GameplayTriggerDefinition Definition(params GameplayTriggerRouteDefinition[] routes) =>
            new GameplayTriggerDefinition(new GameplayTriggerDefinitionId(1), 1, new GameplayTriggerSourceDefinitionId(1), routes);

        private static GameplayTriggerBinding Binding() =>
            new GameplayTriggerBinding(
                new GameplayTriggerBindingId(1),
                new GameplayTriggerDefinitionId(1),
                SourceBindingId,
                new GameplayEntityId(900, 1));

        private static GameplayTriggerRouteDefinition Route(
            int routeId,
            GameplayTriggerOccurrenceKind mask,
            GameplayTriggerActionKindId actionKind,
            GameplayTriggerConditionProgram condition = null,
            GameplayTriggerActivationPolicy policy = default,
            int executionOrder = 0,
            int exclusiveGroupId = 0,
            GameplayTriggerArbitrationScope arbitrationScope = GameplayTriggerArbitrationScope.Global) =>
            new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(routeId),
                mask,
                condition ?? GameplayTriggerConditionProgram.Always,
                new[] { new GameplayTriggerActionInstruction(actionKind, GameplayTriggerTargetSelector.Owner) },
                policy,
                executionOrder,
                exclusiveGroupId,
                arbitrationScope: arbitrationScope);

        private static GameplayTriggerRuntimeOptions Options(
            int maxOccurrences = 16,
            int maxActivations = 16,
            int maxIntents = 32,
            int maxInputItems = 32,
            int maxConditionStackDepth = 16) =>
            new GameplayTriggerRuntimeOptions(
                maxInputBatches: 8,
                maxInputItems: maxInputItems,
                maxRelations: 32,
                maxOccurrences: maxOccurrences,
                maxCandidates: 64,
                maxRouteStates: 32,
                maxActivations: maxActivations,
                maxIntents: maxIntents,
                maxPreparedActions: 8,
                maxConditionStackDepth: maxConditionStackDepth);

        private sealed class FixedSink : IGameplayTriggerActionSink
        {
            private readonly GameplayTriggerActionIntent[] _committed;
            private readonly GameplayTriggerActionPreparation[] _prepared;
            private int _nextToken;

            public FixedSink(GameplayTriggerActionKindId actionKindId, int capacity = 64)
            {
                ActionKindId = actionKindId;
                _committed = new GameplayTriggerActionIntent[capacity];
                _prepared = new GameplayTriggerActionPreparation[capacity];
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract => GameplayTriggerDeliveryContract.SameFrameReserved;
            public bool RejectPrepare { get; set; }
            public int PrepareCount { get; private set; }
            public int AbortCount { get; private set; }
            public int CommitCount { get; private set; }
            public GameplayTriggerActionPreparation GetPreparation(int index) => _prepared[index];

            public bool TryPrepare(in GameplayTriggerActionPreparation preparation, out int token)
            {
                PrepareCount++;
                if (RejectPrepare || CommitCount >= _committed.Length)
                {
                    token = 0;
                    return false;
                }
                token = ++_nextToken;
                _prepared[PrepareCount - 1] = preparation;
                return true;
            }

            public void Abort(int token)
            {
                AbortCount++;
            }

            public void Commit(int token, in GameplayTriggerActionIntent intent)
            {
                _committed[CommitCount++] = intent;
            }
        }
    }
}
