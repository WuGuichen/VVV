using System;
using System.Collections.Generic;

namespace MxFramework.AI
{
    public sealed class AiWorldState : IAiWorldState
    {
        private readonly Dictionary<AiFactKey, object> _facts;

        public AiWorldState()
        {
            _facts = new Dictionary<AiFactKey, object>();
        }

        private AiWorldState(Dictionary<AiFactKey, object> facts)
        {
            _facts = new Dictionary<AiFactKey, object>(facts);
        }

        public bool Contains(AiFactKey key)
        {
            return key.IsValid && _facts.ContainsKey(key);
        }

        public bool TryGetValue<T>(AiFactKey key, out T value)
        {
            value = default;
            if (!key.IsValid || !_facts.TryGetValue(key, out object raw))
                return false;

            if (raw is T typed)
            {
                value = typed;
                return true;
            }

            return false;
        }

        public object GetRawValue(AiFactKey key)
        {
            if (!key.IsValid)
                return null;

            return _facts.TryGetValue(key, out object raw) ? raw : null;
        }

        public void SetValue<T>(AiFactKey key, T value)
        {
            if (!key.IsValid)
                throw new ArgumentException("AI fact key is invalid.", nameof(key));

            _facts[key] = value;
        }

        public bool Remove(AiFactKey key)
        {
            return key.IsValid && _facts.Remove(key);
        }

        public IAiWorldState Clone()
        {
            return new AiWorldState(_facts);
        }

        public IReadOnlyDictionary<AiFactKey, object> Snapshot()
        {
            return new Dictionary<AiFactKey, object>(_facts);
        }
    }
}
