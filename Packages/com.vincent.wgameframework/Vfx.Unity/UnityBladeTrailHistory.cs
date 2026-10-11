using System;

namespace MxFramework.Vfx.Unity
{
    /// <summary>Value-only position used by bounded trail history; no engine state or attack authority.</summary>
    public readonly struct BladeTrailPosition
    {
        public BladeTrailPosition(float x, float y, float z) { X = x; Y = y; Z = z; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public static float DistanceSquared(BladeTrailPosition a, BladeTrailPosition b)
        {
            float x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z;
            return x * x + y * y + z * z;
        }
        public bool IsFinite => !float.IsNaN(X) && !float.IsInfinity(X)
            && !float.IsNaN(Y) && !float.IsInfinity(Y) && !float.IsNaN(Z) && !float.IsInfinity(Z);
    }

    public readonly struct BladeTrailSlice
    {
        public BladeTrailSlice(BladeTrailPosition root, BladeTrailPosition tip, double time)
        { Root = root; Tip = tip; Time = time; }
        public BladeTrailPosition Root { get; }
        public BladeTrailPosition Tip { get; }
        public double Time { get; }
    }

    /// <summary>Fixed storage. NoAlloc after construction. Caller supplies a monotonic presentation clock.</summary>
    public sealed class UnityBladeTrailHistory
    {
        private readonly BladeTrailSlice[] _slices;
        private int _first;
        public UnityBladeTrailHistory(int capacity)
        {
            if (capacity < 2 || capacity > 128) throw new ArgumentOutOfRangeException(nameof(capacity));
            _slices = new BladeTrailSlice[capacity];
        }
        public int Count { get; private set; }
        public int Capacity => _slices.Length;
        public BladeTrailSlice this[int index]
        {
            get
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
                return _slices[(_first + index) % Capacity];
            }
        }
        public void Clear() { Count = 0; _first = 0; }
        public void Expire(double now, double lifetime)
        {
            while (Count > 0 && now - _slices[_first].Time >= lifetime)
            { _first = (_first + 1) % Capacity; Count--; }
        }
        public bool Append(BladeTrailPosition root, BladeTrailPosition tip, double time)
        {
            if (!root.IsFinite || !tip.IsFinite || double.IsNaN(time) || double.IsInfinity(time))
            { Clear(); return false; }
            if (Count > 0)
            {
                double previous = this[Count - 1].Time;
                if (time < previous) Clear();
                else if (time == previous)
                { _slices[(_first + Count - 1) % Capacity] = new BladeTrailSlice(root, tip, time); return true; }
            }
            if (Count == Capacity) { _first = (_first + 1) % Capacity; Count--; }
            _slices[(_first + Count) % Capacity] = new BladeTrailSlice(root, tip, time);
            Count++;
            return true;
        }
        public static float Fade(double now, double sampleTime, double lifetime)
        {
            if (lifetime <= 0d) return 0f;
            double value = 1d - (now - sampleTime) / lifetime;
            return (float)(value < 0d ? 0d : value > 1d ? 1d : value);
        }
    }
}
