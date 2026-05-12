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
                new[] { new RuntimeModuleSaveState(moduleId, 0, new RuntimeCustomState("test.module", 0, "{}")) },
                null);
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
    }
}
