using System;

namespace MxFramework.CharacterAction
{
    /// <summary>Action-local timing facts for planning; not permission to execute a transition.</summary>
    public readonly struct CharacterActionTacticalTiming
    {
        internal CharacterActionTacticalTiming(long startupFrames, int startupEndFrame, long activeFrames, int? totalDurationFrames)
        {
            StartupFrames = startupFrames;
            StartupEndFrame = startupEndFrame;
            ActiveFrames = activeFrames;
            TotalDurationFrames = totalDurationFrames;
        }

        public long StartupFrames { get; }
        public int StartupEndFrame { get; }
        public long ActiveFrames { get; }
        public int? TotalDurationFrames { get; }
    }

    /// <summary>Extracts authored timing without depending on a runtime or Unity.</summary>
    public static class CharacterActionTacticalUtility
    {
        /// <summary>Counts the union of inclusive phase ranges. Missing startup ends at -1.</summary>
        /// <remarks>NoAlloc. Quadratic in phase count, independent of duration; intended for configuration queries.</remarks>
        public static CharacterActionTacticalTiming ExtractTiming(CharacterActionConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            long startup = CountFrames(config.Phases, CharacterActionPhaseKind.Startup, out int end);
            long active = CountFrames(config.Phases, CharacterActionPhaseKind.Active, out _);
            return new CharacterActionTacticalTiming(startup, end, active, config.DurationFrames);
        }

        /// <summary>Returns the earliest matching local window start, not current execution eligibility.
        /// Zero query masks only match wildcard rules; multi-bit masks express existence, not universal permission.
        /// No match returns false and -1. Natural completion is separate.</summary>
        /// <remarks>NoAlloc. The Gateway remains authoritative at the actual execution frame.</remarks>
        public static bool TryGetEarliestTransitionFrame(CharacterActionConfig config,
            CharacterActionRequestType requestType, CharacterCancelInputMask slotMask, out int frame)
        {
            frame = -1;
            if (config == null || (requestType & ~CharacterActionRequestType.All) != 0
                || (slotMask & ~CharacterCancelInputMask.All) != 0) return false;
            for (int i = 0; i < config.CancelRules.Length; i++)
            {
                CharacterCancelRule rule = config.CancelRules[i];
                if (rule.Matches(rule.StartFrame, requestType, (int)slotMask)
                    && (frame < 0 || rule.StartFrame < frame)) frame = rule.StartFrame;
            }
            return frame >= 0;
        }

        private static long CountFrames(CharacterActionPhase[] phases, CharacterActionPhaseKind kind, out int endFrame)
        {
            endFrame = -1;
            long count = 0;
            long cursor = 0;
            while (true)
            {
                long first = long.MaxValue;
                long last = -1;
                for (int i = 0; i < phases.Length; i++)
                {
                    CharacterActionPhase phase = phases[i];
                    if (phase.Kind != kind || phase.EndFrame < cursor) continue;
                    long start = Math.Max(cursor, phase.StartFrame);
                    if (start < first) { first = start; last = phase.EndFrame; }
                    else if (start == first) last = Math.Max(last, phase.EndFrame);
                }
                if (last < 0) break;
                count += last - first + 1;
                endFrame = (int)last;
                cursor = last + 1;
            }
            return count;
        }
    }
}
