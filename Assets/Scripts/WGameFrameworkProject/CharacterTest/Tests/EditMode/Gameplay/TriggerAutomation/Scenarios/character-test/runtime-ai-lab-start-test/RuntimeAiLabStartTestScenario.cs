namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabStartTestScenario :
        TriggerTestRuntimeAiLabCommandScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-start-test";

        public RuntimeAiLabStartTestScenario()
            : base(
                ScenarioId,
                "Start Runtime AI Lab test",
                "start-test",
                TriggerTestRuntimeAiLabCommandKind.StartTest)
        {
        }
    }
}
