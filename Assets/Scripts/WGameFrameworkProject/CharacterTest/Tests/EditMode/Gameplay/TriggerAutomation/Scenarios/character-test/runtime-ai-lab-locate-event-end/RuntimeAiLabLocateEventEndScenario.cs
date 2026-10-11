namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabLocateEventEndScenario :
        TriggerTestRuntimeAiLabTimelineScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-locate-event-end";

        public RuntimeAiLabLocateEventEndScenario()
            : base(
                ScenarioId,
                "Locate Runtime AI Lab event end",
                "locate-event-end",
                TriggerTestRuntimeAiLabTimelineCommandKind.LocateEventEnd)
        {
        }
    }
}
