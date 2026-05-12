namespace MxFramework.Runtime
{
    public readonly struct RuntimeEventQueueSnapshot
    {
        public RuntimeEventQueueSnapshot(
            int pendingCount,
            RuntimeFrame oldestFrame,
            RuntimeFrame newestFrame,
            long nextSequence,
            string eventTypeName)
        {
            PendingCount = pendingCount;
            OldestFrame = oldestFrame;
            NewestFrame = newestFrame;
            NextSequence = nextSequence;
            EventTypeName = eventTypeName ?? string.Empty;
        }

        public int PendingCount { get; }
        public bool HasPending => PendingCount > 0;
        public RuntimeFrame OldestFrame { get; }
        public RuntimeFrame NewestFrame { get; }
        public long NextSequence { get; }
        public string EventTypeName { get; }
    }
}
