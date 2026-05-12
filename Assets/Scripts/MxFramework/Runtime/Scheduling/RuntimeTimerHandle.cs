using System;
using MxFramework.Core.Handles;

namespace MxFramework.Runtime
{
    public readonly struct RuntimeTimerHandle : IEquatable<RuntimeTimerHandle>
    {
        private readonly StableHandle _stableHandle;

        internal RuntimeTimerHandle(StableHandle stableHandle)
        {
            _stableHandle = stableHandle;
        }

        public int Index => _stableHandle.Index;
        public int Generation => _stableHandle.Generation;
        public bool IsValid => _stableHandle.IsValid;

        internal StableHandle StableHandle => _stableHandle;

        public bool Equals(RuntimeTimerHandle other)
        {
            return _stableHandle == other._stableHandle;
        }

        public override bool Equals(object obj)
        {
            return obj is RuntimeTimerHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _stableHandle.GetHashCode();
        }

        public override string ToString()
        {
            return IsValid
                ? "RuntimeTimerHandle(Index=" + Index + ", Generation=" + Generation + ")"
                : "RuntimeTimerHandle.Invalid";
        }

        public static RuntimeTimerHandle Invalid => new RuntimeTimerHandle(default);

        public static bool operator ==(RuntimeTimerHandle left, RuntimeTimerHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RuntimeTimerHandle left, RuntimeTimerHandle right)
        {
            return !left.Equals(right);
        }
    }
}
