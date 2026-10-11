using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static class CharacterTestCharacterActionHotReloadCommand
    {
        private const string MenuPath = "MxFramework/CharacterTest/Runtime/Reload Character Actions";

        [MenuItem(MenuPath, priority = 2020)]
        private static void ReloadCharacterActions()
        {
            CharacterTestDeveloperDebugPanel[] panels =
                Object.FindObjectsByType<CharacterTestDeveloperDebugPanel>(
                    FindObjectsInactive.Include);
            for (int i = 0; i < panels.Length; i++)
            {
                CharacterTestDeveloperDebugPanel panel = panels[i];
                if (panel == null || !panel.TryReloadCharacterActions(out CharacterTestCharacterActionReloadResult result))
                    continue;

                if (result.Success)
                {
                    EditorUtility.DisplayDialog(
                        "角色动作热重载",
                        "热重载成功。\n来源：" + result.SourceDescription + "\n运行帧：" + result.Frame.Value,
                        "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "角色动作热重载失败",
                        result.Message + "\n\n来源：" + result.SourceDescription,
                        "OK");
                }

                return;
            }

            EditorUtility.DisplayDialog(
                "角色动作热重载不可用",
                "当前 Play Mode 中没有已初始化的 CharacterTest 运行时会话。",
                "OK");
        }

        [MenuItem(MenuPath, true)]
        private static bool CanReloadCharacterActions()
        {
            return EditorApplication.isPlaying;
        }
    }
}
