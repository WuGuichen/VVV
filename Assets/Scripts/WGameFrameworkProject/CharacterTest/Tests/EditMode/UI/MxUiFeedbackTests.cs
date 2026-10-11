using System;
using System.Collections.Generic;
using System.Threading;
using MxFramework.UI;
using NUnit.Framework;

namespace MxFramework.Tests.UI
{
    public sealed class MxUiFeedbackTests
    {
        private static readonly MxUiViewId ViewId = new MxUiViewId("settings.panel");

        [Test]
        public void Command_ThreeArgumentConstructorRemainsUnrouted()
        {
            var payload = new object();
            var command = new MxUiCommand(ViewId, "settings.apply", payload);

            MxUiCommand routed = command.WithInvocationId(42L);

            Assert.AreEqual(0L, command.InvocationId);
            Assert.AreEqual(42L, routed.InvocationId);
            Assert.AreEqual(command.SourceViewId, routed.SourceViewId);
            Assert.AreEqual(command.CommandId, routed.CommandId);
            Assert.AreSame(payload, routed.Payload);
            Assert.Throws<ArgumentOutOfRangeException>(() => command.WithInvocationId(0L));
        }

        [Test]
        public void PolicyRegistry_UsesExactViewAndCommandMatch()
        {
            var click = new MxUiFeedbackPolicy(
                MxUiFeedbackKind.Click,
                MxUiFeedbackKind.None,
                MxUiFeedbackKind.None);
            var registry = Registry("settings.toggle", click);

            Assert.IsTrue(registry.TryGetPolicy(ViewId, "settings.toggle", out MxUiFeedbackPolicy found));
            Assert.AreEqual(click, found);
            Assert.IsFalse(registry.TryGetPolicy(ViewId, "SETTINGS.TOGGLE", out _));
            Assert.IsFalse(registry.TryGetPolicy(new MxUiViewId("other.panel"), "settings.toggle", out _));
        }

        [Test]
        public void PolicyRegistry_RejectsDuplicateBindings()
        {
            var binding = new MxUiFeedbackPolicyBinding(ViewId, "settings.toggle", default);

            Assert.Throws<ArgumentException>(() => new MxUiFeedbackPolicyRegistry(
                new[] { binding, binding }));
        }

        [Test]
        public void Enqueue_PlaysActivationBeforeForwardingAndAssignsInvocation()
        {
            var log = new List<string>();
            var inner = new RecordingCommandSink(log);
            var player = new RecordingFeedbackPlayer(log);
            var policy = new MxUiFeedbackPolicy(
                MxUiFeedbackKind.Click,
                MxUiFeedbackKind.None,
                MxUiFeedbackKind.None);
            using var route = new MxUiFeedbackCommandSink(
                inner,
                Registry("settings.toggle", policy),
                player);

            route.Enqueue(new MxUiCommand(ViewId, "settings.toggle", payload: null));

            CollectionAssert.AreEqual(new[] { "play:Click", "enqueue" }, log);
            Assert.AreEqual(1L, inner.Commands[0].InvocationId);
            Assert.AreEqual(0, route.CaptureDebugSnapshot().PendingCount);
        }

        [Test]
        public void Outcome_RoutesRapidInvocationsIndependentlyAndOutOfOrder()
        {
            var inner = new RecordingCommandSink();
            var player = new RecordingFeedbackPlayer();
            var policy = new MxUiFeedbackPolicy(
                MxUiFeedbackKind.None,
                MxUiFeedbackKind.Confirm,
                MxUiFeedbackKind.Error);
            using var route = new MxUiFeedbackCommandSink(
                inner,
                Registry("settings.save", policy),
                player);

            route.Enqueue(new MxUiCommand(ViewId, "settings.save", payload: null));
            route.Enqueue(new MxUiCommand(ViewId, "settings.save", payload: null));
            long first = inner.Commands[0].InvocationId;
            long second = inner.Commands[1].InvocationId;

            route.Complete(new MxUiCommandOutcome(second, MxUiCommandOutcomeStatus.Failure, "disk"));
            route.Complete(new MxUiCommandOutcome(first, MxUiCommandOutcomeStatus.Success));

            CollectionAssert.AreEqual(
                new[] { MxUiFeedbackKind.Error, MxUiFeedbackKind.Confirm },
                player.Kinds);
            CollectionAssert.AreEqual(new[] { second, first }, player.InvocationIds);
            Assert.AreEqual(0, route.CaptureDebugSnapshot().PendingCount);
        }

