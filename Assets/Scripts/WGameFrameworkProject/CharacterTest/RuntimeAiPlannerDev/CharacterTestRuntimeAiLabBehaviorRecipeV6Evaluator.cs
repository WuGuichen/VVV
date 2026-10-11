using System;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestRuntimeAiLabBehaviorRecipeSubjectAvailability
    {
        private static readonly CharacterTestRuntimeAiLabCompiledCondition TargetExists =
            new CharacterTestRuntimeAiLabCompiledCondition(
                "subject.current-combat-target.exists",
                CharacterTestRuntimeAiLabCardConditionFact.TargetExists,
                CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                true,
                0f,
                0f,
                0,
                default);

        internal static bool IsAvailable(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind subject,
            ICharacterTestRuntimeAiLabBehaviorCardLogicRuntime runtime)
        {
            if (runtime == null)
                return false;
            switch (subject)
            {
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget:
                    return runtime.Evaluate(TargetExists);
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Self:
                case CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.Environment:
                    return true;
                default:
                    return false;
            }
        }
    }

    internal enum CharacterTestRuntimeAiLabBehaviorRecipeV6PendingOperation
    {
        None = 0,
        StartActorEvent = 1,
        CancelActorEvent = 2,
        EnterTimeline = 3,
        ReturnToPlanner = 4,
        RewindLoop = 5,
        CompleteTimeline = 6
    }

}
