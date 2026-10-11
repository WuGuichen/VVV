using FairyGUI;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiLabShellSurfaceViewModel
    {
        internal bool Visible { get; set; }
        internal string Title { get; set; } = string.Empty;
        internal string Detail { get; set; } = string.Empty;
        internal bool CanRestore { get; set; }
        internal bool CanReset { get; set; }
        internal bool CanHide { get; set; } = true;

        internal static CharacterTestRuntimeAiLabShellSurfaceViewModel Create(
            CharacterTestRuntimeAiLabShellState state,
            string detail)
        {
            bool recovery = state != null
                && state.PresentationMode == CharacterTestRuntimeAiLabPresentationMode.Recovery;
            CharacterTestRuntimeAiLabShellFaultContext fault = state?.Fault;
            string faultDetail = fault != null
                ? fault.Code + "\n" + fault.Detail
                : detail;
            return new CharacterTestRuntimeAiLabShellSurfaceViewModel
            {
                Visible = true,
                Title = recovery
                    ? "Runtime AI Lab 需要恢复"
                    : "正在建立 Runtime AI Lab 会话",
                Detail = recovery
                    ? faultDetail
                    : "正在解析共享行为卡并准备运行时状态，请稍候。",
                CanRestore = recovery && fault != null && fault.CanRetry,
                CanReset = recovery && fault != null && fault.CanResetSession,
                CanHide = true
            };
        }
    }

    /// <summary>
    /// Fail-safe binder for transition and recovery presentation. It intentionally does not
    /// invoke the Runtime AI Lab workspace ViewModel builder or workspace binder.
    /// </summary>
    internal sealed class CharacterTestRuntimeAiLabShellSurfaceBinder :
        IMxFairyGuiViewBinder<CharacterTestRuntimeAiLabShellSurfaceViewModel>
    {
        private readonly IMxUiCommandSink _commandSink;
        private readonly MxUiViewId _viewId;

        internal CharacterTestRuntimeAiLabShellSurfaceBinder(
            IMxUiCommandSink commandSink,
            MxUiViewId viewId)
        {
            _commandSink = commandSink;
            _viewId = viewId;
        }

        public void Bind(
            IMxFairyGuiComponentHandle component,
            CharacterTestRuntimeAiLabShellSurfaceViewModel viewModel)
        {
            GComponent root = (component as MxFairyGuiComponentHandle)?.Component;
            if (root == null || viewModel == null)
                return;

            SetVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ShellSurfaceScrim, true);
            SetVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ShellSurfacePanel, true);
            SetText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ShellSurfaceTitle, viewModel.Title);
            SetText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ShellSurfaceDetail, viewModel.Detail);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ShellRestore,
                "返回安全页面",
                viewModel.CanRestore,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ShellRestore);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ShellReset,
                "重置并返回设置",
                viewModel.CanReset,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ShellReset);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ShellHide,
                "隐藏 (F9)",
                viewModel.CanHide,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ShellHide);
        }

        private void BindButton(
            GComponent root,
            string childName,
            string label,
            bool enabled,
            string commandId)
        {
            GButton button = root.GetChild(childName)?.asButton;
            if (button == null)
                return;
            button.visible = true;
            button.touchable = enabled;
            button.text = label;
            button.enabled = enabled;
            button.grayed = !enabled;
            button.onClick.Set(() =>
            {
                if (enabled)
                    _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, null));
            });
        }

        private static void SetText(GComponent root, string childName, string text)
        {
            GTextField field = root.GetChild(childName)?.asTextField;
            if (field == null)
                return;
            field.visible = true;
            field.touchable = false;
            field.text = text ?? string.Empty;
        }

        private static void SetVisible(GComponent root, string childName, bool visible)
        {
            GObject child = root.GetChild(childName);
            if (child == null)
                return;
            child.visible = visible;
            child.touchable = visible;
        }
    }
}
