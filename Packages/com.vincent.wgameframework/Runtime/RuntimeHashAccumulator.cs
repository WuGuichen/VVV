using System;
using System.Runtime.CompilerServices;

namespace MxFramework.Runtime
{
    /// <summary>The sole runtime state-hash accumulator. V3 is fixed-width, little-endian and string-key free.</summary>
    public sealed class RuntimeHashAccumulator
    {
        public const int EncodingVersion = 3;
#if MXFRAMEWORK_RUNTIME_HASH_XXH64
        public const int AlgorithmId = 2;
#else
        public const int AlgorithmId = 1;
#endif
        public const int MaximumDepth = 64;
        private const byte IntType = 1, LongType = 2, QuantizedType = 3, BoolType = 4, DigestType = 5;
        private const byte ObjectType = 16, ArrayType = 17, ElementType = 18, AbsentObjectType = 19;
        private readonly byte[] _scopeTypes = new byte[MaximumDepth];
        private readonly int[] _arrayLengths = new int[MaximumDepth];
        private readonly int[] _arrayWritten = new int[MaximumDepth];
        private readonly int _schemaId;
        private readonly int _schemaVersion;
#if MXFRAMEWORK_RUNTIME_HASH_XXH64
        private Xxh64StreamHasher _xxh64;
#else
        private Fnv1aStreamHasher _fnv1a;
#endif
        private int _depth;
        private long _fieldCount;
        private long _encodedByteCount;

        public RuntimeHashAccumulator(int schemaId, int schemaVersion)
        {
            ValidateSchema(schemaId, schemaVersion);
            _schemaId = schemaId;
            _schemaVersion = schemaVersion;
            Reset();
        }

        public int SchemaId => _schemaId;
        public int SchemaVersion => _schemaVersion;
        public long FieldCount => _fieldCount;
        public long EncodedByteCount => _encodedByteCount;

        public void Reset()
        {
            _depth = 0; _fieldCount = 0; _encodedByteCount = 0;
#if MXFRAMEWORK_RUNTIME_HASH_XXH64
            _xxh64.Reset();
#else
            _fnv1a.Reset();
#endif
            MixByte(0x52); MixByte(0x48); MixInt(EncodingVersion); MixInt(AlgorithmId);
            MixInt(_schemaId); MixInt(_schemaVersion);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] public void AddInt(int key, int value) { Header(IntType, key); MixInt(value); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public void AddLong(int key, long value) { Header(LongType, key); MixLong(value); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public void AddBool(int key, bool value) { Header(BoolType, key); MixByte(value ? (byte)1 : (byte)0); }

        public void AddDoubleQuantized(int key, double value, double scale)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentException("Hash values must be finite.", nameof(value));
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0d) throw new ArgumentOutOfRangeException(nameof(scale));
            double scaled = value * scale;
            if (double.IsNaN(scaled) || double.IsInfinity(scaled)) throw new ArgumentOutOfRangeException(nameof(value));
            long quantized;
            try { quantized = checked((long)Math.Round(scaled, MidpointRounding.AwayFromZero)); }
            catch (OverflowException) { throw new ArgumentOutOfRangeException(nameof(value)); }
            Header(QuantizedType, key); MixLong(quantized);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] public void AddDigest(int key, in RuntimeHashDigest digest)
        {
            Header(DigestType, key);
            MixLong(unchecked((long)digest.Word0)); MixLong(unchecked((long)digest.Word1));
            MixLong(unchecked((long)digest.Word2)); MixLong(unchecked((long)digest.Word3));
        }

        public void BeginObject(int key, int schemaId, int schemaVersion, bool present = true)
        {
            ValidateSchema(schemaId, schemaVersion); EnsureDepth(); Header(ObjectType, key);
            MixInt(schemaId); MixInt(schemaVersion); MixByte(present ? (byte)1 : (byte)0);
            _scopeTypes[_depth++] = present ? ObjectType : AbsentObjectType;
        }
        public void EndObject()
        {
            if (_depth == 0 || (_scopeTypes[_depth - 1] != ObjectType && _scopeTypes[_depth - 1] != AbsentObjectType)) throw new InvalidOperationException("No object scope is open.");
            _depth--; MixByte(240);
        }
        public void BeginArray(int key, int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            EnsureDepth(); Header(ArrayType, key); MixInt(count);
            _scopeTypes[_depth] = ArrayType; _arrayLengths[_depth] = count; _arrayWritten[_depth] = 0; _depth++;
        }
        public void BeginElement(int index)
        {
            if (_depth == 0 || _scopeTypes[_depth - 1] != ArrayType) throw new InvalidOperationException("Elements require an array scope.");
            int array = _depth - 1;
            if (index != _arrayWritten[array] || index >= _arrayLengths[array]) throw new ArgumentOutOfRangeException(nameof(index));
            EnsureDepth(); MixByte(ElementType); MixInt(index); _scopeTypes[_depth++] = ElementType;
        }
        public void EndElement()
        {
            if (_depth < 2 || _scopeTypes[_depth - 1] != ElementType) throw new InvalidOperationException("No element scope is open.");
            _depth--; _arrayWritten[_depth - 1]++; MixByte(242);
        }
        public void EndArray()
        {
            if (_depth == 0 || _scopeTypes[_depth - 1] != ArrayType) throw new InvalidOperationException("No array scope is open.");
            if (_arrayWritten[_depth - 1] != _arrayLengths[_depth - 1]) throw new InvalidOperationException("Array element count does not match its declaration.");
            _depth--; MixByte(241);
        }

        public long ToHash()
        {
            if (_depth != 0) throw new InvalidOperationException("All hash scopes must be closed.");
#if MXFRAMEWORK_RUNTIME_HASH_XXH64
            return _xxh64.Finalize();
#else
            return _fnv1a.Finalize(_fieldCount);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] private void MixByte(byte value)
        {
#if MXFRAMEWORK_RUNTIME_HASH_XXH64
            _xxh64.MixByte(value);
#else
            _fnv1a.MixByte(value);
#endif
            _encodedByteCount++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private void MixInt(int value)
        {
#if MXFRAMEWORK_RUNTIME_HASH_XXH64
            _xxh64.MixInt(value);
#else
            _fnv1a.MixInt(value);
#endif
            _encodedByteCount += 4;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] private void MixLong(long value)
        {
#if MXFRAMEWORK_RUNTIME_HASH_XXH64
            _xxh64.MixLong(value);
#else
            _fnv1a.MixLong(value);
#endif
            _encodedByteCount += 8;
        }
        private void Header(byte type, int key)
        {
            if (key <= 0) throw new ArgumentOutOfRangeException(nameof(key));
            if (_depth > 0 && (_scopeTypes[_depth - 1] == ArrayType || _scopeTypes[_depth - 1] == AbsentObjectType)) throw new InvalidOperationException("Fields require a present object or array element.");
            _fieldCount++; MixByte(167); MixInt(key); MixByte(type);
        }
        private static void ValidateSchema(int schemaId, int schemaVersion)
        {
            if (schemaId <= 0) throw new ArgumentOutOfRangeException(nameof(schemaId));
            if (schemaVersion <= 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }
        private void EnsureDepth() { if (_depth == MaximumDepth) throw new InvalidOperationException("Hash scope depth limit exceeded."); }

    }
}
