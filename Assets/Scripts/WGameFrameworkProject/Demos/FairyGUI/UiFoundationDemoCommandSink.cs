using System;
using MxFramework.UI;

namespace MxFramework.Demo.FairyGui
{
    public sealed class UiFoundationDemoCommandSink : IMxUiCommandSink
    {
        private readonly Action<string> _handler;

        public UiFoundationDemoCommandSink(Action<string> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public MxUiCommand LastCommand { get; private set; }
        public int AcceptedCount { get; private set; }

        public void Enqueue(MxUiCommand command)
        {
            LastCommand = command;
            if (command.SourceViewId != UiFoundationDemoFairyGuiIds.ViewId)
                return;

            AcceptedCount++;
            _handler(command.CommandId);
        }
    }
}
