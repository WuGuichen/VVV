namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestAiTraceReason
    {
        internal const int None = 0;
        internal const int LOGIC_DETECTOR_FIRED = 60037;
        internal const int LOGIC_DETECTOR_NOT_FIRED = 60038;
        internal const int LOGIC_STAGE_ENTERED = 60039;
        internal const int LOGIC_STAGE_HELD = 60040;
        internal const int LOGIC_TRANSITION_EVALUATED = 60041;
        internal const int LOGIC_TRANSITION_SELECTED = 60042;
        internal const int LOGIC_TRANSITION_UNAVAILABLE = 60043;
        internal const int RUNTIME_ACTION_STARTED = 60032;
        internal const int RUNTIME_ACTION_RUNNING = 60033;
        internal const int RUNTIME_ACTION_COMPLETED = 60034;
        internal const int RUNTIME_ACTION_CANCELLED = 60035;
        internal const int RUNTIME_ACTION_FAILED = 60036;
        internal const int ACTION_USE_ = 60001;
        internal const int COMMAND_REJECTED = 60002;
        internal const int FACT_CANDIDATE_CONDITIONS_NOT_MET = 60003;
        internal const int FACT_CANDIDATE_LOWER_RANK = 60004;
        internal const int FACT_CANDIDATE_NOT_EVALUATED = 60005;
        internal const int FACT_CANDIDATE_SELECTED = 60006;
        internal const int FACT_COMMAND_ENQUEUE_REJECTED = 60007;
        internal const int FACT_DERIVE_ABILITY_AVAILABILITY = 60008;
        internal const int FACT_DERIVE_CHARACTER_ACTION_CANCEL_WINDOW = 60009;
        internal const int FACT_DERIVE_CHARACTER_ACTION_TRANSITION = 60010;
        internal const int FACT_DERIVE_PLANNER_SENSE = 60011;
        internal const int FACT_DERIVE_SELF_HEALTH_RATIO = 60012;
        internal const int FACT_TRACE_OVERFLOW = 60013;
        internal const int LOGIC_COMMITTED = 60014;
        internal const int LOGIC_FALLBACK = 60015;
        internal const int LOGIC_HELD = 60016;
        internal const int LOGIC_NONE = 60017;
        internal const int LOGIC_REJECTED = 60018;
        internal const int LOGIC_STAGE_REPLACED = 60019;
        internal const int LOGIC_TRANSITION_PENDING = 60020;
        internal const int PLANNER_ACTION_CONDITION_FAILED = 60021;
        internal const int PLANNER_ACTION_EVALUATED = 60022;
        internal const int PLANNER_ACTION_OUTSIDE_ACTIVE_PATH = 60023;
        internal const int PLANNER_ACTION_SELECTED = 60024;
        internal const int PLANNER_ACTION_SUPPRESSED_BY_BEHAVIOR_CARD = 60025;
        internal const int PLANNER_NO_EXECUTABLE_ACTION = 60026;
        internal const int PLANNER_RUNTIME_ACTION_COMPLETED = 60027;
        internal const int PLANNER_RUNTIME_ACTION_RUNNING = 60028;
        internal const int PLANNER_RUNTIME_ACTION_STARTED = 60029;
        internal const int PLANNER_SELECTION_HELD = 60030;
        internal const int PLANNER_TARGET_MISSING = 60031;

        internal static string Text(int value)
        {
            switch (value)
            {
                case None: return string.Empty;
                case LOGIC_DETECTOR_FIRED: return nameof(LOGIC_DETECTOR_FIRED);
                case LOGIC_DETECTOR_NOT_FIRED: return nameof(LOGIC_DETECTOR_NOT_FIRED);
                case LOGIC_STAGE_ENTERED: return nameof(LOGIC_STAGE_ENTERED);
                case LOGIC_STAGE_HELD: return nameof(LOGIC_STAGE_HELD);
                case LOGIC_TRANSITION_EVALUATED: return nameof(LOGIC_TRANSITION_EVALUATED);
                case LOGIC_TRANSITION_SELECTED: return nameof(LOGIC_TRANSITION_SELECTED);
                case LOGIC_TRANSITION_UNAVAILABLE: return nameof(LOGIC_TRANSITION_UNAVAILABLE);
                case RUNTIME_ACTION_STARTED: return nameof(RUNTIME_ACTION_STARTED);
                case RUNTIME_ACTION_RUNNING: return nameof(RUNTIME_ACTION_RUNNING);
                case RUNTIME_ACTION_COMPLETED: return nameof(RUNTIME_ACTION_COMPLETED);
                case RUNTIME_ACTION_CANCELLED: return nameof(RUNTIME_ACTION_CANCELLED);
                case RUNTIME_ACTION_FAILED: return nameof(RUNTIME_ACTION_FAILED);
                case ACTION_USE_: return nameof(ACTION_USE_);
                case COMMAND_REJECTED: return nameof(COMMAND_REJECTED);
                case FACT_CANDIDATE_CONDITIONS_NOT_MET: return nameof(FACT_CANDIDATE_CONDITIONS_NOT_MET);
                case FACT_CANDIDATE_LOWER_RANK: return nameof(FACT_CANDIDATE_LOWER_RANK);
                case FACT_CANDIDATE_NOT_EVALUATED: return nameof(FACT_CANDIDATE_NOT_EVALUATED);
                case FACT_CANDIDATE_SELECTED: return nameof(FACT_CANDIDATE_SELECTED);
                case FACT_COMMAND_ENQUEUE_REJECTED: return nameof(FACT_COMMAND_ENQUEUE_REJECTED);
                case FACT_DERIVE_ABILITY_AVAILABILITY: return nameof(FACT_DERIVE_ABILITY_AVAILABILITY);
                case FACT_DERIVE_CHARACTER_ACTION_CANCEL_WINDOW: return nameof(FACT_DERIVE_CHARACTER_ACTION_CANCEL_WINDOW);
                case FACT_DERIVE_CHARACTER_ACTION_TRANSITION: return nameof(FACT_DERIVE_CHARACTER_ACTION_TRANSITION);
                case FACT_DERIVE_PLANNER_SENSE: return nameof(FACT_DERIVE_PLANNER_SENSE);
                case FACT_DERIVE_SELF_HEALTH_RATIO: return nameof(FACT_DERIVE_SELF_HEALTH_RATIO);
                case FACT_TRACE_OVERFLOW: return nameof(FACT_TRACE_OVERFLOW);
                case LOGIC_COMMITTED: return nameof(LOGIC_COMMITTED);
                case LOGIC_FALLBACK: return nameof(LOGIC_FALLBACK);
                case LOGIC_HELD: return nameof(LOGIC_HELD);
                case LOGIC_NONE: return nameof(LOGIC_NONE);
                case LOGIC_REJECTED: return nameof(LOGIC_REJECTED);
                case LOGIC_STAGE_REPLACED: return nameof(LOGIC_STAGE_REPLACED);
                case LOGIC_TRANSITION_PENDING: return nameof(LOGIC_TRANSITION_PENDING);
                case PLANNER_ACTION_CONDITION_FAILED: return nameof(PLANNER_ACTION_CONDITION_FAILED);
                case PLANNER_ACTION_EVALUATED: return nameof(PLANNER_ACTION_EVALUATED);
                case PLANNER_ACTION_OUTSIDE_ACTIVE_PATH: return nameof(PLANNER_ACTION_OUTSIDE_ACTIVE_PATH);
                case PLANNER_ACTION_SELECTED: return nameof(PLANNER_ACTION_SELECTED);
                case PLANNER_ACTION_SUPPRESSED_BY_BEHAVIOR_CARD: return nameof(PLANNER_ACTION_SUPPRESSED_BY_BEHAVIOR_CARD);
                case PLANNER_NO_EXECUTABLE_ACTION: return nameof(PLANNER_NO_EXECUTABLE_ACTION);
                case PLANNER_RUNTIME_ACTION_COMPLETED: return nameof(PLANNER_RUNTIME_ACTION_COMPLETED);
                case PLANNER_RUNTIME_ACTION_RUNNING: return nameof(PLANNER_RUNTIME_ACTION_RUNNING);
                case PLANNER_RUNTIME_ACTION_STARTED: return nameof(PLANNER_RUNTIME_ACTION_STARTED);
                case PLANNER_SELECTION_HELD: return nameof(PLANNER_SELECTION_HELD);
                case PLANNER_TARGET_MISSING: return nameof(PLANNER_TARGET_MISSING);
                default: return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