        [Test]
        public void Outcome_CancelledRemovesPendingWithoutPlaying()
        {
            var inner = new RecordingCommandSink();
            var player = new RecordingFeedbackPlayer();
            using var route = new MxUiFeedbackCommandSink(
                inner,
                Registry(
                    "settings.save",
                    new MxUiFeedbackPolicy(
                        MxUiFeedbackKind.None,
                        MxUiFeedbackKind.Confirm,
                        MxUiFeedbackKind.Error)),
                player);

            route.Enqueue(new MxUiCommand(ViewId, "settings.save", payload: null));
            route.Complete(new MxUiCommandOutcome(
                inner.Commands[0].InvocationId,
                MxUiCommandOutcomeStatus.Cancelled));

            Assert.AreEqual(0, player.Kinds.Count);
            Assert.AreEqual(0, route.CaptureDebugSnapshot().PendingCount);
        }

        [Test]
        public void Outcome_CanCompleteSynchronouslyInsideInnerEnqueue()
        {
            var inner = new OutcomeCommandSink
            {
                CompleteOnEnqueue = MxUiCommandOutcomeStatus.Success
            };
            var player = new RecordingFeedbackPlayer();
            using var route = new MxUiFeedbackCommandSink(
                inner,
                Registry(
                    "settings.save",
                    new MxUiFeedbackPolicy(
                        MxUiFeedbackKind.None,
                        MxUiFeedbackKind.Confirm,
                        MxUiFeedbackKind.Error)),
                player);

            route.Enqueue(new MxUiCommand(ViewId, "settings.save", payload: null));

            CollectionAssert.AreEqual(
                new[] { MxUiFeedbackKind.Confirm },
                player.Kinds);
            Assert.AreEqual(0, route.CaptureDebugSnapshot().PendingCount);
        }

        [Test]
        public void Outcome_DuplicateAndUnknownAreSilentDiagnostics()
        {
            var inner = new RecordingCommandSink();
            var player = new RecordingFeedbackPlayer();
            using var route = CreateResultRoute(inner, player);

            route.Enqueue(new MxUiCommand(ViewId, "settings.save", payload: null));
            long invocationId = inner.Commands[0].InvocationId;
            var outcome = new MxUiCommandOutcome(
                invocationId,
                MxUiCommandOutcomeStatus.Success);

            route.Complete(in outcome);
            route.Complete(in outcome);

            Assert.AreEqual(1, player.Kinds.Count);
            Assert.AreEqual(
                MxUiFeedbackDiagnosticCode.OutcomeUnknown,
                route.CaptureDebugSnapshot().LastDiagnostic);
        }

        [Test]
        public void Outcome_ExpiresAfterThirtySeconds()
        {
            var clock = new FakeClock(frequency: 10L);
            var inner = new RecordingCommandSink();
            var player = new RecordingFeedbackPlayer();
            using var route = new MxUiFeedbackCommandSink(
                inner,
                Registry(
                    "settings.save",
                    new MxUiFeedbackPolicy(
                        MxUiFeedbackKind.None,
                        MxUiFeedbackKind.Confirm,
                        MxUiFeedbackKind.Error)),
                player,
                clock);

            route.Enqueue(new MxUiCommand(ViewId, "settings.save", payload: null));
            clock.TimestampValue = 301L;
            route.Complete(new MxUiCommandOutcome(
                inner.Commands[0].InvocationId,
                MxUiCommandOutcomeStatus.Success));

            Assert.AreEqual(0, player.Kinds.Count);
            Assert.AreEqual(0, route.CaptureDebugSnapshot().PendingCount);
            Assert.AreEqual(
                MxUiFeedbackDiagnosticCode.OutcomeExpired,
                route.CaptureDebugSnapshot().LastDiagnostic);
        }

        [Test]
        public void PendingOverflow_StillForwardsBusinessCommand()
        {
            var inner = new RecordingCommandSink();
            using var route = CreateResultRoute(inner, new RecordingFeedbackPlayer());

            for (int i = 0; i < MxUiFeedbackCommandSink.PendingCapacity + 1; i++)
                route.Enqueue(new MxUiCommand(ViewId, "settings.save", payload: i));

            MxUiFeedbackDebugSnapshot snapshot = route.CaptureDebugSnapshot();
            Assert.AreEqual(MxUiFeedbackCommandSink.PendingCapacity + 1, inner.Commands.Count);
            Assert.AreEqual(MxUiFeedbackCommandSink.PendingCapacity, snapshot.PendingCount);
            Assert.AreEqual(MxUiFeedbackDiagnosticCode.PendingOverflow, snapshot.LastDiagnostic);
        }

