namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabDisableSetupSlotScenario :
        TriggerTestRuntimeAiLabSetupScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-disable-setup-slot";

        public RuntimeAiLabDisableSetupSlotScenario()
            : base(
                ScenarioId,
                "Disable Runtime AI Lab setup slot",
                "disable-setup-slot",
                TriggerTestRuntimeAiLabSetupCommandKind.DisableSlot)
        {
        }
    }
}
