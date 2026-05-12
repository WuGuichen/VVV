using System;

namespace MxFramework.Input
{
    public interface IInputProvider
    {
        InputSnapshot Snapshot { get; }
        InputCommandQueue Commands { get; }
        InputContext CurrentContext { get; }

        void SetContext(InputContext context);
        IDisposable PushContext(InputContext context, InputContextPolicy policy = InputContextPolicy.Exclusive);
    }
}
