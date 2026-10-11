using System;
using System.Collections.Generic;

namespace MxFramework.Runtime
{
    public sealed class RuntimeDebouncer
    {
        private readonly Dictionary<int, long> _lastMarkedFrames = new Dictionary<int, long>();
        private readonly Dictionary<int, double> _lastMarkedSeconds = new Dictionary<int, double>();

        public void MarkFrame(int id, RuntimeFrame frame)
        {
            _lastMarkedFrames[id] = frame.Value;
        }

        public bool IsReadyFrame(int id, RuntimeFrame frame, long intervalFrames)
        {
            ValidateFrameInterval(intervalFrames);

            if (!_lastMarkedFrames.TryGetValue(id, out long lastMarkedFrame))
            {
                return false;
            }

            if (frame.Value < lastMarkedFrame)
            {
                return false;
            }

            return frame.Value - lastMarkedFrame >= intervalFrames;
        }

        public bool ConsumeReadyFrame(int id, RuntimeFrame frame, long intervalFrames)
        {
            if (!IsReadyFrame(id, frame, intervalFrames))
            {
                return false;
            }

            _lastMarkedFrames.Remove(id);
            return true;
        }

        public void MarkSeconds(int id, double elapsedSeconds)
        {
            ValidateSeconds(nameof(elapsedSeconds), elapsedSeconds);
            _lastMarkedSeconds[id] = elapsedSeconds;
        }

        public bool IsReadySeconds(int id, double elapsedSeconds, double intervalSeconds)
        {
            ValidateSeconds(nameof(elapsedSeconds), elapsedSeconds);
            ValidateSeconds(nameof(intervalSeconds), intervalSeconds);

            if (!_lastMarkedSeconds.TryGetValue(id, out double lastMarkedSeconds))
            {
                return false;
            }

            if (elapsedSeconds < lastMarkedSeconds)
            {
                return false;
            }

            return elapsedSeconds - lastMarkedSeconds >= intervalSeconds;
        }

        public bool ConsumeReadySeconds(int id, double elapsedSeconds, double intervalSeconds)
        {
            if (!IsReadySeconds(id, elapsedSeconds, intervalSeconds))
            {
                return false;
            }

            _lastMarkedSeconds.Remove(id);
            return true;
        }

        public void Reset(int id)
        {
            _lastMarkedFrames.Remove(id);
            _lastMarkedSeconds.Remove(id);
        }

        public void ResetFrame(int id)
        {
            _lastMarkedFrames.Remove(id);
        }

        public void ResetSeconds(int id)
        {
            _lastMarkedSeconds.Remove(id);
        }

        public void Clear()
        {
            _lastMarkedFrames.Clear();
            _lastMarkedSeconds.Clear();
        }

        private static void ValidateFrameInterval(long intervalFrames)
        {
            if (intervalFrames < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalFrames), "Debounce interval cannot be negative.");
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
