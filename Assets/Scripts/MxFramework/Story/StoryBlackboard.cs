using System;
using System.Collections.Generic;

namespace MxFramework.Story
{
    public interface IStoryBlackboard
    {
        int Count { get; }
        bool TryGet(in StoryFactKey key, out StoryValue value);
        void Set(in StoryFactKey key, in StoryValue value);
        bool Remove(in StoryFactKey key);
        StoryFactCopyResult CopyOrdered(Span<StoryFactEntry> buffer);
    }

    public sealed class StoryBlackboard : IStoryBlackboard
    {
        private readonly Dictionary<StoryFactKey, StoryValue> _facts = new Dictionary<StoryFactKey, StoryValue>();
        private StoryFactEntry[] _orderedCache = Array.Empty<StoryFactEntry>();
        private bool _dirty = true;

        public int Count => _facts.Count;

        public bool TryGet(in StoryFactKey key, out StoryValue value)
        {
            if (!key.IsValid)
            {
                value = default;
                return false;
            }

            return _facts.TryGetValue(key, out value);
        }

        public void Set(in StoryFactKey key, in StoryValue value)
        {
            ValidateKey(key);
            _facts[key] = value;
            _dirty = true;
        }

        public bool Remove(in StoryFactKey key)
        {
            if (!key.IsValid)
            {
                return false;
            }

            bool removed = _facts.Remove(key);
            if (removed)
            {
                _dirty = true;
            }

            return removed;
        }

        public void Clear()
        {
            if (_facts.Count == 0)
            {
                return;
            }

            _facts.Clear();
            _orderedCache = Array.Empty<StoryFactEntry>();
            _dirty = false;
        }

        public StoryFactEntry[] CreateOrderedSnapshot()
        {
            EnsureOrderedCache();
            var copy = new StoryFactEntry[_orderedCache.Length];
            Array.Copy(_orderedCache, copy, _orderedCache.Length);
            return copy;
        }

        public StoryFactCopyResult CopyOrdered(Span<StoryFactEntry> buffer)
        {
            EnsureOrderedCache();
            int required = _orderedCache.Length;
            int written = Math.Min(required, buffer.Length);
            for (int i = 0; i < written; i++)
            {
                buffer[i] = _orderedCache[i];
            }

            return new StoryFactCopyResult(required, written, written == required);
        }

        private void EnsureOrderedCache()
        {
            if (!_dirty)
            {
                return;
            }

            if (_facts.Count == 0)
            {
                _orderedCache = Array.Empty<StoryFactEntry>();
                _dirty = false;
                return;
            }

            var entries = new StoryFactEntry[_facts.Count];
            int index = 0;
            foreach (KeyValuePair<StoryFactKey, StoryValue> pair in _facts)
            {
                entries[index++] = new StoryFactEntry(pair.Key, pair.Value);
            }

            Array.Sort(entries, CompareEntries);
            _orderedCache = entries;
            _dirty = false;
        }

        private static int CompareEntries(StoryFactEntry left, StoryFactEntry right)
        {
            return left.Key.CompareTo(right.Key);
        }

        private static void ValidateKey(in StoryFactKey key)
        {
            if (!key.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(key), "Story fact key requires Namespace >= 0 and Id > 0.");
            }
        }
    }
}
