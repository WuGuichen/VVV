using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;

namespace MxFramework.Combat.GameplayBridge
{
    /// <summary>
    /// Defines how a combat-space aura refreshes one component-native gameplay buff.
    /// </summary>
    public readonly struct CombatGameplayAuraBuffDefinition : IEquatable<CombatGameplayAuraBuffDefinition>
    {
        public CombatGameplayAuraBuffDefinition(
            int auraId,
            int buffId,
            Fix64 radius,
            CombatPhysicsLayerMask layerMask,
            GameplayTargetRelationFilter relationFilter,
            bool includeCaster = true)
        {
            if (auraId <= 0)
                throw new ArgumentOutOfRangeException(nameof(auraId), "Combat gameplay aura id must be greater than zero.");
            if (buffId <= 0)
                throw new ArgumentOutOfRangeException(nameof(buffId), "Combat gameplay aura buff id must be greater than zero.");
            if (radius <= Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(radius), "Combat gameplay aura radius must be greater than zero.");
            if (!Enum.IsDefined(typeof(GameplayTargetRelationFilter), relationFilter))
                throw new ArgumentOutOfRangeException(nameof(relationFilter), "Combat gameplay aura relation filter is not supported.");

            AuraId = auraId;
            BuffId = buffId;
            Radius = radius;
            LayerMask = layerMask;
            RelationFilter = relationFilter;
            IncludeCaster = includeCaster;
        }

        public int AuraId { get; }
        public int BuffId { get; }
        public Fix64 Radius { get; }
        public CombatPhysicsLayerMask LayerMask { get; }
        public GameplayTargetRelationFilter RelationFilter { get; }
        public bool IncludeCaster { get; }

        public bool Equals(CombatGameplayAuraBuffDefinition other)
        {
            return AuraId == other.AuraId
                && BuffId == other.BuffId
                && Radius.Equals(other.Radius)
                && LayerMask.Equals(other.LayerMask)
                && RelationFilter == other.RelationFilter
                && IncludeCaster == other.IncludeCaster;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatGameplayAuraBuffDefinition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AuraId;
                hash = (hash * 397) ^ BuffId;
                hash = (hash * 397) ^ Radius.GetHashCode();
                hash = (hash * 397) ^ LayerMask.GetHashCode();
                hash = (hash * 397) ^ (int)RelationFilter;
                hash = (hash * 397) ^ IncludeCaster.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// Stable lookup table for combat gameplay aura buff definitions.
    /// </summary>
    public sealed class CombatGameplayAuraBuffDefinitionRegistry
    {
        private readonly CombatGameplayAuraBuffDefinition[] _definitions;

        public CombatGameplayAuraBuffDefinitionRegistry(params CombatGameplayAuraBuffDefinition[] definitions)
        {
            _definitions = CopySorted(definitions);
        }

        public int Count => _definitions.Length;

        public bool TryGet(int auraId, out CombatGameplayAuraBuffDefinition definition)
        {
            if (auraId <= 0)
            {
                definition = default;
                return false;
            }

            int left = 0;
            int right = _definitions.Length - 1;
            while (left <= right)
            {
                int mid = left + ((right - left) / 2);
                int id = _definitions[mid].AuraId;
                if (id == auraId)
                {
                    definition = _definitions[mid];
                    return true;
                }

                if (id < auraId)
                    left = mid + 1;
                else
                    right = mid - 1;
            }

            definition = default;
            return false;
        }

        public CombatGameplayAuraBuffDefinition[] ToArray()
        {
            if (_definitions.Length == 0)
                return Array.Empty<CombatGameplayAuraBuffDefinition>();

            var copy = new CombatGameplayAuraBuffDefinition[_definitions.Length];
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

        private static CombatGameplayAuraBuffDefinition[] CopySorted(CombatGameplayAuraBuffDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
                return Array.Empty<CombatGameplayAuraBuffDefinition>();

            var copy = new CombatGameplayAuraBuffDefinition[definitions.Length];
            Array.Copy(definitions, copy, definitions.Length);
            Array.Sort(copy, CompareDefinitions);
            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i].AuraId <= 0)
                    throw new ArgumentException("Combat gameplay aura definition registry cannot contain default definitions.", nameof(definitions));
                if (i > 0 && copy[i - 1].AuraId == copy[i].AuraId)
                    throw new ArgumentException("Combat gameplay aura definition registry cannot contain duplicate aura ids.", nameof(definitions));
            }

            return copy;
        }

        private static int CompareDefinitions(
            CombatGameplayAuraBuffDefinition left,
            CombatGameplayAuraBuffDefinition right)
        {
            return left.AuraId.CompareTo(right.AuraId);
        }
    }

    /// <summary>
    /// Resolves the combat-space center point for a caster-following gameplay aura.
    /// </summary>
    public interface ICombatGameplayAuraAnchorProvider
    {
        bool TryGetAuraCenter(CombatEntityId sourceCombatId, out FixVector3 center);
    }
}


