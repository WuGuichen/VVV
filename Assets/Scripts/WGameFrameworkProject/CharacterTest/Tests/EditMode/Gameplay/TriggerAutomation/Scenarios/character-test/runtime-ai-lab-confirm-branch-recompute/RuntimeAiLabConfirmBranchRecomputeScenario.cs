namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabConfirmBranchRecomputeScenario :
        TriggerTestRuntimeAiLabTimelineScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-confirm-branch-recompute";

        public RuntimeAiLabConfirmBranchRecomputeScenario()
            : base(
                ScenarioId,
                "Confirm Runtime AI Lab branch recompute",
                "confirm-branch-recompute",
                TriggerTestRuntimeAiLabTimelineCommandKind
                    .ConfirmBranchRecompute)
        {
        }
    }
}
