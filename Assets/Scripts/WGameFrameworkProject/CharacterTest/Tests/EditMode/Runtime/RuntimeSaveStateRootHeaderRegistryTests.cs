using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeSaveStateRootHeaderRegistryTests
    {
        [Test]
        public void RootHeaderProvider_UsesReservedIdentityAndCapturesHeaderOnly()
        {
            var descriptor = CreateDescriptor();
            var provider = new RuntimeSaveStateRootHeaderProvider(descriptor);

            RuntimeSaveStateParticipant participant = provider.CreateParticipant();
            RuntimeSaveStateResult<RuntimeSaveState> result = provider.CaptureSaveState();

            Assert.AreEqual("mxframework.runtime.save-header", RuntimeSaveStateRootHeaderProvider.ParticipantId);
            Assert.AreEqual(0, RuntimeSaveStateRootHeaderProvider.ParticipantOrder);
            Assert.AreEqual(RuntimeSaveStateRootHeaderProvider.ParticipantId, participant.ParticipantId);
            Assert.AreEqual(RuntimeSaveStateRootHeaderProvider.ParticipantOrder, participant.Order);
            Assert.AreSame(provider, participant.Provider);
            Assert.IsNull(participant.Restorer);
            Assert.IsTrue(result.Success);
            Assert.IsTrue(descriptor.Matches(result.Value));
            Assert.AreEqual(DateTimeKind.Utc, result.Value.CreatedAtUtc.Kind);
            Assert.AreEqual(0L, result.Value.Frame);
            Assert.AreEqual(0, result.Value.Entities.Count);
            Assert.AreEqual(0, result.Value.GlobalCounters.Count);
            Assert.AreEqual(0, result.Value.ModuleStates.Count);
            Assert.AreEqual(0, result.Value.Metadata.Count);
        }

        [Test]
        public void RootHeaderDescriptor_IsOrdinalAndRejectsIncompleteIdentity()
        {
            RuntimeSaveStateRootHeaderDescriptor descriptor = CreateDescriptor();
            RuntimeSaveStateRootHeaderDescriptor equal = CreateDescriptor();
            var caseChanged = new RuntimeSaveStateRootHeaderDescriptor(0, "Framework-A", "config-a", "resources-a");

            Assert.AreEqual(descriptor, equal);
            Assert.AreEqual(descriptor.GetHashCode(), equal.GetHashCode());
            Assert.AreNotEqual(descriptor, caseChanged);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RuntimeSaveStateRootHeaderDescriptor(-1, "framework", "config", "resources"));
            Assert.Throws<ArgumentException>(() =>
                new RuntimeSaveStateRootHeaderDescriptor(0, "", "config", "resources"));
            Assert.Throws<ArgumentException>(() =>
                new RuntimeSaveStateRootHeaderDescriptor(0, "framework", " ", "resources"));
            Assert.Throws<ArgumentException>(() =>
                new RuntimeSaveStateRootHeaderDescriptor(0, "framework", "config", null));
        }

        [Test]
        public void AuthoritativePreflight_AcceptsUniqueFirstAuthorityAndRestorerOnlyBoundaries()
        {
            RuntimeSaveStateRegistry registry = CreateRegistryWithAuthority();
            registry.Register("negative-restorer", -10, null, new SuccessfulRestorer());
            registry.Register("zero-restorer", 0, null, new SuccessfulRestorer());
            registry.Register("body", 20, new EmptyProvider(), null);

            RuntimeSaveStateRegistryResult result = registry.PreflightAuthoritative();

            Assert.IsTrue(result.Success, result.Error != null ? result.Error.ToString() : string.Empty);
            Assert.IsFalse(registry.IsSealed);
            Assert.IsNull(registry.AuthoritativeSnapshot);
            Assert.IsTrue(registry.Register("after-preflight", 30, new EmptyProvider(), null).Success);
        }

        [Test]
        public void AuthoritativePreflight_RejectsMissingAuthority()
        {
            var registry = new RuntimeSaveStateRegistry();
            registry.Register("body", 10, new EmptyProvider(), null);

            AssertFailure(registry.PreflightAuthoritative(), RuntimeSaveStateRegistryErrorCode.MissingRootHeaderAuthority);
        }

        [Test]
        public void AuthoritativePreflight_RejectsReservedProviderWithoutAuthorityContract()
        {
            var registry = new RuntimeSaveStateRegistry();
            registry.Register(RuntimeSaveStateRootHeaderProvider.ParticipantId, 0, new EmptyProvider(), null);

            AssertFailure(registry.PreflightAuthoritative(), RuntimeSaveStateRegistryErrorCode.RootHeaderAuthorityContractMismatch);
        }

        [Test]
        public void AuthoritativePreflight_RejectsAuthorityAtWrongOrder()
        {
            var registry = new RuntimeSaveStateRegistry();
            registry.Register(RuntimeSaveStateRootHeaderProvider.ParticipantId, 1, new TestAuthority(CreateDescriptor()), null);

            AssertFailure(registry.PreflightAuthoritative(), RuntimeSaveStateRegistryErrorCode.RootHeaderAuthorityOrderMismatch);
        }

        [Test]
        public void AuthoritativePreflight_RejectsAuthorityUsingNonReservedId()
        {
            var registry = new RuntimeSaveStateRegistry();
            registry.Register("other-authority", 0, new TestAuthority(CreateDescriptor()), null);

            AssertFailure(registry.PreflightAuthoritative(), RuntimeSaveStateRegistryErrorCode.RootHeaderAuthorityIdMismatch);
        }

        [Test]
        public void AuthoritativePreflight_RejectsCompetingZeroOrderProvider()
        {
            RuntimeSaveStateRegistry registry = CreateRegistryWithAuthority();
            registry.Register("competitor", 0, new EmptyProvider(), null);

            AssertFailure(registry.PreflightAuthoritative(), RuntimeSaveStateRegistryErrorCode.CompetingRootHeaderProvider);
        }

        [Test]
        public void AuthoritativePreflight_RejectsNegativeOrderProvider()
        {
            RuntimeSaveStateRegistry registry = CreateRegistryWithAuthority();
            registry.Register("negative", -1, new EmptyProvider(), null);

            AssertFailure(registry.PreflightAuthoritative(), RuntimeSaveStateRegistryErrorCode.NegativeProviderOrder);
        }

        [Test]
        public void AuthoritativeSeal_FreezesOneStableSortedSnapshotAndRejectsMutation()
        {
            RuntimeSaveStateRegistry registry = CreateRegistryWithAuthority();
            registry.Register("z", 20, new EmptyProvider(), null);
            registry.Register("a", 20, new EmptyProvider(), null);
            registry.Register("restore-first", -1, null, new SuccessfulRestorer());

            RuntimeSaveStateRegistryResult firstSeal = registry.SealAuthoritative();
            RuntimeSaveStateRegistrySnapshot snapshot = registry.AuthoritativeSnapshot;
            RuntimeSaveStateRegistryResult secondSeal = registry.SealAuthoritative();
            RuntimeSaveStateRegistryResult mutation = registry.Register("later", 30, new EmptyProvider(), null);

            Assert.IsTrue(firstSeal.Success);
            Assert.IsTrue(secondSeal.Success);
            Assert.IsTrue(registry.IsSealed);
            Assert.AreSame(snapshot, registry.AuthoritativeSnapshot);
            Assert.AreSame(snapshot.Participants, registry.GetParticipantsInStableOrder());
            Assert.AreEqual(RuntimeSaveStateRootHeaderProvider.ParticipantId, snapshot.RootHeaderParticipant.ParticipantId);
            Assert.AreSame(snapshot.RootHeaderParticipant.Provider, snapshot.RootHeaderAuthority);
            CollectionAssert.AreEqual(
                new[] { "restore-first", RuntimeSaveStateRootHeaderProvider.ParticipantId, "a", "z" },
                ParticipantIds(snapshot.Participants));
            AssertFailure(mutation, RuntimeSaveStateRegistryErrorCode.RegistrySealed);
            Assert.AreEqual(4, registry.Count);
        }

        [Test]
        public void RegistryDuplicateId_PreservesLegacyResultAndAddsStructuredCode()
        {
            var registry = new RuntimeSaveStateRegistry();
            registry.Register("duplicate", 1, new EmptyProvider(), null);

            RuntimeSaveStateRegistryResult duplicate = registry.Register("duplicate", 2, new EmptyProvider(), null);

            AssertFailure(duplicate, RuntimeSaveStateRegistryErrorCode.DuplicateParticipantId);
            Assert.AreEqual("duplicate", duplicate.Error.ParticipantId);
            Assert.AreEqual(1, registry.Count);
        }

        private static RuntimeSaveStateRootHeaderDescriptor CreateDescriptor()
        {
            return new RuntimeSaveStateRootHeaderDescriptor(0, "framework-a", "config-a", "resources-a");
        }

        private static RuntimeSaveStateRegistry CreateRegistryWithAuthority()
        {
            var registry = new RuntimeSaveStateRegistry();
            registry.Register(new RuntimeSaveStateRootHeaderProvider(CreateDescriptor()).CreateParticipant());
            return registry;
        }

        private static void AssertFailure(
            RuntimeSaveStateRegistryResult result,
            RuntimeSaveStateRegistryErrorCode expectedCode)
        {
            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.Error);
            Assert.AreEqual(expectedCode, result.Error.Code, result.Error.ToString());
        }

        private static string[] ParticipantIds(IReadOnlyList<RuntimeSaveStateParticipant> participants)
        {
            var ids = new string[participants.Count];
            for (int i = 0; i < participants.Count; i++)
            {
                ids[i] = participants[i].ParticipantId;
            }

            return ids;
        }

        private sealed class EmptyProvider : IRuntimeSaveStateProvider
        {
            public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(null);
            }
        }

        private sealed class TestAuthority : IRuntimeSaveStateRootHeaderAuthority
        {
            public TestAuthority(RuntimeSaveStateRootHeaderDescriptor descriptor)
            {
                Descriptor = descriptor;
            }

            public RuntimeSaveStateRootHeaderDescriptor Descriptor { get; }

            public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(null);
            }
        }

        private sealed class SuccessfulRestorer : IRuntimeSaveStateRestorer
        {
            public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState)
            {
                return RuntimeSaveStateResult<bool>.Succeeded(true);
            }
        }
    }
}
