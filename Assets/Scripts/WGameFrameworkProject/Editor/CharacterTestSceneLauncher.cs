using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MxFramework.Project.Editor
{
    internal static class CharacterTestSceneLauncher
    {
        private const string CharacterTestMenuPath = "MxFramework/Editor/Open CharacterTest Scene";
        private const string RuntimeAiLabMenuPath = "MxFramework/Editor/Open Runtime AI Lab Scene";
        private const string CharacterTestScenePath = "Assets/Scenes/CharacterTestScene.unity";
        private const string RuntimeAiLabScenePath = "Assets/Scenes/CharacterTestRuntimeAiLabScene.unity";

        [MenuItem(CharacterTestMenuPath, priority = 21)]
        private static void OpenCharacterTestScene()
        {
            OpenScene(CharacterTestScenePath, "CharacterTest scene");
        }

        [MenuItem(RuntimeAiLabMenuPath, priority = 22)]
        private static void OpenRuntimeAiLabScene()
        {
            OpenScene(RuntimeAiLabScenePath, "Runtime AI Lab scene");
        }

        [MenuItem(CharacterTestMenuPath, true)]
        [MenuItem(RuntimeAiLabMenuPath, true)]
        private static bool CanOpenScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void OpenScene(string scenePath, string displayName)
        {
            if (!File.Exists(scenePath))
            {
                EditorUtility.DisplayDialog(
                    displayName + " not found",
                    "Expected scene at:\n" + scenePath,
                    "OK");
                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
    }
}
