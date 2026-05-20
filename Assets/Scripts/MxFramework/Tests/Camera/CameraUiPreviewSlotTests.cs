using MxFramework.Camera.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Tests.Camera
{
    public sealed class CameraUiPreviewSlotTests
    {
        [Test]
        public void EnsureTexture_AssignsRenderTextureToCamera()
        {
            GameObject go = new GameObject("preview-camera");
            try
            {
                UnityCamera camera = go.AddComponent<UnityCamera>();
                using (var slot = new MxUiPreviewCameraSlot(camera))
                {
                    MxUiPreviewCameraSlotResult result = slot.EnsureTexture(128, 64);

                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(128, result.Width);
                    Assert.AreEqual(64, result.Height);
                    Assert.IsTrue(result.TextureAssigned);
                    Assert.IsNotNull(slot.TargetTexture);
                    Assert.AreSame(slot.TargetTexture, camera.targetTexture);
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EnsureTexture_Resize_ReplacesOldTexture()
        {
            GameObject go = new GameObject("preview-camera");
            try
            {
                UnityCamera camera = go.AddComponent<UnityCamera>();
                using (var slot = new MxUiPreviewCameraSlot(camera))
                {
                    slot.EnsureTexture(128, 64);
                    RenderTexture oldTexture = slot.TargetTexture;

                    MxUiPreviewCameraSlotResult result = slot.EnsureTexture(256, 128);

                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(256, result.Width);
                    Assert.AreEqual(128, result.Height);
                    Assert.IsNotNull(slot.TargetTexture);
                    Assert.AreNotSame(oldTexture, slot.TargetTexture);
                    Assert.AreSame(slot.TargetTexture, camera.targetTexture);
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ReleaseTexture_ClearsCameraTargetTexture()
        {
            GameObject go = new GameObject("preview-camera");
            try
            {
                UnityCamera camera = go.AddComponent<UnityCamera>();
                using (var slot = new MxUiPreviewCameraSlot(camera))
                {
                    slot.EnsureTexture(128, 64);

                    MxUiPreviewCameraSlotResult result = slot.ReleaseTexture();

                    Assert.IsTrue(result.Success);
                    Assert.IsFalse(result.TextureAssigned);
                    Assert.IsNull(slot.TargetTexture);
                    Assert.IsNull(camera.targetTexture);
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EnsureTexture_MissingCamera_ReturnsDiagnostic()
        {
            using (var slot = new MxUiPreviewCameraSlot(null))
            {
                MxUiPreviewCameraSlotResult result = slot.EnsureTexture(128, 64);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(MxUiPreviewCameraSlot.MissingCameraCode, result.Code);
                Assert.IsFalse(result.TextureAssigned);
            }
        }

        [Test]
        public void CaptureState_MissingTexture_ReturnsDiagnostic()
        {
            GameObject go = new GameObject("preview-camera");
            try
            {
                UnityCamera camera = go.AddComponent<UnityCamera>();
                using (var slot = new MxUiPreviewCameraSlot(camera))
                {
                    MxUiPreviewCameraSlotResult result = slot.CaptureState();

                    Assert.IsFalse(result.Success);
                    Assert.AreEqual(MxUiPreviewCameraSlot.TextureMissingCode, result.Code);
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
