using System;
using System.Collections.Generic;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerReplayPlaybackTests
    {
        private const string FrameworkVersion = "test-framework";
        private const string ResourceHash = "test-resources";
        private const int ExternalSourceId = 700;
        private const int DerivedSourceId = 701;
        private static readonly GameplayTriggerSourceBindingId SignalBinding =
            new GameplayTriggerSourceBindingId(100);
        private static readonly GameplayTriggerSourceBindingId SecondSignalBinding =
            new GameplayTriggerSourceBindingId(101);
        private static readonly GameplayTriggerSourceBindingId OccupancyBinding =
            new GameplayTriggerSourceBindingId(200);
        private static readonly GameplayTriggerSourceBindingId ScalarBinding =
            new GameplayTriggerSourceBindingId(300);

        [Test]
        public void HeaderContract_UsesInvariantFixedWidthCatalogIdentity()
        {
            const long hash = -1L;
            string configHash = GameplayTriggerReplayHeaderContract.FormatConfigHash(hash);
            RuntimeReplayHeader header = GameplayTriggerReplayHeaderContract.Create(
                FrameworkVersion,
                ResourceHash,
                hash,
                new RuntimeFrame(12L));

            Assert.AreEqual(
                "mxframework.gameplay.triggers.catalog/v2/ffffffffffffffff",
                configHash);
            Assert.AreEqual(GameplayTriggerReplayHeaderContract.CurrentSchemaVersion, header.SchemaVersion);
            Assert.AreEqual(configHash, header.ConfigHash);
            Assert.AreEqual(12L, header.StartFrame.Value);
        }

        [TestCase(HeaderFault.Schema)]
        [TestCase(HeaderFault.Framework)]
        [TestCase(HeaderFault.Config)]
        [TestCase(HeaderFault.Resources)]
        [TestCase(HeaderFault.StartFrameMax)]
        public void Reset_HeaderFaultMatrixRejectsBeforeFactory(HeaderFault fault)
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 1);
            RuntimeReplayHeader valid = CreateHeader(catalog, RuntimeFrame.Zero);
            RuntimeReplayHeader invalid = CreateHeaderFault(valid, fault);
            var factory = new TestFactory(catalog);
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                GameplayTriggerReplayException exception =
                    Assert.Throws<GameplayTriggerReplayException>(() =>
                        driver.Reset(invalid));

                Assert.AreEqual(GameplayTriggerReplayFailureCode.InvalidHeader, exception.Code);
                Assert.AreEqual(0, factory.CreateCount);
                Assert.IsNull(factory.Last);
                Assert.IsTrue(driver.IsFaulted);
                Assert.IsFalse(driver.HasActiveSession);
            }
        }

        [Test]
        public void Reset_DefaultHeaderDisposesActiveSessionBeforeRejectingNewBootstrap()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 1);
            var factory = new TestFactory(catalog);
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                driver.Reset(CreateHeader(catalog, RuntimeFrame.Zero));
                RuntimeHost oldHost = factory.Last.Host;

                GameplayTriggerReplayException exception =
                    Assert.Throws<GameplayTriggerReplayException>(() => driver.Reset(default));

                Assert.AreEqual(GameplayTriggerReplayFailureCode.InvalidHeader, exception.Code);
                Assert.AreEqual(RuntimeLifecycleState.Disposed, oldHost.State);
                Assert.AreEqual(1, factory.CreateCount);
                Assert.IsTrue(driver.IsFaulted);
                Assert.IsFalse(driver.HasActiveSession);
            }
        }

        [Test]
        public void FreshPlayback_RebuildsTrustedSignalFrameAndMatchesGenericRunnerHash()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 4);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);
            RuntimeCommand signal = CreateSignal(RuntimeFrame.Zero, sequence: 0L);
            long expectedHash = ProbeHash(
                new TestFactory(catalog),
                options,
                header,
                new[] { signal });
            var snapshot = new RuntimeReplaySnapshot(
                header,
                new[]
                {
                    new RuntimeReplayFrameRecord(
                        RuntimeFrame.Zero,
                        new[] { signal },
                        expectedHash,
                        string.Empty)
                });
            var factory = new TestFactory(catalog);
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                RuntimeReplayPlaybackResult result =
                    new RuntimeReplayPlaybackRunner().Play(snapshot, driver);

                Assert.IsTrue(result.Success, result.FailureMessage);
                Assert.AreEqual(1, result.FramesPlayed);
                Assert.IsTrue(driver.HasActiveSession);
                Assert.IsFalse(driver.IsFaulted);
                Assert.AreEqual(1L, driver.NextExpectedFrame.Value);
                Assert.AreEqual(1L, factory.Last.Host.TickCount);
                Assert.AreEqual(0, factory.Last.Buffer.PendingCount);
            }
        }

        [Test]
        public void FreshPlayback_RebuildsSignalOccupancyScalarThenEmptyFrameAgainstControl()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 3);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);
            RuntimeCommand[] firstFrame =
            {
                CreateSignal(RuntimeFrame.Zero, 0L),
                CreateOccupancy(RuntimeFrame.Zero, 1L),
                CreateScalar(RuntimeFrame.Zero, 2L)
            };
            long[] controlHashes = RunControlHashes(
                catalog,
                options,
                new[]
                {
                    firstFrame,
                    Array.Empty<RuntimeCommand>()
                });
            var snapshot = new RuntimeReplaySnapshot(
                header,
                new[]
                {
                    new RuntimeReplayFrameRecord(
                        RuntimeFrame.Zero,
                        firstFrame,
                        controlHashes[0],
                        string.Empty),
                    new RuntimeReplayFrameRecord(
                        new RuntimeFrame(1L),
                        Array.Empty<RuntimeCommand>(),
                        controlHashes[1],
                        string.Empty)
                });
            var factory = new TestFactory(catalog);

            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                RuntimeReplayPlaybackResult result =
                    new RuntimeReplayPlaybackRunner().Play(snapshot, driver);

                Assert.IsTrue(result.Success, result.FailureMessage);
                Assert.AreEqual(2, result.FramesPlayed);
                Assert.AreEqual(2L, factory.Last.Host.TickCount);
                Assert.AreEqual(0, factory.Last.Buffer.PendingCount);
                Assert.AreEqual(controlHashes[1], ComputeHash(
                    factory.Last.Runtime,
                    new RuntimeFrame(1L)));
            }
        }

        [Test]
        public void UnorderedSignal_ReversedPayloadOrderProducesIdenticalReplayHash()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 2);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);
            var first = new GameplayTriggerSignal(10, payload0: 100L, sequence: 10L);
            var second = new GameplayTriggerSignal(20, payload0: 200L, sequence: 20L);
            RuntimeCommand[] forward = CreateSignalBatch(
                RuntimeFrame.Zero,
                SignalBinding,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                batchId: 1L,
                new[] { first, second });
            RuntimeCommand[] reversed = CreateSignalBatch(
                RuntimeFrame.Zero,
                SignalBinding,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                batchId: 1L,
                new[] { second, first });

            long forwardHash = ProbeHash(new TestFactory(catalog), options, header, forward);
            long reversedHash = ProbeHash(new TestFactory(catalog), options, header, reversed);

            Assert.AreEqual(forwardHash, reversedHash);
        }

        [Test]
        public void OrderedSignal_ReconstructsItemOrderBeforeRuntimeSubmission()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(
                reliable: false,
                GameplayTriggerSignalOrdering.Ordered);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 2);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);
            RuntimeCommand[] encoded = CreateSignalBatch(
                RuntimeFrame.Zero,
                SignalBinding,
                GameplayTriggerSignalOrdering.Ordered,
                batchId: 1L,
                new[]
                {
                    new GameplayTriggerSignal(10, sequence: 10L),
                    new GameplayTriggerSignal(20, sequence: 20L)
                });
            RuntimeCommand[] recordedOutOfPayloadOrder =
            {
                encoded[1].WithSequence(0L),
                encoded[0].WithSequence(1L)
            };
            long expectedHash = ProbeHash(
                new TestFactory(catalog),
                options,
                header,
                recordedOutOfPayloadOrder);
            var factory = new TestFactory(catalog);

            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                driver.Reset(header);
                driver.RunFrame(new RuntimeReplayFrameRecord(
                    RuntimeFrame.Zero,
                    recordedOutOfPayloadOrder,
                    expectedHash,
                    string.Empty));

                Assert.AreEqual(2, factory.Last.Runtime.OccurrenceCount);
                Assert.AreEqual(10, factory.Last.Runtime.GetOccurrence(0).SignalId);
                Assert.AreEqual(10L, factory.Last.Runtime.GetOccurrence(0).OrderingValue);
                Assert.AreEqual(20, factory.Last.Runtime.GetOccurrence(1).SignalId);
                Assert.AreEqual(20L, factory.Last.Runtime.GetOccurrence(1).OrderingValue);
            }
        }

        [TestCase(FactoryMode.WrongSameIdModule)]
        [TestCase(FactoryMode.WrongSameIdRouter)]
        [TestCase(FactoryMode.WrongRuntimeSystem)]
        [TestCase(FactoryMode.MissingReliableOutbox)]
        [TestCase(FactoryMode.WrongSameIdOutbox)]
        [TestCase(FactoryMode.UndersizedCommandSystem)]
        [TestCase(FactoryMode.WrongGameplayPriority)]
        [TestCase(FactoryMode.WrongTriggerSystemPriority)]
        public void Reset_RejectsCompositionIdentityBeforeLifecycleOrTick(FactoryMode mode)
        {
            bool reliable = mode == FactoryMode.MissingReliableOutbox ||
                mode == FactoryMode.WrongSameIdOutbox;
            GameplayTriggerCatalog catalog = CreateCatalog(reliable);
            var factory = new TestFactory(catalog, mode);
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       CreateOptions(catalog, maxCommands: 4),
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                GameplayTriggerReplayException exception =
                    Assert.Throws<GameplayTriggerReplayException>(() =>
                        driver.Reset(CreateHeader(catalog, RuntimeFrame.Zero)));

                Assert.AreEqual(
                    GameplayTriggerReplayFailureCode.CompositionIdentityMismatch,
                    exception.Code);
                Assert.IsFalse(exception.HasFrame);
                Assert.AreEqual(-1, exception.CommandIndex);
                Assert.IsNull(exception.CheckpointError);
                Assert.IsTrue(driver.IsFaulted);
                Assert.IsFalse(driver.HasActiveSession);
                Assert.AreEqual(RuntimeLifecycleState.Disposed, factory.Last.Host.State);
                Assert.AreEqual(0L, factory.Last.Host.TickCount);
                Assert.AreEqual(0, factory.Last.LifecycleEvents.Count);
            }
        }

        [Test]
        public void FrameGap_FaultsDisposesAndRejectsFurtherFramesUntilReset()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 2);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);
            var factory = new TestFactory(catalog);
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                driver.Reset(header);
                GameplayTriggerReplayException gap =
                    Assert.Throws<GameplayTriggerReplayException>(() => driver.RunFrame(
                        new RuntimeReplayFrameRecord(
                            new RuntimeFrame(1L),
                            Array.Empty<RuntimeCommand>(),
                            0L,
                            string.Empty)));

                Assert.AreEqual(GameplayTriggerReplayFailureCode.FrameSequenceMismatch, gap.Code);
                Assert.IsTrue(driver.IsFaulted);
                Assert.IsFalse(driver.HasActiveSession);
                Assert.AreEqual(GameplayTriggerReplayFailureCode.FrameSequenceMismatch, driver.LastFailureCode);
                Assert.AreEqual(RuntimeLifecycleState.Disposed, factory.Last.Host.State);
                Assert.AreEqual(0L, factory.Last.Host.TickCount);
                Assert.AreEqual(1L, factory.Last.EventSource.NextSequence);
                Assert.AreEqual(0, factory.Last.Buffer.PendingCount);

                GameplayTriggerReplayException later =
                    Assert.Throws<GameplayTriggerReplayException>(() => driver.RunFrame(
                        new RuntimeReplayFrameRecord(
                            RuntimeFrame.Zero,
                            Array.Empty<RuntimeCommand>(),
                            0L,
                            string.Empty)));
                Assert.AreEqual(GameplayTriggerReplayFailureCode.InvalidDriverState, later.Code);
                Assert.AreEqual(GameplayTriggerReplayFailureCode.FrameSequenceMismatch, driver.LastFailureCode);

                driver.Reset(header);
                Assert.IsTrue(driver.HasActiveSession);
                Assert.IsFalse(driver.IsFaulted);
            }
        }

        [Test]
        public void DuplicateFrame_FaultsWithoutSecondSubmitTickHashOrSourceMutation()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 2);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);
            RuntimeCommand signal = CreateSignal(RuntimeFrame.Zero, 0L);
            long expectedHash = ProbeHash(
                new TestFactory(catalog),
                options,
                header,
                new[] { signal });
            var factory = new TestFactory(catalog);
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                driver.Reset(header);
                driver.RunFrame(new RuntimeReplayFrameRecord(
                    RuntimeFrame.Zero,
                    new[] { signal },
                    expectedHash,
                    string.Empty));
                long sequenceBefore = factory.Last.EventSource.NextSequence;
                long hashBefore = ComputeHash(factory.Last.Runtime, RuntimeFrame.Zero);

                GameplayTriggerReplayException duplicate =
                    Assert.Throws<GameplayTriggerReplayException>(() => driver.RunFrame(
                        new RuntimeReplayFrameRecord(
                            RuntimeFrame.Zero,
                            new[] { signal },
                            expectedHash,
                            string.Empty)));

                Assert.AreEqual(GameplayTriggerReplayFailureCode.FrameSequenceMismatch, duplicate.Code);
                Assert.IsTrue(driver.IsFaulted);
                Assert.IsFalse(driver.HasActiveSession);
                Assert.AreEqual(RuntimeLifecycleState.Disposed, factory.Last.Host.State);
                Assert.AreEqual(1L, factory.Last.Host.TickCount);
                Assert.AreEqual(sequenceBefore, factory.Last.EventSource.NextSequence);
                Assert.AreEqual(hashBefore, ComputeHash(factory.Last.Runtime, RuntimeFrame.Zero));
                Assert.AreEqual(0, factory.Last.Buffer.PendingCount);
            }
        }

        [Test]
        public void Admission_OrderAndSequenceFailuresOccurBeforeSubmitOrTick()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 4);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);

            AssertAdmissionFailure(
                catalog,
                options,
                header,
                new[]
                {
                    CreateSignal(RuntimeFrame.Zero, 0L),
                    CreateScalar(RuntimeFrame.Zero, 0L)
                },
                GameplayTriggerReplayFailureCode.InvalidCommandOrder,
                expectedCommandIndex: 1);

            AssertAdmissionFailure(
                catalog,
                options,
                header,
                new[]
                {
                    CreateScalar(RuntimeFrame.Zero, 0L),
                    CreateSignal(RuntimeFrame.Zero, 1L)
                },
                GameplayTriggerReplayFailureCode.InvalidCommandOrder,
                expectedCommandIndex: 1);
        }

        [Test]
        public void Admission_NPlusOneFailsBeforeSubmitOrTick()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 3);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);
            RuntimeCommand signal = CreateSignal(RuntimeFrame.Zero, 0L);

            AssertAdmissionFailure(
                catalog,
                options,
                header,
                new[]
                {
                    signal,
                    signal.WithSequence(1L),
                    signal.WithSequence(2L),
                    signal.WithSequence(3L)
                },
                GameplayTriggerReplayFailureCode.CommandCapacityExceeded,
                expectedCommandIndex: -1);
        }

        [Test]
        public void Admission_NMinusOneAndNBothTickSuccessfully()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 3);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);
            RuntimeCommand[] nMinusOne = CreateSignalBatch(
                RuntimeFrame.Zero,
                SignalBinding,
                batchId: 1L,
                itemCount: 2,
                firstRecordedSequence: 0L);
            RuntimeCommand[] n =
            {
                CreateSignal(RuntimeFrame.Zero, 0L),
                CreateOccupancy(RuntimeFrame.Zero, 1L),
                CreateScalar(RuntimeFrame.Zero, 2L)
            };
            long nMinusOneHash = ProbeHash(
                new TestFactory(catalog), options, header, nMinusOne);
            long nHash = ProbeHash(new TestFactory(catalog), options, header, n);

            var firstFactory = new TestFactory(catalog);
            using (var firstDriver = new GameplayTriggerReplayFrameDriver(
                       firstFactory, options, GameplayTriggerReplayBootstrapMode.Fresh))
            {
                firstDriver.Reset(header);
                firstDriver.RunFrame(new RuntimeReplayFrameRecord(
                    RuntimeFrame.Zero, nMinusOne, nMinusOneHash, string.Empty));
                Assert.AreEqual(1L, firstFactory.Last.Host.TickCount);
            }

            var secondFactory = new TestFactory(catalog);
            using (var secondDriver = new GameplayTriggerReplayFrameDriver(
                       secondFactory, options, GameplayTriggerReplayBootstrapMode.Fresh))
            {
                secondDriver.Reset(header);
                secondDriver.RunFrame(new RuntimeReplayFrameRecord(
                    RuntimeFrame.Zero, n, nHash, string.Empty));
                Assert.AreEqual(1L, secondFactory.Last.Host.TickCount);
            }
        }

        [TestCase(CommandFault.UnknownSource,
            GameplayTriggerReplayFailureCode.UnauthorizedCommand)]
        [TestCase(CommandFault.UnknownCommand,
            GameplayTriggerReplayFailureCode.UnauthorizedCommand)]
        [TestCase(CommandFault.RegisteredDerivedSource,
            GameplayTriggerReplayFailureCode.UnauthorizedCommand)]
        [TestCase(CommandFault.ExternalProvenanceMismatch,
            GameplayTriggerReplayFailureCode.UnauthorizedCommand)]
        [TestCase(CommandFault.LegacyLifecycle,
            GameplayTriggerReplayFailureCode.UnauthorizedCommand)]
        [TestCase(CommandFault.WrongFrame,
            GameplayTriggerReplayFailureCode.InvalidCommandPayload)]
        [TestCase(CommandFault.MalformedEnvelope,
            GameplayTriggerReplayFailureCode.InvalidCommandPayload)]
        [TestCase(CommandFault.IncompleteBatch,
            GameplayTriggerReplayFailureCode.InvalidCommandPayload)]
        [TestCase(CommandFault.MalformedOccupancyBatch,
            GameplayTriggerReplayFailureCode.InvalidCommandPayload)]
        [TestCase(CommandFault.IncompleteOccupancyBatch,
            GameplayTriggerReplayFailureCode.InvalidCommandPayload)]
        [TestCase(CommandFault.MalformedScalarBatch,
            GameplayTriggerReplayFailureCode.InvalidCommandPayload)]
        [TestCase(CommandFault.IncompleteScalarBatch,
            GameplayTriggerReplayFailureCode.InvalidCommandPayload)]
        [TestCase(CommandFault.DuplicateSourceSubmission,
            GameplayTriggerReplayFailureCode.InvalidCommandPayload)]
        public void Admission_CommandFaultMatrixFailsBeforeTick(
            CommandFault fault,
            GameplayTriggerReplayFailureCode expectedCode)
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 4);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);
            IReadOnlyList<RuntimeCommand> commands = CreateFaultedCommands(fault);

            AssertAdmissionFailure(
                catalog,
                options,
                header,
                commands,
                expectedCode,
                expectedCommandIndex: fault == CommandFault.DuplicateSourceSubmission ? 1 : 0);
        }

        [Test]
        public void Admission_SecondGateFailureRestoresWholeBufferSnapshot()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 4);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);
            var factory = new TestFactory(
                catalog,
                FactoryMode.Valid,
                new RejectScalarValidator());
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                driver.Reset(header);
                GameplayTriggerReplayException exception =
                    Assert.Throws<GameplayTriggerReplayException>(() => driver.RunFrame(
                        new RuntimeReplayFrameRecord(
                            RuntimeFrame.Zero,
                            new[]
                            {
                                CreateSignal(RuntimeFrame.Zero, 0L),
                                CreateScalar(RuntimeFrame.Zero, 1L)
                            },
                            0L,
                            string.Empty)));

                Assert.AreEqual(
                    GameplayTriggerReplayFailureCode.CommandAdmissionFailed,
                    exception.Code);
                Assert.AreEqual(1, exception.CommandIndex);
                Assert.AreEqual(0, factory.Last.Buffer.PendingCount);
                Assert.AreEqual(RuntimeFrame.Zero, factory.Last.Buffer.CurrentFrame);
                Assert.AreEqual(0L, factory.Last.Host.TickCount);
                Assert.IsTrue(driver.IsFaulted);
            }
        }

        [Test]
        public void TerminalFrame_MaxMinusOneTicksOnceAndMaxRecordNeverTicks()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 2);
            var start = new RuntimeFrame(long.MaxValue - 1L);
            RuntimeReplayHeader header = CreateHeader(catalog, start);
            long expectedHash = ProbeHash(
                new TestFactory(catalog),
                options,
                header,
                Array.Empty<RuntimeCommand>());
            var factory = new TestFactory(catalog);
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                driver.Reset(header);
                RuntimeReplayPlaybackFrameResult result = driver.RunFrame(
                    new RuntimeReplayFrameRecord(
                        start,
                        Array.Empty<RuntimeCommand>(),
                        expectedHash,
                        string.Empty));

                Assert.AreEqual(expectedHash, result.ActualResultHash);
                Assert.AreEqual(long.MaxValue, driver.NextExpectedFrame.Value);
                Assert.AreEqual(1L, factory.Last.Host.TickCount);

                GameplayTriggerReplayException exception =
                    Assert.Throws<GameplayTriggerReplayException>(() => driver.RunFrame(
                        new RuntimeReplayFrameRecord(
                            new RuntimeFrame(long.MaxValue),
                            Array.Empty<RuntimeCommand>(),
                            0L,
                            string.Empty)));

                Assert.AreEqual(GameplayTriggerReplayFailureCode.FrameExhausted, exception.Code);
                Assert.AreEqual(1L, factory.Last.Host.TickCount);
                Assert.AreEqual(long.MaxValue, factory.Last.Buffer.CurrentFrame.Value);
                Assert.IsTrue(driver.IsFaulted);
                Assert.IsFalse(driver.HasActiveSession);
                Assert.AreEqual(RuntimeLifecycleState.Disposed, factory.Last.Host.State);
            }
        }

        [Test]
        public void TypedMaxima_ArePerBatchScratchLimitsNotCumulativeFrameQuotas()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            var options = new GameplayTriggerReplayBootstrapOptions(
                FrameworkVersion,
                ResourceHash,
                catalog.GameplayTriggerCatalogHash,
                maxCommandsPerFrame: 10,
                maxSignalCommandItems: 5,
                maxOccupancyCommandItems: 8,
                maxScalarCommandItems: 8);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);
            RuntimeCommand[] first = CreateSignalBatch(
                RuntimeFrame.Zero,
                SignalBinding,
                batchId: 1L,
                itemCount: 5,
                firstRecordedSequence: 0L);
            RuntimeCommand[] second = CreateSignalBatch(
                RuntimeFrame.Zero,
                SecondSignalBinding,
                batchId: 2L,
                itemCount: 5,
                firstRecordedSequence: 5L);
            var commands = new RuntimeCommand[10];
            Array.Copy(first, 0, commands, 0, first.Length);
            Array.Copy(second, 0, commands, first.Length, second.Length);
            long expectedHash = ProbeHash(
                new TestFactory(catalog),
                options,
                header,
                commands);
            var factory = new TestFactory(catalog);
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                var snapshot = new RuntimeReplaySnapshot(
                    header,
                    new[]
                    {
                        new RuntimeReplayFrameRecord(
                            RuntimeFrame.Zero,
                            commands,
                            expectedHash,
                            string.Empty)
                    });

                RuntimeReplayPlaybackResult result =
                    new RuntimeReplayPlaybackRunner().Play(snapshot, driver);

                Assert.IsTrue(result.Success, result.FailureMessage);
                Assert.AreEqual(10, factory.Last.CommandSystem.HandledCommandCount);
                Assert.AreEqual(1L, factory.Last.Host.TickCount);
            }
        }

        [Test]
        public void CheckpointSuffix_InitializesStartsThenRestoresAndClearsPresentation()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 4);
            GameplayTriggerRuntime sourceRuntime = CreateRuntime(
                catalog,
                new GameplayTriggerRuntimeEventSource(32));
            RuntimeSaveStateResult<RuntimeSaveState> captured =
                new GameplayTriggerRuntimeSaveStateParticipant(
                    sourceRuntime,
                    AlwaysValidEntity.Instance)
                .CaptureSaveState();
            Assert.IsTrue(captured.Success, captured.Error.ToString());
            RuntimeSaveState checkpoint = WithReplayRoot(
                captured.Value,
                options,
                frame: 0L);
            var factory = new TestFactory(
                catalog,
                FactoryMode.Valid,
                validator: null,
                mutateDuringLifecycle: true);
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.CheckpointSuffix,
                       checkpoint))
            {
                driver.Reset(CreateHeader(catalog, new RuntimeFrame(1L)));

                Assert.IsTrue(driver.HasActiveSession);
                CollectionAssert.AreEqual(
                    new[] { "initialize", "start", "restore" },
                    factory.Last.LifecycleEvents);
                Assert.IsFalse(factory.Last.Runtime.AuthorityHasEvaluated);
                Assert.AreEqual(
                    ComputeHash(sourceRuntime, RuntimeFrame.Zero),
                    ComputeHash(factory.Last.Runtime, RuntimeFrame.Zero));
                Assert.AreEqual(0, factory.Last.EventSource.PendingCount);
                Assert.AreEqual(1L, factory.Last.EventSource.NextSequence);
                Assert.AreEqual(0L, factory.Last.Host.TickCount);
                Assert.IsFalse(factory.Last.Host.HasTickAttempt);
                Assert.AreEqual(new RuntimeFrame(1L), factory.Last.Buffer.CurrentFrame);
            }
        }

        [Test]
        public void CheckpointSuffix_RestoresDueInternalAndReliableWorkExactlyOnceAtFPlusOne()
        {
            GameplayTriggerCatalog catalog = CreateMixedDeferredReliableCatalog();
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 1);
            TestComposition control = TestComposition.Create(
                catalog,
                options,
                RuntimeFrame.Zero,
                FactoryMode.Valid,
                validator: null,
                mutateDuringLifecycle: false,
                enableReliableTarget: true);
            control.SourceRegistry.Seal();
            control.Host.Initialize();
            control.Host.Start();
            var gate = new RuntimeCommandSubmissionGate(
                control.SourceRegistry,
                control.Buffer,
                RuntimeCommandIngressKind.External);
            Assert.IsTrue(gate.Submit(CreateSignal(RuntimeFrame.Zero, 0L)).Success);
            control.Host.Tick(0L, 0d, 0d);
            Assert.AreEqual(1, control.Runtime.DeferredRecordCount);
            Assert.AreEqual(1, control.Runtime.ReliableOutboxRecordCount);
            GameplayTriggerDeferredRecord deferred = control.Runtime.GetDeferredRecord(0);
            GameplayTriggerReliableOutboxRecord reliable =
                control.Runtime.GetReliableOutboxRecord(0);
            Assert.AreEqual(1L, deferred.OriginalDueFrame.Value);
            Assert.AreEqual(1L, reliable.OriginalDueFrame.Value);
            Assert.AreEqual(1L, deferred.Sequence);
            Assert.AreEqual(1L, reliable.Sequence);
            RuntimeSaveStateResult<RuntimeSaveState> captured =
                new GameplayTriggerRuntimeSaveStateParticipant(
                    control.Runtime,
                    AlwaysValidEntity.Instance)
                .CaptureSaveState();
            Assert.IsTrue(captured.Success, captured.Error.ToString());
            RuntimeSaveState checkpoint = WithReplayRoot(captured.Value, options, frame: 0L);

            control.Host.Tick(1L, 0d, 0d);
            long uninterruptedHash = ComputeHash(control.Runtime, new RuntimeFrame(1L));
            Assert.AreEqual(0, control.Runtime.DeferredRecordCount);
            Assert.AreEqual(0, control.Runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(1, control.ReliableTransfer.CallCount);
            control.Host.Dispose();

            var replayFactory = new TestFactory(
                catalog,
                enableReliableTarget: true);
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       replayFactory,
                       options,
                       GameplayTriggerReplayBootstrapMode.CheckpointSuffix,
                       checkpoint))
            {
                driver.Reset(CreateHeader(catalog, new RuntimeFrame(1L)));
                Assert.AreEqual(1, replayFactory.Last.Runtime.DeferredRecordCount);
                Assert.AreEqual(1, replayFactory.Last.Runtime.ReliableOutboxRecordCount);
                Assert.AreEqual(1L,
                    replayFactory.Last.Runtime.GetDeferredRecord(0).Sequence);
                Assert.AreEqual(1L,
                    replayFactory.Last.Runtime.GetReliableOutboxRecord(0).Sequence);

                RuntimeReplayPlaybackFrameResult result = driver.RunFrame(
                    new RuntimeReplayFrameRecord(
                        new RuntimeFrame(1L),
                        Array.Empty<RuntimeCommand>(),
                        uninterruptedHash,
                        string.Empty));

                Assert.AreEqual(uninterruptedHash, result.ActualResultHash);
                Assert.AreEqual(0, replayFactory.Last.Runtime.DeferredRecordCount);
                Assert.AreEqual(0, replayFactory.Last.Runtime.ReliableOutboxRecordCount);
                Assert.AreEqual(1, replayFactory.Last.ReliableTransfer.CallCount);
                Assert.AreEqual(1L, replayFactory.Last.ReliableTransfer.LastFrame.Value);
                Assert.AreEqual(1L, replayFactory.Last.ReliableTransfer.LastSequence);
                Assert.AreEqual(control.Runtime.NextDeferredSequence,
                    replayFactory.Last.Runtime.NextDeferredSequence);
                Assert.AreEqual(control.Runtime.NextReliableOutboxSequence,
                    replayFactory.Last.Runtime.NextReliableOutboxSequence);
            }
        }

        [Test]
        public void Replay_RegeneratesDerivedH1TraceWithoutSnapshotReinjection()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            var sink = (TestSink)catalog.GetSink(0);
            sink.PublishActionResult = true;
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 1);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);
            RuntimeCommand signal = CreateSignal(RuntimeFrame.Zero, 0L);
            long expectedHash = ProbeHash(
                new TestFactory(catalog, observerCapacity: 64),
                options,
                header,
                new[] { signal });
            int commitsBeforePlayback = sink.CommitCount;
            var snapshot = new RuntimeReplaySnapshot(
                header,
                new[]
                {
                    new RuntimeReplayFrameRecord(
                        RuntimeFrame.Zero,
                        new[] { signal },
                        expectedHash,
                        string.Empty)
                });
            var factory = new TestFactory(catalog, observerCapacity: 64);

            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                RuntimeReplayPlaybackResult result =
                    new RuntimeReplayPlaybackRunner().Play(snapshot, driver);

                Assert.IsTrue(result.Success, result.FailureMessage);
                Assert.AreEqual(commitsBeforePlayback + 1, sink.CommitCount);
                var kinds = new List<GameplayTriggerRuntimeEventKind>();
                while (factory.Last.Observer.TryDequeue(
                           out GameplayTriggerRuntimeEvent evt))
                {
                    kinds.Add(evt.Kind);
                }
                CollectionAssert.AreEqual(
                    new[]
                    {
                        GameplayTriggerRuntimeEventKind.CommandResult,
                        GameplayTriggerRuntimeEventKind.Occurrence,
                        GameplayTriggerRuntimeEventKind.ActivationAccepted,
                        GameplayTriggerRuntimeEventKind.ActionResult,
                        GameplayTriggerRuntimeEventKind.ActionDispatched
                    },
                    kinds);
                Assert.AreEqual(1, snapshot.Records[0].Commands.Count);
                Assert.AreEqual(RuntimeCommandProvenanceKind.Exogenous,
                    snapshot.Records[0].Commands[0].Envelope.Provenance);
            }
        }

        [Test]
        public void ObserverCapacityAndOverflow_DoNotChangeReplayAuthorityHash()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            ((TestSink)catalog.GetSink(0)).PublishActionResult = true;
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 1);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);
            RuntimeCommand signal = CreateSignal(RuntimeFrame.Zero, 0L);
            long expectedHash = ProbeHash(
                new TestFactory(catalog, observerCapacity: 64),
                options,
                header,
                new[] { signal });
            var small = new TestFactory(catalog, observerCapacity: 1);
            var large = new TestFactory(catalog, observerCapacity: 64);

            long smallHash = RunReplayFrame(small, options, header, signal, expectedHash);
            long largeHash = RunReplayFrame(large, options, header, signal, expectedHash);

            Assert.AreEqual(expectedHash, smallHash);
            Assert.AreEqual(expectedHash, largeHash);
            Assert.AreEqual(1, small.Last.Observer.Count);
            Assert.Greater(small.Last.Observer.DroppedCount, 0L);
            Assert.AreEqual(5, large.Last.Observer.Count);
            Assert.AreEqual(0L, large.Last.Observer.DroppedCount);
        }

        [Test]
        public void ObserverRegistrationOrderAndDisabledPresentation_DoNotChangeAuthorityHash()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            ((TestSink)catalog.GetSink(0)).PublishActionResult = true;
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 1);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);
            RuntimeCommand signal = CreateSignal(RuntimeFrame.Zero, 0L);
            long expectedHash = ProbeHash(
                new TestFactory(catalog, observerCapacity: 64, addSecondObserver: true),
                options,
                header,
                new[] { signal });
            var reverse = new TestFactory(
                catalog,
                observerCapacity: 64,
                addSecondObserver: true,
                reverseObserverRegistration: true);
            var disabledObservers = new TestFactory(
                catalog,
                observerCapacity: 64,
                disableObserver: true,
                addSecondObserver: true);
            var disabledRouter = new TestFactory(
                catalog,
                mutateDuringLifecycle: true,
                observerCapacity: 64,
                disableRouter: true);

            long reverseHash = RunReplayFrame(reverse, options, header, signal, expectedHash);
            long disabledObserverHash = RunReplayFrame(
                disabledObservers,
                options,
                header,
                signal,
                expectedHash);
            long disabledRouterHash;
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       disabledRouter,
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                driver.Reset(header);
                Assert.IsFalse(disabledRouter.Last.HandoffRouter.IsEnabled);
                Assert.AreEqual(0, disabledRouter.Last.EventSource.PendingCount);
                Assert.AreEqual(1L, disabledRouter.Last.EventSource.NextSequence);
                Assert.AreEqual(0, disabledRouter.Last.Observer.Count);
                disabledRouterHash = driver.RunFrame(new RuntimeReplayFrameRecord(
                    RuntimeFrame.Zero,
                    new[] { signal },
                    expectedHash,
                    string.Empty)).ActualResultHash;
            }

            Assert.AreEqual(expectedHash, reverseHash);
            Assert.AreEqual(expectedHash, disabledObserverHash);
            Assert.AreEqual(expectedHash, disabledRouterHash);
            Assert.AreEqual(5, reverse.Last.Observer.Count);
            Assert.AreEqual(5, reverse.Last.SecondObserver.Count);
            Assert.IsFalse(disabledObservers.Last.Observer.IsEnabled);
            Assert.IsFalse(disabledObservers.Last.SecondObserver.IsEnabled);
            Assert.AreEqual(0, disabledObservers.Last.Observer.Count);
            Assert.AreEqual(0, disabledObservers.Last.SecondObserver.Count);
            Assert.IsFalse(disabledRouter.Last.HandoffRouter.IsEnabled);
            Assert.AreEqual(0, disabledRouter.Last.Observer.Count);
            Assert.Greater(disabledRouter.Last.EventSource.PendingCount, 0);
        }

        [Test]
        public void HostTickExceptionFaultsAndDisposesSessionWithFrameContext()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 1);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);
            var factory = new TestFactory(catalog, throwOnHostTick: true);
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                driver.Reset(header);

                GameplayTriggerReplayException exception =
                    Assert.Throws<GameplayTriggerReplayException>(() => driver.RunFrame(
                        new RuntimeReplayFrameRecord(
                            RuntimeFrame.Zero,
                            Array.Empty<RuntimeCommand>(),
                            0L,
                            string.Empty)));

                Assert.AreEqual(GameplayTriggerReplayFailureCode.HostTickFailed, exception.Code);
                Assert.IsTrue(exception.HasFrame);
                Assert.AreEqual(RuntimeFrame.Zero, exception.Frame);
                Assert.IsInstanceOf<RuntimeHostException>(exception.InnerException);
                Assert.AreEqual(RuntimeHostErrorPolicy.FailFast, factory.Last.Host.ErrorPolicy);
                Assert.IsTrue(driver.IsFaulted);
                Assert.IsFalse(driver.HasActiveSession);
                Assert.IsTrue(factory.Last.Host.HasTickAttempt);
                Assert.IsFalse(factory.Last.Host.LastTickAttemptSucceeded);
                Assert.AreEqual(RuntimeLifecycleState.Disposed, factory.Last.Host.State);

                GameplayTriggerReplayException continuation =
                    Assert.Throws<GameplayTriggerReplayException>(() => driver.RunFrame(
                        new RuntimeReplayFrameRecord(
                            RuntimeFrame.Zero,
                            Array.Empty<RuntimeCommand>(),
                            0L,
                            string.Empty)));
                Assert.AreEqual(
                    GameplayTriggerReplayFailureCode.InvalidDriverState,
                    continuation.Code);
            }
        }

        [Test]
        public void ReliableTransferExceptionFaultsAndDisposesSessionWithFrameContext()
        {
            GameplayTriggerCatalog catalog = CreateMixedDeferredReliableCatalog();
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 1);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);
            RuntimeCommand signal = CreateSignal(RuntimeFrame.Zero, 0L);
            long firstHash = ProbeHash(
                new TestFactory(catalog, enableReliableTarget: true),
                options,
                header,
                new[] { signal });
            var factory = new TestFactory(
                catalog,
                enableReliableTarget: true,
                throwOnReliableTransfer: true);
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                driver.Reset(header);
                driver.RunFrame(new RuntimeReplayFrameRecord(
                    RuntimeFrame.Zero,
                    new[] { signal },
                    firstHash,
                    string.Empty));

                GameplayTriggerReplayException exception =
                    Assert.Throws<GameplayTriggerReplayException>(() => driver.RunFrame(
                        new RuntimeReplayFrameRecord(
                            new RuntimeFrame(1L),
                            Array.Empty<RuntimeCommand>(),
                            0L,
                            string.Empty)));

                Assert.AreEqual(GameplayTriggerReplayFailureCode.HostTickFailed, exception.Code);
                Assert.IsTrue(exception.HasFrame);
                Assert.AreEqual(1L, exception.Frame.Value);
                Assert.IsInstanceOf<RuntimeHostException>(exception.InnerException);
                Assert.AreEqual(RuntimeHostErrorPolicy.FailFast, factory.Last.Host.ErrorPolicy);
                Assert.AreEqual(1, factory.Last.ReliableTransfer.CallCount);
                Assert.IsFalse(factory.Last.Host.LastTickAttemptSucceeded);
                Assert.AreEqual(RuntimeLifecycleState.Disposed, factory.Last.Host.State);
                Assert.IsTrue(driver.IsFaulted);
                Assert.IsFalse(driver.HasActiveSession);

                GameplayTriggerReplayException continuation =
                    Assert.Throws<GameplayTriggerReplayException>(() => driver.RunFrame(
                        new RuntimeReplayFrameRecord(
                            new RuntimeFrame(1L),
                            Array.Empty<RuntimeCommand>(),
                            0L,
                            string.Empty)));
                Assert.AreEqual(
                    GameplayTriggerReplayFailureCode.InvalidDriverState,
                    continuation.Code);
            }
        }

        [Test]
        public void GenericRunner_HashMismatchPreservesFrameHashAndCommandContext()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 1);
            RuntimeReplayHeader header = CreateHeader(catalog, RuntimeFrame.Zero);
            RuntimeCommand signal = CreateSignal(RuntimeFrame.Zero, 0L);
            long actualHash = ProbeHash(
                new TestFactory(catalog), options, header, new[] { signal });
            var snapshot = new RuntimeReplaySnapshot(
                header,
                new[]
                {
                    new RuntimeReplayFrameRecord(
                        RuntimeFrame.Zero,
                        new[] { signal },
                        actualHash + 1L,
                        "expected-diagnostics")
                });
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       new TestFactory(catalog),
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                RuntimeReplayPlaybackResult result =
                    new RuntimeReplayPlaybackRunner().Play(snapshot, driver);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(RuntimeReplayPlaybackFailureCode.HashMismatch,
                    result.FailureCode);
                Assert.IsTrue(result.HasFailureFrame);
                Assert.AreEqual(RuntimeFrame.Zero, result.FailureFrame);
                Assert.AreEqual(actualHash + 1L, result.ExpectedResultHash);
                Assert.AreEqual(actualHash, result.ActualResultHash);
                Assert.AreEqual(1, result.FailureCommands.Count);
                Assert.AreEqual(signal, result.FailureCommands[0]);
                Assert.AreEqual("expected-diagnostics", result.ExpectedDiagnosticsSummary);
                Assert.IsNull(result.Exception);
            }
        }

        [Test]
        public void GenericRunner_FactoryExceptionPreservesTypedReplayCause()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 1);
            var snapshot = new RuntimeReplaySnapshot(
                CreateHeader(catalog, RuntimeFrame.Zero),
                Array.Empty<RuntimeReplayFrameRecord>());
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       new ThrowingFactory(),
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                RuntimeReplayPlaybackResult result =
                    new RuntimeReplayPlaybackRunner().Play(snapshot, driver);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(RuntimeReplayPlaybackFailureCode.DriverException,
                    result.FailureCode);
                Assert.IsFalse(result.HasFailureFrame);
                Assert.IsInstanceOf<GameplayTriggerReplayException>(result.Exception);
                var replayException = (GameplayTriggerReplayException)result.Exception;
                Assert.AreEqual(GameplayTriggerReplayFailureCode.SessionFactoryFailed,
                    replayException.Code);
                Assert.IsInstanceOf<InvalidOperationException>(
                    replayException.InnerException);
            }
        }

        [Test]
        public void CheckpointSuffix_UnknownRootSchemaRejectsBeforeFactoryOrLifecycle()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 2);
            RuntimeSaveState checkpoint = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion + 1,
                DateTime.UtcNow,
                FrameworkVersion,
                GameplayTriggerReplayHeaderContract.FormatConfigHash(
                    catalog.GameplayTriggerCatalogHash),
                ResourceHash,
                frame: 0L,
                entities: null,
                globalCounters: null,
                moduleStates: null);
            var factory = new TestFactory(catalog);
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.CheckpointSuffix,
                       checkpoint))
            {
                GameplayTriggerReplayException exception =
                    Assert.Throws<GameplayTriggerReplayException>(() =>
                        driver.Reset(CreateHeader(catalog, new RuntimeFrame(1L))));

                Assert.AreEqual(GameplayTriggerReplayFailureCode.InvalidCheckpoint, exception.Code);
                Assert.AreEqual(0, factory.CreateCount);
                Assert.IsNull(factory.Last);
                Assert.IsTrue(driver.IsFaulted);
                Assert.IsFalse(driver.HasActiveSession);
            }
        }

        [TestCase(CheckpointRootFault.NegativeFrame)]
        [TestCase(CheckpointRootFault.FrameMax)]
        [TestCase(CheckpointRootFault.NotImmediatelyPrior)]
        [TestCase(CheckpointRootFault.Framework)]
        [TestCase(CheckpointRootFault.Config)]
        [TestCase(CheckpointRootFault.Resources)]
        public void CheckpointSuffix_RootFaultMatrixRejectsBeforeFactory(
            CheckpointRootFault fault)
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 1);
            RuntimeSaveStateResult<RuntimeSaveState> captured =
                new GameplayTriggerRuntimeSaveStateParticipant(
                    CreateRuntime(catalog, new GameplayTriggerRuntimeEventSource(8)),
                    AlwaysValidEntity.Instance)
                .CaptureSaveState();
            Assert.IsTrue(captured.Success, captured.Error.ToString());
            RuntimeSaveState checkpoint = CreateCheckpointRootFault(
                WithReplayRoot(captured.Value, options, frame: 0L),
                fault);
            var factory = new TestFactory(catalog);
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.CheckpointSuffix,
                       checkpoint))
            {
                GameplayTriggerReplayException exception =
                    Assert.Throws<GameplayTriggerReplayException>(() =>
                        driver.Reset(CreateHeader(catalog, new RuntimeFrame(1L))));

                Assert.AreEqual(GameplayTriggerReplayFailureCode.InvalidCheckpoint, exception.Code);
                Assert.AreEqual(0, factory.CreateCount);
                Assert.IsNull(factory.Last);
            }
        }

        [TestCase(RestoreFault.InvalidPayloadJson)]
        [TestCase(RestoreFault.InvalidEntity)]
        [TestCase(RestoreFault.ThrowingEntityValidator)]
        public void CheckpointSuffix_RestoreFaultsExposeTypedCheckpointErrorAndDispose(
            RestoreFault fault)
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 1);
            RuntimeSaveStateResult<RuntimeSaveState> captured =
                new GameplayTriggerRuntimeSaveStateParticipant(
                    CreateRuntime(catalog, new GameplayTriggerRuntimeEventSource(8)),
                    AlwaysValidEntity.Instance)
                .CaptureSaveState();
            Assert.IsTrue(captured.Success, captured.Error.ToString());
            RuntimeSaveState checkpoint = WithReplayRoot(captured.Value, options, frame: 0L);
            IGameplayTriggerRestoreEntityValidator entityValidator = AlwaysValidEntity.Instance;
            if (fault == RestoreFault.InvalidPayloadJson)
                checkpoint = CorruptTriggerPayload(checkpoint);
            else if (fault == RestoreFault.InvalidEntity)
                entityValidator = RejectAllEntity.Instance;
            else
                entityValidator = ThrowingEntity.Instance;
            var factory = new TestFactory(
                catalog,
                restoreEntityValidator: entityValidator);
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.CheckpointSuffix,
                       checkpoint))
            {
                GameplayTriggerReplayException exception =
                    Assert.Throws<GameplayTriggerReplayException>(() =>
                        driver.Reset(CreateHeader(catalog, new RuntimeFrame(1L))));

                Assert.AreEqual(
                    GameplayTriggerReplayFailureCode.CheckpointRestoreFailed,
                    exception.Code);
                Assert.IsNotNull(exception.CheckpointError);
                Assert.AreNotEqual(RuntimeSaveStateErrorCode.None,
                    exception.CheckpointError.Code);
                Assert.IsTrue(driver.IsFaulted);
                Assert.IsFalse(driver.HasActiveSession);
                Assert.AreEqual(RuntimeLifecycleState.Disposed, factory.Last.Host.State);
                Assert.AreEqual(0L, factory.Last.Host.TickCount);
            }
        }

        [Test]
        public void ConformanceFactory_FullCapacityWarmupKeepsAdmissionAndHostTickAtZeroBytes()
        {
            GameplayTriggerCatalog catalog = CreateCatalog(reliable: false);
            GameplayTriggerReplayBootstrapOptions options = CreateOptions(catalog, maxCommands: 3);
            TestComposition composition = TestComposition.Create(
                catalog,
                options,
                RuntimeFrame.Zero,
                FactoryMode.Valid,
                validator: null,
                mutateDuringLifecycle: false);
            composition.SourceRegistry.Seal();
            composition.Host.Initialize();
            composition.Host.Start();
            var gate = new RuntimeCommandSubmissionGate(
                composition.SourceRegistry,
                composition.Buffer,
                RuntimeCommandIngressKind.External);

            SubmitFullFrame(composition.Host, gate, RuntimeFrame.Zero);
            long probeStart = GC.GetAllocatedBytesForCurrentThread();
            long probeEnd = GC.GetAllocatedBytesForCurrentThread();
            long probeOverhead = probeEnd - probeStart;

            long before = GC.GetAllocatedBytesForCurrentThread();
            SubmitFullFrame(composition.Host, gate, new RuntimeFrame(1L));
            long after = GC.GetAllocatedBytesForCurrentThread();

            composition.Host.Dispose();
            Assert.AreEqual(0L, after - before - probeOverhead);
        }

        [TestCase(NoAllocWorkload.SteadyAuthority)]
        [TestCase(NoAllocWorkload.DueInternalDeferred)]
        [TestCase(NoAllocWorkload.DueInternalAndReliableOutbox)]
        public void ConformanceFactory_SixtyFourTickWorkloadsRemainNoAlloc(
            NoAllocWorkload workload)
        {
            GameplayTriggerCatalog catalog = workload == NoAllocWorkload.SteadyAuthority
                ? CreateCatalog(reliable: false)
                : workload == NoAllocWorkload.DueInternalDeferred
                    ? CreateInternalDeferredCatalog()
                    : CreateMixedDeferredReliableCatalog();
            var options = new GameplayTriggerReplayBootstrapOptions(
                FrameworkVersion,
                ResourceHash,
                catalog.GameplayTriggerCatalogHash,
                maxCommandsPerFrame: 64,
                maxSignalCommandItems: 8,
                maxOccupancyCommandItems: 8,
                maxScalarCommandItems: 8);
            bool reliable = workload == NoAllocWorkload.DueInternalAndReliableOutbox;
            TestComposition composition = TestComposition.Create(
                catalog,
                options,
                RuntimeFrame.Zero,
                FactoryMode.Valid,
                validator: null,
                mutateDuringLifecycle: false,
                observerCapacity: 4,
                enableReliableTarget: reliable);
            composition.SourceRegistry.Seal();
            composition.Host.Initialize();
            composition.Host.Start();
            var gate = new RuntimeCommandSubmissionGate(
                composition.SourceRegistry,
                composition.Buffer,
                RuntimeCommandIngressKind.External);

            Assert.IsTrue(gate.Submit(CreateSignal(RuntimeFrame.Zero, 0L)).Success);
            composition.Host.Tick(0L, 0d, 0d);
            for (int frame = 1; frame <= 64; frame++)
            {
                Assert.IsTrue(gate.Submit(CreateSignal(
                    new RuntimeFrame(frame),
                    sequence: 0L)).Success);
            }
            _ = GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 1; frame <= 64; frame++)
                composition.Host.Tick(frame, 0d, 0d);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.AreEqual(65L, composition.Host.TickCount);
            Assert.AreEqual(0, composition.Buffer.PendingCount);
            if (reliable)
                Assert.AreEqual(64, composition.ReliableTransfer.CallCount);
            composition.Host.Dispose();
        }

        private static void AssertAdmissionFailure(
            GameplayTriggerCatalog catalog,
            GameplayTriggerReplayBootstrapOptions options,
            RuntimeReplayHeader header,
            IReadOnlyList<RuntimeCommand> commands,
            GameplayTriggerReplayFailureCode expectedCode,
            int expectedCommandIndex)
        {
            var factory = new TestFactory(catalog);
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                driver.Reset(header);
                GameplayTriggerReplayException exception =
                    Assert.Throws<GameplayTriggerReplayException>(() => driver.RunFrame(
                        new RuntimeReplayFrameRecord(
                            header.StartFrame,
                            commands,
                            0L,
                            string.Empty)));

                Assert.AreEqual(expectedCode, exception.Code);
                Assert.IsTrue(exception.HasFrame);
                Assert.AreEqual(expectedCommandIndex, exception.CommandIndex);
                Assert.AreEqual(0L, factory.Last.Host.TickCount);
                Assert.AreEqual(0, factory.Last.Buffer.PendingCount);
                Assert.IsTrue(driver.IsFaulted);
            }
        }

        private static long ProbeHash(
            TestFactory factory,
            GameplayTriggerReplayBootstrapOptions options,
            RuntimeReplayHeader header,
            IReadOnlyList<RuntimeCommand> commands)
        {
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                driver.Reset(header);
                RuntimeReplayPlaybackFrameResult result = driver.RunFrame(
                    new RuntimeReplayFrameRecord(
                        header.StartFrame,
                        commands,
                        0L,
                        string.Empty));
                Assert.IsTrue(driver.IsFaulted, "Probe intentionally uses a mismatching result hash.");
                return result.ActualResultHash;
            }
        }

        private static long[] RunControlHashes(
            GameplayTriggerCatalog catalog,
            GameplayTriggerReplayBootstrapOptions options,
            IReadOnlyList<RuntimeCommand[]> frameCommands)
        {
            TestComposition composition = TestComposition.Create(
                catalog,
                options,
                RuntimeFrame.Zero,
                FactoryMode.Valid,
                validator: null,
                mutateDuringLifecycle: false);
            composition.SourceRegistry.Seal();
            composition.Host.Initialize();
            composition.Host.Start();
            var gate = new RuntimeCommandSubmissionGate(
                composition.SourceRegistry,
                composition.Buffer,
                RuntimeCommandIngressKind.External);
            var hashes = new long[frameCommands.Count];
            try
            {
                for (int frameIndex = 0; frameIndex < frameCommands.Count; frameIndex++)
                {
                    RuntimeCommand[] commands = frameCommands[frameIndex];
                    for (int commandIndex = 0; commandIndex < commands.Length; commandIndex++)
                    {
                        RuntimeCommandValidationResult submitted = gate.Submit(commands[commandIndex]);
                        Assert.IsTrue(submitted.Success, submitted.Error.Message);
                    }

                    composition.Host.Tick(frameIndex, 0d, 0d);
                    hashes[frameIndex] = ComputeHash(
                        composition.Runtime,
                        new RuntimeFrame(frameIndex));
                }
            }
            finally
            {
                composition.Host.Dispose();
            }

            return hashes;
        }

        private static long RunReplayFrame(
            TestFactory factory,
            GameplayTriggerReplayBootstrapOptions options,
            RuntimeReplayHeader header,
            RuntimeCommand command,
            long expectedHash)
        {
            using (var driver = new GameplayTriggerReplayFrameDriver(
                       factory,
                       options,
                       GameplayTriggerReplayBootstrapMode.Fresh))
            {
                driver.Reset(header);
                RuntimeReplayPlaybackFrameResult result = driver.RunFrame(
                    new RuntimeReplayFrameRecord(
                        header.StartFrame,
                        new[] { command },
                        expectedHash,
                        string.Empty));
                return result.ActualResultHash;
            }
        }

        private static void SubmitFullFrame(
            RuntimeHost host,
            RuntimeCommandSubmissionGate gate,
            RuntimeFrame frame)
        {
            Assert.IsTrue(gate.Submit(CreateSignal(frame, 0L)).Success);
            Assert.IsTrue(gate.Submit(CreateOccupancy(frame, 1L)).Success);
            Assert.IsTrue(gate.Submit(CreateScalar(frame, 2L)).Success);
            host.Tick(frame.Value, 0d, 0d);
        }

        private static RuntimeCommand CreateSignal(RuntimeFrame frame, long sequence)
        {
            var destination = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                frame,
                ExternalSourceId,
                RuntimeCommandProvenanceKind.Exogenous,
                SignalBinding,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                batchId: frame.Value + 1L,
                new[] { new GameplayTriggerSignal(10, sequence: 5L) },
                destination,
                out int written,
                out GameplayTriggerSignalCommandCodecStatus status), status.ToString());
            Assert.AreEqual(1, written);
            return destination[0].WithSequence(sequence);
        }

        private static RuntimeCommand[] CreateSignalBatch(
            RuntimeFrame frame,
            GameplayTriggerSourceBindingId sourceBindingId,
            long batchId,
            int itemCount,
            long firstRecordedSequence)
        {
            var signals = new GameplayTriggerSignal[itemCount];
            for (int index = 0; index < itemCount; index++)
                signals[index] = new GameplayTriggerSignal(100 + index, sequence: index);
            var destination = new RuntimeCommand[itemCount];
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                frame,
                ExternalSourceId,
                RuntimeCommandProvenanceKind.Exogenous,
                sourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                batchId,
                signals,
                destination,
                out int written,
                out GameplayTriggerSignalCommandCodecStatus status), status.ToString());
            Assert.AreEqual(itemCount, written);
            for (int index = 0; index < itemCount; index++)
                destination[index] = destination[index].WithSequence(firstRecordedSequence + index);
            return destination;
        }

        private static RuntimeCommand[] CreateSignalBatch(
            RuntimeFrame frame,
            GameplayTriggerSourceBindingId sourceBindingId,
            GameplayTriggerSignalOrdering ordering,
            long batchId,
            GameplayTriggerSignal[] signals)
        {
            var destination = new RuntimeCommand[signals.Length];
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                frame,
                ExternalSourceId,
                RuntimeCommandProvenanceKind.Exogenous,
                sourceBindingId,
                ordering,
                batchId,
                signals,
                destination,
                out int written,
                out GameplayTriggerSignalCommandCodecStatus status), status.ToString());
            Assert.AreEqual(signals.Length, written);
            for (int index = 0; index < destination.Length; index++)
                destination[index] = destination[index].WithSequence(index);
            return destination;
        }

        private static RuntimeCommand CreateOccupancy(RuntimeFrame frame, long sequence)
        {
            return CreateOccupancyBatch(frame, 1, sequence)[0];
        }

        private static RuntimeCommand[] CreateOccupancyBatch(
            RuntimeFrame frame,
            int itemCount,
            long firstRecordedSequence)
        {
            var occupants = new GameplayEntityId[itemCount];
            for (int index = 0; index < itemCount; index++)
                occupants[index] = new GameplayEntityId(2 + index, 1);
            var destination = new RuntimeCommand[itemCount];
            Assert.IsTrue(GameplayTriggerOccupancyCommandCodec.TryEncodeBatch(
                frame,
                ExternalSourceId,
                RuntimeCommandProvenanceKind.Exogenous,
                OccupancyBinding,
                batchId: frame.Value + 1L,
                occupants,
                destination,
                out int written,
                out GameplayTriggerOccupancyCommandCodecStatus status), status.ToString());
            Assert.AreEqual(itemCount, written);
            for (int index = 0; index < itemCount; index++)
                destination[index] = destination[index].WithSequence(firstRecordedSequence + index);
            return destination;
        }

        private static RuntimeCommand CreateScalar(RuntimeFrame frame, long sequence)
        {
            return CreateScalarBatch(frame, 1, sequence)[0];
        }

        private static RuntimeCommand[] CreateScalarBatch(
            RuntimeFrame frame,
            int itemCount,
            long firstRecordedSequence)
        {
            var samples = new GameplayTriggerScalarSample[itemCount];
            for (int index = 0; index < itemCount; index++)
            {
                samples[index] = new GameplayTriggerScalarSample(
                    new GameplayEntityId(3 + index, 1),
                    Fix64.FromInt(2 + index));
            }
            var destination = new RuntimeCommand[itemCount];
            Assert.IsTrue(GameplayTriggerScalarCommandCodec.TryEncodeBatch(
                frame,
                ExternalSourceId,
                RuntimeCommandProvenanceKind.Exogenous,
                ScalarBinding,
                batchId: frame.Value + 1L,
                samples,
                destination,
                out int written,
                out GameplayTriggerScalarCommandCodecStatus status), status.ToString());
            Assert.AreEqual(itemCount, written);
            for (int index = 0; index < itemCount; index++)
                destination[index] = destination[index].WithSequence(firstRecordedSequence + index);
            return destination;
        }

        private static GameplayTriggerReplayBootstrapOptions CreateOptions(
            GameplayTriggerCatalog catalog,
            int maxCommands)
        {
            return new GameplayTriggerReplayBootstrapOptions(
                FrameworkVersion,
                ResourceHash,
                catalog.GameplayTriggerCatalogHash,
                maxCommands,
                maxSignalCommandItems: 8,
                maxOccupancyCommandItems: 8,
                maxScalarCommandItems: 8);
        }

        private static RuntimeReplayHeader CreateHeader(
            GameplayTriggerCatalog catalog,
            RuntimeFrame startFrame)
        {
            return GameplayTriggerReplayHeaderContract.Create(
                FrameworkVersion,
                ResourceHash,
                catalog.GameplayTriggerCatalogHash,
                startFrame);
        }

        private static RuntimeReplayHeader CreateHeaderFault(
            RuntimeReplayHeader valid,
            HeaderFault fault)
        {
            return new RuntimeReplayHeader(
                fault == HeaderFault.Schema ? valid.SchemaVersion + 1 : valid.SchemaVersion,
                fault == HeaderFault.Framework ? "wrong-framework" : valid.FrameworkVersion,
                fault == HeaderFault.Config ? "wrong-config" : valid.ConfigHash,
                fault == HeaderFault.Resources ? "wrong-resources" : valid.ResourceCatalogHash,
                fault == HeaderFault.StartFrameMax
                    ? new RuntimeFrame(long.MaxValue)
                    : valid.StartFrame);
        }

        private static IReadOnlyList<RuntimeCommand> CreateFaultedCommands(CommandFault fault)
        {
            RuntimeCommand signal = CreateSignal(RuntimeFrame.Zero, 0L);
            switch (fault)
            {
                case CommandFault.UnknownSource:
                    return new[] { CopyCommand(signal, sourceId: ExternalSourceId + 1) };
                case CommandFault.UnknownCommand:
                    return new[]
                    {
                        CopyCommand(
                            signal,
                            commandId: GameplayTriggerRuntimeCommandIds.SetEnabled)
                    };
                case CommandFault.RegisteredDerivedSource:
                    return new[]
                    {
                        CopyCommand(
                            signal,
                            sourceId: DerivedSourceId,
                            envelope: CopyEnvelope(
                                signal.Envelope,
                                RuntimeCommandProvenanceKind.Derived))
                    };
                case CommandFault.ExternalProvenanceMismatch:
                    return new[]
                    {
                        CopyCommand(
                            signal,
                            envelope: CopyEnvelope(
                                signal.Envelope,
                                RuntimeCommandProvenanceKind.Derived))
                    };
                case CommandFault.LegacyLifecycle:
                    return new[]
                    {
                        GameplayTriggerRuntimeCommandFactory.SetEnabled(
                            RuntimeFrame.Zero,
                            new GameplayTriggerInstanceId(1, 1),
                            enabled: true,
                            sourceId: ExternalSourceId)
                    };
                case CommandFault.WrongFrame:
                    return new[]
                    {
                        CopyCommand(signal, frame: new RuntimeFrame(1L))
                    };
                case CommandFault.MalformedEnvelope:
                    return new[]
                    {
                        CopyCommand(
                            signal,
                            envelope: new RuntimeCommandEnvelope(
                                signal.Envelope.TypeId,
                                signal.Envelope.SchemaVersion + 1,
                                signal.Envelope.Provenance,
                                signal.Envelope.Data0,
                                signal.Envelope.Data1,
                                signal.Envelope.Data2,
                                signal.Envelope.Data3,
                                signal.Envelope.Data4,
                                signal.Envelope.Data5,
                                signal.Envelope.Data6,
                                signal.Envelope.Data7))
                    };
                case CommandFault.IncompleteBatch:
                    RuntimeCommand[] incomplete = CreateSignalBatch(
                        RuntimeFrame.Zero,
                        SignalBinding,
                        batchId: 9L,
                        itemCount: 2,
                        firstRecordedSequence: 0L);
                    return new[] { incomplete[0] };
                case CommandFault.MalformedOccupancyBatch:
                    RuntimeCommand occupancy = CreateOccupancy(RuntimeFrame.Zero, 0L);
                    return new[] { CorruptEnvelopeSchema(occupancy) };
                case CommandFault.IncompleteOccupancyBatch:
                    RuntimeCommand[] incompleteOccupancy = CreateOccupancyBatch(
                        RuntimeFrame.Zero,
                        itemCount: 2,
                        firstRecordedSequence: 0L);
                    return new[] { incompleteOccupancy[0] };
                case CommandFault.MalformedScalarBatch:
                    RuntimeCommand scalar = CreateScalar(RuntimeFrame.Zero, 0L);
                    return new[] { CorruptEnvelopeSchema(scalar) };
                case CommandFault.IncompleteScalarBatch:
                    RuntimeCommand[] incompleteScalar = CreateScalarBatch(
                        RuntimeFrame.Zero,
                        itemCount: 2,
                        firstRecordedSequence: 0L);
                    return new[] { incompleteScalar[0] };
                case CommandFault.DuplicateSourceSubmission:
                    RuntimeCommand first = CreateSignal(RuntimeFrame.Zero, 0L);
                    RuntimeCommand second = CreateSignalBatch(
                        RuntimeFrame.Zero,
                        SignalBinding,
                        batchId: 2L,
                        itemCount: 1,
                        firstRecordedSequence: 1L)[0];
                    return new[] { first, second };
                default:
                    throw new ArgumentOutOfRangeException(nameof(fault));
            }
        }

        private static RuntimeCommand CorruptEnvelopeSchema(RuntimeCommand command)
        {
            RuntimeCommandEnvelope envelope = command.Envelope;
            return CopyCommand(
                command,
                envelope: new RuntimeCommandEnvelope(
                    envelope.TypeId,
                    envelope.SchemaVersion + 1,
                    envelope.Provenance,
                    envelope.Data0,
                    envelope.Data1,
                    envelope.Data2,
                    envelope.Data3,
                    envelope.Data4,
                    envelope.Data5,
                    envelope.Data6,
                    envelope.Data7));
        }

        private static RuntimeCommand CopyCommand(
            RuntimeCommand command,
            RuntimeFrame? frame = null,
            int? sourceId = null,
            int? commandId = null,
            RuntimeCommandEnvelope? envelope = null)
        {
            return new RuntimeCommand(
                frame ?? command.Frame,
                sourceId ?? command.SourceId,
                commandId ?? command.CommandId,
                command.TargetId,
                command.Payload0,
                command.Payload1,
                command.Payload2,
                command.TraceId,
                command.Sequence,
                envelope ?? command.Envelope);
        }

        private static RuntimeCommandEnvelope CopyEnvelope(
            RuntimeCommandEnvelope envelope,
            RuntimeCommandProvenanceKind provenance)
        {
            return new RuntimeCommandEnvelope(
                envelope.TypeId,
                envelope.SchemaVersion,
                provenance,
                envelope.Data0,
                envelope.Data1,
                envelope.Data2,
                envelope.Data3,
                envelope.Data4,
                envelope.Data5,
                envelope.Data6,
                envelope.Data7);
        }

        private static RuntimeSaveState WithReplayRoot(
            RuntimeSaveState captured,
            GameplayTriggerReplayBootstrapOptions options,
            long frame)
        {
            return new RuntimeSaveState(
                captured.SchemaVersion,
                captured.CreatedAtUtc,
                options.ExpectedFrameworkVersion,
                GameplayTriggerReplayHeaderContract.FormatConfigHash(
                    options.ExpectedGameplayTriggerCatalogHash),
                options.ExpectedResourceCatalogHash,
                frame,
                captured.Entities,
                captured.GlobalCounters,
                captured.ModuleStates,
                captured.Metadata);
        }

        private static RuntimeSaveState CreateCheckpointRootFault(
            RuntimeSaveState valid,
            CheckpointRootFault fault)
        {
            return new RuntimeSaveState(
                valid.SchemaVersion,
                valid.CreatedAtUtc,
                fault == CheckpointRootFault.Framework
                    ? "wrong-framework"
                    : valid.FrameworkVersion,
                fault == CheckpointRootFault.Config
                    ? "wrong-config"
                    : valid.ConfigVersion,
                fault == CheckpointRootFault.Resources
                    ? "wrong-resources"
                    : valid.ResourceCatalogVersion,
                fault == CheckpointRootFault.NegativeFrame
                    ? -1L
                    : fault == CheckpointRootFault.FrameMax
                        ? long.MaxValue
                    : fault == CheckpointRootFault.NotImmediatelyPrior
                        ? 1L
                        : valid.Frame,
                valid.Entities,
                valid.GlobalCounters,
                valid.ModuleStates,
                valid.Metadata);
        }

        private static RuntimeSaveState CorruptTriggerPayload(RuntimeSaveState valid)
        {
            var moduleStates = new RuntimeModuleSaveState[valid.ModuleStates.Count];
            for (int index = 0; index < moduleStates.Length; index++)
            {
                RuntimeModuleSaveState module = valid.ModuleStates[index];
                RuntimeCustomState custom = module.CustomState;
                moduleStates[index] = new RuntimeModuleSaveState(
                    module.ModuleId,
                    module.SchemaVersion,
                    new RuntimeCustomState(custom.NumericTypeId, custom.TypeId,
                        custom.SchemaVersion,
                        "{"));
            }
            return new RuntimeSaveState(
                valid.SchemaVersion,
                valid.CreatedAtUtc,
                valid.FrameworkVersion,
                valid.ConfigVersion,
                valid.ResourceCatalogVersion,
                valid.Frame,
                valid.Entities,
                valid.GlobalCounters,
                moduleStates,
                valid.Metadata);
        }

        private static long ComputeHash(GameplayTriggerRuntime runtime, RuntimeFrame frame)
        {
            return new RuntimeHashCombiner(new IRuntimeHashContributor[]
            {
                new GameplayTriggerRuntimeHashContributor(runtime)
            }).ComputeHash(frame);
        }

        private static GameplayTriggerCatalog CreateCatalog(
            bool reliable,
            GameplayTriggerSignalOrdering signalOrdering =
                GameplayTriggerSignalOrdering.UnorderedMultiset)
        {
            GameplayTriggerDeliveryContract delivery = reliable
                ? GameplayTriggerDeliveryContract.ReliableDeferred
                : GameplayTriggerDeliveryContract.SameFrameReserved;
            var sink = new TestSink(new GameplayTriggerActionKindId(900), delivery);
            var action = new GameplayTriggerActionInstruction(
                sink.ActionKindId,
                GameplayTriggerTargetSelector.Owner);
            GameplayTriggerDefinition CreateDefinition(
                int definitionId,
                int sourceDefinitionId)
            {
                return new GameplayTriggerDefinition(
                    new GameplayTriggerDefinitionId(definitionId),
                    1,
                    new GameplayTriggerSourceDefinitionId(sourceDefinitionId),
                    new[]
                    {
                        new GameplayTriggerRouteDefinition(
                            new GameplayTriggerRouteId(1),
                            GameplayTriggerOccurrenceKind.Entered |
                            GameplayTriggerOccurrenceKind.Pulsed,
                            GameplayTriggerConditionProgram.Always,
                            new[] { action })
                    });
            }

            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[]
                {
                    new GameplayTriggerSourceDefinition(
                        new GameplayTriggerSourceDefinitionId(1),
                        GameplayTriggerSourceKind.Signal,
                        signalOrdering),
                    new GameplayTriggerSourceDefinition(
                        new GameplayTriggerSourceDefinitionId(2),
                        GameplayTriggerSourceKind.Occupancy),
                    new GameplayTriggerSourceDefinition(
                        new GameplayTriggerSourceDefinitionId(3),
                        GameplayTriggerSourceKind.Threshold,
                        enterThreshold: Fix64.FromInt(1),
                        exitThreshold: Fix64.Zero)
                },
                new[]
                {
                    CreateDefinition(10, 1),
                    CreateDefinition(20, 2),
                    CreateDefinition(30, 3)
                },
                new[]
                {
                    new GameplayTriggerBinding(
                        new GameplayTriggerBindingId(1),
                        new GameplayTriggerDefinitionId(10),
                        SignalBinding,
                        new GameplayEntityId(1, 1)),
                    new GameplayTriggerBinding(
                        new GameplayTriggerBindingId(2),
                        new GameplayTriggerDefinitionId(20),
                        OccupancyBinding,
                        new GameplayEntityId(1, 1)),
                    new GameplayTriggerBinding(
                        new GameplayTriggerBindingId(3),
                        new GameplayTriggerDefinitionId(30),
                        ScalarBinding,
                        new GameplayEntityId(1, 1)),
                    new GameplayTriggerBinding(
                        new GameplayTriggerBindingId(4),
                        new GameplayTriggerDefinitionId(10),
                        SecondSignalBinding,
                        new GameplayEntityId(1, 1))
                },
                new IGameplayTriggerActionSink[] { sink },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return catalog;
        }

        private static GameplayTriggerCatalog CreateMixedDeferredReliableCatalog()
        {
            var internalKind = new GameplayTriggerActionKindId(901);
            var reliableSink = new TestSink(
                new GameplayTriggerActionKindId(902),
                GameplayTriggerDeliveryContract.ReliableDeferred);
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
                        internalKind,
                        GameplayTriggerTargetSelector.ExplicitBindingTarget,
                        targetSlotId: 1),
                    new GameplayTriggerActionInstruction(
                        reliableSink.ActionKindId,
                        GameplayTriggerTargetSelector.Owner)
                });
            var definition = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(10),
                1,
                source.SourceDefinitionId,
                new[] { route });
            var binding = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(1),
                definition.DefinitionId,
                SignalBinding,
                new GameplayEntityId(1, 1),
                explicitTargets: new[]
                {
                    new GameplayTriggerBindingTarget(
                        1,
                        GameplayTriggerTargetRef.FromTriggerSourceBinding(SignalBinding))
                });

            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[] { binding },
                new IGameplayTriggerActionSink[] { reliableSink },
                new[]
                {
                    new GameplayTriggerInternalDeferredActionDescriptor(
                        internalKind,
                        GameplayTriggerInternalDeferredOperationKind.Reset)
                },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return catalog;
        }

        private static GameplayTriggerCatalog CreateInternalDeferredCatalog()
        {
            var internalKind = new GameplayTriggerActionKindId(901);
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal,
                GameplayTriggerSignalOrdering.UnorderedMultiset);
            var definition = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(10),
                1,
                source.SourceDefinitionId,
                new[]
                {
                    new GameplayTriggerRouteDefinition(
                        new GameplayTriggerRouteId(1),
                        GameplayTriggerOccurrenceKind.Pulsed,
                        GameplayTriggerConditionProgram.Always,
                        new[]
                        {
                            new GameplayTriggerActionInstruction(
                                internalKind,
                                GameplayTriggerTargetSelector.ExplicitBindingTarget,
                                targetSlotId: 1)
                        })
                });
            var binding = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(1),
                definition.DefinitionId,
                SignalBinding,
                new GameplayEntityId(1, 1),
                explicitTargets: new[]
                {
                    new GameplayTriggerBindingTarget(
                        1,
                        GameplayTriggerTargetRef.FromTriggerSourceBinding(SignalBinding))
                });
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[] { binding },
                Array.Empty<IGameplayTriggerActionSink>(),
                new[]
                {
                    new GameplayTriggerInternalDeferredActionDescriptor(
                        internalKind,
                        GameplayTriggerInternalDeferredOperationKind.Reset)
                },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return catalog;
        }

        private static GameplayTriggerRuntime CreateRuntime(
            GameplayTriggerCatalog catalog,
            GameplayTriggerRuntimeEventSource eventSource)
        {
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(
                    maxInputBatches: 8,
                    maxInputItems: 24,
                    maxRelations: 16,
                    maxOccurrences: 16,
                    maxCandidates: 16,
                    maxRouteStates: 16,
                    maxActivations: 16,
                    maxIntents: 16,
                    maxPreparedActions: 16,
                    maxConditionStackDepth: 16,
                    maxDeferredRecords: 8,
                    maxReliableOutboxRecords:
                        catalog.ReliableDeferredActionCount > 0 ? 8 : 0),
                eventSource);
        }

        private sealed class TestFactory : IGameplayTriggerReplaySessionFactory
        {
            private readonly GameplayTriggerCatalog _catalog;
            private readonly FactoryMode _mode;
            private readonly IRuntimeCommandValidator _validator;
            private readonly bool _mutateDuringLifecycle;
            private readonly int _observerCapacity;
            private readonly bool _enableReliableTarget;
            private readonly IGameplayTriggerRestoreEntityValidator _restoreEntityValidator;
            private readonly bool _disableRouter;
            private readonly bool _disableObserver;
            private readonly bool _addSecondObserver;
            private readonly bool _reverseObserverRegistration;
            private readonly bool _throwOnHostTick;
            private readonly bool _throwOnReliableTransfer;

            public TestFactory(
                GameplayTriggerCatalog catalog,
                FactoryMode mode = FactoryMode.Valid,
                IRuntimeCommandValidator validator = null,
                bool mutateDuringLifecycle = false,
                int observerCapacity = -1,
                bool enableReliableTarget = false,
                IGameplayTriggerRestoreEntityValidator restoreEntityValidator = null,
                bool disableRouter = false,
                bool disableObserver = false,
                bool addSecondObserver = false,
                bool reverseObserverRegistration = false,
                bool throwOnHostTick = false,
                bool throwOnReliableTransfer = false)
            {
                _catalog = catalog;
                _mode = mode;
                _validator = validator;
                _mutateDuringLifecycle = mutateDuringLifecycle;
                _observerCapacity = observerCapacity;
                _enableReliableTarget = enableReliableTarget;
                _restoreEntityValidator = restoreEntityValidator ?? AlwaysValidEntity.Instance;
                _disableRouter = disableRouter;
                _disableObserver = disableObserver;
                _addSecondObserver = addSecondObserver;
                _reverseObserverRegistration = reverseObserverRegistration;
                _throwOnHostTick = throwOnHostTick;
                _throwOnReliableTransfer = throwOnReliableTransfer;
            }

            public TestComposition Last { get; private set; }
            public int CreateCount { get; private set; }

            public void Create(
                in GameplayTriggerReplaySessionCreateContext context,
                GameplayTriggerReplaySessionBuilder builder)
            {
                CreateCount++;
                Last = TestComposition.Create(
                    _catalog,
                    new GameplayTriggerReplayBootstrapOptions(
                        FrameworkVersion,
                        ResourceHash,
                        context.ExpectedGameplayTriggerCatalogHash,
                        context.MaxCommandsPerFrame,
                        context.MaxSignalCommandItems,
                        context.MaxOccupancyCommandItems,
                        context.MaxScalarCommandItems),
                    context.StartFrame,
                    _mode,
                    _validator,
                    _mutateDuringLifecycle,
                    _observerCapacity,
                    _enableReliableTarget,
                    _restoreEntityValidator,
                    _disableRouter,
                    _disableObserver,
                    _addSecondObserver,
                    _reverseObserverRegistration,
                    _throwOnHostTick,
                    _throwOnReliableTransfer);
                Last.SourceRegistry.Seal();
                builder.SetComposition(
                    Last.Host,
                    Last.GameplayModule,
                    Last.Runtime,
                    Last.DeferredSystem,
                    Last.CommandSystem,
                    Last.EvaluationSystem,
                    Last.HandoffRouter,
                    Last.HandoffOutbox,
                    Last.SourceRegistry,
                    Last.RestoreCoordinator);
            }
        }

        private sealed class ThrowingFactory : IGameplayTriggerReplaySessionFactory
        {
            public void Create(
                in GameplayTriggerReplaySessionCreateContext context,
                GameplayTriggerReplaySessionBuilder builder)
            {
                throw new InvalidOperationException("Focused session factory failure.");
            }
        }

        private sealed class TestComposition
        {
            private TestComposition()
            {
            }

            public RuntimeHost Host { get; private set; }
            public GameplayRuntimeModule GameplayModule { get; private set; }
            public GameplayTriggerRuntime Runtime { get; private set; }
            public GameplayTriggerDeferredSystem DeferredSystem { get; private set; }
            public GameplayTriggerCommandSystem CommandSystem { get; private set; }
            public GameplayTriggerEvaluationSystem EvaluationSystem { get; private set; }
            public GameplayTriggerEventRouterSystem HandoffRouter { get; private set; }
            public GameplayTriggerReliableOutboxModule HandoffOutbox { get; private set; }
            public RuntimeCommandSourceRegistry SourceRegistry { get; private set; }
            public RuntimeSaveStateCoordinator RestoreCoordinator { get; private set; }
            public RuntimeCommandBuffer Buffer => GameplayModule.CommandBuffer;
            public GameplayTriggerRuntimeEventSource EventSource { get; private set; }
            public GameplayTriggerRuntimeEventObserverBuffer Observer { get; private set; }
            public GameplayTriggerRuntimeEventObserverBuffer SecondObserver { get; private set; }
            public CountingReliableTransfer ReliableTransfer { get; private set; }
            public ReliableTargetModule ReliableTarget { get; private set; }
            public List<string> LifecycleEvents { get; } = new List<string>();

            public static TestComposition Create(
                GameplayTriggerCatalog catalog,
                GameplayTriggerReplayBootstrapOptions options,
                RuntimeFrame startFrame,
                FactoryMode mode,
                IRuntimeCommandValidator validator,
                bool mutateDuringLifecycle,
                int observerCapacity = -1,
                bool enableReliableTarget = false,
                IGameplayTriggerRestoreEntityValidator restoreEntityValidator = null,
                bool disableRouter = false,
                bool disableObserver = false,
                bool addSecondObserver = false,
                bool reverseObserverRegistration = false,
                bool throwOnHostTick = false,
                bool throwOnReliableTransfer = false)
            {
                var result = new TestComposition();
                result.EventSource = new GameplayTriggerRuntimeEventSource(64);
                result.Runtime = CreateRuntime(catalog, result.EventSource);
                result.DeferredSystem = new GameplayTriggerDeferredSystem(result.Runtime);
                GameplayTriggerRuntime commandRuntime = result.Runtime;
                if (mode == FactoryMode.WrongRuntimeSystem)
                {
                    commandRuntime = new GameplayTriggerRuntime(
                        catalog,
                        result.Runtime.AuthorityOptions);
                }
                result.CommandSystem = new GameplayTriggerCommandSystem(
                    commandRuntime,
                    GameplayTriggerCommandSystem.DefaultSystemId,
                    mode == FactoryMode.WrongTriggerSystemPriority
                        ? GameplayTriggerCommandSystem.DefaultPriority + 1
                        : GameplayTriggerCommandSystem.DefaultPriority,
                    mode == FactoryMode.UndersizedCommandSystem
                        ? Math.Max(1, options.MaxSignalCommandItems - 1)
                        : options.MaxSignalCommandItems,
                    mode == FactoryMode.UndersizedCommandSystem
                        ? Math.Max(1, options.MaxOccupancyCommandItems - 1)
                        : options.MaxOccupancyCommandItems,
                    mode == FactoryMode.UndersizedCommandSystem
                        ? Math.Max(1, options.MaxScalarCommandItems - 1)
                        : options.MaxScalarCommandItems);
                result.EvaluationSystem = new GameplayTriggerEvaluationSystem(result.Runtime);
                GameplayTriggerRuntimeEventObserverRegistration[] observers = null;
                if (observerCapacity >= 0)
                {
                    result.Observer = new GameplayTriggerRuntimeEventObserverBuffer(observerCapacity);
                    if (disableObserver)
                        result.Observer.SetEnabled(false);
                    var first = new GameplayTriggerRuntimeEventObserverRegistration(
                        "test.replay.observer-a",
                        result.Observer);
                    if (!addSecondObserver)
                    {
                        observers = new[] { first };
                    }
                    else
                    {
                        result.SecondObserver =
                            new GameplayTriggerRuntimeEventObserverBuffer(observerCapacity);
                        if (disableObserver)
                            result.SecondObserver.SetEnabled(false);
                        var second = new GameplayTriggerRuntimeEventObserverRegistration(
                            "test.replay.observer-b",
                            result.SecondObserver);
                        observers = reverseObserverRegistration
                            ? new[] { second, first }
                            : new[] { first, second };
                    }
                }
                var registeredRouter = new GameplayTriggerEventRouterSystem(
                    result.EventSource,
                    observers);
                if (disableRouter)
                    registeredRouter.SetEnabled(false);
                result.HandoffRouter = registeredRouter;
                if (mode == FactoryMode.WrongSameIdRouter)
                {
                    var otherSource = new GameplayTriggerRuntimeEventSource(8);
                    _ = CreateRuntime(catalog, otherSource);
                    result.HandoffRouter = new GameplayTriggerEventRouterSystem(otherSource);
                }

                var pipeline = new GameplaySystemPipeline();
                pipeline.Add(result.DeferredSystem);
                pipeline.Add(result.CommandSystem);
                pipeline.Add(result.EvaluationSystem);
                pipeline.Add(registeredRouter);
                var buffer = new RuntimeCommandBuffer(
                    validator,
                    startFrame,
                    options.MaxCommandsPerFrame);
                result.GameplayModule = new GameplayRuntimeModule(
                    new GameplayWorld(),
                    new GameplayAbilityRegistry(),
                    buffer,
                    tickWorldAutomatically: false,
                    priority: mode == FactoryMode.WrongGameplayPriority ? 101 : 100,
                    systemPipeline: pipeline);

                result.SourceRegistry = new RuntimeCommandSourceRegistry(2);
                Assert.IsTrue(result.SourceRegistry.TryRegister(
                    ExternalSourceId,
                    RuntimeCommandSourceKind.Exogenous,
                    new[]
                    {
                        GameplayTriggerRuntimeCommandIds.SubmitSignalBatch,
                        GameplayTriggerRuntimeCommandIds.SubmitOccupancyBatch,
                        GameplayTriggerRuntimeCommandIds.SubmitScalarBatch
                    },
                    out RuntimeCommandSourceRegistrationResult registration),
                    registration.ErrorCode.ToString());
                Assert.IsTrue(result.SourceRegistry.TryRegister(
                    DerivedSourceId,
                    RuntimeCommandSourceKind.Derived,
                    new[] { GameplayTriggerRuntimeCommandIds.SubmitSignalBatch },
                    out registration),
                    registration.ErrorCode.ToString());

                var saveRegistry = new RuntimeSaveStateRegistry();
                var participant = new GameplayTriggerRuntimeSaveStateParticipant(
                    result.Runtime,
                    restoreEntityValidator ?? AlwaysValidEntity.Instance);
                IRuntimeSaveStateRestorer restorer = participant;
                if (mutateDuringLifecycle)
                    restorer = new RecordingRestorer(participant, result.LifecycleEvents);
                Assert.IsTrue(saveRegistry.Register(
                    GameplayTriggerRuntimeSaveStateParticipant.ParticipantId,
                    GameplayTriggerRuntimeSaveStateParticipant.DefaultSaveStateOrder,
                    participant,
                    restorer).Success);
                result.RestoreCoordinator = new RuntimeSaveStateCoordinator(saveRegistry);

                result.Host = new RuntimeHost();
                if (mode == FactoryMode.WrongSameIdModule)
                {
                    result.Host.RegisterModule(new RecordingModule(
                        GameplayRuntimeModule.DefaultModuleId,
                        result.LifecycleEvents));
                }
                else
                {
                    result.Host.RegisterModule(result.GameplayModule);
                }

                if (mutateDuringLifecycle)
                {
                    result.Host.RegisterModule(new BootstrapMutationModule(
                        result.Runtime,
                        result.LifecycleEvents));
                }

                if (throwOnHostTick)
                    result.Host.RegisterModule(new ThrowingTickModule());

                if (catalog.ReliableDeferredActionCount > 0)
                {
                    if (mode == FactoryMode.WrongSameIdOutbox)
                    {
                        var registeredOutbox = new GameplayTriggerReliableOutboxModule(result.Runtime);
                        result.Host.RegisterModule(registeredOutbox);
                        var otherRuntime = new GameplayTriggerRuntime(
                            catalog,
                            result.Runtime.AuthorityOptions);
                        result.HandoffOutbox = new GameplayTriggerReliableOutboxModule(otherRuntime);
                    }
                    else if (mode != FactoryMode.MissingReliableOutbox)
                    {
                        IGameplayTriggerReliableTargetTransfer[] transfers = null;
                        if (enableReliableTarget)
                        {
                            GameplayTriggerActionKindId reliableKind =
                                FindReliableActionKind(catalog);
                            result.ReliableTransfer = new CountingReliableTransfer(
                                reliableKind,
                                ReliableTargetModule.TargetModuleId,
                                throwOnReliableTransfer);
                            transfers = new IGameplayTriggerReliableTargetTransfer[]
                            {
                                result.ReliableTransfer
                            };
                        }
                        result.HandoffOutbox = new GameplayTriggerReliableOutboxModule(
                            result.Runtime,
                            transfers);
                        result.Host.RegisterModule(result.HandoffOutbox);
                        if (enableReliableTarget)
                        {
                            result.ReliableTarget = new ReliableTargetModule();
                            result.Host.RegisterModule(result.ReliableTarget);
                        }
                    }
                }

                for (int sinkIndex = 0; sinkIndex < catalog.ActionSinkCount; sinkIndex++)
                {
                    if (catalog.GetSink(sinkIndex) is TestSink sink)
                        sink.EventSource = result.EventSource;
                }

                return result;
            }

            private static GameplayTriggerActionKindId FindReliableActionKind(
                GameplayTriggerCatalog catalog)
            {
                for (int index = 0; index < catalog.ActionSinkCount; index++)
                {
                    GameplayTriggerActionSinkEntry entry = catalog.GetSinkEntry(index);
                    if (entry.DeliveryContract == GameplayTriggerDeliveryContract.ReliableDeferred)
                        return entry.ActionKindId;
                }

                throw new InvalidOperationException("Reliable catalog has no reliable action kind.");
            }
        }

        public enum FactoryMode
        {
            Valid = 0,
            WrongSameIdModule = 1,
            WrongSameIdRouter = 2,
            WrongRuntimeSystem = 3,
            MissingReliableOutbox = 4,
            WrongSameIdOutbox = 5,
            UndersizedCommandSystem = 6,
            WrongGameplayPriority = 7,
            WrongTriggerSystemPriority = 8
        }

        public enum HeaderFault
        {
            Schema = 0,
            Framework = 1,
            Config = 2,
            Resources = 3,
            StartFrameMax = 4
        }

        public enum CommandFault
        {
            UnknownSource = 0,
            UnknownCommand = 1,
            RegisteredDerivedSource = 2,
            ExternalProvenanceMismatch = 3,
            LegacyLifecycle = 4,
            WrongFrame = 5,
            MalformedEnvelope = 6,
            IncompleteBatch = 7,
            MalformedOccupancyBatch = 8,
            IncompleteOccupancyBatch = 9,
            MalformedScalarBatch = 10,
            IncompleteScalarBatch = 11,
            DuplicateSourceSubmission = 12
        }

        public enum CheckpointRootFault
        {
            NegativeFrame = 0,
            FrameMax = 1,
            NotImmediatelyPrior = 2,
            Framework = 3,
            Config = 4,
            Resources = 5
        }

        public enum RestoreFault
        {
            InvalidPayloadJson = 0,
            InvalidEntity = 1,
            ThrowingEntityValidator = 2
        }

        public enum NoAllocWorkload
        {
            SteadyAuthority = 0,
            DueInternalDeferred = 1,
            DueInternalAndReliableOutbox = 2
        }

        private sealed class BootstrapMutationModule : RuntimeModule
        {
            private readonly GameplayTriggerRuntime _runtime;
            private readonly List<string> _events;

            public BootstrapMutationModule(
                GameplayTriggerRuntime runtime,
                List<string> events)
                : base("test.replay.bootstrap-mutation", RuntimeTickStage.PreSimulation)
            {
                _runtime = runtime;
                _events = events;
            }

            public override void Initialize(RuntimeHostContext context)
            {
                _events.Add("initialize");
                Assert.IsTrue(_runtime.TrySubmitSignals(
                    RuntimeFrame.Zero,
                    SignalBinding,
                    GameplayTriggerSignalOrdering.UnorderedMultiset,
                    new[] { new GameplayTriggerSignal(99) },
                    out GameplayTriggerResult submitted),
                    submitted.ErrorCode.ToString());
                Assert.IsTrue(_runtime.Evaluate(RuntimeFrame.Zero).Success);
            }

            public override void Start(RuntimeHostContext context)
            {
                _events.Add("start");
            }
        }

        private sealed class RecordingModule : RuntimeModule
        {
            private readonly List<string> _events;

            public RecordingModule(string moduleId, List<string> events)
                : base(moduleId)
            {
                _events = events;
            }

            public override void Initialize(RuntimeHostContext context)
            {
                _events.Add("initialize");
            }
        }

        private sealed class ThrowingTickModule : RuntimeModule
        {
            public ThrowingTickModule()
                : base("test.replay.throwing-tick", RuntimeTickStage.PostSimulation)
            {
            }

            public override void Tick(RuntimeTickContext context)
            {
                throw new InvalidOperationException("Focused Host.Tick failure.");
            }
        }

        private sealed class RecordingRestorer : IRuntimeSaveStateRestorer
        {
            private readonly IRuntimeSaveStateRestorer _inner;
            private readonly List<string> _events;

            public RecordingRestorer(
                IRuntimeSaveStateRestorer inner,
                List<string> events)
            {
                _inner = inner;
                _events = events;
            }

            public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState)
            {
                _events.Add("restore");
                return _inner.RestoreSaveState(saveState);
            }
        }

        private sealed class RejectScalarValidator : IRuntimeCommandValidator
        {
            public RuntimeCommandValidationResult Validate(RuntimeCommand command)
            {
                if (command.CommandId != GameplayTriggerRuntimeCommandIds.SubmitScalarBatch)
                    return RuntimeCommandValidationResult.Accepted(command);

                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.InvalidPayload,
                    command,
                    command.Frame,
                    "Rejected by focused replay rollback test."));
            }
        }

        private sealed class AlwaysValidEntity : IGameplayTriggerRestoreEntityValidator
        {
            public static readonly AlwaysValidEntity Instance = new AlwaysValidEntity();

            public bool IsRestoredEntityValid(GameplayEntityId entityId) => true;
        }

        private sealed class RejectAllEntity : IGameplayTriggerRestoreEntityValidator
        {
            public static readonly RejectAllEntity Instance = new RejectAllEntity();

            public bool IsRestoredEntityValid(GameplayEntityId entityId) => false;
        }

        private sealed class ThrowingEntity : IGameplayTriggerRestoreEntityValidator
        {
            public static readonly ThrowingEntity Instance = new ThrowingEntity();

            public bool IsRestoredEntityValid(GameplayEntityId entityId)
            {
                throw new InvalidOperationException("Focused restore validator failure.");
            }
        }

        private sealed class TestSink : IGameplayTriggerActionSink
        {
            private int _nextToken;

            public TestSink(
                GameplayTriggerActionKindId actionKindId,
                GameplayTriggerDeliveryContract deliveryContract)
            {
                ActionKindId = actionKindId;
                DeliveryContract = deliveryContract;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract { get; }
            public GameplayTriggerRuntimeEventSource EventSource { get; set; }
            public bool PublishActionResult { get; set; }
            public int CommitCount { get; private set; }

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                token = ++_nextToken;
                return true;
            }

            public void Abort(int token)
            {
            }

            public void Commit(int token, in GameplayTriggerActionIntent intent)
            {
                CommitCount++;
                if (PublishActionResult && EventSource != null)
                {
                    Assert.IsTrue(EventSource.TryPublishActionResult(
                        intent.Frame,
                        intent.ActivationId,
                        intent.ActionIndex,
                        intent.Instruction.ActionKindId,
                        intent.Target,
                        GameplayTriggerRuntimeEventStatus.Succeeded));
                }
            }
        }

        private sealed class CountingReliableTransfer : IGameplayTriggerReliableTargetTransfer
        {
            private readonly bool _throwOnTransfer;

            public CountingReliableTransfer(
                GameplayTriggerActionKindId actionKindId,
                string targetModuleId,
                bool throwOnTransfer = false)
            {
                ActionKindId = actionKindId;
                TargetModuleId = targetModuleId;
                _throwOnTransfer = throwOnTransfer;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public string TargetModuleId { get; }
            public bool IsEnabled => true;
            public int CallCount { get; private set; }
            public RuntimeFrame LastFrame { get; private set; }
            public long LastSequence { get; private set; }

            public GameplayTriggerReliableTransferResult TryTransfer(
                RuntimeFrame currentFrame,
                in GameplayTriggerReliableOutboxRecord record)
            {
                CallCount++;
                LastFrame = currentFrame;
                LastSequence = record.Sequence;
                if (_throwOnTransfer)
                    throw new InvalidOperationException("Focused reliable transfer failure.");
                return new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.Accepted);
            }
        }

        private sealed class ReliableTargetModule : RuntimeModule
        {
            public const string TargetModuleId = "test.replay.reliable-target";

            public ReliableTargetModule()
                : base(TargetModuleId, RuntimeTickStage.Simulation, 200)
            {
            }
        }
    }
}
