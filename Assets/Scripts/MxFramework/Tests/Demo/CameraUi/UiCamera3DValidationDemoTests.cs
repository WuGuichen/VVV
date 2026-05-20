using MxFramework.Demo.CameraUi;
using NUnit.Framework;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Tests.Demo.CameraUi
{
    public sealed class UiCamera3DValidationDemoTests
    {
        [Test]
        public void ValidateNow_BindsOverlayAndReportsLayerPolicy()
        {
            GameObject root = new GameObject("validation-root");
            GameObject baseGo = new GameObject("base-camera");
            GameObject overlayGo = new GameObject("overlay-camera");
            GameObject uiRoot = new GameObject("ui-root");
            try
            {
                UnityCamera baseCamera = baseGo.AddComponent<UnityCamera>();
                UnityCamera overlayCamera = overlayGo.AddComponent<UnityCamera>();
                UiCamera3DValidationDemo demo = root.AddComponent<UiCamera3DValidationDemo>();
                demo.ConfigureAssets(null, null, null, null, baseCamera, overlayCamera, uiRoot.transform);

                UiCamera3DValidationSnapshot snapshot = demo.ValidateNow();

                Assert.IsTrue(snapshot.StackBound);
                Assert.IsTrue(snapshot.BaseExcludesUiLayer);
                Assert.IsTrue(snapshot.OverlayOnlyUiLayer);
                Assert.IsTrue(snapshot.ObjectOnUiLayer);
                Assert.AreEqual(1, snapshot.StackCount);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(baseGo);
                Object.DestroyImmediate(overlayGo);
                Object.DestroyImmediate(uiRoot);
            }
        }
    }
}
