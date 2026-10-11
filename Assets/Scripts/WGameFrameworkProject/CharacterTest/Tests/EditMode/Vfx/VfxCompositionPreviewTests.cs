using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Editor;
using MxFramework.Editor.CharacterAction;
using MxFramework.Resources;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests
{
    public sealed partial class VfxManualCompositionTests
    {
        [Test]
        public void LifecycleController_AdvancesBuildRebuildLoopAndSelectedExit()
        {
            var root = new GameObject("Lifecycle test");
            try
            {
                GameObject build = new GameObject("Build"); build.transform.SetParent(root.transform);
                GameObject rebuild = new GameObject("Rebuild"); rebuild.transform.SetParent(root.transform);
                GameObject loop = new GameObject("Loop"); loop.transform.SetParent(root.transform);
                GameObject complete = new GameObject("Complete"); complete.transform.SetParent(root.transform);
                GameObject dissipate = new GameObject("Dissipate"); dissipate.transform.SetParent(root.transform);
                UnityVfxLifecycleController controller = root.AddComponent<UnityVfxLifecycleController>();
                controller.Configure(build, rebuild, loop, complete, dissipate, 1, 1, 2, 2, 3, UnityVfxLoopPlaybackMode.Continuous);
                controller.SetPlaybackRate(0.5f);
                controller.PlayBuild(new VfxInstanceId(1));
                Assert.That(controller.CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Build));
                controller.Advance(1f / 60f);
                Assert.That(controller.CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Build));
                controller.Advance(1f / 60f);
                Assert.That(controller.CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Loop));
                controller.PlayRebuild(new VfxInstanceId(1));
                Assert.That(controller.CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Rebuild));
                controller.SetPlaybackRate(2f);
                controller.Advance(0.5f / 60f);
                Assert.That(controller.CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Loop));
                controller.PlayExit(VfxStopMode.Dissipate);
                Assert.That(controller.CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Dissipate));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LifecycleController_SeekSourceFrames_UsesSourceTimeAtAnyPlaybackRate()
        {
            var root = new GameObject("Lifecycle seek test");
            try
            {
                GameObject build = new GameObject("Build"); build.transform.SetParent(root.transform);
                GameObject rebuild = new GameObject("Rebuild"); rebuild.transform.SetParent(root.transform);
                GameObject loop = new GameObject("Loop"); loop.transform.SetParent(root.transform);
                GameObject complete = new GameObject("Complete"); complete.transform.SetParent(root.transform);
                GameObject dissipate = new GameObject("Dissipate"); dissipate.transform.SetParent(root.transform);
                UnityVfxLifecycleController controller = root.AddComponent<UnityVfxLifecycleController>();
                controller.Configure(build, rebuild, loop, complete, dissipate, 2, 1, 8, 1, 1, UnityVfxLoopPlaybackMode.Continuous);
                controller.SetPlaybackRate(2f);
                controller.PlayBuild(new VfxInstanceId(1));

                controller.SeekSourceFrames(2);

                Assert.That(controller.CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Loop));
                Assert.That(controller.CurrentPhaseFrame, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LifecycleController_PlaybackRateClampsAtSixteenTimes()
        {
            var root = new GameObject("Lifecycle max playback rate test");
            try
            {
                GameObject build = new GameObject("Build"); build.transform.SetParent(root.transform);
                GameObject rebuild = new GameObject("Rebuild"); rebuild.transform.SetParent(root.transform);
                GameObject loop = new GameObject("Loop"); loop.transform.SetParent(root.transform);
                GameObject complete = new GameObject("Complete"); complete.transform.SetParent(root.transform);
                GameObject dissipate = new GameObject("Dissipate"); dissipate.transform.SetParent(root.transform);
                UnityVfxLifecycleController controller = root.AddComponent<UnityVfxLifecycleController>();
                controller.Configure(build, rebuild, loop, complete, dissipate, 1, 1, 2, 1, 1, UnityVfxLoopPlaybackMode.Continuous);
                controller.SetPlaybackRate(32f);
                controller.PlayBuild(new VfxInstanceId(1));

                controller.Advance(1f / (60f * 16f));

                Assert.That(controller.CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Loop));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BakedLayerScheduler_StartsAndStopsAtExactPhaseFrames()
        {
            var root = new GameObject("Baked schedule test");
            var layer = new GameObject("Layer");
            layer.transform.SetParent(root.transform, false);
            ParticleSystem particle = layer.AddComponent<ParticleSystem>();
            try
            {
                var entry = new UnityVfxBakedLayerScheduleEntry();
                entry.Configure(layer, UnityVfxLifecyclePhase.Loop, 2, 4);
                UnityVfxBakedLayerScheduler scheduler = root.AddComponent<UnityVfxBakedLayerScheduler>();
                scheduler.Configure(new[] { entry });
                scheduler.EnterPhase(UnityVfxLifecyclePhase.Loop);
                Assert.That(particle.isPlaying, Is.False);
                scheduler.AdvancePhase(1.999f);
                Assert.That(particle.isPlaying, Is.False);
                scheduler.AdvancePhase(2f);
                Assert.That(particle.isPlaying, Is.True);
                scheduler.AdvancePhase(4f);
                Assert.That(particle.isEmitting, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BakedLayerScheduler_AppliesPlaybackWindowEndPolicies()
        {
            var root = new GameObject("Baked window policy test");
            var immediateLayer = new GameObject("Immediate layer");
            immediateLayer.transform.SetParent(root.transform, false);
            ParticleSystem immediate = immediateLayer.AddComponent<ParticleSystem>();
            var dissipateLayer = new GameObject("Dissipate layer");
            dissipateLayer.transform.SetParent(root.transform, false);
            ParticleSystem dissipate = dissipateLayer.AddComponent<ParticleSystem>();
            try
            {
                var immediateEntry = new UnityVfxBakedLayerScheduleEntry();
                immediateEntry.Configure(immediateLayer, UnityVfxLifecyclePhase.Loop, 0, 10, UnityVfxLayerWindowEndPolicy.StopAndClear, 0);
                var dissipateEntry = new UnityVfxBakedLayerScheduleEntry();
                dissipateEntry.Configure(dissipateLayer, UnityVfxLifecyclePhase.Loop, 0, 10, UnityVfxLayerWindowEndPolicy.DissipateThenClear, 3);
                UnityVfxBakedLayerScheduler scheduler = root.AddComponent<UnityVfxBakedLayerScheduler>();
                scheduler.Configure(new[] { immediateEntry, dissipateEntry });

                scheduler.EnterPhase(UnityVfxLifecyclePhase.Loop);
                immediate.Emit(1);
                dissipate.Emit(1);
                scheduler.AdvancePhase(7f);
                Assert.That(immediate.isEmitting, Is.True);
                Assert.That(dissipate.isEmitting, Is.False);
                Assert.That(dissipate.particleCount, Is.GreaterThan(0));

                scheduler.AdvancePhase(10f);
                Assert.That(immediate.particleCount, Is.Zero);
                Assert.That(dissipate.particleCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BakedPropertyDriver_EvaluatesCachedMaterialPropertyBlockTrack()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material material = null;
            try
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                Renderer renderer = root.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.blue, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                var track = new UnityVfxBakedPropertyTrack();
                track.ConfigureColor(renderer, "_BaseColor", UnityVfxLifecyclePhase.Loop, 0, 10, gradient);
                UnityVfxBakedPropertyDriver driver = root.AddComponent<UnityVfxBakedPropertyDriver>();
                driver.Configure(new[] { track });

                driver.EnterPhase(UnityVfxLifecyclePhase.Loop);
                driver.AdvancePhase(5f);
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Color actual = block.GetColor(Shader.PropertyToID("_BaseColor"));
                Assert.That(actual.r, Is.EqualTo(0.5f).Within(0.02f));
                Assert.That(actual.b, Is.EqualTo(0.5f).Within(0.02f));
            }
            finally
            {
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BakedSchedulerAndPropertyDriver_AllocateZeroManagedBytesAfterInitialization()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material material = null;
            try
            {
                ParticleSystem particle = root.AddComponent<ParticleSystem>();
                var entry = new UnityVfxBakedLayerScheduleEntry();
                entry.Configure(root, UnityVfxLifecyclePhase.Loop, 0, 600);
                UnityVfxBakedLayerScheduler scheduler = root.AddComponent<UnityVfxBakedLayerScheduler>();
                scheduler.Configure(new[] { entry });
                scheduler.EnterPhase(UnityVfxLifecyclePhase.Loop);

                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                Renderer renderer = root.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                var track = new UnityVfxBakedPropertyTrack();
                track.ConfigureFloat(renderer, "_Surface", UnityVfxLifecyclePhase.Loop, 0, 600, AnimationCurve.Linear(0f, 0f, 1f, 1f));
                UnityVfxBakedPropertyDriver driver = root.AddComponent<UnityVfxBakedPropertyDriver>();
                driver.Configure(new[] { track });
                driver.EnterPhase(UnityVfxLifecyclePhase.Loop);

                for (int i = 0; i < 128; i++)
                {
                    scheduler.AdvancePhase(100f + (i & 1));
                    driver.AdvancePhase(100f + (i & 1));
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 4096; i++)
                {
                    float frame = 100f + (i & 1);
                    scheduler.AdvancePhase(frame);
                    driver.AdvancePhase(frame);
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.Zero, "Lifecycle steady-state authoring drivers must remain NoAlloc after initialization.");
                Assert.That(particle, Is.Not.Null);
            }
            finally
            {
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CompositionSession_SupportsUndoAndRedoWithoutMutatingSource()
        {
            VfxSourceElementCatalog catalog = LoadElementCatalogForTests();
            VfxCompositionDraftView draft = new VfxCompositionDraftService().Create(catalog, TestDraftId, "Undo test");
            var session = new VfxCompositionDraftSession(draft);
            Assert.That(session.CanUndo, Is.False);
            Assert.That(session.CanRedo, Is.False);
            session.RecordChange();
            draft.Layers.Add(new VfxCompositionLayerView { LayerId = "layer_01", ElementId = PaletteElements(catalog).First().ElementId });
            Assert.That(session.CanUndo, Is.True);
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.Draft.Layers, Is.Empty);
            Assert.That(session.CanRedo, Is.True);
            Assert.That(session.Redo(), Is.True);
            Assert.That(session.Draft.Layers, Has.Count.EqualTo(1));
        }

        [Test]
        public void CompositionSession_MarkSavedClearsDirtyAndUpdatesDiskBaseline()
        {
            VfxSourceElementCatalog catalog = LoadElementCatalogForTests();
            VfxCompositionDraftView draft = new VfxCompositionDraftService().Create(catalog, TestDraftId, "Save state test");
            var session = new VfxCompositionDraftSession(draft);

            session.RecordChange();
            draft.DisplayName = "Changed";
            Assert.That(session.Dirty, Is.True);

            session.MarkSaved("Assets/Config/MxFramework/Vfx/Compositions/save-state-test.json", "sha256:saved");

            Assert.That(session.Dirty, Is.False);
            Assert.That(session.LoadedPath, Is.EqualTo("Assets/Config/MxFramework/Vfx/Compositions/save-state-test.json"));
            Assert.That(session.BaseFileHash, Is.EqualTo("sha256:saved"));
        }

        [Test]
        public void PreviewSession_ExposesDeterministicPlaybackStateAndTimeline()
        {
            var instance = new GameObject("VFX preview state test");
            ParticleSystem particle = instance.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particle.main;
            main.loop = false;
            main.duration = 2f;
            main.startLifetime = 0.5f;
            using var preview = new VfxLibraryPreviewSession();
            preview.SelectInstance(instance);

            Assert.That(preview.HasInstance, Is.True);
            Assert.That(preview.State, Is.EqualTo(VfxLibraryPlaybackState.Playing));
            Assert.That(preview.EstimatedDurationSeconds, Is.GreaterThanOrEqualTo(2.5f));
            preview.SetRootPosition(new Vector3(1f, 2f, 3f));
            Assert.That(preview.RootPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            preview.SetGridVisible(false);
            Assert.That(preview.GridVisible, Is.False);
            preview.SetCameraMode("俯视");
            Assert.That(preview.CameraMode, Is.EqualTo("俯视"));
            preview.Pause();
            Assert.That(preview.State, Is.EqualTo(VfxLibraryPlaybackState.Paused));
            preview.SetTimelineDuration(4f);
            preview.Scrub(0.5f);
            Assert.That(preview.EstimatedDurationSeconds, Is.EqualTo(4f));
            Assert.That(preview.ElapsedSeconds, Is.EqualTo(2f).Within(0.001f));
            preview.Stop();
            Assert.That(preview.State, Is.EqualTo(VfxLibraryPlaybackState.Stopped));
            Assert.That(preview.ElapsedSeconds, Is.Zero);
        }

        [TestCase((int)VfxPreviewMotionMode.Stationary, 0f, 0f, 0f, 0f)]
        [TestCase((int)VfxPreviewMotionMode.Linear, 0f, 1f, 4f, 0f)]
        [TestCase((int)VfxPreviewMotionMode.Linear, 90f, 1f, 0f, 4f)]
        [TestCase((int)VfxPreviewMotionMode.PingPong, 0f, 3f, 4f, 0f)]
        [TestCase((int)VfxPreviewMotionMode.Parabola, 0f, 1f, 4f, 3f)]
        public void PreviewMotion_EvaluatesDeterministicTrajectorySamples(
            int modeValue,
            float direction,
            float time,
            float expectedX,
            float expectedYOrZ)
        {
            var settings = new VfxPreviewMotionSettings(
                (VfxPreviewMotionMode)modeValue,
                direction,
                8f,
                4f,
                3f,
                false);

            Vector3 position = settings.EvaluatePosition(time);

            if (Mathf.Approximately(direction, 90f))
                Assert.That(position.z, Is.EqualTo(expectedYOrZ).Within(0.0001f));
            else
            {
                Assert.That(position.x, Is.EqualTo(expectedX).Within(0.0001f));
                Assert.That(position.y, Is.EqualTo(expectedYOrZ).Within(0.0001f));
            }
        }

        [Test]
        public void PreviewMotion_OrbitUsesOriginAsCenter()
        {
            var settings = new VfxPreviewMotionSettings(
                VfxPreviewMotionMode.Orbit,
                0f,
                8f,
                4f,
                3f,
                false);

            Vector3 start = settings.EvaluatePosition(0f);
            Vector3 quarterTurn = settings.EvaluatePosition(Mathf.PI * 0.5f);

            Assert.That(Vector3.Distance(start, new Vector3(4f, 0f, 0f)), Is.LessThan(0.0001f));
            Assert.That(quarterTurn.x, Is.Zero.Within(0.0001f));
            Assert.That(quarterTurn.z, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void PreviewMotion_ScrubSynchronizesRootsAndCameraBehaviorWithoutDrift()
        {
            using var fixedPreview = new VfxLibraryPreviewSession();
            using var followPreview = new VfxLibraryPreviewSession();
            fixedPreview.SelectInstance(new GameObject("fixed motion preview"));
            followPreview.SelectInstance(new GameObject("follow motion preview"));
            fixedPreview.SetTimelineDuration(2f);
            followPreview.SetTimelineDuration(2f);
            var fixedCamera = new VfxPreviewCameraState(Vector2.zero, 8f, Vector3.up);
            fixedPreview.ApplyCameraState(fixedCamera);
            followPreview.ApplyCameraState(fixedCamera);
            fixedPreview.SetPreviewMotion(new VfxPreviewMotionSettings(
                VfxPreviewMotionMode.Linear, 0f, 8f, 4f, 3f, false));
            followPreview.SetPreviewMotion(new VfxPreviewMotionSettings(
                VfxPreviewMotionMode.Linear, 0f, 8f, 4f, 3f, true));

            fixedPreview.Scrub(0.5f);
            followPreview.Scrub(0.5f);
            Vector3 firstFollowTarget = followPreview.CameraTarget;
            followPreview.Scrub(0.5f);

            Assert.That(fixedPreview.RootPosition.x, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(followPreview.RootPosition, Is.EqualTo(fixedPreview.RootPosition));
            Assert.That(fixedPreview.CameraTarget, Is.EqualTo(Vector3.up));
            Assert.That(
                Vector3.Distance(firstFollowTarget, Vector3.up + Vector3.right * 4f),
                Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(followPreview.CameraTarget, firstFollowTarget), Is.LessThan(0.0001f));
            Assert.That(fixedPreview.State, Is.EqualTo(VfxLibraryPlaybackState.Paused));
            Assert.That(followPreview.State, Is.EqualTo(VfxLibraryPlaybackState.Paused));
        }

        [Test]
        public void PreviewMotion_ProjectileTrailProducesParticlesAlongMovingRoot()
        {
            const string path =
                "Assets/Piloto Studio/Super Realistic FX Bundle/ARPG_Realistic Essentials Fire/Prefabs/Ranged/Red/Projectile_Trail.prefab";
            using var preview = new VfxLibraryPreviewSession();
            preview.Select(path);
            preview.SetTimelineDuration(5f);
            preview.SetPreviewMotion(new VfxPreviewMotionSettings(
                VfxPreviewMotionMode.Linear,
                0f,
                8f,
                4f,
                3f,
                false));

            preview.Scrub(0.25f);

            Assert.That(preview.HasInstance, Is.True);
            Assert.That(preview.RootPosition.x, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(preview.TotalParticleCount, Is.GreaterThan(0));
            Assert.That(preview.State, Is.EqualTo(VfxLibraryPlaybackState.Paused));
        }

        [Test]
        public void PreviewMotion_SynchronizedPlaybackUsesMasterTime()
        {
            using var source = new VfxLibraryPreviewSession();
            using var target = new VfxLibraryPreviewSession();
            source.SelectInstance(new GameObject("motion source"));
            target.SelectInstance(new GameObject("motion target"));
            source.SetTimelineDuration(2f);
            target.SetTimelineDuration(2f);
            var motion = new VfxPreviewMotionSettings(
                VfxPreviewMotionMode.Parabola,
                25f,
                8f,
                4f,
                3f,
                false);
            source.SetPreviewMotion(motion);
            target.SetPreviewMotion(motion);

            source.StepFrame();
            target.SynchronizePlaybackFrom(source);

            Assert.That(target.ElapsedSeconds, Is.EqualTo(source.ElapsedSeconds).Within(0.00001f));
            Assert.That(Vector3.Distance(target.RootPosition, source.RootPosition), Is.LessThan(0.00001f));
            Assert.That(target.State, Is.EqualTo(source.State));
        }

        [Test]
        public void PreviewSession_ContinuousFullLifecycleAutoExitsAfterOneLoopCycle()
        {
            var root = new GameObject("VFX full lifecycle preview test");
            GameObject build = new GameObject("Build"); build.transform.SetParent(root.transform);
            GameObject rebuild = new GameObject("Rebuild"); rebuild.transform.SetParent(root.transform);
            GameObject loop = new GameObject("Loop"); loop.transform.SetParent(root.transform);
            GameObject complete = new GameObject("Complete"); complete.transform.SetParent(root.transform);
            GameObject dissipate = new GameObject("Dissipate"); dissipate.transform.SetParent(root.transform);
            UnityVfxLifecycleController controller = root.AddComponent<UnityVfxLifecycleController>();
            controller.Configure(build, rebuild, loop, complete, dissipate, 60, 60, 60, 60, 60, UnityVfxLoopPlaybackMode.Continuous);

            using var preview = new VfxLibraryPreviewSession();
            preview.SelectInstance(root);
            preview.SetTimelineDuration(3f);
            preview.SetLifecycleAutoExit(2f, VfxStopMode.Complete);
            preview.Scrub(0.9f);

            Assert.That(preview.ElapsedSeconds, Is.EqualTo(2.7f).Within(0.0001f));
            Assert.That(preview.CurrentLifecyclePhase, Is.EqualTo(UnityVfxLifecyclePhase.Complete));
            Assert.That(preview.CharacterAnimationSampleSeconds, Is.EqualTo(preview.CurrentLifecyclePhaseFrame / 60f).Within(0.0001f));
        }

        [Test]
        public void PreviewSession_PreservesCameraDistanceWhenPreviewInstanceIsRebuilt()
        {
            using var preview = new VfxLibraryPreviewSession();
            GameObject first = GameObject.CreatePrimitive(PrimitiveType.Cube);
            preview.SelectInstance(first);
            float framedDistance = preview.CameraDistance;

            GameObject replacement = GameObject.CreatePrimitive(PrimitiveType.Cube);
            replacement.transform.localScale = Vector3.one * 20f;
            preview.SelectInstance(replacement);

            Assert.That(preview.CameraDistance, Is.EqualTo(framedDistance).Within(0.0001f));
        }

        [Test]
        public void PreviewSession_DefaultPerspectiveLooksDownFromAbove()
        {
            using var preview = new VfxLibraryPreviewSession();
            preview.SelectInstance(GameObject.CreatePrimitive(PrimitiveType.Sphere));

            Texture2D capture = preview.CaptureFrame(192, 108);
            try
            {
                Assert.That(preview.CameraOrbit.y, Is.GreaterThan(0f));
                Assert.That(preview.CameraWorldPosition.y, Is.GreaterThan(preview.CameraTarget.y));
            }
            finally
            {
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
            }
        }

        [Test]
        public void PreviewSession_RestoresSavedCameraPose()
        {
            using var preview = new VfxLibraryPreviewSession();
            preview.SetCameraPose(new Vector2(42f, -18f), 7.5f);

            Assert.That(preview.CameraOrbit, Is.EqualTo(new Vector2(42f, -18f)));
            Assert.That(preview.CameraDistance, Is.EqualTo(7.5f).Within(0.0001f));
        }
        [Test]
        public void PreviewSession_ApplyCameraStateSynchronizesWorldPositionImmediately()
        {
            using var source = new VfxLibraryPreviewSession();
            using var target = new VfxLibraryPreviewSession();
            source.SelectInstance(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            target.SelectInstance(GameObject.CreatePrimitive(PrimitiveType.Cube));
            source.SetRootPosition(new Vector3(3f, 2f, -4f));
            source.SetCameraPose(new Vector2(63f, 31f), 8.25f);

            Texture2D sourceCapture = source.CaptureFrame(192, 108);
            Texture2D targetCapture = target.CaptureFrame(192, 108);
            try
            {
                target.ApplyCameraState(source.CaptureCameraState());

                Assert.That(target.CameraOrbit, Is.EqualTo(source.CameraOrbit));
                Assert.That(target.CameraDistance, Is.EqualTo(source.CameraDistance).Within(0.0001f));
                Assert.That(target.CameraTarget, Is.EqualTo(source.CameraTarget));
                Assert.That(Vector3.Distance(target.CameraWorldPosition, source.CameraWorldPosition), Is.LessThan(0.0001f));
            }
            finally
            {
                if (sourceCapture != null) UnityEngine.Object.DestroyImmediate(sourceCapture);
                if (targetCapture != null) UnityEngine.Object.DestroyImmediate(targetCapture);
            }
        }


        [Test]
        public void PreviewSession_AttachesEffectToCharacterSocketWithoutResettingCamera()
        {
            VfxCharacterPreviewDescriptor character = new VfxCharacterPreviewCatalog().Discover().First(value => value.PackageId == "codex_flow_test");
            Assert.That(VfxCharacterPreviewCatalog.TryResolvePose(character, "Attack", string.Empty, out CharacterActionAnimationClipChoice choice), Is.True);
            Assert.That(CharacterActionPreviewBindingDiscovery.TryLoadAnimationClip(choice, out AnimationClip clip), Is.True);

            using var preview = new VfxLibraryPreviewSession();
            preview.SelectInstance(GameObject.CreatePrimitive(PrimitiveType.Cube));
            float cameraDistance = preview.CameraDistance;
            Assert.That(preview.SetCharacterContext(character.PrefabPath, character.Package, "mainHand", clip, true, 1f, true, true, out string message), Is.True, message);
            GameObject characterInstance = preview.ActiveCharacterInstance;
            Assert.That(preview.HasCharacter, Is.True);
            Assert.That(preview.HasCharacterBoneSync, Is.True);
            Assert.That(preview.AttachmentSocketId, Is.EqualTo("mainHand"));
            Assert.That(preview.CameraDistance, Is.EqualTo(cameraDistance).Within(0.0001f));

            preview.SetTimelineDuration(Mathf.Max(clip.length, 0.5f));
            preview.Scrub(0.1f);
            Vector3 firstSocketPosition = preview.AttachmentWorldPosition;
            Quaternion firstSocketRotation = preview.AttachmentWorldRotation;
            Assert.That(Vector3.Distance(preview.EffectWorldPosition, firstSocketPosition), Is.LessThan(0.0001f));
            preview.Scrub(0.6f);
            Assert.That(Vector3.Distance(preview.EffectWorldPosition, preview.AttachmentWorldPosition), Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Distance(firstSocketPosition, preview.AttachmentWorldPosition) > 0.001f
                || Quaternion.Angle(firstSocketRotation, preview.AttachmentWorldRotation) > 0.1f,
                Is.True,
                "攻击动画采样后主手挂点应随骨骼发生位移或旋转。");
            Assert.That(preview.SetAttachmentSocket("headVfx", out message), Is.True, message);
            preview.ReplaceEffectInstance(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            Assert.That(preview.AttachmentSocketId, Is.EqualTo("headVfx"));
            Assert.That(preview.CameraDistance, Is.EqualTo(cameraDistance).Within(0.0001f));
            preview.ClearCharacterContext();
            Assert.That(preview.HasCharacter, Is.False);
            Assert.That(preview.HasInstance, Is.True);
            Assert.That(preview.CachedCharacterCount, Is.EqualTo(1));
            Assert.That(preview.SetCharacterContext(character.PrefabPath, character.Package, "mainHand", clip, true, 1f, true, true, out message), Is.True, message);
            Assert.That(preview.ActiveCharacterInstance, Is.SameAs(characterInstance), "阶段切换回同一角色时应复用缓存预览实例。");
        }

        [Test]
        public void ElementPreviewSession_DoesNotChangeCompositionPreviewState()
        {
            using var composition = new VfxLibraryPreviewSession();
            using var element = new VfxLibraryPreviewSession();
            composition.SelectInstance(new GameObject("Composition preview"));
            composition.Pause();
            float compositionDistance = composition.CameraDistance;

            var elementInstance = new GameObject("Element preview");
            elementInstance.AddComponent<ParticleSystem>();
            element.SelectInstance(elementInstance);
            element.Restart();

            Assert.That(composition.HasInstance, Is.True);
            Assert.That(composition.State, Is.EqualTo(VfxLibraryPlaybackState.Paused));
            Assert.That(composition.CameraDistance, Is.EqualTo(compositionDistance).Within(0.0001f));
            Assert.That(element.State, Is.EqualTo(VfxLibraryPlaybackState.Playing));
        }

        [Test]
        public void ElementPreviewSession_CapturesCurrentFrameForThumbnail()
        {
            using var preview = new VfxLibraryPreviewSession();
            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            preview.SelectInstance(instance);
            preview.SetCameraPose(new Vector2(35f, -15f), 4.5f);

            Texture2D capture = preview.CaptureFrame(256, 144);
            try
            {
                Assert.That(capture, Is.Not.Null);
                Assert.That(capture.width, Is.EqualTo(256));
                Assert.That(capture.height, Is.EqualTo(144));
                Assert.That(capture.EncodeToPNG().Length, Is.GreaterThan(64));
                Assert.That(preview.CameraOrbit, Is.EqualTo(new Vector2(35f, -15f)));
                Assert.That(preview.CameraDistance, Is.EqualTo(4.5f).Within(0.0001f));
            }
            finally
            {
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
            }
        }

        [Test]
        public void ElementPreviewSession_StepsOneSixtiethAndKeepsPaused()
        {
            using var preview = new VfxLibraryPreviewSession();
            var instance = new GameObject("Frame step preview");
            instance.AddComponent<ParticleSystem>();
            preview.SelectInstance(instance);
            preview.Pause();

            preview.StepFrame();
            Assert.That(preview.ElapsedSeconds, Is.EqualTo(1f / 60f).Within(0.000001f));
            Assert.That(preview.State, Is.EqualTo(VfxLibraryPlaybackState.Paused));

            preview.StepFrame();
            Assert.That(preview.ElapsedSeconds, Is.EqualTo(2f / 60f).Within(0.000001f));
            Assert.That(preview.State, Is.EqualTo(VfxLibraryPlaybackState.Paused));
        }

        [Test]
        public void ElementPreviewSession_DrivesSubEmitterTreeFromItsRootSystem()
        {
            var root = new GameObject("SubEmitter preview root");
            var emitterObject = new GameObject("Primary");
            emitterObject.transform.SetParent(root.transform, false);
            ParticleSystem primary = emitterObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule primaryMain = primary.main;
            primaryMain.loop = false;
            primaryMain.duration = 0.1f;
            primaryMain.startLifetime = 0.04f;
            primaryMain.startSpeed = 0f;
            ParticleSystem.EmissionModule primaryEmission = primary.emission;
            primaryEmission.rateOverTime = 0f;
            primaryEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var subEmitterObject = new GameObject("Death burst");
            subEmitterObject.transform.SetParent(emitterObject.transform, false);
            ParticleSystem deathBurst = subEmitterObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule deathMain = deathBurst.main;
            deathMain.loop = false;
            deathMain.duration = 0.1f;
            deathMain.startLifetime = 1f;
            deathMain.startSpeed = 0f;
            ParticleSystem.EmissionModule deathEmission = deathBurst.emission;
            deathEmission.rateOverTime = 0f;
            deathEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            ParticleSystem.SubEmittersModule subEmitters = primary.subEmitters;
            subEmitters.enabled = true;
            subEmitters.AddSubEmitter(
                deathBurst,
                ParticleSystemSubEmitterType.Death,
                ParticleSystemSubEmitterProperties.InheritNothing);

            using var preview = new VfxLibraryPreviewSession();
            preview.SelectInstance(root);
            preview.Pause();

            Assert.That(preview.ParticleSimulationRootCount, Is.EqualTo(1));
            for (int i = 0; i < 8; i++) preview.StepFrame();

            Assert.That(deathBurst.particleCount, Is.GreaterThan(0), "Death SubEmitter should be triggered by the root simulation instead of being played independently.");
            Assert.That(preview.State, Is.EqualTo(VfxLibraryPlaybackState.Paused));
        }

        [Test]
        public void ElementPreviewSession_ManaProjectileRainDrivesCompleteSubEmitterGraphAndFramesLiveParticles()
        {
            const string prefabPath = "Assets/Piloto Studio/Super Realistic FX Bundle/ARPG Mana theme pack/Complex/1) Mana Projectile Rain.prefab";
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), Is.Not.Null);

            using var preview = new VfxLibraryPreviewSession();
            preview.Select(prefabPath);
            preview.SetTimelineDuration(8.3f);
            preview.Scrub(0.1f);
            preview.FrameCamera();

            Assert.That(preview.ParticleSimulationRootCount, Is.EqualTo(30), "Only the 18 referenced SubEmitter targets should be excluded from direct playback.");
            Assert.That(preview.ActiveParticleSystemCount, Is.GreaterThan(20));
            Assert.That(preview.TotalParticleCount, Is.GreaterThan(50));
            Assert.That(preview.CameraTarget.magnitude, Is.LessThan(5f), "Live particle bounds should win over inflated renderer bounds.");
            Assert.That(preview.CameraDistance, Is.InRange(2f, 100f));

            Texture2D capture = preview.CaptureFrame(480, 270);
            try
            {
                Assert.That(capture, Is.Not.Null);
                Color32[] pixels = capture.GetPixels32();
                int blueEffectPixels = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    if (pixel.b > pixel.r + 30 && pixel.b > pixel.g + 10 && pixel.b > 110)
                        blueEffectPixels++;
                }
                Assert.That(blueEffectPixels, Is.GreaterThan(10), "The real Mana compound should render visible effect pixels, not only the background/grid.");
            }
            finally
            {
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
            }
        }

        [Test]
        public void ElementPreviewSession_FrameCameraTargetsVisibleContentCenter()
        {
            using var preview = new VfxLibraryPreviewSession();
            var root = new GameObject("Centered preview");
            GameObject visible = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visible.transform.SetParent(root.transform, false);
            visible.transform.localPosition = new Vector3(2f, -1.5f, 0.75f);
            preview.SelectInstance(root);

            preview.FrameCamera();

            Assert.That(preview.CameraTarget, Is.EqualTo(visible.GetComponent<Renderer>().bounds.center));
        }

        [Test]
        public void PreviewSession_DrawIgnoresInvalidLayoutRects()
        {
            using var preview = new VfxLibraryPreviewSession();

            Assert.DoesNotThrow(() => preview.Draw(new Rect(0f, 0f, float.NaN, 144f)));
            Assert.DoesNotThrow(() => preview.Draw(new Rect(0f, 0f, 256f, float.PositiveInfinity)));
            Assert.DoesNotThrow(() => preview.Draw(new Rect(0f, 0f, 1f, 1f)));
        }

        [Test]
        public void PreviewSession_GameLookUsesUrpPostProcessingForLiveAndCapturePaths()
        {
            using var preview = new VfxLibraryPreviewSession();
            preview.SelectInstance(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            preview.SetPostProcessingMode("游戏观感");

            Texture2D capture = preview.CaptureFrame(192, 108);
            try
            {
                Assert.That(preview.PostProcessingAvailable, Is.True, "当前 VFX 工作台项目应运行在 URP 下。");
                Assert.That(preview.PostProcessingEnabled, Is.True);
                Assert.That(preview.HasPostProcessingProfile, Is.True);
                Assert.That(preview.UsesSharedPostProcessingProfile, Is.True);
                Assert.That(preview.IsCameraPostProcessingEnabled, Is.True);
                Assert.That(capture, Is.Not.Null);
            }
            finally
            {
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
            }

            preview.SetPostProcessingMode("原始材质");
            Assert.That(preview.PostProcessingEnabled, Is.False);
            Assert.That(preview.IsCameraPostProcessingEnabled, Is.False);
        }

        [Test]
        public void ManualElementIconPath_IsTrackedAndIndependentFromTemporaryEvidence()
        {
            const string elementId = "vfx.element.piloto.group.0094d8b3487594ce";
            string first = VfxElementManualIconStore.GetIconPath(elementId);
            string second = VfxElementManualIconStore.GetIconPath(elementId);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Does.StartWith("Assets/VFX/MxFramework/Authoring/ElementIcons/"));
            Assert.That(first, Does.EndWith("/icon.png"));
            Assert.That(first, Does.Not.Contain("Temp/"));
            Assert.That(VfxElementManualIconStore.GetMetadataPath(elementId), Does.EndWith("/icon.json"));
        }

        [Test]
        public void TemporaryElementOverlay_PreservesCompositionCameraAndDraftInstance()
        {
            using var preview = new VfxLibraryPreviewSession();
            GameObject composition = GameObject.CreatePrimitive(PrimitiveType.Cube);
            composition.name = "Composition preview";
            composition.AddComponent<ParticleSystem>();
            Renderer compositionRenderer = composition.GetComponent<Renderer>();
            preview.SelectInstance(composition);
            preview.SetCameraPose(new Vector2(37f, -11f), 6.25f);

            var temporary = new GameObject("Temporary Element");
            temporary.AddComponent<ParticleSystem>();
            preview.SetTemporaryOverlay(temporary);

            Assert.That(preview.HasInstance, Is.True, "临时试放不得替换当前组合实例。");
            Assert.That(preview.HasTemporaryOverlay, Is.True);
            Assert.That(preview.HasPreviewContent, Is.True);
            Assert.That(preview.CameraOrbit, Is.EqualTo(new Vector2(37f, -11f)));
            Assert.That(preview.CameraDistance, Is.EqualTo(6.25f).Within(0.0001f));
            Assert.That(preview.State, Is.EqualTo(VfxLibraryPlaybackState.Playing));

            preview.SetTemporaryOverlaySolo(true);
            Assert.That(preview.TemporaryOverlaySolo, Is.True);
            Assert.That(compositionRenderer.enabled, Is.False, "A 键 Solo 模式只隐藏组合渲染，不修改 Draft Layer。");
            preview.SetTemporaryOverlaySolo(false);
            Assert.That(compositionRenderer.enabled, Is.True);

            preview.ReplaceEffectInstance(new GameObject("Rebuilt composition preview"));
            Assert.That(preview.HasTemporaryOverlay, Is.True, "Layer 显隐或 Solo 重建组合时应保留临时试放层。");
            preview.ClearTemporaryOverlay();
            Assert.That(preview.HasTemporaryOverlay, Is.False);
            Assert.That(preview.HasInstance, Is.True, "移除临时试放不得移除正式组合预览。");
            Assert.That(preview.CameraDistance, Is.EqualTo(6.25f).Within(0.0001f));
        }

        [Test]
        public void CompositionTimeline_UsesStableSixtyFpsFrameConversions()
        {
            Assert.That(VfxManualCompositionStudioController.PreviewFramesPerSecond, Is.EqualTo(60));
            Assert.That(VfxManualCompositionStudioController.SecondsToFrame(0f), Is.Zero);
            Assert.That(VfxManualCompositionStudioController.SecondsToFrame(1.5f), Is.EqualTo(90));
            Assert.That(VfxManualCompositionStudioController.FrameToSeconds(90), Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(VfxManualCompositionStudioController.FrameToSeconds(-1), Is.EqualTo(-1f / 60f).Within(0.0001f));
        }

        [Test]
        public void FullLifecycleTimeline_AccumulatesBuildLoopAndCompleteFrames()
        {
            var draft = new VfxCompositionDraftView
            {
                PhaseDurationOverrides = new VfxCompositionPhaseDurationOverridesView
                {
                    BuildFrames = 480,
                    LoopFrames = 120,
                    CompleteFrames = 300
                }
            };

            Assert.That(VfxManualCompositionStudioController.CalculateFullLifecyclePreviewFrames(draft, null), Is.EqualTo(900));
        }

        [Test]
        public void PhaseDuration_AutomaticValueUsesLatestEnabledLayerEndFrame()
        {
            var draft = new VfxCompositionDraftView();
            draft.Layers.Add(new VfxCompositionLayerView
            {
                LayerId = "build-a",
                ElementId = "missing-a",
                Phase = "Build",
                StartFrame = 30,
                Timing = new VfxLayerTimingView
                {
                    SourceSegments = new VfxTimeSegmentsView { EntryFrames = 10, BodyFrames = 60, ExitFrames = 20 },
                    TargetArtisticFrames = 90
                },
                Enabled = true
            });
            draft.Layers.Add(new VfxCompositionLayerView
            {
                LayerId = "build-disabled",
                ElementId = "missing-b",
                Phase = "Build",
                StartFrame = 600,
                Enabled = false
            });

            Assert.That(VfxManualCompositionStudioController.CalculateAutomaticPhaseFrames(draft, null, "Build"), Is.EqualTo(120));
            Assert.That(VfxManualCompositionStudioController.CalculateAutomaticPhaseFrames(draft, null, "Complete"), Is.Zero);
            Assert.That(VfxManualCompositionStudioController.CalculateAutomaticPhaseFrames(draft, null, "Loop"), Is.EqualTo(1));
        }

        [Test]
        public void PhaseDurationOverride_UsesNullableV3AutomaticContract()
        {
            var overrides = new VfxCompositionPhaseDurationOverridesView();
            overrides.Set("Build", 45);
            overrides.Set("Loop", 180);
            Assert.That(overrides.Get("Build"), Is.EqualTo(45));
            Assert.That(overrides.Get("Loop"), Is.EqualTo(180));
            overrides.Set("Build", null);
            Assert.That(overrides.Get("Build"), Is.Null);
        }

        [Test]
        public void LifecycleStageFilter_ShowsOnlyMatchingLayerStackRows()
        {
            var build = new VfxCompositionLayerView { LayerId = "build", Phase = "Build" };
            var loop = new VfxCompositionLayerView { LayerId = "loop", Phase = "Loop" };
            var complete = new VfxCompositionLayerView { LayerId = "complete", Phase = "Complete" };

            Assert.That(VfxManualCompositionStudioController.IsLayerVisibleForStage(build, "Build"), Is.True);
            Assert.That(VfxManualCompositionStudioController.IsLayerVisibleForStage(loop, "Build"), Is.False);
            Assert.That(VfxManualCompositionStudioController.IsLayerVisibleForStage(complete, "Build"), Is.False);
            Assert.That(VfxManualCompositionStudioController.IsLayerVisibleForStage(build, "完整生命周期"), Is.True);
            Assert.That(VfxManualCompositionStudioController.IsLayerVisibleForStage(loop, "完整生命周期"), Is.True);
            Assert.That(VfxManualCompositionStudioController.IsLayerVisibleForStage(complete, "完整生命周期"), Is.True);
        }

        [Test]
        public void CompleteElementIndex_DeduplicatesFingerprintsAndPrefersMappedSource()
        {
            var source = new VfxSourceLibraryView { IndexHash = "sha256:index" };
            source.Effects.Add(new VfxSourceEffectView { SourceEffectId = "effect", Family = "Impact" });
            source.Components.Add(new VfxSourceComponentView { ComponentId = "source.component.a", SourceEffectId = "effect", DisplayName = "A", TechnicalFingerprint = "sha256:1234567890abcdef", MaterialGuids = new List<string> { "mat" }, MaxParticles = 5 });
            source.Components.Add(new VfxSourceComponentView { ComponentId = "source.component.b", SourceEffectId = "effect", DisplayName = "B", TechnicalFingerprint = "sha256:1234567890abcdef", MaterialGuids = new List<string> { "mat" }, MaxParticles = 10, ExistingPrimitiveId = "vfx.primitive.existing" });
            var registry = new VfxSourceExtractionRegistryView
            {
                RegistryHash = "sha256:registry",
                Entries = new List<VfxSourceExtractionRegistryEntryView>
                {
                    new VfxSourceExtractionRegistryEntryView { PrimitiveId = "vfx.primitive.existing", SourceComponentIds = new List<string> { "source.component.b" }, TechnicalFingerprints = new List<string> { "sha256:1234567890abcdef" } }
                }
            };
            VfxSourceElementIndexView result = VfxSourceElementIndexService.Build(source, new VfxSourceCurationView(), registry);
            Assert.That(result.Elements, Has.Count.EqualTo(1));
            Assert.That(result.Elements[0].MemberComponentIds[0], Is.EqualTo("source.component.b"));
            Assert.That(result.Elements[0].EquivalentComponentIds, Is.EquivalentTo(new[] { "source.component.a" }));
            Assert.That(result.Elements[0].ExtractionStatus, Is.EqualTo("ReuseExisting"));
        }

        [Test]
        public void CompleteElementIndex_MissingRegistryStillBuildsVisibleComponents()
        {
            var source = new VfxSourceLibraryView { IndexHash = "sha256:index" };
            source.Components.Add(new VfxSourceComponentView
            {
                ComponentId = "source.component.visible",
                SourceEffectId = "effect",
                DisplayName = "Visible Element",
                TechnicalFingerprint = "sha256:fedcba0987654321",
                MaterialGuids = new List<string> { "mat" },
                MaxParticles = 8
            });

            VfxSourceElementIndexView result = VfxSourceElementIndexService.Build(source, null, null);

            Assert.That(result.Elements, Has.Count.EqualTo(1));
            Assert.That(result.Elements[0].DisplayName, Is.EqualTo("Visible Element"));
            Assert.That(result.Elements[0].ExtractionStatus, Is.EqualTo("NotExtracted"));
        }

        [Test]
        public void CuratedElement_IndexGrowthDoesNotHideUnchangedComponent()
        {
            var component = new VfxSourceComponentView
            {
                ComponentId = "source.component.unchanged",
                SourceContentHash = "sha256:component"
            };
            var element = new VfxSourceElementView
            {
                ElementId = "vfx.element.unchanged",
                MemberComponentIds = new List<string> { component.ComponentId },
                SourceContentHashes = new List<string> { component.SourceContentHash }
            };
            var catalog = new VfxSourceElementCatalog
            {
                Index = new VfxSourceLibraryView { IndexHash = "sha256:new-index" },
                Curation = new VfxSourceCurationView { SourceIndexHash = "sha256:old-index" }
            };
            catalog.Components.Add(component.ComponentId, component);
            catalog.Elements.Add(element);

            VfxSourceElementCurationService.ValidateStale(catalog);

            Assert.That(element.Stale, Is.False);
        }

        [Test]
        public void ElementTaxonomy_ProvidesIndependentProductionClassifications()
        {
            var element = new VfxSourceElementView
            {
                ElementId = "vfx.element.test",
                DisplayName = "Blood Rune Projectile Impact",
                Family = "General",
                Theme = "Generic",
                Shape = "Circle",
                Lifecycle = "OneShot",
                TimingPhase = "Peak",
                BudgetMaxParticles = 320,
                MemberComponentIds = new List<string> { "source.component.test" }
            };
            var components = new Dictionary<string, VfxSourceComponentView>(StringComparer.Ordinal)
            {
                ["source.component.test"] = new VfxSourceComponentView
                {
                    ComponentId = "source.component.test",
                    SourceEffectId = "source.effect.test",
                    DisplayName = "Blood_Rune_Projectile_Hit",
                    PrefabPath = "Assets/Piloto Studio/Bloody/Blood_Rune_Projectile_Hit.prefab",
                    ObjectPath = "Rune/Impact",
                    SimulationSpace = "World",
                    RenderMode = "Billboard"
                }
            };
            var effects = new Dictionary<string, VfxSourceEffectView>(StringComparer.Ordinal)
            {
                ["source.effect.test"] = new VfxSourceEffectView { SourceEffectId = "source.effect.test", Family = "Impact" }
            };

            VfxSourceElementTaxonomy.Apply(element, components, effects);

            Assert.That(element.Theme, Is.EqualTo("Blood"));
            Assert.That(element.UseCases, Does.Contain("Impact"));
            Assert.That(element.UseCases, Does.Contain("Projectile"));
            Assert.That(element.UseCases, Does.Contain("Zone"));
            Assert.That(element.Roles, Does.Contain("rune"));
            Assert.That(element.Motions, Does.Contain("Radial"));
            Assert.That(element.SimulationSpaces, Is.EquivalentTo(new[] { "World" }));
            Assert.That(element.RendererModes, Is.EquivalentTo(new[] { "Billboard" }));
            Assert.That(element.BudgetTier, Is.EqualTo("中型 101–500"));
            Assert.That(element.Granularity, Is.EqualTo("单层组件"));
        }

        [Test]
        public void PreviewComponentSelection_UsesStableLocalFileIdForSameNamedSiblings()
        {
            const string folderPath = "Assets/MxFrameworkGenerated/Vfx/__VfxPreviewComponentSelectionTest";
            const string prefabPath = folderPath + "/SameNamedSiblings.prefab";
            GameObject source = null;
            GameObject instance = null;
            try
            {
                AssetDatabase.DeleteAsset(folderPath);
                Directory.CreateDirectory(folderPath);
                source = new GameObject("Source");
                CreateParticleChild(source.transform, "Duplicate");
                CreateParticleChild(source.transform, "Duplicate");
                CreateParticleChild(source.transform, "Unique");
                PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
                UnityEngine.Object.DestroyImmediate(source);
                source = null;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(prefab, Is.Not.Null);
                ParticleSystem[] prefabSystems = prefab.GetComponentsInChildren<ParticleSystem>(true);
                Assert.That(prefabSystems, Has.Length.EqualTo(3));
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefabSystems[0], out _, out long firstLocalFileId), Is.True);
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefabSystems[1], out _, out long selectedLocalFileId), Is.True);
                Assert.That(firstLocalFileId, Is.Not.Zero);
                Assert.That(selectedLocalFileId, Is.Not.Zero);

                instance = UnityEngine.Object.Instantiate(prefab);
                ParticleSystem[] instanceSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
                bool applied = VfxLibraryPreviewSession.ApplyComponentSelection(
                    prefab,
                    instance,
                    new[]
                    {
                        new VfxSourceComponentView
                        {
                            PrefabPath = prefabPath,
                            LocalFileId = selectedLocalFileId,
                            ObjectPath = "Duplicate"
                        }
                    });

                Assert.That(applied, Is.True);
                Assert.That(instanceSystems[0].gameObject.activeSelf, Is.False);
                Assert.That(instanceSystems[0].GetComponent<ParticleSystemRenderer>().enabled, Is.False);
                Assert.That(instanceSystems[1].gameObject.activeSelf, Is.True);
                Assert.That(instanceSystems[1].GetComponent<ParticleSystemRenderer>().enabled, Is.True);
                Assert.That(instanceSystems[2].gameObject.activeSelf, Is.False);

                applied = VfxLibraryPreviewSession.ApplyComponentSelection(
                    prefab,
                    instance,
                    new[]
                    {
                        new VfxSourceComponentView { PrefabPath = prefabPath, LocalFileId = selectedLocalFileId, ObjectPath = "Duplicate" },
                        new VfxSourceComponentView { PrefabPath = prefabPath, LocalFileId = firstLocalFileId, ObjectPath = "Duplicate" }
                    },
                    out ParticleSystem[] orderedSelectedSystems);

                Assert.That(applied, Is.True);
                Assert.That(orderedSelectedSystems, Is.EqualTo(new[] { instanceSystems[0], instanceSystems[1] }));

                applied = VfxLibraryPreviewSession.ApplyComponentSelection(
                    prefab,
                    instance,
                    new[] { new VfxSourceComponentView { PrefabPath = prefabPath, LocalFileId = 0, ObjectPath = "Unique" } });

                Assert.That(applied, Is.True);
                Assert.That(instanceSystems[0].gameObject.activeSelf, Is.False);
                Assert.That(instanceSystems[1].gameObject.activeSelf, Is.False);
                Assert.That(instanceSystems[2].gameObject.activeSelf, Is.True);
            }
            finally
            {
                if (source != null) UnityEngine.Object.DestroyImmediate(source);
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        [Test]
        public void PreviewComponentSelection_KeepsSelectedChildActiveUnderUnselectedParticleParent()
        {
            const string folderPath = "Assets/MxFrameworkGenerated/Vfx/__VfxNestedPreviewComponentSelectionTest";
            const string prefabPath = folderPath + "/NestedParticles.prefab";
            GameObject source = null;
            GameObject instance = null;
            try
            {
                AssetDatabase.DeleteAsset(folderPath);
                Directory.CreateDirectory(folderPath);
                source = new GameObject("Source");
                var parent = new GameObject("Parent");
                parent.transform.SetParent(source.transform, false);
                parent.AddComponent<ParticleSystem>();
                CreateParticleChild(parent.transform, "Child");
                PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
                UnityEngine.Object.DestroyImmediate(source);
                source = null;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(prefab, Is.Not.Null);
                ParticleSystem[] prefabSystems = prefab.GetComponentsInChildren<ParticleSystem>(true);
                Assert.That(prefabSystems, Has.Length.EqualTo(2));
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefabSystems[1], out _, out long childLocalFileId), Is.True);
                Assert.That(childLocalFileId, Is.Not.Zero);

                instance = UnityEngine.Object.Instantiate(prefab);
                ParticleSystem[] instanceSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
                bool applied = VfxLibraryPreviewSession.ApplyComponentSelection(
                    prefab,
                    instance,
                    new[] { new VfxSourceComponentView { PrefabPath = prefabPath, LocalFileId = childLocalFileId, ObjectPath = "Parent/Child" } },
                    out ParticleSystem[] selectedSystems);

                Assert.That(applied, Is.True);
                Assert.That(selectedSystems, Is.EqualTo(new[] { instanceSystems[1] }));
                Assert.That(instanceSystems[0].gameObject.activeSelf, Is.True);
                Assert.That(instanceSystems[0].GetComponent<ParticleSystemRenderer>().enabled, Is.False);
                Assert.That(instanceSystems[0].isPlaying, Is.False);
                Assert.That(instanceSystems[1].gameObject.activeInHierarchy, Is.True);
                Assert.That(instanceSystems[1].GetComponent<ParticleSystemRenderer>().enabled, Is.True);

                selectedSystems[0].Play(false);
                Assert.That(instanceSystems[1].isPlaying, Is.True);
                Assert.That(instanceSystems[0].isPlaying, Is.False);
            }
            finally
            {
                if (source != null) UnityEngine.Object.DestroyImmediate(source);
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        private static void CreateParticleChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.AddComponent<ParticleSystem>();
        }

        [Test]
        public void ElementChineseLabels_LocalizeControlledValuesWithoutChangingUnknownIds()
        {
            Assert.That(VfxSourceElementChineseLabels.Format("Impact"), Is.EqualTo("命中 / 冲击"));
            Assert.That(VfxSourceElementChineseLabels.Format("Directional"), Is.EqualTo("定向运动"));
            Assert.That(VfxSourceElementChineseLabels.Format("HitPoint"), Is.EqualTo("命中点"));
            Assert.That(VfxSourceElementChineseLabels.Format("SourceComplex"), Is.EqualTo("源素材复杂"));
            Assert.That(VfxSourceElementChineseLabels.Format("VerticalBillboard"), Is.EqualTo("垂直公告板"));
            Assert.That(VfxSourceElementChineseLabels.Format("NotExtracted"), Is.EqualTo("尚未提取"));
            Assert.That(VfxSourceElementChineseLabels.Format("piloto.custom.package"), Is.EqualTo("piloto.custom.package"));
            Assert.That(VfxSourceElementChineseLabels.FormatMany(new[] { "flash", "smoke" }), Is.EqualTo("瞬时闪光 / 烟雾"));
        }

        [Test]
        public void ManualPreview_FirstLayerUsesLoopAndIncompleteDraftFallsBackToVisiblePhase()
        {
            var smoke = new VfxSourceElementView();
            smoke.Roles.Add("smoke");
            Assert.That(VfxManualCompositionStudioController.SuggestedPhase(smoke, true), Is.EqualTo("Loop"));
            Assert.That(VfxManualCompositionStudioController.SuggestedPhase(smoke, false), Is.EqualTo("Dissipate"));
            Assert.That(VfxManualCompositionStudioController.ResolveAddPhase("Build", smoke, true), Is.EqualTo("Build"));
            Assert.That(VfxManualCompositionStudioController.ResolveAddPhase("Rebuild", smoke, false), Is.EqualTo("Rebuild"));
            Assert.That(VfxManualCompositionStudioController.ResolveAddPhase("Loop", smoke, false), Is.EqualTo("Loop"));
            Assert.That(VfxManualCompositionStudioController.ResolveAddPhase("Complete", smoke, false), Is.EqualTo("Complete"));
            Assert.That(VfxManualCompositionStudioController.ResolveAddPhase("Dissipate", smoke, false), Is.EqualTo("Dissipate"));
            Assert.That(VfxManualCompositionStudioController.ResolveAddPhase("完整生命周期", smoke, false), Is.EqualTo("Dissipate"));

            var draft = new VfxCompositionDraftView();
            draft.Layers.Add(new VfxCompositionLayerView { LayerId = "smoke", Phase = "Dissipate", Enabled = true });
            Assert.That(VfxManualCompositionStudioController.ResolvePreviewPhase(draft, "完整生命周期"), Is.EqualTo("Dissipate"));
            draft.Layers.Add(new VfxCompositionLayerView { LayerId = "core", Phase = "Loop", Enabled = true });
            Assert.That(VfxManualCompositionStudioController.ResolvePreviewPhase(draft, "完整生命周期"), Is.EqualTo("完整生命周期"));
        }

        [Test]
        public void SourceImportFolder_UsesRealHierarchyAndDescendantMatching()
        {
            const string prefabPath = "Assets/Piloto Studio/Super Realistic FX Bundle/ARPG Mana theme pack/Complex/2) Mana Cross Damage Area.prefab";
            const string folder = "Super Realistic FX Bundle/ARPG Mana theme pack/Complex";

            Assert.That(VfxManualCompositionStudioController.SourceImportFolder(prefabPath), Is.EqualTo(folder));
            Assert.That(VfxManualCompositionStudioController.SourceImportFolder(prefabPath.Replace('/', '\\')), Is.EqualTo(folder));
            Assert.That(VfxManualCompositionStudioController.SourceImportFolder("Assets/Outside/Test.prefab"), Is.Empty);
            Assert.That(VfxManualCompositionStudioController.MatchesSourceImportFolder(prefabPath, string.Empty), Is.True);
            Assert.That(VfxManualCompositionStudioController.MatchesSourceImportFolder(prefabPath, "全部"), Is.True);
            Assert.That(VfxManualCompositionStudioController.MatchesSourceImportFolder(prefabPath, folder), Is.True);
            Assert.That(VfxManualCompositionStudioController.MatchesSourceImportFolder(prefabPath, "Super Realistic FX Bundle"), Is.True);
            Assert.That(VfxManualCompositionStudioController.MatchesSourceImportFolder(prefabPath, "Super Realistic FX"), Is.False);
            Assert.That(VfxManualCompositionStudioController.MatchesSourceImportFolder(prefabPath, "Super Realistic FX Bundle/Other"), Is.False);
        }

        [Test]
        public void SourceImportFolderTree_AggregatesRealPathsWithStableUniqueIds()
        {
            string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { VfxSourceLibraryEditorService.SourceRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(VfxManualCompositionStudioController.SourceImportFolder(path)))
                .ToArray();

            SourceImportFolderNode root = VfxManualCompositionStudioController.BuildSourceImportFolderTree(paths);
            Assert.That(root.DisplayName, Is.EqualTo("全部文件夹"));
            Assert.That(root.DescendantPrefabCount, Is.EqualTo(paths.Length));
            Assert.That(root.Children.Select(node => node.DisplayName), Does.Contain("Super Realistic FX Bundle"));
            Assert.That(root.Children.Select(node => node.DisplayName), Does.Contain("Super Realistic FX Bundle 02"));

            var nodes = new List<SourceImportFolderNode>();
            var pending = new Stack<SourceImportFolderNode>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                SourceImportFolderNode node = pending.Pop();
                nodes.Add(node);
                for (int i = 0; i < node.Children.Count; i++) pending.Push(node.Children[i]);
            }
            Assert.That(nodes.Select(node => node.Path), Is.Unique);
            Assert.That(nodes.Select(node => node.TreeId), Is.Unique);
            Assert.That(nodes.All(node => node.DescendantPrefabCount > 0), Is.True);
        }

        [Test]
        public void WorkbenchUxml_ContainsManualSourceImportAndDevelopmentAgentModes()
        {
            const string path = "Packages/com.vincent.wgameframework/Editor/VfxAuthoringResourceLibraryWindow.uxml";
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            Assert.That(tree, Is.Not.Null);
            TemplateContainer root = tree.CloneTree();
            Assert.That(root.Q<Button>("manual-studio-mode"), Is.Not.Null);
            Assert.That(root.Q<Button>("agent-studio-mode"), Is.Not.Null);
            Assert.That(root.Q("manual-studio-workspace"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("composition-draft-selector"), Is.Null);
            Assert.That(root.Q("composition-draft-current"), Is.Not.Null);
            Assert.That(root.Q<Label>("composition-draft-current-name"), Is.Not.Null);
            Assert.That(root.Q<Label>("composition-draft-current-id"), Is.Not.Null);
            Assert.That(root.Q<Label>("composition-draft-current-meta"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-draft-browser-open"), Is.Not.Null);
            Assert.That(root.Q("composition-draft-browser-overlay"), Is.Not.Null);
            Assert.That(root.Q<ToolbarSearchField>("composition-draft-browser-search"), Is.Not.Null);
            Assert.That(root.Q<ListView>("composition-draft-browser-list"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-draft-browser-close"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-draft-browser-import"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-draft-browser-create"), Is.Not.Null);
            Assert.That(root.Q<TextField>("composition-draft-id"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-copy-draft-id"), Is.Not.Null);
            Assert.That(root.Q<Button>("layer-copy"), Is.Not.Null);
            Assert.That(root.Q<Button>("layer-paste"), Is.Not.Null);
            Assert.That(root.Q<Button>("layer-duplicate"), Is.Null);
            Assert.That(root.Q<Button>("composition-phase-summary-all"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-phase-summary-build"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-phase-summary-dissipate"), Is.Not.Null);
            Assert.That(root.Q("composition-phase-duration-editor"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-maximize-preview"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-reset-layout"), Is.Not.Null);
            Assert.That(root.Q("composition-spatial-context"), Is.Null);
            Assert.That(root.Q(className: "preview-spatial-inline"), Is.Not.Null);
            Assert.That(root.Q(className: "composition-control-row"), Is.Not.Null);
            Assert.That(root.Q("composition-layer-splitter"), Is.Not.Null);
            Assert.That(root.Q("composition-layer-region"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-collapse-layers"), Is.Not.Null);
            Assert.That(root.Query<Button>("composition-new").ToList(), Has.Count.EqualTo(1));
            Assert.That(root.Query<Button>("composition-import-source-prefab").ToList(), Has.Count.EqualTo(1));
            Assert.That(root.Q<Button>("composition-import-source-prefab").text, Is.EqualTo("+ 添加素材"));
            Assert.That(root.Q<Button>("source-import-studio-mode"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-import-studio-mode").text, Is.EqualTo("+ 添加素材"));
            Assert.That(root.Q("source-prefab-import-workspace"), Is.Not.Null);
            Assert.That(root.Q("source-prefab-import-overlay"), Is.Null);
            Assert.That(root.Q("source-prefab-import-panel"), Is.Null);
            Assert.That(root.Q<Button>("source-prefab-import-back"), Is.Not.Null);
            Assert.That(root.Q<ToolbarSearchField>("source-prefab-import-search"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("source-prefab-import-folder"), Is.Null);
            Assert.That(root.Q("source-prefab-import-folder-panel"), Is.Not.Null);
            Assert.That(root.Q("source-prefab-import-folder-tree-host"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-prefab-import-folder-collapse"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("source-prefab-import-theme"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("source-prefab-import-family"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("source-prefab-import-role"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("source-prefab-import-quality"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-prefab-import-source-all"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-prefab-import-source-favorite"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-prefab-import-source-recent"), Is.Not.Null);
            Assert.That(root.Q<ListView>("source-prefab-import-list"), Is.Not.Null);
            Assert.That(root.Q("source-prefab-import-drop"), Is.Not.Null);
            Assert.That(root.Q("source-prefab-import-preview-host"), Is.Not.Null);
            Assert.That(root.Q("source-prefab-import-source-preview-host"), Is.Not.Null);
            Assert.That(root.Q("source-prefab-import-derived-preview-host"), Is.Not.Null);
            Assert.That(root.Q<Label>("source-prefab-import-derived-empty"), Is.Not.Null);
            Assert.That(root.Q<Toggle>("source-prefab-import-derived-solo"), Is.Not.Null);
            Assert.That(root.Q<ListView>("source-prefab-import-members"), Is.Null);
            Assert.That(root.Q("source-prefab-import-member-tree-host"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-prefab-import-member-all"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-prefab-import-member-clear"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-prefab-import-member-branch"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-prefab-import-member-group"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-prefab-import-play"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-prefab-import-pause"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-prefab-import-step"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-prefab-import-restart"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-prefab-import-frame"), Is.Not.Null);
            Assert.That(root.Q<Slider>("source-prefab-import-timeline"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("source-prefab-import-speed"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("source-prefab-import-background"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("source-prefab-import-motion-mode"), Is.Not.Null);
            Assert.That(root.Q<Slider>("source-prefab-import-motion-direction"), Is.Not.Null);
            Assert.That(root.Q<Slider>("source-prefab-import-motion-distance"), Is.Not.Null);
            Assert.That(root.Q<Slider>("source-prefab-import-motion-speed"), Is.Not.Null);
            Assert.That(root.Q<Slider>("source-prefab-import-motion-height"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("source-prefab-import-motion-camera"), Is.Not.Null);
            Assert.That(root.Q<TextField>("source-prefab-import-display-name"), Is.Not.Null);
            Assert.That(root.Q<TextField>("source-prefab-import-source-name"), Is.Not.Null);
            Assert.That(root.Q<Label>("source-prefab-import-validation-index"), Is.Not.Null);
            Assert.That(root.Q<Label>("source-prefab-import-validation-hash"), Is.Not.Null);
            Assert.That(root.Q<Label>("source-prefab-import-validation-members"), Is.Not.Null);
            Assert.That(root.Q<Label>("source-prefab-import-validation-subemitters"), Is.Not.Null);
            Assert.That(root.Q<ListView>("source-prefab-import-existing-list"), Is.Not.Null);
            Assert.That(root.Q<Label>("source-prefab-import-existing-count"), Is.Not.Null);
            Assert.That(root.Q<Label>("source-prefab-import-existing-empty"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-prefab-import-rebuild"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-prefab-import-update-evidence"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-prefab-import-add"), Is.Not.Null);
            Assert.That(root.Q<Button>("source-prefab-import-add-layer"), Is.Not.Null);
            Assert.That(root.Query<Button>("composition-open").ToList(), Has.Count.EqualTo(1));
            Assert.That(root.Query<Button>("composition-save").ToList(), Has.Count.EqualTo(1));
            Assert.That(root.Query<Button>("composition-delete").ToList(), Has.Count.EqualTo(1));
            Assert.That(root.Q<Label>("composition-save-status"), Is.Not.Null);
            Assert.That(root.Q<Label>("composition-draft-save-state"), Is.Not.Null);
            Assert.That(root.Q<Label>("composition-prefab-sync-state"), Is.Not.Null);
            Assert.That(root.Q<Label>("composition-publication-state"), Is.Not.Null);
            Assert.That(root.Q<ListView>("composition-draft-browser-list").fixedItemHeight, Is.EqualTo(92f));
            Assert.That(root.Q<ListView>("element-list"), Is.Not.Null);
            Assert.That(root.Q("element-index-unavailable"), Is.Not.Null);
            Assert.That(root.Q<Label>("element-index-unavailable-status"), Is.Not.Null);
            Assert.That(root.Q<Button>("element-index-rebuild"), Is.Not.Null);
            Assert.That(root.Q<Button>("element-preview"), Is.Null);
            Assert.That(root.Q<Button>("element-add"), Is.Null);
            Assert.That(root.Q("element-quick-preview"), Is.Not.Null);
            Assert.That(root.Q("element-quick-preview-host"), Is.Not.Null);
            Assert.That(root.Q<Button>("element-quick-preview-play"), Is.Not.Null);
            Assert.That(root.Q<Button>("element-quick-preview-pause"), Is.Not.Null);
            Assert.That(root.Q<Button>("element-quick-preview-restart"), Is.Not.Null);
            Assert.That(root.Q<Button>("element-quick-preview-frame"), Is.Not.Null);
            Assert.That(root.Q<Button>("element-quick-preview-step"), Is.Not.Null);
            Assert.That(root.Q<Button>("element-quick-preview-set-icon"), Is.Not.Null);
            Assert.That(root.Q<Button>("element-quick-preview-close"), Is.Not.Null);
            Assert.That(root.Q("composition-temporary-element"), Is.Not.Null);
            Assert.That(root.Q<Label>("composition-temporary-element-name"), Is.Not.Null);
            Assert.That(root.Q<Label>("composition-temporary-element-mode"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-temporary-element-restart"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-temporary-element-clear"), Is.Not.Null);
            ScrollView candidateDetail = root.Q<ScrollView>("candidate-detail-scroll");
            Assert.That(candidateDetail, Is.Not.Null);
            Assert.That(candidateDetail.mode, Is.EqualTo(ScrollViewMode.Vertical));
            Assert.That(candidateDetail.horizontalScrollerVisibility, Is.EqualTo(ScrollerVisibility.Hidden));
            Assert.That(root.Q<ListView>("composition-layer-list"), Is.Not.Null);
            Assert.That(root.Q<Label>("composition-playback-status"), Is.Not.Null);
            Assert.That(root.Q<Toggle>("composition-live-refresh"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("composition-camera"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("composition-post-processing"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-package-filter"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-shape-filter"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-lifecycle-filter"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-use-case-filter"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-motion-filter"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-timing-filter"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-space-filter"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-renderer-filter"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-budget-filter"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-granularity-filter"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-extraction-filter"), Is.Not.Null);
            Assert.That(root.Q<Button>("element-show-equivalents"), Is.Not.Null);
            Assert.That(root.Q<Vector3Field>("composition-root-position"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-root-reset"), Is.Not.Null);
            Assert.That(root.Q<Toggle>("composition-grid"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-camera-perspective"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-camera-top"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-camera-front"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-camera-side"), Is.Not.Null);
            Assert.That(root.Q("character-preview-context"), Is.Not.Null);
            Assert.That(root.Q<Foldout>("character-preview-context"), Is.Null);
            Assert.That(root.Q<Button>("character-preview-context-toggle"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("character-preview-edit-target"), Is.Not.Null);
            Assert.That(root.Q<Label>("character-preview-active-phase"), Is.Not.Null);
            Assert.That(root.Q<Label>("character-preview-override-summary"), Is.Not.Null);
            Assert.That(root.Q<Button>("character-preview-reset-phase"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("character-preview-mode"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("character-preview-character"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("character-preview-pose"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("character-preview-animation"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("character-preview-socket"), Is.Not.Null);
            Assert.That(root.Q<Toggle>("character-preview-animation-loop"), Is.Not.Null);
            Assert.That(root.Q<Slider>("character-preview-animation-speed"), Is.Not.Null);
            Assert.That(root.Q<Button>("character-preview-restart"), Is.Not.Null);
            Assert.That(root.Q<Toggle>("character-preview-visible"), Is.Not.Null);
            Assert.That(root.Q<Toggle>("character-preview-socket-axes"), Is.Not.Null);
            Assert.That(root.Q<Slider>("composition-speed"), Is.Not.Null);
            Assert.That(root.Q<ProgressBar>("composition-budget"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-tab"), Is.Not.Null);
            Assert.That(root.Q<Button>("layer-inspector-tab"), Is.Not.Null);
            Assert.That(root.Q("composition-preview-empty"), Is.Not.Null);
            Assert.That(root.Q<Label>("layer-stack-empty"), Is.Not.Null);
            Assert.That(root.Q<Button>("layer-frame-timeline"), Is.Not.Null);
            Assert.That(root.Q<Label>("layer-current-frame"), Is.Not.Null);
            Assert.That(root.Q<Label>("timeline-tick-4"), Is.Not.Null);
            Assert.That(root.Q<FloatField>("layer-start-offset"), Is.Not.Null);
            Assert.That(root.Q("manual-action-bar"), Is.Null);
            Button bakeButton = root.Q<Button>("composition-bake");
            Button productionBakeButton = root.Q<Button>("composition-bake-production");
            Button abButton = root.Q<Button>("composition-ab-action");
            Assert.That(bakeButton, Is.Not.Null);
            Assert.That(productionBakeButton, Is.Not.Null);
            Assert.That(abButton, Is.Not.Null);
            Assert.That(bakeButton.parent.ClassListContains("layer-toolbar"), Is.True);
            Assert.That(productionBakeButton.parent.ClassListContains("layer-toolbar"), Is.True);
            Assert.That(abButton.parent.ClassListContains("layer-toolbar"), Is.True);
            Assert.That(root.Q(className: "vfx-authoring-boundary-banner"), Is.Null);
            Assert.That(root.Q("footer-summary"), Is.Null);
            Assert.That(root.Q<Button>("authoring-boundary-help"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("composition-skill-slot"), Is.Null);
            Assert.That(root.Q<Button>("composition-bake-skill"), Is.Null);
            Assert.That(root.Q<Button>("composition-enable-playtest"), Is.Null);
            Assert.That(root.Q<Button>("composition-plan-publish"), Is.Null);
            Assert.That(root.Q<DropdownField>("composition-loop-mode"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("semantic-visual-start-mode"), Is.Not.Null);
            Assert.That(root.Q<IntegerField>("semantic-manual-lead-in"), Is.Not.Null);
            Assert.That(root.Q<Label>("semantic-visual-start-summary"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-play-first"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-trigger-rebuild"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-stop-complete"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-stop-dissipate"), Is.Not.Null);
            Assert.That(root.Q<Label>("composition-current-phase"), Is.Null);
            Assert.That(root.Q<IntegerField>("phase-loop-frames"), Is.Null);
            Assert.That(root.Q<IntegerField>("composition-phase-duration-frames"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-phase-duration-auto"), Is.Not.Null);
            Assert.That(root.Q("composition-phase-summary-strip"), Is.Not.Null);
            Assert.That(root.Query<Button>(className: "phase-summary-item").ToList(), Has.Count.EqualTo(6));
            Assert.That(root.Q<Label>("phase-loop-summary"), Is.Not.Null);
            Assert.That(root.Q<Foldout>("element-semantic-editor"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-semantic-member"), Is.Not.Null);
            Assert.That(root.Q<TextField>("element-semantic-name"), Is.Not.Null);
            Assert.That(root.Q<TextField>("element-semantic-member-name"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-semantic-role-primary"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-semantic-role-secondary"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-semantic-role-tertiary"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-semantic-theme"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-semantic-family"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-semantic-shape"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-semantic-anchor"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-semantic-use-case-primary"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-semantic-use-case-secondary"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-semantic-timing"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-semantic-motion"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("element-semantic-quality"), Is.Not.Null);
            Assert.That(root.Q<Label>("element-semantic-technical"), Is.Not.Null);
            Assert.That(root.Q<Toggle>("element-semantic-preferred"), Is.Not.Null);
            Assert.That(root.Q<Toggle>("element-semantic-deprecated"), Is.Not.Null);
            Assert.That(root.Q<Button>("element-semantic-save"), Is.Not.Null);
            Assert.That(root.Q<Button>("element-semantic-reset"), Is.Not.Null);
            Assert.That(root.Q<Button>("semantic-tab"), Is.Not.Null);
            Assert.That(root.Q<Button>("semantic-members-tab"), Is.Not.Null);
            Assert.That(root.Q<Button>("semantic-raw-tab"), Is.Not.Null);
            Assert.That(root.Q<Button>("semantic-diff-tab"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("semantic-edit-scope"), Is.Not.Null);
            Assert.That(root.Q<Label>("semantic-edit-scope-summary"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("semantic-retime-mode"), Is.Not.Null);
            Assert.That(root.Q<Slider>("semantic-material-time-scale"), Is.Not.Null);
            ScrollView libraryTabContent = root.Q<ScrollView>("tab-content");
            Assert.That(libraryTabContent, Is.Not.Null);
            Assert.That(libraryTabContent.mode, Is.EqualTo(ScrollViewMode.Vertical));
            Assert.That(libraryTabContent.horizontalScrollerVisibility, Is.EqualTo(ScrollerVisibility.Hidden));
            ScrollView layerInspector = root.Q<ScrollView>("layer-inspector");
            Assert.That(layerInspector, Is.Not.Null);
            Assert.That(layerInspector.mode, Is.EqualTo(ScrollViewMode.Vertical));
            Assert.That(layerInspector.horizontalScrollerVisibility, Is.EqualTo(ScrollerVisibility.Hidden));
            Assert.That(root.Query(className: "semantic-curve-command-row").ToList(), Has.Count.EqualTo(1));
            Assert.That(root.Q<SliderInt>("semantic-target-frames"), Is.Not.Null, "target length slider");
            Assert.That(root.Q<Label>("semantic-target-frames-error"), Is.Not.Null, "target length error");
            Assert.That(root.Q<Label>("semantic-playback-source-length"), Is.Not.Null, "playback source length");
            Assert.That(root.Q<DropdownField>("semantic-window-end-policy"), Is.Not.Null, "playback window end policy");
            Assert.That(root.Q<SliderInt>("semantic-dissipate-frames"), Is.Not.Null, "playback dissipate frames");
            Assert.That(root.Q("semantic-segment-grid"), Is.Not.Null, "retime segment controls");
            Assert.That(root.Q<ProgressBar>("semantic-entry-frames"), Is.Not.Null, "derived entry segment");
            Assert.That(root.Q<ProgressBar>("semantic-body-frames"), Is.Not.Null, "derived body segment");
            Assert.That(root.Q<SliderInt>("semantic-minimum-body-frames"), Is.Not.Null, "authored minimum body slider");
            Assert.That(root.Q<ProgressBar>("semantic-exit-frames"), Is.Not.Null, "derived exit segment");
            Assert.That(root.Q("semantic-time-segment-bar"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("semantic-curve-property"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("semantic-curve-operation"), Is.Not.Null);
            Assert.That(root.Q<CurveField>("semantic-curve-editor"), Is.Not.Null);
            Assert.That(root.Q<Button>("semantic-curve-reset"), Is.Not.Null);
            Assert.That(root.Q<Foldout>("semantic-minmax-editor"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("semantic-minmax-mode"), Is.Not.Null);
            Assert.That(root.Q<CurveField>("semantic-minmax-curve-min"), Is.Not.Null);
            Assert.That(root.Q<CurveField>("semantic-minmax-curve-max"), Is.Not.Null);
            Assert.That(root.Q<Button>("semantic-minmax-apply"), Is.Not.Null);
            Assert.That(root.Q<Foldout>("semantic-gradient-editor"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("semantic-gradient-mode"), Is.Not.Null);
            Assert.That(root.Q<GradientField>("semantic-gradient-min"), Is.Not.Null);
            Assert.That(root.Q<GradientField>("semantic-gradient-max"), Is.Not.Null);
            Assert.That(root.Q<Button>("semantic-gradient-apply"), Is.Not.Null);
            Assert.That(root.Q<ColorField>("semantic-emission-color"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("semantic-render-mode"), Is.Not.Null);
            Assert.That(root.Q<ObjectField>("semantic-render-material"), Is.Not.Null);
            Assert.That(root.Q<ObjectField>("semantic-texture-main"), Is.Not.Null);
            Assert.That(root.Q<ObjectField>("semantic-texture-mask"), Is.Not.Null);
            Assert.That(root.Q<ObjectField>("semantic-texture-noise"), Is.Not.Null);
            Assert.That(root.Q<ObjectField>("semantic-texture-ramp"), Is.Not.Null);
            Assert.That(root.Q<ObjectField>("semantic-texture-normal"), Is.Not.Null);
            Assert.That(root.Q<ObjectField>("semantic-texture-flipbook"), Is.Not.Null);
            Assert.That(root.Q<Label>("semantic-texture-contract"), Is.Not.Null);
            Assert.That(root.Q<Button>("composition-remap"), Is.Not.Null);
            Assert.That(root.Q<IntegerField>("semantic-emission-window-start"), Is.Not.Null);
            Assert.That(root.Q<IntegerField>("semantic-emission-window-end"), Is.Not.Null);
            Assert.That(root.Q<IntegerField>("semantic-flipbook-cycles"), Is.Not.Null);
            Assert.That(root.Q("semantic-curve-track"), Is.Not.Null);
            Assert.That(root.Q<DropdownField>("semantic-member-property"), Is.Not.Null);
            Assert.That(root.Q<Label>("semantic-profile-coverage"), Is.Not.Null);
            Assert.That(root.Q<Button>("semantic-confirm-profile"), Is.Not.Null);
            Assert.That(root.Q<Button>("semantic-confirm-profile").text, Does.Contain("共享"));
            Assert.That(root.Q<Button>("semantic-copy-profile-gap"), Is.Not.Null);
            Assert.That(root.Q<Label>("semantic-property-capability-summary"), Is.Not.Null);
            Assert.That(root.Q<ListView>("semantic-property-capability-list"), Is.Not.Null);
            ScrollView memberMatrix = root.Q<ScrollView>("semantic-member-matrix");
            Assert.That(memberMatrix, Is.Not.Null);
            Assert.That(memberMatrix.mode, Is.EqualTo(ScrollViewMode.Vertical));
            Assert.That(memberMatrix.horizontalScrollerVisibility, Is.EqualTo(ScrollerVisibility.Hidden));
            Assert.That(root.Q<Label>("semantic-member-summary"), Is.Not.Null);
            Assert.That(root.Q<ListView>("semantic-member-list"), Is.Not.Null);
            Assert.That(root.Q<ListView>("semantic-raw-list"), Is.Not.Null);
            Assert.That(root.Q<ListView>("semantic-diff-list"), Is.Not.Null);
        }
    }
}
