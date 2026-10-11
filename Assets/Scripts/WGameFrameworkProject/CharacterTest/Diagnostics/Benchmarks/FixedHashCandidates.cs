#if UNITY_EDITOR || CHARACTER_TEST_HASH_BENCHMARK
using System;
using System.Runtime.CompilerServices;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.Diagnostics
{
    // xxHash fixed-length specializations follow v0.8.3, Copyright Yann Collet.
    // BSD-2-Clause; see Tools/FixedHashBenchmark/xxHash-LICENSE.txt.
    internal static class FixedHashCandidates
    {
        internal const ulong DomainSeed = (1001UL << 32) | 1UL;
        private const ulong P1 = 0x9E3779B185EBCA87UL, P2 = 0xC2B2AE3D27D4EB4FUL;
        private const ulong P3 = 0x165667B19E3779F9UL, P4 = 0x85EBCA77C2B2AE63UL, P5 = 0x27D4EB2F165667C5UL;
        // Explicit benchmark baseline: the pre-migration v1 byte encoding and finalizer.
        [ThreadStatic] private static RuntimeContentHashAccumulator _legacy;

        internal static long Legacy(long first, long second)
        {
            var hash = _legacy ??= new RuntimeContentHashAccumulator();
            hash.Reset();
            hash.AddStringStable("sessionAuthority.combinedSchema", "mx.character-test.runtime-ai-planner.session-deck-lock.v1");
            hash.AddLong("sessionAuthority.gameplayStateHash", first);
            hash.AddLong("sessionAuthority.authorityHash", second);
            return hash.ToHash();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long Fnv(long first, long second, ulong seed)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL ^ seed;
                h = FnvWord(h, (ulong)first); h = FnvWord(h, (ulong)second);
                h ^= h >> 33; h *= 0xff51afd7ed558ccdUL;
                h ^= h >> 33; h *= 0xc4ceb9fe1a85ec53UL; h ^= h >> 33;
                return (long)h;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong FnvWord(ulong h, ulong v)
        {
            unchecked
            {
                const ulong prime = 1099511628211UL;
                h = (h ^ (byte)v) * prime; h = (h ^ (byte)(v >> 8)) * prime;
                h = (h ^ (byte)(v >> 16)) * prime; h = (h ^ (byte)(v >> 24)) * prime;
                h = (h ^ (byte)(v >> 32)) * prime; h = (h ^ (byte)(v >> 40)) * prime;
                h = (h ^ (byte)(v >> 48)) * prime; return (h ^ (byte)(v >> 56)) * prime;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long Xxh64(long first, long second, ulong seed)
        {
            unchecked
            {
                ulong h = seed + P5 + 16UL;
                h ^= RotateLeft((ulong)first * P2, 31) * P1;
                h = RotateLeft(h, 27) * P1 + P4;
                h ^= RotateLeft((ulong)second * P2, 31) * P1;
                h = RotateLeft(h, 27) * P1 + P4;
                h ^= h >> 33; h *= P2; h ^= h >> 29; h *= P3; h ^= h >> 32;
                return (long)h;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long Xxh3(long first, long second, ulong seed)
        {
            unchecked
            {
                ulong lo = (ulong)first ^ (0x6782737bea4239b9UL + seed);
                ulong hi = (ulong)second ^ (0xaf56bc3b0996523aUL - seed);
                ulong h = 16UL + ReverseBytes(lo) + hi + MultiplyFold(lo, hi);
                h ^= h >> 37; h *= 0x165667919E3779F9UL; h ^= h >> 32;
                return (long)h;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong MultiplyFold(ulong left, ulong right)
        {
            unchecked
            {
                ulong loLo = (ulong)(uint)left * (uint)right;
                ulong hiLo = (left >> 32) * (uint)right;
                ulong loHi = (ulong)(uint)left * (right >> 32);
                ulong hiHi = (left >> 32) * (right >> 32);
                ulong cross = (loLo >> 32) + (uint)hiLo + loHi;
                ulong upper = (hiLo >> 32) + (cross >> 32) + hiHi;
                ulong lower = (cross << 32) | (uint)loLo;
                return lower ^ upper;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RotateLeft(ulong value, int count) => (value << count) | (value >> (64 - count));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ReverseBytes(ulong value)
        {
            value = ((value & 0x00ff00ff00ff00ffUL) << 8) | ((value >> 8) & 0x00ff00ff00ff00ffUL);
            value = ((value & 0x0000ffff0000ffffUL) << 16) | ((value >> 16) & 0x0000ffff0000ffffUL);
            return (value << 32) | (value >> 32);
        }
    }
}
#endif
