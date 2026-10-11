namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestTacticalInputNavigation
    {
        /// <summary>Focus leaves before the clicked card/reference updates its edit token.</summary>
        internal static bool IsContextNavigationPointer()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current?.leftButton.isPressed != true) return false;
#else
            if (!UnityEngine.Input.GetMouseButton(0)) return false;
#endif
            var target = global::FairyGUI.Stage.inst.touchTarget?.gOwner;
            for (var current = target; current != null; current = current.parent)
                switch (current.name)
                {
                    case "compositionList":
                    case "workbenchEntryList":
                    case "btnActivation":
                    case "btnEntrance":
                    case "btnLocalEnd":
                    case "btnExpandShared":
                    case "btnCloseInternal":
                    case "btnConfirmShared":
                    case "btnTacticalBlank":
                    case "btnTacticalTemplate":
                    case "btnTacticalDuplicate":
                    case "btnCompositionCopy":
                    case "btnAuthoringWorkspaceRole":
                    case "btnWorkspaceBack":
                        return true;
                }
            return false;
        }
    }
}
