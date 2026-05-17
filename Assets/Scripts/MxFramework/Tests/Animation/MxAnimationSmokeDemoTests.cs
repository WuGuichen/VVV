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
            MxAnimationSetDefinition set = CreateAnimationSet();
            MxAnimationClipRegistry registry = MxAnimationClipRegistryBuilder.FromCatalog(catalog, version: 1, catalogHash: "test-catalog");
            var warmupService = new MxAnimationWarmupService(new ResourcePreloadService(manager));
            MxAnimationWarmupResult warmup = warmupService.Warmup(new MxAnimationWarmupRequest(set, registry, catalog));
            Assert.IsTrue(warmup.Success);

            GameObject instance = Object.Instantiate(model.Value.Value);
            UnityPlayablesAnimationBackend backend = null;
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>() ?? instance.AddComponent<Animator>();
                backend = new UnityPlayablesAnimationBackend(animator, manager, set, "test.skeleton");

                Assert.GreaterOrEqual(manager.CreateDebugSnapshot().LoadedCount, 5);

                MxAnimationBackendResult blend = backend.SetBlend1D(new MxAnimationBlend1DRequest
                {
                    BlendId = MxAnimationSmokeDemoBootstrap.LocomotionBlendId,
                    Parameter = new MxAnimationQuantizedParameter(MxAnimationSmokeDemoBootstrap.SpeedParameterId, 750)
                });
                Assert.IsTrue(blend.Success, blend.Message);

                MxAnimationBackendResult attack = backend.CrossFade(new MxAnimationCrossFadeRequest
                {
                    BindingId = "upper_attack",
                    FadeDurationSeconds = 0.05f
                });
                Assert.IsTrue(attack.Success, attack.Message);
                backend.SetLayerWeight(new MxAnimationLayerWeightRequest
                {
                    LayerId = new MxAnimationLayerId("upper_body"),
                    Weight = 1f
                });
                backend.Tick(0.1f);

                MxAnimationLayerDiagnostic layer = FindBaseLayer(backend.CreateSnapshot());
                Assert.AreEqual(MxAnimationLayerStatus.Playing, layer.Status);
                Assert.AreEqual(MxAnimationSmokeDemoBootstrap.LocomotionBlendId, layer.Blend1DId);
                Assert.AreEqual(3, layer.Blend1DWeights.Count);
                Assert.AreEqual(0.5f, layer.Blend1DWeights[1].Weight, 0.0001f);
                Assert.AreEqual(0.5f, layer.Blend1DWeights[2].Weight, 0.0001f);
                MxAnimationLayerDiagnostic upper = FindLayer(backend.CreateSnapshot(), new MxAnimationLayerId("upper_body"));
                Assert.AreEqual(1f, upper.LayerWeight);
                Assert.AreEqual(0, manager.CreateDebugSnapshot().FailedCount);
            }
            finally
            {
                backend?.Release();
                if (instance != null)
                    Object.DestroyImmediate(instance);
                warmupService.Release(warmup);
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
            AssertSerializedArrayReferences(serialized, "_warmupAnimationClips", 16);
            AssertSerializedReference(serialized, "_upperBodyMask");

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
            Assert.IsNotNull(bootstrap.WarmupResult);
            Assert.IsTrue(bootstrap.WarmupResult.Success);
            Assert.GreaterOrEqual(bootstrap.ResourceManager.CreateDebugSnapshot().LoadedCount, 2);

            UIDocument document = root.GetComponent<UIDocument>();
            Assert.IsNotNull(document);
            VisualElement hud = document.rootVisualElement.Q<VisualElement>("mxanimation-smoke-hud");
            Assert.IsNotNull(hud);
            Assert.AreEqual(DisplayStyle.Flex, hud.resolvedStyle.display);
            AssertReadableLabel(document.rootVisualElement.Q<Label>("title"), "MxAnimation 1D Locomotion Blend");
            AssertReadableLabel(document.rootVisualElement.Q<Label>("action"), "Action:");
            AssertReadableLabel(document.rootVisualElement.Q<Label>("speed"), "Speed:");
            AssertReadableLabel(document.rootVisualElement.Q<Label>("clip"), "Blend:");
            AssertReadableLabel(document.rootVisualElement.Q<Label>("layers"), "Layers:");
            AssertReadableLabel(document.rootVisualElement.Q<Label>("warmup"), "Warmup:");

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
                    new MxAnimationActionBinding(
                        "upper_attack",
                        "action:upper_attack",
                        Key(TempImportedResourceCatalog.SkeletonJumpAnimationId, ResourceTypeIds.AnimationClip),
                        new MxAnimationLayerId("upper_body"))
                },
                layers: new[]
                {
                    new MxAnimationLayerDefinition(MxAnimationLayerId.Base, defaultWeight: 1f),
                    new MxAnimationLayerDefinition(
                        new MxAnimationLayerId("upper_body"),
                        "humanoid.upper",
                        0f,
                        MxAnimationLayerBlendMode.Override,
                        Key(TempImportedResourceCatalog.SkeletonUpperBodyMaskId, ResourceTypeIds.AvatarMask))
                },
                warmup: new MxAnimationWarmupDefinition("test.mxanimation.smoke"),
                blend1DDefinitions: new[]
                {
                    new MxAnimationBlend1DDefinition(
                        MxAnimationSmokeDemoBootstrap.LocomotionBlendId,
                        MxAnimationSmokeDemoBootstrap.SpeedParameterId,
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend1DPoint(0, Key(TempImportedResourceCatalog.SkeletonIdleAnimationId, ResourceTypeIds.AnimationClip)),
                            new MxAnimationBlend1DPoint(500, Key(TempImportedResourceCatalog.SkeletonWalkForwardAnimationId, ResourceTypeIds.AnimationClip)),
                            new MxAnimationBlend1DPoint(1000, Key(TempImportedResourceCatalog.SkeletonRunForwardAnimationId, ResourceTypeIds.AnimationClip))
                        })
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
            return FindLayer(snapshot, MxAnimationLayerId.Base);
        }

        private static MxAnimationLayerDiagnostic FindLayer(MxAnimationDiagnosticSnapshot snapshot, MxAnimationLayerId layerId)
        {
            for (int i = 0; i < snapshot.LayerStates.Count; i++)
            {
                if (snapshot.LayerStates[i].LayerId == layerId)
                    return snapshot.LayerStates[i];
            }

            Assert.Fail("Expected layer diagnostic: " + layerId + ".");
            return null;
        }

        private static void AssertSerializedReference(SerializedObject serialized, string propertyName)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            Assert.IsNotNull(property, propertyName);
            Assert.IsNotNull(property.objectReferenceValue, propertyName);
        }

        private static void AssertSerializedArrayReferences(SerializedObject serialized, string propertyName, int minimumSize)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            Assert.IsNotNull(property, propertyName);
            Assert.GreaterOrEqual(property.arraySize, minimumSize, propertyName);
            for (int i = 0; i < property.arraySize; i++)
                Assert.IsNotNull(property.GetArrayElementAtIndex(i).objectReferenceValue, propertyName + "[" + i + "]");
        }

        private static void AssertReadableLabel(Label label, string expectedText)
        {
            Assert.IsNotNull(label);
            StringAssert.Contains(expectedText, label.text);
        }
    }
}
