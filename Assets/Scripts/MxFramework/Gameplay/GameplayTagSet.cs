using System.Collections;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    public sealed class GameplayTagSet : IEnumerable<GameplayTagId>
    {
        private readonly SortedSet<GameplayTagId> _ids = new SortedSet<GameplayTagId>();

        public int Count => _ids.Count;

        public bool Add(GameplayTagId id)
        {
            return id.IsValid && _ids.Add(id);
        }

        public bool Remove(GameplayTagId id)
        {
            return id.IsValid && _ids.Remove(id);
        }

        public bool Contains(GameplayTagId id)
        {
            return id.IsValid && _ids.Contains(id);
        }

        public void Clear()
        {
            _ids.Clear();
        }

        public GameplayTagId[] ToArray()
        {
            var copy = new GameplayTagId[_ids.Count];
            _ids.CopyTo(copy);
            return copy;
        }

        public IEnumerator<GameplayTagId> GetEnumerator()
        {
            return _ids.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
