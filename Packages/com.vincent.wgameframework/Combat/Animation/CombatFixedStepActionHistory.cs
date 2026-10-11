using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Animation
{
    internal sealed class CombatFixedStepActionHistory
    {
        private readonly List<CombatActionStepSnapshot> _snapshots = new List<CombatActionStepSnapshot>();

        public long RuntimeFrameIndex { get; private set; } = -1L;

        public IReadOnlyList<CombatActionStepSnapshot> Snapshots => _snapshots;

        public void BeginFrame(long runtimeFrameIndex)
        {
            if (runtimeFrameIndex < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(runtimeFrameIndex), "Runtime frame index cannot be negative.");
            }

            RuntimeFrameIndex = runtimeFrameIndex;
            _snapshots.Clear();
        }

        public void AddStep(CombatFrame frame, CombatActionState[] actionStates)
        {
            if (actionStates == null)
            {
                throw new ArgumentNullException(nameof(actionStates));
            }

            _snapshots.Add(new CombatActionStepSnapshot(frame, actionStates));
        }

        public bool TryGetSnapshots(long runtimeFrameIndex, out IReadOnlyList<CombatActionStepSnapshot> snapshots)
        {
            if (RuntimeFrameIndex == runtimeFrameIndex)
            {
                snapshots = _snapshots;
                return true;
            }

            snapshots = Array.Empty<CombatActionStepSnapshot>();
            return false;
        }
    }

    internal readonly struct CombatActionStepSnapshot
    {
        private readonly CombatActionState[] _actionStates;

        public CombatActionStepSnapshot(CombatFrame frame, IReadOnlyList<CombatActionState> actionStates)
        {
            if (actionStates == null)
            {
                throw new ArgumentNullException(nameof(actionStates));
            }

            Frame = frame;
            _actionStates = new CombatActionState[actionStates.Count];
            for (int i = 0; i < actionStates.Count; i++)
            {
                _actionStates[i] = actionStates[i];
            }
        }

        public CombatFrame Frame { get; }

        public IReadOnlyList<CombatActionState> ActionStates => _actionStates ?? Array.Empty<CombatActionState>();
    }
}
