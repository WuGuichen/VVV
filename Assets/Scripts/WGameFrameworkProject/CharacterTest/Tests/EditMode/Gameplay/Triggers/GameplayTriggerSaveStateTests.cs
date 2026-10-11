using System;
using System.Collections.Generic;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerSaveStateTests
    {
        private static readonly GameplayTriggerSourceBindingId SourceBinding = new GameplayTriggerSourceBindingId(100);
        private static readonly GameplayTriggerActionKindId ActionKind = new GameplayTriggerActionKindId(1000);
        private static readonly GameplayEntityId Owner = new GameplayEntityId(1, 1);
        private static readonly GameplayEntityId Subject = new GameplayEntityId(2, 1);
        private static readonly GameplayEntityId OtherSubject = new GameplayEntityId(3, 1);
        private static readonly GameplayEntityId Instigator = new GameplayEntityId(4, 1);
        private const string TriggerPayloadPath = "$.moduleStates[mxframework.gameplay.triggers].customState.payloadJson";

        [Test]
        public void CaptureRestoreRoundtrip_PreservesCanonicalPayloadHashNextIdAndContinuation()
        {
            GameplayTriggerRuntime source = CreateOccupancyRuntime();
            SubmitOccupancy(source, 0L, Subject);
            var sourceParticipant = Participant(source);

            RuntimeSaveState captured = AssertCapture(sourceParticipant);
            string canonical = PayloadJson(captured);
            RuntimeSaveStateResult<RuntimeSaveState> loaded = RuntimeSaveStateJson.LoadFromJson(
                RuntimeSaveStateJson.SaveToJson(captured));
            Assert.IsTrue(loaded.Success, loaded.Error.ToString());
            Assert.AreEqual(canonical, PayloadJson(loaded.Value));
            long expectedHash = ComputeHash(source);
            long expectedNext = source.NextActivationId;
            Assert.AreEqual(1L, source.GetRouteAuthorityState(0).LastActivatedPresenceEpoch, "TRG-SAVE-01 requires PerSubject OncePerPresence authority.");
            GameplayTriggerRuntime target = CreateOccupancyRuntime();
            var targetParticipant = Participant(target);

            RuntimeSaveStateResult<bool> restore = targetParticipant.RestoreSaveState(loaded.Value);

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(canonical, PayloadJson(AssertCapture(targetParticipant)));
            Assert.AreEqual(expectedHash, ComputeHash(target));
            Assert.AreEqual(expectedNext, target.NextActivationId);

            SubmitOccupancy(source, 1L, Subject);
            SubmitOccupancy(target, 1L, Subject);
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Stayed, source.GetOccurrence(0).Kind);
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Stayed, target.GetOccurrence(0).Kind);
            Assert.AreEqual(0, source.ActivationCount, "OncePerPresence must reject a repeated activation in the same presence.");
            Assert.AreEqual(0, target.ActivationCount, "Restored OncePerPresence authority must reject the same repeated activation.");
            Assert.AreEqual(expectedNext, source.NextActivationId);
            Assert.AreEqual(expectedNext, target.NextActivationId);
            Assert.AreEqual(PayloadJson(AssertCapture(sourceParticipant)), PayloadJson(AssertCapture(targetParticipant)));
            Assert.AreEqual(ComputeHash(source), ComputeHash(target));
            Assert.AreEqual(source.NextActivationId, target.NextActivationId);
        }

        [Test]
        public void Restore_GlobalThresholdDefaultSubject_DoesNotCallEntityValidatorForDefaultRelation()
        {
            GameplayTriggerRuntime source = CreateThresholdRuntime();
            Assert.IsTrue(source.TrySubmitScalarBatch(
                RuntimeFrame.Zero,
                SourceBinding,
                new[] { new GameplayTriggerScalarSample(default, Fix64.FromInt(10)) },
                out GameplayTriggerResult submit), submit.ErrorCode.ToString());
            Assert.IsTrue(source.Evaluate(RuntimeFrame.Zero).Success);
            RuntimeSaveState captured = AssertCapture(Participant(source));
            var validator = new RecordingValidator();
            GameplayTriggerRuntime target = CreateThresholdRuntime();

            RuntimeSaveStateResult<bool> restore = new GameplayTriggerRuntimeSaveStateParticipant(target, validator)
                .RestoreSaveState(captured);

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(1, validator.CallCount, "Only the non-default binding owner should be validated.");
            Assert.AreEqual(default(GameplayEntityId), target.GetRelationAuthorityState(0).Subject);
            Assert.AreEqual(ComputeHash(source), ComputeHash(target));
        }

        [Test]
        public void Restore_AcceptsSaturatedStayScheduleAtLongMaxValue()
        {
            GameplayTriggerRuntime source = CreateOccupancyRuntime(oncePerPresence: false);
            SubmitOccupancy(source, long.MaxValue - 2L, Subject);
            SubmitOccupancy(source, long.MaxValue - 1L, Subject);
            Assert.IsTrue(source.GetRouteAuthorityState(0).HasStaySchedule);
            Assert.AreEqual(long.MaxValue, source.GetRouteAuthorityState(0).NextStayFrame);
            RuntimeSaveState captured = AssertCapture(Participant(source));
            GameplayTriggerRuntime target = CreateOccupancyRuntime(oncePerPresence: false);

            RuntimeSaveStateResult<bool> restore = Participant(target).RestoreSaveState(captured);

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(long.MaxValue, target.GetRouteAuthorityState(0).NextStayFrame);
            Assert.AreEqual(ComputeHash(source), ComputeHash(target));
        }

        [Test]
        public void PendingClassifier_UsesRealBatchFirstAndPureSeamCoversEveryDefensiveDetail()
        {
            Assert.AreEqual(GameplayTriggerCheckpointPendingDetailCode.PendingInputBatches, GameplayTriggerRuntime.ClassifyCheckpointPendingWork(1, 0, 0, 0));
            Assert.AreEqual(GameplayTriggerCheckpointPendingDetailCode.PendingOccupancyItems, GameplayTriggerRuntime.ClassifyCheckpointPendingWork(0, 1, 0, 0));
            Assert.AreEqual(GameplayTriggerCheckpointPendingDetailCode.PendingScalarItems, GameplayTriggerRuntime.ClassifyCheckpointPendingWork(0, 0, 1, 0));
            Assert.AreEqual(GameplayTriggerCheckpointPendingDetailCode.PendingSignalItems, GameplayTriggerRuntime.ClassifyCheckpointPendingWork(0, 0, 0, 1));
            Assert.AreEqual(GameplayTriggerCheckpointPendingDetailCode.PendingInputBatches, GameplayTriggerRuntime.ClassifyCheckpointPendingWork(1, 1, 1, 1));

            GameplayTriggerRuntime runtime = CreateOccupancyRuntime();
            Assert.IsTrue(runtime.TrySubmitOccupancy(RuntimeFrame.Zero, SourceBinding, new[] { Subject }, out _));
            var participant = Participant(runtime);

            RuntimeCheckpointQuiescenceResult quiescence = participant.CheckQuiescence(new RuntimeCheckpointContext(0L));
            RuntimeSaveStateResult<RuntimeSaveState> capture = participant.CaptureSaveState();

            Assert.IsFalse(quiescence.IsReady);
            Assert.AreEqual((int)GameplayTriggerCheckpointPendingDetailCode.PendingInputBatches, quiescence.DetailCode);
            Assert.IsFalse(capture.Success);
            Assert.IsNull(capture.Value);
            Assert.AreEqual(RuntimeSaveStateErrorCode.PendingWork, capture.Error.Code);
            Assert.AreEqual((int)GameplayTriggerCheckpointPendingDetailCode.PendingInputBatches, capture.Error.DetailCode);
        }

        [Test]
        public void RuntimeCheckpointBarrier_ReadyNormalizesRootFrameAndPendingSkipsProvider()
        {
            GameplayTriggerRuntime runtime = CreateOccupancyRuntime();
            var participant = Participant(runtime);
            RuntimeSaveStateResult<RuntimeSaveState> directCapture = participant.CaptureSaveState();
            Assert.IsTrue(directCapture.Success, directCapture.Error?.ToString());
            string expectedOpaquePayload = directCapture.Value.ModuleStates[0].CustomState.PayloadJson;
            var provider = new CountingProvider(participant);
            var saveRegistry = new RuntimeSaveStateRegistry();
            Assert.IsTrue(saveRegistry.Register(
                "a-trigger",
                GameplayTriggerRuntimeSaveStateParticipant.DefaultSaveStateOrder,
                provider,
                null).Success);
            var descriptor = new RuntimeSaveStateRootHeaderDescriptor(
                RuntimeSaveState.CurrentSchemaVersion,
                "framework-test",
                "config-test",
                "resources-test");
            Assert.IsTrue(saveRegistry.Register(
                new RuntimeSaveStateRootHeaderProvider(descriptor).CreateParticipant()).Success);
            var quiescenceRegistry = new RuntimeCheckpointQuiescenceRegistry();
            Assert.IsTrue(quiescenceRegistry.Register(
                "a-trigger",
                GameplayTriggerRuntimeSaveStateParticipant.DefaultQuiescenceOrder,
                participant).Success);
            var host = new RuntimeHost();
            host.Initialize();
            host.Start();
            host.Tick(42L, 0.016d);
            RuntimeSaveStateCoordinatorFactoryResult factory =
                RuntimeSaveStateCoordinator.CreateAuthoritative(saveRegistry);
            Assert.IsTrue(factory.Success, factory.Error?.ToString());
            var barrier = new RuntimeCheckpointBarrier(
                host,
                factory.Coordinator,
                quiescenceRegistry);

            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> ready = barrier.CaptureCheckpoint();
            Assert.IsTrue(ready.Success, ready.FirstError?.ToString());
            Assert.AreEqual(42L, ready.Value.Frame);
            Assert.AreEqual(descriptor.FrameworkVersion, ready.Value.FrameworkVersion);
            Assert.AreEqual(descriptor.ConfigVersion, ready.Value.ConfigVersion);
            Assert.AreEqual(descriptor.ResourceCatalogVersion, ready.Value.ResourceCatalogVersion);
            Assert.AreEqual(1, ready.Value.ModuleStates.Count);
            Assert.AreEqual(
                GameplayTriggerRuntimeSaveStateParticipant.TypeId,
                ready.Value.ModuleStates[0].CustomState.TypeId);
            Assert.AreEqual(
                expectedOpaquePayload,
                ready.Value.ModuleStates[0].CustomState.PayloadJson,
                "Runtime root composition must preserve Trigger payload as opaque JSON.");
            Assert.AreEqual(1, provider.CaptureCount);

            Assert.IsTrue(runtime.TrySubmitOccupancy(new RuntimeFrame(43L), SourceBinding, new[] { Subject }, out _));
            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> pending = barrier.CaptureCheckpoint();

            Assert.IsFalse(pending.Success);
            Assert.IsNull(pending.Value);
            Assert.AreEqual(1, provider.CaptureCount, "F1 must not invoke any provider after a pending checker.");
            Assert.AreEqual(RuntimeSaveStateErrorCode.PendingWork, pending.FirstError.Error.Code);
            Assert.AreEqual((int)GameplayTriggerCheckpointPendingDetailCode.PendingInputBatches, pending.FirstError.Error.DetailCode);
        }

        [Test]
        public void CheckQuiescence_ReadyPathDoesNotAllocateAcross128Calls()
        {
            var participant = Participant(CreateOccupancyRuntime());
            var context = new RuntimeCheckpointContext(1L);
            CheckReady128(participant, in context);
            long probeStart = GC.GetAllocatedBytesForCurrentThread();
            long probeEnd = GC.GetAllocatedBytesForCurrentThread();
            long probeOverhead = probeEnd - probeStart;

            long before = GC.GetAllocatedBytesForCurrentThread();
            CheckReady128(participant, in context);

            long measured = GC.GetAllocatedBytesForCurrentThread() - before - probeOverhead;
            Assert.AreEqual(0L, measured);
        }

        [Test]
        public void CoordinatorRestore_WorldOrder100PrecedesTriggerOrder200DespiteLexicalReverse()
        {
            var sourceWorld = new GameplayComponentWorld();
            Assert.AreEqual(Owner, sourceWorld.CreateEntity());
            Assert.AreEqual(Subject, sourceWorld.CreateEntity());
            GameplayTriggerRuntime sourceRuntime = CreateOccupancyRuntime();
            SubmitOccupancy(sourceRuntime, 0L, Subject);
            var sourceRegistry = new RuntimeSaveStateRegistry();
            sourceRegistry.Register("z-world", 100, new GameplayComponentWorldSaveStateProvider(sourceWorld), null);
            sourceRegistry.Register("a-trigger", 200, Participant(sourceRuntime), null);
            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> captured =
                new RuntimeSaveStateCoordinator(sourceRegistry).CaptureSaveState();
            Assert.IsTrue(captured.Success, captured.FirstError?.ToString());

            var targetWorld = new GameplayComponentWorld();
            GameplayTriggerRuntime targetRuntime = CreateOccupancyRuntime();
            var targetRegistry = new RuntimeSaveStateRegistry();
            targetRegistry.Register(
                "a-trigger",
                200,
                null,
                new GameplayTriggerRuntimeSaveStateParticipant(targetRuntime, new WorldValidator(targetWorld)));
            targetRegistry.Register("z-world", 100, null, new GameplayComponentWorldSaveStateProvider(targetWorld));

            RuntimeSaveStateCoordinatorResult<bool> restored =
                new RuntimeSaveStateCoordinator(targetRegistry).RestoreSaveState(captured.Value);

            Assert.IsTrue(restored.Success, restored.FirstError?.ToString());
            Assert.IsTrue(targetWorld.IsAlive(Owner));
            Assert.IsTrue(targetWorld.IsAlive(Subject));
            Assert.AreEqual(ComputeHash(sourceRuntime), ComputeHash(targetRuntime));
        }

        [Test]
        public void Capture_CanonicalBytesIgnoreRelationAndRouteBackingHistoryPermutation()
        {
            GameplayTriggerRuntime first = CreateOccupancyRuntime();
            GameplayTriggerRuntime second = CreateOccupancyRuntime();
            SubmitOccupancy(first, 0L, Subject);
            SubmitOccupancy(second, 0L, OtherSubject);
            SubmitOccupancy(first, 1L, new[] { Subject, OtherSubject });
            SubmitOccupancy(second, 1L, new[] { Subject, OtherSubject });
            SubmitOccupancy(first, 2L, Array.Empty<GameplayEntityId>());
            SubmitOccupancy(second, 2L, Array.Empty<GameplayEntityId>());
            SubmitOccupancy(first, 3L, new[] { Subject, OtherSubject });
            SubmitOccupancy(second, 3L, new[] { Subject, OtherSubject });
            Assert.AreNotEqual(
                first.GetRelationAuthorityState(0).Subject,
                second.GetRelationAuthorityState(0).Subject,
                "Fixture must prove distinct live relation backing history before comparing canonical output.");
            Assert.AreNotEqual(
                first.GetRouteAuthorityState(0).Subject,
                second.GetRouteAuthorityState(0).Subject,
                "Fixture must prove distinct live route-state backing history before comparing canonical output.");

            string firstPayload = PayloadJson(AssertCapture(Participant(first)));
            string secondPayload = PayloadJson(AssertCapture(Participant(second)));

            Assert.AreEqual(firstPayload, secondPayload);
            Assert.AreEqual(ComputeHash(first), ComputeHash(second));
        }

        [Test]
        public void RestoreNull_ReturnsStructuredMissingSaveStateFailure()
        {
            RuntimeSaveStateResult<bool> result = Participant(CreateOccupancyRuntime()).RestoreSaveState(null);

            AssertFailure(result, RuntimeSaveStateErrorCode.CustomStateMismatch, GameplayTriggerSaveStateErrorDetailCode.SaveStateMissing);
        }

        [Test]
        public void RestoreMissingAndDuplicateModule_ReturnStructuredFailures()
        {
            var participant = Participant(CreateOccupancyRuntime());
            RuntimeSaveState empty = Root(0L, Array.Empty<RuntimeModuleSaveState>());
            RuntimeModuleSaveState module = AssertCapture(Participant(CreateOccupancyRuntime())).ModuleStates[0];
            RuntimeSaveState duplicate = Root(0L, new[] { module, module });

            AssertFailure(participant.RestoreSaveState(empty), RuntimeSaveStateErrorCode.CustomStateMismatch, GameplayTriggerSaveStateErrorDetailCode.MissingModuleState);
            AssertFailure(participant.RestoreSaveState(duplicate), RuntimeSaveStateErrorCode.CustomStateMismatch, GameplayTriggerSaveStateErrorDetailCode.DuplicateModuleState);
        }

        [Test]
        public void Restore_ModuleAndCustomContractsUseStableMismatchDetails()
        {
            RuntimeSaveState valid = AssertCapture(Participant(CreateOccupancyRuntime()));
            RuntimeModuleSaveState module = valid.ModuleStates[0];
            RuntimeCustomState custom = module.CustomState;
            var participant = Participant(CreateOccupancyRuntime());

            AssertFailure(
                participant.RestoreSaveState(Root(valid.Frame, new[] { new RuntimeModuleSaveState(module.ModuleId, 4, custom) })),
                RuntimeSaveStateErrorCode.CustomStateMismatch,
                GameplayTriggerSaveStateErrorDetailCode.ModuleSchemaMismatch);
            AssertFailure(
                participant.RestoreSaveState(Root(valid.Frame, new[] { new RuntimeModuleSaveState(module.ModuleId, module.SchemaVersion, null) })),
                RuntimeSaveStateErrorCode.CustomStateMismatch,
                GameplayTriggerSaveStateErrorDetailCode.MissingCustomState);
            AssertFailure(
                participant.RestoreSaveState(Root(valid.Frame, new[]
                {
                    new RuntimeModuleSaveState(module.ModuleId, module.SchemaVersion, new RuntimeCustomState(999999, "wrong.type", custom.SchemaVersion, custom.PayloadJson))
                })),
                RuntimeSaveStateErrorCode.CustomStateMismatch,
                GameplayTriggerSaveStateErrorDetailCode.CustomStateTypeMismatch);
            AssertFailure(
                participant.RestoreSaveState(Root(valid.Frame, new[]
                {
                    new RuntimeModuleSaveState(module.ModuleId, module.SchemaVersion, new RuntimeCustomState(custom.NumericTypeId, custom.TypeId, 4, custom.PayloadJson))
                })),
                RuntimeSaveStateErrorCode.CustomStateMismatch,
                GameplayTriggerSaveStateErrorDetailCode.CustomStateSchemaMismatch);
        }

        [TestCase(StrictJsonFailure.DuplicateProperty)]
        [TestCase(StrictJsonFailure.TrailingRoot)]
        [TestCase(StrictJsonFailure.Comment)]
        [TestCase(StrictJsonFailure.UnknownProperty)]
        [TestCase(StrictJsonFailure.MissingProperty)]
        [TestCase(StrictJsonFailure.NullProperty)]
        [TestCase(StrictJsonFailure.NumericOverflow)]
        public void Restore_StrictJsonRejectsMalformedContract(StrictJsonFailure failure)
        {
            RuntimeSaveState valid = AssertCapture(Participant(CreateOccupancyRuntime()));
            string json = PayloadJson(valid);
            JObject payload = JObject.Parse(json);
            switch (failure)
            {
                case StrictJsonFailure.DuplicateProperty:
                    json = json.Insert(1, "\"schemaVersion\":1,");
                    break;
                case StrictJsonFailure.TrailingRoot:
                    json += "{}";
                    break;
                case StrictJsonFailure.Comment:
                    json = json.Insert(1, "/*not accepted*/");
                    break;
                case StrictJsonFailure.UnknownProperty:
                    payload["unknown"] = 1;
                    json = payload.ToString(Formatting.None);
                    break;
                case StrictJsonFailure.MissingProperty:
                    payload.Remove("instances");
                    json = payload.ToString(Formatting.None);
                    break;
                case StrictJsonFailure.NullProperty:
                    payload["options"] = JValue.CreateNull();
                    json = payload.ToString(Formatting.None);
                    break;
                case StrictJsonFailure.NumericOverflow:
                    json = json.Replace("\"nextActivationId\":1", "\"nextActivationId\":999999999999999999999999999999999999999");
                    break;
            }

            RuntimeSaveStateResult<bool> result = Participant(CreateOccupancyRuntime()).RestoreSaveState(WithPayload(valid, json));

            AssertFailure(result, RuntimeSaveStateErrorCode.InvalidDocument, GameplayTriggerSaveStateErrorDetailCode.PayloadJsonInvalid);
        }

        [Test]
        public void Restore_PayloadSchemaMismatchIsNotReportedAsJsonFailure()
        {
            RuntimeSaveState valid = AssertCapture(Participant(CreateOccupancyRuntime()));
            JObject payload = JObject.Parse(PayloadJson(valid));
            payload["schemaVersion"] = 2;

            RuntimeSaveStateResult<bool> result = Participant(CreateOccupancyRuntime()).RestoreSaveState(
                WithPayload(valid, payload.ToString(Formatting.None)));

            AssertFailure(result, RuntimeSaveStateErrorCode.CustomStateMismatch, GameplayTriggerSaveStateErrorDetailCode.PayloadSchemaMismatch);
        }

        [Test]
        public void Restore_RejectsCatalogOptionsAndRootFrameMismatches()
        {
            RuntimeSaveState valid = AssertCapture(Participant(CreateOccupancyRuntime()));
            JObject hashPayload = JObject.Parse(PayloadJson(valid));
            hashPayload["definitionCatalogHash"] = hashPayload.Value<long>("definitionCatalogHash") + 1L;
            JObject bindingPayload = JObject.Parse(PayloadJson(valid));
            bindingPayload["bindingCatalogHash"] = bindingPayload.Value<long>("bindingCatalogHash") + 1L;
            JObject combinedPayload = JObject.Parse(PayloadJson(valid));
            combinedPayload["gameplayTriggerCatalogHash"] = combinedPayload.Value<long>("gameplayTriggerCatalogHash") + 1L;

            AssertFailure(
                Participant(CreateOccupancyRuntime()).RestoreSaveState(WithPayload(valid, hashPayload.ToString(Formatting.None))),
                RuntimeSaveStateErrorCode.MissingConfig,
                GameplayTriggerSaveStateErrorDetailCode.DefinitionCatalogHashMismatch);
            AssertFailure(
                Participant(CreateOccupancyRuntime()).RestoreSaveState(WithPayload(valid, bindingPayload.ToString(Formatting.None))),
                RuntimeSaveStateErrorCode.MissingConfig,
                GameplayTriggerSaveStateErrorDetailCode.BindingCatalogHashMismatch);
            AssertFailure(
                Participant(CreateOccupancyRuntime()).RestoreSaveState(WithPayload(valid, combinedPayload.ToString(Formatting.None))),
                RuntimeSaveStateErrorCode.MissingConfig,
                GameplayTriggerSaveStateErrorDetailCode.GameplayTriggerCatalogHashMismatch);
            AssertFailure(
                Participant(CreateOccupancyRuntime()).RestoreSaveState(Root(long.MaxValue, valid.ModuleStates)),
                RuntimeSaveStateErrorCode.InvalidDocument,
                GameplayTriggerSaveStateErrorDetailCode.EvaluationStateInvalid);
        }

        [Test]
        public void Restore_AllRuntimeOptionFieldsUseConstructorOrderDetails()
        {
            RuntimeSaveState valid = AssertCapture(Participant(CreateOccupancyRuntime()));
            string[] names =
            {
                "maxInputBatches", "maxInputItems", "maxRelations", "maxOccurrences", "maxCandidates",
                "maxRouteStates", "maxActivations", "maxIntents", "maxPreparedActions", "maxConditionStackDepth",
                "maxDeferredRecords"
            };
            GameplayTriggerSaveStateErrorDetailCode[] details =
            {
                GameplayTriggerSaveStateErrorDetailCode.MaxInputBatchesMismatch,
                GameplayTriggerSaveStateErrorDetailCode.MaxInputItemsMismatch,
                GameplayTriggerSaveStateErrorDetailCode.MaxRelationsMismatch,
                GameplayTriggerSaveStateErrorDetailCode.MaxOccurrencesMismatch,
                GameplayTriggerSaveStateErrorDetailCode.MaxCandidatesMismatch,
                GameplayTriggerSaveStateErrorDetailCode.MaxRouteStatesMismatch,
                GameplayTriggerSaveStateErrorDetailCode.MaxActivationsMismatch,
                GameplayTriggerSaveStateErrorDetailCode.MaxIntentsMismatch,
                GameplayTriggerSaveStateErrorDetailCode.MaxPreparedActionsMismatch,
                GameplayTriggerSaveStateErrorDetailCode.MaxConditionStackDepthMismatch,
                GameplayTriggerSaveStateErrorDetailCode.MaxDeferredRecordsMismatch
            };

            for (int i = 0; i < names.Length; i++)
            {
                JObject payload = JObject.Parse(PayloadJson(valid));
                payload["options"][names[i]] = payload["options"].Value<int>(names[i]) + 1;
                RuntimeSaveStateResult<bool> result = Participant(CreateOccupancyRuntime()).RestoreSaveState(
                    WithPayload(valid, payload.ToString(Formatting.None)));
                AssertFailure(result, RuntimeSaveStateErrorCode.MissingConfig, details[i]);
            }
        }

        [Test]
        public void Restore_RejectsDuplicateRelationsAndActiveCountMismatch()
        {
            GameplayTriggerRuntime source = CreateOccupancyRuntime();
            SubmitOccupancy(source, 0L, Subject);
            RuntimeSaveState valid = AssertCapture(Participant(source));
            JObject duplicate = JObject.Parse(PayloadJson(valid));
            ((JArray)duplicate["relations"]).Add(((JArray)duplicate["relations"])[0].DeepClone());
            JObject count = JObject.Parse(PayloadJson(valid));
            count["instances"][0]["activeCount"] = 2;

            AssertFailure(
                Participant(CreateOccupancyRuntime()).RestoreSaveState(WithPayload(valid, duplicate.ToString(Formatting.None))),
                RuntimeSaveStateErrorCode.InvalidDocument,
                GameplayTriggerSaveStateErrorDetailCode.DuplicateRelation);
            AssertFailure(
                Participant(CreateOccupancyRuntime()).RestoreSaveState(WithPayload(valid, count.ToString(Formatting.None))),
                RuntimeSaveStateErrorCode.InvalidDocument,
                GameplayTriggerSaveStateErrorDetailCode.ActiveCountMismatch);
        }

        [Test]
        public void Restore_RejectsDuplicateAndNPlusOneInstanceAndRouteSets()
        {
            RuntimeSaveState twoInstances = AssertCapture(Participant(CreateTwoBindingRuntime()));
            JObject duplicateInstance = JObject.Parse(PayloadJson(twoInstances));
            duplicateInstance["instances"][1] = duplicateInstance["instances"][0].DeepClone();
            JObject extraInstance = JObject.Parse(PayloadJson(twoInstances));
            ((JArray)extraInstance["instances"]).Add(extraInstance["instances"][0].DeepClone());

            AssertFailure(
                Participant(CreateTwoBindingRuntime()).RestoreSaveState(WithPayload(twoInstances, duplicateInstance.ToString(Formatting.None))),
                RuntimeSaveStateErrorCode.InvalidDocument,
                GameplayTriggerSaveStateErrorDetailCode.DuplicateInstance);
            AssertFailure(
                Participant(CreateTwoBindingRuntime()).RestoreSaveState(WithPayload(twoInstances, extraInstance.ToString(Formatting.None))),
                RuntimeSaveStateErrorCode.InvalidDocument,
                GameplayTriggerSaveStateErrorDetailCode.InstanceCountMismatch);

            GameplayTriggerRuntime source = CreateOccupancyRuntime();
            SubmitOccupancy(source, 0L, Subject);
            RuntimeSaveState routeState = AssertCapture(Participant(source));
            JObject duplicateRoute = JObject.Parse(PayloadJson(routeState));
            ((JArray)duplicateRoute["routeStates"]).Add(duplicateRoute["routeStates"][0].DeepClone());
            JObject tooManyRoutes = JObject.Parse(PayloadJson(routeState));
            JToken route = tooManyRoutes["routeStates"][0].DeepClone();
            var routes = new JArray();
            for (int i = 0; i < 17; i++) routes.Add(route.DeepClone());
            tooManyRoutes["routeStates"] = routes;

            AssertFailure(
                Participant(CreateOccupancyRuntime()).RestoreSaveState(WithPayload(routeState, duplicateRoute.ToString(Formatting.None))),
                RuntimeSaveStateErrorCode.InvalidDocument,
                GameplayTriggerSaveStateErrorDetailCode.DuplicateRouteState);
            AssertFailure(
                Participant(CreateOccupancyRuntime()).RestoreSaveState(WithPayload(routeState, tooManyRoutes.ToString(Formatting.None))),
                RuntimeSaveStateErrorCode.InvalidDocument,
                GameplayTriggerSaveStateErrorDetailCode.RouteStateCapacityExceeded);
        }

        [TestCase(ValidatorTarget.OccupancyRelationSubject, false)]
        [TestCase(ValidatorTarget.OccupancyRelationSubject, true)]
        [TestCase(ValidatorTarget.ThresholdRelationSubject, false)]
        [TestCase(ValidatorTarget.ThresholdRelationSubject, true)]
        [TestCase(ValidatorTarget.RouteSubject, false)]
        [TestCase(ValidatorTarget.RouteSubject, true)]
        [TestCase(ValidatorTarget.RouteInstigator, false)]
        [TestCase(ValidatorTarget.RouteInstigator, true)]
        public void Restore_ValidatorTargetsEachEntityPathWithoutMutation(
            ValidatorTarget target,
            bool throwAtTarget)
        {
            GameplayTriggerRuntime runtime;
            GameplayEntityId rejectedEntity;
            string expectedPath;
            int expectedAcceptedBeforeTarget;
            switch (target)
            {
                case ValidatorTarget.OccupancyRelationSubject:
                    runtime = CreateOccupancyRuntime();
                    SubmitOccupancy(runtime, 0L, Subject);
                    rejectedEntity = Subject;
                    expectedPath = TriggerPayloadPath + ".relations[0].subjectIndex";
                    expectedAcceptedBeforeTarget = 1;
                    break;
                case ValidatorTarget.ThresholdRelationSubject:
                    runtime = CreateThresholdRuntime();
                    SubmitThreshold(runtime, 0L, Subject, Fix64.FromInt(10));
                    rejectedEntity = Subject;
                    expectedPath = TriggerPayloadPath + ".relations[0].subjectIndex";
                    expectedAcceptedBeforeTarget = 1;
                    break;
                case ValidatorTarget.RouteSubject:
                    runtime = CreateSignalRuntime(GameplayTriggerStateScope.PerSubject);
                    SubmitSignal(runtime, 0L, Subject, default);
                    rejectedEntity = Subject;
                    expectedPath = TriggerPayloadPath + ".routeStates[0].subjectIndex";
                    expectedAcceptedBeforeTarget = 1;
                    break;
                case ValidatorTarget.RouteInstigator:
                    runtime = CreateSignalRuntime(GameplayTriggerStateScope.PerPair);
                    SubmitSignal(runtime, 0L, Subject, Instigator);
                    rejectedEntity = Instigator;
                    expectedPath = TriggerPayloadPath + ".routeStates[0].instigatorIndex";
                    expectedAcceptedBeforeTarget = 2;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(target));
            }

            RuntimeSaveState valid = AssertCapture(Participant(runtime));
            RuntimeSentinel before = RuntimeSentinel.Capture(runtime, valid);
            var validator = new SelectiveValidator(rejectedEntity, throwAtTarget);
            var participant = new GameplayTriggerRuntimeSaveStateParticipant(runtime, validator);

            RuntimeSaveStateResult<bool> result = participant.RestoreSaveState(valid);

            AssertFailure(
                result,
                throwAtTarget ? RuntimeSaveStateErrorCode.InvalidDocument : RuntimeSaveStateErrorCode.UnknownEntity,
                throwAtTarget
                    ? GameplayTriggerSaveStateErrorDetailCode.EntityValidatorException
                    : GameplayTriggerSaveStateErrorDetailCode.UnknownEntity);
            Assert.AreEqual(expectedPath, result.Error.Path);
            Assert.AreEqual(expectedAcceptedBeforeTarget, validator.AcceptedCount);
            Assert.AreEqual(1, validator.TargetCallCount);
            if (throwAtTarget)
                Assert.AreSame(validator.TargetException, result.Error.Exception);
            else
                Assert.IsNull(result.Error.Exception);
            AssertRuntimeUnchanged(runtime, before);
        }

        [TestCase(InvariantFailure.RelationCapacityNPlusOne)]
        [TestCase(InvariantFailure.InstanceStaticIdentity)]
        [TestCase(InvariantFailure.RelationReference)]
        [TestCase(InvariantFailure.RelationFrame)]
        [TestCase(InvariantFailure.RelationFlag)]
        [TestCase(InvariantFailure.DisabledActiveRelation)]
        [TestCase(InvariantFailure.DisabledPrimedRelation)]
        [TestCase(InvariantFailure.RouteReference)]
        [TestCase(InvariantFailure.RouteScopeShape)]
        [TestCase(InvariantFailure.RouteMaxActivations)]
        [TestCase(InvariantFailure.RouteOncePerPresenceEpoch)]
        [TestCase(InvariantFailure.RouteStayFlag)]
        public void Restore_InvariantTailCorruptionUsesStableDetailAndPreservesAtomicSentinel(
            InvariantFailure failure)
        {
            GameplayTriggerRuntime runtime;
            switch (failure)
            {
                case InvariantFailure.DisabledPrimedRelation:
                    runtime = CreateThresholdRuntime();
                    SubmitThreshold(runtime, 0L, Subject, Fix64.Zero);
                    break;
                case InvariantFailure.RouteMaxActivations:
                    runtime = CreateOccupancyRuntime(maxActivations: 1);
                    SubmitOccupancy(runtime, 0L, Subject);
                    break;
                case InvariantFailure.RouteStayFlag:
                    runtime = CreateOccupancyRuntime(oncePerPresence: false);
                    SubmitOccupancy(runtime, 0L, Subject);
                    SubmitOccupancy(runtime, 1L, Subject);
                    break;
                default:
                    runtime = CreateOccupancyRuntime();
                    SubmitOccupancy(runtime, 0L, Subject);
                    break;
            }

            RuntimeSaveState valid = AssertCapture(Participant(runtime));
            RuntimeSentinel before = RuntimeSentinel.Capture(runtime, valid);
            JObject payload = JObject.Parse(PayloadJson(valid));
            GameplayTriggerSaveStateErrorDetailCode expectedDetail;
            string expectedPath;
            switch (failure)
            {
                case InvariantFailure.RelationCapacityNPlusOne:
                    JToken relation = payload["relations"][0].DeepClone();
                    var relations = new JArray();
                    for (int i = 0; i < runtime.RelationCapacity + 1; i++)
                        relations.Add(relation.DeepClone());
                    payload["relations"] = relations;
                    expectedDetail = GameplayTriggerSaveStateErrorDetailCode.RelationCapacityExceeded;
                    expectedPath = TriggerPayloadPath + ".relations";
                    break;
                case InvariantFailure.InstanceStaticIdentity:
                    payload["instances"][0]["definitionVersion"] = 2;
                    expectedDetail = GameplayTriggerSaveStateErrorDetailCode.InstanceIdentityMismatch;
                    expectedPath = TriggerPayloadPath + ".instances[0]";
                    break;
                case InvariantFailure.RelationReference:
                    payload["relations"][0]["instanceIndex"] = 99;
                    expectedDetail = GameplayTriggerSaveStateErrorDetailCode.RelationReferenceInvalid;
                    expectedPath = TriggerPayloadPath + ".relations[0]";
                    break;
                case InvariantFailure.RelationFrame:
                    payload["relations"][0]["presenceStartFrame"] = payload.Value<long>("lastEvaluatedFrame") + 1L;
                    expectedDetail = GameplayTriggerSaveStateErrorDetailCode.RelationStateInvalid;
                    expectedPath = TriggerPayloadPath + ".relations[0]";
                    break;
                case InvariantFailure.RelationFlag:
                    payload["relations"][0]["primed"] = true;
                    expectedDetail = GameplayTriggerSaveStateErrorDetailCode.RelationReferenceInvalid;
                    expectedPath = TriggerPayloadPath + ".relations[0]";
                    break;
                case InvariantFailure.DisabledActiveRelation:
                case InvariantFailure.DisabledPrimedRelation:
                    payload["instances"][0]["enabled"] = false;
                    expectedDetail = GameplayTriggerSaveStateErrorDetailCode.InstanceStateInvalid;
                    expectedPath = TriggerPayloadPath + ".instances[0].enabled";
                    break;
                case InvariantFailure.RouteReference:
                    payload["routeStates"][0]["routeId"] = 99;
                    expectedDetail = GameplayTriggerSaveStateErrorDetailCode.RouteReferenceInvalid;
                    expectedPath = TriggerPayloadPath + ".routeStates[0]";
                    break;
                case InvariantFailure.RouteScopeShape:
                    payload["routeStates"][0]["subjectIndex"] = 0;
                    payload["routeStates"][0]["subjectGeneration"] = 0;
                    expectedDetail = GameplayTriggerSaveStateErrorDetailCode.RouteScopeShapeInvalid;
                    expectedPath = TriggerPayloadPath + ".routeStates[0]";
                    break;
                case InvariantFailure.RouteMaxActivations:
                    payload["routeStates"][0]["activationCount"] = 2;
                    expectedDetail = GameplayTriggerSaveStateErrorDetailCode.RouteStateInvalid;
                    expectedPath = TriggerPayloadPath + ".routeStates[0]";
                    break;
                case InvariantFailure.RouteOncePerPresenceEpoch:
                    payload["routeStates"][0]["lastActivatedPresenceEpoch"] = 2L;
                    expectedDetail = GameplayTriggerSaveStateErrorDetailCode.RouteStateInvalid;
                    expectedPath = TriggerPayloadPath + ".routeStates[0]";
                    break;
                case InvariantFailure.RouteStayFlag:
                    payload["routeStates"][0]["hasStaySchedule"] = false;
                    expectedDetail = GameplayTriggerSaveStateErrorDetailCode.RouteStateInvalid;
                    expectedPath = TriggerPayloadPath + ".routeStates[0]";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(failure));
            }

            RuntimeSaveStateResult<bool> result = Participant(runtime).RestoreSaveState(
                WithPayload(valid, payload.ToString(Formatting.None)));

            AssertFailure(result, RuntimeSaveStateErrorCode.InvalidDocument, expectedDetail);
            Assert.AreEqual(expectedPath, result.Error.Path);
            AssertRuntimeUnchanged(runtime, before);
        }

        [Test]
        public void RestoreFailure_PreservesCanonicalPayloadHashNextIdAndRealPendingBatch()
        {
            GameplayTriggerRuntime target = CreateOccupancyRuntime();
            var participant = Participant(target);
            string canonicalBefore = PayloadJson(AssertCapture(participant));
            long hashBefore = ComputeHash(target);
            long nextBefore = target.NextActivationId;
            RuntimeSaveState valid = AssertCapture(Participant(CreateOccupancyRuntime()));
            RuntimeSaveState malformed = WithPayload(valid, PayloadJson(valid) + "{}");

            RuntimeSaveStateResult<bool> canonicalFailure = participant.RestoreSaveState(malformed);
            AssertFailure(canonicalFailure, RuntimeSaveStateErrorCode.InvalidDocument, GameplayTriggerSaveStateErrorDetailCode.PayloadJsonInvalid);
            Assert.AreEqual(canonicalBefore, PayloadJson(AssertCapture(participant)));

            Assert.IsTrue(target.TrySubmitOccupancy(RuntimeFrame.Zero, SourceBinding, new[] { Subject }, out _));
            RuntimeSaveStateResult<bool> pendingFailure = participant.RestoreSaveState(malformed);

            AssertFailure(pendingFailure, RuntimeSaveStateErrorCode.InvalidDocument, GameplayTriggerSaveStateErrorDetailCode.PayloadJsonInvalid);
            Assert.AreEqual(hashBefore, ComputeHash(target));
            Assert.AreEqual(nextBefore, target.NextActivationId);
            RuntimeSaveStateResult<RuntimeSaveState> capture = participant.CaptureSaveState();
            Assert.IsFalse(capture.Success);
            Assert.AreEqual((int)GameplayTriggerCheckpointPendingDetailCode.PendingInputBatches, capture.Error.DetailCode);
        }

        [Test]
        public void Restore_UnEvaluatedInstanceEpochTailCorruptionFailsAtomically()
        {
            GameplayTriggerRuntime target = CreateOccupancyRuntime();
            var participant = Participant(target);
            string before = PayloadJson(AssertCapture(participant));
            long hashBefore = ComputeHash(target);
            RuntimeSaveState valid = AssertCapture(Participant(CreateOccupancyRuntime()));
            JObject payload = JObject.Parse(PayloadJson(valid));
            payload["instances"][0]["aggregatePresenceEpoch"] = 1L;

            RuntimeSaveStateResult<bool> result = participant.RestoreSaveState(
                WithPayload(valid, payload.ToString(Formatting.None)));

            AssertFailure(result, RuntimeSaveStateErrorCode.InvalidDocument, GameplayTriggerSaveStateErrorDetailCode.InstanceStateInvalid);
            Assert.AreEqual(before, PayloadJson(AssertCapture(participant)));
            Assert.AreEqual(hashBefore, ComputeHash(target));
            Assert.AreEqual(1L, target.NextActivationId);
        }

        private static GameplayTriggerRuntimeSaveStateParticipant Participant(GameplayTriggerRuntime runtime) =>
            new GameplayTriggerRuntimeSaveStateParticipant(runtime, new RecordingValidator());

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void CheckReady128(
            GameplayTriggerRuntimeSaveStateParticipant participant,
            in RuntimeCheckpointContext context)
        {
            for (int i = 0; i < 128; i++)
            {
                RuntimeCheckpointQuiescenceResult result = participant.CheckQuiescence(in context);
                if (!result.IsReady) throw new InvalidOperationException("Unexpected pending state.");
            }
        }

        private static RuntimeSaveState AssertCapture(GameplayTriggerRuntimeSaveStateParticipant participant)
        {
            RuntimeSaveStateResult<RuntimeSaveState> result = participant.CaptureSaveState();
            Assert.IsTrue(result.Success, result.Error.ToString());
            return result.Value;
        }

        private static void SubmitOccupancy(GameplayTriggerRuntime runtime, long frame, GameplayEntityId subject)
        {
            SubmitOccupancy(runtime, frame, new[] { subject });
        }

        private static void SubmitOccupancy(GameplayTriggerRuntime runtime, long frame, GameplayEntityId[] subjects)
        {
            var runtimeFrame = new RuntimeFrame(frame);
            Assert.IsTrue(runtime.TrySubmitOccupancy(runtimeFrame, SourceBinding, subjects, out GameplayTriggerResult submit), submit.ErrorCode.ToString());
            GameplayTriggerResult evaluate = runtime.Evaluate(runtimeFrame);
            Assert.IsTrue(evaluate.Success, evaluate.ErrorCode.ToString());
        }

        private static void SubmitThreshold(
            GameplayTriggerRuntime runtime,
            long frame,
            GameplayEntityId subject,
            Fix64 value)
        {
            var runtimeFrame = new RuntimeFrame(frame);
            Assert.IsTrue(runtime.TrySubmitScalarBatch(
                runtimeFrame,
                SourceBinding,
                new[] { new GameplayTriggerScalarSample(subject, value) },
                out GameplayTriggerResult submit), submit.ErrorCode.ToString());
            GameplayTriggerResult evaluate = runtime.Evaluate(runtimeFrame);
            Assert.IsTrue(evaluate.Success, evaluate.ErrorCode.ToString());
        }

        private static void SubmitSignal(
            GameplayTriggerRuntime runtime,
            long frame,
            GameplayEntityId subject,
            GameplayEntityId instigator)
        {
            var runtimeFrame = new RuntimeFrame(frame);
            Assert.IsTrue(runtime.TrySubmitSignals(
                runtimeFrame,
                SourceBinding,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[] { new GameplayTriggerSignal(1, subject, instigator) },
                out GameplayTriggerResult submit), submit.ErrorCode.ToString());
            GameplayTriggerResult evaluate = runtime.Evaluate(runtimeFrame);
            Assert.IsTrue(evaluate.Success, evaluate.ErrorCode.ToString());
        }

        private static GameplayTriggerRuntime CreateOccupancyRuntime(
            bool oncePerPresence = true,
            int maxActivations = 0)
        {
            var source = new GameplayTriggerSourceDefinition(new GameplayTriggerSourceDefinitionId(1), GameplayTriggerSourceKind.Occupancy);
            var policy = new GameplayTriggerActivationPolicy(
                GameplayTriggerStateScope.PerSubject,
                maxActivations: maxActivations,
                presenceLimit: oncePerPresence
                    ? GameplayTriggerPresenceLimit.OncePerPresence
                    : GameplayTriggerPresenceLimit.Ignore,
                stayIntervalFrames: 1L);
            return CreateRuntime(source, GameplayTriggerOccurrenceKind.Entered | GameplayTriggerOccurrenceKind.Stayed, policy);
        }

        private static GameplayTriggerRuntime CreateThresholdRuntime()
        {
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Threshold,
                thresholdDirection: GameplayTriggerThresholdDirection.High,
                enterThreshold: Fix64.FromInt(10),
                exitThreshold: Fix64.FromInt(8),
                initialSamplePolicy: GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive);
            return CreateRuntime(source, GameplayTriggerOccurrenceKind.Entered, default);
        }

        private static GameplayTriggerRuntime CreateSignalRuntime(GameplayTriggerStateScope scope)
        {
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal);
            return CreateRuntime(
                source,
                GameplayTriggerOccurrenceKind.Pulsed,
                new GameplayTriggerActivationPolicy(scope));
        }

        private static GameplayTriggerRuntime CreateTwoBindingRuntime()
        {
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Occupancy);
            var route = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                GameplayTriggerOccurrenceKind.Entered,
                GameplayTriggerConditionProgram.Always,
                new[] { new GameplayTriggerActionInstruction(ActionKind, GameplayTriggerTargetSelector.Owner) });
            var definition = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(1),
                1,
                source.SourceDefinitionId,
                new[] { route });
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[]
                {
                    new GameplayTriggerBinding(new GameplayTriggerBindingId(1), definition.DefinitionId, SourceBinding, Owner),
                    new GameplayTriggerBinding(new GameplayTriggerBindingId(2), definition.DefinitionId, new GameplayTriggerSourceBindingId(101), Subject)
                },
                new IGameplayTriggerActionSink[] { new Sink() },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation), validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(8, 16, 16, 16, 16, 16, 16, 16, 8, 8));
        }

        private static GameplayTriggerRuntime CreateRuntime(
            GameplayTriggerSourceDefinition source,
            GameplayTriggerOccurrenceKind mask,
            GameplayTriggerActivationPolicy policy)
        {
            var route = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                mask,
                GameplayTriggerConditionProgram.Always,
                new[] { new GameplayTriggerActionInstruction(ActionKind, GameplayTriggerTargetSelector.Owner) },
                policy);
            var definition = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(1),
                1,
                source.SourceDefinitionId,
                new[] { route });
            var binding = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(1),
                definition.DefinitionId,
                SourceBinding,
                Owner);
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[] { binding },
                new IGameplayTriggerActionSink[] { new Sink() },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation), validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(8, 16, 16, 16, 16, 16, 16, 16, 8, 8));
        }

        private static long ComputeHash(GameplayTriggerRuntime runtime)
        {
            var contributor = new GameplayTriggerRuntimeHashContributor(runtime);
            var accumulator = new RuntimeHashAccumulator(9504, 1);
            contributor.Contribute(new RuntimeHashContext(runtime.AuthorityLastEvaluatedFrame), accumulator);
            return accumulator.ToHash();
        }

        private static string PayloadJson(RuntimeSaveState state) => state.ModuleStates[0].CustomState.PayloadJson;

        private static RuntimeSaveState WithPayload(RuntimeSaveState source, string payloadJson)
        {
            RuntimeModuleSaveState old = source.ModuleStates[0];
            var module = new RuntimeModuleSaveState(
                old.ModuleId,
                old.SchemaVersion,
                new RuntimeCustomState(old.CustomState.NumericTypeId, old.CustomState.TypeId, old.CustomState.SchemaVersion, payloadJson));
            return Root(source.Frame, new[] { module });
        }

        private static RuntimeSaveState Root(long frame, IReadOnlyList<RuntimeModuleSaveState> modules) =>
            new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                string.Empty,
                string.Empty,
                string.Empty,
                frame,
                null,
                null,
                modules,
                null);

        private static void AssertFailure<T>(
            RuntimeSaveStateResult<T> result,
            RuntimeSaveStateErrorCode code,
            GameplayTriggerSaveStateErrorDetailCode detail)
        {
            Assert.IsFalse(result.Success);
            Assert.AreEqual(code, result.Error.Code);
            Assert.AreEqual((int)detail, result.Error.DetailCode);
        }

        private static void AssertRuntimeUnchanged(
            GameplayTriggerRuntime runtime,
            RuntimeSentinel before)
        {
            RuntimeSentinel after = RuntimeSentinel.Capture(
                runtime,
                AssertCapture(Participant(runtime)));
            Assert.AreEqual(before.PayloadJson, after.PayloadJson, "Canonical payload changed after rejected restore.");
            Assert.AreEqual(before.AuthorityHash, after.AuthorityHash, "Authority hash changed after rejected restore.");
            Assert.AreEqual(before.NextActivationId, after.NextActivationId, "Activation allocator changed after rejected restore.");
            Assert.AreEqual(before.PendingInputBatches, after.PendingInputBatches);
            Assert.AreEqual(before.PendingOccupancyItems, after.PendingOccupancyItems);
            Assert.AreEqual(before.PendingScalarItems, after.PendingScalarItems);
            Assert.AreEqual(before.PendingSignalItems, after.PendingSignalItems);
            Assert.AreEqual(before.OccurrenceCount, after.OccurrenceCount);
            Assert.AreEqual(before.ActivationCount, after.ActivationCount);
            Assert.AreEqual(before.IntentCount, after.IntentCount);
            Assert.AreEqual(before.DiagnosticCount, after.DiagnosticCount);
            Assert.AreEqual(before.FirstOccurrenceKind, after.FirstOccurrenceKind);
            Assert.AreEqual(before.FirstOccurrenceSubject, after.FirstOccurrenceSubject);
            Assert.AreEqual(before.FirstOccurrenceInstigator, after.FirstOccurrenceInstigator);
            Assert.AreEqual(before.FirstActivationId, after.FirstActivationId);
            Assert.AreEqual(before.FirstIntentActivationId, after.FirstIntentActivationId);
            Assert.AreEqual(before.FirstDiagnosticCode, after.FirstDiagnosticCode);
            Assert.AreEqual(before.FirstDiagnosticDetail, after.FirstDiagnosticDetail);
        }

        public enum StrictJsonFailure
        {
            DuplicateProperty,
            TrailingRoot,
            Comment,
            UnknownProperty,
            MissingProperty,
            NullProperty,
            NumericOverflow
        }

        public enum ValidatorTarget
        {
            OccupancyRelationSubject,
            ThresholdRelationSubject,
            RouteSubject,
            RouteInstigator
        }

        public enum InvariantFailure
        {
            RelationCapacityNPlusOne,
            InstanceStaticIdentity,
            RelationReference,
            RelationFrame,
            RelationFlag,
            DisabledActiveRelation,
            DisabledPrimedRelation,
            RouteReference,
            RouteScopeShape,
            RouteMaxActivations,
            RouteOncePerPresenceEpoch,
            RouteStayFlag
        }

        private sealed class RuntimeSentinel
        {
            private RuntimeSentinel()
            {
            }

            public string PayloadJson { get; private set; }
            public long AuthorityHash { get; private set; }
            public long NextActivationId { get; private set; }
            public int PendingInputBatches { get; private set; }
            public int PendingOccupancyItems { get; private set; }
            public int PendingScalarItems { get; private set; }
            public int PendingSignalItems { get; private set; }
            public int OccurrenceCount { get; private set; }
            public int ActivationCount { get; private set; }
            public int IntentCount { get; private set; }
            public int DiagnosticCount { get; private set; }
            public GameplayTriggerOccurrenceKind FirstOccurrenceKind { get; private set; }
            public GameplayEntityId FirstOccurrenceSubject { get; private set; }
            public GameplayEntityId FirstOccurrenceInstigator { get; private set; }
            public long FirstActivationId { get; private set; }
            public long FirstIntentActivationId { get; private set; }
            public GameplayTriggerErrorCode FirstDiagnosticCode { get; private set; }
            public int FirstDiagnosticDetail { get; private set; }

            public static RuntimeSentinel Capture(
                GameplayTriggerRuntime runtime,
                RuntimeSaveState saveState)
            {
                GameplayTriggerOccurrence occurrence = runtime.OccurrenceCount > 0
                    ? runtime.GetOccurrence(0)
                    : default;
                GameplayTriggerActivation activation = runtime.ActivationCount > 0
                    ? runtime.GetActivation(0)
                    : default;
                GameplayTriggerActionIntent intent = runtime.IntentCount > 0
                    ? runtime.GetIntent(0)
                    : default;
                GameplayTriggerDiagnostic diagnostic = runtime.DiagnosticCount > 0
                    ? runtime.GetDiagnostic(0)
                    : default;
                return new RuntimeSentinel
                {
                    PayloadJson = GameplayTriggerSaveStateTests.PayloadJson(saveState),
                    AuthorityHash = ComputeHash(runtime),
                    NextActivationId = runtime.NextActivationId,
                    PendingInputBatches = runtime.PendingInputBatchCount,
                    PendingOccupancyItems = runtime.PendingOccupancyItemCount,
                    PendingScalarItems = runtime.PendingScalarItemCount,
                    PendingSignalItems = runtime.PendingSignalItemCount,
                    OccurrenceCount = runtime.OccurrenceCount,
                    ActivationCount = runtime.ActivationCount,
                    IntentCount = runtime.IntentCount,
                    DiagnosticCount = runtime.DiagnosticCount,
                    FirstOccurrenceKind = occurrence.Kind,
                    FirstOccurrenceSubject = occurrence.Subject,
                    FirstOccurrenceInstigator = occurrence.Instigator,
                    FirstActivationId = activation.ActivationId,
                    FirstIntentActivationId = intent.ActivationId,
                    FirstDiagnosticCode = diagnostic.Code,
                    FirstDiagnosticDetail = diagnostic.Detail
                };
            }
        }

        private sealed class RecordingValidator : IGameplayTriggerRestoreEntityValidator
        {
            public int CallCount { get; private set; }

            public bool IsRestoredEntityValid(GameplayEntityId entityId)
            {
                CallCount++;
                return entityId.IsValid;
            }
        }

        private sealed class SelectiveValidator : IGameplayTriggerRestoreEntityValidator
        {
            private readonly GameplayEntityId _target;
            private readonly bool _throwAtTarget;

            public SelectiveValidator(GameplayEntityId target, bool throwAtTarget)
            {
                _target = target;
                _throwAtTarget = throwAtTarget;
                TargetException = new InvalidOperationException("validator target test");
            }

            public int AcceptedCount { get; private set; }
            public int TargetCallCount { get; private set; }
            public Exception TargetException { get; }

            public bool IsRestoredEntityValid(GameplayEntityId entityId)
            {
                if (entityId == _target)
                {
                    TargetCallCount++;
                    if (_throwAtTarget) throw TargetException;
                    return false;
                }
                AcceptedCount++;
                return true;
            }
        }

        private sealed class WorldValidator : IGameplayTriggerRestoreEntityValidator
        {
            private readonly GameplayComponentWorld _world;

            public WorldValidator(GameplayComponentWorld world)
            {
                _world = world;
            }

            public bool IsRestoredEntityValid(GameplayEntityId entityId) => _world.IsAlive(entityId);
        }

        private sealed class CountingProvider : IRuntimeSaveStateProvider
        {
            private readonly IRuntimeSaveStateProvider _inner;

            public CountingProvider(IRuntimeSaveStateProvider inner)
            {
                _inner = inner;
            }

            public int CaptureCount { get; private set; }

            public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
            {
                CaptureCount++;
                return _inner.CaptureSaveState();
            }
        }

        private sealed class Sink : IGameplayTriggerActionSink
        {
            private int _nextToken;
            public GameplayTriggerActionKindId ActionKindId => ActionKind;
            public GameplayTriggerDeliveryContract DeliveryContract => GameplayTriggerDeliveryContract.SameFrameReserved;
            public bool TryPrepare(in GameplayTriggerActionPreparation preparation, out int token) { token = ++_nextToken; return true; }
            public void Abort(int token) { }
            public void Commit(int token, in GameplayTriggerActionIntent intent) { }
        }
    }
}
