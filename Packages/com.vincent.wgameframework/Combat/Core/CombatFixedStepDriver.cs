using System;
using MxFramework.Runtime;

namespace MxFramework.Combat.Core
{
    public sealed class CombatFixedStepDriver
    {
        private const double Epsilon = 0.000000000001d;

        private readonly CombatFrameClock _clock;
        private double _accumulatedTime;
        private bool _hasCurrentBatch;
        private CombatFixedStepBatch _currentBatch;

        public CombatFixedStepDriver()
            : this(new CombatFrameClock())
        {
        }

        public CombatFixedStepDriver(CombatStepConfig config)
            : this(new CombatFrameClock(config))
        {
        }

        public CombatFixedStepDriver(CombatFrameClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public CombatFrameClock Clock => _clock;

        public CombatStepConfig Config => _clock.Config;

        public CombatFrame CurrentFrame => _clock.CurrentFrame;

        public double FixedDeltaTime => 1d / Config.TicksPerSecond;

        public double AccumulatedTime => _accumulatedTime;

        public bool HasCurrentBatch => _hasCurrentBatch;

        public CombatFixedStepBatch CurrentBatch => _currentBatch;

        public CombatFixedStepBatch Advance(RuntimeTickContext context)
        {
            return Advance(context.FrameIndex, context.DeltaTime, context.ElapsedTime);
        }

        public CombatFixedStepBatch Advance(long runtimeFrameIndex, double runtimeDeltaTime, double runtimeElapsedTime = 0d)
        {
            if (runtimeFrameIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(runtimeFrameIndex), "Runtime frame index cannot be negative.");
            }

            if (!IsFiniteNonNegative(runtimeDeltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(runtimeDeltaTime), "Runtime delta time must be finite and non-negative.");
            }

            if (!IsFiniteNonNegative(runtimeElapsedTime))
            {
                throw new ArgumentOutOfRangeException(nameof(runtimeElapsedTime), "Runtime elapsed time must be finite and non-negative.");
            }

            if (_hasCurrentBatch && _currentBatch.RuntimeFrameIndex == runtimeFrameIndex)
            {
                return _currentBatch;
            }

            double fixedDeltaTime = FixedDeltaTime;
            CombatFrame startFrame = _clock.CurrentFrame;
            _accumulatedTime += runtimeDeltaTime;

            int stepCount = 0;
            while (stepCount < Config.MaxStepsPerUpdate && _accumulatedTime + Epsilon >= fixedDeltaTime)
            {
                _accumulatedTime -= fixedDeltaTime;
                stepCount++;
            }

            if (_accumulatedTime < Epsilon)
            {
                _accumulatedTime = 0d;
            }

            bool maxStepLimitReached = _accumulatedTime + Epsilon >= fixedDeltaTime;
            CombatFrame endFrame = _clock.Step(stepCount);
            _currentBatch = new CombatFixedStepBatch(
                runtimeFrameIndex,
                runtimeDeltaTime,
                runtimeElapsedTime,
                fixedDeltaTime,
                startFrame,
                endFrame,
                stepCount,
                _accumulatedTime,
                maxStepLimitReached);
            _hasCurrentBatch = true;
            return _currentBatch;
        }

        public void Reset()
        {
            _clock.Reset();
            _accumulatedTime = 0d;
            _currentBatch = default;
            _hasCurrentBatch = false;
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return value >= 0d && !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
