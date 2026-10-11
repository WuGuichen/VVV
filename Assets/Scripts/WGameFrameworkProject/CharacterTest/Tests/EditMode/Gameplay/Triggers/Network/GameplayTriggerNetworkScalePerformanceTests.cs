using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.NetworkBridge;
using MxFramework.Network;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityApplication = UnityEngine.Application;

namespace MxFramework.Tests.Gameplay.Triggers.Network
{
    public sealed class GameplayTriggerNetworkScalePerformanceTests
    {
        private const int WarmupFrames = 120;
        private const int MeasuredFrames = 480;
        private const int DefinitionCount = 64;
        private const int InstanceCount = 256;
        private const int RelationCount = 2048;
        private const int SignalsPerFrame = 64;
        private const int CandidatesPerFrame = 512;
        private const int ActionsPerFrame = 256;
        private const int ReliableOutboxCount = 128;
        private const int SnapshotTotalLimit = 524288;
        private const int Seed = 0x0739194B;

        [Test]
        public void FrozenProfile_RealBoundedAuthorityAndRollbackMeetApprovedBudget()
        {
            ScaleFixture fixture = ScaleFixture.Create();
            fixture.InitializeRelations();
            fixture.InitializeNetworkAuthority();
            Assert.AreEqual(DefinitionCount, fixture.Catalog.DefinitionCount);
            Assert.AreEqual(InstanceCount, fixture.Runtime.InstanceCount);
            Assert.AreEqual(RelationCount, fixture.Runtime.RelationCount);
            Assert.AreEqual(512, fixture.World.CountAlive);

            var elapsedTicks = new long[MeasuredFrames];
            long firstFrame = fixture.NextFrame;
            for (int i = 0; i < WarmupFrames; i++)
                fixture.RunSignalFrame(firstFrame + i);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long heapStart = GC.GetTotalMemory(false);
            long heapPeak = heapStart;
            long triggerOwnedAllocated = 0L;
            long bridgeWireRollbackAllocated = 0L;
            long infrastructureAllocated = 0L;
            for (int i = 0; i < MeasuredFrames; i++)
            {
                long frameValue = firstFrame + WarmupFrames + i;
                long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                fixture.DrainReliableOutbox(frameValue);
                long allocationAfterInfrastructure = GC.GetAllocatedBytesForCurrentThread();
                fixture.ReceiveBoundedAuthority(frameValue);
                long allocationAfterReceive = GC.GetAllocatedBytesForCurrentThread();
                fixture.RunSignalAuthorityFrame(frameValue);
                long allocationAfterTrigger = GC.GetAllocatedBytesForCurrentThread();
                fixture.RecordRollback(frameValue);
                long allocationAfterRollback = GC.GetAllocatedBytesForCurrentThread();
                elapsedTicks[i] = Stopwatch.GetTimestamp() - start;
                infrastructureAllocated += allocationAfterInfrastructure - allocationBefore;
                bridgeWireRollbackAllocated += allocationAfterReceive - allocationAfterInfrastructure;
                bridgeWireRollbackAllocated += allocationAfterRollback - allocationAfterTrigger;
                triggerOwnedAllocated += allocationAfterTrigger - allocationAfterReceive;
                long heap = GC.GetTotalMemory(false);
                if (heap > heapPeak)
                    heapPeak = heap;
            }

            Assert.AreEqual(SignalsPerFrame, fixture.Runtime.OccurrenceCount);
            Assert.AreEqual(ActionsPerFrame, fixture.Runtime.ActivationCount);
            Assert.AreEqual(ActionsPerFrame, fixture.Runtime.IntentCount);
            Assert.AreEqual(ReliableOutboxCount, fixture.Runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(CandidatesPerFrame, fixture.Runtime.CandidateCount);
            Assert.AreEqual(128, fixture.RollbackHistoryCount);
            Assert.AreEqual(0L, triggerOwnedAllocated, "Trigger-owned steady authority path must allocate zero bytes after warm-up.");
            Assert.AreEqual(0L, bridgeWireRollbackAllocated, "Bounded wire receive and rollback record must allocate zero bytes after warm-up.");
            Assert.LessOrEqual(
                infrastructureAllocated,
                1024L * 1024L,
                "Measured runtime infrastructure allocations exceed the approved 1 MiB budget.");

            int snapshotBytes = fixture.CaptureSnapshotTotalBytes();
            Assert.LessOrEqual(snapshotBytes, SnapshotTotalLimit);

            Array.Sort(elapsedTicks);
            double tickToMilliseconds = 1000d / Stopwatch.Frequency;
            double median = ((elapsedTicks[239] + elapsedTicks[240]) * 0.5d) * tickToMilliseconds;
            double p95 = elapsedTicks[455] * tickToMilliseconds;
            double max = elapsedTicks[MeasuredFrames - 1] * tickToMilliseconds;
            long heapDelta = heapPeak - heapStart;

            WriteSummary(
                median,
                p95,
                max,
                triggerOwnedAllocated,
                bridgeWireRollbackAllocated,
                infrastructureAllocated,
                heapDelta,
                snapshotBytes,
                fixture);
            Assert.LessOrEqual(median, 4d);
            Assert.LessOrEqual(p95, 6d);
            Assert.LessOrEqual(max, 10d);
            Assert.LessOrEqual(heapDelta, 4L * 1024L * 1024L);
        }

        [Test]
        public void ProtocolWirePeerAndRecordCapacitiesCoverInvalidMinimumAndNMinusOneNPlusOne()
        {
            var peers = new NetworkBoundedPeerSet();
            Assert.AreEqual(NetworkBoundedErrorCode.InvalidArgument, peers.TryAdd(0).ErrorCode);
            for (uint peer = 1; peer <= 7; peer++)
                Assert.IsTrue(peers.TryAdd(peer).Success);
            Assert.AreEqual(7, peers.Count);
            Assert.IsTrue(peers.TryAdd(8).Success);
            Assert.AreEqual(8, peers.Count);
            Assert.AreEqual(NetworkBoundedErrorCode.CapacityExceeded, peers.TryAdd(9).ErrorCode);
            long peerAllocationBefore = GC.GetAllocatedBytesForCurrentThread();
            NetworkBoundedResult peerOverflow = peers.TryAdd(9);
            long peerOverflowAllocated = GC.GetAllocatedBytesForCurrentThread() - peerAllocationBefore;
            Assert.AreEqual(NetworkBoundedErrorCode.CapacityExceeded, peerOverflow.ErrorCode);
            Assert.AreEqual(8, peers.Count);
            Assert.AreEqual(0L, peerOverflowAllocated);

            GameplayTriggerNetworkProfile profile = CreateCodecProfile();
            RuntimeCommand[] records = CreateCodecRecords(65);
            var payload = new byte[GameplayTriggerNetworkCommandCodec.MaximumRuntimePayloadBytes];
            Assert.AreEqual(GameplayTriggerNetworkErrorCode.InvalidPacket,
                GameplayTriggerNetworkCommandCodec.TryEncodeRuntimePayload(
                    profile, RuntimeFrame.Zero, 0, ReadOnlySpan<RuntimeCommand>.Empty, payload, out _).ErrorCode);
            Assert.IsTrue(GameplayTriggerNetworkCommandCodec.TryEncodeRuntimePayload(
                profile, RuntimeFrame.Zero, 0, records.AsSpan(0, 63), payload, out int nMinusOneBytes).Success);
            Assert.AreEqual(104 + (63 * 116), nMinusOneBytes);
            Assert.IsTrue(GameplayTriggerNetworkCommandCodec.TryEncodeRuntimePayload(
                profile, RuntimeFrame.Zero, 0, records.AsSpan(0, 64), payload, out int nBytes).Success);
            Assert.AreEqual(104 + (64 * 116), nBytes);
            Assert.AreEqual(GameplayTriggerNetworkErrorCode.InvalidPacket,
                GameplayTriggerNetworkCommandCodec.TryEncodeRuntimePayload(
                    profile, RuntimeFrame.Zero, 0, records, payload, out _).ErrorCode);
            long recordAllocationBefore = GC.GetAllocatedBytesForCurrentThread();
            GameplayTriggerNetworkResult recordOverflow = GameplayTriggerNetworkCommandCodec.TryEncodeRuntimePayload(
                profile, RuntimeFrame.Zero, 0, records, payload, out _);
            long recordOverflowAllocated = GC.GetAllocatedBytesForCurrentThread() - recordAllocationBefore;
            Assert.AreEqual(GameplayTriggerNetworkErrorCode.InvalidPacket, recordOverflow.ErrorCode);
            Assert.AreEqual(0L, recordOverflowAllocated);

            AssertWireTotalBoundary(NetworkMessageKind.BoundedRuntimeCommands, 8192);
            AssertWireTotalBoundary(NetworkMessageKind.BoundedStateSnapshot, 524288);
        }

        [Test]
        public void PendingHistoryAndGameplayEntityCapacitiesCoverNMinusOneNPlusOneWithoutMutation()
        {
            RuntimeCommand[] commands = CreateCodecRecords(129);
            RuntimeCommandBuffer commandBuffer = RuntimeCommandBuffer.CreateBounded(128);
            var sources = new RuntimeCommandSourceRegistry(1);
            Assert.IsTrue(sources.TryRegister(1, RuntimeCommandSourceKind.Exogenous, new[] { 100 }, out _));
            var gate = new RuntimeCommandSubmissionGate(sources, commandBuffer, RuntimeCommandIngressKind.External);
            Assert.IsTrue(gate.SubmitBatch(commands.AsSpan(0, 127)).Success);
            Assert.AreEqual(127, commandBuffer.PendingCount);
            Assert.IsTrue(gate.Submit(commands[127]).Success);
            Assert.AreEqual(128, commandBuffer.PendingCount);
            Assert.IsFalse(gate.Submit(commands[128]).Success);
            long pendingAllocationBefore = GC.GetAllocatedBytesForCurrentThread();
            RuntimeCommandValidationResult pendingOverflow = gate.Submit(commands[128]);
            long pendingOverflowAllocated = GC.GetAllocatedBytesForCurrentThread() - pendingAllocationBefore;
            Assert.IsFalse(pendingOverflow.Success);
            Assert.AreEqual(RuntimeCommandErrorCode.PendingCapacityExceeded, pendingOverflow.Error.Code);
            Assert.AreEqual(128, commandBuffer.PendingCount);
            Assert.AreEqual(0L, pendingOverflowAllocated);

            Assert.Throws<ArgumentOutOfRangeException>(() => new NetworkBoundedRollbackHistory(0, 1));
            var history = new NetworkBoundedRollbackHistory(128, 1);
            history.Register(new ByteRollbackParticipant());
            history.Seal();
            for (int frame = 0; frame < 127; frame++)
                Assert.IsTrue(history.TryRecord(new RuntimeFrame(frame)).Success);
            Assert.AreEqual(127, history.Count);
            Assert.IsTrue(history.TryRecord(new RuntimeFrame(127)).Success);
            Assert.AreEqual(128, history.Count);
            Assert.IsTrue(history.TryRecord(new RuntimeFrame(128)).Success);
            Assert.AreEqual(128, history.Count);
            Assert.AreEqual(NetworkBoundedErrorCode.HistoryMissing, history.TryRestore(RuntimeFrame.Zero).ErrorCode);
            Assert.IsTrue(history.TryRestore(new RuntimeFrame(128)).Success);
            long historyAllocationBefore = GC.GetAllocatedBytesForCurrentThread();
            NetworkBoundedResult historyOverflow = history.TryRecord(new RuntimeFrame(129));
            long historyOverflowAllocated = GC.GetAllocatedBytesForCurrentThread() - historyAllocationBefore;
            Assert.IsTrue(historyOverflow.Success);
            Assert.AreEqual(128, history.Count);
            Assert.AreEqual(0L, historyOverflowAllocated);

            AssertEntityCaptureBoundary(511, expectSuccess: true);
            AssertEntityCaptureBoundary(512, expectSuccess: true);
            AssertEntityCaptureBoundary(513, expectSuccess: false);
        }

        [Test]
        public void FullSnapshotParticipantCapacityRequiresExactlyGameplayAndTrigger()
        {
            ScaleFixture fixture = ScaleFixture.Create();
            fixture.InitializeRelations();
            fixture.InitializeNetworkAuthority();
            Assert.AreEqual(GameplayTriggerNetworkErrorCode.InvalidPacket, fixture.ApplySnapshotWithParticipantCount(1).ErrorCode);
            Assert.AreEqual(GameplayTriggerNetworkErrorCode.InvalidPacket, fixture.ApplySnapshotWithParticipantCount(3).ErrorCode);
            Assert.IsTrue(fixture.ApplySnapshotWithParticipantCount(2).Success);
        }

        private static GameplayTriggerNetworkProfile CreateCodecProfile()
        {
            return new GameplayTriggerNetworkProfile(
                11,
                22,
                33,
                new GameplayTriggerRuntimeOptions(1, 64, 2048, 64, 512, 4, 256, 256, 128, 1, 0, 128),
                44,
                NetworkHashComparability.FullComparable,
                Array.Empty<GameplayTriggerNetworkSourcePolicy>());
        }

        private static RuntimeCommand[] CreateCodecRecords(int count)
        {
            var records = new RuntimeCommand[count];
            for (int i = 0; i < count; i++)
            {
                records[i] = new RuntimeCommand(
                    RuntimeFrame.Zero,
                    sourceId: 1,
                    commandId: 100,
                    targetId: 1,
                    sequence: i,
                    envelope: new RuntimeCommandEnvelope(
                        1,
                        1,
                        RuntimeCommandProvenanceKind.Exogenous,
                        data0: i + 1L));
            }
            return records;
        }

        private static void AssertWireTotalBoundary(NetworkMessageKind kind, int totalLimit)
        {
            int nMinusOnePayloadBytes = totalLimit - NetworkBoundedWire.HeaderBytes - 1;
            int nPayloadBytes = totalLimit - NetworkBoundedWire.HeaderBytes;
            var nMinusOnePayload = new byte[nMinusOnePayloadBytes];
            var nPayload = new byte[nPayloadBytes];
            var nPlusOnePayload = new byte[nPayloadBytes + 1];
            var destination = new byte[totalLimit + 1];
            Assert.IsTrue(NetworkBoundedWire.TryEncode(
                CreateWireHeader(kind, nMinusOnePayloadBytes), nMinusOnePayload, destination, out int nMinusOne).Success);
            Assert.AreEqual(totalLimit - 1, nMinusOne);
            Assert.IsTrue(NetworkBoundedWire.TryEncode(
                CreateWireHeader(kind, nPayloadBytes), nPayload, destination, out int n).Success);
            Assert.AreEqual(totalLimit, n);
            Assert.AreEqual(NetworkBoundedErrorCode.PacketTooLarge,
                NetworkBoundedWire.TryEncode(
                    CreateWireHeader(kind, nPlusOnePayload.Length), nPlusOnePayload, destination, out _).ErrorCode);
            long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
            NetworkBoundedResult overflow = NetworkBoundedWire.TryEncode(
                CreateWireHeader(kind, nPlusOnePayload.Length), nPlusOnePayload, destination, out _);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
            Assert.AreEqual(NetworkBoundedErrorCode.PacketTooLarge, overflow.ErrorCode);
            Assert.AreEqual(0L, allocated);
        }

        private static NetworkBoundedPacketHeader CreateWireHeader(NetworkMessageKind kind, int payloadBytes)
        {
            return new NetworkBoundedPacketHeader(kind, 1, 1, 2, 0, 0, payloadBytes);
        }

        private static void AssertEntityCaptureBoundary(int entityCount, bool expectSuccess)
        {
            var world = new GameplayComponentWorld();
            for (int i = 0; i < entityCount; i++)
                world.CreateEntity();
            var state = new GameplayComponentWorldNetworkState(world, 512);
            var bytes = new byte[state.MaximumEncodedBytes];
            GameplayComponentNetworkStateResult warm = state.TryCapture(bytes, out int written);
            long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
            GameplayComponentNetworkStateResult measured = state.TryCapture(bytes, out _);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
            Assert.AreEqual(expectSuccess, warm.Success);
            Assert.AreEqual(expectSuccess, measured.Success);
            Assert.AreEqual(expectSuccess ? 32 + (entityCount * 8) : 0, written);
            Assert.AreEqual(expectSuccess ? GameplayComponentNetworkStateErrorCode.None : GameplayComponentNetworkStateErrorCode.CapacityExceeded,
                measured.ErrorCode);
            Assert.AreEqual(0L, allocated);
        }

        private static void WriteSummary(
            double median,
            double p95,
            double max,
            long triggerOwnedAllocated,
            long bridgeWireRollbackAllocated,
            long infrastructureAllocated,
            long heapDelta,
            int snapshotBytes,
            ScaleFixture fixture)
        {
            string json = "{\n"
                + "  \"seed\": \"0x0739194B\",\n"
                + "  \"warmupFrames\": 120,\n"
                + "  \"measuredFrames\": 480,\n"
                + "  \"medianMs\": " + median.ToString("F6", CultureInfo.InvariantCulture) + ",\n"
                + "  \"p95Ms\": " + p95.ToString("F6", CultureInfo.InvariantCulture) + ",\n"
                + "  \"maxMs\": " + max.ToString("F6", CultureInfo.InvariantCulture) + ",\n"
                + "  \"triggerOwnedAllocatedBytes\": " + triggerOwnedAllocated + ",\n"
                + "  \"bridgeBoundedWireRollbackAllocatedBytes\": " + bridgeWireRollbackAllocated + ",\n"
                + "  \"infrastructureAllocatedBytes\": " + infrastructureAllocated + ",\n"
                + "  \"managedHeapPeakDeltaBytes\": " + heapDelta + ",\n"
                + "  \"snapshotTotalBytes\": " + snapshotBytes + ",\n"
                + "  \"highWater\": {\n"
                + "    \"recordsPerFrame\": " + fixture.NetworkRecordHighWater + ",\n"
                + "    \"relations\": " + fixture.RelationHighWater + ",\n"
                + "    \"candidates\": " + fixture.CandidateHighWater + ",\n"
                + "    \"actions\": " + fixture.ActionHighWater + ",\n"
                + "    \"reliableOutbox\": " + fixture.ReliableOutboxHighWater + ",\n"
                + "    \"historyFrames\": " + fixture.RollbackHistoryCount + ",\n"
                + "    \"participants\": 2,\n"
                + "    \"snapshotBytes\": " + snapshotBytes + "\n"
                + "  }\n"
                + "}\n";
            string projectPath = Path.GetDirectoryName(UnityApplication.dataPath);
            string tempPath = Path.Combine(projectPath, "Temp", "GameplayTriggerNetworkScaleSummary.json");
            string retainedPath = Path.Combine(projectPath, "Library", "GameplayTriggerNetworkScaleSummary.json");
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath));
            File.WriteAllText(tempPath, json);
            File.WriteAllText(retainedPath, json);
            TestContext.Out.WriteLine(json);
        }

