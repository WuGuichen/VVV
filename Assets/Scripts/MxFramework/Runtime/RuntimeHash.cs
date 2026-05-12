using System;
using System.Collections.Generic;

namespace MxFramework.Runtime
{
    public readonly struct RuntimeHashContext
    {
        public RuntimeHashContext(RuntimeFrame frame)
        {
            Frame = frame;
        }

        public RuntimeFrame Frame { get; }
    }

    public interface IRuntimeHashContributor
    {
        string ContributorId { get; }
        void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator);
    }

    public sealed class RuntimeHashCombiner
    {
        private readonly List<ContributorEntry> _contributors;

        public RuntimeHashCombiner(IEnumerable<IRuntimeHashContributor> contributors)
        {
            _contributors = CopyAndSortContributors(contributors);
        }

        public long ComputeHash(RuntimeFrame frame)
        {
            return ComputeHash(new RuntimeHashContext(frame));
        }

        public long ComputeHash(RuntimeHashContext context)
        {
            var accumulator = new RuntimeHashAccumulator();
            accumulator.AddLong("runtime.frame", context.Frame.Value);

            for (int i = 0; i < _contributors.Count; i++)
            {
                ContributorEntry entry = _contributors[i];
                accumulator.AddStringStable("runtime.contributor.id", entry.ContributorId);
                entry.Contributor.Contribute(context, accumulator);
            }

            return accumulator.ToHash();
        }

        public static long ComputeHash(RuntimeFrame frame, IEnumerable<IRuntimeHashContributor> contributors)
        {
            return new RuntimeHashCombiner(contributors).ComputeHash(frame);
        }

        public static long ComputeHash(RuntimeHashContext context, IEnumerable<IRuntimeHashContributor> contributors)
        {
            return new RuntimeHashCombiner(contributors).ComputeHash(context);
        }

        private static List<ContributorEntry> CopyAndSortContributors(IEnumerable<IRuntimeHashContributor> contributors)
        {
            if (contributors == null)
            {
                throw new ArgumentNullException(nameof(contributors));
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<ContributorEntry>();
            foreach (IRuntimeHashContributor contributor in contributors)
            {
                if (contributor == null)
                {
                    throw new ArgumentException("Runtime hash contributor cannot be null.", nameof(contributors));
                }

                string contributorId = contributor.ContributorId;
                if (string.IsNullOrEmpty(contributorId))
                {
                    throw new ArgumentException("Runtime hash contributor id cannot be null or empty.", nameof(contributors));
                }

                if (!ids.Add(contributorId))
                {
                    throw new ArgumentException("Runtime hash contributor id must be unique. Duplicate id: " + contributorId, nameof(contributors));
                }

                entries.Add(new ContributorEntry(contributorId, contributor));
            }

            entries.Sort(CompareContributors);
            return entries;
        }

        private static int CompareContributors(ContributorEntry left, ContributorEntry right)
        {
            return string.CompareOrdinal(left.ContributorId, right.ContributorId);
        }

        private sealed class ContributorEntry
        {
            public ContributorEntry(string contributorId, IRuntimeHashContributor contributor)
            {
                ContributorId = contributorId;
                Contributor = contributor;
            }

            public string ContributorId { get; }
            public IRuntimeHashContributor Contributor { get; }
        }
    }

    public sealed class RuntimeHashAccumulator
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        private const byte IntField = 1;
        private const byte LongField = 2;
        private const byte DoubleQuantizedField = 3;
        private const byte StringField = 4;

        private ulong _hash;
        private long _fieldCount;

        public RuntimeHashAccumulator()
        {
            _hash = OffsetBasis;
            MixByte(82);
            MixByte(72);
            MixInt(1);
        }

        public void AddInt(string key, int value)
        {
            AddFieldHeader(IntField, key);
            MixInt(value);
        }

        public void AddLong(string key, long value)
        {
            AddFieldHeader(LongField, key);
            MixLong(value);
        }

        public void AddDoubleQuantized(string key, double value, double scale)
        {
            long quantized = QuantizeDouble(value, scale);
            AddFieldHeader(DoubleQuantizedField, key);
            MixLong(quantized);
        }

        public void AddStringStable(string key, string value)
        {
            AddFieldHeader(StringField, key);
            MixString(value);
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
            for (int i = 0; i < value.Length; i++)
            {
                MixChar(value[i]);
            }
        }

        private void MixChar(char value)
        {
            MixByte((byte)value);
            MixByte((byte)(value >> 8));
        }

        private void MixInt(int value)
        {
            MixUInt((uint)value);
        }

        private void MixUInt(uint value)
        {
            MixByte((byte)value);
            MixByte((byte)(value >> 8));
            MixByte((byte)(value >> 16));
            MixByte((byte)(value >> 24));
        }

        private void MixLong(long value)
        {
            MixULong((ulong)value);
        }

        private void MixULong(ulong value)
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
