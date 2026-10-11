using System;
using System.Collections.Generic;

namespace MxFramework.Runtime
{
    public sealed class RuntimeRateLimiter
    {
        private readonly Dictionary<int, long> _lastAllowedFrames = new Dictionary<int, long>();
        private readonly Dictionary<int, double> _lastAllowedSeconds = new Dictionary<int, double>();

        public bool AllowFrame(int id, RuntimeFrame frame, long intervalFrames)
        {
            ValidateFrameInterval(intervalFrames);

            if (intervalFrames == 0L)
            {
                _lastAllowedFrames[id] = frame.Value;
                return true;
            }

            if (!_lastAllowedFrames.TryGetValue(id, out long lastAllowedFrame))
            {
                _lastAllowedFrames[id] = frame.Value;
                return true;
            }

            if (frame.Value < lastAllowedFrame || frame.Value - lastAllowedFrame < intervalFrames)
            {
                return false;
            }

            _lastAllowedFrames[id] = frame.Value;
            return true;
        }

        public bool AllowSeconds(int id, double elapsedSeconds, double intervalSeconds)
        {
            ValidateSeconds(nameof(elapsedSeconds), elapsedSeconds);
            ValidateSeconds(nameof(intervalSeconds), intervalSeconds);

            if (intervalSeconds == 0d)
            {
                _lastAllowedSeconds[id] = elapsedSeconds;
                return true;
            }

            if (!_lastAllowedSeconds.TryGetValue(id, out double lastAllowedSeconds))
            {
                _lastAllowedSeconds[id] = elapsedSeconds;
                return true;
            }

            if (elapsedSeconds < lastAllowedSeconds || elapsedSeconds - lastAllowedSeconds < intervalSeconds)
            {
                return false;
            }

            _lastAllowedSeconds[id] = elapsedSeconds;
            return true;
        }

        public void Reset(int id)
        {
            _lastAllowedFrames.Remove(id);
            _lastAllowedSeconds.Remove(id);
        }

        public void ResetFrame(int id)
        {
            _lastAllowedFrames.Remove(id);
        }

        public void ResetSeconds(int id)
        {
            _lastAllowedSeconds.Remove(id);
        }

        public void Clear()
        {
            _lastAllowedFrames.Clear();
            _lastAllowedSeconds.Clear();
        }

        private static void ValidateFrameInterval(long intervalFrames)
        {
            if (intervalFrames < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalFrames), "Rate limit interval cannot be negative.");
            }
        }

        private static void ValidateSeconds(string parameterName, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Seconds value must be finite and non-negative.");
            }
        }
    }
}
