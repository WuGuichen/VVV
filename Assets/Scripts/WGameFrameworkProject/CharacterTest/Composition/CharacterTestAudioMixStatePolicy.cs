using MxFramework.Audio;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Pure priority policy mapping CharacterTest presentation state to the product audio mix.</summary>
    internal static class CharacterTestAudioMixStatePolicy
    {
        /// <remarks>NoAlloc presentation path.</remarks>
        public static AudioMixState Resolve(
            bool mainMenuVisible,
            bool mainMenuHostOpen,
            bool storyDialogueOpen,
            bool paused,
            bool gameplayEntered)
        {
            if (mainMenuVisible || mainMenuHostOpen)
                return AudioMixState.Menu;
            if (storyDialogueOpen)
                return AudioMixState.Dialogue;
            if (paused)
                return AudioMixState.Paused;
            return gameplayEntered ? AudioMixState.Combat : AudioMixState.Standard;
        }

        /// <remarks>NoAlloc presentation path. Pause and dialogue keep the current music under snapshot ducking.</remarks>
        public static AudioMusicState ResolveMusic(
            AudioMixState mixState,
            bool gameplayEntered,
            AudioMusicState current)
        {
            switch (mixState)
            {
                case AudioMixState.Menu:
                    return AudioMusicState.Menu;
                case AudioMixState.Combat:
                    return AudioMusicState.Combat;
                case AudioMixState.Paused:
                case AudioMixState.Dialogue:
                    return current;
                default:
                    return gameplayEntered ? AudioMusicState.Exploration : AudioMusicState.None;
            }
        }
    }
}
