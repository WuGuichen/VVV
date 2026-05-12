using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    public interface IAbilityGraphEffectResolver
    {
        bool TryResolve(int effectId, out IAbilityEffect effect);
    }

    /// <summary>Deterministic in-memory effect registry keyed by graph effect id.</summary>
    public sealed class AbilityGraphRuntimeEffectRegistry : IAbilityGraphEffectResolver
    {
        public const string InvalidEffectIdFailureReason = "InvalidEffectId";
        public const string NullEffectFailureReason = "NullEffect";
        public const string DuplicateEffectIdFailureReason = "DuplicateEffectId";

        private readonly SortedDictionary<int, IAbilityEffect> _effects = new SortedDictionary<int, IAbilityEffect>();

        public int Count => _effects.Count;

        public bool TryRegister(int effectId, IAbilityEffect effect, out string failureReason)
        {
            if (effectId <= 0)
            {
                failureReason = InvalidEffectIdFailureReason;
                return false;
            }

            if (effect == null)
            {
                failureReason = NullEffectFailureReason;
                return false;
            }

            if (_effects.ContainsKey(effectId))
            {
                failureReason = DuplicateEffectIdFailureReason;
                return false;
            }

            _effects.Add(effectId, effect);
            failureReason = null;
            return true;
        }

        public bool TryResolve(int effectId, out IAbilityEffect effect)
        {
            if (effectId <= 0)
            {
                effect = null;
                return false;
            }

            return _effects.TryGetValue(effectId, out effect);
        }

        public IReadOnlyList<int> GetEffectIds()
        {
            if (_effects.Count == 0)
                return Array.Empty<int>();

            int[] ids = new int[_effects.Count];
            int index = 0;
            foreach (KeyValuePair<int, IAbilityEffect> effect in _effects)
                ids[index++] = effect.Key;

            return ids;
        }

        public void Clear()
        {
            _effects.Clear();
        }
    }
}
