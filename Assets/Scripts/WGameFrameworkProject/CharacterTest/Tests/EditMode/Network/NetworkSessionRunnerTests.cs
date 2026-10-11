using System.Collections.Generic;
using MxFramework.Network;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Network
{
    public sealed class NetworkSessionRunnerTests
    {
        private static class TestTraceIds
        {
            internal static readonly RuntimeTraceIdentity AckMe = new RuntimeTraceIdentity(91021001); // ack-me
            internal static readonly RuntimeTraceIdentity AfterRestore = new RuntimeTraceIdentity(91021002); // after-restore
            internal static readonly RuntimeTraceIdentity ClientACommand = new RuntimeTraceIdentity(91021003); // client-a-command
            internal static readonly RuntimeTraceIdentity CodecCommand = new RuntimeTraceIdentity(91021004); // codec-command
            internal static readonly RuntimeTraceIdentity ContinuousInput = new RuntimeTraceIdentity(91021005); // continuous-input
            internal static readonly RuntimeTraceIdentity First = new RuntimeTraceIdentity(91021006); // first
            internal static readonly RuntimeTraceIdentity Input = new RuntimeTraceIdentity(91021007); // input
            internal static readonly RuntimeTraceIdentity LaggingClient = new RuntimeTraceIdentity(91021008); // lagging-client
            internal static readonly RuntimeTraceIdentity Late = new RuntimeTraceIdentity(91021009); // late
            internal static readonly RuntimeTraceIdentity Late1 = new RuntimeTraceIdentity(91021010); // late-1
            internal static readonly RuntimeTraceIdentity Late2 = new RuntimeTraceIdentity(91021011); // late-2
            internal static readonly RuntimeTraceIdentity LateAuthoritative = new RuntimeTraceIdentity(91021012); // late-authoritative
            internal static readonly RuntimeTraceIdentity Left = new RuntimeTraceIdentity(91021013); // left
            internal static readonly RuntimeTraceIdentity Move = new RuntimeTraceIdentity(91021014); // move
            internal static readonly RuntimeTraceIdentity Move0 = new RuntimeTraceIdentity(91021015); // move-0
            internal static readonly RuntimeTraceIdentity Move1 = new RuntimeTraceIdentity(91021016); // move-1
            internal static readonly RuntimeTraceIdentity Once = new RuntimeTraceIdentity(91021017); // once
            internal static readonly RuntimeTraceIdentity OwnedTarget = new RuntimeTraceIdentity(91021018); // owned-target
            internal static readonly RuntimeTraceIdentity Predicted = new RuntimeTraceIdentity(91021019); // predicted
            internal static readonly RuntimeTraceIdentity Right = new RuntimeTraceIdentity(91021020); // right
            internal static readonly RuntimeTraceIdentity Second = new RuntimeTraceIdentity(91021021); // second
            internal static readonly RuntimeTraceIdentity Submitted = new RuntimeTraceIdentity(91021022); // submitted
            internal static readonly RuntimeTraceIdentity Third = new RuntimeTraceIdentity(91021023); // third
            internal static readonly RuntimeTraceIdentity Wrapped = new RuntimeTraceIdentity(91021024); // wrapped
            internal static readonly RuntimeTraceIdentity WrongTarget = new RuntimeTraceIdentity(91021025); // wrong-target
        }

        [Test]
        public void RuntimeCommandBufferSnapshot_RestoresFramePendingCommandsAndSequence()
        {
            var buffer = new RuntimeCommandBuffer();
            RuntimeCommandValidationResult first = buffer.Enqueue(new RuntimeCommand(
                new RuntimeFrame(3),
                1,
                10,
                100,
                traceId: TestTraceIds.First));
            RuntimeCommandValidationResult second = buffer.Enqueue(new RuntimeCommand(
                new RuntimeFrame(5),
                1,
                10,
                100,
                traceId: TestTraceIds.Second));
            Assert.IsTrue(first.Success, first.Error.Message);
            Assert.IsTrue(second.Success, second.Error.Message);

            Assert.AreEqual(0, buffer.DrainForFrame(new RuntimeFrame(2)).Count);
            RuntimeCommandBufferSnapshot snapshot = buffer.CaptureSnapshot();
            RuntimeCommandValidationResult third = buffer.Enqueue(new RuntimeCommand(
                new RuntimeFrame(6),
                1,
                10,
                100,
                traceId: TestTraceIds.Third));
            Assert.IsTrue(third.Success, third.Error.Message);
            Assert.AreEqual(2, third.Command.Sequence);
            Assert.AreEqual(1, buffer.DrainForFrame(new RuntimeFrame(3)).Count);

            buffer.RestoreSnapshot(snapshot);

            RuntimeCommandValidationResult afterRestore = buffer.Enqueue(new RuntimeCommand(
                new RuntimeFrame(6),
                1,
                10,
                100,
                traceId: TestTraceIds.AfterRestore));
            Assert.IsTrue(afterRestore.Success, afterRestore.Error.Message);
            Assert.AreEqual(2, afterRestore.Command.Sequence);
            Assert.AreEqual(new RuntimeFrame(3), buffer.CurrentFrame);
            IReadOnlyList<RuntimeCommand> frame3 = buffer.DrainForFrame(new RuntimeFrame(3));
            Assert.AreEqual(1, frame3.Count);
            Assert.AreEqual(TestTraceIds.First, frame3[0].TraceId);
            IReadOnlyList<RuntimeCommand> frame6 = buffer.DrainForFrame(new RuntimeFrame(6));
            Assert.AreEqual(2, frame6.Count);
            Assert.AreEqual(TestTraceIds.Second, frame6[0].TraceId);
            Assert.AreEqual(TestTraceIds.AfterRestore, frame6[1].TraceId);
        }

        [Test]
        public void PredictionRollbackCoordinator_ReplaysFromEarliestAuthoritativeFrame()
        {
            var state = new RollbackCounterState();
            var participant = new CounterRollbackParticipant(state);
            var coordinator = new NetworkPredictionRollbackCoordinator(historyCapacity: 4);
            var pending = new List<RuntimeCommand>();
            coordinator.RegisterParticipant(participant);

            RuntimeCommand predicted0 = new RuntimeCommand(
                RuntimeFrame.Zero,
                1,
                10,
                100,
                payload0: 1,
                traceId: TestTraceIds.Move0);
            RuntimeCommand predicted1 = new RuntimeCommand(
                new RuntimeFrame(1),
                1,
                10,
                100,
                payload0: 1,
                traceId: TestTraceIds.Move1);
            coordinator.RecordPredictedCommand(predicted0);
            coordinator.RecordPredictedCommand(predicted1);
            coordinator.RecordFrame(RuntimeFrame.Zero);
            state.Apply(predicted0);
            state.Frame = new RuntimeFrame(1);
            coordinator.RecordFrame(new RuntimeFrame(1));
            state.Apply(predicted1);
            state.Frame = new RuntimeFrame(2);

            RuntimeCommand authoritative0 = new RuntimeCommand(
                RuntimeFrame.Zero,
                1,
                10,
                100,
                payload0: 5,
                traceId: TestTraceIds.Move0);

            NetworkPredictionRollbackResult result = coordinator.ReconcileAuthoritativeCommands(
                new[] { authoritative0 },
                new RuntimeFrame(2),
                delegate(RuntimeCommand command, out string failure)
                {
                    failure = string.Empty;
                    pending.RemoveAll(existing =>
                        NetworkPredictionRollbackCoordinator.AreEquivalentCommandIntents(existing, command));
                    pending.Add(command);
                    return true;
                },
                delegate(RuntimeFrame frame, out string failure)
                {
                    failure = string.Empty;
                    pending.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
                    for (int i = 0; i < pending.Count; i++)
                    {
                        if (pending[i].Frame == frame)
                            state.Apply(pending[i]);
                    }

                    pending.RemoveAll(command => command.Frame == frame);
                    state.Frame = frame.Next();
                    return true;
                });

            Assert.IsTrue(result.Success, result.Message);
            Assert.IsTrue(result.RolledBack);
            Assert.AreEqual(new RuntimeFrame(0), result.RollbackFrame);
            Assert.AreEqual(2, result.ReplayedFrameCount);
            Assert.AreEqual(6, state.Value);
            Assert.AreEqual(new RuntimeFrame(2), state.Frame);
            Assert.AreEqual(1, coordinator.Diagnostics.RollbackCount);
        }

        [Test]
        public void PredictionRollbackCoordinator_DoesNotMatchSharedTraceAcrossFramesOrTargets()
        {
            RuntimeCommand reference = new RuntimeCommand(
                new RuntimeFrame(10),
                1,
                20,
                100,
                payload0: 7,
                traceId: TestTraceIds.ContinuousInput);
            RuntimeCommand otherFrame = new RuntimeCommand(
                new RuntimeFrame(11),
                1,
                20,
                100,
                payload0: 7,
                traceId: TestTraceIds.ContinuousInput);
            RuntimeCommand otherTarget = new RuntimeCommand(
                new RuntimeFrame(10),
                1,
                20,
                200,
                payload0: 7,
                traceId: TestTraceIds.ContinuousInput);
            RuntimeCommand correctedPayload = new RuntimeCommand(
                new RuntimeFrame(10),
                1,
                20,
                100,
                payload0: 9,
                traceId: TestTraceIds.ContinuousInput);

            Assert.IsFalse(NetworkPredictionRollbackCoordinator.AreEquivalentCommandIntents(
                reference,
                otherFrame));
            Assert.IsFalse(NetworkPredictionRollbackCoordinator.AreEquivalentCommandIntents(
                reference,
                otherTarget));
            Assert.IsTrue(NetworkPredictionRollbackCoordinator.AreEquivalentCommandIntents(
                reference,
                correctedPayload));
        }

        [Test]
        public void PredictionRollbackCoordinator_ReportsMissingHistoryAfterRingWrap()
        {
            var state = new RollbackCounterState();
            var coordinator = new NetworkPredictionRollbackCoordinator(historyCapacity: 2);
            coordinator.RegisterParticipant(new CounterRollbackParticipant(state));

            RuntimeCommand predicted = new RuntimeCommand(
                RuntimeFrame.Zero,
                1,
                10,
                100,
                payload0: 1,
                traceId: TestTraceIds.Wrapped);
            coordinator.RecordPredictedCommand(predicted);
            coordinator.RecordFrame(RuntimeFrame.Zero);
            coordinator.RecordFrame(new RuntimeFrame(1));
            coordinator.RecordFrame(new RuntimeFrame(2));

            RuntimeCommand authoritative = new RuntimeCommand(
                RuntimeFrame.Zero,
                1,
                10,
                100,
                payload0: 5,
                traceId: TestTraceIds.Wrapped);
            NetworkPredictionRollbackResult result = coordinator.ReconcileAuthoritativeCommands(
                new[] { authoritative },
                new RuntimeFrame(3),
                delegate(RuntimeCommand command, out string failure)
                {
                    failure = string.Empty;
                    return true;
                },
                delegate(RuntimeFrame frame, out string failure)
                {
                    failure = string.Empty;
                    return true;
                });

            Assert.IsFalse(result.Success);
            StringAssert.Contains("rollback history missing", result.Message);
            Assert.AreEqual(1, coordinator.Diagnostics.HistoryDropCount);
            Assert.AreEqual(1, coordinator.Diagnostics.RollbackFailureCount);
        }

        [Test]
        public void AuthoritativeCommands_ReceiveHookHandlesLateCommandBeforeDefaultDrop()
        {
            var buffer = new RuntimeCommandBuffer();
            var config = new NetworkSessionConfig(1, 60, 1, 1, "config", "resources");
            var transport = new LoopbackNetworkTransport();
            bool hookCalled = false;
            var client = CreateClient(
                config,
                transport,
                null,
                commandBuffer: buffer,
                authoritativeCommandReceiveHandler: delegate(NetworkAuthoritativeCommandPacket packet, RuntimeFrame receiveFrame)
                {
                    hookCalled = true;
                    Assert.AreEqual(new RuntimeFrame(12), receiveFrame);
                    Assert.AreEqual(1, packet.Commands.Count);
                    Assert.AreEqual(TestTraceIds.LateAuthoritative, packet.Commands[0].TraceId);
                    return true;
                });

            buffer.DrainForFrame(new RuntimeFrame(10));
            transport.Send(new NetworkMessageEnvelope(
                "session",
                "server",
                "client",
                0,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.AuthoritativeCommands,
                new RuntimeFrame(12),
                new NetworkAuthoritativeCommandPacket(
                    "remote",
                    new RuntimeFrame(12),
                    RuntimeFrame.Zero,
                    new[]
                    {
                        new RuntimeCommand(
                            new RuntimeFrame(3),
                            1,
                            10,
                            100,
                            traceId: TestTraceIds.LateAuthoritative)
                    })));

            client.ProcessIncoming(new RuntimeFrame(12));

            Assert.IsTrue(hookCalled);
            Assert.AreEqual(1, client.AuthoritativeCommandReceivedCount);
            Assert.AreEqual(0, client.AuthoritativeCommandLateCount);
            Assert.AreEqual(0, client.AuthoritativeCommandDroppedCount);
        }

        [Test]
        public void Handshake_AcceptsCompatibleConfigAndRejectsHashMismatch()
        {
            var config = new NetworkSessionConfig(1, 60, 1, 2, "config-a", "resources-a");
            var server = CreateServer(config, new LoopbackNetworkTransport(), null);
            var clientTransport = new LoopbackNetworkTransport();
            var rejectingServer = CreateServer(config, clientTransport, null);
            var client = CreateClient(new NetworkSessionConfig(1, 60, 1, 2, "config-b", "resources-a"), clientTransport, null);

            var transport = new LoopbackNetworkTransport();
            server = CreateServer(config, transport, null);
            var acceptedClient = CreateClient(config, transport, null);

            acceptedClient.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            acceptedClient.ProcessIncoming(RuntimeFrame.Zero);

            Assert.AreEqual(NetworkSessionStatus.Connected, acceptedClient.Status);

            client.Connect(RuntimeFrame.Zero);
            rejectingServer.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);

            Assert.AreEqual(NetworkSessionStatus.Rejected, client.Status);
            StringAssert.Contains("config hash", client.RejectionReason);
        }

        [Test]
        public void ApplicationControl_UnhandshakenSenderIsRejectedBeforeHandler()
        {
            var transport = new LoopbackNetworkTransport(
                LoopbackNetworkTransportOptions.ReliableImmediate);
            var server = new NetworkSessionRunner(
                NetworkSessionRole.Server,
                "session",
                "server",
                "server",
                NetworkSessionConfig.Default,
                transport);
            int handled = 0;
            server.SetApplicationControlHandler((peerId, request) =>
            {
                handled++;
                return new NetworkApplicationControlResponsePacket(
                    request.TypeId,
                    request.SchemaVersion,
                    request.RequestId,
                    accepted: true,
                    reason: string.Empty);
            });
            transport.Send(new NetworkMessageEnvelope(
                "session",
                "peer.untrusted",
                "server",
                0L,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.ApplicationControlRequest,
                RuntimeFrame.Zero,
                new NetworkApplicationControlRequestPacket(
                    "mx.test.control",
                    1,
                    1L,
                    "{}")));

            server.ProcessIncoming(RuntimeFrame.Zero);

            Assert.AreEqual(0, handled);
            Assert.IsFalse(server.IsPeerConnected("peer.untrusted"));
            StringAssert.Contains(
                "unconnected peer",
                string.Join(" | ", server.RecentDiagnostics));
        }

        [Test]
        public void RuntimeCommands_ArriveAndUseConfiguredInputDelay()
        {
            var buffer = new RuntimeCommandBuffer();
            var config = new NetworkSessionConfig(1, 60, 3, 2, "config", "resources");
            var transport = new LoopbackNetworkTransport();
            var server = CreateServer(config, transport, buffer);
            var client = CreateClient(config, transport, null);

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);

            client.SubmitCommands(
                new RuntimeFrame(2),
                new[]
                {
                    new RuntimeCommand(new RuntimeFrame(2), 2, 20, 100, traceId: TestTraceIds.Second),
                    new RuntimeCommand(new RuntimeFrame(1), 1, 10, 100, traceId: TestTraceIds.First)
                });
            server.ProcessIncoming(new RuntimeFrame(2));

            IReadOnlyList<RuntimeCommand> early = buffer.DrainForFrame(new RuntimeFrame(8));
            Assert.AreEqual(0, early.Count);

            IReadOnlyList<RuntimeCommand> first = buffer.DrainForFrame(new RuntimeFrame(9));
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(TestTraceIds.First, first[0].TraceId);
            Assert.AreEqual(new RuntimeFrame(9), first[0].Frame);

            IReadOnlyList<RuntimeCommand> second = buffer.DrainForFrame(new RuntimeFrame(10));
            Assert.AreEqual(1, second.Count);
            Assert.AreEqual(TestTraceIds.Second, second[0].TraceId);
            Assert.AreEqual(new RuntimeFrame(10), second[0].Frame);
            Assert.AreEqual(new RuntimeFrame(2), server.LastAckedInputFrame);
        }

        [Test]
        public void RuntimeCommands_UseReliableOrderedChannelForDeterministicInputStream()
        {
            var config = new NetworkSessionConfig(1, 60, 3, 2, "config", "resources");
            var transport = new LoopbackNetworkTransport();
            var server = CreateServer(config, transport, new RuntimeCommandBuffer());
            var client = CreateClient(config, transport, null);

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);

            client.SubmitCommands(
                new RuntimeFrame(2),
                new[] { new RuntimeCommand(new RuntimeFrame(2), 1, 10, 100, traceId: TestTraceIds.Input) });

            var messages = new List<NetworkMessageEnvelope>();
            transport.Poll("server", new RuntimeFrame(2), messages);

            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual(NetworkMessageKind.RuntimeCommands, messages[0].Kind);
            Assert.AreEqual(NetworkChannel.ReliableOrdered, messages[0].Channel);
        }

        [Test]
        public void RuntimeCommands_FromLaggingClientScheduleAgainstServerFrame()
        {
            var buffer = new RuntimeCommandBuffer();
            var config = new NetworkSessionConfig(1, 60, 3, 2, "config", "resources");
            var transport = new LoopbackNetworkTransport();
            var server = CreateServer(config, transport, buffer);
            var client = CreateClient(config, transport, null);

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);

            buffer.DrainForFrame(new RuntimeFrame(100));
            client.SubmitCommands(
                new RuntimeFrame(2),
                new[] { new RuntimeCommand(new RuntimeFrame(2), 1, 10, 100, traceId: TestTraceIds.LaggingClient) });
            server.ProcessIncoming(new RuntimeFrame(101));

            IReadOnlyList<RuntimeCommand> early = buffer.DrainForFrame(new RuntimeFrame(107));
            Assert.AreEqual(0, early.Count);
            Assert.AreEqual(0, server.LastCommandErrors.Count);

            IReadOnlyList<RuntimeCommand> drained = buffer.DrainForFrame(new RuntimeFrame(108));
            Assert.AreEqual(1, drained.Count);
            Assert.AreEqual(TestTraceIds.LaggingClient, drained[0].TraceId);
            Assert.AreEqual(new RuntimeFrame(108), drained[0].Frame);
            Assert.AreEqual(new RuntimeFrame(2), server.LastAckedInputFrame);
        }

        [Test]
        public void RuntimeCommands_DeliveredTogetherKeepClientFrameSpacing()
        {
            var buffer = new RuntimeCommandBuffer();
            var config = new NetworkSessionConfig(1, 60, 3, 2, "config", "resources");
            var transport = new LoopbackNetworkTransport();
            var server = CreateServer(config, transport, buffer);
            var client = CreateClient(config, transport, null);

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);

            buffer.DrainForFrame(new RuntimeFrame(100));
            client.SubmitCommands(
                new RuntimeFrame(2),
                new[] { new RuntimeCommand(new RuntimeFrame(2), 1, 10, 100, traceId: TestTraceIds.Left) });
            client.SubmitCommands(
                new RuntimeFrame(3),
                new[] { new RuntimeCommand(new RuntimeFrame(3), 1, 10, 100, traceId: TestTraceIds.Right) });
            server.ProcessIncoming(new RuntimeFrame(101));

            IReadOnlyList<RuntimeCommand> first = buffer.DrainForFrame(new RuntimeFrame(108));
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(TestTraceIds.Left, first[0].TraceId);
            Assert.AreEqual(new RuntimeFrame(108), first[0].Frame);

            IReadOnlyList<RuntimeCommand> second = buffer.DrainForFrame(new RuntimeFrame(109));
            Assert.AreEqual(1, second.Count);
            Assert.AreEqual(TestTraceIds.Right, second[0].TraceId);
            Assert.AreEqual(new RuntimeFrame(109), second[0].Frame);
            Assert.AreEqual(0, server.LastCommandErrors.Count);
            Assert.AreEqual(new RuntimeFrame(3), server.LastAckedInputFrame);
        }

        [Test]
        public void RuntimeCommands_ServerBroadcastsScheduledAuthoritativeCommandsToOtherClients()
        {
            var serverBuffer = new RuntimeCommandBuffer();
            var remoteBuffer = new RuntimeCommandBuffer();
            var config = new NetworkSessionConfig(1, 60, 2, 2, "config", "resources");
            var transport = new LoopbackNetworkTransport();
            var server = CreateServer(config, transport, serverBuffer);
            var clientA = CreateClient(config, transport, null, peerId: "client-a");
            var clientB = CreateClient(config, transport, null, peerId: "client-b", commandBuffer: remoteBuffer);

            clientA.Connect(RuntimeFrame.Zero);
            clientB.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            clientA.ProcessIncoming(RuntimeFrame.Zero);
            clientB.ProcessIncoming(RuntimeFrame.Zero);

            clientA.SubmitCommands(
                new RuntimeFrame(1),
                new[] { new RuntimeCommand(new RuntimeFrame(1), 1, 10, 100, traceId: TestTraceIds.Move) });
            server.ProcessIncoming(new RuntimeFrame(1));
            clientA.ProcessIncoming(new RuntimeFrame(1));
            clientB.ProcessIncoming(new RuntimeFrame(1));

            IReadOnlyList<RuntimeCommand> serverCommands = serverBuffer.DrainForFrame(new RuntimeFrame(7));
            IReadOnlyList<RuntimeCommand> remoteCommands = remoteBuffer.DrainForFrame(new RuntimeFrame(7));

            Assert.AreEqual(1, serverCommands.Count);
            Assert.AreEqual(new RuntimeFrame(7), serverCommands[0].Frame);
            Assert.AreEqual(1, remoteCommands.Count);
            Assert.AreEqual(TestTraceIds.Move, remoteCommands[0].TraceId);
            Assert.AreEqual(new RuntimeFrame(7), remoteCommands[0].Frame);
            Assert.AreEqual(2, server.AuthoritativeCommandSentCount);
            Assert.AreEqual(1, clientA.AuthoritativeCommandEchoCount);
            Assert.AreEqual(0, clientA.AuthoritativeCommandEnqueuedCount);
            Assert.AreEqual(1, clientB.AuthoritativeCommandEnqueuedCount);
            Assert.AreEqual(new RuntimeFrame(1), clientB.LastServerFrame);
        }

        [Test]
        public void RuntimeCommands_AuthoritativeBroadcastUsesReliableOrderedChannel()
        {
            var serverBuffer = new RuntimeCommandBuffer();
            var config = new NetworkSessionConfig(1, 60, 2, 2, "config", "resources");
            var transport = new LoopbackNetworkTransport();
            var server = CreateServer(config, transport, serverBuffer);
            var clientA = CreateClient(config, transport, null, peerId: "client-a");
            var clientB = CreateClient(config, transport, null, peerId: "client-b", commandBuffer: new RuntimeCommandBuffer());

            clientA.Connect(RuntimeFrame.Zero);
            clientB.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            clientA.ProcessIncoming(RuntimeFrame.Zero);
            clientB.ProcessIncoming(RuntimeFrame.Zero);

            clientA.SubmitCommands(
                new RuntimeFrame(1),
                new[] { new RuntimeCommand(new RuntimeFrame(1), 1, 10, 100, traceId: TestTraceIds.Move) });
            server.ProcessIncoming(new RuntimeFrame(1));

            var messages = new List<NetworkMessageEnvelope>();
            transport.Poll("client-b", new RuntimeFrame(1), messages);

            NetworkMessageEnvelope authoritative = FindSingleMessage(messages, NetworkMessageKind.AuthoritativeCommands);
            Assert.AreEqual(NetworkChannel.ReliableOrdered, authoritative.Channel);
        }

        [Test]
        public void RuntimeCommands_AuthoritativeBroadcastAllowsRemoteBufferDrift()
        {
            var serverBuffer = new RuntimeCommandBuffer();
            var remoteBuffer = new RuntimeCommandBuffer();
            var config = new NetworkSessionConfig(1, 60, 2, 2, "config", "resources");
            var transport = new LoopbackNetworkTransport();
            var server = CreateServer(config, transport, serverBuffer);
            var clientA = CreateClient(config, transport, null, peerId: "client-a");
            var clientB = CreateClient(config, transport, null, peerId: "client-b", commandBuffer: remoteBuffer);

            clientA.Connect(RuntimeFrame.Zero);
            clientB.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            clientA.ProcessIncoming(RuntimeFrame.Zero);
            clientB.ProcessIncoming(RuntimeFrame.Zero);
            remoteBuffer.DrainForFrame(new RuntimeFrame(4));

            clientA.SubmitCommands(
                new RuntimeFrame(1),
                new[] { new RuntimeCommand(new RuntimeFrame(1), 1, 10, 100, traceId: TestTraceIds.Move) });
            server.ProcessIncoming(new RuntimeFrame(1));
            clientB.ProcessIncoming(new RuntimeFrame(1));

            IReadOnlyList<RuntimeCommand> earlyRemoteCommands = remoteBuffer.DrainForFrame(new RuntimeFrame(6));
            Assert.AreEqual(0, earlyRemoteCommands.Count);

            IReadOnlyList<RuntimeCommand> remoteCommands = remoteBuffer.DrainForFrame(new RuntimeFrame(7));
            Assert.AreEqual(1, remoteCommands.Count);
            Assert.AreEqual(TestTraceIds.Move, remoteCommands[0].TraceId);
            Assert.AreEqual(new RuntimeFrame(7), remoteCommands[0].Frame);
            Assert.AreEqual(0, clientB.AuthoritativeCommandLateCount);
            Assert.AreEqual(1, clientB.AuthoritativeCommandEnqueuedCount);
            Assert.AreEqual(0, clientB.SnapshotRequestSentCount);
        }

        [Test]
        public void RuntimeCommands_OwnAuthoritativeEchoDoesNotEnqueueDuplicateCommand()
        {
            var serverBuffer = new RuntimeCommandBuffer();
            var localBuffer = new RuntimeCommandBuffer();
            var config = new NetworkSessionConfig(1, 60, 2, 2, "config", "resources");
            var transport = new LoopbackNetworkTransport();
            var server = CreateServer(config, transport, serverBuffer);
            var client = CreateClient(config, transport, null, peerId: "client", commandBuffer: localBuffer);

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);

            localBuffer.Enqueue(new RuntimeCommand(new RuntimeFrame(3), 1, 10, 100, traceId: TestTraceIds.Predicted));
            client.SubmitCommands(
                new RuntimeFrame(1),
                new[] { new RuntimeCommand(new RuntimeFrame(1), 1, 10, 100, traceId: TestTraceIds.Submitted) });
            server.ProcessIncoming(new RuntimeFrame(1));
            client.ProcessIncoming(new RuntimeFrame(1));

            IReadOnlyList<RuntimeCommand> commands = localBuffer.DrainForFrame(new RuntimeFrame(3));
            Assert.AreEqual(1, commands.Count);
            Assert.AreEqual(TestTraceIds.Predicted, commands[0].TraceId);
            Assert.AreEqual(1, client.AuthoritativeCommandEchoCount);
            Assert.AreEqual(0, client.AuthoritativeCommandEnqueuedCount);
            Assert.AreEqual(new RuntimeFrame(1), client.LastAckedInputFrame);
        }

        [Test]
        public void RuntimeCommands_LateAuthoritativeCommandIsDroppedWithoutSnapshotStorm()
        {
            var serverBuffer = new RuntimeCommandBuffer();
            var remoteBuffer = new RuntimeCommandBuffer();
            var config = new NetworkSessionConfig(1, 60, 0, 2, "config", "resources");
            var transport = new LoopbackNetworkTransport();
            var server = CreateServer(config, transport, serverBuffer);
            var clientA = CreateClient(config, transport, null, peerId: "client-a");
            var clientB = CreateClient(config, transport, null, peerId: "client-b", commandBuffer: remoteBuffer);

            clientA.Connect(RuntimeFrame.Zero);
            clientB.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            clientA.ProcessIncoming(RuntimeFrame.Zero);
            clientB.ProcessIncoming(RuntimeFrame.Zero);
            remoteBuffer.DrainForFrame(new RuntimeFrame(10));

            clientA.SubmitCommands(
                new RuntimeFrame(1),
                new[] { new RuntimeCommand(new RuntimeFrame(1), 1, 10, 100, traceId: TestTraceIds.Late) });
            server.ProcessIncoming(new RuntimeFrame(1));
            clientB.ProcessIncoming(new RuntimeFrame(1));

            Assert.AreEqual(1, clientB.AuthoritativeCommandLateCount);
            Assert.AreEqual(0, clientB.AuthoritativeCommandEnqueuedCount);
            Assert.AreEqual(1, clientB.SnapshotRequestSentCount);
            Assert.AreEqual(NetworkSessionStatus.Resynchronizing, clientB.Status);
        }

        [Test]
        public void RuntimeCommands_AdditionalCommandsWithoutRecoveryHandlerFailClosed()
        {
            var serverBuffer = new RuntimeCommandBuffer();
            var remoteBuffer = new RuntimeCommandBuffer();
            var config = new NetworkSessionConfig(1, 60, 0, 2, "config", "resources");
            var transport = new LoopbackNetworkTransport();
            var server = CreateServer(config, transport, serverBuffer);
            var clientA = CreateClient(config, transport, null, peerId: "client-a");
            var clientB = CreateClient(config, transport, null, peerId: "client-b", commandBuffer: remoteBuffer);

            clientA.Connect(RuntimeFrame.Zero);
            clientB.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            clientA.ProcessIncoming(RuntimeFrame.Zero);
            clientB.ProcessIncoming(RuntimeFrame.Zero);
            remoteBuffer.DrainForFrame(new RuntimeFrame(10));

            clientA.SubmitCommands(
                new RuntimeFrame(1),
                new[] { new RuntimeCommand(new RuntimeFrame(1), 1, 10, 100, traceId: TestTraceIds.Late1) });
            server.ProcessIncoming(new RuntimeFrame(1));
            clientB.ProcessIncoming(new RuntimeFrame(1));

            clientA.SubmitCommands(
                new RuntimeFrame(2),
                new[] { new RuntimeCommand(new RuntimeFrame(2), 1, 10, 100, traceId: TestTraceIds.Late2) });
            server.ProcessIncoming(new RuntimeFrame(2));
            clientB.ProcessIncoming(new RuntimeFrame(2));

            Assert.AreEqual(1, clientB.AuthoritativeCommandLateCount);
            Assert.AreEqual(0, clientB.AuthoritativeCommandEnqueuedCount);
            Assert.AreEqual(1, clientB.SnapshotRequestSentCount);
            Assert.AreEqual(NetworkSessionStatus.RecoveryFailed, clientB.Status);
        }

        [Test]
        public void RuntimeCommands_CommandAuthorizerRejectsWrongPeerTargetWithoutAck()
        {
            var buffer = new RuntimeCommandBuffer();
            var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
            var transport = new LoopbackNetworkTransport();
            var server = CreateServer(
                config,
                transport,
                buffer,
                commandAuthorizer: (peerId, command) => command.TargetId == 1
                    ? NetworkCommandAuthorizationResult.Accept()
                    : NetworkCommandAuthorizationResult.Reject("target not owned by " + peerId));
            var client = CreateClient(config, transport, null);

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);

            client.SubmitCommands(
                new RuntimeFrame(1),
                new[] { new RuntimeCommand(new RuntimeFrame(1), 1, 10, 2, traceId: TestTraceIds.WrongTarget) });
            server.ProcessIncoming(new RuntimeFrame(1));

            Assert.AreEqual(0, buffer.DrainForFrame(new RuntimeFrame(1)).Count);
            Assert.AreEqual(RuntimeFrame.Zero, server.GetLastAckedInputFrame("client"));
            Assert.AreEqual(1, server.LastCommandErrors.Count);
            StringAssert.Contains("target not owned", server.LastCommandErrors[0].Message);

            client.SubmitCommands(
                new RuntimeFrame(2),
                new[] { new RuntimeCommand(new RuntimeFrame(2), 1, 10, 1, traceId: TestTraceIds.OwnedTarget) });
            server.ProcessIncoming(new RuntimeFrame(2));

            IReadOnlyList<RuntimeCommand> accepted = buffer.DrainForFrame(new RuntimeFrame(6));
            Assert.AreEqual(1, accepted.Count);
            Assert.AreEqual(TestTraceIds.OwnedTarget, accepted[0].TraceId);
            Assert.AreEqual(new RuntimeFrame(2), server.GetLastAckedInputFrame("client"));
        }

        [Test]
        public void Snapshot_FullSnapshotCaptureCanUseReceiverPeerId()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 1, 1, "config", "resources");
            var serverState = new PeerRecordingSnapshotParticipant("mx.test");
            var server = CreateServer(config, transport, null, serverState, frame => 99);
            var clientA = CreateClient(config, transport, null, peerId: "client-a");
            var clientB = CreateClient(config, transport, null, peerId: "client-b");

            clientA.Connect(RuntimeFrame.Zero);
            clientB.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            clientA.ProcessIncoming(RuntimeFrame.Zero);
            clientB.ProcessIncoming(RuntimeFrame.Zero);

            server.BroadcastSnapshot(new RuntimeFrame(4), fullSnapshot: true);

            var messagesA = new List<NetworkMessageEnvelope>();
            var messagesB = new List<NetworkMessageEnvelope>();
            transport.Poll("client-a", new RuntimeFrame(4), messagesA);
            transport.Poll("client-b", new RuntimeFrame(4), messagesB);

            Assert.AreEqual(1, messagesA.Count);
            Assert.AreEqual(1, messagesB.Count);
            var packetA = (NetworkSnapshotPacket)messagesA[0].Payload;
            var packetB = (NetworkSnapshotPacket)messagesB[0].Payload;
            Assert.AreEqual("client-a", packetA.ModuleSnapshots[0].PayloadJson);
            Assert.AreEqual("client-b", packetB.ModuleSnapshots[0].PayloadJson);
        }

        [Test]
        public void Snapshot_HashMismatchAppliesParticipantAndTracksResync()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 1, 1, "config", "resources");
            var serverState = new RecordingSnapshotParticipant("mx.test", "server");
            var clientState = new RecordingSnapshotParticipant("mx.test", "client");
            var server = CreateServer(config, transport, null, serverState, frame => 99);
            var client = CreateClient(config, transport, clientState, frame => 1);

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);

            server.BroadcastSnapshot(new RuntimeFrame(4), fullSnapshot: true);
            client.ProcessIncoming(new RuntimeFrame(5));

            Assert.AreEqual("server", clientState.Value);
            Assert.AreEqual(1, client.ResyncCount);
            Assert.AreEqual(99, client.LastSnapshotHash);
            Assert.AreEqual(new RuntimeFrame(4), client.LastServerFrame);
        }

        [Test]
        public void Snapshot_BroadcastUsesUnreliableChannel()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 1, 1, "config", "resources");
            var serverState = new RecordingSnapshotParticipant("mx.test", "server");
            var server = CreateServer(config, transport, null, serverState, frame => 99);
            var client = CreateClient(config, transport, null);

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);

            server.BroadcastSnapshot(new RuntimeFrame(4));

            var messages = new List<NetworkMessageEnvelope>();
            transport.Poll("client", new RuntimeFrame(4), messages);

            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual(NetworkMessageKind.Snapshot, messages[0].Kind);
            Assert.AreEqual(NetworkChannel.ReliableOrdered, messages[0].Channel);
            Assert.IsTrue(((NetworkSnapshotPacket)messages[0].Payload).FullSnapshot);

            server.BroadcastSnapshot(new RuntimeFrame(5), fullSnapshot: false);
            messages.Clear();
            transport.Poll("client", new RuntimeFrame(5), messages);

            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual(NetworkMessageKind.Snapshot, messages[0].Kind);
            Assert.AreEqual(NetworkChannel.Unreliable, messages[0].Channel);
            var digest = (NetworkSnapshotPacket)messages[0].Payload;
            Assert.IsFalse(digest.FullSnapshot);
            Assert.IsTrue(digest.IsDigestOnly);
            Assert.AreEqual(0, digest.ModuleSnapshots.Count);
        }

        [Test]
        public void Snapshot_PendingFullSnapshotReceiverBypassesDigestInterval()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 1, 8, "config", "resources");
            var serverState = new RecordingSnapshotParticipant("mx.test", "server");
            var server = CreateServer(config, transport, null, serverState, frame => 99);
            var client = CreateClient(config, transport, null);

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);

            Assert.IsTrue(server.HasPendingFullSnapshotReceivers);
            server.BroadcastSnapshot(new RuntimeFrame(3), fullSnapshot: false);

            var messages = new List<NetworkMessageEnvelope>();
            transport.Poll("client", new RuntimeFrame(3), messages);

            NetworkMessageEnvelope snapshot = FindSingleMessage(messages, NetworkMessageKind.Snapshot);
            Assert.AreEqual(NetworkChannel.ReliableOrdered, snapshot.Channel);
            Assert.IsTrue(((NetworkSnapshotPacket)snapshot.Payload).FullSnapshot);
            Assert.IsFalse(server.HasPendingFullSnapshotReceivers);
        }

        [Test]
        public void Snapshot_BootstrapOnlyDoesNotBroadcastDigestWithoutPendingFullReceiver()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(
                1,
                60,
                1,
                1,
                "config",
                "resources",
                NetworkStateSyncPolicy.BootstrapOnly);
            var serverState = new RecordingSnapshotParticipant("mx.test", "server");
            var server = CreateServer(config, transport, null, serverState, frame => 99);
            var client = CreateClient(config, transport, null);

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);
            server.BroadcastSnapshot(new RuntimeFrame(1), fullSnapshot: false);

            var messages = new List<NetworkMessageEnvelope>();
            transport.Poll("client", new RuntimeFrame(1), messages);
            Assert.AreEqual(1, messages.Count);
            Assert.IsTrue(((NetworkSnapshotPacket)messages[0].Payload).FullSnapshot);

            server.BroadcastSnapshot(new RuntimeFrame(2), fullSnapshot: false);
            messages.Clear();
            transport.Poll("client", new RuntimeFrame(2), messages);

            Assert.AreEqual(0, messages.Count);
            Assert.AreEqual(0, server.DigestSnapshotSentCount);
            Assert.AreEqual(1, server.FullSnapshotSentCount);
        }

        [Test]
        public void Snapshot_BootstrapOnlySendsPendingFullOnlyToPendingPeer()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(
                1,
                60,
                1,
                1,
                "config",
                "resources",
                NetworkStateSyncPolicy.BootstrapOnly);
            var serverState = new RecordingSnapshotParticipant("mx.test", "server");
            var server = CreateServer(config, transport, null, serverState, frame => 99);
            var clientA = CreateClient(config, transport, null, peerId: "client-a");
            var clientB = CreateClient(config, transport, null, peerId: "client-b");

            clientA.Connect(RuntimeFrame.Zero);
            clientB.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            clientA.ProcessIncoming(RuntimeFrame.Zero);
            clientB.ProcessIncoming(RuntimeFrame.Zero);
            server.BroadcastSnapshot(new RuntimeFrame(1), fullSnapshot: false);

            var messages = new List<NetworkMessageEnvelope>();
            transport.Poll("client-a", new RuntimeFrame(1), messages);
            Assert.AreEqual(1, messages.Count);
            messages.Clear();
            transport.Poll("client-b", new RuntimeFrame(1), messages);
            Assert.AreEqual(1, messages.Count);

            server.RequestFullSnapshotForPeer("client-a");
            server.BroadcastSnapshot(new RuntimeFrame(2), fullSnapshot: false);

            messages.Clear();
            transport.Poll("client-a", new RuntimeFrame(2), messages);
            Assert.AreEqual(1, messages.Count);
            Assert.IsTrue(((NetworkSnapshotPacket)messages[0].Payload).FullSnapshot);

            messages.Clear();
            transport.Poll("client-b", new RuntimeFrame(2), messages);
            Assert.AreEqual(0, messages.Count);
            Assert.AreEqual(0, server.DigestSnapshotSentCount);
            Assert.AreEqual(3, server.FullSnapshotSentCount);
        }

        [Test]
        public void Snapshot_DigestBroadcastCarriesHashAndAckWithoutModuleSnapshots()
        {
            var buffer = new RuntimeCommandBuffer();
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
            var serverState = new RecordingSnapshotParticipant("mx.test", "server");
            var server = CreateServer(config, transport, buffer, serverState, frame => 99);
            var client = CreateClient(config, transport, new RecordingSnapshotParticipant("mx.test", "client"));

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);
            server.BroadcastSnapshot(new RuntimeFrame(1), fullSnapshot: false);
            client.ProcessIncoming(new RuntimeFrame(1));

            client.SubmitCommands(
                new RuntimeFrame(8),
                new[] { new RuntimeCommand(new RuntimeFrame(8), 1, 10, 100, traceId: TestTraceIds.AckMe) });
            server.ProcessIncoming(new RuntimeFrame(8));
            server.BroadcastSnapshot(new RuntimeFrame(10), fullSnapshot: false);

            var messages = new List<NetworkMessageEnvelope>();
            transport.Poll("client", new RuntimeFrame(10), messages);

            NetworkMessageEnvelope snapshot = FindSingleMessage(messages, NetworkMessageKind.Snapshot);
            var packet = (NetworkSnapshotPacket)snapshot.Payload;
            Assert.IsTrue(packet.IsDigestOnly);
            Assert.AreEqual(99, packet.ResultHash);
            Assert.AreEqual(new RuntimeFrame(8), packet.AckedInputFrame);
            Assert.AreEqual(0, packet.ModuleSnapshots.Count);
        }

        [Test]
        public void Snapshot_FullOnlyPolicySuppressesNonFullMismatchApply()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 1, 1, "config", "resources");
            var clientState = new RecordingSnapshotParticipant("mx.test", "client");
            var client = CreateClient(
                config,
                transport,
                clientState,
                frame => 1,
                NetworkSnapshotApplyPolicy.ApplyFullSnapshotOnly);

            transport.Send(CreateSnapshotEnvelope(
                sequence: 0,
                frame: new RuntimeFrame(4),
                hash: 99,
                value: "server",
                fullSnapshot: false));
            client.ProcessIncoming(new RuntimeFrame(5));

            Assert.AreEqual("client", clientState.Value);
            Assert.AreEqual(0, client.ResyncCount);
            Assert.AreEqual(1, client.SnapshotMismatchCount);
            Assert.AreEqual(1, client.SuppressedSnapshotMismatchCount);

            transport.Send(CreateSnapshotEnvelope(
                sequence: 1,
                frame: new RuntimeFrame(5),
                hash: 99,
                value: "server",
                fullSnapshot: true));
            client.ProcessIncoming(new RuntimeFrame(5));

            Assert.AreEqual("server", clientState.Value);
            Assert.AreEqual(1, client.ResyncCount);
        }

        [Test]
        public void SnapshotCorrectionHook_InterceptsFullSnapshotBeforeDefaultApply()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 1, 1, "config", "resources");
            var clientState = new RecordingSnapshotParticipant("mx.test", "client");
            bool hookCalled = false;
            var client = CreateClient(
                config,
                transport,
                clientState,
                frame => 1,
                snapshotCorrectionHandler: delegate(NetworkSnapshotPacket packet, RuntimeFrame receiveFrame, RuntimeFrame localFrame)
                {
                    hookCalled = true;
                    Assert.AreEqual(new RuntimeFrame(4), packet.AuthoritativeFrame);
                    Assert.AreEqual(new RuntimeFrame(8), receiveFrame);
                    Assert.AreEqual(new RuntimeFrame(8), localFrame);
                    return NetworkSnapshotCorrectionResult.HandledApplied("rollback applied");
                });

            transport.Send(CreateSnapshotEnvelope(
                sequence: 0,
                frame: new RuntimeFrame(4),
                hash: 99,
                value: "server",
                fullSnapshot: true));
            client.ProcessIncoming(new RuntimeFrame(8));

            Assert.IsTrue(hookCalled);
            Assert.AreEqual("client", clientState.Value);
            Assert.AreEqual(1, client.ResyncCount);
            Assert.AreEqual(1, client.SnapshotMismatchCount);
            Assert.AreEqual(0, client.SnapshotApplyFailureCount);
        }

        [Test]
        public void Snapshot_HashLookupMissingHistoryRequestsRecoveryWithoutCountingMismatch()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 1, 1, "config", "resources");
            var server = CreateServer(config, transport, null);
            var client = CreateClient(
                config,
                transport,
                null,
                snapshotApplyPolicy: NetworkSnapshotApplyPolicy.ApplyFullSnapshotOnly,
                snapshotHashLookup: frame => NetworkSnapshotHashLookupResult.Missing());

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);
            transport.Send(CreateDigestSnapshotEnvelope(
                sequence: 1,
                frame: new RuntimeFrame(4),
                hash: 99));
            client.ProcessIncoming(new RuntimeFrame(5));

            Assert.AreEqual(0, client.SnapshotMismatchCount);
            Assert.AreEqual(0, client.SuppressedSnapshotMismatchCount);
            Assert.AreEqual(1, client.SnapshotRequestSentCount);
            Assert.AreEqual(NetworkSessionStatus.Resynchronizing, client.Status);
            Assert.AreEqual(1, client.MissingSnapshotHashCount);
            Assert.AreEqual(0, client.ComparableSnapshotHashCount);
        }

        [Test]
        public void Snapshot_ComparableHistoryMismatchRequestsFullSnapshotForPeer()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 1, 1, "config", "resources");
            var serverState = new RecordingSnapshotParticipant("mx.test", "server");
            var clientState = new RecordingSnapshotParticipant("mx.test", "client");
            var server = CreateServer(config, transport, null, serverState, frame => 99);
            var client = CreateClient(
                config,
                transport,
                clientState,
                snapshotApplyPolicy: NetworkSnapshotApplyPolicy.ApplyFullSnapshotOnly,
                snapshotHashLookup: frame => NetworkSnapshotHashLookupResult.Found(frame.Value == 1 ? 99 : 1),
                snapshotRequestMismatchThreshold: 2,
                snapshotRequestCooldownFrames: 60);

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);
            server.BroadcastSnapshot(new RuntimeFrame(1), fullSnapshot: false);
            client.ProcessIncoming(new RuntimeFrame(1));

            server.BroadcastSnapshot(new RuntimeFrame(4), fullSnapshot: false);
            client.ProcessIncoming(new RuntimeFrame(4));
            server.BroadcastSnapshot(new RuntimeFrame(5), fullSnapshot: false);
            client.ProcessIncoming(new RuntimeFrame(5));
            server.ProcessIncoming(new RuntimeFrame(5));
            server.BroadcastSnapshot(new RuntimeFrame(6), fullSnapshot: false);

            var messages = new List<NetworkMessageEnvelope>();
            transport.Poll("client", new RuntimeFrame(6), messages);

            Assert.AreEqual(2, client.SnapshotMismatchCount);
            Assert.AreEqual(3, client.ComparableSnapshotHashCount);
            Assert.AreEqual(1, client.SnapshotRequestSentCount);
            Assert.AreEqual(1, server.SnapshotRequestReceivedCount);
            Assert.AreEqual(1, messages.Count);
            var packet = (NetworkSnapshotPacket)messages[0].Payload;
            Assert.IsTrue(packet.FullSnapshot);
            Assert.AreEqual(1, packet.ModuleSnapshots.Count);
        }

        [Test]
        public void Snapshot_BootstrapOnlyComparableHistoryMismatchDoesNotRequestFullSnapshot()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(
                1,
                60,
                1,
                1,
                "config",
                "resources",
                NetworkStateSyncPolicy.BootstrapOnly);
            var client = CreateClient(
                config,
                transport,
                null,
                snapshotApplyPolicy: NetworkSnapshotApplyPolicy.ApplyFullSnapshotOnly,
                snapshotHashLookup: frame => NetworkSnapshotHashLookupResult.Found(1),
                snapshotRequestMismatchThreshold: 1,
                snapshotRequestCooldownFrames: 60);

            transport.Send(CreateDigestSnapshotEnvelope(
                sequence: 0,
                frame: new RuntimeFrame(4),
                hash: 99));
            client.ProcessIncoming(new RuntimeFrame(4));
            transport.Send(CreateDigestSnapshotEnvelope(
                sequence: 1,
                frame: new RuntimeFrame(5),
                hash: 99));
            client.ProcessIncoming(new RuntimeFrame(5));

            Assert.AreEqual(2, client.SnapshotMismatchCount);
            Assert.AreEqual(2, client.SuppressedSnapshotMismatchCount);
            Assert.AreEqual(0, client.SnapshotRequestSentCount);
        }

        [Test]
        public void Snapshot_RequestCooldownPreventsEveryFrameFullSnapshotRequests()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 1, 1, "config", "resources");
            var server = CreateServer(config, transport, null);
            var client = CreateClient(
                config,
                transport,
                null,
                snapshotApplyPolicy: NetworkSnapshotApplyPolicy.ApplyFullSnapshotOnly,
                snapshotHashLookup: frame => NetworkSnapshotHashLookupResult.Found(1),
                snapshotRequestMismatchThreshold: 1,
                snapshotRequestCooldownFrames: 60);

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);
            transport.Send(CreateDigestSnapshotEnvelope(
                sequence: 2,
                frame: new RuntimeFrame(4),
                hash: 99));
            client.ProcessIncoming(new RuntimeFrame(4));
            transport.Send(CreateDigestSnapshotEnvelope(
                sequence: 3,
                frame: new RuntimeFrame(5),
                hash: 99));
            client.ProcessIncoming(new RuntimeFrame(5));

            Assert.AreEqual(2, client.SnapshotMismatchCount);
            Assert.AreEqual(1, client.SnapshotRequestSentCount);
        }

        [Test]
        public void Snapshot_TracksIntervalAndLocalFrameDrift()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 1, 1, "config", "resources");
            var client = CreateClient(config, transport, null);

            transport.Send(CreateSnapshotEnvelope(
                sequence: 0,
                frame: new RuntimeFrame(4),
                hash: 4,
                value: "server"));
            client.ProcessIncoming(new RuntimeFrame(6));

            Assert.AreEqual(2, client.LastFrameDriftFrames);
            Assert.AreEqual(2, client.AverageFrameDriftFrames);
            Assert.AreEqual(2, client.MaxFrameDriftFrames);
            Assert.AreEqual(0, client.AverageSnapshotIntervalFrames);

            transport.Send(CreateSnapshotEnvelope(
                sequence: 1,
                frame: new RuntimeFrame(8),
                hash: 8,
                value: "server"));
            client.ProcessIncoming(new RuntimeFrame(12));

            Assert.AreEqual(4, client.AverageSnapshotIntervalFrames);
            Assert.AreEqual(4, client.MaxSnapshotIntervalFrames);
            Assert.AreEqual(4, client.LastFrameDriftFrames);
            Assert.AreEqual(3, client.AverageFrameDriftFrames);
            Assert.AreEqual(4, client.MaxFrameDriftFrames);
        }

        [Test]
        public void Snapshot_PerPeerAckDoesNotBleedAcrossReceivers()
        {
            var buffer = new RuntimeCommandBuffer();
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
            var server = CreateServer(config, transport, buffer);
            var clientA = CreateClient(config, transport, null, peerId: "client-a");
            var clientB = CreateClient(config, transport, null, peerId: "client-b");

            clientA.Connect(RuntimeFrame.Zero);
            clientB.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            clientA.ProcessIncoming(RuntimeFrame.Zero);
            clientB.ProcessIncoming(RuntimeFrame.Zero);

            clientA.SubmitCommands(
                new RuntimeFrame(10),
                new[] { new RuntimeCommand(new RuntimeFrame(10), 1, 10, 100, traceId: TestTraceIds.ClientACommand) });
            server.ProcessIncoming(new RuntimeFrame(10));

            Assert.AreEqual(new RuntimeFrame(10), server.GetLastAckedInputFrame("client-a"));
            Assert.AreEqual(RuntimeFrame.Zero, server.GetLastAckedInputFrame("client-b"));
            Assert.AreEqual(new RuntimeFrame(10), server.LastAckedInputFrame);

            server.BroadcastSnapshot(new RuntimeFrame(12), fullSnapshot: false);

            var messagesA = new List<NetworkMessageEnvelope>();
            var messagesB = new List<NetworkMessageEnvelope>();
            transport.Poll("client-a", new RuntimeFrame(12), messagesA);
            transport.Poll("client-b", new RuntimeFrame(12), messagesB);

            NetworkMessageEnvelope snapshotA = FindSingleMessage(messagesA, NetworkMessageKind.Snapshot);
            NetworkMessageEnvelope snapshotB = FindSingleMessage(messagesB, NetworkMessageKind.Snapshot);
            var packetA = (NetworkSnapshotPacket)snapshotA.Payload;
            var packetB = (NetworkSnapshotPacket)snapshotB.Payload;
            Assert.AreEqual(new RuntimeFrame(10), packetA.AckedInputFrame);
            Assert.AreEqual(RuntimeFrame.Zero, packetB.AckedInputFrame);
        }

        [Test]
        public void Snapshot_OlderAuthoritativeFrameDoesNotOverwriteNewerState()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 1, 1, "config", "resources");
            var clientState = new RecordingSnapshotParticipant("mx.test", "initial");
            var client = CreateClient(config, transport, clientState, frame => 0);

            transport.Send(CreateSnapshotEnvelope(
                sequence: 0,
                frame: new RuntimeFrame(10),
                hash: 10,
                value: "new"));
            client.ProcessIncoming(new RuntimeFrame(10));

            Assert.AreEqual("new", clientState.Value);
            Assert.AreEqual(new RuntimeFrame(10), client.LastServerFrame);
            Assert.AreEqual(10, client.LastSnapshotHash);

            transport.Send(CreateSnapshotEnvelope(
                sequence: 1,
                frame: new RuntimeFrame(9),
                hash: 9,
                value: "old"));
            client.ProcessIncoming(new RuntimeFrame(11));

            Assert.AreEqual("new", clientState.Value);
            Assert.AreEqual(new RuntimeFrame(10), client.LastServerFrame);
            Assert.AreEqual(10, client.LastSnapshotHash);
        }

        [Test]
        public void Snapshot_NewerThanLastSnapshotIsProcessedAfterServerClockAdvances()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 1, 1, "config", "resources");
            var clientState = new RecordingSnapshotParticipant("mx.test", "initial");
            var client = CreateClient(config, transport, clientState, frame => 0);

            transport.Send(CreateSnapshotEnvelope(
                sequence: 0,
                frame: new RuntimeFrame(10),
                hash: 10,
                value: "frame-10"));
            client.ProcessIncoming(new RuntimeFrame(10));

            transport.Send(CreatePongEnvelope(
                sequence: 1,
                sentFrame: RuntimeFrame.Zero,
                receiveFrame: new RuntimeFrame(12)));
            client.ProcessIncoming(new RuntimeFrame(12));
            Assert.AreEqual(new RuntimeFrame(12), client.LastServerFrame);

            transport.Send(CreateSnapshotEnvelope(
                sequence: 2,
                frame: new RuntimeFrame(11),
                hash: 11,
                value: "frame-11"));
            client.ProcessIncoming(new RuntimeFrame(13));

            Assert.AreEqual("frame-11", clientState.Value);
            Assert.AreEqual(new RuntimeFrame(12), client.LastServerFrame);
            Assert.AreEqual(11, client.LastSnapshotHash);
            Assert.AreEqual(2, client.FullSnapshotReceivedCount);
        }

        [Test]
        public void LoopbackTransport_ReportsDropAndReorderDiagnostics()
        {
            var transport = new LoopbackNetworkTransport(new LoopbackNetworkTransportOptions(
                latencyFrames: 2,
                jitterFrames: 0,
                dropRate: 0.5d,
                reorderMessages: true,
                randomSeed: 1));

            for (int i = 0; i < 10; i++)
            {
                transport.Send(new NetworkMessageEnvelope(
                    "s",
                    "a",
                    "b",
                    i,
                    NetworkChannel.ReliableOrdered,
                    NetworkMessageKind.Snapshot,
                    RuntimeFrame.Zero,
                    new object()));
            }

            var messages = new List<NetworkMessageEnvelope>();
            transport.Poll("b", new RuntimeFrame(2), messages);
            NetworkTransportSnapshot snapshot = transport.CreateSnapshot("b");

            Assert.Greater(snapshot.DroppedCount, 0);
            Assert.Greater(snapshot.ReorderedCount, 0);
            Assert.AreEqual(10, snapshot.SentCount);
            Assert.AreEqual(messages.Count, snapshot.DeliveredCount);
        }

        [Test]
        public void LoopbackTransport_ReordersUnreliableButKeepsReliableOrdered()
        {
            var transport = new LoopbackNetworkTransport(new LoopbackNetworkTransportOptions(
                latencyFrames: 0,
                reorderMessages: true));
            transport.Send(CreateEnvelope(0, NetworkChannel.ReliableOrdered));
            transport.Send(CreateEnvelope(1, NetworkChannel.ReliableOrdered));

            var messages = new List<NetworkMessageEnvelope>();
            transport.Poll("b", RuntimeFrame.Zero, messages);

            Assert.AreEqual(2, messages.Count);
            Assert.AreEqual(0, messages[0].Sequence);
            Assert.AreEqual(1, messages[1].Sequence);

            transport.Clear();
            transport.Send(CreateEnvelope(0, NetworkChannel.Unreliable));
            transport.Send(CreateEnvelope(1, NetworkChannel.Unreliable));

            messages.Clear();
            transport.Poll("b", RuntimeFrame.Zero, messages);

            Assert.AreEqual(2, messages.Count);
            Assert.AreEqual(1, messages[0].Sequence);
            Assert.AreEqual(0, messages[1].Sequence);
        }

        [Test]
        public void CodecLoopbackTransport_RunsHandshakeCommandAndSnapshot()
        {
            var buffer = new RuntimeCommandBuffer();
            var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
            var transport = new CodecLoopbackNetworkTransport(new JsonNetworkMessageCodec());
            var serverState = new RecordingSnapshotParticipant("mx.test", "server");
            var clientState = new RecordingSnapshotParticipant("mx.test", "client");
            var server = CreateServer(config, transport, buffer, serverState, frame => 7);
            var client = CreateClient(config, transport, clientState, frame => 1);

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);

            Assert.AreEqual(NetworkSessionStatus.Connected, client.Status);

            client.SubmitCommands(
                new RuntimeFrame(1),
                new[] { new RuntimeCommand(new RuntimeFrame(1), 10, 20, 30, traceId: TestTraceIds.CodecCommand) });
            server.ProcessIncoming(new RuntimeFrame(1));

            IReadOnlyList<RuntimeCommand> commands = buffer.DrainForFrame(new RuntimeFrame(5));
            Assert.AreEqual(1, commands.Count);
            Assert.AreEqual(TestTraceIds.CodecCommand, commands[0].TraceId);

            server.BroadcastSnapshot(new RuntimeFrame(2));
            client.ProcessIncoming(new RuntimeFrame(2));

            Assert.AreEqual("server", clientState.Value);
            Assert.AreEqual(7, client.LastSnapshotHash);
            NetworkTransportSnapshot transportSnapshot = transport.CreateSnapshot("client");
            Assert.AreEqual(0, transportSnapshot.CodecErrorCount);
            Assert.Greater(transportSnapshot.SentBytes, 0);
            Assert.Greater(transportSnapshot.DeliveredBytes, 0);
            Assert.Greater(transportSnapshot.SnapshotSentBytes, 0);
            Assert.Greater(transportSnapshot.RuntimeCommandSentBytes, 0);
            Assert.Greater(transportSnapshot.MaxSentPayloadBytes, 0);
        }

        [Test]
        public void ReliableOrderedDuplicate_DoesNotExecuteCommandTwice()
        {
            var buffer = new RuntimeCommandBuffer();
            var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
            var transport = new LoopbackNetworkTransport();
            var server = CreateServer(config, transport, buffer);
            var packet = new NetworkRuntimeCommandPacket(
                "client",
                new RuntimeFrame(1),
                new[] { new RuntimeCommand(new RuntimeFrame(1), 1, 10, 100, traceId: TestTraceIds.Once) });
            var envelope = new NetworkMessageEnvelope(
                "session",
                "client",
                "server",
                0,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.RuntimeCommands,
                new RuntimeFrame(1),
                packet);

            transport.Send(envelope);
            transport.Send(envelope);
            server.ProcessIncoming(new RuntimeFrame(1));

            IReadOnlyList<RuntimeCommand> commands = buffer.DrainForFrame(new RuntimeFrame(5));
            Assert.AreEqual(1, commands.Count);
            Assert.AreEqual(TestTraceIds.Once, commands[0].TraceId);
            Assert.AreEqual(1, server.DuplicateMessageCount);
            Assert.AreEqual(0, server.OutOfOrderMessageCount);
        }

        [Test]
        public void TransportEvents_DisconnectAndTimeoutUpdateSessionStatus()
        {
            var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
            var transport = new LoopbackNetworkTransport();
            var client = CreateClient(config, transport, null);

            transport.QueueEvent(new NetworkTransportEvent(
                NetworkTransportEventKind.Disconnected,
                "client",
                new RuntimeFrame(3),
                "socket closed"));
            client.ProcessIncoming(new RuntimeFrame(3));

            Assert.AreEqual(NetworkSessionStatus.Disconnected, client.Status);
            StringAssert.Contains("socket closed", client.DisconnectReason);

            client.Connect(new RuntimeFrame(4));
            transport.QueueEvent(new NetworkTransportEvent(
                NetworkTransportEventKind.Timeout,
                "client",
                new RuntimeFrame(5),
                "heartbeat missed"));
            client.ProcessIncoming(new RuntimeFrame(5));

            Assert.AreEqual(NetworkSessionStatus.Disconnected, client.Status);
            StringAssert.Contains("heartbeat missed", client.DisconnectReason);
        }

        [Test]
        public void Heartbeat_PingPongUpdatesRttEstimateFrames()
        {
            var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
            var transport = new LoopbackNetworkTransport(new LoopbackNetworkTransportOptions(latencyFrames: 2));
            var server = CreateServer(config, transport, null);
            var client = CreateClient(config, transport, null);

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(new RuntimeFrame(2));
            client.ProcessIncoming(new RuntimeFrame(4));

            Assert.AreEqual(NetworkSessionStatus.Connected, client.Status);

            client.SendPing(new RuntimeFrame(5));
            server.ProcessIncoming(new RuntimeFrame(7));
            client.ProcessIncoming(new RuntimeFrame(9));

            Assert.AreEqual(4, client.RttEstimateFrames);
            Assert.AreEqual(4, client.AverageRttFrames);
            Assert.AreEqual(4, client.MaxRttFrames);
            Assert.AreEqual(new RuntimeFrame(7), client.LastServerFrame);
        }

        [Test]
        public void Heartbeat_UsesUnreliableChannel()
        {
            var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
            var transport = new LoopbackNetworkTransport();
            var server = CreateServer(config, transport, null);
            var client = CreateClient(config, transport, null);

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);

            client.SendPing(new RuntimeFrame(5));
            var messages = new List<NetworkMessageEnvelope>();
            transport.Poll("server", new RuntimeFrame(5), messages);

            NetworkMessageEnvelope ping = FindSingleMessage(messages, NetworkMessageKind.Ping);
            Assert.AreEqual(NetworkChannel.Unreliable, ping.Channel);

            client.SendPing(new RuntimeFrame(6));
            server.ProcessIncoming(new RuntimeFrame(6));
            messages.Clear();
            transport.Poll("client", new RuntimeFrame(6), messages);

            NetworkMessageEnvelope pong = FindSingleMessage(messages, NetworkMessageKind.Pong);
            Assert.AreEqual(NetworkChannel.Unreliable, pong.Channel);
        }

        [Test]
        public void Heartbeat_RttEstimateUsesSmoothedWindow()
        {
            var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
            var transport = new LoopbackNetworkTransport();
            var client = CreateClient(config, transport, null);

            transport.Send(CreatePongEnvelope(
                sequence: 0,
                sentFrame: new RuntimeFrame(5),
                receiveFrame: new RuntimeFrame(9)));
            client.ProcessIncoming(new RuntimeFrame(9));

            Assert.AreEqual(4, client.RttEstimateFrames);
            Assert.AreEqual(4, client.AverageRttFrames);
            Assert.AreEqual(4, client.MaxRttFrames);

            transport.Send(CreatePongEnvelope(
                sequence: 1,
                sentFrame: new RuntimeFrame(10),
                receiveFrame: new RuntimeFrame(18)));
            client.ProcessIncoming(new RuntimeFrame(18));

            Assert.AreEqual(6, client.RttEstimateFrames);
            Assert.AreEqual(6, client.AverageRttFrames);
            Assert.AreEqual(8, client.MaxRttFrames);
        }

        [Test]
        public void BoundedRunnerRejectsAuthenticatedSessionReceiverAndDirectionMismatchBeforeIngress()
        {
            var config = new NetworkSessionConfig(2, 60, 0, 1, "config", "resources");
            var transport = new CodecLoopbackNetworkTransport();
            transport.ConfigureBounded(4, 256);
            var ingress = new CapturingBoundedIngress();
            NetworkSessionRunner server = CreateServer(config, transport, null);
            server.Connect(RuntimeFrame.Zero);
            server.ConfigureBoundedProtocol(55, 1, 99, ingress, 256);

            byte[] matchingHeader = EncodeBoundedPacket(55, 7, 1);
            byte[] wrongReceiverHeader = EncodeBoundedPacket(55, 7, 2);
            var wrongSession = new NetworkBoundedAuthenticatedMetadata(
                56, 7, 1, NetworkBoundedPacketDirection.ClientToServer);
            var receiverMetadata = new NetworkBoundedAuthenticatedMetadata(
                55, 7, 1, NetworkBoundedPacketDirection.ClientToServer);
            var wrongDirection = new NetworkBoundedAuthenticatedMetadata(
                55, 7, 1, NetworkBoundedPacketDirection.ServerToClient);
            Assert.IsTrue(transport.TrySendBounded(matchingHeader, wrongSession, RuntimeFrame.Zero).Success);
            Assert.IsTrue(transport.TrySendBounded(wrongReceiverHeader, receiverMetadata, RuntimeFrame.Zero).Success);
            Assert.IsTrue(transport.TrySendBounded(matchingHeader, wrongDirection, RuntimeFrame.Zero).Success);

            server.ProcessIncoming(RuntimeFrame.Zero);
            Assert.AreEqual(0, ingress.AdmitCount);
            Assert.AreEqual(0, server.BoundedPacketReceivedCount);
            Assert.AreEqual(3, server.BoundedPacketRejectedCount);
        }

        [Test]
        public void BoundedRunnerPassesAuthenticatedRemoteToIngressAfterExactHeaderMatch()
        {
            var config = new NetworkSessionConfig(2, 60, 0, 1, "config", "resources");
            var transport = new CodecLoopbackNetworkTransport();
            transport.ConfigureBounded(1, 256);
            var ingress = new CapturingBoundedIngress();
            NetworkSessionRunner server = CreateServer(config, transport, null);
            server.Connect(RuntimeFrame.Zero);
            server.ConfigureBoundedProtocol(55, 1, 99, ingress, 256);
            byte[] packet = EncodeBoundedPacket(55, 7, 1);
            var authenticated = new NetworkBoundedAuthenticatedMetadata(
                55, 7, 1, NetworkBoundedPacketDirection.ClientToServer);
            Assert.IsTrue(transport.TrySendBounded(packet, authenticated, RuntimeFrame.Zero).Success);

            server.ProcessIncoming(RuntimeFrame.Zero);
            Assert.AreEqual(1, ingress.AdmitCount);
            Assert.AreEqual(7U, ingress.LastContext.AuthenticatedRemotePeerHandle);
            Assert.AreEqual(NetworkBoundedPacketDirection.ClientToServer, ingress.LastContext.AuthenticatedDirection);
            Assert.AreEqual(1, server.BoundedPacketReceivedCount);
            Assert.AreEqual(0, server.BoundedPacketRejectedCount);
        }

        [Test]
        public void BoundedRunnerRejectsExactPacketWhileDisconnectedOrConnectingBeforeIngress()
        {
            var config = new NetworkSessionConfig(2, 60, 0, 1, "config", "resources");

            var disconnectedTransport = new CodecLoopbackNetworkTransport();
            disconnectedTransport.ConfigureBounded(1, 256);
            var disconnectedIngress = new CapturingBoundedIngress();
            NetworkSessionRunner disconnectedClient = CreateClient(config, disconnectedTransport, null);
            disconnectedClient.ConfigureBoundedProtocol(55, 2, 99, disconnectedIngress, 256);
            byte[] disconnectedPacket = EncodeBoundedPacket(
                55,
                1,
                2,
                NetworkMessageKind.BoundedStateSnapshot);
            var disconnectedMetadata = new NetworkBoundedAuthenticatedMetadata(
                55, 1, 2, NetworkBoundedPacketDirection.ServerToClient);
            Assert.IsTrue(disconnectedTransport.TrySendBounded(
                disconnectedPacket,
                disconnectedMetadata,
                RuntimeFrame.Zero).Success);

            disconnectedClient.ProcessIncoming(RuntimeFrame.Zero);
            Assert.AreEqual(NetworkSessionStatus.Disconnected, disconnectedClient.Status);
            Assert.AreEqual(0, disconnectedIngress.AdmitCount);
            Assert.AreEqual(1, disconnectedClient.BoundedPacketRejectedCount);

            var connectingTransport = new CodecLoopbackNetworkTransport();
            connectingTransport.ConfigureBounded(1, 256);
            var connectingIngress = new CapturingBoundedIngress();
            NetworkSessionRunner connectingClient = CreateClient(config, connectingTransport, null);
            connectingClient.ConfigureBoundedProtocol(55, 2, 99, connectingIngress, 256);
            connectingClient.Connect(RuntimeFrame.Zero);
            byte[] serverPacket = EncodeBoundedPacket(
                55,
                1,
                2,
                NetworkMessageKind.BoundedStateSnapshot);
            var authenticatedServer = new NetworkBoundedAuthenticatedMetadata(
                55, 1, 2, NetworkBoundedPacketDirection.ServerToClient);
            Assert.IsTrue(connectingTransport.TrySendBounded(
                serverPacket,
                authenticatedServer,
                RuntimeFrame.Zero).Success);

            connectingClient.ProcessIncoming(RuntimeFrame.Zero);
            Assert.AreEqual(NetworkSessionStatus.Connecting, connectingClient.Status);
            Assert.AreEqual(0, connectingIngress.AdmitCount);
            Assert.AreEqual(1, connectingClient.BoundedPacketRejectedCount);
        }

        private static byte[] EncodeBoundedPacket(
            ulong sessionHandle,
            uint senderPeerHandle,
            uint receiverPeerHandle,
            NetworkMessageKind kind = NetworkMessageKind.BoundedRuntimeCommands)
        {
            byte[] payload = { 1 };
            var packet = new byte[NetworkBoundedWire.HeaderBytes + payload.Length];
            var header = new NetworkBoundedPacketHeader(
                kind,
                sessionHandle,
                senderPeerHandle,
                receiverPeerHandle,
                0,
                0,
                payload.Length);
            Assert.IsTrue(NetworkBoundedWire.TryEncode(header, payload, packet, out _).Success);
            return packet;
        }

        private sealed class CapturingBoundedIngress : INetworkBoundedPacketIngress
        {
            public int AdmitCount { get; private set; }
            public NetworkBoundedIngressContext LastContext { get; private set; }

            public NetworkBoundedResult TryAdmit(
                System.ReadOnlySpan<byte> packet,
                in NetworkBoundedIngressContext context)
            {
                AdmitCount++;
                LastContext = context;
                return NetworkBoundedResult.Succeeded();
            }
        }

        [Test]
        public void DeterminismRegression_MissingRollbackHistoryMustResyncOrFailStopBeforeContinuing()
        {
            var buffer = new RuntimeCommandBuffer();
            var config = new NetworkSessionConfig(1, 60, 0, 2, "config", "resources", NetworkStateSyncPolicy.BootstrapOnly);
            var transport = new LoopbackNetworkTransport();
            var state = new RollbackCounterState();
            var rollback = new NetworkPredictionRollbackCoordinator(historyCapacity: 2);
            rollback.RegisterParticipant(new CounterRollbackParticipant(state));
            rollback.RecordFrame(RuntimeFrame.Zero);
            rollback.RecordFrame(new RuntimeFrame(1));
            rollback.RecordFrame(new RuntimeFrame(2));
            bool missingHistoryObserved = false;
            var server = CreateServer(config, transport, new RuntimeCommandBuffer());
            var client = CreateClient(config, transport, null, commandBuffer: buffer,
                authoritativeCommandReceiveHandler: (packet, receiveFrame) =>
                {
                    NetworkPredictionRollbackResult correction = rollback.ReconcileAuthoritativeCommands(
                        packet.Commands, receiveFrame,
                        delegate(RuntimeCommand command, out string failure) { failure = ""; return true; },
                        delegate(RuntimeFrame frame, out string failure) { failure = ""; return true; });
                    missingHistoryObserved = !correction.Success && correction.Message.Contains("rollback history missing");
                    return correction.Success;
                });
            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);
            Assert.AreEqual(NetworkSessionStatus.Connected, client.Status);
            buffer.DrainForFrame(new RuntimeFrame(10));
            transport.Send(new NetworkMessageEnvelope("session", "server", "client", 1,
                NetworkChannel.ReliableOrdered, NetworkMessageKind.AuthoritativeCommands, new RuntimeFrame(12),
                new NetworkAuthoritativeCommandPacket("remote", new RuntimeFrame(12), RuntimeFrame.Zero,
                    new[] { new RuntimeCommand(RuntimeFrame.Zero, 1, 10, 100, payload0: 5, traceId: TestTraceIds.Wrapped) })));
            client.ProcessIncoming(new RuntimeFrame(12));
            Assert.IsTrue(missingHistoryObserved, "The fixture must reach production rollback history exhaustion.");
            Assert.AreEqual(1, client.AuthoritativeCommandLateCount);
            Assert.That(client.SnapshotRequestSentCount > 0 || client.Status != NetworkSessionStatus.Connected,
                Is.True, "first divergence receiveFrame=12 commandFrame=0 field=recovery.contract expected=resync-or-fail-stop " +
                "actual=history-failed, late-command-discarded, session-connected, requests=" + client.SnapshotRequestSentCount +
                ". A failed correction must stop authority admission.");
        }

        [Test]
        public void Recovery_MissingHistoryRetriesAndAppliesCompleteStateOnce()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
            var state = new RecordingSnapshotParticipant("mx.test", "client");
            var server = CreateServer(config, transport, null, new RecordingSnapshotParticipant("mx.test", "restored"));
            var client = CreateClient(config, transport, state,
                snapshotHashLookup: frame => NetworkSnapshotHashLookupResult.Missing(), snapshotRequestCooldownFrames: 3);
            client.Connect(RuntimeFrame.Zero); server.ProcessIncoming(RuntimeFrame.Zero); client.ProcessIncoming(RuntimeFrame.Zero);
            transport.Send(CreateDigestSnapshotEnvelope(10, new RuntimeFrame(4), 99));
            client.ProcessIncoming(new RuntimeFrame(5));
            Assert.AreEqual(NetworkSessionStatus.Resynchronizing, client.Status);
            Assert.IsFalse(client.IsAuthorityReady);
            client.ProcessIncoming(new RuntimeFrame(6));
            Assert.AreEqual(1, client.SnapshotRequestSentCount);
            client.ProcessIncoming(new RuntimeFrame(8));
            Assert.AreEqual(2, client.SnapshotRequestSentCount);
            server.ProcessIncoming(new RuntimeFrame(8));
            // Use a monotonically newer envelope because this fixture inserted sequence 10 above.
            transport.Send(CreateSnapshotEnvelope(11, new RuntimeFrame(8), 99, "restored"));
            client.ProcessIncoming(new RuntimeFrame(8));
            Assert.AreEqual(NetworkSessionStatus.Connected, client.Status);
            Assert.AreEqual(1, state.ApplyCount);
            Assert.AreEqual(1, client.ResyncCount);
            transport.Send(CreateSnapshotEnvelope(12, new RuntimeFrame(8), 99, "restored"));
            transport.Send(CreateSnapshotEnvelope(13, new RuntimeFrame(7), 99, "old"));
            client.ProcessIncoming(new RuntimeFrame(9));
            Assert.AreEqual("restored", state.Value);
            Assert.AreEqual(1, state.ApplyCount);
            Assert.AreEqual(1, client.ResyncCount);
        }

        [Test]
        public void Recovery_IncompleteFullStateNeverReportsSuccessOrPublishes()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
            var state = new RecordingSnapshotParticipant("mx.test", "live");
            var client = CreateClient(config, transport, state);
            var server = CreateServer(config, transport, null, new RecordingSnapshotParticipant("mx.test", "server"));
            client.Connect(RuntimeFrame.Zero); server.ProcessIncoming(RuntimeFrame.Zero); client.ProcessIncoming(RuntimeFrame.Zero);
            client.RequireRecovery(new RuntimeFrame(5), "history exhausted");
            transport.Send(new NetworkMessageEnvelope("session", "server", "client", 1, NetworkChannel.ReliableOrdered,
                NetworkMessageKind.Snapshot, new RuntimeFrame(6), new NetworkSnapshotPacket(new RuntimeFrame(6), 99,
                    RuntimeFrame.Zero, System.Array.Empty<RuntimeCustomState>(), true)));
            client.ProcessIncoming(new RuntimeFrame(6));
            Assert.AreEqual(NetworkSessionStatus.Resynchronizing, client.Status);
            Assert.AreEqual(0, client.ResyncCount);
            Assert.AreEqual(0, state.ApplyCount);
            Assert.AreEqual("live", state.Value);
            Assert.Greater(client.SnapshotApplyFailureCount, 0);
        }

        [Test]
        public void Recovery_FailedApplyStopsUntilExplicitReconnectAndFullState()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
            var state = new RecordingSnapshotParticipant("mx.test", "live");
            var client = CreateClient(config, transport, state);
            var server = CreateServer(config, transport, null, new RecordingSnapshotParticipant("mx.test", "new"));
            client.Connect(RuntimeFrame.Zero); server.ProcessIncoming(RuntimeFrame.Zero); client.ProcessIncoming(RuntimeFrame.Zero);
            transport.Send(new NetworkMessageEnvelope("session", "server", "client", 1, NetworkChannel.ReliableOrdered,
                NetworkMessageKind.Snapshot, new RuntimeFrame(2), new NetworkSnapshotPacket(new RuntimeFrame(2), 99,
                    RuntimeFrame.Zero, new[] { new RuntimeCustomState(TestCustomStateTypeIds.NetworkParticipant, "mx.test", 99, "invalid") }, true)));
            client.ProcessIncoming(new RuntimeFrame(2));
            Assert.AreEqual(NetworkSessionStatus.RecoveryFailed, client.Status);
            Assert.AreEqual(0, state.ApplyCount);
            client.Connect(new RuntimeFrame(3)); server.ProcessIncoming(new RuntimeFrame(3)); client.ProcessIncoming(new RuntimeFrame(3));
            Assert.AreEqual(NetworkSessionStatus.Resynchronizing, client.Status);
            Assert.AreEqual(0, state.ApplyCount);
            // Reconnect requires a new full state after the fresh handshake.
            transport.Send(CreateSnapshotEnvelope(20, new RuntimeFrame(4), 99, "new"));
            client.ProcessIncoming(new RuntimeFrame(4));
            Assert.AreEqual(NetworkSessionStatus.Connected, client.Status);
            Assert.AreEqual(1, state.ApplyCount);
        }

        [Test]
        public void Recovery_BootstrapOnlyFailsClosedWithoutChangingWirePolicy()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources", NetworkStateSyncPolicy.BootstrapOnly);
            var client = CreateClient(config, transport, null);
            var server = CreateServer(config, transport, null);
            client.Connect(RuntimeFrame.Zero); server.ProcessIncoming(RuntimeFrame.Zero); client.ProcessIncoming(RuntimeFrame.Zero);
            client.RequireRecovery(new RuntimeFrame(5), "rollback history missing");
            client.SubmitCommands(new RuntimeFrame(6), new[] { new RuntimeCommand(new RuntimeFrame(6), 1, 10, 100) });
            client.ProcessIncoming(new RuntimeFrame(6));
            Assert.AreEqual(NetworkSessionStatus.RecoveryFailed, client.Status);
            Assert.AreEqual(0, client.SnapshotRequestSentCount);
            Assert.IsFalse(client.IsAuthorityReady);
        }

        [Test]
        public void Recovery_RetainedAuthoritativeCommandsReplayOnceWithoutDuplicateActions()
        {
            var coordinator = new NetworkPredictionRollbackCoordinator(2);
            var state = new RollbackCounterState();
            coordinator.RegisterParticipant(new CounterRollbackParticipant(state));
            coordinator.RecordFrame(RuntimeFrame.Zero);
            var command = new RuntimeCommand(new RuntimeFrame(2), 1, 10, 100, payload0: 5, traceId: TestTraceIds.Wrapped);
            coordinator.RecordAuthoritativeCommand(command);
            coordinator.RecordAuthoritativeCommand(command);
            int actions = 0;
            var result = coordinator.ReplayFromRestoredState(new RuntimeFrame(2), new RuntimeFrame(3),
                delegate(RuntimeCommand value, out string failure) { actions++; state.Apply(value); failure = ""; return true; },
                delegate(RuntimeFrame frame, out string failure) { failure = ""; return true; });
            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(1, actions);
            Assert.AreEqual(5, state.Value);
        }

        [Test]
        public void Recovery_ReplayedHistoryReplacesSameFrameBeforeSecondCorrection()
        {
            var coordinator = new NetworkPredictionRollbackCoordinator(4);
            var state = new RollbackCounterState();
            coordinator.RegisterParticipant(new CounterRollbackParticipant(state));
            state.Restore(new RuntimeFrame(1), 5); coordinator.RecordFrame(new RuntimeFrame(1));
            state.Restore(new RuntimeFrame(1), 10); coordinator.RecordFrame(new RuntimeFrame(1));
            state.Restore(new RuntimeFrame(2), 99);
            int restored = -1;
            var result = coordinator.ReconcileFromFrame(new RuntimeFrame(1), new RuntimeFrame(2),
                delegate(RuntimeCommand value, out string failure) { failure = ""; return true; },
                delegate(RuntimeFrame frame, out string failure) { restored = state.Value; failure = ""; return true; });
            Assert.IsTrue(result.Success);
            Assert.AreEqual(10, restored, "second correction must use the replacement checkpoint");
            Assert.AreEqual(1, coordinator.HistoryCount);
        }

        [Test]
        public void Recovery_DisconnectRequiresFreshHandshakeAndFullStateBeforeAuthorityResumes()
        {
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 0, 1, "config", "resources");
            var state = new RecordingSnapshotParticipant("mx.test", "live");
            var server = CreateServer(config, transport, null, new RecordingSnapshotParticipant("mx.test", "fresh"));
            var client = CreateClient(config, transport, state);
            client.Connect(RuntimeFrame.Zero); server.ProcessIncoming(RuntimeFrame.Zero); client.ProcessIncoming(RuntimeFrame.Zero);
            client.RequireRecovery(new RuntimeFrame(1), "history missing");
            transport.QueueEvent(new NetworkTransportEvent(NetworkTransportEventKind.Disconnected, "client", new RuntimeFrame(2), "lost"));
            transport.Send(CreateSnapshotEnvelope(40, new RuntimeFrame(1), 99, "stale"));
            client.ProcessIncoming(new RuntimeFrame(2));
            Assert.AreEqual(NetworkSessionStatus.Disconnected, client.Status);
            Assert.AreEqual(0, state.ApplyCount);
            client.Connect(new RuntimeFrame(3)); server.ProcessIncoming(new RuntimeFrame(3)); client.ProcessIncoming(new RuntimeFrame(3));
            Assert.AreEqual(NetworkSessionStatus.Resynchronizing, client.Status);
            Assert.IsFalse(client.IsAuthorityReady);
            server.BroadcastSnapshot(new RuntimeFrame(4), true); client.ProcessIncoming(new RuntimeFrame(4));
            Assert.AreEqual(NetworkSessionStatus.Connected, client.Status);
            Assert.AreEqual("fresh", state.Value);
            Assert.AreEqual(1, state.ApplyCount);
        }

        [Test]
        public void Recovery_FullRestoreDiscardsSupersededHistoryAndKeepsFutureCommands()
        {
            var coordinator = new NetworkPredictionRollbackCoordinator(4);
            var state = new RollbackCounterState();
            coordinator.RegisterParticipant(new CounterRollbackParticipant(state));
            for (int i = 0; i < 4; i++) { state.Restore(new RuntimeFrame(i), i); coordinator.RecordFrame(new RuntimeFrame(i)); }
            var command = new RuntimeCommand(new RuntimeFrame(5), 1, 10, 100, payload0: 7);
            coordinator.RecordAuthoritativeCommand(command);
            state.Restore(new RuntimeFrame(2), 100);
            var buffer = new RuntimeCommandBuffer(null, new RuntimeFrame(2));
            var result = coordinator.ReplayFromRestoredState(new RuntimeFrame(2), new RuntimeFrame(4),
                delegate(RuntimeCommand value, out string failure) { var admission = buffer.Enqueue(value); failure = admission.Success ? "" : admission.Error.Message; return admission.Success; },
                delegate(RuntimeFrame frame, out string failure) { Assert.AreEqual(0, buffer.DrainForFrame(frame).Count); coordinator.RecordFrame(frame); failure = ""; return true; });
            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(1, buffer.PendingCount);
            Assert.AreEqual(command.WithSequence(0), buffer.DrainForFrame(new RuntimeFrame(5))[0].WithSequence(0));
            var stale = coordinator.ReconcileFromFrame(RuntimeFrame.Zero, new RuntimeFrame(4),
                delegate(RuntimeCommand value, out string failure) { failure = ""; return true; },
                delegate(RuntimeFrame frame, out string failure) { failure = ""; return true; });
            Assert.IsFalse(stale.Success);
            StringAssert.Contains("history missing", stale.Message);
        }

        private static NetworkSessionRunner CreateServer(
            NetworkSessionConfig config,
            INetworkTransport transport,
            RuntimeCommandBuffer buffer,
            INetworkSnapshotParticipant participant = null,
            System.Func<RuntimeFrame, long> hashProvider = null,
            System.Func<string, RuntimeCommand, NetworkCommandAuthorizationResult> commandAuthorizer = null,
            System.Func<RuntimeFrame, string, long> peerHashProvider = null)
        {
            return new NetworkSessionRunner(
                NetworkSessionRole.Server,
                "session",
                "server",
                "server",
                config,
                transport,
                buffer,
                participant == null ? null : new[] { participant },
                hashProvider,
                commandAuthorizer: commandAuthorizer,
                peerResultHashProvider: peerHashProvider);
        }

        private static NetworkSessionRunner CreateClient(
            NetworkSessionConfig config,
            INetworkTransport transport,
            INetworkSnapshotParticipant participant,
            System.Func<RuntimeFrame, long> hashProvider = null,
            NetworkSnapshotApplyPolicy snapshotApplyPolicy = NetworkSnapshotApplyPolicy.ApplyOnMismatch,
            string peerId = "client",
            System.Func<RuntimeFrame, NetworkSnapshotHashLookupResult> snapshotHashLookup = null,
            int snapshotRequestMismatchThreshold = 3,
            int snapshotRequestCooldownFrames = 60,
            RuntimeCommandBuffer commandBuffer = null,
            System.Func<NetworkAuthoritativeCommandPacket, RuntimeFrame, bool> authoritativeCommandReceiveHandler = null,
            System.Func<NetworkSnapshotPacket, RuntimeFrame, RuntimeFrame, NetworkSnapshotCorrectionResult> snapshotCorrectionHandler = null)
        {
            return new NetworkSessionRunner(
                NetworkSessionRole.Client,
                "session",
                peerId,
                "server",
                config,
                transport,
                commandBuffer,
                participant == null ? null : new[] { participant },
                hashProvider,
                snapshotApplyPolicy: snapshotApplyPolicy,
                snapshotHashLookup: snapshotHashLookup,
                snapshotRequestMismatchThreshold: snapshotRequestMismatchThreshold,
                snapshotRequestCooldownFrames: snapshotRequestCooldownFrames,
                authoritativeCommandReceiveHandler: authoritativeCommandReceiveHandler,
                snapshotCorrectionHandler: snapshotCorrectionHandler);
        }

        private static NetworkMessageEnvelope CreateEnvelope(long sequence, NetworkChannel channel)
        {
            return new NetworkMessageEnvelope(
                "s",
                "a",
                "b",
                sequence,
                channel,
                NetworkMessageKind.Snapshot,
                RuntimeFrame.Zero,
                new object());
        }

        private static NetworkMessageEnvelope FindSingleMessage(
            IReadOnlyList<NetworkMessageEnvelope> messages,
            NetworkMessageKind kind)
        {
            NetworkMessageEnvelope found = null;
            int count = 0;
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].Kind != kind)
                    continue;
                found = messages[i];
                count++;
            }

            Assert.AreEqual(1, count, "Expected exactly one " + kind + " message.");
            return found;
        }

        private static NetworkMessageEnvelope CreateSnapshotEnvelope(
            long sequence,
            RuntimeFrame frame,
            long hash,
            string value,
            bool fullSnapshot = true)
        {
            return new NetworkMessageEnvelope(
                "session",
                "server",
                "client",
                sequence,
                NetworkChannel.Unreliable,
                NetworkMessageKind.Snapshot,
                frame,
                new NetworkSnapshotPacket(
                    frame,
                    hash,
                    RuntimeFrame.Zero,
                    new[] { new RuntimeCustomState(TestCustomStateTypeIds.NetworkTest, "mx.test", 0, value) },
                    fullSnapshot));
        }

        private static NetworkMessageEnvelope CreateDigestSnapshotEnvelope(
            long sequence,
            RuntimeFrame frame,
            long hash)
        {
            return new NetworkMessageEnvelope(
                "session",
                "server",
                "client",
                sequence,
                NetworkChannel.Unreliable,
                NetworkMessageKind.Snapshot,
                frame,
                new NetworkSnapshotPacket(
                    frame,
                    hash,
                    RuntimeFrame.Zero,
                    moduleSnapshots: null,
                    fullSnapshot: false));
        }

        private static NetworkMessageEnvelope CreatePongEnvelope(
            long sequence,
            RuntimeFrame sentFrame,
            RuntimeFrame receiveFrame)
        {
            return new NetworkMessageEnvelope(
                "session",
                "server",
                "client",
                sequence,
                NetworkChannel.ReliableOrdered,
                NetworkMessageKind.Pong,
                receiveFrame,
                new NetworkHeartbeatPacket("server", sentFrame, sequence));
        }

        private sealed class RollbackCounterState
        {
            public RuntimeFrame Frame { get; set; }
            public int Value { get; private set; }

            public void Apply(RuntimeCommand command)
            {
                Value += command.Payload0;
            }

            public void Restore(RuntimeFrame frame, int value)
            {
                Frame = frame;
                Value = value;
            }
        }

        private sealed class CounterRollbackParticipant : INetworkRollbackParticipant
        {
            private readonly RollbackCounterState _state;

            public CounterRollbackParticipant(RollbackCounterState state)
            {
                _state = state;
            }

            public string ParticipantId => "counter";

            public object CaptureRollbackState(RuntimeFrame frame)
            {
                return new CounterRollbackSnapshot(_state.Value);
            }

            public bool RestoreRollbackState(RuntimeFrame frame, object state, out string failure)
            {
                if (!(state is CounterRollbackSnapshot snapshot))
                {
                    failure = "invalid counter snapshot";
                    return false;
                }

                _state.Restore(frame, snapshot.Value);
                failure = string.Empty;
                return true;
            }
        }

        private readonly struct CounterRollbackSnapshot
        {
            public CounterRollbackSnapshot(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private sealed class RecordingSnapshotParticipant : INetworkSnapshotParticipant
        {
            public RecordingSnapshotParticipant(string participantId, string value)
            {
                ParticipantId = participantId;
                Value = value;
            }

            public string ParticipantId { get; }
            public string Value { get; private set; }
            public int ApplyCount { get; private set; }

            public RuntimeCustomState CaptureSnapshot(RuntimeFrame frame)
            {
                return new RuntimeCustomState(TestCustomStateTypeIds.NetworkParticipant, ParticipantId, 0, Value);
            }

            public NetworkSnapshotApplyResult ApplySnapshot(RuntimeFrame frame, RuntimeCustomState snapshot)
            {
                if (snapshot.SchemaVersion != 0)
                    return NetworkSnapshotApplyResult.Failed("unsupported schema");

                Value = snapshot.PayloadJson;
                ApplyCount++;
                return NetworkSnapshotApplyResult.Succeeded();
            }
        }

        private sealed class PeerRecordingSnapshotParticipant : INetworkPeerSnapshotParticipant
        {
            public PeerRecordingSnapshotParticipant(string participantId)
            {
                ParticipantId = participantId;
            }

            public string ParticipantId { get; }

            public RuntimeCustomState CaptureSnapshot(RuntimeFrame frame)
            {
                return new RuntimeCustomState(TestCustomStateTypeIds.NetworkParticipant, ParticipantId, 0, "none");
            }

            public RuntimeCustomState CaptureSnapshot(RuntimeFrame frame, string receiverPeerId)
            {
                return new RuntimeCustomState(TestCustomStateTypeIds.NetworkParticipant, ParticipantId, 0, receiverPeerId);
            }

            public NetworkSnapshotApplyResult ApplySnapshot(RuntimeFrame frame, RuntimeCustomState snapshot)
            {
                return NetworkSnapshotApplyResult.Succeeded();
            }
        }
    }
}
