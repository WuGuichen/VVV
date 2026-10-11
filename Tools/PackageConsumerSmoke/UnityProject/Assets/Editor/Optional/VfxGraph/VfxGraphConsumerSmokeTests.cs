using MxFramework.Vfx.Unity.VfxGraph;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.PackageConsumerSmoke
{
    public sealed class VfxGraphConsumerSmokeTests
    {
        [Test]
        public void VfxGraphAdapter_HandlesEmptyController()
        {
            var gameObject = new GameObject("VfxGraphConsumerSmoke");
            try
            {
                var controller = gameObject.AddComponent<UnityVfxGraphController>();

                Assert.That(controller.IsAvailable, Is.False);
                controller.StopAndClear();
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
