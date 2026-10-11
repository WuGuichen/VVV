using System;
using MxFramework.ApplicationStartup.Unity;

namespace MxFramework.ApplicationStartup.Unity.Validation
{
    /// <summary>Bounded FIFO owned by one Validation surface.</summary>
    internal sealed class ApplicationUnityStartupRequestQueue
    {
        private readonly ApplicationUnityStartupSurfaceRequest[] _items;
        private int _head;
        private int _count;
        private long _nextRequestId;

        /// <remarks>LowFreqAlloc. Allocates the fixed request storage.</remarks>
        internal ApplicationUnityStartupRequestQueue(int capacity)
            : this(capacity, 0L)
        {
        }

        internal ApplicationUnityStartupRequestQueue(int capacity, long initialRequestId)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Request capacity must be positive.");
            if (initialRequestId < 0L)
                throw new ArgumentOutOfRangeException(nameof(initialRequestId), initialRequestId, "Initial request id cannot be negative.");

            _items = new ApplicationUnityStartupSurfaceRequest[capacity];
            _nextRequestId = initialRequestId;
        }

        internal int Count => _count;
        internal int Capacity => _items.Length;
        internal long DroppedRequestCount { get; private set; }

        /// <remarks>NoAlloc.</remarks>
        internal bool TryEnqueue(ApplicationUnityStartupRequestKind kind)
        {
            if (kind != ApplicationUnityStartupRequestKind.Start &&
                kind != ApplicationUnityStartupRequestKind.Retry &&
                kind != ApplicationUnityStartupRequestKind.Cancel)
                return false;

            if (_count == _items.Length || _nextRequestId == long.MaxValue)
            {
                RecordDrop();
                return false;
            }

            long requestId = _nextRequestId + 1L;
            int tail = (_head + _count) % _items.Length;
            _items[tail] = new ApplicationUnityStartupSurfaceRequest(requestId, kind);
            _nextRequestId = requestId;
            _count++;
            return true;
        }

        /// <remarks>NoAlloc.</remarks>
        internal bool TryDequeue(out ApplicationUnityStartupSurfaceRequest request)
        {
            if (_count == 0)
            {
                request = default;
                return false;
            }

            request = _items[_head];
            _items[_head] = default;
            _head = (_head + 1) % _items.Length;
            _count--;
            return true;
        }

        private void RecordDrop()
        {
            if (DroppedRequestCount != long.MaxValue)
                DroppedRequestCount++;
        }
    }
}
