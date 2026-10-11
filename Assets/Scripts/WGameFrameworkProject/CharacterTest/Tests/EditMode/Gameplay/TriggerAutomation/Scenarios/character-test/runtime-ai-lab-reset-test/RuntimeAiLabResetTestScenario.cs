namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabResetTestScenario :
        TriggerTestRuntimeAiLabCommandScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-reset-test";

        public RuntimeAiLabResetTestScenario()
            : base(
                ScenarioId,
                "Reset Runtime AI Lab test",
                "reset-test",
                TriggerTestRuntimeAiLabCommandKind.ResetTest)
        {
        }
    }
}