        [Test]
        public void MissingPolicy_IsSilentAndStillForwards()
        {
            var inner = new RecordingCommandSink();
            var player = new RecordingFeedbackPlayer();
            using var route = new MxUiFeedbackCommandSink(
                inner,
                new MxUiFeedbackPolicyRegistry(Array.Empty<MxUiFeedbackPolicyBinding>()),
                player);

            route.Enqueue(new MxUiCommand(ViewId, "unregistered", payload: null));

            Assert.AreEqual(1, inner.Commands.Count);
            Assert.AreEqual(0, player.Kinds.Count);
            Assert.AreEqual(
                MxUiFeedbackDiagnosticCode.PolicyMissing,
                route.CaptureDebugSnapshot().LastDiagnostic);
        }

        [Test]
        public void HandlerException_ClearsPendingAndPreservesException()
        {
            var inner = new ThrowingCommandSink();
            using var route = new MxUiFeedbackCommandSink(
                inner,
                Registry(
                    "settings.save",
                    new MxUiFeedbackPolicy(
                        MxUiFeedbackKind.None,
                        MxUiFeedbackKind.Confirm,
                        MxUiFeedbackKind.Error)));

            Assert.Throws<ExpectedException>(() =>
                route.Enqueue(new MxUiCommand(ViewId, "settings.save", payload: null)));
            Assert.AreEqual(0, route.CaptureDebugSnapshot().PendingCount);
            Assert.AreEqual(
                MxUiFeedbackDiagnosticCode.HandlerException,
                route.CaptureDebugSnapshot().LastDiagnostic);
        }

        [Test]
        public void Complete_FromBackgroundThreadFailsBeforePlayerAccess()
        {
            var inner = new RecordingCommandSink();
            var player = new RecordingFeedbackPlayer();
            using var route = CreateResultRoute(inner, player);
            route.Enqueue(new MxUiCommand(ViewId, "settings.save", payload: null));
            var outcome = new MxUiCommandOutcome(
                inner.Commands[0].InvocationId,
                MxUiCommandOutcomeStatus.Success);
            Exception backgroundException = null;

            var thread = new Thread(() =>
            {
                try
                {
                    route.Complete(in outcome);
                }
                catch (Exception exception)
                {
                    backgroundException = exception;
                }
            });
            thread.Start();
            thread.Join();

            Assert.IsInstanceOf<InvalidOperationException>(backgroundException);
            Assert.AreEqual(0, player.Kinds.Count);
            Assert.AreEqual(1, route.CaptureDebugSnapshot().PendingCount);
            Assert.AreEqual(
                MxUiFeedbackDiagnosticCode.WrongThread,
                route.CaptureDebugSnapshot().LastDiagnostic);
        }

        [Test]
        public void Dispose_ReleasesBindingAndLateDirectOutcomeIsSilent()
        {
            var inner = new OutcomeCommandSink();
            var player = new RecordingFeedbackPlayer();
            var route = new MxUiFeedbackCommandSink(
                inner,
                Registry(
                    "settings.save",
                    new MxUiFeedbackPolicy(
                        MxUiFeedbackKind.None,
                        MxUiFeedbackKind.Confirm,
                        MxUiFeedbackKind.Error)),
                player);
            route.Enqueue(new MxUiCommand(ViewId, "settings.save", payload: null));
            long invocationId = inner.Commands[0].InvocationId;

            route.Dispose();
            route.Complete(new MxUiCommandOutcome(
                invocationId,
                MxUiCommandOutcomeStatus.Success));

            Assert.IsFalse(inner.Outcomes.HasActiveBinding);
            Assert.AreEqual(0, player.Kinds.Count);
            Assert.AreEqual(
                MxUiFeedbackDiagnosticCode.OutcomeAfterDispose,
                route.CaptureDebugSnapshot().LastDiagnostic);
        }

