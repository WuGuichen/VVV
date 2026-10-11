using System;
using System.Diagnostics;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestFixedTickDriver
    {
        private const double Epsilon = 0.0000001d;
        private double _accumulatorSeconds;

        public int MaxStepsPerUpdate { get; set; } = 8;
        public Func<bool> CanStepPredicate { get; set; }
        public double AccumulatorSeconds => _accumulatorSeconds;
        public int LastExecutedSteps { get; private set; }
        public double LastCpuMilliseconds { get; private set; }
        public double DiscardedWallSeconds { get; private set; }
        public bool LastBudgetExhausted { get; private set; }

        // Host pacing only. Every admitted tick remains atomic and keeps its fixed delta.
        public int StepInteractive(double deltaTime, double fixedDeltaTime, Action<double> tick,
            int maxSteps = 1, double budgetMilliseconds = 4d)
            => Advance(deltaTime, fixedDeltaTime, tick, Math.Max(1, maxSteps), budgetMilliseconds, true);

        public int Step(double deltaTime, double fixedDeltaTime, Action<double> tick)
            => Advance(deltaTime, fixedDeltaTime, tick, Math.Max(1, MaxStepsPerUpdate), 0d, false);

        private int Advance(double deltaTime, double fixedDeltaTime, Action<double> tick,
            int maxSteps, double budgetMilliseconds, bool preserveFraction)
        {
            if (tick == null)
                throw new ArgumentNullException(nameof(tick));
            if (fixedDeltaTime <= 0d || double.IsNaN(fixedDeltaTime) || double.IsInfinity(fixedDeltaTime))
                throw new ArgumentOutOfRangeException(nameof(fixedDeltaTime));
            if (double.IsNaN(deltaTime) || double.IsInfinity(deltaTime)) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (deltaTime < 0d)
                deltaTime = 0d;

            _accumulatorSeconds += deltaTime;
            long started = Stopwatch.GetTimestamp();
            LastBudgetExhausted = false;
            int steps = 0;
            while (_accumulatorSeconds + Epsilon >= fixedDeltaTime && steps < maxSteps)
            {
                if (CanStepPredicate != null && !CanStepPredicate())
                {
                    _accumulatorSeconds = 0d;
                    break;
                }
                _accumulatorSeconds -= fixedDeltaTime;
                if (_accumulatorSeconds < 0d)
                    _accumulatorSeconds = 0d;
                tick(fixedDeltaTime);
                steps++;
                if (budgetMilliseconds > 0d && (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency >= budgetMilliseconds)
                { LastBudgetExhausted = true; break; }
            }

            // Also discard fractional time when the final admitted step closed the gate.
            if (CanStepPredicate != null && !CanStepPredicate())
                _accumulatorSeconds = 0d;

            if ((steps == maxSteps || LastBudgetExhausted) && _accumulatorSeconds + Epsilon >= fixedDeltaTime)
            {
                double remainder = preserveFraction ? Math.Max(0d, _accumulatorSeconds - Math.Floor((_accumulatorSeconds + Epsilon) / fixedDeltaTime) * fixedDeltaTime) : 0d;
                DiscardedWallSeconds += _accumulatorSeconds - remainder;
                _accumulatorSeconds = remainder;
                LastBudgetExhausted = true;
            }
            LastExecutedSteps = steps;
            LastCpuMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;

            return steps;
        }

        public void Reset()
        {
            _accumulatorSeconds = 0d;
            LastExecutedSteps = 0; LastCpuMilliseconds = 0; LastBudgetExhausted = false; DiscardedWallSeconds = 0;
        }
    }
}
