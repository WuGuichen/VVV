using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerRuntimeEventRouterTests
    {
        private static readonly GameplayTriggerSourceBindingId SourceBindingId =
            new GameplayTriggerSourceBindingId(101);
        private static readonly GameplayTriggerActionKindId ActionKindId =
            new GameplayTriggerActionKindId(1001);
        private static readonly GameplayEntityId Owner = new GameplayEntityId(7, 2);
        private static readonly GameplayEntityId Subject = new GameplayEntityId(11, 3);
        private static readonly GameplayEntityId Instigator = new GameplayEntityId(12, 4);

        [Test]
        public void StableEnumsAndPublicSurface_MatchFrozenH1Contract()
        {
            Assert.AreEqual(0, (int)GameplayTriggerRuntimeEventKind.None);
            Assert.AreEqual(1, (int)GameplayTriggerRuntimeEventKind.Occurrence);
            Assert.AreEqual(2, (int)GameplayTriggerRuntimeEventKind.ActivationAccepted);
            Assert.AreEqual(3, (int)GameplayTriggerRuntimeEventKind.ActionDispatched);
            Assert.AreEqual(4, (int)GameplayTriggerRuntimeEventKind.RuntimeDiagnostic);
            Assert.AreEqual(5, (int)GameplayTriggerRuntimeEventKind.CommandResult);
            Assert.AreEqual(6, (int)GameplayTriggerRuntimeEventKind.InternalDeferredResult);
            Assert.AreEqual(7, (int)GameplayTriggerRuntimeEventKind.ReliableTransferResult);
            Assert.AreEqual(8, (int)GameplayTriggerRuntimeEventKind.ActionResult);
            Assert.AreEqual(5, (int)GameplayTriggerRuntimeEventDropReason.InvalidActionResult);
            Assert.AreEqual(GameplaySystemPhase.Diagnostics,
                CreateRouter(out _, out _, out _).Phase);
            Assert.AreEqual(1000, GameplayTriggerEventRouterSystem.DefaultPriority);
            Assert.AreEqual("mxframework.gameplay.triggers.events.route",
                GameplayTriggerEventRouterSystem.DefaultSystemId);
            Assert.IsFalse(RuntimeHelpers.IsReferenceOrContainsReferences<GameplayTriggerRuntimeEvent>());

            ConstructorInfo[] constructors = typeof(GameplayTriggerRuntime).GetConstructors();
            Assert.AreEqual(2, constructors.Length);
            Assert.IsNotNull(typeof(GameplayTriggerRuntime).GetConstructor(new[]
            {
                typeof(GameplayTriggerCatalog),
                typeof(GameplayTriggerRuntimeOptions)
            }));
            Assert.IsNotNull(typeof(GameplayTriggerRuntime).GetConstructor(new[]
            {
                typeof(GameplayTriggerCatalog),
                typeof(GameplayTriggerRuntimeOptions),
                typeof(GameplayTriggerRuntimeEventSource)
            }));
            Assert.IsNull(typeof(GameplayTriggerRuntimeEventSource).GetMethod(
                "TryPublish",
                BindingFlags.Instance | BindingFlags.Public));
        }

        [Test]
        public void ProducerOwnershipAndConstructionFailures_AreAtomic()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(new RecordingSink());
            GameplayTriggerRuntimeOptions options = Options();
            var source = new GameplayTriggerRuntimeEventSource(8);

            Assert.Throws<InvalidOperationException>(() =>
                new GameplayTriggerEventRouterSystem(source));
            Assert.IsFalse(source.IsProducerOwnerBound);
            Assert.IsFalse(source.IsDrainOwnerBound);

            var firstRuntime = new GameplayTriggerRuntime(catalog, options, source);
            var router = new GameplayTriggerEventRouterSystem(source);
            Assert.IsTrue(source.IsProducerOwnerBound);
            Assert.IsTrue(source.IsDrainOwnerBound);
            Assert.AreEqual(0, router.ObserverCount);
            Assert.Throws<InvalidOperationException>(() =>
                new GameplayTriggerRuntime(catalog, options, source));
            Assert.Throws<InvalidOperationException>(() =>
                new GameplayTriggerEventRouterSystem(source));

            var separateSource = new GameplayTriggerRuntimeEventSource(8);
            var secondRuntime = new GameplayTriggerRuntime(catalog, options, separateSource);
            Assert.AreNotSame(firstRuntime, secondRuntime);

            var invalidSource = new GameplayTriggerRuntimeEventSource(8);
            GameplayTriggerCatalog reliableCatalog = CreateCatalog(
                new ReliableSink(ActionKindId));
            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerRuntime(reliableCatalog, Options(), invalidSource));
            Assert.IsFalse(invalidSource.IsProducerOwnerBound);
            _ = new GameplayTriggerRuntime(
                reliableCatalog,
                Options(maxReliable: 1),
                invalidSource);
            Assert.IsTrue(invalidSource.IsProducerOwnerBound);
        }

        [Test]
        public void RouterRegistryFailures_DoNotPoisonSourceOrObservers()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new RecordingSink(),
                out GameplayTriggerRuntimeEventSource source);
            var observer = new GameplayTriggerRuntimeEventObserverBuffer(4);
            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerEventRouterSystem(
                    source,
                    new[]
                    {
                        new GameplayTriggerRuntimeEventObserverRegistration(
                            "observer",
                            observer)
                    },
                    systemId: " "));
            Assert.IsFalse(source.IsDrainOwnerBound);
            Assert.IsFalse(observer.IsRouterBound);
            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerEventRouterSystem(
                    source,
                    new[]
                    {
                        new GameplayTriggerRuntimeEventObserverRegistration(
                            string.Empty,
                            observer)
                    }));
            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerEventRouterSystem(
                    source,
                    new[]
                    {
                        new GameplayTriggerRuntimeEventObserverRegistration(
                            "null",
                            null)
                    }));
            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerEventRouterSystem(
                    source,
                    new[]
                    {
                        new GameplayTriggerRuntimeEventObserverRegistration("first", observer),
                        new GameplayTriggerRuntimeEventObserverRegistration("second", observer)
                    }));
            var duplicate = new[]
            {
                new GameplayTriggerRuntimeEventObserverRegistration("same", observer),
                new GameplayTriggerRuntimeEventObserverRegistration("same", new GameplayTriggerRuntimeEventObserverBuffer(4))
            };

            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerEventRouterSystem(source, duplicate));
            Assert.IsFalse(source.IsDrainOwnerBound);
            Assert.IsFalse(observer.IsRouterBound);

            var router = new GameplayTriggerEventRouterSystem(
                source,
                new[] { new GameplayTriggerRuntimeEventObserverRegistration("observer", observer) });
            Assert.IsTrue(source.IsDrainOwnerBound);
            Assert.IsTrue(observer.IsRouterBound);

            GameplayTriggerRuntime otherRuntime = CreateRuntime(
                new RecordingSink(),
                out GameplayTriggerRuntimeEventSource otherSource);
            var otherObserver = new GameplayTriggerRuntimeEventObserverBuffer(4);
            _ = new GameplayTriggerEventRouterSystem(
                otherSource,
                new[] { new GameplayTriggerRuntimeEventObserverRegistration("other", otherObserver) });
            Assert.Throws<InvalidOperationException>(() =>
                new GameplayTriggerEventRouterSystem(
                    CreateBoundSource(),
                    new[] { new GameplayTriggerRuntimeEventObserverRegistration("shared", observer) }));
            Assert.IsNotNull(runtime);
            Assert.IsNotNull(otherRuntime);
            Assert.AreEqual(1, router.ObserverCount);
        }

        [Test]
        public void UnboundActionResult_IsInvalidAndDoesNotConsumeSequenceBeforeBinding()
        {
            var source = new GameplayTriggerRuntimeEventSource(2);

            Assert.IsFalse(PublishResult(source, new RuntimeFrame(3)));
            Assert.AreEqual(1L, source.DroppedCount);
            Assert.AreEqual(GameplayTriggerRuntimeEventDropReason.InvalidActionResult,
                source.LastDropReason);
            Assert.AreEqual(1L, source.NextSequence);
            Assert.AreEqual(0, source.PendingCount);

            GameplayTriggerCatalog catalog = CreateCatalog(new RecordingSink());
            _ = new GameplayTriggerRuntime(catalog, Options(), source);
            Assert.IsTrue(PublishResult(source, new RuntimeFrame(3)));
            Assert.AreEqual(1L, source.GetPending(0).SourceSequence);
        }

        [Test]
        public void ActionResultValidation_PrecedesSequenceAndCapacityRejection()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(new RecordingSink());
            var exhausted = new GameplayTriggerRuntimeEventSource(
                capacity: 0,
                nextSequence: long.MaxValue,
                sequenceExhausted: true,
                droppedCount: long.MaxValue - 1L);
            _ = new GameplayTriggerRuntime(catalog, Options(), exhausted);

            Assert.IsFalse(exhausted.TryPublishActionResult(
                RuntimeFrame.Zero,
                activationId: 0L,
                actionIndex: -1,
                default,
                default,
                GameplayTriggerRuntimeEventStatus.None));
            Assert.AreEqual(GameplayTriggerRuntimeEventDropReason.InvalidActionResult,
                exhausted.LastDropReason);
            Assert.AreEqual(long.MaxValue, exhausted.DroppedCount);
            Assert.IsFalse(PublishResult(exhausted, RuntimeFrame.Zero));
            Assert.AreEqual(GameplayTriggerRuntimeEventDropReason.SourceSequenceExhausted,
                exhausted.LastDropReason);

            var full = new GameplayTriggerRuntimeEventSource(0);
            _ = new GameplayTriggerRuntime(catalog, Options(), full);
            Assert.IsFalse(PublishResult(full, RuntimeFrame.Zero));
            Assert.AreEqual(GameplayTriggerRuntimeEventDropReason.SourceCapacityExceeded,
                full.LastDropReason);
            Assert.AreEqual(1L, full.NextSequence);
        }

        [Test]
        public void Source_SortsByFrameThenSequenceAndAcceptsLongMaxBeforeTerminalState()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(new RecordingSink());
            var source = new GameplayTriggerRuntimeEventSource(2);
            _ = new GameplayTriggerRuntime(catalog, Options(), source);

            Assert.IsTrue(PublishResult(source, new RuntimeFrame(5)));
            Assert.IsTrue(PublishResult(source, new RuntimeFrame(3), activationId: 2L));
            Assert.AreEqual(new RuntimeFrame(3), source.GetPending(0).Frame);
            Assert.AreEqual(2L, source.GetPending(0).SourceSequence);
            Assert.AreEqual(new RuntimeFrame(5), source.GetPending(1).Frame);
            Assert.AreEqual(1L, source.GetPending(1).SourceSequence);
            Assert.IsFalse(PublishResult(source, new RuntimeFrame(6), activationId: 3L));
            Assert.AreEqual(3L, source.NextSequence);

            var terminal = new GameplayTriggerRuntimeEventSource(
                capacity: 2,
                nextSequence: long.MaxValue,
                sequenceExhausted: false,
                droppedCount: 0L);
            _ = new GameplayTriggerRuntime(catalog, Options(), terminal);
            Assert.IsTrue(PublishResult(terminal, RuntimeFrame.Zero));
            Assert.AreEqual(long.MaxValue, terminal.GetPending(0).SourceSequence);
            Assert.IsTrue(terminal.IsSequenceExhausted);
            Assert.AreEqual(long.MaxValue, terminal.NextSequence);
            Assert.IsFalse(PublishResult(terminal, new RuntimeFrame(1), activationId: 2L));
            Assert.AreEqual(GameplayTriggerRuntimeEventDropReason.SourceSequenceExhausted,
                terminal.LastDropReason);
        }

        [Test]
        public void Router_FansOutDuePrefixAndRetainsLateSameFrameProducerUntilNextTick()
        {
            GameplayTriggerEventRouterSystem router = CreateRouter(
                out GameplayTriggerRuntimeEventSource source,
                out GameplayTriggerRuntimeEventObserverBuffer first,
                out GameplayTriggerRuntimeEventObserverBuffer second);
            RuntimeFrame frame = new RuntimeFrame(5);

            router.Tick(CreateContext(frame));
            Assert.IsTrue(PublishResult(source, frame));
            Assert.AreEqual(1, source.PendingCount);
            Assert.AreEqual(0, first.Count);

            router.Tick(CreateContext(new RuntimeFrame(6)));
            Assert.AreEqual(0, source.PendingCount);
            Assert.IsTrue(first.TryDequeue(out GameplayTriggerRuntimeEvent firstEvent));
            Assert.IsTrue(second.TryDequeue(out GameplayTriggerRuntimeEvent secondEvent));
            Assert.AreEqual(frame, firstEvent.Frame);
            Assert.AreEqual(firstEvent.SourceSequence, secondEvent.SourceSequence);

            Assert.IsTrue(PublishResult(source, new RuntimeFrame(9), activationId: 2L));
            router.Tick(CreateContext(new RuntimeFrame(8)));
            Assert.AreEqual(1, source.PendingCount);
            Assert.AreEqual(0, first.Count);
            router.Tick(CreateContext(new RuntimeFrame(9)));
            Assert.AreEqual(0, source.PendingCount);
            Assert.AreEqual(1, first.Count);
        }

        [Test]
        public void Router_DrainsOverdueAndCurrentTogetherInFrameThenSequenceOrder()
        {
            GameplayTriggerEventRouterSystem router = CreateRouter(
                out GameplayTriggerRuntimeEventSource source,
                out GameplayTriggerRuntimeEventObserverBuffer first,
                out _);
            Assert.IsTrue(PublishResult(
                source,
                new RuntimeFrame(8),
                activationId: 3L));
            Assert.IsTrue(PublishResult(
                source,
                new RuntimeFrame(6),
                activationId: 1L));
            Assert.IsTrue(PublishResult(
                source,
                new RuntimeFrame(8),
                activationId: 2L));

            router.Tick(CreateContext(new RuntimeFrame(8)));

            Assert.AreEqual(0, source.PendingCount);
            Assert.IsTrue(first.TryDequeue(out GameplayTriggerRuntimeEvent overdue));
            Assert.IsTrue(first.TryDequeue(out GameplayTriggerRuntimeEvent currentFirst));
            Assert.IsTrue(first.TryDequeue(out GameplayTriggerRuntimeEvent currentSecond));
            Assert.AreEqual(new RuntimeFrame(6), overdue.Frame);
            Assert.AreEqual(2L, overdue.SourceSequence);
            Assert.AreEqual(new RuntimeFrame(8), currentFirst.Frame);
            Assert.AreEqual(1L, currentFirst.SourceSequence);
            Assert.AreEqual(new RuntimeFrame(8), currentSecond.Frame);
            Assert.AreEqual(3L, currentSecond.SourceSequence);
        }

        [Test]
        public void DisabledRouterAndObserver_PreservePendingWithoutReplayOrAuthorityMutation()
        {
            GameplayTriggerEventRouterSystem router = CreateRouter(
                out GameplayTriggerRuntimeEventSource source,
                out GameplayTriggerRuntimeEventObserverBuffer first,
                out GameplayTriggerRuntimeEventObserverBuffer second);
            router.SetEnabled(false);
            second.SetEnabled(false);
            Assert.IsTrue(PublishResult(source, RuntimeFrame.Zero));

            router.Tick(CreateContext(RuntimeFrame.Zero));
            Assert.AreEqual(1, source.PendingCount);
            Assert.AreEqual(0L, first.DroppedCount);
            Assert.AreEqual(0L, second.DroppedCount);

            router.SetEnabled(true);
            router.Tick(CreateContext(RuntimeFrame.Zero));
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(0, second.Count);
            Assert.AreEqual(1L, second.DroppedCount);
            Assert.AreEqual(GameplayTriggerRuntimeEventDropReason.ObserverDisabled,
                second.LastDropReason);
            second.SetEnabled(true);
            Assert.IsFalse(second.TryDequeue(out _), "Disabled delivery must not replay.");
        }

        [Test]
        public void ObserverCapacityClearAndPresentationReset_HaveBoundedExactSemantics()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new RecordingSink(),
                out GameplayTriggerRuntimeEventSource source);
            var observer = new GameplayTriggerRuntimeEventObserverBuffer(1);
            var zero = new GameplayTriggerRuntimeEventObserverBuffer(0);
            var router = new GameplayTriggerEventRouterSystem(
                source,
                new[]
                {
                    new GameplayTriggerRuntimeEventObserverRegistration("a", observer),
                    new GameplayTriggerRuntimeEventObserverRegistration("z", zero)
                });

            Assert.IsTrue(PublishResult(source, RuntimeFrame.Zero));
            Assert.IsTrue(PublishResult(source, RuntimeFrame.Zero, activationId: 2L));
            router.Tick(CreateContext(RuntimeFrame.Zero));
            Assert.AreEqual(1, observer.Count);
            Assert.AreEqual(1L, observer.DroppedCount);
            Assert.AreEqual(2L, zero.DroppedCount);
            Assert.AreEqual(1, observer.HighWaterMark);
            observer.Clear();
            Assert.AreEqual(0, observer.Count);
            Assert.AreEqual(1L, observer.DroppedCount);

            long nextActivationId = runtime.NextActivationId;
            observer.SetEnabled(false);
            router.SetEnabled(false);
            router.ResetPresentationState();
            Assert.AreEqual(0L, source.DroppedCount);
            Assert.AreEqual(1L, source.NextSequence);
            Assert.AreEqual(0, source.HighWaterMark);
            Assert.AreEqual(0L, observer.DroppedCount);
            Assert.AreEqual(0, zero.HighWaterMark);
            Assert.IsFalse(observer.IsEnabled);
            Assert.IsFalse(router.IsEnabled);
            Assert.AreEqual(nextActivationId, runtime.NextActivationId);
            Assert.IsTrue(source.IsProducerOwnerBound);
            Assert.IsTrue(source.IsDrainOwnerBound);
        }

        [Test]
        public void Evaluation_EmitsCanonicalOccurrenceActivationAndDispatchPayloads()
        {
            var sink = new RecordingSink();
            GameplayTriggerRuntime runtime = CreateRuntime(
                sink,
                out GameplayTriggerRuntimeEventSource source);
            RuntimeFrame frame = new RuntimeFrame(4);
            var signal = new GameplayTriggerSignal(
                31,
                Subject,
                Instigator,
                payload0: 101,
                payload1: 102,
                payload2: 103,
                sequence: 9);

            Assert.IsTrue(runtime.TrySubmitSignals(
                frame,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[] { signal },
                out _));
            Assert.IsTrue(runtime.Evaluate(frame).Success);
            Assert.AreEqual(3, source.PendingCount);
            Assert.AreEqual(1, sink.CommitCount);

            GameplayTriggerRuntimeEvent occurrence = source.GetPending(0);
            GameplayTriggerRuntimeEvent activation = source.GetPending(1);
            GameplayTriggerRuntimeEvent action = source.GetPending(2);
            Assert.AreEqual(GameplayTriggerRuntimeEventKind.Occurrence, occurrence.Kind);
            Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Observed, occurrence.Status);
            Assert.AreEqual(-1, occurrence.ActionIndex);
            Assert.AreEqual(default(GameplayTriggerRouteId), occurrence.RouteId);
            AssertOrigin(occurrence, runtime.GetInstanceId(0));
            AssertOccurrence(occurrence, frame);
            Assert.AreEqual(1L, occurrence.SourceSequence);

            Assert.AreEqual(GameplayTriggerRuntimeEventKind.ActivationAccepted, activation.Kind);
            Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Accepted, activation.Status);
            Assert.AreEqual(1L, activation.ActivationId);
            Assert.AreEqual(-1, activation.ActionIndex);
            Assert.AreEqual(new GameplayTriggerRouteId(1), activation.RouteId);
            AssertOrigin(activation, runtime.GetInstanceId(0));
            AssertOccurrence(activation, frame);
            Assert.AreEqual(2L, activation.SourceSequence);

            Assert.AreEqual(GameplayTriggerRuntimeEventKind.ActionDispatched, action.Kind);
            Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Dispatched, action.Status);
            Assert.AreEqual(1L, action.ActivationId);
            Assert.AreEqual(0, action.ActionIndex);
            Assert.AreEqual(ActionKindId, action.ActionKindId);
            Assert.AreEqual(GameplayTriggerTargetRef.FromGameplayEntity(Owner), action.Target);
            Assert.AreEqual(201L, action.ActionPayload0);
            Assert.AreEqual(202L, action.ActionPayload1);
            Assert.AreEqual(203L, action.ActionPayload2);
            AssertOrigin(action, runtime.GetInstanceId(0));
            AssertOccurrence(action, frame);
            Assert.AreEqual(3L, action.SourceSequence);
        }

        [Test]
        public void Evaluation_EmitsEverySameFrameOccurrenceActivationAndActionExactlyOnce()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new RecordingSink(),
                out GameplayTriggerRuntimeEventSource source);
            RuntimeFrame frame = new RuntimeFrame(5);
            Assert.IsTrue(runtime.TrySubmitSignals(
                frame,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[]
                {
                    new GameplayTriggerSignal(33, payload0: 3L),
                    new GameplayTriggerSignal(31, payload0: 1L),
                    new GameplayTriggerSignal(32, payload0: 2L)
                },
                out _));

            Assert.IsTrue(runtime.Evaluate(frame).Success);

            Assert.AreEqual(3, runtime.OccurrenceCount);
            Assert.AreEqual(3, runtime.ActivationCount);
            Assert.AreEqual(3, runtime.IntentCount);
            Assert.AreEqual(9, source.PendingCount);
            int occurrenceCount = 0;
            int activationCount = 0;
            int actionCount = 0;
            for (int i = 0; i < source.PendingCount; i++)
            {
                GameplayTriggerRuntimeEvent evt = source.GetPending(i);
                Assert.AreEqual(i + 1L, evt.SourceSequence);
                if (evt.Kind == GameplayTriggerRuntimeEventKind.Occurrence)
                    occurrenceCount++;
                else if (evt.Kind == GameplayTriggerRuntimeEventKind.ActivationAccepted)
                    activationCount++;
                else if (evt.Kind == GameplayTriggerRuntimeEventKind.ActionDispatched)
                    actionCount++;
            }
            Assert.AreEqual(3, occurrenceCount);
            Assert.AreEqual(3, activationCount);
            Assert.AreEqual(3, actionCount);
        }

        [Test]
        public void CommitThrow_DoesNotFabricateDispatchAndSourceDropDoesNotChangeAuthority()
        {
            var throwing = new RecordingSink { ThrowOnCommit = true };
            GameplayTriggerRuntime runtime = CreateRuntime(
                throwing,
                out GameplayTriggerRuntimeEventSource source);
            Assert.IsTrue(SubmitOneSignal(runtime, RuntimeFrame.Zero));

            Assert.Throws<InvalidOperationException>(() => runtime.Evaluate(RuntimeFrame.Zero));
            Assert.AreEqual(2, source.PendingCount);
            Assert.AreEqual(GameplayTriggerRuntimeEventKind.Occurrence, source.GetPending(0).Kind);
            Assert.AreEqual(GameplayTriggerRuntimeEventKind.ActivationAccepted, source.GetPending(1).Kind);
            Assert.AreEqual(1, runtime.ActivationCount);
            Assert.AreEqual(1, runtime.IntentCount);

            GameplayTriggerCatalog catalog = CreateCatalog(new RecordingSink());
            var zeroSource = new GameplayTriggerRuntimeEventSource(0);
            var zeroRuntime = new GameplayTriggerRuntime(catalog, Options(), zeroSource);
            Assert.IsTrue(SubmitOneSignal(zeroRuntime, RuntimeFrame.Zero));
            Assert.IsTrue(zeroRuntime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(1, zeroRuntime.ActivationCount);
            Assert.AreEqual(1, zeroRuntime.IntentCount);
            Assert.AreEqual(3L, zeroSource.DroppedCount);
            Assert.AreEqual(1L, zeroSource.NextSequence);
        }

        [Test]
        public void CommandSystem_EmitsOneResultPerLifecycleCommandWithRawHeader()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new RecordingSink(),
                out GameplayTriggerRuntimeEventSource source);
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            RuntimeCommand command = GameplayTriggerRuntimeCommandFactory.SetEnabled(
                new RuntimeFrame(7),
                instanceId,
                false,
                sourceId: 44,
                traceId: CharacterTestRuntimeTraceIds.MustNotEnterEvent).WithSequence(19L);
            var system = new GameplayTriggerCommandSystem(runtime);

            system.Tick(CreateContext(new RuntimeFrame(7), new[] { command }));

            Assert.AreEqual(1, source.PendingCount);
            GameplayTriggerRuntimeEvent evt = source.GetPending(0);
            Assert.AreEqual(GameplayTriggerRuntimeEventKind.CommandResult, evt.Kind);
            Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Succeeded, evt.Status);
            Assert.AreEqual(-1, evt.ActionIndex);
            Assert.AreEqual(new RuntimeFrame(7), evt.OriginFrame);
            Assert.AreEqual(default(RuntimeFrame), evt.DueFrame);
            Assert.AreEqual(19L, evt.ProducerSequence);
            Assert.AreEqual(GameplayTriggerRuntimeCommandKind.SetEnabled, evt.RuntimeCommandKind);
            Assert.AreEqual(44, evt.CommandSourceId);
            Assert.AreEqual(GameplayTriggerRuntimeCommandIds.SetEnabled, evt.CommandId);
            Assert.AreEqual(instanceId.Index, evt.CommandTargetId);
            Assert.AreEqual(instanceId.Generation, evt.CommandPayload0);
            Assert.AreEqual(0, evt.CommandPayload1);
            Assert.AreEqual(0, evt.CommandPayload2);
            Assert.AreEqual(instanceId, evt.InstanceId);
            AssertOrigin(evt, instanceId);
        }

        [Test]
        public void CommandSystem_EmitsEverySameFrameLifecycleResultWithoutPollingLoss()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new RecordingSink(),
                out GameplayTriggerRuntimeEventSource source);
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            RuntimeFrame frame = new RuntimeFrame(7);
            RuntimeCommand disable = GameplayTriggerRuntimeCommandFactory.SetEnabled(
                frame,
                instanceId,
                false).WithSequence(1L);
            RuntimeCommand reset = GameplayTriggerRuntimeCommandFactory.Reset(
                frame,
                instanceId).WithSequence(2L);
            var system = new GameplayTriggerCommandSystem(runtime);

            system.Tick(CreateContext(frame, new[] { disable, reset }));

            Assert.AreEqual(2, source.PendingCount);
            Assert.AreEqual(GameplayTriggerRuntimeEventKind.CommandResult,
                source.GetPending(0).Kind);
            Assert.AreEqual(GameplayTriggerRuntimeCommandKind.SetEnabled,
                source.GetPending(0).RuntimeCommandKind);
            Assert.AreEqual(1L, source.GetPending(0).ProducerSequence);
            Assert.AreEqual(GameplayTriggerRuntimeCommandKind.Reset,
                source.GetPending(1).RuntimeCommandKind);
            Assert.AreEqual(2L, source.GetPending(1).ProducerSequence);
        }

        [Test]
        public void RawSignalBatch_EmitsExactlyOneResolvedCommandResult()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new RecordingSink(),
                out GameplayTriggerRuntimeEventSource source);
            var commands = new RuntimeCommand[2];
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                new RuntimeFrame(8),
                sourceId: 55,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                batchId: 444L,
                new[]
                {
                    new GameplayTriggerSignal(31, Subject, Instigator, sequence: 1L),
                    new GameplayTriggerSignal(32, Subject, Instigator, sequence: 2L)
                },
                commands,
                out int written,
                out _));
            commands[0] = commands[0].WithSequence(21L);
            commands[1] = commands[1].WithSequence(22L);
            var system = new GameplayTriggerCommandSystem(runtime, maxSignalCommandItems: 2);

            system.Tick(CreateContext(new RuntimeFrame(8), commands));

            Assert.AreEqual(2, written);
            Assert.AreEqual(2, system.HandledCommandCount);
            Assert.AreEqual(1, source.PendingCount);
            GameplayTriggerRuntimeEvent evt = source.GetPending(0);
            Assert.AreEqual(GameplayTriggerRuntimeEventKind.CommandResult, evt.Kind);
            Assert.AreEqual(GameplayTriggerRuntimeCommandKind.SubmitSignalBatch,
                evt.RuntimeCommandKind);
            Assert.AreEqual(21L, evt.ProducerSequence);
            Assert.AreEqual(444L, evt.BatchId);
            Assert.AreEqual(RuntimeCommandProvenanceKind.Exogenous, evt.Provenance);
            Assert.AreEqual(SourceBindingId, evt.SourceBindingId);
            AssertOrigin(evt, runtime.GetInstanceId(0));
        }

        [Test]
        public void Diagnostics_PublishEverySameFrameResultAfterLegacySnapshotIsFull()
        {
            GameplayTriggerCatalog catalog = CreateThreeBindingCatalog(new RecordingSink());
            var source = new GameplayTriggerRuntimeEventSource(32);
            var runtime = new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(
                    8, 8, 8, 8, 1, 8, 8, 8, 4, 8),
                source);
            RuntimeFrame frame = RuntimeFrame.Zero;
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(runtime.TrySubmitSignals(
                    frame,
                    new GameplayTriggerSourceBindingId(101 + i),
                    GameplayTriggerSignalOrdering.UnorderedMultiset,
                    new[] { new GameplayTriggerSignal(31 + i) },
                    out _));
            }

            GameplayTriggerResult result = runtime.Evaluate(frame);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(GameplayTriggerErrorCode.CandidateCapacityExceeded,
                result.ErrorCode);
            Assert.AreEqual(1, runtime.DiagnosticCount,
                "Legacy diagnostic snapshot must remain bounded by MaxCandidates.");
            int eventDiagnostics = 0;
            for (int i = 0; i < source.PendingCount; i++)
            {
                GameplayTriggerRuntimeEvent evt = source.GetPending(i);
                if (evt.Kind != GameplayTriggerRuntimeEventKind.RuntimeDiagnostic) continue;
                eventDiagnostics++;
                Assert.AreEqual(-1, evt.ActionIndex);
                Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Rejected, evt.Status);
                Assert.AreEqual((int)GameplayTriggerErrorCode.CandidateCapacityExceeded,
                    evt.ReasonCode);
                Assert.IsTrue(evt.InstanceId.IsValid);
                Assert.IsTrue(evt.DefinitionId.IsValid);
                Assert.IsTrue(evt.BindingId.IsValid);
                Assert.IsTrue(evt.SourceBindingId.IsValid);
                Assert.AreEqual(Owner, evt.Owner);
            }
            Assert.AreEqual(2, eventDiagnostics,
                "Every actual diagnostic must try the event source even after the legacy snapshot is full.");
        }

        [Test]
        public void ObserverRegistryPermutation_LeavesNormalSequenceIdenticalAndDropsIndependent()
        {
            RunFanoutPermutation(
                reverse: false,
                out GameplayTriggerRuntimeEvent[] first,
                out long firstZeroDrops,
                out long firstFullDrops);
            RunFanoutPermutation(
                reverse: true,
                out GameplayTriggerRuntimeEvent[] second,
                out long secondZeroDrops,
                out long secondFullDrops);

            Assert.AreEqual(3, first.Length);
            Assert.AreEqual(first.Length, second.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i].Frame, second[i].Frame);
                Assert.AreEqual(first[i].SourceSequence, second[i].SourceSequence);
                Assert.AreEqual(first[i].ActivationId, second[i].ActivationId);
                Assert.AreEqual(first[i].ResultPayload0, second[i].ResultPayload0);
            }
            Assert.AreEqual(3L, firstZeroDrops);
            Assert.AreEqual(firstZeroDrops, secondZeroDrops);
            Assert.AreEqual(1L, firstFullDrops);
            Assert.AreEqual(firstFullDrops, secondFullDrops);
        }

        [Test]
        public void PresentationState_IsExcludedFromHashSaveCheckpointAndAuthorityRestore()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new RecordingSink(),
                out GameplayTriggerRuntimeEventSource source);
            var observer = new GameplayTriggerRuntimeEventObserverBuffer(2);
            var router = new GameplayTriggerEventRouterSystem(
                source,
                new[] { new GameplayTriggerRuntimeEventObserverRegistration("observer", observer) });
            Assert.IsTrue(SubmitOneSignal(runtime, RuntimeFrame.Zero));
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(3, source.PendingCount);

            var participant = new GameplayTriggerRuntimeSaveStateParticipant(
                runtime,
                new AlwaysValidRestoreEntityValidator());
            RuntimeCheckpointQuiescenceResult quiescence = participant.CheckQuiescence(
                new RuntimeCheckpointContext(0L));
            Assert.IsTrue(quiescence.IsReady);
            RuntimeSaveStateResult<RuntimeSaveState> firstCapture = participant.CaptureSaveState();
            Assert.IsTrue(firstCapture.Success, firstCapture.Error.ToString());
            long firstHash = Hash(runtime);
            string firstPayload = firstCapture.Value.ModuleStates[0].CustomState.PayloadJson;

            router.Tick(CreateContext(RuntimeFrame.Zero));
            Assert.AreEqual(2, observer.Count);
            Assert.AreEqual(2, observer.HighWaterMark);
            Assert.AreEqual(1L, observer.DroppedCount);
            Assert.IsTrue(PublishResult(source, RuntimeFrame.Zero, activationId: 90L));
            router.SetEnabled(false);
            observer.SetEnabled(false);
            RuntimeSaveStateResult<RuntimeSaveState> secondCapture = participant.CaptureSaveState();
            Assert.IsTrue(secondCapture.Success, secondCapture.Error.ToString());
            Assert.AreEqual(firstHash, Hash(runtime));
            Assert.AreEqual(firstPayload,
                secondCapture.Value.ModuleStates[0].CustomState.PayloadJson);

            int pendingBeforeRestore = source.PendingCount;
            long nextSequenceBeforeRestore = source.NextSequence;
            Assert.IsTrue(participant.RestoreSaveState(firstCapture.Value).Success);
            Assert.AreEqual(pendingBeforeRestore, source.PendingCount);
            Assert.AreEqual(nextSequenceBeforeRestore, source.NextSequence);
            Assert.AreEqual(2, observer.Count);
            Assert.AreEqual(2, observer.HighWaterMark);
            Assert.AreEqual(1L, observer.DroppedCount);
            Assert.IsFalse(router.IsEnabled);
            Assert.IsFalse(observer.IsEnabled);

            router.ResetPresentationState();
            Assert.AreEqual(0, source.PendingCount);
            Assert.AreEqual(1L, source.NextSequence);
            Assert.AreEqual(firstHash, Hash(runtime));
            Assert.AreEqual(2, GameplayTriggerCatalog.CurrentCatalogHashSchemaVersion);
            Assert.AreEqual(3, GameplayTriggerRuntimeHashContributor.CurrentStateSchemaVersion);
            Assert.AreEqual(3,
                GameplayTriggerRuntimeSaveStateParticipant.CurrentPayloadSchemaVersion);
        }

        [Test]
        public void InternalDeferredResult_UsesFrozenTargetAndDefaultOriginFrame()
        {
            GameplayTriggerRuntime runtime = CreateInternalDeferredRuntime(
                out GameplayTriggerRuntimeEventSource source);
            Assert.IsTrue(runtime.TrySubmitSignals(
                RuntimeFrame.Zero,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[]
                {
                    new GameplayTriggerSignal(31, Subject, Instigator),
                    new GameplayTriggerSignal(32, Subject, Instigator)
                },
                out _));
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(2, runtime.DeferredRecordCount);
            GameplayTriggerDeferredRecord record = runtime.GetDeferredRecord(1);

            Assert.IsTrue(runtime.StageDueDeferred(new RuntimeFrame(1)).Success);
            int resultCount = CountKind(
                source,
                GameplayTriggerRuntimeEventKind.InternalDeferredResult);
            Assert.AreEqual(2, resultCount);
            GameplayTriggerRuntimeEvent evt = FindLast(
                source,
                GameplayTriggerRuntimeEventKind.InternalDeferredResult);
            Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Succeeded, evt.Status);
            Assert.AreEqual(runtime.GetInstanceId(0), evt.InstanceId);
            Assert.AreEqual(Owner, evt.Owner);
            Assert.AreEqual(record.TargetSourceBindingId, evt.TargetSourceBindingId);
            Assert.AreEqual(
                GameplayTriggerTargetRef.FromTriggerSourceBinding(record.TargetSourceBindingId),
                evt.Target);
            Assert.AreEqual(default(RuntimeFrame), evt.OriginFrame);
            Assert.AreEqual(record.OriginalDueFrame, evt.DueFrame);
            Assert.AreEqual(record.Sequence, evt.ProducerSequence);
        }

        [Test]
        public void ReliableResult_MapsSucceededAndRetainedWithoutMutatingFrozenRecord()
        {
            GameplayTriggerRuntime runtime = CreateReliableRuntime(
                out GameplayTriggerRuntimeEventSource source);
            Assert.IsTrue(runtime.TrySubmitSignals(
                new RuntimeFrame(4),
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[]
                {
                    new GameplayTriggerSignal(31, Subject, Instigator),
                    new GameplayTriggerSignal(32, Subject, Instigator)
                },
                out _));
            Assert.IsTrue(runtime.Evaluate(new RuntimeFrame(4)).Success);
            GameplayTriggerReliableOutboxRecord record = runtime.GetReliableOutboxRecord(0);
            GameplayTriggerReliableOutboxRecord secondRecord = runtime.GetReliableOutboxRecord(1);

            runtime.RecordReliableExecution(
                new RuntimeFrame(5),
                in record,
                new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.TransientRejected,
                    77));
            runtime.RecordReliableExecution(
                new RuntimeFrame(5),
                in secondRecord,
                new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.TargetCapacityExceeded,
                    78));
            Assert.AreEqual(2, CountKind(
                source,
                GameplayTriggerRuntimeEventKind.ReliableTransferResult));
            GameplayTriggerRuntimeEvent retained = FindLast(
                source,
                GameplayTriggerRuntimeEventKind.ReliableTransferResult);
            Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Retained, retained.Status);
            Assert.AreEqual(GameplayTriggerReliableTransferStatus.TargetCapacityExceeded,
                retained.ReliableTransferStatus);
            Assert.AreEqual(78, retained.Detail0);
            Assert.AreEqual(secondRecord.OriginalActivationFrame, retained.OriginFrame);
            Assert.AreEqual(secondRecord.OriginalDueFrame, retained.DueFrame);
            Assert.AreEqual(secondRecord.Sequence, retained.ProducerSequence);
            Assert.AreEqual(secondRecord.ResolvedTarget, retained.Target);

            runtime.RecordReliableExecution(
                new RuntimeFrame(6),
                in record,
                new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.Accepted));
            GameplayTriggerRuntimeEvent accepted = FindLast(
                source,
                GameplayTriggerRuntimeEventKind.ReliableTransferResult);
            Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Succeeded, accepted.Status);
            Assert.AreEqual(GameplayTriggerReliableTransferStatus.Accepted,
                accepted.ReliableTransferStatus);
        }

        [Test]
        public void ReliableOutboxTransferThrow_DoesNotFabricateReliableResult()
        {
            GameplayTriggerRuntime runtime = CreateReliableRuntime(
                out GameplayTriggerRuntimeEventSource source);
            Assert.IsTrue(SubmitOneSignal(runtime, new RuntimeFrame(4)));
            Assert.IsTrue(runtime.Evaluate(new RuntimeFrame(4)).Success);
            Assert.AreEqual(0, CountKind(
                source,
                GameplayTriggerRuntimeEventKind.ReliableTransferResult));
            var transfer = new ThrowingTransfer(ActionKindId, "throw.target");
            var outbox = new GameplayTriggerReliableOutboxModule(
                runtime,
                new IGameplayTriggerReliableTargetTransfer[] { transfer });
            var target = new PassiveRuntimeModule("throw.target");
            var host = new RuntimeHost();
            host.RegisterModule(outbox);
            host.RegisterModule(target);
            host.Initialize();
            host.Start();

            Assert.Throws<RuntimeHostException>(() => host.Tick(5L, 0d));

            Assert.AreEqual(1, transfer.CallCount);
            Assert.AreEqual(0, CountKind(
                source,
                GameplayTriggerRuntimeEventKind.ReliableTransferResult));
            host.Dispose();
        }

        [Test]
        public void SourceRouterAndObserver_DoNotAllocateAfterWarmup()
        {
            GameplayTriggerEventRouterSystem router = CreateRouter(
                out GameplayTriggerRuntimeEventSource source,
                out GameplayTriggerRuntimeEventObserverBuffer first,
                out GameplayTriggerRuntimeEventObserverBuffer second);
            Assert.IsTrue(PublishResult(source, RuntimeFrame.Zero));
            router.Tick(CreateContext(RuntimeFrame.Zero));
            Assert.IsTrue(first.TryDequeue(out _));
            Assert.IsTrue(second.TryDequeue(out _));
            _ = GC.GetAllocatedBytesForCurrentThread();

            var world = new GameplayWorld();
            var events = new RuntimeEventQueue<GameplayRuntimeEvent>();
            var commandState = new GameplayCommandExecutionState();
            IReadOnlyList<RuntimeCommand> commands = Array.Empty<RuntimeCommand>();
            bool succeeded = true;

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 1; i <= 64; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                succeeded &= PublishResult(source, frame, activationId: i + 1L);
                router.Tick(new GameplaySystemContext(
                    frame,
                    0d,
                    0d,
                    world,
                    commands,
                    events,
                    commandState));
                succeeded &= first.TryDequeue(out _);
                succeeded &= second.TryDequeue(out _);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(succeeded);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void RuntimeProducerEmissionAndRouterFanout_DoNotAllocateAfterWarmup()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new RecordingSink(),
                out GameplayTriggerRuntimeEventSource source);
            var observer = new GameplayTriggerRuntimeEventObserverBuffer(8);
            var router = new GameplayTriggerEventRouterSystem(
                source,
                new[] { new GameplayTriggerRuntimeEventObserverRegistration("observer", observer) });
            var signals = new[] { new GameplayTriggerSignal(31, Subject, Instigator) };
            Assert.IsTrue(runtime.TrySubmitSignals(
                RuntimeFrame.Zero,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                signals,
                out _));
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            router.Tick(CreateContext(RuntimeFrame.Zero));
            Assert.IsTrue(observer.TryDequeue(out _));
            Assert.IsTrue(observer.TryDequeue(out _));
            Assert.IsTrue(observer.TryDequeue(out _));

            var world = new GameplayWorld();
            var events = new RuntimeEventQueue<GameplayRuntimeEvent>();
            var commandState = new GameplayCommandExecutionState();
            IReadOnlyList<RuntimeCommand> commands = Array.Empty<RuntimeCommand>();
            bool succeeded = true;
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 1; i <= 64; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                succeeded &= runtime.TrySubmitSignals(
                    frame,
                    SourceBindingId,
                    GameplayTriggerSignalOrdering.UnorderedMultiset,
                    signals,
                    out _);
                succeeded &= runtime.Evaluate(frame).Success;
                router.Tick(new GameplaySystemContext(
                    frame,
                    0d,
                    0d,
                    world,
                    commands,
                    events,
                    commandState));
                succeeded &= observer.TryDequeue(out _);
                succeeded &= observer.TryDequeue(out _);
                succeeded &= observer.TryDequeue(out _);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(succeeded);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void RepeatedSourceAndObserverOverflow_AreNoAllocAndSaturating()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(new RecordingSink());
            var source = new GameplayTriggerRuntimeEventSource(1);
            _ = new GameplayTriggerRuntime(catalog, Options(), source);
            Assert.IsTrue(PublishResult(source, RuntimeFrame.Zero));
            Assert.IsFalse(PublishResult(source, RuntimeFrame.Zero, activationId: 2L));
            _ = GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            bool allRejected = true;
            for (int i = 0; i < 64; i++)
                allRejected &= !PublishResult(source, RuntimeFrame.Zero, activationId: i + 3L);
            long sourceAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(allRejected);
            Assert.AreEqual(0L, sourceAllocated);
            Assert.AreEqual(65L, source.DroppedCount);

            var zero = new GameplayTriggerRuntimeEventObserverBuffer(0);
            var router = new GameplayTriggerEventRouterSystem(
                source,
                new[] { new GameplayTriggerRuntimeEventObserverRegistration("zero", zero) });
            var world = new GameplayWorld();
            var events = new RuntimeEventQueue<GameplayRuntimeEvent>();
            var commandState = new GameplayCommandExecutionState();
            IReadOnlyList<RuntimeCommand> commands = Array.Empty<RuntimeCommand>();
            GameplaySystemContext context = new GameplaySystemContext(
                RuntimeFrame.Zero,
                0d,
                0d,
                world,
                commands,
                events,
                commandState);
            router.Tick(context);
            Assert.AreEqual(1L, zero.DroppedCount);

            before = GC.GetAllocatedBytesForCurrentThread();
            bool allAccepted = true;
            for (int i = 0; i < 64; i++)
            {
                allAccepted &= PublishResult(
                    source,
                    RuntimeFrame.Zero,
                    activationId: i + 100L);
                router.Tick(context);
            }
            long observerAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(allAccepted);
            Assert.AreEqual(0L, observerAllocated);
            Assert.AreEqual(65L, zero.DroppedCount);

            var saturated = new GameplayTriggerRuntimeEventObserverBuffer(
                capacity: 0,
                droppedCount: long.MaxValue);
            var saturatedSource = new GameplayTriggerRuntimeEventSource(1);
            _ = new GameplayTriggerRuntime(catalog, Options(), saturatedSource);
            var saturatedRouter = new GameplayTriggerEventRouterSystem(
                saturatedSource,
                new[]
                {
                    new GameplayTriggerRuntimeEventObserverRegistration(
                        "saturated",
                        saturated)
                });
            Assert.IsTrue(PublishResult(saturatedSource, RuntimeFrame.Zero));
            saturatedRouter.Tick(context);
            Assert.AreEqual(long.MaxValue, saturated.DroppedCount);
        }

        private static void RunFanoutPermutation(
            bool reverse,
            out GameplayTriggerRuntimeEvent[] normalEvents,
            out long zeroDrops,
            out long fullDrops)
        {
            _ = CreateRuntime(
                new RecordingSink(),
                out GameplayTriggerRuntimeEventSource source);
            var zero = new GameplayTriggerRuntimeEventObserverBuffer(0);
            var full = new GameplayTriggerRuntimeEventObserverBuffer(2);
            var normal = new GameplayTriggerRuntimeEventObserverBuffer(4);
            var registrations = reverse
                ? new[]
                {
                    new GameplayTriggerRuntimeEventObserverRegistration("normal", normal),
                    new GameplayTriggerRuntimeEventObserverRegistration("full", full),
                    new GameplayTriggerRuntimeEventObserverRegistration("zero", zero)
                }
                : new[]
                {
                    new GameplayTriggerRuntimeEventObserverRegistration("zero", zero),
                    new GameplayTriggerRuntimeEventObserverRegistration("full", full),
                    new GameplayTriggerRuntimeEventObserverRegistration("normal", normal)
                };
            var router = new GameplayTriggerEventRouterSystem(source, registrations);
            Assert.IsTrue(PublishResult(source, new RuntimeFrame(2), activationId: 3L));
            Assert.IsTrue(PublishResult(source, RuntimeFrame.Zero, activationId: 1L));
            Assert.IsTrue(PublishResult(source, new RuntimeFrame(1), activationId: 2L));

            router.Tick(CreateContext(new RuntimeFrame(2)));
            normalEvents = new GameplayTriggerRuntimeEvent[normal.Count];
            for (int i = 0; i < normalEvents.Length; i++)
                Assert.IsTrue(normal.TryDequeue(out normalEvents[i]));
            int fullCount = full.Count;
            zeroDrops = zero.DroppedCount;
            fullDrops = full.DroppedCount;
            router.Tick(CreateContext(new RuntimeFrame(2)));
            Assert.AreEqual(0, normal.Count, "A drained source must not replay.");
            Assert.AreEqual(fullCount, full.Count);
            Assert.AreEqual(zeroDrops, zero.DroppedCount);
            Assert.AreEqual(fullDrops, full.DroppedCount);
        }

        private static long Hash(GameplayTriggerRuntime runtime)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.GameplayTriggers, RuntimeHashSchemaIds.Version1);
            new GameplayTriggerRuntimeHashContributor(runtime).Contribute(
                new RuntimeHashContext(RuntimeFrame.Zero),
                accumulator);
            return accumulator.ToHash();
        }

        private static GameplayTriggerEventRouterSystem CreateRouter(
            out GameplayTriggerRuntimeEventSource source,
            out GameplayTriggerRuntimeEventObserverBuffer first,
            out GameplayTriggerRuntimeEventObserverBuffer second)
        {
            _ = CreateRuntime(new RecordingSink(), out source);
            first = new GameplayTriggerRuntimeEventObserverBuffer(8);
            second = new GameplayTriggerRuntimeEventObserverBuffer(8);
            return new GameplayTriggerEventRouterSystem(
                source,
                new[]
                {
                    new GameplayTriggerRuntimeEventObserverRegistration("z-observer", second),
                    new GameplayTriggerRuntimeEventObserverRegistration("a-observer", first)
                });
        }

        private static GameplayTriggerRuntimeEventSource CreateBoundSource()
        {
            _ = CreateRuntime(new RecordingSink(), out GameplayTriggerRuntimeEventSource source);
            return source;
        }

        private static GameplayTriggerRuntime CreateRuntime(
            IGameplayTriggerActionSink sink,
            out GameplayTriggerRuntimeEventSource source)
        {
            GameplayTriggerCatalog catalog = CreateCatalog(sink);
            source = new GameplayTriggerRuntimeEventSource(64);
            return new GameplayTriggerRuntime(catalog, Options(), source);
        }

        private static GameplayTriggerCatalog CreateCatalog(IGameplayTriggerActionSink sink)
        {
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal,
                GameplayTriggerSignalOrdering.UnorderedMultiset);
            var route = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                GameplayTriggerOccurrenceKind.Pulsed,
                GameplayTriggerConditionProgram.Always,
                new[]
                {
                    new GameplayTriggerActionInstruction(
                        sink.ActionKindId,
                        GameplayTriggerTargetSelector.Owner,
                        payload0: 201,
                        payload1: 202,
                        payload2: 203)
                });
            var definition = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(1),
                1,
                source.SourceDefinitionId,
                new[] { route });
            var binding = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(1),
                definition.DefinitionId,
                SourceBindingId,
                Owner);
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[] { binding },
                new[] { sink },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult result),
                result.ErrorCode.ToString());
            return catalog;
        }

        private static GameplayTriggerCatalog CreateThreeBindingCatalog(
            IGameplayTriggerActionSink sink)
        {
            GameplayTriggerSourceDefinition source = SignalSource();
            var route = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                GameplayTriggerOccurrenceKind.Pulsed,
                GameplayTriggerConditionProgram.Always,
                new[]
                {
                    new GameplayTriggerActionInstruction(
                        sink.ActionKindId,
                        GameplayTriggerTargetSelector.Owner)
                });
            var definition = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(1),
                1,
                source.SourceDefinitionId,
                new[] { route });
            var bindings = new GameplayTriggerBinding[3];
            for (int i = 0; i < bindings.Length; i++)
            {
                bindings[i] = new GameplayTriggerBinding(
                    new GameplayTriggerBindingId(i + 1),
                    definition.DefinitionId,
                    new GameplayTriggerSourceBindingId(101 + i),
                    Owner);
            }
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                bindings,
                new[] { sink },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult result),
                result.ErrorCode.ToString());
            return catalog;
        }

        private static GameplayTriggerRuntime CreateInternalDeferredRuntime(
            out GameplayTriggerRuntimeEventSource eventSource)
        {
            GameplayTriggerActionKindId actionKindId = new GameplayTriggerActionKindId(2001);
            GameplayTriggerSourceDefinition source = SignalSource();
            var route = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                GameplayTriggerOccurrenceKind.Pulsed,
                GameplayTriggerConditionProgram.Always,
                new[]
                {
                    new GameplayTriggerActionInstruction(
                        actionKindId,
                        GameplayTriggerTargetSelector.ExplicitBindingTarget,
                        targetSlotId: 1,
                        payload0: 1L)
                });
            var definition = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(1),
                1,
                source.SourceDefinitionId,
                new[] { route });
            var binding = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(1),
                definition.DefinitionId,
                SourceBindingId,
                Owner,
                explicitTargets: new[]
                {
                    new GameplayTriggerBindingTarget(
                        1,
                        GameplayTriggerTargetRef.FromTriggerSourceBinding(SourceBindingId))
                });
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[] { binding },
                Array.Empty<IGameplayTriggerActionSink>(),
                new[]
                {
                    new GameplayTriggerInternalDeferredActionDescriptor(
                        actionKindId,
                        GameplayTriggerInternalDeferredOperationKind.SetEnabled)
                },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult result),
                result.ErrorCode.ToString());
            eventSource = new GameplayTriggerRuntimeEventSource(64);
            return new GameplayTriggerRuntime(catalog, Options(maxDeferred: 4), eventSource);
        }

        private static GameplayTriggerRuntime CreateReliableRuntime(
            out GameplayTriggerRuntimeEventSource eventSource)
        {
            var sink = new ReliableSink(ActionKindId);
            GameplayTriggerCatalog catalog = CreateCatalog(sink);
            eventSource = new GameplayTriggerRuntimeEventSource(64);
            return new GameplayTriggerRuntime(catalog, Options(maxReliable: 4), eventSource);
        }

        private static GameplayTriggerSourceDefinition SignalSource() =>
            new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal,
                GameplayTriggerSignalOrdering.UnorderedMultiset);

        private static GameplayTriggerRuntimeOptions Options(
            int maxDeferred = 0,
            int maxReliable = 0) =>
            new GameplayTriggerRuntimeOptions(
                8, 16, 16, 16, 16, 16, 16, 16, 8, 8,
                maxDeferred,
                maxReliable);

        private static bool SubmitOneSignal(
            GameplayTriggerRuntime runtime,
            RuntimeFrame frame) =>
            runtime.TrySubmitSignals(
                frame,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[]
                {
                    new GameplayTriggerSignal(
                        31,
                        Subject,
                        Instigator,
                        payload0: 101,
                        payload1: 102,
                        payload2: 103)
                },
                out _);

        private static bool PublishResult(
            GameplayTriggerRuntimeEventSource source,
            RuntimeFrame frame,
            long activationId = 1L) =>
            source.TryPublishActionResult(
                frame,
                activationId,
                actionIndex: 0,
                ActionKindId,
                GameplayTriggerTargetRef.FromGameplayEntity(Owner),
                GameplayTriggerRuntimeEventStatus.Succeeded,
                reasonCode: 0,
                resultPayload0: 301,
                resultPayload1: 302,
                resultPayload2: 303);

        private static GameplayTriggerRuntimeEvent FindLast(
            GameplayTriggerRuntimeEventSource source,
            GameplayTriggerRuntimeEventKind kind)
        {
            for (int i = source.PendingCount - 1; i >= 0; i--)
            {
                GameplayTriggerRuntimeEvent evt = source.GetPending(i);
                if (evt.Kind == kind) return evt;
            }
            Assert.Fail("Missing event kind " + kind);
            return default;
        }

        private static int CountKind(
            GameplayTriggerRuntimeEventSource source,
            GameplayTriggerRuntimeEventKind kind)
        {
            int count = 0;
            for (int i = 0; i < source.PendingCount; i++)
            {
                if (source.GetPending(i).Kind == kind) count++;
            }
            return count;
        }

        private static GameplaySystemContext CreateContext(
            RuntimeFrame frame,
            IReadOnlyList<RuntimeCommand> commands = null)
        {
            var componentWorld = new GameplayComponentWorld();
            return new GameplaySystemContext(
                frame,
                0d,
                0d,
                new GameplayWorld(),
                commands ?? Array.Empty<RuntimeCommand>(),
                componentWorld.Events,
                new GameplayCommandExecutionState(),
                componentWorld);
        }

        private static void AssertOrigin(
            GameplayTriggerRuntimeEvent evt,
            GameplayTriggerInstanceId instanceId)
        {
            Assert.AreEqual(new GameplayTriggerDefinitionId(1), evt.DefinitionId);
            Assert.AreEqual(new GameplayTriggerBindingId(1), evt.BindingId);
            Assert.AreEqual(SourceBindingId, evt.SourceBindingId);
            Assert.AreEqual(instanceId, evt.InstanceId);
            Assert.AreEqual(Owner, evt.Owner);
        }

        private static void AssertOccurrence(
            GameplayTriggerRuntimeEvent evt,
            RuntimeFrame frame)
        {
            Assert.AreEqual(frame, evt.Frame);
            Assert.AreEqual(frame, evt.OriginFrame);
            Assert.AreEqual(default(RuntimeFrame), evt.DueFrame);
            Assert.AreEqual(0L, evt.ProducerSequence);
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Pulsed, evt.OccurrenceKind);
            Assert.AreEqual(31, evt.SignalId);
            Assert.AreEqual(Subject, evt.Subject);
            Assert.AreEqual(Instigator, evt.Instigator);
            Assert.AreEqual(101L, evt.OccurrencePayload0);
            Assert.AreEqual(102L, evt.OccurrencePayload1);
            Assert.AreEqual(103L, evt.OccurrencePayload2);
        }

        private sealed class RecordingSink : IGameplayTriggerActionSink
        {
            public GameplayTriggerActionKindId ActionKindId =>
                GameplayTriggerRuntimeEventRouterTests.ActionKindId;
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;
            public bool ThrowOnCommit { get; set; }
            public int CommitCount { get; private set; }

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                token = 1;
                return true;
            }

            public void Abort(int token)
            {
            }

            public void Commit(int token, in GameplayTriggerActionIntent intent)
            {
                if (ThrowOnCommit) throw new InvalidOperationException("commit failed");
                CommitCount++;
            }
        }

        private sealed class ReliableSink : IGameplayTriggerActionSink
        {
            public ReliableSink(GameplayTriggerActionKindId actionKindId)
            {
                ActionKindId = actionKindId;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.ReliableDeferred;
            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token) => throw new InvalidOperationException();
            public void Abort(int token) => throw new InvalidOperationException();
            public void Commit(
                int token,
                in GameplayTriggerActionIntent intent) => throw new InvalidOperationException();
        }

        private sealed class AlwaysValidRestoreEntityValidator :
            IGameplayTriggerRestoreEntityValidator
        {
            public bool IsRestoredEntityValid(GameplayEntityId entityId) => entityId.IsValid;
        }

        private sealed class ThrowingTransfer : IGameplayTriggerReliableTargetTransfer
        {
            public ThrowingTransfer(
                GameplayTriggerActionKindId actionKindId,
                string targetModuleId)
            {
                ActionKindId = actionKindId;
                TargetModuleId = targetModuleId;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public string TargetModuleId { get; }
            public bool IsEnabled => true;
            public int CallCount { get; private set; }

            public GameplayTriggerReliableTransferResult TryTransfer(
                RuntimeFrame currentFrame,
                in GameplayTriggerReliableOutboxRecord record)
            {
                CallCount++;
                throw new InvalidOperationException("transfer failed");
            }
        }

        private sealed class PassiveRuntimeModule : RuntimeModule
        {
            public PassiveRuntimeModule(string moduleId)
                : base(moduleId, RuntimeTickStage.Simulation, 0)
            {
            }

            public override void Tick(RuntimeTickContext context)
            {
            }
        }
    }
}
