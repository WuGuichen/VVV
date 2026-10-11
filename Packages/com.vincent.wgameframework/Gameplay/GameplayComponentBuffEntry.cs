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
        /// <param name="startFrame">Frame where this active buff state was first created or replaced.</param>
        /// <param name="endFrame">Frame where the buff expires when it is not permanent.</param>
        /// <param name="isPermanent">Whether the buff ignores frame-based expiration.</param>
        /// <param name="sourceId">Optional non-negative source id used by caller-defined systems.</param>
        public GameplayComponentBuffEntry(
            int buffId,
            int stackCount,
            long startFrame,
            long endFrame,
            bool isPermanent = false,
            int sourceId = 0)
        {
            if (buffId <= 0)
                throw new ArgumentOutOfRangeException(nameof(buffId), "Gameplay component buff id must be greater than zero.");
            if (stackCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(stackCount), "Gameplay component buff stack count must be greater than zero.");
            if (startFrame < 0L)
                throw new ArgumentOutOfRangeException(nameof(startFrame), "Gameplay component buff start frame cannot be negative.");
            if (endFrame < 0L)
                throw new ArgumentOutOfRangeException(nameof(endFrame), "Gameplay component buff end frame cannot be negative.");
            if (sourceId < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceId), "Gameplay component buff source id cannot be negative.");

            BuffId = buffId;
            StackCount = stackCount;
            StartFrame = startFrame;
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
        /// Gets the frame where this active buff state was first created or replaced.
        /// </summary>
        public long StartFrame { get; }

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
            return new GameplayComponentBuffEntry(BuffId, stackCount, StartFrame, EndFrame, IsPermanent, SourceId);
        }

        /// <summary>
        /// Creates a copy with a different end frame.
        /// </summary>
        /// <param name="endFrame">The replacement non-negative end frame.</param>
        /// <returns>A new buff entry with the requested end frame.</returns>
        public GameplayComponentBuffEntry WithEndFrame(long endFrame)
        {
            return new GameplayComponentBuffEntry(BuffId, StackCount, StartFrame, endFrame, IsPermanent, SourceId);
        }

        /// <summary>
        /// Creates a copy with a different source id.
        /// </summary>
        /// <param name="sourceId">The replacement non-negative source id.</param>
        /// <returns>A new buff entry with the requested source id.</returns>
        public GameplayComponentBuffEntry WithSourceId(int sourceId)
        {
            return new GameplayComponentBuffEntry(BuffId, StackCount, StartFrame, EndFrame, IsPermanent, sourceId);
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
                && StartFrame == other.StartFrame
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
                hash = (hash * 397) ^ StartFrame.GetHashCode();
                hash = (hash * 397) ^ EndFrame.GetHashCode();
                hash = (hash * 397) ^ IsPermanent.GetHashCode();
                hash = (hash * 397) ^ SourceId;
                return hash;
            }
        }
    }
}
