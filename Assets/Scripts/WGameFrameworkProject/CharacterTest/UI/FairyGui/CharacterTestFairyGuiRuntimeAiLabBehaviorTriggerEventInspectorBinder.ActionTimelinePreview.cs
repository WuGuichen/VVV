using System;
using MxFramework.CharacterAction;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder
    {
        private const float ActionPreviewLabelWidth = 132f;
        private const float ActionPreviewMinimumPixelsPerFrame = 4f;
        private const float ActionPreviewMaximumPixelsPerFrame = 18f;
        private const float ActionPreviewAvailableTimelineWidth = 744f;

        private Fgui.GComponent _characterActionTimelinePreview;
        private Fgui.GObject _characterActionTimelinePreviewAnchor;
        private bool _characterActionTimelinePreviewUsesPopupService;
        private CharacterActionTimelinePreviewViewModel _actionTimelinePreviewModel;

        private void ShowCharacterActionTimelinePreview()
        {
            CharacterActionTimelinePreviewViewModel preview =
                _model?.CharacterActionTimelinePreview;
            if (_model?.CharacterActionEvent != true
                || _model.CharacterActionReferenceValid != true
                || preview?.Available != true)
            {
                return;
            }

            _actionTimelinePreviewModel = preview;
            if (CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge.IsRegistered)
            {
                OpenSelectedCharacterActionAuthoring();
                return;
            }

            EnsureCharacterActionTimelinePreview();
            if (_characterActionTimelinePreview == null)
                return;
            Fgui.GObject anchor = _root?.GetChild("btnViewCharacterActionTimeline");
            bool open = _popupService?.IsOpen(_characterActionTimelinePreview) == true
                || _characterActionTimelinePreview.parent != null;
            if (open)
            {
                CloseCharacterActionTimelinePreview();
                return;
            }

            _characterActionTimelinePreviewAnchor = anchor;
            RefreshCharacterActionTimelinePreview();
            if (_popupService != null)
            {
                _characterActionTimelinePreviewUsesPopupService = _popupService.Show(
                    _viewId,
                    _characterActionTimelinePreview,
                    anchor,
                    new MxFairyGuiPopupOptions(
                        MxFairyGuiPopupPlacement.Auto,
                        safeMargin: 12f,
                        anchorGap: 4f));
            }
            if (!_characterActionTimelinePreviewUsesPopupService)
            {
                Fgui.GRoot.inst.TogglePopup(
                    _characterActionTimelinePreview,
                    anchor,
                    Fgui.PopupDirection.Auto,
                    false);
            }
        }

        private void EnsureCharacterActionTimelinePreview()
        {
            if (_characterActionTimelinePreview != null)
                return;
            _characterActionTimelinePreview = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                "RuntimeAiLabCharacterActionTimelinePreview")?.asCom;
            if (_characterActionTimelinePreview == null)
                return;

            Fgui.GButton close = _characterActionTimelinePreview.GetChild("btnClose")?.asButton;
            if (close != null)
                close.onClick.Set(CloseCharacterActionTimelinePreview);
            Fgui.GButton edit = _characterActionTimelinePreview.GetChild("btnEdit")?.asButton;
            if (edit != null)
                edit.onClick.Set(OpenSelectedCharacterActionAuthoring);
            Fgui.GButton retry = _characterActionTimelinePreview.GetChild("btnRetry")?.asButton;
            if (retry != null)
                retry.onClick.Set(RetrySelectedCharacterActionApply);
            Fgui.GList list = _characterActionTimelinePreview.GetChild("trackList")?.asList;
            if (list != null)
            {
                list.layout = Fgui.ListLayoutType.SingleColumn;
                list.selectionMode = Fgui.ListSelectionMode.None;
                list.lineGap = 3;
                list.itemRenderer = BindCharacterActionTimelinePreviewRow;
            }
            _characterActionTimelinePreview.onKeyDown.Set(context =>
            {
                if (context.inputEvent?.keyCode == KeyCode.Escape)
                {
                    context.StopPropagation();
                    CloseCharacterActionTimelinePreview();
                }
            });
        }

        private void RefreshCharacterActionTimelinePreview()
        {
            if (_characterActionTimelinePreview == null || _actionTimelinePreviewModel == null)
                return;
            CharacterActionTimelinePreviewViewModel model = _actionTimelinePreviewModel;
            SetActionPreviewText("title", model.DisplayName);
            SetActionPreviewText(
                "meta",
                model.ActionId + " · " + model.Category + " · "
                + model.DurationFrames + "f · " + model.TimelineAuthority);
            CharacterTestRuntimeAiLabCharacterActionAuthoringResult status =
                CharacterTestRuntimeAiLabCharacterActionAuthoringBridge.LastResult;
            string statusText = status.ActionId == model.ActionId && status.Message.Length > 0
                ? status.Message
                : CharacterTestRuntimeAiLabCharacterActionAuthoringBridge.IsRegistered
                    ? "当前显示运行目录；正式编辑将在 Character Action Authoring 中打开。"
                    : "当前显示运行目录；Player 仅支持只读预览。";
            SetActionPreviewText("status", statusText);
            Fgui.GButton edit = _characterActionTimelinePreview.GetChild("btnEdit")?.asButton;
            if (edit != null)
            {
                edit.visible = CharacterTestRuntimeAiLabCharacterActionAuthoringBridge.IsRegistered;
                edit.enabled = edit.visible;
                edit.title = "在动作编辑器中编辑";
            }
            Fgui.GButton retry = _characterActionTimelinePreview.GetChild("btnRetry")?.asButton;
            if (retry != null)
            {
                bool canRetry = status.ActionId == model.ActionId && status.CanRetry;
                retry.visible = canRetry;
                retry.enabled = canRetry;
                retry.title = "重试应用";
            }
            Fgui.GList list = _characterActionTimelinePreview.GetChild("trackList")?.asList;
            if (list != null)
                list.numItems = model.Rows.Length + 1;
        }

        private void BindCharacterActionTimelinePreviewRow(int index, Fgui.GObject itemObject)
        {
            Fgui.GComponent item = itemObject?.asCom;
            CharacterActionTimelinePreviewViewModel model = _actionTimelinePreviewModel;
            if (item == null || model == null)
                return;
            while (item.numChildren > 2)
                item.RemoveChildAt(2, true);
            float pixelsPerFrame = Mathf.Clamp(
                ActionPreviewAvailableTimelineWidth / Math.Max(1, model.DurationFrames),
                ActionPreviewMinimumPixelsPerFrame,
                ActionPreviewMaximumPixelsPerFrame);
            float contentWidth = ActionPreviewLabelWidth
                + Math.Max(ActionPreviewAvailableTimelineWidth, model.DurationFrames * pixelsPerFrame)
                + 28f;
            item.SetSize(contentWidth, index == 0 ? 38f : 48f);
            Fgui.GTextField label = item.GetChild("label")?.asTextField;
            if (label != null)
                label.text = index == 0 ? "帧" : model.Rows[index - 1].Label;

            if (index == 0)
            {
                BindActionPreviewRuler(item, model.DurationFrames, pixelsPerFrame);
                return;
            }

            CharacterActionTimelinePreviewRowViewModel row = model.Rows[index - 1];
            Color32 color = ResolveActionPreviewColor(row.Kind);
            for (int entryIndex = 0; entryIndex < row.Entries.Length; entryIndex++)
            {
                CharacterActionTimelinePreviewEntryViewModel entry = row.Entries[entryIndex];
                float x = ActionPreviewLabelWidth + Math.Max(0, entry.StartFrame) * pixelsPerFrame;
                float width = Math.Max(4f, (entry.EndFrame - entry.StartFrame + 1) * pixelsPerFrame);
                var graph = new Fgui.GGraph { name = "entry", touchable = true };
                graph.SetXY(x, 8f);
                graph.SetSize(width, 30f);
                graph.DrawRect(
                    width,
                    30f,
                    1,
                    new Color32(color.r, color.g, color.b, 255),
                    new Color32(color.r, color.g, color.b, 145));
                BindCharacterActionTooltip(graph, entry.Tooltip);
                item.AddChild(graph);
                if (width >= 54f)
                {
                    var text = new Fgui.GTextField
                    {
                        touchable = false,
                        singleLine = true,
                        text = entry.Label,
                        color = new Color32(230, 244, 247, 255),
                    };
                    text.SetXY(x + 5f, 10f);
                    text.SetSize(width - 10f, 26f);
                    Fgui.TextFormat format = text.textFormat;
                    format.size = 11;
                    format.color = text.color;
                    text.textFormat = format;
                    item.AddChild(text);
                }
            }
        }

        private static void BindActionPreviewRuler(
            Fgui.GComponent item,
            int durationFrames,
            float pixelsPerFrame)
        {
            int interval = durationFrames <= 60 ? 5 : durationFrames <= 180 ? 10 : 30;
            for (int frame = 0; frame <= durationFrames; frame += interval)
            {
                float x = ActionPreviewLabelWidth + frame * pixelsPerFrame;
                var tick = new Fgui.GGraph { touchable = false };
                tick.SetXY(x, 22f);
                tick.SetSize(1f, 12f);
                tick.DrawRect(1f, 12f, 0, Color.clear, new Color32(75, 130, 142, 180));
                item.AddChild(tick);
                var text = new Fgui.GTextField
                {
                    touchable = false,
                    singleLine = true,
                    text = frame.ToString(),
                    color = new Color32(143, 174, 184, 255),
                    align = Fgui.AlignType.Center,
                };
                text.SetXY(x - 20f, 1f);
                text.SetSize(40f, 20f);
                Fgui.TextFormat format = text.textFormat;
                format.size = 11;
                format.color = text.color;
                text.textFormat = format;
                item.AddChild(text);
            }
        }

        private static Color32 ResolveActionPreviewColor(CharacterActionWorkstationRowKind kind)
        {
            return kind switch
            {
                CharacterActionWorkstationRowKind.Phase => new Color32(92, 145, 209, 255),
                CharacterActionWorkstationRowKind.Motion => new Color32(70, 190, 143, 255),
                CharacterActionWorkstationRowKind.Combat => new Color32(229, 92, 92, 255),
                CharacterActionWorkstationRowKind.Gameplay => new Color32(75, 151, 229, 255),
                CharacterActionWorkstationRowKind.Animation => new Color32(142, 117, 218, 255),
                CharacterActionWorkstationRowKind.Presentation => new Color32(222, 162, 68, 255),
                CharacterActionWorkstationRowKind.Cancel => new Color32(212, 127, 61, 255),
                CharacterActionWorkstationRowKind.Interrupt => new Color32(195, 91, 156, 255),
                _ => new Color32(102, 143, 153, 255),
            };
        }

        private void OpenSelectedCharacterActionAuthoring()
        {
            if (_actionTimelinePreviewModel == null)
                return;
            Enqueue(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenCharacterActionAuthoring,
                BuildActionAuthoringPayload(_actionTimelinePreviewModel.ActionId));
            ShowEmbeddedCharacterActionWorkspace();
        }

        private void RetrySelectedCharacterActionApply()
        {
            if (_actionTimelinePreviewModel == null)
                return;
            Enqueue(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.RetryCharacterActionApply,
                BuildActionAuthoringPayload(_actionTimelinePreviewModel.ActionId));
        }

        private CharacterTestRuntimeAiLabCharacterActionAuthoringCommandPayload BuildActionAuthoringPayload(
            CharacterActionId actionId)
        {
            return new CharacterTestRuntimeAiLabCharacterActionAuthoringCommandPayload
            {
                ActionId = actionId,
                TimelineId = _model?.TimelineId ?? string.Empty,
                EventId = _model?.EventId ?? string.Empty,
            };
        }

        private void CloseCharacterActionTimelinePreview()
        {
            if (_characterActionTimelinePreview == null)
                return;
            _characterActionTimelinePreviewAnchor = null;
            _actionTimelinePreviewModel = null;
            if (_characterActionTimelinePreviewUsesPopupService && _popupService != null)
            {
                _popupService.Hide(
                    _characterActionTimelinePreview,
                    MxFairyGuiPopupCloseReason.Toggle);
            }
            else if (_characterActionTimelinePreview.parent != null)
            {
                Fgui.GRoot.inst.HidePopup(_characterActionTimelinePreview);
            }
            _characterActionTimelinePreviewUsesPopupService = false;
        }

        private void SetActionPreviewText(string childName, string value)
        {
            Fgui.GTextField text = _characterActionTimelinePreview?.GetChild(childName)?.asTextField;
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }
}
