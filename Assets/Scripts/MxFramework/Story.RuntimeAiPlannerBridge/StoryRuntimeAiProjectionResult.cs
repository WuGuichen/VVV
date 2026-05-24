namespace MxFramework.Story.RuntimeAiPlannerBridge
{
    public readonly struct StoryRuntimeAiProjectionResult
    {
        public StoryRuntimeAiProjectionResult(
            int projectedCount,
            int missingFactCount,
            int skippedFactCount,
            int unsupportedFactCount)
        {
            ProjectedCount = projectedCount;
            MissingFactCount = missingFactCount;
            SkippedFactCount = skippedFactCount;
            UnsupportedFactCount = unsupportedFactCount;
        }

        public int ProjectedCount { get; }
        public int MissingFactCount { get; }
        public int SkippedFactCount { get; }
        public int UnsupportedFactCount { get; }
        public bool Success => UnsupportedFactCount == 0;
    }
}
