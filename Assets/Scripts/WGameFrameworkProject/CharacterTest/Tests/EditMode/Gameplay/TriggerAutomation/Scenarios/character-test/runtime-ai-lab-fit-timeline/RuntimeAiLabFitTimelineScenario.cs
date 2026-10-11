namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabFitTimelineScenario :
        TriggerTestRuntimeAiLabTimelineScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-fit-timeline";

        public RuntimeAiLabFitTimelineScenario()
            : base(
                ScenarioId,
                "Fit complete Runtime AI Lab timeline",
                "fit-timeline",
                TriggerTestRuntimeAiLabTimelineCommandKind.FitTimeline)
        {
        }
    }
}
