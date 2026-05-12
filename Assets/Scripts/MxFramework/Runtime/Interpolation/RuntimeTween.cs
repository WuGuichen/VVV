using System;

namespace MxFramework.Runtime
{
    /// <summary>
    /// Presentation-only tween helper for view, UI, and diagnostics. Do not feed tween output into replay/hash authoritative runtime state
    /// unless the caller records the result as deterministic input.
    /// </summary>
    public struct RuntimeTween
    {
        public RuntimeTween(float from, float to, float duration)
            : this(from, to, duration, RuntimeEasing.Linear)
        {
        }

        public RuntimeTween(float from, float to, float duration, RuntimeEasing easing)
        {
            RuntimeFloatInterpolator.ValidateFinite(from, nameof(from));
            RuntimeFloatInterpolator.ValidateFinite(to, nameof(to));
            ValidateDuration(duration);
            ValidateEasing(easing);

            From = from;
            To = to;
            Duration = duration;
            Easing = easing;
            Elapsed = 0f;
            Value = duration == 0f ? to : from;
        }

        public float From { get; }
        public float To { get; }
        public float Duration { get; }
        public RuntimeEasing Easing { get; }
        public float Elapsed { get; private set; }
        public float Value { get; private set; }
        public bool IsComplete => Duration == 0f || Elapsed >= Duration;
        public float Progress => Duration == 0f ? 1f : RuntimeEasingFunctions.Clamp01(Elapsed / Duration);

        public float Tick(float delta)
        {
            ValidateDelta(delta);

            if (IsComplete)
            {
                Value = To;
                return Value;
            }

            Elapsed += delta;
            if (Elapsed >= Duration)
            {
                Elapsed = Duration;
            }

            Value = RuntimeFloatInterpolator.Lerp(From, To, Progress, Easing);
            return Value;
        }

        public RuntimeTween Reset()
        {
            return new RuntimeTween(From, To, Duration, Easing);
        }

        private static void ValidateDuration(float duration)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "Runtime tween duration must be finite and non-negative.");
            }
        }

        private static void ValidateDelta(float delta)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(delta), delta, "Runtime tween delta must be finite and non-negative.");
            }
        }

        private static void ValidateEasing(RuntimeEasing easing)
        {
            switch (easing)
            {
                case RuntimeEasing.Linear:
                case RuntimeEasing.EaseIn:
                case RuntimeEasing.EaseOut:
                case RuntimeEasing.EaseInOut:
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(easing), easing, "Unsupported runtime easing mode.");
            }
        }
    }
}
