using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Demo.FairyGui
{
    public static class StoryRuntimeFairyGuiDialogIds
    {
        public const string ViewIdValue = "ui.story.dialog";
        public const string PackageId = "MxStoryDialog";
        public const string ComponentName = "StoryDialogPanel";
        public const string ChoiceButtonComponentName = "StoryDialogButton";
        public const string PackageBytesResourceId = "ui.fairygui.storydialog.fui";

        public const string Title = "title";
        public const string Phase = "phase";
        public const string DialogueText = "dialogueText";
        public const string ChoiceText = "choiceText";
        public const string SignalText = "signalText";
        public const string EventLog = "eventLog";
        public const string Continue = "btnContinue";
        public const string Choice = "btnChoice";
        public const string ChoiceItemPrefix = "btnChoice__";

        public static MxUiViewId ViewId => new MxUiViewId(ViewIdValue);

        public static ResourceKey PackageBytesKey =>
            new ResourceKey(PackageBytesResourceId, MxFairyGuiResourceTypeIds.PackageBytes, packageId: PackageId);
    }
}
