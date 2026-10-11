using System.Runtime.CompilerServices;

namespace MxFramework.Runtime
{
    /// <summary>XXH64 of exactly LE64(first) || LE64(second), seeded by the owning layout.</summary>
    /// <remarks>NoAlloc. Stateless, safe integer arithmetic; signed inputs retain their 64-bit bit patterns.
    /// Specialized from xxHash v0.8.3 by Yann Collet; see RuntimeFixedLayoutHash64.LICENSE.txt.</remarks>
    public static class RuntimeFixedLayoutHash64
    {
        // Stable protocol IDs: 1 = FNV-1a/finalizer, 2 = XXH64, 3 = XXH3-64.
        public const int AlgorithmId = 2;
        private const ulong Prime1 = 0x9E3779B185EBCA87UL;
        private const ulong Prime2 = 0xC2B2AE3D27D4EB4FUL;
        private const ulong Prime3 = 0x165667B19E3779F9UL;
        private const ulong Prime4 = 0x85EBCA77C2B2AE63UL;
        private const ulong LengthSeed = 0x27D4EB2F165667C5UL + 16UL;

        /// <summary>Hashes two ordered Int64 values without materializing their 16-byte encoding.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Hash16(long first, long second, ulong domainSeed)
        {
            unchecked
            {
                ulong hash = domainSeed + LengthSeed;
                hash ^= RotateLeft((ulong)first * Prime2, 31) * Prime1;
                hash = RotateLeft(hash, 27) * Prime1 + Prime4;
                hash ^= RotateLeft((ulong)second * Prime2, 31) * Prime1;
                hash = RotateLeft(hash, 27) * Prime1 + Prime4;
                hash ^= hash >> 33;
                hash *= Prime2;
                hash ^= hash >> 29;
                hash *= Prime3;
                hash ^= hash >> 32;
                return (long)hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RotateLeft(ulong value, int count) => (value << count) | (value >> (64 - count));
    }
}
