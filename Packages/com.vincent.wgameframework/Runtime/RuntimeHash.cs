using System;
using System.Security.Cryptography;

namespace MxFramework.Runtime
{
    public readonly struct RuntimeHashContext
    {
        public RuntimeHashContext(RuntimeFrame frame) { Frame = frame; }
        public RuntimeFrame Frame { get; }
    }

    /// <summary>Prepared immutable 256-bit content digest; never a field key or definition identity.</summary>
    public readonly struct RuntimeHashDigest : IEquatable<RuntimeHashDigest>
    {
        public RuntimeHashDigest(ulong word0, ulong word1, ulong word2, ulong word3)
        { Word0 = word0; Word1 = word1; Word2 = word2; Word3 = word3; }

        public ulong Word0 { get; }
        public ulong Word1 { get; }
        public ulong Word2 { get; }
        public ulong Word3 { get; }
        public bool Equals(RuntimeHashDigest other) =>
            Word0 == other.Word0 && Word1 == other.Word1 && Word2 == other.Word2 && Word3 == other.Word3;
        public override bool Equals(object obj) => obj is RuntimeHashDigest other && Equals(other);
        public override int GetHashCode() => unchecked((int)(Word0 ^ Word1 ^ Word2 ^ Word3));

        private static readonly RuntimeHashDigest NullDigest = ComputeContentDigestCore(null);
        private static readonly RuntimeHashDigest EmptyDigest = ComputeContentDigestCore(string.Empty);

        /// <summary>SHA-256 of LE32 UTF-16 code-unit count followed by exact UTF-16LE code units; null has length -1.</summary>
        /// <remarks>LowFreqAlloc. Prepare at content mutation/import boundaries and retain the result; never call from state hash contribution.</remarks>
        public static RuntimeHashDigest ComputeContentDigest(string value)
        {
            if (value == null) return NullDigest;
            return value.Length == 0 ? EmptyDigest : ComputeContentDigestCore(value);
        }

        private static RuntimeHashDigest ComputeContentDigestCore(string value)
        {
            int length = value == null ? -1 : value.Length;
            byte[] input = new byte[checked(4 + (value == null ? 0 : length * 2))];
            input[0] = unchecked((byte)length); input[1] = unchecked((byte)(length >> 8));
            input[2] = unchecked((byte)(length >> 16)); input[3] = unchecked((byte)(length >> 24));
            for (int i = 0; i < length; i++)
            {
                // Preserve even unpaired surrogate code units; Encoding.Unicode may replace them.
                input[4 + i * 2] = unchecked((byte)value[i]);
                input[5 + i * 2] = unchecked((byte)(value[i] >> 8));
            }
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] bytes = algorithm.ComputeHash(input);
                return new RuntimeHashDigest(ReadWord(bytes, 0), ReadWord(bytes, 8), ReadWord(bytes, 16), ReadWord(bytes, 24));
            }
        }

        private static ulong ReadWord(byte[] bytes, int offset) =>
            (ulong)bytes[offset] | ((ulong)bytes[offset + 1] << 8) | ((ulong)bytes[offset + 2] << 16)
            | ((ulong)bytes[offset + 3] << 24) | ((ulong)bytes[offset + 4] << 32)
            | ((ulong)bytes[offset + 5] << 40) | ((ulong)bytes[offset + 6] << 48) | ((ulong)bytes[offset + 7] << 56);
    }
}
