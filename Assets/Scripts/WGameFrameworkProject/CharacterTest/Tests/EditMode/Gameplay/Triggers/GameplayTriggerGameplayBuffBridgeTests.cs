using System;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.GameplayBridge;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerGameplayBuffBridgeTests
    {
        private const int BuffId = 8101;
        private static readonly GameplayTriggerSourceBindingId SourceBindingId =
            new GameplayTriggerSourceBindingId(81);

        [Test]
        public void PublicContract_FreezesKindsReasonsAndOpaqueToken()
        {
            Assert.AreEqual(GameplayRuntimeCommandIds.ApplyComponentBuff,
                GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuffValue);
            Assert.AreEqual(GameplayRuntimeCommandIds.RemoveComponentBuff,
                GameplayTriggerGameplayBuffActionKinds.RemoveComponentBuffValue);
            Assert.AreEqual(GameplayTriggerDeliveryContract.SameFrameReserved,
                GameplayTriggerGameplayBuffActionKinds.DeliveryContract);
            Assert.AreEqual(1, (int)GameplayBuffApplicationResultStatus.Succeeded);
            Assert.AreEqual(2, (int)GameplayBuffApplicationResultStatus.Rejected);
            Assert.AreEqual(1, (int)GameplayBuffApplicationResultReasonCode.Applied);
            Assert.AreEqual(2, (int)GameplayBuffApplicationResultReasonCode.Refreshed);
            Assert.AreEqual(3, (int)GameplayBuffApplicationResultReasonCode.Removed);
            Assert.AreEqual(4, (int)GameplayBuffApplicationResultReasonCode.RemoveNoOp);
            Assert.AreEqual(1001, (int)GameplayBuffApplicationResultReasonCode.MissingComponentWorld);
            Assert.AreEqual(1002, (int)GameplayBuffApplicationResultReasonCode.MissingComponentEntity);
            Assert.AreEqual(1003, (int)GameplayBuffApplicationResultReasonCode.MissingBuffDefinition);
            Assert.AreEqual(1004, (int)GameplayBuffApplicationResultReasonCode.DurationOverflow);
            Assert.AreEqual(0, typeof(GameplayBuffRequestReservationToken).GetConstructors(
                BindingFlags.Instance | BindingFlags.Public).Length);
        }

        [Test]
        public void ActionSink_RejectsUnsupportedOrZeroCapacityComposition()
        {
            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerGameplayBuffActionSink(
                    GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                    new GameplayBuffRequestBuffer()));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new GameplayTriggerGameplayBuffActionSink(
                    new GameplayTriggerActionKindId(999999),
                    new GameplayBuffRequestBuffer(1)));
        }

        [Test]
        public void ActionSink_PrepareValidatesFrozenSchemaAndCommitUsesZeroCommandId()
        {
            var buffer = new GameplayBuffRequestBuffer(1);
            var sink = new GameplayTriggerGameplayBuffActionSink(
                GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                buffer);
            GameplayTriggerActionPreparation valid = Preparation(
                sink.ActionKindId,
                new GameplayTriggerTargetRef(
                    GameplayTriggerTargetRef.GameplayEntityDomainId,
                    int.MaxValue,
                    int.MaxValue),
                int.MaxValue,
                int.MaxValue,
                0L);

            Assert.IsTrue(sink.TryPrepare(in valid, out int token));
            Assert.Greater(token, 0);
            Assert.AreEqual(0, buffer.Count);
            Assert.AreEqual(1, buffer.PreparedReservationCount);
            var intent = new GameplayTriggerActionIntent(7L, valid);
            sink.Commit(token, in intent);
            sink.Commit(token, in intent);

            GameplayBuffApplicationRequest[] snapshot = buffer.CreateSnapshot();
            Assert.AreEqual(1, snapshot.Length);
            Assert.AreEqual(int.MaxValue, snapshot[0].TargetEntityId.Index);
            Assert.AreEqual(int.MaxValue, snapshot[0].TargetEntityId.Generation);
            Assert.AreEqual(int.MaxValue, snapshot[0].BuffId);
            Assert.AreEqual(int.MaxValue, snapshot[0].SourceId);
            Assert.AreEqual(string.Empty, snapshot[0].TraceId);
            Assert.AreEqual(0, snapshot[0].CommandId);
            Assert.AreEqual(7L, snapshot[0].Correlation.RequestId);
            Assert.AreEqual(0, snapshot[0].Correlation.OperationIndex);
            Assert.AreEqual(sink.ActionKindId.Value, snapshot[0].Correlation.OperationKindId);
        }

        [TestCase(-1L, 0L, 0L, GameplayTriggerTargetRef.GameplayEntityDomainId, 1L, 1)]
        [TestCase(0L, 0L, 0L, GameplayTriggerTargetRef.GameplayEntityDomainId, 1L, 1)]
        [TestCase((long)int.MaxValue + 1L, 0L, 0L, GameplayTriggerTargetRef.GameplayEntityDomainId, 1L, 1)]
        [TestCase(1L, -1L, 0L, GameplayTriggerTargetRef.GameplayEntityDomainId, 1L, 1)]
        [TestCase(1L, (long)int.MaxValue + 1L, 0L, GameplayTriggerTargetRef.GameplayEntityDomainId, 1L, 1)]
        [TestCase(1L, 0L, 1L, GameplayTriggerTargetRef.GameplayEntityDomainId, 1L, 1)]
        [TestCase(1L, 0L, 0L, 99, 1L, 1)]
        [TestCase(1L, 0L, 0L, GameplayTriggerTargetRef.GameplayEntityDomainId, (long)int.MaxValue + 1L, 1)]
        [TestCase(1L, 0L, 0L, GameplayTriggerTargetRef.GameplayEntityDomainId, 1L, 0)]
        public void ActionSink_InvalidStructuralPayloadIsAtomic(
            long payload0,
            long payload1,
            long payload2,
            int domainId,
            long targetValue,
            int targetGeneration)
        {
            var buffer = new GameplayBuffRequestBuffer(1);
            var sink = new GameplayTriggerGameplayBuffActionSink(
                GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                buffer);
            GameplayTriggerActionPreparation preparation = Preparation(
                sink.ActionKindId,
                new GameplayTriggerTargetRef(domainId, targetValue, targetGeneration),
                payload0,
                payload1,
                payload2);

            Assert.IsFalse(sink.TryPrepare(in preparation, out int token));
            Assert.AreEqual(0, token);
            Assert.AreEqual(0, buffer.PreparedReservationCount);
            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void ActionSink_DisabledAndFullRejectWithoutRevokingLiveToken()
        {
            var buffer = new GameplayBuffRequestBuffer(1);
            var sink = new GameplayTriggerGameplayBuffActionSink(
                GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                buffer);
            GameplayTriggerActionPreparation preparation = Preparation(
                sink.ActionKindId,
                GameplayTriggerTargetRef.FromGameplayEntity(new GameplayEntityId(1, 1)),
                BuffId,
                0L,
                0L);

            Assert.IsTrue(sink.TryPrepare(in preparation, out int token));
            Assert.IsFalse(sink.TryPrepare(in preparation, out _));
            sink.SetEnabled(false);
            Assert.IsFalse(sink.TryPrepare(in preparation, out _));

            var intent = new GameplayTriggerActionIntent(1L, preparation);
            Assert.DoesNotThrow(() => sink.Commit(token, in intent));
            Assert.AreEqual(1, buffer.Count);
        }

        [Test]
        public void ActionSink_MismatchedInstructionKindFailsAtomically()
        {
            var buffer = new GameplayBuffRequestBuffer(1);
            var sink = new GameplayTriggerGameplayBuffActionSink(
                GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                buffer);
            GameplayTriggerActionPreparation preparation = Preparation(
                GameplayTriggerGameplayBuffActionKinds.RemoveComponentBuff,
                GameplayTriggerTargetRef.FromGameplayEntity(new GameplayEntityId(1, 1)),
                BuffId,
                0L,
                0L);

            Assert.IsFalse(sink.TryPrepare(in preparation, out int token));
            Assert.AreEqual(0, token);
            Assert.AreEqual(0, buffer.PreparedReservationCount);
            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void TriggerTransaction_AbortsSharedReservationsInStrictReverseOrder()
        {
            var trace = new List<string>();
            var buffer = new GameplayBuffRequestBuffer(2);
            var apply = new TraceSink(new GameplayTriggerGameplayBuffActionSink(
                GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                buffer), "apply", trace);
            var remove = new TraceSink(new GameplayTriggerGameplayBuffActionSink(
                GameplayTriggerGameplayBuffActionKinds.RemoveComponentBuff,
                buffer), "remove", trace);
            var reject = new RejectingSink(new GameplayTriggerActionKindId(8199));
            GameplayTriggerRuntime runtime = CreateRuntime(
                new IGameplayTriggerActionSink[] { apply, remove, reject },
                new[]
                {
                    Action(apply.ActionKindId),
                    Action(remove.ActionKindId),
                    Action(reject.ActionKindId)
                },
                out _);

            SubmitSignals(runtime, RuntimeFrame.Zero, new GameplayEntityId(1, 1));
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);

            CollectionAssert.AreEqual(
                new[] { "prepare-apply", "prepare-remove", "abort-remove", "abort-apply" },
                trace);
            Assert.AreEqual(0, runtime.ActivationCount);
            Assert.AreEqual(0, runtime.IntentCount);
            Assert.AreEqual(0, buffer.PreparedReservationCount);
            Assert.AreEqual(0, buffer.Count);
            Assert.AreEqual(GameplayTriggerErrorCode.ReservationRejected,
                runtime.GetDiagnostic(0).Code);
        }

        [Test]
        public void TriggerTransaction_TwoActivationsCompeteForOneCommittedSlot()
        {
            var buffer = new GameplayBuffRequestBuffer(1);
            var sink = new GameplayTriggerGameplayBuffActionSink(
                GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                buffer);
            GameplayTriggerRuntime runtime = CreateRuntime(
                new IGameplayTriggerActionSink[] { sink },
                new[] { Action(sink.ActionKindId) },
                out _);
            var first = new GameplayEntityId(1, 1);
            var second = new GameplayEntityId(2, 1);

            SubmitSignals(runtime, RuntimeFrame.Zero, second, first);
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);

            Assert.AreEqual(1, runtime.ActivationCount);
            Assert.AreEqual(first, runtime.GetActivation(0).Occurrence.Subject);
            Assert.AreEqual(1, runtime.IntentCount);
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(1, buffer.CommittedReservationCount);
            Assert.AreEqual(GameplayTriggerErrorCode.ReservationRejected,
                runtime.GetDiagnostic(0).Code);
        }

        [Test]
        public void Pipeline_AppliesRefreshesAndPublishesOneCorrelatedSuccessEachFrame()
        {
            using (BridgeHarness harness = BridgeHarness.Create(
                GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                reservationCapacity: 2,
                observerCapacity: 32))
            {
                GameplayEntityId target = harness.CreateTarget();
                harness.Tick(10, target);
                GameplayTriggerRuntimeEvent applied = harness.TakeOnlyActionResult();
                harness.Tick(11, target);
                GameplayTriggerRuntimeEvent refreshed = harness.TakeOnlyActionResult();

                AssertActionResult(applied, 1L, target,
                    GameplayTriggerRuntimeEventStatus.Succeeded,
                    GameplayBuffApplicationResultReasonCode.Applied,
                    0L, 1L, 12L);
                AssertActionResult(refreshed, 2L, target,
                    GameplayTriggerRuntimeEventStatus.Succeeded,
                    GameplayBuffApplicationResultReasonCode.Refreshed,
                    1L, 1L, 13L);
                AssertBuff(harness.World, target, expected: true);
                Assert.AreEqual(GameplaySystemPhase.Simulation, harness.Evaluation.Phase);
                Assert.AreEqual(1000, harness.Evaluation.Priority);
                Assert.AreEqual(GameplaySystemPhase.Resolution, harness.Application.Phase);
                Assert.AreEqual(50, harness.Application.Priority);
                Assert.AreEqual(GameplaySystemPhase.Diagnostics, harness.Router.Phase);
                Assert.AreEqual(1000, harness.Router.Priority);
            }
        }

        [Test]
        public void Pipeline_RemovesExistingAndTreatsMissingAsSuccessfulNoOp()
        {
            using (BridgeHarness harness = BridgeHarness.Create(
                GameplayTriggerGameplayBuffActionKinds.RemoveComponentBuff,
                reservationCapacity: 2,
                observerCapacity: 32))
            {
                GameplayEntityId target = harness.CreateTarget();
                harness.World.GetOrCreateStore<GameplayComponentBuffSetComponent>().Set(
                    target,
                    new GameplayComponentBuffSetComponent(
                        new GameplayComponentBuffEntry(BuffId, 1, 1L, 20L, false, 9)));

                harness.Tick(10, target);
                GameplayTriggerRuntimeEvent removed = harness.TakeOnlyActionResult();
                harness.Tick(11, target);
                GameplayTriggerRuntimeEvent noOp = harness.TakeOnlyActionResult();

                AssertActionResult(removed, 1L, target,
                    GameplayTriggerRuntimeEventStatus.Succeeded,
                    GameplayBuffApplicationResultReasonCode.Removed,
                    1L, 0L, 0L);
                AssertActionResult(noOp, 2L, target,
                    GameplayTriggerRuntimeEventStatus.Succeeded,
                    GameplayBuffApplicationResultReasonCode.RemoveNoOp,
                    0L, 0L, 0L);
                AssertBuff(harness.World, target, expected: false);
            }
        }

        [Test]
        public void Pipeline_TargetDestroyedAfterCommitRejectsOnceAndKeepsTriggerPolicyCommitted()
        {
            using (BridgeHarness harness = BridgeHarness.Create(
                GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                reservationCapacity: 1,
                observerCapacity: 32,
                maxActivationsPerSubject: 1,
                destroyBeforeApplication: true))
            {
                GameplayEntityId target = harness.CreateTarget();
                harness.Tick(10, target);
                GameplayTriggerRuntimeEvent rejected = harness.TakeOnlyActionResult();

                AssertActionResult(rejected, 1L, target,
                    GameplayTriggerRuntimeEventStatus.Rejected,
                    GameplayBuffApplicationResultReasonCode.MissingComponentEntity,
                    0L, 0L, 0L);
                Assert.AreEqual(1, harness.Runtime.ActivationCount);
                Assert.AreEqual(1, harness.Runtime.IntentCount);
                Assert.AreEqual(2L, harness.Runtime.NextActivationId);
                Assert.AreEqual(0, harness.Buffer.Count);

                harness.Tick(11, target);
                Assert.AreEqual(0, harness.Runtime.ActivationCount);
                Assert.AreEqual(0, harness.Runtime.IntentCount);
                Assert.AreEqual(2L, harness.Runtime.NextActivationId);
                Assert.IsFalse(harness.TryTakeActionResult(out _));
            }
        }

        [Test]
        public void Pipeline_MissingDefinitionAndDurationOverflowAreCorrelatedSemanticRejections()
        {
            using (BridgeHarness missing = BridgeHarness.Create(
                GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                reservationCapacity: 1,
                observerCapacity: 16,
                definitions: new GameplayBuffDefinitionRegistry()))
            {
                GameplayEntityId target = missing.CreateTarget();
                missing.Tick(10, target);
                AssertActionResult(
                    missing.TakeOnlyActionResult(),
                    1L,
                    target,
                    GameplayTriggerRuntimeEventStatus.Rejected,
                    GameplayBuffApplicationResultReasonCode.MissingBuffDefinition,
                    0L, 0L, 0L);
                Assert.AreEqual(1, missing.Runtime.ActivationCount);
            }

            using (BridgeHarness overflow = BridgeHarness.Create(
                GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                reservationCapacity: 1,
                observerCapacity: 16))
            {
                GameplayEntityId target = overflow.CreateTarget();
                overflow.Tick(long.MaxValue, target);
                AssertActionResult(
                    overflow.TakeOnlyActionResult(),
                    1L,
                    target,
                    GameplayTriggerRuntimeEventStatus.Rejected,
                    GameplayBuffApplicationResultReasonCode.DurationOverflow,
                    0L, 0L, 0L);
                Assert.AreEqual(1, overflow.Runtime.ActivationCount);
            }
        }

        [Test]
        public void Pipeline_ObserverOverflowOnlyChangesPresentationDiagnostics()
        {
            using (BridgeHarness harness = BridgeHarness.Create(
                GameplayTriggerGameplayBuffActionKinds.RemoveComponentBuff,
                reservationCapacity: 1,
                observerCapacity: 0))
            {
                GameplayEntityId target = harness.CreateTarget();
                harness.Tick(1, target);

                Assert.AreEqual(1, harness.Runtime.ActivationCount);
                Assert.AreEqual(1, harness.Runtime.IntentCount);
                Assert.AreEqual(2L, harness.Runtime.NextActivationId);
                Assert.Greater(harness.Observer.DroppedCount, 0L);
                Assert.AreEqual(GameplayTriggerRuntimeEventDropReason.ObserverCapacityExceeded,
                    harness.Observer.LastDropReason);
                Assert.AreEqual(0, harness.Buffer.Count);
            }
        }

        [Test]
        public void CheckpointSuffix_RegeneratesDerivedRequestAndResultWithoutAuthorityDuplication()
        {
            using (BridgeHarness baseline = BridgeHarness.Create(
                GameplayTriggerGameplayBuffActionKinds.RemoveComponentBuff,
                reservationCapacity: 1,
                observerCapacity: 32))
            {
                GameplayEntityId baselineTarget = baseline.CreateTarget();
                baseline.Tick(1, baselineTarget);
                GameplayTriggerRuntimeEvent first = baseline.TakeOnlyActionResult();
                Assert.AreEqual(1L, first.ActivationId);

                var sourceParticipant = new GameplayTriggerRuntimeSaveStateParticipant(
                    baseline.Runtime,
                    new AlwaysValidEntity());
                RuntimeSaveStateResult<RuntimeSaveState> captured =
                    sourceParticipant.CaptureSaveState();
                Assert.IsTrue(captured.Success, captured.Error.ToString());
                string checkpointPayload = captured.Value.ModuleStates[0].CustomState.PayloadJson;
                StringAssert.DoesNotContain("GameplayBuff", checkpointPayload);
                StringAssert.DoesNotContain("correlation", checkpointPayload.ToLowerInvariant());

                baseline.Tick(2, baselineTarget);
                GameplayTriggerRuntimeEvent uninterrupted = baseline.TakeOnlyActionResult();
                long uninterruptedHash = ComputeTriggerHash(baseline.Runtime, 2L);
                RuntimeSaveStateResult<RuntimeSaveState> uninterruptedState =
                    sourceParticipant.CaptureSaveState();
                Assert.IsTrue(uninterruptedState.Success, uninterruptedState.Error.ToString());

                using (BridgeHarness resumed = BridgeHarness.Create(
                    GameplayTriggerGameplayBuffActionKinds.RemoveComponentBuff,
                    reservationCapacity: 1,
                    observerCapacity: 32))
                {
                    GameplayEntityId resumedTarget = resumed.CreateTarget();
                    var targetParticipant = new GameplayTriggerRuntimeSaveStateParticipant(
                        resumed.Runtime,
                        new AlwaysValidEntity());
                    RuntimeSaveStateResult<bool> restored =
                        targetParticipant.RestoreSaveState(captured.Value);
                    Assert.IsTrue(restored.Success, restored.Error.ToString());
                    resumed.Buffer.Clear();

                    resumed.Tick(2, resumedTarget);
                    GameplayTriggerRuntimeEvent replayed = resumed.TakeOnlyActionResult();
                    long resumedHash = ComputeTriggerHash(resumed.Runtime, 2L);
                    RuntimeSaveStateResult<RuntimeSaveState> resumedState =
                        targetParticipant.CaptureSaveState();
                    Assert.IsTrue(resumedState.Success, resumedState.Error.ToString());

                    Assert.AreEqual(2L, uninterrupted.ActivationId);
                    Assert.AreEqual(uninterrupted.ActivationId, replayed.ActivationId);
                    Assert.AreEqual(uninterrupted.Status, replayed.Status);
                    Assert.AreEqual(uninterrupted.ReasonCode, replayed.ReasonCode);
                    Assert.AreEqual(uninterruptedHash, resumedHash);
                    Assert.AreEqual(
                        uninterruptedState.Value.ModuleStates[0].CustomState.PayloadJson,
                        resumedState.Value.ModuleStates[0].CustomState.PayloadJson);
                    Assert.AreEqual(3L, baseline.Runtime.NextActivationId);
                    Assert.AreEqual(3L, resumed.Runtime.NextActivationId);
                    Assert.AreEqual(0, resumed.Buffer.Count);
                }
            }
        }

        [Test]
        public void AllocationOracle_BridgeOwnedNonMutatingPipelineIsZeroBytesFor64Ticks()
        {
            using (BridgeHarness harness = BridgeHarness.Create(
                GameplayTriggerGameplayBuffActionKinds.RemoveComponentBuff,
                reservationCapacity: 1,
                observerCapacity: 0))
            {
                GameplayEntityId target = harness.CreateTarget();
                for (int i = 0; i < 8; i++)
                    Assert.IsTrue(harness.TickNoAssert(i + 1L, target));

                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 64; i++)
                {
                    if (!harness.TickNoAssert(i + 100L, target))
                        throw new InvalidOperationException("Unexpected bridge Tick rejection.");
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                TestContext.WriteLine(
                    "Bridge-owned full non-mutating pipeline for 64 ticks: " + allocated + " B");
                Assert.AreEqual(0L, allocated);
            }
        }

        [Test]
        public void AllocationOracle_DisabledInvalidAndFullPrepareAreZeroBytes()
        {
            var fullBuffer = new GameplayBuffRequestBuffer(1);
            var fullSink = new GameplayTriggerGameplayBuffActionSink(
                GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                fullBuffer);
            GameplayTriggerActionPreparation valid = Preparation(
                fullSink.ActionKindId,
                GameplayTriggerTargetRef.FromGameplayEntity(new GameplayEntityId(1, 1)),
                BuffId,
                0L,
                0L);
            Assert.IsTrue(fullSink.TryPrepare(in valid, out int token));
            var intent = new GameplayTriggerActionIntent(1L, valid);
            fullSink.Commit(token, in intent);

            var disabledBuffer = new GameplayBuffRequestBuffer(1);
            var disabledSink = new GameplayTriggerGameplayBuffActionSink(
                GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                disabledBuffer);
            disabledSink.SetEnabled(false);
            var invalidBuffer = new GameplayBuffRequestBuffer(1);
            var invalidSink = new GameplayTriggerGameplayBuffActionSink(
                GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                invalidBuffer);
            GameplayTriggerActionPreparation invalid = Preparation(
                invalidSink.ActionKindId,
                GameplayTriggerTargetRef.FromGameplayEntity(new GameplayEntityId(1, 1)),
                0L,
                0L,
                0L);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                if (fullSink.TryPrepare(in valid, out _) ||
                    disabledSink.TryPrepare(in valid, out _) ||
                    invalidSink.TryPrepare(in invalid, out _))
                {
                    throw new InvalidOperationException("Unexpected Prepare acceptance.");
                }
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void AllocationEvidence_SuccessfulMutationsReportExistingGameplayComponentCost()
        {
            var apply = new AllocationPair[5];
            var refresh = new AllocationPair[5];
            var remove = new AllocationPair[5];
            for (int i = 0; i < apply.Length; i++)
            {
                apply[i] = MeasureApplyAllocation();
                refresh[i] = MeasureRefreshAllocation();
                remove[i] = MeasureRemoveAllocation();
            }

            TestContext.WriteLine(
                "Report-only GC.GetTotalMemory(false) observations; each sample is 64 successful mutations.");
            WriteAllocationEvidence("apply", apply);
            WriteAllocationEvidence("refresh", refresh);
            WriteAllocationEvidence("remove", remove);
        }

        private static void WriteAllocationEvidence(string label, AllocationPair[] samples)
        {
            var direct = new long[samples.Length];
            var bridge = new long[samples.Length];
            var delta = new long[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                direct[i] = samples[i].DirectBytes;
                bridge[i] = samples[i].BridgeBytes;
                delta[i] = samples[i].BridgeBytes - samples[i].DirectBytes;
            }

            Array.Sort(direct);
            Array.Sort(bridge);
            Array.Sort(delta);
            int median = samples.Length / 2;
            TestContext.WriteLine(
                label + " direct samples=" + string.Join(",", direct) +
                " B; median=" + direct[median] + " B; range=" + direct[0] + ".." +
                direct[direct.Length - 1] + " B");
            TestContext.WriteLine(
                label + " bridge samples=" + string.Join(",", bridge) +
                " B; median=" + bridge[median] + " B; range=" + bridge[0] + ".." +
                bridge[bridge.Length - 1] + " B");
            TestContext.WriteLine(
                label + " bridge-minus-direct samples=" + string.Join(",", delta) +
                " B; median=" + delta[median] + " B; range=" + delta[0] + ".." +
                delta[delta.Length - 1] + " B");
        }

        private static AllocationPair MeasureApplyAllocation()
        {
            using (BridgeHarness harness = BridgeHarness.Create(
                GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                reservationCapacity: 1,
                observerCapacity: 0))
            {
                GameplayEntityId target = harness.CreateTarget();
                GameplayComponentStore<GameplayComponentBuffSetComponent> store =
                    harness.World.GetOrCreateStore<GameplayComponentBuffSetComponent>();
                store.Set(target, default);
                harness.DirectApply(1L, target);
                store.Set(target, default);
                Assert.IsTrue(harness.TickNoAssert(2L, target));
                store.Set(target, default);

                long directBytes = 0L;
                long bridgeBytes = 0L;
                long frame = 10L;
                for (int i = 0; i < 64; i++)
                {
                    long before = GC.GetTotalMemory(false);
                    harness.DirectApply(frame++, target);
                    directBytes += GC.GetTotalMemory(false) - before;
                    AssertBuff(harness.World, target, true);
                    store.Set(target, default);
                }

                for (int i = 0; i < 64; i++)
                {
                    long before = GC.GetTotalMemory(false);
                    if (!harness.TickNoAssert(frame++, target))
                        throw new InvalidOperationException("Unexpected bridge apply rejection.");
                    bridgeBytes += GC.GetTotalMemory(false) - before;
                    AssertBuff(harness.World, target, true);
                    store.Set(target, default);
                }

                return new AllocationPair(directBytes, bridgeBytes);
            }
        }

        private static AllocationPair MeasureRefreshAllocation()
        {
            using (BridgeHarness harness = BridgeHarness.Create(
                GameplayTriggerGameplayBuffActionKinds.ApplyComponentBuff,
                reservationCapacity: 1,
                observerCapacity: 0))
            {
                GameplayEntityId target = harness.CreateTarget();
                harness.DirectApply(1L, target);
                harness.DirectApply(2L, target);
                Assert.IsTrue(harness.TickNoAssert(3L, target));

                long directBytes = 0L;
                long bridgeBytes = 0L;
                long frame = 10L;
                for (int i = 0; i < 64; i++)
                {
                    long before = GC.GetTotalMemory(false);
                    harness.DirectApply(frame++, target);
                    directBytes += GC.GetTotalMemory(false) - before;
                    AssertBuff(harness.World, target, true);
                }

                for (int i = 0; i < 64; i++)
                {
                    long before = GC.GetTotalMemory(false);
                    if (!harness.TickNoAssert(frame++, target))
                        throw new InvalidOperationException("Unexpected bridge refresh rejection.");
                    bridgeBytes += GC.GetTotalMemory(false) - before;
                    AssertBuff(harness.World, target, true);
                }

                return new AllocationPair(directBytes, bridgeBytes);
            }
        }

        private static AllocationPair MeasureRemoveAllocation()
        {
            using (BridgeHarness harness = BridgeHarness.Create(
                GameplayTriggerGameplayBuffActionKinds.RemoveComponentBuff,
                reservationCapacity: 1,
                observerCapacity: 0))
            {
                GameplayEntityId target = harness.CreateTarget();
                harness.DirectApply(1L, target);
                harness.DirectRemove(2L, target);
                harness.DirectApply(3L, target);
                Assert.IsTrue(harness.TickNoAssert(4L, target));
                harness.DirectApply(5L, target);

                long directBytes = 0L;
                long bridgeBytes = 0L;
                long frame = 10L;
                for (int i = 0; i < 64; i++)
                {
                    long before = GC.GetTotalMemory(false);
                    harness.DirectRemove(frame++, target);
                    directBytes += GC.GetTotalMemory(false) - before;
                    AssertBuff(harness.World, target, false);
                    harness.DirectApply(frame++, target);
                }

                for (int i = 0; i < 64; i++)
                {
                    long before = GC.GetTotalMemory(false);
                    if (!harness.TickNoAssert(frame++, target))
                        throw new InvalidOperationException("Unexpected bridge remove rejection.");
                    bridgeBytes += GC.GetTotalMemory(false) - before;
                    AssertBuff(harness.World, target, false);
                    harness.DirectApply(frame++, target);
                }

                return new AllocationPair(directBytes, bridgeBytes);
            }
        }

        private readonly struct AllocationPair
        {
            public AllocationPair(long directBytes, long bridgeBytes)
            {
                DirectBytes = directBytes;
                BridgeBytes = bridgeBytes;
            }

            public long DirectBytes { get; }
            public long BridgeBytes { get; }
        }

        private static GameplayTriggerActionPreparation Preparation(
            GameplayTriggerActionKindId actionKindId,
            GameplayTriggerTargetRef target,
            long payload0,
            long payload1,
            long payload2)
        {
            var instruction = new GameplayTriggerActionInstruction(
                actionKindId,
                GameplayTriggerTargetSelector.Subject,
                payload0: payload0,
                payload1: payload1,
                payload2: payload2);
            return new GameplayTriggerActionPreparation(
                RuntimeFrame.Zero,
                new GameplayTriggerBindingId(1),
                new GameplayEntityId(9, 1),
                target,
                new GameplayTriggerRouteId(1),
                0,
                default,
                instruction);
        }

        private static GameplayTriggerActionInstruction Action(
            GameplayTriggerActionKindId actionKindId)
        {
            return new GameplayTriggerActionInstruction(
                actionKindId,
                GameplayTriggerTargetSelector.Subject,
                payload0: BuffId,
                payload1: 17L,
                payload2: 0L);
        }

        private static GameplayTriggerRuntime CreateRuntime(
            IGameplayTriggerActionSink[] sinks,
            GameplayTriggerActionInstruction[] actions,
            out GameplayTriggerRuntimeEventSource eventSource,
            int maxActivationsPerSubject = 0)
        {
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal,
                GameplayTriggerSignalOrdering.UnorderedMultiset);
            var route = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                GameplayTriggerOccurrenceKind.Pulsed,
                GameplayTriggerConditionProgram.Always,
                actions,
                maxActivationsPerSubject > 0
                    ? new GameplayTriggerActivationPolicy(
                        GameplayTriggerStateScope.PerSubject,
                        maxActivations: maxActivationsPerSubject)
                    : default);
            var definition = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(1),
                1,
                source.SourceDefinitionId,
                new[] { route });
            var binding = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(1),
                definition.DefinitionId,
                SourceBindingId,
                new GameplayEntityId(99, 1));
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[] { binding },
                sinks,
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());

            eventSource = new GameplayTriggerRuntimeEventSource(64);
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(
                    8, 32, 32, 32, 64, 32, 32, 32, 8, 16),
                eventSource);
        }

        private static void SubmitSignals(
            GameplayTriggerRuntime runtime,
            RuntimeFrame frame,
            params GameplayEntityId[] subjects)
        {
            var signals = new GameplayTriggerSignal[subjects.Length];
            for (int i = 0; i < subjects.Length; i++)
                signals[i] = new GameplayTriggerSignal(1, subjects[i]);
            Assert.IsTrue(runtime.TrySubmitSignals(
                frame,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                signals,
                out GameplayTriggerResult result),
                result.ErrorCode.ToString());
        }

        private static GameplayBuffDefinitionRegistry Definitions()
        {
            return new GameplayBuffDefinitionRegistry(new GameplayBuffDefinition(
                BuffId,
                "buff.trigger.bridge",
                defaultDurationFrames: 2,
                maxStackCount: 1,
                stackPolicy: GameplayBuffStackPolicy.RefreshDuration,
                effects: Array.Empty<GameplayBuffEffectDefinition>()));
        }

        private static long ComputeTriggerHash(GameplayTriggerRuntime runtime, long frame)
        {
            return RuntimeHashCombiner.ComputeHash(
                new RuntimeFrame(frame),
                new IRuntimeHashContributor[]
                {
                    new GameplayTriggerRuntimeHashContributor(runtime)
                });
        }

        private static void AssertActionResult(
            GameplayTriggerRuntimeEvent evt,
            long activationId,
            GameplayEntityId target,
            GameplayTriggerRuntimeEventStatus status,
            GameplayBuffApplicationResultReasonCode reason,
            long payload0,
            long payload1,
            long payload2)
        {
            Assert.AreEqual(GameplayTriggerRuntimeEventKind.ActionResult, evt.Kind);
            Assert.AreEqual(status, evt.Status);
            Assert.AreEqual(activationId, evt.ActivationId);
            Assert.AreEqual(0, evt.ActionIndex);
            Assert.AreEqual(GameplayTriggerTargetRef.FromGameplayEntity(target), evt.Target);
            Assert.AreEqual((int)reason, evt.ReasonCode);
            Assert.AreEqual(payload0, evt.ResultPayload0);
            Assert.AreEqual(payload1, evt.ResultPayload1);
            Assert.AreEqual(payload2, evt.ResultPayload2);
        }

        private static void AssertBuff(
            GameplayComponentWorld world,
            GameplayEntityId target,
            bool expected)
        {
            bool hasBuff = world.TryGetStore(
                               out GameplayComponentStore<GameplayComponentBuffSetComponent> store) &&
                           store.TryGet(target, out GameplayComponentBuffSetComponent set) &&
                           set.TryGet(BuffId, out _);
            Assert.AreEqual(expected, hasBuff);
        }

        private sealed class RejectingSink : IGameplayTriggerActionSink
        {
            public RejectingSink(GameplayTriggerActionKindId actionKindId)
            {
                ActionKindId = actionKindId;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;
            public bool TryPrepare(in GameplayTriggerActionPreparation preparation, out int token)
            {
                token = 0;
                return false;
            }
            public void Abort(int token) { }
            public void Commit(int token, in GameplayTriggerActionIntent intent) { }
        }

        private sealed class TraceSink : IGameplayTriggerActionSink
        {
            private readonly IGameplayTriggerActionSink _inner;
            private readonly string _id;
            private readonly List<string> _trace;

            public TraceSink(IGameplayTriggerActionSink inner, string id, List<string> trace)
            {
                _inner = inner;
                _id = id;
                _trace = trace;
            }

            public GameplayTriggerActionKindId ActionKindId => _inner.ActionKindId;
            public GameplayTriggerDeliveryContract DeliveryContract => _inner.DeliveryContract;
            public bool TryPrepare(in GameplayTriggerActionPreparation preparation, out int token)
            {
                _trace.Add("prepare-" + _id);
                return _inner.TryPrepare(in preparation, out token);
            }
            public void Abort(int token)
            {
                _trace.Add("abort-" + _id);
                _inner.Abort(token);
            }
            public void Commit(int token, in GameplayTriggerActionIntent intent)
            {
                _trace.Add("commit-" + _id);
                _inner.Commit(token, in intent);
            }
        }

        private sealed class DestroyBeforeBuffSystem : IGameplaySystem
        {
            public string SystemId => "test.destroy-before-buff";
            public GameplaySystemPhase Phase => GameplaySystemPhase.Resolution;
            public int Priority => 40;
            public bool IsEnabled { get; private set; } = true;
            public GameplayEntityId Target { get; set; }
            public void SetEnabled(bool enabled) => IsEnabled = enabled;
            public void Tick(GameplaySystemContext context)
            {
                if (Target.IsValid && context.ComponentWorld != null)
                    context.ComponentWorld.DestroyEntity(Target);
            }
        }

        private sealed class AlwaysValidEntity : IGameplayTriggerRestoreEntityValidator
        {
            public bool IsRestoredEntityValid(GameplayEntityId entityId)
            {
                return entityId.IsValid;
            }
        }

        private sealed class BridgeHarness : IDisposable
        {
            private readonly GameplaySystemPipeline _pipeline;
            private readonly GameplayTriggerSignal[] _signals = new GameplayTriggerSignal[1];
            private readonly List<GameplayTriggerRuntimeEvent> _actionResults =
                new List<GameplayTriggerRuntimeEvent>();
            private readonly List<GameplayRuntimeEvent> _gameplayEvents =
                new List<GameplayRuntimeEvent>(1);
            private readonly DestroyBeforeBuffSystem _destroySystem;
            private readonly GameplayWorld _gameplayWorld = new GameplayWorld();
            private readonly GameplayCommandExecutionState _commandState =
                new GameplayCommandExecutionState();

            private BridgeHarness(
                GameplayComponentWorld world,
                GameplayBuffRequestBuffer buffer,
                GameplayTriggerRuntime runtime,
                GameplayTriggerEvaluationSystem evaluation,
                GameplayBuffApplicationSystem application,
                GameplayTriggerEventRouterSystem router,
                GameplayTriggerRuntimeEventObserverBuffer observer,
                GameplaySystemPipeline pipeline,
                DestroyBeforeBuffSystem destroySystem)
            {
                World = world;
                Buffer = buffer;
                Runtime = runtime;
                Evaluation = evaluation;
                Application = application;
                Router = router;
                Observer = observer;
                _pipeline = pipeline;
                _destroySystem = destroySystem;
            }

            public GameplayComponentWorld World { get; }
            public GameplayBuffRequestBuffer Buffer { get; }
            public GameplayTriggerRuntime Runtime { get; }
            public GameplayTriggerEvaluationSystem Evaluation { get; }
            public GameplayBuffApplicationSystem Application { get; }
            public GameplayTriggerEventRouterSystem Router { get; }
            public GameplayTriggerRuntimeEventObserverBuffer Observer { get; }

            public static BridgeHarness Create(
                GameplayTriggerActionKindId actionKindId,
                int reservationCapacity,
                int observerCapacity,
                int maxActivationsPerSubject = 0,
                bool destroyBeforeApplication = false,
                GameplayBuffDefinitionRegistry definitions = null)
            {
                var world = new GameplayComponentWorld();
                var buffer = new GameplayBuffRequestBuffer(reservationCapacity);
                var sink = new GameplayTriggerGameplayBuffActionSink(actionKindId, buffer);
                GameplayTriggerRuntime runtime = CreateRuntime(
                    new IGameplayTriggerActionSink[] { sink },
                    new[] { Action(actionKindId) },
                    out GameplayTriggerRuntimeEventSource source,
                    maxActivationsPerSubject);
                var relay = new GameplayTriggerGameplayBuffResultRelay(source);
                var observer = new GameplayTriggerRuntimeEventObserverBuffer(observerCapacity);
                var evaluation = new GameplayTriggerEvaluationSystem(runtime);
                var application = new GameplayBuffApplicationSystem(
                    definitions ?? Definitions(),
                    buffer,
                    resultSink: relay);
                var router = new GameplayTriggerEventRouterSystem(
                    source,
                    new[]
                    {
                        new GameplayTriggerRuntimeEventObserverRegistration(
                            "bridge-test",
                            observer)
                    });
                var pipeline = new GameplaySystemPipeline();
                pipeline.Add(evaluation);
                DestroyBeforeBuffSystem destroy = null;
                if (destroyBeforeApplication)
                {
                    destroy = new DestroyBeforeBuffSystem();
                    pipeline.Add(destroy);
                }
                pipeline.Add(application);
                pipeline.Add(router);
                return new BridgeHarness(
                    world,
                    buffer,
                    runtime,
                    evaluation,
                    application,
                    router,
                    observer,
                    pipeline,
                    destroy);
            }

            public GameplayEntityId CreateTarget()
            {
                return World.CreateEntity();
            }

            public void Tick(long frameValue, GameplayEntityId target)
            {
                Assert.IsTrue(TickNoAssert(frameValue, target));
            }

            public void DirectApply(long frameValue, GameplayEntityId target)
            {
                Buffer.EnqueueApply(target, BuffId);
                TickApplication(frameValue);
            }

            public void DirectRemove(long frameValue, GameplayEntityId target)
            {
                Buffer.EnqueueRemove(target, BuffId);
                TickApplication(frameValue);
            }

            public bool TickNoAssert(long frameValue, GameplayEntityId target)
            {
                var frame = new RuntimeFrame(frameValue);
                _signals[0] = new GameplayTriggerSignal(1, target);
                if (!Runtime.TrySubmitSignals(
                    frame,
                    SourceBindingId,
                    GameplayTriggerSignalOrdering.UnorderedMultiset,
                    _signals,
                    out _))
                {
                    return false;
                }
                if (_destroySystem != null)
                    _destroySystem.Target = target;
                _pipeline.Tick(new GameplaySystemContext(
                    frame,
                    0d,
                    0d,
                    _gameplayWorld,
                    Array.Empty<RuntimeCommand>(),
                    World.Events,
                    _commandState,
                    componentWorld: World));
                DrainGameplayEvents(frame);
                return true;
            }

            private void TickApplication(long frameValue)
            {
                var frame = new RuntimeFrame(frameValue);
                Application.Tick(new GameplaySystemContext(
                    frame,
                    0d,
                    0d,
                    _gameplayWorld,
                    Array.Empty<RuntimeCommand>(),
                    World.Events,
                    _commandState,
                    componentWorld: World));
                DrainGameplayEvents(frame);
            }

            private void DrainGameplayEvents(RuntimeFrame frame)
            {
                _gameplayEvents.Clear();
                World.DrainEvents(frame, _gameplayEvents);
            }

            public GameplayTriggerRuntimeEvent TakeOnlyActionResult()
            {
                _actionResults.Clear();
                while (Observer.TryDequeue(out GameplayTriggerRuntimeEvent evt))
                {
                    if (evt.Kind == GameplayTriggerRuntimeEventKind.ActionResult)
                        _actionResults.Add(evt);
                }
                Assert.AreEqual(1, _actionResults.Count);
                return _actionResults[0];
            }

            public bool TryTakeActionResult(out GameplayTriggerRuntimeEvent result)
            {
                while (Observer.TryDequeue(out GameplayTriggerRuntimeEvent evt))
                {
                    if (evt.Kind != GameplayTriggerRuntimeEventKind.ActionResult)
                        continue;
                    result = evt;
                    return true;
                }
                result = default;
                return false;
            }

            public void Dispose()
            {
                Buffer.Clear();
                Observer.Clear();
            }
        }
    }
}
