using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Stores one component-native buff instance with deterministic stack, lifetime, and source metadata.
    /// </summary>
    public readonly struct GameplayComponentBuffEntry : IEquatable<GameplayComponentBuffEntry>
    {
        /// <summary>
        /// Creates a buff entry.
        /// </summary>
        /// <param name="buffId">Stable positive buff id.</param>
        /// <param name="stackCount">Current positive stack count.</param>
        /// <param name="maxStackCount">Maximum positive stack count.</param>
        /// <param name="endFrame">Frame where the buff expires when it is not permanent.</param>
        /// <param name="isPermanent">Whether the buff ignores frame-based expiration.</param>
        /// <param name="sourceId">Optional non-negative source id used by caller-defined systems.</param>
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

        /// <summary>
        /// Gets the stable buff id.
        /// </summary>
        public int BuffId { get; }

        /// <summary>
        /// Gets the current stack count.
        /// </summary>
        public int StackCount { get; }

        /// <summary>
        /// Gets the maximum stack count.
        /// </summary>
        public int MaxStackCount { get; }

        /// <summary>
        /// Gets the frame where the buff expires when it is not permanent.
        /// </summary>
        public long EndFrame { get; }

        /// <summary>
        /// Gets whether the buff ignores frame-based expiration.
        /// </summary>
        public bool IsPermanent { get; }

        /// <summary>
        /// Gets the optional caller-defined source id.
        /// </summary>
        public int SourceId { get; }

        /// <summary>
        /// Returns whether this buff is expired at the supplied runtime frame.
        /// </summary>
        /// <param name="frame">The runtime frame to test.</param>
        /// <returns><c>true</c> when this non-permanent buff has reached or passed its end frame.</returns>
        public bool IsExpired(RuntimeFrame frame)
        {
            return !IsPermanent && EndFrame <= frame.Value;
        }

        /// <summary>
        /// Creates a copy with a different stack count.
        /// </summary>
        /// <param name="stackCount">The replacement positive stack count.</param>
        /// <returns>A new buff entry with the requested stack count.</returns>
        public GameplayComponentBuffEntry WithStackCount(int stackCount)
        {
            return new GameplayComponentBuffEntry(BuffId, stackCount, MaxStackCount, EndFrame, IsPermanent, SourceId);
        }

        /// <summary>
        /// Creates a copy with a different end frame.
        /// </summary>
        /// <param name="endFrame">The replacement non-negative end frame.</param>
        /// <returns>A new buff entry with the requested end frame.</returns>
        public GameplayComponentBuffEntry WithEndFrame(long endFrame)
        {
            return new GameplayComponentBuffEntry(BuffId, StackCount, MaxStackCount, endFrame, IsPermanent, SourceId);
        }

        /// <summary>
        /// Compares this entry with another entry.
        /// </summary>
        /// <param name="other">The entry to compare.</param>
        /// <returns><c>true</c> when all fields are equal.</returns>
        public bool Equals(GameplayComponentBuffEntry other)
        {
            return BuffId == other.BuffId
                && StackCount == other.StackCount
                && MaxStackCount == other.MaxStackCount
                && EndFrame == other.EndFrame
                && IsPermanent == other.IsPermanent
                && SourceId == other.SourceId;
        }

        /// <summary>
        /// Compares this entry with another object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns><c>true</c> when the object is an equal buff entry.</returns>
        public override bool Equals(object obj)
        {
            return obj is GameplayComponentBuffEntry other && Equals(other);
        }

        /// <summary>
        /// Returns a hash code for this entry.
        /// </summary>
        /// <returns>A hash code built from all entry fields.</returns>
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
