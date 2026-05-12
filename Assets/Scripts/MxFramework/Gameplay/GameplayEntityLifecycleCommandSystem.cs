using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public sealed class GameplayEntityLifecycleCommandSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.command.entity_lifecycle";

        public GameplayEntityLifecycleCommandSystem(
            string systemId = DefaultSystemId,
            int priority = 10)
        {
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
                if (command.CommandId != GameplayRuntimeCommandIds.DespawnEntity)
                    continue;

                ExecuteDespawnEntity(context, command);
                context.CommandState.MarkHandled(command);
            }
        }

        private static void ExecuteDespawnEntity(GameplaySystemContext context, RuntimeCommand command)
        {
            int entityId = command.Payload0 != 0 ? command.Payload0 : command.TargetId;
            bool removed = context.World.Remove(entityId);
            context.Events.Enqueue(context.Frame, new GameplayRuntimeEvent(
                context.Frame,
                removed ? GameplayRuntimeEventType.EntityDespawned : GameplayRuntimeEventType.CommandRejected,
                command.CommandId,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: entityId,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: removed ? string.Empty : "MissingEntity",
                traceId: command.TraceId));
        }
    }
}