        private sealed class ScaleFixture
        {
            private const int ImmediateActionKind = 7001;
            private const int ReliableActionKind = 7002;
            private readonly RuntimeHost _host;
            private readonly GameplayTriggerSignal[] _signals;
            private BoundedAuthorityHarness _network;
            private GameplayTriggerNetworkStateBundle _stateBundle;
            private GameplayTriggerNetworkRollbackResync _rollback;
            private RuntimeFrame _lastFrame;

            private ScaleFixture(
                GameplayTriggerCatalog catalog,
                GameplayTriggerRuntime runtime,
                RuntimeHost host,
                GameplayComponentWorld world)
            {
                Catalog = catalog;
                Runtime = runtime;
                World = world;
                _host = host;
                _signals = new GameplayTriggerSignal[SignalsPerFrame];
                int signalIndex = 0;
                for (int client = 0; client < BoundedAuthorityHarness.ClientCount; client++)
                {
                    for (int item = 0; item < BoundedAuthorityHarness.SignalsPerClient; item++)
                    {
                        _signals[signalIndex++] = BoundedAuthorityHarness.CreateSignal(client, item);
                    }
                }
                Array.Sort(_signals, CompareSignals);
            }

            public GameplayTriggerCatalog Catalog { get; }
            public GameplayTriggerRuntime Runtime { get; }
            public GameplayComponentWorld World { get; }
            public long NextFrame { get; private set; }
            public int NetworkRecordHighWater { get; private set; }
            public int RelationHighWater { get; private set; }
            public int CandidateHighWater { get; private set; }
            public int ActionHighWater { get; private set; }
            public int ReliableOutboxHighWater { get; private set; }
            public int RollbackHistoryCount => _rollback != null ? _rollback.HistoryCount : 0;

