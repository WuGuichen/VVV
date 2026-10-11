using MxFramework.Audio;
using NUnit.Framework;

namespace MxFramework.Tests.Audio
{
    public sealed class AudioMusicStateControllerTests
    {
        [Test]
        public void TransitionTo_ReplacesMusicAndReportsMissingBinding()
        {
            var catalog = new AudioDefinitionCatalog(
                new[] { new AudioBusDefinition(AudioMixBusIds.Music, "Music", "bus:/Music") },
                new[]
                {
                    Music(610001, "menu"),
                    Music(610002, "combat")
                },
                null);
            using (var service = new AudioService(catalog, new NullAudioBackend()))
            using (var music = new AudioMusicStateController(service, new[]
            {
                new AudioMusicStateBinding(AudioMusicState.Menu, 610001),
                new AudioMusicStateBinding(AudioMusicState.Combat, 610002)
            }))
            {
                Assert.IsTrue(music.TransitionTo(AudioMusicState.Menu).Success);
                AudioHandle menu = music.ActiveHandle;
                Assert.IsTrue(menu.IsValid);

                Assert.IsTrue(music.TransitionTo(AudioMusicState.Combat).Success);
                Assert.IsFalse(service.IsHandleActive(menu));
                Assert.AreEqual(610002, music.ActiveHandle.EventId);

                AudioResult missing = music.TransitionTo(AudioMusicState.Victory);
                Assert.AreEqual(AudioErrorCode.InvalidEvent, missing.ErrorCode);
            }
        }

        private static AudioEventDefinition Music(int id, string name)
        {
            return new AudioEventDefinition(
                id,
                name,
                "event:/Music/" + name,
                string.Empty,
                AudioEventKind.Event,
                AudioMixBusIds.Music,
                false,
                true,
                0f);
        }
    }
}
