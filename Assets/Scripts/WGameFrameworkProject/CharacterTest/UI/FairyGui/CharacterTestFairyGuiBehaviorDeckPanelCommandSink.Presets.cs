using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiBehaviorDeckPanelCommandSink
    {
        private CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview _presetPreview;
        private long _presetExitFocusSequence;
        private string _presetExitFocusTargetId = string.Empty;

        internal CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview PresetPreview => _presetPreview;
        internal bool PresetPreviewVisible => _presetPreview != null;
        internal long PresetExitFocusSequence => _presetExitFocusSequence;
        internal string PresetExitFocusTargetId => _presetExitFocusTargetId;
        internal bool HasTopModal => _presetPreview != null || !string.IsNullOrEmpty(DetailsStableCardId);

        internal bool TryDismissTopModalForVisibilityToggle(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (_presetPreview != null)
            {
                CancelRecommendedPreset();
                return true;
            }
            if (!string.IsNullOrEmpty(DetailsStableCardId))
            {
                CloseDetails();
                return true;
            }
            if (InstallSelectionActive)
            {
                InstallSelectionActive = false;
                LastStatusMessage = string.Empty;
                return true;
            }
            return false;
        }

        private bool HandlePresetModalCommand(MxUiCommand command)
        {
            if (string.Equals(
                    command.CommandId,
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ConfirmRecommendedPreset,
                    StringComparison.Ordinal))
            {
                ConfirmRecommendedPreset();
                return true;
            }
            if (string.Equals(
                    command.CommandId,
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CancelRecommendedPreset,
                    StringComparison.Ordinal)
                || string.Equals(
                    command.CommandId,
                    MxFramework.UI.FairyGui.MxFairyGuiCommandIds.Cancel,
                    StringComparison.Ordinal))
            {
                CancelRecommendedPreset();
                return true;
            }

            // Preset preview is modal. Direct or bridged background commands are consumed.
            return true;
        }

        private void PreviewRecommendedPreset()
        {
            _feedbackController.ClearFeedback();
            InstallSelectionActive = false;
            DetailsStableCardId = string.Empty;
            DetailsPageIndex = 0;
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreviewResult result =
                _service != null
                    ? _service.TryPreviewRecommendedPreset()
                    : Workbench?.TryPreviewPreset(
                        CreateDefaultRecommendedPreset(),
                        CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault());
            if (result == null || !result.Success)
            {
                _presetPreview = null;
                LastStatusMessage = FirstDiagnosticMessage(
                    result?.Diagnostics,
                    L("command.preset_unavailable", "推荐预设不可用"));
                return;
            }

            _presetPreview = result.Preview;
            _presetExitFocusTargetId = string.Empty;
            LastStatusMessage = string.Empty;
        }

        private void ConfirmRecommendedPreset()
        {
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview preview = _presetPreview;
            if (preview == null)
                return;
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult result = _service != null
                ? _service.TryConfirmRecommendedPreset(preview)
                : Workbench.TryConfirmPreset(preview);
            if (result.Success)
            {
                _presetPreview = null;
                ReplacementSourceStableCardId = string.Empty;
                LastStatusMessage = L("command.preset_applied", "已应用推荐预设：新增 ")
                    + preview.Added.Count + " / "
                    + L("command.preset_removed_count", "移除 ") + preview.Removed.Count;
                RequestPresetExitFocus();
                return;
            }

            LastStatusMessage = FirstDiagnosticMessage(
                result.Diagnostics,
                L("command.preset_apply_failed", "推荐预设应用失败"));
            if (HasDiagnostic(result, "PLAYER_DECK_PRESET_PREVIEW_STALE")
                || HasDiagnostic(result, "PLAYER_DECK_SESSION_LOCKED"))
            {
                _presetPreview = null;
                RequestPresetExitFocus();
            }
        }

        private void CancelRecommendedPreset()
        {
            if (_presetPreview == null)
                return;
            _presetPreview = null;
            LastStatusMessage = string.Empty;
            RequestPresetExitFocus();
        }

        private void UndoRecommendedPreset()
        {
            _feedbackController.ClearFeedback();
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult result = _service != null
                ? _service.TryUndoRecommendedPreset()
                : Workbench?.TryUndoLastPreset();
            if (result != null && result.Success)
            {
                LastStatusMessage = L("command.preset_undone", "已撤销推荐预设");
                RequestPresetExitFocus();
                return;
            }
            LastStatusMessage = FirstDiagnosticMessage(
                result?.Diagnostics,
                L("command.preset_undo_failed", "无法撤销推荐预设"));
        }

        private void ClearPresetInteraction()
        {
            _presetPreview = null;
            _presetExitFocusTargetId = string.Empty;
        }

        private void RequestPresetExitFocus()
        {
            _presetExitFocusTargetId = ResolvePresetExitFocusTarget(
                CanPreviewRecommendedPreset,
                CanContinueToSetup,
                _closePanel != null);
            _presetExitFocusSequence++;
        }

        internal static string ResolvePresetExitFocusTarget(
            bool canPreview,
            bool canContinueToSetup,
            bool canClose)
        {
            if (canPreview)
                return CharacterTestFairyGuiBehaviorDeckPanelIds.PreviewRecommendedPreset;
            if (canContinueToSetup)
                return CharacterTestFairyGuiBehaviorDeckPanelIds.ContinueToSetup;
            if (canClose)
                return CharacterTestFairyGuiBehaviorDeckPanelIds.Close;
            return string.Empty;
        }

        private static bool HasDiagnostic(
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult result,
            string code)
        {
            for (int i = 0; result != null && i < result.Diagnostics.Count; i++)
            {
                if (string.Equals(result.Diagnostics[i].Code, code, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string FirstDiagnosticMessage(
            System.Collections.Generic.IReadOnlyList<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic> diagnostics,
            string fallback)
        {
            return diagnostics != null
                && diagnostics.Count > 0
                && !string.IsNullOrWhiteSpace(diagnostics[0].Message)
                    ? diagnostics[0].Message
                    : fallback;
        }

        private static CharacterTestRuntimeAiPlannerInstalledDeckPresetDefinition CreateDefaultRecommendedPreset()
        {
            return new CharacterTestRuntimeAiPlannerInstalledDeckPresetDefinition(
                1,
                1,
                new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog
                        .FoundationApproachAndAttack,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle
                });
        }
    }
}
