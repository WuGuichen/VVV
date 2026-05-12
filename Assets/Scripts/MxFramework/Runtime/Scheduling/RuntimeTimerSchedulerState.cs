using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Runtime
{
    public class RuntimeTimerSchedulerStateSummary
    {
        private readonly ReadOnlyCollection<RuntimeTimerStateSummary> _timers;

        public RuntimeTimerSchedulerStateSummary(
            int schemaVersion,
            long currentFrame,
            long nextTimerId,
            long nextSequence,
            IReadOnlyList<RuntimeTimerStateSummary> timers)
        {
            SchemaVersion = schemaVersion;
            CurrentFrame = currentFrame;
            NextTimerId = nextTimerId;
            NextSequence = nextSequence;
            _timers = new ReadOnlyCollection<RuntimeTimerStateSummary>(
                timers != null ? new List<RuntimeTimerStateSummary>(timers) : new List<RuntimeTimerStateSummary>());
        }

        public int SchemaVersion { get; }
        public long CurrentFrame { get; }
        public long NextTimerId { get; }
        public long NextSequence { get; }
        public IReadOnlyList<RuntimeTimerStateSummary> Timers => _timers;
        public bool IsRestorable => false;
    }

    public sealed class RuntimeTimerSchedulerState : RuntimeTimerSchedulerStateSummary
    {
        public RuntimeTimerSchedulerState(
            int schemaVersion,
            long currentFrame,
            long nextTimerId,
            long nextSequence,
            IReadOnlyList<RuntimeTimerStateSummary> timers)
            : base(schemaVersion, currentFrame, nextTimerId, nextSequence, timers)
        {
        }
    }

    public class RuntimeTimerStateSummary
    {
        public RuntimeTimerStateSummary(
            long timerId,
            long sequence,
            RuntimeTimerKind kind,
            RuntimeTimerHandle handle,
            long targetFrame,
            long remainingFrames,
            double remainingSeconds,
            long intervalFrames,
            double intervalSeconds,
            bool isRepeating,
            string traceId,
            string commandSummary)
        {
            TimerId = timerId;
            Sequence = sequence;
            Kind = kind;
            Handle = handle;
            TargetFrame = targetFrame;
            RemainingFrames = remainingFrames;
            RemainingSeconds = remainingSeconds;
            IntervalFrames = intervalFrames;
            IntervalSeconds = intervalSeconds;
            IsRepeating = isRepeating;
            TraceId = traceId ?? string.Empty;
            CommandSummary = commandSummary ?? string.Empty;
        }

        public long TimerId { get; }
        public long Sequence { get; }
        public RuntimeTimerKind Kind { get; }
        public RuntimeTimerHandle Handle { get; }
        public long TargetFrame { get; }
        public long RemainingFrames { get; }
        public double RemainingSeconds { get; }
        public long IntervalFrames { get; }
        public double IntervalSeconds { get; }
        public bool IsRepeating { get; }
        public string TraceId { get; }
        public string CommandSummary { get; }
        public bool IsRestorable => false;
    }

    public sealed class RuntimeTimerState : RuntimeTimerStateSummary
    {
        public RuntimeTimerState(
            long timerId,
            long sequence,
            RuntimeTimerKind kind,
            RuntimeTimerHandle handle,
            long targetFrame,
            long remainingFrames,
            double remainingSeconds,
            long intervalFrames,
            double intervalSeconds,
            bool isRepeating,
            string traceId,
            string commandSummary)
            : base(
                timerId,
                sequence,
                kind,
                handle,
                targetFrame,
                remainingFrames,
                remainingSeconds,
                intervalFrames,
                intervalSeconds,
                isRepeating,
                traceId,
                commandSummary)
        {
        }
    }
}
