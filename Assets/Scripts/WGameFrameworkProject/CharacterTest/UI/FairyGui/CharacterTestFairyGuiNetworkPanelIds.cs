using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestFairyGuiNetworkPanelIds
    {
        public const string ViewIdValue = "ui.character_test.network.panel";
        public const string PackageId = "MxCharacterTestNetworkUi";
        public const string ComponentName = "NetworkPanel";
        public const string PackageBytesResourceId = "ui.fairygui.charactertest.network_ui.fui";
        public const string Atlas0ResourceId = "ui.fairygui.charactertest.network_ui.atlas0";

        public const string PanelFrame = "panelFrame";
        public const string PanelTrim = "panelTrim";
        public const string Title = "title";
        public const string Status = "status";
        public const string Endpoint = "endpoint";
        public const string DirectLanMode = "btnDirectLan";
        public const string RoomMode = "btnRoom";
        public const string ModeDescription = "modeDescription";
        public const string ConnectionHeader = "connectionHeader";
        public const string RemoteAddressLabel = "remoteAddressLabel";
        public const string RemoteAddressInput = "remoteAddressInput";
        public const string PortLabel = "portLabel";
        public const string PortInput = "portInput";
        public const string DisplayNameLabel = "displayNameLabel";
        public const string DisplayNameInput = "displayNameInput";
        public const string RoomNameLabel = "roomNameLabel";
        public const string RoomNameInput = "roomNameInput";
        public const string ConnectionSummary = "connectionSummary";
        public const string RoomHeader = "roomHeader";
        public const string RoomSummary = "roomSummary";
        public const string MemberSummary = "memberSummary";
        public const string ReadinessSummary = "readinessSummary";
        public const string SyncProbeSummary = "syncProbeSummary";
        public const string ReplayHeader = "replayHeader";
        public const string ReplaySummary = "replaySummary";
        public const string DetailsHeader = "detailsHeader";
        public const string DetailTransport = "detailTransport";
        public const string DetailPeer = "detailPeer";
        public const string DetailRtt = "detailRtt";
        public const string DetailFrames = "detailFrames";
        public const string DetailSnapshot = "detailSnapshot";
        public const string DetailDisconnect = "detailDisconnect";
        public const string Message = "message";
        public const string Host = "btnHost";
        public const string Join = "btnJoin";
        public const string Disconnect = "btnDisconnect";
        public const string Ready = "btnReady";
        public const string EnterGame = "btnEnterGame";
        public const string AutoSyncProbe = "btnAutoSyncProbe";
        public const string RunSyncProbe = "btnRunSyncProbe";
        public const string Close = "btnClose";

        public static MxUiViewId ViewId => new MxUiViewId(ViewIdValue);

        public static ResourceKey PackageBytesKey =>
            new ResourceKey(PackageBytesResourceId, MxFairyGuiResourceTypeIds.PackageBytes, packageId: PackageId);

        public static ResourceKey Atlas0Key =>
            new ResourceKey(Atlas0ResourceId, MxFairyGuiResourceTypeIds.PackageAtlas, packageId: PackageId);
    }

    public static class CharacterTestFairyGuiNetworkPanelCommandIds
    {
        public const string DirectLanMode = "character_test.network.mode.direct_lan";
        public const string RoomMode = "character_test.network.mode.room";
        public const string Host = "character_test.network.host";
        public const string Join = "character_test.network.join";
        public const string Disconnect = "character_test.network.disconnect";
        public const string Ready = "character_test.network.ready";
        public const string EnterGame = "character_test.network.enter_game";
        public const string AutoSyncProbe = "character_test.network.auto_sync_probe";
        public const string RunSyncProbe = "character_test.network.run_sync_probe";
        public const string Close = "character_test.network.close";
    }
}
