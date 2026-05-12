using System;
using System.Collections.Generic;
using MxFramework.Core.Handles;

namespace MxFramework.Runtime
{
    public sealed class RuntimeTimerScheduler : RuntimeModule, IRuntimeTimerScheduler
    {
        public const string DefaultModuleId = "runtime.timer-scheduler";
        public const int StateSchemaVersion = 1;
        private const double SecondsEpsilon = 0.000000000001d;

        private readonly StableHandleTable<TimerRecord> _handles = new StableHandleTable<TimerRecord>();
        private readonly List<TimerRecord> _timers = new List<TimerRecord>();
        private readonly List<TimerRecord> _due = new List<TimerRecord>();
        private RuntimeFrame _currentFrame = RuntimeFrame.Zero;
        private long _nextTimerId;
        private long _nextSequence;

        public RuntimeTimerScheduler(
            string moduleId = DefaultModuleId,
            RuntimeTickStage tickStage = RuntimeTickStage.Simulation,
            int priority = 0)
            : base(moduleId, tickStage, priority)
        {
        }

        public RuntimeFrame CurrentFrame => _currentFrame;
        public int PendingCount => _handles.ActiveCount;

        public RuntimeTimerHandle ScheduleFrames(long delayFrames, RuntimeTimerCallback callback, string traceId = "")
        {
            if (delayFrames < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(delayFrames), "Runtime timer frame delay cannot be negative.");
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return AddTimer(new TimerRecord(
                timerId: _nextTimerId++,
                sequence: _nextSequence++,
                kind: RuntimeTimerKind.Frames,
                targetFrame: AddFrames(_currentFrame.Value, delayFrames),
                remainingSeconds: 0d,
                intervalFrames: 0L,
                intervalSeconds: 0d,
                isRepeating: false,
                callback: callback,
                commandBuffer: null,
                command: default,
                traceId: traceId));
        }

        public RuntimeTimerHandle ScheduleSeconds(double delaySeconds, RuntimeTimerCallback callback, string traceId = "")
        {
            ValidateFiniteNonNegativeSeconds(delaySeconds, nameof(delaySeconds));

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return AddTimer(new TimerRecord(
                timerId: _nextTimerId++,
                sequence: _nextSequence++,
                kind: RuntimeTimerKind.Seconds,
                targetFrame: _currentFrame,
                remainingSeconds: delaySeconds,
                intervalFrames: 0L,
                intervalSeconds: 0d,
                isRepeating: false,
                callback: callback,
                commandBuffer: null,
                command: default,
                traceId: traceId));
        }

        public RuntimeTimerHandle ScheduleRepeatingFrames(long intervalFrames, RuntimeTimerCallback callback, string traceId = "")
        {
            if (intervalFrames <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalFrames), "Runtime repeating timer frame interval must be greater than zero.");
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return AddTimer(new TimerRecord(
                timerId: _nextTimerId++,
                sequence: _nextSequence++,
                kind: RuntimeTimerKind.Frames,
                targetFrame: AddFrames(_currentFrame.Value, intervalFrames),
                remainingSeconds: 0d,
                intervalFrames: intervalFrames,
                intervalSeconds: 0d,
                isRepeating: true,
                callback: callback,
                commandBuffer: null,
                command: default,
                traceId: traceId));
        }

        public RuntimeTimerHandle ScheduleCommand(
            long frameDelay,
            RuntimeCommandBuffer commandBuffer,
            RuntimeCommand command,
            string traceId = "",
            RuntimeScheduledCommandFramePolicy framePolicy = RuntimeScheduledCommandFramePolicy.NextFrame)
        {
            if (frameDelay < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(frameDelay), "Runtime command frame delay cannot be negative.");
            }

            if (commandBuffer == null)
            {
                throw new ArgumentNullException(nameof(commandBuffer));
            }

            RuntimeFrame targetFrame = AddFrames(_currentFrame.Value, frameDelay);
            RuntimeFrame commandFrame = ResolveCommandFrame(command.Frame, targetFrame, framePolicy);
            RuntimeCommand scheduledCommand = new RuntimeCommand(
                commandFrame,
                command.SourceId,
                command.CommandId,
                command.TargetId,
                command.Payload0,
                command.Payload1,
                command.Payload2,
                command.TraceId,
                command.Sequence);

