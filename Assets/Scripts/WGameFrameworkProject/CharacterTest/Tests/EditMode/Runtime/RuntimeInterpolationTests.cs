using System;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeInterpolationTests
    {
        [Test]
        public void Lerp_InterpolatesLinearly()
        {
            Assert.AreEqual(10f, RuntimeFloatInterpolator.Lerp(10f, 20f, 0f));
            Assert.AreEqual(15f, RuntimeFloatInterpolator.Lerp(10f, 20f, 0.5f));
            Assert.AreEqual(20f, RuntimeFloatInterpolator.Lerp(10f, 20f, 1f));
        }

        [Test]
        public void EasingFunctions_AreMonotonicAndBounded()
        {
            AssertEasingMonotonicAndBounded(RuntimeEasing.Linear);
            AssertEasingMonotonicAndBounded(RuntimeEasing.EaseIn);
            AssertEasingMonotonicAndBounded(RuntimeEasing.EaseOut);
            AssertEasingMonotonicAndBounded(RuntimeEasing.EaseInOut);
        }

        [Test]
        public void ZeroDurationTween_CompletesImmediately()
        {
            var tween = new RuntimeTween(2f, 8f, 0f);

            Assert.IsTrue(tween.IsComplete);
            Assert.AreEqual(1f, tween.Progress);
            Assert.AreEqual(8f, tween.Value);
            Assert.AreEqual(8f, tween.Tick(0f));
        }

        [Test]
        public void Tick_ClampsToEnd()
        {
            var tween = new RuntimeTween(0f, 10f, 1f);

            var value = tween.Tick(2f);

            Assert.AreEqual(10f, value);
            Assert.AreEqual(10f, tween.Value);
            Assert.AreEqual(1f, tween.Elapsed);
            Assert.AreEqual(1f, tween.Progress);
            Assert.IsTrue(tween.IsComplete);
        }

        [Test]
        public void NegativeDuration_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeTween(0f, 1f, -0.01f));
        }

        [Test]
        public void NegativeDelta_Throws()
        {
            var tween = new RuntimeTween(0f, 1f, 1f);

            Assert.Throws<ArgumentOutOfRangeException>(() => tween.Tick(-0.01f));
        }

        [Test]
        public void Tick_UsesExplicitDeltaDeterministically()
        {
            var first = new RuntimeTween(0f, 100f, 2f, RuntimeEasing.EaseInOut);
            var second = new RuntimeTween(0f, 100f, 2f, RuntimeEasing.EaseInOut);

            first.Tick(0.25f);
            first.Tick(0.5f);
            first.Tick(0.75f);

            second.Tick(0.25f);
            second.Tick(0.5f);
            second.Tick(0.75f);

            Assert.AreEqual(first.Elapsed, second.Elapsed);
            Assert.AreEqual(first.Progress, second.Progress);
            Assert.AreEqual(first.Value, second.Value);
        }

        private static void AssertEasingMonotonicAndBounded(RuntimeEasing easing)
        {
            var previous = RuntimeEasingFunctions.Evaluate(easing, 0f);
            Assert.AreEqual(0f, previous);

            for (var index = 1; index <= 20; index++)
            {
                var current = RuntimeEasingFunctions.Evaluate(easing, index / 20f);
                Assert.GreaterOrEqual(current, previous);
                Assert.GreaterOrEqual(current, 0f);
                Assert.LessOrEqual(current, 1f);
                previous = current;
            }

            Assert.AreEqual(1f, previous);
        }
    }
}
