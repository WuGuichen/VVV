using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Allocation-free composite cooldown key per character and card/group identity.</summary>
    public readonly struct TacticalCooldownKey : IEquatable<TacticalCooldownKey>
    {
        public int CharacterId { get; }
        public string Identifier { get; }

        public TacticalCooldownKey(int characterId, string identifier)
        {
            CharacterId = characterId;
            Identifier = identifier ?? string.Empty;
        }

        public bool Equals(TacticalCooldownKey other)
            => CharacterId == other.CharacterId && string.Equals(Identifier, other.Identifier, StringComparison.Ordinal);

        public override bool Equals(object obj)
            => obj is TacticalCooldownKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (CharacterId * 397) ^ (Identifier != null ? StringComparer.Ordinal.GetHashCode(Identifier) : 0);
            }
        }
    }

    /// <summary>
    /// Runtime cooldown tracker for approved pure point-move mechanisms.
    /// Physical-action cooldowns remain owned by the actor's Ability runtime.
    /// Manages per-character cooldown state with zero runtime allocations.
    /// </summary>
    public sealed partial class TacticalCooldownTracker
    {
        private readonly Dictionary<TacticalCooldownKey, long> _endTicks = new Dictionary<TacticalCooldownKey, long>();
        private readonly float _tickRate;

        public const float DefaultTickRate = 60f;

        public TacticalCooldownTracker(float tickRate = DefaultTickRate)
        {
            if (float.IsNaN(tickRate) || float.IsInfinity(tickRate) || tickRate <= 0f)
                throw new ArgumentOutOfRangeException(nameof(tickRate), "Tick rate must be positive and finite.");
            _tickRate = tickRate;
        }

        public bool IsOnCooldown(int characterId, TacticalActionCardDefinition card, long currentTick)
        {
            if (card == null || !card.UsesCardCooldown) return false;

            var cardKey = new TacticalCooldownKey(characterId, card.Id);
            if (_endTicks.TryGetValue(cardKey, out long cardEnd) && currentTick < cardEnd)
                return true;

            if (!string.IsNullOrEmpty(card.SharedCooldownGroup))
            {
                var groupKey = new TacticalCooldownKey(characterId, card.SharedCooldownGroup);
                if (_endTicks.TryGetValue(groupKey, out long groupEnd) && currentTick < groupEnd)
                    return true;
            }

            return false;
        }

        public float GetRemainingCooldownSeconds(int characterId, TacticalActionCardDefinition card, long currentTick)
        {
            if (card == null || !card.UsesCardCooldown) return 0f;

            long maxEnd = 0;
            var cardKey = new TacticalCooldownKey(characterId, card.Id);
            if (_endTicks.TryGetValue(cardKey, out long cardEnd) && cardEnd > maxEnd)
                maxEnd = cardEnd;

            if (!string.IsNullOrEmpty(card.SharedCooldownGroup))
            {
                var groupKey = new TacticalCooldownKey(characterId, card.SharedCooldownGroup);
                if (_endTicks.TryGetValue(groupKey, out long groupEnd) && groupEnd > maxEnd)
                    maxEnd = groupEnd;
            }

            if (maxEnd <= currentTick) return 0f;
            return (float)(maxEnd - currentTick) / _tickRate;
        }

        public void TriggerCooldown(int characterId, TacticalActionCardDefinition card, long currentTick)
        {
            if (card == null || !card.UsesCardCooldown) return;

            long durationTicks = (long)Math.Ceiling(card.CooldownSeconds * _tickRate);
            if (durationTicks <= 0) return;

            long endTick = currentTick + durationTicks;
            var cardKey = new TacticalCooldownKey(characterId, card.Id);
            _endTicks[cardKey] = endTick;

            if (!string.IsNullOrEmpty(card.SharedCooldownGroup))
            {
                var groupKey = new TacticalCooldownKey(characterId, card.SharedCooldownGroup);
                _endTicks[groupKey] = endTick;
            }
        }

        public void ClearCooldown(int characterId, TacticalActionCardDefinition card)
        {
            if (card == null) return;
            _endTicks.Remove(new TacticalCooldownKey(characterId, card.Id));
            if (!string.IsNullOrEmpty(card.SharedCooldownGroup))
                _endTicks.Remove(new TacticalCooldownKey(characterId, card.SharedCooldownGroup));
        }

        public void Reset()
        {
            _endTicks.Clear();
        }
    }
}
