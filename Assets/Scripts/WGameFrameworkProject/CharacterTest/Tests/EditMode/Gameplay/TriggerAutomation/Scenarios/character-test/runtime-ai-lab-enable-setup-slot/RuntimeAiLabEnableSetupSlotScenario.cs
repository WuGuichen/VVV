namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabEnableSetupSlotScenario :
        TriggerTestRuntimeAiLabSetupScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-enable-setup-slot";

        public RuntimeAiLabEnableSetupSlotScenario()
            : base(
                ScenarioId,
                "Enable Runtime AI Lab setup slot",
                "enable-setup-slot",
                TriggerTestRuntimeAiLabSetupCommandKind.EnableSlot)
        {
        }
    }
}
