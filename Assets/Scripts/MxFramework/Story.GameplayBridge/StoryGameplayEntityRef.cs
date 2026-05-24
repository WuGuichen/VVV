using System;
using MxFramework.Gameplay;

namespace MxFramework.Story.GameplayBridge
{
    public static class StoryGameplayEntityRefKinds
    {
        public const int None = 0;
        public const int LegacyRuntimeEntity = 1;
        public const int ComponentEntity = 2;
        public const int ProjectStableHandle = 3;
    }

    public readonly struct StoryGameplayEntityRef : IEquatable<StoryGameplayEntityRef>
    {
        public StoryGameplayEntityRef(int kind, int id0, int id1 = 0)
        {
            Kind = kind;
            Id0 = id0;
            Id1 = id1;
        }

        public int Kind { get; }
        public int Id0 { get; }
        public int Id1 { get; }
        public bool IsNone => Kind == StoryGameplayEntityRefKinds.None;

        public static StoryGameplayEntityRef None => default;

        public static StoryGameplayEntityRef LegacyRuntimeEntity(int entityId)
        {
            return new StoryGameplayEntityRef(StoryGameplayEntityRefKinds.LegacyRuntimeEntity, entityId);
        }

        public static StoryGameplayEntityRef ComponentEntity(GameplayEntityId entityId)
        {
            return new StoryGameplayEntityRef(StoryGameplayEntityRefKinds.ComponentEntity, entityId.Index, entityId.Generation);
        }

        public static StoryGameplayEntityRef ProjectStableHandle(int handleId, int generation = 0)
        {
            return new StoryGameplayEntityRef(StoryGameplayEntityRefKinds.ProjectStableHandle, handleId, generation);
        }

        public bool TryGetComponentEntityId(out GameplayEntityId entityId)
        {
            if (Kind != StoryGameplayEntityRefKinds.ComponentEntity || Id0 <= 0 || Id1 <= 0)
            {
                entityId = default;
                return false;
            }

            entityId = new GameplayEntityId(Id0, Id1);
            return true;
        }

        public bool Equals(StoryGameplayEntityRef other)
        {
            return Kind == other.Kind && Id0 == other.Id0 && Id1 == other.Id1;
        }

        public override bool Equals(object obj)
        {
            return obj is StoryGameplayEntityRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Kind;
                hash = (hash * 397) ^ Id0;
                hash = (hash * 397) ^ Id1;
                return hash;
            }
        }

        public override string ToString()
        {
            return Kind + ":" + Id0 + ":" + Id1;
        }

        public static bool operator ==(StoryGameplayEntityRef left, StoryGameplayEntityRef right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StoryGameplayEntityRef left, StoryGameplayEntityRef right)
        {
            return !left.Equals(right);
        }
    }
}
