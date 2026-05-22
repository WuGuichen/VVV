using MxFramework.Animation;
using MxFramework.CharacterRuntimeSpawn.DebugUI.Unity;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Diagnostics;
using MxFramework.Resources;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.DebugUI
{
    public sealed class CharacterRuntimeAnimationDebugSourceTests
    {
        [Test]
        public void CreateSnapshot_NullBootstrapReportsStableUnavailableSections()
        {
            var source = new CharacterRuntimeAnimationDebugSource(null);

            FrameworkDebugSnapshot snapshot = source.CreateSnapshot();

            Assert.AreEqual("CharacterAnimation", snapshot.SourceName);
            Assert.That(FindSection(snapshot, "Status"), Does.Contain("available: false"));
            Assert.That(FindSection(snapshot, "Warmup"), Does.Contain("result: none"));
            Assert.That(FindSection(snapshot, "Playback"), Does.Contain("locomotionController: missing"));
            Assert.That(FindSection(snapshot, "Locomotion Blend Probe"), Does.Contain("locomotionController: missing"));
            Assert.That(FindSection(snapshot, "Backend"), Does.Contain("backend: missing"));
        }

        [Test]
        public void CreateSnapshot_LocomotionControllerReportsPlaybackWithoutBackend()
        {
            var character = new GameObject("debug-character");
            try
            {
                CharacterRuntimeLocomotionBlendController locomotion =
                    character.AddComponent<CharacterRuntimeLocomotionBlendController>();
                var source = new CharacterRuntimeAnimationDebugSource(
                    null,
                    locomotionResolver: () => locomotion);

                FrameworkDebugSnapshot snapshot = source.CreateSnapshot();

                string playback = FindSection(snapshot, "Playback");
                Assert.That(playback, Does.Contain("locomotionController: present"));
                Assert.That(playback, Does.Contain("hasBackend: false"));
                Assert.That(playback, Does.Contain("activeBlend2DId: blend.move2d"));
                Assert.That(FindSection(snapshot, "Locomotion Blend Probe"), Does.Contain("probe: unavailable"));
                Assert.That(FindSection(snapshot, "Blend Weights"), Does.Contain("none"));
            }
            finally
            {
                Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void CreateSnapshot_LocomotionBlendProbeReportsDefinitionDomainAndUnreachablePoints()
        {
            var character = new GameObject("debug-character");
            try
            {
                CharacterRuntimeLocomotionBlendController locomotion =
                    character.AddComponent<CharacterRuntimeLocomotionBlendController>();
                ResourceKey idle = ClipKey("demo.animation.idle");
                ResourceKey run = ClipKey("demo.animation.run_f");
                var blend = new MxAnimationBlend2DDefinition(
                    "blend.move2d",
                    "moveX",
                    "moveY",
                    MxAnimationLayerId.Base,
                    new[]
                    {
                        new MxAnimationBlend2DPoint(0, 0, idle),
                        new MxAnimationBlend2DPoint(0, 2000, run)
                    });
                locomotion.ConfigureAnimationBackend(null, blend);

                var source = new CharacterRuntimeAnimationDebugSource(
                    null,
                    locomotionResolver: () => locomotion);

                FrameworkDebugSnapshot snapshot = source.CreateSnapshot();
                string probe = FindSection(snapshot, "Locomotion Blend Probe");

                Assert.That(probe, Does.Contain("blendId: blend.move2d"));
                Assert.That(probe, Does.Contain("domain: x=[-1000,1000] y=[-1000,1000]"));
                Assert.That(probe, Does.Contain("sample: 0, 0"));
                Assert.That(probe, Does.Contain("weightsFromBackend: false"));
                Assert.That(probe, Does.Contain(MxAnimationLocomotionCalibrationIssueCodes.BlendUnreachablePoint));
                Assert.That(probe, Does.Contain("unreachablePoints: 1"));
                Assert.That(probe, Does.Contain("weights:"));
            }
            finally
            {
                Object.DestroyImmediate(character);
            }
        }

        private static string FindSection(FrameworkDebugSnapshot snapshot, string title)
        {
            for (int i = 0; i < snapshot.Sections.Count; i++)
            {
                if (snapshot.Sections[i].Title == title)
                    return snapshot.Sections[i].Body;
            }

            Assert.Fail("Missing debug section: " + title);
            return string.Empty;
        }

        private static ResourceKey ClipKey(string id)
        {
            return new ResourceKey(id, ResourceTypeIds.AnimationClip);
        }
    }
}
