namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabResumeSimulationScenario :
        TriggerTestRuntimeAiLabCommandScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-resume-simulation";

        public RuntimeAiLabResumeSimulationScenario()
            : base(
                ScenarioId,
                "Resume Runtime AI Lab simulation",
                "resume-simulation",
                TriggerTestRuntimeAiLabCommandKind.ResumeSimulation)
        {
        }
    }
}
