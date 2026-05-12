using System;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeHashContributorTests
    {
        [Test]
        public void RuntimeHashCombiner_SortsContributorsByStableId()
        {
            var firstOrder = new IRuntimeHashContributor[]
            {
                new ValueContributor("z.state", 3),
                new ValueContributor("a.state", 1),
                new ValueContributor("m.state", 2)
            };
            var secondOrder = new IRuntimeHashContributor[]
            {
                new ValueContributor("m.state", 2),
                new ValueContributor("z.state", 3),
                new ValueContributor("a.state", 1)
            };

            long firstHash = RuntimeHashCombiner.ComputeHash(new RuntimeFrame(7), firstOrder);
            long secondHash = RuntimeHashCombiner.ComputeHash(new RuntimeFrame(7), secondOrder);

            Assert.AreEqual(firstHash, secondHash);
        }

        [Test]
        public void RuntimeHashAccumulator_FixedFieldOrderProducesStableHash()
        {
            long firstHash = BuildStableAccumulator().ToHash();
            long secondHash = BuildStableAccumulator().ToHash();

            Assert.AreEqual(firstHash, secondHash);
            Assert.AreEqual(253104892703202921L, firstHash);
        }

        [Test]
        public void RuntimeHashCombiner_DifferentFrameOrValueChangesHash()
        {
            var contributors = new IRuntimeHashContributor[]
            {
                new ValueContributor("state", 10)
            };

            long frame0 = RuntimeHashCombiner.ComputeHash(RuntimeFrame.Zero, contributors);
            long frame1 = RuntimeHashCombiner.ComputeHash(new RuntimeFrame(1), contributors);
            long valueChanged = RuntimeHashCombiner.ComputeHash(RuntimeFrame.Zero, new IRuntimeHashContributor[]
            {
                new ValueContributor("state", 11)
            });

            Assert.AreNotEqual(frame0, frame1);
            Assert.AreNotEqual(frame0, valueChanged);
        }

        [Test]
        public void RuntimeHashAccumulator_DoubleQuantizationIsStable()
        {
            long lower = HashQuantizedDouble(1.001d, 100d);
            long sameBucket = HashQuantizedDouble(1.004d, 100d);
            long differentBucket = HashQuantizedDouble(1.006d, 100d);

            Assert.AreEqual(lower, sameBucket);
            Assert.AreNotEqual(lower, differentBucket);
        }

        [Test]
        public void RuntimeHashAccumulator_NullAndEmptyStringsAreStableAndDistinct()
        {
            long nullFirst = HashStableString(null);
            long nullSecond = HashStableString(null);
            long emptyFirst = HashStableString(string.Empty);
            long emptySecond = HashStableString(string.Empty);

            Assert.AreEqual(nullFirst, nullSecond);
            Assert.AreEqual(emptyFirst, emptySecond);
            Assert.AreNotEqual(nullFirst, emptyFirst);
        }

        [Test]
        public void RuntimeHashCombiner_RejectsDuplicateContributorIds()
        {
            Assert.Throws<ArgumentException>(() => new RuntimeHashCombiner(new IRuntimeHashContributor[]
            {
                new ValueContributor("duplicate", 1),
                new ValueContributor("duplicate", 2)
            }));
        }

        private static RuntimeHashAccumulator BuildStableAccumulator()
        {
            var accumulator = new RuntimeHashAccumulator();
            accumulator.AddLong("runtime.frame", 9L);
            accumulator.AddInt("entity.id", 10);
            accumulator.AddLong("revision", 22L);
            accumulator.AddDoubleQuantized("position.x", 1.234d, 1000d);
            accumulator.AddStringStable("state", "ready");
            return accumulator;
        }

        private static long HashQuantizedDouble(double value, double scale)
        {
            var accumulator = new RuntimeHashAccumulator();
            accumulator.AddDoubleQuantized("value", value, scale);
            return accumulator.ToHash();
        }

        private static long HashStableString(string value)
        {
            var accumulator = new RuntimeHashAccumulator();
            accumulator.AddStringStable("value", value);
            return accumulator.ToHash();
        }

        private sealed class ValueContributor : IRuntimeHashContributor
        {
            private readonly int _value;

            public ValueContributor(string contributorId, int value)
            {
                ContributorId = contributorId;
                _value = value;
            }

            public string ContributorId { get; }

            public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
            {
                accumulator.AddInt("value", _value);
            }
        }
    }
}
