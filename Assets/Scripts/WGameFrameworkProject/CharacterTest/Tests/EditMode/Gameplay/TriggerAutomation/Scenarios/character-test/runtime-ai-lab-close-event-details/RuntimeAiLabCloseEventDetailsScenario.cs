namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabCloseEventDetailsScenario :
        TriggerTestRuntimeAiLabTimelineScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-close-event-details";

        public RuntimeAiLabCloseEventDetailsScenario()
            : base(
                ScenarioId,
                "Close Runtime AI Lab event details",
                "close-event-details",
                TriggerTestRuntimeAiLabTimelineCommandKind.CloseEventDetails)
        {
        }
    }
}
