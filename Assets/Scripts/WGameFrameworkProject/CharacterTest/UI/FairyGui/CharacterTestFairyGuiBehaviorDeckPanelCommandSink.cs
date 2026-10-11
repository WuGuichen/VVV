using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiBehaviorDeckPanelCommandSink : IMxUiCommandSink
    {
        private readonly CharacterTestRuntimeAiPlannerPlayerDeckWorkbench _fixedWorkbench;
        private CharacterTestRuntimeAiPlannerLocalPlayerDeckService _service;
        private readonly Func<bool> _closePanel;
        private readonly IMxUiTextProvider _textProvider;
        private readonly Func<string, bool> _openCardInLab;
        private readonly Func<bool> _continueToSetup;
        private readonly Func<int, string, bool> _updateShellContext;
        private readonly Func<bool> _hasActiveRuntimeAiLabSession;
        private readonly CharacterTestBehaviorDeckOperationFeedbackController _feedbackController;

        internal CharacterTestFairyGuiBehaviorDeckPanelCommandSink(
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench workbench,
            Func<bool> closePanel = null,
            IMxUiTextProvider textProvider = null,
            Func<string, bool> openCardInLab = null,
            Func<bool> continueToSetup = null,
            Func<int, string, bool> updateShellContext = null,
            Func<double> unscaledTimeProvider = null,
            Func<bool> hasActiveRuntimeAiLabSession = null)
        {
            _fixedWorkbench = workbench;
            _closePanel = closePanel;
            _textProvider = textProvider ?? MxUiNullTextProvider.Instance;
            _openCardInLab = openCardInLab;
            _continueToSetup = continueToSetup;
            _updateShellContext = updateShellContext;
            _hasActiveRuntimeAiLabSession = hasActiveRuntimeAiLabSession;
            _feedbackController = new CharacterTestBehaviorDeckOperationFeedbackController(
                unscaledTimeProvider ?? (() => UnityEngine.Time.unscaledTimeAsDouble));
        }

        internal CharacterTestFairyGuiBehaviorDeckPanelCommandSink(
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
            Func<bool> closePanel = null,
            IMxUiTextProvider textProvider = null,
            Func<string, bool> openCardInLab = null,
            Func<bool> continueToSetup = null,
            Func<int, string, bool> updateShellContext = null,
            Func<double> unscaledTimeProvider = null,
            Func<bool> hasActiveRuntimeAiLabSession = null)
        {
            _service = service;
            _closePanel = closePanel;
            _textProvider = textProvider ?? MxUiNullTextProvider.Instance;
            _openCardInLab = openCardInLab;
            _continueToSetup = continueToSetup;
            _updateShellContext = updateShellContext;
            _hasActiveRuntimeAiLabSession = hasActiveRuntimeAiLabSession;
            _feedbackController = new CharacterTestBehaviorDeckOperationFeedbackController(
                unscaledTimeProvider ?? (() => UnityEngine.Time.unscaledTimeAsDouble));
        }

        internal void UpdateService(CharacterTestRuntimeAiPlannerLocalPlayerDeckService service)
        {
            _service = service;
        }

        public int PageIndex { get; private set; }
        public string ReplacementSourceStableCardId { get; private set; } = string.Empty;
        public string DetailsStableCardId { get; private set; } = string.Empty;
        public int DetailsPageIndex { get; private set; }
        public bool InstallSelectionActive { get; private set; }
        public string LastStatusMessage { get; private set; } = string.Empty;
        public int CommandCount { get; private set; }
        public MxUiCommand LastCommand { get; private set; }
        internal CharacterTestRuntimeAiPlannerPlayerDeckWorkbench Workbench =>
            _service?.Workbench ?? _fixedWorkbench;
        internal CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView View => Workbench?.View;
        internal bool CanPreviousCharacter => _service?.CanSelectPreviousCharacter ?? false;
        internal bool CanNextCharacter => _service?.CanSelectNextCharacter ?? false;
        internal int SelectedCharacterIndex => _service?.SelectedCharacterIndex ?? 0;
        internal int CharacterCount => _service?.CharacterCount ?? 1;
        internal string SelectedCharacterDisplayName => _service?.SelectedCharacterDisplayName ?? string.Empty;
        internal bool CanPreviewRecommendedPreset => _service?.CanPreviewRecommendedPreset
            ?? (Workbench != null && !Workbench.View.IsSessionLocked);
        internal bool CanUndoRecommendedPreset => _service?.CanUndoRecommendedPreset
            ?? (Workbench?.CanUndoLastPreset ?? false);
        internal bool CanAuthorInLab => _openCardInLab != null;
        internal bool CanContinueToSetup => _continueToSetup != null;
        internal bool HasActiveRuntimeAiLabSession =>
            _hasActiveRuntimeAiLabSession != null && _hasActiveRuntimeAiLabSession();
        internal long FeedbackVersion => _feedbackController.FeedbackVersion;
        internal CharacterTestBehaviorDeckOperationFeedback OperationFeedback => _feedbackController.Feedback;
        internal CharacterTestBehaviorDeckFocusIntent FocusIntent => _feedbackController.FocusIntent;

        internal bool ReconcileOperationFeedback()
        {
            if (!_feedbackController.ReconcileAndExpire(Workbench, View))
                return false;
            LastStatusMessage = string.Empty;
            return true;
        }

        internal void ClearOperationState()
        {
            _feedbackController.ClearAll();
            DetailsStableCardId = string.Empty;
            DetailsPageIndex = 0;
            InstallSelectionActive = false;
            ClearPresetInteraction();
        }

        internal void RestoreShellContext(
            int pageIndex,
            string replacementSourceStableCardId)
        {
            PageIndex = Math.Max(0, pageIndex);
            ReplacementSourceStableCardId = replacementSourceStableCardId?.Trim() ?? string.Empty;
            DetailsStableCardId = string.Empty;
            DetailsPageIndex = 0;
            InstallSelectionActive = false;
            _feedbackController.ClearAll();
            ClearPresetInteraction();
        }

        public void Enqueue(MxUiCommand command)
        {
            CommandCount++;
            LastCommand = command;
            if (PresetPreview != null && HandlePresetModalCommand(command))
                return;
            if (!string.IsNullOrEmpty(DetailsStableCardId)
                && HandleDetailsModalCommand(command))
            {
                return;
            }
            switch (command.CommandId)
            {
                case CharacterTestFairyGuiBehaviorDeckPanelCommandIds.Close:
                    _feedbackController.ClearAll();
                    DetailsStableCardId = string.Empty;
                    InstallSelectionActive = false;
                    PublishShellContext();
                    _closePanel?.Invoke();
                    break;
                case MxFramework.UI.FairyGui.MxFairyGuiCommandIds.Cancel:
                    if (InstallSelectionActive)
                    {
                        InstallSelectionActive = false;
                        LastStatusMessage = string.Empty;
                        PublishShellContext();
                        break;
                    }
                    _feedbackController.ClearAll();
                    DetailsStableCardId = string.Empty;
                    PublishShellContext();
                    _closePanel?.Invoke();
                    break;
                case CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ToggleInstallSelection:
                    ToggleInstallSelection();
                    PublishShellContext();
                    break;
                case CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CloseDetails:
                    CloseDetails();
                    break;
                case CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviousPage:
                    if (PageIndex > 0)
                        PageIndex--;
                    LastStatusMessage = string.Empty;
                    PublishShellContext();
                    break;
                case CharacterTestFairyGuiBehaviorDeckPanelCommandIds.NextPage:
                    int maximumPageIndex = GetMaximumPageIndex();
                    if (PageIndex < maximumPageIndex)
                        PageIndex++;
                    LastStatusMessage = string.Empty;
                    PublishShellContext();
                    break;
                case CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction:
                    CharacterTestFairyGuiBehaviorDeckCardActionPayload cardAction =
                        command.Payload as CharacterTestFairyGuiBehaviorDeckCardActionPayload;
                    bool leavesPlayerDeck = cardAction?.Action ==
                        CharacterTestFairyGuiBehaviorDeckCardAction.EditInLab;
                    if (!leavesPlayerDeck)
                        _feedbackController.RecordFocusIntent(cardAction, PageIndex);
                    if (leavesPlayerDeck)
                        PublishShellContext();
                    ExecuteCardAction(cardAction);
                    if (!leavesPlayerDeck)
                        PublishShellContext();
                    break;
                case CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviousCharacter:
                    SelectCharacter(-1);
                    PublishShellContext();
                    break;
                case CharacterTestFairyGuiBehaviorDeckPanelCommandIds.NextCharacter:
                    SelectCharacter(1);
                    PublishShellContext();
                    break;
                case CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviewRecommendedPreset:
                    PreviewRecommendedPreset();
                    PublishShellContext();
                    break;
                case CharacterTestFairyGuiBehaviorDeckPanelCommandIds.UndoRecommendedPreset:
                    UndoRecommendedPreset();
                    PublishShellContext();
                    break;
                case CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ContinueToSetup:
                    bool resumeRuntimeAiLab = HasActiveRuntimeAiLabSession;
                    LastStatusMessage = _continueToSetup != null && _continueToSetup()
                        ? resumeRuntimeAiLab
                            ? L("command.runtime_debug_restored", "已返回调试")
                            : L("command.setup_opened", "已进入实验设置")
                        : resumeRuntimeAiLab
                            ? L("command.runtime_debug_unavailable", "当前测试无法恢复")
                            : L("command.setup_unavailable", "实验设置当前不可用");
                    break;
                default:
                    LastStatusMessage = L("command.unsupported", "不支持的卡组命令：") + command.CommandId;
                    break;
            }
        }

        private bool HandleDetailsModalCommand(MxUiCommand command)
        {
            if (string.Equals(
                    command.CommandId,
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CloseDetails,
                    StringComparison.Ordinal)
                || string.Equals(
                    command.CommandId,
                    MxFramework.UI.FairyGui.MxFairyGuiCommandIds.Cancel,
                    StringComparison.Ordinal))
            {
                CloseDetails();
                return true;
            }

            if (string.Equals(
                command.CommandId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.PreviousDetailsPage,
                StringComparison.Ordinal))
            {
                if (DetailsPageIndex > 0)
                    DetailsPageIndex--;
                LastStatusMessage = string.Empty;
                return true;
            }

            if (string.Equals(
                command.CommandId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.NextDetailsPage,
                StringComparison.Ordinal))
            {
                CharacterTestRuntimeAiPlannerPlayerDeckCardView card = FindCard(DetailsStableCardId);
                int pageCount = CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder.CountDetailsPages(
                    card,
                    _textProvider);
                if (DetailsPageIndex + 1 < pageCount)
                    DetailsPageIndex++;
                LastStatusMessage = string.Empty;
                return true;
            }

            // The details layer is modal. Direct or bridged background commands are consumed
            // without mutating the deck, navigation state, or shell.
            return true;
        }

        private void CloseDetails()
        {
            if (!string.IsNullOrEmpty(DetailsStableCardId))
            {
                _feedbackController.RecordFocusIntent(
                    new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                    {
                        StableCardId = DetailsStableCardId,
                        Action = CharacterTestFairyGuiBehaviorDeckCardAction.ShowDetails,
                        Origin = CharacterTestFairyGuiBehaviorDeckCardOrigin.Library,
                        ActionRole = CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary
                    },
                    PageIndex);
            }
            DetailsStableCardId = string.Empty;
            DetailsPageIndex = 0;
            LastStatusMessage = string.Empty;
        }

        private void PublishShellContext()
        {
            _updateShellContext?.Invoke(PageIndex, ReplacementSourceStableCardId);
        }

        private void SelectCharacter(int offset)
        {
            _feedbackController.ClearAll();
            DetailsStableCardId = string.Empty;
            DetailsPageIndex = 0;
            InstallSelectionActive = false;
            string error = string.Empty;
            if (_service == null
                || !_service.TrySelectCharacter(_service.SelectedCharacterIndex + offset, out error))
            {
                LastStatusMessage = string.IsNullOrEmpty(error)
                    ? L("command.character_unavailable", "角色不可用")
                    : error;
                return;
            }
            PageIndex = 0;
            ReplacementSourceStableCardId = string.Empty;
            LastStatusMessage = L("command.character_selected", "已切换角色卡组");
        }

        private void ExecuteCardAction(CharacterTestFairyGuiBehaviorDeckCardActionPayload payload)
        {
            string stableCardId = payload?.StableCardId?.Trim() ?? string.Empty;
            CharacterTestRuntimeAiPlannerPlayerDeckCardView card = FindCard(stableCardId);
            if (card == null)
            {
                LastStatusMessage = L("command.card_not_found", "找不到这张行为卡，请刷新卡库后重试");
                return;
            }

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult result;
            switch (payload.Action)
            {
                case CharacterTestFairyGuiBehaviorDeckCardAction.Select:
                    LastStatusMessage = string.Empty;
                    return;
                case CharacterTestFairyGuiBehaviorDeckCardAction.ShowDetails:
                    DetailsStableCardId = stableCardId;
                    DetailsPageIndex = 0;
                    LastStatusMessage = string.Empty;
                    return;
                case CharacterTestFairyGuiBehaviorDeckCardAction.EditInLab:
                    if (_openCardInLab == null)
                    {
                        LastStatusMessage = L("command.lab_unavailable", "Runtime AI Lab 编辑器不可用");
                        return;
                    }
                    if (card.Availability != CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Available)
                    {
                        LastStatusMessage = L("command.lab_card_unavailable", "此行为卡当前不能进入 Lab 编辑");
                        return;
                    }
                    LastStatusMessage = _openCardInLab(stableCardId)
                        ? L("command.lab_opened", "已在 Runtime AI Lab 中创建隔离草稿")
                        : L("command.lab_open_failed", "无法在 Runtime AI Lab 中打开此行为卡");
                    return;
                case CharacterTestFairyGuiBehaviorDeckCardAction.CancelReplacement:
                    _feedbackController.ClearFeedback();
                    ReplacementSourceStableCardId = string.Empty;
                    LastStatusMessage = L("command.replace_cancelled", "已取消替换");
                    return;
                case CharacterTestFairyGuiBehaviorDeckCardAction.BeginReplacement:
                    if (!card.CanReplace)
                    {
                        LastStatusMessage = L("command.cannot_replace", "此行为卡当前不可替换");
                        return;
                    }
                    _feedbackController.ClearFeedback();
                    InstallSelectionActive = false;
                    ReplacementSourceStableCardId = stableCardId;
                    LastStatusMessage = L("command.choose_replacement", "请选择用于替换的可用行为卡");
                    return;
                case CharacterTestFairyGuiBehaviorDeckCardAction.ChooseReplacement:
                    if (string.IsNullOrEmpty(ReplacementSourceStableCardId))
                    {
                        LastStatusMessage = L("command.choose_source", "请先选择要替换的行为卡");
                        return;
                    }
                    result = Workbench.TryReplace(ReplacementSourceStableCardId, stableCardId);
                    if (result.Success)
                        ReplacementSourceStableCardId = string.Empty;
                    SetResultStatus(result, L("command.replaced", "已替换行为卡"));
                    return;
                case CharacterTestFairyGuiBehaviorDeckCardAction.Remove:
                    CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView beforeRemove = Workbench.View;
                    result = Workbench.TryRemove(stableCardId);
                    if (result.Success
                        && string.Equals(
                            ReplacementSourceStableCardId,
                            stableCardId,
                            StringComparison.Ordinal))
                    {
                        ReplacementSourceStableCardId = string.Empty;
                    }
                    SetOperationResultStatus(
                        result,
                        payload,
                        CharacterTestBehaviorDeckOperationKind.Remove,
                        beforeRemove,
                        L("command.removed", "已移除行为卡"));
                    return;
                case CharacterTestFairyGuiBehaviorDeckCardAction.Install:
                    CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView beforeInstall = Workbench.View;
                    result = Workbench.TryInstall(stableCardId);
                    if (result.Success)
                        InstallSelectionActive = false;
                    SetOperationResultStatus(
                        result,
                        payload,
                        CharacterTestBehaviorDeckOperationKind.Install,
                        beforeInstall,
                        L("command.installed", "已安装行为卡"));
                    return;
                default:
                    LastStatusMessage = L("command.no_action", "此卡面没有可执行操作");
                    return;
            }
        }

        private void ToggleInstallSelection()
        {
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView view = View;
            if (view == null
                || view.IsSessionLocked
                || view.SlotCount >= view.MaximumSlots
                || !string.IsNullOrEmpty(ReplacementSourceStableCardId))
            {
                InstallSelectionActive = false;
                LastStatusMessage = L(
                    "command.install_selection_unavailable",
                    "当前不能添加行为卡");
                return;
            }

            InstallSelectionActive = !InstallSelectionActive;
            DetailsStableCardId = string.Empty;
            DetailsPageIndex = 0;
            LastStatusMessage = InstallSelectionActive
                ? L("command.choose_install_card", "请选择一张可安装的行为卡")
                : string.Empty;
        }

        private int GetMaximumPageIndex()
        {
            int cardCount = Workbench?.View?.Cards.Count ?? 0;
            return cardCount > 0
                ? (cardCount - 1) / CharacterTestFairyGuiBehaviorDeckPanelIds.LibraryCardCount
                : 0;
        }

        private CharacterTestRuntimeAiPlannerPlayerDeckCardView FindCard(string stableCardId)
        {
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView view = Workbench?.View;
            for (int i = 0; view != null && i < view.Cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPlayerDeckCardView card = view.Cards[i];
                if (string.Equals(card.StableCardId, stableCardId, StringComparison.Ordinal))
                    return card;
            }
            return null;
        }

        private void SetResultStatus(
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult result,
            string successMessage)
        {
            if (result != null && result.Success)
            {
                LastStatusMessage = successMessage;
                return;
            }
            if (result != null && result.Diagnostics.Count > 0)
            {
                LastStatusMessage = PlayerFacingCommandDiagnostic(result.Diagnostics[0]);
                return;
            }
            LastStatusMessage = L("command.failed", "卡组操作失败");
        }

        private void SetOperationResultStatus(
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult result,
            CharacterTestFairyGuiBehaviorDeckCardActionPayload payload,
            CharacterTestBehaviorDeckOperationKind kind,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView before,
            string successMessage)
        {
            SetResultStatus(result, successMessage);
            _feedbackController.RecordOperation(
                Workbench,
                before,
                result,
                payload,
                kind,
                LastStatusMessage);
        }

        private string PlayerFacingCommandDiagnostic(
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic diagnostic)
        {
            switch (diagnostic.Code)
            {
                case "PLAYER_DECK_SESSION_LOCKED":
                    return L("command.session_locked", "战斗会话已锁定，当前不能修改卡组");
                case "PLAYER_DECK_CARD_NOT_UNLOCKED":
                case "PLAYER_DECK_PRESET_CARD_NOT_UNLOCKED":
                case "PLAYER_DECK_REPLACEMENT_NOT_UNLOCKED":
                case "PLAYER_DECK_INSTALLED_CARD_NOT_UNLOCKED":
                    return L("command.card_not_unlocked", "这张行为卡尚未解锁");
                case "PLAYER_DECK_CARD_ALREADY_INSTALLED":
                case "PLAYER_DECK_REPLACEMENT_DUPLICATE":
                    return L("command.card_already_installed", "这张行为卡已经在卡组中");
                case "PLAYER_DECK_CARD_NOT_INSTALLED":
                    return L("command.card_not_installed", "这张行为卡已不在当前卡组中");
                case "PLAYER_DECK_CARD_UNAVAILABLE":
                case "PLAYER_DECK_CARD_INCOMPATIBLE":
                case "INSTALLED_DECK_CARD_UNAVAILABLE":
                    return L("command.card_unavailable", "这张行为卡当前不可用，请替换或移除");
                case "INSTALLED_DECK_SLOT_LIMIT_EXCEEDED":
                    return L("command.slot_limit", "行为卡槽位已满");
                case "INSTALLED_DECK_COST_LIMIT_EXCEEDED":
                    return L("command.cost_limit", "安装后会超过卡组费用上限");
                case "PLAYER_DECK_PERSISTENCE_FAILED":
                case "PLAYER_DECK_PERSISTENCE_WRITE_FAILED":
                case "PLAYER_DECK_PERSISTENCE_RECOVERY_REQUIRED":
                case "PLAYER_DECK_PERSISTENCE_RESTORE_FAILED":
                    return L("command.save_failed", "卡组保存失败，请稍后重试");
                default:
                    return L("command.failed", "卡组操作失败");
            }
        }

        private string L(string keySuffix, string fallback) =>
            CharacterTestUiText.Resolve(
                _textProvider,
                "ui.character_test.behavior_deck." + keySuffix,
                fallback);
    }
}
