#if UNITY_EDITOR
using System;
using System.Globalization;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiLabAuthoringWorkspacePickerRowViewModel
    {
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRole Role { get; set; }
        internal string DisplayName { get; set; } = string.Empty;
        internal string RoleKindText { get; set; } = string.Empty;
        internal string StateText { get; set; } = string.Empty;
        internal string RevisionText { get; set; } = string.Empty;
        internal string UpdatedText { get; set; } = string.Empty;
        internal string SourceText { get; set; } = string.Empty;
        internal string Diagnostic { get; set; } = string.Empty;
        internal bool IsCurrent { get; set; }
        internal bool CanSelect { get; set; }
        internal bool CanCopyFromMain { get; set; }

        internal object SelectionPayload => (object)Role;
    }

    internal sealed class CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModel
    {
        internal bool Available { get; set; }
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRole ActiveRole { get; set; }
        internal CharacterTestRuntimeAiLabAuthoringWorkspacePickerRowViewModel[] Rows { get; set; } =
            Array.Empty<CharacterTestRuntimeAiLabAuthoringWorkspacePickerRowViewModel>();
        internal string StatusText { get; set; } = string.Empty;
        internal string Diagnostic { get; set; } = string.Empty;
        internal bool CanCopyExperimentA { get; set; }
        internal bool CanCopyExperimentB { get; set; }
        internal bool ShowResetExperiment { get; set; }
        internal bool CanResetExperiment { get; set; }
        internal bool ShowApplyExperimentToMain { get; set; }
        internal bool CanApplyExperimentToMain { get; set; }
        internal string ApplyDisabledReason { get; set; } = string.Empty;
        internal bool ShowConfirmation { get; set; }
        internal string ConfirmationTitle { get; set; } = string.Empty;
        internal string ConfirmationSummary { get; set; } = string.Empty;
        internal string ConfirmationButtonText { get; set; } = string.Empty;
        internal bool ConfirmationDangerous { get; set; }
        internal CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation ConfirmationOperation { get; set; }
        internal CharacterTestRuntimeAiLabAuthoringWorkspaceRole ConfirmationRole { get; set; }
    }

    internal static class CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModelBuilder
    {
        /// <remarks>LowFreqAlloc: popup open, explicit operation, or panel refresh only.</remarks>
        internal static CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModel Build(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            in CharacterTestRuntimeAiLabAuthoringWorkspacePickerConfirmation confirmation,
            string statusText)
        {
            if (controller == null)
            {
                return new CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModel
                {
                    StatusText = statusText ?? string.Empty,
                    Diagnostic = "Runtime AI Lab 控制器不可用。"
                };
            }

            CharacterTestRuntimeAiLabAuthoringWorkspaceSelectorSnapshot snapshot =
                controller.CaptureAuthoringWorkspaceSelectorSnapshot();
            var viewModel = new CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModel
            {
                Available = snapshot.Available,
                ActiveRole = snapshot.ActiveRole,
                StatusText = statusText ?? string.Empty,
                Diagnostic = snapshot.Diagnostic,
                Rows = BuildRows(snapshot)
            };
            CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview experimentA =
                controller.CaptureAuthoringWorkspaceExperimentPreview(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA);
            CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview experimentB =
                controller.CaptureAuthoringWorkspaceExperimentPreview(
                    CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB);
            viewModel.CanCopyExperimentA = experimentA.Available;
            viewModel.CanCopyExperimentB = experimentB.Available;

            bool activeExperiment = CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsExperiment(
                snapshot.ActiveRole);
            CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview activePreview =
                snapshot.ActiveRole == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA
                    ? experimentA
                    : experimentB;
            viewModel.ShowResetExperiment = activeExperiment && activePreview.Initialized;
            viewModel.CanResetExperiment = viewModel.ShowResetExperiment && activePreview.Available;
            viewModel.ShowApplyExperimentToMain = activeExperiment;
            viewModel.CanApplyExperimentToMain = activeExperiment
                && activePreview.Available
                && activePreview.Initialized
                && activePreview.MainMatchesSource;
            if (viewModel.ShowApplyExperimentToMain && !viewModel.CanApplyExperimentToMain)
            {
                viewModel.ApplyDisabledReason = !activePreview.Available
                    ? activePreview.Diagnostic
                    : !activePreview.Initialized
                        ? "当前实验尚未创建。"
                        : activePreview.MainMatchesSource
                            ? activePreview.Diagnostic
                            : "主工作区在实验创建后已经变化，不能直接覆盖。请重新从主工作区复制到实验，或返回主工作区继续编辑。";
            }

            if (confirmation.Pending)
            {
                CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview preview =
                    confirmation.Role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA
                        ? experimentA
                        : experimentB;
                FillConfirmation(viewModel, confirmation, preview);
            }
            return viewModel;
        }

        private static CharacterTestRuntimeAiLabAuthoringWorkspacePickerRowViewModel[] BuildRows(
            CharacterTestRuntimeAiLabAuthoringWorkspaceSelectorSnapshot snapshot)
        {
            var rows = new CharacterTestRuntimeAiLabAuthoringWorkspacePickerRowViewModel[
                snapshot.Rows.Count];
            for (int i = 0; i < rows.Length; i++)
            {
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRow row = snapshot.Rows[i];
                rows[i] = new CharacterTestRuntimeAiLabAuthoringWorkspacePickerRowViewModel
                {
                    Role = row.Role,
                    DisplayName = row.DisplayName,
                    RoleKindText = row.RoleKindText,
                    StateText = row.StateText,
                    RevisionText = row.Revision > 0 ? "r" + row.Revision : "—",
                    UpdatedText = row.UpdatedUtc.HasValue
                        ? "UTC " + row.UpdatedUtc.Value.ToUniversalTime().ToString(
                            "yyyy-MM-dd HH:mm",
                            CultureInfo.InvariantCulture)
                        : "尚无保存时间",
                    SourceText = row.SourceSummaryText,
                    Diagnostic = row.Diagnostic,
                    IsCurrent = row.IsCurrent,
                    CanSelect = row.CanSelect,
                    CanCopyFromMain = row.CanCopyFromMain
                };
            }
            return rows;
        }

        private static void FillConfirmation(
            CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModel viewModel,
            in CharacterTestRuntimeAiLabAuthoringWorkspacePickerConfirmation confirmation,
            in CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview preview)
        {
            viewModel.ShowConfirmation = true;
            viewModel.ConfirmationOperation = confirmation.Operation;
            viewModel.ConfirmationRole = confirmation.Role;
            string roleName = DisplayName(confirmation.Role);
            switch (confirmation.Operation)
            {
                case CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.CopyMainToExperiment:
                    viewModel.ConfirmationTitle = preview.Initialized
                        ? "覆盖" + roleName + "？"
                        : "创建" + roleName + "？";
                    viewModel.ConfirmationSummary = preview.Initialized
                        ? "当前实验与主工作区有 " + preview.ChangedGroupCount + " 个行为组、"
                            + preview.ChangedCardCount + " 个行为卡不同；实验来源 Main r"
                            + preview.SourceMainRevision + "，当前 Main r" + preview.MainRevision
                            + "。覆盖后仍可从恢复副本找回。"
                            + (string.IsNullOrEmpty(preview.Diagnostic)
                                ? string.Empty
                                : " " + preview.Diagnostic)
                        : "将从主工作区 r" + preview.MainRevision + " 复制 "
                            + preview.MainGroupCount + " 个行为组和 " + preview.MainCardCount
                            + " 个行为卡。";
                    viewModel.ConfirmationButtonText = preview.Initialized ? "确认覆盖" : "确认复制";
                    viewModel.ConfirmationDangerous = preview.Initialized;
                    break;
                case CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.ResetExperiment:
                    viewModel.ConfirmationTitle = "重置" + roleName + "？";
                    viewModel.ConfirmationSummary = "将丢弃当前实验相对主工作区的 "
                        + preview.ChangedGroupCount + " 个行为组和 " + preview.ChangedCardCount
                        + " 个行为卡差异，并从当前 Main r" + preview.MainRevision
                        + " 重新复制；恢复副本仍会保留。";
                    viewModel.ConfirmationButtonText = "确认重置";
                    viewModel.ConfirmationDangerous = true;
                    break;
                case CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.ApplyExperimentToMain:
                    viewModel.ConfirmationTitle = "应用" + roleName + "到主工作区？";
                    viewModel.ConfirmationSummary = "将更新 " + preview.ChangedGroupCount
                        + " 个行为组和 " + preview.ChangedCardCount + " 个行为卡；主工作区"
                        + (preview.MainMatchesSource ? "仍等于实验复制来源" : "已经变化，不能应用")
                        + "。成功后会切回主工作区。";
                    viewModel.ConfirmationButtonText = "确认应用";
                    viewModel.ConfirmationDangerous = true;
                    break;
            }
        }

        private static string DisplayName(CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            return role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA
                ? "实验 A"
                : role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB
                    ? "实验 B"
                    : "主工作区";
        }
    }
}
#endif
