using MxFramework.Resources;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestFairyGuiSettingsPanelIds
    {
        public const string ViewIdValue = "ui.character_test.settings.panel";
        public const string PackageId = "MxCharacterTestSettingsUi";
        public const string ComponentName = "SettingsPanel";
        public const string PackageBytesResourceId = "ui.fairygui.charactertest.settings_ui.fui";
        public const string Atlas0ResourceId = "ui.fairygui.charactertest.settings_ui.atlas0";
        public const string PanelFrame = "panelFrame";
        public const string PanelTrim = "panelTrim";
        public const string Title = "title";
        public const string Status = "status";
        public const string DirtyMarker = "dirtyMarker";
        public const string Close = "btnClose";
        public const string QualityLabel = "qualityLabel";
        public const string GraphicsHeader = "graphicsHeader";
        public const string QualityValue = "qualityValue";
        public const string QualityPrev = "btnQualityPrev";
        public const string QualityNext = "btnQualityNext";
        public const string AntiAliasingLabel = "antiAliasingLabel";
        public const string AntiAliasingValue = "antiAliasingValue";
        public const string AntiAliasingPrev = "btnAntiAliasingPrev";
        public const string AntiAliasingNext = "btnAntiAliasingNext";
        public const string ShadowDistanceLabel = "shadowDistanceLabel";
        public const string ShadowDistanceInput = "shadowDistanceInput";
        public const string DirectionalShadowQualityLabel = "mainShadowResolutionLabel";
        public const string DirectionalShadowQualityValue = "mainShadowResolutionValue";
        public const string DirectionalShadowQualityPrev = "btnMainShadowResolutionPrev";
        public const string DirectionalShadowQualityNext = "btnMainShadowResolutionNext";
        public const string ShadowCascadeCountLabel = "shadowCascadeCountLabel";
        public const string ShadowCascadeCountValue = "shadowCascadeCountValue";
        public const string ShadowCascadeCountPrev = "btnShadowCascadeCountPrev";
        public const string ShadowCascadeCountNext = "btnShadowCascadeCountNext";
        public const string LocalShadowResolutionLabel = "localShadowResolutionLabel";
        public const string LocalShadowResolutionValue = "localShadowResolutionValue";
        public const string LocalShadowResolutionPrev = "btnLocalShadowResolutionPrev";
        public const string LocalShadowResolutionNext = "btnLocalShadowResolutionNext";
        public const string VSyncLabel = "vSyncLabel";
        public const string HdrLabel = "hdrLabel";
        public const string HdrValue = "hdrValue";
        public const string HdrToggle = "btnHdr";
        public const string HdrStatus = "hdrStatus";
        public const string HdrPaperWhiteLabel = "hdrPaperWhiteLabel";
        public const string HdrPaperWhiteValue = "hdrPaperWhiteValue";
        public const string HdrPaperWhitePrev = "btnHdrPaperWhitePrev";
        public const string HdrPaperWhiteNext = "btnHdrPaperWhiteNext";
        public const string HdrPeakLabel = "hdrPeakLabel";
        public const string HdrPeakValue = "hdrPeakValue";
        public const string HdrPeakPrev = "btnHdrPeakPrev";
        public const string HdrPeakNext = "btnHdrPeakNext";
        public const string VSyncValue = "vSyncValue";
        public const string VSyncToggle = "btnVSync";
        public const string RenderScaleLabel = "renderScaleLabel";
        public const string RenderScaleInput = "renderScaleInput";
        public const string ResolutionLabel = "resolutionLabel";
        public const string ResolutionValue = "resolutionValue";
        public const string WindowModeLabel = "windowModeLabel";
        public const string WindowModeValue = "windowModeValue";
        public const string LanguageLabel = "languageLabel";
        public const string LanguageValue = "languageValue";
        public const string LanguagePrev = "btnLanguagePrev";
        public const string LanguageNext = "btnLanguageNext";
        public const string AudioVolumeLabel = "audioVolumeLabel";
        public const string AudioHeader = "audioHeader";
        public const string AudioCategoryLabel = "audioCategoryLabel";
        public const string AudioCategoryValue = "audioCategoryValue";
        public const string AudioCategoryPrevious = "btnAudioCategoryPrev";
        public const string AudioCategoryNext = "btnAudioCategoryNext";
        public const string AudioVolumeInput = "audioVolumeInput";
        public const string AudioMutedValue = "audioMutedValue";
        public const string AudioMuteToggle = "btnMute";
        public const string Apply = "btnApply";
        public const string Save = "btnSave";
        public const string Reload = "btnReload";
        public const string SaveInput = "btnSaveInput";
        public const string ResetInput = "btnResetInput";
        public const string ControlsHeader = "controlsHeader";
        public const string Subtitle = "subtitle";
        public const string PageTitle = "pageTitle";
        public const string PageDescription = "pageDescription";
        public const string NavGraphics = "btnNavGraphics";
        public const string NavAudio = "btnNavAudio";
        public const string NavControls = "btnNavControls";
        public const string KeyboardMouseTab = "btnKeyboardMouse";
        public const string GamepadTab = "btnGamepad";
        public const string InputInstruction = "inputInstruction";
        public const string ActionColumnTitle = "actionColumnTitle";
        public const string PrimaryColumnTitle = "primaryColumnTitle";
        public const string SecondaryColumnTitle = "secondaryColumnTitle";
        public const string InputList = "inputList";
        public const string InputRebindProxy = "btnInputRebindProxy";
        public const string CancelChanges = "btnCancel";
        public const string GraphicsPage = "graphicsPage";
        public const string AudioPage = "audioPage";
        public const string ControlsPage = "controlsPage";

        public static MxUiViewId ViewId => new MxUiViewId(ViewIdValue);

        public static ResourceKey PackageBytesKey =>
            new ResourceKey(PackageBytesResourceId, MxFairyGuiResourceTypeIds.PackageBytes, packageId: PackageId);

        public static ResourceKey Atlas0Key =>
            new ResourceKey(Atlas0ResourceId, MxFairyGuiResourceTypeIds.PackageAtlas, packageId: PackageId);

    }

    public static class CharacterTestFairyGuiSettingsPanelCommandIds
    {
        public const string Apply = "character_test.settings.apply";
        public const string Save = "character_test.settings.save";
        public const string Reload = "character_test.settings.reload";
        public const string QualityPrevious = "character_test.settings.quality.previous";
        public const string QualityNext = "character_test.settings.quality.next";
        public const string AntiAliasingPrevious = "character_test.settings.anti_aliasing.previous";
        public const string AntiAliasingNext = "character_test.settings.anti_aliasing.next";
        public const string DirectionalShadowQualityPrevious = "character_test.settings.directional_shadow_quality.previous";
        public const string DirectionalShadowQualityNext = "character_test.settings.directional_shadow_quality.next";
        public const string ShadowCascadeCountPrevious = "character_test.settings.shadow_cascades.previous";
        public const string ShadowCascadeCountNext = "character_test.settings.shadow_cascades.next";
        public const string LocalShadowResolutionPrevious = "character_test.settings.local_shadow_resolution.previous";
        public const string LocalShadowResolutionNext = "character_test.settings.local_shadow_resolution.next";
        public const string LanguagePrevious = "character_test.settings.language.previous";
        public const string LanguageNext = "character_test.settings.language.next";
        public const string ToggleVSync = "character_test.settings.vsync.toggle";
        public const string ToggleHdr = "character_test.settings.hdr.toggle";
        public const string HdrPaperWhitePrevious = "character_test.settings.hdr.paper_white.previous";
        public const string HdrPaperWhiteNext = "character_test.settings.hdr.paper_white.next";
        public const string HdrPeakPrevious = "character_test.settings.hdr.peak.previous";
        public const string HdrPeakNext = "character_test.settings.hdr.peak.next";
        public const string AudioCategoryPrevious = "character_test.settings.audio.category.previous";
        public const string AudioCategoryNext = "character_test.settings.audio.category.next";
        public const string ToggleMute = "character_test.settings.audio.mute.toggle";
        public const string StartInputRebind = "character_test.settings.input.rebind";
        public const string SaveInput = "character_test.settings.input.save";
        public const string ResetInput = "character_test.settings.input.reset";
        public const string Close = "character_test.settings.close";
        public const string CancelChanges = "character_test.settings.cancel";
        public const string SelectGraphics = "character_test.settings.page.graphics";
        public const string SelectAudio = "character_test.settings.page.audio";
        public const string SelectControls = "character_test.settings.page.controls";
        public const string SelectKeyboardMouse = "character_test.settings.device.keyboard_mouse";
        public const string SelectGamepad = "character_test.settings.device.gamepad";
    }
}
