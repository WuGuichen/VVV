using MxFramework.Camera;
using MxFramework.Camera.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Tests.Camera
{
    public sealed class CameraUnityTests
    {
        [Test]
        public void UnityRig_ApplyLate_AppliesTransformAndPerspective()
        {
            GameObject go = new GameObject("camera-test");
            try
            {
                UnityCamera camera = go.AddComponent<UnityCamera>();
                MxCameraUnityRig rig = go.AddComponent<MxCameraUnityRig>();
                var state = new MxCameraState(
                    new MxCameraRigId("rig"),
                    new MxCameraProfileId("profile"),
                    new MxCameraVector3(1f, 2f, 3f),
                    MxCameraVector3.Forward,
                    MxCameraVector3.Up,
                    new MxCameraEulerRotation(10f, 20f, 0f),
                    MxCameraProjectionKind.Perspective,
                    55f,
                    4f,
                    MxCameraVector3.Zero,
                    MxCameraVector3.Zero,
                    0f,
                    0f,
                    MxCameraStateSource.Normal);

                MxCameraResult result = rig.ApplyLate(state);

                Assert.IsTrue(result.Success);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), camera.transform.position);
                Assert.IsFalse(camera.orthographic);
                Assert.AreEqual(55f, camera.fieldOfView, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UnityRig_ApplyLate_AppliesOrthographicSizeAtMostOncePerFrame()
        {
            GameObject go = new GameObject("camera-test");
            try
            {
                UnityCamera camera = go.AddComponent<UnityCamera>();
                MxCameraUnityRig rig = go.AddComponent<MxCameraUnityRig>();
                var first = new MxCameraState(
                    new MxCameraRigId("rig"),
                    new MxCameraProfileId("profile"),
                    new MxCameraVector3(1f, 2f, 3f),
                    MxCameraVector3.Forward,
                    MxCameraVector3.Up,
                    new MxCameraEulerRotation(0f, 0f, 0f),
                    MxCameraProjectionKind.Orthographic,
                    60f,
                    7f,
                    MxCameraVector3.Zero,
                    MxCameraVector3.Zero,
                    0f,
                    0f,
                    MxCameraStateSource.Normal);
                var second = new MxCameraState(
                    new MxCameraRigId("rig"),
                    new MxCameraProfileId("profile"),
                    new MxCameraVector3(9f, 9f, 9f),
                    MxCameraVector3.Forward,
                    MxCameraVector3.Up,
                    new MxCameraEulerRotation(0f, 0f, 0f),
                    MxCameraProjectionKind.Orthographic,
                    60f,
                    9f,
                    MxCameraVector3.Zero,
                    MxCameraVector3.Zero,
                    0f,
                    0f,
                    MxCameraStateSource.Normal);

                rig.ApplyLate(first);
                rig.ApplyLate(second);

                Assert.IsTrue(camera.orthographic);
                Assert.AreEqual(7f, camera.orthographicSize, 0.001f);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), camera.transform.position);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
