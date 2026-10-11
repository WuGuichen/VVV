using System;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public sealed class RuntimeNumericHashCombinerTests
    {
        [Test]
        public void ConstructorOrderDoesNotChangeStateHash()
        {
            var a = new ValueContributor(10, 4); var b = new ValueContributor(2, 7);
            var first = new RuntimeHashCombiner(new IRuntimeHashContributor[] { a, b });
            var second = new RuntimeHashCombiner(new IRuntimeHashContributor[] { b, a });
            Assert.AreEqual(first.ComputeHash(RuntimeFrame.Zero), second.ComputeHash(RuntimeFrame.Zero));
            Assert.AreNotEqual(first.ComputeHash(RuntimeFrame.Zero), first.ComputeHash(new RuntimeFrame(1)));
            long before = first.ComputeHash(RuntimeFrame.Zero);
            a.Value++;
            Assert.AreNotEqual(before, first.ComputeHash(RuntimeFrame.Zero));
        }

        [Test]
        public void InvalidAndDuplicateContributorIdentitiesAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new RuntimeHashCombiner(new IRuntimeHashContributor[] { new ValueContributor(0, 1) }));
            Assert.Throws<ArgumentException>(() => new RuntimeHashCombiner(new IRuntimeHashContributor[] { new ValueContributor(1, 1), new ValueContributor(1, 2) }));
            Assert.Throws<ArgumentException>(() => new RuntimeHashCombiner(new IRuntimeHashContributor[] { null }));
        }

        [Test]
        public void FailedContributionDoesNotPoisonTheNextComputation()
        {
            var contributor = new ValueContributor(1, 1) { Fail = true };
            var combiner = new RuntimeHashCombiner(new IRuntimeHashContributor[] { contributor });
            Assert.Throws<InvalidOperationException>(() => combiner.ComputeHash(RuntimeFrame.Zero));
            contributor.Fail = false;
            Assert.AreEqual(new RuntimeHashCombiner(new IRuntimeHashContributor[] { contributor }).ComputeHash(RuntimeFrame.Zero), combiner.ComputeHash(RuntimeFrame.Zero));
        }

        private sealed class ValueContributor : IRuntimeHashContributor
        {
            public ValueContributor(int id, int value) { ContributorId = id; Value = value; }
            public int ContributorId { get; }
            public int HashSchemaId => 91000003;
            public int HashSchemaVersion => 1;
            public int Value;
            public bool Fail;
            public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
            {
                if (Fail) throw new InvalidOperationException("test failure");
                const int ValueKey = 1;
                accumulator.AddInt(ValueKey, Value);
            }
        }
    }
}
