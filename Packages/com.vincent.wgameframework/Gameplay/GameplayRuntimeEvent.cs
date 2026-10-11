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
        ComponentAttributeChanged = 8,
        ComponentBuffApplied = 9,
        ComponentBuffRefreshed = 10,
        ComponentBuffRemoved = 11,
        ComponentBuffExpired = 12,
        ComponentBuffRejected = 13
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
            RuntimeTraceIdentity traceId,
            int componentEntityIndex = 0,
            int componentEntityGeneration = 0,
            int attributeId = 0,
            int oldAttributeValue = 0,
            int newAttributeValue = 0,
            int attributeDelta = 0,
            int buffId = 0,
            int oldBuffStackCount = 0,
            int newBuffStackCount = 0,
            long oldBuffEndFrame = 0L,
            long newBuffEndFrame = 0L)
        {
            Frame = frame;
            Type = type;
            CommandId = commandId;
            CasterEntityId = casterEntityId;
            AbilityId = abilityId;
            TargetEntityId = targetEntityId;
            FailureCode = failureCode;
            Reason = reason ?? string.Empty;
            TraceId = traceId;
            ValidateComponentEntity(componentEntityIndex, componentEntityGeneration);
            if (attributeId < 0)
                throw new ArgumentOutOfRangeException(nameof(attributeId), "Gameplay runtime event attribute id cannot be negative.");
            if (buffId < 0)
                throw new ArgumentOutOfRangeException(nameof(buffId), "Gameplay runtime event buff id cannot be negative.");
            if (oldBuffStackCount < 0)
                throw new ArgumentOutOfRangeException(nameof(oldBuffStackCount), "Gameplay runtime event old buff stack count cannot be negative.");
            if (newBuffStackCount < 0)
                throw new ArgumentOutOfRangeException(nameof(newBuffStackCount), "Gameplay runtime event new buff stack count cannot be negative.");
            if (oldBuffEndFrame < 0L)
                throw new ArgumentOutOfRangeException(nameof(oldBuffEndFrame), "Gameplay runtime event old buff end frame cannot be negative.");
            if (newBuffEndFrame < 0L)
                throw new ArgumentOutOfRangeException(nameof(newBuffEndFrame), "Gameplay runtime event new buff end frame cannot be negative.");

            ComponentEntityIndex = componentEntityIndex;
            ComponentEntityGeneration = componentEntityGeneration;
            AttributeId = attributeId;
            OldAttributeValue = oldAttributeValue;
            NewAttributeValue = newAttributeValue;
            AttributeDelta = attributeDelta;
            BuffId = buffId;
            OldBuffStackCount = oldBuffStackCount;
            NewBuffStackCount = newBuffStackCount;
            OldBuffEndFrame = oldBuffEndFrame;
            NewBuffEndFrame = newBuffEndFrame;
        }

        public RuntimeFrame Frame { get; }
        public GameplayRuntimeEventType Type { get; }
        public int CommandId { get; }
        public int CasterEntityId { get; }
        public int AbilityId { get; }
        public int TargetEntityId { get; }
        public GameplayAbilityRuntimeFailureCode FailureCode { get; }
        public string Reason { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public int ComponentEntityIndex { get; }
        public int ComponentEntityGeneration { get; }
        public int AttributeId { get; }
        public int OldAttributeValue { get; }
        public int NewAttributeValue { get; }
        public int AttributeDelta { get; }
        public int BuffId { get; }
        public int OldBuffStackCount { get; }
        public int NewBuffStackCount { get; }
        public long OldBuffEndFrame { get; }
        public long NewBuffEndFrame { get; }
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
