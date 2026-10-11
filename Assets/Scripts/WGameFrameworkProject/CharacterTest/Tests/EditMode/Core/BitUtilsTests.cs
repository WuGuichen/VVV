using NUnit.Framework;
using MxFramework.Core.Math;

namespace MxFramework.Tests.Core.Math
{
    public class BitUtilsTests
    {
        [Test]
        public void PackPair_Roundtrip()
        {
            int packed = BitUtils.PackPair(0x1234, 0x5678);
            packed.UnpackPair(out int high, out int low);
            Assert.AreEqual(0x1234, high);
            Assert.AreEqual(0x5678, low);
        }

        [Test]
        public void PackQuarter_Roundtrip()
        {
            int packed = BitUtils.PackQuarter(0x12, 0x34, 0x56, 0x78);
            byte[] bytes = packed.UnpackQuarter();
            Assert.AreEqual(0x12, bytes[0]);
            Assert.AreEqual(0x34, bytes[1]);
            Assert.AreEqual(0x56, bytes[2]);
            Assert.AreEqual(0x78, bytes[3]);
        }

        [Test]
        public void PackType_Roundtrip()
        {
            int packed = BitUtils.PackType(100, 5);
            packed.UnpackType(out int id, out int type);
            Assert.AreEqual(100, id);
            Assert.AreEqual(5, type);
        }

        [Test]
        public void UnpackTypeId_ReturnsOnlyId()
        {
            int packed = BitUtils.PackType(255, 15);
            Assert.AreEqual(255, BitUtils.UnpackTypeId(packed));
        }

        [Test]
        public void ToUID_ExtractsLow16Bits()
        {
            Assert.AreEqual(0xABCD, 0x1234ABCD.ToUID());
        }

        [Test]
        public void FirstBitIndex_KnownValues()
        {
            Assert.AreEqual(0, 1.FirstBitIndex());    // bit 0
            Assert.AreEqual(1, 2.FirstBitIndex());    // bit 1
            Assert.AreEqual(2, 4.FirstBitIndex());    // bit 2
            Assert.AreEqual(0, 3.FirstBitIndex());    // bit 0 (0b11)
            Assert.AreEqual(32, 0.FirstBitIndex());   // no bits
        }

        [Test]
        public void IsEmpty_MatchesSentinel()
        {
            Assert.IsTrue(BitUtils.EmptyInt.IsEmpty());
            Assert.IsFalse(0.IsEmpty());
        }

        [Test]
        public void IsNone_ChecksNegativeOne()
        {
            Assert.IsTrue((-1).IsNone());
            Assert.IsFalse(0.IsNone());
        }
    }
}
