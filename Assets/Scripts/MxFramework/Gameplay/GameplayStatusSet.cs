using System.Collections;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    public sealed class GameplayStatusSet : IEnumerable<GameplayStatusId>
    {
        private readonly SortedSet<GameplayStatusId> _ids = new SortedSet<GameplayStatusId>();

        public int Count => _ids.Count;

        public bool Add(GameplayStatusId id)
        {
            return id.IsValid && _ids.Add(id);
        }

        public bool Remove(GameplayStatusId id)
        {
            return id.IsValid && _ids.Remove(id);
        }

        public bool Contains(GameplayStatusId id)
        {
            return id.IsValid && _ids.Contains(id);
        }

        public void Clear()
        {
            _ids.Clear();
        }

        public GameplayStatusId[] ToArray()
        {
            var copy = new GameplayStatusId[_ids.Count];
            _ids.CopyTo(copy);
            return copy;
        }

        public IEnumerator<GameplayStatusId> GetEnumerator()
        {
            return _ids.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
