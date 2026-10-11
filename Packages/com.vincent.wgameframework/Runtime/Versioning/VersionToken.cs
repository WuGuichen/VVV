using System;

namespace MxFramework.Runtime
{
    public readonly struct VersionToken : IEquatable<VersionToken>
    {
        public VersionToken(int version)
        {
            Version = version;
        }

        public int Version { get; }

        public bool Equals(VersionToken other)
        {
            return Version == other.Version;
        }

        public override bool Equals(object obj)
        {
            return obj is VersionToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Version;
        }

        public override string ToString()
        {
            return Version.ToString();
        }

        public static bool operator ==(VersionToken left, VersionToken right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VersionToken left, VersionToken right)
        {
            return !left.Equals(right);
        }
    }
}
