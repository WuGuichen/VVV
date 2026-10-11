namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabZoomInTimelineScenario :
        TriggerTestRuntimeAiLabTimelineScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-zoom-in-timeline";

        public RuntimeAiLabZoomInTimelineScenario()
            : base(
                ScenarioId,
                "Zoom in Runtime AI Lab timeline",
                "zoom-in-timeline",
                TriggerTestRuntimeAiLabTimelineCommandKind.ZoomIn)
        {
        }
    }
}
