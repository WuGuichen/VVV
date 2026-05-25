using System;
using System.Collections.Generic;
using MxFramework.Story;

namespace MxFramework.Demo.CharacterTest
{
    public sealed class CharacterTestStoryContent
    {
        private readonly IReadOnlyDictionary<int, string> _texts;
        private readonly IReadOnlyDictionary<int, int> _stepTextKeys;

        public CharacterTestStoryContent(
            StoryGraphDefinition graph,
            IReadOnlyDictionary<int, string> texts)
        {
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _texts = texts ?? EmptyTexts;
            _stepTextKeys = BuildStepTextKeyIndex(graph);
        }

        public StoryGraphDefinition Graph { get; }
        public int GraphId => Graph.GraphId;
        public int EntryBeatId => Graph.EntryBeatId;

        public bool TryResolveStepText(int stepId, out string text)
        {
            text = string.Empty;
            if (!_stepTextKeys.TryGetValue(stepId, out int textKey))
                return false;

            return TryResolveText(textKey, out text);
        }

        public bool TryResolveText(int textKey, out string text)
        {
            text = string.Empty;
            if (textKey <= 0 || !_texts.TryGetValue(textKey, out string resolved))
                return false;

            text = resolved ?? string.Empty;
            return text.Length > 0;
        }

        private static IReadOnlyDictionary<int, int> BuildStepTextKeyIndex(StoryGraphDefinition graph)
        {
            var index = new Dictionary<int, int>();
            for (int beatIndex = 0; beatIndex < graph.Beats.Count; beatIndex++)
            {
                StoryBeatDefinition beat = graph.Beats[beatIndex];
                if (beat == null)
                    continue;

                for (int stepIndex = 0; stepIndex < beat.Steps.Count; stepIndex++)
                {
                    StoryStepDefinition step = beat.Steps[stepIndex];
                    if (step != null && step.TextKey > 0)
                        index[step.StepId] = step.TextKey;
                }
            }

            return index;
        }

        private static IReadOnlyDictionary<int, string> EmptyTexts { get; } =
            new Dictionary<int, string>();
    }
}
