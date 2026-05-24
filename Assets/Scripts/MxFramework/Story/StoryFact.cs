using System;

namespace MxFramework.Story
{
    public readonly struct StoryFactKey : IEquatable<StoryFactKey>, IComparable<StoryFactKey>
    {
        public StoryFactKey(int @namespace, int id)
        {
            Namespace = @namespace;
            Id = id;
        }

        public int Namespace { get; }
        public int Id { get; }
        public bool IsValid => Namespace >= 0 && Id > 0;

        public int CompareTo(StoryFactKey other)
        {
            int namespaceCompare = Namespace.CompareTo(other.Namespace);
            if (namespaceCompare != 0)
            {
                return namespaceCompare;
            }

            return Id.CompareTo(other.Id);
        }

        public bool Equals(StoryFactKey other)
        {
            return Namespace == other.Namespace && Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is StoryFactKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Namespace * 397) ^ Id;
            }
        }

        public override string ToString()
        {
            return Namespace + ":" + Id;
        }

        public static bool operator ==(StoryFactKey left, StoryFactKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StoryFactKey left, StoryFactKey right)
        {
            return !left.Equals(right);
        }
    }

    public enum StoryValueKind : byte
    {
        None = 0,
        Bool = 1,
        Int32 = 2,
        Int64 = 3,
        Fix64 = 4,
        StringRef = 5
    }

    public readonly struct StoryValue : IEquatable<StoryValue>
    {
        public StoryValue(StoryValueKind kind, long raw)
        {
            Kind = kind;
            Raw = raw;
        }

        public StoryValueKind Kind { get; }
        public long Raw { get; }

        public static StoryValue None => new StoryValue(StoryValueKind.None, 0L);

        public static StoryValue FromBool(bool value)
        {
            return new StoryValue(StoryValueKind.Bool, value ? 1L : 0L);
        }

        public static StoryValue FromInt32(int value)
        {
            return new StoryValue(StoryValueKind.Int32, value);
        }

        public static StoryValue FromInt64(long value)
        {
            return new StoryValue(StoryValueKind.Int64, value);
        }

        public static StoryValue FromFix64Raw(long rawValue)
        {
            return new StoryValue(StoryValueKind.Fix64, rawValue);
        }

        public static StoryValue FromStringRef(int stringId)
        {
            if (stringId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stringId), "Story string ref id must be positive.");
            }

            return new StoryValue(StoryValueKind.StringRef, stringId);
        }

        public bool AsBool()
        {
            if (Kind != StoryValueKind.Bool)
            {
                throw new InvalidOperationException("Story value is not a bool.");
            }

            return Raw != 0L;
        }

        public bool Equals(StoryValue other)
        {
            return Kind == other.Kind && Raw == other.Raw;
        }

        public override bool Equals(object obj)
        {
            return obj is StoryValue other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ Raw.GetHashCode();
            }
        }

        public static bool operator ==(StoryValue left, StoryValue right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StoryValue left, StoryValue right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct StoryFactEntry : IEquatable<StoryFactEntry>
    {
        public StoryFactEntry(StoryFactKey key, StoryValue value)
        {
            Key = key;
            Value = value;
        }

        public StoryFactKey Key { get; }
        public StoryValue Value { get; }

        public bool Equals(StoryFactEntry other)
        {
            return Key.Equals(other.Key) && Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is StoryFactEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Key.GetHashCode() * 397) ^ Value.GetHashCode();
            }
        }
    }

    public readonly struct StoryFactCopyResult
    {
        public StoryFactCopyResult(int requiredCount, int writtenCount, bool complete)
        {
            RequiredCount = requiredCount;
            WrittenCount = writtenCount;
            Complete = complete;
        }

        public int RequiredCount { get; }
        public int WrittenCount { get; }
        public bool Complete { get; }
    }
}
