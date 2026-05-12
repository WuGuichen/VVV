using MxFramework.Core.Unity;
using NUnit.Framework;

namespace MxFramework.Tests.Core.Unity
{
    public class RandomTableTests
    {
        [Test]
        public void Tables_HaveExpectedSizes()
        {
            Assert.AreEqual(100, RandomTable.OnUnitSphere.Length);
            Assert.AreEqual(100, RandomTable.Float01.Length);
            Assert.AreEqual(256, RandomTable.Int0To100.Length);
        }

        [Test]
        public void NextIndex_ReturnsValueFromByteTable()
        {
            int index = RandomTable.NextIndex();

            Assert.GreaterOrEqual(index, 0);
            Assert.LessOrEqual(index, 100);
        }

        [Test]
        public void OnSphere_ReturnsUnitVector()
        {
            Assert.AreEqual(1f, RandomTable.OnSphere(0).magnitude, 0.0001f);
        }

        [Test]
        public void Float_ReturnsStoredValue()
        {
            Assert.AreEqual(RandomTable.Float01[0], RandomTable.Float(0));
        }
    }
}
