namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabSelectLatestFrameScenario :
        TriggerTestRuntimeAiLabTimelineScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-select-latest-frame";

        public RuntimeAiLabSelectLatestFrameScenario()
            : base(
                ScenarioId,
                "Select latest Runtime AI Lab frame",
                "select-latest-frame",
                TriggerTestRuntimeAiLabTimelineCommandKind.SelectLatestFrame)
        {
        }
    }
}
