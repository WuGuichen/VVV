using System;
using System.Collections.Generic;

namespace MxFramework.Story.GameplayBridge
{
    public enum StoryGameplayBridgeDiagnosticCode
    {
        None = 0,
        InvalidEntityRef = 1,
        MissingEntityRef = 2,
        StaleEntityRef = 3,
        UnsupportedEntityRefKind = 4,
        MissingModifierContextTarget = 10,
        ConditionResolverFailed = 11,
        ConditionEvaluationFailed = 12,
        InvalidEffectIntent = 20,
        UnsupportedEffectIntent = 21,
        UnsupportedBuffEffect = 22,
        CommandEnqueueFailed = 23,
        FrameOverflow = 24
    }

    public readonly struct StoryGameplayBridgeDiagnostic
    {
        public StoryGameplayBridgeDiagnostic(
            StoryGameplayBridgeDiagnosticCode code,
            string message,
            StoryGameplayEntityRef entityRef = default,
            int commandId = 0)
        {
            Code = code;
            Message = message ?? string.Empty;
            EntityRef = entityRef;
            CommandId = commandId;
        }

        public StoryGameplayBridgeDiagnosticCode Code { get; }
        public string Message { get; }
        public StoryGameplayEntityRef EntityRef { get; }
        public int CommandId { get; }
        public bool IsNone => Code == StoryGameplayBridgeDiagnosticCode.None;

        public static StoryGameplayBridgeDiagnostic None =>
            new StoryGameplayBridgeDiagnostic(StoryGameplayBridgeDiagnosticCode.None, string.Empty);
    }

    public sealed class StoryGameplayBridgeDiagnostics
    {
        public const int DefaultRecentCapacity = 32;

        private readonly int _recentCapacity;
        private readonly List<StoryGameplayBridgeDiagnostic> _recent;

        public StoryGameplayBridgeDiagnostics(int recentCapacity = DefaultRecentCapacity)
        {
            _recentCapacity = recentCapacity < 1 ? 1 : recentCapacity;
            _recent = new List<StoryGameplayBridgeDiagnostic>(_recentCapacity);
        }

        public int ResolvedEntityCount { get; private set; }
        public int UnresolvedEntityCount { get; private set; }
        public int EnqueuedCommandCount { get; private set; }
        public int RejectedEffectCount { get; private set; }
        public int ConditionFailureCount { get; private set; }

        public void RecordResolvedEntity()
        {
            ResolvedEntityCount++;
        }

        public void RecordUnresolvedEntity(StoryGameplayBridgeDiagnostic diagnostic)
        {
            UnresolvedEntityCount++;
            Record(diagnostic);
        }

        public void RecordEnqueuedCommand()
        {
            EnqueuedCommandCount++;
        }

        public void RecordRejectedEffect(StoryGameplayBridgeDiagnostic diagnostic)
        {
            RejectedEffectCount++;
            Record(diagnostic);
        }

        public void RecordConditionFailure(StoryGameplayBridgeDiagnostic diagnostic)
        {
            ConditionFailureCount++;
            Record(diagnostic);
        }

        public void Record(StoryGameplayBridgeDiagnostic diagnostic)
        {
            if (diagnostic.IsNone)
                return;

            if (_recent.Count == _recentCapacity)
                _recent.RemoveAt(0);

            _recent.Add(diagnostic);
        }

        public StoryGameplayBridgeDiagnosticSnapshot CreateSnapshot()
        {
            return new StoryGameplayBridgeDiagnosticSnapshot(
                ResolvedEntityCount,
                UnresolvedEntityCount,
                EnqueuedCommandCount,
                RejectedEffectCount,
                ConditionFailureCount,
                _recent.ToArray());
        }
    }

    public readonly struct StoryGameplayBridgeDiagnosticSnapshot
    {
        public StoryGameplayBridgeDiagnosticSnapshot(
            int resolvedEntityCount,
            int unresolvedEntityCount,
            int enqueuedCommandCount,
            int rejectedEffectCount,
            int conditionFailureCount,
            IReadOnlyList<StoryGameplayBridgeDiagnostic> recentDiagnostics)
        {
            ResolvedEntityCount = resolvedEntityCount;
            UnresolvedEntityCount = unresolvedEntityCount;
            EnqueuedCommandCount = enqueuedCommandCount;
            RejectedEffectCount = rejectedEffectCount;
            ConditionFailureCount = conditionFailureCount;
            RecentDiagnostics = recentDiagnostics ?? Array.Empty<StoryGameplayBridgeDiagnostic>();
        }

        public int ResolvedEntityCount { get; }
        public int UnresolvedEntityCount { get; }
        public int EnqueuedCommandCount { get; }
        public int RejectedEffectCount { get; }
        public int ConditionFailureCount { get; }
        public IReadOnlyList<StoryGameplayBridgeDiagnostic> RecentDiagnostics { get; }
    }
}
