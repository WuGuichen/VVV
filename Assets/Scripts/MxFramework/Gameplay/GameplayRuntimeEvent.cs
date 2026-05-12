using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public enum GameplayRuntimeEventType
    {
        None = 0,
        AbilityCastSucceeded = 1,
        AbilityCastFailed = 2,
        EntityDespawned = 3,
        CommandRejected = 4,
        WorldTicked = 5,
        ComponentEntityCreated = 6,
        ComponentEntityDestroyed = 7,
        ComponentAttributeChanged = 8
    }

    public readonly struct GameplayRuntimeEvent
    {
        public GameplayRuntimeEvent(
            RuntimeFrame frame,
            GameplayRuntimeEventType type,
            int commandId,
            int casterEntityId,
            int abilityId,
            int targetEntityId,
            GameplayAbilityRuntimeFailureCode failureCode,
            string reason,
            string traceId,
            int componentEntityIndex = 0,
            int componentEntityGeneration = 0,
            int attributeId = 0,
            int oldAttributeValue = 0,
            int newAttributeValue = 0,
            int attributeDelta = 0)
        {
            Frame = frame;
            Type = type;
            CommandId = commandId;
            CasterEntityId = casterEntityId;
            AbilityId = abilityId;
            TargetEntityId = targetEntityId;
            FailureCode = failureCode;
            Reason = reason ?? string.Empty;
            TraceId = traceId ?? string.Empty;
            ValidateComponentEntity(componentEntityIndex, componentEntityGeneration);
            if (attributeId < 0)
                throw new ArgumentOutOfRangeException(nameof(attributeId), "Gameplay runtime event attribute id cannot be negative.");

            ComponentEntityIndex = componentEntityIndex;
            ComponentEntityGeneration = componentEntityGeneration;
            AttributeId = attributeId;
            OldAttributeValue = oldAttributeValue;
            NewAttributeValue = newAttributeValue;
            AttributeDelta = attributeDelta;
        }

        public RuntimeFrame Frame { get; }
        public GameplayRuntimeEventType Type { get; }
        public int CommandId { get; }
        public int CasterEntityId { get; }
        public int AbilityId { get; }
        public int TargetEntityId { get; }
        public GameplayAbilityRuntimeFailureCode FailureCode { get; }
        public string Reason { get; }
        public string TraceId { get; }
        public int ComponentEntityIndex { get; }
        public int ComponentEntityGeneration { get; }
        public int AttributeId { get; }
        public int OldAttributeValue { get; }
        public int NewAttributeValue { get; }
        public int AttributeDelta { get; }
        public GameplayEntityId ComponentEntityId => new GameplayEntityId(ComponentEntityIndex, ComponentEntityGeneration);

        public bool TryGetComponentEntityId(out GameplayEntityId entityId)
        {
            if (ComponentEntityIndex <= 0 || ComponentEntityGeneration <= 0)
            {
                entityId = default;
                return false;
            }

            entityId = new GameplayEntityId(ComponentEntityIndex, ComponentEntityGeneration);
            return true;
        }

        private static void ValidateComponentEntity(int index, int generation)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), "Gameplay runtime event component entity index cannot be negative.");
            if (generation < 0)
                throw new ArgumentOutOfRangeException(nameof(generation), "Gameplay runtime event component entity generation cannot be negative.");
            if ((index == 0) != (generation == 0))
                throw new ArgumentException("Gameplay runtime event component entity id must be either default or have both index and generation greater than zero.");
        }
    }
}
