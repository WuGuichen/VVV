using MxFramework.Core.Unity;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Core.Unity
{
    public class VectorExtensionsTests
    {
        [Test]
        public void ScaledToLength_NormalizesBeforeScaling()
        {
            Vector3 source = new Vector3(3f, 4f, 0f);
            Vector3 scaled = source.ScaledToLength(10f);

            Assert.AreEqual(10f, scaled.magnitude, 0.0001f);
            Assert.AreEqual(new Vector3(6f, 8f, 0f), scaled);
        }

        [Test]
        public void AngleTo_ReturnsRadians()
        {
            float angle = Vector3.right.AngleTo(Vector3.up);

            Assert.AreEqual(Mathf.PI * 0.5f, angle, 0.0001f);
        }

        [Test]
        public void AngleTo_NormalizesInputVectors()
        {
            var from = new Vector3(2f, 0f, 0f);
            var to = new Vector3(2f, 2f, 0f);

            float angle = from.AngleTo(to);

            Assert.AreEqual(Mathf.PI * 0.25f, angle, 0.0001f);
        }

        [Test]
        public void AngleTo_WhenVectorIsZero_ReturnsZero()
        {
            float angle = Vector3.zero.AngleTo(Vector3.up);

            Assert.AreEqual(0f, angle);
        }

        [Test]
        public void AngleTo360_ReturnsFullCircleAngle()
        {
            float angle = Vector3.forward.AngleTo360(Vector3.right, Vector3.up);

            Assert.AreEqual(Mathf.PI * 0.5f, angle, 0.0001f);
        }

        [Test]
        public void AngleTo360_WhenAngleIsZero_ReturnsZero()
        {
            float angle = Vector3.forward.AngleTo360(Vector3.forward, Vector3.up);

            Assert.AreEqual(0f, angle);
        }
    }
}
