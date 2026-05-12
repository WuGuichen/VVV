namespace MxFramework.Gameplay
{
    public readonly struct GameplaySystemPipelineSnapshot
    {
        public GameplaySystemPipelineSnapshot(int systemCount, int enabledSystemCount)
        {
            SystemCount = systemCount;
            EnabledSystemCount = enabledSystemCount;
        }

        public int SystemCount { get; }
        public int EnabledSystemCount { get; }
    }
}