            public static ScaleFixture Create()
            {
                var sources = new GameplayTriggerSourceDefinition[DefinitionCount];
                var definitions = new GameplayTriggerDefinition[DefinitionCount];
                int routeId = 1;
                for (int i = 0; i < DefinitionCount; i++)
                {
                    var sourceId = new GameplayTriggerSourceDefinitionId(i + 1);
                    if (i == 0)
                    {
                        sources[i] = new GameplayTriggerSourceDefinition(
                            sourceId,
                            GameplayTriggerSourceKind.Signal,
                            GameplayTriggerSignalOrdering.Ordered);
                        var routes = new GameplayTriggerRouteDefinition[8];
                        for (int routeIndex = 0; routeIndex < routes.Length; routeIndex++)
                        {
                            bool accepted = routeIndex < 4;
                            int actionKind = routeIndex < 2
                                ? ImmediateActionKind
                                : ReliableActionKind;
                            routes[routeIndex] = new GameplayTriggerRouteDefinition(
                                new GameplayTriggerRouteId(routeId++),
                                GameplayTriggerOccurrenceKind.Pulsed,
                                accepted
                                    ? GameplayTriggerConditionProgram.Always
                                    : new GameplayTriggerConditionProgram(new[]
                                    {
                                        new GameplayTriggerConditionInstruction(
                                            GameplayTriggerConditionOperation.PushFalse)
                                    }),
                                new[]
                                {
                                    new GameplayTriggerActionInstruction(
                                        new GameplayTriggerActionKindId(actionKind),
                                        GameplayTriggerTargetSelector.Subject)
                                });
                        }
                        definitions[i] = new GameplayTriggerDefinition(
                            new GameplayTriggerDefinitionId(i + 1),
                            1,
                            sourceId,
                            routes);
                    }
                    else
                    {
                        sources[i] = new GameplayTriggerSourceDefinition(
                            sourceId,
                            GameplayTriggerSourceKind.Occupancy);
                        var route = new GameplayTriggerRouteDefinition(
                            new GameplayTriggerRouteId(routeId++),
                            GameplayTriggerOccurrenceKind.Entered,
                            new GameplayTriggerConditionProgram(new[]
                            {
                                new GameplayTriggerConditionInstruction(
                                    GameplayTriggerConditionOperation.PushFalse)
                            }),
                            new[]
                            {
                                new GameplayTriggerActionInstruction(
                                    new GameplayTriggerActionKindId(ImmediateActionKind),
                                    GameplayTriggerTargetSelector.Owner)
                            });
                        definitions[i] = new GameplayTriggerDefinition(
                            new GameplayTriggerDefinitionId(i + 1),
                            1,
                            sourceId,
                            new[] { route });
                    }
                }

                var bindings = new GameplayTriggerBinding[InstanceCount];
                bindings[0] = CreateBinding(1, 1);
                for (int i = 1; i < bindings.Length; i++)
                {
                    int definitionId = 2 + ((i - 1) % (DefinitionCount - 1));
                    bindings[i] = CreateBinding(i + 1, definitionId);
                }

                var immediate = new Sink(ImmediateActionKind, GameplayTriggerDeliveryContract.SameFrameReserved);
                var reliable = new Sink(ReliableActionKind, GameplayTriggerDeliveryContract.ReliableDeferred);
                Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                    sources,
                    definitions,
                    bindings,
                    new IGameplayTriggerActionSink[] { immediate, reliable },
                    out GameplayTriggerCatalog catalog,
                    out GameplayTriggerCatalogValidationResult validation),
                    validation.ErrorCode.ToString());

