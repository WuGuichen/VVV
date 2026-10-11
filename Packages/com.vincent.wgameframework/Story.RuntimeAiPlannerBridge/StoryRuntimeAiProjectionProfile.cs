using System;
using System.Collections.Generic;
using MxFramework.AI;

namespace MxFramework.Story.RuntimeAiPlannerBridge
{
    public sealed class StoryRuntimeAiProjectionProfile
    {
        private readonly StoryRuntimeAiFactMapping[] _mappings;

        public StoryRuntimeAiProjectionProfile(IReadOnlyList<StoryRuntimeAiFactMapping> mappings)
        {
            _mappings = CopyAndValidate(mappings);
        }

        public static StoryRuntimeAiProjectionProfile Empty { get; } =
            new StoryRuntimeAiProjectionProfile(Array.Empty<StoryRuntimeAiFactMapping>());

        public IReadOnlyList<StoryRuntimeAiFactMapping> Mappings => _mappings;
        public int Count => _mappings.Length;

        public bool TryGetMapping(StoryFactKey storyKey, out StoryRuntimeAiFactMapping mapping)
        {
            int index = IndexOf(storyKey);
            if (index >= 0)
            {
                mapping = _mappings[index];
                return true;
            }

            mapping = default;
            return false;
        }

        public int IndexOf(StoryFactKey storyKey)
        {
            for (int i = 0; i < _mappings.Length; i++)
            {
                if (_mappings[i].StoryKey == storyKey)
                    return i;
            }

            return -1;
        }

        private static StoryRuntimeAiFactMapping[] CopyAndValidate(IReadOnlyList<StoryRuntimeAiFactMapping> mappings)
        {
            if (mappings == null || mappings.Count == 0)
                return Array.Empty<StoryRuntimeAiFactMapping>();

            var copy = new StoryRuntimeAiFactMapping[mappings.Count];
            var storyKeys = new HashSet<StoryFactKey>();
            var aiKeys = new HashSet<AiFactKey>();
            for (int i = 0; i < mappings.Count; i++)
            {
                StoryRuntimeAiFactMapping mapping = mappings[i];
                if (!mapping.IsValid)
                    throw new ArgumentException("Story Runtime AI Planner projection mapping is invalid.", nameof(mappings));

                if (!storyKeys.Add(mapping.StoryKey))
                    throw new ArgumentException("Story Runtime AI Planner projection profile contains a duplicate Story fact key.", nameof(mappings));

                if (!aiKeys.Add(mapping.AiKey))
                    throw new ArgumentException("Story Runtime AI Planner projection profile contains a duplicate AI fact key.", nameof(mappings));

                copy[i] = mapping;
            }

            Array.Sort(copy, CompareMappings);
            return copy;
        }

        private static int CompareMappings(StoryRuntimeAiFactMapping left, StoryRuntimeAiFactMapping right)
        {
            return left.StoryKey.CompareTo(right.StoryKey);
        }
    }
}
