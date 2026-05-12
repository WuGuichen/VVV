using System;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Core.Math
{
    public class Fix64Tests
    {
        [Test]
        public void FromInt_StoresScaledRawValue()
        {
            Assert.AreEqual(Fix64.Scale * 3, Fix64.FromInt(3).RawValue);
            Assert.AreEqual(-Fix64.Scale * 2, Fix64.FromInt(-2).RawValue);
        }

        [Test]
        public void AddSubtractMultiply_ReturnExpectedRawValues()
        {
            Fix64 two = Fix64.FromInt(2);
            Fix64 three = Fix64.FromInt(3);
            Fix64 half = Fix64.Half;

            Assert.AreEqual(Fix64.FromInt(5), two + three);
            Assert.AreEqual(Fix64.FromInt(1), three - two);
            Assert.AreEqual(Fix64.FromInt(6), two * three);
            Assert.AreEqual(Fix64.FromInt(1), two * half);
        }

        [Test]
        public void Divide_TruncatesTowardZero()
        {
            Fix64 one = Fix64.One;
            Fix64 three = Fix64.FromInt(3);

            Assert.AreEqual(333333, (one / three).RawValue);
            Assert.AreEqual(-333333, ((-one) / three).RawValue);
            Assert.AreEqual(333333, Fix64.FromRatio(1, 3).RawValue);
        }

        [Test]
        public void DivideByZero_Throws()
        {
            Assert.Throws<DivideByZeroException>(() =>
            {
                Fix64 ignored = Fix64.One / Fix64.Zero;
            });
        }

        [Test]
        public void AbsMinMaxClamp_WorkWithNegativeValues()
        {
            Fix64 negative = Fix64.FromInt(-2);
            Fix64 one = Fix64.One;
            Fix64 three = Fix64.FromInt(3);

            Assert.AreEqual(Fix64.FromInt(2), negative.Abs());
            Assert.AreEqual(negative, Fix64.Min(negative, one));
            Assert.AreEqual(three, Fix64.Max(one, three));
            Assert.AreEqual(one, Fix64.Clamp(Fix64.Zero, one, three));
            Assert.AreEqual(three, Fix64.Clamp(Fix64.FromInt(4), one, three));
            Assert.AreEqual(Fix64.FromInt(2), Fix64.Clamp(Fix64.FromInt(2), one, three));
        }

        [Test]
        public void Clamp_InvalidRangeThrows()
        {
            Assert.Throws<ArgumentException>(() => Fix64.Clamp(Fix64.One, Fix64.FromInt(2), Fix64.One));
        }

        [Test]
        public void Sqrt_UsesDeterministicIntegerFloor()
        {
            Assert.AreEqual(Fix64.FromInt(2), Fix64.FromInt(4).Sqrt());
            Assert.AreEqual(1414213, Fix64.FromInt(2).Sqrt().RawValue);
        }

        [Test]
        public void Overflow_IsChecked()
        {
            Assert.Throws<OverflowException>(() =>
            {
                Fix64 ignored = Fix64.MaxValue + Fix64.One;
            });

            Assert.Throws<OverflowException>(() =>
            {
                Fix64 ignored = Fix64.FromRaw(long.MaxValue) * Fix64.FromInt(2);
            });
        }
    }
}
