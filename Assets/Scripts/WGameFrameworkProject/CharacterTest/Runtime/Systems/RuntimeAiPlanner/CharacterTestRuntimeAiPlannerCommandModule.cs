using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using Unity.Profiling;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal interface ICharacterTestRuntimeAiPlannerDodgeOutcomeSink
    {
        void RecordDodgeActionOutcome(int entityId, RuntimeFrame frame, bool accepted);
    }

    public sealed class CharacterTestRuntimeAiPlannerCommandModule : RuntimeModule,
        ICharacterTestRuntimeAiPlannerDodgeOutcomeSink
    {
        private static readonly ProfilerMarker EvaluateMarker =
            new ProfilerMarker("CharacterTest.RuntimeAiPlannerCommands.Evaluate");
        private static readonly ProfilerMarker SubmitMarker =
            new ProfilerMarker("CharacterTest.RuntimeAiPlannerCommands.Submit");


        private readonly CharacterTestCharacterRuntimeModule _characterRuntimeModule;
        internal bool TryPreparePublishedTacticalInputs(int profileSlot, string cardId, out CharacterTestTacticalCardInputs inputs, out string issue)
        {
            inputs = null; issue = "角色战术清单尚未加载。";
            var player = TacticalCard.CharacterTacticalServices.Player;
            if (player == null) return false;
            foreach (var entity in _characterRuntimeModule.Entities)
            {
                if (entity.Control.RuntimeAiPlannerProfileSlot != profileSlot) continue;
                foreach (var character in CharacterTestPlayableCharacterCatalog.Discover())
                    if (character.CharacterId == entity.Identity.CharacterId && character.TacticalDefaults != null)
                        return new TacticalCard.CharacterTacticalBoundCardInputs(player, _characterRuntimeModule, entity.Id.Value,
                            character.TacticalDefaults.CharacterStableId, cardId).TryPrepare(out inputs, out issue);
            }
            issue = "正式装配角色不可用。"; return false;
        }
        internal bool TryPreparePublishedTacticalInputs(int profileSlot, out CharacterTestTacticalCardInputs inputs, out string issue)
        {
            inputs = null; issue = "正式装配角色不可用。";
            foreach (var entity in _characterRuntimeModule.Entities)
                if (entity.Control.RuntimeAiPlannerProfileSlot == profileSlot)
                    return CharacterTestTacticalCardInputs.TryPrepare(_characterRuntimeModule.ActionRuntime, entity,
                        _characterRuntimeModule.PhysicsRuntime, _characterRuntimeModule.CombatTraceSystem.BakedTraceSource,
                        _characterRuntimeModule.CombatTraceSystem.WeaponTraceBindingResolver,
                        new CharacterActionTacticalMetricsStore(), CharacterTestTacticalSummaryPreparation.StandardMeasurement(), out inputs, out issue);
            return false;
        }
        private readonly CharacterTestPerceptionFrameCache _perceptionFrameCache;
        private readonly ICharacterTestAiNavigationService _navigationService;
        private readonly ICharacterTestRuntimeAiPlannerPerceptionSource _perceptionSource;
        private readonly Func<RuntimeCommand, RuntimeCommandValidationResult> _submitCommand;
        private readonly IRuntimeLogger _logger;
        private CharacterTestRuntimeAiPlannerValidationReport _validationReport;
        private string _validationReportText;
        private readonly CharacterTestRuntimeAiPlannerEvaluator[] _evaluators;
        private readonly CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay[] _behaviorLogicOverlays;
        private readonly CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest?[]
            _replayOverlayInstallRequests;
        private readonly bool[] _behaviorLoadoutSlots;
        private readonly AbilityRuntimeModule _abilityRuntime;
        private readonly CharacterTestAbilityHotbarRuntime _abilityHotbar;
        private readonly CharacterTestRuntimeAiPlannerAttackOpportunityAdapter _attackOpportunityAdapter;
        private readonly CharacterTestRuntimeAiJointRuntime _jointRuntime;
        private CharacterTestRuntimeAiPlannerEvaluator _defaultEvaluator;
        private string _profileIdText;
        private CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult _lastBehaviorHotUpdateResult;
        private readonly RuntimeLogBuffer _logBuffer = new RuntimeLogBuffer(192);

        public CharacterTestRuntimeAiPlannerCommandModule(
            CharacterTestCharacterRuntimeModule characterRuntimeModule,
            Func<RuntimeCommand, RuntimeCommandValidationResult> submitCommand,
            IRuntimeLogger logger,
            CharacterTestRuntimeAiPlannerProfile profile,
            ICharacterTestAiNavigationService navigationService = null,
            CharacterTestRuntimeAiPlannerValidationReport sourceReport = null)
            : this(
                characterRuntimeModule,
                submitCommand,
                logger,
                profile,
                navigationService,
                null,
                null,
                null,
                sourceReport,
                null)
        {
        }

        internal CharacterTestRuntimeAiPlannerCommandModule(
            CharacterTestCharacterRuntimeModule characterRuntimeModule,
            Func<RuntimeCommand, RuntimeCommandValidationResult> submitCommand,
            IRuntimeLogger logger,
            CharacterTestRuntimeAiPlannerProfile profile,
            ICharacterTestAiNavigationService navigationService,
            AbilityRuntimeModule abilityRuntime,
            CharacterTestAbilityHotbarRuntime abilityHotbar,
            IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> profileBindings,
            CharacterTestRuntimeAiPlannerValidationReport sourceReport = null,
            CharacterTestNavigationSession navigationSession = null)
            : base("CharacterTest.RuntimeAiPlannerCommands", RuntimeTickStage.PreSimulation, priority: 10)
        {
            _characterRuntimeModule = characterRuntimeModule;
            _perceptionFrameCache = characterRuntimeModule?.PerceptionFrameCache;
            _submitCommand = submitCommand == null ? null : command => { SubmittedCommandCount++; return submitCommand(command); };
            _logger = logger ?? NullRuntimeLogger.Instance;

            ICharacterTestAiNavigationService safeNavigation =
                navigationService ?? CharacterTestDirectAiNavigationService.Instance;
            ICharacterTestRuntimeAiPlannerPerceptionSource perceptionSource =
                new CharacterTestRuntimeAiPlannerWorldPerceptionSource(characterRuntimeModule);
            _navigationService = safeNavigation;
            _perceptionSource = perceptionSource;
            _abilityRuntime = abilityRuntime;
            _abilityHotbar = abilityHotbar;
            _attackOpportunityAdapter = new CharacterTestRuntimeAiPlannerAttackOpportunityAdapter(
                _characterRuntimeModule,
                navigationSession);
            _jointRuntime = new CharacterTestRuntimeAiJointRuntime(
                _characterRuntimeModule,
                navigationSession,
                _attackOpportunityAdapter);
            // Behavior groups are the action authority for Runtime AI characters. Attack
            // opportunity remains diagnostic/planning evidence; it must not veto an explicit
            // CharacterAction. Actual HitTrace/Hurtbox resolution remains the hit authority.
            _characterRuntimeModule?.SetPrimaryAttackOpportunityGuard(null);
            CharacterTestRuntimeAiPlannerValidationReport combinedReport = null;
            int maxSlot = ResolveMaxProfileSlot(profileBindings);
            _evaluators = new CharacterTestRuntimeAiPlannerEvaluator[maxSlot + 1];
            _behaviorLogicOverlays = new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay[_evaluators.Length];
            _replayOverlayInstallRequests =
                new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest?[_evaluators.Length];
            _behaviorLoadoutSlots = new bool[_evaluators.Length];
            _defaultEvaluator = CompileEvaluator(
                profile,
                safeNavigation,
                perceptionSource,
                _perceptionFrameCache,
                sourceReport,
                out CharacterTestRuntimeAiPlannerValidationReport defaultReport);
            combinedReport = CharacterTestRuntimeAiPlannerProfileJson.CombineReports(combinedReport, defaultReport);
            _evaluators[0] = _defaultEvaluator;

            if (profileBindings != null)
            {
                for (int i = 0; i < profileBindings.Count; i++)
                {
                    CharacterTestRuntimeAiPlannerProfileBinding binding = profileBindings[i];
                    if (binding.Slot <= 0 || binding.Slot >= _evaluators.Length)
                        continue;

                    _behaviorLoadoutSlots[binding.Slot] = IsBehaviorLoadoutBinding(binding);
                    CharacterTestRuntimeAiPlannerEvaluator evaluator = CompileEvaluator(
                        binding.Profile,
                        safeNavigation,
                        perceptionSource,
                        _perceptionFrameCache,
                        binding.SourceReport,
                        out CharacterTestRuntimeAiPlannerValidationReport bindingReport);
                    combinedReport =
                        CharacterTestRuntimeAiPlannerProfileJson.CombineReports(combinedReport, bindingReport);
                    if (evaluator != null)
                        _evaluators[binding.Slot] = evaluator;
                }
            }

            _validationReport = combinedReport;
            _validationReportText = _validationReport != null ? _validationReport.ToText() : string.Empty;
            _profileIdText = BuildProfileIdText(_evaluators, _behaviorLoadoutSlots);
            if (!HasAnyEvaluator(_evaluators))
                _logger.Warning("CharacterTest.RuntimeAiPlanner", _validationReportText);
        }

        public bool IsAvailable => HasAnyEvaluator(_evaluators);
        internal bool BootstrapDecisionsSuppressed { get; private set; }
        internal int BootstrapSuppressedTicks { get; private set; }
        internal long SubmittedCommandCount { get; private set; }
        internal void SetBootstrapDecisionsSuppressed(bool value) => BootstrapDecisionsSuppressed = value;
        public CharacterTestRuntimeAiPlannerValidationReport ValidationReport => _validationReport;
        internal CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult LastBehaviorHotUpdateResult =>
            _lastBehaviorHotUpdateResult;
        internal CharacterTestRuntimeAiLabLogicEventBuffer LatestBehaviorLogicEvents
        {
            get
            {
                for (int i = 0; i < _behaviorLogicOverlays.Length; i++)
                {
                    CharacterTestRuntimeAiLabLogicEventBuffer events = _behaviorLogicOverlays[i]?.LatestEvents;
                    if (events != null && events.Count > 0)
                        return events;
                }
                return null;
            }
        }

        internal int BehaviorLogicEventSlotCount => _behaviorLogicOverlays.Length;

        /// <remarks>NoAlloc. Copies the current frame's Runtime AI Lab diagnostics into a preallocated writer.</remarks>
        internal void WriteLatestBehaviorDecisionTraces(
            CharacterTestRuntimeAiLabDecisionTraceWriter writer,
            int frame)
        {
            if (!writer.IsValid)
                return;
            for (int i = 0; i < _evaluators.Length; i++)
                _evaluators[i]?.WriteDecisionTraces(writer, frame);
            for (int i = 0; i < _behaviorLogicOverlays.Length; i++)
                _behaviorLogicOverlays[i]?.WriteDecisionTraces(writer, frame);
            writer.Seal();
        }

        /// <remarks>LowFreqAlloc-free presentation recovery; does not alter authority state.</remarks>
        internal void RestoreDecisionTraceHistory(
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiPlannerDevTimelineRecord record)
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents =
                record?.FrameSnapshot?.Agents;
            for (int i = 0; timeline != null && agents != null && i < agents.Count; i++)
            {
                CharacterTestRuntimeAiPlannerAgentSnapshot agent = agents[i];
                timeline.TryGetDecisionTrace(record, agent.EntityId, out var trace);
                ResolveEvaluator(agent.RuntimeAiPlannerProfileSlot)
                    ?.RestoreDecisionTraceHistory(agent.EntityId, trace);
                ResolveBehaviorLogicOverlay(agent.RuntimeAiPlannerProfileSlot)
                    ?.RestoreDecisionTraceHistory(agent.EntityId, trace);
            }
        }

        /// <remarks>NoAlloc. Returns the current-frame buffer owned by the requested profile slot.</remarks>
        internal CharacterTestRuntimeAiLabLogicEventBuffer GetLatestBehaviorLogicEventsForSlot(int profileSlot)
        {
            return profileSlot >= 0 && profileSlot < _behaviorLogicOverlays.Length
                ? _behaviorLogicOverlays[profileSlot]?.LatestEvents
                : null;
        }

        internal void GetBehaviorCardExecutionProgress(int profileSlot, out bool active, out bool terminal)
        {
            var overlay = ResolveBehaviorLogicOverlay(profileSlot);
            active = overlay?.HasActiveExecution == true;
            terminal = overlay?.HasTerminalCommit == true;
        }

        internal CharacterTestBehaviorExecutionEndReason GetBehaviorCardExecutionEndReason(int profileSlot)
            => ResolveBehaviorLogicOverlay(profileSlot)?.LastExecutionEndReason ?? CharacterTestBehaviorExecutionEndReason.None;

        /// <summary>Clears attack-opportunity state when the authoritative timeline rewinds.</summary>
        internal void ResetAttackOpportunityCacheForRollbackReplay()
        {
            _attackOpportunityAdapter.ResetForRollbackReplay();
            _jointRuntime.ResetForRollbackReplay();
        }

        /// <remarks>AllocByDesign: Game View/editor diagnostics only; never called by simulation.</remarks>
        internal CharacterTestDamageZoneLockStateSnapshot[] CaptureDamageZoneLocksForDiagnostics()
        {
            return _attackOpportunityAdapter.CaptureZoneLocks();
        }

        internal void SweepInvalidTopologyStateAfterAuthorityRestore(RuntimeFrame frame)
        {
            _attackOpportunityAdapter.SweepInvalidTopologyStateAfterAuthorityRestore(frame);
        }

        /// <remarks>AllocByDesign: Game View/editor diagnostics only; never called by simulation.</remarks>
        internal CharacterTestEngagementDiagnosticSnapshot[] CaptureEngagementDiagnosticsForDiagnostics()
        {
            return _attackOpportunityAdapter.CaptureEngagementDiagnostics();
        }

        /// <remarks>AllocByDesign: Game View/editor diagnostics only; never called by simulation.</remarks>
        internal CharacterTestRuntimeAiJointDiagnosticSnapshot[] CaptureJointSelectionDiagnosticsForDiagnostics()
        {
            return _jointRuntime.CaptureDiagnostics();
        }

        void ICharacterTestRuntimeAiPlannerDodgeOutcomeSink.RecordDodgeActionOutcome(
            int entityId,
            RuntimeFrame frame,
            bool accepted)
        {
            CharacterTestCharacterEntity entity = _characterRuntimeModule?.FindEntityById(entityId);
            CharacterTestRuntimeAiPlannerEvaluator evaluator = entity != null
                ? ResolveEvaluator(entity.Control.RuntimeAiPlannerProfileSlot)
                : null;
            evaluator?.RecordDodgeActionOutcome(entityId, frame, accepted);
        }

        /// <remarks>NoAlloc on the steady-state path once AI agents are registered and command buffer capacity is stable.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            if (BootstrapDecisionsSuppressed) { BootstrapSuppressedTicks++; return; }
            if (!IsAvailable || _characterRuntimeModule == null)
                return;

            IReadOnlyList<CharacterTestCharacterEntity> entities = _characterRuntimeModule.Entities;
            var frame = new RuntimeFrame(context.FrameIndex);
            _perceptionFrameCache?.BeginCommittedConsumerPhase(
                frame,
                _characterRuntimeModule,
                _characterRuntimeModule.PhysicsRuntime);
            _jointRuntime.BeginFrame();
            for (int i = 0; i < _behaviorLogicOverlays.Length; i++)
                _behaviorLogicOverlays[i]?.BeginFrame();
            using (EvaluateMarker.Auto())
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    CharacterTestCharacterEntity entity = entities[i];
                    if (entity.Control.Owner != CharacterTestCharacterControlOwner.AiControlled)
                        continue;
                    if (!entity.BattleState.CanRunRuntimeAiPlanner)
                        continue;

                    CharacterTestRuntimeAiPlannerEvaluator evaluator =
                        ResolveEvaluator(entity.Control.RuntimeAiPlannerProfileSlot);
                    CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay overlay =
                        ResolveBehaviorLogicOverlay(entity.Control.RuntimeAiPlannerProfileSlot);
                    if (evaluator == null) continue;
                    bool cardActive = overlay?.OwnsActiveCardExecution(entity.Id.Value) == true;
                    CharacterTestRuntimeAiJointDirective jointDirective = default;
                    CharacterTestRuntimeAiPlannerDecision decision;
                    if (cardActive)
                    {
                        _attackOpportunityAdapter.MaintainDecisionAuthority(frame);
                        if (!evaluator.TryRefreshFactsWithoutSelection(entity, entities, frame, out decision, overlay.ResolvePerceptionRequirements(entity.Id.Value, frame))) continue;
                    }
                    else
                    {
                        jointDirective = _jointRuntime.Select(entity, entities, evaluator.Profile, frame);
                        if (!evaluator.TryEvaluate(entity, entities, frame, jointDirective, out decision)) continue;
                    }
                    if (overlay != null
                        && evaluator.TryGetFactState(entity.Id.Value, out CharacterTestRuntimeAiPlannerFactState facts)
                        && overlay.TryTick(entity, facts, frame, out CharacterTestRuntimeAiLabRuntimeIntent logicIntent))
                    {
                        evaluator.SuppressDecisionTraceSelectionForBehaviorOverlay(entity.Id.Value);
                        if (logicIntent.Kind == CharacterTestRuntimeAiLabRuntimeIntentKind.None)
                            continue;
                        RuntimeCommand logicCommand = CreateBehaviorLogicCommand(frame, entity, logicIntent);
                        RuntimeCommandValidationResult logicResult;
                        using (SubmitMarker.Auto())
                        {
                            logicResult = _submitCommand != null
                                ? logicIntent.HasExecutionControl || logicIntent.OrchestrationExpectedInstanceId > 0
                                    ? CharacterTestBehaviorExecutionCommandWriter.Submit(frame, entity.Id.Value, logicIntent, _submitCommand)
                                    : _submitCommand(logicCommand)
                                : RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                                    RuntimeCommandErrorCode.InvalidPayload,
                                    logicCommand,
                                    frame,
                                    "Runtime AI Lab behavior command submitter is unavailable."));
                        }
                        overlay.RecordCommandResult(entity.Id.Value, logicResult);
                        if (!logicResult.Success)
                        {
                            _logBuffer.Clear()
                                .Append("behavior logic submit rejected entity=").Append(entity.Id.Value)
                                .Append(" reason=").Append(logicResult.Error.Message);
                            _logger.Warning("CharacterTest.RuntimeAiPlanner", _logBuffer);
                        }
                        continue;
                    }

                    if (!decision.ShouldSubmitControlIntent)
                        continue;

                    bool attackPrimaryPressed = decision.AttackPrimaryPressed;
                    bool turnInPlace = false;
                    int turnInPlaceYawMilliDegrees = 0;
                    Vector2? engagementWorldMove = null;
                    bool hasJointOpportunity = false;
                    bool jointOwnsAttackDecision = false;
                    CharacterTestAttackOpportunityResult jointOpportunity = default;
                    if (jointDirective.HasSelection
                        && !jointDirective.IsCompatibility
                        && jointDirective.PreparedAttack.IsValid
                        && jointDirective.TargetEntityId == decision.TargetEntityId
                        && (decision.SelectedActionKind
                                == CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack
                            || decision.SelectedActionKind
                                == CharacterTestRuntimeAiPlannerActionKind.MoveToTarget))
                    {
                        jointOwnsAttackDecision = true;
                        hasJointOpportunity =
                            _attackOpportunityAdapter.TryCommitPreparedJointPrimaryAttack(
                                jointDirective.PreparedAttack,
                                jointDirective.Selection.ZoneId,
                                out jointOpportunity);
                        if (!hasJointOpportunity)
                            engagementWorldMove = Vector2.zero;
                        if (hasJointOpportunity
                            && jointDirective.Selection.Evaluation.ApproachPose
                            && _attackOpportunityAdapter.TryGetEngagementWorldMove(
                                entity,
                                frame,
                                out Vector2 selectedJointWorldMove))
                        {
                            engagementWorldMove = selectedJointWorldMove;
                        }
                    }
                    if (decision.SelectedActionKind == CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack)
                    {
                        CharacterTestCharacterEntity target = decision.TargetExists
                            ? _characterRuntimeModule.FindEntityById(decision.TargetEntityId)
                            : null;
                        CharacterTestAttackOpportunityResult opportunity = jointOpportunity;
                        bool opportunityValid = jointOwnsAttackDecision
                            ? hasJointOpportunity
                            : target != null
                                && _attackOpportunityAdapter.TryEvaluatePlannerPrimaryAttack(
                                    entity,
                                    target,
                                    frame,
                                    out opportunity);
                        if (target == null
                            || !opportunityValid
                            || opportunity.Status != CharacterTestAttackOpportunityStatus.Valid)
                        {
                            attackPrimaryPressed = false;
                        }
                        else if (opportunity.Level == CharacterTestAttackOpportunityLevel.TurnInPlace)
                        {
                            attackPrimaryPressed = false;
                            turnInPlace = true;
                            turnInPlaceYawMilliDegrees = opportunity.RequiredYawDeltaMilliDegrees;
                        }
                        else if (opportunity.Level != CharacterTestAttackOpportunityLevel.CurrentPose
                            || !opportunity.CanHitFromCurrentPose)
                        {
                            attackPrimaryPressed = false;
                            if (_attackOpportunityAdapter.TryGetEngagementWorldMove(
                                    entity,
                                    frame,
                                    out Vector2 selectedWorldMove))
                            {
                                engagementWorldMove = selectedWorldMove;
                            }
                        }
                    }

                    RuntimeCommand command = CreateControlIntentCommand(
                        frame,
                        entity,
                        decision,
                        attackPrimaryPressed,
                        turnInPlace,
                        turnInPlaceYawMilliDegrees,
                        engagementWorldMove);
                    RuntimeCommandValidationResult result;
                    using (SubmitMarker.Auto())
                    {
                        result = _submitCommand != null
                            ? _submitCommand(command)
                            : RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                                RuntimeCommandErrorCode.InvalidPayload,
                                command,
                                frame,
                                "Runtime AI Planner command submitter is unavailable."));
                    }

                    if (result.Success
                        && decision.SelectedActionKind
                            == CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot)
                    {
                        _attackOpportunityAdapter.MarkCompatibilityLegacyRange(entity, frame);
                        if (TryCreateAbilitySlotCommand(frame, entity, decision, out command))
                        {
                            using (SubmitMarker.Auto())
                            {
                                result = _submitCommand != null
                                    ? _submitCommand(command)
                                    : RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                                        RuntimeCommandErrorCode.InvalidPayload,
                                        command,
                                        frame,
                                        "Runtime AI Planner command submitter is unavailable."));
                            }
                        }
                        else
                        {
                            result = RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                                RuntimeCommandErrorCode.InvalidPayload,
                                command,
                                frame,
                                "Runtime AI Planner ability hotbar slot is unavailable."));
                        }
                    }

                    evaluator.RecordCommandResult(entity.Id.Value, result);
                    if (!result.Success)
                    {
                        _logBuffer.Clear()
                            .Append("submit rejected entity=").Append(entity.Id.Value)
                            .Append(" reason=").Append(result.Error.Message);
                        _logger.Warning("CharacterTest.RuntimeAiPlanner", _logBuffer);
                    }
                }
            }
        }

        internal int SnapshotAgentCount
        {
            get
            {
                int count = 0;
                if (IsAvailable) for (int i = 0; i < _evaluators.Length; i++) count += _evaluators[i]?.SnapshotAgentCount ?? 0;
                return count;
            }
        }

        internal void CopySnapshotAgentsTo(CharacterTestRuntimeAiPlannerAgentSnapshot[] agents)
        {
            int offset = 0;
            if (IsAvailable) for (int i = 0; i < _evaluators.Length; i++)
            {
                var evaluator = _evaluators[i];
                if (evaluator == null) continue;
                evaluator.CopySnapshotAgentsTo(agents, offset);
                offset += evaluator.SnapshotAgentCount;
            }
            for (int i = 0; i < offset; i++)
            {
                var entity = _characterRuntimeModule.FindEntityById(agents[i].EntityId);
                if (entity != null) agents[i] = agents[i].WithRuntimeAiBehaviorSelection(
                    entity.Control.RuntimeAiPlannerProfileSlot, entity.Control.RuntimeAiBehaviorSelection);
            }
        }

        public CharacterTestRuntimeAiPlannerSnapshot CreateSnapshot()
        {
            var agents = new CharacterTestRuntimeAiPlannerAgentSnapshot[SnapshotAgentCount];
            CopySnapshotAgentsTo(agents);
            return new CharacterTestRuntimeAiPlannerSnapshot(IsAvailable, IsAvailable ? _profileIdText : string.Empty, agents, _validationReportText);
        }

        // A capture workspace is private to its consumer. Published history receives a fresh workspace per slot.
        internal sealed class StateCaptureStorage
        {
            internal readonly CharacterTestRuntimeAiPlannerStateSnapshot Snapshot;
            private readonly CharacterTestRuntimeAiPlannerEvaluator[] _evaluators;
            private readonly CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay[] _overlays;
            private readonly CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.StateCaptureStorage[] _logic;
            private readonly CharacterTestDamageZoneLockStateSnapshot[][] _locks;
            private readonly CharacterTestEngagementReservationStateSnapshot[][] _reservations;
            private readonly int[] _sourceIds;
            private readonly bool _available;

            internal StateCaptureStorage(CharacterTestRuntimeAiPlannerCommandModule source)
            {
                _available = source.IsAvailable;
                _evaluators = (CharacterTestRuntimeAiPlannerEvaluator[])source._evaluators.Clone();
                _overlays = (CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay[])source._behaviorLogicOverlays.Clone();
                _logic = new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.StateCaptureStorage[_overlays.Length];
                Snapshot = source.CreateStateSnapshot();
                int logicIndex = 0;
                if (_available) for (int i = 0; i < _overlays.Length; i++)
                {
                    if (_overlays[i] == null) continue;
                    _logic[i] = new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.StateCaptureStorage(_overlays[i], i);
                    Snapshot.BehaviorLogicSlots[logicIndex++] = _logic[i].Snapshot;
                }
                int capacity = Math.Max(source._characterRuntimeModule?.Entities.Count ?? 0,
                    Math.Max(source._attackOpportunityAdapter.ZoneLockCount, source._attackOpportunityAdapter.EngagementReservationCount));
                _sourceIds = new int[capacity];
                _locks = PrepareArrays<CharacterTestDamageZoneLockStateSnapshot>(capacity);
                _reservations = PrepareArrays<CharacterTestEngagementReservationStateSnapshot>(capacity);
            }

            private static T[][] PrepareArrays<T>(int capacity) where T : new()
            {
                var arrays = new T[capacity + 1][];
                arrays[0] = Array.Empty<T>();
                // Count variants belong to the same unpublished slot and can share its objects.
                // Only one variant is published; this avoids allocating O(capacity squared) DTOs.
                var entries = new T[capacity];
                for (int i = 0; i < capacity; i++) entries[i] = new T();
                for (int count = 1; count <= capacity; count++)
                {
                    var values = new T[count];
                    Array.Copy(entries, values, count);
                    arrays[count] = values;
                }
                return arrays;
            }

            internal bool IsCompatible(CharacterTestRuntimeAiPlannerCommandModule source)
            {
                if (_available != source.IsAvailable || _evaluators.Length != source._evaluators.Length
                    || _overlays.Length != source._behaviorLogicOverlays.Length
                    || source._attackOpportunityAdapter.ZoneLockCount >= _locks.Length
                    || source._attackOpportunityAdapter.EngagementReservationCount >= _reservations.Length) return false;
                int index = 0;
                for (int i = 0; i < _evaluators.Length; i++)
                {
                    if (!ReferenceEquals(_evaluators[i], source._evaluators[i])) return false;
                    if (_available && _evaluators[i] != null && _evaluators[i].SnapshotAgentCount != Snapshot.Slots[index++].Agents.Length) return false;
                    if (!ReferenceEquals(_overlays[i], source._behaviorLogicOverlays[i])) return false;
                    if (_logic[i] != null && !_logic[i].IsCompatible(_overlays[i])) return false;
                }
                return true;
            }

            internal CharacterTestRuntimeAiPlannerStateSnapshot Capture(CharacterTestRuntimeAiPlannerCommandModule source)
            {
                if (!_available || Snapshot.Slots.Length == 0) return Snapshot;
                int index = 0;
                if (_available) for (int i = 0; i < _evaluators.Length; i++)
                {
                    if (_evaluators[i] == null) continue;
                    _evaluators[i].CopyStateSnapshotTo(Snapshot.Slots[index++]);
                    _logic[i]?.Capture(_overlays[i]);
                }
                var adapter = source._attackOpportunityAdapter;
                Snapshot.DamageZoneLocks = _locks[adapter.ZoneLockCount];
                Snapshot.EngagementReservations = _reservations[adapter.EngagementReservationCount];
                adapter.CaptureZoneLocksInto(Snapshot.DamageZoneLocks, _sourceIds);
                adapter.CaptureEngagementReservationsInto(Snapshot.EngagementReservations);
                return Snapshot;
            }
        }

        private StateCaptureStorage _hashCaptureStorage;
        internal CharacterTestRuntimeAiPlannerStateSnapshot CaptureStateForHash()
        {
            if (_hashCaptureStorage == null || !_hashCaptureStorage.IsCompatible(this))
                _hashCaptureStorage = new StateCaptureStorage(this);
            return _hashCaptureStorage.Capture(this);
        }


        internal CharacterTestRuntimeAiPlannerStateSnapshot CreateStateSnapshot()
        {
            if (!IsAvailable)
                return CharacterTestRuntimeAiPlannerStateSnapshot.Empty();

            int slotCount = CountAvailableEvaluators(_evaluators);
            if (slotCount == 0)
                return CharacterTestRuntimeAiPlannerStateSnapshot.Empty();

            var slots = new CharacterTestRuntimeAiPlannerSlotStateSnapshot[slotCount];
            int logicSlotCount = 0;
            for (int i = 0; i < _behaviorLogicOverlays.Length; i++)
            {
                if (_behaviorLogicOverlays[i] != null)
                    logicSlotCount++;
            }
            var logicSlots = new CharacterTestRuntimeAiLabLogicSlotStateSnapshot[logicSlotCount];
            int index = 0;
            int logicIndex = 0;
            for (int i = 0; i < _evaluators.Length; i++)
            {
                CharacterTestRuntimeAiPlannerEvaluator evaluator = _evaluators[i];
                if (evaluator == null)
                    continue;

                slots[index] = evaluator.CreateStateSnapshot(i);
                index++;
                if (_behaviorLogicOverlays[i] != null)
                {
                    logicSlots[logicIndex] = _behaviorLogicOverlays[i].CreateStateSnapshot(i);
                    logicIndex++;
                }
            }

            return new CharacterTestRuntimeAiPlannerStateSnapshot
            {
                SchemaVersion = CharacterTestRuntimeAiPlannerStateSnapshot.CurrentSchemaVersion,
                Slots = slots,
                BehaviorLogicSlots = logicSlots,
                DamageZoneLocks = _attackOpportunityAdapter.CaptureZoneLocks(),
                EngagementReservations = _attackOpportunityAdapter.CaptureEngagementReservations()
            };
        }

        internal bool TryApplyStateSnapshot(
            CharacterTestRuntimeAiPlannerStateSnapshot snapshot,
            out string failure)
        {
            failure = string.Empty;
            snapshot ??= CharacterTestRuntimeAiPlannerStateSnapshot.Empty();
            if (snapshot.SchemaVersion != CharacterTestRuntimeAiPlannerStateSnapshot.CurrentSchemaVersion)
            {
                failure = "Runtime AI Planner state snapshot contract mismatch. snapshot="
                    + snapshot.SchemaVersion
                    + ", runtime="
                    + CharacterTestRuntimeAiPlannerStateSnapshot.CurrentSchemaVersion
                    + ", behaviorCard="
                    + ResolveSnapshotBehaviorCardIdentity(snapshot)
                    + ", stateType=BehaviorRecipeTimelineEvent. State cannot be restored silently.";
                return false;
            }
            if (_evaluators == null || _evaluators.Length == 0)
                return true;

            CharacterTestRuntimeAiPlannerSlotStateSnapshot[] slots =
                snapshot.Slots ?? Array.Empty<CharacterTestRuntimeAiPlannerSlotStateSnapshot>();
            CharacterTestRuntimeAiLabLogicSlotStateSnapshot[] logicSlots =
                snapshot.BehaviorLogicSlots ?? Array.Empty<CharacterTestRuntimeAiLabLogicSlotStateSnapshot>();
            for (int i = 0; i < _evaluators.Length; i++)
            {
                if (_evaluators[i] != null && !_evaluators[i].ValidateStateSnapshot(FindSlotSnapshot(slots, i), out failure)) return false;
            }
            _attackOpportunityAdapter.ResetForRollbackReplay();
            _jointRuntime.ResetForRollbackReplay();
            if (!_attackOpportunityAdapter.TryRestoreZoneLocks(snapshot.DamageZoneLocks, out failure))
                return false;
            if (!_attackOpportunityAdapter.TryRestoreEngagementReservations(
                    snapshot.EngagementReservations,
                    out failure))
            {
                return false;
            }
            for (int i = 0; i < _evaluators.Length; i++)
            {
                CharacterTestRuntimeAiPlannerEvaluator evaluator = _evaluators[i];
                if (evaluator == null)
                    continue;

                if (!evaluator.TryApplyStateSnapshot(FindSlotSnapshot(slots, i), out failure))
                    return false;
                CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay overlay = _behaviorLogicOverlays[i];
                if (overlay != null
                    && !overlay.TryApplyStateSnapshot(FindLogicSlotSnapshot(logicSlots, i), out failure))
                {
                    return false;
                }
            }

            return true;
        }

        private static string ResolveSnapshotBehaviorCardIdentity(
            CharacterTestRuntimeAiPlannerStateSnapshot snapshot)
        {
            CharacterTestRuntimeAiLabLogicSlotStateSnapshot[] logicSlots =
                snapshot?.BehaviorLogicSlots ?? Array.Empty<CharacterTestRuntimeAiLabLogicSlotStateSnapshot>();
            for (int slotIndex = 0; slotIndex < logicSlots.Length; slotIndex++)
            {
                CharacterTestRuntimeAiLabLogicAgentStateSnapshot[] agents =
                    logicSlots[slotIndex]?.Agents ?? Array.Empty<CharacterTestRuntimeAiLabLogicAgentStateSnapshot>();
                for (int agentIndex = 0; agentIndex < agents.Length; agentIndex++)
                {
                    CharacterTestRuntimeAiLabLogicAgentStateSnapshot agent = agents[agentIndex];
                    if (agent != null && agent.BehaviorCardId > 0)
                        return agent.BehaviorCardId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (agent != null && agent.ProgramSemanticHash != 0L)
                        return "semantic-hash:" + agent.ProgramSemanticHash;
                }
            }
            return "unknown";
        }

        /// <remarks>
        /// LowFreqAlloc: explicit Editor/debug hot update. Not used by Tick, SaveState, network snapshots,
        /// replay, or deterministic hashes.
        /// </remarks>
        internal bool TryApplyBehaviorCompositionOverlay(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile compositionProfile,
            int profileSlot,
            out CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult result)
        {
            return TryApplyBehaviorCompositionOverlay(
                compositionProfile,
                profileSlot,
                logicPrograms: null,
                authorityHash: 0L,
                out result);
        }

        internal bool TryApplyBehaviorCompositionOverlay(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile compositionProfile,
            int profileSlot,
            IReadOnlyList<CharacterTestRuntimeAiLabCompiledLogicProgram> logicPrograms,
            long authorityHash,
            out CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult result,
            IReadOnlyDictionary<string, TacticalCard.TacticalAutomaticExecutionBinding> automaticExecutions = null)
        {
            int slot = profileSlot < 0 ? 0 : profileSlot;
            if (slot >= _evaluators.Length)
            {
                result = CreateHotUpdateFailure(
                    compositionProfile,
                    slot,
                    "Runtime AI Planner behavior composition hot update target slot is unavailable: " + slot);
                _lastBehaviorHotUpdateResult = result;
                _validationReportText = result.ReportText;
                return false;
            }

            if (_behaviorLogicOverlays[slot]?.HasActiveExecution == true)
            {
                result = CreateHotUpdateFailure(compositionProfile, slot,
                    "An active behavior binding cannot be replaced. Stop its execution before applying a new compiled binding.");
                _lastBehaviorHotUpdateResult = result;
                _validationReportText = result.ReportText;
                return false;
            }

            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(compositionProfile);
            if (!compile.Success)
            {
                _validationReport = compile.Report;
                _validationReportText = compile.Report != null
                    ? compile.Report.ToText()
                    : "Runtime AI Planner behavior composition compile failed.";
                result = new CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult(
                    false,
                    compositionProfile != null ? compositionProfile.CompositionId : string.Empty,
                    slot,
                    _validationReportText);
                _lastBehaviorHotUpdateResult = result;
                return false;
            }

            CharacterTestRuntimeAiPlannerEvaluator evaluator = CompileEvaluator(
                compile.Profile,
                _navigationService,
                _perceptionSource,
                _perceptionFrameCache,
                compile.Report,
                out CharacterTestRuntimeAiPlannerValidationReport report);
            if (evaluator == null)
            {
                _validationReport = report;
                _validationReportText = report != null
                    ? report.ToText()
                    : "Runtime AI Planner behavior composition evaluator compile failed.";
                result = new CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult(
                    false,
                    compositionProfile != null ? compositionProfile.CompositionId : string.Empty,
                    slot,
                    _validationReportText);
                _lastBehaviorHotUpdateResult = result;
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay logicOverlay = null;
            if (logicPrograms != null)
            {
                if (compositionProfile == null || logicPrograms.Count != compositionProfile.Entries.Count)
                {
                    result = CreateHotUpdateFailure(
                        compositionProfile,
                        slot,
                        "Behavior-card logic program count does not match composition entries.");
                    _lastBehaviorHotUpdateResult = result;
                    return false;
                }
                try
                {
                    logicOverlay = new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay(
                        compositionProfile,
                        logicPrograms,
                        _characterRuntimeModule?.ActionRuntime,
                        _abilityRuntime,
                        authorityHash,
                        slot,
                        _logger,
                        _characterRuntimeModule?.Entities,
                        _navigationService,
                        _perceptionFrameCache, characterRuntime: _characterRuntimeModule, attackOpportunity: _attackOpportunityAdapter, automaticExecutions: automaticExecutions);
                }
                catch (Exception exception)
                {
                    result = CreateHotUpdateFailure(
                        compositionProfile,
                        slot,
                        "Behavior-card runtime overlay creation failed: " + exception.Message);
                    _lastBehaviorHotUpdateResult = result;
                    return false;
                }
            }

            _evaluators[slot] = evaluator;
            _behaviorLogicOverlays[slot] = logicOverlay;
            _replayOverlayInstallRequests[slot] = CloneReplayInstallRequest(
                slot,
                compositionProfile,
                logicPrograms,
                authorityHash, automaticExecutions);
            if (slot == 0)
                _defaultEvaluator = evaluator;

            _validationReport = report;
            _validationReportText = report != null ? report.ToText() : string.Empty;
            _profileIdText = BuildProfileIdText(_evaluators, _behaviorLoadoutSlots);
            result = new CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult(
                true,
                compositionProfile != null ? compositionProfile.CompositionId : string.Empty,
                slot,
                _validationReportText);
            _lastBehaviorHotUpdateResult = result;
            return true;
        }

        /// <remarks>LowFreqAlloc: pre-battle authority boundary; restores all runtime slots on failure.</remarks>
        internal bool TryApplyBehaviorCompositionOverlayBatch(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest> requests,
            out CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult result)
        {
            if (requests == null || requests.Count == 0)
            {
                result = default;
                return true;
            }
            result = default;

            for (int i = 0; i < requests.Count; i++)
            {
                int slot = Math.Max(0, requests[i].ProfileSlot);
                for (int previous = 0; previous < i; previous++)
                {
                    if (Math.Max(0, requests[previous].ProfileSlot) != slot)
                        continue;
                    result = CreateHotUpdateFailure(
                        requests[i].Profile,
                        slot,
                        "Behavior-card batch contains duplicate Runtime AI Planner profile slot: " + slot);
                    _lastBehaviorHotUpdateResult = result;
                    return false;
                }
            }

            var previousEvaluators = new CharacterTestRuntimeAiPlannerEvaluator[_evaluators.Length];
            var previousOverlays = new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay[
                _behaviorLogicOverlays.Length];
            var previousReplayRequests =
                new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest?[
                    _replayOverlayInstallRequests.Length];
            Array.Copy(_evaluators, previousEvaluators, _evaluators.Length);
            Array.Copy(_behaviorLogicOverlays, previousOverlays, _behaviorLogicOverlays.Length);
            Array.Copy(
                _replayOverlayInstallRequests,
                previousReplayRequests,
                _replayOverlayInstallRequests.Length);
            CharacterTestRuntimeAiPlannerEvaluator previousDefaultEvaluator = _defaultEvaluator;
            CharacterTestRuntimeAiPlannerValidationReport previousReport = _validationReport;
            string previousReportText = _validationReportText;
            string previousProfileIdText = _profileIdText;

            for (int i = 0; i < requests.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest request = requests[i];
                if (TryApplyBehaviorCompositionOverlay(
                        request.Profile,
                        request.ProfileSlot,
                        request.LogicPrograms,
                        request.AuthorityHash,
                        out result, request.AutomaticExecutions))
                {
                    continue;
                }

                Array.Copy(previousEvaluators, _evaluators, _evaluators.Length);
                Array.Copy(previousOverlays, _behaviorLogicOverlays, _behaviorLogicOverlays.Length);
                Array.Copy(
                    previousReplayRequests,
                    _replayOverlayInstallRequests,
                    _replayOverlayInstallRequests.Length);
                _defaultEvaluator = previousDefaultEvaluator;
                _validationReport = previousReport;
                _validationReportText = previousReportText;
                _profileIdText = previousProfileIdText;
                return false;
            }
            return true;
        }

        internal CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest[]
            CaptureReplayOverlayInstallRequests()
        {
            int count = 0;
            for (int i = 0; i < _replayOverlayInstallRequests.Length; i++)
            {
                if (_replayOverlayInstallRequests[i].HasValue)
                {
                    _replayOverlayInstallRequests[i].Value.ValidateReplayExport();
                    count++;
                }
            }
            if (count == 0)
                return Array.Empty<CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest>();

            var requests = new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest[count];
            int writeIndex = 0;
            for (int i = 0; i < _replayOverlayInstallRequests.Length; i++)
            {
                if (!_replayOverlayInstallRequests[i].HasValue)
                    continue;
                CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest request =
                    _replayOverlayInstallRequests[i].Value;
                requests[writeIndex++] = CloneReplayInstallRequest(
                    request.ProfileSlot,
                    request.Profile,
                    request.LogicPrograms,
                    request.AuthorityHash);
            }
            return requests;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest
            CloneReplayInstallRequest(
                int profileSlot,
                CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
                IReadOnlyList<CharacterTestRuntimeAiLabCompiledLogicProgram> logicPrograms,
                long authorityHash,
                IReadOnlyDictionary<string, TacticalCard.TacticalAutomaticExecutionBinding> automaticExecutions = null)
        {
            CharacterTestRuntimeAiLabCompiledLogicProgram[] programCopy = null;
            if (logicPrograms != null)
            {
                programCopy = new CharacterTestRuntimeAiLabCompiledLogicProgram[logicPrograms.Count];
                for (int i = 0; i < programCopy.Length; i++)
                    programCopy[i] = logicPrograms[i];
            }
            return new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest(
                profileSlot,
                profile?.Clone(),
                programCopy,
                authorityHash, automaticExecutions);
        }

        /// <remarks>NoAlloc. Internal CharacterTest presentation query; does not extend MxFramework.AI.</remarks>
        internal bool TryGetBehaviorCardRuntimePresentation(
            int entityId,
            int profileSlot,
            out CharacterTestRuntimeAiPlannerBehaviorCardRuntimePresentation presentation)
        {
            CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay overlay =
                ResolveBehaviorLogicOverlay(profileSlot);
            if (overlay != null)
                return overlay.TryGetRuntimePresentation(entityId, out presentation);
            presentation = default;
            return false;
        }

        // Presentation only: the destination owns its reusable state copy.
        internal bool ReadEnemyCard(int entityId, int profileSlot, CharacterTestEnemyCardPresentation destination)
            => ResolveBehaviorLogicOverlay(profileSlot)?.ReadEnemyCard(entityId, destination) == true;

        private CharacterTestRuntimeAiPlannerEvaluator ResolveEvaluator(int runtimeAiPlannerProfileSlot)
        {
            int slot = runtimeAiPlannerProfileSlot < 0 ? 0 : runtimeAiPlannerProfileSlot;
            if (slot < _evaluators.Length && _evaluators[slot] != null)
                return _evaluators[slot];

            return _defaultEvaluator;
        }

        private CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay ResolveBehaviorLogicOverlay(
            int runtimeAiPlannerProfileSlot)
        {
            int slot = runtimeAiPlannerProfileSlot < 0 ? 0 : runtimeAiPlannerProfileSlot;
            if (slot < _behaviorLogicOverlays.Length && _behaviorLogicOverlays[slot] != null)
                return _behaviorLogicOverlays[slot];
            return null;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult CreateHotUpdateFailure(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile compositionProfile,
            int profileSlot,
            string message)
        {
            return new CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult(
                false,
                compositionProfile != null ? compositionProfile.CompositionId : string.Empty,
                profileSlot,
                message ?? string.Empty);
        }

        private CharacterTestRuntimeAiPlannerEvaluator CompileEvaluator(
            CharacterTestRuntimeAiPlannerProfile profile,
            ICharacterTestAiNavigationService navigationService,
            ICharacterTestRuntimeAiPlannerPerceptionSource perceptionSource,
            CharacterTestPerceptionFrameCache perceptionFrameCache,
            CharacterTestRuntimeAiPlannerValidationReport sourceReport,
            out CharacterTestRuntimeAiPlannerValidationReport combinedReport)
        {
            CharacterTestRuntimeAiPlannerProfile safeProfile =
                CharacterTestRuntimeAiPlannerProfileSource.Normalize(profile);
            CharacterTestRuntimeAiPlannerValidationReport validationReport;
            if (CharacterTestRuntimeAiPlannerCompiledProfile.TryCompile(
                    safeProfile,
                    out CharacterTestRuntimeAiPlannerCompiledProfile compiled,
                    out validationReport))
            {
                combinedReport =
                    CharacterTestRuntimeAiPlannerProfileJson.CombineReports(sourceReport, validationReport);
                return new CharacterTestRuntimeAiPlannerEvaluator(
                    compiled,
                    navigationService,
                    perceptionSource,
                    perceptionFrameCache,
                    _characterRuntimeModule?.ActionRuntime,
                    _attackOpportunityAdapter.EvaluatePlannerPrimaryAttack,
                    _attackOpportunityAdapter);
            }

            combinedReport =
                CharacterTestRuntimeAiPlannerProfileJson.CombineReports(sourceReport, validationReport);
            return null;
        }

        private static int ResolveMaxProfileSlot(
            IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> profileBindings)
        {
            int maxSlot = 0;
            if (profileBindings == null)
                return maxSlot;

            for (int i = 0; i < profileBindings.Count; i++)
            {
                int slot = profileBindings[i].Slot;
                if (slot > maxSlot)
                    maxSlot = slot;
            }

            return maxSlot;
        }

        private static bool HasAnyEvaluator(IReadOnlyList<CharacterTestRuntimeAiPlannerEvaluator> evaluators)
        {
            if (evaluators == null)
                return false;

            for (int i = 0; i < evaluators.Count; i++)
            {
                if (evaluators[i] != null)
                    return true;
            }

            return false;
        }

        private static int CountAvailableEvaluators(IReadOnlyList<CharacterTestRuntimeAiPlannerEvaluator> evaluators)
        {
            if (evaluators == null)
                return 0;

            int count = 0;
            for (int i = 0; i < evaluators.Count; i++)
            {
                if (evaluators[i] != null)
                    count++;
            }

            return count;
        }

        private static CharacterTestRuntimeAiPlannerSlotStateSnapshot FindSlotSnapshot(
            IReadOnlyList<CharacterTestRuntimeAiPlannerSlotStateSnapshot> slots,
            int slot)
        {
            if (slots == null)
                return null;

            for (int i = 0; i < slots.Count; i++)
            {
                CharacterTestRuntimeAiPlannerSlotStateSnapshot snapshot = slots[i];
                if (snapshot != null && snapshot.Slot == slot)
                    return snapshot;
            }

            return null;
        }

        private static CharacterTestRuntimeAiLabLogicSlotStateSnapshot FindLogicSlotSnapshot(
            IReadOnlyList<CharacterTestRuntimeAiLabLogicSlotStateSnapshot> slots,
            int slot)
        {
            if (slots == null)
                return null;
            for (int i = 0; i < slots.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicSlotStateSnapshot snapshot = slots[i];
                if (snapshot != null && snapshot.Slot == slot)
                    return snapshot;
            }
            return null;
        }

        private static string BuildProfileIdText(
            IReadOnlyList<CharacterTestRuntimeAiPlannerEvaluator> evaluators,
            IReadOnlyList<bool> behaviorLoadoutSlots)
        {
            string profileId = string.Empty;
            int count = 0;
            if (evaluators != null)
            {
                for (int i = 0; i < evaluators.Count; i++)
                {
                    CharacterTestRuntimeAiPlannerEvaluator evaluator = evaluators[i];
                    if (evaluator == null)
                        continue;
                    if (behaviorLoadoutSlots != null
                        && i < behaviorLoadoutSlots.Count
                        && behaviorLoadoutSlots[i])
                    {
                        continue;
                    }

                    string candidate = evaluator.Profile.Source.ProfileId;
                    if (count == 0)
                    {
                        profileId = candidate;
                        count = 1;
                        continue;
                    }

                    if (!string.Equals(profileId, candidate, StringComparison.Ordinal))
                        return "multiple";
                }
            }

            return profileId;
        }

        private static bool IsBehaviorLoadoutBinding(
            CharacterTestRuntimeAiPlannerProfileBinding binding)
        {
            return binding.SourcePath != null
                && binding.SourcePath.StartsWith("behavior-loadout:", StringComparison.Ordinal);
        }

        private RuntimeCommand CreateControlIntentCommand(
            RuntimeFrame frame,
            CharacterTestCharacterEntity entity,
            CharacterTestRuntimeAiPlannerDecision decision,
            bool attackPrimaryPressed,
            bool turnInPlace,
            int turnInPlaceYawMilliDegrees,
            Vector2? worldMoveOverride = null)
        {
            Vector2 worldMove = worldMoveOverride
                ?? _characterRuntimeModule.ResolveControlWorldMove(entity, decision.Move);
            return CharacterTestRuntimeCommandFactory.SetControlIntent(
                frame,
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                entity.Id.Value,
                decision.Move,
                worldMove,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                attackPrimaryPressed: attackPrimaryPressed,
                attackSecondaryPressed: false,
                debugSecondaryPressed: false,
                debugPrimaryPressed: false,
                traceId: CharacterTestAiTraceKinds.AtFrame(CharacterTestAiTraceKinds.PlannerControl, entity.Id.Value, frame),
                skillSlotPressedMask: 0,
                dodgePressed: decision.DodgePressed,
                turnInPlace: turnInPlace,
                turnInPlaceYawMilliDegrees: turnInPlaceYawMilliDegrees);
        }

        private bool TryCreateAbilitySlotCommand(
            RuntimeFrame frame,
            CharacterTestCharacterEntity entity,
            CharacterTestRuntimeAiPlannerDecision decision,
            out RuntimeCommand command)
        {
            int slotNumber = ResolveSelectedAbilitySlot(decision.SkillSlotPressedMask);
            if (_abilityHotbar == null
                || !_abilityHotbar.TryGetAbilityForSlot(slotNumber, out AbilityDefinitionConfig ability))
            {
                command = CharacterTestRuntimeCommandFactory.CastAbilitySlot(
                    frame,
                    CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                    entity.Id.Value,
                    slotNumber,
                    abilityId: 0,
                    traceId: CharacterTestAiTraceKinds.AtFrame(CharacterTestAiTraceKinds.AbilitySlot, entity.Id.Value, frame));
                return false;
            }

            command = CharacterTestRuntimeCommandFactory.CastAbilitySlot(
                frame,
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                entity.Id.Value,
                slotNumber,
                ability.AbilityId,
                CharacterTestAiTraceKinds.AtFrame(CharacterTestAiTraceKinds.AbilitySlot, entity.Id.Value, frame));
            return true;
        }

        private static int ResolveSelectedAbilitySlot(int abilitySlotMask)
        {
            int mask = abilitySlotMask & CharacterTestControlIntent.SkillSlotMaskBits;
            if (mask == 0)
                return 0;

            for (int i = 0; i < CharacterTestControlIntent.SkillSlotCount; i++)
            {
                if ((mask & (1 << i)) != 0)
                    return i + 1;
            }

            return 0;
        }

        private RuntimeCommand CreateBehaviorLogicCommand(
            RuntimeFrame frame,
            CharacterTestCharacterEntity entity,
            CharacterTestRuntimeAiLabRuntimeIntent intent)
        {
            switch (intent.Kind)
            {
                case CharacterTestRuntimeAiLabRuntimeIntentKind.CastAbility:
                    _attackOpportunityAdapter.MarkCompatibilityLegacyRange(entity, frame);
                    return CharacterTestRuntimeCommandFactory.CastAbilitySlot(
                        frame,
                        CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                        entity.Id.Value,
                        slotNumber: 1,
                        abilityId: intent.AbilityId,
                        traceId: CharacterTestAiTraceKinds.AtFrame(CharacterTestAiTraceKinds.CardAbility, entity.Id.Value, frame));
                case CharacterTestRuntimeAiLabRuntimeIntentKind.CharacterAction:
                    return CharacterTestRuntimeCommandFactory.RequestCharacterAction(
                        frame,
                        CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                        entity.Id.Value,
                        intent.CharacterActionNumericId,
                        CharacterTestAiTraceKinds.AtFrame(CharacterTestAiTraceKinds.CardAction, entity.Id.Value, frame));
                case CharacterTestRuntimeAiLabRuntimeIntentKind.BehaviorRecipeOrchestration:
                    return CharacterTestRuntimeCommandFactory.BehaviorRecipeOrchestration(
                        frame,
                        CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                        entity.Id.Value,
                        intent.OrchestrationOperation,
                        intent.OrchestrationResourceId,
                        CharacterTestAiTraceKinds.AtFrame(CharacterTestAiTraceKinds.RecipeOrchestration, entity.Id.Value, frame));
                default:
                    Vector2 worldMove = _characterRuntimeModule.ResolveControlWorldMove(entity, intent.Move);
                    return CharacterTestRuntimeCommandFactory.SetControlIntent(
                        frame,
                        CharacterTestRuntimeCommandSources.RuntimeAiPlanner,
                        entity.Id.Value,
                        intent.Move,
                        worldMove,
                        jumpPressed: false,
                        jumpHeld: false,
                        jumpReleased: false,
                        attackPrimaryPressed: false,
                        attackSecondaryPressed: false,
                        debugSecondaryPressed: false,
                        debugPrimaryPressed: false,
                        traceId: CharacterTestAiTraceKinds.AtFrame(CharacterTestAiTraceKinds.CardControl, entity.Id.Value, frame),
                        skillSlotPressedMask: 0,
                        dodgePressed: false,
                        movementSpeedMultiplier: intent.MovementSpeedMultiplier);
            }
        }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerProfileBinding
    {
        public CharacterTestRuntimeAiPlannerProfileBinding(
            int slot,
            string sourcePath,
            CharacterTestRuntimeAiPlannerProfile profile,
            CharacterTestRuntimeAiPlannerValidationReport sourceReport)
        {
            Slot = slot < 0 ? 0 : slot;
            SourcePath = sourcePath ?? string.Empty;
            Profile = profile;
            SourceReport = sourceReport;
        }

        public int Slot { get; }
        public string SourcePath { get; }
        public CharacterTestRuntimeAiPlannerProfile Profile { get; }
        public CharacterTestRuntimeAiPlannerValidationReport SourceReport { get; }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest
    {
        public CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest(
            int profileSlot,
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            IReadOnlyList<CharacterTestRuntimeAiLabCompiledLogicProgram> logicPrograms,
            long authorityHash,
            IReadOnlyDictionary<string, TacticalCard.TacticalAutomaticExecutionBinding> automaticExecutions = null)
        {
            ProfileSlot = profileSlot;
            Profile = profile;
            LogicPrograms = logicPrograms;
            AuthorityHash = authorityHash;
            if (automaticExecutions == null) AutomaticExecutions = null;
            else
            {
                var copy = new Dictionary<string, TacticalCard.TacticalAutomaticExecutionBinding>(StringComparer.Ordinal);
                foreach (var pair in automaticExecutions) copy.Add(pair.Key, pair.Value);
                AutomaticExecutions = new System.Collections.ObjectModel.ReadOnlyDictionary<string, TacticalCard.TacticalAutomaticExecutionBinding>(copy);
            }
        }

        public int ProfileSlot { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCompositionProfile Profile { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabCompiledLogicProgram> LogicPrograms { get; }
        public long AuthorityHash { get; }
        internal IReadOnlyDictionary<string, TacticalCard.TacticalAutomaticExecutionBinding> AutomaticExecutions { get; }
        internal void ValidateReplayExport()
        {
            if (AutomaticExecutions?.Count > 0)
                throw new InvalidOperationException("execution:AutomaticOwnerSnapshotUnsupported");
        }
    }
}
