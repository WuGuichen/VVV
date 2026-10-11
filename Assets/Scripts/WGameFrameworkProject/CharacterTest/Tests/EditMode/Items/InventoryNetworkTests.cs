using System;
using System.Collections.Generic;
using MxFramework.Items;
using MxFramework.Items.Network;
using MxFramework.Network;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Items
{
    public sealed class InventoryNetworkTests
    {
        private static class SampleOwnerIds { public const int Hero = 1; public const int Other = 2; }

        [Test]
        public void ItemNetworkCommandIds_KeepStableWireValues()
        {
            Assert.AreEqual(1003201, ItemNetworkCommandIds.AddStack);
            Assert.AreEqual(1003202, ItemNetworkCommandIds.RemoveStack);
            Assert.AreEqual(1003203, ItemNetworkCommandIds.MoveStack);
            Assert.AreEqual(1003204, ItemNetworkCommandIds.SplitStack);
            Assert.AreEqual(1003205, ItemNetworkCommandIds.MergeStack);
            Assert.AreEqual(1003206, ItemNetworkCommandIds.SwapSlots);
            Assert.AreEqual(1003207, ItemNetworkCommandIds.UseItem);
        }

        [Test]
        public void InventoryNetworkCommandCodec_ExecutesMoveSplitMergeSwapAndUse()
        {
            InventoryService service = InventoryRuntimeTests.CreateService(out InventoryStore store, out ItemDefinition herb, out ItemDefinition ore);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 4, "bag");
            var chest = new InventoryContainer(new InventoryContainerId(2), new InventoryOwnerId(SampleOwnerIds.Hero), 4, "chest");
            store.Register(bag);
            store.Register(chest);
            service.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 10));
            service.AddStack(bag.ContainerId, 2, new ItemStack(ore.DefinitionId, 1));

            RuntimeCommand move = InventoryNetworkCommandCodec.CreateMoveStack(
                RuntimeFrame.Zero,
                sourceId: 7,
                bag.ContainerId,
                0,
                chest.ContainerId,
                0,
                5,
                new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_InventoryNetworkTests.Id_net_move, 0));
            Assert.IsTrue(InventoryNetworkCommandCodec.TryDecode(move, out InventoryNetworkCommand decodedMove));
            Assert.AreEqual(InventoryNetworkCommandKind.MoveStack, decodedMove.Kind);
            Assert.AreEqual(bag.ContainerId, decodedMove.SourceContainerId);
            Assert.AreEqual(chest.ContainerId, decodedMove.DestinationContainerId);
            Assert.IsTrue(InventoryNetworkCommandCodec.TryExecute(service, move, out InventoryOperationResult result, out _));
            Assert.AreEqual(InventoryOperationStatus.Success, result.Status);
            Assert.AreEqual(5, bag.GetStack(0).Count);
            Assert.AreEqual(5, chest.GetStack(0).Count);

            RuntimeCommand split = InventoryNetworkCommandCodec.CreateSplitStack(new RuntimeFrame(1), 7, chest.ContainerId, 0, bag.ContainerId, 1, 2);
            Assert.IsTrue(InventoryNetworkCommandCodec.TryExecute(service, split, out result, out _));
            Assert.AreEqual(InventoryOperationStatus.Success, result.Status);
            Assert.AreEqual(3, chest.GetStack(0).Count);
            Assert.AreEqual(2, bag.GetStack(1).Count);

            RuntimeCommand merge = InventoryNetworkCommandCodec.CreateMergeStack(new RuntimeFrame(2), 7, bag.ContainerId, 1, bag.ContainerId, 0);
            Assert.IsTrue(InventoryNetworkCommandCodec.TryExecute(service, merge, out result, out _));
            Assert.AreEqual(InventoryOperationStatus.Success, result.Status);
            Assert.AreEqual(7, bag.GetStack(0).Count);
            Assert.IsTrue(bag.GetStack(1).IsEmpty);

            RuntimeCommand swap = InventoryNetworkCommandCodec.CreateSwapSlots(new RuntimeFrame(3), 7, bag.ContainerId, 0, bag.ContainerId, 2);
            Assert.IsTrue(InventoryNetworkCommandCodec.TryExecute(service, swap, out result, out _));
            Assert.AreEqual(InventoryOperationStatus.Success, result.Status);
            Assert.AreEqual(ore.DefinitionId, bag.GetStack(0).DefinitionId);
            Assert.AreEqual(herb.DefinitionId, bag.GetStack(2).DefinitionId);

            RuntimeCommand use = InventoryNetworkCommandCodec.CreateUseItem(new RuntimeFrame(4), 7, bag.ContainerId, 0);
            ItemStack beforeUse = bag.GetStack(0);
            Assert.IsTrue(InventoryNetworkCommandCodec.TryExecute(service, use, out result, out _));
            Assert.AreEqual(InventoryOperationStatus.UnsupportedUse, result.Status);
            Assert.AreEqual(beforeUse, bag.GetStack(0));
        }

        [Test]
        public void InventoryNetworkCommandCodec_DoesNotUseRequestHandleAcrossNetwork()
        {
            InventoryService service = InventoryRuntimeTests.CreateService(out InventoryStore store, out ItemDefinition herb, out _);
            var bag = new InventoryContainer(new InventoryContainerId(12), new InventoryOwnerId(SampleOwnerIds.Hero), 2, "bag");
            var chest = new InventoryContainer(new InventoryContainerId(22), new InventoryOwnerId(SampleOwnerIds.Hero), 2, "chest");
            store.Register(bag);
            store.Register(chest);
            service.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 4));

            RuntimeCommand command = InventoryNetworkCommandCodec.CreateMoveStack(
                new RuntimeFrame(5),
                sourceId: 9,
                bag.ContainerId,
                0,
                chest.ContainerId,
                1,
                3);

            Assert.AreEqual(ItemNetworkCommandIds.MoveStack, command.CommandId);
            Assert.AreEqual(bag.ContainerId.Value, command.TargetId);
            Assert.AreEqual(chest.ContainerId.Value, command.Payload1);
            Assert.IsTrue(InventoryNetworkCommandCodec.TryExecute(service, command, out InventoryOperationResult result, out _));
            Assert.AreEqual(InventoryOperationStatus.Success, result.Status);
            Assert.AreEqual(1, bag.GetStack(0).Count);
            Assert.AreEqual(3, chest.GetStack(1).Count);
        }

        [Test]
        public void InventoryNetworkAuthorizer_RejectsClientGrantRemoveAndForeignContainers()
        {
            InventoryService service = InventoryRuntimeTests.CreateService(out InventoryStore store, out ItemDefinition herb, out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 3, "bag");
            var other = new InventoryContainer(new InventoryContainerId(2), new InventoryOwnerId(SampleOwnerIds.Other), 3, "other");
            store.Register(bag);
            store.Register(other);
            service.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 2));

            var authority = new InventoryOwnerNetworkAuthority(store);
            authority.SetOwnerForPeer("peer.hero", new InventoryOwnerId(SampleOwnerIds.Hero));
            var authorizer = new InventoryNetworkCommandAuthorizer(authority);

            RuntimeCommand add = InventoryNetworkCommandCodec.CreateAddStack(RuntimeFrame.Zero, 7, bag.ContainerId, 1, herb.DefinitionId, 1);
            RuntimeCommand remove = InventoryNetworkCommandCodec.CreateRemoveStack(RuntimeFrame.Zero, 7, bag.ContainerId, 0, 1);
            RuntimeCommand ownMove = InventoryNetworkCommandCodec.CreateMoveStack(RuntimeFrame.Zero, 7, bag.ContainerId, 0, bag.ContainerId, 1, 1);
            RuntimeCommand foreignMove = InventoryNetworkCommandCodec.CreateMoveStack(RuntimeFrame.Zero, 7, bag.ContainerId, 0, other.ContainerId, 0, 1);
            RuntimeCommand unrelated = new RuntimeCommand(RuntimeFrame.Zero, 7, 999, 1);

            Assert.IsFalse(authorizer.Authorize("peer.hero", add).Accepted);
            Assert.IsFalse(authorizer.Authorize("peer.hero", remove).Accepted);
            Assert.IsTrue(authorizer.Authorize("peer.hero", ownMove).Accepted);
            Assert.IsFalse(authorizer.Authorize("peer.hero", foreignMove).Accepted);
            Assert.IsTrue(authorizer.Authorize("peer.hero", unrelated).Accepted);

            Assert.AreEqual(2, bag.GetStack(0).Count);
            Assert.IsTrue(bag.GetStack(1).IsEmpty);
        }

        [Test]
        public void InventoryNetworkAuthorizer_AllowsExplicitServerAuthorityForAddRemove()
        {
            InventoryService service = InventoryRuntimeTests.CreateService(out InventoryStore store, out ItemDefinition herb, out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 2, "bag");
            store.Register(bag);

            var authority = new InventoryOwnerNetworkAuthority(store);
            authority.GrantAllContainerAccess("server");
            authority.GrantStackMutationAuthority("server");
            var authorizer = new InventoryNetworkCommandAuthorizer(authority);

            RuntimeCommand add = InventoryNetworkCommandCodec.CreateAddStack(RuntimeFrame.Zero, 1, bag.ContainerId, 0, herb.DefinitionId, 3);
            Assert.IsTrue(authorizer.Authorize("server", add).Accepted);
            Assert.IsTrue(InventoryNetworkCommandCodec.TryExecute(service, add, out InventoryOperationResult result, out _));
            Assert.AreEqual(InventoryOperationStatus.Success, result.Status);
            Assert.AreEqual(3, bag.GetStack(0).Count);
        }

        [Test]
        public void InventoryNetworkSnapshotParticipant_RoundTripsHashAndRejectsDefinitionMismatch()
        {
            InventoryService service = InventoryRuntimeTests.CreateService(out InventoryStore store, out ItemDefinition herb, out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 2, "bag");
            store.Register(bag);
            service.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 8));

            long hashBefore = new RuntimeHashCombiner(new[] { new InventoryHashContributor(store) }).ComputeHash(RuntimeFrame.Zero);
            var participant = new InventoryNetworkSnapshotParticipant(store, service.Definitions);
            RuntimeCustomState snapshot = participant.CaptureSnapshot(RuntimeFrame.Zero);

            Assert.AreEqual(ItemRuntimeIds.InventoryModule, snapshot.TypeId);
            Assert.AreEqual(InventoryNetworkSnapshotState.CurrentSchemaVersion, snapshot.SchemaVersion);
            StringAssert.DoesNotContain("item.herb.name", snapshot.PayloadJson);
            StringAssert.DoesNotContain("item.herb.icon", snapshot.PayloadJson);
            StringAssert.DoesNotContain("profile", snapshot.PayloadJson);
            StringAssert.DoesNotContain("moduleStates", snapshot.PayloadJson);

            var restoredStore = new InventoryStore();
            var restorer = new InventoryNetworkSnapshotParticipant(restoredStore, service.Definitions);
            NetworkSnapshotApplyResult apply = restorer.ApplySnapshot(RuntimeFrame.Zero, snapshot);
            Assert.IsTrue(apply.Success, apply.Message);
            long hashAfter = new RuntimeHashCombiner(new[] { new InventoryHashContributor(restoredStore) }).ComputeHash(RuntimeFrame.Zero);
            Assert.AreEqual(hashBefore, hashAfter);

            var changedDefinitions = new ItemDefinitionRegistry();
            changedDefinitions.Register(ItemDefinitionTests.CreateDefinition(1001, "item.herb.changed", ItemKind.Material, 20));
            var mismatch = new InventoryNetworkSnapshotParticipant(new InventoryStore(), changedDefinitions).ApplySnapshot(RuntimeFrame.Zero, snapshot);
            Assert.IsFalse(mismatch.Success);
            StringAssert.Contains("definition hash mismatch", mismatch.Message);

            var unchangedStore = new InventoryStore();
            var unchangedBag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 1, "bag");
            unchangedStore.Register(unchangedBag);
            var unchangedService = new InventoryService(service.Definitions, unchangedStore);
            unchangedService.AddStack(unchangedBag.ContainerId, 0, new ItemStack(herb.DefinitionId, 2));
            string invalidPayload = "{\"schemaVersion\":1,\"definitionHash\":" + service.Definitions.ComputeStableHash()
                + ",\"containers\":[{\"containerId\":1,\"stableId\":\"bag\",\"ownerStableId\":\"owner.hero\",\"slotCount\":1,\"slots\":[{\"slotIndex\":0,\"itemDefinitionId\":1001,\"count\":99,\"itemInstanceId\":0,\"customStateTypeId\":\"\",\"customStateSchemaVersion\":0,\"customStatePayloadJson\":\"\"}]}]}";
            var invalidSnapshot = new RuntimeCustomState(3302, ItemRuntimeIds.InventoryModule,
                InventoryNetworkSnapshotState.CurrentSchemaVersion,
                invalidPayload);
            NetworkSnapshotApplyResult invalidApply =
                new InventoryNetworkSnapshotParticipant(unchangedStore, service.Definitions).ApplySnapshot(RuntimeFrame.Zero, invalidSnapshot);
            Assert.IsFalse(invalidApply.Success);
            Assert.AreEqual(2, unchangedBag.GetStack(0).Count);
        }

        [Test]
        public void InventoryRollbackParticipant_RestoresCapturedState()
        {
            InventoryService service = InventoryRuntimeTests.CreateService(out InventoryStore store, out ItemDefinition herb, out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 2, "bag");
            store.Register(bag);
            service.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 8));

            long hashBefore = new RuntimeHashCombiner(new[] { new InventoryHashContributor(store) }).ComputeHash(RuntimeFrame.Zero);
            var participant = new InventoryRollbackParticipant(store, service.Definitions, historyCapacity: 2);
            object rollbackState = participant.CaptureRollbackState(RuntimeFrame.Zero);
            Assert.IsInstanceOf<InventoryRollbackState>(rollbackState);
            var typedState = (InventoryRollbackState)rollbackState;
            Assert.AreEqual(1, typedState.ContainerCount);
            Assert.AreEqual(1, typedState.SlotCount);
            Assert.GreaterOrEqual(typedState.SlotCapacity, 2);

            service.RemoveStack(bag.ContainerId, 0, 5);
            Assert.AreEqual(3, bag.GetStack(0).Count);

            Assert.IsTrue(participant.RestoreRollbackState(RuntimeFrame.Zero, rollbackState, out string failure), failure);
            long hashAfter = new RuntimeHashCombiner(new[] { new InventoryHashContributor(store) }).ComputeHash(RuntimeFrame.Zero);
            Assert.AreEqual(hashBefore, hashAfter);
        }

        [Test]
        public void InventoryRollbackParticipant_CaptureDoesNotAllocateAfterWarmup()
        {
            InventoryService service = CreateInventory(
                FixtureIds_InventoryNetworkTests.Id_owner_hero,
                out InventoryStore store,
                out InventoryContainer bag,
                out _,
                out ItemDefinition herb);
            service.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 8));
            var participant = new InventoryRollbackParticipant(store, service.Definitions, historyCapacity: 4);
            for (int i = 0; i < participant.HistoryCapacity; i++)
                participant.CaptureRollbackState(new RuntimeFrame(i));

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            object lastState = null;
            for (int i = 0; i < 64; i++)
                lastState = participant.CaptureRollbackState(new RuntimeFrame(i + 10));

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.IsInstanceOf<InventoryRollbackState>(lastState);
        }

        [Test]
        public void InventoryRollbackParticipant_KeepsHistoryStatesDistinctWithinCapacityThenReusesRing()
        {
            InventoryService service = InventoryRuntimeTests.CreateService(out InventoryStore store, out ItemDefinition herb, out _);
            var bagId = new InventoryContainerId(1);
            var bag = new InventoryContainer(bagId, new InventoryOwnerId(SampleOwnerIds.Hero), 2, "bag");
            store.Register(bag);
            service.AddStack(bagId, 0, new ItemStack(herb.DefinitionId, 8));

            var participant = new InventoryRollbackParticipant(store, service.Definitions, historyCapacity: 2);
            object stateA = participant.CaptureRollbackState(RuntimeFrame.Zero);

            Assert.IsTrue(service.RemoveStack(bagId, 0, 3).Success);
            object stateB = participant.CaptureRollbackState(new RuntimeFrame(1));

            Assert.AreNotSame(stateA, stateB);
            Assert.IsTrue(participant.RestoreRollbackState(RuntimeFrame.Zero, stateA, out string failureA), failureA);
            Assert.AreEqual(8, GetStackCount(store, bagId, 0));

            Assert.IsTrue(participant.RestoreRollbackState(new RuntimeFrame(1), stateB, out string failureB), failureB);
            Assert.AreEqual(5, GetStackCount(store, bagId, 0));

            object stateC = participant.CaptureRollbackState(new RuntimeFrame(2));
            Assert.AreSame(stateA, stateC);
        }

        [Test]
        public void InventoryNetworkCommandStream_ServerAcceptsAndClientAppliesAuthoritativeCommand()
        {
            InventoryService serverService = CreateInventory(FixtureIds_InventoryNetworkTests.Id_owner_hero, out InventoryStore serverStore, out InventoryContainer serverBag, out InventoryContainer serverChest, out ItemDefinition herb);
            InventoryService clientService = CreateInventory(FixtureIds_InventoryNetworkTests.Id_owner_hero, out InventoryStore clientStore, out _, out _, out _);
            serverService.AddStack(serverBag.ContainerId, 0, new ItemStack(herb.DefinitionId, 6));
            clientService.AddStack(serverBag.ContainerId, 0, new ItemStack(herb.DefinitionId, 6));

            var authority = new InventoryOwnerNetworkAuthority(serverStore);
            authority.SetOwnerForPeer("peer.hero", new InventoryOwnerId(SampleOwnerIds.Hero));
            var authorizer = new InventoryNetworkCommandAuthorizer(authority);

            RuntimeCommand request = InventoryNetworkCommandCodec.CreateMoveStack(
                new RuntimeFrame(10),
                sourceId: 42,
                serverBag.ContainerId,
                0,
                serverChest.ContainerId,
                0,
                4);

            Assert.IsTrue(authorizer.Authorize("peer.hero", request).Accepted);
            Assert.IsTrue(InventoryNetworkCommandCodec.TryExecute(serverService, request, out InventoryOperationResult serverResult, out _));
            Assert.AreEqual(InventoryOperationStatus.Success, serverResult.Status);

            var authoritative = new NetworkAuthoritativeCommandPacket(
                "peer.hero",
                new RuntimeFrame(10),
                new RuntimeFrame(10),
                new List<RuntimeCommand> { request });

            Assert.IsTrue(InventoryNetworkCommandCodec.TryExecute(clientService, authoritative.Commands[0], out InventoryOperationResult clientResult, out _));
            Assert.AreEqual(InventoryOperationStatus.Success, clientResult.Status);
            long serverHash = new RuntimeHashCombiner(new[] { new InventoryHashContributor(serverStore) }).ComputeHash(RuntimeFrame.Zero);
            long clientHash = new RuntimeHashCombiner(new[] { new InventoryHashContributor(clientStore) }).ComputeHash(RuntimeFrame.Zero);
            Assert.AreEqual(serverHash, clientHash);
        }

        [Test]
        public void InventoryNetworkSessionRunner_ClientMoveRoundTripsThroughLoopbackAuthority()
        {
            InventoryService serverService = CreateInventory(FixtureIds_InventoryNetworkTests.Id_owner_hero, out InventoryStore serverStore, out InventoryContainer serverBag, out InventoryContainer serverChest, out ItemDefinition herb);
            InventoryService clientService = CreateInventory(FixtureIds_InventoryNetworkTests.Id_owner_hero, out InventoryStore clientStore, out _, out _, out _);
            serverService.AddStack(serverBag.ContainerId, 0, new ItemStack(herb.DefinitionId, 6));
            clientService.AddStack(serverBag.ContainerId, 0, new ItemStack(herb.DefinitionId, 6));

            var authority = new InventoryOwnerNetworkAuthority(serverStore);
            authority.SetOwnerForPeer("client", new InventoryOwnerId(SampleOwnerIds.Hero));
            var authorizer = new InventoryNetworkCommandAuthorizer(authority);
            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 0, 20, "config", "resources");
            var server = new NetworkSessionRunner(
                NetworkSessionRole.Server,
                "session",
                "server",
                "server",
                config,
                transport,
                new RuntimeCommandBuffer(),
                resultHashProvider: frame => ComputeInventoryHash(serverStore, frame),
                commandAuthorizer: authorizer.Authorize,
                authoritativeCommandObserver: packet =>
                {
                    for (int i = 0; i < packet.Commands.Count; i++)
                    {
                        Assert.IsTrue(InventoryNetworkCommandCodec.TryExecute(
                            serverService,
                            packet.Commands[i],
                            out InventoryOperationResult result,
                            out _));
                        Assert.AreEqual(InventoryOperationStatus.Success, result.Status);
                    }
                });
            var client = new NetworkSessionRunner(
                NetworkSessionRole.Client,
                "session",
                "client",
                "server",
                config,
                transport,
                resultHashProvider: frame => ComputeInventoryHash(clientStore, frame),
                authoritativeCommandReceiveHandler: (packet, frame) =>
                {
                    for (int i = 0; i < packet.Commands.Count; i++)
                    {
                        Assert.IsTrue(InventoryNetworkCommandCodec.TryExecute(
                            clientService,
                            packet.Commands[i],
                            out InventoryOperationResult result,
                            out _));
                        Assert.AreEqual(InventoryOperationStatus.Success, result.Status);
                    }

                    return true;
                });

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);
            Assert.AreEqual(NetworkSessionStatus.Connected, client.Status);

            RuntimeCommand move = InventoryNetworkCommandCodec.CreateMoveStack(
                new RuntimeFrame(1),
                sourceId: 7,
                serverBag.ContainerId,
                0,
                serverChest.ContainerId,
                0,
                4,
                new MxFramework.Runtime.RuntimeTraceIdentity(900, 0, FixtureIds_InventoryNetworkTests.Id_runner_move, 0));
            client.SubmitCommands(new RuntimeFrame(1), new[] { move });
            server.ProcessIncoming(new RuntimeFrame(1));
            client.ProcessIncoming(new RuntimeFrame(1));

            Assert.AreEqual(2, GetStackCount(serverStore, serverBag.ContainerId, 0));
            Assert.AreEqual(4, GetStackCount(serverStore, serverChest.ContainerId, 0));
            Assert.AreEqual(2, GetStackCount(clientStore, serverBag.ContainerId, 0));
            Assert.AreEqual(4, GetStackCount(clientStore, serverChest.ContainerId, 0));
            Assert.AreEqual(ComputeInventoryHash(serverStore, RuntimeFrame.Zero), ComputeInventoryHash(clientStore, RuntimeFrame.Zero));
            Assert.AreEqual(new RuntimeFrame(1), server.GetLastAckedInputFrame("client"));
            Assert.AreEqual(1, server.AuthoritativeCommandSentCount);
            Assert.AreEqual(1, client.AuthoritativeCommandReceivedCount);
            Assert.AreEqual(1, client.AuthoritativeCommandEchoCount);
        }

        [Test]
        public void InventoryNetworkSessionRunner_LateJoinBootstrapSnapshotAppliesInventoryState()
        {
            InventoryService serverService = CreateInventory(FixtureIds_InventoryNetworkTests.Id_owner_hero, out InventoryStore serverStore, out InventoryContainer serverBag, out _, out ItemDefinition herb);
            InventoryService clientService = CreateInventory(FixtureIds_InventoryNetworkTests.Id_owner_hero, out InventoryStore clientStore, out _, out _, out _);
            serverService.AddStack(serverBag.ContainerId, 0, new ItemStack(herb.DefinitionId, 6));

            var transport = new LoopbackNetworkTransport();
            var config = new NetworkSessionConfig(1, 60, 0, 20, "config", "resources");
            var server = new NetworkSessionRunner(
                NetworkSessionRole.Server,
                "session",
                "server",
                "server",
                config,
                transport,
                participants: new[] { new InventoryNetworkSnapshotParticipant(serverStore, serverService.Definitions) },
                resultHashProvider: frame => ComputeInventoryHash(serverStore, frame));
            var client = new NetworkSessionRunner(
                NetworkSessionRole.Client,
                "session",
                "client",
                "server",
                config,
                transport,
                participants: new[] { new InventoryNetworkSnapshotParticipant(clientStore, clientService.Definitions) },
                resultHashProvider: frame => ComputeInventoryHash(clientStore, frame));

            client.Connect(RuntimeFrame.Zero);
            server.ProcessIncoming(RuntimeFrame.Zero);
            client.ProcessIncoming(RuntimeFrame.Zero);
            server.BroadcastSnapshot(new RuntimeFrame(1), fullSnapshot: true);
            client.ProcessIncoming(new RuntimeFrame(1));

            Assert.AreEqual(1, server.FullSnapshotSentCount);
            Assert.AreEqual(1, client.FullSnapshotReceivedCount);
            Assert.AreEqual(6, GetStackCount(clientStore, serverBag.ContainerId, 0));
            Assert.AreEqual(ComputeInventoryHash(serverStore, RuntimeFrame.Zero), ComputeInventoryHash(clientStore, RuntimeFrame.Zero));
        }

        [Test]
        public void InventoryNetworkCommandCodec_RejectsMalformedCommandsWithoutMutatingState()
        {
            InventoryService service = InventoryRuntimeTests.CreateService(out InventoryStore store, out ItemDefinition herb, out _);
            var bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(SampleOwnerIds.Hero), 2, "bag");
            store.Register(bag);
            service.AddStack(bag.ContainerId, 0, new ItemStack(herb.DefinitionId, 5));
            long hashBefore = ComputeInventoryHash(store, RuntimeFrame.Zero);

            var authority = new InventoryOwnerNetworkAuthority(store);
            authority.SetOwnerForPeer("peer.hero", new InventoryOwnerId(SampleOwnerIds.Hero));
            var authorizer = new InventoryNetworkCommandAuthorizer(authority);

            RuntimeCommand invalidMoveCount = new RuntimeCommand(
                RuntimeFrame.Zero,
                7,
                ItemNetworkCommandIds.MoveStack,
                bag.ContainerId.Value,
                0 | (1 << 16),
                bag.ContainerId.Value,
                0);
            RuntimeCommand invalidMergeCount = new RuntimeCommand(
                RuntimeFrame.Zero,
                7,
                ItemNetworkCommandIds.MergeStack,
                bag.ContainerId.Value,
                0 | (1 << 16),
                bag.ContainerId.Value,
                1);
            RuntimeCommand invalidUsePayload = new RuntimeCommand(
                RuntimeFrame.Zero,
                7,
                ItemNetworkCommandIds.UseItem,
                bag.ContainerId.Value,
                0,
                99,
                0);
            RuntimeCommand missingDestination = new RuntimeCommand(
                RuntimeFrame.Zero,
                7,
                ItemNetworkCommandIds.MoveStack,
                bag.ContainerId.Value,
                0 | (1 << 16),
                999,
                1);

            Assert.IsFalse(InventoryNetworkCommandCodec.TryExecute(service, invalidMoveCount, out _, out InventoryNetworkCommandDecodeStatus moveStatus));
            Assert.AreEqual(InventoryNetworkCommandDecodeStatus.InvalidPayload, moveStatus);
            Assert.IsFalse(authorizer.Authorize("peer.hero", invalidMergeCount).Accepted);
            Assert.IsFalse(InventoryNetworkCommandCodec.TryExecute(service, invalidUsePayload, out _, out InventoryNetworkCommandDecodeStatus useStatus));
            Assert.AreEqual(InventoryNetworkCommandDecodeStatus.InvalidPayload, useStatus);
            Assert.IsTrue(InventoryNetworkCommandCodec.TryExecute(service, missingDestination, out InventoryOperationResult missingResult, out _));
            Assert.AreEqual(InventoryOperationStatus.InvalidContainer, missingResult.Status);
            Assert.AreEqual(hashBefore, ComputeInventoryHash(store, RuntimeFrame.Zero));
            Assert.AreEqual(5, bag.GetStack(0).Count);
            Assert.IsTrue(bag.GetStack(1).IsEmpty);
        }

        [Test]
        public void InventoryNetworkSnapshotParticipant_PreservesInstanceCustomStateAndContainerOrdering()
        {
            var definitions = new ItemDefinitionRegistry();
            ItemDefinition herb = ItemDefinitionTests.CreateDefinition(1001, "item.herb", ItemKind.Material, 20);
            ItemDefinition ore = ItemDefinitionTests.CreateDefinition(1002, "item.ore", ItemKind.Material, 10);
            definitions.Register(ore);
            definitions.Register(herb);

            InventoryService left = CreateOrderedInventory(
                definitions,
                out InventoryStore leftStore,
                registrationOrder: new[] { 3, 1, 2 });
            InventoryService right = CreateOrderedInventory(
                definitions,
                out InventoryStore rightStore,
                registrationOrder: new[] { 1, 2, 3 });

            var customState = new RuntimeCustomState(2, "item.custom", 2, "{\"durability\":7}");
            Assert.IsTrue(left.AddStack(new InventoryContainerId(1), 0, new ItemStack(herb.DefinitionId, 1, new ItemInstanceId(77), customState)).Success);
            Assert.IsTrue(left.AddStack(new InventoryContainerId(2), 1, new ItemStack(ore.DefinitionId, 4)).Success);
            Assert.IsTrue(right.AddStack(new InventoryContainerId(1), 0, new ItemStack(herb.DefinitionId, 1, new ItemInstanceId(77), customState)).Success);
            Assert.IsTrue(right.AddStack(new InventoryContainerId(2), 1, new ItemStack(ore.DefinitionId, 4)).Success);

            long leftHash = ComputeInventoryHash(leftStore, RuntimeFrame.Zero);
            long rightHash = ComputeInventoryHash(rightStore, RuntimeFrame.Zero);
            Assert.AreEqual(leftHash, rightHash);

            RuntimeCustomState snapshot = new InventoryNetworkSnapshotParticipant(leftStore, definitions).CaptureSnapshot(RuntimeFrame.Zero);
            var restoredStore = new InventoryStore();
            NetworkSnapshotApplyResult apply =
                new InventoryNetworkSnapshotParticipant(restoredStore, definitions).ApplySnapshot(RuntimeFrame.Zero, snapshot);
            Assert.IsTrue(apply.Success, apply.Message);
            Assert.AreEqual(leftHash, ComputeInventoryHash(restoredStore, RuntimeFrame.Zero));
            Assert.IsTrue(restoredStore.TryGet(new InventoryContainerId(1), out InventoryContainer restoredBag));
            ItemStack restoredStack = restoredBag.GetStack(0);
            Assert.AreEqual(herb.DefinitionId, restoredStack.DefinitionId);
            Assert.AreEqual(1, restoredStack.Count);
            Assert.AreEqual(new ItemInstanceId(77), restoredStack.InstanceId);
            Assert.IsNotNull(restoredStack.CustomState);
            Assert.AreEqual("item.custom", restoredStack.CustomState.TypeId);
            Assert.AreEqual(2, restoredStack.CustomState.SchemaVersion);
            Assert.AreEqual("{\"durability\":7}", restoredStack.CustomState.PayloadJson);
        }

        [Test]
        public void InventoryIdentityCounter_SnapshotAndRollbackRestoreUnplacedAllocations()
        {
            var inventory = CreateInventory(1, out var store, out _, out _, out _);
            Assert.IsTrue(store.TryAllocateInstanceId(out var first));
            var participant = new InventoryNetworkSnapshotParticipant(store, inventory.Definitions);
            var snapshot = participant.CaptureSnapshot(RuntimeFrame.Zero);
            long expectedHash = ComputeInventoryHash(store, RuntimeFrame.Zero);
            Assert.IsTrue(store.TryAllocateInstanceId(out _));
            Assert.IsTrue(participant.ApplySnapshot(RuntimeFrame.Zero, snapshot).Success);
            Assert.AreEqual(expectedHash, ComputeInventoryHash(store, RuntimeFrame.Zero));
            Assert.AreEqual(2, store.NextItemInstanceId);
            var rollback = new InventoryRollbackParticipant(store, inventory.Definitions);
            object captured = rollback.CaptureRollbackState(RuntimeFrame.Zero);
            Assert.IsTrue(store.TryAllocateInstanceId(out var second));
            Assert.AreEqual(2, second.Value);
            Assert.IsTrue(rollback.RestoreRollbackState(RuntimeFrame.Zero, captured, out var failure), failure);
            Assert.AreEqual(expectedHash, ComputeInventoryHash(store, RuntimeFrame.Zero));
            Assert.IsTrue(store.TryAllocateInstanceId(out var replayed));
            Assert.AreEqual(second, replayed);
            Assert.AreEqual(1, first.Value);
        }

        [Test]
        public void InventoryIdentityCounter_LegacySnapshotMigratesFromOccupiedIds()
        {
            var inventory = CreateInventory(1, out var store, out var bag, out _, out var herb);
            Assert.IsTrue(inventory.AddStack(bag.ContainerId, 0,
                new ItemStack(herb.DefinitionId, 1, new ItemInstanceId(77))).Success);
            var participant = new InventoryNetworkSnapshotParticipant(store, inventory.Definitions);
            var current = participant.CaptureSnapshot(RuntimeFrame.Zero);
            var json = Newtonsoft.Json.Linq.JObject.Parse(current.PayloadJson);
            json["schemaVersion"] = 2; json.Remove("nextItemInstanceId");
            var legacy = new RuntimeCustomState(3302, participant.ParticipantId, 2, json.ToString());
            var restored = new InventoryStore();
            Assert.IsTrue(new InventoryNetworkSnapshotParticipant(restored, inventory.Definitions)
                .ApplySnapshot(RuntimeFrame.Zero, legacy).Success);
            Assert.IsTrue(restored.TryAllocateInstanceId(out var next));
            Assert.AreEqual(78, next.Value);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void InventoryIdentityCounter_InvalidSnapshotLeavesLiveAuthorityUnchanged(bool schemaMismatch)
        {
            var inventory = CreateInventory(1, out var store, out _, out _, out _);
            Assert.IsTrue(store.TryAllocateInstanceId(out _));
            var participant = new InventoryNetworkSnapshotParticipant(store, inventory.Definitions);
            long beforeHash = ComputeInventoryHash(store, RuntimeFrame.Zero);
            var snapshot = participant.CaptureSnapshot(RuntimeFrame.Zero);
            var json = Newtonsoft.Json.Linq.JObject.Parse(snapshot.PayloadJson);
            if (schemaMismatch) json["schemaVersion"] = 2;
            else json["nextItemInstanceId"] = 0;
            var invalid = new RuntimeCustomState(3302, participant.ParticipantId,
                InventoryNetworkSnapshotParticipant.CustomStateSchemaVersion, json.ToString());
            Assert.IsFalse(participant.ApplySnapshot(RuntimeFrame.Zero, invalid).Success);
            Assert.AreEqual(beforeHash, ComputeInventoryHash(store, RuntimeFrame.Zero));
            Assert.AreEqual(2, store.NextItemInstanceId);
        }

        private static InventoryService CreateInventory(
            int ownerStableId,
            out InventoryStore store,
            out InventoryContainer bag,
            out InventoryContainer chest,
            out ItemDefinition herb)
        {
            var definitions = new ItemDefinitionRegistry();
            herb = ItemDefinitionTests.CreateDefinition(1001, "item.herb", ItemKind.Material, 20);
            definitions.Register(herb);
            store = new InventoryStore();
            bag = new InventoryContainer(new InventoryContainerId(1), new InventoryOwnerId(ownerStableId), 2, "bag");
            chest = new InventoryContainer(new InventoryContainerId(2), new InventoryOwnerId(ownerStableId), 2, "chest");
            store.Register(bag);
            store.Register(chest);
            return new InventoryService(definitions, store);
        }

        private static InventoryService CreateOrderedInventory(
            ItemDefinitionRegistry definitions,
            out InventoryStore store,
            int[] registrationOrder)
        {
            store = new InventoryStore();
            for (int i = 0; i < registrationOrder.Length; i++)
            {
                int containerId = registrationOrder[i];
                store.Register(new InventoryContainer(
                    new InventoryContainerId(containerId),
                    new InventoryOwnerId(SampleOwnerIds.Hero),
                    slotCount: 2,
                    stableId: "container." + containerId));
            }

            return new InventoryService(definitions, store);
        }

        private static int GetStackCount(InventoryStore store, InventoryContainerId containerId, int slotIndex)
        {
            Assert.IsTrue(store.TryGet(containerId, out InventoryContainer container));
            return container.GetStack(slotIndex).Count;
        }

        private static long ComputeInventoryHash(InventoryStore store, RuntimeFrame frame)
        {
            return new RuntimeHashCombiner(new[] { new InventoryHashContributor(store) }).ComputeHash(frame);
        }
    }

    internal static class FixtureIds_InventoryNetworkTests
    {
        public const int Id_owner_hero = 1;

        public const int Id_net_move = 1;
        public const int Id_runner_move = 2;
    }
}
