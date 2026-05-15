using System.Collections;
using MxFramework.Animation;
using MxFramework.Animation.Unity;
using MxFramework.Demo;
using MxFramework.Demo.MxAnimationSmoke;
using MxFramework.Input;
using MxFramework.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Animation
{
    public sealed class MxAnimationSmokeDemoTests
    {
        [Test]
        public void FormalSamplesCatalog_LoadsSkeletonModelAndClipsThroughResourceManager()
        {
            ResourceCatalog catalog = TempImportedResourceCatalog.CreateCatalog();
            MemoryResourceProvider provider = TempImportedResourceCatalog.CreateMemoryProvider(
                catalog,
                AssetDatabase.LoadAssetAtPath<Object>);
            var manager = new ResourceManager();
            manager.RegisterProvider(provider);
            manager.AddCatalog(catalog);
            manager.ValidateCatalogs();

            ResourceLoadResult<ResourceHandle<GameObject>> model = manager.Load<GameObject>(
                Key(TempImportedResourceCatalog.SkeletonModelId, ResourceTypeIds.GameObject));
            Assert.IsTrue(model.Success, model.Error.Message);

            GameObject instance = Object.Instantiate(model.Value.Value);
            UnityPlayablesAnimationBackend backend = null;
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>() ?? instance.AddComponent<Animator>();
                backend = new UnityPlayablesAnimationBackend(animator, manager, CreateAnimationSet(), "test.skeleton");

                Assert.AreEqual(2, manager.CreateDebugSnapshot().LoadedCount);

                MxAnimationBackendResult walk = backend.CrossFade(new MxAnimationCrossFadeRequest
                {
                    BindingId = "walk",
                    FadeDurationSeconds = 0.05f
                });
                Assert.IsTrue(walk.Success, walk.Message);

                MxAnimationBackendResult run = backend.CrossFade(new MxAnimationCrossFadeRequest
                {
                    BindingId = "run",
                    FadeDurationSeconds = 0.05f
                });
                Assert.IsTrue(run.Success, run.Message);
                backend.Tick(0.1f);

                MxAnimationLayerDiagnostic layer = FindBaseLayer(backend.CreateSnapshot());
                Assert.AreEqual(MxAnimationLayerStatus.Playing, layer.Status);
                Assert.AreEqual(Key(TempImportedResourceCatalog.SkeletonRunForwardAnimationId, ResourceTypeIds.AnimationClip), layer.CurrentClipKey);
                Assert.AreEqual(0, manager.CreateDebugSnapshot().FailedCount);
            }
            finally
            {
                backend?.Release();
                if (instance != null)
                    Object.DestroyImmediate(instance);
                if (model.Success)
                    manager.Release(model.Value);
            }

            ResourceDebugSnapshot released = manager.CreateDebugSnapshot();
            Assert.AreEqual(0, released.LoadedCount);
            Assert.AreEqual(0, released.TotalRefCount);
        }

        [Test]
        public void SmokeScene_BindsFormalResourceReferencesAndUi()
        {
            Assert.IsTrue(System.IO.File.Exists(MxAnimationSmokeDemoBootstrap.ScenePath), MxAnimationSmokeDemoBootstrap.ScenePath);
            EditorSceneManager.OpenScene(MxAnimationSmokeDemoBootstrap.ScenePath, OpenSceneMode.Single);

            GameObject root = GameObject.Find("MxAnimationSmokeRoot");
            Assert.IsNotNull(root);
            Assert.IsNotNull(root.GetComponent<DefaultInputService>());
            Assert.IsNotNull(root.GetComponent<UIDocument>());
            Assert.IsNotNull(root.GetComponent<MxAnimationSmokeDemoBootstrap>());

            var serialized = new SerializedObject(root.GetComponent<MxAnimationSmokeDemoBootstrap>());
            AssertSerializedReference(serialized, "_visualTree");
            AssertSerializedReference(serialized, "_styleSheet");
            AssertSerializedReference(serialized, "_skeletonModel");
            AssertSerializedReference(serialized, "_idleClip");
            AssertSerializedReference(serialized, "_walkForwardClip");
            AssertSerializedReference(serialized, "_runForwardClip");
            AssertSerializedReference(serialized, "_jumpClip");

            var document = root.GetComponent<UIDocument>();
            Assert.IsNotNull(document.visualTreeAsset);
            Assert.IsNotNull(document.panelSettings);
        }

        [UnityTest]
        public IEnumerator SmokeScene_PlayModeInitializesModelBackendAndHud()
        {
            EditorSceneManager.OpenScene(MxAnimationSmokeDemoBootstrap.ScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            GameObject root = GameObject.Find("MxAnimationSmokeRoot");
            Assert.IsNotNull(root);
            MxAnimationSmokeDemoBootstrap bootstrap = root.GetComponent<MxAnimationSmokeDemoBootstrap>();
            Assert.IsTrue(bootstrap.IsInitialized);
            Assert.IsFalse(bootstrap.HasInitializationError);
            Assert.IsNotNull(bootstrap.ModelInstance);
            Assert.IsNotNull(bootstrap.Animator);
            Assert.IsNotNull(bootstrap.Backend);
            Assert.IsNotNull(bootstrap.ResourceManager);
            Assert.GreaterOrEqual(bootstrap.ResourceManager.CreateDebugSnapshot().LoadedCount, 2);

            UIDocument document = root.GetComponent<UIDocument>();
            Assert.IsNotNull(document);
            VisualElement hud = document.rootVisualElement.Q<VisualElement>("mxanimation-smoke-hud");
            Assert.IsNotNull(hud);
            Assert.AreEqual(DisplayStyle.Flex, hud.resolvedStyle.display);
            AssertReadableLabel(document.rootVisualElement.Q<Label>("title"), "MxAnimation Play Mode Smoke");
            AssertReadableLabel(document.rootVisualElement.Q<Label>("action"), "Action:");
            AssertReadableLabel(document.rootVisualElement.Q<Label>("clip"), "Clip:");

            yield return new ExitPlayMode();
        }

        private static MxAnimationSetDefinition CreateAnimationSet()
        {
            return new MxAnimationSetDefinition(
                "mxanimation.smoke.tests",
                1,
                Key(TempImportedResourceCatalog.SkeletonIdleAnimationId, ResourceTypeIds.AnimationClip),
                Key(TempImportedResourceCatalog.SkeletonIdleAnimationId, ResourceTypeIds.AnimationClip),
                new[]
                {
                    Binding("idle", TempImportedResourceCatalog.SkeletonIdleAnimationId, loop: true),
                    Binding("walk", TempImportedResourceCatalog.SkeletonWalkForwardAnimationId, loop: true),
                    Binding("run", TempImportedResourceCatalog.SkeletonRunForwardAnimationId, loop: true),
                    Binding("jump", TempImportedResourceCatalog.SkeletonJumpAnimationId, loop: false)
                });
        }

        private static MxAnimationActionBinding Binding(string bindingId, string clipId, bool loop)
        {
            return new MxAnimationActionBinding(
                bindingId,
                "action:" + bindingId,
                Key(clipId, ResourceTypeIds.AnimationClip),
                MxAnimationLayerId.Base,
                playbackSpeed: 1f,
                loop: loop,
                alignmentPolicy: MxAnimationAlignmentPolicy.StartAtZero);
        }

        private static ResourceKey Key(string id, string typeId)
        {
            return new ResourceKey(id, typeId, string.Empty, TempImportedResourceCatalog.PackageId);
        }

        private static MxAnimationLayerDiagnostic FindBaseLayer(MxAnimationDiagnosticSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.LayerStates.Count; i++)
            {
                if (snapshot.LayerStates[i].LayerId == MxAnimationLayerId.Base)
                    return snapshot.LayerStates[i];
            }

            Assert.Fail("Expected base layer diagnostic.");
            return null;
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
