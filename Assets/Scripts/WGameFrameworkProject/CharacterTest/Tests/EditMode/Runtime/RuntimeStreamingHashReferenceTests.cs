using System;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public sealed class RuntimeStreamingHashReferenceTests
    {
        // Frozen outputs from the independent upstream xxHash v0.8.3 XXH64(data, length, 0).
        // Includes the stripe merge regression at 32 bytes, unaligned writes and long tails.
        [TestCase(0, -1205034819632174695L)]
        [TestCase(1, 2153089070947830424L)]
        [TestCase(2, -4595884172858232412L)]
        [TestCase(3, 294488440918882985L)]
        [TestCase(4, -1699665460079683222L)]
        [TestCase(7, 2626017118473690735L)]
        [TestCase(8, 4219696063009705604L)]
        [TestCase(9, -5005551365189827172L)]
        [TestCase(15, 1621656856277505041L)]
        [TestCase(16, -41321995738652674L)]
        [TestCase(17, 2285213403603385921L)]
        [TestCase(23, -7154032583773012713L)]
        [TestCase(24, -8229121394944824654L)]
        [TestCase(25, -354454200386831638L)]
        [TestCase(31, 865163742872182692L)]
        [TestCase(32, 174152358407405054L)]
        [TestCase(33, -3453969758702594159L)]
        [TestCase(63, -2845219665230953919L)]
        [TestCase(64, 7541677848960369573L)]
        [TestCase(65, -3080682679472019883L)]
        [TestCase(127, 7907368086672461493L)]
        [TestCase(128, -9092538886168774306L)]
        [TestCase(129, 7484767241417387714L)]
        [TestCase(255, -2770281803566968774L)]
        [TestCase(256, 4361977806609268543L)]
        [TestCase(257, 3165356049369142339L)]
        [TestCase(1024, -9072675834521201696L)]
        public void MixedLittleEndianWritesMatchUpstream(int length, long expected)
        {
            var bytes = new byte[length];
            for (int i = 0; i < length; i++) bytes[i] = unchecked((byte)((i * 73 + 19) ^ (i >> 3)));
            for (int prefix = 0; prefix < 8; prefix++)
            for (int mode = 0; mode < 3; mode++)
            {
                var hash = new Xxh64StreamHasher();
                hash.Reset();
                int at = 0;
                while (at < Math.Min(prefix, length)) hash.MixByte(bytes[at++]);
                while (at < length)
                {
                    if (mode == 1 && at + 4 <= length) { hash.MixInt(ReadInt(bytes, at)); at += 4; }
                    else if (mode == 2 && at + 8 <= length) { hash.MixLong(ReadLong(bytes, at)); at += 8; }
                    else hash.MixByte(bytes[at++]);
                }
                Assert.AreEqual(expected, hash.Finalize(), "prefix={0}, mode={1}", prefix, mode);
                Assert.AreEqual(expected, hash.Finalize(), "Finalization must not consume or alter state.");
                hash.Reset();
                foreach (byte value in bytes) hash.MixByte(value);
                Assert.AreEqual(expected, hash.Finalize(), "Reset and byte writes must preserve the same stream.");
            }
        }

        private static int ReadInt(byte[] bytes, int offset) => bytes[offset] | bytes[offset + 1] << 8
            | bytes[offset + 2] << 16 | bytes[offset + 3] << 24;
        private static long ReadLong(byte[] bytes, int offset) => unchecked((long)((ulong)(uint)ReadInt(bytes, offset)
            | (ulong)(uint)ReadInt(bytes, offset + 4) << 32));
    }
}
