using System;
using System.Threading;
using System.Threading.Tasks;
using MxFramework.Persistence;
using MxFramework.Persistence.RuntimeBridge;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Persistence.RuntimeBridge
{
    public sealed class RuntimeSaveGameBridgeContractTests
    {
        [Test]
        public void CheckpointRequest_CopiesMetadataAndContainsNoBuildDescriptor()
        {
            var tags = new[] { new SaveGameKeyValue("mode", "original") };
            var display = new[] { new SaveGameKeyValue("title", "slot") };

            var request = new RuntimeSaveGameCheckpointRequest(
                new SaveGameProfileId("profile"),
                new SaveGameSlotId("slot"),
                new SaveGameGeneration(7),
                Utc(1),
                Utc(2),
                tags,
                display);
            tags[0] = new SaveGameKeyValue("mode", "mutated");
            display[0] = new SaveGameKeyValue("title", "mutated");

            Assert.That(request.GetCompatibilityTag(0).Value, Is.EqualTo("original"));
            Assert.That(request.GetDisplayMetadata(0).Value, Is.EqualTo("slot"));
            Assert.That(typeof(RuntimeSaveGameCheckpointRequest).GetProperty("Build"), Is.Null);
        }

        [Test]
        public void CheckpointRequest_ValidatesCanonicalEnvelopeMetadataAtConstruction()
        {
            SaveGameKeyValue[] maximumTags = CreateEntries(SaveGameLimits.MaximumCompatibilityTags);
            SaveGameKeyValue[] maximumDisplay = CreateEntries(SaveGameLimits.MaximumDisplayMetadataEntries);
            maximumTags[0] = new SaveGameKeyValue(
                new string('k', SaveGameLimits.MaximumCompatibilityTagKeyUtf8Bytes),
                new string('v', SaveGameLimits.MaximumCompatibilityTagValueUtf8Bytes));
            maximumDisplay[0] = new SaveGameKeyValue(
                new string('d', SaveGameLimits.MaximumDisplayMetadataKeyUtf8Bytes),
                new string('m', SaveGameLimits.MaximumDisplayMetadataValueUtf8Bytes));

            Assert.DoesNotThrow(() => CreateRequest(maximumTags, maximumDisplay));
            Assert.Throws<ArgumentException>(() => CreateRequest(
                CreateEntries(SaveGameLimits.MaximumCompatibilityTags + 1), null));
            Assert.Throws<ArgumentException>(() => CreateRequest(null,
                CreateEntries(SaveGameLimits.MaximumDisplayMetadataEntries + 1)));
            Assert.Throws<ArgumentException>(() => CreateRequest(new[]
            {
                new SaveGameKeyValue("duplicate", "a"),
                new SaveGameKeyValue("duplicate", "b")
            }, null));
            Assert.Throws<ArgumentException>(() => CreateRequest(new SaveGameKeyValue[1], null));
            Assert.Throws<ArgumentException>(() => CreateRequest(new[]
            {
                new SaveGameKeyValue(
                    new string('k', SaveGameLimits.MaximumCompatibilityTagKeyUtf8Bytes + 1), "v")
            }, null));
            Assert.Throws<ArgumentException>(() => CreateRequest(null, new[]
            {
                new SaveGameKeyValue("k",
                    new string('v', SaveGameLimits.MaximumDisplayMetadataValueUtf8Bytes + 1))
            }));
        }

        [Test]
        public void Compatibility_DefaultIsFailClosed()
        {
            RuntimeSaveGameCompatibilityResult result = default;

            Assert.That(result.Status, Is.EqualTo(RuntimeSaveGameCompatibilityStatus.Unknown));
            Assert.That(result.ErrorCode, Is.EqualTo(RuntimeSaveGameCompatibilityErrorCode.Unknown));
            Assert.That(result.IsCompatible, Is.False);
            Assert.That(RuntimeSaveGameCompatibilityResult.Compatible.ErrorCode, Is.EqualTo(RuntimeSaveGameCompatibilityErrorCode.None));
        }

        [Test]
        public void Compatibility_IncompatibleRequiresStableReason()
        {
            Assert.Throws<ArgumentException>(() =>
                RuntimeSaveGameCompatibilityResult.Incompatible(RuntimeSaveGameCompatibilityErrorCode.Unknown));

            RuntimeSaveGameCompatibilityResult result = RuntimeSaveGameCompatibilityResult.Incompatible(
                RuntimeSaveGameCompatibilityErrorCode.BuildMismatch);

            Assert.That(result.IsCompatible, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(RuntimeSaveGameCompatibilityErrorCode.BuildMismatch));
        }

        [Test]
        public void BridgeErrorDetail_PreservesRuntimeErrorsAndCopiesParticipantOrder()
        {
            var first = new RuntimeSaveStateParticipantError(
                "first",
                RuntimeSaveStateCoordinatorPhase.Restore,
                new RuntimeSaveStateError(RuntimeSaveStateErrorCode.UnknownEntity, "$.entities[0]", "missing"));
            var second = new RuntimeSaveStateParticipantError(
                "second",
                RuntimeSaveStateCoordinatorPhase.Restore,
                new RuntimeSaveStateError(RuntimeSaveStateErrorCode.UnknownBuff, "$.buffs[0]", "missing"));
            var errors = new[] { first, second };
            var runtimeError = new RuntimeSaveStateError(RuntimeSaveStateErrorCode.RootHeaderMismatch, "$.schemaVersion", "mismatch");
            var persistenceError = new SaveGameError(SaveGameErrorCode.InvalidEnvelope, SaveGameOperationPhase.Validation, "envelope", "invalid");

            var detail = new RuntimeSaveGameBridgeErrorDetail(
                RuntimeSaveGameBridgeErrorCode.ParticipantFailure,
                persistenceError,
                runtimeError,
                errors);
            errors[0] = second;

            Assert.That(detail.PersistenceError, Is.EqualTo(persistenceError));
            Assert.That(detail.RuntimeError, Is.SameAs(runtimeError));
            Assert.That(detail.GetParticipantError(0), Is.SameAs(first));
            Assert.That(detail.GetParticipantError(1), Is.SameAs(second));
        }

        [Test]
        public void BridgeResult_DefaultIsStructuredFailClosed_AndExplicitFailureRequiresMeaning()
        {
            RuntimeSaveGameBridgeResult<object> defaultResult = default;

            Assert.That(defaultResult.Success, Is.False);
            Assert.That(defaultResult.ErrorCode, Is.EqualTo(RuntimeSaveGameBridgeErrorCode.Uninitialized));
            Assert.That(defaultResult.HasErrorDetail, Is.False);
            Assert.That(RuntimeSaveGameBridgeResult<object>.Succeeded(new object()).ErrorCode, Is.EqualTo(RuntimeSaveGameBridgeErrorCode.None));
            Assert.Throws<ArgumentException>(() => new RuntimeSaveGameBridgeErrorDetail(
                RuntimeSaveGameBridgeErrorCode.None,
                SaveGameError.None,
                null,
                null));
            Assert.Throws<ArgumentException>(() => new RuntimeSaveGameBridgeErrorDetail(
                RuntimeSaveGameBridgeErrorCode.Uninitialized,
                SaveGameError.None,
                null,
                null));
        }

        [Test]
        public void BridgeResult_SuccessRejectsNullReferenceButAcceptsValueTypes()
        {
            Assert.Throws<ArgumentNullException>(() => RuntimeSaveGameBridgeResult<object>.Succeeded(null));
            RuntimeSaveGameBridgeResult<int> valueResult = RuntimeSaveGameBridgeResult<int>.Succeeded(0);
            Assert.That(valueResult.Success, Is.True);
            Assert.That(valueResult.Value, Is.Zero);
        }

        [Test]
        public void WritePlan_OwnerMismatchDisposeAndTerminalTransitionsAreLinearized()
        {
            var owner = new object();
            var foreignOwner = new object();
            var disposedReservation = new CountingWriteReservation();
            var disposedFirst = new RuntimeSaveGameWritePlan(owner, disposedReservation, CreateState(4), CreateRequest());

            Assert.That(disposedFirst.TryTransfer(foreignOwner), Is.False);
            Assert.That(disposedReservation.ReleaseCount, Is.Zero);
            disposedFirst.Dispose();
            disposedFirst.Dispose();
            Assert.That(disposedFirst.State, Is.EqualTo(RuntimeSaveGameWritePlanState.Released));
            Assert.That(disposedFirst.TryTransfer(owner), Is.False);
            Assert.That(disposedReservation.ReleaseCount, Is.EqualTo(1));

            var transferredReservation = new CountingWriteReservation();
            var transferredFirst = new RuntimeSaveGameWritePlan(owner, transferredReservation, CreateState(4), CreateRequest());
            Assert.That(transferredFirst.TryTransfer(owner), Is.True);
            transferredFirst.Dispose();
            Assert.That(transferredFirst.State, Is.EqualTo(RuntimeSaveGameWritePlanState.Transferred));
            Assert.That(transferredReservation.ReleaseCount, Is.Zero);
            Assert.That(transferredFirst.TryReleaseTransferred(foreignOwner), Is.False);
            Assert.That(transferredReservation.ReleaseCount, Is.Zero);
            Assert.That(transferredFirst.TryReleaseTransferred(owner), Is.True);
            Assert.That(transferredFirst.TryReleaseTransferred(owner), Is.False);
            Assert.That(transferredFirst.State, Is.EqualTo(RuntimeSaveGameWritePlanState.Released));
            Assert.That(transferredReservation.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void WritePlan_DisposeVsTransferRaceHasOneLinearizedWinnerAndOneRelease()
        {
            for (int iteration = 0; iteration < 64; iteration++)
            {
                var owner = new object();
                var reservation = new CountingWriteReservation();
                var plan = new RuntimeSaveGameWritePlan(owner, reservation, CreateState(4), CreateRequest());
                using (var start = new Barrier(3))
                {
                    bool transferred = false;
                    Task transfer = Task.Run(() =>
                    {
                        start.SignalAndWait();
                        transferred = plan.TryTransfer(owner);
                    });
                    Task dispose = Task.Run(() =>
                    {
                        start.SignalAndWait();
                        plan.Dispose();
                    });

                    start.SignalAndWait();
                    Task.WaitAll(transfer, dispose);

                    if (transferred)
                    {
                        Assert.That(plan.State, Is.EqualTo(RuntimeSaveGameWritePlanState.Transferred));
                        Assert.That(reservation.ReleaseCount, Is.Zero);
                        Assert.That(plan.TryReleaseTransferred(owner), Is.True);
                    }
                    else
                    {
                        Assert.That(plan.State, Is.EqualTo(RuntimeSaveGameWritePlanState.Released));
                    }

                    Assert.That(plan.State, Is.EqualTo(RuntimeSaveGameWritePlanState.Released));
                    Assert.That(reservation.ReleaseCount, Is.EqualTo(1));
                    Assert.That(plan.TryReleaseTransferred(owner), Is.False);
                    plan.Dispose();
                    Assert.That(reservation.ReleaseCount, Is.EqualTo(1));
                }
            }
        }

        [Test]
        public void Prepared_OwnerMismatchDisposeAndSuccessTerminalAreLinearized()
        {
            var owner = new object();
            var foreignOwner = new object();
            PreparedRuntimeSaveGame disposedFirst = CreatePrepared(owner, SaveGameRecoverySource.Primary, 5, 5);

            Assert.That(disposedFirst.TryClaimRestore(foreignOwner), Is.False);
            disposedFirst.Dispose();
            Assert.That(disposedFirst.TryClaimRestore(owner), Is.False);
            Assert.That(disposedFirst.State, Is.EqualTo(PreparedRuntimeSaveGameState.Released));

            PreparedRuntimeSaveGame claimedFirst = CreatePrepared(owner, SaveGameRecoverySource.Primary, 5, 5);
            Assert.That(claimedFirst.TryClaimRestore(owner), Is.True);
            claimedFirst.Dispose();
            Assert.That(claimedFirst.State, Is.EqualTo(PreparedRuntimeSaveGameState.RestoreClaimed));
            Assert.That(claimedFirst.TryMarkConsumed(foreignOwner), Is.False);
            Assert.That(claimedFirst.TryMarkConsumed(owner), Is.True);
            Assert.That(claimedFirst.TryMarkConsumed(owner), Is.False);
            Assert.That(claimedFirst.State, Is.EqualTo(PreparedRuntimeSaveGameState.Consumed));
            Assert.That(claimedFirst.TryClaimRestore(owner), Is.False);
        }

        [Test]
        public void Prepared_DisposeVsClaimRaceHasOneLinearizedWinnerAndNoIllegalState()
        {
            for (int iteration = 0; iteration < 64; iteration++)
            {
                var owner = new object();
                PreparedRuntimeSaveGame prepared = CreatePrepared(owner, SaveGameRecoverySource.Primary, 5, 5);
                using (var start = new Barrier(3))
                {
                    bool claimed = false;
                    Task claim = Task.Run(() =>
                    {
                        start.SignalAndWait();
                        claimed = prepared.TryClaimRestore(owner);
                    });
                    Task dispose = Task.Run(() =>
                    {
                        start.SignalAndWait();
                        prepared.Dispose();
                    });

                    start.SignalAndWait();
                    Task.WaitAll(claim, dispose);

                    if (claimed)
                    {
                        Assert.That(prepared.State, Is.EqualTo(PreparedRuntimeSaveGameState.RestoreClaimed));
                        Assert.That(prepared.TryReleaseClaimed(owner), Is.True);
                    }
                    else
                    {
                        Assert.That(prepared.State, Is.EqualTo(PreparedRuntimeSaveGameState.Released));
                    }

                    Assert.That(prepared.State, Is.EqualTo(PreparedRuntimeSaveGameState.Released));
                    Assert.That(prepared.TryClaimRestore(owner), Is.False);
                    Assert.That(prepared.TryReleaseClaimed(owner), Is.False);
                }
            }
        }

        [Test]
        public void Prepared_FailedAttemptReleasesAndCannotBeReused()
        {
            var owner = new object();
            PreparedRuntimeSaveGame prepared = CreatePrepared(owner, SaveGameRecoverySource.LastKnownGood, 8, 7);

            Assert.That(prepared.TryClaimRestore(owner), Is.True);
            Assert.That(prepared.TryReleaseClaimed(owner), Is.True);
            Assert.That(prepared.TryReleaseClaimed(owner), Is.False);
            Assert.That(prepared.State, Is.EqualTo(PreparedRuntimeSaveGameState.Released));
            Assert.That(prepared.TryClaimRestore(owner), Is.False);
            Assert.That(prepared.TryMarkConsumed(owner), Is.False);
        }

        [Test]
        public void RepeatedDispose_AllocatesZeroBytesAfterTerminalInitialization()
        {
            var owner = new object();
            var plan = new RuntimeSaveGameWritePlan(
                owner, new CountingWriteReservation(), CreateState(4), CreateRequest());
            PreparedRuntimeSaveGame prepared = CreatePrepared(owner, SaveGameRecoverySource.Primary, 5, 5);
            RuntimeSaveStateRestoreProfile profile = CreateRestoreProfile();
            var target = new FakeRestoreTarget(profile);
            Assert.That(target.TryAcquireRestoreLease(profile, out IRuntimeSaveGameCandidateRestoreLease lease,
                out _), Is.True);
            plan.Dispose();
            prepared.Dispose();
            lease.Dispose();

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 512; i++)
            {
                plan.Dispose();
                prepared.Dispose();
                lease.Dispose();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void Prepared_RejectsInvalidRecoveryProvenance()
        {
            var owner = new object();
            Assert.Throws<ArgumentOutOfRangeException>(() => CreatePrepared(owner, SaveGameRecoverySource.None, 5, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreatePrepared(owner, (SaveGameRecoverySource)99, 5, 5));
            Assert.Throws<ArgumentException>(() => CreatePrepared(owner, SaveGameRecoverySource.Primary, 5, 6));
            Assert.Throws<ArgumentException>(() => CreatePrepared(owner, SaveGameRecoverySource.LastKnownGood, 5, 6));
        }

        [Test]
        public void Prepared_AllowsExactPrimarySelectionOlderThanCurrentHead()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(
                new object(),
                SaveGameRecoverySource.Primary,
                5,
                4);

            Assert.That(prepared.OriginalGeneration, Is.EqualTo(new SaveGameGeneration(5)));
            Assert.That(prepared.SelectedGeneration, Is.EqualTo(new SaveGameGeneration(4)));
            Assert.That(prepared.RecoverySource, Is.EqualTo(SaveGameRecoverySource.Primary));
        }

        [Test]
        public void RestoreTarget_TryContractsReturnConsistentOutValuesAndRejectRepeatCompletion()
        {
            RuntimeSaveStateRestoreProfile expected = CreateRestoreProfile();
            RuntimeSaveStateRestoreProfile other = CreateRestoreProfile();
            var target = new FakeRestoreTarget(expected);

            Assert.That(target.TryAcquireRestoreLease(other, out IRuntimeSaveGameCandidateRestoreLease wrongLease, out RuntimeSaveGameTargetValidationError wrongError), Is.False);
            Assert.That(wrongLease, Is.Null);
            Assert.That(wrongError.Code, Is.EqualTo(RuntimeSaveGameTargetValidationErrorCode.InvalidComposition));

            Assert.That(target.TryAcquireRestoreLease(expected, out IRuntimeSaveGameCandidateRestoreLease lease, out RuntimeSaveGameTargetValidationError acquireError), Is.True);
            Assert.That(lease, Is.Not.Null);
            Assert.That(acquireError.IsNone, Is.True);
            Assert.That(lease.TargetId, Is.Not.Null.And.Not.Empty);
            Assert.That(lease.Coordinator, Is.Not.Null);
            Assert.That(lease.Coordinator.IsAuthoritative, Is.True);
            Assert.That(lease.RestoreProfile, Is.SameAs(expected));
            Assert.That(lease.Coordinator.RestoreProfile, Is.SameAs(expected));

            RuntimeSaveGameRestoreCompletion invalidCompletion = default;
            Assert.That(lease.TryCompleteRestore(in invalidCompletion, out RuntimeSaveGameTargetValidationError invalidFrame), Is.False);
            Assert.That(invalidFrame.Code, Is.EqualTo(RuntimeSaveGameTargetValidationErrorCode.InvalidFrame));
            RuntimeSaveGameRestoreCompletion completion = CreateCompletion();
            Assert.That(lease.TryCompleteRestore(in completion, out RuntimeSaveGameTargetValidationError completeError), Is.True);
            Assert.That(completeError.IsNone, Is.True);
            Assert.That(lease.TryCompleteRestore(in completion, out RuntimeSaveGameTargetValidationError repeatError), Is.False);
            Assert.That(repeatError.Code, Is.EqualTo(RuntimeSaveGameTargetValidationErrorCode.CompletionRejected));
        }

        [Test]
        public void RestoreTarget_EarlyDisposePoisonsCandidate()
        {
            RuntimeSaveStateRestoreProfile expected = CreateRestoreProfile();
            var target = new FakeRestoreTarget(expected);
            Assert.That(target.TryAcquireRestoreLease(expected, out IRuntimeSaveGameCandidateRestoreLease lease, out _), Is.True);

            lease.Dispose();

            RuntimeSaveGameRestoreCompletion completion = CreateCompletion();
            Assert.That(lease.TryCompleteRestore(in completion, out RuntimeSaveGameTargetValidationError error), Is.False);
            Assert.That(error.Code, Is.EqualTo(RuntimeSaveGameTargetValidationErrorCode.CompletionRejected));
        }

        [Test]
        public void ContradictoryTargetFixture_ExposesImpossibleOutCombinationsForServiceDefenseTests()
        {
            RuntimeSaveStateRestoreProfile profile = CreateRestoreProfile();
            var target = new RuntimeSaveGameContradictoryTargetFixture(profile);

            bool acquired = target.TryAcquireRestoreLease(
                profile,
                out IRuntimeSaveGameCandidateRestoreLease lease,
                out RuntimeSaveGameTargetValidationError error);

            Assert.That(acquired, Is.False);
            Assert.That(lease, Is.Not.Null);
            Assert.That(error.IsNone, Is.True);
            RuntimeSaveGameRestoreCompletion completion = CreateCompletion();
            Assert.That(lease.TryCompleteRestore(in completion, out RuntimeSaveGameTargetValidationError completionError), Is.True);
            Assert.That(completionError.IsNone, Is.False);
        }

        private static RuntimeSaveStateRestoreProfile CreateRestoreProfile()
        {
            return RuntimeSaveStateRestoreProfile.Create(
                new RuntimeSaveStateRootHeaderDescriptor(0, "framework", "config", "resources"),
                new RuntimeSaveStateMigrationPipeline());
        }

        private static RuntimeSaveGameCheckpointRequest CreateRequest()
        {
            return new RuntimeSaveGameCheckpointRequest(
                new SaveGameProfileId("profile"),
                new SaveGameSlotId("slot"),
                SaveGameGeneration.None,
                Utc(1),
                Utc(2),
                null,
                null);
        }

        private static RuntimeSaveGameCheckpointRequest CreateRequest(
            SaveGameKeyValue[] tags,
            SaveGameKeyValue[] display)
        {
            return new RuntimeSaveGameCheckpointRequest(
                new SaveGameProfileId("profile"),
                new SaveGameSlotId("slot"),
                SaveGameGeneration.None,
                Utc(1),
                Utc(2),
                tags,
                display);
        }

        private static SaveGameKeyValue[] CreateEntries(int count)
        {
            var entries = new SaveGameKeyValue[count];
            for (int i = 0; i < count; i++)
                entries[i] = new SaveGameKeyValue("key-" + i, "value-" + i);
            return entries;
        }

        private static RuntimeSaveState CreateState(long frame)
        {
            return new RuntimeSaveState(0, Utc(1), "framework", "config", "resources", frame, null, null, null);
        }

        private static PreparedRuntimeSaveGame CreatePrepared(
            object owner,
            SaveGameRecoverySource recoverySource,
            ulong originalGeneration,
            ulong selectedGeneration)
        {
            const long frame = 9;
            RuntimeSaveStateRestoreProfile profile = CreateRestoreProfile();
            RuntimeSaveStateRestorePlan plan = profile.Prepare(CreateState(frame)).Value;
            return new PreparedRuntimeSaveGame(
                owner,
                plan,
                new SaveGameProfileId("profile"),
                new SaveGameSlotId("slot"),
                new SaveGameGeneration(originalGeneration),
                new SaveGameGeneration(selectedGeneration),
                recoverySource,
                Utc(1),
                frame,
                frame + 1L);
        }

        private static RuntimeSaveGameRestoreCompletion CreateCompletion() =>
            new RuntimeSaveGameRestoreCompletion(
                new SaveGameProfileId("profile"),
                new SaveGameSlotId("slot"),
                new SaveGameGeneration(1),
                new SaveGameGeneration(1),
                SaveGameRecoverySource.Primary,
                Utc(1),
                9,
                10);

        private static DateTime Utc(int seconds)
        {
            return new DateTime(638000000000000000L + TimeSpan.TicksPerSecond * seconds, DateTimeKind.Utc);
        }

        private sealed class FakeRestoreTarget : IRuntimeSaveGameRestoreTarget
        {
            private readonly RuntimeSaveStateRestoreProfile _profile;
            private bool _acquired;

            internal FakeRestoreTarget(RuntimeSaveStateRestoreProfile profile)
            {
                _profile = profile;
            }

            public bool TryAcquireRestoreLease(
                RuntimeSaveStateRestoreProfile expectedRestoreProfile,
                out IRuntimeSaveGameCandidateRestoreLease lease,
                out RuntimeSaveGameTargetValidationError validationError)
            {
                if (!ReferenceEquals(_profile, expectedRestoreProfile))
                {
                    lease = null;
                    validationError = new RuntimeSaveGameTargetValidationError(
                        RuntimeSaveGameTargetValidationErrorCode.InvalidComposition,
                        "restoreProfile");
                    return false;
                }

                if (_acquired)
                {
                    lease = null;
                    validationError = new RuntimeSaveGameTargetValidationError(
                        RuntimeSaveGameTargetValidationErrorCode.Unavailable,
                        "target");
                    return false;
                }

                _acquired = true;
                RuntimeSaveStateRootHeaderDescriptor descriptor = _profile.Descriptor;
                var registry = new RuntimeSaveStateRegistry();
                registry.Register(new RuntimeSaveStateRootHeaderProvider(descriptor).CreateParticipant());
                RuntimeSaveStateCoordinatorFactoryResult factory =
                    RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(registry, _profile);
                if (!factory.Success)
                {
                    lease = null;
                    validationError = new RuntimeSaveGameTargetValidationError(
                        RuntimeSaveGameTargetValidationErrorCode.InvalidComposition,
                        "coordinator");
                    return false;
                }

                lease = new FakeRestoreLease(_profile, factory.Coordinator);
                validationError = RuntimeSaveGameTargetValidationError.None;
                return true;
            }
        }

        private sealed class CountingWriteReservation : IRuntimeSaveGameWriteReservation
        {
            private int _releaseCount;

            public int ReleaseCount => Volatile.Read(ref _releaseCount);

            public void Release()
            {
                Interlocked.Increment(ref _releaseCount);
            }
        }

        private sealed class FakeRestoreLease : IRuntimeSaveGameCandidateRestoreLease
        {
            private bool _completed;
            private bool _disposed;

            internal FakeRestoreLease(
                RuntimeSaveStateRestoreProfile profile,
                RuntimeSaveStateCoordinator coordinator)
            {
                RestoreProfile = profile;
                Coordinator = coordinator;
            }

            public string TargetId => "candidate";
            public RuntimeSaveStateCoordinator Coordinator { get; }
            public RuntimeSaveStateRestoreProfile RestoreProfile { get; }

            public bool TryCompleteRestore(
                in RuntimeSaveGameRestoreCompletion completion,
                out RuntimeSaveGameTargetValidationError validationError)
            {
                if (_disposed || _completed)
                {
                    validationError = new RuntimeSaveGameTargetValidationError(
                        RuntimeSaveGameTargetValidationErrorCode.CompletionRejected,
                        "completion");
                    return false;
                }

                if (completion.CompletedFrame < 0 || completion.CompletedFrame == long.MaxValue ||
                    completion.NextFrame != completion.CompletedFrame + 1L)
                {
                    validationError = new RuntimeSaveGameTargetValidationError(
                        RuntimeSaveGameTargetValidationErrorCode.InvalidFrame,
                        "frame");
                    return false;
                }

                _completed = true;
                validationError = RuntimeSaveGameTargetValidationError.None;
                return true;
            }

            public void Dispose()
            {
                _disposed = true;
            }
        }
    }
}
