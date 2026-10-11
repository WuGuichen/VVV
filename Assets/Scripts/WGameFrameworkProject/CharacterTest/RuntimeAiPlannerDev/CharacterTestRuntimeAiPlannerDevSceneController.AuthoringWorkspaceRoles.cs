#if UNITY_EDITOR
namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private CharacterTestRuntimeAiLabAuthoringWorkspaceRoles _authoringWorkspaceRoles;
        private CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult _initialOpenResult;
        private CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult _lastOperationResult;

        internal bool HasAuthoringWorkspaceRoleOpenFailure =>
            _initialOpenResult.Status
                != CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.NotStarted
            && !_initialOpenResult.Success
            && !WorkspaceAvailable;
        internal bool HasActiveRole =>
            _authoringWorkspaceRoles?.HasActiveRole == true;
        // Compatibility aliases for the existing UX-01 projection. Initial-open failure remains
        // independently available through InitialOpenResult/HasAuthoringWorkspaceRoleOpenFailure.
        internal bool HasActiveAuthoringWorkspaceRole => HasActiveRole;
        internal bool AuthoringWorkspacePickerAvailable => _authoringWorkspaceRoles?.Enabled == true;
        internal bool WorkspaceAvailable =>
            TryGetAvailableDraftWorkspace(out _, out _);
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRole ActiveAuthoringWorkspaceRole =>
            _authoringWorkspaceRoles?.ActiveRole
                ?? CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main;
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult InitialOpenResult =>
            _initialOpenResult;
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult LastOperationResult =>
            _lastOperationResult;
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult LastAuthoringWorkspaceRoleResult =>
            _lastOperationResult.Status != CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.NotStarted
                ? _lastOperationResult
                : _initialOpenResult;

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult TrySwitchAuthoringWorkspaceRole(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            return FinishRoleOperation(RoleSession.Switch(role), adoptActive: true);
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult
            TryInitializeAuthoringWorkspaceExperiment(
                CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            return FinishRoleOperation(
                RoleSession.InitializeExperimentFromMain(role),
                adoptActive: false);
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult
            TryResetAuthoringWorkspaceExperiment(
                CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result =
                RoleSession.ResetExperimentFromMain(role);
            return FinishRoleOperation(
                result,
                result.Success && RoleSession.ActiveRole == role);
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult
            TryApplyAuthoringWorkspaceExperimentToMain(
                CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            return FinishRoleOperation(RoleSession.ApplyExperimentToMain(role), adoptActive: true);
        }

        private CharacterTestRuntimeAiLabAuthoringWorkspaceRoles RoleSession =>
            _authoringWorkspaceRoles
                ?? (_authoringWorkspaceRoles =
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRoles.Create(_draftPersistence));

        private CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult OpenInitialAuthoringWorkspaceRole()
        {
            _draftWorkspace = null;
            _initialOpenResult = RoleSession.OpenInitial(_matchConfig);
            return _initialOpenResult;
        }

        private void ResetAuthoringWorkspaceRoleSession()
        {
            _authoringWorkspaceRoles = null;
            _initialOpenResult = default;
            _lastOperationResult = default;
        }

        private CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult FinishRoleOperation(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result,
            bool adoptActive)
        {
            _lastOperationResult = result;
            if (result.Success && adoptActive)
                AdoptRoleWorkspace(result.Workspace);
            _statusMessage = result.Message;
            return result;
        }

        private void AdoptRoleWorkspace(CharacterTestRuntimeAiLabDraftWorkspace workspace)
        {
            if (workspace == null)
                return;
            _draftWorkspace = workspace;
            if (_phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
                _frameLimit = workspace.FrameLimit;
            if (_draftWorkspace.EnsurePublishedProjectCardDrafts(
                    _matchConfig?.ProjectCatalogState?.CardCatalog))
            {
                _draftPersistence.MarkDirty();
            }
            _standaloneProjectCardDrafts.Clear();
            _standaloneProjectCardDraftRevision++;
            ResetBehaviorRecipeSaveCheckpoint(_draftWorkspace);
            InvalidateStageEditAuthority();
            _behaviorCardCompileCache.Clear();
            _draftWorkspace.RestoreSetupToArena(_matchConfig);
            ResetAuthoringRuntimeState();
            BuildCandidateRevision();
        }
    }
}
#endif
