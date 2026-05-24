using System;
using MxFramework.AI;

namespace MxFramework.Story.RuntimeAiPlannerBridge
{
    public readonly struct StoryRuntimeAiFactMapping : IEquatable<StoryRuntimeAiFactMapping>
    {
        public StoryRuntimeAiFactMapping(StoryFactKey storyKey, AiFactKey aiKey)
        {
            StoryKey = storyKey;
            AiKey = aiKey;
        }

        public StoryFactKey StoryKey { get; }
        public AiFactKey AiKey { get; }
        public bool IsValid => StoryKey.IsValid && AiKey.IsValid;

        public bool Equals(StoryRuntimeAiFactMapping other)
        {
            return StoryKey.Equals(other.StoryKey) && AiKey.Equals(other.AiKey);
        }

        public override bool Equals(object obj)
        {
            return obj is StoryRuntimeAiFactMapping other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StoryKey.GetHashCode() * 397) ^ AiKey.GetHashCode();
            }
        }
    }
}
