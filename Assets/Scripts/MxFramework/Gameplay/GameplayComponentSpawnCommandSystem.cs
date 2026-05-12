using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentSpawnCommandSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.command.component_spawn";

        private readonly GameplayComponentSpawnRegistry _spawnRegistry;

        public GameplayComponentSpawnCommandSystem(
            GameplayComponentSpawnRegistry spawnRegistry,
            string systemId = DefaultSystemId,
            int priority = 30)
        {
            _spawnRegistry = spawnRegistry;
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
                if (command.CommandId != GameplayRuntimeCommandIds.SpawnComponentEntity)
                    continue;

                ExecuteSpawn(context, command);
                context.CommandState.MarkHandled(command);
            }
        }

        private void ExecuteSpawn(GameplaySystemContext context, RuntimeCommand command)
        {
            GameplayComponentWorld componentWorld = context.ComponentWorld;
            if (componentWorld == null)
            {
                EnqueueRejected(context, command, default, GameplayComponentSpawnEvents.MissingComponentWorldReason);
                return;
            }

            if (_spawnRegistry == null)
            {
                EnqueueRejected(context, command, default, GameplayComponentSpawnEvents.MissingSpawnRegistryReason);
                return;
            }

            int definitionId = command.Payload0;
            if (!_spawnRegistry.TryGet(definitionId, out GameplayComponentSpawnDefinition definition))
            {
                EnqueueRejected(context, command, default, GameplayComponentSpawnEvents.MissingSpawnDefinitionReason);
                return;
            }

            if (definition.Initializers.Count == 0)
            {
                EnqueueRejected(context, command, default, GameplayComponentSpawnEvents.InvalidSpawnDefinitionReason);
                return;
            }

            GameplayEntityId entityId = componentWorld.CreateEntity();
            var spawnContext = new GameplayComponentSpawnContext(context.Frame, command, definition, command.Payload1);
            for (int i = 0; i < definition.Initializers.Count; i++)
            {
                IGameplayComponentSpawnInitializer initializer = definition.Initializers[i];
                RuntimeSaveStateResult<bool> result = initializer.Apply(componentWorld, entityId, spawnContext);
                if (!result.Success)
                {
                    componentWorld.DestroyEntity(entityId);
                    EnqueueRejected(context, command, entityId, GameplayComponentSpawnEvents.SpawnInitializerFailedReason);
                    return;
                }
            }

            context.Events.Enqueue(context.Frame, new GameplayRuntimeEvent(
                context.Frame,
                GameplayRuntimeEventType.ComponentEntityCreated,
                command.CommandId,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: entityId.Index,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: GameplayComponentSpawnEvents.SpawnedReason,
                traceId: command.TraceId,
                componentEntityIndex: entityId.Index,
                componentEntityGeneration: entityId.Generation));
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
