using System;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestTacticalCardSessionState
    {
        public const int CurrentVersion = 2;
        public int Version { get; set; } = CurrentVersion;
        public CharacterTestTacticalCardBindingState Binding { get; set; }
        public CharacterTestBehaviorExecutionHostSnapshot Execution { get; set; }
    }

    /// <summary>Connects validated per-character card bindings to the shared execution host and caller's command queue.</summary>
    /// <remarks>Start/compile/restore allocate. Tick and Stop reuse the host; no file reads, hashing or slot resolution occur in Tick.</remarks>
    internal sealed partial class CharacterTestTacticalCardSession
    {
        private readonly CharacterTestCharacterRuntimeModule _runtime;
        private readonly int _entityId;
        private CharacterTestTacticalCardBinding _binding;
        private readonly Func<RuntimeCommand, RuntimeCommandValidationResult> _submit;
        private readonly ICharacterTestAiNavigationService _navigation;
        private readonly CharacterTestRuntimeAiPlannerFactState _unavailableFacts = new CharacterTestRuntimeAiPlannerFactState();
        private CharacterTestBehaviorExecutionHost _host;

        internal CharacterTestTacticalCardSession(CharacterTestCharacterRuntimeModule runtime, int entityId,
            CharacterTestTacticalCardBinding binding, Func<RuntimeCommand, RuntimeCommandValidationResult> submitCommand,
            ICharacterTestAiNavigationService navigation = null)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            if (entityId <= 0) throw new ArgumentOutOfRangeException(nameof(entityId));
            _entityId = entityId;
            _binding = binding ?? throw new ArgumentNullException(nameof(binding));
            _submit = submitCommand ?? throw new ArgumentNullException(nameof(submitCommand));
            _navigation = navigation ?? CharacterTestDirectAiNavigationService.Instance;
        }

        internal bool IsExecuting => _binding.IsExecuting;
        internal CharacterTestRuntimeAiLabLogicRuntimeState State => _host?.State ?? CharacterTestRuntimeAiLabLogicRuntimeState.Inactive;
        internal int LocalFrame => _host?.LocalFrame ?? 0;
        internal CharacterTestBehaviorExecutionEndReason EndReason => _host?.EndReason ?? CharacterTestBehaviorExecutionEndReason.None;
        internal bool NaturallyCompleted => _host?.NaturallyCompleted == true;
        internal long OwnedActionInstanceId => _host?.OwnedActionInstanceId ?? 0;
        internal MxFramework.CharacterAction.CharacterActionInstance AcknowledgedActionInstance => _host?.AcknowledgedActionInstance;
        internal ulong AcknowledgedActionRevision => _host?.AcknowledgedActionRevision ?? 0;
        internal MxFramework.CharacterAction.CharacterActionInstanceState AcknowledgedActionState
            => _host?.AcknowledgedActionState ?? MxFramework.CharacterAction.CharacterActionInstanceState.None;
        internal bool HasPendingActionCommand => _host != null && _host.HasPendingActionCommand;
        internal int LastActionConditionRejectedEvent => _host?.LastActionConditionRejectedEvent ?? -1;
        internal string LastIssue { get; private set; } = string.Empty;
        internal CharacterTestTacticalCardCompilation Compilation => _binding.RetainedCompilation;

        internal bool TryCompilePublished(string name, string fingerprint, CharacterTestRuntimeAiLabCompiledLogicProgram program,
            CharacterTestTacticalCardInputs inputs, ICharacterTestTacticalCardInputSource currentInputs)
        {
            if (!_binding.TryCompilePublished(name, fingerprint, program, inputs, currentInputs)) return Reject(_binding.LastIssue);
            _host = null; LastIssue = string.Empty; return true;
        }

        internal bool TryStartPublished(CharacterTestRuntimeAiPlannerFactState facts, RuntimeFrame frame)
        {
            var entity = FindEntity();
            if (entity == null || facts == null || !CanDrive(entity)) return Reject("session:ContextUnavailable");
            if (!_binding.TryAcquirePublished(out var compiled)) return Reject(_binding.LastIssue);
            var candidate = CreateHost(compiled);
            if (!candidate.Start(entity, facts, frame)) { _binding.Release(); return Reject("session:ActivationRejected"); }
            _host = candidate; LastIssue = string.Empty; return true;
        }

        internal bool TryCompile(CharacterTestTacticalCardDefinition source, int actionId = 0)
        {
            if (!_binding.TryCompile(source, actionId)) return Reject(_binding.LastIssue);
            // Compilation is forbidden while a host is active, so no output can be orphaned here.
            _host = null;
            LastIssue = string.Empty;
            return true;
        }

        internal bool TryStart(CharacterTestTacticalCardDefinition currentSource,
            CharacterTestRuntimeAiPlannerFactState facts, RuntimeFrame frame)
        {
            CharacterTestCharacterEntity entity = FindEntity();
            if (entity == null || facts == null) return Reject("session:ContextUnavailable");
            if (!CanDrive(entity)) return Reject("session:SourceNotCommandDriven");
            if (!_binding.TryAcquire(currentSource, out var compiled)) return Reject(_binding.LastIssue);
            var candidate = CreateHost(compiled);
            if (!candidate.Start(entity, facts, frame))
            {
                _binding.Release();
                return Reject("session:ActivationRejected");
            }
            _host = candidate;
            LastIssue = string.Empty;
            return true;
        }

        /// <remarks>NoAlloc after Start. The caller supplies the formal RuntimeFrame and then consumes queued commands.</remarks>
        internal bool Tick(CharacterTestRuntimeAiPlannerFactState facts, RuntimeFrame frame)
        {
            if (_host == null || !_binding.IsExecuting) return false;
            CharacterTestCharacterEntity entity = FindEntity();
            if (entity == null)
            {
                Stop(facts, frame);
                return Reject("session:SourceEntityUnavailable");
            }
            bool unavailable = facts == null;
            if (unavailable)
            {
                // Missing facts cannot leave the previous move/facing input latched. Sampling
                // an unavailable target releases those channels without inventing an exit guard.
                _unavailableFacts.SetNoTarget(false);
                facts = _unavailableFacts;
            }
            bool result = _host.Tick(entity, facts, frame);
            ReleaseTerminal();
            if (unavailable) return Reject("session:FactsUnavailable");
            return result;
        }

        /// <remarks>NoAlloc. Releases terminal-card ownership at a complete frame without stopping physical recovery.</remarks>
        internal bool TryCompleteTerminalAction(MxFramework.CharacterAction.CharacterActionInstance action,
            CharacterTestRuntimeAiPlannerFactState facts, RuntimeFrame completedFrame)
        {
            if (_host == null || !_binding.IsExecuting) return false;
            var entity = FindEntity();
            if (entity == null) return false;
            bool completed = _host.TryCompleteTerminalAction(entity, facts ?? _unavailableFacts, action, completedFrame);
            ReleaseTerminal();
            if (!completed && _host.State == CharacterTestRuntimeAiLabLogicRuntimeState.Failed)
                return Reject(_host.LastFailureReason);
            return completed;
        }

        /// <summary>Requests formal cleanup; binding ownership remains until the host confirms a terminal state.</summary>
        internal void Stop(CharacterTestRuntimeAiPlannerFactState facts, RuntimeFrame frame)
        {
            if (_host == null || !_binding.IsExecuting) return;
            var entity = FindEntity();
            if (entity == null)
            {
                // There is no surviving character to drive; new starts still re-query entity and dependencies.
                _host = null;
                _binding.Release();
                LastIssue = "session:SourceEntityUnavailable";
                return;
            }
            _unavailableFacts.SetNoTarget(false);
            _host.Stop(entity, facts ?? _unavailableFacts, frame);
            ReleaseTerminal();
        }

        /// <summary>Captures a compiled binding, with or without an already-created execution host.</summary>
        internal CharacterTestTacticalCardSessionState CaptureState()
        {
            if (_binding.RetainedCompilation == null) throw new InvalidOperationException("Compile a binding before capturing card state.");
            return new CharacterTestTacticalCardSessionState
            {
                Binding = _binding.CaptureState(),
                Execution = _host?.CreateSnapshot()
            };
        }

        /// <summary>The world must already be at the saved authority frame; this restores only the card-owned state.</summary>
        internal bool TryRestore(CharacterTestTacticalCardDefinition currentSource, CharacterTestTacticalCardSessionState state,
            CharacterTestRuntimeAiPlannerFactState facts, RuntimeFrame frame)
        {
            if (_binding.IsExecuting) return Reject("session:StopBeforeRestore");
            if (state == null || state.Version != CharacterTestTacticalCardSessionState.CurrentVersion)
                return Reject("session:SnapshotUnavailableOrVersionMismatch");
            if (state.Execution == null)
            {
                if (state.Binding.Executing) return Reject("session:SnapshotLifecycleMismatch");
                if (!_binding.TryValidateRestore(currentSource, state.Binding)) return Reject(_binding.LastIssue);
                _host = null;
                LastIssue = string.Empty;
                return true;
            }
            if (state.Execution.Execution == null || state.Execution.Runtime == null)
                return Reject("session:SnapshotUnavailableOrVersionMismatch");
            bool active = IsActive((CharacterTestRuntimeAiLabLogicRuntimeState)state.Execution.Execution.State);
            if (active != state.Binding.Executing) return Reject("session:SnapshotLifecycleMismatch");
            if (!_binding.TryValidateRestore(currentSource, state.Binding)) return Reject(_binding.LastIssue);
            var entity = FindEntity();
            if (entity == null || facts == null) return Reject("session:ContextUnavailable");
            if (active && !CanDrive(entity)) return Reject("session:SourceNotCommandDriven");
            if (!_binding.TryAcquire(currentSource, out var compiled)) return Reject(_binding.LastIssue);
            var candidate = CreateHost(compiled);
            if (!candidate.TryRestoreSnapshot(state.Execution, entity, facts, frame))
            {
                _binding.Release();
                return Reject("session:ExecutionSnapshotRejected");
            }
            _host = candidate;
            LastIssue = string.Empty;
            if (!active) _binding.Release();
            return true;
        }

        /// <remarks>NoAlloc. Only binding/execution authority contributes; descriptions and diagnostics do not.</remarks>
        internal void AddHash(RuntimeHashAccumulator hash, int key = 1)
        {
            hash.BeginObject(key, TacticalSessionHashFields.SchemaId, TacticalSessionHashFields.SchemaVersion);
            var binding = _binding.CaptureState();
            hash.AddInt(TacticalSessionHashFields.TacticalVersion, CharacterTestTacticalCardSessionState.CurrentVersion);
            hash.AddInt(TacticalSessionHashFields.TacticalEntity, _entityId);
            hash.AddLong(TacticalSessionHashFields.TacticalProgram, binding.ProgramIdentity);
            hash.AddDigest(TacticalSessionHashFields.TacticalSource, binding.SourceFingerprint);
            hash.AddDigest(TacticalSessionHashFields.TacticalDependencies, binding.DependencyFingerprint);
            hash.AddInt(TacticalSessionHashFields.DependencyKind, binding.DependencyKind);
            hash.AddInt(TacticalSessionHashFields.DependencyCharacter, binding.DependencyCharacterId);
            hash.AddInt(TacticalSessionHashFields.TacticalExecuting, binding.Executing ? 1 : 0);
            if (_host != null) _host.AddHash(hash, TacticalSessionHashFields.Host);
            else { hash.BeginObject(TacticalSessionHashFields.Host, BehaviorExecutionHostHashFields.SchemaId, 1, false); hash.EndObject(); }
            hash.EndObject();
        }

        private CharacterTestBehaviorExecutionHost CreateHost(CharacterTestTacticalCardCompilation compiled) =>
            new CharacterTestBehaviorExecutionHost(compiled.Program, _runtime, _runtime.ActionRuntime, null, _submit,
                  _navigation, _runtime.PerceptionFrameCache);

        private void ReleaseTerminal()
        {
            if (_host.State == CharacterTestRuntimeAiLabLogicRuntimeState.Failed)
                LastIssue = _host.LastFailureReason;
            if (!IsActive(_host.State)) _binding.Release();
        }

        private static bool IsActive(CharacterTestRuntimeAiLabLogicRuntimeState state) =>
            state == CharacterTestRuntimeAiLabLogicRuntimeState.Running || state == CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning;

        private static bool CanDrive(CharacterTestCharacterEntity entity) =>
            entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer
            || entity.Control.Owner == CharacterTestCharacterControlOwner.RemotePlayer
            || entity.Control.Owner == CharacterTestCharacterControlOwner.AiControlled;

        private CharacterTestCharacterEntity FindEntity()
        {
            for (int i = 0; i < _runtime.Entities.Count; i++)
                if (_runtime.Entities[i].Id.Value == _entityId) return _runtime.Entities[i];
            return null;
        }

        private bool Reject(string issue) { LastIssue = issue; return false; }
    }

    internal static class TacticalSessionHashFields
    {
        internal const int SchemaId = RuntimeHashSchemaIds.RuntimeAiPlannerTacticalSession;
        internal const int SchemaVersion = 1;
        internal const int TacticalVersion = 1;
        internal const int TacticalEntity = 2;
        internal const int TacticalProgram = 3;
        internal const int TacticalSource = 4;
        internal const int TacticalDependencies = 5;
        internal const int TacticalExecuting = 6;
        internal const int Host = 7;
        internal const int DependencyKind = 8;
        internal const int DependencyCharacter = 9;
    }
}
