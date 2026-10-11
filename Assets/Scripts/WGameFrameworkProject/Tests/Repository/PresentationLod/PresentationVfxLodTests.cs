using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MxFramework.Tests.PresentationLod
{
    public sealed class PresentationVfxLodTests
    {
        [Test]
        public void HiddenDecorationSharesOneDecision_SecondCameraAndImportancePromoteImmediately()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            var lod = new CharacterTestPresentationLod();
            try
            {
                var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(root, scene); root.transform.position = new Vector3(10000, 0, 10);
                root.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var view = new CharacterTestRuntimeCharacterView(new CharacterTestCharacterEntityId(713), root, null, null, null);
                var handle = lod.Register(view);
                var cameraRoot = new GameObject("owned offscreen camera"); SceneManager.MoveGameObjectToScene(cameraRoot, scene);
                var camera = cameraRoot.AddComponent<UnityEngine.Camera>(); camera.orthographic = true;
                camera.orthographicSize = 5; camera.farClipPlane = 100;
                Assert.True(lod.RegisterGameplayCamera(camera, PresentationLodCameraPurpose.GameplayBase));
                Assert.True(lod.ShouldUpdateVfx(713, 0, 1));
                lod.ShouldUpdateVfx(713, 0.25, 2); lod.ShouldUpdateVfx(713, 0.5, 3); lod.ShouldUpdateVfx(713, 0.75, 4);
                Assert.False(lod.ShouldUpdateVfx(713, 0.751, 5));
                long holds = lod.VfxHolds;
                Assert.False(lod.ShouldUpdateVfx(713, 0.751, 5)); Assert.AreEqual(holds, lod.VfxHolds);
                var secondRoot = new GameObject("owned second receiver"); SceneManager.MoveGameObjectToScene(secondRoot, scene);
                secondRoot.transform.position = new Vector3(10000, 0, 0);
                var second = secondRoot.AddComponent<UnityEngine.Camera>(); second.orthographic = true;
                second.orthographicSize = 5; second.farClipPlane = 100;
                Assert.True(lod.RegisterGameplayCamera(second, PresentationLodCameraPurpose.GameplayBase));
                Assert.True(lod.ShouldUpdateVfx(713, 0.752, 6));
                lod.UnregisterGameplayCamera(second); Object.DestroyImmediate(secondRoot);
                lod.SetOwnerImportance(handle, 713, true);
                Assert.True(lod.ShouldUpdateVfx(713, 0.753, 7));
                lod.Unregister(handle, 713); Assert.True(lod.ShouldUpdateVfx(713, 1, 8));
                Assert.AreEqual(0, lod.Counters.Subjects);
            }
            finally { lod.Clear(); EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void UnknownOwnerAndMissingBudgetKeepPresentationActive()
        {
            var lod = new CharacterTestPresentationLod();
            Assert.True(lod.ShouldUpdateVfx(123, 0, 1));
            Assert.True(new CharacterTestVfxPresentationBudgetSource(null).ShouldUpdatePresentation(null));
            Assert.True(new CharacterTestVfxPresentationBudgetSource(lod).ShouldUpdatePresentation(null));
            Assert.AreEqual(0, lod.VfxHolds);
        }
    }
}
