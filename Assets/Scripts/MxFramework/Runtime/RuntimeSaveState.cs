using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Runtime
{
    public sealed class RuntimeSaveState
    {
        public const int CurrentSchemaVersion = 0;

        public RuntimeSaveState(
            int schemaVersion,
            DateTime createdAtUtc,
            string frameworkVersion,
            string configVersion,
            string resourceCatalogVersion,
            long frame,
            IReadOnlyList<RuntimeEntitySaveState> entities,
            IReadOnlyList<RuntimeCounterSaveState> globalCounters,
            IReadOnlyList<RuntimeModuleSaveState> moduleStates,
            IReadOnlyDictionary<string, string> metadata = null)
        {
            SchemaVersion = schemaVersion;
            CreatedAtUtc = createdAtUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc)
                : createdAtUtc.ToUniversalTime();
            FrameworkVersion = frameworkVersion ?? string.Empty;
            ConfigVersion = configVersion ?? string.Empty;
            ResourceCatalogVersion = resourceCatalogVersion ?? string.Empty;
            Frame = frame;
            Entities = CopyList(entities);
            GlobalCounters = CopyList(globalCounters);
            ModuleStates = CopyList(moduleStates);
            Metadata = CopyDictionary(metadata);
        }

        public int SchemaVersion { get; }
        public DateTime CreatedAtUtc { get; }
        public string FrameworkVersion { get; }
        public string ConfigVersion { get; }
        public string ResourceCatalogVersion { get; }
        public long Frame { get; }
        public IReadOnlyList<RuntimeEntitySaveState> Entities { get; }
        public IReadOnlyList<RuntimeCounterSaveState> GlobalCounters { get; }
        public IReadOnlyList<RuntimeModuleSaveState> ModuleStates { get; }
        public IReadOnlyDictionary<string, string> Metadata { get; }

        internal static ReadOnlyCollection<T> CopyList<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0)
            {
                return new ReadOnlyCollection<T>(new List<T>());
            }

            var copy = new List<T>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                copy.Add(source[i]);
            }

            return new ReadOnlyCollection<T>(copy);
        }

        internal static ReadOnlyDictionary<string, string> CopyDictionary(IReadOnlyDictionary<string, string> source)
        {
            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            if (source != null)
            {
                foreach (KeyValuePair<string, string> pair in source)
                {
                    copy[pair.Key ?? string.Empty] = pair.Value ?? string.Empty;
                }
            }

            return new ReadOnlyDictionary<string, string>(copy);
        }
    }

    public sealed class RuntimeEntitySaveState
    {
        public RuntimeEntitySaveState(
            int entityId,
            int definitionId,
            int teamId,
            bool isAlive,
            IReadOnlyList<RuntimeAttributeSaveState> attributes,
            IReadOnlyList<RuntimeBuffSaveState> buffs,
            IReadOnlyList<RuntimeModifierSaveState> modifiers,
            IReadOnlyList<RuntimeAbilitySaveState> abilities,
            IReadOnlyList<RuntimeCounterSaveState> counters,
            RuntimeCustomState customState = null)
        {
            EntityId = entityId;
            DefinitionId = definitionId;
            TeamId = teamId;
            IsAlive = isAlive;
            Attributes = RuntimeSaveState.CopyList(attributes);
            Buffs = RuntimeSaveState.CopyList(buffs);
            Modifiers = RuntimeSaveState.CopyList(modifiers);
            Abilities = RuntimeSaveState.CopyList(abilities);
            Counters = RuntimeSaveState.CopyList(counters);
            CustomState = customState;
        }

        public int EntityId { get; }
        public int DefinitionId { get; }
        public int TeamId { get; }
        public bool IsAlive { get; }
        public IReadOnlyList<RuntimeAttributeSaveState> Attributes { get; }
        public IReadOnlyList<RuntimeBuffSaveState> Buffs { get; }
        public IReadOnlyList<RuntimeModifierSaveState> Modifiers { get; }
        public IReadOnlyList<RuntimeAbilitySaveState> Abilities { get; }
        public IReadOnlyList<RuntimeCounterSaveState> Counters { get; }
        public RuntimeCustomState CustomState { get; }
    }

    public sealed class RuntimeAttributeSaveState
    {
        public RuntimeAttributeSaveState(int attributeId, double baseValue, string finalValuePolicy)
        {
            AttributeId = attributeId;
            BaseValue = baseValue;
            FinalValuePolicy = finalValuePolicy ?? string.Empty;
        }

        public int AttributeId { get; }
        public double BaseValue { get; }
        public string FinalValuePolicy { get; }
    }

    public sealed class RuntimeBuffSaveState
    {
        public RuntimeBuffSaveState(
            int buffId,
            long instanceId,
            int layer,
            double remainingTime,
            double duration,
            int sourceId,
            string configVersion,
            RuntimeCustomState customState = null)
        {
            BuffId = buffId;
            InstanceId = instanceId;
            Layer = layer;
            RemainingTime = remainingTime;
            Duration = duration;
            SourceId = sourceId;
            ConfigVersion = configVersion ?? string.Empty;
            CustomState = customState;
        }

        public int BuffId { get; }
        public long InstanceId { get; }
        public int Layer { get; }
        public double RemainingTime { get; }
        public double Duration { get; }
        public int SourceId { get; }
        public string ConfigVersion { get; }
        public RuntimeCustomState CustomState { get; }
    }

    public sealed class RuntimeModifierSaveState
    {
        public RuntimeModifierSaveState(
            int modifierId,
            long instanceId,
            int sourceId,
            int paramIndex,
            IReadOnlyList<RuntimeCounterSaveState> counters,
            RuntimeCustomState customState = null)
        {
            ModifierId = modifierId;
            InstanceId = instanceId;
            SourceId = sourceId;
            ParamIndex = paramIndex;
            Counters = RuntimeSaveState.CopyList(counters);
            CustomState = customState;
        }

        public int ModifierId { get; }
        public long InstanceId { get; }
        public int SourceId { get; }
        public int ParamIndex { get; }
        public IReadOnlyList<RuntimeCounterSaveState> Counters { get; }
        public RuntimeCustomState CustomState { get; }
    }

    public sealed class RuntimeAbilitySaveState
    {
        public RuntimeAbilitySaveState(
            int abilityId,
            double cooldownRemaining,
            int charges,
            long lastCastFrame,
            int sourceConfigId,
            RuntimeCustomState customState = null)
        {
            AbilityId = abilityId;
            CooldownRemaining = cooldownRemaining;
            Charges = charges;
            LastCastFrame = lastCastFrame;
            SourceConfigId = sourceConfigId;
            CustomState = customState;
        }

        public int AbilityId { get; }
        public double CooldownRemaining { get; }
        public int Charges { get; }
        public long LastCastFrame { get; }
        public int SourceConfigId { get; }
        public RuntimeCustomState CustomState { get; }
    }

    public sealed class RuntimeCounterSaveState
    {
        public RuntimeCounterSaveState(int counterId, int value)
        {
            CounterId = counterId;
            Value = value;
        }

        public int CounterId { get; }
        public int Value { get; }
    }

    public sealed class RuntimeCustomState
    {
        public RuntimeCustomState(string typeId, int schemaVersion, string payloadJson)
        {
            TypeId = typeId ?? string.Empty;
            SchemaVersion = schemaVersion;
            PayloadJson = payloadJson ?? string.Empty;
        }

        public string TypeId { get; }
        public int SchemaVersion { get; }
        public string PayloadJson { get; }
    }

    public sealed class RuntimeModuleSaveState
    {
        public RuntimeModuleSaveState(string moduleId, int schemaVersion, RuntimeCustomState customState)
        {
            ModuleId = moduleId ?? string.Empty;
            SchemaVersion = schemaVersion;
            CustomState = customState;
        }

        public string ModuleId { get; }
        public int SchemaVersion { get; }
        public RuntimeCustomState CustomState { get; }
    }

    public interface IRuntimeSaveStateProvider
    {
        RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState();
    }

    public interface IRuntimeSaveStateRestorer
    {
        RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState);
    }
}
