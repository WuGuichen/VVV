namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabPauseSimulationScenario :
        TriggerTestRuntimeAiLabCommandScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-pause-simulation";

        public RuntimeAiLabPauseSimulationScenario()
            : base(
                ScenarioId,
                "Pause Runtime AI Lab simulation",
                "pause-simulation",
                TriggerTestRuntimeAiLabCommandKind.PauseSimulation)
        {
        }
    }
}
