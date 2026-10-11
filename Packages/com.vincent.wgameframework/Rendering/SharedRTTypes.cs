using System;
using System.Collections.Generic;
using UnityEngine;

namespace MxFramework.Rendering
{
    public readonly struct SharedRTId : IEquatable<SharedRTId>
    {
        public SharedRTId(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(SharedRTId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SharedRTId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(SharedRTId left, SharedRTId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SharedRTId left, SharedRTId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct SharedRTOwnerId : IEquatable<SharedRTOwnerId>
    {
        public SharedRTOwnerId(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(SharedRTOwnerId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SharedRTOwnerId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(SharedRTOwnerId left, SharedRTOwnerId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SharedRTOwnerId left, SharedRTOwnerId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct SharedRTWriterSetId : IEquatable<SharedRTWriterSetId>
    {
        public SharedRTWriterSetId(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(SharedRTWriterSetId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SharedRTWriterSetId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(SharedRTWriterSetId left, SharedRTWriterSetId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SharedRTWriterSetId left, SharedRTWriterSetId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct SharedRTHandle : IEquatable<SharedRTHandle>
    {
        public static readonly SharedRTHandle Invalid = new SharedRTHandle(0, 0);

        internal SharedRTHandle(int value, int version)
        {
            Value = value;
            Version = version;
        }

        public int Value { get; }
        public int Version { get; }
        public bool IsValid => Value > 0 && Version > 0;

        public bool Equals(SharedRTHandle other)
        {
            return Value == other.Value && Version == other.Version;
        }

        public override bool Equals(object obj)
        {
            return obj is SharedRTHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Value * 397) ^ Version;
            }
        }

        public override string ToString()
        {
            return IsValid ? Value + ":" + Version : "Invalid";
        }

        public static bool operator ==(SharedRTHandle left, SharedRTHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SharedRTHandle left, SharedRTHandle right)
        {
            return !left.Equals(right);
        }
    }

    public enum MxRenderPhase
    {
        BeforeRendering = 0,
        BeforeRenderingShadows = 100,
        AfterRenderingShadows = 200,
        BeforeRenderingPrePasses = 300,
        AfterRenderingPrePasses = 400,
        BeforeRenderingOpaques = 500,
        AfterRenderingOpaques = 600,
        BeforeRenderingTransparents = 700,
        AfterRenderingTransparents = 800,
        BeforeRenderingPostProcessing = 900,
        AfterRenderingPostProcessing = 1000,
        AfterRendering = 1100
    }

    public readonly struct SharedRTFrameOrder : IComparable<SharedRTFrameOrder>, IEquatable<SharedRTFrameOrder>
    {
        public SharedRTFrameOrder(MxRenderPhase phase, int order)
        {
            Phase = phase;
            Order = order;
        }

        public MxRenderPhase Phase { get; }
        public int Order { get; }

        public int CompareTo(SharedRTFrameOrder other)
        {
            int phaseCompare = ((int)Phase).CompareTo((int)other.Phase);
            return phaseCompare != 0 ? phaseCompare : Order.CompareTo(other.Order);
        }

        public bool Equals(SharedRTFrameOrder other)
        {
            return Phase == other.Phase && Order == other.Order;
        }

        public override bool Equals(object obj)
        {
            return obj is SharedRTFrameOrder other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Phase * 397) ^ Order;
            }
        }

        public override string ToString()
        {
            return Phase + ":" + Order;
        }
    }

    public readonly struct SharedRTSize : IEquatable<SharedRTSize>
    {
        public SharedRTSize(int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
        }

        public int Width { get; }
        public int Height { get; }
        public bool IsValid => Width > 0 && Height > 0;

        public bool Equals(SharedRTSize other)
        {
            return Width == other.Width && Height == other.Height;
        }

        public override bool Equals(object obj)
        {
            return obj is SharedRTSize other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Width * 397) ^ Height;
            }
        }

        public override string ToString()
        {
            return Width + "x" + Height;
        }

        public static bool operator ==(SharedRTSize left, SharedRTSize right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SharedRTSize left, SharedRTSize right)
        {
            return !left.Equals(right);
        }
    }

    public enum SharedRTOrderRule
    {
        ReadAfterWriteSameFrame = 0,
        ReadPrevFrame = 1
    }

    public enum SharedRTAnchor
    {
        World = 0,
        MainCamera = 1,
        PrimarySubject = 2,
        Static = 3
    }

    public enum SharedRTFormat
    {
        R8 = 0,
        RHalf = 1,
        ARGB32 = 2,
        ARGBHalf = 3,
        Depth = 4
    }

    public enum SharedRTResizePolicy
    {
        FailOnResize = 0,
        Reallocate = 1,
        KeepLargest = 2
    }

    public enum SharedRTClearKind
    {
        NeverClear = 0,
        ClearEveryFrame = 1,
        RollClear = 2,
        FadeOut = 3
    }

    public enum SharedRTConflictCode
    {
        None = 0,
        AdditiveWritersAllowed = 1,
        WriterConflict = 2,
        StaleReader = 3,
        UnauthorizedWriter = 4,
        OrphanRT = 5,
        ResizeRejected = 6,
        ResizeBurst = 7,
        DroppedAllocation = 8
    }

    public readonly struct SharedRTAccessPolicy : IEquatable<SharedRTAccessPolicy>
    {
        public SharedRTAccessPolicy(bool allowAdditiveWriters, SharedRTOrderRule order, SharedRTWriterSetId writerSetId)
        {
            AllowAdditiveWriters = allowAdditiveWriters;
            Order = order;
            WriterSetId = writerSetId;
        }

        public bool AllowAdditiveWriters { get; }
        public SharedRTOrderRule Order { get; }
        public SharedRTWriterSetId WriterSetId { get; }

        public bool Equals(SharedRTAccessPolicy other)
        {
            return AllowAdditiveWriters == other.AllowAdditiveWriters && Order == other.Order && WriterSetId.Equals(other.WriterSetId);
        }

        public override bool Equals(object obj)
        {
            return obj is SharedRTAccessPolicy other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = AllowAdditiveWriters ? 1 : 0;
                hashCode = (hashCode * 397) ^ (int)Order;
                hashCode = (hashCode * 397) ^ WriterSetId.GetHashCode();
                return hashCode;
            }
        }
    }

    public readonly struct SharedRTClearSpec : IEquatable<SharedRTClearSpec>
    {
        public SharedRTClearSpec(SharedRTClearKind kind, Color clearColor, float fadeOutRate = 0f)
        {
            Kind = kind;
            ClearColor = clearColor;
            FadeOutRate = fadeOutRate;
        }

        public SharedRTClearKind Kind { get; }
        public Color ClearColor { get; }
        public float FadeOutRate { get; }

        public bool Equals(SharedRTClearSpec other)
        {
            return Kind == other.Kind && ClearColor.Equals(other.ClearColor) && FadeOutRate.Equals(other.FadeOutRate);
        }

        public override bool Equals(object obj)
        {
            return obj is SharedRTClearSpec other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)Kind;
                hashCode = (hashCode * 397) ^ ClearColor.GetHashCode();
                hashCode = (hashCode * 397) ^ FadeOutRate.GetHashCode();
                return hashCode;
            }
        }
    }

    public readonly struct SharedRenderTextureKey : IEquatable<SharedRenderTextureKey>
    {
        public SharedRenderTextureKey(
            SharedRTId id,
            string debugName,
            SharedRTOwnerId owner,
            SharedRTAccessPolicy access,
            SharedRTAnchor anchor,
            SharedRTFormat format,
            SharedRTSize size,
            SharedRTClearSpec clear,
            SharedRTResizePolicy resize,
            long estimatedMemoryBytes = 0)
        {
            Id = id;
            DebugName = debugName ?? string.Empty;
            Owner = owner;
            Access = access;
            Anchor = anchor;
            Format = format;
            Size = size;
            Clear = clear;
            Resize = resize;
            EstimatedMemoryBytes = estimatedMemoryBytes;
        }

        public SharedRTId Id { get; }
        public string DebugName { get; }
        public SharedRTOwnerId Owner { get; }
        public SharedRTAccessPolicy Access { get; }
        public SharedRTAnchor Anchor { get; }
        public SharedRTFormat Format { get; }
        public SharedRTSize Size { get; }
        public SharedRTClearSpec Clear { get; }
        public SharedRTResizePolicy Resize { get; }
        public long EstimatedMemoryBytes { get; }

        public bool Equals(SharedRenderTextureKey other)
        {
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is SharedRenderTextureKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }

    public readonly struct SharedRTAccessResult
    {
        public SharedRTAccessResult(bool succeeded, SharedRTConflictCode conflictCode, SharedRTHandle handle, string message)
        {
            Succeeded = succeeded;
            ConflictCode = conflictCode;
            Handle = handle;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public SharedRTConflictCode ConflictCode { get; }
        public SharedRTHandle Handle { get; }
        public string Message { get; }
    }

    public readonly struct SharedRTConflictEvent
    {
        public SharedRTConflictEvent(SharedRTConflictCode code, SharedRTId id, SharedRTOwnerId owner, int frameIndex, float timeSeconds, string message)
        {
            Code = code;
            Id = id;
            Owner = owner;
            FrameIndex = frameIndex;
            TimeSeconds = timeSeconds;
            Message = message ?? string.Empty;
        }

        public SharedRTConflictCode Code { get; }
        public SharedRTId Id { get; }
        public SharedRTOwnerId Owner { get; }
        public int FrameIndex { get; }
        public float TimeSeconds { get; }
        public string Message { get; }
    }

    public readonly struct SharedRTResizeEvent
    {
        public SharedRTResizeEvent(SharedRTId id, SharedRTSize from, SharedRTSize to, bool accepted, int frameIndex, float timeSeconds)
        {
            Id = id;
            From = from;
            To = to;
            Accepted = accepted;
            FrameIndex = frameIndex;
            TimeSeconds = timeSeconds;
        }

        public SharedRTId Id { get; }
        public SharedRTSize From { get; }
        public SharedRTSize To { get; }
        public bool Accepted { get; }
        public int FrameIndex { get; }
        public float TimeSeconds { get; }
    }

    public readonly struct SharedRTDiagnosticsEntry
    {
        public SharedRTDiagnosticsEntry(
            SharedRTId id,
            string debugName,
            SharedRTOwnerId owner,
            SharedRTFormat format,
            SharedRTSize dimensions,
            SharedRTResizePolicy resize,
            long estimatedMemoryBytes,
            long actualMemoryBytes,
            bool isAllocated,
            bool isFallback,
            bool isOrphaned,
            int orphanFrameCount,
            IReadOnlyList<SharedRTOwnerId> currentFrameReaders,
            IReadOnlyList<SharedRTOwnerId> currentFrameWriters,
            IReadOnlyList<SharedRTResizeEvent> recentResizeEvents,
            IReadOnlyList<SharedRTConflictEvent> recentConflicts)
        {
            Id = id;
            DebugName = debugName ?? string.Empty;
            Owner = owner;
            Format = format;
            Dimensions = dimensions;
            Resize = resize;
            EstimatedMemoryBytes = estimatedMemoryBytes;
            ActualMemoryBytes = actualMemoryBytes;
            IsAllocated = isAllocated;
            IsFallback = isFallback;
            IsOrphaned = isOrphaned;
            OrphanFrameCount = orphanFrameCount;
            CurrentFrameReaders = currentFrameReaders ?? Array.Empty<SharedRTOwnerId>();
            CurrentFrameWriters = currentFrameWriters ?? Array.Empty<SharedRTOwnerId>();
            RecentResizeEvents = recentResizeEvents ?? Array.Empty<SharedRTResizeEvent>();
            RecentConflicts = recentConflicts ?? Array.Empty<SharedRTConflictEvent>();
        }

        public SharedRTId Id { get; }
        public string DebugName { get; }
        public SharedRTOwnerId Owner { get; }
        public SharedRTFormat Format { get; }
        public SharedRTSize Dimensions { get; }
        public SharedRTResizePolicy Resize { get; }
        public long EstimatedMemoryBytes { get; }
        public long ActualMemoryBytes { get; }
        public bool IsAllocated { get; }
        public bool IsFallback { get; }
        public bool IsOrphaned { get; }
        public int OrphanFrameCount { get; }
        public IReadOnlyList<SharedRTOwnerId> CurrentFrameReaders { get; }
        public IReadOnlyList<SharedRTOwnerId> CurrentFrameWriters { get; }
        public IReadOnlyList<SharedRTResizeEvent> RecentResizeEvents { get; }
        public IReadOnlyList<SharedRTConflictEvent> RecentConflicts { get; }
    }

    public sealed class SharedRTDiagnosticsSnapshot
    {
        public SharedRTDiagnosticsSnapshot(
            IReadOnlyList<SharedRTDiagnosticsEntry> entries,
            IReadOnlyList<SharedRTConflictEvent> recentConflicts,
            long memoryBudgetBytes,
            long actualMemoryBytes)
        {
            Entries = entries != null ? new List<SharedRTDiagnosticsEntry>(entries) : new List<SharedRTDiagnosticsEntry>();
            RecentConflicts = recentConflicts != null ? new List<SharedRTConflictEvent>(recentConflicts) : new List<SharedRTConflictEvent>();
            MemoryBudgetBytes = memoryBudgetBytes;
            ActualMemoryBytes = actualMemoryBytes;
        }

        public IReadOnlyList<SharedRTDiagnosticsEntry> Entries { get; }
        public IReadOnlyList<SharedRTConflictEvent> RecentConflicts { get; }
        public long MemoryBudgetBytes { get; }
        public long ActualMemoryBytes { get; }
        public long EstimatedMemoryBytes
        {
            get
            {
                long total = 0;
                for (int i = 0; i < Entries.Count; i++)
                    total += Entries[i].EstimatedMemoryBytes;
                return total;
            }
        }
    }

    public sealed class SharedRenderTextureRegistryOptions
    {
        public long MemoryBudgetBytes { get; set; } = 256L * 1024L * 1024L;
        public int OrphanFrameThreshold { get; set; } = 3;
        public int ResizeBurstThresholdPerSecond { get; set; } = 3;
        public bool SimulateAllocationFailure { get; set; }
    }
}
