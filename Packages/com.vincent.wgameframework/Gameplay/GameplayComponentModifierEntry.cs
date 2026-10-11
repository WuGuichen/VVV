using System;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Stores one additive component modifier and the optional buff that owns it.
    /// </summary>
    public readonly struct GameplayComponentModifierEntry : IEquatable<GameplayComponentModifierEntry>
    {
        /// <summary>
        /// Creates a modifier entry.
        /// </summary>
        /// <param name="modifierId">Stable positive modifier id.</param>
        /// <param name="attributeId">Positive attribute id affected by this modifier.</param>
        /// <param name="addValue">Additive value applied to the attribute.</param>
        /// <param name="sourceBuffId">Optional non-negative buff id used for cleanup.</param>
        public GameplayComponentModifierEntry(int modifierId, int attributeId, int addValue, int sourceBuffId = 0)
        {
            if (modifierId <= 0)
                throw new ArgumentOutOfRangeException(nameof(modifierId), "Gameplay component modifier id must be greater than zero.");
            if (attributeId <= 0)
                throw new ArgumentOutOfRangeException(nameof(attributeId), "Gameplay component modifier attribute id must be greater than zero.");
            if (sourceBuffId < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceBuffId), "Gameplay component modifier source buff id cannot be negative.");

            ModifierId = modifierId;
            AttributeId = attributeId;
            AddValue = addValue;
            SourceBuffId = sourceBuffId;
        }

        /// <summary>
        /// Gets the stable modifier id.
        /// </summary>
        public int ModifierId { get; }

        /// <summary>
        /// Gets the attribute id affected by this modifier.
        /// </summary>
        public int AttributeId { get; }

        /// <summary>
        /// Gets the additive value applied to the attribute.
        /// </summary>
        public int AddValue { get; }

        /// <summary>
        /// Gets the optional source buff id used for modifier cleanup.
        /// </summary>
        public int SourceBuffId { get; }

        /// <summary>
        /// Compares this entry with another entry.
        /// </summary>
        /// <param name="other">The entry to compare.</param>
        /// <returns><c>true</c> when all fields are equal.</returns>
        public bool Equals(GameplayComponentModifierEntry other)
        {
            return ModifierId == other.ModifierId
                && AttributeId == other.AttributeId
                && AddValue == other.AddValue
                && SourceBuffId == other.SourceBuffId;
        }

        /// <summary>
        /// Compares this entry with another object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns><c>true</c> when the object is an equal modifier entry.</returns>
        public override bool Equals(object obj)
        {
            return obj is GameplayComponentModifierEntry other && Equals(other);
        }

        /// <summary>
        /// Returns a hash code for this entry.
        /// </summary>
        /// <returns>A hash code built from all entry fields.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ModifierId;
                hash = (hash * 397) ^ AttributeId;
                hash = (hash * 397) ^ AddValue;
                hash = (hash * 397) ^ SourceBuffId;
                return hash;
            }
        }
    }
}
