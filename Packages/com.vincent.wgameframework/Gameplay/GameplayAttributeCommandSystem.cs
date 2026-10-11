using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public sealed class GameplayAttributeCommandSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.command.attribute";

        private readonly IGameplayComponentCommandResultSink _resultSink;

        public GameplayAttributeCommandSystem(
            string systemId = DefaultSystemId,
            int priority = 40)
            : this(systemId, priority, null)
        {
        }

        public GameplayAttributeCommandSystem(
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

        private void Execute(GameplaySystemContext context, RuntimeCommand command, bool isAdd)
        {
            GameplayComponentWorld componentWorld = context.ComponentWorld;
            if (componentWorld == null)
            {
                EnqueueRejected(
                    context,
                    command,
                    default,
                    GameplayAttributeEvents.MissingComponentWorldReason,
                    GameplayComponentCommandResultReasonCode.MissingComponentWorld);
                return;
            }

            if (!TryReadEntity(command, out GameplayEntityId entityId))
            {
                EnqueueRejected(
                    context,
                    command,
                    default,
                    GameplayAttributeEvents.InvalidComponentEntityReason,
                    GameplayComponentCommandResultReasonCode.InvalidComponentEntity);
                return;
            }

            if (!componentWorld.IsAlive(entityId))
            {
                EnqueueRejected(
                    context,
                    command,
                    entityId,
                    GameplayAttributeEvents.MissingComponentEntityReason,
                    GameplayComponentCommandResultReasonCode.MissingComponentEntity);
                return;
            }

            int attributeId = command.Payload1;
            if (attributeId <= 0)
            {
                EnqueueRejected(
                    context,
                    command,
                    entityId,
                    GameplayAttributeEvents.InvalidAttributeIdReason,
                    GameplayComponentCommandResultReasonCode.InvalidAttributeId);
                return;
            }

            GameplayComponentStore<GameplayAttributeSetComponent> store =
                componentWorld.GetOrCreateStore<GameplayAttributeSetComponent>();
            bool hasSet = store.TryGet(entityId, out GameplayAttributeSetComponent attributes);
            GameplayAttributeValue attribute = default;
            bool hasAttribute = hasSet && attributes.TryGet(attributeId, out attribute);
            if (isAdd && !hasSet)
            {
                EnqueueRejected(
                    context,
                    command,
                    entityId,
                    GameplayAttributeEvents.MissingAttributeSetReason,
                    GameplayComponentCommandResultReasonCode.MissingAttributeSet);
                return;
            }
            if (isAdd && !hasAttribute)
            {
                EnqueueRejected(
                    context,
                    command,
                    entityId,
                    GameplayAttributeEvents.MissingAttributeReason,
                    GameplayComponentCommandResultReasonCode.MissingAttribute);
                return;
            }

            int oldValue = hasAttribute ? attribute.CurrentValue : 0;
            int newValue;
            GameplayAttributeSetComponent updated;
            try
            {
                if (isAdd)
                {
                    newValue = checked(attribute.CurrentValue + command.Payload2);
                    updated = attributes.WithValue(new GameplayAttributeValue(
                        attributeId,
                        attribute.BaseValue,
                        newValue));
                }
                else
                {
                    newValue = command.Payload2;
                    updated = attributes.WithValue(new GameplayAttributeValue(
                        attributeId,
                        hasAttribute ? attribute.BaseValue : command.Payload2,
                        newValue));
                }
            }
            catch (Exception)
            {
                EnqueueRejected(
                    context,
                    command,
                    entityId,
                    GameplayAttributeEvents.AttributeUpdateFailedReason,
                    GameplayComponentCommandResultReasonCode.AttributeUpdateFailed);
                return;
            }

            store.Set(entityId, updated);
            EnqueueChanged(context, command, entityId, attributeId, oldValue, newValue, isAdd ? command.Payload2 : newValue - oldValue, isAdd);
            PublishResult(
                context.Frame,
                in command,
                GameplayComponentCommandResultStatus.Succeeded,
                isAdd
                    ? GameplayComponentCommandResultReasonCode.ComponentAttributeAdded
                    : GameplayComponentCommandResultReasonCode.ComponentAttributeSet,
                oldValue,
                newValue,
                isAdd ? command.Payload2 : newValue - oldValue);
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

        private void EnqueueRejected(
            GameplaySystemContext context,
            RuntimeCommand command,
            GameplayEntityId entityId,
            string reason,
            GameplayComponentCommandResultReasonCode reasonCode)
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
            PublishResult(
                context.Frame,
                in command,
                GameplayComponentCommandResultStatus.Rejected,
                reasonCode,
                command.Payload1);
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
