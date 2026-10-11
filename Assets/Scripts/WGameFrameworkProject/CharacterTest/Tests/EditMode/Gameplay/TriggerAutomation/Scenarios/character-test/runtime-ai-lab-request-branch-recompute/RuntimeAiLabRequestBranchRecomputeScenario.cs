namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabRequestBranchRecomputeScenario :
        TriggerTestRuntimeAiLabTimelineScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-request-branch-recompute";

        public RuntimeAiLabRequestBranchRecomputeScenario()
            : base(
                ScenarioId,
                "Request Runtime AI Lab branch recompute",
                "request-branch-recompute",
                TriggerTestRuntimeAiLabTimelineCommandKind
                    .RequestBranchRecompute)
        {
        }
    }
}
