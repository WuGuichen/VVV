using System;

namespace MxFramework.Runtime
{
    public enum RuntimeDigestRepresentation
    {
        Null = 0,
        Empty = 1,
        Hex = 2,
        Sha256 = 3,
        Sha1 = 4,
        Md5 = 5,
        Utf16ContentSha256 = 6
    }

    /// <summary>Prepared content fingerprint retained across runtime state hash calls.</summary>
    /// <remarks>Preparation belongs to import/configuration mutation boundaries; Contribute is read-only.</remarks>
    public readonly struct RuntimePreparedDigest : IEquatable<RuntimePreparedDigest>
    {
        public const int HashSchemaId = RuntimeHashSchemaIds.PreparedContentDigest;
        private const int RepresentationKey = 1;
        private const int ByteLengthKey = 2;
        private const int DigestKey = 3;

        public RuntimePreparedDigest(RuntimeDigestRepresentation representation, int byteLength, RuntimeHashDigest digest)
        {
            bool validWidth = representation == RuntimeDigestRepresentation.Null || representation == RuntimeDigestRepresentation.Empty
                ? byteLength == 0
                : representation == RuntimeDigestRepresentation.Hex
                    ? byteLength == 8 || byteLength == 16 || byteLength == 20 || byteLength == 32
                    : representation == RuntimeDigestRepresentation.Sha256 || representation == RuntimeDigestRepresentation.Utf16ContentSha256
                        ? byteLength == 32
                        : representation == RuntimeDigestRepresentation.Sha1
                            ? byteLength == 20
                            : representation == RuntimeDigestRepresentation.Md5 && byteLength == 16;
            if (!validWidth) throw new ArgumentOutOfRangeException(nameof(byteLength));
            if ((byteLength == 0 && digest.Word0 != 0)
                || (byteLength <= 8 && digest.Word1 != 0)
                || (byteLength <= 16 && digest.Word2 != 0)
                || (byteLength == 20 && (digest.Word2 >> 32) != 0)
                || (byteLength <= 20 && digest.Word3 != 0))
                throw new ArgumentException("Digest padding must be zero.", nameof(digest));
            Representation = representation;
            ByteLength = byteLength;
            Digest = digest;
        }

        public RuntimeDigestRepresentation Representation { get; }
        public int ByteLength { get; }
        public RuntimeHashDigest Digest { get; }
        public bool Equals(RuntimePreparedDigest other) => Representation == other.Representation
            && ByteLength == other.ByteLength && Digest.Equals(other.Digest);
        public override bool Equals(object obj) => obj is RuntimePreparedDigest other && Equals(other);
        public override int GetHashCode() => unchecked(((int)Representation * 397 ^ ByteLength) * 397 ^ Digest.GetHashCode());
        public static bool operator ==(RuntimePreparedDigest left, RuntimePreparedDigest right) => left.Equals(right);
        public static bool operator !=(RuntimePreparedDigest left, RuntimePreparedDigest right) => !left.Equals(right);

        public void Contribute(RuntimeHashAccumulator hash, int key)
        {
            hash.BeginObject(key, HashSchemaId, 1);
            hash.AddInt(RepresentationKey, (int)Representation);
            hash.AddInt(ByteLengthKey, ByteLength);
            hash.AddDigest(DigestKey, Digest);
            hash.EndObject();
        }

        /// <summary>Converts a persisted fingerprint at an import/configuration boundary.</summary>
        public static RuntimePreparedDigest PrepareExisting(string value)
        {
            if (value == null) return default;
            if (value.Length == 0) return new RuntimePreparedDigest(RuntimeDigestRepresentation.Empty, 0, default);
            int offset = 0;
            int expected = 0;
            RuntimeDigestRepresentation representation = RuntimeDigestRepresentation.Hex;
            if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            { offset = 7; expected = 32; representation = RuntimeDigestRepresentation.Sha256; }
            else if (value.StartsWith("sha1:", StringComparison.OrdinalIgnoreCase))
            { offset = 5; expected = 20; representation = RuntimeDigestRepresentation.Sha1; }
            else if (value.StartsWith("md5:", StringComparison.OrdinalIgnoreCase))
            { offset = 4; expected = 16; representation = RuntimeDigestRepresentation.Md5; }
            int bytes = (value.Length - offset) / 2;
            if ((expected == 0
                    ? bytes == 8 || bytes == 16 || bytes == 20 || bytes == 32
                    : bytes == expected)
                && TryReadHex(value, offset, bytes, out RuntimeHashDigest digest))
                return new RuntimePreparedDigest(representation, bytes, digest);
            if (expected != 0)
                throw new FormatException("Malformed fingerprint with an explicit digest algorithm prefix.");
            return new RuntimePreparedDigest(
                RuntimeDigestRepresentation.Utf16ContentSha256,
                32,
                RuntimeHashDigest.ComputeContentDigest(value));
        }

        public static bool TryParseSha256(string value, out RuntimeHashDigest digest)
        {
            int offset = value != null && value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? 7 : 0;
            return TryReadHex(value, offset, 32, out digest);
        }

        private static bool TryReadHex(string value, int offset, int byteLength, out RuntimeHashDigest digest)
        {
            digest = default;
            if (value == null || byteLength < 1 || byteLength > 32 || value.Length - offset != byteLength * 2)
                return false;
            ulong a = 0, b = 0, c = 0, d = 0;
            for (int i = 0; i < byteLength; i++)
            {
                int high = Nibble(value[offset + i * 2]);
                int low = Nibble(value[offset + i * 2 + 1]);
                if (high < 0 || low < 0) return false;
                ulong part = (ulong)((high << 4) | low) << ((i & 7) * 8);
                switch (i >> 3)
                {
                    case 0: a |= part; break;
                    case 1: b |= part; break;
                    case 2: c |= part; break;
                    default: d |= part; break;
                }
            }
            digest = new RuntimeHashDigest(a, b, c, d);
            return true;
        }

        private static int Nibble(char c) => c >= '0' && c <= '9' ? c - '0'
            : c >= 'a' && c <= 'f' ? c - 'a' + 10
            : c >= 'A' && c <= 'F' ? c - 'A' + 10
            : -1;
    }
}
