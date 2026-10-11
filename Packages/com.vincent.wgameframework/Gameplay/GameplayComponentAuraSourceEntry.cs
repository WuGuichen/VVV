using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Stores one component-native aura source with deterministic lifetime and source metadata.
    /// </summary>
    public readonly struct GameplayComponentAuraSourceEntry : IEquatable<GameplayComponentAuraSourceEntry>
    {
        /// <summary>
        /// Creates an aura source entry.
        /// </summary>
        /// <param name="auraId">Stable positive aura id.</param>
        /// <param name="endFrame">Frame where the aura source expires when it is not permanent.</param>
        /// <param name="isPermanent">Whether the aura source ignores frame-based expiration.</param>
        /// <param name="sourceId">Optional non-negative source id used by caller-defined systems.</param>
        public GameplayComponentAuraSourceEntry(
            int auraId,
            long endFrame,
            bool isPermanent = false,
            int sourceId = 0)
        {
            if (auraId <= 0)
                throw new ArgumentOutOfRangeException(nameof(auraId), "Gameplay component aura id must be greater than zero.");
            if (endFrame < 0L)
                throw new ArgumentOutOfRangeException(nameof(endFrame), "Gameplay component aura end frame cannot be negative.");
            if (sourceId < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceId), "Gameplay component aura source id cannot be negative.");

            AuraId = auraId;
            EndFrame = endFrame;
            IsPermanent = isPermanent;
            SourceId = sourceId;
        }

        /// <summary>
        /// Gets the stable aura id.
        /// </summary>
        public int AuraId { get; }

        /// <summary>
        /// Gets the frame where the aura source expires when it is not permanent.
        /// </summary>
        public long EndFrame { get; }

        /// <summary>
        /// Gets whether the aura source ignores frame-based expiration.
        /// </summary>
        public bool IsPermanent { get; }

        /// <summary>
        /// Gets the optional caller-defined source id.
        /// </summary>
        public int SourceId { get; }

        /// <summary>
        /// Returns whether this aura source is expired at the supplied runtime frame.
        /// </summary>
        /// <param name="frame">The runtime frame to test.</param>
        /// <returns><c>true</c> when this non-permanent aura has reached or passed its end frame.</returns>
        public bool IsExpired(RuntimeFrame frame)
        {
            return !IsPermanent && EndFrame <= frame.Value;
        }

        /// <summary>
        /// Creates a copy with a different end frame.
        /// </summary>
        /// <param name="endFrame">The replacement non-negative end frame.</param>
        /// <returns>A new aura source entry with the requested end frame.</returns>
        public GameplayComponentAuraSourceEntry WithEndFrame(long endFrame)
        {
            return new GameplayComponentAuraSourceEntry(AuraId, endFrame, IsPermanent, SourceId);
        }

        /// <summary>
        /// Compares this entry with another entry.
        /// </summary>
        /// <param name="other">The entry to compare.</param>
        /// <returns><c>true</c> when all fields are equal.</returns>
        public bool Equals(GameplayComponentAuraSourceEntry other)
        {
            return AuraId == other.AuraId
                && EndFrame == other.EndFrame
                && IsPermanent == other.IsPermanent
                && SourceId == other.SourceId;
        }

        /// <summary>
        /// Compares this entry with another object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns><c>true</c> when the object is an equal aura source entry.</returns>
        public override bool Equals(object obj)
        {
            return obj is GameplayComponentAuraSourceEntry other && Equals(other);
        }

        /// <summary>
        /// Returns a hash code for this entry.
        /// </summary>
        /// <returns>A hash code built from all entry fields.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AuraId;
                hash = (hash * 397) ^ EndFrame.GetHashCode();
                hash = (hash * 397) ^ IsPermanent.GetHashCode();
                hash = (hash * 397) ^ SourceId;
                return hash;
            }
        }
    }
}
