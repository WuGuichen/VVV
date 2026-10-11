using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;

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
        private readonly RuntimeHashDigest _payloadDigest;

        [JsonConstructor]
        public RuntimeCustomState(
            int numericTypeId,
            [JsonProperty("typeId")] string typeIdLabel,
            int schemaVersion,
            string payloadJson,
            bool preparePayloadDigest = false)
        {
            if (numericTypeId <= 0) throw new ArgumentOutOfRangeException(nameof(numericTypeId));
            if (schemaVersion < 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            NumericTypeId = numericTypeId;
            TypeId = typeIdLabel ?? string.Empty;
            SchemaVersion = schemaVersion;
            PayloadJson = payloadJson;
            HasPreparedPayloadDigest = preparePayloadDigest;
            _payloadDigest = preparePayloadDigest ? RuntimeHashDigest.ComputeContentDigest(payloadJson) : default;
        }

        public int NumericTypeId { get; }
        /// <summary>Diagnostic label. NumericTypeId is the persisted definition identity.</summary>
        public string TypeId { get; }
        public int SchemaVersion { get; }
        public string PayloadJson { get; }
        [JsonIgnore] public bool HasPreparedPayloadDigest { get; }
        [JsonIgnore]
        public RuntimeHashDigest PreparedPayloadDigest => HasPreparedPayloadDigest ? _payloadDigest
            : throw new InvalidOperationException("Prepare custom payload content before state hashing.");

        private RuntimeCustomState(RuntimeCustomState source)
        {
            NumericTypeId = source.NumericTypeId;
            TypeId = source.TypeId;
            SchemaVersion = source.SchemaVersion;
            PayloadJson = source.PayloadJson;
            HasPreparedPayloadDigest = source.HasPreparedPayloadDigest;
            _payloadDigest = source._payloadDigest;
        }

        /// <remarks>LowFreqAlloc. Snapshot copy retains any prepared digest without reading the text again.</remarks>
        public RuntimeCustomState Copy() => new RuntimeCustomState(this);

        /// <remarks>LowFreqAlloc. Call only at payload mutation/import boundaries; existing snapshots remain immutable.</remarks>
        public RuntimeCustomState PreparePayloadDigest() => HasPreparedPayloadDigest ? this
            : new RuntimeCustomState(NumericTypeId, TypeId, SchemaVersion, PayloadJson, true);
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
