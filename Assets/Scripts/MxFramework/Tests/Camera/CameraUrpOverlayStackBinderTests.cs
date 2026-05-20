using MxFramework.Camera.URP;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Tests.Camera
{
    public sealed class CameraUrpOverlayStackBinderTests
    {
        [Test]
        public void Bind_AddsOverlayOnceAndSetsRenderTypes()
        {
            GameObject baseGo = new GameObject("base-camera");
            GameObject overlayGo = new GameObject("overlay-camera");
            try
            {
                UnityCamera baseCamera = baseGo.AddComponent<UnityCamera>();
                UnityCamera overlayCamera = overlayGo.AddComponent<UnityCamera>();

                MxCameraUrpStackResult first = MxCameraUrpOverlayStackBinder.Bind(baseCamera, overlayCamera);
                MxCameraUrpStackResult second = MxCameraUrpOverlayStackBinder.Bind(baseCamera, overlayCamera);

                UniversalAdditionalCameraData baseData = baseCamera.GetUniversalAdditionalCameraData();
                UniversalAdditionalCameraData overlayData = overlayCamera.GetUniversalAdditionalCameraData();
                Assert.IsTrue(first.Success);
                Assert.IsTrue(second.Success);
                Assert.AreEqual(CameraRenderType.Base, baseData.renderType);
                Assert.AreEqual(CameraRenderType.Overlay, overlayData.renderType);
                Assert.AreEqual(1, CountInStack(baseData, overlayCamera));
            }
            finally
            {
                Object.DestroyImmediate(baseGo);
                Object.DestroyImmediate(overlayGo);
            }
        }

        [Test]
        public void ValidateBound_ReturnsDiagnosticWhenOverlayMissing()
        {
            GameObject baseGo = new GameObject("base-camera");
            GameObject overlayGo = new GameObject("overlay-camera");
            try
            {
                UnityCamera baseCamera = baseGo.AddComponent<UnityCamera>();
                UnityCamera overlayCamera = overlayGo.AddComponent<UnityCamera>();
                baseCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Base;
                overlayCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;

                MxCameraUrpStackResult result = MxCameraUrpOverlayStackBinder.ValidateBound(baseCamera, overlayCamera);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(MxCameraUrpDiagnosticCodes.MissingOverlayCamera, result.Code);
            }
            finally
            {
                Object.DestroyImmediate(baseGo);
                Object.DestroyImmediate(overlayGo);
            }
        }

        [Test]
        public void Unbind_RemovesOverlayAndNullEntries()
        {
            GameObject baseGo = new GameObject("base-camera");
            GameObject overlayGo = new GameObject("overlay-camera");
            try
            {
                UnityCamera baseCamera = baseGo.AddComponent<UnityCamera>();
                UnityCamera overlayCamera = overlayGo.AddComponent<UnityCamera>();
                MxCameraUrpOverlayStackBinder.Bind(baseCamera, overlayCamera);

                MxCameraUrpStackResult result = MxCameraUrpOverlayStackBinder.Unbind(baseCamera, overlayCamera);

                Assert.IsTrue(result.Success);
                Assert.AreEqual(0, CountInStack(baseCamera.GetUniversalAdditionalCameraData(), overlayCamera));
            }
            finally
            {
                Object.DestroyImmediate(baseGo);
                Object.DestroyImmediate(overlayGo);
            }
        }

        [Test]
        public void Bind_MissingBaseCamera_ReturnsDiagnostic()
        {
            GameObject overlayGo = new GameObject("overlay-camera");
            try
            {
                UnityCamera overlayCamera = overlayGo.AddComponent<UnityCamera>();

                MxCameraUrpStackResult result = MxCameraUrpOverlayStackBinder.Bind(null, overlayCamera);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(MxCameraUrpDiagnosticCodes.MissingBaseCamera, result.Code);
            }
            finally
            {
                Object.DestroyImmediate(overlayGo);
            }
        }

        [Test]
        public void Bind_WhenRenderTypesAreStrict_ReturnsDiagnostic()
        {
            GameObject baseGo = new GameObject("base-camera");
            GameObject overlayGo = new GameObject("overlay-camera");
            try
            {
                UnityCamera baseCamera = baseGo.AddComponent<UnityCamera>();
                UnityCamera overlayCamera = overlayGo.AddComponent<UnityCamera>();
                baseCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
                overlayCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;

                MxCameraUrpStackResult result = MxCameraUrpOverlayStackBinder.Bind(baseCamera, overlayCamera, setRenderTypes: false);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(MxCameraUrpDiagnosticCodes.InvalidBaseRenderType, result.Code);
            }
            finally
            {
                Object.DestroyImmediate(baseGo);
                Object.DestroyImmediate(overlayGo);
            }
        }

        private static int CountInStack(UniversalAdditionalCameraData baseData, UnityCamera overlayCamera)
        {
            int count = 0;
            for (int i = 0; i < baseData.cameraStack.Count; i++)
            {
                if (baseData.cameraStack[i] == overlayCamera)
                    count++;
            }

            return count;
        }
    }
}
