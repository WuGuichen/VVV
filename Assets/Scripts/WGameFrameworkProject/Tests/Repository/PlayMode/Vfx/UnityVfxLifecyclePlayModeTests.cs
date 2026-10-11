using System.Collections;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.Vfx
{
    public sealed class UnityVfxLifecyclePlayModeTests
    {
        [Test]
        public void Lifecycle_ZeroDeltaFreezesPhaseAndExplicitClearStillWorks()
        {
            GameObject root = new GameObject("paused lifecycle regression");
            try
            {
                UnityVfxLifecycleController controller = root.AddComponent<UnityVfxLifecycleController>();
                controller.Configure(null, null, null, null, null, 60, 60, 60, 30, 30, UnityVfxLoopPlaybackMode.OneShot);
                controller.PlayBuild(new VfxInstanceId(1));
                for (int i = 0; i < 100; i++) controller.Advance(0f);
                Assert.That(controller.CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Build));
                Assert.That(controller.CurrentPhaseFrame, Is.Zero);
                controller.Advance(0.5f);
                Assert.That(controller.CurrentPhaseFrame, Is.EqualTo(30));
                controller.PlayExit(VfxStopMode.Complete);
                controller.Advance(0f);
                Assert.That(controller.CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Complete));
                controller.ForceClear();
                Assert.That(controller.CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.None));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Lifecycle_RepeatedBuildRebuildAndBothStopsLeavesNoGhostParticles()
        {
            GameObject root = new GameObject("Lifecycle PlayMode test");
            GameObject build = CreatePhase(root, "Build", loop: false);
            GameObject rebuild = CreatePhase(root, "Rebuild", loop: false);
            GameObject loop = CreatePhase(root, "Loop", loop: true);
            GameObject complete = CreatePhase(root, "Complete", loop: false);
            GameObject dissipate = CreatePhase(root, "Dissipate", loop: false);
            UnityVfxLifecycleController controller = root.AddComponent<UnityVfxLifecycleController>();
            controller.Configure(build, rebuild, loop, complete, dissipate, 1, 1, 2, 1, 1, UnityVfxLoopPlaybackMode.Continuous, 2);

            for (int i = 0; i < 20; i++)
            {
                controller.PlayBuild(new VfxInstanceId(1));
                controller.Advance(1f / 60f);
                yield return null;
                Assert.That(controller.CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Loop));
                controller.PlayRebuild(new VfxInstanceId(1));
                controller.Advance(1f / 60f);
                yield return null;
                Assert.That(controller.CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Loop));
                controller.PlayExit((i & 1) == 0 ? VfxStopMode.Complete : VfxStopMode.Dissipate);
                controller.Advance(3f / 60f);
                yield return null;
                yield return null;
                controller.ForceClear();
                ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(true);
                for (int particleIndex = 0; particleIndex < particles.Length; particleIndex++)
                    Assert.That(particles[particleIndex].particleCount, Is.Zero);
            }

            Object.Destroy(root);
            yield return null;
            Assert.That(root == null, Is.True);
        }

        [UnityTest]
        public IEnumerator BakedScheduleAndPropertyTrack_FollowSharedFloatFrameAndCleanUp()
        {
            GameObject root = new GameObject("Baked schedule PlayMode test");
            GameObject build = new GameObject("Build"); build.transform.SetParent(root.transform, false);
            GameObject rebuild = new GameObject("Rebuild"); rebuild.transform.SetParent(root.transform, false);
            GameObject loop = new GameObject("Loop"); loop.transform.SetParent(root.transform, false);
            GameObject complete = new GameObject("Complete"); complete.transform.SetParent(root.transform, false);
            GameObject dissipate = new GameObject("Dissipate"); dissipate.transform.SetParent(root.transform, false);
            GameObject layer = GameObject.CreatePrimitive(PrimitiveType.Quad);
            layer.name = "Timed Layer";
            layer.transform.SetParent(loop.transform, false);
            ParticleSystem particle = layer.AddComponent<ParticleSystem>();
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particle.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 0.1f;
            main.startLifetime = 0.05f;

            var entry = new UnityVfxBakedLayerScheduleEntry();
            entry.Configure(layer, UnityVfxLifecyclePhase.Loop, 2, 8);
            UnityVfxBakedLayerScheduler scheduler = root.AddComponent<UnityVfxBakedLayerScheduler>();
            scheduler.Configure(new[] { entry });
            Renderer renderer = layer.GetComponent<Renderer>();
            var track = new UnityVfxBakedPropertyTrack();
            track.ConfigureFloat(renderer, "_AuthoringOpacity", UnityVfxLifecyclePhase.Loop, 2, 8, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            UnityVfxBakedPropertyDriver driver = root.AddComponent<UnityVfxBakedPropertyDriver>();
            driver.Configure(new[] { track });
            UnityVfxLifecycleController controller = root.AddComponent<UnityVfxLifecycleController>();
            controller.Configure(build, rebuild, loop, complete, dissipate, 0, 0, 10, 0, 0, UnityVfxLoopPlaybackMode.Continuous, 20);

            controller.PlayBuild(new VfxInstanceId(7));
            Assert.That(controller.CurrentPhase, Is.EqualTo(UnityVfxLifecyclePhase.Loop));
            controller.Advance(1f / 60f);
            Assert.That(particle.isPlaying, Is.False);
            controller.Advance(1f / 60f);
            Assert.That(particle.isPlaying, Is.True);
            controller.Advance(3f / 60f);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            float opacity = block.GetFloat(Shader.PropertyToID("_AuthoringOpacity"));
            Assert.That(opacity, Is.EqualTo(0.5f).Within(0.02f));
            controller.Advance(3f / 60f);
            Assert.That(particle.isEmitting, Is.False);

            controller.PlayExit(VfxStopMode.Complete);
            for (int i = 0; i < 4; i++) yield return null;
            controller.ForceClear();
            Assert.That(particle.particleCount, Is.Zero);
            Object.Destroy(root);
            yield return null;
            Assert.That(root == null, Is.True);
        }

        [UnityTest]
        public IEnumerator BakedPlaybackWindow_StopAndClearClearsAtExactBoundary()
        {
            var root = new GameObject("Playback window PlayMode test");
            var layer = new GameObject("Window layer");
            layer.transform.SetParent(root.transform, false);
            ParticleSystem particle = layer.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particle.main;
            main.playOnAwake = false;
            main.startLifetime = 10f;
            var entry = new UnityVfxBakedLayerScheduleEntry();
            entry.Configure(
                layer,
                UnityVfxLifecyclePhase.Loop,
                0,
                2,
                UnityVfxLayerWindowEndPolicy.StopAndClear,
                0);
            UnityVfxBakedLayerScheduler scheduler = root.AddComponent<UnityVfxBakedLayerScheduler>();
            scheduler.Configure(new[] { entry });

            scheduler.EnterPhase(UnityVfxLifecyclePhase.Loop);
            particle.Emit(1);
            scheduler.AdvancePhase(1.999f);
            Assert.That(particle.particleCount, Is.EqualTo(1));
            scheduler.AdvancePhase(2f);
            Assert.That(particle.particleCount, Is.Zero);

            Object.Destroy(root);
            yield return null;
            Assert.That(root == null, Is.True);
        }

        private static GameObject CreatePhase(GameObject root, string name, bool loop)
        {
            var phase = new GameObject(name);
            phase.transform.SetParent(root.transform, false);
            ParticleSystem particle = phase.AddComponent<ParticleSystem>();
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particle.main;
            main.loop = loop;
            main.duration = 0.01f;
            main.startLifetime = 0.01f;
            main.playOnAwake = false;
            return phase;
        }
    }
}
