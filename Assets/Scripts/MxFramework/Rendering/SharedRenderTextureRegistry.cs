using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace MxFramework.Rendering
{
    public interface ISharedRenderTextureRegistry
    {
        SharedRTHandle Register(in SharedRenderTextureKey key);
        bool Unregister(SharedRTHandle handle);
        bool RegisterWriterSet(SharedRTWriterSetId id, IReadOnlyList<SharedRTOwnerId> allowedWriters);
        bool TryResolve(in SharedRenderTextureKey key, out RTHandle handle);
        bool TryResolve(SharedRTHandle handle, out RTHandle rtHandle);
        SharedRTDiagnosticsSnapshot CaptureDiagnostics();
    }

    internal interface IRenderingTestHooks
    {
        void AdvanceTestTime(float deltaTime);
        void ForceSharedRTConflict(SharedRTConflictCode code);
    }

    public sealed class SharedRenderTextureRegistry : ISharedRenderTextureRegistry, IRenderingTestHooks, IDisposable
    {
        private readonly Dictionary<SharedRTId, Entry> _entriesById = new Dictionary<SharedRTId, Entry>();
        private readonly Dictionary<SharedRTHandle, Entry> _entriesByHandle = new Dictionary<SharedRTHandle, Entry>();
        private readonly Dictionary<SharedRTWriterSetId, HashSet<SharedRTOwnerId>> _writerSets = new Dictionary<SharedRTWriterSetId, HashSet<SharedRTOwnerId>>();
        private readonly List<SharedRTConflictEvent> _recentConflicts = new List<SharedRTConflictEvent>();
        private readonly SharedRenderTextureRegistryOptions _options;

        private RTHandle _fallbackHandle;
        private int _nextHandleValue = 1;
        private int _frameIndex;
        private float _timeSeconds;
        private bool _disposed;

        public SharedRenderTextureRegistry()
            : this(null)
        {
        }

        public SharedRenderTextureRegistry(SharedRenderTextureRegistryOptions options)
        {
            _options = options ?? new SharedRenderTextureRegistryOptions();
            if (_options.OrphanFrameThreshold < 1)
                _options.OrphanFrameThreshold = 1;
            if (_options.ResizeBurstThresholdPerSecond < 1)
                _options.ResizeBurstThresholdPerSecond = 1;
        }

        public SharedRTHandle Register(in SharedRenderTextureKey key)
        {
            if (_disposed || !key.Id.IsValid)
                return SharedRTHandle.Invalid;

            if (_entriesById.TryGetValue(key.Id, out Entry existing))
            {
                if (!CanReRegisterExisting(existing, key))
                {
                    ReportConflict(existing, SharedRTConflictCode.UnauthorizedWriter, key.Id, key.Owner, "Owner is not authorized to re-register the existing SharedRT.");
                    return SharedRTHandle.Invalid;
                }

                if (existing.Key.Size != key.Size)
                    RequestResize(existing.Handle, key.Size);
                existing.Key = WithRuntimeStableFields(existing.Key, key);
                return existing.Handle;
            }

            if (!IsWriterAllowed(key.Access.WriterSetId, key.Owner))
            {
                ReportConflict(null, SharedRTConflictCode.UnauthorizedWriter, key.Id, key.Owner, "Owner is not in the registered SharedRT writer set.");
                return SharedRTHandle.Invalid;
            }

            var handle = new SharedRTHandle(_nextHandleValue++, 1);
            var entry = new Entry(key, handle);
            Allocate(entry, key.Size, true);
            _entriesById.Add(key.Id, entry);
            _entriesByHandle.Add(handle, entry);
            return handle;
        }

        public bool Unregister(SharedRTHandle handle)
        {
            if (!_entriesByHandle.TryGetValue(handle, out Entry entry))
                return false;

            ReleaseEntryAllocation(entry);
            _entriesByHandle.Remove(handle);
            _entriesById.Remove(entry.Key.Id);
            return true;
        }

        public bool RegisterWriterSet(SharedRTWriterSetId id, IReadOnlyList<SharedRTOwnerId> allowedWriters)
        {
            if (!id.IsValid || allowedWriters == null)
                return false;

            var writers = new HashSet<SharedRTOwnerId>();
            for (int i = 0; i < allowedWriters.Count; i++)
            {
                if (allowedWriters[i].IsValid)
                    writers.Add(allowedWriters[i]);
            }

            if (writers.Count == 0)
                return false;

            _writerSets[id] = writers;
            return true;
        }

        public bool TryResolve(in SharedRenderTextureKey key, out RTHandle handle)
        {
            if (_entriesById.TryGetValue(key.Id, out Entry entry))
                return TryResolve(entry.Handle, out handle);

            handle = null;
            return false;
        }

        public bool TryResolve(SharedRTHandle handle, out RTHandle rtHandle)
        {
            if (_entriesByHandle.TryGetValue(handle, out Entry entry))
            {
                rtHandle = entry.RtHandle ?? TryGetFallbackHandle();
                return rtHandle != null;
            }

            rtHandle = null;
            return false;
        }

        public SharedRTDiagnosticsSnapshot CaptureDiagnostics()
        {
            var entries = new List<SharedRTDiagnosticsEntry>(_entriesById.Count);
            long actualBytes = 0;

            foreach (Entry entry in _entriesById.Values)
            {
                actualBytes += entry.ActualMemoryBytes;
                entries.Add(new SharedRTDiagnosticsEntry(
                    entry.Key.Id,
                    entry.Key.DebugName,
                    entry.Key.Owner,
                    entry.Key.Format,
                    entry.CurrentSize,
                    entry.Key.Resize,
                    entry.EstimatedMemoryBytes,
                    entry.ActualMemoryBytes,
                    entry.IsAllocated,
                    entry.IsFallback,
                    entry.IsOrphaned,
                    entry.OrphanFrameCount,
                    ToArray(entry.CurrentReaders),
                    ToArray(entry.CurrentWriters),
                    entry.ResizeEvents.ToArray(),
                    entry.Conflicts.ToArray()));
            }

            return new SharedRTDiagnosticsSnapshot(entries, _recentConflicts.ToArray(), _options.MemoryBudgetBytes, actualBytes);
        }

        public void BeginFrame()
        {
            _frameIndex++;
            foreach (Entry entry in _entriesById.Values)
            {
                entry.CurrentReaders.Clear();
                entry.CurrentWriters.Clear();
                entry.LatestWriterOrder = null;
            }
        }

        public void EndFrame()
        {
            foreach (Entry entry in _entriesById.Values)
            {
                if (entry.CurrentReaders.Count == 0)
                {
                    entry.OrphanFrameCount++;
                    if (!entry.IsOrphaned && entry.OrphanFrameCount >= _options.OrphanFrameThreshold)
                    {
                        entry.IsOrphaned = true;
                        ReportConflict(entry, SharedRTConflictCode.OrphanRT, entry.Key.Id, entry.Key.Owner, "SharedRT has no readers for the configured orphan threshold.");
                        ReleaseEntryAllocation(entry);
                    }
                }
                else
                {
                    entry.OrphanFrameCount = 0;
                    entry.IsOrphaned = false;
                }
            }
        }

        public SharedRTAccessResult RecordWriter(SharedRTHandle handle, SharedRTOwnerId writer, MxRenderPhase phase, int order)
        {
            if (!_entriesByHandle.TryGetValue(handle, out Entry entry))
                return new SharedRTAccessResult(false, SharedRTConflictCode.None, handle, "Unknown SharedRT handle.");

            if (!IsWriterAllowed(entry.Key.Access.WriterSetId, writer))
            {
                ReportConflict(entry, SharedRTConflictCode.UnauthorizedWriter, entry.Key.Id, writer, "Writer is not in the registered SharedRT writer set.");
                return new SharedRTAccessResult(false, SharedRTConflictCode.UnauthorizedWriter, handle, "Writer is not authorized.");
            }

            bool newWriter = !entry.CurrentWriters.Contains(writer);
            if (newWriter && entry.CurrentWriters.Count > 0 && !entry.Key.Access.AllowAdditiveWriters)
            {
                ReportConflict(entry, SharedRTConflictCode.WriterConflict, entry.Key.Id, writer, "A second writer attempted to write a non-additive SharedRT in the same frame.");
                return new SharedRTAccessResult(false, SharedRTConflictCode.WriterConflict, handle, "Second writer rejected.");
            }

            entry.CurrentWriters.Add(writer);
            var frameOrder = new SharedRTFrameOrder(phase, order);
            if (!entry.LatestWriterOrder.HasValue || entry.LatestWriterOrder.Value.CompareTo(frameOrder) < 0)
                entry.LatestWriterOrder = frameOrder;

            if (newWriter && entry.CurrentWriters.Count > 1 && entry.Key.Access.AllowAdditiveWriters)
            {
                ReportConflict(entry, SharedRTConflictCode.AdditiveWritersAllowed, entry.Key.Id, writer, "Additional writer accepted by additive writer policy.");
                return new SharedRTAccessResult(true, SharedRTConflictCode.AdditiveWritersAllowed, handle, "Additive writer accepted.");
            }

            return new SharedRTAccessResult(true, SharedRTConflictCode.None, handle, string.Empty);
        }

        public SharedRTAccessResult RecordReader(SharedRTHandle handle, SharedRTOwnerId reader, MxRenderPhase phase, int order)
        {
            if (!_entriesByHandle.TryGetValue(handle, out Entry entry))
                return new SharedRTAccessResult(false, SharedRTConflictCode.None, handle, "Unknown SharedRT handle.");

            if (entry.Key.Access.Order == SharedRTOrderRule.ReadAfterWriteSameFrame)
            {
                var readerOrder = new SharedRTFrameOrder(phase, order);
                if (!entry.LatestWriterOrder.HasValue || readerOrder.CompareTo(entry.LatestWriterOrder.Value) <= 0)
                {
                    ReportConflict(entry, SharedRTConflictCode.StaleReader, entry.Key.Id, reader, "Reader attempted to consume current-frame data before the required writer order.");
                    return new SharedRTAccessResult(false, SharedRTConflictCode.StaleReader, handle, "Reader is stale for this frame.");
                }
            }

            if (reader.IsValid)
                entry.CurrentReaders.Add(reader);
            return new SharedRTAccessResult(true, SharedRTConflictCode.None, handle, string.Empty);
        }

        public SharedRTAccessResult RequestResize(SharedRTHandle handle, SharedRTSize requestedSize)
        {
            if (!_entriesByHandle.TryGetValue(handle, out Entry entry))
                return new SharedRTAccessResult(false, SharedRTConflictCode.None, handle, "Unknown SharedRT handle.");

            if (entry.CurrentSize == requestedSize)
                return new SharedRTAccessResult(true, SharedRTConflictCode.None, handle, string.Empty);

            SharedRTSize from = entry.CurrentSize;
            if (entry.Key.Resize == SharedRTResizePolicy.FailOnResize)
            {
                entry.ResizeEvents.Add(new SharedRTResizeEvent(entry.Key.Id, from, requestedSize, false, _frameIndex, _timeSeconds));
                ReportConflict(entry, SharedRTConflictCode.ResizeRejected, entry.Key.Id, entry.Key.Owner, "Resize rejected by FailOnResize policy.");
                return new SharedRTAccessResult(false, SharedRTConflictCode.ResizeRejected, handle, "Resize rejected.");
            }

            if (entry.Key.Resize == SharedRTResizePolicy.KeepLargest &&
                requestedSize.Width <= entry.CurrentSize.Width &&
                requestedSize.Height <= entry.CurrentSize.Height)
            {
                entry.ResizeEvents.Add(new SharedRTResizeEvent(entry.Key.Id, from, requestedSize, true, _frameIndex, _timeSeconds));
                return new SharedRTAccessResult(true, SharedRTConflictCode.None, handle, "Current allocation already satisfies requested size.");
            }

            ReleaseEntryAllocation(entry);
            bool allocated = Allocate(entry, requestedSize, false);
            entry.ResizeEvents.Add(new SharedRTResizeEvent(entry.Key.Id, from, requestedSize, allocated, _frameIndex, _timeSeconds));

            if (!allocated)
                return new SharedRTAccessResult(false, SharedRTConflictCode.DroppedAllocation, handle, "Resize allocation failed; fallback handle is active.");

            SharedRTConflictCode code = DetectResizeBurst(entry);
            return new SharedRTAccessResult(true, code, handle, string.Empty);
        }

        public void AdvanceTestTime(float deltaTime)
        {
            _timeSeconds += Math.Max(0f, deltaTime);
        }

        public void ForceSharedRTConflict(SharedRTConflictCode code)
        {
            ReportConflict(null, code, default, default, "Forced SharedRT conflict for diagnostics validation.");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            foreach (Entry entry in _entriesById.Values)
                ReleaseEntryAllocation(entry);
            _entriesById.Clear();
            _entriesByHandle.Clear();

            if (_fallbackHandle != null)
            {
                _fallbackHandle.Release();
                _fallbackHandle = null;
            }

            _disposed = true;
        }

        private static SharedRenderTextureKey WithRuntimeStableFields(SharedRenderTextureKey current, SharedRenderTextureKey requested)
        {
            return new SharedRenderTextureKey(
                current.Id,
                requested.DebugName,
                requested.Owner,
                requested.Access,
                requested.Anchor,
                requested.Format,
                current.Size,
                requested.Clear,
                requested.Resize,
                requested.EstimatedMemoryBytes);
        }

        private bool Allocate(Entry entry, SharedRTSize size, bool initialAllocation)
        {
            entry.CurrentSize = size;
            entry.EstimatedMemoryBytes = entry.Key.EstimatedMemoryBytes > 0 ? entry.Key.EstimatedMemoryBytes : EstimateMemoryBytes(size, entry.Key.Format);

            if (_options.SimulateAllocationFailure)
            {
                UseFallback(entry);
                ReportConflict(entry, SharedRTConflictCode.DroppedAllocation, entry.Key.Id, entry.Key.Owner, "SharedRT allocation failed; fallback handle is active.");
                return false;
            }

            try
            {
                entry.RtHandle = AllocateHandle(size, entry.Key.Format, entry.Key.DebugName);
                entry.ActualMemoryBytes = EstimateMemoryBytes(size, entry.Key.Format);
                entry.IsAllocated = entry.RtHandle != null;
                entry.IsFallback = !entry.IsAllocated;
                if (!entry.IsAllocated)
                {
                    UseFallback(entry);
                    ReportConflict(entry, SharedRTConflictCode.DroppedAllocation, entry.Key.Id, entry.Key.Owner, "SharedRT allocation returned null; fallback handle is active.");
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                UseFallback(entry);
                ReportConflict(entry, SharedRTConflictCode.DroppedAllocation, entry.Key.Id, entry.Key.Owner, "SharedRT allocation failed: " + exception.GetType().Name);
                return false;
            }
        }

        private RTHandle AllocateHandle(SharedRTSize size, SharedRTFormat format, string debugName)
        {
            DepthBits depthBits = format == SharedRTFormat.Depth ? DepthBits.Depth32 : DepthBits.None;
            GraphicsFormat colorFormat = ToGraphicsFormat(format);
            return RTHandles.Alloc(
                size.Width,
                size.Height,
                depthBufferBits: depthBits,
                colorFormat: colorFormat,
                filterMode: FilterMode.Point,
                wrapMode: TextureWrapMode.Clamp,
                name: string.IsNullOrWhiteSpace(debugName) ? "MxSharedRT" : debugName);
        }

        private void UseFallback(Entry entry)
        {
            entry.RtHandle = TryGetFallbackHandle();
            entry.ActualMemoryBytes = 0;
            entry.IsAllocated = false;
            entry.IsFallback = true;
        }

        private RTHandle TryGetFallbackHandle()
        {
            try
            {
                return GetFallbackHandle();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private RTHandle GetFallbackHandle()
        {
            return _fallbackHandle ?? (_fallbackHandle = RTHandles.Alloc(Texture2D.blackTexture));
        }

        private void ReleaseEntryAllocation(Entry entry)
        {
            if (entry.RtHandle != null && !entry.IsFallback)
                entry.RtHandle.Release();
            entry.RtHandle = null;
            entry.ActualMemoryBytes = 0;
            entry.IsAllocated = false;
        }

        private bool IsWriterAllowed(SharedRTWriterSetId writerSetId, SharedRTOwnerId writer)
        {
            return writerSetId.IsValid &&
                writer.IsValid &&
                _writerSets.TryGetValue(writerSetId, out HashSet<SharedRTOwnerId> writers) &&
                writers.Contains(writer);
        }

        private bool CanReRegisterExisting(Entry existing, in SharedRenderTextureKey requested)
        {
            return requested.Owner == existing.Key.Owner &&
                IsWriterAllowed(existing.Key.Access.WriterSetId, requested.Owner) &&
                IsWriterAllowed(requested.Access.WriterSetId, requested.Owner);
        }

        private SharedRTConflictCode DetectResizeBurst(Entry entry)
        {
            int recentAccepted = 0;
            for (int i = entry.ResizeEvents.Count - 1; i >= 0; i--)
            {
                SharedRTResizeEvent resizeEvent = entry.ResizeEvents[i];
                if (_timeSeconds - resizeEvent.TimeSeconds > 1f)
                    break;
                if (resizeEvent.Accepted)
                    recentAccepted++;
            }

            if (recentAccepted > _options.ResizeBurstThresholdPerSecond)
            {
                ReportConflict(entry, SharedRTConflictCode.ResizeBurst, entry.Key.Id, entry.Key.Owner, "SharedRT resize burst exceeded threshold.");
                return SharedRTConflictCode.ResizeBurst;
            }

            return SharedRTConflictCode.None;
        }

        private void ReportConflict(Entry entry, SharedRTConflictCode code, SharedRTId id, SharedRTOwnerId owner, string message)
        {
            if (code == SharedRTConflictCode.None)
                return;

            var conflict = new SharedRTConflictEvent(code, id, owner, _frameIndex, _timeSeconds, message);
            _recentConflicts.Add(conflict);
            TrimRecent(_recentConflicts, 32);
            if (entry != null)
            {
                entry.Conflicts.Add(conflict);
                TrimRecent(entry.Conflicts, 16);
            }
        }

        private static SharedRTOwnerId[] ToArray(HashSet<SharedRTOwnerId> values)
        {
            var result = new SharedRTOwnerId[values.Count];
            values.CopyTo(result);
            Array.Sort(result, (left, right) => string.CompareOrdinal(left.Value, right.Value));
            return result;
        }

        private static void TrimRecent<T>(List<T> values, int max)
        {
            if (values.Count > max)
                values.RemoveRange(0, values.Count - max);
        }

        private static long EstimateMemoryBytes(SharedRTSize size, SharedRTFormat format)
        {
            return (long)size.Width * size.Height * BytesPerPixel(format);
        }

        private static int BytesPerPixel(SharedRTFormat format)
        {
            switch (format)
            {
                case SharedRTFormat.R8:
                    return 1;
                case SharedRTFormat.RHalf:
                    return 2;
                case SharedRTFormat.ARGBHalf:
                    return 8;
                case SharedRTFormat.Depth:
                case SharedRTFormat.ARGB32:
                default:
                    return 4;
            }
        }

        private static GraphicsFormat ToGraphicsFormat(SharedRTFormat format)
        {
            switch (format)
            {
                case SharedRTFormat.R8:
                    return GraphicsFormat.R8_UNorm;
                case SharedRTFormat.RHalf:
                    return GraphicsFormat.R16_SFloat;
                case SharedRTFormat.ARGBHalf:
                    return GraphicsFormat.R16G16B16A16_SFloat;
                case SharedRTFormat.Depth:
                    return GraphicsFormat.None;
                case SharedRTFormat.ARGB32:
                default:
                    return GraphicsFormat.R8G8B8A8_UNorm;
            }
        }

        private sealed class Entry
        {
            public Entry(SharedRenderTextureKey key, SharedRTHandle handle)
            {
                Key = key;
                Handle = handle;
                CurrentSize = key.Size;
            }

            public SharedRenderTextureKey Key;
            public SharedRTHandle Handle { get; }
            public RTHandle RtHandle;
            public SharedRTSize CurrentSize;
            public long EstimatedMemoryBytes;
            public long ActualMemoryBytes;
            public bool IsAllocated;
            public bool IsFallback;
            public bool IsOrphaned;
            public int OrphanFrameCount;
            public SharedRTFrameOrder? LatestWriterOrder;
            public readonly HashSet<SharedRTOwnerId> CurrentReaders = new HashSet<SharedRTOwnerId>();
            public readonly HashSet<SharedRTOwnerId> CurrentWriters = new HashSet<SharedRTOwnerId>();
            public readonly List<SharedRTResizeEvent> ResizeEvents = new List<SharedRTResizeEvent>();
            public readonly List<SharedRTConflictEvent> Conflicts = new List<SharedRTConflictEvent>();
        }
    }

    public sealed class SharedRTRegistryDebugSource : IRenderingDebugSource
    {
        private readonly ISharedRenderTextureRegistry _registry;

        public SharedRTRegistryDebugSource(ISharedRenderTextureRegistry registry, string name = "Rendering")
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            Name = string.IsNullOrWhiteSpace(name) ? "Rendering" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => true;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[] { new FrameworkDebugSection(RenderingDebugSectionNames.SharedRTHealth, Format(_registry.CaptureDiagnostics())) });
        }

        private static string Format(SharedRTDiagnosticsSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.Append("budgetBytes: ").Append(snapshot.MemoryBudgetBytes).Append('\n');
            builder.Append("actualBytes: ").Append(snapshot.ActualMemoryBytes).Append('\n');
            builder.Append("estimatedBytes: ").Append(snapshot.EstimatedMemoryBytes).Append('\n');
            builder.Append("entries: ").Append(snapshot.Entries.Count).Append('\n');
            builder.Append("recentConflicts: ").Append(snapshot.RecentConflicts.Count);

            for (int i = 0; i < snapshot.Entries.Count; i++)
            {
                SharedRTDiagnosticsEntry entry = snapshot.Entries[i];
                builder.Append('\n')
                    .Append(entry.Id.Value)
                    .Append(" owner=").Append(entry.Owner.Value)
                    .Append(" size=").Append(entry.Dimensions)
                    .Append(" actualBytes=").Append(entry.ActualMemoryBytes)
                    .Append(" readers=").Append(entry.CurrentFrameReaders.Count)
                    .Append(" writers=").Append(entry.CurrentFrameWriters.Count)
                    .Append(" fallback=").Append(entry.IsFallback);
            }

            return builder.ToString();
        }
    }
}
