using System;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Displays structured authoring-navigation feedback as a modal, input-owning surface.</summary>
    internal sealed class CharacterTestFairyGuiRuntimeAiLabNavigationFeedbackBinder
    {
        private readonly CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink _commandSink;
        private Fgui.GComponent _overlay;
        private bool _detailsExpanded;
        private string _feedbackCode = string.Empty;

        internal CharacterTestFairyGuiRuntimeAiLabNavigationFeedbackBinder(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink commandSink)
        {
            _commandSink = commandSink;
        }

        internal void Bind(Fgui.GComponent root)
        {
            Fgui.GComponent overlay = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NavigationFeedbackOverlay)?.asCom;
            if (!ReferenceEquals(_overlay, overlay))
            {
                _overlay = overlay;
                _detailsExpanded = false;
                _feedbackCode = string.Empty;
                InstallInputBoundary();
            }
            if (_overlay == null)
                return;

            CharacterTestRuntimeAiLabNavigationFeedback feedback =
                _commandSink?.LastNavigationFeedback
                ?? CharacterTestRuntimeAiLabNavigationFeedback.None;
            if (!feedback.IsVisible)
            {
                _overlay.visible = false;
                return;
            }

            if (!string.Equals(_feedbackCode, feedback.Code, StringComparison.Ordinal))
            {
                _feedbackCode = feedback.Code;
                _detailsExpanded = false;
            }
            _overlay.visible = true;
            _overlay.sortingOrder = 5000;
            _overlay.SetSize(root.width, root.height);
            SetText("navigationFeedbackTitle", feedback.Title);
            SetText("navigationFeedbackMessage", feedback.Message);
            SetText("navigationFeedbackImpact", feedback.Impact);
            SetText("navigationFeedbackTechnicalDetails", feedback.TechnicalDetails);
            SetText(
                "navigationBusyStageText",
                feedback.Code == "NAVIGATION_OPERATION_BUSY" ? feedback.Message : string.Empty);

            bool hasDetails = !string.IsNullOrWhiteSpace(feedback.TechnicalDetails)
                && HasAction(feedback, CharacterTestRuntimeAiLabNavigationFeedbackAction.ViewDetails);
            SetVisible("navigationFeedbackTechnicalScroll", hasDetails && _detailsExpanded);
            SetVisible("navigationFeedbackTechnicalDetails", hasDetails && _detailsExpanded);
            BindButton(
                "btnNavigationRetrySave",
                "重试保存",
                HasAction(feedback, CharacterTestRuntimeAiLabNavigationFeedbackAction.RetrySave),
                CharacterTestRuntimeAiLabNavigationFeedbackAction.RetrySave);
            BindButton(
                "btnNavigationDiscardChanges",
                "放弃本次修改",
                HasAction(
                    feedback,
                    CharacterTestRuntimeAiLabNavigationFeedbackAction.DiscardPendingChanges),
                CharacterTestRuntimeAiLabNavigationFeedbackAction.DiscardPendingChanges);
            BindButton(
                "btnNavigationCancel",
                "取消",
                HasAction(feedback, CharacterTestRuntimeAiLabNavigationFeedbackAction.Cancel),
                CharacterTestRuntimeAiLabNavigationFeedbackAction.Cancel);

            Fgui.GButton details = _overlay.GetChild("btnNavigationViewDetails")?.asButton;
            if (details != null)
            {
                details.visible = hasDetails;
                details.enabled = hasDetails;
                details.grayed = !hasDetails;
                details.text = _detailsExpanded ? "收起详情" : "查看详情";
                details.onClick.Set(context =>
                {
                    context.StopPropagation();
                    _detailsExpanded = !_detailsExpanded;
                    Bind(root);
                });
            }
            _overlay.RequestFocus();
        }

        private void InstallInputBoundary()
        {
            if (_overlay == null)
                return;
            _overlay.opaque = true;
            _overlay.touchable = true;
            _overlay.focusable = true;
            _overlay.onTouchBegin.Set(context => context.StopPropagation());
            _overlay.onTouchMove.Set(context => context.StopPropagation());
            _overlay.onTouchEnd.Set(context => context.StopPropagation());
            _overlay.onClick.Set(context => context.StopPropagation());
            _overlay.displayObject.onMouseWheel.Set(context => context.StopPropagation());
            _overlay.onKeyDown.Set(context =>
            {
                if (context?.inputEvent?.keyCode == KeyCode.Escape
                    && HasAction(
                        _commandSink?.LastNavigationFeedback,
                        CharacterTestRuntimeAiLabNavigationFeedbackAction.Cancel))
                {
                    _commandSink.ExecuteNavigationFeedbackAction(
                        CharacterTestRuntimeAiLabNavigationFeedbackAction.Cancel);
                }
                context?.StopPropagation();
            });
        }

        private void BindButton(
            string childName,
            string label,
            bool visible,
            CharacterTestRuntimeAiLabNavigationFeedbackAction action)
        {
            Fgui.GButton button = _overlay.GetChild(childName)?.asButton;
            if (button == null)
                return;
            button.visible = visible;
            button.enabled = visible;
            button.grayed = !visible;
            button.text = label;
            button.onClick.Set(context =>
            {
                context.StopPropagation();
                if (visible)
                    _commandSink?.ExecuteNavigationFeedbackAction(action);
            });
        }

        private static bool HasAction(
            CharacterTestRuntimeAiLabNavigationFeedback feedback,
            CharacterTestRuntimeAiLabNavigationFeedbackAction action)
        {
            CharacterTestRuntimeAiLabNavigationFeedbackAction[] actions = feedback?.Actions;
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                if (actions[i] == action)
                    return true;
            }
            return false;
        }

        private void SetText(string childName, string value)
        {
            Fgui.GTextField text = _overlay.GetChild(childName)?.asTextField;
            if (text != null)
                text.text = value ?? string.Empty;
        }

        private void SetVisible(string childName, bool visible)
        {
            Fgui.GObject child = _overlay.GetChild(childName);
            if (child != null)
                child.visible = visible;
        }
    }
}
