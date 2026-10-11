#if UNITY_EDITOR
namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceSelectorSnapshot
            CaptureAuthoringWorkspaceSelectorSnapshot()
        {
            if (_draftPersistence == null)
            {
                return new CharacterTestRuntimeAiLabAuthoringWorkspaceSelectorSnapshot(
                    available: false,
                    hasActiveRole: false,
                    activeRole: CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main,
                    rows: null,
                    diagnostic: "Runtime AI Lab draft persistence is unavailable.");
            }

            return new CharacterTestRuntimeAiLabAuthoringWorkspaceSelector(
                    RoleSession,
                    _draftPersistence)
                .Capture(LastAuthoringWorkspaceRoleResult);
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult
            TrySelectAuthoringWorkspaceRole(
                CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            LeaveConfiguredCard();
            if (_draftPersistence == null)
            {
                return FinishRoleOperation(
                    new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.WorkspaceLoadFailed,
                        role,
                        null,
                        "Runtime AI Lab draft persistence is unavailable."),
                    adoptActive: false);
            }

            bool unchanged = HasActiveAuthoringWorkspaceRole
                && ActiveAuthoringWorkspaceRole == role
                && WorkspaceAvailable;
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result =
                new CharacterTestRuntimeAiLabAuthoringWorkspaceSelector(
                        RoleSession,
                        _draftPersistence)
                    .Select(role);
            return FinishRoleOperation(
                result,
                adoptActive: result.Success && !unchanged);
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview
            CaptureAuthoringWorkspaceExperimentPreview(
                CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            if (_draftPersistence == null)
            {
                return new CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview(
                    role: role,
                    available: false,
                    initialized: false,
                    mainMatchesSource: false,
                    mainRevision: 0,
                    sourceMainRevision: 0,
                    experimentRevision: 0,
                    mainGroupCount: 0,
                    mainCardCount: 0,
                    changedGroupCount: 0,
                    changedCardCount: 0,
                    experimentDirty: false,
                    diagnostic: "Runtime AI Lab draft persistence is unavailable.");
            }

            return new CharacterTestRuntimeAiLabAuthoringWorkspaceSelector(
                    RoleSession,
                    _draftPersistence)
                .CaptureExperimentPreview(role);
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult
            TryCopyMainToAuthoringWorkspaceExperiment(
                CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview preview =
                CaptureAuthoringWorkspaceExperimentPreview(role);
            if (!preview.Available)
            {
                return FinishRoleOperation(
                    new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.WorkspaceLoadFailed,
                        role,
                        null,
                        preview.Diagnostic),
                    adoptActive: false);
            }

            return preview.Initialized
                ? TryResetAuthoringWorkspaceExperiment(role)
                : TryInitializeAuthoringWorkspaceExperiment(role);
        }
    }
}
#endif
