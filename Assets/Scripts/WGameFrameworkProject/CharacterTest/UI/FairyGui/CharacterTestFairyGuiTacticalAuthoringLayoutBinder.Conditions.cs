using UnityEngine;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiTacticalAuthoringLayoutBinder
    {
        private CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder _activationBinder;
        private IMxFairyGuiPopupService _popupService;
        private MxUiViewId _viewId;

        internal void ConfigureServices(IMxUiCommandSink commandSink, MxUiViewId viewId, IMxFairyGuiPopupService popupService)
        {
            _viewId = viewId; _popupService = popupService;
            _activationBinder = new CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder(commandSink, viewId, popupService, null, null);
            _composition.ConfigureServices(viewId, popupService);
        }

        private void BindActivationOverview(Fgui.GComponent root, CharacterTestRuntimeAiLabTacticalAuthoringLayout layout)
        {
            var panel = root.GetChild(CharacterTestTacticalPreviewControlContract.ActivationInspector)?.asCom;
            if (panel == null) return;
            if (_activationBinder == null)
                _activationBinder = new CharacterTestFairyGuiRuntimeAiLabBehaviorTriggerEventInspectorBinder(null, default, null, null, null);
            var preview = _compositionSession.BuildActivationOverview();
            var model = BehaviorRecipeTriggerEventInspectorProjection.BuildActivationOverview(preview,
                _workbenchModel?.BehaviorCardWorkspace?.Timeline?.CharacterActionOptions);
            var rect = new Rect(layout.Overview.x + 8, layout.Overview.y + _overviewConditionsOffset,
                layout.Overview.width - 16, Mathf.Max(140, layout.Overview.height - _overviewConditionsOffset - 84));
            _activationBinder.BindActivationOverview(panel, model, rect.width, rect.height);
            Geometry(panel, rect);
            panel.visible = true;
            panel.sortingOrder = 45;
            float requiredY = rect.y + _activationBinder.OverviewContentHeight + 10;
            SetWorkbenchText(root, CharacterTestTacticalPreviewControlContract.RequiredText, "动作可用性在出牌时自动检查",
                new Rect(layout.Overview.x + 16, requiredY, layout.Overview.width - 32, 24));
            if (root.GetChild(CharacterTestTacticalPreviewControlContract.RequiredText)?.asTextField is Fgui.GTextField hint)
            { hint.textFormat.size = 12; hint.color = new Color32(128, 157, 170, 255); }
        }
    }
}
