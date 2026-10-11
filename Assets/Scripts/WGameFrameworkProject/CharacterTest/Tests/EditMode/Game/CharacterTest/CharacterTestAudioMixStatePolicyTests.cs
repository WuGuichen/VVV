using MxFramework.Audio;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestAudioMixStatePolicyTests
    {
        [TestCase(true, false, true, true, true, AudioMixState.Menu)]
        [TestCase(false, true, true, true, true, AudioMixState.Menu)]
        [TestCase(false, false, true, true, true, AudioMixState.Dialogue)]
        [TestCase(false, false, false, true, true, AudioMixState.Paused)]
        [TestCase(false, false, false, false, true, AudioMixState.Combat)]
        [TestCase(false, false, false, false, false, AudioMixState.Standard)]
        public void Resolve_UsesProductPriorityOrder(
            bool mainMenuVisible,
            bool mainMenuHostOpen,
            bool storyDialogueOpen,
            bool paused,
            bool gameplayEntered,
            AudioMixState expected)
        {
            Assert.AreEqual(
                expected,
                CharacterTestAudioMixStatePolicy.Resolve(
                    mainMenuVisible,
                    mainMenuHostOpen,
                    storyDialogueOpen,
                    paused,
                    gameplayEntered));
        }

        [TestCase(AudioMixState.Menu, true, AudioMusicState.Combat, AudioMusicState.Menu)]
        [TestCase(AudioMixState.Combat, true, AudioMusicState.Exploration, AudioMusicState.Combat)]
        [TestCase(AudioMixState.Standard, true, AudioMusicState.None, AudioMusicState.Exploration)]
        [TestCase(AudioMixState.Standard, false, AudioMusicState.Exploration, AudioMusicState.None)]
        [TestCase(AudioMixState.Paused, true, AudioMusicState.Combat, AudioMusicState.Combat)]
        [TestCase(AudioMixState.Dialogue, true, AudioMusicState.Exploration, AudioMusicState.Exploration)]
        public void ResolveMusic_KeepsSnapshotOnlyStatesAndMapsProductFlow(
            AudioMixState mixState,
            bool gameplayEntered,
            AudioMusicState current,
            AudioMusicState expected)
        {
            Assert.AreEqual(
                expected,
                CharacterTestAudioMixStatePolicy.ResolveMusic(
                    mixState,
                    gameplayEntered,
                    current));
        }
    }
}
