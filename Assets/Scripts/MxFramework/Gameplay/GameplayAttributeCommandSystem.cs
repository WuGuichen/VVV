using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public sealed class GameplayAttributeCommandSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.command.attribute";

        public GameplayAttributeCommandSystem(
            string systemId = DefaultSystemId,
            int priority = 40)
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
                if (command.CommandId == GameplayRuntimeCommandIds.SetComponentAttribute)
                {
                    Execute(context, command, isAdd: false);
                    context.CommandState.MarkHandled(command);
                    continue;
                }

                if (command.CommandId == GameplayRuntimeCommandIds.AddComponentAttribute)
                {
                    Execute(context, command, isAdd: true);
                    context.CommandState.MarkHandled(command);
                }
            }
        }

        private static void Execute(GameplaySystemContext context, RuntimeCommand command, bool isAdd)
        {
            GameplayComponentWorld componentWorld = context.ComponentWorld;
            if (componentWorld == null)
            {
                EnqueueRejected(context, command, default, GameplayAttributeEvents.MissingComponentWorldReason);
                return;
            }

            if (!TryReadEntity(command, out GameplayEntityId entityId))
            {
                EnqueueRejected(context, command, default, GameplayAttributeEvents.InvalidComponentEntityReason);
                return;
            }

            if (!componentWorld.IsAlive(entityId))
            {
                EnqueueRejected(context, command, entityId, GameplayAttributeEvents.MissingComponentEntityReason);
                return;
            }

            int attributeId = command.Payload1;
            if (attributeId <= 0)
            {
                EnqueueRejected(context, command, entityId, GameplayAttributeEvents.InvalidAttributeIdReason);
                return;
            }

            GameplayComponentStore<GameplayAttributeSetComponent> store =
                componentWorld.GetOrCreateStore<GameplayAttributeSetComponent>();
            bool hasSet = store.TryGet(entityId, out GameplayAttributeSetComponent attributes);
            if (isAdd && !hasSet)
            {
                EnqueueRejected(context, command, entityId, GameplayAttributeEvents.MissingAttributeSetReason);
                return;
            }
            if (isAdd && !attributes.TryGet(attributeId, out _))
            {
                EnqueueRejected(context, command, entityId, GameplayAttributeEvents.MissingAttributeReason);
                return;
            }

            int oldValue = attributes.GetCurrentValueOrDefault(attributeId);
            GameplayAttributeSetComponent updated;
            try
            {
                updated = isAdd
                    ? attributes.AddCurrentValue(attributeId, command.Payload2)
                    : attributes.SetCurrentValue(attributeId, command.Payload2);
            }
            catch (Exception)
            {
                EnqueueRejected(context, command, entityId, GameplayAttributeEvents.AttributeUpdateFailedReason);
                return;
            }

            store.Set(entityId, updated);
            int newValue = updated.GetCurrentValueOrDefault(attributeId);
            EnqueueChanged(context, command, entityId, attributeId, oldValue, newValue, isAdd ? command.Payload2 : newValue - oldValue, isAdd);
        }

        private static bool TryReadEntity(RuntimeCommand command, out GameplayEntityId entityId)
        {
            int index = command.TargetId;
            int generation = command.Payload0;
            if (index <= 0 || generation <= 0)
            {
                entityId = default;
                return false;
            }

            entityId = new GameplayEntityId(index, generation);
            return true;
        }

        private static void EnqueueChanged(
            GameplaySystemContext context,
            RuntimeCommand command,
            GameplayEntityId entityId,
            int attributeId,
            int oldValue,
            int newValue,
            int delta,
            bool isAdd)
        {
            context.Events.Enqueue(context.Frame, new GameplayRuntimeEvent(
                context.Frame,
                GameplayRuntimeEventType.ComponentAttributeChanged,
                command.CommandId,
                casterEntityId: 0,
                abilityId: 0,
                targetEntityId: entityId.Index,
                failureCode: GameplayAbilityRuntimeFailureCode.None,
                reason: isAdd ? GameplayAttributeEvents.AddAttributeReason : GameplayAttributeEvents.SetAttributeReason,
                traceId: command.TraceId,
                componentEntityIndex: entityId.Index,
                componentEntityGeneration: entityId.Generation,
                attributeId: attributeId,
                oldAttributeValue: oldValue,
                newAttributeValue: newValue,
                attributeDelta: delta));
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
                componentEntityGeneration: entityId.Generation,
                attributeId: command.Payload1));
        }
    }
}
