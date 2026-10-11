namespace MxFramework.Gameplay
{
    public readonly struct GameplayComponentWorldSnapshot
    {
        public GameplayComponentWorldSnapshot(
            int aliveEntityCount,
            int componentStoreCount,
            int pendingEventCount)
        {
            AliveEntityCount = aliveEntityCount;
            ComponentStoreCount = componentStoreCount;
            PendingEventCount = pendingEventCount;
        }

        public int AliveEntityCount { get; }
        public int ComponentStoreCount { get; }
        public int PendingEventCount { get; }
    }
}
