using MxFramework.UI;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder
    {
        private void BindCardTrialEntry(Fgui.GComponent root)
        {
            var button = root.GetChild("cardTrialEntry")?.asButton;
            if (button == null) return; // Older published package remains usable until this candidate is published.
            button.title = "真实出牌测试";
            button.onClick.Set(context => { context.StopPropagation(); _commandSink.Enqueue(new MxUiCommand(_viewId, CharacterTestRuntimeAiLabCardTrialIds.OpenCommandId, null)); });
        }
    }
}