            return AddTimer(new TimerRecord(
                timerId: _nextTimerId++,
                sequence: _nextSequence++,
                kind: RuntimeTimerKind.Command,
                targetFrame: targetFrame,
                remainingSeconds: 0d,
                intervalFrames: 0L,
                intervalSeconds: 0d,
                isRepeating: false,
                callback: null,
                commandBuffer: commandBuffer,
                command: scheduledCommand,
                traceId: string.IsNullOrEmpty(traceId) ? command.TraceId : traceId));
        }

        public bool Cancel(RuntimeTimerHandle handle)
        {
            if (!_handles.TryGet(handle.StableHandle, out TimerRecord record))
            {
                return false;
            }

            record.Cancelled = true;
            return _handles.Remove(handle.StableHandle);
        }

        public override void Tick(RuntimeTickContext context)
        {
            ValidateFiniteNonNegativeSeconds(context.DeltaTime, nameof(context.DeltaTime));

            _currentFrame = new RuntimeFrame(context.FrameIndex);

            CollectDueTimers(context);
            if (_due.Count == 0)
            {
                CleanupCancelled();
                return;
            }

            _due.Sort(CompareTimers);
            for (int i = 0; i < _due.Count; i++)
            {
                TimerRecord timer = _due[i];
                if (timer.Cancelled || !_handles.TryGet(timer.Handle.StableHandle, out TimerRecord current) || !ReferenceEquals(timer, current))
                {
                    continue;
                }

                FireTimer(timer, context);
            }

            _due.Clear();
            CleanupCancelled();
        }

        public RuntimeTimerSchedulerSnapshot CreateSnapshot()
        {
            var entries = new List<RuntimeTimerSnapshotEntry>();
            AppendSnapshotEntries(entries);
            return new RuntimeTimerSchedulerSnapshot(_currentFrame, entries);
        }

        public RuntimeTimerSchedulerState CreateState()
        {
            RuntimeTimerSchedulerStateSummary summary = CreateStateSummary();
            return new RuntimeTimerSchedulerState(
                summary.SchemaVersion,
                summary.CurrentFrame,
                summary.NextTimerId,
                summary.NextSequence,
                summary.Timers);
        }

        public RuntimeTimerSchedulerStateSummary CreateStateSummary()
        {
            var states = new List<RuntimeTimerStateSummary>();
            for (int i = 0; i < _timers.Count; i++)
            {
                TimerRecord timer = _timers[i];
                if (timer.Cancelled)
                {
                    continue;
                }

                states.Add(new RuntimeTimerStateSummary(
                    timer.TimerId,
                    timer.Sequence,
                    timer.Kind,
                    timer.Handle,
                    timer.TargetFrame.Value,
                    RemainingFrames(timer),
                    RemainingSeconds(timer),
                    timer.IntervalFrames,
                    timer.IntervalSeconds,
                    timer.IsRepeating,
                    timer.TraceId,
                    CommandSummary(timer)));
            }

            states.Sort(CompareStates);
            return new RuntimeTimerSchedulerStateSummary(StateSchemaVersion, _currentFrame.Value, _nextTimerId, _nextSequence, states);
        }

        private RuntimeTimerHandle AddTimer(TimerRecord record)
        {
            StableHandle stableHandle = _handles.Add(record);
            record.Handle = new RuntimeTimerHandle(stableHandle);
            _timers.Add(record);
            return record.Handle;
        }

        private void CollectDueTimers(RuntimeTickContext context)
        {
            _due.Clear();
            int timerCount = _timers.Count;
            for (int i = 0; i < timerCount; i++)
            {
                TimerRecord timer = _timers[i];
                if (timer.Cancelled)
                {
                    continue;
                }

                if (timer.Kind == RuntimeTimerKind.Seconds)
                {
                    timer.RemainingSeconds -= context.DeltaTime;
                    if (timer.RemainingSeconds <= SecondsEpsilon)
                    {
                        timer.TargetFrame = _currentFrame;
                        _due.Add(timer);
                    }
                }
                else if (timer.TargetFrame <= _currentFrame)
                {
                    _due.Add(timer);
                }
            }
        }

        private void FireTimer(TimerRecord timer, RuntimeTickContext context)
        {
            if (timer.Kind == RuntimeTimerKind.Command)
            {
                RuntimeCommandValidationResult result = timer.CommandBuffer.Enqueue(timer.Command);
                if (!result.Success)
                {
                    throw new InvalidOperationException("Scheduled runtime command was rejected: " + result.Error);
                }
            }
            else
            {
                timer.Callback(new RuntimeTimerContext(this, timer.Handle, _currentFrame, context.DeltaTime, timer.TraceId));
            }

            if (timer.IsRepeating && !timer.Cancelled && _handles.TryGet(timer.Handle.StableHandle, out _))
            {
                timer.TargetFrame = AddFrames(_currentFrame.Value, timer.IntervalFrames);
                timer.Sequence = _nextSequence++;
                return;
            }

            timer.Cancelled = true;
            _handles.Remove(timer.Handle.StableHandle);
        }

        private void AppendSnapshotEntries(List<RuntimeTimerSnapshotEntry> entries)
        {
            for (int i = 0; i < _timers.Count; i++)
            {
                TimerRecord timer = _timers[i];
                if (timer.Cancelled)
                {
                    continue;
                }

                entries.Add(new RuntimeTimerSnapshotEntry(
                    timer.Handle,
                    timer.Kind,
                    timer.TargetFrame,
                    RemainingFrames(timer),
                    RemainingSeconds(timer),
                    timer.IntervalFrames,
                    timer.IntervalSeconds,
                    timer.IsRepeating,
                    timer.TraceId));
            }

            entries.Sort(CompareSnapshotEntries);
        }

        private void CleanupCancelled()
        {
            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                if (_timers[i].Cancelled)
                {
                    _timers.RemoveAt(i);
                }
            }
        }

        private long RemainingFrames(TimerRecord timer)
        {
            if (timer.Kind == RuntimeTimerKind.Seconds)
            {
                return 0L;
            }

            long remaining = timer.TargetFrame.Value - _currentFrame.Value;
            return remaining > 0L ? remaining : 0L;
        }

        private double RemainingSeconds(TimerRecord timer)
        {
            if (timer.Kind != RuntimeTimerKind.Seconds)
            {
                return 0d;
            }

            return timer.RemainingSeconds > 0d ? timer.RemainingSeconds : 0d;
        }

        private static RuntimeFrame AddFrames(long frame, long frames)
        {
            if (long.MaxValue - frame < frames)
            {
                throw new InvalidOperationException("Runtime timer target frame overflow.");
            }

            return new RuntimeFrame(frame + frames);
        }

        private static int CompareTimers(TimerRecord left, TimerRecord right)
        {
            int frame = left.TargetFrame.CompareTo(right.TargetFrame);
            if (frame != 0)
            {
                return frame;
            }

            int sequence = left.Sequence.CompareTo(right.Sequence);
            if (sequence != 0)
            {
                return sequence;
            }

            return left.TimerId.CompareTo(right.TimerId);
        }

        private static int CompareSnapshotEntries(RuntimeTimerSnapshotEntry left, RuntimeTimerSnapshotEntry right)
        {
            int frame = left.TargetFrame.CompareTo(right.TargetFrame);
            if (frame != 0)
            {
                return frame;
            }

            int index = left.Handle.Index.CompareTo(right.Handle.Index);
            if (index != 0)
            {
                return index;
            }

            return left.Handle.Generation.CompareTo(right.Handle.Generation);
        }

        private static int CompareStates(RuntimeTimerStateSummary left, RuntimeTimerStateSummary right)
        {
            int frame = left.TargetFrame.CompareTo(right.TargetFrame);
            if (frame != 0)
            {
                return frame;
            }

            int sequence = left.Sequence.CompareTo(right.Sequence);
            if (sequence != 0)
            {
                return sequence;
            }

            return left.TimerId.CompareTo(right.TimerId);
        }

        private static string CommandSummary(TimerRecord timer)
        {
            if (timer.Kind != RuntimeTimerKind.Command)
            {
                return string.Empty;
            }

            RuntimeCommand command = timer.Command;
            return "Frame=" + command.Frame
                + " SourceId=" + command.SourceId
                + " CommandId=" + command.CommandId
                + " TargetId=" + command.TargetId
                + " TraceId=" + command.TraceId;
        }

        private static RuntimeFrame ResolveCommandFrame(
            RuntimeFrame originalFrame,
            RuntimeFrame targetFrame,
            RuntimeScheduledCommandFramePolicy framePolicy)
        {
            switch (framePolicy)
            {
                case RuntimeScheduledCommandFramePolicy.DueFrame:
                    return targetFrame;
                case RuntimeScheduledCommandFramePolicy.NextFrame:
                    return targetFrame.Next();
                case RuntimeScheduledCommandFramePolicy.PreserveOriginalFrame:
                    return originalFrame;
                default:
                    throw new ArgumentOutOfRangeException(nameof(framePolicy), framePolicy, "Unsupported scheduled command frame policy.");
            }
        }

        private static void ValidateFiniteNonNegativeSeconds(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Seconds value must be finite and non-negative.");
            }
        }

        private sealed class TimerRecord
        {
            public TimerRecord(
                long timerId,
                long sequence,
                RuntimeTimerKind kind,
                RuntimeFrame targetFrame,
                double remainingSeconds,
                long intervalFrames,
                double intervalSeconds,
                bool isRepeating,
                RuntimeTimerCallback callback,
                RuntimeCommandBuffer commandBuffer,
                RuntimeCommand command,
                string traceId)
            {
                TimerId = timerId;
                Sequence = sequence;
                Kind = kind;
                TargetFrame = targetFrame;
                RemainingSeconds = remainingSeconds;
                IntervalFrames = intervalFrames;
                IntervalSeconds = intervalSeconds;
                IsRepeating = isRepeating;
                Callback = callback;
                CommandBuffer = commandBuffer;
                Command = command;
                TraceId = traceId ?? string.Empty;
            }

            public long TimerId { get; }
            public long Sequence { get; set; }
            public RuntimeTimerHandle Handle { get; set; }
            public RuntimeTimerKind Kind { get; }
            public RuntimeFrame TargetFrame { get; set; }
            public double RemainingSeconds { get; set; }
            public long IntervalFrames { get; }
            public double IntervalSeconds { get; }
            public bool IsRepeating { get; }
            public RuntimeTimerCallback Callback { get; }
            public RuntimeCommandBuffer CommandBuffer { get; }
            public RuntimeCommand Command { get; }
            public string TraceId { get; }
            public bool Cancelled { get; set; }
        }
    }
}
