using System;
using MxFramework.Editor.Levels;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>
    /// Projects CharacterTest Gameplay Trigger authoring without owning a
    /// second level document or save participant.
    /// Allocation behavior: AllocByDesign; editor UI construction only.
    /// </summary>
    internal sealed class CharacterTestGameplayTriggerWorkspace :
        IMxLevelEditorWorkspace
    {
        internal const string WorkspaceIdValue =
            "characterTest.gameplayTriggers";

        private readonly CharacterTestWorldObjectLevelEditorModule _owner;

        internal CharacterTestGameplayTriggerWorkspace(
            CharacterTestWorldObjectLevelEditorModule owner)
        {
            _owner = owner
                ?? throw new ArgumentNullException(nameof(owner));
        }

        public string WorkspaceId => WorkspaceIdValue;

        public string DisplayName => "玩法触发器";

        public string OwnerModuleId => _owner.ModuleId;

        public void OnEnable()
        {
            // The shell enables the owner module before activating this
            // projection. This workspace has no independent document state.
        }

        public VisualElement CreateContent(MxLevelEditorWindow window)
        {
            return _owner.CreateGameplayTriggerWorkspace(window);
        }
    }
}
