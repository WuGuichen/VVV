using MxFramework.Demo.MxAnimationShowcase;
using MxFramework.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Animation
{
    public sealed class MxAnimationShowcaseDemoTests
    {
        [Test]
        public void ShowcaseScene_BindsFormalResourceReferencesAndUi()
        {
            Assert.IsTrue(System.IO.File.Exists(MxAnimationShowcaseDemoBootstrap.ScenePath), MxAnimationShowcaseDemoBootstrap.ScenePath);
            EditorSceneManager.OpenScene(MxAnimationShowcaseDemoBootstrap.ScenePath, OpenSceneMode.Single);

            GameObject root = GameObject.Find("MxAnimationSystemShowcaseRoot");
            Assert.IsNotNull(root);
            Assert.IsNotNull(root.GetComponent<DefaultInputService>());
            Assert.IsNotNull(root.GetComponent<UIDocument>());
            Assert.IsNotNull(root.GetComponent<MxAnimationShowcaseDemoBootstrap>());

            var serialized = new SerializedObject(root.GetComponent<MxAnimationShowcaseDemoBootstrap>());
            AssertSerializedReference(serialized, "_visualTree");
            AssertSerializedReference(serialized, "_styleSheet");
            AssertSerializedReference(serialized, "_locomotionAnchor");
            AssertSerializedReference(serialized, "_directionalAnchor");
            AssertSerializedReference(serialized, "_layerAnchor");
            AssertSerializedReference(serialized, "_overrideAnchor");
            AssertSerializedReference(serialized, "_skeletonModel");
            AssertSerializedReference(serialized, "_idleClip");
            AssertSerializedReference(serialized, "_walkForwardClip");
            AssertSerializedReference(serialized, "_walkBackClip");
            AssertSerializedReference(serialized, "_walkLeftClip");
            AssertSerializedReference(serialized, "_walkRightClip");
            AssertSerializedReference(serialized, "_runForwardClip");
            AssertSerializedReference(serialized, "_runBackClip");
            AssertSerializedReference(serialized, "_runLeftClip");
            AssertSerializedReference(serialized, "_runRightClip");
            AssertSerializedReference(serialized, "_sprintForwardClip");
            AssertSerializedReference(serialized, "_jumpClip");
            AssertSerializedReference(serialized, "_jumpRunningClip");
            AssertSerializedReference(serialized, "_jumpRunningLandingClip");
            AssertSerializedReference(serialized, "_landToIdleClip");
            AssertSerializedReference(serialized, "_turnLeft90Clip");
            AssertSerializedReference(serialized, "_turnRight90Clip");
            AssertSerializedReference(serialized, "_upperBodyMask");
            AssertSerializedReference(serialized, "_bakeReport");

            UIDocument document = root.GetComponent<UIDocument>();
            Assert.IsNotNull(document.visualTreeAsset);
            Assert.IsNotNull(document.panelSettings);
            Assert.IsNotNull(GameObject.Find("Station_1D_Locomotion"));
            Assert.IsNotNull(GameObject.Find("Station_2D_Directional"));
            Assert.IsNotNull(GameObject.Find("Station_Layer_Mask_Bridge"));
            Assert.IsNotNull(GameObject.Find("Station_Mod_Override"));
        }

        [Test]
        public void ShowcaseScene_InitializesActorsBackendWarmupAndHud()
        {
            EditorSceneManager.OpenScene(MxAnimationShowcaseDemoBootstrap.ScenePath, OpenSceneMode.Single);

            GameObject root = GameObject.Find("MxAnimationSystemShowcaseRoot");
            Assert.IsNotNull(root);
            MxAnimationShowcaseDemoBootstrap bootstrap = root.GetComponent<MxAnimationShowcaseDemoBootstrap>();
            try
            {
                bootstrap.InitializeForValidation();

                Assert.IsTrue(bootstrap.IsInitialized);
                Assert.IsFalse(bootstrap.HasInitializationError);
                Assert.AreEqual(4, bootstrap.ActorCount);
                Assert.IsNotNull(bootstrap.ResourceManager);
                Assert.IsNotNull(bootstrap.WarmupResult);
                Assert.IsTrue(bootstrap.WarmupResult.Success);
                Assert.IsNotNull(bootstrap.ModMergeResult);
                Assert.IsTrue(bootstrap.ModMergeResult.Success);
                Assert.GreaterOrEqual(bootstrap.ResourceManager.CreateDebugSnapshot().LoadedCount, 8);

                UIDocument document = root.GetComponent<UIDocument>();
                Assert.IsNotNull(document);
                VisualElement hud = document.rootVisualElement.Q<VisualElement>("mxanimation-showcase-hud");
                Assert.IsNotNull(hud);
                AssertReadableLabel(document.rootVisualElement.Q<Label>("title"), "MxAnimation System Showcase");
                AssertReadableLabel(document.rootVisualElement.Q<Label>("locomotion"), "1D Locomotion:");
                AssertReadableLabel(document.rootVisualElement.Q<Label>("directional"), "2D Directional:");
                AssertReadableLabel(document.rootVisualElement.Q<Label>("layer"), "Layer + Mask:");
                AssertReadableLabel(document.rootVisualElement.Q<Label>("override"), "Mod/Fallback:");
                AssertReadableLabel(document.rootVisualElement.Q<Label>("package"), "Package:");
                AssertReadableLabel(document.rootVisualElement.Q<Label>("compatibility"), "Compatibility:");
                AssertReadableLabel(document.rootVisualElement.Q<Label>("bake"), "Bake:");
                AssertReadableLabel(document.rootVisualElement.Q<Label>("cache"), "Cache:");
            }
            finally
            {
                bootstrap.DisposeForValidation();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void AssertSerializedReference(SerializedObject serialized, string propertyName)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            Assert.IsNotNull(property, propertyName);
            Assert.IsNotNull(property.objectReferenceValue, propertyName);
        }

        private static void AssertReadableLabel(Label label, string expectedText)
        {
            Assert.IsNotNull(label);
            StringAssert.Contains(expectedText, label.text);
        }
    }
}