                var runtime = new GameplayTriggerRuntime(
                    catalog,
                    new GameplayTriggerRuntimeOptions(
                        maxInputBatches: 1,
                        maxInputItems: SignalsPerFrame,
                        maxRelations: RelationCount,
                        maxOccurrences: SignalsPerFrame,
                        maxCandidates: CandidatesPerFrame,
                        maxRouteStates: 4,
                        maxActivations: ActionsPerFrame,
                        maxIntents: ActionsPerFrame,
                        maxPreparedActions: ReliableOutboxCount,
                        maxConditionStackDepth: 1,
                        maxDeferredRecords: 0,
                        maxReliableOutboxRecords: ReliableOutboxCount));
                var transfer = new AcceptingTransfer(
                    new GameplayTriggerActionKindId(ReliableActionKind));
                var outbox = new GameplayTriggerReliableOutboxModule(
                    runtime,
                    new IGameplayTriggerReliableTargetTransfer[] { transfer });
                var host = new RuntimeHost();
                host.RegisterModule(outbox);
                host.RegisterModule(new TargetModule());
                host.Initialize();
                host.Start();
                var world = new GameplayComponentWorld();
                for (int i = 0; i < 512; i++)
                    world.CreateEntity();
                return new ScaleFixture(catalog, runtime, host, world);
            }

            public void InitializeRelations()
            {
                var subjects = new GameplayEntityId[9];
                for (int i = 0; i < subjects.Length; i++)
                    subjects[i] = new GameplayEntityId(i + 1, 1);

                for (int bindingIndex = 1; bindingIndex < InstanceCount; bindingIndex++)
                {
                    int count = bindingIndex <= 8 ? 9 : 8;
                    var frame = new RuntimeFrame(NextFrame++);
                    _host.Tick(frame.Value, 1d / 60d);
                    Assert.IsTrue(Runtime.TrySubmitOccupancy(
                        frame,
                        new GameplayTriggerSourceBindingId(bindingIndex + 1),
                        subjects.AsSpan(0, count),
                        out GameplayTriggerResult submit),
                        submit.ErrorCode.ToString());
                    Assert.IsTrue(Runtime.Evaluate(frame).Success);
                }
            }

            public void InitializeNetworkAuthority()
            {
                var facts = new AuthorityFacts();
                var sourceRows = new[]
                {
                    new NetworkBoundedSourceAuthorizationRow(
                        BoundedAuthorityHarness.NetworkSourceId,
                        allowedCommandMask: 1,
                        observationMask: (int)GameplayTriggerNetworkObservationMask.Signal,
                        ownershipPolicy: 1)
                };
                var participants = new[]
                {
                    new NetworkBoundedParticipantDescriptor(
                        GameplayTriggerNetworkIds.GameplayParticipantTypeId,
                        GameplayTriggerNetworkIds.ParticipantSchema,
                        GameplayTriggerNetworkIds.GameplayRestoreOrder),
                    new NetworkBoundedParticipantDescriptor(
                        GameplayTriggerNetworkIds.TriggerParticipantTypeId,
                        GameplayTriggerNetworkIds.ParticipantSchema,
                        GameplayTriggerNetworkIds.TriggerRestoreOrder)
                };
                long profileHash = NetworkBoundedProfileHash.Compute(
                    facts.IdentityHash,
                    NetworkHashComparability.FullComparable,
                    sourceRows,
                    participants);
                var profile = new GameplayTriggerNetworkProfile(
                    Catalog.DefinitionCatalogHash,
                    Catalog.BindingCatalogHash,
                    Catalog.GameplayTriggerCatalogHash,
                    Runtime.AuthorityOptions,
                    profileHash,
                    NetworkHashComparability.FullComparable,
                    new[]
                    {
                        new GameplayTriggerNetworkSourcePolicy(
                            BoundedAuthorityHarness.NetworkSourceId,
                            GameplayTriggerNetworkObservationMask.Signal)
                    });
                _network = new BoundedAuthorityHarness(profile, facts);
                _stateBundle = new GameplayTriggerNetworkStateBundle(profile, World, Runtime, 512);
                _rollback = new GameplayTriggerNetworkRollbackResync(
                    new NetworkPredictionRollbackCoordinator(),
                    _stateBundle,
                    historyCapacity: 128);
            }

            public void RunSignalFrame(long frameValue)
            {
                DrainReliableOutbox(frameValue);
                ReceiveBoundedAuthority(frameValue);
                RunSignalAuthorityFrame(frameValue);
                RecordRollback(frameValue);
            }

            public void DrainReliableOutbox(long frameValue)
            {
                _host.Tick(frameValue, 1d / 60d);
            }

            public void ReceiveBoundedAuthority(long frameValue)
            {
                if (_network == null)
                    throw new InvalidOperationException("Network authority harness is not initialized.");
                _network.Receive(new RuntimeFrame(frameValue));
                if (_network.LastRecordCount > NetworkRecordHighWater)
                    NetworkRecordHighWater = _network.LastRecordCount;
            }

            public void RunSignalAuthorityFrame(long frameValue)
            {
                var frame = new RuntimeFrame(frameValue);
                GameplayTriggerResult submit;
                GameplayTriggerResult evaluate;
                bool submitted = Runtime.TrySubmitSignals(
                    frame,
                    new GameplayTriggerSourceBindingId(1),
                    GameplayTriggerSignalOrdering.Ordered,
                    _signals,
                    out submit);
                evaluate = submitted ? Runtime.Evaluate(frame) : submit;
                if (!submitted || !evaluate.Success)
                    throw new InvalidOperationException("Frozen scale frame failed.");
                _lastFrame = frame;
                if (Runtime.RelationCount > RelationHighWater) RelationHighWater = Runtime.RelationCount;
                if (Runtime.CandidateCount > CandidateHighWater) CandidateHighWater = Runtime.CandidateCount;
                if (Runtime.ActivationCount > ActionHighWater) ActionHighWater = Runtime.ActivationCount;
                if (Runtime.ReliableOutboxRecordCount > ReliableOutboxHighWater)
                    ReliableOutboxHighWater = Runtime.ReliableOutboxRecordCount;
            }

            public void RecordRollback(long frameValue)
            {
                if (_rollback == null || !_rollback.TryRecord(new RuntimeFrame(frameValue)).Success)
                    throw new InvalidOperationException("Frozen bounded rollback record failed.");
            }

            public int CaptureSnapshotTotalBytes()
            {
                if (_stateBundle == null)
                    throw new InvalidOperationException("Network state bundle is not initialized.");
                var payload = new byte[_stateBundle.MaximumSnapshotPayloadBytes];
                GameplayTriggerNetworkResult capture = _stateBundle.TryCaptureSnapshotPayload(
                    0,
                    _lastFrame,
                    _lastFrame,
                    0,
                    payload,
                    out int payloadBytes);
                if (!capture.Success)
                    throw new InvalidOperationException("Frozen bounded snapshot capture failed: " + capture.ErrorCode);
                return NetworkBoundedWire.HeaderBytes + payloadBytes;
            }

            public GameplayTriggerNetworkResult ApplySnapshotWithParticipantCount(ushort participantCount)
            {
                if (_stateBundle == null)
                    throw new InvalidOperationException("Network state bundle is not initialized.");
                var payload = new byte[_stateBundle.MaximumSnapshotPayloadBytes];
                RuntimeFrame frame = NextFrame > 0 ? new RuntimeFrame(NextFrame - 1L) : RuntimeFrame.Zero;
                GameplayTriggerNetworkResult capture = _stateBundle.TryCaptureSnapshotPayload(
                    0,
                    frame,
                    frame,
                    0,
                    payload,
                    out int payloadBytes);
                if (!capture.Success)
                    throw new InvalidOperationException("Frozen participant snapshot capture failed.");
                BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(114, 2), participantCount);
                return _stateBundle.TryApplySnapshotPayload(payload.AsSpan(0, payloadBytes), out _, out _);
            }

            private static GameplayTriggerBinding CreateBinding(int bindingId, int definitionId)
            {
                return new GameplayTriggerBinding(
                    new GameplayTriggerBindingId(bindingId),
                    new GameplayTriggerDefinitionId(definitionId),
                    new GameplayTriggerSourceBindingId(bindingId),
                    new GameplayEntityId(bindingId, 1));
            }

            private static int CompareSignals(GameplayTriggerSignal left, GameplayTriggerSignal right)
            {
                return left.Sequence.CompareTo(right.Sequence);
            }
        }

        private sealed class BoundedAuthorityHarness
        {
            public const int ClientCount = 4;
            public const int SignalsPerClient = 16;
            public const int NetworkSourceId = 77;
            private const ulong SessionHandle = 55;
            private const uint ServerPeerHandle = 1;

            private readonly GameplayTriggerNetworkProfile _profile;
            private readonly GameplayTriggerNetworkAuthorityIngress _ingress;
            private readonly RuntimeCommandBuffer _commandBuffer;
            private readonly List<RuntimeCommand> _drained = new List<RuntimeCommand>(SignalsPerFrame);
            private readonly GameplayTriggerSignal[][] _signals = new GameplayTriggerSignal[ClientCount][];
            private readonly RuntimeCommand[][] _commands = new RuntimeCommand[ClientCount][];
            private readonly int[] _networkOrder = new int[ClientCount];
            private readonly byte[] _payload = new byte[GameplayTriggerNetworkCommandCodec.MaximumRuntimePayloadBytes];
            private readonly byte[] _packet = new byte[NetworkBoundedWire.HeaderBytes + GameplayTriggerNetworkCommandCodec.MaximumRuntimePayloadBytes];
            private long _outerSequence;
            private long _packetSequence;
            private long _batchId = 1L;
            private uint _shuffleState = Seed;

            public BoundedAuthorityHarness(GameplayTriggerNetworkProfile profile, AuthorityFacts facts)
            {
                _profile = profile;
                _commandBuffer = RuntimeCommandBuffer.CreateBounded(SignalsPerFrame);
                var sources = new RuntimeCommandSourceRegistry(1);
                if (!sources.TryRegister(
                        NetworkSourceId,
                        RuntimeCommandSourceKind.Exogenous,
                        new[] { GameplayTriggerRuntimeCommandIds.SubmitSignalBatch },
                        out _))
                {
                    throw new InvalidOperationException("Frozen network source registration failed.");
                }
                _ingress = new GameplayTriggerNetworkAuthorityIngress(
                    profile,
                    facts,
                    facts,
                    new RuntimeCommandSubmissionGate(
                        sources,
                        _commandBuffer,
                        RuntimeCommandIngressKind.External));
                for (int client = 0; client < ClientCount; client++)
                {
                    _signals[client] = new GameplayTriggerSignal[SignalsPerClient];
                    _commands[client] = new RuntimeCommand[SignalsPerClient];
                    for (int item = 0; item < SignalsPerClient; item++)
                        _signals[client][item] = CreateSignal(client, item);
                }
            }

            public int LastRecordCount { get; private set; }

            public static GameplayTriggerSignal CreateSignal(int client, int item)
            {
                return new GameplayTriggerSignal(
                    signalId: 1,
                    subject: new GameplayEntityId((client * 32) + item + 1, 1),
                    payload0: (client * SignalsPerClient) + item,
                    sequence: (client * SignalsPerClient) + item + 1L);
            }

            /// <remarks>NoAlloc after construction.</remarks>
            public void Receive(RuntimeFrame frame)
            {
                for (int client = 0; client < ClientCount; client++)
                    _networkOrder[client] = client;
                ShuffleNetworkOrder();

                for (int orderIndex = 0; orderIndex < ClientCount; orderIndex++)
                {
                    int client = _networkOrder[orderIndex];
                    var metadata = new NetworkBoundedAuthenticatedMetadata(
                        SessionHandle,
                        (uint)(client + 2),
                        ServerPeerHandle,
                        NetworkBoundedPacketDirection.ClientToServer);
                    var context = new NetworkBoundedIngressContext(
                        metadata,
                        NetworkSessionRole.Server,
                        NetworkSessionStatus.Connected,
                        frame,
                        _profile.NetworkProfileHash);
                    if (!GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                            frame,
                            NetworkSourceId,
                            RuntimeCommandProvenanceKind.Exogenous,
                            new GameplayTriggerSourceBindingId(1),
                            GameplayTriggerSignalOrdering.Ordered,
                            _batchId++,
                            _signals[client],
                            _commands[client],
                            out int commandCount,
                            out _)
                        || commandCount != SignalsPerClient)
                    {
                        throw new InvalidOperationException("Frozen network command staging failed.");
                    }
                    GameplayTriggerNetworkResult payload = GameplayTriggerNetworkCommandCodec.TryEncodeRuntimePayload(
                        _profile,
                        frame,
                        _packetSequence,
                        _commands[client],
                        _payload,
                        out int payloadBytes);
                    if (!payload.Success)
                        throw new InvalidOperationException("Frozen network payload encode failed.");
                    var header = new NetworkBoundedPacketHeader(
                        NetworkMessageKind.BoundedRuntimeCommands,
                        SessionHandle,
                        (uint)(client + 2),
                        ServerPeerHandle,
                        _outerSequence,
                        frame.Value,
                        payloadBytes);
                    NetworkBoundedResult wire = NetworkBoundedWire.TryEncode(
                        header,
                        _payload.AsSpan(0, payloadBytes),
                        _packet,
                        out int packetBytes);
                    if (!wire.Success || !_ingress.TryAdmit(_packet.AsSpan(0, packetBytes), context).Success)
                        throw new InvalidOperationException("Frozen bounded authority admission failed.");
                    _outerSequence++;
                    _packetSequence++;
                }

                _drained.Clear();
                LastRecordCount = _commandBuffer.DrainForFrame(frame, _drained);
                if (LastRecordCount != SignalsPerFrame || _commandBuffer.PendingCount != 0)
                    throw new InvalidOperationException("Frozen network command drain failed.");
            }

            private void ShuffleNetworkOrder()
            {
                for (int i = ClientCount - 1; i > 0; i--)
                {
                    _shuffleState ^= _shuffleState << 13;
                    _shuffleState ^= _shuffleState >> 17;
                    _shuffleState ^= _shuffleState << 5;
                    int swap = (int)(_shuffleState % (uint)(i + 1));
                    int value = _networkOrder[i];
                    _networkOrder[i] = _networkOrder[swap];
                    _networkOrder[swap] = value;
                }
            }
        }

        private sealed class AuthorityFacts : IGameplayTriggerNetworkPeerDirectory, IGameplayTriggerNetworkOwnershipProvider
        {
            private readonly NetworkBoundedPeerSet _peers = new NetworkBoundedPeerSet();

            public AuthorityFacts()
            {
                for (uint peer = 2; peer < 2U + BoundedAuthorityHarness.ClientCount; peer++)
                {
                    if (!_peers.TryAdd(peer).Success)
                        throw new InvalidOperationException("Frozen peer directory initialization failed.");
                }
            }

            public long IdentityHash => 0x0739194B;

            public bool IsConnected(uint peerHandle)
            {
                return _peers.Contains(peerHandle);
            }

            public bool IsOwnedBy(uint peerHandle, GameplayEntityId entityId)
            {
                if (!IsConnected(peerHandle))
                    return false;
                int client = (int)peerHandle - 2;
                int firstIndex = (client * 32) + 1;
                return entityId.Generation == 1
                    && entityId.Index >= firstIndex
                    && entityId.Index < firstIndex + 32;
            }
        }

        private sealed class ByteRollbackParticipant : INetworkBoundedRollbackParticipant
        {
            public uint TypeId => 1;
            public ushort RestoreOrder => 1;
            public int StateBytes => 1;

            public NetworkBoundedResult TryCaptureRollbackState(RuntimeFrame frame, Span<byte> destination)
            {
                destination[0] = (byte)frame.Value;
                return NetworkBoundedResult.Succeeded();
            }

            public NetworkBoundedResult TryPrepareRollbackState(RuntimeFrame frame, ReadOnlySpan<byte> state)
            {
                return state.Length == 1 && state[0] == (byte)frame.Value
                    ? NetworkBoundedResult.Succeeded()
                    : NetworkBoundedResult.Failed(NetworkBoundedErrorCode.ParticipantValidationFailed);
            }
            public void CommitPreparedRollbackState() { }
            public void DiscardPreparedRollbackState() { }
        }

        private sealed class Sink : IGameplayTriggerActionSink
        {
            public Sink(int actionKindId, GameplayTriggerDeliveryContract delivery)
            {
                ActionKindId = new GameplayTriggerActionKindId(actionKindId);
                DeliveryContract = delivery;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract { get; }
            public bool TryPrepare(in GameplayTriggerActionPreparation preparation, out int token)
            {
                token = preparation.ActionIndex + 1;
                return true;
            }
            public void Commit(int token, in GameplayTriggerActionIntent intent) { }
            public void Abort(int token) { }
        }

        private sealed class AcceptingTransfer : IGameplayTriggerReliableTargetTransfer
        {
            public AcceptingTransfer(GameplayTriggerActionKindId actionKindId)
            {
                ActionKindId = actionKindId;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public string TargetModuleId => TargetModule.StableModuleId;
            public bool IsEnabled => true;
            public GameplayTriggerReliableTransferResult TryTransfer(
                RuntimeFrame currentFrame,
                in GameplayTriggerReliableOutboxRecord record)
            {
                return new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.Accepted);
            }
        }

        private sealed class TargetModule : RuntimeModule
        {
            public const string StableModuleId = "issue739.baseline.target";
            public TargetModule()
                : base(StableModuleId, RuntimeTickStage.Simulation, 0)
            {
            }
            public override void Tick(RuntimeTickContext context) { }
        }
    }
}
