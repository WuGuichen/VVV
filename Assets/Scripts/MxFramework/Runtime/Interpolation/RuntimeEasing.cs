using System;

namespace MxFramework.Runtime
{
    public enum RuntimeEasing
    {
        Linear = 0,
        EaseIn = 1,
        EaseOut = 2,
        EaseInOut = 3
    }

    public static class RuntimeEasingFunctions
    {
        public static float Evaluate(RuntimeEasing easing, float t)
        {
            var clamped = Clamp01(t);

            switch (easing)
            {
                case RuntimeEasing.Linear:
                    return Linear(clamped);
                case RuntimeEasing.EaseIn:
                    return EaseIn(clamped);
                case RuntimeEasing.EaseOut:
                    return EaseOut(clamped);
                case RuntimeEasing.EaseInOut:
                    return EaseInOut(clamped);
                default:
                    throw new ArgumentOutOfRangeException(nameof(easing), easing, "Unsupported runtime easing mode.");
            }
        }

        public static float Linear(float t)
        {
            return Clamp01(t);
        }

        public static float EaseIn(float t)
        {
            var clamped = Clamp01(t);
            return clamped * clamped;
        }

        public static float EaseOut(float t)
        {
            var clamped = Clamp01(t);
            return 1f - ((1f - clamped) * (1f - clamped));
        }

        public static float EaseInOut(float t)
        {
            var clamped = Clamp01(t);
            if (clamped < 0.5f)
            {
                return 2f * clamped * clamped;
            }

            var inverse = -2f * clamped + 2f;
            return 1f - (inverse * inverse * 0.5f);
        }

        internal static float Clamp01(float value)
        {
            if (float.IsNaN(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Interpolation value cannot be NaN.");
            }

            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }
    }
}
