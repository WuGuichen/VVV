using System;

namespace MxFramework.Combat.Physics
{
    public readonly struct CombatPhysicsLayerMask : IEquatable<CombatPhysicsLayerMask>
    {
        public static readonly CombatPhysicsLayerMask None = new CombatPhysicsLayerMask(0);
        public static readonly CombatPhysicsLayerMask All = new CombatPhysicsLayerMask(-1);

        public CombatPhysicsLayerMask(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool ContainsLayer(int layer)
        {
            ValidateLayer(layer);
            return (Value & (1 << layer)) != 0;
        }

        public CombatPhysicsLayerMask AddLayer(int layer)
        {
            ValidateLayer(layer);
            return new CombatPhysicsLayerMask(Value | (1 << layer));
        }

        public bool Equals(CombatPhysicsLayerMask other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatPhysicsLayerMask other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public static CombatPhysicsLayerMask FromLayer(int layer)
        {
            ValidateLayer(layer);
            return new CombatPhysicsLayerMask(1 << layer);
        }

        private static void ValidateLayer(int layer)
        {
            if (layer < 0 || layer > 31)
            {
                throw new ArgumentOutOfRangeException(nameof(layer), "Combat physics layer must be in range 0-31.");
            }
        }
    }
}
