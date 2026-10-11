namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabSelectSetupSlotScenario :
        TriggerTestRuntimeAiLabSetupScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-select-setup-slot";

        public RuntimeAiLabSelectSetupSlotScenario()
            : base(
                ScenarioId,
                "Select Runtime AI Lab setup slot",
                "select-setup-slot",
                TriggerTestRuntimeAiLabSetupCommandKind.SelectSlot)
        {
        }
    }
}
