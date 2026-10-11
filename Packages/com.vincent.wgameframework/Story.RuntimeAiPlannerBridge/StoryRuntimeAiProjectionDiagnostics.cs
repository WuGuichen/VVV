using System;
using System.Collections.Generic;
using MxFramework.AI;

namespace MxFramework.Story.RuntimeAiPlannerBridge
{
    public enum StoryRuntimeAiProjectionDiagnosticCode
    {
        None = 0,
        MissingStoryFact = 1,
        UnsupportedStoryValueKind = 2,
        SkippedUnlistedStoryFact = 3,
        InvalidMapping = 4
    }

    public readonly struct StoryRuntimeAiProjectionDiagnostic
    {
        public StoryRuntimeAiProjectionDiagnostic(
            StoryRuntimeAiProjectionDiagnosticCode code,
            StoryFactKey storyKey,
            AiFactKey aiKey,
            StoryValueKind valueKind,
            string message)
        {
            Code = code;
            StoryKey = storyKey;
            AiKey = aiKey;
            ValueKind = valueKind;
            Message = message ?? string.Empty;
        }

        public StoryRuntimeAiProjectionDiagnosticCode Code { get; }
        public StoryFactKey StoryKey { get; }
        public AiFactKey AiKey { get; }
        public StoryValueKind ValueKind { get; }
        public string Message { get; }
        public bool IsNone => Code == StoryRuntimeAiProjectionDiagnosticCode.None;

        public static StoryRuntimeAiProjectionDiagnostic None =>
            new StoryRuntimeAiProjectionDiagnostic(
                StoryRuntimeAiProjectionDiagnosticCode.None,
                default,
                default,
                StoryValueKind.None,
                string.Empty);
    }

    public sealed class StoryRuntimeAiProjectionDiagnostics
    {
        public const int DefaultRecentCapacity = 32;

        private readonly int _recentCapacity;
        private readonly List<StoryRuntimeAiProjectionDiagnostic> _recent;

        public StoryRuntimeAiProjectionDiagnostics(int recentCapacity = DefaultRecentCapacity)
        {
            _recentCapacity = recentCapacity < 1 ? 1 : recentCapacity;
            _recent = new List<StoryRuntimeAiProjectionDiagnostic>(_recentCapacity);
        }

        public int MissingFactCount { get; private set; }
        public int UnsupportedFactCount { get; private set; }
        public int SkippedFactCount { get; private set; }
        public int InvalidMappingCount { get; private set; }
        public IReadOnlyList<StoryRuntimeAiProjectionDiagnostic> Recent => _recent;

        public void Record(StoryRuntimeAiProjectionDiagnostic diagnostic)
        {
            if (diagnostic.IsNone)
                return;

            switch (diagnostic.Code)
            {
                case StoryRuntimeAiProjectionDiagnosticCode.MissingStoryFact:
                    MissingFactCount++;
                    break;
                case StoryRuntimeAiProjectionDiagnosticCode.UnsupportedStoryValueKind:
                    UnsupportedFactCount++;
                    break;
                case StoryRuntimeAiProjectionDiagnosticCode.SkippedUnlistedStoryFact:
                    SkippedFactCount++;
                    break;
                case StoryRuntimeAiProjectionDiagnosticCode.InvalidMapping:
                    InvalidMappingCount++;
                    break;
            }

            if (_recent.Count == _recentCapacity)
                _recent.RemoveAt(0);

            _recent.Add(diagnostic);
        }

        public StoryRuntimeAiProjectionDiagnosticSnapshot CreateSnapshot()
        {
            return new StoryRuntimeAiProjectionDiagnosticSnapshot(
                MissingFactCount,
                UnsupportedFactCount,
                SkippedFactCount,
                InvalidMappingCount,
                _recent.ToArray());
        }
    }

    public readonly struct StoryRuntimeAiProjectionDiagnosticSnapshot
    {
        public StoryRuntimeAiProjectionDiagnosticSnapshot(
            int missingFactCount,
            int unsupportedFactCount,
            int skippedFactCount,
            int invalidMappingCount,
            IReadOnlyList<StoryRuntimeAiProjectionDiagnostic> recent)
        {
            MissingFactCount = missingFactCount;
            UnsupportedFactCount = unsupportedFactCount;
            SkippedFactCount = skippedFactCount;
            InvalidMappingCount = invalidMappingCount;
            Recent = recent ?? Array.Empty<StoryRuntimeAiProjectionDiagnostic>();
        }

        public int MissingFactCount { get; }
        public int UnsupportedFactCount { get; }
        public int SkippedFactCount { get; }
        public int InvalidMappingCount { get; }
        public IReadOnlyList<StoryRuntimeAiProjectionDiagnostic> Recent { get; }
    }
}
