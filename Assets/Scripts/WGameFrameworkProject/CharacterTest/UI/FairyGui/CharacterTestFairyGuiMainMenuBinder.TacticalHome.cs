using System.Collections.Generic;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiMainMenuBinder
    {
        private enum HomepageChoice { None, NewProgress, Exit }
        private Fgui.GComponent _homepage;
        private Fgui.GComponent _homepageRoot;
        private Fgui.GComponent _homepageConfirmation;
        private CharacterTestFairyGuiMainMenuViewModel _homepageModel;
        private HomepageChoice _homepageChoice;
        private MxFairyGuiFocusNavigationMetadata _homepageFocus;
        private MxFairyGuiFocusNavigationMetadata _confirmationFocus;
        private readonly HashSet<Fgui.GButton> _homepageStyledButtons = new();
        private float _homepageNextKeyboardSubmit;
        internal Fgui.GComponent HomeSurface=>_homepage;

        private bool BindTacticalHomepage(Fgui.GComponent root,
            CharacterTestFairyGuiMainMenuViewModel model, float width, float height)
        {
            Fgui.GComponent surface = root.GetChild("tacticalHome")?.asCom;
            bool home = model?.Status?.IsTacticalSlice == true
                && (TacticalCard.TacticalPreparationState)model.Status.TacticalPhase == TacticalCard.TacticalPreparationState.Home;
            if (surface == null) return false;
            for (int i = 0; i < root.numChildren; i++)
                root.GetChildAt(i).visible = root.GetChildAt(i) == surface ? home : !home;
            if (!home)
            {
                _homepageChoice = HomepageChoice.None;
                if (_commandSink is CharacterTestFairyGuiMainMenuCommandSink inactiveSink)
                    inactiveSink.SetHomepageConfirmationDismissal(null);
                return false;
            }

            if (_homepage != surface)
            {
                _homepageStyledButtons.Clear();
                _homepageFeedback.Clear();
                _homepageKeyboardFocus = false;
                _homepage = surface;
                surface.onKeyDown.Add(OnHomepageKeyDown);
                _homepageConfirmation = surface.GetChild("confirmation")?.asCom;
                StyleHomepageDialogDecorations(_homepageConfirmation);
                _homepageChoice = HomepageChoice.None;
            }
            _homepageModel = model;
            _homepageRoot = root;
            float scale = Mathf.Min(width / DesignWidth, height / DesignHeight);
            surface.SetSize(DesignWidth, DesignHeight);
            surface.SetScale(scale, scale);
            surface.SetXY((width - DesignWidth * scale) * 0.5f, (height - DesignHeight * scale) * 0.5f);
            bool busy = model.Status.TacticalBusy;
            bool hasProgress = model.Status.HasTacticalCheckpoint;
            SetHomepageText(surface, "title", "战术对战");
            SetHomepageText(surface, "message", !string.IsNullOrWhiteSpace(model.Message)
                && model.Message != "继续恢复最近保存的角色清单和整备；战斗中退出后从整备重新出战。"
                ? model.Message
                : hasProgress ? "已保存整备" : "准备开始新的挑战");
            BindHomepageButton(surface, "btnContinue", busy ? "正在准备…" : hasProgress ? "继续整备" : "开始挑战",
                model.CanStart, () => SubmitHomepage(CharacterTestFairyGuiMainMenuCommandIds.Start));
            BindHomepageButton(surface, "btnNew", "新建挑战", model.CanRestart,
                () => ShowHomepageConfirmation(HomepageChoice.NewProgress));
            surface.GetChild("btnNew").visible = hasProgress;
            surface.GetChild("btnContinue").x=hasProgress?408:690;
            LayoutHomepageStatusLabel(surface, hasProgress);
            BindHomepageButton(surface, "btnSettings", "设置", model.CanSettings && !busy,
                () => SubmitHomepage(CharacterTestFairyGuiMainMenuCommandIds.SettingsToggle));
            BindHomepageButton(surface, "btnExit", busy ? "正在保存…" : "保存并退出", model.CanClose,
                () => ShowHomepageConfirmation(HomepageChoice.Exit));
            AlignHomepagePattern(surface, "btnContinue", "btnContinuePattern");
            AlignHomepagePattern(surface, "btnNew", "btnNewPattern");
            AlignHomepagePattern(surface, "btnSettings", "btnSettingsPattern");
            AlignHomepagePattern(surface, "btnExit", "btnExitPattern");

            if (_homepageFocus == null)
                _homepageFocus = new MxFairyGuiFocusNavigationMetadata(_viewId, "tacticalHome/btnContinue",
                    new[] { "tacticalHome/btnContinue", "tacticalHome/btnNew", "tacticalHome/btnSettings", "tacticalHome/btnExit" });
            MxFairyGuiFocusNavigation.Configure(root, _homepageFocus);
            if (_commandSink is CharacterTestFairyGuiMainMenuCommandSink sink)
                sink.SetHomepageConfirmationDismissal(TryDismissHomepageConfirmation);
            RefreshHomepageConfirmation();
            if (_homepageChoice == HomepageChoice.None && !busy && !_homepageKeyboardFocus)
                MxFairyGuiFocusNavigation.RequestDefaultFocus(root);
            return true;
        }

        private void BindHomepageButton(Fgui.GComponent surface, string name, string title, bool enabled, System.Action action)
        {
            Fgui.GButton button = surface.GetChild(name)?.asButton;
            if (button == null) return;
            button.text = title;
            button.enabled = enabled;
            button.grayed = !enabled;
            button.onClick.Set(() => { if (button.enabled) action(); });
            if (_homepageStyledButtons.Add(button))
            {
                RegisterHomepageFeedback(button);
            }
            var field=button.GetTextField();var format=field.textFormat;
            bool large=button.width>400;
            format.size=large?36:24;field.textFormat=format;
            if(button.GetChild("icon") is Fgui.GLoader icon)
            {
                bool hasIcon = !string.IsNullOrEmpty(icon.url);
                icon.visible = hasIcon;
                float size=large?44:28;icon.SetSize(size,size);icon.SetXY(large?56:40,(button.height-size)*.5f);
                field.x = hasIcon ? (large ? 104 : 76) : 12;
                field.width = button.width - field.x - (hasIcon ? 20 : 12);
            }
        }

        private static void StyleHomepageDialogDecorations(Fgui.GComponent dialog)
        {
            if (dialog == null) return;
            for (int i = 0; i < dialog.numChildren; i++)
            {
                Fgui.GObject decoration = dialog.GetChildAt(i);
                if (!decoration.name.StartsWith("dialogDecor", System.StringComparison.Ordinal)) continue;
                decoration.touchable = false;
                decoration.focusable = false;
                decoration.tabStop = false;
                decoration.alpha = decoration.name.Contains("Pattern") ? 0.055f : 0.36f;
                if (decoration.name == "dialogDecorPatternLeft")
                {
                    decoration.SetPivot(0.5f, 0.5f);
                    decoration.rotation = 180f;
                }
            }
        }

        private void LayoutHomepageStatusLabel(Fgui.GComponent surface, bool hasProgress)
        {
            Fgui.GTextField message = surface.GetChild("message")?.asTextField;
            Fgui.GImage background = surface.GetChild("statusLabelBackground")?.asImage;
            Fgui.GObject icon = surface.GetChild("checkpointIcon");
            if (message == null || background == null) return;
            float inset = hasProgress ? 36f : 14f;
            float width = Mathf.Clamp(message.textWidth + inset + 14f, 160f, 700f);
            float x = surface.GetChild("btnContinue").x;
            background.SetXY(x, 694f);
            background.SetSize(width, 32f);
            message.SetXY(x + inset, 694f);
            message.SetSize(width - inset - 14f, 32f);
            SetHomepageLabelNonInteractive(background);
            SetHomepageLabelNonInteractive(message);
            if (icon != null)
            {
                icon.visible = hasProgress;
                icon.SetXY(x + 12f, 702f);
                icon.SetSize(16f, 16f);
                SetHomepageLabelNonInteractive(icon);
            }
        }

        private static void SetHomepageLabelNonInteractive(Fgui.GObject element)
        {
            element.touchable = false;
            element.focusable = false;
            element.tabStop = false;
        }

        private static void AlignHomepagePattern(Fgui.GComponent surface, string buttonName, string patternName)
        {
            Fgui.GObject button = surface.GetChild(buttonName);
            Fgui.GObject pattern = surface.GetChild(patternName);
            if (button == null || pattern == null) return;
            float size = button.height * 0.52f;
            pattern.SetSize(size, size);
            pattern.SetXY(button.x + button.width - size - 48f, button.y + (button.height - size) * 0.5f);
            pattern.alpha = 0.12f;
            pattern.visible = button.visible;
            pattern.touchable = false;
        }

        private static void SetHomepageButtonContentColor(Fgui.GButton button, Color color)
        {
            if (button.GetChild("title")?.asTextField is Fgui.GTextField title) title.color = color;
            if (button.GetChild("icon")?.asLoader is Fgui.GLoader icon)
                icon.color = button.name == "btnContinue" || button.name == "btnNew"
                    || button.name == "btnSettings" || button.name == "btnExit" ? Color.white : color;
        }

        private static void SetHomepageText(Fgui.GComponent surface, string name, string value)
        {
            if (surface.GetChild(name)?.asTextField is Fgui.GTextField text) text.text = value;
        }

        private void ShowHomepageConfirmation(HomepageChoice choice)
        {
            if (_homepageModel?.Status.TacticalBusy != false) return;
            _homepageChoice = choice;
            RefreshHomepageConfirmation();
        }

        private void RefreshHomepageConfirmation()
        {
            bool visible = _homepageChoice != HomepageChoice.None;
            if (_homepage.GetChild("confirmationShade") is Fgui.GObject shade) shade.visible = visible;
            if (_homepageConfirmation == null) return;
            _homepageConfirmation.visible = visible;
            if (!visible) return;
            bool newProgress = _homepageChoice == HomepageChoice.NewProgress;
            SetHomepageText(_homepageConfirmation, "title", newProgress ? "开始新的挑战？" : "保存并退出？");
            SetHomepageText(_homepageConfirmation, "body", newProgress
                ? "使用初始角色与卡组配置。\n已有进度会保留，可以之后继续。"
                : "保存角色与整备后退出。\n战斗中的实时状态不作为续战存档。");
            BindHomepageButton(_homepageConfirmation, "btnCancel", "取消", true,
                () => TryDismissHomepageConfirmation());
            BindHomepageButton(_homepageConfirmation, "btnConfirm", newProgress ? "新建挑战" : "保存并退出",
                !_homepageModel.Status.TacticalBusy, () =>
                {
                    string command = _homepageChoice == HomepageChoice.NewProgress
                        ? CharacterTestFairyGuiMainMenuCommandIds.RestartOffline : CharacterTestFairyGuiMainMenuCommandIds.Close;
                    TryDismissHomepageConfirmation();
                    SubmitHomepage(command);
                });
            if (_confirmationFocus == null)
                _confirmationFocus = new MxFairyGuiFocusNavigationMetadata(_viewId, "tacticalHome/confirmation/btnCancel",
                    new[] { "tacticalHome/confirmation/btnCancel", "tacticalHome/confirmation/btnConfirm" });
            MxFairyGuiFocusNavigation.Configure(_homepageRoot, _confirmationFocus);
            MxFairyGuiFocusNavigation.RequestDefaultFocus(_homepageRoot);
        }

        private bool TryDismissHomepageConfirmation()
        {
            if (_homepage == null || _homepage.isDisposed || !_homepage.visible || _homepageChoice == HomepageChoice.None)
                return false;
            _homepageChoice = HomepageChoice.None;
            RefreshHomepageConfirmation();
            MxFairyGuiFocusNavigation.Configure(_homepageRoot, _homepageFocus);
            MxFairyGuiFocusNavigation.RequestDefaultFocus(_homepageRoot);
            return true;
        }

        private void OnHomepageKeyDown(Fgui.EventContext context)
        {
            Fgui.InputEvent input = context.data as Fgui.InputEvent ?? context.inputEvent;
            if (input == null) return;
            KeyCode key = input.keyCode;
            if (key == KeyCode.None)
            {
                if (input.character == '\r' || input.character == '\n') key = KeyCode.Return;
                else if (input.character == '\t') key = KeyCode.Tab;
            }
            bool handled;
            switch (key)
            {
                case KeyCode.Escape:
                    handled = TryDismissHomepageConfirmation();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    _homepageKeyboardFocus = true;
                    // Unity can deliver the physical key and its newline as separate GUI events.
                    if (Time.unscaledTime < _homepageNextKeyboardSubmit)
                    {
                        handled = true;
                        break;
                    }
                    _homepageNextKeyboardSubmit = Time.unscaledTime + 0.2f;
                    handled = MxFairyGuiFocusNavigation.Submit(_homepageRoot);
                    break;
                case KeyCode.Tab:
                    _homepageKeyboardFocus = true;
                    handled = (input.modifiers & EventModifiers.Shift) != 0
                        ? MxFairyGuiFocusNavigation.MovePrevious(_homepageRoot)
                        : MxFairyGuiFocusNavigation.MoveNext(_homepageRoot);
                    break;
                default:
                    handled = false;
                    break;
            }
            if (!handled)
                return;
            context.StopPropagation();
            context.PreventDefault();
        }

        private void SubmitHomepage(string command)
        {
            if (_homepageModel?.Status.TacticalBusy != false) return;
            _commandSink?.Enqueue(new MxUiCommand(_viewId, command, null));
        }
    }
}
