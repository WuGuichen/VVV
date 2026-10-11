using MxFramework.Audio;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public sealed class AudioMixControllerTests
    {
        [Test]
        public void DialogueDucking_DoesNotOverwriteSavedVolumeOrMute()
        {
            var catalog = new AudioDefinitionCatalog(
                new[]
                {
                    new AudioBusDefinition(AudioMixBusIds.Master, "Master", "bus:/"),
                    new AudioBusDefinition(AudioMixBusIds.Music, "Music", "bus:/Music"),
                    new AudioBusDefinition(AudioMixBusIds.Sfx, "SFX", "bus:/SFX"),
                    new AudioBusDefinition(AudioMixBusIds.Voice, "Voice", "bus:/Voice")
                },
                null,
                null);
            using (var service = new AudioService(catalog, new NullAudioBackend()))
            {
                var controller = new AudioMixController(service, new[]
                {
                    new AudioMixSetting(AudioMixBusIds.Master, 1f),
                    new AudioMixSetting(AudioMixBusIds.Music, 1f),
                    new AudioMixSetting(AudioMixBusIds.Sfx, 1f),
                    new AudioMixSetting(AudioMixBusIds.Voice, 1f)
                });
                Assert.IsTrue(controller.ApplyUserSettings(new[]
                {
                    new AudioMixSetting(AudioMixBusIds.Music, 0.8f),
                    new AudioMixSetting(AudioMixBusIds.Sfx, 0.6f, muted: true)
                }).Success);
                Assert.IsTrue(controller.SetState(AudioMixState.Dialogue).Success);

                AudioDebugSnapshot ducked = service.CaptureSnapshot();
                Assert.AreEqual(0.36f, Find(ducked, AudioMixBusIds.Music).Volume, 0.0001f);
                Assert.AreEqual(0.21f, Find(ducked, AudioMixBusIds.Sfx).Volume, 0.0001f);
                Assert.IsTrue(Find(ducked, AudioMixBusIds.Sfx).Muted);

                Assert.IsTrue(controller.SetState(AudioMixState.Standard).Success);
                AudioDebugSnapshot restored = service.CaptureSnapshot();
                Assert.AreEqual(0.8f, Find(restored, AudioMixBusIds.Music).Volume, 0.0001f);
                Assert.AreEqual(0.6f, Find(restored, AudioMixBusIds.Sfx).Volume, 0.0001f);
                Assert.IsTrue(Find(restored, AudioMixBusIds.Sfx).Muted);
            }
        }

        [Test]
        public void TransitionToState_RampsWithoutChangingPlayerSetting()
        {
            var catalog = new AudioDefinitionCatalog(
                new[] { new AudioBusDefinition(AudioMixBusIds.Music, "Music", "bus:/Music") },
                null,
                null);
            using (var service = new AudioService(catalog, new NullAudioBackend()))
            {
                var controller = new AudioMixController(service, new[] { new AudioMixSetting(AudioMixBusIds.Music, 0.8f) });
                Assert.IsTrue(controller.ApplyUserSettings(new[] { new AudioMixSetting(AudioMixBusIds.Music, 0.8f) }).Success);
                Assert.IsTrue(controller.TransitionToState(AudioMixState.Dialogue, 2f).Success);
                Assert.IsTrue(controller.IsTransitioning);

                Assert.IsTrue(controller.Tick(1f).Success);
                Assert.AreEqual(0.58f, Find(service.CaptureSnapshot(), AudioMixBusIds.Music).Volume, 0.0001f);

                Assert.IsTrue(controller.Tick(1f).Success);
                Assert.IsFalse(controller.IsTransitioning);
                Assert.AreEqual(0.36f, Find(service.CaptureSnapshot(), AudioMixBusIds.Music).Volume, 0.0001f);

                Assert.IsTrue(controller.SetState(AudioMixState.Standard).Success);
                Assert.AreEqual(0.8f, Find(service.CaptureSnapshot(), AudioMixBusIds.Music).Volume, 0.0001f);
            }
        }

        private static AudioDebugBusState Find(AudioDebugSnapshot snapshot, int busId)
        {
            for (int i = 0; i < snapshot.BusStates.Count; i++)
            {
                if (snapshot.BusStates[i].BusId == busId)
                    return snapshot.BusStates[i];
            }
            Assert.Fail("Missing Bus " + busId);
            return default;
        }
    }
}
