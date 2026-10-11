using System;
using System.Collections.Generic;

namespace MxFramework.AI
{
    public enum AiBlackboardFactChangeKind
    {
        Set = 0,
        Removed = 1,
        Cleared = 2
    }

    public readonly struct AiBlackboardFactChangedEvent
    {
        public AiBlackboardFactChangedEvent(
            IAiBlackboard source,
            AiFactKey key,
            AiBlackboardFactChangeKind kind,
            bool hadPreviousValue,
            object previousValue,
            bool hasCurrentValue,
            object currentValue)
        {
            Source = source;
            Key = key;
            Kind = kind;
            HadPreviousValue = hadPreviousValue;
            PreviousValue = previousValue;
            HasCurrentValue = hasCurrentValue;
            CurrentValue = currentValue;
        }

        public IAiBlackboard Source { get; }
        public AiFactKey Key { get; }
        public AiBlackboardFactChangeKind Kind { get; }
        public bool HadPreviousValue { get; }
        public object PreviousValue { get; }
        public bool HasCurrentValue { get; }
        public object CurrentValue { get; }
    }

    public interface IReadOnlyAiBlackboard
    {
        IReadOnlyAiBlackboard Parent { get; }
        bool ContainsLocal(AiFactKey key);
        bool Contains(AiFactKey key);
        bool TryGetValue<T>(AiFactKey key, out T value);
        object GetRawValue(AiFactKey key);
        IReadOnlyDictionary<AiFactKey, object> SnapshotLocal();
        IReadOnlyDictionary<AiFactKey, object> SnapshotFlattened();
    }

    public interface IAiBlackboard : IReadOnlyAiBlackboard, IAiWorldState
    {
        event Action<AiBlackboardFactChangedEvent> FactChanged;

        bool RemoveLocal(AiFactKey key);
        void ClearLocal();
        AiWorldState ToWorldState();
    }

    public class AiBlackboard : IAiBlackboard
    {
        private readonly Dictionary<AiFactKey, object> _facts;

        public AiBlackboard(IReadOnlyAiBlackboard parent = null)
        {
            Parent = parent;
            _facts = new Dictionary<AiFactKey, object>();
        }

        public event Action<AiBlackboardFactChangedEvent> FactChanged;

        public IReadOnlyAiBlackboard Parent { get; }

        public bool ContainsLocal(AiFactKey key)
        {
            return key.IsValid && _facts.ContainsKey(key);
        }

        public bool Contains(AiFactKey key)
        {
            return ContainsLocal(key) || (Parent != null && Parent.Contains(key));
        }

        public bool TryGetValue<T>(AiFactKey key, out T value)
        {
            value = default;
            if (!key.IsValid)
                return false;

            if (_facts.TryGetValue(key, out object raw) && raw is T localValue)
            {
                value = localValue;
                return true;
            }

            return Parent != null && Parent.TryGetValue(key, out value);
        }

        public object GetRawValue(AiFactKey key)
        {
            if (!key.IsValid)
                return null;

            if (_facts.TryGetValue(key, out object raw))
                return raw;

            return Parent != null ? Parent.GetRawValue(key) : null;
        }

        public void SetValue<T>(AiFactKey key, T value)
        {
            if (!key.IsValid)
                throw new ArgumentException("Runtime AI Planner blackboard fact key is invalid.", nameof(key));

            bool hadPrevious = TryGetRawValue(key, out object previous);
            _facts[key] = value;
            FactChanged?.Invoke(new AiBlackboardFactChangedEvent(
                this,
                key,
                AiBlackboardFactChangeKind.Set,
                hadPrevious,
                previous,
                hasCurrentValue: true,
                currentValue: value));
        }

        public bool Remove(AiFactKey key)
        {
            return RemoveLocal(key);
        }

        public bool RemoveLocal(AiFactKey key)
        {
            if (!key.IsValid || !_facts.ContainsKey(key))
                return false;

            bool hadPrevious = TryGetRawValue(key, out object previous);
            _facts.Remove(key);
            bool hasCurrent = TryGetRawValue(key, out object current);
            FactChanged?.Invoke(new AiBlackboardFactChangedEvent(
                this,
                key,
                AiBlackboardFactChangeKind.Removed,
                hadPrevious,
                previous,
                hasCurrent,
                current));
            return true;
        }

        public void ClearLocal()
        {
            if (_facts.Count == 0)
                return;

            var keys = new List<AiFactKey>(_facts.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                AiFactKey key = keys[i];
                bool hadPrevious = TryGetRawValue(key, out object previous);
                _facts.Remove(key);
                bool hasCurrent = TryGetRawValue(key, out object current);
                FactChanged?.Invoke(new AiBlackboardFactChangedEvent(
                    this,
                    key,
                    AiBlackboardFactChangeKind.Cleared,
                    hadPrevious,
                    previous,
                    hasCurrent,
                    current));
            }
        }

        public IAiWorldState Clone()
        {
            return ToWorldState();
        }

        public IReadOnlyDictionary<AiFactKey, object> Snapshot()
        {
            return SnapshotFlattened();
        }

        public IReadOnlyDictionary<AiFactKey, object> SnapshotLocal()
        {
            return new Dictionary<AiFactKey, object>(_facts);
        }

        public IReadOnlyDictionary<AiFactKey, object> SnapshotFlattened()
        {
            var snapshot = Parent != null
                ? new Dictionary<AiFactKey, object>(Parent.SnapshotFlattened())
                : new Dictionary<AiFactKey, object>();
            foreach (KeyValuePair<AiFactKey, object> fact in _facts)
                snapshot[fact.Key] = fact.Value;
            return snapshot;
        }

        public AiWorldState ToWorldState()
        {
            var worldState = new AiWorldState();
            IReadOnlyDictionary<AiFactKey, object> snapshot = SnapshotFlattened();
            foreach (KeyValuePair<AiFactKey, object> fact in snapshot)
                worldState.SetValue(fact.Key, fact.Value);
            return worldState;
        }

        private bool TryGetRawValue(AiFactKey key, out object value)
        {
            value = GetRawValue(key);
            return key.IsValid && (ContainsLocal(key) || (Parent != null && Parent.Contains(key)));
        }
    }

    public sealed class SharedAiBlackboard : AiBlackboard
    {
        public SharedAiBlackboard()
            : base(parent: null)
        {
        }
    }
}
