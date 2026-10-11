using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeDeterministicRandomTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var first = new DeterministicRandom(12345u);
            var second = new DeterministicRandom(12345u);

            for (int i = 0; i < 32; i++)
            {
                Assert.AreEqual(first.NextInt(-1000, 1000), second.NextInt(-1000, 1000));
                Assert.AreEqual(first.NextFloat01(), second.NextFloat01());
                Assert.AreEqual(first.Chance(0.35f), second.Chance(0.35f));
            }
        }

        [Test]
        public void DifferentSeed_ProducesDifferentSequence()
        {
            var first = new DeterministicRandom(1u);
            var second = new DeterministicRandom(2u);
            bool anyDifference = false;

            for (int i = 0; i < 16; i++)
            {
                if (first.NextInt(0, 100000) != second.NextInt(0, 100000))
                {
                    anyDifference = true;
                    break;
                }
            }

            Assert.IsTrue(anyDifference);
        }

        [Test]
        public void NextInt_UsesInclusiveExclusiveBounds()
        {
            var random = new DeterministicRandom(7u);

            for (int i = 0; i < 256; i++)
            {
                int value = random.NextInt(-3, 4);

                Assert.GreaterOrEqual(value, -3);
                Assert.Less(value, 4);
            }

            Assert.AreEqual(5, random.NextInt(5, 6));
        }

        [Test]
        public void NextInt_RejectsInvalidRange()
        {
            var random = new DeterministicRandom(7u);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => random.NextInt(1, 1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => random.NextInt(2, 1));
        }

        [Test]
        public void NextFloat01_StaysInRange()
        {
            var random = new DeterministicRandom(99u);

            for (int i = 0; i < 256; i++)
            {
                float value = random.NextFloat01();

                Assert.GreaterOrEqual(value, 0f);
                Assert.Less(value, 1f);
            }
        }

        [Test]
        public void Chance_HandlesBoundariesWithoutDrawing()
        {
            var random = new DeterministicRandom(123u);
            RuntimeRandomState before = random.CaptureState();

            Assert.IsFalse(random.Chance(0f));
            Assert.IsTrue(random.Chance(1f));

            RuntimeRandomState after = random.CaptureState();
            Assert.AreEqual(before.DrawCount, after.DrawCount);
            Assert.AreEqual(before.State, after.State);
        }

        [Test]
        public void Chance_RejectsInvalidProbability()
        {
            var random = new DeterministicRandom(123u);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => random.Chance(-0.001f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => random.Chance(1.001f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => random.Chance(float.NaN));
        }

        [Test]
        public void CaptureRestore_ContinuesSequence()
        {
            var baseline = new DeterministicRandom(555u);
            var restored = new DeterministicRandom(999u);

            baseline.NextInt(0, 100);
            baseline.NextFloat01();
            RuntimeRandomState captured = baseline.CaptureState();

            int expectedInt = baseline.NextInt(-50, 50);
            float expectedFloat = baseline.NextFloat01();
            bool expectedChance = baseline.Chance(0.75f);

            restored.RestoreState(captured);

            Assert.AreEqual(expectedInt, restored.NextInt(-50, 50));
            Assert.AreEqual(expectedFloat, restored.NextFloat01());
            Assert.AreEqual(expectedChance, restored.Chance(0.75f));
        }

        [Test]
        public void RuntimeRandomState_RoundtripsJson()
        {
            var random = new DeterministicRandom(777u);
            random.NextInt(0, 10);
            random.NextFloat01();

            RuntimeRandomState state = random.CaptureState();
            string json = RuntimeRandomStateJson.SaveToJson(state);
            RuntimeRandomState loaded = RuntimeRandomStateJson.LoadFromJson(json);
            var restored = new DeterministicRandom(1u);
            restored.RestoreState(loaded);

            Assert.AreEqual(state.AlgorithmId, loaded.AlgorithmId);
            Assert.AreEqual(state.Seed, loaded.Seed);
            Assert.AreEqual(state.State, loaded.State);
            Assert.AreEqual(state.DrawCount, loaded.DrawCount);
            Assert.AreEqual(random.NextInt(0, 1000), restored.NextInt(0, 1000));
        }
    }
}
