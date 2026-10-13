using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestBehaviorExecutionHostSnapshot
    {
        public const int CurrentVersion = 2;
        public int Version { get; set; } = CurrentVersion;
        public long ProgramIdentity { get; set; }
        public int EntityId { get; set; }
        public CharacterTestBehaviorExecutionState Execution { get; set; }
        public CharacterTestBehaviorExecutionRuntimeState Runtime { get; set; }
        public CharacterTestBehaviorExecutionHostSnapshot Clone() => new CharacterTestBehaviorExecutionHostSnapshot
        { Version = Version, ProgramIdentity = ProgramIdentity, EntityId = EntityId, Execution = Execution?.Clone(), Runtime = Runtime?.Clone() };
        public void AddHash(RuntimeHashAccumulator hash, int key = 1)
        {
            hash.BeginObject(key, BehaviorExecutionHostHashFields.SchemaId, BehaviorExecutionHostHashFields.SchemaVersion);
            hash.AddInt(BehaviorExecutionHostHashFields.ExecutionHostVersion, Version); hash.AddLong(BehaviorExecutionHostHashFields.ExecutionHostProgram, ProgramIdentity); hash.AddInt(BehaviorExecutionHostHashFields.ExecutionHostEntity, EntityId);
            if (Execution != null) Execution.AddHash(hash, BehaviorExecutionHostHashFields.Execution);
            else { hash.BeginObject(BehaviorExecutionHostHashFields.Execution, BehaviorExecutionStateHashFields.SchemaId, 1, false); hash.EndObject(); }
            if (Runtime != null) Runtime.AddHash(hash, BehaviorExecutionHostHashFields.Runtime);
            else { hash.BeginObject(BehaviorExecutionHostHashFields.Runtime, BehaviorExecutionRuntimeHashFields.SchemaId, 1, false); hash.EndObject(); }
            hash.EndObject();
        }
    }

    /// <summary>Shared formal adapter for a single compiled behavior binding. The caller owns world Tick and command consumption.</summary>
    internal sealed partial class CharacterTestBehaviorExecutionHost
    {
        private readonly CharacterTestRuntimeAiLabCompiledLogicProgram _program;
        private readonly CharacterTestCharacterRuntimeModule _world;
        private readonly CharacterTestCharacterActionRuntime _actions;
        private readonly AbilityRuntimeModule _abilities;
        private readonly Func<RuntimeCommand, RuntimeCommandValidationResult> _submit;
        private readonly ICharacterTestAiNavigationService _navigation;
        private readonly CharacterTestPerceptionFrameCache _perception;
        private readonly CharacterTestRuntimeAiPlannerAttackOpportunityAdapter _opportunity;
        private CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge _runtime;
        private CharacterTestBehaviorExecutionEvaluator _evaluator;
        private int _entityId;
        public CharacterTestBehaviorExecutionHost(CharacterTestRuntimeAiLabCompiledLogicProgram program,
            CharacterTestCharacterRuntimeModule characterRuntime, CharacterTestCharacterActionRuntime actionRuntime,
            AbilityRuntimeModule abilityRuntime, Func<RuntimeCommand, RuntimeCommandValidationResult> submitCommand,
            ICharacterTestAiNavigationService navigationService = null, CharacterTestPerceptionFrameCache perceptionFrameCache = null,
            CharacterTestRuntimeAiPlannerAttackOpportunityAdapter attackOpportunity = null)
        {
            if (program?.Execution == null) throw new ArgumentException("A compiled execution program is required.", nameof(program));
            _program = program; _world = characterRuntime ?? throw new ArgumentNullException(nameof(characterRuntime));
            _actions = actionRuntime ?? throw new ArgumentNullException(nameof(actionRuntime)); _abilities = abilityRuntime;
            _submit = submitCommand ?? throw new ArgumentNullException(nameof(submitCommand)); _navigation = navigationService; _perception = perceptionFrameCache;
            _opportunity = attackOpportunity ?? new CharacterTestRuntimeAiPlannerAttackOpportunityAdapter(characterRuntime);
        }
        public CharacterTestRuntimeAiLabLogicRuntimeState State => _evaluator?.State ?? CharacterTestRuntimeAiLabLogicRuntimeState.Inactive;
        public int LocalFrame => _evaluator?.LocalFrame ?? 0;
        internal CharacterTestBehaviorExecutionEndReason EndReason => _evaluator?.EndReason ?? CharacterTestBehaviorExecutionEndReason.None;
        internal bool NaturallyCompleted => _evaluator?.NaturallyCompleted == true;
        public long OwnedActionInstanceId => _evaluator?.OwnedActionInstanceId ?? 0L;
        // The command receipt survives until the next evaluator Tick, including a one-frame action.
        internal MxFramework.CharacterAction.CharacterActionInstance AcknowledgedActionInstance => _runtime?.LastAcknowledgedActionInstance;
        internal ulong AcknowledgedActionRevision => _runtime?.AcknowledgedActionRevision ?? 0;
        internal MxFramework.CharacterAction.CharacterActionInstanceState AcknowledgedActionState
            => _runtime?.AcknowledgedActionState ?? MxFramework.CharacterAction.CharacterActionInstanceState.None;
        internal bool HasPendingActionCommand => _runtime != null
            && _runtime.ActionCommandState == CharacterTestRuntimeAiLabBehaviorRecipeOrchestrationState.Pending;
        public string LastFailureReason { get; private set; } = string.Empty;
        public int LastActionConditionRejectedEvent => _evaluator?.LastActionConditionRejectedEvent ?? -1;
        public CharacterTestRuntimeAiLabLogicEventBuffer LatestEvents { get; } = new CharacterTestRuntimeAiLabLogicEventBuffer(32);
        public bool Start(CharacterTestCharacterEntity entity, CharacterTestRuntimeAiPlannerFactState facts, RuntimeFrame frame)
        {
            if (State == CharacterTestRuntimeAiLabLogicRuntimeState.Running || State == CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning) return false;
            if (entity == null || !ReferenceEquals(_world.FindEntityById(entity.Id.Value), entity)
                || (entity.Control.Owner != CharacterTestCharacterControlOwner.LocalPlayer
                    && entity.Control.Owner != CharacterTestCharacterControlOwner.RemotePlayer
                    && entity.Control.Owner != CharacterTestCharacterControlOwner.AiControlled))
            { LastFailureReason = "The behavior actor must be a command-driven entity in this world."; return false; }
            if (!ValidateDependencies()) return false;
            if (_program.TacticalSource != null && !_program.TacticalSource.ValidateBinding(_world, entity, out string tacticalIssue))
            { LastFailureReason = tacticalIssue; return false; }
            EnsureRuntime(entity);
            _evaluator.Reset(); _runtime.Reset(); Prepare(entity, facts, frame);
            LastFailureReason = string.Empty;
            return _evaluator.Start(CheckedFrame(frame));
        }
        public bool Tick(CharacterTestCharacterEntity entity, CharacterTestRuntimeAiPlannerFactState facts, RuntimeFrame frame)
        {
            if (_evaluator == null || entity == null || entity.Id.Value != _entityId) return false;
            Prepare(entity, facts, frame); _evaluator.Tick(CheckedFrame(frame), LatestEvents);
            if (Flush(entity, frame)) { _runtime.PublishDoubleEdgeTerminal(frame); return true; }
            _evaluator.Stop(true); Flush(entity, frame); return false;
        }
        /// <remarks>NoAlloc. Reconciles a completed world frame without ticking the behavior a second time.</remarks>
        internal bool TryCompleteTerminalAction(CharacterTestCharacterEntity entity, CharacterTestRuntimeAiPlannerFactState facts,
            MxFramework.CharacterAction.CharacterActionInstance action, RuntimeFrame completedFrame)
        {
            if (_evaluator == null || entity == null || entity.Id.Value != _entityId || action == null
                || completedFrame.Value >= int.MaxValue - 1)
                return false;
            bool finished = action.State == MxFramework.CharacterAction.CharacterActionInstanceState.Finished;
            if ((!finished && !IsFreelyRecoveringAttack(entity, action))
                || !_evaluator.TryCompleteTerminalAction(action.InstanceId)) return false;
            // Completed-frame commands have already been consumed; release ownership at the next boundary.
            var releaseFrame = new RuntimeFrame(completedFrame.Value + 1);
            Prepare(entity, facts, releaseFrame);
            if (Flush(entity, releaseFrame)) { _runtime.PublishDoubleEdgeTerminal(completedFrame); return true; }
            _evaluator.Stop(true);
            return false;
        }

        /// <remarks>NoAlloc. Shared by terminal-card release and admission while physical recovery continues.</remarks>
        internal static bool IsFreelyRecoveringAttack(CharacterTestCharacterEntity entity,
            MxFramework.CharacterAction.CharacterActionInstance action)
        {
            if (entity == null || action?.Plan == null) return false;
            var battle = entity.BattleState;
            bool attack = action.Plan.Category == MxFramework.CharacterAction.CharacterActionCategory.BasicAttack
                || action.Plan.Category == MxFramework.CharacterAction.CharacterActionCategory.Skill;
            return attack && action.State == MxFramework.CharacterAction.CharacterActionInstanceState.Running
                && action.CurrentPhaseKind == MxFramework.CharacterAction.CharacterActionPhaseKind.Recovery
                && entity.CombatState.IsAlive && !entity.CombatState.IsReacting && battle.IsValid
                && battle.DominantReason == CharacterTestBattleStateDominantReason.None
                && battle.CanStartNormalAction && battle.CanMoveByInput;
        }

        public void Stop(CharacterTestCharacterEntity entity, CharacterTestRuntimeAiPlannerFactState facts, RuntimeFrame frame)
        {
            if (_evaluator == null || entity == null || entity.Id.Value != _entityId) return;
            Prepare(entity, facts, frame); _evaluator.Stop(); Flush(entity, frame);
        }
        private void EnsureRuntime(CharacterTestCharacterEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (_runtime != null && _entityId == entity.Id.Value) return;
            _entityId = entity.Id.Value;
            _runtime = new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge(_entityId, "execution", _program,
                _world.Entities, _navigation, _perception, _world, _opportunity);
            _evaluator = new CharacterTestBehaviorExecutionEvaluator(_program.Execution, _runtime, _program.ExecutionDebugMap) { EntityId = _entityId };
        }
        private void Prepare(CharacterTestCharacterEntity entity, CharacterTestRuntimeAiPlannerFactState facts, RuntimeFrame frame)
        { _runtime.SetFrame(entity, facts, frame, _actions, _abilities); _runtime.RefreshActionState(); }
        private bool Flush(CharacterTestCharacterEntity entity, RuntimeFrame frame)
        {
            if (!_runtime.TryBuildIntent(out var intent)) return true;
            RuntimeCommandValidationResult result = CharacterTestBehaviorExecutionCommandWriter.Submit(frame, entity.Id.Value, intent, _submit, _fixedPayment);
            _runtime.RecordCommandResult(result.Success);
            if (!result.Success) LastFailureReason = result.Error.Message;
            return result.Success;
        }
        private bool ValidateDependencies()
        {
            try
            {
                var required = new SortedSet<int>();
                var abilities = new SortedSet<int>();
                foreach (var action in _program.Execution.Actions) required.Add(action.ActionId);
                foreach (var condition in _program.Execution.Predicates) if (condition.ActionId > 0) required.Add(condition.ActionId);
                foreach (var condition in _program.Execution.Predicates) if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable && condition.AbilityId > 0) abilities.Add(condition.AbilityId);
                var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(_abilities?.Definitions, _actions.Registry);
                string fingerprint = CharacterTestBehaviorExecutionCompiler.ComputeDependencyFingerprint(references, required, _program.Execution.ExternalDependencyIdentity, abilities);
                if (string.Equals(fingerprint, _program.Execution.DependencyFingerprint, StringComparison.Ordinal)) return true;
                LastFailureReason = "Execution action dependencies changed; recompile at an inactive binding boundary.";
            }
            catch (ArgumentException exception) { LastFailureReason = exception.Message; }
            return false;
        }
        public CharacterTestBehaviorExecutionHostSnapshot CreateSnapshot() => new CharacterTestBehaviorExecutionHostSnapshot
        {
            ProgramIdentity = _program.Execution.Identity, EntityId = _entityId,
            Execution = _evaluator?.CreateSnapshot(), Runtime = _runtime?.CreateExecutionRuntimeSnapshot()
        };
        public bool TryRestoreSnapshot(CharacterTestBehaviorExecutionHostSnapshot snapshot, CharacterTestCharacterEntity entity,
            CharacterTestRuntimeAiPlannerFactState facts, RuntimeFrame frame)
        {
            if (snapshot == null || snapshot.Version != CharacterTestBehaviorExecutionHostSnapshot.CurrentVersion || snapshot.ProgramIdentity != _program.Execution.Identity
                || entity == null || snapshot.EntityId != entity.Id.Value || snapshot.Execution == null || snapshot.Runtime == null || !ValidateDependencies()) return false;
            if (_program.TacticalSource != null && !_program.TacticalSource.ValidateBinding(_world, entity, out string tacticalIssue))
            { LastFailureReason = tacticalIssue; return false; }
            EnsureRuntime(entity); Prepare(entity, facts, frame);
            if (!_runtime.CanRestoreExecutionRuntimeSnapshot(snapshot.Runtime) || !_evaluator.TryRestoreSnapshot(snapshot.Execution)) return false;
            return _runtime.TryRestoreExecutionRuntimeSnapshot(snapshot.Runtime);
        }
        public void AddHash(RuntimeHashAccumulator hash, int key = 1)
        {
            hash.BeginObject(key, BehaviorExecutionHostHashFields.SchemaId, BehaviorExecutionHostHashFields.SchemaVersion);
            hash.AddInt(BehaviorExecutionHostHashFields.ExecutionHostVersion, CharacterTestBehaviorExecutionHostSnapshot.CurrentVersion); hash.AddLong(BehaviorExecutionHostHashFields.ExecutionHostProgram, _program.Execution.Identity); hash.AddInt(BehaviorExecutionHostHashFields.ExecutionHostEntity, _entityId);
            if (_evaluator != null) _evaluator.AddHash(hash, BehaviorExecutionHostHashFields.Execution);
            else { hash.BeginObject(BehaviorExecutionHostHashFields.Execution, BehaviorExecutionStateHashFields.SchemaId, 1, false); hash.EndObject(); }
            if (_runtime != null) _runtime.AddExecutionRuntimeHash(hash, BehaviorExecutionHostHashFields.Runtime);
            else { hash.BeginObject(BehaviorExecutionHostHashFields.Runtime, BehaviorExecutionRuntimeHashFields.SchemaId, 1, false); hash.EndObject(); }
            hash.EndObject();
        }
        private static int CheckedFrame(RuntimeFrame frame)
        { if (frame.Value < 0 || frame.Value > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(frame)); return (int)frame.Value; }
    }

    internal static class CharacterTestBehaviorExecutionCommandWriter
    {
        internal static RuntimeCommandValidationResult Submit(RuntimeFrame frame, int entityId, in CharacterTestRuntimeAiLabRuntimeIntent intent,
            Func<RuntimeCommand, RuntimeCommandValidationResult> submit, TacticalCard.TacticalFixedCardPaymentCommit fixedPayment = null)
        {
            RuntimeCommandValidationResult result = default;
            if (intent.HasExecutionControl)
            {
                result = submit(CharacterTestRuntimeCommandFactory.SetBehaviorControlIntent(frame, CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                    entityId, intent.Move, intent.MovementSpeedMultiplier, intent.ExecutionControl, CharacterTestAiTraceKinds.AtFrame(CharacterTestAiTraceKinds.ExecutionControl, entityId, frame)));
                if (!result.Success || intent.Kind == CharacterTestRuntimeAiLabRuntimeIntentKind.Control) return result;
            }
            RuntimeCommand action = CharacterTestRuntimeCommandFactory.BehaviorRecipeOrchestration(frame, CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                entityId, intent.OrchestrationOperation, intent.OrchestrationResourceId, CharacterTestAiTraceKinds.AtFrame(CharacterTestAiTraceKinds.ExecutionAction, entityId, frame));
            if (intent.OrchestrationExpectedInstanceId > 0)
                action = new RuntimeCommand(frame, action.SourceId, action.CommandId, action.TargetId, action.Payload0, action.Payload1, 2,
                    traceId: action.TraceId, envelope: new RuntimeCommandEnvelope(CharacterTestRuntimeCommandFactory.BehaviorStopEnvelopeTypeId, 1,
                        RuntimeCommandProvenanceKind.Unspecified, data0: intent.OrchestrationExpectedInstanceId));
            if (fixedPayment != null && intent.OrchestrationOperation == CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction)
                action = fixedPayment.BindCommand(action);
            return submit(action);
        }
    }

    internal static class BehaviorExecutionHostHashFields
    {
        internal const int SchemaId = RuntimeHashSchemaIds.RuntimeAiPlannerBehaviorExecutionHost;
        internal const int SchemaVersion = 1;
        internal const int ExecutionHostVersion = 1;
        internal const int ExecutionHostProgram = 2;
        internal const int ExecutionHostEntity = 3;
        internal const int Execution = 4;
        internal const int Runtime = 5;
    }
}
