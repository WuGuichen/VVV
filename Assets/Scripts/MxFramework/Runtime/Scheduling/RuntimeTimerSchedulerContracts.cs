namespace MxFramework.Runtime
{
    public delegate void RuntimeTimerCallback(RuntimeTimerContext context);

    public interface IRuntimeTimerScheduler
    {
        RuntimeTimerHandle ScheduleFrames(long delayFrames, RuntimeTimerCallback callback, string traceId = "");
        RuntimeTimerHandle ScheduleSeconds(double delaySeconds, RuntimeTimerCallback callback, string traceId = "");
        RuntimeTimerHandle ScheduleRepeatingFrames(long intervalFrames, RuntimeTimerCallback callback, string traceId = "");
        RuntimeTimerHandle ScheduleCommand(
            long frameDelay,
            RuntimeCommandBuffer commandBuffer,
            RuntimeCommand command,
            string traceId = "",
            RuntimeScheduledCommandFramePolicy framePolicy = RuntimeScheduledCommandFramePolicy.NextFrame);
        bool Cancel(RuntimeTimerHandle handle);
        RuntimeTimerSchedulerSnapshot CreateSnapshot();
    }

    public enum RuntimeScheduledCommandFramePolicy
    {
        DueFrame = 0,
        NextFrame = 1,
        PreserveOriginalFrame = 2
    }

    public readonly struct RuntimeTimerContext
    {
        public RuntimeTimerContext(
            IRuntimeTimerScheduler scheduler,
            RuntimeTimerHandle handle,
            RuntimeFrame frame,
            double deltaTime,
            string traceId)
        {
            Scheduler = scheduler;
            Handle = handle;
            Frame = frame;
            DeltaTime = deltaTime;
            TraceId = traceId ?? string.Empty;
        }

        public IRuntimeTimerScheduler Scheduler { get; }
        public RuntimeTimerHandle Handle { get; }
        public RuntimeFrame Frame { get; }
        public double DeltaTime { get; }
        public string TraceId { get; }
    }
}
