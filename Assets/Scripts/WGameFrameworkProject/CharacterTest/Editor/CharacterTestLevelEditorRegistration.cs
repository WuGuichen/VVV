using MxFramework.Editor.Levels;
using UnityEditor;

namespace MxFramework.Game.CharacterTest.Editor
{
    [InitializeOnLoad]
    public static class CharacterTestLevelEditorRegistration
    {
        static CharacterTestLevelEditorRegistration()
        {
            MxLevelEditorDocumentOwnerRegistry.Register(CharacterTestLevelEditorDocumentSession.Instance);
            var worldObjects =
                new CharacterTestWorldObjectLevelEditorModule();
            MxLevelEditorModuleRegistry.Register(worldObjects);
            MxLevelEditorWorkspaceRegistry.Register(
                new CharacterTestGameplayTriggerWorkspace(
                    worldObjects));
            MxLevelEditorModuleRegistry.Register(new CharacterTestSceneCharacterLevelEditorModule());
            MxLevelEditorModuleRegistry.Register(
                new CharacterTestGenesisValidationLevelEditorModule(worldObjects));
            MxLevelEditorModuleRegistry.Register(new CharacterTestGuidanceLevelEditorModule());
        }
    }
}
