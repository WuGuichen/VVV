namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabSelectHistoricalFrameScenario :
        TriggerTestRuntimeAiLabTimelineScenarioBase,
        ITriggerTestRuntimeAiLabHistoricalFrameScenario
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-select-historical-frame";

        public RuntimeAiLabSelectHistoricalFrameScenario()
            : base(
                ScenarioId,
                "Select specified Runtime AI Lab historical frame",
                "select-historical-frame",
                TriggerTestRuntimeAiLabTimelineCommandKind
                    .SelectHistoricalFrame)
        {
        }

        public int TargetFrame { get; set; }

        protected override int GetTargetFrame() => TargetFrame;
    }
}
