using MxFramework.Resources;
using MxFramework.UI;

namespace MxFramework.UI.FairyGui
{
    /// <summary>Stable runtime identifiers for the shared Player Guidance FairyGUI package.</summary>
    public static class MxPlayerGuidanceFairyGuiIds
    {
        public const string PackageId = "MxPlayerGuidanceUi";
        public const string PackageBytesResourceId = "ui.fairygui.player_guidance.fui";
        public const string Atlas0ResourceId = "ui.fairygui.player_guidance.atlas0";

        public const string CardViewIdValue = "ui.guidance.card";
        public const string CardComponentName = "GuidanceCardPanel";
        public const string CardChrome = "cardChrome";
        public const string HeaderIcon = "headerIcon";
        public const string Title = "title";
        public const string ActionIcon = "actionIcon";
        public const string ActionLabel = "actionLabel";
        public const string ActionBody = "actionBody";
        public const string FocusIcon = "focusIcon";
        public const string FocusLabel = "focusLabel";
        public const string FocusBody = "focusBody";
        public const string StatusIcon = "statusIcon";
        public const string StatusLabel = "statusLabel";
        public const string StatusBody = "statusBody";

        public const string NoticeViewIdValue = "ui.guidance.notices";
        public const string NoticeComponentName = "GuidanceNoticeStackPanel";
        public const string Notice1Chrome = "notice1Chrome";
        public const string Notice1ToneOverlay = "notice1ToneOverlay";
        public const string Notice1Icon = "notice1Icon";
        public const string Notice1Text = "notice1Text";
        public const string Notice2Chrome = "notice2Chrome";
        public const string Notice2ToneOverlay = "notice2ToneOverlay";
        public const string Notice2Icon = "notice2Icon";
        public const string Notice2Text = "notice2Text";

        public const string ContextIconResourceName = "lock_crosshair_64x64";
        public const string ActionIconResourceName = "action_continue_32x32";
        public const string FocusIconResourceName = "action_view_32x32";
        public const string SuccessIconResourceName = "action_valid_32x32";
        public const string WarningIconResourceName = "state_warning_64x64";
        public const string ErrorIconResourceName = "state_blocked_32x32";

        public static MxUiViewId CardViewId => new MxUiViewId(CardViewIdValue);
        public static MxUiViewId NoticeViewId => new MxUiViewId(NoticeViewIdValue);

        public static ResourceKey PackageBytesKey =>
            new ResourceKey(
                PackageBytesResourceId,
                MxFairyGuiResourceTypeIds.PackageBytes,
                packageId: PackageId);

        public static ResourceKey Atlas0Key =>
            new ResourceKey(
                Atlas0ResourceId,
                MxFairyGuiResourceTypeIds.PackageAtlas,
                packageId: PackageId);
    }
}
