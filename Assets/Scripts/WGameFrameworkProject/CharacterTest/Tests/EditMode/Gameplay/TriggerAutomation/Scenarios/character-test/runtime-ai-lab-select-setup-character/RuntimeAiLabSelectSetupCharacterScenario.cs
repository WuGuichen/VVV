namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabSelectSetupCharacterScenario :
        TriggerTestRuntimeAiLabSetupScenarioBase
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-select-setup-character";

        public RuntimeAiLabSelectSetupCharacterScenario()
            : base(
                ScenarioId,
                "Select Runtime AI Lab setup character",
                "select-setup-character",
                TriggerTestRuntimeAiLabSetupCommandKind.SelectCharacter)
        {
        }
    }
}
