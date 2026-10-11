using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    [InitializeOnLoad]
    internal static class CharacterTestTacticalAuthoringEditorBridgeRegistration
    {
        static CharacterTestTacticalAuthoringEditorBridgeRegistration()
        {
            CharacterTestTacticalAuthoringEditorBridge.ImportRequested = Import;
            CharacterTestTacticalAuthoringEditorBridge.SummaryRequested = CharacterTestTacticalSummaryPreparationWindow.OpenFromLab;
            CharacterTestTacticalAuthoringEditorBridge.SummaryLibraryRequested = CharacterTestTacticalSummaryPreparationWindow.OpenLibraryFromLab;
            CharacterTestRuntimeAiLabDraftNumericIdentities.BatchResolveForAuthoring =
                requests => MxFramework.Editor.CharacterAction.CharacterContentIdentityAuthoringCatalog.BatchGetOrAllocate(requests);
            CharacterTestRuntimeAiLabDraftNumericIdentities.ResolveForAuthoring = (domain, source) =>
                MxFramework.Editor.CharacterAction.CharacterContentIdentityAuthoringCatalog.GetOrAllocate(domain, source);
        }

        private static void Import(CharacterTestRuntimeAiPlannerDevSceneController controller, string workspaceId, int team, int slot, int revision)
        {
            string path = EditorUtility.OpenFilePanel("导入战术作者源到当前组", "StorySource", "json");
            if (string.IsNullOrEmpty(path) || controller == null) return;
            try
            {
                if (!controller.ImportTacticalSource(workspaceId, team, slot, revision, File.ReadAllText(path), out string message))
                    EditorUtility.DisplayDialog("导入未完成", message, "关闭");
            }
            catch (IOException error) { EditorUtility.DisplayDialog("读取失败", error.Message, "关闭"); }
            catch (System.UnauthorizedAccessException error) { EditorUtility.DisplayDialog("读取失败", error.Message, "关闭"); }
        }
    }
}
