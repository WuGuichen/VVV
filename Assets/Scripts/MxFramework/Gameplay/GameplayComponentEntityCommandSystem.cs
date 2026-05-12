using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentEntityCommandSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.command.component_entity";
        public const string MissingComponentWorldReason = "MissingComponentWorld";
        public const string InvalidEntityReason = "InvalidComponentEntity";
        public const string MissingEntityReason = "MissingComponentEntity";

        public GameplayComponentEntityCommandSystem(
            string systemId = DefaultSystemId,
            int priority = 20)
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
                if (command.CommandId == GameplayRuntimeCommandIds.CreateComponentEntity)
                {
                    if (TryGetComponentWorld(context, command, out GameplayComponentWorld componentWorld))
                        ExecuteCreate(context, command, componentWorld);

                    context.CommandState.MarkHandled(command);
                    continue;
                }

                if (command.CommandId == GameplayRuntimeCommandIds.DestroyComponentEntity)
                {
                    if (TryGetComponentWorld(context, command, out GameplayComponentWorld componentWorld))
                        ExecuteDestroy(context, command, componentWorld);

                    context.CommandState.MarkHandled(command);
                }
            }
        }

        private static void ExecuteCreate(
            GameplaySystemContext context,
            RuntimeCommand command,
            GameplayComponentWorld componentWorld)
        {
            GameplayEntityId entityId = componentWorld.CreateEntity();
            context.Events.Enqueue(context.Frame, new GameplayRuntimeEvent(
                context.Frame,
                GameplayRuntimeEventType.ComponentEntityCreated,
                command.CommandId,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: entityId.Index,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: string.Empty,
                traceId: command.TraceId,
                componentEntityIndex: entityId.Index,
                componentEntityGeneration: entityId.Generation));
        }

        private static void ExecuteDestroy(
            GameplaySystemContext context,
            RuntimeCommand command,
            GameplayComponentWorld componentWorld)
        {
            if (!TryReadEntityId(command, out GameplayEntityId entityId))
            {
                EnqueueRejected(context, command, entityId, InvalidEntityReason);
                return;
            }

            if (!componentWorld.DestroyEntity(entityId))
            {
                EnqueueRejected(context, command, entityId, MissingEntityReason);
                return;
            }

            context.Events.Enqueue(context.Frame, new GameplayRuntimeEvent(
                context.Frame,
                GameplayRuntimeEventType.ComponentEntityDestroyed,
                command.CommandId,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: entityId.Index,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: string.Empty,
                traceId: command.TraceId,
                componentEntityIndex: entityId.Index,
                componentEntityGeneration: entityId.Generation));
        }

        private static bool TryGetComponentWorld(
            GameplaySystemContext context,
            RuntimeCommand command,
            out GameplayComponentWorld componentWorld)
        {
            componentWorld = context.ComponentWorld;
            if (componentWorld != null)
                return true;

            EnqueueRejected(context, command, default, MissingComponentWorldReason);
            return false;
        }

        private static bool TryReadEntityId(RuntimeCommand command, out GameplayEntityId entityId)
        {
            int index = command.Payload0;
            int generation = command.Payload1;
            if (index <= 0 || generation <= 0)
            {
                entityId = default;
                return false;
            }

            entityId = new GameplayEntityId(index, generation);
            return true;
        }

        private static void EnqueueRejected(
            GameplaySystemContext context,
            RuntimeCommand command,
            GameplayEntityId entityId,
            string reason)
        {
            context.Events.Enqueue(context.Frame, new GameplayRuntimeEvent(
                context.Frame,
                GameplayRuntimeEventType.CommandRejected,
                command.CommandId,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: entityId.Index,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: reason,
                traceId: command.TraceId,
                componentEntityIndex: entityId.Index,
                componentEntityGeneration: entityId.Generation));
        }
    }
}
