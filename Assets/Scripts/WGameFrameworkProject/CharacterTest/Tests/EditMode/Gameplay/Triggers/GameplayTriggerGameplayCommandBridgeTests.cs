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
    public sealed class GameplayTriggerGameplayCommandBridgeTests
    {
        private const int SourceId = 72801;
        private const int AttributeId = 31;
        private const int AbilityId = 300031;
        private const int CooldownAbilityId = 300032;
        private const int EffectFailureAbilityId = 300033;
        private static readonly GameplayTriggerSourceBindingId SourceBindingId =
            new GameplayTriggerSourceBindingId(728);

        [Test]
        public void PublicContract_FreezesNumericIdsAndResultShape()
        {
            Assert.AreEqual(GameplayRuntimeCommandIds.DestroyComponentEntity,
                GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntityValue);
            Assert.AreEqual(GameplayRuntimeCommandIds.SetComponentAttribute,
                GameplayTriggerGameplayCommandActionKinds.SetComponentAttributeValue);
            Assert.AreEqual(GameplayRuntimeCommandIds.AddComponentAttribute,
                GameplayTriggerGameplayCommandActionKinds.AddComponentAttributeValue);
            Assert.AreEqual(GameplayRuntimeCommandIds.CastComponentAbility,
                GameplayTriggerGameplayCommandActionKinds.CastComponentAbilityValue);
            Assert.AreEqual(GameplayTriggerDeliveryContract.ReliableDeferred,
                GameplayTriggerGameplayCommandActionKinds.DeliveryContract);
            Assert.AreEqual(0, (int)GameplayComponentCommandResultStatus.None);
            Assert.AreEqual(1, (int)GameplayComponentCommandResultStatus.Succeeded);
            Assert.AreEqual(2, (int)GameplayComponentCommandResultStatus.Rejected);
            Assert.AreEqual(1209,
                (int)GameplayComponentCommandResultReasonCode.InvalidComponentAbilityResult);

            FieldInfo[] fields = typeof(GameplayComponentCommandResult).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                Type type = fields[i].FieldType;
                Assert.AreNotEqual(typeof(string), type, fields[i].Name);
                Assert.AreNotEqual(typeof(object), type, fields[i].Name);
                Assert.AreNotEqual(typeof(RuntimeCommand), type, fields[i].Name);
                Assert.IsFalse(typeof(Delegate).IsAssignableFrom(type), fields[i].Name);
                Assert.IsFalse(
                    type.Namespace != null && type.Namespace.Contains("Gameplay.Triggers"),
                    fields[i].Name);
            }
        }

        [Test]
        public void StructuralPreflight_RejectsBeforeActivationPolicyAndOutboxMutation()
        {
            var sink = new GameplayTriggerGameplayReliableCommandActionSink(
                GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity);
            GameplayTriggerRuntime runtime = CreateRuntime(
                sink,
                new GameplayTriggerActionInstruction(
                    sink.ActionKindId,
                    GameplayTriggerTargetSelector.Subject,
                    payload0: 1L),
                eventSource: null);

            Queue(runtime, RuntimeFrame.Zero, new GameplayEntityId(1, 1));

            Assert.AreEqual(0, runtime.ActivationCount);
            Assert.AreEqual(0, runtime.IntentCount);
            Assert.AreEqual(0, runtime.RouteStateCount);
            Assert.AreEqual(0, runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(1L, runtime.NextActivationId);
            Assert.AreEqual(1L, runtime.NextReliableOutboxSequence);
            Assert.AreEqual(GameplayTriggerErrorCode.ReservationRejected,
                runtime.GetDiagnostic(0).Code);
        }

        [Test]
        public void StructuralPreflight_FreezesTargetAndFullInt32PayloadBoundaries()
        {
            var set = new GameplayTriggerGameplayReliableCommandActionSink(
                GameplayTriggerGameplayCommandActionKinds.SetComponentAttribute);
            Assert.IsTrue(set.TryValidateReliableAction(Preparation(
                set.ActionKindId,
                new GameplayTriggerTargetRef(
                    GameplayTriggerTargetRef.GameplayEntityDomainId,
                    int.MaxValue,
                    int.MaxValue),
                int.MaxValue,
                int.MinValue,
                0L)));
            Assert.IsFalse(set.TryValidateReliableAction(Preparation(
                set.ActionKindId,
                new GameplayTriggerTargetRef(
                    GameplayTriggerTargetRef.GameplayEntityDomainId,
                    int.MaxValue,
                    0),
                1L,
                0L,
                0L)));
            Assert.IsFalse(set.TryValidateReliableAction(Preparation(
                set.ActionKindId,
                new GameplayTriggerTargetRef(99, 1L, 1),
                1L,
                0L,
                0L)));
            Assert.IsFalse(set.TryValidateReliableAction(Preparation(
                set.ActionKindId,
                GameplayTriggerTargetRef.FromGameplayEntity(new GameplayEntityId(1, 1)),
                (long)int.MaxValue + 1L,
                0L,
                0L)));
            Assert.IsFalse(set.TryValidateReliableAction(Preparation(
                set.ActionKindId,
                GameplayTriggerTargetRef.FromGameplayEntity(new GameplayEntityId(1, 1)),
                1L,
                (long)int.MaxValue + 1L,
                0L)));

            var ability = new GameplayTriggerGameplayReliableCommandActionSink(
                GameplayTriggerGameplayCommandActionKinds.CastComponentAbility);
            Assert.IsTrue(ability.TryValidateReliableAction(Preparation(
                ability.ActionKindId,
                GameplayTriggerTargetRef.FromGameplayEntity(new GameplayEntityId(1, 1)),
                int.MaxValue,
                0L,
                0L)));
            Assert.IsFalse(ability.TryValidateReliableAction(Preparation(
                ability.ActionKindId,
                GameplayTriggerTargetRef.FromGameplayEntity(new GameplayEntityId(1, 1)),
                1L,
                1L,
                0L)));
        }

        [Test]
        public void LegacyReliableSink_RemainsBypassedAndQueuesUnchanged()
        {
            var sink = new LegacyReliableSink(
                GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity);
            GameplayTriggerRuntime runtime = CreateRuntime(
                sink,
                new GameplayTriggerActionInstruction(
                    sink.ActionKindId,
                    GameplayTriggerTargetSelector.Subject,
                    payload0: 91L,
                    payload1: 92L,
                    payload2: 93L),
                eventSource: null);

            Queue(runtime, RuntimeFrame.Zero, new GameplayEntityId(1, 1));

            Assert.AreEqual(0, sink.TryPrepareCount);
            Assert.AreEqual(1, runtime.ActivationCount);
            Assert.AreEqual(1, runtime.ReliableOutboxRecordCount);
            GameplayTriggerReliableOutboxRecord record = runtime.GetReliableOutboxRecord(0);
            Assert.AreEqual(91L, record.ActionPayload0);
            Assert.AreEqual(92L, record.ActionPayload1);
            Assert.AreEqual(93L, record.ActionPayload2);
        }

        [Test]
        public void TransferComposition_RejectsWrongIngressUnboundedMismatchSourceAndConsumer()
        {
            var system = new GameplayComponentEntityCommandSystem();
            RuntimeCommandBuffer bounded = RuntimeCommandBuffer.CreateBounded(1);
            GameplayRuntimeModule module = CreateGameplayModule(bounded, system);

            Assert.Throws<ArgumentException>(() => new GameplayTriggerGameplayReliableCommandTransfer(
                GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity,
                SourceId,
                new RuntimeHost(),
                module,
                system,
                CreateGate(bounded, RuntimeCommandIngressKind.External)));
            Assert.Throws<ArgumentException>(() => new GameplayTriggerGameplayReliableCommandTransfer(
                GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity,
                SourceId,
                new RuntimeHost(),
                CreateGameplayModule(new RuntimeCommandBuffer(), system),
                system,
                CreateGate(new RuntimeCommandBuffer(), RuntimeCommandIngressKind.TrustedDerived)));

            RuntimeCommandBuffer other = RuntimeCommandBuffer.CreateBounded(1);
            Assert.Throws<ArgumentException>(() => new GameplayTriggerGameplayReliableCommandTransfer(
                GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity,
                SourceId,
                new RuntimeHost(),
                module,
                system,
                CreateGate(other, RuntimeCommandIngressKind.TrustedDerived)));

            var wrongRegistry = new RuntimeCommandSourceRegistry(1);
            Assert.IsTrue(wrongRegistry.TryRegister(
                SourceId,
                RuntimeCommandSourceKind.Derived,
                new[] { GameplayRuntimeCommandIds.DestroyComponentEntity },
                out _));
            var wrongSourceGate = new RuntimeCommandSubmissionGate(
                wrongRegistry,
                bounded,
                RuntimeCommandIngressKind.TrustedDerived);
            Assert.Throws<ArgumentException>(() => new GameplayTriggerGameplayReliableCommandTransfer(
                GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity,
                SourceId,
                new RuntimeHost(),
                module,
                system,
                wrongSourceGate));

            Assert.Throws<ArgumentException>(() => new GameplayTriggerGameplayReliableCommandTransfer(
                GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity,
                SourceId,
                new RuntimeHost(),
                module,
                new GameplayAttributeCommandSystem(),
                CreateGate(bounded, RuntimeCommandIngressKind.TrustedDerived)));
        }

        [Test]
        public void TransferDynamicIdentity_ReturnsExactUnavailableDetailsForImpostors()
        {
            GameplayTriggerRuntime runtime = QueuedDestroyRuntime(
                new GameplayEntityId(3, 1),
                eventSource: null);
            GameplayTriggerReliableOutboxRecord record = runtime.GetReliableOutboxRecord(0);
            var system = new GameplayComponentEntityCommandSystem();
            RuntimeCommandBuffer buffer = RuntimeCommandBuffer.CreateBounded(1);
            GameplayRuntimeModule module = CreateGameplayModule(buffer, system);
            var host = new RuntimeHost();
            var transfer = new GameplayTriggerGameplayReliableCommandTransfer(
                GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity,
                SourceId,
                host,
                module,
                system,
                CreateGate(buffer, RuntimeCommandIngressKind.TrustedDerived));

            GameplayTriggerReliableTransferResult notStarted = transfer.TryTransfer(
                new RuntimeFrame(1), in record);
            Assert.AreEqual(GameplayTriggerReliableTransferStatus.TargetModuleUnavailable,
                notStarted.Status);
            Assert.AreEqual(GameplayTriggerGameplayCommandContracts.TargetHostNotStartedDetailCode,
                notStarted.DetailCode);

            var sameIdSystem = new GameplayComponentEntityCommandSystem();
            GameplayRuntimeModule sameIdImpostor = CreateGameplayModule(
                RuntimeCommandBuffer.CreateBounded(1),
                sameIdSystem);
            host.RegisterModule(sameIdImpostor);
            host.Initialize();
            host.Start();
            GameplayTriggerReliableTransferResult missingModule = transfer.TryTransfer(
                new RuntimeFrame(1), in record);
            Assert.AreEqual(GameplayTriggerReliableTransferStatus.TargetModuleUnavailable,
                missingModule.Status);
            Assert.AreEqual(GameplayTriggerGameplayCommandContracts.TargetModuleNotRegisteredDetailCode,
                missingModule.DetailCode);
            host.Dispose();

            var registeredHost = new RuntimeHost();
            var registeredSystem = new GameplayComponentEntityCommandSystem();
            RuntimeCommandBuffer registeredBuffer = RuntimeCommandBuffer.CreateBounded(1);
            GameplayRuntimeModule registeredModule = CreateGameplayModule(
                registeredBuffer,
                registeredSystem);
            var sameIdConsumerImpostor = new GameplayComponentEntityCommandSystem();
            var consumerTransfer = new GameplayTriggerGameplayReliableCommandTransfer(
                GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity,
                SourceId,
                registeredHost,
                registeredModule,
                sameIdConsumerImpostor,
                CreateGate(registeredBuffer, RuntimeCommandIngressKind.TrustedDerived));
            registeredHost.RegisterModule(registeredModule);
            registeredHost.Initialize();
            registeredHost.Start();
            GameplayTriggerReliableTransferResult missingConsumer = consumerTransfer.TryTransfer(
                new RuntimeFrame(1), in record);
            Assert.AreEqual(GameplayTriggerReliableTransferStatus.TargetModuleUnavailable,
                missingConsumer.Status);
            Assert.AreEqual(GameplayTriggerGameplayCommandContracts.TargetConsumerNotRegisteredDetailCode,
                missingConsumer.DetailCode);
            registeredHost.Dispose();
        }

        [Test]
        public void FullTarget_RetainsThenRetriesOnceAndPublishesCorrelatedResult()
        {
            using (CommandBridgeHarness harness = CommandBridgeHarness.Create(
                commandCapacity: 1,
                prefillFrameOne: true))
            {
                GameplayEntityId target = harness.World.CreateEntity();
                harness.QueueDestroy(RuntimeFrame.Zero, target);

                harness.Host.Tick(1L, 0d);

                Assert.AreEqual(1, harness.Runtime.ReliableOutboxRecordCount);
                Assert.IsTrue(harness.World.IsAlive(target));
                Assert.AreEqual(GameplayTriggerReliableTransferStatus.TargetCapacityExceeded,
                    harness.Runtime.LastReliableOutboxExecutionResult.TransferResult.Status);
                Assert.AreEqual((int)RuntimeCommandErrorCode.PendingCapacityExceeded,
                    harness.Runtime.LastReliableOutboxExecutionResult.TransferResult.DetailCode);
                Assert.AreEqual(1L, harness.CommandBuffer.CaptureSnapshot().NextSequence);

                harness.Host.Tick(2L, 0d);

                Assert.AreEqual(0, harness.Runtime.ReliableOutboxRecordCount);
                Assert.IsFalse(harness.World.IsAlive(target));
                GameplayTriggerRuntimeEvent result = harness.TakeOnlyActionResult();
                Assert.AreEqual(new RuntimeFrame(2), result.Frame);
                Assert.AreEqual(1L, result.ActivationId);
                Assert.AreEqual(0, result.ActionIndex);
                Assert.AreEqual(GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity,
                    result.ActionKindId);
                Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Succeeded, result.Status);
                Assert.AreEqual((int)GameplayComponentCommandResultReasonCode.ComponentEntityDestroyed,
                    result.ReasonCode);
                Assert.AreEqual(0L, result.ResultPayload0);
                Assert.AreEqual(0L, result.ResultPayload1);
                Assert.AreEqual(0L, result.ResultPayload2);
                Assert.AreEqual(3, harness.Validator.AcceptedCount);
                Assert.AreEqual(0L, harness.Validator.LastCommand.Sequence);
                Assert.Greater(harness.Validator.LastCommand.Envelope.Data3, 0L);
                Assert.AreEqual(2L, harness.CommandBuffer.CaptureSnapshot().NextSequence);
            }
        }

        [Test]
        public void DisabledConsumer_RetainsAndLaterEnabledRetriesSuccessfully()
        {
            using (CommandBridgeHarness harness = CommandBridgeHarness.Create(
                commandCapacity: 1,
                prefillFrameOne: false))
            {
                GameplayEntityId target = harness.World.CreateEntity();
                harness.QueueAction(RuntimeFrame.Zero, target);
                ((GameplayComponentEntityCommandSystem)harness.Consumer).SetEnabled(false);

                harness.Host.Tick(1L, 0d);

                Assert.AreEqual(1, harness.Runtime.ReliableOutboxRecordCount);
                Assert.AreEqual(GameplayTriggerReliableTransferStatus.TargetDisabled,
                    harness.Runtime.LastReliableOutboxExecutionResult.TransferResult.Status);
                Assert.IsTrue(harness.World.IsAlive(target));
                Assert.AreEqual(0L, harness.CommandBuffer.CaptureSnapshot().NextSequence);

                ((GameplayComponentEntityCommandSystem)harness.Consumer).SetEnabled(true);
                harness.Host.Tick(2L, 0d);

                Assert.AreEqual(0, harness.Runtime.ReliableOutboxRecordCount);
                Assert.IsFalse(harness.World.IsAlive(target));
                Assert.AreEqual(1L, harness.CommandBuffer.CaptureSnapshot().NextSequence);
                Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Succeeded,
                    harness.TakeOnlyActionResult().Status);
            }
        }

        [Test]
        public void MultipleSameFrameRecordsKeepOutboxThenTargetCanonicalOrder()
        {
            using (CommandBridgeHarness harness = CommandBridgeHarness.Create(
                commandCapacity: 2,
                prefillFrameOne: false))
            {
                GameplayEntityId first = harness.World.CreateEntity();
                GameplayEntityId second = harness.World.CreateEntity();
                QueueMany(harness.Runtime, RuntimeFrame.Zero, second, first);
                Assert.AreEqual(2, harness.Runtime.ReliableOutboxRecordCount);
                GameplayTriggerReliableOutboxRecord outbox0 = harness.Runtime.GetReliableOutboxRecord(0);
                GameplayTriggerReliableOutboxRecord outbox1 = harness.Runtime.GetReliableOutboxRecord(1);
                Assert.AreEqual(1L, outbox0.Sequence);
                Assert.AreEqual(2L, outbox1.Sequence);
                Assert.AreEqual(first.Index, outbox0.ResolvedTarget.Value);
                Assert.AreEqual(second.Index, outbox1.ResolvedTarget.Value);

                harness.Host.Tick(1L, 0d);

                Assert.AreEqual(0, harness.Runtime.ReliableOutboxRecordCount);
                GameplayTriggerRuntimeEvent[] results = harness.TakeActionResults(2);
                Assert.AreEqual(first.Index, results[0].Target.Value);
                Assert.AreEqual(second.Index, results[1].Target.Value);
                Assert.Less(results[0].SourceSequence, results[1].SourceSequence);
            }
        }

        [Test]
        public void SetAttribute_EndToEndPublishesExactSuccess()
        {
            using (CommandBridgeHarness harness = CommandBridgeHarness.CreateAction(
                GameplayTriggerGameplayCommandActionKinds.SetComponentAttribute,
                AttributeId,
                65L))
            {
                GameplayEntityId target = CreateAttributedTarget(harness.World, 80);
                harness.QueueAction(RuntimeFrame.Zero, target);

                harness.Host.Tick(1L, 0d);

                Assert.AreEqual(65, CurrentValue(harness.World, target));
                GameplayTriggerRuntimeEvent result = harness.TakeOnlyActionResult();
                Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Succeeded, result.Status);
                Assert.AreEqual((int)GameplayComponentCommandResultReasonCode.ComponentAttributeSet,
                    result.ReasonCode);
                Assert.AreEqual(80L, result.ResultPayload0);
                Assert.AreEqual(65L, result.ResultPayload1);
                Assert.AreEqual(-15L, result.ResultPayload2);
            }
        }

        [Test]
        public void AddAttribute_EndToEndPublishesExactSuccess()
        {
            using (CommandBridgeHarness harness = CommandBridgeHarness.CreateAction(
                GameplayTriggerGameplayCommandActionKinds.AddComponentAttribute,
                AttributeId,
                -7L))
            {
                GameplayEntityId target = CreateAttributedTarget(harness.World, 80);
                harness.QueueAction(RuntimeFrame.Zero, target);

                harness.Host.Tick(1L, 0d);

                Assert.AreEqual(73, CurrentValue(harness.World, target));
                GameplayTriggerRuntimeEvent result = harness.TakeOnlyActionResult();
                Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Succeeded, result.Status);
                Assert.AreEqual((int)GameplayComponentCommandResultReasonCode.ComponentAttributeAdded,
                    result.ReasonCode);
                Assert.AreEqual(80L, result.ResultPayload0);
                Assert.AreEqual(73L, result.ResultPayload1);
                Assert.AreEqual(-7L, result.ResultPayload2);
            }
        }

        [Test]
        public void DirectAbility_EndToEndPublishesExactSuccess()
        {
            using (CommandBridgeHarness harness = CommandBridgeHarness.CreateAction(
                GameplayTriggerGameplayCommandActionKinds.CastComponentAbility,
                AbilityId,
                0L))
            {
                GameplayEntityId target = CreateAttributedTarget(harness.World, 80);
                harness.QueueAction(RuntimeFrame.Zero, target);

                harness.Host.Tick(1L, 0d);

                Assert.AreEqual(70, CurrentValue(harness.World, target));
                GameplayTriggerRuntimeEvent result = harness.TakeOnlyActionResult();
                Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Succeeded, result.Status);
                Assert.AreEqual((int)GameplayComponentCommandResultReasonCode.ComponentAbilityCastSucceeded,
                    result.ReasonCode);
                Assert.AreEqual(AbilityId, result.ResultPayload0);
                Assert.AreEqual(0L, result.ResultPayload1);
                Assert.AreEqual(0L, result.ResultPayload2);
            }
        }

        [Test]
        public void AcceptedSemanticRejections_DoNotRetryAndPublishExactReasons()
        {
            using (CommandBridgeHarness stale = CommandBridgeHarness.CreateAction(
                GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity,
                0L,
                0L))
            {
                GameplayEntityId target = stale.World.CreateEntity();
                stale.QueueAction(RuntimeFrame.Zero, target);
                Assert.IsTrue(stale.World.DestroyEntity(target));
                stale.Host.Tick(1L, 0d);
                Assert.AreEqual(0, stale.Runtime.ReliableOutboxRecordCount);
                GameplayTriggerRuntimeEvent result = stale.TakeOnlyActionResult();
                Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Rejected, result.Status);
                Assert.AreEqual((int)GameplayComponentCommandResultReasonCode.MissingComponentEntity,
                    result.ReasonCode);
                Assert.AreEqual(target.Index, result.ResultPayload0);
                Assert.AreEqual(target.Generation, result.ResultPayload1);
            }

            using (CommandBridgeHarness missingSet = CommandBridgeHarness.CreateAction(
                GameplayTriggerGameplayCommandActionKinds.AddComponentAttribute,
                AttributeId,
                1L))
            {
                GameplayEntityId target = missingSet.World.CreateEntity();
                missingSet.QueueAction(RuntimeFrame.Zero, target);
                missingSet.Host.Tick(1L, 0d);
                Assert.AreEqual(0, missingSet.Runtime.ReliableOutboxRecordCount);
                GameplayTriggerRuntimeEvent result = missingSet.TakeOnlyActionResult();
                Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Rejected, result.Status);
                Assert.AreEqual((int)GameplayComponentCommandResultReasonCode.MissingAttributeSet,
                    result.ReasonCode);
                Assert.AreEqual(AttributeId, result.ResultPayload0);
            }

            using (CommandBridgeHarness missingAbility = CommandBridgeHarness.CreateAction(
                GameplayTriggerGameplayCommandActionKinds.CastComponentAbility,
                AbilityId + 100,
                0L))
            {
                GameplayEntityId target = CreateAttributedTarget(missingAbility.World, 80);
                missingAbility.QueueAction(RuntimeFrame.Zero, target);
                missingAbility.Host.Tick(1L, 0d);
                GameplayTriggerRuntimeEvent result = missingAbility.TakeOnlyActionResult();
                Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Rejected, result.Status);
                Assert.AreEqual((int)GameplayComponentCommandResultReasonCode.MissingComponentAbility,
                    result.ReasonCode);
                Assert.AreEqual(AbilityId + 100, result.ResultPayload0);
            }
        }

        [Test]
        public void AbilityRuleAndEffectRejections_PreserveTypedNumericDetails()
        {
            using (CommandBridgeHarness cooldown = CommandBridgeHarness.CreateAction(
                GameplayTriggerGameplayCommandActionKinds.CastComponentAbility,
                CooldownAbilityId,
                0L))
            {
                GameplayEntityId target = CreateAttributedTarget(cooldown.World, 80);
                cooldown.World.GetOrCreateStore<GameplayAbilityCooldownComponent>().Set(
                    target,
                    new GameplayAbilityCooldownComponent(
                        new GameplayAbilityCooldownEntry(CooldownAbilityId, 9L)));
                cooldown.QueueAction(RuntimeFrame.Zero, target);
                cooldown.Host.Tick(1L, 0d);
                GameplayTriggerRuntimeEvent result = cooldown.TakeOnlyActionResult();
                Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Rejected, result.Status);
                Assert.AreEqual((int)GameplayComponentCommandResultReasonCode.ComponentAbilityOnCooldown,
                    result.ReasonCode);
                Assert.AreEqual(CooldownAbilityId, result.ResultPayload0);
                Assert.AreEqual(CooldownAbilityId, result.ResultPayload1);
                Assert.AreEqual(8L, result.ResultPayload2);
            }

            using (CommandBridgeHarness effect = CommandBridgeHarness.CreateAction(
                GameplayTriggerGameplayCommandActionKinds.CastComponentAbility,
                EffectFailureAbilityId,
                0L))
            {
                GameplayEntityId target = CreateAttributedTarget(effect.World, int.MaxValue);
                effect.QueueAction(RuntimeFrame.Zero, target);
                effect.Host.Tick(1L, 0d);
                GameplayTriggerRuntimeEvent result = effect.TakeOnlyActionResult();
                Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Rejected, result.Status);
                Assert.AreEqual((int)GameplayComponentCommandResultReasonCode.ComponentAbilityEffectFailed,
                    result.ReasonCode);
                Assert.AreEqual(EffectFailureAbilityId, result.ResultPayload0);
                Assert.AreEqual(0L, result.ResultPayload1);
                Assert.AreEqual(0L, result.ResultPayload2);
            }
        }

        [Test]
        public void CheckpointRoundTrip_RegeneratesDerivedCommandExactlyOnceAndReplayFiltersIt()
        {
            var sourceSink = new GameplayTriggerGameplayReliableCommandActionSink(
                GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity);
            GameplayTriggerRuntime source = QueuedDestroyRuntime(
                new GameplayEntityId(8, 1),
                eventSource: null,
                sink: sourceSink);
            RuntimeSaveState checkpoint = Capture(source);
            string beforeBytes = Payload(checkpoint);
            long beforeHash = Hash(source);
            StringAssert.DoesNotContain("commandSequence", beforeBytes);
            StringAssert.DoesNotContain("resultPayload", beforeBytes);
            StringAssert.DoesNotContain("envelopeTypeId", beforeBytes);

            var restoredSink = new GameplayTriggerGameplayReliableCommandActionSink(
                GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity);
            GameplayTriggerRuntime restored = CreateRuntime(
                restoredSink,
                DestroyAction(restoredSink.ActionKindId),
                eventSource: null);
            RuntimeSaveStateResult<bool> restore = Participant(restored).RestoreSaveState(checkpoint);
            Assert.IsTrue(restore.Success, restore.Error.ToString());
            string afterBytes = Payload(Capture(restored));
            Assert.AreEqual(beforeBytes, afterBytes);
            Assert.AreEqual(beforeHash, Hash(restored));

            using (CommandBridgeHarness harness = CommandBridgeHarness.CreateForRuntime(restored))
            {
                GameplayEntityId target = harness.World.CreateEntity();
                while (target.Index < 8)
                    target = harness.World.CreateEntity();
                Assert.AreEqual(new GameplayEntityId(8, 1), target);

                harness.Host.Tick(1L, 0d);

                Assert.AreEqual(0, harness.Runtime.ReliableOutboxRecordCount);
                Assert.AreEqual(1, harness.Validator.AcceptedCount);
                Assert.IsFalse(harness.World.IsAlive(target));
                harness.Host.Tick(2L, 0d);
                Assert.AreEqual(1, harness.Validator.AcceptedCount);

                RuntimeReplayRecorder recorder = RuntimeReplayRecorder.CreateExogenousStream(
                    new RuntimeReplayHeader(
                        1,
                        "trigger-command-bridge",
                        "config",
                        "resources",
                        RuntimeFrame.Zero),
                    harness.SourceRegistry);
                RuntimeReplayFrameRecord replayFrame = recorder.RecordFrame(
                    new RuntimeFrame(1),
                    new[] { harness.Validator.LastCommand },
                    0L);
                Assert.AreEqual(RuntimeReplayCommandCaptureMode.ExogenousFiltered,
                    recorder.CaptureMode);
                Assert.AreEqual(0, replayFrame.Commands.Count);
            }
        }

        [TestCase(GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntityValue, 0L, 0L)]
        [TestCase(GameplayTriggerGameplayCommandActionKinds.SetComponentAttributeValue, AttributeId, 77L)]
        [TestCase(GameplayTriggerGameplayCommandActionKinds.AddComponentAttributeValue, AttributeId, -3L)]
        [TestCase(GameplayTriggerGameplayCommandActionKinds.CastComponentAbilityValue, AbilityId, 0L)]
        public void Checkpoint_AllFourActionRecordsRoundTripCanonical(
            int actionKindValue,
            long payload0,
            long payload1)
        {
            var actionKindId = new GameplayTriggerActionKindId(actionKindValue);
            var sourceSink = new GameplayTriggerGameplayReliableCommandActionSink(actionKindId);
            GameplayTriggerRuntime source = CreateRuntime(
                sourceSink,
                Action(actionKindId, payload0, payload1),
                eventSource: null);
            Queue(source, new RuntimeFrame(4), new GameplayEntityId(10, 2));
            RuntimeSaveState checkpoint = Capture(source);
            string expectedPayload = Payload(checkpoint);
            long expectedHash = Hash(source);

            var restoredSink = new GameplayTriggerGameplayReliableCommandActionSink(actionKindId);
            GameplayTriggerRuntime restored = CreateRuntime(
                restoredSink,
                Action(actionKindId, payload0, payload1),
                eventSource: null);
            RuntimeSaveStateResult<bool> restore = Participant(restored).RestoreSaveState(checkpoint);

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(expectedPayload, Payload(Capture(restored)));
            Assert.AreEqual(expectedHash, Hash(restored));
            Assert.AreEqual(1, restored.ReliableOutboxRecordCount);
            GameplayTriggerReliableOutboxRecord record = restored.GetReliableOutboxRecord(0);
            Assert.AreEqual(new RuntimeFrame(4), record.OriginalActivationFrame);
            Assert.AreEqual(new RuntimeFrame(5), record.OriginalDueFrame);
            Assert.AreEqual(actionKindId, record.ActionKindId);
            Assert.AreEqual(payload0, record.ActionPayload0);
            Assert.AreEqual(payload1, record.ActionPayload1);
            Assert.AreEqual(0L, record.ActionPayload2);
        }

        [Test]
        public void ResultRelay_InvalidInputsDoNotIncrementDropButValidOverflowIsPresentationOnly()
        {
            var eventSource = new GameplayTriggerRuntimeEventSource(0);
            var sink = new GameplayTriggerGameplayReliableCommandActionSink(
                GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity);
            CreateRuntime(sink, DestroyAction(sink.ActionKindId), eventSource);
            var relay = new GameplayTriggerGameplayCommandResultRelay(eventSource, SourceId);

            relay.Publish(default);
            GameplayComponentCommandResult unrelated = ValidDestroyResult(sourceId: SourceId + 1);
            relay.Publish(in unrelated);
            GameplayComponentCommandResult invalidEnvelope = ValidDestroyResult(
                sourceId: SourceId,
                envelope: new RuntimeCommandEnvelope(
                    GameplayTriggerGameplayCommandContracts.EnvelopeTypeId,
                    GameplayTriggerGameplayCommandContracts.EnvelopeSchemaVersion,
                    RuntimeCommandProvenanceKind.Derived,
                    data0: 1L,
                    data1: 0L,
                    data2: GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntityValue,
                    data3: 0L,
                    data4: 0L,
                    data5: 1L));
            relay.Publish(in invalidEnvelope);
            Assert.AreEqual(0L, eventSource.DroppedCount);
            Assert.AreEqual(GameplayTriggerRuntimeEventDropReason.None,
                eventSource.LastDropReason);

            GameplayComponentCommandResult valid = ValidDestroyResult(SourceId);
            relay.Publish(in valid);
            Assert.AreEqual(1L, eventSource.DroppedCount);
            Assert.AreEqual(GameplayTriggerRuntimeEventDropReason.SourceCapacityExceeded,
                eventSource.LastDropReason);
        }

        [Test]
        public void BridgeOwnedRetainedAcceptedAndResultValidationPaths_AllocateZeroAfterWarmup()
        {
            using (CommandBridgeHarness harness = CommandBridgeHarness.Create(
                commandCapacity: 1,
                prefillFrameOne: false))
            {
                GameplayEntityId target = harness.World.CreateEntity();
                harness.QueueAction(RuntimeFrame.Zero, target);
                GameplayTriggerReliableOutboxRecord record =
                    harness.Runtime.GetReliableOutboxRecord(0);
                var drained = new List<RuntimeCommand>(1);

                Assert.IsTrue(harness.Transfer.TryTransfer(
                    new RuntimeFrame(1), in record).Accepted);
                Assert.AreEqual(1, harness.CommandBuffer.DrainForFrame(
                    new RuntimeFrame(1), drained));
                drained.Clear();

                long acceptedBefore = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 64; i++)
                {
                    RuntimeFrame frame = harness.CommandBuffer.CurrentFrame;
                    harness.Transfer.TryTransfer(frame, in record);
                    harness.CommandBuffer.DrainForFrame(frame, drained);
                    drained.Clear();
                }
                long acceptedBytes = GC.GetAllocatedBytesForCurrentThread() - acceptedBefore;
                Assert.AreEqual(0L, acceptedBytes);

                harness.Transfer.SetEnabled(false);
                RuntimeFrame retainedFrame = harness.CommandBuffer.CurrentFrame;
                harness.Transfer.TryTransfer(retainedFrame, in record);
                long retainedBefore = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 64; i++)
                    harness.Transfer.TryTransfer(retainedFrame, in record);
                long retainedBytes = GC.GetAllocatedBytesForCurrentThread() - retainedBefore;
                Assert.AreEqual(0L, retainedBytes);
            }

            var eventSource = new GameplayTriggerRuntimeEventSource(1);
            var sink = new GameplayTriggerGameplayReliableCommandActionSink(
                GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity);
            CreateRuntime(sink, DestroyAction(sink.ActionKindId), eventSource);
            var relay = new GameplayTriggerGameplayCommandResultRelay(eventSource, SourceId);
            GameplayComponentCommandResult rejected = ValidDestroyResult(
                SourceId,
                status: GameplayComponentCommandResultStatus.Rejected,
                reason: GameplayComponentCommandResultReasonCode.MissingComponentEntity);
            var observer = new GameplayTriggerRuntimeEventObserverBuffer(1);
            var router = new GameplayTriggerEventRouterSystem(
                eventSource,
                new[]
                {
                    new GameplayTriggerRuntimeEventObserverRegistration(
                        "alloc",
                        observer)
                });
            GameplaySystemContext context = EmptyContext(new RuntimeFrame(1));
            relay.Publish(in rejected);
            router.Tick(context);
            observer.TryDequeue(out _);

            long resultBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                relay.Publish(in rejected);
                router.Tick(context);
                observer.TryDequeue(out _);
            }
            long resultBytes = GC.GetAllocatedBytesForCurrentThread() - resultBefore;
            Assert.AreEqual(0L, resultBytes);
        }

        private static GameplayComponentCommandResult ValidDestroyResult(
            int sourceId,
            RuntimeCommandEnvelope envelope = default,
            GameplayComponentCommandResultStatus status =
                GameplayComponentCommandResultStatus.Succeeded,
            GameplayComponentCommandResultReasonCode reason =
                GameplayComponentCommandResultReasonCode.ComponentEntityDestroyed)
        {
            if (!envelope.IsPresent)
            {
                envelope = new RuntimeCommandEnvelope(
                    GameplayTriggerGameplayCommandContracts.EnvelopeTypeId,
                    GameplayTriggerGameplayCommandContracts.EnvelopeSchemaVersion,
                    RuntimeCommandProvenanceKind.Derived,
                    data0: 1L,
                    data1: 0L,
                    data2: GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntityValue,
                    data3: 1L,
                    data4: 0L,
                    data5: 1L);
            }
            return new GameplayComponentCommandResult(
                new RuntimeFrame(1),
                sourceId,
                GameplayRuntimeCommandIds.DestroyComponentEntity,
                targetId: 1,
                commandPayload0: 1,
                commandPayload1: 1,
                commandPayload2: 0,
                commandSequence: 0L,
                traceIdIsEmpty: true,
                envelope,
                status,
                reason,
                status == GameplayComponentCommandResultStatus.Rejected ? 1L : 0L,
                status == GameplayComponentCommandResultStatus.Rejected ? 1L : 0L,
                0L);
        }

        private static GameplaySystemContext EmptyContext(RuntimeFrame frame)
        {
            var world = new GameplayComponentWorld();
            return new GameplaySystemContext(
                frame,
                0d,
                0d,
                new GameplayWorld(),
                Array.Empty<RuntimeCommand>(),
                world.Events,
                componentWorld: world);
        }

        private static GameplayTriggerRuntime QueuedDestroyRuntime(
            GameplayEntityId target,
            GameplayTriggerRuntimeEventSource eventSource,
            IGameplayTriggerActionSink sink = null)
        {
            sink = sink ?? new GameplayTriggerGameplayReliableCommandActionSink(
                GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity);
            GameplayTriggerRuntime runtime = CreateRuntime(
                sink,
                DestroyAction(sink.ActionKindId),
                eventSource);
            Queue(runtime, RuntimeFrame.Zero, target);
            return runtime;
        }

        private static GameplayTriggerActionPreparation Preparation(
            GameplayTriggerActionKindId actionKindId,
            GameplayTriggerTargetRef target,
            long payload0,
            long payload1,
            long payload2)
        {
            return new GameplayTriggerActionPreparation(
                RuntimeFrame.Zero,
                new GameplayTriggerBindingId(1),
                new GameplayEntityId(99, 1),
                target,
                new GameplayTriggerRouteId(1),
                0,
                default,
                new GameplayTriggerActionInstruction(
                    actionKindId,
                    GameplayTriggerTargetSelector.Subject,
                    payload0: payload0,
                    payload1: payload1,
                    payload2: payload2));
        }

        private static GameplayTriggerActionInstruction DestroyAction(
            GameplayTriggerActionKindId actionKindId)
        {
            return new GameplayTriggerActionInstruction(
                actionKindId,
                GameplayTriggerTargetSelector.Subject);
        }

        private static GameplayTriggerActionInstruction Action(
            GameplayTriggerActionKindId actionKindId,
            long payload0,
            long payload1)
        {
            return new GameplayTriggerActionInstruction(
                actionKindId,
                GameplayTriggerTargetSelector.Subject,
                payload0: payload0,
                payload1: payload1);
        }

        private static GameplayEntityId CreateAttributedTarget(
            GameplayComponentWorld world,
            int value)
        {
            GameplayEntityId target = world.CreateEntity();
            world.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                target,
                new GameplayAttributeSetComponent(
                    new GameplayAttributeValue(AttributeId, value, value)));
            return target;
        }

        private static int CurrentValue(
            GameplayComponentWorld world,
            GameplayEntityId target)
        {
            Assert.IsTrue(world.TryGetStore(
                out GameplayComponentStore<GameplayAttributeSetComponent> store));
            Assert.IsTrue(store.TryGet(target, out GameplayAttributeSetComponent attributes));
            return attributes.GetCurrentValueOrDefault(AttributeId);
        }

        private static GameplayTriggerRuntime CreateRuntime(
            IGameplayTriggerActionSink sink,
            GameplayTriggerActionInstruction action,
            GameplayTriggerRuntimeEventSource eventSource)
        {
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal,
                GameplayTriggerSignalOrdering.UnorderedMultiset);
            var route = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                GameplayTriggerOccurrenceKind.Pulsed,
                GameplayTriggerConditionProgram.Always,
                new[] { action },
                new GameplayTriggerActivationPolicy(
                    GameplayTriggerStateScope.PerSubject,
                    maxActivations: 1));
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
                new[] { sink },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            var options = new GameplayTriggerRuntimeOptions(
                8, 32, 32, 32, 32, 32, 32, 32, 8, 16, 0, 8);
            return eventSource == null
                ? new GameplayTriggerRuntime(catalog, options)
                : new GameplayTriggerRuntime(catalog, options, eventSource);
        }

        private static void Queue(
            GameplayTriggerRuntime runtime,
            RuntimeFrame frame,
            GameplayEntityId subject)
        {
            Assert.IsTrue(runtime.TrySubmitSignals(
                frame,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[] { new GameplayTriggerSignal(1, subject) },
                out GameplayTriggerResult result),
                result.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(frame).Success);
        }

        private static void QueueMany(
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
            Assert.IsTrue(runtime.Evaluate(frame).Success);
        }

        private static RuntimeCommandSubmissionGate CreateGate(
            RuntimeCommandBuffer buffer,
            RuntimeCommandIngressKind ingressKind)
        {
            var registry = new RuntimeCommandSourceRegistry(1);
            Assert.IsTrue(registry.TryRegister(
                SourceId,
                RuntimeCommandSourceKind.Derived,
                new[]
                {
                    GameplayRuntimeCommandIds.DestroyComponentEntity,
                    GameplayRuntimeCommandIds.SetComponentAttribute,
                    GameplayRuntimeCommandIds.AddComponentAttribute,
                    GameplayRuntimeCommandIds.CastComponentAbility
                },
                out RuntimeCommandSourceRegistrationResult registration),
                registration.ErrorCode.ToString());
            return new RuntimeCommandSubmissionGate(registry, buffer, ingressKind);
        }

        private static GameplayRuntimeModule CreateGameplayModule(
            RuntimeCommandBuffer buffer,
            IGameplaySystem system)
        {
            var pipeline = new GameplaySystemPipeline();
            pipeline.Add(system);
            return new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                buffer,
                tickWorldAutomatically: false,
                systemPipeline: pipeline);
        }

        private static GameplayTriggerRuntimeSaveStateParticipant Participant(
            GameplayTriggerRuntime runtime)
        {
            return new GameplayTriggerRuntimeSaveStateParticipant(
                runtime,
                new RestoreEntityValidator());
        }

        private static RuntimeSaveState Capture(GameplayTriggerRuntime runtime)
        {
            RuntimeSaveStateResult<RuntimeSaveState> result = Participant(runtime)
                .CaptureSaveState();
            Assert.IsTrue(result.Success, result.Error.ToString());
            return result.Value;
        }

        private static string Payload(RuntimeSaveState state)
        {
            return state.ModuleStates[0].CustomState.PayloadJson;
        }

        private static long Hash(GameplayTriggerRuntime runtime)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.GameplayTriggers, RuntimeHashSchemaIds.Version1);
            new GameplayTriggerRuntimeHashContributor(runtime).Contribute(
                new RuntimeHashContext(runtime.AuthorityLastEvaluatedFrame),
                accumulator);
            return accumulator.ToHash();
        }

        private sealed class LegacyReliableSink : IGameplayTriggerActionSink
        {
            public LegacyReliableSink(GameplayTriggerActionKindId actionKindId)
            {
                ActionKindId = actionKindId;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.ReliableDeferred;
            public int TryPrepareCount { get; private set; }

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                TryPrepareCount++;
                token = 0;
                throw new InvalidOperationException("Legacy reliable callbacks must remain bypassed.");
            }

            public void Abort(int token) =>
                throw new InvalidOperationException("Legacy reliable callbacks must remain bypassed.");

            public void Commit(int token, in GameplayTriggerActionIntent intent) =>
                throw new InvalidOperationException("Legacy reliable callbacks must remain bypassed.");
        }

        private sealed class CaptureValidator : IRuntimeCommandValidator
        {
            public int AcceptedCount { get; private set; }
            public RuntimeCommand LastCommand { get; private set; }

            public RuntimeCommandValidationResult Validate(RuntimeCommand command)
            {
                AcceptedCount++;
                LastCommand = command;
                return RuntimeCommandValidationResult.Accepted(command);
            }
        }

        private sealed class RestoreEntityValidator : IGameplayTriggerRestoreEntityValidator
        {
            public bool IsRestoredEntityValid(GameplayEntityId entityId) => entityId.IsValid;
        }

        private sealed class CommandBridgeHarness : IDisposable
        {
            private readonly List<GameplayTriggerRuntimeEvent> _results =
                new List<GameplayTriggerRuntimeEvent>();

            private CommandBridgeHarness(
                GameplayTriggerRuntime runtime,
                GameplayTriggerRuntimeEventSource eventSource,
                GameplayTriggerRuntimeEventObserverBuffer observer,
                GameplayComponentWorld world,
                RuntimeHost host,
                RuntimeCommandSourceRegistry sourceRegistry,
                CaptureValidator validator,
                RuntimeCommandBuffer commandBuffer,
                GameplayTriggerGameplayReliableCommandTransfer transfer,
                IGameplaySystem consumer)
            {
                Runtime = runtime;
                EventSource = eventSource;
                Observer = observer;
                World = world;
                Host = host;
                SourceRegistry = sourceRegistry;
                Validator = validator;
                CommandBuffer = commandBuffer;
                Transfer = transfer;
                Consumer = consumer;
            }

            public GameplayTriggerRuntime Runtime { get; }
            public GameplayTriggerRuntimeEventSource EventSource { get; }
            public GameplayTriggerRuntimeEventObserverBuffer Observer { get; }
            public GameplayComponentWorld World { get; }
            public RuntimeHost Host { get; }
            public RuntimeCommandSourceRegistry SourceRegistry { get; }
            public CaptureValidator Validator { get; }
            public RuntimeCommandBuffer CommandBuffer { get; }
            public GameplayTriggerGameplayReliableCommandTransfer Transfer { get; }
            public IGameplaySystem Consumer { get; }

            public static CommandBridgeHarness Create(
                int commandCapacity,
                bool prefillFrameOne)
            {
                var eventSource = new GameplayTriggerRuntimeEventSource(64);
                var sink = new GameplayTriggerGameplayReliableCommandActionSink(
                    GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity);
                GameplayTriggerRuntime runtime = CreateRuntime(
                    sink,
                    DestroyAction(sink.ActionKindId),
                    eventSource);
                return CreateCore(runtime, eventSource, commandCapacity, prefillFrameOne);
            }

            public static CommandBridgeHarness CreateAction(
                GameplayTriggerActionKindId actionKindId,
                long payload0,
                long payload1)
            {
                var eventSource = new GameplayTriggerRuntimeEventSource(64);
                var sink = new GameplayTriggerGameplayReliableCommandActionSink(actionKindId);
                GameplayTriggerRuntime runtime = CreateRuntime(
                    sink,
                    Action(actionKindId, payload0, payload1),
                    eventSource);
                return CreateCore(
                    runtime,
                    eventSource,
                    commandCapacity: 4,
                    prefillFrameOne: false,
                    actionKindId);
            }

            public static CommandBridgeHarness CreateForRuntime(
                GameplayTriggerRuntime runtime)
            {
                var eventSource = new GameplayTriggerRuntimeEventSource(64);
                GameplayTriggerRuntimeSaveStateParticipant sourceParticipant = Participant(runtime);
                RuntimeSaveState checkpoint = sourceParticipant.CaptureSaveState().Value;
                var sink = new GameplayTriggerGameplayReliableCommandActionSink(
                    GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity);
                GameplayTriggerRuntime rebound = CreateRuntime(
                    sink,
                    DestroyAction(sink.ActionKindId),
                    eventSource);
                Assert.IsTrue(Participant(rebound).RestoreSaveState(checkpoint).Success);
                return CreateCore(rebound, eventSource, 1, false);
            }

            private static CommandBridgeHarness CreateCore(
                GameplayTriggerRuntime runtime,
                GameplayTriggerRuntimeEventSource eventSource,
                int commandCapacity,
                bool prefillFrameOne,
                GameplayTriggerActionKindId actionKindId = default)
            {
                var validator = new CaptureValidator();
                RuntimeCommandBuffer buffer = RuntimeCommandBuffer.CreateBounded(
                    validator,
                    RuntimeFrame.Zero,
                    commandCapacity);
                if (prefillFrameOne)
                {
                    Assert.IsTrue(buffer.Enqueue(
                        GameplayRuntimeCommandFactory.CreateComponentEntity(
                            new RuntimeFrame(1))).Success);
                }
                var sourceRegistry = new RuntimeCommandSourceRegistry(1);
                Assert.IsTrue(sourceRegistry.TryRegister(
                    SourceId,
                    RuntimeCommandSourceKind.Derived,
                    new[]
                    {
                        GameplayRuntimeCommandIds.DestroyComponentEntity,
                        GameplayRuntimeCommandIds.SetComponentAttribute,
                        GameplayRuntimeCommandIds.AddComponentAttribute,
                        GameplayRuntimeCommandIds.CastComponentAbility
                    },
                    out _));
                var gate = new RuntimeCommandSubmissionGate(
                    sourceRegistry,
                    buffer,
                    RuntimeCommandIngressKind.TrustedDerived);
                var world = new GameplayComponentWorld();
                var relay = new GameplayTriggerGameplayCommandResultRelay(
                    eventSource,
                    SourceId);
                var entitySystem = new GameplayComponentEntityCommandSystem(
                    GameplayComponentEntityCommandSystem.DefaultSystemId,
                    20,
                    relay);
                var attributeSystem = new GameplayAttributeCommandSystem(
                    GameplayAttributeCommandSystem.DefaultSystemId,
                    40,
                    relay);
                var componentAbilities = new GameplayComponentAbilityRegistry();
                componentAbilities.Register(new GameplayComponentAttributeDeltaAbility(
                    AbilityId,
                    AttributeId,
                    -10,
                    GameplayComponentTargetMode.Self));
                componentAbilities.Register(new GameplayComponentAttributeDeltaAbility(
                    CooldownAbilityId,
                    AttributeId,
                    -1,
                    GameplayComponentTargetMode.Self,
                    new GameplayComponentAbilityRuleSet(cooldownFrames: 10L)));
                componentAbilities.Register(new GameplayComponentAttributeDeltaAbility(
                    EffectFailureAbilityId,
                    AttributeId,
                    1,
                    GameplayComponentTargetMode.Self));
                var abilitySystem = new GameplayComponentAbilityCommandSystem(
                    componentAbilities,
                    requestStore: null,
                    targetingService: null,
                    buffRequests: null,
                    GameplayComponentAbilityCommandSystem.DefaultSystemId,
                    50,
                    relay);
                var observer = new GameplayTriggerRuntimeEventObserverBuffer(64);
                var router = new GameplayTriggerEventRouterSystem(
                    eventSource,
                    new[]
                    {
                        new GameplayTriggerRuntimeEventObserverRegistration(
                            "trigger-command-bridge-test",
                            observer)
                    });
                var pipeline = new GameplaySystemPipeline();
                pipeline.Add(entitySystem);
                pipeline.Add(attributeSystem);
                pipeline.Add(abilitySystem);
                pipeline.Add(router);
                var gameplayModule = new GameplayRuntimeModule(
                    new GameplayWorld(),
                    new GameplayAbilityRegistry(),
                    buffer,
                    tickWorldAutomatically: false,
                    systemPipeline: pipeline,
                    componentWorld: world);
                var host = new RuntimeHost();
                if (!actionKindId.IsValid)
                    actionKindId = GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity;
                IGameplaySystem consumer =
                    actionKindId == GameplayTriggerGameplayCommandActionKinds.DestroyComponentEntity
                        ? (IGameplaySystem)entitySystem
                        : actionKindId == GameplayTriggerGameplayCommandActionKinds.SetComponentAttribute ||
                          actionKindId == GameplayTriggerGameplayCommandActionKinds.AddComponentAttribute
                            ? attributeSystem
                            : abilitySystem;
                var transfer = new GameplayTriggerGameplayReliableCommandTransfer(
                    actionKindId,
                    SourceId,
                    host,
                    gameplayModule,
                    consumer,
                    gate);
                var outbox = new GameplayTriggerReliableOutboxModule(
                    runtime,
                    new IGameplayTriggerReliableTargetTransfer[] { transfer });
                host.RegisterModule(outbox);
                host.RegisterModule(gameplayModule);
                host.Initialize();
                host.Start();
                return new CommandBridgeHarness(
                    runtime,
                    eventSource,
                    observer,
                    world,
                    host,
                    sourceRegistry,
                    validator,
                    buffer,
                    transfer,
                    consumer);
            }

            public void QueueDestroy(RuntimeFrame frame, GameplayEntityId target)
            {
                Queue(Runtime, frame, target);
            }

            public void QueueAction(RuntimeFrame frame, GameplayEntityId target)
            {
                Queue(Runtime, frame, target);
            }

            public GameplayTriggerRuntimeEvent TakeOnlyActionResult()
            {
                _results.Clear();
                while (Observer.TryDequeue(out GameplayTriggerRuntimeEvent evt))
                {
                    if (evt.Kind == GameplayTriggerRuntimeEventKind.ActionResult)
                        _results.Add(evt);
                }
                Assert.AreEqual(1, _results.Count);
                return _results[0];
            }

            public GameplayTriggerRuntimeEvent[] TakeActionResults(int expectedCount)
            {
                _results.Clear();
                while (Observer.TryDequeue(out GameplayTriggerRuntimeEvent evt))
                {
                    if (evt.Kind == GameplayTriggerRuntimeEventKind.ActionResult)
                        _results.Add(evt);
                }
                Assert.AreEqual(expectedCount, _results.Count);
                return _results.ToArray();
            }

            public void Dispose()
            {
                Host.Dispose();
                Observer.Clear();
            }
        }
    }
}
