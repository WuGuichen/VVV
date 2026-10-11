// -----------------------------------------------------------------------
// <copyright file="RuntimeHashStreamHasher.cs" company="Tencent">
// Copyright (c) Tencent. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

/*
 * xxHash - Extremely Fast Hash algorithm
 * Copyright (C) 2012-2023 Yann Collet
 * 
 * BSD 2-Clause License (https://www.opensource.org/licenses/bsd-license.php)
 * 
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are
 * met:
 * 
 *    * Redistributions of source code must retain the above copyright
 *      notice, this list of conditions and the following disclaimer.
 *    * Redistributions in binary form must reproduce the above
 *      copyright notice, this list of conditions and the following disclaimer
 *      in the documentation and/or other materials provided with the
 *      distribution.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 * A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
 * OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Runtime.CompilerServices;

namespace MxFramework.Runtime
{
    /// <summary>
    /// A streaming XXH64 hasher following the xxHash v0.8.3 specification.
    /// Pure safe C# implementation, zero-allocation, uses ulong buffer fields.
    /// </summary>
    public struct Xxh64StreamHasher
    {
        private const ulong P1 = 0x9E3779B185EBCA87UL;
        private const ulong P2 = 0xC2B2AE3D27D4EB4FUL;
        private const ulong P3 = 0x165667B19E3779F9UL;
        private const ulong P4 = 0x85EBCA77C2B2AE63UL;
        private const ulong P5 = 0x27D4EB2F165667C5UL;
        private const ulong Seed = 0;

        private ulong _v1;
        private ulong _v2;
        private ulong _v3;
        private ulong _v4;

        private ulong _buf0;
        private ulong _buf1;
        private ulong _buf2;
        private ulong _buf3;

        private int _bufferSize;
        private long _totalLength;

        /// <summary>
        /// Initializes or resets the hasher state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            unchecked
            {
                _v1 = Seed + P1 + P2;
                _v2 = Seed + P2;
                _v3 = Seed;
                _v4 = Seed - P1;

                _buf0 = 0;
                _buf1 = 0;
                _buf2 = 0;
                _buf3 = 0;

                _bufferSize = 0;
                _totalLength = 0;
            }
        }

        /// <summary>
        /// Adds a single byte to the hash state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MixByte(byte value)
        {
            unchecked
            {
                int index = _bufferSize >> 3;
                int shift = (_bufferSize & 7) << 3;
                ulong v = (ulong)value << shift;

                if (index == 0) _buf0 |= v;
                else if (index == 1) _buf1 |= v;
                else if (index == 2) _buf2 |= v;
                else _buf3 |= v;

                _bufferSize++;
                _totalLength++;

                if (_bufferSize == 32)
                {
                    ProcessStripe();
                }
            }
        }

        /// <summary>
        /// Adds a 32-bit integer (little-endian) to the hash state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MixInt(int value)
        {
            unchecked
            {
                if ((_bufferSize & 7) <= 4)
                {
                    int index = _bufferSize >> 3;
                    int shift = (_bufferSize & 7) << 3;
                    ulong v = (ulong)(uint)value << shift;

                    if (index == 0) _buf0 |= v;
                    else if (index == 1) _buf1 |= v;
                    else if (index == 2) _buf2 |= v;
                    else _buf3 |= v;

                    _bufferSize += 4;
                    _totalLength += 4;

                    if (_bufferSize == 32)
                    {
                        ProcessStripe();
                    }
                }
                else
                {
                    MixByte((byte)value);
                    MixByte((byte)(value >> 8));
                    MixByte((byte)(value >> 16));
                    MixByte((byte)(value >> 24));
                }
            }
        }

        /// <summary>
        /// Adds a 64-bit integer (little-endian) to the hash state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MixLong(long value)
        {
            unchecked
            {
                if ((_bufferSize & 7) == 0)
                {
                    int index = _bufferSize >> 3;
                    ulong v = (ulong)value;

                    if (index == 0) _buf0 |= v;
                    else if (index == 1) _buf1 |= v;
                    else if (index == 2) _buf2 |= v;
                    else _buf3 |= v;

                    _bufferSize += 8;
                    _totalLength += 8;

                    if (_bufferSize == 32)
                    {
                        ProcessStripe();
                    }
                }
                else
                {
                    MixByte((byte)value);
                    MixByte((byte)(value >> 8));
                    MixByte((byte)(value >> 16));
                    MixByte((byte)(value >> 24));
                    MixByte((byte)(value >> 32));
                    MixByte((byte)(value >> 40));
                    MixByte((byte)(value >> 48));
                    MixByte((byte)(value >> 56));
                }
            }
        }

        /// <summary>
        /// Computes the final XXH64 hash code.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Finalize()
        {
            unchecked
            {
                ulong h;
                if (_totalLength >= 32)
                {
                    h = RotateLeft(_v1, 1) + RotateLeft(_v2, 7) + RotateLeft(_v3, 12) + RotateLeft(_v4, 18);
                    h = Merge(h, _v1);
                    h = Merge(h, _v2);
                    h = Merge(h, _v3);
                    h = Merge(h, _v4);
                }
                else
                {
                    h = Seed + P5;
                }

                h += (ulong)_totalLength;

                int pos = 0;
                int remain = _bufferSize;

                if (remain >= 8)
                {
                    h ^= RotateLeft(_buf0 * P2, 31) * P1;
                    h = RotateLeft(h, 27) * P1 + P4;
                    remain -= 8;
                    pos += 8;
                }
                if (remain >= 8)
                {
                    h ^= RotateLeft(_buf1 * P2, 31) * P1;
                    h = RotateLeft(h, 27) * P1 + P4;
                    remain -= 8;
                    pos += 8;
                }
                if (remain >= 8)
                {
                    h ^= RotateLeft(_buf2 * P2, 31) * P1;
                    h = RotateLeft(h, 27) * P1 + P4;
                    remain -= 8;
                    pos += 8;
                }

                while (remain >= 4)
                {
                    uint v = ReadUIntFromBuffer(pos);
                    h ^= v * P1;
                    h = RotateLeft(h, 23) * P2 + P3;
                    remain -= 4;
                    pos += 4;
                }

                while (remain > 0)
                {
                    byte v = ReadByteFromBuffer(pos);
                    h ^= v * P5;
                    h = RotateLeft(h, 11) * P1;
                    remain -= 1;
                    pos += 1;
                }

                h ^= h >> 33;
                h *= P2;
                h ^= h >> 29;
                h *= P3;
                h ^= h >> 32;

                return (long)h;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessStripe()
        {
            unchecked
            {
                _v1 = RotateLeft(_v1 + _buf0 * P2, 31) * P1;
                _v2 = RotateLeft(_v2 + _buf1 * P2, 31) * P1;
                _v3 = RotateLeft(_v3 + _buf2 * P2, 31) * P1;
                _v4 = RotateLeft(_v4 + _buf3 * P2, 31) * P1;

                _bufferSize = 0;
                _buf0 = 0;
                _buf1 = 0;
                _buf2 = 0;
                _buf3 = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RotateLeft(ulong value, int count)
        {
            return (value << count) | (value >> (64 - count));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Merge(ulong acc, ulong lane)
        {
            unchecked
            {
                ulong round = RotateLeft(lane * P2, 31) * P1;
                return (acc ^ round) * P1 + P4;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint ReadUIntFromBuffer(int pos)
        {
            int index = pos >> 3;
            int shift = (pos & 7) << 3;
            ulong buf = index == 0 ? _buf0 : index == 1 ? _buf1 : index == 2 ? _buf2 : _buf3;
            return unchecked((uint)(buf >> shift));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte ReadByteFromBuffer(int pos)
        {
            int index = pos >> 3;
            int shift = (pos & 7) << 3;
            ulong buf = index == 0 ? _buf0 : index == 1 ? _buf1 : index == 2 ? _buf2 : _buf3;
            return unchecked((byte)(buf >> shift));
        }
    }

    /// <summary>
    /// A streaming FNV-1a hasher used by the v3 RuntimeHashAccumulator candidate.
    /// </summary>
    public struct Fnv1aStreamHasher
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        private ulong _hash;

        /// <summary>
        /// Initializes or resets the hasher state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _hash = OffsetBasis;
        }

        /// <summary>
        /// Adds a single byte to the hash state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MixByte(byte value)
        {
            unchecked
            {
                _hash = (_hash ^ value) * Prime;
            }
        }

        /// <summary>
        /// Adds a 32-bit integer (little-endian) to the hash state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MixInt(int value)
        {
            unchecked
            {
                MixByte((byte)value);
                MixByte((byte)(value >> 8));
                MixByte((byte)(value >> 16));
                MixByte((byte)(value >> 24));
            }
        }

        /// <summary>
        /// Adds a 64-bit integer (little-endian) to the hash state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MixLong(long value)
        {
            unchecked
            {
                MixByte((byte)value);
                MixByte((byte)(value >> 8));
                MixByte((byte)(value >> 16));
                MixByte((byte)(value >> 24));
                MixByte((byte)(value >> 32));
                MixByte((byte)(value >> 40));
                MixByte((byte)(value >> 48));
                MixByte((byte)(value >> 56));
            }
        }

        /// <summary>
        /// Computes the final FNV-1a hash code with one avalanche.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Finalize() => Finalize(0);

        /// <summary>Mixes the state field count into the raw FNV state before the single final avalanche.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Finalize(long fieldCount)
        {
            unchecked
            {
                ulong h = _hash ^ ((ulong)fieldCount * 0x9E3779B97F4A7C15UL);
                h ^= h >> 33;
                h *= 0xFF51AFD7ED558CCDUL;
                h ^= h >> 33;
                h *= 0xC4CEB9FE1A85EC53UL;
                h ^= h >> 33;
                return (long)h;
            }
        }
    }
}
