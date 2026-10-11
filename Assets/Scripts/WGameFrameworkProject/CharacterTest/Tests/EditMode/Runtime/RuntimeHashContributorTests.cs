using System;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeHashContributorTests
    {
        private const int RootSchema = 91000003;
        private const int ValueSchema = 91000004;
        private const int FrameKey = 1, EntityKey = 2, RevisionKey = 3, PositionKey = 4, StateKey = 5, ValueKey = 6;
        private enum StateCode { Ready = 1 }
        [Test]
        public void RuntimeHashCombiner_SortsContributorsByPredefinedNumericId()
        {
            var firstOrder = new IRuntimeHashContributor[]
            {
                new ValueContributor(3, 3),
                new ValueContributor(1, 1),
                new ValueContributor(2, 2)
            };
            var secondOrder = new IRuntimeHashContributor[]
            {
                new ValueContributor(2, 2),
                new ValueContributor(3, 3),
                new ValueContributor(1, 1)
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
            #if MXFRAMEWORK_RUNTIME_HASH_XXH64
            Assert.AreEqual(-3379802504332647759L, firstHash);
#else
            Assert.AreEqual(6196676476039040620L, firstHash);
#endif
        }

        [Test]
        public void RuntimeHashCombiner_DifferentFrameOrValueChangesHash()
        {
            var contributors = new IRuntimeHashContributor[]
            {
                new ValueContributor(1, 10)
            };

            long frame0 = RuntimeHashCombiner.ComputeHash(RuntimeFrame.Zero, contributors);
            long frame1 = RuntimeHashCombiner.ComputeHash(new RuntimeFrame(1), contributors);
            long valueChanged = RuntimeHashCombiner.ComputeHash(RuntimeFrame.Zero, new IRuntimeHashContributor[]
            {
                new ValueContributor(1, 11)
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
        public void RuntimeHashAccumulator_PreparedNullAndEmptyTextDigestsAreStableAndDistinct()
        {
            long nullFirst = HashPreparedText(null);
            long nullSecond = HashPreparedText(null);
            long emptyFirst = HashPreparedText(string.Empty);
            long emptySecond = HashPreparedText(string.Empty);

            Assert.AreEqual(nullFirst, nullSecond);
            Assert.AreEqual(emptyFirst, emptySecond);
            Assert.AreNotEqual(nullFirst, emptyFirst);
        }

        [Test]
        public void RuntimeHashCombiner_RejectsDuplicateContributorIds()
        {
            Assert.Throws<ArgumentException>(() => new RuntimeHashCombiner(new IRuntimeHashContributor[]
            {
                new ValueContributor(1, 1),
                new ValueContributor(1, 2)
            }));
        }

        private static RuntimeHashAccumulator BuildStableAccumulator()
        {
            var accumulator = new RuntimeHashAccumulator(RootSchema, 1);
            accumulator.AddLong(FrameKey, 9L);
            accumulator.AddInt(EntityKey, 10);
            accumulator.AddLong(RevisionKey, 22L);
            accumulator.AddDoubleQuantized(PositionKey, 1.234d, 1000d);
            accumulator.AddInt(StateKey, (int)StateCode.Ready);
            return accumulator;
        }

        private static long HashQuantizedDouble(double value, double scale)
        {
            var accumulator = new RuntimeHashAccumulator(RootSchema, 1);
            accumulator.AddDoubleQuantized(ValueKey, value, scale);
            return accumulator.ToHash();
        }

        private static long HashPreparedText(string value)
        {
            var accumulator = new RuntimeHashAccumulator(RootSchema, 1);
            accumulator.AddDigest(ValueKey, RuntimeHashDigest.ComputeContentDigest(value));
            return accumulator.ToHash();
        }

        private sealed class ValueContributor : IRuntimeHashContributor
        {
            private readonly int _value;

            public ValueContributor(int contributorId, int value)
            {
                ContributorId = contributorId;
                _value = value;
            }

            public int ContributorId { get; }
            public int HashSchemaId => ValueSchema;
            public int HashSchemaVersion => 1;

            public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
            {
                accumulator.AddInt(ValueKey, _value);
            }
        }
    }
}
