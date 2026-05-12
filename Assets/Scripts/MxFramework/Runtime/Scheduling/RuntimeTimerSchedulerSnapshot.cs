using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Runtime
{
    public enum RuntimeTimerKind
    {
        Frames = 0,
        Seconds = 1,
        Command = 2
    }

    public sealed class RuntimeTimerSchedulerSnapshot
    {
        private readonly ReadOnlyCollection<RuntimeTimerSnapshotEntry> _entries;

        public RuntimeTimerSchedulerSnapshot(RuntimeFrame currentFrame, IReadOnlyList<RuntimeTimerSnapshotEntry> entries)
        {
            CurrentFrame = currentFrame;
            _entries = new ReadOnlyCollection<RuntimeTimerSnapshotEntry>(
                entries != null ? new List<RuntimeTimerSnapshotEntry>(entries) : new List<RuntimeTimerSnapshotEntry>());
        }

        public RuntimeFrame CurrentFrame { get; }
        public int PendingCount => _entries.Count;
        public IReadOnlyList<RuntimeTimerSnapshotEntry> Entries => _entries;
    }

    public readonly struct RuntimeTimerSnapshotEntry
    {
        public RuntimeTimerSnapshotEntry(
            RuntimeTimerHandle handle,
            RuntimeTimerKind kind,
            RuntimeFrame targetFrame,
            long remainingFrames,
            double remainingSeconds,
            long intervalFrames,
            double intervalSeconds,
            bool isRepeating,
            string traceId)
        {
            Handle = handle;
            Kind = kind;
            TargetFrame = targetFrame;
            RemainingFrames = remainingFrames;
            RemainingSeconds = remainingSeconds;
            IntervalFrames = intervalFrames;
            IntervalSeconds = intervalSeconds;
            IsRepeating = isRepeating;
            TraceId = traceId ?? string.Empty;
        }

        public RuntimeTimerHandle Handle { get; }
        public RuntimeTimerKind Kind { get; }
        public RuntimeFrame TargetFrame { get; }
        public long RemainingFrames { get; }
        public double RemainingSeconds { get; }
        public long IntervalFrames { get; }
        public double IntervalSeconds { get; }
        public bool IsRepeating { get; }
        public string TraceId { get; }
    }
}
