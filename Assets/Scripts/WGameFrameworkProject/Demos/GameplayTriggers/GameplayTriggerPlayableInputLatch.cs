namespace MxFramework.Demo.GameplayTriggers
{
    /// <summary>Pre-admission Unity input sample latch with fixed edge storage.</summary>
    internal sealed class GameplayTriggerPlayableInputLatch
    {
        internal const int EdgeCapacity = 8;

        private const byte CaptureEdge = 1 << 0;
        private const byte RestoreEdge = 1 << 1;
        private const byte ResetEdge = 1 << 2;

        private readonly ControlEdgeSample[] _edges =
            new ControlEdgeSample[EdgeCapacity];
        private int _head;
        private int _count;
        private int _heldMoveX;
        private int _heldMoveZ;
        private long _nextSampleSequence;

        internal int PendingEdgeCount => _count;

        /// <remarks>NoAlloc. A full edge ring rejects the whole sample, including held-state changes.</remarks>
        internal bool TrySubmit(in GameplayTriggerPlayableInput input)
        {
            byte edgeMask = CreateEdgeMask(in input);
            if (edgeMask != 0 && _count == EdgeCapacity)
                return false;

            long sampleSequence = _nextSampleSequence;
            _nextSampleSequence++;
            _heldMoveX = input.MoveX;
            _heldMoveZ = input.MoveZ;
            if (edgeMask == 0)
                return true;

            int tail = (_head + _count) % EdgeCapacity;
            _edges[tail] = new ControlEdgeSample(sampleSequence, edgeMask);
            _count++;
            return true;
        }

        /// <remarks>NoAlloc. Held movement is returned every call; at most one queued edge is peeked.</remarks>
        internal void Peek(
            out int moveX,
            out int moveZ,
            out int controlCommandId,
            out long controlSampleSequence)
        {
            moveX = _heldMoveX;
            moveZ = _heldMoveZ;
            if (_count == 0)
            {
                controlCommandId = 0;
                controlSampleSequence = -1L;
                return;
            }

            ControlEdgeSample sample = _edges[_head];
            controlCommandId = SelectControl(sample.EdgeMask);
            controlSampleSequence = sample.SampleSequence;
        }

        /// <remarks>NoAlloc. Call only after the corresponding Move plus control batch is admitted.</remarks>
        internal void CommitPeekedEdge()
        {
            if (_count == 0)
                return;

            _edges[_head] = default;
            _head = (_head + 1) % EdgeCapacity;
            _count--;
        }

        /// <remarks>NoAlloc. Restore and Reset clear all presentation-owned pre-admission state.</remarks>
        internal void Clear()
        {
            for (int i = 0; i < _edges.Length; i++)
                _edges[i] = default;
            _head = 0;
            _count = 0;
            _heldMoveX = 0;
            _heldMoveZ = 0;
            _nextSampleSequence = 0L;
        }

        private static byte CreateEdgeMask(in GameplayTriggerPlayableInput input)
        {
            byte mask = 0;
            if (input.Capture)
                mask |= CaptureEdge;
            if (input.Restore)
                mask |= RestoreEdge;
            if (input.Reset)
                mask |= ResetEdge;
            return mask;
        }

        private static int SelectControl(byte edgeMask)
        {
            if ((edgeMask & ResetEdge) != 0)
                return GameplayTriggerPlayableIds.ResetCommand;
            if ((edgeMask & RestoreEdge) != 0)
                return GameplayTriggerPlayableIds.RestoreCommand;
            return (edgeMask & CaptureEdge) != 0
                ? GameplayTriggerPlayableIds.CaptureCommand
                : 0;
        }

        private readonly struct ControlEdgeSample
        {
            internal ControlEdgeSample(long sampleSequence, byte edgeMask)
            {
                SampleSequence = sampleSequence;
                EdgeMask = edgeMask;
            }

            internal long SampleSequence { get; }
            internal byte EdgeMask { get; }
        }
    }
}
