using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeSaveStateOrchestrationTests
    {
        [Test]
        public void RuntimeSaveStateCoordinator_CapturesProvidersInStableOrder()
        {
            var calls = new List<string>();
            var registry = new RuntimeSaveStateRegistry();
            registry.Register("module-b", 10, new RecordingProvider("module-b", calls), null);
            registry.Register("module-c", 5, new RecordingProvider("module-c", calls), null);
            registry.Register("module-a", 10, new RecordingProvider("module-a", calls), null);
            var coordinator = new RuntimeSaveStateCoordinator(registry);

            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> result = coordinator.CaptureSaveState();

            Assert.IsTrue(result.Success, FirstError(result));
            CollectionAssert.AreEqual(new[] { "module-c", "module-a", "module-b" }, calls);
            CollectionAssert.AreEqual(
                new[] { "module-c", "module-a", "module-b" },
                ModuleIds(result.Value.ModuleStates));
        }

        [Test]
        public void RuntimeSaveStateCoordinator_RestoresParticipantsInStableOrder()
        {
            var calls = new List<string>();
            var registry = new RuntimeSaveStateRegistry();
            registry.Register("restore-b", 20, null, new RecordingRestorer("restore-b", calls));
            registry.Register("restore-c", 5, null, new RecordingRestorer("restore-c", calls));
            registry.Register("restore-a", 20, null, new RecordingRestorer("restore-a", calls));
            var coordinator = new RuntimeSaveStateCoordinator(registry);

            RuntimeSaveStateCoordinatorResult<bool> result = coordinator.RestoreSaveState(CreateState(0));

            Assert.IsTrue(result.Success, FirstError(result));
            Assert.IsTrue(result.Value);
            CollectionAssert.AreEqual(new[] { "restore-c", "restore-a", "restore-b" }, calls);
        }

        [Test]
        public void RuntimeSaveStateRegistry_RejectsDuplicateParticipantId()
        {
            var registry = new RuntimeSaveStateRegistry();
            RuntimeSaveStateRegistryResult first = registry.Register("duplicate", 0, new RecordingProvider("duplicate", null), null);
            RuntimeSaveStateRegistryResult second = registry.Register("duplicate", 1, new RecordingProvider("duplicate", null), null);

            Assert.IsTrue(first.Success);
            Assert.IsFalse(second.Success);
            Assert.AreEqual("duplicate", second.Error.ParticipantId);
            Assert.AreEqual(1, registry.Count);
        }

        [Test]
        public void RuntimeSaveStateCoordinator_WrapsProviderExceptionsAndKeepsLaterErrors()
        {
            var calls = new List<string>();
            var registry = new RuntimeSaveStateRegistry();
            registry.Register("throwing", 0, new ThrowingProvider("throwing", calls), null);
            registry.Register(
                "failing",
                1,
                new FailingProvider(
                    "failing",
                    calls,
                    new RuntimeSaveStateError(RuntimeSaveStateErrorCode.MissingConfig, "$.configVersion", "missing config")),
                null);
            var coordinator = new RuntimeSaveStateCoordinator(registry);

            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> result = coordinator.CaptureSaveState();

            Assert.IsFalse(result.Success);
            CollectionAssert.AreEqual(new[] { "throwing", "failing" }, calls);
            Assert.AreEqual(2, result.Errors.Count);
            Assert.AreEqual("throwing", result.Errors[0].ParticipantId);
            Assert.AreEqual(RuntimeSaveStateCoordinatorPhase.Capture, result.Errors[0].Phase);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Errors[0].Error.Code);
            Assert.IsInstanceOf<InvalidOperationException>(result.Errors[0].Error.Exception);
            Assert.AreEqual("failing", result.Errors[1].ParticipantId);
            Assert.AreEqual(RuntimeSaveStateErrorCode.MissingConfig, result.Errors[1].Error.Code);
        }

        [Test]
        public void RuntimeSaveStateCoordinator_AggregatesRestorerFailures()
        {
            var calls = new List<string>();
            var registry = new RuntimeSaveStateRegistry();
            registry.Register(
                "resource",
                0,
                null,
                new FailingRestorer(
                    "resource",
                    calls,
                    RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.MissingResource,
                        "$.moduleStates[0]",
                        "missing resource"))));
            registry.Register("false-result", 1, null, new FailingRestorer("false-result", calls, RuntimeSaveStateResult<bool>.Succeeded(false)));
            registry.Register("ok", 2, null, new RecordingRestorer("ok", calls));
            var coordinator = new RuntimeSaveStateCoordinator(registry);

            RuntimeSaveStateCoordinatorResult<bool> result = coordinator.RestoreSaveState(CreateState(0));

            Assert.IsFalse(result.Success);
            CollectionAssert.AreEqual(new[] { "resource", "false-result", "ok" }, calls);
            Assert.AreEqual(2, result.Errors.Count);
            Assert.AreEqual("resource", result.Errors[0].ParticipantId);
            Assert.AreEqual(RuntimeSaveStateErrorCode.MissingResource, result.Errors[0].Error.Code);
            Assert.AreEqual("false-result", result.Errors[1].ParticipantId);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Errors[1].Error.Code);
        }

        [Test]
        public void RuntimeSaveStateCoordinator_ReturnsMissingMigrationBeforeRestore()
        {
            var calls = new List<string>();
            var registry = new RuntimeSaveStateRegistry();
            registry.Register("restore", 0, null, new RecordingRestorer("restore", calls));
            var coordinator = new RuntimeSaveStateCoordinator(
                registry,
                new RuntimeSaveStateMigrationPipeline(),
                targetSchemaVersion: 1);

            RuntimeSaveStateCoordinatorResult<bool> result = coordinator.RestoreSaveState(CreateState(0));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, calls.Count);
            Assert.AreEqual(1, result.Errors.Count);
            Assert.AreEqual(RuntimeSaveStateCoordinatorPhase.Migration, result.Errors[0].Phase);
            Assert.AreEqual(RuntimeSaveStateErrorCode.MissingMigration, result.Errors[0].Error.Code);
            Assert.AreEqual(0, result.Errors[0].Error.SourceSchemaVersion);
            Assert.AreEqual(1, result.Errors[0].Error.TargetSchemaVersion);
        }

        [Test]
        public void AuthoritativeFactory_SealsCatalogFreezesDescriptorAndDoesNotCapture()
        {
            var calls = new List<string>();
            RuntimeSaveStateRootHeaderDescriptor descriptor = CreateDescriptor(0);
            var authority = new MutableAuthority(descriptor, CreateHeader(descriptor), calls);
            var registry = new RuntimeSaveStateRegistry();
            registry.Register(RuntimeSaveStateRootHeaderProvider.ParticipantId, 0, authority, null);

            RuntimeSaveStateCoordinatorFactoryResult result =
                RuntimeSaveStateCoordinator.CreateAuthoritative(registry);

            Assert.IsTrue(result.Success, result.Error != null ? result.Error.ToString() : string.Empty);
            Assert.IsNotNull(result.Coordinator);
            Assert.IsTrue(registry.IsSealed);
            Assert.AreEqual(0, calls.Count);
            Assert.AreEqual(1, authority.DescriptorReadCount);
            authority.DescriptorValue = CreateDescriptor(1);

            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> capture = result.Coordinator.CaptureSaveState();

            Assert.IsTrue(capture.Success, FirstError(capture));
            Assert.AreEqual(1, authority.DescriptorReadCount);
            Assert.AreEqual(0, capture.Value.SchemaVersion);
        }

        [Test]
        public void AuthoritativeFactory_FailureHasNoCoordinatorAndDoesNotInvokeProviders()
        {
            var calls = new List<string>();
            var registry = new RuntimeSaveStateRegistry();
            registry.Register("legacy", 1, new RecordingProvider("legacy", calls), null);

            RuntimeSaveStateCoordinatorFactoryResult result =
                RuntimeSaveStateCoordinator.CreateAuthoritative(registry);

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Coordinator);
            Assert.IsNotNull(result.Error);
            Assert.AreEqual(RuntimeSaveStateErrorCode.CompositionFailed, result.Error.Error.Code);
            Assert.AreEqual(RuntimeSaveStateCoordinatorPhase.Composition, result.Error.Phase);
            Assert.AreEqual(0, calls.Count);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AuthoritativeFactory_InvalidDescriptorGetterFailsWithoutCapture(bool throws)
        {
            var calls = new List<string>();
            var authority = new InvalidDescriptorAuthority(throws, calls);
            var registry = CreateAuthoritativeRegistry(authority);

            RuntimeSaveStateCoordinatorFactoryResult result =
                RuntimeSaveStateCoordinator.CreateAuthoritative(registry);

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Coordinator);
            Assert.IsFalse(registry.IsSealed);
            Assert.AreEqual(RuntimeSaveStateCoordinatorPhase.Composition, result.Error.Phase);
            Assert.AreEqual(RuntimeSaveStateErrorCode.CompositionFailed, result.Error.Error.Code);
            Assert.AreEqual(0, calls.Count);

            Assert.IsTrue(registry.Register("late-restorer", 10, null, new RecordingRestorer("late", calls)).Success);
            authority.Throws = false;
            authority.DescriptorValue = CreateDescriptor(0);
            RuntimeSaveStateCoordinatorFactoryResult retry =
                RuntimeSaveStateCoordinator.CreateAuthoritative(registry);
            Assert.IsTrue(retry.Success, retry.Error != null ? retry.Error.ToString() : string.Empty);
            Assert.IsTrue(registry.IsSealed);
            Assert.AreEqual(0, calls.Count);
        }

        [Test]
        public void AuthoritativeFactory_ReentrantRegistryMutationRejectsCommitWithoutSealing()
        {
            var registry = new RuntimeSaveStateRegistry();
            var authority = new ReentrantDescriptorAuthority(registry, CreateDescriptor(0));
            registry.Register(RuntimeSaveStateRootHeaderProvider.ParticipantId, 0, authority, null);

            RuntimeSaveStateCoordinatorFactoryResult result =
                RuntimeSaveStateCoordinator.CreateAuthoritative(registry);

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Coordinator);
            Assert.IsFalse(registry.IsSealed);
            Assert.AreEqual(RuntimeSaveStateCoordinatorPhase.Composition, result.Error.Phase);
            Assert.AreEqual(2, registry.Count);
        }

        [Test]
        public void AuthoritativeFactory_ReentrantSealIsRejectedWithoutStealingCompositionLease()
        {
            var registry = new RuntimeSaveStateRegistry();
            var authority = new ReentrantSealAuthority(registry, CreateDescriptor(0));
            registry.Register(RuntimeSaveStateRootHeaderProvider.ParticipantId, 0, authority, null);

            RuntimeSaveStateCoordinatorFactoryResult result =
                RuntimeSaveStateCoordinator.CreateAuthoritative(registry);

            Assert.IsTrue(result.Success, result.Error != null ? result.Error.ToString() : string.Empty);
            Assert.IsNotNull(result.Coordinator);
            Assert.IsTrue(registry.IsSealed);
            Assert.IsFalse(authority.NestedSealResult.Success);
            Assert.AreEqual(
                RuntimeSaveStateRegistryErrorCode.CompositionInProgress,
                authority.NestedSealResult.Error.Code);
            Assert.AreEqual(0, authority.CaptureCount);
        }

        [Test]
        public void AuthoritativeCapture_IgnoresLegacyHeadersAndPreservesBodiesInStableOrder()
        {
            RuntimeSaveStateRootHeaderDescriptor descriptor = CreateDescriptor(0);
            var registry = CreateAuthoritativeRegistry(new StaticAuthority(CreateHeader(descriptor), descriptor));
            registry.Register("body-b", 10, new StaticProvider(CreateBody("body-b", "wrong-b", "b", "2")), null);
            registry.Register("body-a", 10, new StaticProvider(CreateBody("body-a", string.Empty, "a", "1")), null);
            RuntimeSaveStateCoordinator coordinator = RuntimeSaveStateCoordinator.CreateAuthoritative(registry).Coordinator;

            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> result = coordinator.CaptureSaveState();

            Assert.IsTrue(result.Success, FirstError(result));
            Assert.IsTrue(descriptor.Matches(result.Value));
            CollectionAssert.AreEqual(new[] { "body-a", "body-b" }, ModuleIds(result.Value.ModuleStates));
            Assert.AreEqual("1", result.Value.Metadata["a"]);
            Assert.AreEqual("2", result.Value.Metadata["b"]);
        }

        [Test]
        public void AuthoritativeCapture_AuthorityFailureIsTerminalAndNeverFallsBack()
        {
            var calls = new List<string>();
            RuntimeSaveStateRootHeaderDescriptor descriptor = CreateDescriptor(0);
            var authority = new FailingAuthority(descriptor, calls);
            var registry = CreateAuthoritativeRegistry(authority);
            registry.Register("fallback", 1, new RecordingProvider("fallback", calls), null);
            RuntimeSaveStateCoordinator coordinator = RuntimeSaveStateCoordinator.CreateAuthoritative(registry).Coordinator;

            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> result = coordinator.CaptureSaveState();

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Value);
            CollectionAssert.AreEqual(new[] { RuntimeSaveStateRootHeaderProvider.ParticipantId }, calls);
        }

        [TestCase(AuthorityOutput.Null)]
        [TestCase(AuthorityOutput.Throw)]
        [TestCase(AuthorityOutput.Mismatch)]
        [TestCase(AuthorityOutput.Polluted)]
        [TestCase(AuthorityOutput.DefaultFailure)]
        [TestCase(AuthorityOutput.NonZeroFrame)]
        public void AuthoritativeCapture_InvalidAuthorityOutputIsTerminal(AuthorityOutput output)
        {
            var calls = new List<string>();
            RuntimeSaveStateRootHeaderDescriptor descriptor = CreateDescriptor(0);
            var registry = CreateAuthoritativeRegistry(new InvalidAuthority(descriptor, output, calls));
            registry.Register("later", 1, new RecordingProvider("later", calls), null);
            RuntimeSaveStateCoordinator coordinator = RuntimeSaveStateCoordinator.CreateAuthoritative(registry).Coordinator;

            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> result = coordinator.CaptureSaveState();

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Value);
            CollectionAssert.AreEqual(new[] { RuntimeSaveStateRootHeaderProvider.ParticipantId }, calls);
            Assert.IsFalse(result.FirstError.Error.IsNone);
            if (output == AuthorityOutput.NonZeroFrame)
            {
                Assert.AreEqual(RuntimeSaveStateErrorCode.RootHeaderPolluted, result.FirstError.Error.Code);
                Assert.AreEqual("$.frame", result.FirstError.Error.Path);
            }
        }

        [Test]
        public void AuthoritativeCapture_BodyFailureSuppressesPartialDocument()
        {
            RuntimeSaveStateRootHeaderDescriptor descriptor = CreateDescriptor(0);
            var registry = CreateAuthoritativeRegistry(new StaticAuthority(CreateHeader(descriptor), descriptor));
            registry.Register("ok", 1, new StaticProvider(CreateBody("ok", "legacy", "key", "value")), null);
            registry.Register("failed", 2, new FailingProvider(
                "failed",
                new List<string>(),
                new RuntimeSaveStateError(RuntimeSaveStateErrorCode.MissingConfig, "$", "failed")), null);
            RuntimeSaveStateCoordinator coordinator = RuntimeSaveStateCoordinator.CreateAuthoritative(registry).Coordinator;

            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> result = coordinator.CaptureSaveState();

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Value);
        }

        [Test]
        public void AuthoritativeRestore_PreflightsExactDescriptorBeforeRestorerSideEffects()
        {
            var calls = new List<string>();
            RuntimeSaveStateRootHeaderDescriptor descriptor = CreateDescriptor(0);
            var registry = CreateAuthoritativeRegistry(new StaticAuthority(CreateHeader(descriptor), descriptor));
            registry.Register("restore", 1, null, new RecordingRestorer("restore", calls));
            RuntimeSaveStateCoordinator coordinator = RuntimeSaveStateCoordinator.CreateAuthoritative(registry).Coordinator;

            RuntimeSaveStateCoordinatorResult<bool> result = coordinator.RestoreSaveState(CreateState(0));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.RootHeaderMismatch, result.FirstError.Error.Code);
            Assert.AreEqual("$.frameworkVersion", result.FirstError.Error.Path);
            Assert.AreEqual(RuntimeSaveStateCoordinatorPhase.Restore, result.FirstError.Phase);
            Assert.AreEqual(RuntimeSaveStateRootHeaderProvider.ParticipantId, result.FirstError.ParticipantId);
            Assert.AreEqual(0, calls.Count);
        }

        [Test]
        public void AuthoritativeFactory_FreezesMigrationCatalogAndTargetSchema()
        {
            var calls = new List<string>();
            RuntimeSaveStateRootHeaderDescriptor descriptor = CreateDescriptor(2);
            var registry = CreateAuthoritativeRegistry(new StaticAuthority(CreateHeader(descriptor), descriptor));
            registry.Register("restore", 1, null, new RecordingRestorer("restore", calls));
            var pipeline = new RuntimeSaveStateMigrationPipeline();
            pipeline.Register(new HeaderMigration(0, 1, "framework", "config", "resources"));
            RuntimeSaveStateCoordinator coordinator =
                RuntimeSaveStateCoordinator.CreateAuthoritative(registry, pipeline).Coordinator;
            pipeline.Register(new HeaderMigration(1, 2, "framework", "config", "resources"));

            RuntimeSaveStateCoordinatorResult<bool> result = coordinator.RestoreSaveState(CreateState(0));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.MissingMigration, result.FirstError.Error.Code);
            Assert.AreEqual(0, calls.Count);
        }

        [Test]
        public void AuthoritativeRestore_MigratesThenAllowsExactDescriptor()
        {
            var calls = new List<string>();
            RuntimeSaveStateRootHeaderDescriptor descriptor = CreateDescriptor(1);
            var registry = CreateAuthoritativeRegistry(new StaticAuthority(CreateHeader(descriptor), descriptor));
            registry.Register("restore", 1, null, new RecordingRestorer("restore", calls));
            var pipeline = new RuntimeSaveStateMigrationPipeline();
            pipeline.Register(new HeaderMigration(0, 1, "framework", "config", "resources"));
            RuntimeSaveStateCoordinator coordinator =
                RuntimeSaveStateCoordinator.CreateAuthoritative(registry, pipeline).Coordinator;

            RuntimeSaveStateCoordinatorResult<bool> result = coordinator.RestoreSaveState(CreateState(0));

            Assert.IsTrue(result.Success, FirstError(result));
            CollectionAssert.AreEqual(new[] { "restore" }, calls);
        }

        [Test]
        public void AuthoritativeFactory_FreezesMutableMigrationTopology()
        {
            var calls = new List<string>();
            RuntimeSaveStateRootHeaderDescriptor descriptor = CreateDescriptor(1);
            var registry = CreateAuthoritativeRegistry(new StaticAuthority(CreateHeader(descriptor), descriptor));
            registry.Register("restore", 1, null, new RecordingRestorer("restore", calls));
            var migration = new MutableTopologyMigration(0, 1);
            var pipeline = new RuntimeSaveStateMigrationPipeline();
            pipeline.Register(migration);
            RuntimeSaveStateCoordinator coordinator =
                RuntimeSaveStateCoordinator.CreateAuthoritative(registry, pipeline).Coordinator;
            migration.FromSchemaVersion = 5;
            migration.ToSchemaVersion = 9;

            RuntimeSaveStateCoordinatorResult<bool> result = coordinator.RestoreSaveState(CreateState(0));

            Assert.IsTrue(result.Success, FirstError(result));
            CollectionAssert.AreEqual(new[] { "restore" }, calls);
        }

        [Test]
        public void RestoreProfile_FreezesMutableTopologyAndPreparesWithoutRestorers()
        {
            var migration = new MutableTopologyMigration(0, 1);
            var pipeline = new RuntimeSaveStateMigrationPipeline();
            pipeline.Register(migration);
            RuntimeSaveStateRestoreProfile profile = RuntimeSaveStateRestoreProfile.Create(CreateDescriptor(1), pipeline);
            migration.FromSchemaVersion = 4;
            migration.ToSchemaVersion = 8;

            RuntimeSaveStateResult<RuntimeSaveStateRestorePlan> result = profile.Prepare(CreateState(0));

            Assert.IsTrue(result.Success, result.Error.ToString());
            Assert.AreSame(profile, result.Value.Profile);
            Assert.AreEqual(1, result.Value.State.SchemaVersion);
            Assert.AreEqual(7L, result.Value.State.Frame);
        }

        [Test]
        public void RestoreProfile_RejectsFrameMutationAndDescriptorMismatch()
        {
            var framePipeline = new RuntimeSaveStateMigrationPipeline();
            framePipeline.Register(new FrameChangingMigration());
            RuntimeSaveStateRestoreProfile frameProfile = RuntimeSaveStateRestoreProfile.Create(CreateDescriptor(1), framePipeline);
            RuntimeSaveStateRestoreProfile descriptorProfile = RuntimeSaveStateRestoreProfile.Create(CreateDescriptor(0), null);

            RuntimeSaveStateResult<RuntimeSaveStateRestorePlan> frame = frameProfile.Prepare(CreateState(0));
            RuntimeSaveStateResult<RuntimeSaveStateRestorePlan> descriptor = descriptorProfile.Prepare(CreateState(0));

            Assert.IsFalse(frame.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.FrameChangedDuringMigration, frame.Error.Code);
            Assert.AreEqual("$.frame", frame.Error.Path);
            Assert.IsFalse(descriptor.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.RootHeaderMismatch, descriptor.Error.Code);
            Assert.AreEqual("$.frameworkVersion", descriptor.Error.Path);
        }

        [Test]
        public void RestoreProfile_RejectsIntermediateFrameMutationEvenWhenLaterStepRestoresOriginalFrame()
        {
            var pipeline = new RuntimeSaveStateMigrationPipeline();
            pipeline.Register(new FrameDeltaMigration(0, 1, 1L));
            pipeline.Register(new FrameDeltaMigration(1, 2, -1L));
            RuntimeSaveStateRestoreProfile profile = RuntimeSaveStateRestoreProfile.Create(
                CreateDescriptor(2), pipeline);

            RuntimeSaveStateResult<RuntimeSaveStateRestorePlan> result = profile.Prepare(CreateState(0));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.FrameChangedDuringMigration, result.Error.Code);
            Assert.AreEqual("$.frame", result.Error.Path);
            Assert.AreEqual(0, result.Error.SourceSchemaVersion);
            Assert.AreEqual(1, result.Error.TargetSchemaVersion);
        }

        [Test]
        public void RestorePrepared_RejectsCrossProfileAndLegacyWithoutRestorerSideEffects()
        {
            var calls = new List<string>();
            RuntimeSaveStateRootHeaderDescriptor descriptor = CreateDescriptor(0);
            RuntimeSaveStateRestoreProfile firstProfile = RuntimeSaveStateRestoreProfile.Create(descriptor, null);
            RuntimeSaveStateRestoreProfile secondProfile = RuntimeSaveStateRestoreProfile.Create(descriptor, null);
            RuntimeSaveStateRestorePlan plan = firstProfile.Prepare(CreateHeader(descriptor)).Value;
            var authoritativeRegistry = CreateAuthoritativeRegistry(new StaticAuthority(CreateHeader(descriptor), descriptor));
            authoritativeRegistry.Register("restore", 1, null, new RecordingRestorer("restore", calls));
            RuntimeSaveStateCoordinator authoritative =
                RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(authoritativeRegistry, secondProfile).Coordinator;
            var legacyRegistry = new RuntimeSaveStateRegistry();
            legacyRegistry.Register("legacy", 1, null, new RecordingRestorer("legacy", calls));
            var legacy = new RuntimeSaveStateCoordinator(legacyRegistry);

            RuntimeSaveStateCoordinatorResult<bool> cross = authoritative.RestorePrepared(plan);
            RuntimeSaveStateCoordinatorResult<bool> legacyResult = legacy.RestorePrepared(plan);

            Assert.IsFalse(cross.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.RestoreProfileMismatch, cross.FirstError.Error.Code);
            Assert.IsFalse(legacyResult.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.RestoreProfileMismatch, legacyResult.FirstError.Error.Code);
            Assert.AreEqual(0, calls.Count);
        }

        [Test]
        public void AuthoritativeFactory_ProfileOverloadRequiresExactAuthorityDescriptor()
        {
            RuntimeSaveStateRootHeaderDescriptor authorityDescriptor = CreateDescriptor(0);
            var registry = CreateAuthoritativeRegistry(new StaticAuthority(CreateHeader(authorityDescriptor), authorityDescriptor));
            var mismatched = RuntimeSaveStateRestoreProfile.Create(
                new RuntimeSaveStateRootHeaderDescriptor(0, "other", "config", "resources"),
                null);

            RuntimeSaveStateCoordinatorFactoryResult result =
                RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(registry, mismatched);

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Coordinator);
            Assert.IsFalse(registry.IsSealed);
        }

        [Test]
        public void AuthoritativeFactory_ExplicitNullRetainsMigrationPipelineOverload()
        {
            RuntimeSaveStateRootHeaderDescriptor descriptor = CreateDescriptor(0);
            var registry = CreateAuthoritativeRegistry(new StaticAuthority(CreateHeader(descriptor), descriptor));

            RuntimeSaveStateCoordinatorFactoryResult result =
                RuntimeSaveStateCoordinator.CreateAuthoritative(registry, null);

            Assert.IsTrue(result.Success, result.Error != null ? result.Error.ToString() : string.Empty);
            Assert.IsTrue(result.Coordinator.IsAuthoritative);
            Assert.IsNotNull(result.Coordinator.RestoreProfile);
        }

        [Test]
        public void AuthoritativeFactory_ProfileCanBindOnlyOneCoordinator()
        {
            RuntimeSaveStateRootHeaderDescriptor descriptor = CreateDescriptor(0);
            RuntimeSaveStateRestoreProfile profile = RuntimeSaveStateRestoreProfile.Create(descriptor, null);
            var firstRegistry = CreateAuthoritativeRegistry(new StaticAuthority(CreateHeader(descriptor), descriptor));
            var secondRegistry = CreateAuthoritativeRegistry(new StaticAuthority(CreateHeader(descriptor), descriptor));

            RuntimeSaveStateCoordinatorFactoryResult first =
                RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(firstRegistry, profile);
            RuntimeSaveStateCoordinatorFactoryResult second =
                RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(secondRegistry, profile);

            Assert.IsTrue(first.Success, first.Error != null ? first.Error.ToString() : string.Empty);
            Assert.AreSame(profile, first.Coordinator.RestoreProfile);
            Assert.IsTrue(first.Coordinator.IsAuthoritative);
            Assert.IsFalse(second.Success);
            Assert.IsNull(second.Coordinator);
            Assert.IsFalse(secondRegistry.IsSealed);
        }

        [Test]
        public void AuthoritativeFactory_FailedCommitReleasesProfileClaimForRetry()
        {
            RuntimeSaveStateRootHeaderDescriptor descriptor = CreateDescriptor(0);
            RuntimeSaveStateRestoreProfile profile = RuntimeSaveStateRestoreProfile.Create(descriptor, null);
            var registry = new RuntimeSaveStateRegistry();
            var authority = new ReentrantDescriptorAuthority(registry, descriptor);
            registry.Register(RuntimeSaveStateRootHeaderProvider.ParticipantId, 0, authority, null);

            RuntimeSaveStateCoordinatorFactoryResult failed =
                RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(registry, profile);
            RuntimeSaveStateCoordinatorFactoryResult retry =
                RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(registry, profile);

            Assert.IsFalse(failed.Success);
            Assert.IsTrue(retry.Success, retry.Error != null ? retry.Error.ToString() : string.Empty);
            Assert.AreSame(profile, retry.Coordinator.RestoreProfile);
        }

        [Test]
        public void RestoreProfile_NormalizesDefaultMigrationFailure()
        {
            var pipeline = new RuntimeSaveStateMigrationPipeline();
            pipeline.Register(new DefaultFailureMigration());
            RuntimeSaveStateRestoreProfile profile = RuntimeSaveStateRestoreProfile.Create(CreateDescriptor(1), pipeline);

            RuntimeSaveStateResult<RuntimeSaveStateRestorePlan> result = profile.Prepare(CreateState(0));

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.Error);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Error.Code);
            Assert.AreEqual("$.schemaVersion", result.Error.Path);
        }

        private static RuntimeSaveState CreateState(int schemaVersion)
        {
            return new RuntimeSaveState(
                schemaVersion,
                new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
                "0.1",
                "config",
                "resources",
                7L,
                null,
                null,
                null,
                null);
        }

        private static RuntimeSaveState CreateModuleState(string moduleId)
        {
            return new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
                "0.1",
                "config",
                "resources",
                7L,
                null,
                null,
                new[] { new RuntimeModuleSaveState(moduleId, 0, new RuntimeCustomState(TestCustomStateTypeIds.TestModule, "test.module", 0, "{}")) },
                null);
        }

        private static RuntimeSaveStateRootHeaderDescriptor CreateDescriptor(int schemaVersion)
        {
            return new RuntimeSaveStateRootHeaderDescriptor(schemaVersion, "framework", "config", "resources");
        }

        private static RuntimeSaveState CreateHeader(RuntimeSaveStateRootHeaderDescriptor descriptor)
        {
            return new RuntimeSaveState(
                descriptor.SchemaVersion,
                new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc),
                descriptor.FrameworkVersion,
                descriptor.ConfigVersion,
                descriptor.ResourceCatalogVersion,
                0L,
                null,
                null,
                null,
                null);
        }

        private static RuntimeSaveState CreateBody(string moduleId, string frameworkVersion, string metadataKey, string metadataValue)
        {
            return new RuntimeSaveState(
                99,
                new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                frameworkVersion,
                "legacy-config",
                "legacy-resources",
                99L,
                null,
                null,
                new[] { new RuntimeModuleSaveState(moduleId, 0, new RuntimeCustomState(TestCustomStateTypeIds.TestModule, "test.module", 0, "{}")) },
                new Dictionary<string, string> { { metadataKey, metadataValue } });
        }

        private static RuntimeSaveStateRegistry CreateAuthoritativeRegistry(IRuntimeSaveStateRootHeaderAuthority authority)
        {
            var registry = new RuntimeSaveStateRegistry();
            registry.Register(RuntimeSaveStateRootHeaderProvider.ParticipantId, 0, authority, null);
            return registry;
        }

        private static string[] ModuleIds(IReadOnlyList<RuntimeModuleSaveState> moduleStates)
        {
            var ids = new string[moduleStates.Count];
            for (int i = 0; i < moduleStates.Count; i++)
            {
                ids[i] = moduleStates[i].ModuleId;
            }

            return ids;
        }

        private static string FirstError<T>(RuntimeSaveStateCoordinatorResult<T> result)
        {
            return result.FirstError != null ? result.FirstError.ToString() : string.Empty;
        }

        private sealed class RecordingProvider : IRuntimeSaveStateProvider
        {
            private readonly string _participantId;
            private readonly List<string> _calls;

            public RecordingProvider(string participantId, List<string> calls)
            {
                _participantId = participantId;
                _calls = calls;
            }

            public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
            {
                if (_calls != null)
                {
                    _calls.Add(_participantId);
                }

                return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(CreateModuleState(_participantId));
            }
        }

        private sealed class ThrowingProvider : IRuntimeSaveStateProvider
        {
            private readonly string _participantId;
            private readonly List<string> _calls;

            public ThrowingProvider(string participantId, List<string> calls)
            {
                _participantId = participantId;
                _calls = calls;
            }

            public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
            {
                _calls.Add(_participantId);
                throw new InvalidOperationException("provider failed");
            }
        }

        private sealed class FailingProvider : IRuntimeSaveStateProvider
        {
            private readonly string _participantId;
            private readonly List<string> _calls;
            private readonly RuntimeSaveStateError _error;

            public FailingProvider(string participantId, List<string> calls, RuntimeSaveStateError error)
            {
                _participantId = participantId;
                _calls = calls;
                _error = error;
            }

            public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
            {
                _calls.Add(_participantId);
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(_error);
            }
        }

        private sealed class RecordingRestorer : IRuntimeSaveStateRestorer
        {
            private readonly string _participantId;
            private readonly List<string> _calls;

            public RecordingRestorer(string participantId, List<string> calls)
            {
                _participantId = participantId;
                _calls = calls;
            }

            public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState)
            {
                _calls.Add(_participantId);
                return RuntimeSaveStateResult<bool>.Succeeded(true);
            }
        }

        private sealed class FailingRestorer : IRuntimeSaveStateRestorer
        {
            private readonly string _participantId;
            private readonly List<string> _calls;
            private readonly RuntimeSaveStateResult<bool> _result;

            public FailingRestorer(string participantId, List<string> calls, RuntimeSaveStateResult<bool> result)
            {
                _participantId = participantId;
                _calls = calls;
                _result = result;
            }

            public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState)
            {
                _calls.Add(_participantId);
                return _result;
            }
        }

        private sealed class StaticProvider : IRuntimeSaveStateProvider
        {
            private readonly RuntimeSaveState _state;

            public StaticProvider(RuntimeSaveState state)
            {
                _state = state;
            }

            public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(_state);
            }
        }

        private class StaticAuthority : IRuntimeSaveStateRootHeaderAuthority
        {
            private readonly RuntimeSaveState _state;

            public StaticAuthority(RuntimeSaveState state, RuntimeSaveStateRootHeaderDescriptor descriptor)
            {
                _state = state;
                Descriptor = descriptor;
            }

            public RuntimeSaveStateRootHeaderDescriptor Descriptor { get; }

            public virtual RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(_state);
            }
        }

        private sealed class MutableAuthority : IRuntimeSaveStateRootHeaderAuthority
        {
            private readonly RuntimeSaveState _state;
            private readonly List<string> _calls;

            public MutableAuthority(
                RuntimeSaveStateRootHeaderDescriptor descriptor,
                RuntimeSaveState state,
                List<string> calls)
            {
                DescriptorValue = descriptor;
                _state = state;
                _calls = calls;
            }

            public RuntimeSaveStateRootHeaderDescriptor DescriptorValue { get; set; }
            public int DescriptorReadCount { get; private set; }

            public RuntimeSaveStateRootHeaderDescriptor Descriptor
            {
                get
                {
                    DescriptorReadCount++;
                    return DescriptorValue;
                }
            }

            public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
            {
                _calls.Add(RuntimeSaveStateRootHeaderProvider.ParticipantId);
                return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(_state);
            }
        }

        private sealed class FailingAuthority : IRuntimeSaveStateRootHeaderAuthority
        {
            private readonly List<string> _calls;

            public FailingAuthority(RuntimeSaveStateRootHeaderDescriptor descriptor, List<string> calls)
            {
                Descriptor = descriptor;
                _calls = calls;
            }

            public RuntimeSaveStateRootHeaderDescriptor Descriptor { get; }

            public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
            {
                _calls.Add(RuntimeSaveStateRootHeaderProvider.ParticipantId);
                return RuntimeSaveStateResult<RuntimeSaveState>.Failed(
                    new RuntimeSaveStateError(RuntimeSaveStateErrorCode.InvalidDocument, "$", "authority failed"));
            }
        }

        private sealed class InvalidDescriptorAuthority : IRuntimeSaveStateRootHeaderAuthority
        {
            private readonly List<string> _calls;

            public InvalidDescriptorAuthority(bool throws, List<string> calls)
            {
                Throws = throws;
                _calls = calls;
            }

            public bool Throws { get; set; }
            public RuntimeSaveStateRootHeaderDescriptor DescriptorValue { get; set; }

            public RuntimeSaveStateRootHeaderDescriptor Descriptor
            {
                get
                {
                    if (Throws)
                    {
                        throw new InvalidOperationException("descriptor failed");
                    }

                    return DescriptorValue;
                }
            }

            public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
            {
                _calls.Add(RuntimeSaveStateRootHeaderProvider.ParticipantId);
                return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(null);
            }
        }

        public enum AuthorityOutput
        {
            Null,
            Throw,
            Mismatch,
            Polluted,
            DefaultFailure,
            NonZeroFrame
        }

        private sealed class InvalidAuthority : IRuntimeSaveStateRootHeaderAuthority
        {
            private readonly AuthorityOutput _output;
            private readonly List<string> _calls;

            public InvalidAuthority(
                RuntimeSaveStateRootHeaderDescriptor descriptor,
                AuthorityOutput output,
                List<string> calls)
            {
                Descriptor = descriptor;
                _output = output;
                _calls = calls;
            }

            public RuntimeSaveStateRootHeaderDescriptor Descriptor { get; }

            public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
            {
                _calls.Add(RuntimeSaveStateRootHeaderProvider.ParticipantId);
                if (_output == AuthorityOutput.Throw)
                {
                    throw new InvalidOperationException("authority threw");
                }

                if (_output == AuthorityOutput.Null)
                {
                    return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(null);
                }

                if (_output == AuthorityOutput.DefaultFailure)
                {
                    return default;
                }

                if (_output == AuthorityOutput.NonZeroFrame)
                {
                    RuntimeSaveState nonZeroHeader = CreateHeader(Descriptor);
                    return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(new RuntimeSaveState(
                        nonZeroHeader.SchemaVersion,
                        nonZeroHeader.CreatedAtUtc,
                        nonZeroHeader.FrameworkVersion,
                        nonZeroHeader.ConfigVersion,
                        nonZeroHeader.ResourceCatalogVersion,
                        1L,
                        null,
                        null,
                        null,
                        null));
                }

                if (_output == AuthorityOutput.Mismatch)
                {
                    return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(CreateState(Descriptor.SchemaVersion));
                }

                RuntimeSaveState header = CreateHeader(Descriptor);
                return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(new RuntimeSaveState(
                    header.SchemaVersion,
                    header.CreatedAtUtc,
                    header.FrameworkVersion,
                    header.ConfigVersion,
                    header.ResourceCatalogVersion,
                    header.Frame,
                    null,
                    null,
                    new[] { new RuntimeModuleSaveState("pollution", 0, new RuntimeCustomState(TestCustomStateTypeIds.Test, "test", 0, "{}")) },
                    null));
            }
        }

        private sealed class HeaderMigration : IRuntimeSaveStateMigration
        {
            private readonly string _frameworkVersion;
            private readonly string _configVersion;
            private readonly string _resourceVersion;

            public HeaderMigration(
                int from,
                int to,
                string frameworkVersion,
                string configVersion,
                string resourceVersion)
            {
                FromSchemaVersion = from;
                ToSchemaVersion = to;
                _frameworkVersion = frameworkVersion;
                _configVersion = configVersion;
                _resourceVersion = resourceVersion;
            }

            public int FromSchemaVersion { get; }
            public int ToSchemaVersion { get; }

            public RuntimeSaveStateResult<RuntimeSaveState> Migrate(RuntimeSaveState saveState)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(new RuntimeSaveState(
                    ToSchemaVersion,
                    saveState.CreatedAtUtc,
                    _frameworkVersion,
                    _configVersion,
                    _resourceVersion,
                    saveState.Frame,
                    saveState.Entities,
                    saveState.GlobalCounters,
                    saveState.ModuleStates,
                    saveState.Metadata));
            }
        }

        private sealed class ReentrantDescriptorAuthority : IRuntimeSaveStateRootHeaderAuthority
        {
            private readonly RuntimeSaveStateRegistry _registry;
            private readonly RuntimeSaveStateRootHeaderDescriptor _descriptor;

            public ReentrantDescriptorAuthority(
                RuntimeSaveStateRegistry registry,
                RuntimeSaveStateRootHeaderDescriptor descriptor)
            {
                _registry = registry;
                _descriptor = descriptor;
            }

            public RuntimeSaveStateRootHeaderDescriptor Descriptor
            {
                get
                {
                    _registry.Register("reentrant", 10, null, new RecordingRestorer("reentrant", new List<string>()));
                    return _descriptor;
                }
            }

            public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(CreateHeader(_descriptor));
            }
        }

        private sealed class MutableTopologyMigration : IRuntimeSaveStateMigration
        {
            public MutableTopologyMigration(int fromSchemaVersion, int toSchemaVersion)
            {
                FromSchemaVersion = fromSchemaVersion;
                ToSchemaVersion = toSchemaVersion;
            }

            public int FromSchemaVersion { get; set; }
            public int ToSchemaVersion { get; set; }

            public RuntimeSaveStateResult<RuntimeSaveState> Migrate(RuntimeSaveState saveState)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(new RuntimeSaveState(
                    saveState.SchemaVersion + 1,
                    saveState.CreatedAtUtc,
                    "framework",
                    "config",
                    "resources",
                    saveState.Frame,
                    saveState.Entities,
                    saveState.GlobalCounters,
                    saveState.ModuleStates,
                    saveState.Metadata));
            }
        }

        private sealed class ReentrantSealAuthority : IRuntimeSaveStateRootHeaderAuthority
        {
            private readonly RuntimeSaveStateRegistry _registry;
            private readonly RuntimeSaveStateRootHeaderDescriptor _descriptor;

            public ReentrantSealAuthority(
                RuntimeSaveStateRegistry registry,
                RuntimeSaveStateRootHeaderDescriptor descriptor)
            {
                _registry = registry;
                _descriptor = descriptor;
            }

            public RuntimeSaveStateRegistryResult NestedSealResult { get; private set; }
            public int CaptureCount { get; private set; }

            public RuntimeSaveStateRootHeaderDescriptor Descriptor
            {
                get
                {
                    NestedSealResult = _registry.SealAuthoritative();
                    return _descriptor;
                }
            }

            public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
            {
                CaptureCount++;
                return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(CreateHeader(_descriptor));
            }
        }

        private sealed class FrameChangingMigration : IRuntimeSaveStateMigration
        {
            public int FromSchemaVersion => 0;
            public int ToSchemaVersion => 1;

            public RuntimeSaveStateResult<RuntimeSaveState> Migrate(RuntimeSaveState saveState)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(new RuntimeSaveState(
                    1,
                    saveState.CreatedAtUtc,
                    "framework",
                    "config",
                    "resources",
                    saveState.Frame + 1L,
                    saveState.Entities,
                    saveState.GlobalCounters,
                    saveState.ModuleStates,
                    saveState.Metadata));
            }
        }

        private sealed class FrameDeltaMigration : IRuntimeSaveStateMigration
        {
            private readonly long _delta;

            public FrameDeltaMigration(int fromSchemaVersion, int toSchemaVersion, long delta)
            {
                FromSchemaVersion = fromSchemaVersion;
                ToSchemaVersion = toSchemaVersion;
                _delta = delta;
            }

            public int FromSchemaVersion { get; }
            public int ToSchemaVersion { get; }

            public RuntimeSaveStateResult<RuntimeSaveState> Migrate(RuntimeSaveState saveState)
            {
                return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(new RuntimeSaveState(
                    ToSchemaVersion,
                    saveState.CreatedAtUtc,
                    "framework",
                    "config",
                    "resources",
                    saveState.Frame + _delta,
                    saveState.Entities,
                    saveState.GlobalCounters,
                    saveState.ModuleStates,
                    saveState.Metadata));
            }
        }

        private sealed class DefaultFailureMigration : IRuntimeSaveStateMigration
        {
            public int FromSchemaVersion => 0;
            public int ToSchemaVersion => 1;

            public RuntimeSaveStateResult<RuntimeSaveState> Migrate(RuntimeSaveState saveState)
            {
                return default;
            }
        }
    }
}
