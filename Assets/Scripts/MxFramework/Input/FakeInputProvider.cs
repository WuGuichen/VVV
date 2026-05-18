using System;

namespace MxFramework.Input
{
    public sealed class FakeInputProvider : IInputProvider
    {
        private readonly InputCommandQueue _commands = new InputCommandQueue();
        private readonly InputContextStack _contexts = new InputContextStack();

        public InputSnapshot Snapshot { get; private set; }
        public InputCommandQueue Commands => _commands;
        public InputContext CurrentContext => _contexts.ActiveContext;

        public bool IsContextEnabled(InputContext context)
        {
            return _contexts.IsContextEnabled(context);
        }

        public void SetSnapshot(InputSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public InputCommand Enqueue(InputCommand command)
        {
            return _commands.Enqueue(command);
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
