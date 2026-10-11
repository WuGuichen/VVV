using System;
using System.Collections.Generic;

namespace MxFramework.AI
{
    public readonly struct AiSensorRegistration
    {
        public AiSensorRegistration(
            IAiSensor sensor,
            string sensorId,
            int intervalFrames,
            int priority,
            int lastTickFrame)
        {
            Sensor = sensor;
            SensorId = sensorId ?? string.Empty;
            IntervalFrames = intervalFrames;
            Priority = priority;
            LastTickFrame = lastTickFrame;
        }

        public IAiSensor Sensor { get; }
        public string SensorId { get; }
        public int IntervalFrames { get; }
        public int Priority { get; }
        public int LastTickFrame { get; }
    }

    public readonly struct AiSensorManagerDiagnostics
    {
        public AiSensorManagerDiagnostics(
            int lastFrame,
            int executedCount,
            int skippedCount,
            int failedCount,
            string lastFailureSensorId,
            string lastFailureMessage)
        {
            LastFrame = lastFrame;
            ExecutedCount = executedCount;
            SkippedCount = skippedCount;
            FailedCount = failedCount;
            LastFailureSensorId = lastFailureSensorId ?? string.Empty;
            LastFailureMessage = lastFailureMessage ?? string.Empty;
        }

        public int LastFrame { get; }
        public int ExecutedCount { get; }
        public int SkippedCount { get; }
        public int FailedCount { get; }
        public string LastFailureSensorId { get; }
        public string LastFailureMessage { get; }
    }

    public interface IAiSensorManager
    {
        int Count { get; }
        IReadOnlyList<AiSensorRegistration> Registrations { get; }
        AiSensorManagerDiagnostics Diagnostics { get; }
        void Register(IAiSensor sensor, int intervalFrames = 1, int priority = 0, string sensorId = "");
        bool Unregister(IAiSensor sensor);
        void Clear();
        /// <remarks>NoAlloc after registration when sensors themselves are NoAlloc.</remarks>
        AiSensorManagerDiagnostics Tick(IAiAgent agent, IAiWorldState worldState, int frame);
    }

    public sealed class AiSensorManager : IAiSensorManager
    {
        private readonly List<SensorEntry> _entries = new List<SensorEntry>();
        private int _nextOrder;

        public AiSensorManager(bool throwOnSensorException = false)
        {
            ThrowOnSensorException = throwOnSensorException;
            Diagnostics = new AiSensorManagerDiagnostics(-1, 0, 0, 0, string.Empty, string.Empty);
        }

        public bool ThrowOnSensorException { get; }
        public int Count => _entries.Count;
        public AiSensorManagerDiagnostics Diagnostics { get; private set; }

        public IReadOnlyList<AiSensorRegistration> Registrations
        {
            get
            {
                var registrations = new AiSensorRegistration[_entries.Count];
                for (int i = 0; i < _entries.Count; i++)
                {
                    SensorEntry entry = _entries[i];
                    registrations[i] = new AiSensorRegistration(
                        entry.Sensor,
                        entry.SensorId,
                        entry.IntervalFrames,
                        entry.Priority,
                        entry.LastTickFrame);
                }

                return registrations;
            }
        }

        public void Register(IAiSensor sensor, int intervalFrames = 1, int priority = 0, string sensorId = "")
        {
            if (sensor == null)
                throw new ArgumentNullException(nameof(sensor));
            if (intervalFrames <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalFrames));

            _entries.Add(new SensorEntry(
                sensor,
                string.IsNullOrWhiteSpace(sensorId) ? sensor.GetType().Name : sensorId,
                intervalFrames,
                priority,
                _nextOrder++));
            _entries.Sort(SensorEntryComparer.Instance);
        }

        public bool Unregister(IAiSensor sensor)
        {
            if (sensor == null)
                return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (!ReferenceEquals(_entries[i].Sensor, sensor))
                    continue;

                _entries.RemoveAt(i);
                return true;
            }

            return false;
        }

        public void Clear()
        {
            _entries.Clear();
        }

        /// <remarks>NoAlloc after registration when sensors themselves are NoAlloc.</remarks>
        public AiSensorManagerDiagnostics Tick(IAiAgent agent, IAiWorldState worldState, int frame)
        {
            if (agent == null)
                throw new ArgumentNullException(nameof(agent));
            if (worldState == null)
                throw new ArgumentNullException(nameof(worldState));
            if (frame < 0)
                throw new ArgumentOutOfRangeException(nameof(frame));

            int executed = 0;
            int skipped = 0;
            int failed = 0;
            string failureSensorId = string.Empty;
            string failureMessage = string.Empty;

            for (int i = 0; i < _entries.Count; i++)
            {
                SensorEntry entry = _entries[i];
                if (!IsDue(entry, frame))
                {
                    skipped++;
                    continue;
                }

                entry.LastTickFrame = frame;
                executed++;
                try
                {
                    entry.Sensor.Sense(agent, worldState);
                }
                catch (Exception ex)
                {
                    failed++;
                    failureSensorId = entry.SensorId;
                    failureMessage = ex.Message;
                    if (ThrowOnSensorException)
                        throw;
                }
            }

            Diagnostics = new AiSensorManagerDiagnostics(
                frame,
                executed,
                skipped,
                failed,
                failureSensorId,
                failureMessage);
            return Diagnostics;
        }

        private static bool IsDue(SensorEntry entry, int frame)
        {
            return entry.LastTickFrame < 0 || frame - entry.LastTickFrame >= entry.IntervalFrames;
        }

        private sealed class SensorEntry
        {
            public SensorEntry(
                IAiSensor sensor,
                string sensorId,
                int intervalFrames,
                int priority,
                int order)
            {
                Sensor = sensor;
                SensorId = sensorId ?? string.Empty;
                IntervalFrames = intervalFrames;
                Priority = priority;
                Order = order;
                LastTickFrame = -1;
            }

            public IAiSensor Sensor { get; }
            public string SensorId { get; }
            public int IntervalFrames { get; }
            public int Priority { get; }
            public int Order { get; }
            public int LastTickFrame { get; set; }
        }

        private sealed class SensorEntryComparer : IComparer<SensorEntry>
        {
            public static readonly SensorEntryComparer Instance = new SensorEntryComparer();

            public int Compare(SensorEntry left, SensorEntry right)
            {
                int priority = right.Priority.CompareTo(left.Priority);
                return priority != 0 ? priority : left.Order.CompareTo(right.Order);
            }
        }
    }
}
