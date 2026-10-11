#if UNITY_EDITOR
using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommandIds
    {
        private const string Prefix = "character_test.runtime_ai_planner.dev.";

        internal const string SelectRole = Prefix + "select_authoring_workspace_role";
        internal const string CopyMainToExperiment = Prefix + "copy_main_to_experiment";
        internal const string ResetExperiment = Prefix + "reset_authoring_experiment";
        internal const string ApplyExperimentToMain = Prefix + "apply_experiment_to_main";
        internal const string ClosePicker = Prefix + "close_authoring_workspace_picker";
    }

    internal enum CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation
    {
        None = 0,
        CopyMainToExperiment = 1,
        ResetExperiment = 2,
        ApplyExperimentToMain = 3
    }

    internal readonly struct CharacterTestRuntimeAiLabAuthoringWorkspacePickerConfirmation
    {
        internal CharacterTestRuntimeAiLabAuthoringWorkspacePickerConfirmation(
            CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation operation,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            Operation = operation;
            Role = role;
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation Operation { get; }
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRole Role { get; }
        internal bool Pending => Operation != CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.None;

        internal bool Matches(
            CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation operation,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            return Pending && Operation == operation && Role == role;
        }
    }

    /// <summary>Creates picker commands whose payload is always a boxed fixed Role enum.</summary>
    internal static class CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands
    {
        internal static MxUiCommand Select(
            MxUiViewId viewId,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            return Create(viewId, CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommandIds.SelectRole, role);
        }

        internal static MxUiCommand CopyMainToExperiment(
            MxUiViewId viewId,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            return Create(
                viewId,
                CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommandIds.CopyMainToExperiment,
                role);
        }

        internal static MxUiCommand ResetExperiment(
            MxUiViewId viewId,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            return Create(
                viewId,
                CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommandIds.ResetExperiment,
                role);
        }

        internal static MxUiCommand ApplyExperimentToMain(
            MxUiViewId viewId,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            return Create(
                viewId,
                CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommandIds.ApplyExperimentToMain,
                role);
        }

        internal static MxUiCommand Close(MxUiViewId viewId)
        {
            return new MxUiCommand(
                viewId,
                CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommandIds.ClosePicker,
                null);
        }

        private static MxUiCommand Create(
            MxUiViewId viewId,
            string commandId,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            if (!CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsValid(role))
                throw new ArgumentOutOfRangeException(nameof(role));
            return new MxUiCommand(viewId, commandId, (object)role);
        }
    }
}
#endif
