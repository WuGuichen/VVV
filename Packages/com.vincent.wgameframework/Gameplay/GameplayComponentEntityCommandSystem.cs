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

        private readonly IGameplayComponentCommandResultSink _resultSink;

        public GameplayComponentEntityCommandSystem(
            string systemId = DefaultSystemId,
            int priority = 20)
            : this(systemId, priority, null)
        {
        }

        public GameplayComponentEntityCommandSystem(
            string systemId,
            int priority,
            IGameplayComponentCommandResultSink resultSink)
        {
            SystemId = systemId ?? string.Empty;
            Priority = priority;
            _resultSink = resultSink;
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
                if (command.CommandId == GameplayRuntimeCommandIds.CreateComponentEntity)
                {
                    if (TryGetComponentWorld(context, command, out GameplayComponentWorld componentWorld))
                        ExecuteCreate(context, command, componentWorld);

                    context.CommandState.MarkHandled(command);
                    continue;
                }

                if (command.CommandId == GameplayRuntimeCommandIds.DestroyComponentEntity)
                {
                    ExecuteDestroy(context, command);
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

        private void ExecuteDestroy(
            GameplaySystemContext context,
            RuntimeCommand command)
        {
            GameplayComponentWorld componentWorld = context.ComponentWorld;
            if (componentWorld == null)
            {
                EnqueueRejected(context, command, default, MissingComponentWorldReason);
                PublishResult(
                    context.Frame,
                    in command,
                    GameplayComponentCommandResultStatus.Rejected,
                    GameplayComponentCommandResultReasonCode.MissingComponentWorld,
                    command.TargetId,
                    command.Payload1);
                return;
            }

            if (!TryReadEntityId(command, out GameplayEntityId entityId))
            {
                EnqueueRejected(context, command, entityId, InvalidEntityReason);
                PublishResult(
                    context.Frame,
                    in command,
                    GameplayComponentCommandResultStatus.Rejected,
                    GameplayComponentCommandResultReasonCode.InvalidComponentEntity,
                    command.TargetId,
                    command.Payload1);
                return;
            }

            if (!componentWorld.DestroyEntity(entityId))
            {
                EnqueueRejected(context, command, entityId, MissingEntityReason);
                PublishResult(
                    context.Frame,
                    in command,
                    GameplayComponentCommandResultStatus.Rejected,
                    GameplayComponentCommandResultReasonCode.MissingComponentEntity,
                    entityId.Index,
                    entityId.Generation);
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
            PublishResult(
                context.Frame,
                in command,
                GameplayComponentCommandResultStatus.Succeeded,
                GameplayComponentCommandResultReasonCode.ComponentEntityDestroyed);
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

        private void PublishResult(
            RuntimeFrame frame,
            in RuntimeCommand command,
            GameplayComponentCommandResultStatus status,
            GameplayComponentCommandResultReasonCode reasonCode,
            long payload0 = 0L,
            long payload1 = 0L,
            long payload2 = 0L)
        {
            if (_resultSink == null || !command.Envelope.IsPresent)
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
