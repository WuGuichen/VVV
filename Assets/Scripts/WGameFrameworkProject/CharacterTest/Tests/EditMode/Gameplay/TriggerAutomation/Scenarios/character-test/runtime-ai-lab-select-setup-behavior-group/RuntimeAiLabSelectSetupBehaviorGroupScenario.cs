namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabSelectSetupBehaviorGroupScenario :
        TriggerTestRuntimeAiLabSetupScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-select-setup-behavior-group";

        public RuntimeAiLabSelectSetupBehaviorGroupScenario()
            : base(
                ScenarioId,
                "Select Runtime AI Lab setup behavior group",
                "select-setup-behavior-group",
                TriggerTestRuntimeAiLabSetupCommandKind.SelectBehaviorGroup)
        {
        }
    }
}
