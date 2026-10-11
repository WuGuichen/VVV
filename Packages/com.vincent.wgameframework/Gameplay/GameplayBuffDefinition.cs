using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayBuffId : IComparable<GameplayBuffId>, IEquatable<GameplayBuffId>
    {
        public static readonly GameplayBuffId None = new GameplayBuffId(0);

        public GameplayBuffId(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Gameplay buff id cannot be negative.");

            Value = value;
        }

        public int Value { get; }
        public bool IsNone => Value == 0;
        public bool IsValid => Value > 0;

        public int CompareTo(GameplayBuffId other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(GameplayBuffId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayBuffId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public enum GameplayBuffStackPolicy
    {
        RefreshDuration = 0,
        Replace = 1,
        Stack = 2
    }

    public enum GameplayBuffEffectKind
    {
        None = 0,
        AttributeModifier = 1,
        Status = 2
    }

    public readonly struct GameplayBuffEffectDefinition : IEquatable<GameplayBuffEffectDefinition>
    {
        private const int MaxEffectIdForGeneratedIds = 999;

        private GameplayBuffEffectDefinition(
            int effectId,
            GameplayBuffEffectKind kind,
            int attributeId,
            int addValue,
            GameplayStatusId statusId)
        {
            if (effectId <= 0 || effectId > MaxEffectIdForGeneratedIds)
                throw new ArgumentOutOfRangeException(nameof(effectId), "Gameplay buff effect id must be in the 1..999 range.");
            if (!Enum.IsDefined(typeof(GameplayBuffEffectKind), kind) || kind == GameplayBuffEffectKind.None)
                throw new ArgumentOutOfRangeException(nameof(kind), "Gameplay buff effect kind is not supported.");

            EffectId = effectId;
            Kind = kind;
            AttributeId = attributeId;
            AddValue = addValue;
            StatusId = statusId;

            if (Kind == GameplayBuffEffectKind.AttributeModifier && AttributeId <= 0)
                throw new ArgumentOutOfRangeException(nameof(attributeId), "Gameplay buff attribute modifier effect requires a positive attribute id.");
            if (Kind == GameplayBuffEffectKind.Status && !StatusId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(statusId), "Gameplay buff status effect requires a valid status id.");
        }

        public int EffectId { get; }
        public GameplayBuffEffectKind Kind { get; }
        public int AttributeId { get; }
        public int AddValue { get; }
        public GameplayStatusId StatusId { get; }

        public static GameplayBuffEffectDefinition AttributeModifier(int effectId, int attributeId, int addValue)
        {
            return new GameplayBuffEffectDefinition(
                effectId,
                GameplayBuffEffectKind.AttributeModifier,
                attributeId,
                addValue,
                GameplayStatusId.None);
        }

        public static GameplayBuffEffectDefinition Status(int effectId, GameplayStatusId statusId)
        {
            return new GameplayBuffEffectDefinition(
                effectId,
                GameplayBuffEffectKind.Status,
                0,
                0,
                statusId);
        }

        public bool Equals(GameplayBuffEffectDefinition other)
        {
            return EffectId == other.EffectId
                && Kind == other.Kind
                && AttributeId == other.AttributeId
                && AddValue == other.AddValue
                && StatusId.Equals(other.StatusId);
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayBuffEffectDefinition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = EffectId;
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ AttributeId;
                hash = (hash * 397) ^ AddValue;
                hash = (hash * 397) ^ StatusId.GetHashCode();
                return hash;
            }
        }
    }

    public readonly struct GameplayBuffDefinition : IEquatable<GameplayBuffDefinition>
    {
        private readonly GameplayBuffEffectDefinition[] _effects;

        public GameplayBuffDefinition(
            int buffId,
            string stableId,
            long defaultDurationFrames,
            int maxStackCount,
            GameplayBuffStackPolicy stackPolicy,
            GameplayBuffEffectDefinition[] effects,
            int applyStackCount = 1,
            bool isPermanent = false)
        {
            if (buffId <= 0)
                throw new ArgumentOutOfRangeException(nameof(buffId), "Gameplay buff definition id must be greater than zero.");
            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException("Gameplay buff definition stable id cannot be null or empty.", nameof(stableId));
            if (maxStackCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxStackCount), "Gameplay buff definition max stack count must be greater than zero.");
            if (applyStackCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(applyStackCount), "Gameplay buff definition apply stack count must be greater than zero.");
            if (applyStackCount > maxStackCount)
                throw new ArgumentOutOfRangeException(nameof(applyStackCount), "Gameplay buff definition apply stack count cannot exceed max stack count.");
            if (!Enum.IsDefined(typeof(GameplayBuffStackPolicy), stackPolicy))
                throw new ArgumentOutOfRangeException(nameof(stackPolicy), "Gameplay buff stack policy is not supported.");
            if (!isPermanent && defaultDurationFrames <= 0L)
                throw new ArgumentOutOfRangeException(nameof(defaultDurationFrames), "Non-permanent gameplay buff definitions require a positive default duration.");
            if (defaultDurationFrames < 0L)
                throw new ArgumentOutOfRangeException(nameof(defaultDurationFrames), "Gameplay buff definition default duration cannot be negative.");

            BuffId = buffId;
            StableId = stableId;
            DefaultDurationFrames = defaultDurationFrames;
            MaxStackCount = maxStackCount;
            ApplyStackCount = applyStackCount;
            StackPolicy = stackPolicy;
            IsPermanent = isPermanent;
            _effects = CopySortedEffects(effects);
        }

        public int BuffId { get; }
        public string StableId { get; }
        public long DefaultDurationFrames { get; }
        public int MaxStackCount { get; }
        public int ApplyStackCount { get; }
        public GameplayBuffStackPolicy StackPolicy { get; }
        public bool IsPermanent { get; }
        public int EffectCount => _effects == null ? 0 : _effects.Length;

        public GameplayBuffEffectDefinition[] GetEffects()
        {
            if (_effects == null || _effects.Length == 0)
                return Array.Empty<GameplayBuffEffectDefinition>();

            var copy = new GameplayBuffEffectDefinition[_effects.Length];
            Array.Copy(_effects, copy, _effects.Length);
            return copy;
        }

        public GameplayBuffEffectDefinition GetEffectAt(int index)
        {
            if (_effects == null || index < 0 || index >= _effects.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _effects[index];
        }

        public bool Equals(GameplayBuffDefinition other)
        {
            if (BuffId != other.BuffId
                || !string.Equals(StableId, other.StableId, StringComparison.Ordinal)
                || DefaultDurationFrames != other.DefaultDurationFrames
                || MaxStackCount != other.MaxStackCount
                || ApplyStackCount != other.ApplyStackCount
                || StackPolicy != other.StackPolicy
                || IsPermanent != other.IsPermanent
                || EffectCount != other.EffectCount)
                return false;

            for (int i = 0; i < EffectCount; i++)
            {
                if (!_effects[i].Equals(other._effects[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayBuffDefinition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = BuffId;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(StableId);
                hash = (hash * 397) ^ DefaultDurationFrames.GetHashCode();
                hash = (hash * 397) ^ MaxStackCount;
                hash = (hash * 397) ^ ApplyStackCount;
                hash = (hash * 397) ^ (int)StackPolicy;
                hash = (hash * 397) ^ IsPermanent.GetHashCode();
                for (int i = 0; i < EffectCount; i++)
                    hash = (hash * 397) ^ _effects[i].GetHashCode();
                return hash;
            }
        }

        private static GameplayBuffEffectDefinition[] CopySortedEffects(GameplayBuffEffectDefinition[] effects)
        {
            if (effects == null || effects.Length == 0)
                return Array.Empty<GameplayBuffEffectDefinition>();

            var copy = new GameplayBuffEffectDefinition[effects.Length];
            Array.Copy(effects, copy, effects.Length);
            Array.Sort(copy, CompareEffects);
            for (int i = 1; i < copy.Length; i++)
            {
                if (copy[i - 1].EffectId == copy[i].EffectId)
                    throw new ArgumentException("Gameplay buff definition cannot contain duplicate effect ids.", nameof(effects));
            }

            return copy;
        }

        private static int CompareEffects(GameplayBuffEffectDefinition left, GameplayBuffEffectDefinition right)
        {
            return left.EffectId.CompareTo(right.EffectId);
        }
    }

    public sealed class GameplayBuffDefinitionRegistry
    {
        private readonly GameplayBuffDefinition[] _definitions;

        public GameplayBuffDefinitionRegistry(params GameplayBuffDefinition[] definitions)
        {
            _definitions = CopySorted(definitions);
        }

        public int Count => _definitions.Length;

        public bool TryGet(int buffId, out GameplayBuffDefinition definition)
        {
            if (buffId <= 0)
            {
                definition = default;
                return false;
            }

            int left = 0;
            int right = _definitions.Length - 1;
            while (left <= right)
            {
                int mid = left + ((right - left) / 2);
                int id = _definitions[mid].BuffId;
                if (id == buffId)
                {
                    definition = _definitions[mid];
                    return true;
                }

                if (id < buffId)
                    left = mid + 1;
                else
                    right = mid - 1;
            }

            definition = default;
            return false;
        }

        public GameplayBuffDefinition[] ToArray()
        {
            if (_definitions.Length == 0)
                return Array.Empty<GameplayBuffDefinition>();

            var copy = new GameplayBuffDefinition[_definitions.Length];
            Array.Copy(_definitions, copy, _definitions.Length);
            return copy;
        }

        public int ComputeStableHash()
        {
            unchecked
            {
                int hash = _definitions.Length;
                for (int i = 0; i < _definitions.Length; i++)
                    hash = (hash * 397) ^ _definitions[i].GetHashCode();
                return hash;
            }
        }

        private static GameplayBuffDefinition[] CopySorted(GameplayBuffDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
                return Array.Empty<GameplayBuffDefinition>();

            var copy = new GameplayBuffDefinition[definitions.Length];
            Array.Copy(definitions, copy, definitions.Length);
            Array.Sort(copy, CompareDefinitions);
            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i].BuffId <= 0)
                    throw new ArgumentException("Gameplay buff definition registry cannot contain default definitions.", nameof(definitions));
                if (i > 0 && copy[i - 1].BuffId == copy[i].BuffId)
                    throw new ArgumentException("Gameplay buff definition registry cannot contain duplicate buff ids.", nameof(definitions));
            }

            return copy;
        }

        private static int CompareDefinitions(GameplayBuffDefinition left, GameplayBuffDefinition right)
        {
            return left.BuffId.CompareTo(right.BuffId);
        }
    }

    public sealed class GameplayBuffDefinitionRegistryHashContributor : IRuntimeHashContributor
    {
        public const string StableContributorId = "mxframework.gameplay.buff-definitions";
        private const int RegistryHashField = 1;

        private readonly GameplayBuffDefinitionRegistry _registry;

        public GameplayBuffDefinitionRegistryHashContributor(GameplayBuffDefinitionRegistry registry)
            : this(StableContributorId, registry)
        {
        }

        public GameplayBuffDefinitionRegistryHashContributor(string contributorId, GameplayBuffDefinitionRegistry registry)
        {
            if (string.IsNullOrEmpty(contributorId))
                throw new ArgumentException("Gameplay buff definition hash contributor id cannot be null or empty.", nameof(contributorId));

            DiagnosticContributorId = contributorId;
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public int ContributorId => RuntimeHashContributorIds.GameplayBuffDefinitions;
        public int HashSchemaId => RuntimeHashSchemaIds.GameplayBuffDefinitions;
        public int HashSchemaVersion => RuntimeHashSchemaIds.Version1;
        public string DiagnosticContributorId { get; }

        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));

            accumulator.AddInt(RegistryHashField, _registry.ComputeStableHash());
        }
    }
}
