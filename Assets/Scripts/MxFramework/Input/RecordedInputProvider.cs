using System;
using System.Collections.Generic;

namespace MxFramework.Input
{
    public sealed class RecordedInputProvider : IInputProvider
    {
        private readonly IReadOnlyList<InputSnapshot> _snapshots;
        private readonly InputCommandQueue _commands = new InputCommandQueue();
        private readonly InputContextStack _contexts = new InputContextStack();
        private int _index;

        public RecordedInputProvider(IReadOnlyList<InputSnapshot> snapshots)
        {
            _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
            Snapshot = _snapshots.Count > 0 ? _snapshots[0] : InputSnapshot.Empty;
        }

        public InputSnapshot Snapshot { get; private set; }
        public InputCommandQueue Commands => _commands;
        public InputContext CurrentContext => _contexts.ActiveContext;
        public int Index => _index;

        public bool IsContextEnabled(InputContext context)
        {
            return _contexts.IsContextEnabled(context);
        }

        public bool Advance()
        {
            if (_snapshots.Count == 0)
            {
                Snapshot = InputSnapshot.Empty;
                return false;
            }

            if (_index >= _snapshots.Count)
            {
                Snapshot = _snapshots[_snapshots.Count - 1];
                return false;
            }

            Snapshot = _snapshots[_index];
            _index++;
            return true;
        }

        public void Rewind()
        {
            _index = 0;
            Snapshot = _snapshots.Count > 0 ? _snapshots[0] : InputSnapshot.Empty;
            _commands.Reset();
        }

        public void SetContext(InputContext context)
        {
            _contexts.Set(context);
        }

        public IDisposable PushContext(InputContext context, InputContextPolicy policy = InputContextPolicy.Exclusive)
        {
            return _contexts.Push(context, policy);
        }
    }
}
