using System.Collections.Generic;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeVersioningTests
    {
        [Test]
        public void VersionToken_DefaultAndExplicitValuesCompareByVersion()
        {
            var defaultToken = default(VersionToken);
            var first = new VersionToken(1);
            var same = new VersionToken(1);
            var different = new VersionToken(2);

            Assert.AreEqual(0, defaultToken.Version);
            Assert.AreEqual(first, same);
            Assert.IsTrue(first == same);
            Assert.IsTrue(first != different);
            Assert.AreEqual("1", first.ToString());
        }

        [Test]
        public void DirtyFlag_DefaultStateIsCleanWithZeroVersion()
        {
            var flag = new DirtyFlag();

            Assert.IsFalse(flag.IsDirty);
            Assert.AreEqual(0, flag.Version);
            Assert.IsFalse(flag.Consume());
            Assert.AreEqual(0, flag.Version);
        }

        [Test]
        public void DirtyFlag_MarkDirtyIncrementsVersionAndConsumeClears()
        {
            var flag = new DirtyFlag();

            flag.MarkDirty();

            Assert.IsTrue(flag.IsDirty);
            Assert.AreEqual(1, flag.Version);
            Assert.IsTrue(flag.Consume());
            Assert.IsFalse(flag.IsDirty);
            Assert.AreEqual(1, flag.Version);
        }

        [Test]
        public void DirtyFlag_RepeatedConsumeReturnsFalseWithoutChangingVersion()
        {
            var flag = new DirtyFlag();

            flag.MarkDirty();
            flag.MarkDirty();

            Assert.AreEqual(2, flag.Version);
            Assert.IsTrue(flag.Consume());
            Assert.IsFalse(flag.Consume());
            Assert.IsFalse(flag.Consume());
            Assert.AreEqual(2, flag.Version);
        }

        [Test]
        public void VersionedValue_DefaultStateUsesDefaultValueAndZeroVersion()
        {
            var value = new VersionedValue<int>();

            Assert.AreEqual(0, value.Value);
            Assert.AreEqual(0, value.Version);
        }

        [Test]
        public void VersionedValue_EqualValueDoesNotIncrementVersion()
        {
            var value = new VersionedValue<int>(10);

            Assert.IsFalse(value.Set(10));

            Assert.AreEqual(10, value.Value);
            Assert.AreEqual(0, value.Version);
        }

        [Test]
        public void VersionedValue_ChangedValueIncrementsVersion()
        {
            var value = new VersionedValue<int>(10);

            Assert.IsTrue(value.Set(20));
            Assert.IsTrue(value.Set(30));

            Assert.AreEqual(30, value.Value);
            Assert.AreEqual(2, value.Version);
        }

        [Test]
        public void VersionedValue_CustomComparerControlsChangeDetection()
        {
            var value = new VersionedValue<string>("alpha", System.StringComparer.OrdinalIgnoreCase);

            Assert.IsFalse(value.Set("ALPHA"));
            Assert.AreEqual(0, value.Version);

            Assert.IsTrue(value.Set("beta"));
            Assert.AreEqual("beta", value.Value);
            Assert.AreEqual(1, value.Version);
        }

        [Test]
        public void VersionedValue_ComparerConstructorStartsWithDefaultValue()
        {
            var value = new VersionedValue<List<int>>(ListCountComparer.Instance);

            Assert.IsFalse(value.Set(null));
            Assert.IsTrue(value.Set(new List<int> { 1 }));
            Assert.IsFalse(value.Set(new List<int> { 9 }));
            Assert.IsTrue(value.Set(new List<int> { 1, 2 }));
            Assert.AreEqual(2, value.Version);
        }

        private sealed class ListCountComparer : IEqualityComparer<List<int>>
        {
            public static readonly ListCountComparer Instance = new ListCountComparer();

            public bool Equals(List<int> x, List<int> y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x == null || y == null)
                {
                    return false;
                }

                return x.Count == y.Count;
            }

            public int GetHashCode(List<int> obj)
            {
                return obj == null ? 0 : obj.Count;
            }
        }
    }
}
