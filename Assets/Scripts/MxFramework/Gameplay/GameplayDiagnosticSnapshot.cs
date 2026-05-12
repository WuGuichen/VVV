using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Pure C# diagnostic state for runtime gameplay systems.</summary>
    public sealed class GameplayDiagnosticSnapshot
    {
        private readonly GameplayEntitySnapshot[] _entities;
        private readonly GameplayAbilityEventSnapshot[] _abilityEvents;
        private readonly GameplayAttributeEventSnapshot[] _attributeEvents;

        public GameplayDiagnosticSnapshot(
            string sourceName,
            string abilitySource,
            IReadOnlyList<GameplayEntitySnapshot> entities,
            GameplayAbilityCastSnapshot lastCast,
            IReadOnlyList<GameplayAbilityEventSnapshot> abilityEvents,
            IReadOnlyList<GameplayAttributeEventSnapshot> attributeEvents)
        {
            SourceName = sourceName ?? string.Empty;
            AbilitySource = abilitySource ?? string.Empty;
            _entities = Copy(entities);
            LastCast = lastCast;
            _abilityEvents = Copy(abilityEvents);
            _attributeEvents = Copy(attributeEvents);
        }

        public string SourceName { get; }
        public string AbilitySource { get; }
        public IReadOnlyList<GameplayEntitySnapshot> Entities => _entities;
        public GameplayAbilityCastSnapshot LastCast { get; }
        public bool LastCastSuccess => LastCast.LastCastSuccess;
        public string LastFailureReason => LastCast.LastFailureReason;
        public IReadOnlyList<int> LastTargetEntityIds => LastCast.LastTargetEntityIds;
        public IReadOnlyList<GameplayAbilityEventSnapshot> AbilityEvents => _abilityEvents;
        public IReadOnlyList<GameplayAttributeEventSnapshot> AttributeEvents => _attributeEvents;

        private static T[] Copy<T>(IReadOnlyList<T> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<T>();

            var copy = new T[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }
    }

    public readonly struct GameplayEntitySnapshot
    {
        private readonly GameplayAttributeSnapshot[] _attributes;
        private readonly GameplayBuffSnapshot[] _buffs;
        private readonly GameplayModifierSnapshot[] _modifiers;

        public GameplayEntitySnapshot(
            int entityId,
            int teamId,
            bool isAlive,
            IReadOnlyList<GameplayAttributeSnapshot> attributes,
            IReadOnlyList<GameplayBuffSnapshot> buffs,
            IReadOnlyList<GameplayModifierSnapshot> modifiers)
        {
            EntityId = entityId;
            TeamId = teamId;
            IsAlive = isAlive;
            _attributes = Copy(attributes);
            _buffs = Copy(buffs);
            _modifiers = Copy(modifiers);
        }

        public int EntityId { get; }
        public int TeamId { get; }
        public bool IsAlive { get; }
        public IReadOnlyList<GameplayAttributeSnapshot> Attributes => _attributes ?? Array.Empty<GameplayAttributeSnapshot>();
        public IReadOnlyList<GameplayBuffSnapshot> Buffs => _buffs ?? Array.Empty<GameplayBuffSnapshot>();
        public IReadOnlyList<GameplayModifierSnapshot> Modifiers => _modifiers ?? Array.Empty<GameplayModifierSnapshot>();

        private static T[] Copy<T>(IReadOnlyList<T> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<T>();

            var copy = new T[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }
    }

    public readonly struct GameplayAttributeSnapshot
    {
        public GameplayAttributeSnapshot(int attributeId, int finalValue)
        {
            AttributeId = attributeId;
            FinalValue = finalValue;
        }

        public int AttributeId { get; }
        public int FinalValue { get; }
    }

    public readonly struct GameplayBuffSnapshot
    {
        public GameplayBuffSnapshot(
            int buffId,
            float duration,
            float remainingTime,
            int currentLayers,
            int maxLayers,
            bool isPermanent,
            bool isExpired)
        {
            BuffId = buffId;
            Duration = duration;
            RemainingTime = remainingTime;
            CurrentLayers = currentLayers;
            MaxLayers = maxLayers;
            IsPermanent = isPermanent;
            IsExpired = isExpired;
        }

        public int BuffId { get; }
        public float Duration { get; }
        public float RemainingTime { get; }
        public int CurrentLayers { get; }
        public int MaxLayers { get; }
        public bool IsPermanent { get; }
        public bool IsExpired { get; }
    }

    public readonly struct GameplayModifierSnapshot
    {
        public GameplayModifierSnapshot(int modifierId, int paramIndex)
        {
            ModifierId = modifierId;
            ParamIndex = paramIndex;
        }

        public int ModifierId { get; }
        public int ParamIndex { get; }
    }

    public readonly struct GameplayAbilityCastSnapshot
    {
        private readonly int[] _lastTargetEntityIds;

        public GameplayAbilityCastSnapshot(
            string abilitySource,
            bool lastCastSuccess,
            string lastFailureReason,
            IReadOnlyList<int> lastTargetEntityIds)
        {
            AbilitySource = abilitySource;
            LastCastSuccess = lastCastSuccess;
            LastFailureReason = lastFailureReason;
            _lastTargetEntityIds = Copy(lastTargetEntityIds);
        }

        public string AbilitySource { get; }
        public bool LastCastSuccess { get; }
        public string LastFailureReason { get; }
        public IReadOnlyList<int> LastTargetEntityIds => _lastTargetEntityIds ?? Array.Empty<int>();

        private static int[] Copy(IReadOnlyList<int> values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<int>();

            var copy = new int[values.Count];
            for (int i = 0; i < values.Count; i++)
                copy[i] = values[i];
            return copy;
        }
    }

    public readonly struct GameplayAbilityEventSnapshot
    {
        public GameplayAbilityEventSnapshot(
            string eventType,
            int abilityId,
            int? casterEntityId,
            int? targetEntityId,
            string failureReason)
        {
            EventType = eventType;
            AbilityId = abilityId;
            CasterEntityId = casterEntityId;
            TargetEntityId = targetEntityId;
            FailureReason = failureReason;
        }

        public string EventType { get; }
        public int AbilityId { get; }
        public int? CasterEntityId { get; }
        public int? TargetEntityId { get; }
        public string FailureReason { get; }
    }

    public readonly struct GameplayAttributeEventSnapshot
    {
        public GameplayAttributeEventSnapshot(
            int attributeId,
            int baseValue,
            int oldValue,
            int newValue,
            int delta,
            string sourceName)
        {
            AttributeId = attributeId;
            BaseValue = baseValue;
            OldValue = oldValue;
            NewValue = newValue;
            Delta = delta;
            SourceName = sourceName;
        }

        public int AttributeId { get; }
        public int BaseValue { get; }
        public int OldValue { get; }
        public int NewValue { get; }
        public int Delta { get; }
        public string SourceName { get; }
    }
}
