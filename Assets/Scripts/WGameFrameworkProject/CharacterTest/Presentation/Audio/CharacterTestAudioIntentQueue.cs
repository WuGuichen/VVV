using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestAudioIntentQueue
    {
        private readonly CharacterTestAudioIntent[] _items;

        public CharacterTestAudioIntentQueue(int capacity)
        {
            if (capacity <= 0)
                capacity = 1;

            _items = new CharacterTestAudioIntent[capacity];
        }

        public int Count { get; private set; }
        public int DroppedCount { get; private set; }
        public int Capacity => _items.Length;

        public CharacterTestAudioIntent this[int index] => _items[index];

        public void Clear()
        {
            Count = 0;
            DroppedCount = 0;
        }

        public bool Enqueue(in CharacterTestAudioIntent intent)
        {
            if (Count >= _items.Length)
            {
                DroppedCount++;
                return false;
            }

            _items[Count++] = intent;
            return true;
        }
    }
}
