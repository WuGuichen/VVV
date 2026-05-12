using System;
using System.Collections.Concurrent;
using System.Threading;

namespace MxFramework.Preview
{
    /// <summary>
    /// Posts callbacks from the RPC worker thread back onto the Unity main thread.
    /// MxPreviewBootstrap drives <see cref="Pump"/> from MonoBehaviour.Update.
    /// </summary>
    public sealed class PreviewMainThreadDispatcher
    {
        private readonly ConcurrentQueue<Item> _queue = new ConcurrentQueue<Item>();

        public bool ExecuteInline { get; set; }

        public void Pump()
        {
            while (_queue.TryDequeue(out Item item))
            {
                try { item.Action(); item.Done.Set(); }
                catch (Exception ex) { item.Error = ex; item.Done.Set(); }
            }
        }

        public void Invoke(Action action, int timeoutMs = 30000)
        {
            if (action == null) return;
            if (ExecuteInline)
            {
                action();
                return;
            }
            Item item = new Item { Action = action, Done = new ManualResetEventSlim(false) };
            _queue.Enqueue(item);
            if (!item.Done.Wait(timeoutMs))
                throw new TimeoutException("Preview main-thread dispatch timed out.");
            if (item.Error != null) throw item.Error;
        }

        private sealed class Item
        {
            public Action Action;
            public ManualResetEventSlim Done;
            public Exception Error;
        }
    }
}
