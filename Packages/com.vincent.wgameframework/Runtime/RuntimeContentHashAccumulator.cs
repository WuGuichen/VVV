using System;

namespace MxFramework.Runtime
{
    /// <summary>Version-one content fingerprints for authoring and configuration preparation.</summary>
    /// <remarks>Low-frequency only. Do not invoke from a numeric state-hash contributor.</remarks>
    public sealed class RuntimeContentHashAccumulator
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        private const byte IntField = 1;
        private const byte LongField = 2;
        private const byte DoubleQuantizedField = 3;
        private const byte StringField = 4;

        private ulong _hash;
        private long _fieldCount;
        private long _encodedByteCount;

        public long FieldCount => _fieldCount;
        public long EncodedByteCount => _encodedByteCount;

        public RuntimeContentHashAccumulator()
        {
            Reset();
        }

        /// <summary>Starts an independent hash using the same protocol seed. Does not allocate.</summary>
        public void Reset()
        {
            _hash = OffsetBasis;
            _fieldCount = 0;
            _encodedByteCount = 6;
            MixByte(82);
            MixByte(72);
            MixInt(1);
        }

        public void AddInt(string key, int value)
        {
            AddFieldHeader(IntField, key);
            _encodedByteCount += 4;
            MixInt(value);
        }

        public void AddLong(string key, long value)
        {
            AddFieldHeader(LongField, key);
            _encodedByteCount += 8;
            MixLong(value);
        }

        public void AddDoubleQuantized(string key, double value, double scale)
        {
            long quantized = QuantizeDouble(value, scale);
            AddFieldHeader(DoubleQuantizedField, key);
            _encodedByteCount += 8;
            MixLong(quantized);
        }

        public void AddStringStable(string key, string value)
        {
            AddFieldHeader(StringField, key);
            _encodedByteCount += 4 + (value?.Length ?? 0) * 2L;
            MixString(value);
        }

        /// <remarks>NoAlloc; identical field encoding to the corresponding string-key overloads.</remarks>
        public void AddInt(in RuntimeHashKey key, int value) { AddFieldHeader(IntField, key); _encodedByteCount += 4; MixInt(value); }
        public void AddLong(in RuntimeHashKey key, long value) { AddFieldHeader(LongField, key); _encodedByteCount += 8; MixLong(value); }
        public void AddStringStable(in RuntimeHashKey key, string value) { AddFieldHeader(StringField, key); _encodedByteCount += 4 + (value?.Length ?? 0) * 2L; MixString(value); }
        public void AddDoubleQuantized(in RuntimeHashKey key, double value, double scale)
        { long quantized = QuantizeDouble(value, scale); AddFieldHeader(DoubleQuantizedField, key); _encodedByteCount += 8; MixLong(quantized); }
        private void AddFieldHeader(byte fieldType, in RuntimeHashKey key)
        {
            if (key.Length == 0) throw new ArgumentException("Runtime hash key cannot be null or empty.", nameof(key));
            _encodedByteCount += 14 + key.Length * 2L;
            _fieldCount++; MixByte(167); MixLong(_fieldCount); MixByte(fieldType); MixInt(key.Length);
            for (int i = 0; i < key.Count; i++)
            {
                var part = key.GetPart(i);
                if (!part.Numeric) MixCharacters(part.Text);
                else
                {
                    int divisor = 1;
                    while (part.Index / divisor >= 10) divisor *= 10;
                    int remaining = part.Index;
                    while (divisor > 0)
                    { MixChar((char)('0' + remaining / divisor)); remaining %= divisor; divisor /= 10; }
                }
            }
        }

        public long ToHash()
        {
            unchecked
            {
                ulong finalized = _hash ^ ((ulong)_fieldCount * 0x9E3779B97F4A7C15UL);
                return (long)Avalanche(finalized);
            }
        }

        private void AddFieldHeader(byte fieldType, string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Runtime hash key cannot be null or empty.", nameof(key));
            }

            _fieldCount++;
            _encodedByteCount += 14 + key.Length * 2L;
            MixByte(167);
            MixLong(_fieldCount);
            MixByte(fieldType);
            MixString(key);
        }

        private static long QuantizeDouble(double value, double scale)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentException("Runtime hash double value must be finite.", nameof(value));
            }

            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(scale), "Runtime hash double scale must be finite and positive.");
            }

            double scaled = value * scale;
            if (double.IsNaN(scaled) || double.IsInfinity(scaled))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Runtime hash quantized double is outside Int64 range.");
            }

            double rounded = Math.Round(scaled, MidpointRounding.AwayFromZero);
            try
            {
                return checked((long)rounded);
            }
            catch (OverflowException exception)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Runtime hash quantized double is outside Int64 range. " + exception.Message);
            }
        }

        private void MixString(string value)
        {
            if (value == null)
            {
                MixInt(-1);
                return;
            }

            MixInt(value.Length);
            MixCharacters(value);
        }

        private void MixCharacters(string value)
        {
            if (value == null) return;
            unchecked
            {
                ulong hash = _hash;
                for (int i = 0; i < value.Length; i++)
                { char c = value[i]; hash = (hash ^ (byte)c) * Prime; hash = (hash ^ (byte)(c >> 8)) * Prime; }
                _hash = hash;
            }
        }

        private void MixChar(char value)
        {
            unchecked
            {
                ulong hash = _hash;
                hash = (hash ^ (byte)value) * Prime;
                hash = (hash ^ (byte)(value >> 8)) * Prime;
                _hash = hash;
            }
        }

        private void MixInt(int value)
        {
            MixUInt((uint)value);
        }

        private void MixUInt(uint value)
        {
            unchecked
            {
                ulong hash = _hash;
                hash = (hash ^ (byte)value) * Prime;
                hash = (hash ^ (byte)(value >> 8)) * Prime;
                hash = (hash ^ (byte)(value >> 16)) * Prime;
                hash = (hash ^ (byte)(value >> 24)) * Prime;
                _hash = hash;
            }
        }

        private void MixLong(long value)
        {
            MixULong((ulong)value);
        }

        private void MixULong(ulong value)
        {
            unchecked
            {
                ulong hash = _hash;
                hash = (hash ^ (byte)value) * Prime;
                hash = (hash ^ (byte)(value >> 8)) * Prime;
                hash = (hash ^ (byte)(value >> 16)) * Prime;
                hash = (hash ^ (byte)(value >> 24)) * Prime;
                hash = (hash ^ (byte)(value >> 32)) * Prime;
                hash = (hash ^ (byte)(value >> 40)) * Prime;
                hash = (hash ^ (byte)(value >> 48)) * Prime;
                hash = (hash ^ (byte)(value >> 56)) * Prime;
                _hash = hash;
            }
        }

        private void MixByte(byte value)
        {
            unchecked
            {
                _hash ^= value;
                _hash *= Prime;
            }
        }

        private static ulong Avalanche(ulong value)
        {
            unchecked
            {
                value ^= value >> 33;
                value *= 0xff51afd7ed558ccdUL;
                value ^= value >> 33;
                value *= 0xc4ceb9fe1a85ec53UL;
                value ^= value >> 33;
                return value;
            }
        }
    }
}
