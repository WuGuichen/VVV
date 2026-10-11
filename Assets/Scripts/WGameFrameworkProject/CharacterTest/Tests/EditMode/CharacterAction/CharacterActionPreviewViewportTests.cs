using MxFramework.Editor.CharacterAction.Timeline.Preview;
using MxFramework.Editor.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionPreviewViewportTests
    {
        private const string CharacterPrefabPath =
            "Assets/MxFrameworkGenerated/CharacterPackages/codex_flow_test/prefabs/codex_flow_test_character_preview.prefab";
        private const string CharacterStableId = "char.codex_flow_test";
        private const string EquipmentStateStableId = "equip_state.codex_flow_test.sword_empty";

        [Test]
        public void RenderScene_InstantiatesIntoPreviewSceneWithoutPollutingActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject instance = null;
            try
            {
                int activeRootCount = activeScene.rootCount;
                using (var renderScene = new CharacterActionPreviewRenderScene())
                {
                    instance = renderScene.Instantiate(source, Vector3.zero, Quaternion.identity);

                    Assert.NotNull(instance);
                    Assert.AreEqual(activeRootCount, activeScene.rootCount);
                    Assert.AreNotEqual(activeScene, instance.scene);
                    Assert.IsTrue(EditorSceneManager.IsPreviewScene(instance.scene));
                    Assert.AreEqual(renderScene.PreviewScene, instance.scene);
                    Assert.AreEqual(1, renderScene.RootCount);
                }

                Assert.IsTrue(instance == null);
            }
            finally
            {
                if (source != null)
                    Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void RenderScene_CameraNavigationAndAutoFrameRemainFiniteAndClamped()
        {
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                using (var renderScene = new CharacterActionPreviewRenderScene())
                {
                    GameObject instance = renderScene.Instantiate(source, new Vector3(2f, 1f, -3f), Quaternion.identity);
                    instance.transform.localScale = new Vector3(2f, 4f, 2f);

                    Assert.IsTrue(renderScene.AutoFrame(instance));
                    Vector3 framedPivot = renderScene.Pivot;
                    float framedDistance = renderScene.Distance;
                    Assert.Greater(framedDistance, 0.35f);

                    renderScene.Orbit(new Vector2(12f, -8f));
                    Assert.That(renderScene.OrbitAngles.y, Is.InRange(-85f, 85f));

                    renderScene.Pan(new Vector2(15f, -6f));
                    Assert.AreNotEqual(framedPivot, renderScene.Pivot);

                    renderScene.Zoom(2f);
                    Assert.Greater(renderScene.Distance, framedDistance);
                    Assert.IsFalse(float.IsNaN(renderScene.Distance));
                    Assert.IsFalse(float.IsInfinity(renderScene.Distance));

                    renderScene.Zoom(-10000f);
                    Assert.GreaterOrEqual(renderScene.Distance, 0.35f);
                }
            }
            finally
            {
                if (source != null)
                    Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void RenderScene_DisposeRestoresPreviewSceneCountAndIsIdempotent()
        {
            int baseline = EditorSceneManager.previewSceneCount;
            var renderScene = new CharacterActionPreviewRenderScene();
            Assert.Greater(EditorSceneManager.previewSceneCount, baseline);

            Assert.DoesNotThrow(renderScene.Dispose);
            Assert.DoesNotThrow(renderScene.Dispose);
            Assert.AreEqual(baseline, EditorSceneManager.previewSceneCount);
            Assert.IsTrue(renderScene.IsDisposed);
        }

        [Test]
        public void EmbeddedRuntimeSession_DoesNotRegisterSceneViewHandles()
        {
            using (var renderScene = new CharacterActionPreviewRenderScene())
            using (var session = new CharacterActionPreviewRuntimeSession(renderScene))
            {
                Assert.AreEqual(CharacterActionPreviewSurfaceMode.Embedded, session.SurfaceMode);
                Assert.IsFalse(session.UsesSceneViewHandles);
            }
        }

        [Test]
        public void LegacyRuntimeSession_PreservesSceneViewHandleMode()
        {
            using (var session = new CharacterActionPreviewRuntimeSession())
            {
                Assert.AreEqual(CharacterActionPreviewSurfaceMode.SceneView, session.SurfaceMode);
                Assert.IsTrue(session.UsesSceneViewHandles);
            }
        }

        [Test]
        public void EmbeddedRuntimeSession_HostsCharacterAndWeaponsOutsideActiveScene()
        {
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath));
            CharacterActionAuthoringModel model = CreatePreviewModel();
            Scene activeScene = SceneManager.GetActiveScene();
            int activeRootCount = activeScene.rootCount;
            bool initialAnimationMode = AnimationMode.InAnimationMode();

            using (var renderScene = new CharacterActionPreviewRenderScene())
            using (var session = new CharacterActionPreviewRuntimeSession(renderScene))
            {
                session.PreviewFrame(model, 0, revisionToken: 1L);

                Assert.NotNull(session.PreviewInstance, session.DiagnosticsText);
                Assert.AreEqual(activeRootCount, activeScene.rootCount);
                Assert.AreEqual(renderScene.PreviewScene, session.PreviewInstance.scene);
                Assert.IsTrue(EditorSceneManager.IsPreviewScene(session.PreviewInstance.scene));

                CharacterDefaultEquipmentRuntimeBinder binder =
                    session.PreviewInstance.GetComponent<CharacterDefaultEquipmentRuntimeBinder>();
                Assert.NotNull(binder);
                for (int i = 0; i < binder.SpawnedWeapons.Count; i++)
                    Assert.AreEqual(renderScene.PreviewScene, binder.SpawnedWeapons[i].scene);

                Assert.AreEqual(initialAnimationMode, AnimationMode.InAnimationMode(),
                    "Embedded fallback sampling must not enter Unity's global Animation Mode or tint the preview.");
                Assert.AreEqual(1, session.ContextValidationCount);
                session.PreviewFrame(model, 1, revisionToken: 1L);
                Assert.AreEqual(1, session.ContextValidationCount,
                    "An unchanged document revision should reuse the validated action context.");
                session.PreviewFrame(model, 1, revisionToken: 2L);
                Assert.AreEqual(2, session.ContextValidationCount);
            }
        }

        [Test]
        public void Viewport_SurfaceSwitchAndDisposeAreIdempotent()
        {
            var viewport = new CharacterActionPreviewViewport();
            Assert.AreEqual(CharacterActionPreviewSurfaceMode.Embedded, viewport.SurfaceMode);
            Assert.NotNull(viewport.Session);
            Assert.IsFalse(viewport.Session.UsesSceneViewHandles);

            viewport.SetSurfaceMode(CharacterActionPreviewSurfaceMode.SceneView);
            Assert.AreEqual(CharacterActionPreviewSurfaceMode.SceneView, viewport.SurfaceMode);
            Assert.IsNull(viewport.Session);

            viewport.SetSurfaceMode(CharacterActionPreviewSurfaceMode.Embedded);
            Assert.NotNull(viewport.Session);
            Assert.IsFalse(viewport.Session.UsesSceneViewHandles);

            Assert.DoesNotThrow(viewport.Dispose);
            Assert.DoesNotThrow(viewport.Dispose);
            Assert.IsTrue(viewport.IsDisposed);
            Assert.IsTrue(viewport.RenderScene.IsDisposed);
        }

        private static CharacterActionAuthoringModel CreatePreviewModel()
        {
            CharacterActionAuthoringModel model = CharacterActionAuthoringModel.CreateDefault();
            model.Profile.StableId = "codex_flow_test.preview";
            model.Profile.DisplayName = "codex flow test Default";
            model.Profile.ActionSetIds = new[] { "codex_flow_test.sword_empty" };
            model.Profile.ActionLibraryIds = new[] { "Common" };
            model.ActionSet.StableId = "codex_flow_test.sword_empty";
            model.ActionSet.DisplayName = "codex flow test Default";
            model.ActionSet.CharacterStableId = CharacterStableId;
            model.ActionSet.EquipmentStateStableId = EquipmentStateStableId;
            model.SelectedActionIndex = 1;
            model.ReferenceContext.AddCharacterStableId(CharacterStableId);
            model.ReferenceContext.AddEquipmentStateStableId(EquipmentStateStableId);
            model.ReferenceContext.Normalize();
            return model;
        }
    }
}
