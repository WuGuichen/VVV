namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabLocateEventStartScenario :
        TriggerTestRuntimeAiLabTimelineScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-locate-event-start";

        public RuntimeAiLabLocateEventStartScenario()
            : base(
                ScenarioId,
                "Locate Runtime AI Lab event start",
                "locate-event-start",
                TriggerTestRuntimeAiLabTimelineCommandKind.LocateEventStart)
        {
        }
    }
}
