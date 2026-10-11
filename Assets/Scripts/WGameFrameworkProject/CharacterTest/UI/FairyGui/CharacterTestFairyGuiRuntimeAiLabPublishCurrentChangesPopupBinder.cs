using System;
using System.Collections.Generic;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Binds the Composer-owned aggregate-publish popup and delegates popup lifecycle/input
    /// ownership to <see cref="IMxFairyGuiPopupService"/>. It never creates a replacement
    /// FairyGUI component tree at runtime.
    /// </summary>
    internal sealed class CharacterTestFairyGuiRuntimeAiLabPublishCurrentChangesPopupBinder :
        IDisposable
    {
        internal const string PopupComponentName = "RuntimeAiLabPublishCurrentChangesPopup";
        internal const string AnchorControlName = "btnPublishCurrentChanges";
        internal const string TitleControlName = "publishPopupTitle";
        internal const string MessageControlName = "publishPopupMessage";
        internal const string SummaryControlName = "publishPopupSummary";
        internal const string DetailsPanelControlName = "publishPopupDetailsPanel";
        internal const string DetailsTextControlName = "publishPopupDetailsText";
        internal const string PrimaryButtonControlName = "btnPublishPopupPrimary";
        internal const string SecondaryButtonControlName = "btnPublishPopupSecondary";
        internal const string DetailsButtonControlName = "btnPublishPopupDetails";
        internal const string CloseButtonControlName = "btnPublishPopupClose";

        private static readonly string[] RequiredSemanticChildren =
        {
            TitleControlName,
            MessageControlName,
            SummaryControlName,
            DetailsPanelControlName,
            DetailsTextControlName,
            PrimaryButtonControlName,
            SecondaryButtonControlName,
            DetailsButtonControlName,
            CloseButtonControlName
        };

        private readonly IMxUiCommandSink _commandSink;
        private readonly MxUiViewId _ownerViewId;
        private readonly IMxFairyGuiPopupService _popupService;
        private readonly CharacterTestRuntimeAiLabPublishCurrentChangesPopupCoordinator
            _coordinator;
        private Fgui.GComponent _popup;
        private Fgui.GObject _anchor;
        private bool _suppressCloseCommand;
        private bool _disposed;

        internal CharacterTestFairyGuiRuntimeAiLabPublishCurrentChangesPopupBinder(
            IMxUiCommandSink commandSink,
            MxUiViewId ownerViewId,
            IMxFairyGuiPopupService popupService,
            CharacterTestRuntimeAiLabPublishCurrentChangesPopupCoordinator coordinator)
        {
            _commandSink = commandSink;
            _ownerViewId = ownerViewId;
            _popupService = popupService;
            _coordinator = coordinator;
            if (_coordinator != null)
                _coordinator.Changed += OnPresentationChanged;
        }

        internal string LastDiagnostic { get; private set; } = string.Empty;

        internal static IReadOnlyList<string> RequiredControls => RequiredSemanticChildren;

        internal bool TryAttach(
            Fgui.GComponent popup,
            Fgui.GObject anchor,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (_disposed)
            {
                diagnostic = "发布弹层 Binder 已释放。";
                LastDiagnostic = diagnostic;
                return false;
            }
            if (popup == null || anchor == null)
            {
                diagnostic = "缺少发布弹层组件或 btnPublishCurrentChanges 锚点。";
                LastDiagnostic = diagnostic;
                return false;
            }

            var missing = new List<string>();
            for (int i = 0; i < RequiredSemanticChildren.Length; i++)
            {
                if (popup.GetChild(RequiredSemanticChildren[i]) == null)
                    missing.Add(RequiredSemanticChildren[i]);
            }
            if (missing.Count > 0)
            {
                diagnostic = "发布弹层缺少 Composer 语义节点："
                    + string.Join(", ", missing);
                LastDiagnostic = diagnostic;
                return false;
            }
            if (popup.GetChild(TitleControlName)?.asTextField == null
                || popup.GetChild(MessageControlName)?.asTextField == null
                || popup.GetChild(SummaryControlName)?.asTextField == null
                || popup.GetChild(DetailsTextControlName)?.asTextField == null
                || popup.GetChild(PrimaryButtonControlName)?.asButton == null
                || popup.GetChild(SecondaryButtonControlName)?.asButton == null
                || popup.GetChild(DetailsButtonControlName)?.asButton == null
                || popup.GetChild(CloseButtonControlName)?.asButton == null)
            {
                diagnostic = "发布弹层 Composer 节点类型与契约不一致。";
                LastDiagnostic = diagnostic;
                return false;
            }

            HidePopup(MxFairyGuiPopupCloseReason.Replaced);
            _popup = popup;
            _popup.name = PopupComponentName;
            _popup.opaque = true;
            _popup.touchable = true;
            _popup.onClick.Set(context => context.StopPropagation());
            _popup.displayObject.onMouseWheel.Set(context => context.StopPropagation());
            _anchor = anchor;
            LastDiagnostic = string.Empty;
            OnPresentationChanged(_coordinator?.Current);
            return true;
        }

        internal void Detach()
        {
            HidePopup(MxFairyGuiPopupCloseReason.OwnerClosed);
            _popup = null;
            _anchor = null;
        }

        private void OnPresentationChanged(
            CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel viewModel)
        {
            if (_disposed || _popup == null || _anchor == null)
                return;
            if (viewModel?.Visible != true)
            {
                HidePopup(MxFairyGuiPopupCloseReason.OwnerClosed);
                return;
            }

            BindText(TitleControlName, viewModel.Title, visible: true);
            BindText(MessageControlName, viewModel.Message, visible: true);
            BindText(
                SummaryControlName,
                viewModel.Summary,
                visible: viewModel.Summary.Length > 0);
            Fgui.GObject detailsPanel = _popup.GetChild(DetailsPanelControlName);
            if (detailsPanel != null)
                detailsPanel.visible = viewModel.DetailsExpanded && viewModel.HasDetails;
            BindText(
                DetailsTextControlName,
                viewModel.Details,
                visible: viewModel.DetailsExpanded && viewModel.HasDetails);
            BindAction(PrimaryButtonControlName, viewModel.Primary);
            BindAction(SecondaryButtonControlName, viewModel.Secondary);
            BindAction(DetailsButtonControlName, viewModel.DetailsAction);
            BindClose(viewModel.ClosePayload);
            _popup.visible = true;

            if (_popupService == null)
            {
                LastDiagnostic = "MxFairyGuiPopupService 未注册，发布弹层无法打开。";
                return;
            }
            if (_popupService.IsOpen(_popup))
                return;
            bool shown = _popupService.Show(
                _ownerViewId,
                _popup,
                _anchor,
                new MxFairyGuiPopupOptions(
                    MxFairyGuiPopupPlacement.Auto,
                    safeMargin: 24f,
                    anchorGap: 12f),
                OnPopupClosed);
            LastDiagnostic = shown
                ? string.Empty
                : "MxFairyGuiPopupService 拒绝打开发布弹层。";
        }

        private void BindText(string childName, string value, bool visible)
        {
            Fgui.GTextField text = _popup.GetChild(childName)?.asTextField;
            if (text == null)
                return;
            text.text = value ?? string.Empty;
            text.visible = visible;
        }

        private void BindAction(
            string childName,
            CharacterTestRuntimeAiLabPublishCurrentChangesPopupAction action)
        {
            Fgui.GButton button = _popup.GetChild(childName)?.asButton;
            if (button == null)
                return;
            button.visible = action.Visible;
            button.text = action.Label ?? string.Empty;
            button.enabled = action.Enabled;
            button.grayed = !action.Enabled;
            button.onClick.Clear();
            if (!action.Enabled)
                return;
            CharacterTestRuntimeAiLabPublishCurrentChangesCommandPayload payload = action.Payload;
            button.onClick.Set(() => Enqueue(payload));
        }

        private void BindClose(
            CharacterTestRuntimeAiLabPublishCurrentChangesCommandPayload payload)
        {
            Fgui.GButton button = _popup.GetChild(CloseButtonControlName)?.asButton;
            if (button == null)
                return;
            button.visible = true;
            button.text = "关闭";
            button.enabled = payload != null;
            button.grayed = payload == null;
            button.onClick.Clear();
            if (payload != null)
                button.onClick.Set(() => Enqueue(payload));
        }

        private void Enqueue(CharacterTestRuntimeAiLabPublishCurrentChangesCommandPayload payload)
        {
            if (payload == null)
                return;
            _commandSink?.Enqueue(new MxUiCommand(
                _ownerViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PublishCurrentChanges,
                payload));
        }

        private void OnPopupClosed(MxFairyGuiPopupCloseReason reason)
        {
            if (_suppressCloseCommand || _disposed)
                return;
            CharacterTestRuntimeAiLabPublishCurrentChangesCommandPayload payload =
                _coordinator?.Current?.ClosePayload;
            if (payload != null)
                Enqueue(payload);
        }

        private void HidePopup(MxFairyGuiPopupCloseReason reason)
        {
            if (_popup != null) _popup.visible = false;
            if (_popup == null || _popupService?.IsOpen(_popup) != true)
                return;
            _suppressCloseCommand = true;
            try
            {
                _popupService.Hide(_popup, reason);
            }
            finally
            {
                _suppressCloseCommand = false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_coordinator != null)
                _coordinator.Changed -= OnPresentationChanged;
            HidePopup(MxFairyGuiPopupCloseReason.OwnerClosed);
            _popup = null;
            _anchor = null;
        }
    }
}
