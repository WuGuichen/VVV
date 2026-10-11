using System.Collections;
using System.Reflection;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MxFramework.Tests.PlayMode.Vfx
{
    [Parallelizable(ParallelScope.None)]
    public sealed class UnityVfxNativeClockAcceptanceTests
    {
        [UnityTest]
        public IEnumerator ActualUpdateAndNativeParticles_FollowScaledClockAndInstanceRate()
        {
            float previousScale = Time.timeScale;
            var root = new GameObject("Owned VFX clock acceptance");
            var loop = new GameObject("Loop"); loop.transform.SetParent(root.transform, false);
            var ps = loop.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = true; main.duration = 1000;
            main.startLifetime = 1000; main.useUnscaledTime = false;
            var c = root.AddComponent<UnityVfxLifecycleController>();
            c.Configure(null, null, loop, null, null, 0, 0, 60000, 0, 0, UnityVfxLoopPlaybackMode.Continuous);
            var phase = typeof(UnityVfxLifecycleController).GetField("_phaseElapsedFrames", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                foreach (float rate in new[] { 0.5f, 2f })
                foreach (float scale in new[] { 0f, 0.5f, 1f, 2f })
                {
                    Time.timeScale = scale;
                    c.SetPlaybackRate(rate);
                    // Presenter applies the same instance rate to native simulation; this fixture tests the two native clocks.
                    main = ps.main; main.simulationSpeed = rate;
                    c.PlayBuild(new VfxInstanceId(1));
                    yield return null; yield return new WaitForEndOfFrame();
                    float start = (float)phase.GetValue(c), particleStart = ps.time, elapsed = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        yield return null; yield return new WaitForEndOfFrame();
                        elapsed += Time.deltaTime;
                    }
                    Assert.AreEqual(start + elapsed * 60 * rate, (float)phase.GetValue(c), 0.001f,
                        "Automatic Update phase: scale=" + scale + " rate=" + rate);
                    Assert.AreEqual(particleStart + elapsed * rate, ps.time, 0.035f,
                        "Native particle simulation: scale=" + scale + " rate=" + rate);
                    if (scale == 0) { Assert.AreEqual(start, (float)phase.GetValue(c)); Assert.AreEqual(particleStart, ps.time); }
                    c.ForceClear();
                }
                Time.timeScale = 0;
                c.Configure(null, null, null, null, null, 0, 0, 60000, 60, 60, UnityVfxLoopPlaybackMode.Continuous);
                foreach (var mode in new[] { VfxStopMode.Complete, VfxStopMode.Dissipate })
                {
                    c.PlayBuild(new VfxInstanceId(2)); c.PlayExit(mode);
                    float before = (float)phase.GetValue(c);
                    yield return null; yield return new WaitForEndOfFrame();
                    Assert.AreEqual(before, (float)phase.GetValue(c));
                    c.ForceClear(); Assert.AreEqual(UnityVfxLifecyclePhase.None, c.CurrentPhase);
                }
            }
            finally { Time.timeScale = previousScale; Object.Destroy(root); }
            yield return null;
            Assert.IsTrue(root == null);
        }

        [UnityTest]
        public IEnumerator CompatibilityCleanup_StopsEmissionKeepsLiveNativeTailAndForceClearRemovesIt()
        {
            float previousScale = Time.timeScale;
            var root = new GameObject("Owned native tail fixture");
            var phaseRoot = new GameObject("Complete"); phaseRoot.transform.SetParent(root.transform, false);
            var ps = phaseRoot.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = true; main.duration = 10; main.startLifetime = 1;
            var trails = ps.trails; trails.enabled = true; trails.dieWithParticles = false; trails.lifetime = 1;
            var c = root.AddComponent<UnityVfxLifecycleController>();
            c.Configure(null, null, null, phaseRoot, phaseRoot, 0, 0, 60000, 1, 1, UnityVfxLoopPlaybackMode.Continuous, 120);
            try
            {
                Time.timeScale = 0;
                c.PlayBuild(new VfxInstanceId(3)); c.PlayExit(VfxStopMode.Complete);
                ps.Emit(1); Assert.Greater(ps.particleCount, 0);
                c.Advance(1f / 60f);
                Assert.IsFalse(ps.isEmitting); Assert.Greater(ps.particleCount, 0);
                Assert.IsFalse(c.IsLifecycleComplete, "Compatibility cleanup must retain live native particles.");
                yield return null;
                Assert.Greater(ps.particleCount, 0, "Paused tail remains alive until explicit clear.");
                c.ForceClear(); Assert.AreEqual(0, ps.particleCount); Assert.IsFalse(ps.IsAlive(true));
            }
            finally { Time.timeScale = previousScale; Object.Destroy(root); }
            yield return null; Assert.IsTrue(root == null);
        }
    }
}
