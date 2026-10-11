using System;
using System.Collections.Generic;
using System.Threading;
using MxFramework.UI;

namespace MxFramework.NoEngineTests.UI.Feedback
{
    internal static class Program
    {
        private static readonly MxUiViewId ViewId = new MxUiViewId("settings.panel");

        private static int Main()
        {
            CommandCompatibilityAndRouting();
            OutcomesRemainInvocationScoped();
            ExpiredAndOverflowOutcomesStaySilent();
            BindingAndThreadOwnershipFailFast();
            Console.WriteLine("UI feedback noEngine tests passed.");
            return 0;
        }

        private static void CommandCompatibilityAndRouting()
        {
            var inner = new RecordingCommandSink();
            var player = new RecordingPlayer();
            using var route = new MxUiFeedbackCommandSink(
                inner,
                Registry(
                    "settings.toggle",
                    new MxUiFeedbackPolicy(
                        MxUiFeedbackKind.Click,
                        MxUiFeedbackKind.None,
                        MxUiFeedbackKind.None)),
                player);

            var original = new MxUiCommand(ViewId, "settings.toggle", payload: null);
            route.Enqueue(original);

            Require(original.InvocationId == 0L, "Legacy commands must remain unrouted.");
            Require(inner.Commands.Count == 1, "The business command was not forwarded.");
            Require(inner.Commands[0].InvocationId == 1L, "The route did not assign an invocation.");
            Require(player.Kinds.Count == 1 && player.Kinds[0] == MxUiFeedbackKind.Click,
                "Activation feedback was not routed.");
        }

        private static void OutcomesRemainInvocationScoped()
        {
            var inner = new RecordingCommandSink();
            var player = new RecordingPlayer();
            using var route = CreateResultRoute(inner, player);

            route.Enqueue(new MxUiCommand(ViewId, "settings.save", payload: null));
            route.Enqueue(new MxUiCommand(ViewId, "settings.save", payload: null));
            long first = inner.Commands[0].InvocationId;
            long second = inner.Commands[1].InvocationId;

            route.Complete(new MxUiCommandOutcome(second, MxUiCommandOutcomeStatus.Failure));
            route.Complete(new MxUiCommandOutcome(first, MxUiCommandOutcomeStatus.Success));

            Require(player.Kinds.Count == 2, "Both outcomes must produce feedback.");
            Require(player.Kinds[0] == MxUiFeedbackKind.Error, "Failure feedback was incorrect.");
            Require(player.Kinds[1] == MxUiFeedbackKind.Confirm, "Success feedback was incorrect.");
            Require(route.CaptureDebugSnapshot().PendingCount == 0, "Completed outcomes remained pending.");
        }

        private static void ExpiredAndOverflowOutcomesStaySilent()
        {
            var clock = new FakeClock();
            var inner = new RecordingCommandSink();
            var player = new RecordingPlayer();
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
            long expired = inner.Commands[0].InvocationId;
            clock.TimestampValue = 301L;
            route.Complete(new MxUiCommandOutcome(expired, MxUiCommandOutcomeStatus.Success));

            Require(player.Kinds.Count == 0, "Expired outcome produced feedback.");
            Require(route.CaptureDebugSnapshot().LastDiagnostic ==
                    MxUiFeedbackDiagnosticCode.OutcomeExpired,
                "Expired outcome diagnostic was not recorded.");

            for (int i = 0; i < MxUiFeedbackCommandSink.PendingCapacity + 1; i++)
                route.Enqueue(new MxUiCommand(ViewId, "settings.save", payload: i));

            MxUiFeedbackDebugSnapshot snapshot = route.CaptureDebugSnapshot();
            Require(snapshot.PendingCount == MxUiFeedbackCommandSink.PendingCapacity,
                "Pending storage did not remain bounded.");
            Require(inner.Commands.Count == MxUiFeedbackCommandSink.PendingCapacity + 2,
                "Overflow blocked a business command.");
            Require(snapshot.LastDiagnostic == MxUiFeedbackDiagnosticCode.PendingOverflow,
                "Overflow diagnostic was not recorded.");
        }

        private static void BindingAndThreadOwnershipFailFast()
        {
            var channel = new MxUiCommandOutcomeChannel();
            var first = new RecordingOutcomeSink();
            var second = new RecordingOutcomeSink();
            using IMxUiCommandOutcomeBinding binding = channel.BindOutcomeSink(first);

            bool conflictFailed = false;
            try
            {
                channel.BindOutcomeSink(second);
            }
            catch (InvalidOperationException)
            {
                conflictFailed = true;
            }

            Require(conflictFailed, "A second outcome owner replaced the first.");

            Exception wrongThread = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var outcome = new MxUiCommandOutcome(1L, MxUiCommandOutcomeStatus.Success);
                    channel.TryComplete(in outcome);
                }
                catch (Exception exception)
                {
                    wrongThread = exception;
                }
            });
            thread.Start();
            thread.Join();

            Require(wrongThread is InvalidOperationException,
                "Background completion did not fail fast.");
            Require(first.Count == 0, "Background completion reached the outcome sink.");
        }

        private static MxUiFeedbackCommandSink CreateResultRoute(
            RecordingCommandSink inner,
            RecordingPlayer player)
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

        private static MxUiFeedbackPolicyRegistry Registry(
            string commandId,
            MxUiFeedbackPolicy policy)
        {
            return new MxUiFeedbackPolicyRegistry(
                new[] { new MxUiFeedbackPolicyBinding(ViewId, commandId, policy) });
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class RecordingCommandSink : IMxUiCommandSink
        {
            public List<MxUiCommand> Commands { get; } = new List<MxUiCommand>();

            public void Enqueue(MxUiCommand command)
            {
                Commands.Add(command);
            }
        }

        private sealed class RecordingPlayer : IMxUiFeedbackPlayer
        {
            public List<MxUiFeedbackKind> Kinds { get; } = new List<MxUiFeedbackKind>();

            public bool TryPlay(MxUiFeedbackKind kind, in MxUiFeedbackContext context)
            {
                Kinds.Add(kind);
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
            public long TimestampValue { get; set; }
            public long Timestamp => TimestampValue;
            public long Frequency => 10L;
        }
    }
}
