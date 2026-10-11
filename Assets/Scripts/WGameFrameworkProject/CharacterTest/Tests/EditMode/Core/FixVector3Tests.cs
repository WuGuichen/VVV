using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Core.Math
{
    public class FixVector3Tests
    {
        [Test]
        public void AddSubtractAndScale_ReturnExpectedValues()
        {
            var left = new FixVector3(Fix64.One, Fix64.FromInt(2), Fix64.FromInt(3));
            var right = new FixVector3(Fix64.FromInt(4), Fix64.FromInt(5), Fix64.FromInt(6));

            Assert.AreEqual(
                new FixVector3(Fix64.FromInt(5), Fix64.FromInt(7), Fix64.FromInt(9)),
                left + right);
            Assert.AreEqual(
                new FixVector3(Fix64.FromInt(3), Fix64.FromInt(3), Fix64.FromInt(3)),
                right - left);
            Assert.AreEqual(
                new FixVector3(Fix64.FromInt(2), Fix64.FromInt(4), Fix64.FromInt(6)),
                left * Fix64.FromInt(2));
        }

        [Test]
        public void DotAndLengthSquared_ReturnExpectedValues()
        {
            var value = new FixVector3(Fix64.One, Fix64.FromInt(2), Fix64.FromInt(3));

            Assert.AreEqual(Fix64.FromInt(14), value.LengthSquared());
            Assert.AreEqual(Fix64.FromInt(32), value.Dot(new FixVector3(Fix64.FromInt(4), Fix64.FromInt(5), Fix64.FromInt(6))));
        }

        [Test]
        public void TryNormalize_ZeroVectorReturnsFalse()
        {
            bool result = FixVector3.Zero.TryNormalize(out FixVector3 normalized);

            Assert.IsFalse(result);
            Assert.AreEqual(FixVector3.Zero, normalized);
        }

        [Test]
        public void TryNormalize_UsesDeterministicSqrt()
        {
            var value = new FixVector3(Fix64.FromInt(3), Fix64.FromInt(4), Fix64.Zero);

            bool result = value.TryNormalize(out FixVector3 normalized);

            Assert.IsTrue(result);
            Assert.AreEqual(600000, normalized.X.RawValue);
            Assert.AreEqual(800000, normalized.Y.RawValue);
            Assert.AreEqual(0, normalized.Z.RawValue);
            Assert.AreEqual(Fix64.One, normalized.LengthSquared());
        }
    }
}
