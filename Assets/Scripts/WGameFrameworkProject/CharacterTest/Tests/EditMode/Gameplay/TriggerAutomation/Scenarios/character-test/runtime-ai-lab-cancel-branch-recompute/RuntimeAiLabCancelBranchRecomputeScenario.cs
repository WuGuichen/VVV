namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabCancelBranchRecomputeScenario :
        TriggerTestRuntimeAiLabTimelineScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-cancel-branch-recompute";

        public RuntimeAiLabCancelBranchRecomputeScenario()
            : base(
                ScenarioId,
                "Cancel Runtime AI Lab branch recompute",
                "cancel-branch-recompute",
                TriggerTestRuntimeAiLabTimelineCommandKind
                    .CancelBranchRecompute)
        {
        }
    }
}
