using MxFramework.CharacterRuntimeSpawn.DebugUI.Unity;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Diagnostics;
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
                Assert.That(FindSection(snapshot, "Blend Weights"), Does.Contain("none"));
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
    }
}
