using System;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Tests.Editor.CharacterPreview
{
    public sealed class AnimationDesignerSceneCompatibilityTests
    {
        [Test]
        public void IsolatedHostUsesPluginSupportedSceneAndClosesOnlyItsOwnScene()
        {
            var stageType = Type.GetType("MxFramework.Project.Editor.AnimationDesigner.AnimationDesignerStage, MxFramework.Project.Editor", true);
            var adapterType = Type.GetType("MxFramework.Project.Editor.AnimationDesigner.AnimationDesignerAdapter, MxFramework.Project.Editor", true);
            MethodInfo supportsScene = adapterType.GetMethod("SupportsScene", BindingFlags.NonPublic | BindingFlags.Static);
            Stage previous = StageUtility.GetCurrentStage();
            Scene main = SceneManager.GetActiveScene();
            string harnessScenePath = null;
            // Unity Test Runner opens a default untitled scratch scene. Give only that harness a local path.
            if (string.IsNullOrEmpty(main.path))
            {
                foreach (var root in main.GetRootGameObjects())
                    Assert.That(root.name, Is.EqualTo("Main Camera").Or.EqualTo("Directional Light"),
                        "Do not save a populated user's untitled scene from a repository test.");
                Directory.CreateDirectory("Library/AnimationDesignerTests");
                harnessScenePath = "Library/AnimationDesignerTests/Main-" + Guid.NewGuid().ToString("N") + ".unity";
                Assert.That(EditorSceneManager.SaveScene(main, harnessScenePath), Is.True);
                EditorSceneManager.MarkSceneDirty(main);
            }
            bool dirty = main.isDirty;
            int roots = main.rootCount;
            int scenes = SceneManager.sceneCount;
            var stage = (PreviewSceneStage)stageType.GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            Scene preview = default;
            Scene editing = default;
            try
            {
                StageUtility.GoToStage(stage, false);
                editing = stage.scene;
                Assert.That(StageUtility.GetCurrentStage(), Is.SameAs(stage));
                Assert.That(EditorSceneManager.IsPreviewScene(editing), Is.False, "The plugin's OnGUI returns before drawing controls in a PreviewScene.");
                Assert.That(EditorSceneManager.GetSceneCullingMask(editing) & EditorSceneManager.GetSceneCullingMask(main), Is.Zero,
                    "A separate rendering mask must exclude the original scene and its Game View camera.");
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(main));
                var model = new GameObject("Animation Designer compatibility test");
                SceneManager.MoveGameObjectToScene(model, editing);
                var animator = model.AddComponent<Animator>();
                Assert.That(supportsScene.Invoke(null, new object[] { animator }), Is.EqualTo(true));

                preview = EditorSceneManager.NewPreviewScene();
                SceneManager.MoveGameObjectToScene(model, preview);
                Assert.That(supportsScene.Invoke(null, new object[] { animator }), Is.EqualTo(false));
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                StageUtility.GoToStage(previous, false);
                if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
                if (harnessScenePath != null && File.Exists(harnessScenePath)) File.Delete(harnessScenePath);
            }
            Assert.That(editing.IsValid(), Is.False);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(scenes));
            Assert.That(main.rootCount, Is.EqualTo(roots));
            Assert.That(main.isDirty, Is.EqualTo(dirty));
        }
    }
}
