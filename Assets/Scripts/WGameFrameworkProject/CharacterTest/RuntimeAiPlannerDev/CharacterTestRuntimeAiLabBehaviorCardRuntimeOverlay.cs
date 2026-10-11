using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabRuntimeIntentKind
    {
        None = 0,
        Control = 1,
        CastAbility = 2,
        CharacterAction = 3,
        BehaviorRecipeOrchestration = 4
    }

    internal readonly struct CharacterTestRuntimeAiLabRuntimeIntent
    {
        public CharacterTestRuntimeAiLabRuntimeIntent(
            CharacterTestRuntimeAiLabRuntimeIntentKind kind,
            Vector2 move,
            int abilityId = 0,
            int characterActionNumericId = 0,
            CharacterTestBehaviorRecipeOrchestrationOperation orchestrationOperation = default,
            int orchestrationResourceId = 0,
            float movementSpeedMultiplier = 1f,
            bool hasExecutionControl = false,
            CharacterTestBehaviorControlInput executionControl = default,
            long orchestrationExpectedInstanceId = 0L)
        {
            Kind = kind;
            Move = move;
            AbilityId = abilityId;
            CharacterActionNumericId = characterActionNumericId;
            OrchestrationOperation = orchestrationOperation;
            OrchestrationResourceId = orchestrationResourceId;
            MovementSpeedMultiplier = movementSpeedMultiplier;
            HasExecutionControl = hasExecutionControl;
            ExecutionControl = executionControl;
            OrchestrationExpectedInstanceId = orchestrationExpectedInstanceId;
        }

        public CharacterTestRuntimeAiLabRuntimeIntentKind Kind { get; }
        public Vector2 Move { get; }
        public int AbilityId { get; }
        public int CharacterActionNumericId { get; }
        public CharacterTestBehaviorRecipeOrchestrationOperation OrchestrationOperation { get; }
        public int OrchestrationResourceId { get; }
        public float MovementSpeedMultiplier { get; }
        public bool HasExecutionControl { get; }
        public CharacterTestBehaviorControlInput ExecutionControl { get; }
        public long OrchestrationExpectedInstanceId { get; }
    }

    /// <summary>
    /// CharacterTest-internal adapter that binds immutable behavior-card v2 programs to sensed planner
    /// facts and command-buffer intents.
    /// </summary>
    /// <remarks>
    /// LowFreqAlloc while an overlay is installed and when an agent is first observed. NoAlloc on the
    /// steady-state Tick path after those agent instances and command buffers are initialized.
    /// </remarks>
    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay
    {
        private const int AgentInitialCapacity = 16;
        private const string FlowLogCategory = "CharacterTest.RuntimeAiPlanner.BehaviorFlow";
        private readonly Binding[] _bindings;
        private readonly CharacterTestCharacterActionRuntime _actionRuntime;
        private readonly CharacterTestCharacterRuntimeModule _characterRuntime;
        private readonly CharacterTestRuntimeAiPlannerAttackOpportunityAdapter _attackOpportunity;
        private readonly AbilityRuntimeModule _abilityRuntime;
        private readonly IReadOnlyList<CharacterTestCharacterEntity> _entities;
        private readonly ICharacterTestAiNavigationService _navigationService;
        private readonly CharacterTestPerceptionFrameCache _perceptionFrameCache;
        private readonly IRuntimeLogger _logger;
        private readonly RuntimeLogBuffer _flowLogBuffer;
        private readonly bool _flowLoggingEnabled;
        private readonly int _profileSlot;
        private readonly List<AgentInstance> _agents = new List<AgentInstance>(AgentInitialCapacity);
        private readonly CharacterTestRuntimeAiLabLogicEventBuffer _latestEvents =
            new CharacterTestRuntimeAiLabLogicEventBuffer(64);

        public CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            IReadOnlyList<CharacterTestRuntimeAiLabCompiledLogicProgram> programs,
            CharacterTestCharacterActionRuntime actionRuntime,
            AbilityRuntimeModule abilityRuntime,
            long authorityHash,
            int profileSlot = 0,
            IRuntimeLogger logger = null,
            IReadOnlyList<CharacterTestCharacterEntity> entities = null,
            ICharacterTestAiNavigationService navigationService = null,
            CharacterTestPerceptionFrameCache perceptionFrameCache = null,
            bool enableFlowLogging = false, CharacterTestCharacterRuntimeModule characterRuntime = null,
            CharacterTestRuntimeAiPlannerAttackOpportunityAdapter attackOpportunity = null,
            IReadOnlyDictionary<string, TacticalCard.TacticalAutomaticExecutionBinding> automaticExecutions = null)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (programs == null || programs.Count != profile.Entries.Count)
                throw new ArgumentException("Behavior-card program count must match composition entries.", nameof(programs));

            _actionRuntime = actionRuntime;
            _characterRuntime = characterRuntime; _attackOpportunity = attackOpportunity;
            _abilityRuntime = abilityRuntime;
            _entities = entities;
            _navigationService = navigationService;
            _perceptionFrameCache = perceptionFrameCache;
            _logger = logger ?? NullRuntimeLogger.Instance;
            _flowLoggingEnabled = enableFlowLogging;
            _flowLogBuffer = enableFlowLogging ? new RuntimeLogBuffer(320) : null;
            _profileSlot = Math.Max(0, profileSlot);
            AuthorityHash = authorityHash;
            _bindings = new Binding[programs.Count];
            for (int i = 0; i < _bindings.Length; i++)
            {
                TacticalCard.TacticalAutomaticExecutionBinding automatic = null;
                if (programs[i]?.TacticalSource?.CloneLegacySource()?.ExecutionInput != null)
                {
                    string entryId = profile.Entries[i]?.EntryId;
                    if (automaticExecutions == null || entryId == null || !automaticExecutions.TryGetValue(entryId, out automatic)
                        || !automatic.Matches(programs[i].TacticalSource)) throw new ArgumentException("execution:AutomaticOwnerBindingMismatch");
                }
                if (programs[i]?.TacticalSource != null)
                {
                    bool foundActor = false;
                    if (_characterRuntime == null || entities == null) throw new ArgumentException("Tactical installation needs the actual character runtime.");
                    foreach (var actor in entities)
                    {
                        if (actor.Control.Owner == CharacterTestCharacterControlOwner.None || actor.Control.RuntimeAiPlannerProfileSlot != _profileSlot) continue;
                        foundActor = true;
                        string issue = "execution:AutomaticOwnerBindingMismatch";
                        bool valid = automatic != null ? actor.Identity.CharacterId == programs[i].TacticalSource.CharacterId
                            && automatic.Owner.CanBind(actor.GameplayEntityId, automatic.Card, out issue)
                            : programs[i].TacticalSource.ValidateBinding(_characterRuntime, actor, out issue);
                        if (!valid) throw new ArgumentException(issue);
                    }
                    if (!foundActor) throw new ArgumentException("Tactical installation has no matching actual actor.");
                }
                if (programs[i]?.RecipeV6 != null && programs[i].Execution == null)
                    throw new ArgumentException("Legacy timeline reference programs cannot be installed; compile execution tables with loaded dependencies.", nameof(programs));
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = profile.Entries[i];
                CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition = null;
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.TryGetDefinition(
                    entry?.BehaviorId,
                    out definition);
                var selection = programs[i]?.SelectionDefinition;
                if (selection == null && entry != null && programs[i] != null)
                {
                    float fixedScore = definition != null && definition.TryGetParameter(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ScoreKey, out var parameter)
                        ? parameter.DefaultFloat : 10f;
                    selection = new CharacterTestBehaviorCardSelectionDefinition(entry.BehaviorId, fixedScore);
                }
                int fixedOrder = 0;
                if (selection != null)
                    for (int other = 0; other < programs.Count; other++)
                    {
                        string otherId = programs[other]?.SelectionDefinition?.DefinitionId
                            ?? profile.Entries[other]?.BehaviorId
                            ?? string.Empty;
                        if (!string.IsNullOrEmpty(otherId)
                            && string.CompareOrdinal(otherId, selection.DefinitionId) < 0) fixedOrder++;
                    }
                _bindings[i] = new Binding(
                    entry?.EntryId,
                    entry?.NumericId ?? 0,
                    fixedOrder,
                    selection?.Priority ?? 0f,
                    entry != null && !entry.Disabled,
                    programs[i],
                    CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ClassifySwitchReason(programs[i]), selection, automatic);
            }
        }

        public long AuthorityHash { get; }
        internal long SelectionPassCount { get; private set; }
        internal bool OwnsActiveCardExecution(int entityId)
        {
            var agent = FindAgent(entityId);
            if (agent == null) return false;
            int index = agent.ActiveBindingIndex >= 0 ? agent.ActiveBindingIndex : agent.PendingBindingIndex;
            return index >= 0 && index < _bindings.Length && _bindings[index].Program != null;
        }
        internal CharacterTestRuntimeAiPerceptionRequirements ResolvePerceptionRequirements(int entityId, RuntimeFrame frame)
        {
            var agent = FindAgent(entityId);
            if (agent == null) return CharacterTestRuntimeAiPerceptionRequirements.All;
            int index = agent.ActiveBindingIndex >= 0 ? agent.ActiveBindingIndex : agent.PendingBindingIndex;
            if (index < 0 || index >= agent.Evaluators.Length) return CharacterTestRuntimeAiPerceptionRequirements.All;
            var requirements = _bindings[index].Program.BackgroundPerceptionRequirements;
            if (agent.Evaluators[index].NeedsPrimaryAttackFact((int)Math.Min(int.MaxValue, frame.Value)))
                requirements |= CharacterTestRuntimeAiPerceptionRequirements.PrimaryAttack;
            return requirements;
        }

        internal bool HasActiveExecution
        {
            get
            {
                for (int i = 0; i < _agents.Count; i++)
                    if (_agents[i].ActiveBindingIndex >= 0 || _agents[i].PendingBindingIndex >= 0) return true;
                return false;
            }
        }
        internal bool HasTerminalCommit
        {
            get
            {
                for (int i = 0; i < _agents.Count; i++)
                    if (_agents[i].HasTerminalCommit) return true;
                return false;
            }
        }
        public int ProfileSlot => _profileSlot;
        internal CharacterTestBehaviorExecutionEndReason LastExecutionEndReason { get; private set; }
        internal string LastAutomaticExecutionIssue { get; private set; } = string.Empty;
        internal static bool TryBeginAutomaticSelection(TacticalCard.TacticalAutomaticExecutionBinding binding,
            GameplayEntityId actor, long frame, out ulong executionId, out string issue)
        {
            executionId = 0; issue = "execution-input:UnsupportedProvider";
            return binding != null && binding.TryBegin(actor, frame, out executionId, out issue);
        }
        public CharacterTestRuntimeAiLabLogicEventBuffer LatestEvents => _latestEvents;

        /// <remarks>NoAlloc. Presentation-only query over the active typed binding.</remarks>
        public bool TryGetRuntimePresentation(
            int entityId,
            out CharacterTestRuntimeAiPlannerBehaviorCardRuntimePresentation presentation)
        {
            AgentInstance agent = FindAgent(entityId);
            if (agent == null)
            {
                presentation = default;
                return false;
            }

            bool hasCommittedBinding = agent.ActiveBindingIndex >= 0;
            int index = hasCommittedBinding
                ? agent.ActiveBindingIndex
                : agent.PendingBindingIndex;
            if (index < 0 || index >= _bindings.Length)
            {
                presentation = default;
                return false;
            }
            Binding binding = _bindings[index];
            CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator evaluator = agent.Evaluators[index];
            int stageIndex = hasCommittedBinding ? evaluator.CurrentStageIndex : -1;
            string stageId = binding.Program.Execution != null ? binding.Program.ExecutionDebugMap?.Timeline(stageIndex) ?? string.Empty : stageIndex >= 0 && stageIndex < binding.Program.Stages.Count
                ? binding.Program.Stages[stageIndex].StageId
                : string.Empty;
            presentation = new CharacterTestRuntimeAiPlannerBehaviorCardRuntimePresentation(
                binding.EntryId,
                binding.Program.SemanticHash,
                AuthorityHash,
                stageId,
                hasCommittedBinding
                    ? agent.AutomaticExecutionId != 0 ? CharacterTestRuntimeAiLabLogicRuntimeState.Running : evaluator.State
                    : CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning,
                binding.SwitchReason);
            return true;
        }

        public void BeginFrame()
        {
            _latestEvents.Clear();
        }

        /// <remarks>NoAlloc. Copies the latest sealed per-agent diagnostic state into a caller-owned arena slot.</remarks>
        public void WriteDecisionTraces(
            CharacterTestRuntimeAiLabDecisionTraceWriter writer,
            int frame)
        {
            if (!writer.IsValid)
                return;
            for (int i = 0; i < _agents.Count; i++)
                _agents[i].WriteDecisionTrace(writer, _bindings, frame);
        }

        internal void RestoreDecisionTraceHistory(
            int entityId,
            in CharacterTestRuntimeAiLabDecisionTraceAgentView source)
        {
            AgentInstance agent = FindAgent(entityId);
            agent?.RestoreDecisionTraceHistory(source.Facts);
        }

        /// <remarks>NoAlloc after the entity has been registered.</remarks>
        public bool TryTick(
            CharacterTestCharacterEntity entity,
            CharacterTestRuntimeAiPlannerFactState facts,
            RuntimeFrame frame,
            out CharacterTestRuntimeAiLabRuntimeIntent intent)
        {
            intent = default;
            if (entity == null || facts == null || _bindings.Length == 0)
                return false;

            AgentInstance agent = GetOrCreateAgent(entity.Id.Value);
            agent.SetFrame(entity, facts, frame, _actionRuntime, _abilityRuntime);
            if (agent.AutomaticExecutionId != 0)
            {
                int active = agent.ActiveBindingIndex;
                var observation = _bindings[active].Automatic.Owner.Observe(entity.GameplayEntityId, agent.AutomaticExecutionId);
                if (observation.ExecutionId != agent.AutomaticExecutionId)
                { LastAutomaticExecutionIssue = "execution:AutomaticObservationMismatch"; return true; }
                if (!string.IsNullOrEmpty(observation.Issue)) LastAutomaticExecutionIssue = observation.Issue;
                if (!observation.BehaviorTerminal || !observation.OwnedIntentsReleased || !observation.CommandsAcknowledged) return true;
                bool failed = !string.IsNullOrEmpty(observation.Issue);
                LastExecutionEndReason = failed ? observation.Issue.Contains("TimedOut") ? CharacterTestBehaviorExecutionEndReason.TimedOut
                    : CharacterTestBehaviorExecutionEndReason.ConditionRejected : CharacterTestBehaviorExecutionEndReason.Completed;
                agent.RecordTerminalCommit(active, failed ? CharacterTestRuntimeAiLabLogicRuntimeState.Failed : CharacterTestRuntimeAiLabLogicRuntimeState.Completed);
                agent.AutomaticExecutionId = 0; agent.ActiveBindingIndex = -1;
                return true;
            }

            if (agent.ActiveBindingIndex < 0)
            {
                bool hadPendingBinding = agent.PendingBindingIndex >= 0;
                int selected = agent.PendingBindingIndex;
                bool invalidatePending = false;
                if (selected >= 0)
                {
                    if (selected >= _bindings.Length
                        || selected >= agent.Runtimes.Length
                        || !_bindings[selected].Enabled
                        || _bindings[selected].Program == null)
                    {
                        invalidatePending = true;
                    }
                }
                if (invalidatePending)
                {
                    LogFlow(agent, frame, facts, "pending-invalidated", selected, -1,
                        "binding-disabled-or-condition-failed");
                    if (selected >= 0 && selected < agent.Runtimes.Length)
                        agent.Runtimes[selected].Reset();
                    selected = -1;
                    hadPendingBinding = false;
                    agent.PendingBindingIndex = -1;
                    agent.BindingReleasePending = false;
                }
                if (selected < 0)
                {
                    selected = SelectBinding(agent);
                    hadPendingBinding = false;
                    if (selected >= 0)
                        LogFlow(agent, frame, facts, "selected", selected, -1, "no-active-binding");
                }
                if (selected < 0)
                    return false;

                if (_bindings[selected].Automatic != null)
                {
                    // Selection transfers to the existing tactical owner. Do not start the empty contract evaluator.
                    agent.PendingBindingIndex = -1; agent.BindingReleasePending = false;
                    if (!TryBeginAutomaticSelection(_bindings[selected].Automatic, entity.GameplayEntityId, frame.Value,
                        out ulong executionId, out string issue))
                    {
                        LastAutomaticExecutionIssue = issue;
                        LastExecutionEndReason = CharacterTestBehaviorExecutionEndReason.ActionRejected;
                        agent.RecordTerminalCommit(selected, CharacterTestRuntimeAiLabLogicRuntimeState.Failed);
                        LogFlow(agent, frame, facts, "automatic-rejected", selected, -1, issue);
                        return true;
                    }
                    agent.AutomaticExecutionId = executionId; agent.ActiveBindingIndex = selected;
                    LastAutomaticExecutionIssue = string.Empty;
                    LogFlow(agent, frame, facts, "automatic-admitted", selected, -1, "original-tactical-owner");
                    return true;
                }
                CharacterTestRuntimeAiLabCompiledLogicProgram selectedProgram = _bindings[selected].Program;
                CharacterTestRuntimeAiLabCompiledLogicStage selectedEntry = GetEntryStage(selectedProgram);
                RuntimeBridge selectedRuntime = agent.Runtimes[selected];
                // Recheck after pending selection, before any first control/action command. An already
                // committed entry action owns its cooldown and must still be adopted by its original owner.
                bool adoptsOwnedEntry = hadPendingBinding && selectedRuntime.IsCharacterActionEntry(selectedEntry)
                    && selectedRuntime.HasObservedEntryAction(selectedEntry);
                if (!adoptsOwnedEntry && !selectedRuntime.MeetsConfiguredActionCooldowns())
                {
                    agent.PendingBindingIndex = -1; agent.BindingReleasePending = false;
                    agent.RecordTerminalCommit(selected, CharacterTestRuntimeAiLabLogicRuntimeState.Failed);
                    LastExecutionEndReason = CharacterTestBehaviorExecutionEndReason.ActionRejected;
                    LogFlow(agent, frame, facts, "configured-action-cooldown-rejected", selected, -1,
                        selectedRuntime.SystemActionUseResult.Status.ToString());
                    selectedRuntime.Reset();
                    return true;
                }
                if (selectedRuntime.IsCharacterActionEntry(selectedEntry))
                {
                    agent.PendingBindingIndex = selected;
                    if (hadPendingBinding && selectedRuntime.HasObservedEntryAction(selectedEntry))
                    {
                        LogFlow(agent, frame, facts, "entry-observed", selected, -1,
                            "character-action-running");
                        selectedRuntime.AdoptObservedEntryAction(selectedEntry);
                    }
                    else
                    {
                        if (hadPendingBinding && selectedRuntime.IsEntryTransitionRejected(selectedEntry))
                        {
                            LogFlow(agent, frame, facts, "entry-rejected", selected, -1,
                                selectedRuntime.RuntimeEffectReasonCode);
                            LastExecutionEndReason = CharacterTestBehaviorExecutionEndReason.ActionRejected;
                            agent.RecordTerminalCommit(
                                selected,
                                CharacterTestRuntimeAiLabLogicRuntimeState.Failed);
                            selectedRuntime.Reset();
                            agent.PendingBindingIndex = -1;
                            agent.BindingReleasePending = false;
                            return true;
                        }
                        if (selectedRuntime.TryBuildEntryTransitionIntent(selectedEntry, out intent))
                        {
                            agent.CommandResultBindingIndex = selected;
                            LogFlow(agent, frame, facts, "entry-requested", selected, -1,
                                "character-action-command");
                        }
                        return true;
                    }
                }
                agent.PendingBindingIndex = -1;
                agent.ActiveBindingIndex = selected;
                agent.LocalEvents.Clear();
                agent.Runtimes[selected].SetFactTraceLayer(
                    CharacterTestRuntimeAiLabDecisionTraceLayer.LogicCondition);
                bool started = selectedRuntime.IsCharacterActionEntry(selectedEntry)
                    ? agent.Evaluators[selected].StartCommitted(
                        ClampFrame(frame.Value),
                        agent.LocalEvents)
                    : agent.Evaluators[selected].Start(
                        ClampFrame(frame.Value),
                        agent.LocalEvents);
                if (!started)
                {
                    LogFlow(agent, frame, facts, "activation-failed", selected, -1,
                        "logic-evaluator-start-rejected");
                    agent.ActiveBindingIndex = -1;
                    return false;
                }
                LogFlow(agent, frame, facts, "activated", selected, -1,
                    selectedRuntime.IsCharacterActionEntry(selectedEntry)
                        ? "entry-action-observed"
                        : "immediate-entry");
                AppendEvents(
                    agent.LocalEvents,
                    agent.EntityId,
                    selected,
                    agent.Evaluators[selected].CardStartedFrame);
            }
            else
            {
                int active = agent.ActiveBindingIndex;
                if (agent.BindingReleasePending)
                {
                    int pending = agent.PendingBindingIndex;
                    if (pending >= 0 && pending < _bindings.Length)
                    {
                        CharacterTestRuntimeAiLabCompiledLogicProgram pendingProgram = _bindings[pending].Program;
                        CharacterTestRuntimeAiLabCompiledLogicStage pendingEntry =
                            pendingProgram.EntryStageIndex >= 0
                            && pendingProgram.EntryStageIndex < pendingProgram.Stages.Count
                                ? pendingProgram.Stages[pendingProgram.EntryStageIndex]
                                : null;
                        RuntimeBridge pendingRuntime = agent.Runtimes[pending];
                        if (pendingRuntime.IsCharacterActionEntry(pendingEntry))
                        {
                            if (pendingRuntime.HasObservedEntryAction(pendingEntry))
                            {
                                LogFlow(agent, frame, facts, "switch-entry-observed", pending, active,
                                    "character-action-running");
                                agent.RecordTerminalCommit(
                                    active,
                                    CharacterTestRuntimeAiLabLogicRuntimeState.Completed);
                                agent.Evaluators[active].Reset();
                                agent.Runtimes[active].Reset();
                                agent.ActiveBindingIndex = pending;
                                agent.PendingBindingIndex = -1;
                                agent.BindingReleasePending = false;
                                 agent.LocalEvents.Clear();
                                 pendingRuntime.SetFactTraceLayer(
                                     CharacterTestRuntimeAiLabDecisionTraceLayer.LogicCondition);
                                 pendingRuntime.AdoptObservedEntryAction(pendingEntry);
                                 if (!agent.Evaluators[pending].StartCommitted(
                                         ClampFrame(frame.Value),
                                         agent.LocalEvents))
                                {
                                    LogFlow(agent, frame, facts, "switch-activation-failed", pending, active,
                                        "logic-evaluator-start-rejected");
                                    pendingRuntime.Reset();
                                     agent.ActiveBindingIndex = -1;
                                     return false;
                                 }
                                 AppendEvents(
                                    agent.LocalEvents,
                                    agent.EntityId,
                                    pending,
                                    agent.Evaluators[pending].CardStartedFrame);
                                LogFlow(agent, frame, facts, "switched", pending, active,
                                    "entry-action-observed");
                                return true;
                            }

                            if (pendingRuntime.IsEntryTransitionRejected(pendingEntry))
                            {
                                LogFlow(agent, frame, facts, "switch-entry-rejected", pending, active,
                                    pendingRuntime.RuntimeEffectReasonCode);
                                pendingRuntime.Reset();
                                agent.PendingBindingIndex = -1;
                                agent.BindingReleasePending = false;
                                return true;
                            }

                            if (pendingRuntime.TryBuildEntryTransitionIntent(pendingEntry, out intent))
                            {
                                agent.CommandResultBindingIndex = pending;
                                LogFlow(agent, frame, facts, "switch-entry-requested", pending, active,
                                    "character-action-command");
                            }
                            return true;
                        }
                    }

                    RuntimeBridge activeRuntime = agent.Runtimes[active];
                    if (activeRuntime.TryBuildIntent(out intent))
                    {
                        agent.CommandResultBindingIndex = active;
                        return true;
                    }
                    activeRuntime.RefreshActionState();
                    CharacterTestRuntimeAiLabLogicActionState releaseState =
                        activeRuntime.PrimaryActionState;
                    if (releaseState == CharacterTestRuntimeAiLabLogicActionState.Completed
                        || releaseState == CharacterTestRuntimeAiLabLogicActionState.Failed
                        || releaseState == CharacterTestRuntimeAiLabLogicActionState.None)
                    {
                        if (_flowLoggingEnabled) LogFlow(agent, frame, facts, "released", active, -1,
                            releaseState.ToString());
                        LastExecutionEndReason = CharacterTestBehaviorExecutionEndReason.Completed;
                        agent.RecordTerminalCommit(
                            active,
                            CharacterTestRuntimeAiLabLogicRuntimeState.Completed);
                        agent.Evaluators[active].Reset();
                        agent.Runtimes[active].Reset();
                        agent.ActiveBindingIndex = -1;
                        agent.BindingReleasePending = false;
                    }
                    return true;
                }
                // Every card in this installed arbitration set, including a legacy/native
                // fallback, keeps ownership until its own execution and cleanup finish.
                // Authoring provenance is not a scheduling policy.
                agent.Runtimes[active].RefreshActionState();
                agent.Runtimes[active].SetFactTraceLayer(
                    CharacterTestRuntimeAiLabDecisionTraceLayer.LogicCondition);
                agent.Evaluators[active].Tick(ClampFrame(frame.Value), agent.LocalEvents);
                AppendEvents(
                    agent.LocalEvents,
                    agent.EntityId,
                    active,
                    agent.Evaluators[active].CardStartedFrame);
                CharacterTestRuntimeAiLabLogicRuntimeState state = agent.Evaluators[active].State;
                if (state == CharacterTestRuntimeAiLabLogicRuntimeState.Completed
                    || state == CharacterTestRuntimeAiLabLogicRuntimeState.Failed)
                {
                    if (_flowLoggingEnabled) LogFlow(agent, frame, facts, "logic-ended", active, -1, state.ToString());
                    if (agent.Runtimes[active].TryBuildReleaseIntent(out intent))
                        agent.CommandResultBindingIndex = active;
                    LastExecutionEndReason = agent.Evaluators[active].ExecutionEndReason;
                    agent.RecordTerminalCommit(active, state);
                    agent.Evaluators[active].Reset();
                    agent.Runtimes[active].Reset();
                    agent.ActiveBindingIndex = -1;
                    return true;
                }
            }

            if (agent.ActiveBindingIndex < 0)
                return false;
            int activeBindingIndex = agent.ActiveBindingIndex;
            if (agent.Runtimes[activeBindingIndex].TryBuildIntent(out intent))
                agent.CommandResultBindingIndex = activeBindingIndex;
            return true;
        }

        public void RecordCommandResult(
            int entityId,
            RuntimeCommandValidationResult result)
        {
            AgentInstance agent = FindAgent(entityId);
            if (agent == null)
                return;
            int bindingIndex = agent.CommandResultBindingIndex >= 0
                ? agent.CommandResultBindingIndex
                : ResolveRestoredCommandResultBinding(agent);
            if (bindingIndex < 0 || bindingIndex >= agent.Runtimes.Length)
                return;
            agent.Runtimes[bindingIndex].RecordCommandResult(result.Success);
            agent.RecordCommandResult(result);
            LogFlow(
                agent,
                agent.CurrentFrame,
                agent.CurrentFacts,
                "command-result",
                bindingIndex,
                -1,
                result.Success ? "accepted" : "rejected");
            agent.CommandResultBindingIndex = -1;
        }

        private void LogFlow(AgentInstance agent, RuntimeFrame frame, CharacterTestRuntimeAiPlannerFactState facts,
            string eventName, int bindingIndex, int relatedBindingIndex, int reason)
        {
            if (_flowLoggingEnabled) LogFlow(agent, frame, facts, eventName, bindingIndex, relatedBindingIndex, CharacterTestAiTraceReason.Text(reason));
        }

        private void LogFlow(
            AgentInstance agent,
            RuntimeFrame frame,
            CharacterTestRuntimeAiPlannerFactState facts,
            string eventName,
            int bindingIndex,
            int relatedBindingIndex,
            string reason)
        {
            if (!_flowLoggingEnabled)
                return;

            RuntimeBridge runtime = bindingIndex >= 0 && bindingIndex < agent.Runtimes.Length
                ? agent.Runtimes[bindingIndex]
                : null;
            _flowLogBuffer.Clear()
                .Append("event=").Append(eventName)
                .Append(" frame=").Append(frame.Value)
                .Append(" entity=").Append(agent.EntityId)
                .Append(" card=").Append(GetBindingId(bindingIndex));
            if (relatedBindingIndex >= 0)
                _flowLogBuffer.Append(" related=").Append(GetBindingId(relatedBindingIndex));
            _flowLogBuffer
                .Append(" reason=").Append(reason)
                .Append(" targetExists=").Append(facts != null && facts.TargetExists)
                .Append(" targetDead=").Append(facts != null && facts.TargetDead)
                .Append(" selfActionRunning=").Append(facts != null && facts.SelfActionRunning);
            if (runtime != null)
            {
                _flowLogBuffer
                    .Append(" actionState=").Append(runtime.PrimaryActionState.ToString())
                    .Append(" commandPending=").Append(runtime.EntryCommandPending)
                    .Append(" actionObserved=").Append(runtime.EntryActionObserved);
            }
            _logger.Info(FlowLogCategory, _flowLogBuffer);
        }

        private string GetBindingId(int bindingIndex)
        {
            return bindingIndex >= 0 && bindingIndex < _bindings.Length
                ? _bindings[bindingIndex].EntryId
                : "none";
        }

        private int ResolveRestoredCommandResultBinding(AgentInstance agent)
        {
            int pending = agent.PendingBindingIndex;
            if (pending >= 0 && pending < _bindings.Length)
            {
                CharacterTestRuntimeAiLabCompiledLogicProgram program = _bindings[pending].Program;
                CharacterTestRuntimeAiLabCompiledLogicStage entry = program != null
                    && program.EntryStageIndex >= 0
                    && program.EntryStageIndex < program.Stages.Count
                        ? program.Stages[program.EntryStageIndex]
                        : null;
                if (agent.Runtimes[pending].IsCharacterActionEntry(entry))
                    return pending;
            }
            return agent.ActiveBindingIndex;
        }

        internal sealed class StateCaptureStorage
        {
            internal readonly CharacterTestRuntimeAiLabLogicSlotStateSnapshot Snapshot;
            private readonly AgentInstance.StateCaptureStorage[] _agents;
            internal StateCaptureStorage(CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay source, int slot)
            {
                Snapshot = new CharacterTestRuntimeAiLabLogicSlotStateSnapshot
                {
                    Slot = Math.Max(0, slot), AuthorityHash = source.AuthorityHash,
                    Agents = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot[source._agents.Count]
                };
                _agents = new AgentInstance.StateCaptureStorage[source._agents.Count];
                for (int i = 0; i < _agents.Length; i++)
                    _agents[i] = new AgentInstance.StateCaptureStorage(source._agents[i], source._bindings);
                Capture(source);
            }
            internal bool IsCompatible(CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay source)
                => source != null && source.AuthorityHash == Snapshot.AuthorityHash && source._agents.Count == _agents.Length;
            internal void Capture(CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay source)
            {
                if (source.AuthorityHash != Snapshot.AuthorityHash || source._agents.Count != _agents.Length)
                    throw new InvalidOperationException("Behavior capture layout changed; prepare capture storage again.");
                for (int i = 0; i < _agents.Length; i++)
                    Snapshot.Agents[i] = source._agents[i].CaptureState(source._bindings, _agents[i]);
            }
        }

        public CharacterTestRuntimeAiLabLogicSlotStateSnapshot CreateStateSnapshot(int slot)
        {
            var agents = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot[_agents.Count];
            for (int i = 0; i < _agents.Count; i++)
                agents[i] = _agents[i].CreateStateSnapshot(_bindings);
            return new CharacterTestRuntimeAiLabLogicSlotStateSnapshot
            {
                Slot = Math.Max(0, slot),
                AuthorityHash = AuthorityHash,
                Agents = agents
            };
        }

        public bool TryApplyStateSnapshot(
            CharacterTestRuntimeAiLabLogicSlotStateSnapshot snapshot,
            out string failure)
        {
            failure = string.Empty;
            for (int i = 0; i < _agents.Count; i++)
                if (_agents[i].AutomaticExecutionId != 0)
                { failure = "execution:AutomaticOwnerSnapshotUnsupported"; return false; }
            if (snapshot == null)
            { _agents.Clear(); return true; }
            if (snapshot.AuthorityHash != AuthorityHash)
            {
                failure = "Behavior-card authority hash does not match the installed revision.";
                return false;
            }
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot[] agents =
                snapshot.Agents ?? Array.Empty<CharacterTestRuntimeAiLabLogicAgentStateSnapshot>();
            var prepared = new List<AgentInstance>(agents.Length);
            for (int i = 0; i < agents.Length; i++)
            {
                CharacterTestRuntimeAiLabLogicAgentStateSnapshot source = agents[i];
                if (source == null || source.EntityId <= 0)
                {
                    failure = "Behavior-card snapshot contains an invalid agent.";
                    return false;
                }
                var agent = new AgentInstance(
                    source.EntityId,
                    _bindings,
                    _entities,
                    _navigationService,
                    _perceptionFrameCache, _characterRuntime, _attackOpportunity);
                if (!agent.TryApplyStateSnapshot(source, _bindings, out failure))
                    return false;
                prepared.Add(agent);
            }
            _agents.Clear();
            _agents.AddRange(prepared);
            return true;
        }

        private int SelectBinding(AgentInstance agent)
        {
            SelectionPassCount++;
            int selected = -1;
            for (int i = 0; i < _bindings.Length; i++)
            {
                Binding binding = _bindings[i];
                agent.Runtimes[i].SetFactTraceLayer(
                    CharacterTestRuntimeAiLabDecisionTraceLayer.CardArbitration);
                bool eligible = binding.Enabled
                    && binding.Program != null
                    && agent.Runtimes[i].MeetsSystemActionRequirements()
                    && agent.Runtimes[i].CanActivate(binding.Program);
                agent.RecordBindingEvaluation(i, eligible);
                if (!eligible)
                    continue;
                if (selected < 0 || CompareBindings(binding, _bindings[selected]) < 0)
                    selected = i;
            }
            agent.RecordSelectedBinding(selected);
            return selected;
        }

        private static CharacterTestRuntimeAiLabCompiledLogicStage GetEntryStage(
            CharacterTestRuntimeAiLabCompiledLogicProgram program)
        {
            return program != null
                && program.EntryStageIndex >= 0
                && program.EntryStageIndex < program.Stages.Count
                    ? program.Stages[program.EntryStageIndex]
                    : null;
        }

        private static int CompareBindings(Binding left, Binding right)
        {
            return CharacterTestBehaviorCardSelectionDefinition.Compare(left.SelectionDefinition, right.SelectionDefinition);
        }

        private AgentInstance GetOrCreateAgent(int entityId)
        {
            AgentInstance existing = FindAgent(entityId);
            if (existing != null)
                return existing;
            var created = new AgentInstance(
                entityId,
                _bindings,
                _entities,
                _navigationService,
                _perceptionFrameCache, _characterRuntime, _attackOpportunity);
            _agents.Add(created);
            return created;
        }

        private AgentInstance FindAgent(int entityId)
        {
            for (int i = 0; i < _agents.Count; i++)
            {
                if (_agents[i].EntityId == entityId)
                    return _agents[i];
            }
            return null;
        }

        private void AppendEvents(
            CharacterTestRuntimeAiLabLogicEventBuffer source,
            int entityId,
            int bindingIndex,
            int cardInstanceStartFrame)
        {
            if (source == null)
                return;
            Binding binding = bindingIndex >= 0 && bindingIndex < _bindings.Length
                ? _bindings[bindingIndex]
                : default;
            for (int i = 0; i < source.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicEvent evt = source[i];
                _latestEvents.Add(new CharacterTestRuntimeAiLabLogicEvent(
                    evt.Frame,
                    evt.Kind,
                    evt.StageId,
                    evt.DetectorId,
                    evt.TransitionId,
                    entityId,
                    binding.EntryId,
                    binding.Program?.SemanticHash ?? 0L,
                    _profileSlot,
                    cardInstanceStartFrame,
                    evt.TargetTimelineId,
                    evt.LocalFrame,
                    evt.PrimaryActionInstanceId,
                    evt.ReasonCode,
                    evt.TriggerSourceKind,
                    evt.OutcomeKind, binding.NumericId, evt.StageIndex, evt.DetectorIndex, evt.TransitionIndex, evt.TargetTimelineIndex));
            }
            _latestEvents.AddOverflow(source.OverflowCount);
        }

        private static int ClampFrame(long frame)
        {
            if (frame <= 0L)
                return 0;
            return frame >= int.MaxValue ? int.MaxValue : (int)frame;
        }

    }
}
