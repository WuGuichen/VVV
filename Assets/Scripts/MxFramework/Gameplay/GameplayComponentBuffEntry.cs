using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayComponentBuffEntry : IEquatable<GameplayComponentBuffEntry>
    {
        public GameplayComponentBuffEntry(
            int buffId,
            int stackCount,
            int maxStackCount,
            long endFrame,
            bool isPermanent = false,
            int sourceId = 0)
        {
            if (buffId <= 0)
                throw new ArgumentOutOfRangeException(nameof(buffId), "Gameplay component buff id must be greater than zero.");
            if (stackCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(stackCount), "Gameplay component buff stack count must be greater than zero.");
            if (maxStackCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxStackCount), "Gameplay component buff max stack count must be greater than zero.");
            if (stackCount > maxStackCount)
                throw new ArgumentOutOfRangeException(nameof(stackCount), "Gameplay component buff stack count cannot exceed max stack count.");
            if (endFrame < 0L)
                throw new ArgumentOutOfRangeException(nameof(endFrame), "Gameplay component buff end frame cannot be negative.");
            if (sourceId < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceId), "Gameplay component buff source id cannot be negative.");

            BuffId = buffId;
            StackCount = stackCount;
            MaxStackCount = maxStackCount;
            EndFrame = endFrame;
            IsPermanent = isPermanent;
            SourceId = sourceId;
        }

        public int BuffId { get; }
        public int StackCount { get; }
        public int MaxStackCount { get; }
        public long EndFrame { get; }
        public bool IsPermanent { get; }
        public int SourceId { get; }

        public bool IsExpired(RuntimeFrame frame)
        {
            return !IsPermanent && EndFrame <= frame.Value;
        }

        public GameplayComponentBuffEntry WithStackCount(int stackCount)
        {
            return new GameplayComponentBuffEntry(BuffId, stackCount, MaxStackCount, EndFrame, IsPermanent, SourceId);
        }

        public GameplayComponentBuffEntry WithEndFrame(long endFrame)
        {
            return new GameplayComponentBuffEntry(BuffId, StackCount, MaxStackCount, endFrame, IsPermanent, SourceId);
        }

        public bool Equals(GameplayComponentBuffEntry other)
        {
            return BuffId == other.BuffId
                && StackCount == other.StackCount
                && MaxStackCount == other.MaxStackCount
                && EndFrame == other.EndFrame
                && IsPermanent == other.IsPermanent
                && SourceId == other.SourceId;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayComponentBuffEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = BuffId;
                hash = (hash * 397) ^ StackCount;
                hash = (hash * 397) ^ MaxStackCount;
                hash = (hash * 397) ^ EndFrame.GetHashCode();
                hash = (hash * 397) ^ IsPermanent.GetHashCode();
                hash = (hash * 397) ^ SourceId;
                return hash;
            }
        }
    }
}
