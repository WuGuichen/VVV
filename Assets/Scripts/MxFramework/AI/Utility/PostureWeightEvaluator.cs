using System;
using System.Collections.Generic;

namespace MxFramework.AI
{
    public readonly struct PressureImpactData : IEquatable<PressureImpactData>
    {
        public PressureImpactData(
            int impactForce,
            bool isHighPoiseDamage,
            bool isDefensiveRecovery = false)
        {
            if (impactForce < 0)
                throw new ArgumentOutOfRangeException(nameof(impactForce));

            ImpactForce = impactForce;
            IsHighPoiseDamage = isHighPoiseDamage;
            IsDefensiveRecovery = isDefensiveRecovery;
        }

        public int ImpactForce { get; }
        public bool IsHighPoiseDamage { get; }
        public bool IsDefensiveRecovery { get; }

        public bool Equals(PressureImpactData other)
        {
            return ImpactForce == other.ImpactForce
                && IsHighPoiseDamage == other.IsHighPoiseDamage
                && IsDefensiveRecovery == other.IsDefensiveRecovery;
        }

        public override bool Equals(object obj)
        {
            return obj is PressureImpactData other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ImpactForce;
                hash = (hash * 397) ^ IsHighPoiseDamage.GetHashCode();
                hash = (hash * 397) ^ IsDefensiveRecovery.GetHashCode();
                return hash;
            }
        }
    }

    public static class PostureWeightEvaluator
    {
        public static float GetActionWeightModifier(
            int actionId,
            IAiWorldState worldState,
            IReadOnlyDictionary<int, PressureImpactData> impactData)
        {
            if (worldState == null)
                throw new ArgumentNullException(nameof(worldState));

            if (impactData == null || !impactData.TryGetValue(actionId, out PressureImpactData data))
                return 1f;

            int selfGuardBand = ReadNonNegativeInt(worldState, RuntimeAiPressureFactKeys.SelfGuardBand);
            int selfPostureBand = ReadNonNegativeInt(worldState, RuntimeAiPressureFactKeys.SelfPostureBand);
            int targetPostureBand = ReadNonNegativeInt(worldState, RuntimeAiPressureFactKeys.TargetPostureBand);
            bool selfArmorBroken = ReadBool(worldState, RuntimeAiPressureFactKeys.SelfArmorBroken);

            float modifier;
            if (data.IsDefensiveRecovery)
            {
                modifier = 1f
                    + (selfGuardBand * 0.12f)
                    + (selfPostureBand * 0.10f)
                    + (data.ImpactForce * 0.005f);
                if (selfArmorBroken)
                    modifier += 0.30f;
                if (targetPostureBand >= 3)
                    modifier -= 0.10f;
            }
            else
            {
                modifier = 1f
                    + (targetPostureBand * 0.15f)
                    + (data.ImpactForce * 0.01f)
                    - (selfGuardBand * 0.10f)
                    - (selfPostureBand * 0.08f);
                if (data.IsHighPoiseDamage && targetPostureBand >= ExploitPostureWeaknessGoal.DefaultActivationBand)
                    modifier += 0.35f;
                if (selfArmorBroken)
                    modifier -= 0.25f;
            }

            return Clamp(modifier, 0.25f, 2.5f);
        }

        private static int ReadNonNegativeInt(IAiWorldState worldState, AiFactKey key)
        {
            return worldState.TryGetValue(key, out int value) && value > 0 ? value : 0;
        }

        private static bool ReadBool(IAiWorldState worldState, AiFactKey key)
        {
            if (worldState.TryGetValue(key, out bool value))
                return value;

            return worldState.TryGetValue(key, out int intValue) && intValue == 1;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
