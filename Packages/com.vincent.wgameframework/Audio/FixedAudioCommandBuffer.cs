using System;

namespace MxFramework.Audio
{
    /// <summary>Fixed-capacity, stable-order command buffer for the gameplay-to-presentation boundary.</summary>
    public sealed class FixedAudioCommandBuffer
    {
        private readonly AudioCommand[] _commands;
        private int _count;
        private int _droppedCount;

        /// <remarks>AllocByDesign during initialization; enqueue and clear are NoAlloc.</remarks>
        public FixedAudioCommandBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _commands = new AudioCommand[capacity];
        }

        public int Capacity => _commands.Length;
        public int Count => _count;
        public int DroppedCount => _droppedCount;
        public AudioCommand this[int index] => index >= 0 && index < _count
            ? _commands[index]
            : throw new ArgumentOutOfRangeException(nameof(index));

        /// <remarks>NoAlloc.</remarks>
        public bool TryEnqueue(in AudioCommand command)
        {
            if (_count == _commands.Length)
            {
                _droppedCount++;
                return false;
            }

            _commands[_count++] = command;
            return true;
        }

        /// <remarks>NoAlloc.</remarks>
        public void Clear()
        {
            Array.Clear(_commands, 0, _count);
            _count = 0;
        }

        public void ResetDroppedCount()
        {
            _droppedCount = 0;
        }
    }
}
