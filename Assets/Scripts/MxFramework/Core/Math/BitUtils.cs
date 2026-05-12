// Source: WGame/Client/Assets/Scripts/Common/IntExtension.cs (generic portions only)
// Migrated: 2026-05-05 — Batch 1: Core utilities

namespace MxFramework.Core.Math
{
    /// <summary>
    /// Bit-packing utilities for encoding multiple small integers into one int.
    /// </summary>
    public static class BitUtils
    {
        public const int EmptyInt = -261354592;

        /// <summary>Pack two int16 values into one int32 (high, low)</summary>
        public static int PackPair(int high, int low) => (high << 16) | (low & 0xFFFF);

        /// <summary>Unpack a packed pair (high, low)</summary>
        public static void UnpackPair(this int packed, out int high, out int low)
        {
            high = packed >> 16;
            low = packed & 0xFFFF;
        }

        /// <summary>Pack four bytes into one int32 (msb first)</summary>
        public static int PackQuarter(int b0, int b1, int b2, int b3)
            => (b0 << 24) + (b1 << 16) + (b2 << 8) + b3;

        /// <summary>Unpack into four bytes (msb first)</summary>
        public static byte[] UnpackQuarter(this int packed)
        {
            return new byte[]
            {
                (byte)(packed >> 24),
                (byte)(packed >> 16),
                (byte)(packed >> 8),
                (byte)packed,
            };
        }

        /// <summary>Unpack into four ints for sorting (msb first)</summary>
        public static int[] UnpackQuarterToInts(this int packed)
        {
            int b3 = packed >> 24;
            int b2 = (packed >> 16) & 0xFF;
            int b1 = (packed >> 8) & 0xFF;
            int b0 = packed & 0xFF;
            return new[] { b3, b2, b1, b0 };
        }

        /// <summary>Pack an ID and type into one int (ID << 4 | type)</summary>
        public static int PackType(int id, int type) => (id << 4) | (type & 0xF);

        /// <summary>Unpack type-tagged ID</summary>
        public static void UnpackType(this int packed, out int id, out int type)
        {
            id = packed >> 4;
            type = packed & 0xF;
        }

        /// <summary>Unpack type-tagged ID (ID only)</summary>
        public static int UnpackTypeId(this int packed) => packed >> 4;

        /// <summary>Extract low 16 bits as UID</summary>
        public static int ToUID(this int v) => v & 0xFFFF;

        /// <summary>Trailing zero count (software fallback, zero-allocation)</summary>
        public static int FirstBitIndex(this int mask)
        {
            // Software trailing zero count — no external dependency
            if (mask == 0) return 32;
            int count = 0;
            while ((mask & 1) == 0)
            {
                mask >>= 1;
                count++;
            }
            return count;
        }

        /// <summary>Check if value is the empty sentinel</summary>
        public static bool IsEmpty(this int v) => v == EmptyInt;

        /// <summary>Check if value is -1 (none)</summary>
        public static bool IsNone(this int v) => v == -1;
    }
}
