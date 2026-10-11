namespace MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest
{
    [TriggerTestScenario(ScenarioId)]
    internal sealed class RuntimeAiLabSelectTimelineEventScenario :
        TriggerTestRuntimeAiLabTimelineScenarioBase,
        ITriggerTestRuntimeAiLabTimelineEventScenario
    {
        internal const string ScenarioId =
            "mx.trigger.test.character-test.runtime-ai-lab-select-timeline-event";

        public RuntimeAiLabSelectTimelineEventScenario()
            : base(
                ScenarioId,
                "Select specified Runtime AI Lab timeline event",
                "select-timeline-event",
                TriggerTestRuntimeAiLabTimelineCommandKind.SelectEvent)
        {
        }

        public int TargetEventIndex { get; set; }

        protected override int GetTargetEventIndex() => TargetEventIndex;
    }
}
