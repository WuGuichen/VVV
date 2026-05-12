using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public sealed class GameplayCommandExecutionState
    {
        private readonly HashSet<RuntimeCommand> _handled = new HashSet<RuntimeCommand>();

        public int HandledCount => _handled.Count;

        public bool MarkHandled(RuntimeCommand command)
        {
            return _handled.Add(command);
        }

        public bool IsHandled(RuntimeCommand command)
        {
            return _handled.Contains(command);
        }

        public void Clear()
        {
            _handled.Clear();
        }

    }
}
