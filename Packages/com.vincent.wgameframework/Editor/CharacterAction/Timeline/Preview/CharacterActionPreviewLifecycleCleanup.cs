using UnityEditor;

namespace MxFramework.Editor.CharacterAction.Timeline.Preview
{
    [InitializeOnLoad]
    internal static class CharacterActionPreviewLifecycleCleanup
    {
        static CharacterActionPreviewLifecycleCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= CleanupBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= CleanupOnPlayModeStateChanged;
            EditorApplication.delayCall -= CleanupAfterScriptsReload;

            AssemblyReloadEvents.beforeAssemblyReload += CleanupBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += CleanupOnPlayModeStateChanged;
            EditorApplication.delayCall += CleanupAfterScriptsReload;
        }

        private static void CleanupAfterScriptsReload()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            CharacterActionPreviewRuntimeSession.CleanupStalePreviewObjects();
        }

        private static void CleanupBeforeAssemblyReload()
        {
            CharacterActionPreviewRuntimeSession.CleanupStalePreviewObjects();
        }

        private static void CleanupOnPlayModeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                case PlayModeStateChange.EnteredPlayMode:
                case PlayModeStateChange.ExitingPlayMode:
                    CharacterActionPreviewRuntimeSession.CleanupStalePreviewObjects();
                    break;
            }
        }
    }
}
