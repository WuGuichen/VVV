using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestBehaviorBlockCompletion { Timed, Arrived, Action, Condition, Facing, Delay, Distance }
    internal enum CharacterTestBehaviorExecutionEndReason { None, Completed, ConditionRejected, TargetMissing, TimedOut, ActionRejected, CleanupTimedOut, CleanupFailed }

    internal readonly struct CharacterTestBehaviorExecutionBlock
    {
        internal const int ActionBoundaryFrames = 2;
        internal const int CleanupFrames = 3;
        internal readonly int Timeline, Start, Duration, CompletionEvent, SuccessCondition;
        internal readonly bool HasAction;
        internal readonly int PreparationTimeoutFrames, CompletionTimeoutFrames;
        internal readonly int MaximumGuardFrames, GuardActiveStartFrame, GuardActiveEndFrame;
        internal readonly bool RequireLivingTarget;
        internal readonly bool ContinueToActionOnPreparationTimeout;
        internal readonly bool PassiveWait;
        internal readonly CharacterTestBehaviorBlockCompletion Completion;
        internal int End => Start + Duration;
        internal int Budget => Duration + (HasAction ? ActionBoundaryFrames : 0);
        internal CharacterTestBehaviorExecutionBlock(int timeline, int start, int duration,
            CharacterTestBehaviorBlockCompletion completion, int completionEvent, int successCondition, bool hasAction,
            int preparationTimeoutFrames = 0, int completionTimeoutFrames = 0, bool requireLivingTarget = false,
            bool continueToActionOnPreparationTimeout = false, bool passiveWait = false,
            int maximumGuardFrames = 0, int guardActiveStartFrame = 0, int guardActiveEndFrame = -1)
        { Timeline = timeline; Start = start; Duration = duration; Completion = completion; CompletionEvent = completionEvent; SuccessCondition = successCondition; HasAction = hasAction;
            PreparationTimeoutFrames = preparationTimeoutFrames; CompletionTimeoutFrames = completionTimeoutFrames; RequireLivingTarget = requireLivingTarget;
            ContinueToActionOnPreparationTimeout = continueToActionOnPreparationTimeout; PassiveWait = passiveWait;
            MaximumGuardFrames = maximumGuardFrames; GuardActiveStartFrame = guardActiveStartFrame; GuardActiveEndFrame = guardActiveEndFrame; }
    }

    internal sealed partial class CharacterTestBehaviorExecutionBuilder
    {
        private readonly List<(string id, string timeline, int start, int duration, CharacterTestBehaviorBlockCompletion completion,
            string completionEvent, int successCondition, bool hasAction, int preparation, int completionTimeout, bool living, bool continueToAction, bool passiveWait, int maximumGuard, int guardStart, int guardEnd)> _blocks = new();
        internal void AddBlock(string id, string timeline, int start, int duration, CharacterTestBehaviorBlockCompletion completion,
            string completionEvent = null, int successCondition = -1, bool hasAction = false,
            int preparationTimeoutFrames = 0, int completionTimeoutFrames = 0, bool requireLivingTarget = false,
            bool continueToActionOnPreparationTimeout = false, bool passiveWait = false,
            int maximumGuardFrames = 0, int guardActiveStartFrame = 0, int guardActiveEndFrame = -1)
            => _blocks.Add((id, timeline, start, duration, completion, completionEvent, successCondition, hasAction,
                preparationTimeoutFrames, completionTimeoutFrames, requireLivingTarget, continueToActionOnPreparationTimeout, passiveWait,
                maximumGuardFrames, guardActiveStartFrame, guardActiveEndFrame));

        private CharacterTestBehaviorExecutionBlock[] BuildBlocks(Dictionary<string, int> timelines, string[] events)
        {
            _blocks.Sort((a, b) => a.timeline != b.timeline ? string.CompareOrdinal(a.timeline, b.timeline)
                : a.start != b.start ? a.start.CompareTo(b.start) : string.CompareOrdinal(a.id, b.id));
            var result = new CharacterTestBehaviorExecutionBlock[_blocks.Count];
            for (int i = 0; i < result.Length; i++)
            {
                var block = _blocks[i];
                int ev = string.IsNullOrEmpty(block.completionEvent) ? -1 : Array.IndexOf(events, block.completionEvent);
                if (!timelines.TryGetValue(block.timeline, out int timeline) || block.completion != CharacterTestBehaviorBlockCompletion.Timed && ev < 0)
                    throw new ArgumentException("Execution block references a missing timeline or completion event: " + block.id);
                result[i] = new CharacterTestBehaviorExecutionBlock(timeline, block.start, block.duration, block.completion, ev, block.successCondition, block.hasAction,
                    block.preparation, block.completionTimeout, block.living, block.continueToAction, block.passiveWait,
                    block.maximumGuard, block.guardStart, block.guardEnd);
            }
            return result;
        }
    }
}
