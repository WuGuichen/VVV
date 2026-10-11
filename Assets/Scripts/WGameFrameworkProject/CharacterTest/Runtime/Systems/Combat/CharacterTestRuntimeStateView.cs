using System;
using System.Collections;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Indexed view used only during synchronous hashing; never retained as a rollback snapshot.</summary>
    internal sealed class CharacterTestRuntimeStateView<T> : IReadOnlyList<T>
    {
        private readonly Func<int> _count;
        private readonly Func<int, T> _read;
        internal CharacterTestRuntimeStateView(Func<int> count, Func<int, T> read)
        { _count = count; _read = read; }
        public int Count => _count();
        public T this[int index] => _read(index);
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < Count; i++) yield return this[i];
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
