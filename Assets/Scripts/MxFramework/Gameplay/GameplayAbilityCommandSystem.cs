using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public sealed class GameplayAbilityCommandSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.command.ability";

        private readonly GameplayAbilityRegistry _abilityRegistry;
        private readonly Action<GameplayAbilityRuntimeResult> _resultSink;

        public GameplayAbilityCommandSystem(
            GameplayAbilityRegistry abilityRegistry,
            Action<GameplayAbilityRuntimeResult> resultSink = null,
            string systemId = DefaultSystemId,
            int priority = 0)
        {
            _abilityRegistry = abilityRegistry ?? throw new ArgumentNullException(nameof(abilityRegistry));
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

        public void Tick(GameplaySystemContext context)
        {
            IReadOnlyList<RuntimeCommand> commands = context.Commands;
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                if (command.CommandId != GameplayRuntimeCommandIds.CastAbility)
                    continue;

                ExecuteCastAbility(context, command);
                context.CommandState.MarkHandled(command);
            }
        }

        private void ExecuteCastAbility(GameplaySystemContext context, RuntimeCommand command)
        {
            int casterEntityId = command.Payload0 != 0 ? command.Payload0 : command.TargetId;
            int abilityId = command.Payload1;
            int candidateEntityId = command.Payload2;
            IReadOnlyList<int> candidates = candidateEntityId > 0
                ? new[] { candidateEntityId }
                : null;

            var service = new GameplayAbilityRuntimeService(context.World.Entities.CreateSnapshot(), _abilityRegistry);
            GameplayAbilityRuntimeResult result = service.Cast(new GameplayAbilityCastRequest(
                casterEntityId,
                abilityId,
                candidates,
                command.TraceId));

            _resultSink?.Invoke(result);

            int firstTargetId = result.TargetEntityIds.Count == 0 ? 0 : result.TargetEntityIds[0];
            context.Events.Enqueue(context.Frame, new GameplayRuntimeEvent(
                context.Frame,
                result.Success ? GameplayRuntimeEventType.AbilityCastSucceeded : GameplayRuntimeEventType.AbilityCastFailed,
                command.CommandId,
                casterEntityId,
                abilityId,
                firstTargetId,
                result.FailureCode,
                result.FailureReason,
                command.TraceId));
        }
    }
}