        [Test]
        public void OutcomeChannel_RejectsSecondOwnerAndStaleLeaseCannotReleaseNewOwner()
        {
            var channel = new MxUiCommandOutcomeChannel();
            var firstSink = new RecordingOutcomeSink();
            var secondSink = new RecordingOutcomeSink();
            IMxUiCommandOutcomeBinding first = channel.BindOutcomeSink(firstSink);

            Assert.Throws<InvalidOperationException>(() => channel.BindOutcomeSink(secondSink));

            first.Dispose();
            IMxUiCommandOutcomeBinding second = channel.BindOutcomeSink(secondSink);
            first.Dispose();
            var outcome = new MxUiCommandOutcome(7L, MxUiCommandOutcomeStatus.Success);

            Assert.IsTrue(channel.TryComplete(in outcome));
            Assert.AreEqual(0, firstSink.Count);
            Assert.AreEqual(1, secondSink.Count);

            second.Dispose();
            Assert.IsFalse(channel.TryComplete(in outcome));
        }

        private static MxUiFeedbackPolicyRegistry Registry(
            string commandId,
            MxUiFeedbackPolicy policy)
        {
            return new MxUiFeedbackPolicyRegistry(
                new[] { new MxUiFeedbackPolicyBinding(ViewId, commandId, policy) });
        }

        private static MxUiFeedbackCommandSink CreateResultRoute(
            RecordingCommandSink inner,
            RecordingFeedbackPlayer player)
        {
            return new MxUiFeedbackCommandSink(
                inner,
                Registry(
                    "settings.save",
                    new MxUiFeedbackPolicy(
                        MxUiFeedbackKind.None,
                        MxUiFeedbackKind.Confirm,
                        MxUiFeedbackKind.Error)),
                player);
        }

        private sealed class RecordingCommandSink : IMxUiCommandSink
        {
            private readonly List<string> _log;

            public RecordingCommandSink(List<string> log = null)
            {
                _log = log;
            }

            public List<MxUiCommand> Commands { get; } = new List<MxUiCommand>();

            public void Enqueue(MxUiCommand command)
            {
                _log?.Add("enqueue");
                Commands.Add(command);
            }
        }

        private sealed class OutcomeCommandSink : IMxUiCommandSink, IMxUiCommandOutcomeSource
        {
            public MxUiCommandOutcomeChannel Outcomes { get; } = new MxUiCommandOutcomeChannel();
            public List<MxUiCommand> Commands { get; } = new List<MxUiCommand>();
            public MxUiCommandOutcomeStatus? CompleteOnEnqueue { get; set; }

            public void Enqueue(MxUiCommand command)
            {
                Commands.Add(command);
                if (CompleteOnEnqueue.HasValue)
                {
                    var outcome = new MxUiCommandOutcome(
                        command.InvocationId,
                        CompleteOnEnqueue.Value);
                    Outcomes.TryComplete(in outcome);
                }
            }

            public IMxUiCommandOutcomeBinding BindOutcomeSink(IMxUiCommandOutcomeSink sink)
            {
                return Outcomes.BindOutcomeSink(sink);
            }
        }

        private sealed class ThrowingCommandSink : IMxUiCommandSink
        {
            public void Enqueue(MxUiCommand command)
            {
                throw new ExpectedException();
            }
        }

        private sealed class ExpectedException : Exception
        {
        }

        private sealed class RecordingFeedbackPlayer : IMxUiFeedbackPlayer
        {
            private readonly List<string> _log;

            public RecordingFeedbackPlayer(List<string> log = null)
            {
                _log = log;
            }

            public List<MxUiFeedbackKind> Kinds { get; } = new List<MxUiFeedbackKind>();
            public List<long> InvocationIds { get; } = new List<long>();

            public bool TryPlay(MxUiFeedbackKind kind, in MxUiFeedbackContext context)
            {
                _log?.Add("play:" + kind);
                Kinds.Add(kind);
                InvocationIds.Add(context.InvocationId);
                return true;
            }
        }

        private sealed class RecordingOutcomeSink : IMxUiCommandOutcomeSink
        {
            public int Count { get; private set; }

            public void Complete(in MxUiCommandOutcome outcome)
            {
                Count++;
            }
        }

        private sealed class FakeClock : IMxUiFeedbackClock
        {
            public FakeClock(long frequency)
            {
                Frequency = frequency;
            }

            public long TimestampValue { get; set; }
            public long Timestamp => TimestampValue;
            public long Frequency { get; }
        }
    }
}
