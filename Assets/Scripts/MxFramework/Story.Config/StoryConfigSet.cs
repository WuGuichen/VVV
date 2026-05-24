using System;
using System.Collections.Generic;
using MxFramework.Config;

namespace MxFramework.Story.Config
{
    public sealed class StoryConfigSet
    {
        public StoryConfigSet(
            IReadOnlyList<StoryGraphConfig> graphs,
            IReadOnlyList<StoryBeatConfig> beats,
            IReadOnlyList<StoryStepConfig> steps,
            IReadOnlyList<StoryBranchConfig> branches,
            IReadOnlyList<StoryChoiceConfig> choices,
            IReadOnlyList<StoryFactConfig> facts)
        {
            Graphs = Copy(graphs);
            Beats = Copy(beats);
            Steps = Copy(steps);
            Branches = Copy(branches);
            Choices = Copy(choices);
            Facts = Copy(facts);
        }

        public IReadOnlyList<StoryGraphConfig> Graphs { get; }
        public IReadOnlyList<StoryBeatConfig> Beats { get; }
        public IReadOnlyList<StoryStepConfig> Steps { get; }
        public IReadOnlyList<StoryBranchConfig> Branches { get; }
        public IReadOnlyList<StoryChoiceConfig> Choices { get; }
        public IReadOnlyList<StoryFactConfig> Facts { get; }

        public static StoryConfigSet Empty { get; } = new StoryConfigSet(
            Array.Empty<StoryGraphConfig>(),
            Array.Empty<StoryBeatConfig>(),
            Array.Empty<StoryStepConfig>(),
            Array.Empty<StoryBranchConfig>(),
            Array.Empty<StoryChoiceConfig>(),
            Array.Empty<StoryFactConfig>());

        public static StoryConfigSet FromProvider(IConfigProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            return new StoryConfigSet(
                Copy(provider.GetAllConfigs<StoryGraphConfig>()),
                Copy(provider.GetAllConfigs<StoryBeatConfig>()),
                Copy(provider.GetAllConfigs<StoryStepConfig>()),
                Copy(provider.GetAllConfigs<StoryBranchConfig>()),
                Copy(provider.GetAllConfigs<StoryChoiceConfig>()),
                Copy(provider.GetAllConfigs<StoryFactConfig>()));
        }

        private static T[] Copy<T>(IReadOnlyCollection<T> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<T>();

            var copy = new T[source.Count];
            int index = 0;
            foreach (T item in source)
                copy[index++] = item;
            return copy;
        }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<T>();

            var copy = new T[source.Count];
            for (int i = 0; i < source.Count; i++)
                copy[i] = source[i];
            return copy;
        }
    }

    public sealed class StoryConfigReferenceIndex
    {
        private readonly HashSet<int> _textKeys = new HashSet<int>();

        public int TextKeyCount => _textKeys.Count;
        public bool HasTextKeys => _textKeys.Count > 0;

        public StoryConfigReferenceIndex AddTextKey(int textKey)
        {
            if (textKey > 0)
                _textKeys.Add(textKey);
            return this;
        }

        public StoryConfigReferenceIndex AddTextKeys(IEnumerable<int> textKeys)
        {
            if (textKeys == null)
                return this;

            foreach (int textKey in textKeys)
                AddTextKey(textKey);
            return this;
        }

        public bool ContainsTextKey(int textKey)
        {
            return textKey > 0 && _textKeys.Contains(textKey);
        }
    }
}
