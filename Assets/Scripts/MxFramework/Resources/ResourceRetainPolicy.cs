namespace MxFramework.Resources
{
    public enum ResourceRetainMode
    {
        None,
        Timed,
        KeepAlive,
        Budgeted
    }

    public sealed class ResourceRetainPolicy
    {
        public ResourceRetainPolicy(
            ResourceRetainMode mode,
            float durationSeconds = 0f,
            int frameCount = 0,
            long maxRetainedBytes = 0)
        {
            Mode = mode;
            DurationSeconds = durationSeconds < 0f ? 0f : durationSeconds;
            FrameCount = frameCount < 0 ? 0 : frameCount;
            MaxRetainedBytes = maxRetainedBytes < 0 ? 0 : maxRetainedBytes;
        }

        public static ResourceRetainPolicy None { get; } = new ResourceRetainPolicy(ResourceRetainMode.None);
        public static ResourceRetainPolicy KeepAlive { get; } = new ResourceRetainPolicy(ResourceRetainMode.KeepAlive);

        public ResourceRetainMode Mode { get; }
        public float DurationSeconds { get; }
        public int FrameCount { get; }
        public long MaxRetainedBytes { get; }

        public static ResourceRetainPolicy Timed(float durationSeconds = 0f, int frameCount = 0)
        {
            return new ResourceRetainPolicy(ResourceRetainMode.Timed, durationSeconds, frameCount);
        }

        public static ResourceRetainPolicy Budgeted(long maxRetainedBytes)
        {
            return new ResourceRetainPolicy(ResourceRetainMode.Budgeted, maxRetainedBytes: maxRetainedBytes);
        }
    }
}
