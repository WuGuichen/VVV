using System;
using System.Collections.Generic;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Read-only card presentation with temporary action bindings.</summary>
    internal sealed class CharacterTestFairyGuiCardUiPreview : IDisposable
    {
        private readonly Fgui.GComponent _owner, _popup, _root, _card;
        private readonly CharacterTestFairyGuiTacticalDescriptionHints _descriptionHints;
        private readonly CharacterTestFairyGuiTacticalCardSkin _skin = new();
        private readonly IMxFairyGuiPopupService _popups;
        private readonly MxUiViewId _view;
        private readonly int _descriptionFontSize;
        private readonly Dictionary<string, int> _previewActions = new(StringComparer.Ordinal);
        private readonly CharacterTestFairyGuiTacticalActionPicker _picker;
        private CharacterTestCardUiPreviewData _data;
        private CharacterTestRuntimeAiPlannerDevSceneController _controller;
        private int _slot;
        private bool _disposed, _cardHovered;
        private string _role, _previewIssue;
        private long _summaryRevision, _summaryStatusRevision;
        internal bool IsDisposed => _disposed;

        internal CharacterTestFairyGuiCardUiPreview(Fgui.GComponent owner, IMxFairyGuiPopupService popups, MxUiViewId view)
        {
            _owner = owner; _popups = popups; _view = view;
            _picker = new CharacterTestFairyGuiTacticalActionPicker(view, popups);
            _popup = owner.GetChild("tacticalCardUiPreview")?.asCom
                ?? Fgui.UIPackage.CreateObject(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    "RuntimeAiLabCardUiPreviewOverlay")?.asCom
                ?? throw new InvalidOperationException("卡面预览组件尚未加载，请重新打开 AI Lab 使用最新发布包。");
            _root = _popup.GetChild("panel").asCom;
            _card = _root.GetChild("card").asCom;
            CharacterTestFairyGuiPlayerCardPresentation.Initialize(_card);
            _card.SetPivot(0.5f, 0.5f);
            _descriptionFontSize = _card.GetChild("description").asTextField.textFormat.size;
            _root.opaque = true;
            _root.onClick.Add(context => context.StopPropagation());
            _popup.GetChild("scrim").onClick.Set(context => { context.StopPropagation(); Close(); });
            foreach (var child in _card.GetChildren()) child.touchable = false;
            _descriptionHints = new CharacterTestFairyGuiTacticalDescriptionHints(_card, _root, ChooseAction);
            _card.opaque = true;
            _card.onRollOver.Add(OnCardRollOver);
            _card.onRollOut.Add(OnCardRollOut);
            _owner.onRemovedFromStage.Add(OnOwnerRemoved);
            WireButton("btnClose", Close);
            WireButton("btnCurrent", () => { _previewActions.Clear(); Reload(); });
            WireButton("btnUnbound", () => {
                foreach (var action in _data.Actions) _previewActions[action.Path] = 0;
                Reload();
            });
            WireButton("btnReload", Reload);
            WireButton("btnCopyDescription", () => GUIUtility.systemCopyBuffer = _data?.Description ?? string.Empty);
            WireButton("btnPreviousSlot", () => { _slot--; Render(); });
            WireButton("btnNextSlot", () => { _slot++; Render(); });
            WireButton("btnChooseAction", ChooseAction);
            WireButton("btnClearAction", () => {
                if (_data.Actions.Length == 0) return;
                _previewActions[_data.Actions[_slot].Path] = 0; Reload();
            });
            _popup.onKeyDown.Add(context => {
                if (context.inputEvent?.keyCode != KeyCode.Escape) return;
                Close(); context.StopPropagation();
            });
            var details = _root.GetChild("details").asList;
            details.autoResizeItem = false;
            details.itemRenderer = (_, item) => {
                var field = item.asCom.GetChild("body").asTextField;
                field.width = details.width - 22;
                field.autoSize = Fgui.AutoSizeType.Height;
                field.text = DetailsText();
                item.SetSize(details.width - 22, Math.Max(100, field.textHeight + 14));
            };
        }

        internal void Show(CharacterTestCardUiPreviewData data, string role, CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CharacterTestFairyGuiCardUiPreview));
            _picker.Close();
            _data = data; _role = role; _controller = controller;
            _previewActions.Clear(); _slot = 0; _previewIssue = string.Empty;
            BindCard();
            _popup.visible = true; _root.visible = true;
            if (_popups?.Show(_view, _popup, _owner, new MxFairyGuiPopupOptions(safeMargin: 12f)) != true)
                Fgui.GRoot.inst.ShowPopup(_popup);
            Layout(); Reload(); _root.RequestFocus();
        }

        private void BindCard()
        {
            _root.GetChild("context").text = (_data.Dirty ? "当前工作副本 · 未保存" : "当前已保存卡片") + " · " + _data.Name;
            CharacterTestFairyGuiPlayerCardPresentation.Bind(_card, _data, _descriptionFontSize);
            _skin.Bind(_card, _data);
            BindHint();
        }

        private void BindHint() => _descriptionHints.Bind(_data, _controller.CardDescriptionTextProvider, _descriptionFontSize);

        private string L(string key, string fallback) => CharacterTestUiText.Resolve(
            _controller?.CardDescriptionTextProvider, "ui.character_test.tactical.description." + key, fallback);

        private void OnCardRollOver() => SetCardHovered(true);
        private void OnCardRollOut() => SetCardHovered(false);

        private void SetCardHovered(bool hovered)
        {
            if (_card.isDisposed) return;
            _cardHovered = hovered;
            CharacterTestFairyGuiPlayerCardPresentation.SetHovered(_card, hovered);
        }

        private void HideHintDetails()
        {
            _descriptionHints.HideDetails();
            SetCardHovered(false);
        }

        private void Reload()
        {
            if (_disposed || _controller == null || _role != _controller.TestCardRole) { Close(); return; }
            if (_controller.TryGetConfiguredCardUiPreview(_data.Id, _previewActions, out var data, out var issue,
                automaticallyPrepareSummaries: true))
            { _data = data; _previewIssue = string.Empty; BindCard(); }
            else _previewIssue = issue;
            _summaryRevision = CharacterTestTacticalPreparationCatalog.Revision;
            _summaryStatusRevision = _controller.CardUiSummaryRevision;
            Render();
        }

        private void ChooseAction()
        {
            if (_data.Actions.Length == 0) return;
            ChooseAction(_data.Actions[_slot].Path, _root.GetChild("btnChooseAction"));
        }

        private void ChooseAction(string path, Fgui.GObject anchor)
        {
            var action = Array.Find(_data.Actions, item => item.Path == path);
            if (action == null) return;
            HideHintDetails();
            string token = _data.Token;
            _picker.Show(_controller, action.ActionId, anchor, false,
                () => !_disposed && _popup.visible && _controller.TestCardRole == _role && _data.Token == token,
                selected => { _previewActions[action.Path] = CharacterTestRuntimeAiPlannerDevSceneController.ResolveAuthoringActionId(selected); Reload(); });
        }

        internal void Guard(bool libraryVisible, string role)
        {
            if (_disposed || !_popup.visible) return;
            if (!libraryVisible || role != _role) Close();
            else
            {
                _skin.Tick();
                if (_data.TextSignature != CharacterTestUiTextProviderSignature.Create(_controller.CardDescriptionTextProvider)
                    || _data.FrameSeconds != _controller.CardDescriptionFrameSeconds
                    || _summaryRevision != CharacterTestTacticalPreparationCatalog.Revision
                    || _summaryStatusRevision != _controller.CardUiSummaryRevision) Reload();
                if (_popup.width != Fgui.GRoot.inst.width || _popup.height != Fgui.GRoot.inst.height) Layout();
            }
        }

        private void Layout()
        {
            var stage = Fgui.GRoot.inst;
            _popup.SetSize(stage.width, stage.height); _popup.SetXY(0, 0);
            _popup.GetChild("scrim").SetSize(stage.width, stage.height);
            float scale = Mathf.Clamp(Mathf.Min((stage.width-24)/_root.width, (stage.height-24)/_root.height), 0.1f, 1f);
            _root.SetScale(scale, scale);
            _root.SetXY((stage.width-_root.width*scale)/2, (stage.height-_root.height*scale)/2);
            _descriptionHints.RefreshPlacement();
        }

        internal void Close()
        {
            _controller?.CancelCardUiSummaries();
            HideHintDetails();
            _picker.Close();
            if (_popup == null || _popup.isDisposed) return;
            _popups?.Hide(_popup, MxFairyGuiPopupCloseReason.Cancel);
            if (_popup.parent != null) Fgui.GRoot.inst.HidePopup(_popup);
            _popup.visible = false;
        }

        private void Render()
        {
            if (_disposed || _data == null) return;
            _card.GetChild("focus").visible = _cardHovered;
            _card.GetChild("utilityEngraving").visible = false;
            _card.GetChild("qualityCircle").visible = false;
            _card.GetChild("qualityDiamond").asImage.color = new Color32(133, 135, 130, 255);
            int count = _data.Actions.Length;
            _slot = count == 0 ? 0 : (_slot % count + count) % count;
            _root.GetChild("slotLabel").text = count == 0 ? "此卡没有可替换的动作槽"
                : "动作 " + (_slot + 1) + "/" + count + " · " + _data.Actions[_slot].Label;
            _root.GetChild("slotLabel").asTextField.autoSize = Fgui.AutoSizeType.Shrink;
            _root.GetChild("slotLabel").tooltips = _root.GetChild("slotLabel").text;
            _root.GetChild("btnChooseAction").asButton.title = count == 0 ? "无动作可装配" : _data.Actions[_slot].ActionName + " · 更换";
            _root.GetChild("btnChooseAction").tooltips = count == 0 ? string.Empty : _data.Actions[_slot].ActionName;
            _root.GetChild("btnPreviousSlot").enabled = _root.GetChild("btnNextSlot").enabled = count > 1;
            _root.GetChild("btnChooseAction").enabled = _root.GetChild("btnClearAction").enabled = count > 0;
            _root.GetChild("btnUnbound").enabled = count > 0;
            _root.GetChild("bindingState").text = !string.IsNullOrEmpty(_previewIssue) ? "刷新失败：" + _previewIssue
                : (_previewActions.Count == 0 ? "当前装配" : "临时预览装配") + " · " + (_data.Prepared ? "绑定准备通过" : "装配未就绪，查看下方原因");
            var list = _root.GetChild("details").asList;
            list.numItems = 0; list.numItems = 1;
        }

        private string DetailsText()
            => L("bound_description", "装配后的说明") + "\n" + _data.Description
                + "\n\n" + L("unbound_description", "未装配时的说明") + "\n" + _data.UnboundDescription + "\n\n绑定结果与诊断\n"
                + (_data.Prepared ? "当前绑定静态准备通过；实际使用条件仍在出牌时检查。" : "当前绑定尚不可执行，保留可靠的源说明。")
                + "\n" + _data.Notice;

        internal static void FitDescription(Fgui.GTextField field, string value, int preferredFontSize)
        {
            string full = value ?? string.Empty;
            field.autoSize = Fgui.AutoSizeType.Height;
            float limit = 0;
            // Preserve the four-line card contract while keeping real values and long action names visible.
            for (int size = preferredFontSize; size >= Math.Max(18, preferredFontSize - 4); size--)
            {
                var format = field.textFormat; format.size = size; field.textFormat = format;
                field.text = "行\n行\n行\n行";
                limit = field.textHeight + 1;
                field.text = full;
                if (field.textHeight <= limit) break;
            }
            if (field.textHeight > limit)
            {
                int low = 0, high = full.Length;
                while (low < high)
                {
                    int count = (low + high + 1) / 2;
                    field.text = full.Substring(0, count) + "…";
                    if (field.textHeight <= limit) low = count; else high = count - 1;
                }
                if (low > 0 && char.IsHighSurrogate(full[low-1])) low--;
                field.text = full.Substring(0, low).TrimEnd() + "…";
            }
            field.autoSize = Fgui.AutoSizeType.None; field.height = limit; field.tooltips = full;
        }

        private void WireButton(string name, Action click)
            => WireButton(_root.GetChild(name).asButton, click);

        private void WireButton(Fgui.GButton button, Action click)
        {
            void Theme(string image, Color32 color)
            {
                button.GetChild("base").asImage.texture = _root.GetChild(image).asImage.texture;
                button.titleColor = color;
            }
            button.GetChild("overOverlay").alpha = 0; button.GetChild("downOverlay").alpha = 0;
            button.onRollOver.Set(() => Theme("hoverBase", new Color32(34, 44, 42, 255)));
            button.onRollOut.Set(() => Theme("normalBase", new Color32(225, 223, 211, 255)));
            button.onTouchBegin.Set(() => Theme("pressedBase", new Color32(34, 44, 42, 255)));
            button.onTouchEnd.Set(() => Theme("hoverBase", new Color32(34, 44, 42, 255)));
            button.onClick.Set(context => { context.StopPropagation(); click(); });
        }

        private void OnOwnerRemoved() => Dispose();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _owner.onRemovedFromStage.Remove(OnOwnerRemoved); Close();
            _card.onRollOver.Remove(OnCardRollOver); _card.onRollOut.Remove(OnCardRollOut);
            _descriptionHints.Dispose();
            _skin.Dispose();
            if (!_popup.isDisposed) _popup.Dispose();
            _data = null; _controller = null; _previewActions.Clear();
        }
    }
    /// <summary>One presentation for the existing AI Lab preview and preparation cards.</summary>
    internal static class CharacterTestFairyGuiPlayerCardPresentation
    {
        internal const float NativeWidth = 378, NativeHeight = 528;
        internal const float FrameWidth = 394, FrameHeight = 594, FramePadding = 8;
        internal static void SetHovered(Fgui.GComponent card, bool hovered)
        {
            card.GetChild("focus").visible = hovered;
            float scale = hovered ? 1.03f : 1f;
            card.SetScale(scale, scale);
        }

        internal const float Width = 378f, Height = 528f;
        internal static void Initialize(Fgui.GComponent card)
        {
            card.SetSize(Width, Height);
            var skinMask = card.GetChild("skinMask").asGraph;
            skinMask.DrawRoundRect(Width, Height, Color.white, new[] { 8f, 8f, 8f, 8f });
            card.mask = skinMask.displayObject;
            var focus = card.GetChild("focus").asGraph;
            focus.touchable = false;
            focus.shape.DrawRoundRect(2, focus.color, Color.clear, 8, 8, 8, 8);
            focus.visible = false;
            var ink = new Color32(32, 43, 44, 255);
            card.GetChild("costPlate").asGraph.shape.DrawEllipse(1, ink, new Color32(225, 223, 211, 255), ink, 0, 360);
        }
        internal static void Bind(Fgui.GComponent card, CharacterTestCardUiPreviewData data, int descriptionFontSize)
        {
            card.GetChild("cost").text = data.Cost >= 0 ? data.Cost.ToString(System.Globalization.CultureInfo.InvariantCulture) : "—";
            card.GetChild("name").text = data.Name;
            card.GetChild("name").asTextField.autoSize = Fgui.AutoSizeType.Shrink;
            card.GetChild("name").tooltips = data.Name;
            card.GetChild("icon").visible = false;
            bool generic = string.IsNullOrWhiteSpace(data.IconKey) || data.IconKey == "ui.behavior-card.custom"
                || data.IconKey == CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter.FallbackIconKey;
            bool swordArtwork = data.HasSwordArtwork && generic;
            card.GetChild("swordIllustration").visible = swordArtwork;
            CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter.Bind(card, data.IconKey, 133, 94, 112,
                new Color32(62, 83, 71, 255), !swordArtwork);
            card.GetChild("qualityCircle").visible = false;
            card.GetChild("qualityDiamond").visible = true;
            card.GetChild("qualityDiamond").asImage.color = new Color32(133, 135, 130, 255);
            card.GetChild("actionEngraving").visible = card.GetChild("utilityEngraving").visible = false;
            CharacterTestFairyGuiCardUiPreview.FitDescription(card.GetChild("description").asTextField,
                data.UtilityFace?.CardText ?? data.Description, descriptionFontSize);
        }
    }

}
