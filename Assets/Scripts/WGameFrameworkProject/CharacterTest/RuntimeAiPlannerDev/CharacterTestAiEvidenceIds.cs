using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestAiEvidenceIds
    {
        internal const int PlannerFactKind = 2100;
        internal const int LogicFactKind = 2101;
        internal const int ParameterFactKind = 2102;
        internal const int PlannerActionKind = 2110;
        internal const int PlannerStateKind = 2111;
        internal const int PlannerFallbackKind = 2112;
        internal const int CardKind = 2120;
        internal const int StageKind = 2121;
        internal const int DetectorKind = 2122;
        internal const int TransitionKind = 2123;
        internal const int MainActionKind = 2124;
        internal const int PlannerSenseKind = 2130;
        internal const int CharacterActionKind = 2131;
        internal const int AbilityKind = 2132;
        internal const int HealthKind = 2133;
        internal const int ActionTransitionKind = 2140;
        internal const int AbilityDefinitionKind = 2141;
        internal const int AbilityCooldownKind = 2142;

        internal static RuntimeTraceIdentity Definition(int kind, int id) =>
            id <= 0 ? default : new RuntimeTraceIdentity(kind, id);

        internal static RuntimeTraceIdentity Index(int kind, int index, int owner = 0) =>
            index < 0 ? default : new RuntimeTraceIdentity(kind, owner, sequence: index + 1L);

        internal static RuntimeTraceIdentity PlannerFact(CharacterTestRuntimeAiPlannerFactKind fact) =>
            new RuntimeTraceIdentity(PlannerFactKind, (int)fact);

        internal static RuntimeTraceIdentity LogicFact(CharacterTestRuntimeAiLabCardConditionFact fact) =>
            new RuntimeTraceIdentity(LogicFactKind, (int)fact);

        internal static RuntimeTraceIdentity Source(CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            switch (fact)
            {
                case CharacterTestRuntimeAiLabCardConditionFact.SelfHealthRatio:
                    return new RuntimeTraceIdentity(HealthKind);
                case CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction:
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible:
                    return new RuntimeTraceIdentity(CharacterActionKind);
                case CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable:
                    return new RuntimeTraceIdentity(AbilityKind);
                default:
                    return new RuntimeTraceIdentity(PlannerSenseKind);
            }
        }
    }
}
