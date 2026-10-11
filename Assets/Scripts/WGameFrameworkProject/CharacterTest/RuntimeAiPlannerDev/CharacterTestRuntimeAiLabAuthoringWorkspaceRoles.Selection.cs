#if UNITY_EDITOR
namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabAuthoringWorkspaceRoles
    {
        /// <summary>
        /// Validates a role target without changing the active workspace or the machine-local
        /// active-role pointer. Unlike the persistence recovery path, the selector deliberately
        /// rejects a damaged current snapshot that could only be opened from its previous copy.
        /// </summary>
        /// <remarks>LowFreqAlloc: workspace picker projection and explicit role selection only.</remarks>
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult InspectSelectionTarget(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role,
            out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleEntry entry)
        {
            entry = null;
            if (!CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsValid(role))
            {
                return Fail(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.InvalidRole,
                    role: role);
            }
            if (!Enabled)
            {
                return Fail(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.DisabledForIsolatedRoot,
                    role: role);
            }

            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryLoadStatus load =
                _registry.Load(
                    out CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryState state,
                    out string message);
            if (load != CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryLoadStatus.Loaded)
            {
                return Fail(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.RegistryInvalid,
                    message,
                    role);
            }

            entry = state.Get(role);
            if (entry == null)
            {
                return Fail(
                    role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main
                        ? CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.MainUnavailable
                        : CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.ExperimentNotInitialized,
                    role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main
                        ? "The Main workspace mapping is missing."
                        : "The selected experiment has not been copied from Main.",
                    role);
            }

            if (!_persistence.TryLoad(
                    entry.WorkspaceId,
                    out CharacterTestRuntimeAiLabDraftWorkspace workspace,
                    out message))
            {
                return Fail(
                    role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main
                        ? CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.MainUnavailable
                        : CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.WorkspaceLoadFailed,
                    message,
                    role);
            }
            if (workspace.RecoveredFromPrevious)
            {
                return Fail(
                    role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main
                        ? CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.MainUnavailable
                        : CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.WorkspaceLoadFailed,
                    "The registered workspace snapshot is damaged. Its previous recovery copy "
                        + "was preserved, but role selection requires an explicit reset or recovery.",
                    role);
            }
            if (workspace.IsReadOnly)
            {
                return Fail(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.ReadOnly,
                    "The registered workspace uses a newer read-only schema and cannot be selected for authoring.",
                    role);
            }

            return Ok(role, workspace, "The authoring workspace role target is available.");
        }
    }
}
#endif
