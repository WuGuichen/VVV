using System;

namespace MxFramework.Input
{
    public interface IInputProvider
    {
        InputSnapshot Snapshot { get; }
        InputCommandQueue Commands { get; }
        InputContext CurrentContext { get; }

        bool ContainsContext(InputContext context);
        bool IsContextEnabled(InputContext context);
        void SetContext(InputContext context);
        IDisposable PushContext(InputContext context, InputContextPolicy policy = InputContextPolicy.Exclusive);
    }
}
