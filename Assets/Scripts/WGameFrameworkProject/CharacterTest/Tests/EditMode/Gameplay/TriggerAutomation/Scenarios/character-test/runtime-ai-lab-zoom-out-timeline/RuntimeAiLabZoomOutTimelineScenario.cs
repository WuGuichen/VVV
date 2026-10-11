namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabZoomOutTimelineScenario :
        TriggerTestRuntimeAiLabTimelineScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-zoom-out-timeline";

        public RuntimeAiLabZoomOutTimelineScenario()
            : base(
                ScenarioId,
                "Zoom out Runtime AI Lab timeline",
                "zoom-out-timeline",
                TriggerTestRuntimeAiLabTimelineCommandKind.ZoomOut)
        {
        }
    }
}
