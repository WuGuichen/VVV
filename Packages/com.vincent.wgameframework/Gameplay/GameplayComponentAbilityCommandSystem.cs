using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentAbilityCommandSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.command.component_ability";

        private readonly GameplayComponentAbilityRegistry _abilityRegistry;
        private readonly GameplayComponentAbilityRequestStore _requestStore;
        private readonly GameplayComponentTargetingService _targetingService;
        private readonly GameplayBuffRequestBuffer _buffRequests;
        private readonly IGameplayComponentCommandResultSink _resultSink;

        public GameplayComponentAbilityCommandSystem(
            GameplayComponentAbilityRegistry abilityRegistry,
            GameplayComponentAbilityRequestStore requestStore = null,
            GameplayComponentTargetingService targetingService = null,
            GameplayBuffRequestBuffer buffRequests = null,
            string systemId = DefaultSystemId,
            int priority = 50)
            : this(
                abilityRegistry,
                requestStore,
                targetingService,
                buffRequests,
                systemId,
                priority,
                null)
        {
        }

        public GameplayComponentAbilityCommandSystem(
            GameplayComponentAbilityRegistry abilityRegistry,
            GameplayComponentAbilityRequestStore requestStore,
            GameplayComponentTargetingService targetingService,
            GameplayBuffRequestBuffer buffRequests,
            string systemId,
            int priority,
            IGameplayComponentCommandResultSink resultSink)
        {
            _abilityRegistry = abilityRegistry ?? throw new ArgumentNullException(nameof(abilityRegistry));
            _requestStore = requestStore;
            _targetingService = targetingService ?? new GameplayComponentTargetingService();
            _buffRequests = buffRequests;
            _resultSink = resultSink;
            SystemId = systemId ?? string.Empty;
            Priority = priority;
        }

        public string SystemId { get; }
        public GameplaySystemPhase Phase => GameplaySystemPhase.Command;
        public int Priority { get; }
        public bool IsEnabled { get; private set; } = true;

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(GameplaySystemContext context)
        {
            IReadOnlyList<RuntimeCommand> commands = context.Commands;
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                if (command.CommandId == GameplayRuntimeCommandIds.CastComponentAbility)
                {
                    ExecuteCast(context, command);
                    context.CommandState.MarkHandled(command);
                    continue;
                }

                if (command.CommandId == GameplayRuntimeCommandIds.CastComponentAbilityRequest)
                {
                    ExecuteRequestCast(context, command);
                    context.CommandState.MarkHandled(command);
                }
            }
        }

        private void ExecuteCast(GameplaySystemContext context, RuntimeCommand command)
        {
            GameplayComponentWorld componentWorld = context.ComponentWorld;
            if (componentWorld == null)
            {
                EnqueueFailure(
                    context,
                    command,
                    default,
                    command.Payload1,
                    GameplayComponentAbilityEvents.MissingComponentWorldReason,
                    GameplayAbilityRuntimeFailureCode.AbilityCastFailed,
                    GameplayComponentCommandResultReasonCode.MissingComponentWorld);
                return;
            }

            if (!TryReadCaster(command, out GameplayEntityId casterEntityId))
            {
                EnqueueFailure(
                    context,
                    command,
                    default,
                    command.Payload1,
                    GameplayComponentAbilityEvents.InvalidCasterReason,
                    GameplayAbilityRuntimeFailureCode.MissingCaster,
                    GameplayComponentCommandResultReasonCode.InvalidComponentEntity);
                return;
            }

            if (!componentWorld.IsAlive(casterEntityId))
            {
                EnqueueFailure(
                    context,
                    command,
                    casterEntityId,
                    command.Payload1,
                    GameplayComponentAbilityEvents.MissingCasterReason,
                    GameplayAbilityRuntimeFailureCode.MissingCaster,
                    GameplayComponentCommandResultReasonCode.MissingComponentEntity);
                return;
            }

            int abilityId = command.Payload1;
            if (command.Payload2 != 0)
            {
                EnqueueFailure(
                    context,
                    command,
                    casterEntityId,
                    abilityId,
                    GameplayComponentAbilityEvents.InvalidCommandPayloadReason,
                    GameplayAbilityRuntimeFailureCode.AbilityCastFailed,
                    GameplayComponentCommandResultReasonCode.InvalidComponentAbilityCommandPayload);
                return;
            }

            if (!_abilityRegistry.TryGet(abilityId, out IGameplayComponentAbility ability))
            {
                EnqueueFailure(
                    context,
                    command,
                    casterEntityId,
                    abilityId,
                    GameplayComponentAbilityEvents.MissingAbilityReason,
                    GameplayAbilityRuntimeFailureCode.MissingAbility,
                    GameplayComponentCommandResultReasonCode.MissingComponentAbility);
                return;
            }

            ExecuteAbilityWithRules(
                context,
                command,
                componentWorld,
                ability,
                casterEntityId,
                new[] { casterEntityId });
        }

        private void ExecuteRequestCast(GameplaySystemContext context, RuntimeCommand command)
        {
            GameplayComponentWorld componentWorld = context.ComponentWorld;
            if (componentWorld == null)
            {
                EnqueueFailure(
                    context,
                    command,
                    default,
                    command.Payload2,
                    GameplayComponentAbilityEvents.MissingComponentWorldReason,
                    GameplayAbilityRuntimeFailureCode.AbilityCastFailed);
                return;
            }

            if (_requestStore == null)
            {
                EnqueueFailure(
                    context,
                    command,
                    default,
                    command.Payload2,
                    GameplayComponentAbilityEvents.MissingRequestReason,
                    GameplayAbilityRuntimeFailureCode.AbilityCastFailed);
                return;
            }

            if (!TryReadRequestHandle(command, out GameplayComponentAbilityRequestHandle handle))
            {
                EnqueueFailure(
                    context,
                    command,
                    default,
                    command.Payload2,
                    GameplayComponentAbilityEvents.InvalidRequestReason,
                    GameplayAbilityRuntimeFailureCode.AbilityCastFailed);
                return;
            }

            if (!_requestStore.TryGet(handle, out GameplayComponentAbilityRequest request))
            {
                EnqueueFailure(
                    context,
                    command,
                    default,
                    command.Payload2,
                    GameplayComponentAbilityEvents.MissingRequestReason,
                    GameplayAbilityRuntimeFailureCode.AbilityCastFailed);
                return;
            }

            try
            {
                ExecuteResolvedRequestCast(context, command, componentWorld, handle, request);
            }
            finally
            {
                _requestStore.Remove(handle);
            }
        }

        private void ExecuteResolvedRequestCast(
            GameplaySystemContext context,
            RuntimeCommand command,
            GameplayComponentWorld componentWorld,
            GameplayComponentAbilityRequestHandle handle,
            GameplayComponentAbilityRequest request)
        {
            int abilityId = command.Payload2;
            if (abilityId != request.AbilityId)
            {
                EnqueueFailure(
                    context,
                    command,
                    request.CasterEntityId,
                    abilityId,
                    GameplayComponentAbilityEvents.InvalidRequestReason,
                    GameplayAbilityRuntimeFailureCode.AbilityCastFailed);
                return;
            }

            GameplayEntityId casterEntityId = request.CasterEntityId;
            if (!componentWorld.IsAlive(casterEntityId))
            {
                EnqueueFailure(
                    context,
                    command,
                    casterEntityId,
                    abilityId,
                    GameplayComponentAbilityEvents.MissingCasterReason,
                    GameplayAbilityRuntimeFailureCode.MissingCaster);
                return;
            }

            if (!_abilityRegistry.TryGet(abilityId, out IGameplayComponentAbility ability))
            {
                EnqueueFailure(
                    context,
                    command,
                    casterEntityId,
                    abilityId,
                    GameplayComponentAbilityEvents.MissingAbilityReason,
                    GameplayAbilityRuntimeFailureCode.MissingAbility);
                return;
            }

            var candidates = new List<GameplayComponentTargetCandidate>();
            if (!TryBuildCandidates(componentWorld, request, candidates))
            {
                EnqueueFailure(
                    context,
                    command,
                    casterEntityId,
                    abilityId,
                    GameplayComponentAbilityEvents.MissingTargetReason,
                    GameplayAbilityRuntimeFailureCode.AbilityCastFailed);
                return;
            }

            GameplayComponentTargetQuery query = request.TargetQuery ?? CreateDefaultQuery(componentWorld, casterEntityId);
            GameplayComponentTargetingResult targetingResult = _targetingService.Select(query, candidates);
            if (!targetingResult.HasTargets)
            {
                EnqueueFailure(
                    context,
                    command,
                    casterEntityId,
                    abilityId,
                    GameplayComponentAbilityEvents.NoValidTargetReason,
                    GameplayAbilityRuntimeFailureCode.AbilityCastFailed);
                return;
            }

            ExecuteAbilityWithRules(
                context,
                command,
                componentWorld,
                ability,
                casterEntityId,
                CopyTargetIds(targetingResult.SelectedTargets));
        }

        private void ExecuteAbilityWithRules(
            GameplaySystemContext context,
            RuntimeCommand command,
            GameplayComponentWorld componentWorld,
            IGameplayComponentAbility ability,
            GameplayEntityId casterEntityId,
            IReadOnlyList<GameplayEntityId> targetIds)
        {
            int abilityId = ability.AbilityId;
            GameplayComponentAbilityRules.RemoveExpiredCooldowns(componentWorld, casterEntityId, context.Frame);

            GameplayComponentAbilityRuleResult ruleResult = GameplayComponentAbilityRules.Evaluate(
                componentWorld,
                casterEntityId,
                abilityId,
                ability.Rules,
                context.Frame);
            if (!ruleResult.Success)
            {
                EnqueueFailure(
                    context,
                    command,
                    casterEntityId,
                    abilityId,
                    ruleResult.Reason,
                    MapFailureCode(ruleResult.FailureCode),
                    MapCommandResultReason(ruleResult.FailureCode),
                    ruleResult.DetailId,
                    ruleResult.RemainingFrames);
                return;
            }

            GameplayComponentAbilityRuleResult costCommitResult = GameplayComponentAbilityRules.CommitCosts(
                componentWorld,
                casterEntityId,
                abilityId,
                ability.Rules,
                context.Frame,
                command.CommandId,
                command.TraceId);
            if (!costCommitResult.Success)
            {
                EnqueueFailure(
                    context,
                    command,
                    casterEntityId,
                    abilityId,
                    costCommitResult.Reason,
                    MapFailureCode(costCommitResult.FailureCode),
                    MapCommandResultReason(costCommitResult.FailureCode),
                    costCommitResult.DetailId,
                    costCommitResult.RemainingFrames);
                return;
            }

            GameplayComponentAbilityResult result = ability.Cast(new GameplayComponentAbilityContext(
                context.Frame,
                componentWorld,
                casterEntityId,
                targetIds,
                command.TraceId,
                command.CommandId,
                _buffRequests));
            if (result == null)
            {
                EnqueueFailure(
                    context,
                    command,
                    casterEntityId,
                    abilityId,
                    GameplayComponentAbilityEvents.EffectFailedReason,
                    GameplayAbilityRuntimeFailureCode.AbilityCastFailed,
                    GameplayComponentCommandResultReasonCode.ComponentAbilityEffectFailed);
                return;
            }

            if (result.Success)
            {
                GameplayComponentAbilityRuleResult cooldownCommitResult = GameplayComponentAbilityRules.CommitCooldown(
                    componentWorld,
                    casterEntityId,
                    abilityId,
                    ability.Rules,
                    context.Frame);
                if (!cooldownCommitResult.Success)
                {
                    EnqueueFailure(
                        context,
                        command,
                        casterEntityId,
                        abilityId,
                        cooldownCommitResult.Reason,
                        MapFailureCode(cooldownCommitResult.FailureCode),
                        MapCommandResultReason(cooldownCommitResult.FailureCode),
                        cooldownCommitResult.DetailId,
                        cooldownCommitResult.RemainingFrames);
                    return;
                }
            }

            GameplayEntityId eventEntityId = ResolveEventEntity(result, casterEntityId);
            context.Events.Enqueue(context.Frame, new GameplayRuntimeEvent(
                context.Frame,
                result.Success ? GameplayRuntimeEventType.AbilityCastSucceeded : GameplayRuntimeEventType.AbilityCastFailed,
                command.CommandId,
                casterEntityId: 0,
                abilityId: abilityId,
                targetEntityId: eventEntityId.Index,
                failureCode: result.Success ? GameplayAbilityRuntimeFailureCode.None : MapFailureCode(result.FailureCode),
                reason: result.Success ? GameplayComponentAbilityEvents.CastComponentAbilityReason : result.FailureReason,
                traceId: command.TraceId,
                componentEntityIndex: eventEntityId.Index,
                componentEntityGeneration: eventEntityId.Generation));
            if (result.Success)
            {
                PublishCommandResult(
                    context.Frame,
                    in command,
                    GameplayComponentCommandResultStatus.Succeeded,
                    GameplayComponentCommandResultReasonCode.ComponentAbilityCastSucceeded,
                    abilityId);
            }
            else
            {
                PublishCommandResult(
                    context.Frame,
                    in command,
                    GameplayComponentCommandResultStatus.Rejected,
                    MapCommandResultReason(result.FailureCode),
                    abilityId,
                    result.FailureCode == GameplayComponentAbilityFailureCode.None ||
                    !IsDefinedFailureCode(result.FailureCode)
                        ? (int)result.FailureCode
                        : 0L);
            }
        }

        private static GameplayEntityId ResolveEventEntity(
            GameplayComponentAbilityResult result,
            GameplayEntityId casterEntityId)
        {
            if (result.TargetEntityIds.Count > 0 && result.TargetEntityIds[0].IsValid)
                return result.TargetEntityIds[0];

            return casterEntityId;
        }

        private static bool TryReadCaster(RuntimeCommand command, out GameplayEntityId casterEntityId)
        {
            int index = command.TargetId;
            int generation = command.Payload0;
            if (index <= 0 || generation <= 0)
            {
                casterEntityId = default;
                return false;
            }

            casterEntityId = new GameplayEntityId(index, generation);
            return true;
        }

        private static bool TryReadRequestHandle(RuntimeCommand command, out GameplayComponentAbilityRequestHandle handle)
        {
            int index = command.Payload0;
            int generation = command.Payload1;
            if (index <= 0 || generation <= 0 || command.TargetId != index || command.Payload2 <= 0)
            {
                handle = default;
                return false;
            }

            handle = new GameplayComponentAbilityRequestHandle(index, generation);
            return true;
        }

        private static bool TryBuildCandidates(
            GameplayComponentWorld world,
            GameplayComponentAbilityRequest request,
            IList<GameplayComponentTargetCandidate> output)
        {
            IReadOnlyList<GameplayEntityId> candidateIds = request.CandidateEntityIds;
            if (candidateIds.Count == 0)
            {
                GameplayComponentTargetCandidates.CopyFromWorld(world, output);
                return true;
            }

            for (int i = 0; i < candidateIds.Count; i++)
            {
                if (!GameplayComponentTargetCandidates.TryCreateFromWorld(world, candidateIds[i], out GameplayComponentTargetCandidate candidate))
                    return false;

                output.Add(candidate);
            }

            return true;
        }

        private static GameplayComponentTargetQuery CreateDefaultQuery(
            GameplayComponentWorld world,
            GameplayEntityId casterEntityId)
        {
            int casterTeamId = 0;
            if (world.TryGetStore(out GameplayComponentStore<GameplayTeamComponent> teams) &&
                teams.TryGet(casterEntityId, out GameplayTeamComponent team))
            {
                casterTeamId = team.TeamId;
            }

            return new GameplayComponentTargetQuery(casterEntityId, casterTeamId, requireAlive: true);
        }

        private static GameplayEntityId[] CopyTargetIds(IReadOnlyList<GameplayComponentTargetCandidate> targets)
        {
            if (targets == null || targets.Count == 0)
                return Array.Empty<GameplayEntityId>();

            var ids = new GameplayEntityId[targets.Count];
            for (int i = 0; i < targets.Count; i++)
                ids[i] = targets[i].EntityId;
            return ids;
        }

        private static GameplayAbilityRuntimeFailureCode MapFailureCode(GameplayComponentAbilityFailureCode failureCode)
        {
            switch (failureCode)
            {
                case GameplayComponentAbilityFailureCode.MissingCaster:
                    return GameplayAbilityRuntimeFailureCode.MissingCaster;
                case GameplayComponentAbilityFailureCode.MissingAbility:
                    return GameplayAbilityRuntimeFailureCode.MissingAbility;
                default:
                    return GameplayAbilityRuntimeFailureCode.AbilityCastFailed;
            }
        }

        private static GameplayComponentCommandResultReasonCode MapCommandResultReason(
            GameplayComponentAbilityFailureCode failureCode)
        {
            switch (failureCode)
            {
                case GameplayComponentAbilityFailureCode.MissingCaster:
                    return GameplayComponentCommandResultReasonCode.MissingComponentEntity;
                case GameplayComponentAbilityFailureCode.MissingAbility:
                    return GameplayComponentCommandResultReasonCode.MissingComponentAbility;
                case GameplayComponentAbilityFailureCode.MissingAttributeSet:
                    return GameplayComponentCommandResultReasonCode.MissingAbilityAttributeSet;
                case GameplayComponentAbilityFailureCode.MissingTarget:
                    return GameplayComponentCommandResultReasonCode.MissingAbilityTarget;
                case GameplayComponentAbilityFailureCode.EffectFailed:
                    return GameplayComponentCommandResultReasonCode.ComponentAbilityEffectFailed;
                case GameplayComponentAbilityFailureCode.InvalidCommandPayload:
                    return GameplayComponentCommandResultReasonCode.InvalidComponentAbilityCommandPayload;
                case GameplayComponentAbilityFailureCode.OnCooldown:
                    return GameplayComponentCommandResultReasonCode.ComponentAbilityOnCooldown;
                case GameplayComponentAbilityFailureCode.InsufficientCost:
                    return GameplayComponentCommandResultReasonCode.ComponentAbilityInsufficientCost;
                case GameplayComponentAbilityFailureCode.InvalidAbilityRule:
                    return GameplayComponentCommandResultReasonCode.InvalidComponentAbilityRule;
                default:
                    return GameplayComponentCommandResultReasonCode.InvalidComponentAbilityResult;
            }
        }

        private static bool IsDefinedFailureCode(GameplayComponentAbilityFailureCode failureCode)
        {
            return failureCode >= GameplayComponentAbilityFailureCode.MissingCaster &&
                   failureCode <= GameplayComponentAbilityFailureCode.InvalidAbilityRule;
        }

        private void EnqueueFailure(
            GameplaySystemContext context,
            RuntimeCommand command,
            GameplayEntityId entityId,
            int abilityId,
            string reason,
            GameplayAbilityRuntimeFailureCode failureCode,
            GameplayComponentCommandResultReasonCode resultReasonCode =
                GameplayComponentCommandResultReasonCode.None,
            int detailId = 0,
            long remainingFrames = 0L)
        {
            context.Events.Enqueue(context.Frame, new GameplayRuntimeEvent(
                context.Frame,
                GameplayRuntimeEventType.AbilityCastFailed,
                command.CommandId,
                casterEntityId: 0,
                abilityId: abilityId,
                targetEntityId: entityId.Index,
                failureCode: failureCode,
                reason: reason,
                traceId: command.TraceId,
                componentEntityIndex: entityId.Index,
                componentEntityGeneration: entityId.Generation));
            if (command.CommandId != GameplayRuntimeCommandIds.CastComponentAbility)
                return;

            PublishCommandResult(
                context.Frame,
                in command,
                GameplayComponentCommandResultStatus.Rejected,
                resultReasonCode,
                abilityId,
                detailId,
                remainingFrames);
        }

        private void PublishCommandResult(
            RuntimeFrame frame,
            in RuntimeCommand command,
            GameplayComponentCommandResultStatus status,
            GameplayComponentCommandResultReasonCode reasonCode,
            long payload0 = 0L,
            long payload1 = 0L,
            long payload2 = 0L)
        {
            if (_resultSink == null ||
                command.CommandId != GameplayRuntimeCommandIds.CastComponentAbility ||
                !command.Envelope.IsPresent)
                return;

            GameplayComponentCommandResult result =
                GameplayComponentCommandResult.FromCommand(
                    frame,
                    in command,
                    status,
                    reasonCode,
                    payload0,
                    payload1,
                    payload2);
            _resultSink.Publish(in result);
        }
    }
}
