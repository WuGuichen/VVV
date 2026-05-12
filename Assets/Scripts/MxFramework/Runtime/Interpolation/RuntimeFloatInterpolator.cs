using System;

namespace MxFramework.Runtime
{
    public static class RuntimeFloatInterpolator
    {
        public static float Lerp(float from, float to, float t)
        {
            return Lerp(from, to, t, RuntimeEasing.Linear);
        }

        public static float Lerp(float from, float to, float t, RuntimeEasing easing)
        {
            ValidateFinite(from, nameof(from));
            ValidateFinite(to, nameof(to));

            var eased = RuntimeEasingFunctions.Evaluate(easing, t);
            return from + ((to - from) * eased);
        }

        internal static void ValidateFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Interpolation value must be finite.");
            }
        }
    }
}
