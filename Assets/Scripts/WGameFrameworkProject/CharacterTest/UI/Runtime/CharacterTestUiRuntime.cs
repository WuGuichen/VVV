using System;
using System.Collections.Generic;
using MxFramework.DebugUI;
using MxFramework.Diagnostics;
using MxFramework.Input;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestUiSurface
    {
        CombatHud,
        FloatingCombatText,
        Guidance,
        MainMenu,
        NetworkPanel,
        SettingsPanel,
        AbilityBindingPanel,
        RuntimeAiBehaviorLoadoutPanel,
        TacticalPreparationPanel,
        ArenaSetupPanel,
        ArenaBehaviorGroupPanel,
        StoryDialog
    }

    internal enum CharacterTestUiSurfaceKind
    {
        Hud,
        LightOverlay,
        NarrativeModal,
        PrimaryPanel,
        TransactionalPanel,
        SystemMenu,
        DebugOverlay
    }

    internal enum CharacterTestUiSurfaceCancelBehavior
    {
        None,
        CloseTopSurface
    }

    internal enum CharacterTestUiSurfaceOpenReason
    {
        Direct,
        MainMenuChild,
        BackNavigation,
        AutoNormalize
    }

    [Flags]
    internal enum CharacterTestUiSurfaceTraits
    {
        None = 0,
        ReleasesCursor = 1 << 0,
        ExclusiveUiInput = 1 << 1,
        SuppressesNarrative = 1 << 2,
        HidesHud = 1 << 3,
        ClearsLightOverlay = 1 << 4,
        HandlesCancel = 1 << 5
    }

    [Flags]
    internal enum CharacterTestUiSurfaceMask
    {
        None = 0,
        CombatHud = 1 << 0,
        FloatingCombatText = 1 << 1,
        MainMenu = 1 << 2,
        NetworkPanel = 1 << 3,
        SettingsPanel = 1 << 4,
        AbilityBindingPanel = 1 << 5,
        RuntimeAiBehaviorLoadoutPanel = 1 << 6,
        ArenaSetupPanel = 1 << 7,
        ArenaBehaviorGroupPanel = 1 << 8,
        StoryDialog = 1 << 9,
        Guidance = 1 << 10,
        TacticalPreparationPanel = 1 << 11
    }

    internal readonly struct CharacterTestUiSurfaceDescriptor
    {
        public CharacterTestUiSurfaceDescriptor(
            CharacterTestUiSurface surface,
            CharacterTestUiSurfaceKind kind,
            CharacterTestUiSurfaceTraits traits,
            CharacterTestUiSurfaceCancelBehavior cancelBehavior)
        {
            Surface = surface;
            Kind = kind;
            Traits = traits;
            CancelBehavior = cancelBehavior;
        }

        public CharacterTestUiSurface Surface { get; }
        public CharacterTestUiSurfaceKind Kind { get; }
        public CharacterTestUiSurfaceTraits Traits { get; }
        public CharacterTestUiSurfaceCancelBehavior CancelBehavior { get; }
    }

    internal static class CharacterTestUiSurfacePolicy
    {
        private const CharacterTestUiSurfaceTraits PlayerModalTraits =
            CharacterTestUiSurfaceTraits.ReleasesCursor
            | CharacterTestUiSurfaceTraits.ExclusiveUiInput
            | CharacterTestUiSurfaceTraits.HandlesCancel;

        private const CharacterTestUiSurfaceTraits LargePanelTraits =
            PlayerModalTraits
            | CharacterTestUiSurfaceTraits.SuppressesNarrative
            | CharacterTestUiSurfaceTraits.HidesHud
            | CharacterTestUiSurfaceTraits.ClearsLightOverlay;

        private const CharacterTestUiSurfaceMask LargePlayerSurfaces =
            CharacterTestUiSurfaceMask.MainMenu
            | CharacterTestUiSurfaceMask.NetworkPanel
            | CharacterTestUiSurfaceMask.SettingsPanel
            | CharacterTestUiSurfaceMask.AbilityBindingPanel
            | CharacterTestUiSurfaceMask.RuntimeAiBehaviorLoadoutPanel
            | CharacterTestUiSurfaceMask.TacticalPreparationPanel
            | CharacterTestUiSurfaceMask.ArenaSetupPanel
            | CharacterTestUiSurfaceMask.ArenaBehaviorGroupPanel;

        private const CharacterTestUiSurfaceMask PlayerModalSurfaces =
            LargePlayerSurfaces | CharacterTestUiSurfaceMask.StoryDialog;

        private const CharacterTestUiSurfaceMask NonMainMenuPlayerModalSurfaces =
            PlayerModalSurfaces & ~CharacterTestUiSurfaceMask.MainMenu;

        public static CharacterTestUiSurfaceMask Add(
            CharacterTestUiSurfaceMask state,
            CharacterTestUiSurface surface)
        {
            return state | ToMask(surface);
        }

        public static bool HasPlayerModalOpen(CharacterTestUiSurfaceMask state)
        {
            return (state & PlayerModalSurfaces) != 0;
        }

        public static bool HasNonMainMenuPlayerModalOpen(CharacterTestUiSurfaceMask state)
        {
            return (state & NonMainMenuPlayerModalSurfaces) != 0;
        }

        public static bool IsStoryDialogSuppressed(CharacterTestUiSurfaceMask state)
        {
            return (state & LargePlayerSurfaces) != 0;
        }

        public static bool ShouldClearGameplayPresentation(CharacterTestUiSurfaceMask state)
        {
            return ContainsTrait(state, CharacterTestUiSurfaceTraits.HidesHud)
                || ContainsTrait(state, CharacterTestUiSurfaceTraits.ClearsLightOverlay);
        }

        public static bool ShouldClearGameplayPresentation(
            CharacterTestUiSurfaceMask state,
            bool arenaSetupPanelBlocksGameplayPresentation)
        {
            if (!arenaSetupPanelBlocksGameplayPresentation)
                state &= ~CharacterTestUiSurfaceMask.ArenaSetupPanel;

            return ShouldClearGameplayPresentation(state);
        }

        public static CharacterTestUiSurfaceMask GetConflictingSurfacesToClose(
            CharacterTestUiSurface openedSurface,
            CharacterTestUiSurfaceMask openSurfaces)
        {
            CharacterTestUiSurfaceMask openedMask = ToMask(openedSurface);
            if (openedMask == CharacterTestUiSurfaceMask.None)
                return CharacterTestUiSurfaceMask.None;

            CharacterTestUiSurfaceDescriptor descriptor = GetDescriptor(openedSurface);
            if (!IsExclusiveLargePanelKind(descriptor.Kind))
                return CharacterTestUiSurfaceMask.None;

            CharacterTestUiSurfaceMask keepOpen = GetSurfaceSessionMask(openedSurface);
            return openSurfaces & LargePlayerSurfaces & ~keepOpen;
        }

        public static bool TryGetCancelTarget(
            CharacterTestUiSurfaceMask openSurfaces,
            CharacterTestUiSurface preferredSurface,
            out CharacterTestUiSurface target)
        {
            CharacterTestUiSurfaceMask preferredMask = ToMask(preferredSurface);
            if ((openSurfaces & preferredMask) != 0
                && GetDescriptor(preferredSurface).CancelBehavior
                    == CharacterTestUiSurfaceCancelBehavior.CloseTopSurface)
            {
                target = preferredSurface;
                return true;
            }

            if (TryGetFirstCancelable(openSurfaces & LargePlayerSurfaces, out target))
                return true;

            if (TryGetFirstCancelable(openSurfaces & CharacterTestUiSurfaceMask.StoryDialog, out target))
                return true;

            target = default;
            return false;
        }

        public static CharacterTestUiSurfaceDescriptor GetDescriptor(CharacterTestUiSurface surface)
        {
            switch (surface)
            {
                case CharacterTestUiSurface.CombatHud:
                    return new CharacterTestUiSurfaceDescriptor(
                        surface,
                        CharacterTestUiSurfaceKind.Hud,
                        CharacterTestUiSurfaceTraits.None,
                        CharacterTestUiSurfaceCancelBehavior.None);
                case CharacterTestUiSurface.FloatingCombatText:
                    return new CharacterTestUiSurfaceDescriptor(
                        surface,
                        CharacterTestUiSurfaceKind.LightOverlay,
                        CharacterTestUiSurfaceTraits.None,
                        CharacterTestUiSurfaceCancelBehavior.None);
                case CharacterTestUiSurface.Guidance:
                    return new CharacterTestUiSurfaceDescriptor(
                        surface,
                        CharacterTestUiSurfaceKind.LightOverlay,
                        CharacterTestUiSurfaceTraits.None,
                        CharacterTestUiSurfaceCancelBehavior.None);
                case CharacterTestUiSurface.MainMenu:
                    return new CharacterTestUiSurfaceDescriptor(
                        surface,
                        CharacterTestUiSurfaceKind.SystemMenu,
                        LargePanelTraits,
                        CharacterTestUiSurfaceCancelBehavior.CloseTopSurface);
                case CharacterTestUiSurface.NetworkPanel:
                    return new CharacterTestUiSurfaceDescriptor(
                        surface,
                        CharacterTestUiSurfaceKind.PrimaryPanel,
                        LargePanelTraits,
                        CharacterTestUiSurfaceCancelBehavior.CloseTopSurface);
                case CharacterTestUiSurface.SettingsPanel:
                    return new CharacterTestUiSurfaceDescriptor(
                        surface,
                        CharacterTestUiSurfaceKind.SystemMenu,
                        LargePanelTraits,
                        CharacterTestUiSurfaceCancelBehavior.CloseTopSurface);
                case CharacterTestUiSurface.AbilityBindingPanel:
                    return new CharacterTestUiSurfaceDescriptor(
                        surface,
                        CharacterTestUiSurfaceKind.PrimaryPanel,
                        LargePanelTraits,
                        CharacterTestUiSurfaceCancelBehavior.CloseTopSurface);
                case CharacterTestUiSurface.RuntimeAiBehaviorLoadoutPanel:
                    return new CharacterTestUiSurfaceDescriptor(
                        surface,
                        CharacterTestUiSurfaceKind.PrimaryPanel,
                        LargePanelTraits,
                        CharacterTestUiSurfaceCancelBehavior.CloseTopSurface);
                case CharacterTestUiSurface.TacticalPreparationPanel:
                    return new CharacterTestUiSurfaceDescriptor(
                        surface,
                        CharacterTestUiSurfaceKind.PrimaryPanel,
                        LargePanelTraits,
                        CharacterTestUiSurfaceCancelBehavior.CloseTopSurface);
                case CharacterTestUiSurface.ArenaSetupPanel:
                    return new CharacterTestUiSurfaceDescriptor(
                        surface,
                        CharacterTestUiSurfaceKind.PrimaryPanel,
                        LargePanelTraits,
                        CharacterTestUiSurfaceCancelBehavior.CloseTopSurface);
                case CharacterTestUiSurface.ArenaBehaviorGroupPanel:
                    return new CharacterTestUiSurfaceDescriptor(
                        surface,
                        CharacterTestUiSurfaceKind.TransactionalPanel,
                        LargePanelTraits,
                        CharacterTestUiSurfaceCancelBehavior.CloseTopSurface);
                case CharacterTestUiSurface.StoryDialog:
                    return new CharacterTestUiSurfaceDescriptor(
                        surface,
                        CharacterTestUiSurfaceKind.NarrativeModal,
                        CharacterTestUiSurfaceTraits.ReleasesCursor
                        | CharacterTestUiSurfaceTraits.ExclusiveUiInput
                        | CharacterTestUiSurfaceTraits.HidesHud
                        | CharacterTestUiSurfaceTraits.ClearsLightOverlay,
                        CharacterTestUiSurfaceCancelBehavior.None);
                default:
                    return new CharacterTestUiSurfaceDescriptor(
                        surface,
                        CharacterTestUiSurfaceKind.LightOverlay,
                        CharacterTestUiSurfaceTraits.None,
                        CharacterTestUiSurfaceCancelBehavior.None);
            }
        }

        public static CharacterTestUiSurfaceMask ToMask(CharacterTestUiSurface surface)
        {
            switch (surface)
            {
                case CharacterTestUiSurface.CombatHud:
                    return CharacterTestUiSurfaceMask.CombatHud;
                case CharacterTestUiSurface.FloatingCombatText:
                    return CharacterTestUiSurfaceMask.FloatingCombatText;
                case CharacterTestUiSurface.Guidance:
                    return CharacterTestUiSurfaceMask.Guidance;
                case CharacterTestUiSurface.MainMenu:
                    return CharacterTestUiSurfaceMask.MainMenu;
                case CharacterTestUiSurface.NetworkPanel:
                    return CharacterTestUiSurfaceMask.NetworkPanel;
                case CharacterTestUiSurface.SettingsPanel:
                    return CharacterTestUiSurfaceMask.SettingsPanel;
                case CharacterTestUiSurface.AbilityBindingPanel:
                    return CharacterTestUiSurfaceMask.AbilityBindingPanel;
                case CharacterTestUiSurface.RuntimeAiBehaviorLoadoutPanel:
                    return CharacterTestUiSurfaceMask.RuntimeAiBehaviorLoadoutPanel;
                case CharacterTestUiSurface.TacticalPreparationPanel:
                    return CharacterTestUiSurfaceMask.TacticalPreparationPanel;
                case CharacterTestUiSurface.ArenaSetupPanel:
                    return CharacterTestUiSurfaceMask.ArenaSetupPanel;
                case CharacterTestUiSurface.ArenaBehaviorGroupPanel:
                    return CharacterTestUiSurfaceMask.ArenaBehaviorGroupPanel;
                case CharacterTestUiSurface.StoryDialog:
                    return CharacterTestUiSurfaceMask.StoryDialog;
                default:
                    return CharacterTestUiSurfaceMask.None;
            }
        }

        private static bool IsExclusiveLargePanelKind(CharacterTestUiSurfaceKind kind)
        {
            return kind == CharacterTestUiSurfaceKind.PrimaryPanel
                || kind == CharacterTestUiSurfaceKind.TransactionalPanel
                || kind == CharacterTestUiSurfaceKind.SystemMenu;
        }

        private static CharacterTestUiSurfaceMask GetSurfaceSessionMask(CharacterTestUiSurface surface)
        {
            return ToMask(surface);
        }

        private static bool TryGetFirstCancelable(
            CharacterTestUiSurfaceMask openSurfaces,
            out CharacterTestUiSurface target)
        {
            if (TryGetCancelable(openSurfaces, CharacterTestUiSurface.ArenaBehaviorGroupPanel, out target))
                return true;
            if (TryGetCancelable(openSurfaces, CharacterTestUiSurface.ArenaSetupPanel, out target))
                return true;
            if (TryGetCancelable(openSurfaces, CharacterTestUiSurface.RuntimeAiBehaviorLoadoutPanel, out target))
                return true;
            if (TryGetCancelable(openSurfaces, CharacterTestUiSurface.TacticalPreparationPanel, out target))
                return true;
            if (TryGetCancelable(openSurfaces, CharacterTestUiSurface.AbilityBindingPanel, out target))
                return true;
            if (TryGetCancelable(openSurfaces, CharacterTestUiSurface.SettingsPanel, out target))
                return true;
            if (TryGetCancelable(openSurfaces, CharacterTestUiSurface.NetworkPanel, out target))
                return true;
            if (TryGetCancelable(openSurfaces, CharacterTestUiSurface.MainMenu, out target))
                return true;
            if (TryGetCancelable(openSurfaces, CharacterTestUiSurface.StoryDialog, out target))
                return true;

            target = default;
            return false;
        }

        private static bool TryGetCancelable(
            CharacterTestUiSurfaceMask openSurfaces,
            CharacterTestUiSurface surface,
            out CharacterTestUiSurface target)
        {
            if ((openSurfaces & ToMask(surface)) != 0
                && GetDescriptor(surface).CancelBehavior == CharacterTestUiSurfaceCancelBehavior.CloseTopSurface)
            {
                target = surface;
                return true;
            }

            target = default;
            return false;
        }

        private static bool ContainsTrait(
            CharacterTestUiSurfaceMask openSurfaces,
            CharacterTestUiSurfaceTraits trait)
        {
            return HasTrait(openSurfaces, CharacterTestUiSurface.CombatHud, trait)
                || HasTrait(openSurfaces, CharacterTestUiSurface.FloatingCombatText, trait)
                || HasTrait(openSurfaces, CharacterTestUiSurface.Guidance, trait)
                || HasTrait(openSurfaces, CharacterTestUiSurface.MainMenu, trait)
                || HasTrait(openSurfaces, CharacterTestUiSurface.NetworkPanel, trait)
                || HasTrait(openSurfaces, CharacterTestUiSurface.SettingsPanel, trait)
                || HasTrait(openSurfaces, CharacterTestUiSurface.AbilityBindingPanel, trait)
                || HasTrait(openSurfaces, CharacterTestUiSurface.RuntimeAiBehaviorLoadoutPanel, trait)
                || HasTrait(openSurfaces, CharacterTestUiSurface.TacticalPreparationPanel, trait)
                || HasTrait(openSurfaces, CharacterTestUiSurface.ArenaSetupPanel, trait)
                || HasTrait(openSurfaces, CharacterTestUiSurface.ArenaBehaviorGroupPanel, trait)
                || HasTrait(openSurfaces, CharacterTestUiSurface.StoryDialog, trait);
        }

        private static bool HasTrait(
            CharacterTestUiSurfaceMask openSurfaces,
            CharacterTestUiSurface surface,
            CharacterTestUiSurfaceTraits trait)
        {
            return (openSurfaces & ToMask(surface)) != 0
                && (GetDescriptor(surface).Traits & trait) != 0;
        }
    }

    public sealed class CharacterTestUiRuntimeOptions
    {
        public GameSlice Slice { get; set; }
        public CharacterTestStoryContent StoryContent { get; set; }
        public Func<bool> IsPausedProvider { get; set; }
        public bool EnableDebugUi { get; set; } = true;
        public PanelSettings DebugPanelSettings { get; set; }
        public bool EnableSkillPanel { get; set; }
        public PanelSettings SkillPanelSettings { get; set; }
        public DebugUiVisibility DebugInitialVisibility { get; set; } = DebugUiVisibility.Collapsed;
        public bool DebugShowOnStart { get; set; } = true;
        public int DebugInitialTabIndex { get; set; }
        public bool EnableFairyGuiCombatHud { get; set; } = true;
        public bool EnableFairyGuiFloatingCombatText { get; set; } = true;
        public bool EnableFairyGuiGuidance { get; set; }
        public bool EnableFairyGuiStoryUi { get; set; } = true;
        public bool EnableFairyGuiMainMenu { get; set; }
        public bool AutoOpenMainMenu { get; set; }
        public bool EnableFairyGuiNetworkPanel { get; set; } = true;
        public bool EnableFairyGuiSettingsPanel { get; set; } = true;
        public bool EnableFairyGuiAbilityBindingPanel { get; set; } = true;
        public bool EnableFairyGuiRuntimeAiBehaviorLoadoutPanel { get; set; } = true;
        public bool EnableFairyGuiArenaSetupPanel { get; set; }
        public bool AutoOpenFairyGuiArenaSetupPanel { get; set; }
        public bool EnableFairyGuiRuntimeAiPlannerDevPanel { get; set; }
        public bool AutoOpenFairyGuiRuntimeAiPlannerDevPanel { get; set; }
        public bool AutoOpenNetworkPanel { get; set; } = true;
        public ICharacterTestMainMenuController MainMenuController { get; set; }
        public ICharacterTestNetworkSessionController NetworkSessionController { get; set; }
        public ICharacterTestNetworkLobbyController NetworkLobbyController { get; set; }
        public Func<CharacterTestNetworkSessionStatus> NetworkSessionStatusProvider { get; set; }
        public CharacterTestSettingsRuntime SettingsRuntime { get; set; }
        public CharacterTestLocalizationRuntime LocalizationRuntime { get; set; }
        public IMxUiTextProvider TextProvider { get; set; }
        public Action NetworkSessionStarted { get; set; }
        public IInputProvider InputProvider { get; set; }
        public IMxCursorController CursorController { get; set; }
        public CharacterTestThirdPersonCameraRigBridge CameraRigBridge { get; set; }
        public CharacterTestArenaSessionController ArenaSessionController { get; set; }
        public CharacterTestRuntimeAiPlannerDevSceneController RuntimeAiPlannerDevSceneController { get; set; }
        internal CharacterTestRuntimeAiPlannerPlayerDeckWorkbench PlayerBehaviorDeckWorkbench { get; set; }
        internal CharacterTestRuntimeAiPlannerLocalPlayerDeckService PlayerBehaviorDeckService { get; set; }
    }

    /// <summary>
    /// Coordinates CharacterTest runtime UI surfaces without becoming another scene component.
    /// </summary>
    public sealed partial class CharacterTestUiRuntime
    {
        private CharacterTestFairyGuiCursor _semanticCursor;
        private static readonly ProfilerMarker MainMenuRefreshMarker =
            new ProfilerMarker("CharacterTest.UI.MainMenuRefresh");
        private static readonly ProfilerMarker NetworkPanelRefreshMarker =
            new ProfilerMarker("CharacterTest.UI.NetworkPanelRefresh");
        private static readonly ProfilerMarker SettingsPanelRefreshMarker =
            new ProfilerMarker("CharacterTest.UI.SettingsPanelRefresh");
        private static readonly ProfilerMarker AbilityPanelRefreshMarker =
            new ProfilerMarker("CharacterTest.UI.AbilityPanelRefresh");
        private static readonly ProfilerMarker RuntimeAiBehaviorPanelRefreshMarker =
            new ProfilerMarker("CharacterTest.UI.RuntimeAiBehaviorPanelRefresh");
        private static readonly ProfilerMarker ArenaSetupPanelRefreshMarker =
            new ProfilerMarker("CharacterTest.UI.ArenaSetupPanelRefresh");
        private static readonly ProfilerMarker ArenaBehaviorGroupPanelRefreshMarker =
            new ProfilerMarker("CharacterTest.UI.ArenaBehaviorGroupPanelRefresh");
        private static readonly ProfilerMarker RuntimeAiPlannerDevPanelRefreshMarker =
            new ProfilerMarker("CharacterTest.UI.RuntimeAiPlannerDevPanelRefresh");
        private static readonly ProfilerMarker CombatHudRefreshMarker =
            new ProfilerMarker("CharacterTest.UI.CombatHudRefresh");
        private static readonly ProfilerMarker FloatingTextRefreshMarker =
            new ProfilerMarker("CharacterTest.UI.FloatingTextRefresh");
        private static readonly ProfilerMarker StoryDialogRefreshMarker =
            new ProfilerMarker("CharacterTest.UI.StoryDialogRefresh");
        private static readonly ProfilerMarker FloatingTextTickMarker =
            new ProfilerMarker("CharacterTest.UI.FloatingTextTick");
        private static readonly ProfilerMarker GuidanceRefreshMarker =
            new ProfilerMarker("CharacterTest.UI.GuidanceRefresh");
        private static readonly ProfilerMarker GuidanceTickMarker =
            new ProfilerMarker("CharacterTest.UI.GuidanceTick");
        private static readonly ProfilerMarker PlayerUiInputMarker =
            new ProfilerMarker("CharacterTest.UI.PlayerInput");
        private const int CombatHudRefreshIntervalFrames = 6;
        private const int ModalPanelRefreshIntervalFrames = 6;

        private readonly MonoBehaviour _owner;
        private readonly IRuntimeLogger _logger;
        private CharacterTestDebugBootstrap _debugBootstrap;
        private CharacterTestSkillPanel _skillPanel;
        private GameObject _skillPanelObject;
        private MxFairyGuiLayerHost _fairyGuiLayerHost;
        private MxFairyGuiHost _fairyGuiHost;
        private MxFairyGuiRuntimeShell _fairyGuiRuntimeShell;
        private MxFairyGuiPopupService _fairyGuiPopupService;
        private CharacterTestFairyGuiCombatHudHost _fairyGuiCombatHudHost;
        private CharacterTestFairyGuiFloatingCombatTextHost _fairyGuiFloatingCombatTextHost;
        private CharacterTestFairyGuiGuidanceHost _fairyGuiGuidanceHost;
        private CharacterTestFairyGuiStoryDialogHost _fairyGuiStoryDialogHost;
        private CharacterTestFairyGuiMainMenuHost _fairyGuiMainMenuHost;
        private CharacterTestFairyGuiNetworkPanelHost _fairyGuiNetworkPanelHost;
        private CharacterTestFairyGuiSettingsPanelHost _fairyGuiSettingsPanelHost;
        private CharacterTestFairyGuiAbilityBindingPanelHost _fairyGuiAbilityBindingPanelHost;
        private CharacterTestFairyGuiBehaviorDeckPanelHost _fairyGuiBehaviorDeckPanelHost;
        private CharacterTestFairyGuiTacticalDeckPanelHost _fairyGuiTacticalDeckPanelHost;
        private CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelHost _fairyGuiRuntimeAiBehaviorLoadoutPanelHost;
        private CharacterTestFairyGuiArenaSetupPanelHost _fairyGuiArenaSetupPanelHost;
        private CharacterTestFairyGuiArenaBehaviorGroupPanelHost _fairyGuiArenaBehaviorGroupPanelHost;
        private CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost _fairyGuiRuntimeAiPlannerDevPanelHost;
        private CharacterTestRuntimeAiPlannerDevSceneController _runtimeAiPlannerDevSceneController;
        private CharacterTestRuntimeAiLabShellCoordinator _runtimeAiLabShellCoordinator;
        private RuntimeAiLabShellMountAdapter _runtimeAiLabShellMountAdapter;
        private CharacterTestRuntimeAiPlannerLocalPlayerDeckService _playerBehaviorDeckService;
        private CharacterTestArenaSessionController _arenaSessionController;
        private string _lastFairyGuiSettingsPanelStatus = string.Empty;
        private string _lastFairyGuiAbilityBindingPanelStatus = string.Empty;
        private string _lastFairyGuiRuntimeAiBehaviorLoadoutPanelStatus = string.Empty;
        private string _lastFairyGuiArenaSetupPanelStatus = string.Empty;
        private string _lastFairyGuiArenaBehaviorGroupPanelStatus = string.Empty;
        private string _lastFairyGuiRuntimeAiPlannerDevPanelStatus = string.Empty;
        private IMxFairyGuiInputContextBridge _fairyGuiPlayerInputBridge;
        private IInputProvider _playerUiInputProvider;
        private MxFairyGuiFocusInputBridge _fairyGuiFocusInputBridge;
        private readonly List<InputCommand> _playerUiInputCommands = new List<InputCommand>(8);
        private readonly Predicate<InputCommand> _playerUiInputCommandPredicate;
        private readonly List<InputCommand> _guidanceInputCommands = new List<InputCommand>(2);
        private readonly Predicate<InputCommand> _guidanceInputCommandPredicate;
        private long _lastProcessedPlayerUiInputSequence;
        private long _lastPlayerUiInputQueueFrame;
        private long _lastPlayerUiInputQueueGeneration;
        private bool _hasProcessedPlayerUiInputSequence;
        private int _lastPlayerUiNavigateDirection;
        private int _lastPlayerModalInputFrame = int.MinValue;
        private int _lastPlayerUiCancelFrame = int.MinValue;
        private long _lastProcessedGuidanceInputSequence;
        private long _lastGuidanceInputQueueFrame;
        private long _lastGuidanceInputQueueGeneration;
        private bool _hasProcessedGuidanceInputSequence;
        private readonly List<MxFairyGuiPackageDescriptor> _fairyGuiPackagePreloadDescriptors =
            new List<MxFairyGuiPackageDescriptor>(8);
        private readonly List<string> _preloadedFairyGuiPackageIds = new List<string>(2);
        private int _lastMainMenuRefreshFrame = int.MinValue;
        private int _lastNetworkPanelRefreshFrame = int.MinValue;
        private int _lastSettingsPanelRefreshFrame = int.MinValue;
        private int _lastAbilityPanelRefreshFrame = int.MinValue;
        private int _lastRuntimeAiBehaviorPanelRefreshFrame = int.MinValue;
        private int _lastArenaSetupPanelRefreshFrame = int.MinValue;
        private int _lastArenaBehaviorGroupPanelRefreshFrame = int.MinValue;
        private int _lastRuntimeAiPlannerDevPanelRefreshFrame = int.MinValue;
        private int _lastCombatHudRefreshFrame = int.MinValue;
        private bool _lastPlayerModalOpen;
        private bool _settingsPanelReturnToMainMenu;
        private bool _abilityPanelReturnToMainMenu;
        private bool _behaviorDeckPanelReturnToMainMenu;
        private int _fairyGuiPackagePreloadAttemptCount;
        private int _fairyGuiPackagePreloadSuccessCount;
        private string _lastFairyGuiPackagePreloadStatus = string.Empty;
        private bool _configured;

        public CharacterTestUiRuntime(MonoBehaviour owner, IRuntimeLogger logger)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _logger = logger ?? NullRuntimeLogger.Instance;
            _playerUiInputCommandPredicate = IsUnprocessedPlayerUiInputCommand;
            _guidanceInputCommandPredicate = IsUnprocessedGuidanceInputCommand;
        }

        public bool IsConfigured => _configured;
        public CharacterTestDebugBootstrap DebugBootstrap => _debugBootstrap;
        public CharacterTestFairyGuiCombatHudHost FairyGuiCombatHudHost => _fairyGuiCombatHudHost;
        public CharacterTestFairyGuiFloatingCombatTextHost FairyGuiFloatingCombatTextHost =>
            _fairyGuiFloatingCombatTextHost;
        public CharacterTestFairyGuiGuidanceHost FairyGuiGuidanceHost => _fairyGuiGuidanceHost;
        public CharacterTestFairyGuiStoryDialogHost FairyGuiStoryDialogHost => _fairyGuiStoryDialogHost;
        public CharacterTestFairyGuiMainMenuHost FairyGuiMainMenuHost => _fairyGuiMainMenuHost;
        public CharacterTestFairyGuiNetworkPanelHost FairyGuiNetworkPanelHost => _fairyGuiNetworkPanelHost;
        public CharacterTestFairyGuiSettingsPanelHost FairyGuiSettingsPanelHost => _fairyGuiSettingsPanelHost;
        public CharacterTestFairyGuiAbilityBindingPanelHost FairyGuiAbilityBindingPanelHost =>
            _fairyGuiAbilityBindingPanelHost;
        public CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelHost
            FairyGuiRuntimeAiBehaviorLoadoutPanelHost => _fairyGuiRuntimeAiBehaviorLoadoutPanelHost;
        public CharacterTestFairyGuiBehaviorDeckPanelHost FairyGuiBehaviorDeckPanelHost =>
            _fairyGuiBehaviorDeckPanelHost;
        public CharacterTestFairyGuiTacticalDeckPanelHost FairyGuiTacticalDeckPanelHost =>
            _fairyGuiTacticalDeckPanelHost;
        public CharacterTestFairyGuiArenaSetupPanelHost FairyGuiArenaSetupPanelHost =>
            _fairyGuiArenaSetupPanelHost;
        public CharacterTestFairyGuiArenaBehaviorGroupPanelHost FairyGuiArenaBehaviorGroupPanelHost =>
            _fairyGuiArenaBehaviorGroupPanelHost;
        public CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost FairyGuiRuntimeAiPlannerDevPanelHost =>
            _fairyGuiRuntimeAiPlannerDevPanelHost;
        public bool IsMainMenuOpen => _fairyGuiMainMenuHost != null && _fairyGuiMainMenuHost.IsOpen;
        public bool IsNetworkPanelOpen => _fairyGuiNetworkPanelHost != null && _fairyGuiNetworkPanelHost.IsOpen;
        internal int FairyGuiRegisteredViewCount =>
            _fairyGuiRuntimeShell != null ? _fairyGuiRuntimeShell.Catalog.ViewCount : 0;
        public bool HasNonMainMenuPlayerModalOpen =>
            HasTacticalLoading || CharacterTestUiSurfacePolicy.HasNonMainMenuPlayerModalOpen(CaptureSurfaceState());
        // Modal input ownership survives closing the view until the next rendered frame.
        internal bool BlocksTacticalBattleInput => HasTacticalLoading
            || (_fairyGuiLayerHost != null && _fairyGuiLayerHost.TryGetTopModal(out _))
            || _lastPlayerModalInputFrame == Time.frameCount;
        internal bool OwnsTacticalCancelInput => BlocksTacticalBattleInput
            || _fairyGuiPopupService?.HasOpenPopup == true
            || _lastPlayerUiCancelFrame == Time.frameCount;
        public bool IsFairyGuiCombatHudConfigured =>
            _fairyGuiCombatHudHost != null && _fairyGuiCombatHudHost.IsConfigured;
        public string LastFairyGuiCombatHudStatus =>
            _fairyGuiCombatHudHost != null ? _fairyGuiCombatHudHost.LastStatusMessage : string.Empty;
        public bool IsFairyGuiFloatingCombatTextConfigured =>
            _fairyGuiFloatingCombatTextHost != null && _fairyGuiFloatingCombatTextHost.IsConfigured;
        public string LastFairyGuiFloatingCombatTextStatus =>
            _fairyGuiFloatingCombatTextHost != null
                ? _fairyGuiFloatingCombatTextHost.LastStatusMessage
                : string.Empty;
        public bool IsFairyGuiGuidanceConfigured =>
            _fairyGuiGuidanceHost != null && _fairyGuiGuidanceHost.IsConfigured;
        public string LastFairyGuiGuidanceStatus =>
            _fairyGuiGuidanceHost != null ? _fairyGuiGuidanceHost.LastStatusMessage : string.Empty;
        public bool IsFairyGuiStoryUiConfigured =>
            _fairyGuiStoryDialogHost != null && _fairyGuiStoryDialogHost.IsConfigured;
        public string LastFairyGuiStoryUiStatus =>
            _fairyGuiStoryDialogHost != null ? _fairyGuiStoryDialogHost.LastStatusMessage : string.Empty;
        public bool IsFairyGuiMainMenuConfigured =>
            _fairyGuiMainMenuHost != null && _fairyGuiMainMenuHost.IsConfigured;
        public string LastFairyGuiMainMenuStatus =>
            _fairyGuiMainMenuHost != null ? _fairyGuiMainMenuHost.LastStatusMessage : string.Empty;
        public bool IsFairyGuiNetworkPanelConfigured =>
            _fairyGuiNetworkPanelHost != null && _fairyGuiNetworkPanelHost.IsConfigured;
        public string LastFairyGuiNetworkPanelStatus =>
            _fairyGuiNetworkPanelHost != null ? _fairyGuiNetworkPanelHost.LastStatusMessage : string.Empty;
        public bool IsFairyGuiSettingsPanelConfigured =>
            _fairyGuiSettingsPanelHost != null && _fairyGuiSettingsPanelHost.IsConfigured;
        public string LastFairyGuiSettingsPanelStatus =>
            _fairyGuiSettingsPanelHost != null
                ? _fairyGuiSettingsPanelHost.LastStatusMessage
                : _lastFairyGuiSettingsPanelStatus;
        public bool IsFairyGuiAbilityBindingPanelConfigured =>
            _fairyGuiAbilityBindingPanelHost != null && _fairyGuiAbilityBindingPanelHost.IsConfigured;
        public string LastFairyGuiAbilityBindingPanelStatus =>
            _fairyGuiAbilityBindingPanelHost != null
                ? _fairyGuiAbilityBindingPanelHost.LastStatusMessage
                : _lastFairyGuiAbilityBindingPanelStatus;
        public bool IsFairyGuiRuntimeAiBehaviorLoadoutPanelConfigured =>
            (_fairyGuiBehaviorDeckPanelHost != null && _fairyGuiBehaviorDeckPanelHost.IsConfigured)
            || (_fairyGuiRuntimeAiBehaviorLoadoutPanelHost != null
                && _fairyGuiRuntimeAiBehaviorLoadoutPanelHost.IsConfigured);
        public string LastFairyGuiRuntimeAiBehaviorLoadoutPanelStatus =>
            _fairyGuiBehaviorDeckPanelHost != null
                ? _fairyGuiBehaviorDeckPanelHost.LastStatusMessage
                : _fairyGuiRuntimeAiBehaviorLoadoutPanelHost != null
                    ? _fairyGuiRuntimeAiBehaviorLoadoutPanelHost.LastStatusMessage
                    : _lastFairyGuiRuntimeAiBehaviorLoadoutPanelStatus;
        public bool IsFairyGuiArenaSetupPanelConfigured =>
            _fairyGuiArenaSetupPanelHost != null
            && _fairyGuiArenaSetupPanelHost.IsConfigured;
        public string LastFairyGuiArenaSetupPanelStatus =>
            _fairyGuiArenaSetupPanelHost != null
                ? _fairyGuiArenaSetupPanelHost.LastStatusMessage
                : _lastFairyGuiArenaSetupPanelStatus;
        public bool IsFairyGuiArenaBehaviorGroupPanelConfigured =>
            _fairyGuiArenaBehaviorGroupPanelHost != null
            && _fairyGuiArenaBehaviorGroupPanelHost.IsConfigured;
        public string LastFairyGuiArenaBehaviorGroupPanelStatus =>
            _fairyGuiArenaBehaviorGroupPanelHost != null
                ? _fairyGuiArenaBehaviorGroupPanelHost.LastStatusMessage
                : _lastFairyGuiArenaBehaviorGroupPanelStatus;
        public bool IsFairyGuiRuntimeAiPlannerDevPanelConfigured =>
            _fairyGuiRuntimeAiPlannerDevPanelHost != null
            && _fairyGuiRuntimeAiPlannerDevPanelHost.IsConfigured;
        public string LastFairyGuiRuntimeAiPlannerDevPanelStatus =>
            _fairyGuiRuntimeAiPlannerDevPanelHost != null
                ? _fairyGuiRuntimeAiPlannerDevPanelHost.LastStatusMessage
                : _lastFairyGuiRuntimeAiPlannerDevPanelStatus;
        internal string RuntimeAiLabAuthoringNavigationStatus { get; private set; } = string.Empty;
        internal int FairyGuiPackagePreloadAttemptCountForTests => _fairyGuiPackagePreloadAttemptCount;
        internal int FairyGuiPackagePreloadSuccessCountForTests => _fairyGuiPackagePreloadSuccessCount;
        internal int FairyGuiPreloadedPackageCountForTests => _preloadedFairyGuiPackageIds.Count;
        internal string LastFairyGuiPackagePreloadStatusForTests => _lastFairyGuiPackagePreloadStatus;

        /// <remarks>AllocByDesign: diagnostic snapshot creation allocates and should not be called from a per-frame hot path.</remarks>
        public FrameworkPerformanceCounterSnapshot CreatePerformanceCounterSnapshot()
        {
            return CharacterTestUiPerformanceCounterSource.Capture(this);
        }

        /// <remarks>LowFreqAlloc: registers a debug source object for explicit diagnostics/report capture.</remarks>
        public bool RegisterPerformanceDebugSource(FrameworkDebugSourceRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            return registry.Register(new FrameworkPerformanceCounterDebugSource(
                CreatePerformanceCounterSnapshot,
                "CharacterTestUiPerformance"));
        }

        public void Configure(CharacterTestUiRuntimeOptions options)
        {
            using var startupProfile = CharacterTestStartupProfiler.UiConfigure.Auto();
            Release();
            options = options ?? new CharacterTestUiRuntimeOptions();
            if (options.Slice == null)
                return;

            CharacterTestSliceServices services = options.Slice.Services;
            _runtimeAiPlannerDevSceneController = options.RuntimeAiPlannerDevSceneController;
            _playerBehaviorDeckService = options.PlayerBehaviorDeckService;
            IMxUiTextProvider textProvider = options.TextProvider;
            if (textProvider == null && options.LocalizationRuntime != null)
                textProvider = options.LocalizationRuntime.TextProvider;
            if (textProvider == null)
                textProvider = MxUiNullTextProvider.Instance;

            _debugBootstrap = new CharacterTestDebugBootstrap(_owner, _logger);
            _arenaSessionController = options.ArenaSessionController;
            _debugBootstrap.RegisterStoryDebugTarget(options.Slice);
            _debugBootstrap.RegisterRuntimeAiPlannerDebugTarget(
                options.Slice,
                () => options.Slice.Services.RuntimeCommands != null
                    && options.Slice.Services.RuntimeCommands.RuntimeAiBehaviorSelectionUnlocked,
                unlocked => options.Slice.Services.RuntimeCommands?.SetRuntimeAiBehaviorSelectionUnlocked(unlocked),
                OpenRuntimeAiBehaviorLoadoutPanel,
                CloseRuntimeAiBehaviorLoadoutPanel,
                () => (_fairyGuiBehaviorDeckPanelHost != null && _fairyGuiBehaviorDeckPanelHost.IsOpen)
                    || (_fairyGuiRuntimeAiBehaviorLoadoutPanelHost != null
                        && _fairyGuiRuntimeAiBehaviorLoadoutPanelHost.IsOpen),
                () => LastFairyGuiRuntimeAiBehaviorLoadoutPanelStatus);
            if (options.EnableDebugUi)
            {
                _debugBootstrap.ConfigureDebugUi(
                    options.Slice,
                    options.IsPausedProvider,
                    options.DebugPanelSettings,
                    options.DebugInitialVisibility,
                    options.DebugShowOnStart,
                    options.DebugInitialTabIndex);
            }

            if (options.EnableSkillPanel)
            {
                _skillPanelObject = new GameObject("CharacterTest Skill Panel");
                _skillPanelObject.transform.SetParent(_owner.transform, false);
                _skillPanel = _skillPanelObject.AddComponent<CharacterTestSkillPanel>();

                _skillPanel.Configure(
                    services,
                    options.SkillPanelSettings,
                    _logger);
            }

            if (options.EnableFairyGuiCombatHud
                || options.EnableFairyGuiFloatingCombatText
                || options.EnableFairyGuiGuidance
                || options.EnableFairyGuiStoryUi
                || options.EnableFairyGuiMainMenu
                || options.EnableFairyGuiNetworkPanel
                || options.EnableFairyGuiSettingsPanel
                || options.EnableFairyGuiAbilityBindingPanel
                || options.EnableFairyGuiRuntimeAiBehaviorLoadoutPanel
                || options.EnableFairyGuiArenaSetupPanel
                || options.EnableFairyGuiRuntimeAiPlannerDevPanel)
            {
                _fairyGuiLayerHost = CharacterTestFairyGuiLayerRoots.SharedLayerHost;
                CharacterTestFairyGuiLayerRoots.EnsureLayerRoots(_fairyGuiLayerHost);
                _fairyGuiHost = new MxFairyGuiHost();
                _semanticCursor = _owner.GetComponent<CharacterTestFairyGuiCursor>();
                if (_semanticCursor == null) _semanticCursor = _owner.gameObject.AddComponent<CharacterTestFairyGuiCursor>();
                _semanticCursor.enabled = true;
                PreloadFairyGuiPackages(options, services);
                CharacterTestFairyGuiHeadPad.PrewarmPool(CharacterTestFairyGuiHeadPad.DefaultPrewarmCount);
            }

            _fairyGuiPlayerInputBridge = CreatePlayerInputBridge(options.InputProvider, options.CursorController);
            _playerUiInputProvider = options.InputProvider;
            if (_fairyGuiLayerHost != null && _playerUiInputProvider != null)
            {
                _fairyGuiPopupService = new MxFairyGuiPopupService();
                var cancelSink = new CharacterTestPlayerUiCancelCommandSink(this);
                _fairyGuiFocusInputBridge = new MxFairyGuiFocusInputBridge(
                    _fairyGuiLayerHost,
                    new MxFairyGuiCancelCommandBridge(_fairyGuiLayerHost, cancelSink),
                    _fairyGuiPopupService);
            }
            else if (_fairyGuiLayerHost != null)
            {
                _fairyGuiPopupService = new MxFairyGuiPopupService();
            }

            if (_fairyGuiLayerHost != null && options.Slice.Resources?.ResourceManager != null)
            {
                _fairyGuiRuntimeShell = new MxFairyGuiRuntimeShell(
                    options.Slice.Resources.ResourceManager,
                    _fairyGuiHost,
                    _fairyGuiLayerHost,
                    _fairyGuiPlayerInputBridge,
                    popupService: _fairyGuiPopupService);
            }

            if (options.EnableFairyGuiGuidance)
            {
                _fairyGuiGuidanceHost = new CharacterTestFairyGuiGuidanceHost();
                _fairyGuiGuidanceHost.ConfigureFromSceneCache(
                    (_owner as GameManager)?.GuidanceSceneCache,
                    options.Slice,
                    _fairyGuiRuntimeShell,
                    _logger,
                    textProvider,
                    (options.InputProvider as InputService)?.BindingDisplay);
                _debugBootstrap.ConfigureGuidanceSelection(
                    () => _fairyGuiGuidanceHost != null ? _fairyGuiGuidanceHost.SelectionSnapshot : default,
                    testId => _fairyGuiGuidanceHost.SelectTest(testId));
            }

            if (options.EnableFairyGuiSettingsPanel && options.SettingsRuntime != null)
            {
                _fairyGuiSettingsPanelHost = _owner.GetComponent<CharacterTestFairyGuiSettingsPanelHost>();
                if (_fairyGuiSettingsPanelHost == null)
                    _fairyGuiSettingsPanelHost = _owner.gameObject.AddComponent<CharacterTestFairyGuiSettingsPanelHost>();

                _fairyGuiSettingsPanelHost.Configure(
                    options.Slice,
                    options.SettingsRuntime,
                    options.LocalizationRuntime,
                    textProvider,
                    _logger,
                    _fairyGuiLayerHost,
                    _fairyGuiPlayerInputBridge,
                    _fairyGuiHost,
                    CloseSettingsPanelFromCommand,
                    _fairyGuiRuntimeShell);
            }
            else if (options.EnableFairyGuiSettingsPanel)
            {
                _lastFairyGuiSettingsPanelStatus =
                    "Settings panel disabled because settings runtime is not available.";
                _logger.Warning("CharacterTest.FairyGUI", _lastFairyGuiSettingsPanelStatus);
            }

            Action toggleSettingsPanel =
                _fairyGuiSettingsPanelHost != null && _fairyGuiSettingsPanelHost.IsConfigured
                    ? OpenSettingsPanelFromMainMenu
                    : null;

            if (options.EnableFairyGuiAbilityBindingPanel && services.Modules.AbilityHotbar != null)
            {
                _fairyGuiAbilityBindingPanelHost =
                    _owner.GetComponent<CharacterTestFairyGuiAbilityBindingPanelHost>();
                if (_fairyGuiAbilityBindingPanelHost == null)
                {
                    _fairyGuiAbilityBindingPanelHost =
                        _owner.gameObject.AddComponent<CharacterTestFairyGuiAbilityBindingPanelHost>();
                }

                _fairyGuiAbilityBindingPanelHost.Configure(
                    options.Slice,
                    services.Modules.AbilityHotbar,
                    _logger,
                    _fairyGuiLayerHost,
                    textProvider,
                    _fairyGuiPlayerInputBridge,
                    _fairyGuiHost,
                    CloseAbilityBindingPanelFromCommand,
                    _fairyGuiRuntimeShell);
            }
            else if (options.EnableFairyGuiAbilityBindingPanel)
            {
                _lastFairyGuiAbilityBindingPanelStatus =
                    "Ability binding panel disabled because hotbar runtime is not available.";
                _logger.Warning("CharacterTest.FairyGUI", _lastFairyGuiAbilityBindingPanelStatus);
            }

            Action toggleAbilitiesPanel =
                _fairyGuiAbilityBindingPanelHost != null && _fairyGuiAbilityBindingPanelHost.IsConfigured
                    ? OpenAbilityBindingPanelFromMainMenu
                    : null;

            if (options.EnableFairyGuiRuntimeAiBehaviorLoadoutPanel)
            {
                if (options.PlayerBehaviorDeckService != null)
                {
                    _fairyGuiBehaviorDeckPanelHost =
                        _owner.GetComponent<CharacterTestFairyGuiBehaviorDeckPanelHost>();
                    if (_fairyGuiBehaviorDeckPanelHost == null)
                    {
                        _fairyGuiBehaviorDeckPanelHost =
                            _owner.gameObject.AddComponent<CharacterTestFairyGuiBehaviorDeckPanelHost>();
                    }

                    _fairyGuiBehaviorDeckPanelHost.Configure(
                        options.Slice,
                        options.PlayerBehaviorDeckService,
                        _logger,
                        _fairyGuiLayerHost,
                        textProvider,
                        _fairyGuiPlayerInputBridge,
                        _fairyGuiHost,
                        CloseRuntimeAiBehaviorLoadoutPanel,
                        options.RuntimeAiPlannerDevSceneController != null
                            ? OpenPublishedBehaviorCardInLab
                            : null,
                        options.RuntimeAiPlannerDevSceneController != null
                            ? ContinueRuntimeAiLabToSetup
                            : null,
                        options.RuntimeAiPlannerDevSceneController != null
                            ? UpdateRuntimeAiLabPlayerDeckContext
                            : null,
                        _fairyGuiRuntimeShell,
                        hasActiveRuntimeAiLabSession: options.RuntimeAiPlannerDevSceneController != null
                            ? HasRetainedRuntimeAiLabSession
                            : null);
                }
                else
                {
                    _fairyGuiRuntimeAiBehaviorLoadoutPanelHost =
                        _owner.GetComponent<CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelHost>();
                    if (_fairyGuiRuntimeAiBehaviorLoadoutPanelHost == null)
                    {
                        _fairyGuiRuntimeAiBehaviorLoadoutPanelHost =
                            _owner.gameObject.AddComponent<CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelHost>();
                    }

                    _fairyGuiRuntimeAiBehaviorLoadoutPanelHost.Configure(
                        options.Slice,
                        _logger,
                        _fairyGuiLayerHost,
                        textProvider,
                        _fairyGuiPlayerInputBridge,
                        _fairyGuiHost,
                        _fairyGuiRuntimeShell);
                }
            }

            _fairyGuiTacticalDeckPanelHost =
                _owner.GetComponent<CharacterTestFairyGuiTacticalDeckPanelHost>();
            if (_fairyGuiTacticalDeckPanelHost == null)
                _fairyGuiTacticalDeckPanelHost =
                    _owner.gameObject.AddComponent<CharacterTestFairyGuiTacticalDeckPanelHost>();
            _fairyGuiTacticalDeckPanelHost.Configure(
                options.Slice,
                _logger,
                _fairyGuiLayerHost,
                _fairyGuiPlayerInputBridge,
                _fairyGuiHost,
                _fairyGuiRuntimeShell);

            if (options.EnableFairyGuiArenaSetupPanel && options.ArenaSessionController != null)
            {
                _fairyGuiArenaBehaviorGroupPanelHost =
                    _owner.GetComponent<CharacterTestFairyGuiArenaBehaviorGroupPanelHost>();
                if (_fairyGuiArenaBehaviorGroupPanelHost == null)
                {
                    _fairyGuiArenaBehaviorGroupPanelHost =
                        _owner.gameObject.AddComponent<CharacterTestFairyGuiArenaBehaviorGroupPanelHost>();
                }

                _fairyGuiArenaBehaviorGroupPanelHost.Configure(
                    options.Slice,
                    options.ArenaSessionController,
                    _logger,
                    _fairyGuiLayerHost,
                    textProvider,
                    _fairyGuiPlayerInputBridge,
                    _fairyGuiHost,
                    () => OpenArenaSetupPanel(),
                    _fairyGuiRuntimeShell);

                _fairyGuiArenaSetupPanelHost =
                    _owner.GetComponent<CharacterTestFairyGuiArenaSetupPanelHost>();
                if (_fairyGuiArenaSetupPanelHost == null)
                    _fairyGuiArenaSetupPanelHost = _owner.gameObject.AddComponent<CharacterTestFairyGuiArenaSetupPanelHost>();

                _fairyGuiArenaSetupPanelHost.Configure(
                    options.Slice,
                    options.ArenaSessionController,
                    _logger,
                    _fairyGuiLayerHost,
                    textProvider,
                    _fairyGuiPlayerInputBridge,
                    _fairyGuiHost,
                    options.AutoOpenFairyGuiArenaSetupPanel,
                    OpenArenaBehaviorGroupPanelForSlot,
                    _fairyGuiRuntimeShell);
            }
            else if (options.EnableFairyGuiArenaSetupPanel)
            {
                _lastFairyGuiArenaSetupPanelStatus =
                    "Arena setup panel disabled because arena controller is not available.";
                _logger.Warning("CharacterTest.FairyGUI", _lastFairyGuiArenaSetupPanelStatus);
            }

            if (options.EnableFairyGuiRuntimeAiPlannerDevPanel
                && options.RuntimeAiPlannerDevSceneController != null)
            {
                options.RuntimeAiPlannerDevSceneController.ConfigurePlayerLegalitySource(
                    options.PlayerBehaviorDeckService);
                options.RuntimeAiPlannerDevSceneController.ConfigurePublishSource(
                    options.PlayerBehaviorDeckService);
                _fairyGuiRuntimeAiPlannerDevPanelHost =
                    _owner.GetComponent<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
                if (_fairyGuiRuntimeAiPlannerDevPanelHost == null)
                {
                    _fairyGuiRuntimeAiPlannerDevPanelHost =
                        _owner.gameObject.AddComponent<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
                }

                _fairyGuiRuntimeAiPlannerDevPanelHost.Configure(
                    options.Slice,
                    options.RuntimeAiPlannerDevSceneController,
                    _logger,
                    _fairyGuiLayerHost,
                    textProvider,
                    _fairyGuiPlayerInputBridge,
                    _fairyGuiHost,
                    autoOpen: false,
                    runtimeShell: _fairyGuiRuntimeShell,
                    popupService: _fairyGuiPopupService);
                _fairyGuiRuntimeAiPlannerDevPanelHost.ConfigureBehaviorCardLibrary(
                    options.PlayerBehaviorDeckService,
                    OpenPublishedBehaviorCardInLab);
                options.RuntimeAiPlannerDevSceneController.ConfigureCardTrialSurface(options.Slice,
                    _fairyGuiLayerHost, _fairyGuiPlayerInputBridge, _fairyGuiHost, _fairyGuiRuntimeShell, _fairyGuiPopupService);
            }
            else if (options.EnableFairyGuiRuntimeAiPlannerDevPanel)
            {
                _lastFairyGuiRuntimeAiPlannerDevPanelStatus =
                    "Runtime AI Planner dev panel disabled because dev scene controller is not available.";
                _logger.Warning("CharacterTest.FairyGUI", _lastFairyGuiRuntimeAiPlannerDevPanelStatus);
            }

            if (options.RuntimeAiPlannerDevSceneController != null)
                ConfigureRuntimeAiLabShell();

            if (options.EnableFairyGuiMainMenu && options.MainMenuController != null)
            {
                _fairyGuiMainMenuHost = _owner.GetComponent<CharacterTestFairyGuiMainMenuHost>();
                if (_fairyGuiMainMenuHost == null)
                    _fairyGuiMainMenuHost = _owner.gameObject.AddComponent<CharacterTestFairyGuiMainMenuHost>();

                _fairyGuiMainMenuHost.Configure(
                    options.Slice,
                    options.MainMenuController,
                    _logger,
                    _fairyGuiLayerHost,
                    options.AutoOpenMainMenu,
                    _fairyGuiPlayerInputBridge,
                    toggleSettingsPanel,
                    textProvider,
                    _fairyGuiHost,
                    toggleAbilitiesPanel,
                    _fairyGuiRuntimeShell,
                    _fairyGuiBehaviorDeckPanelHost != null
                        && _fairyGuiBehaviorDeckPanelHost.IsConfigured
                            ? (Action)OpenBehaviorDeckPanelFromMainMenu
                            : null);
            }

            if (options.EnableFairyGuiMainMenu && options.MainMenuController != null)
                ConfigureTacticalLoading(options.Slice, textProvider);

            if (options.EnableFairyGuiNetworkPanel && options.NetworkSessionController != null)
            {
                _fairyGuiNetworkPanelHost = _owner.GetComponent<CharacterTestFairyGuiNetworkPanelHost>();
                if (_fairyGuiNetworkPanelHost == null)
                    _fairyGuiNetworkPanelHost = _owner.gameObject.AddComponent<CharacterTestFairyGuiNetworkPanelHost>();

                _fairyGuiNetworkPanelHost.Configure(
                    options.Slice,
                    options.NetworkSessionController,
                    options.NetworkLobbyController,
                    _logger,
                    _fairyGuiLayerHost,
                    options.AutoOpenNetworkPanel,
                    OpenMainMenuFromNetworkPanel,
                    options.NetworkSessionStarted,
                    _fairyGuiPlayerInputBridge,
                    textProvider,
                    _fairyGuiHost,
                    _fairyGuiRuntimeShell);
            }

            if (options.EnableFairyGuiCombatHud)
            {
                _fairyGuiCombatHudHost = _owner.GetComponent<CharacterTestFairyGuiCombatHudHost>();
                if (_fairyGuiCombatHudHost == null)
                    _fairyGuiCombatHudHost = _owner.gameObject.AddComponent<CharacterTestFairyGuiCombatHudHost>();

                _fairyGuiCombatHudHost.Configure(
                    options.Slice,
                    _logger,
                    _fairyGuiLayerHost,
                    textProvider,
                    _fairyGuiHost,
                    options.NetworkSessionStatusProvider,
                    _fairyGuiRuntimeShell);
                _fairyGuiCombatHudHost.SetPlayerBehaviorDeckService(
                    options.PlayerBehaviorDeckService);
            }

            if (options.EnableFairyGuiFloatingCombatText)
            {
                _fairyGuiFloatingCombatTextHost =
                    _owner.GetComponent<CharacterTestFairyGuiFloatingCombatTextHost>();
                if (_fairyGuiFloatingCombatTextHost == null)
                {
                    _fairyGuiFloatingCombatTextHost =
                        _owner.gameObject.AddComponent<CharacterTestFairyGuiFloatingCombatTextHost>();
                }

                _fairyGuiFloatingCombatTextHost.Configure(options.Slice, _logger, _fairyGuiLayerHost);
            }

            if (options.EnableFairyGuiStoryUi)
            {
                _fairyGuiStoryDialogHost = _owner.GetComponent<CharacterTestFairyGuiStoryDialogHost>();
                if (_fairyGuiStoryDialogHost == null)
                    _fairyGuiStoryDialogHost = _owner.gameObject.AddComponent<CharacterTestFairyGuiStoryDialogHost>();

                _fairyGuiStoryDialogHost.Configure(
                    options.Slice,
                    options.StoryContent,
                    _logger,
                    _fairyGuiLayerHost,
                    IsStoryDialogSuppressedByMenu,
                    _fairyGuiPlayerInputBridge,
                    textProvider,
                    _fairyGuiHost,
                    _fairyGuiRuntimeShell);
            }

            NormalizeOpenPlayerSurfaces();
            _configured = true;
        }

        /// <remarks>NoAlloc after configuration; call before simulation drains the input command queue.</remarks>
        internal void ProcessInput()
        {
            using (PlayerUiInputMarker.Auto())
            {
                ProcessGuidanceInput();
                IInputProvider inputProvider = _playerUiInputProvider;
                MxFairyGuiFocusInputBridge focusBridge = _fairyGuiFocusInputBridge;
                bool popupOpen = _fairyGuiPopupService?.HasOpenPopup == true;
                bool hasModal = _fairyGuiLayerHost != null
                    && _fairyGuiLayerHost.TryGetTopModal(out MxUiViewId topModal);
                if (inputProvider == null
                    || focusBridge == null
                    || _fairyGuiLayerHost == null
                    || (!popupOpen && !hasModal))
                {
                    _lastPlayerUiNavigateDirection = 0;
                    return;
                }

                InputCommandQueue commands = inputProvider.Commands;
                if (commands.Generation != _lastPlayerUiInputQueueGeneration
                    || commands.CurrentFrame < _lastPlayerUiInputQueueFrame)
                {
                    _hasProcessedPlayerUiInputSequence = false;
                    _lastProcessedPlayerUiInputSequence = 0L;
                }

                _lastPlayerUiInputQueueGeneration = commands.Generation;
                _lastPlayerUiInputQueueFrame = commands.CurrentFrame;
                _playerUiInputCommands.Clear();
                commands.PeekForFrame(
                    long.MaxValue,
                    _playerUiInputCommands,
                    _playerUiInputCommandPredicate);

                long highestSequence = _hasProcessedPlayerUiInputSequence
                    ? _lastProcessedPlayerUiInputSequence
                    : long.MinValue;
                for (int i = 0; i < _playerUiInputCommands.Count; i++)
                {
                    InputCommand command = _playerUiInputCommands[i];
                    if (command.Sequence > highestSequence)
                        highestSequence = command.Sequence;

                    if (command.Intent == InputIntent.Submit)
                    {
                        focusBridge.Process(topModal, MxFairyGuiFocusNavigationIntent.Submit);
                    }
                    else if (command.Intent == InputIntent.Cancel)
                    {
                        _lastPlayerUiCancelFrame = Time.frameCount;
                        focusBridge.Process(topModal, MxFairyGuiFocusNavigationIntent.Cancel);
                    }
                }

                if (_playerUiInputCommands.Count > 0)
                {
                    _lastProcessedPlayerUiInputSequence = highestSequence;
                    _hasProcessedPlayerUiInputSequence = true;
                }

                int navigateDirection = ResolveNavigateDirection(inputProvider.Snapshot.Navigate);
                if (navigateDirection != 0 && navigateDirection != _lastPlayerUiNavigateDirection)
                {
                    focusBridge.Process(
                        topModal,
                        navigateDirection > 0
                            ? MxFairyGuiFocusNavigationIntent.Next
                            : MxFairyGuiFocusNavigationIntent.Previous);
                }

                _lastPlayerUiNavigateDirection = navigateDirection;
            }
        }

        private void ProcessGuidanceInput()
        {
            IInputProvider inputProvider = _playerUiInputProvider;
            CharacterTestFairyGuiGuidanceHost guidanceHost = _fairyGuiGuidanceHost;
            if (inputProvider == null
                || guidanceHost?.HasVisibleContent != true
                || ShouldClearGameplayPresentation(CaptureSurfaceState()))
            {
                return;
            }

            InputCommandQueue commands = inputProvider.Commands;
            if (commands.Generation != _lastGuidanceInputQueueGeneration
                || commands.CurrentFrame < _lastGuidanceInputQueueFrame)
            {
                _hasProcessedGuidanceInputSequence = false;
                _lastProcessedGuidanceInputSequence = 0L;
            }

            _lastGuidanceInputQueueGeneration = commands.Generation;
            _lastGuidanceInputQueueFrame = commands.CurrentFrame;
            _guidanceInputCommands.Clear();
            commands.PeekForFrame(
                long.MaxValue,
                _guidanceInputCommands,
                _guidanceInputCommandPredicate);

            long highestSequence = _hasProcessedGuidanceInputSequence
                ? _lastProcessedGuidanceInputSequence
                : long.MinValue;
            for (int i = 0; i < _guidanceInputCommands.Count; i++)
            {
                InputCommand command = _guidanceInputCommands[i];
                if (command.Sequence > highestSequence)
                    highestSequence = command.Sequence;

                if (command.Intent == InputIntent.GuidancePrevious)
                    guidanceHost.SelectPreviousTest();
                else if (command.Intent == InputIntent.GuidanceNext)
                    guidanceHost.SelectNextTest();
            }

            if (_guidanceInputCommands.Count == 0)
                return;

            _lastProcessedGuidanceInputSequence = highestSequence;
            _hasProcessedGuidanceInputSequence = true;
        }

        public void Refresh()
        {
            if (ShouldRefreshMainMenu())
            {
                using (MainMenuRefreshMarker.Auto())
                {
                    _fairyGuiMainMenuHost.Refresh();
                }
            }

            if (ShouldRefreshNetworkPanel())
            {
                using (NetworkPanelRefreshMarker.Auto())
                {
                    _fairyGuiNetworkPanelHost.Refresh();
                }
            }

            if (ShouldRefreshSettingsPanel())
            {
                using (SettingsPanelRefreshMarker.Auto())
                {
                    _fairyGuiSettingsPanelHost.Refresh();
                }
            }

            if (ShouldRefreshAbilityPanel())
            {
                using (AbilityPanelRefreshMarker.Auto())
                {
                    _fairyGuiAbilityBindingPanelHost.Refresh();
                }
            }

            if (ShouldRefreshRuntimeAiBehaviorPanel())
            {
                using (RuntimeAiBehaviorPanelRefreshMarker.Auto())
                {
                    if (_fairyGuiBehaviorDeckPanelHost != null)
                        _fairyGuiBehaviorDeckPanelHost.Refresh();
                    else
                        _fairyGuiRuntimeAiBehaviorLoadoutPanelHost.Refresh();
                }
            }

            if (ShouldRefreshArenaSetupPanel())
            {
                using (ArenaSetupPanelRefreshMarker.Auto())
                {
                    _fairyGuiArenaSetupPanelHost.Refresh();
                }
            }

            if (ShouldRefreshArenaBehaviorGroupPanel())
            {
                using (ArenaBehaviorGroupPanelRefreshMarker.Auto())
                {
                    _fairyGuiArenaBehaviorGroupPanelHost.Refresh();
                }
            }

            if (ShouldRefreshRuntimeAiPlannerDevPanel())
            {
                using (RuntimeAiPlannerDevPanelRefreshMarker.Auto())
                {
                    _fairyGuiRuntimeAiPlannerDevPanelHost.Refresh();
                }
            }
            FlushRuntimeAiLabPreviewContractFault();

            CharacterTestUiSurfaceMask surfaceState = CaptureSurfaceState();
            bool playerModalOpen = ShouldClearGameplayPresentation(surfaceState);
            if (playerModalOpen)
            {
                if (ShouldClearPlayerSurfacesForModal(playerModalOpen, ref _lastPlayerModalOpen))
                {
                    using (CombatHudRefreshMarker.Auto())
                    {
                        _fairyGuiCombatHudHost?.Close();
                    }

                    using (FloatingTextRefreshMarker.Auto())
                    {
                        _fairyGuiFloatingCombatTextHost?.Clear();
                    }

                    using (GuidanceRefreshMarker.Auto())
                    {
                        _fairyGuiGuidanceHost?.ClearGameplayPresentation();
                    }
                }

                _lastCombatHudRefreshFrame = int.MinValue;
            }
            else
            {
                if (ShouldRefreshCombatHud())
                {
                    using (CombatHudRefreshMarker.Auto())
                    {
                        _fairyGuiCombatHudHost?.Refresh();
                    }
                }

                _fairyGuiCombatHudHost?.SyncPresentation();

                using (FloatingTextRefreshMarker.Auto())
                {
                    _fairyGuiFloatingCombatTextHost?.Refresh();
                }

                using (GuidanceRefreshMarker.Auto())
                {
                    _fairyGuiGuidanceHost?.RefreshFromRuntime();
                }
                ShouldClearPlayerSurfacesForModal(playerModalOpen, ref _lastPlayerModalOpen);
            }

            using (StoryDialogRefreshMarker.Auto())
            {
                _fairyGuiStoryDialogHost?.Refresh();
            }
        }

        public void TickGameplayPresentation(float deltaTime)
        {
            if (ShouldClearGameplayPresentation(CaptureSurfaceState()))
                return;

            using (FloatingTextTickMarker.Auto())
            {
                _fairyGuiFloatingCombatTextHost?.TickPresentation(deltaTime);
            }

            using (GuidanceTickMarker.Auto())
            {
                _fairyGuiGuidanceHost?.TickPresentation(deltaTime);
            }
        }

        /// <summary>Shows explicitly labelled guidance preview content for visual development.</summary>
        /// <remarks>LowFreqAlloc. This preview never reports validation success.</remarks>
        public bool ShowFairyGuiGuidanceDevelopmentPreview()
        {
            return _fairyGuiGuidanceHost?.ShowDevelopmentPreview() == true;
        }

        private bool OpenSurface(
            CharacterTestUiSurface surface,
            CharacterTestUiSurfaceOpenReason reason,
            int teamId = 0,
            int slotIndex = 0)
        {
            _ = reason;
            var tacticalFlow = _owner != null ? _owner.GetComponent<TacticalCard.TacticalCardSliceCoordinator>() : null;
            if (surface == CharacterTestUiSurface.RuntimeAiBehaviorLoadoutPanel && tacticalFlow != null
                && tacticalFlow.State != TacticalCard.TacticalPreparationState.Preparation) return false;
            bool opened;
            switch (surface)
            {
                case CharacterTestUiSurface.MainMenu:
                    opened = _fairyGuiMainMenuHost != null && _fairyGuiMainMenuHost.Open();
                    break;
                case CharacterTestUiSurface.NetworkPanel:
                    opened = _fairyGuiNetworkPanelHost != null && _fairyGuiNetworkPanelHost.Open();
                    break;
                case CharacterTestUiSurface.SettingsPanel:
                    opened = _fairyGuiSettingsPanelHost != null && _fairyGuiSettingsPanelHost.Open();
                    break;
                case CharacterTestUiSurface.AbilityBindingPanel:
                    opened = _fairyGuiAbilityBindingPanelHost != null && _fairyGuiAbilityBindingPanelHost.Open();
                    break;
                case CharacterTestUiSurface.RuntimeAiBehaviorLoadoutPanel:
                    if (_runtimeAiLabShellCoordinator != null)
                    {
                        CharacterTestRuntimeAiLabShellState state =
                            _runtimeAiLabShellCoordinator.CurrentState;
                        opened = state.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.PlayerDeck
                            ? state.Visibility == CharacterTestRuntimeAiLabShellVisibility.Visible
                                || _runtimeAiLabShellCoordinator.ToggleVisibility().Success
                            : _runtimeAiLabShellCoordinator.Navigate(
                                state.Generation,
                                state.Revision,
                                state.ActiveWorkspace,
                                CharacterTestRuntimeAiLabWorkspace.PlayerDeck).Success;
                        break;
                    }
                    opened = _fairyGuiBehaviorDeckPanelHost != null
                        ? _fairyGuiBehaviorDeckPanelHost.Open()
                        : _fairyGuiRuntimeAiBehaviorLoadoutPanelHost != null
                            && _fairyGuiRuntimeAiBehaviorLoadoutPanelHost.Open();
                    break;
                case CharacterTestUiSurface.ArenaSetupPanel:
                    opened = _fairyGuiArenaSetupPanelHost != null && _fairyGuiArenaSetupPanelHost.Open();
                    break;
                case CharacterTestUiSurface.ArenaBehaviorGroupPanel:
                    opened = _fairyGuiArenaBehaviorGroupPanelHost != null
                        && _fairyGuiArenaBehaviorGroupPanelHost.OpenForSlot(teamId, slotIndex);
                    break;
                default:
                    opened = false;
                    break;
            }

            if (!opened)
                return false;

            CloseConflictingPlayerSurfaces(surface);
            return true;
        }

        public bool OpenMainMenu()
        {
            return OpenSurface(CharacterTestUiSurface.MainMenu, CharacterTestUiSurfaceOpenReason.Direct);
        }

        public bool CloseMainMenu()
        {
            return _fairyGuiMainMenuHost != null && _fairyGuiMainMenuHost.Close();
        }

        private void OpenSettingsPanelFromMainMenu()
        {
            bool returnToMainMenu = IsMainMenuOpen;
            if (!OpenSurface(CharacterTestUiSurface.SettingsPanel, CharacterTestUiSurfaceOpenReason.MainMenuChild))
                return;

            _settingsPanelReturnToMainMenu = returnToMainMenu;
        }

        private bool CloseSettingsPanelFromCommand()
        {
            if (_settingsPanelReturnToMainMenu)
            {
                _settingsPanelReturnToMainMenu = false;
                if (OpenSurface(CharacterTestUiSurface.MainMenu, CharacterTestUiSurfaceOpenReason.BackNavigation))
                    return true;
            }

            return _fairyGuiSettingsPanelHost != null && _fairyGuiSettingsPanelHost.Close();
        }

        private void OpenAbilityBindingPanelFromMainMenu()
        {
            bool returnToMainMenu = IsMainMenuOpen;
            if (!OpenSurface(CharacterTestUiSurface.AbilityBindingPanel, CharacterTestUiSurfaceOpenReason.MainMenuChild))
                return;

            _abilityPanelReturnToMainMenu = returnToMainMenu;
        }

        private bool CloseAbilityBindingPanelFromCommand()
        {
            if (_abilityPanelReturnToMainMenu)
            {
                _abilityPanelReturnToMainMenu = false;
                if (OpenSurface(CharacterTestUiSurface.MainMenu, CharacterTestUiSurfaceOpenReason.BackNavigation))
                    return true;
            }

            return _fairyGuiAbilityBindingPanelHost != null && _fairyGuiAbilityBindingPanelHost.Close();
        }

        public bool OpenNetworkPanelFromMenu()
        {
            return OpenSurface(CharacterTestUiSurface.NetworkPanel, CharacterTestUiSurfaceOpenReason.Direct);
        }

        public bool OpenRuntimeAiBehaviorLoadoutPanel()
        {
            if (_runtimeAiLabShellCoordinator != null)
            {
                CharacterTestRuntimeAiLabShellState state = _runtimeAiLabShellCoordinator.CurrentState;
                if (state.Visibility == CharacterTestRuntimeAiLabShellVisibility.Hidden)
                {
                    if (!_runtimeAiLabShellCoordinator.ToggleVisibility().Success)
                        return false;
                    state = _runtimeAiLabShellCoordinator.CurrentState;
                }
                if (state.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.PlayerDeck)
                    return true;
                return _runtimeAiLabShellCoordinator.Navigate(
                    state.Generation,
                    state.Revision,
                    state.ActiveWorkspace,
                    CharacterTestRuntimeAiLabWorkspace.PlayerDeck).Success;
            }
            return OpenSurface(
                CharacterTestUiSurface.RuntimeAiBehaviorLoadoutPanel,
                CharacterTestUiSurfaceOpenReason.Direct);
        }

        private void OpenBehaviorDeckPanelFromMainMenu()
        {
            bool returnToMainMenu = IsMainMenuOpen;
            if (!OpenSurface(
                    CharacterTestUiSurface.RuntimeAiBehaviorLoadoutPanel,
                    CharacterTestUiSurfaceOpenReason.MainMenuChild))
            {
                return;
            }

            _behaviorDeckPanelReturnToMainMenu = returnToMainMenu;
        }

        public bool OpenTacticalContentPanel()
        {
            if (_fairyGuiTacticalDeckPanelHost == null)
                return false;
            _fairyGuiStoryDialogHost?.Close();
            _fairyGuiCombatHudHost?.Close();
            CloseConflictingPlayerSurfaces(CharacterTestUiSurface.TacticalPreparationPanel);
            return _fairyGuiTacticalDeckPanelHost.Open();
        }

        public bool CloseTacticalContentPanel() =>
            _fairyGuiTacticalDeckPanelHost != null
            && _fairyGuiTacticalDeckPanelHost.Close();

        public bool CloseRuntimeAiBehaviorLoadoutPanel()
        {
            if (_runtimeAiLabShellCoordinator != null)
                return _runtimeAiLabShellCoordinator.ToggleVisibility().Success;
            if (_behaviorDeckPanelReturnToMainMenu)
            {
                _behaviorDeckPanelReturnToMainMenu = false;
                if (OpenSurface(CharacterTestUiSurface.MainMenu, CharacterTestUiSurfaceOpenReason.BackNavigation))
                    return true;
            }

            if (_fairyGuiBehaviorDeckPanelHost != null)
                return _fairyGuiBehaviorDeckPanelHost.Close();
            return _fairyGuiRuntimeAiBehaviorLoadoutPanelHost != null
                && _fairyGuiRuntimeAiBehaviorLoadoutPanelHost.Close();
        }

        public bool ToggleRuntimeAiBehaviorLoadoutPanel()
        {
            if (_runtimeAiLabShellCoordinator != null)
                return ToggleRuntimeAiLabVisibility();
            if (_fairyGuiBehaviorDeckPanelHost == null
                && _fairyGuiRuntimeAiBehaviorLoadoutPanelHost == null)
                return false;

            bool isOpen = _fairyGuiBehaviorDeckPanelHost != null
                ? _fairyGuiBehaviorDeckPanelHost.IsOpen
                : _fairyGuiRuntimeAiBehaviorLoadoutPanelHost.IsOpen;
            return isOpen
                ? CloseRuntimeAiBehaviorLoadoutPanel()
                : OpenRuntimeAiBehaviorLoadoutPanel();
        }

        public bool OpenArenaSetupPanel()
        {
            return OpenSurface(CharacterTestUiSurface.ArenaSetupPanel, CharacterTestUiSurfaceOpenReason.Direct);
        }

        public bool CloseArenaSetupPanel()
        {
            return _fairyGuiArenaSetupPanelHost != null
                && _fairyGuiArenaSetupPanelHost.Close();
        }

        public bool OpenArenaBehaviorGroupPanelForSlot(int teamId, int slotIndex)
        {
            return OpenSurface(
                CharacterTestUiSurface.ArenaBehaviorGroupPanel,
                CharacterTestUiSurfaceOpenReason.Direct,
                teamId,
                slotIndex);
        }

        public bool CloseArenaBehaviorGroupPanel()
        {
            return _fairyGuiArenaBehaviorGroupPanelHost != null
                && _fairyGuiArenaBehaviorGroupPanelHost.Close();
        }

        public bool OpenRuntimeAiPlannerDevPanel()
        {
            if (_runtimeAiLabShellCoordinator != null)
            {
                CharacterTestRuntimeAiLabShellState state = _runtimeAiLabShellCoordinator.CurrentState;
                return state.Visibility == CharacterTestRuntimeAiLabShellVisibility.Hidden
                    ? _runtimeAiLabShellCoordinator.ToggleVisibility().Success
                    : true;
            }
            if (_fairyGuiRuntimeAiPlannerDevPanelHost == null)
                return false;

            _behaviorDeckPanelReturnToMainMenu = false;
            _fairyGuiBehaviorDeckPanelHost?.Close();
            _fairyGuiRuntimeAiBehaviorLoadoutPanelHost?.Close();
            return _fairyGuiRuntimeAiPlannerDevPanelHost.Open();
        }

        public bool CloseRuntimeAiPlannerDevPanel()
        {
            if (_runtimeAiLabShellCoordinator != null)
                return _runtimeAiLabShellCoordinator.ToggleVisibility().Success;
            return _fairyGuiRuntimeAiPlannerDevPanelHost != null
                && _fairyGuiRuntimeAiPlannerDevPanelHost.Close();
        }

        private bool OpenPublishedBehaviorCardInLab(string stableCardId)
        {
            RuntimeAiLabAuthoringNavigationStatus = string.Empty;
            if (_runtimeAiPlannerDevSceneController == null
                || _fairyGuiRuntimeAiPlannerDevPanelHost == null
                || !_fairyGuiRuntimeAiPlannerDevPanelHost.IsConfigured)
            {
                RuntimeAiLabAuthoringNavigationStatus = "Runtime AI Lab authoring dependencies are unavailable.";
                return false;
            }
            int entryIndex;
            string authoringMessage;
            bool began;
            if (_runtimeAiPlannerDevSceneController.IsProjectBehaviorCardLibraryActive)
            {
                began = _runtimeAiPlannerDevSceneController.TryBeginProjectCardAuthoring(
                    stableCardId,
                    out entryIndex,
                    out authoringMessage);
            }
            else if (_playerBehaviorDeckService != null
                && _playerBehaviorDeckService.TryResolveLatestPublishedCard(
                    stableCardId,
                    out CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard))
            {
                began = _runtimeAiPlannerDevSceneController.TryBeginPublishedCardAuthoring(
                    publishedCard,
                    _playerBehaviorDeckService.SelectedCharacterId,
                    out entryIndex,
                    out authoringMessage);
            }
            else
            {
                RuntimeAiLabAuthoringNavigationStatus = "Published behavior card cannot be resolved.";
                return false;
            }
            if (!began)
            {
                RuntimeAiLabAuthoringNavigationStatus = authoringMessage;
                return false;
            }

            if (_runtimeAiLabShellCoordinator == null
                || !_runtimeAiPlannerDevSceneController.TryCaptureBehaviorCardShellContext(
                    entryIndex,
                    CharacterTestRuntimeAiLabLogicElementKind.Stage,
                    0,
                    _runtimeAiLabShellCoordinator.CurrentState.PageContext.BehaviorCardViewport,
                    out CharacterTestRuntimeAiLabPageContext page))
            {
                RuntimeAiLabAuthoringNavigationStatus = "Behavior Card shell context cannot be captured.";
                return false;
            }

            CharacterTestRuntimeAiLabShellState shellState = _runtimeAiLabShellCoordinator.CurrentState;
            CharacterTestRuntimeAiLabNavigationResult navigation =
                _runtimeAiLabShellCoordinator.NavigateToBehaviorCard(
                    shellState.Generation,
                    shellState.Revision,
                    shellState.ActiveWorkspace,
                    page.GroupDraftId,
                    page.EntryId,
                    page.CardDraftId,
                    page.SelectedElementKind,
                    page.SelectedElementStableId,
                    page.BehaviorCardViewport);
            RuntimeAiLabAuthoringNavigationStatus = navigation.Message;
            return navigation.Success;
        }

        private bool ReturnToBehaviorDeckFromRuntimeAiLabAuthoring()
        {
            if (_runtimeAiLabShellCoordinator != null)
                return NavigateRuntimeAiLab(CharacterTestRuntimeAiLabWorkspace.PlayerDeck);
            bool closed = _fairyGuiRuntimeAiPlannerDevPanelHost != null
                && _fairyGuiRuntimeAiPlannerDevPanelHost.Close();
            _behaviorDeckPanelReturnToMainMenu = false;
            bool opened = _fairyGuiBehaviorDeckPanelHost != null
                && _fairyGuiBehaviorDeckPanelHost.Open();
            return closed || opened;
        }

        internal bool ToggleRuntimeAiPlannerDevPanel()
        {
            if (_runtimeAiLabShellCoordinator != null)
                return ToggleRuntimeAiLabVisibility();
            if (_fairyGuiRuntimeAiPlannerDevPanelHost == null)
                return false;
            return _fairyGuiRuntimeAiPlannerDevPanelHost.IsOpen
                ? _fairyGuiRuntimeAiPlannerDevPanelHost.Toggle()
                : OpenRuntimeAiPlannerDevPanel();
        }

        public void Release()
        {
            ReleaseTacticalLoading();
            if (_semanticCursor != null) _semanticCursor.enabled = false;
            _semanticCursor = null;
            ReleaseRuntimeAiLabShellMount();
            if (_fairyGuiRuntimeAiPlannerDevPanelHost != null)
            {
                _fairyGuiRuntimeAiPlannerDevPanelHost.Release();
                _fairyGuiRuntimeAiPlannerDevPanelHost = null;
            }

            if (_fairyGuiArenaSetupPanelHost != null)
            {
                _fairyGuiArenaSetupPanelHost.Release();
                _fairyGuiArenaSetupPanelHost = null;
            }

            if (_fairyGuiArenaBehaviorGroupPanelHost != null)
            {
                _fairyGuiArenaBehaviorGroupPanelHost.Release();
                _fairyGuiArenaBehaviorGroupPanelHost = null;
            }

            if (_fairyGuiRuntimeAiBehaviorLoadoutPanelHost != null)
            {
                _fairyGuiRuntimeAiBehaviorLoadoutPanelHost.Release();
                _fairyGuiRuntimeAiBehaviorLoadoutPanelHost = null;
            }

            if (_fairyGuiBehaviorDeckPanelHost != null)
            {
                _fairyGuiBehaviorDeckPanelHost.Release();
                _fairyGuiBehaviorDeckPanelHost = null;
            }

            if (_fairyGuiTacticalDeckPanelHost != null)
            {
                _fairyGuiTacticalDeckPanelHost.Release();
                _fairyGuiTacticalDeckPanelHost = null;
            }

            if (_fairyGuiSettingsPanelHost != null)
            {
                _fairyGuiSettingsPanelHost.Release();
                _fairyGuiSettingsPanelHost = null;
            }

            if (_fairyGuiAbilityBindingPanelHost != null)
            {
                _fairyGuiAbilityBindingPanelHost.Release();
                _fairyGuiAbilityBindingPanelHost = null;
            }

            if (_fairyGuiNetworkPanelHost != null)
            {
                _fairyGuiNetworkPanelHost.Release();
                _fairyGuiNetworkPanelHost = null;
            }

            if (_fairyGuiMainMenuHost != null)
            {
                _fairyGuiMainMenuHost.Release();
                _fairyGuiMainMenuHost = null;
            }

            if (_fairyGuiStoryDialogHost != null)
            {
                _fairyGuiStoryDialogHost.Release();
                _fairyGuiStoryDialogHost = null;
            }

            if (_fairyGuiCombatHudHost != null)
            {
                _fairyGuiCombatHudHost.Release();
                _fairyGuiCombatHudHost = null;
            }

            if (_fairyGuiFloatingCombatTextHost != null)
            {
                _fairyGuiFloatingCombatTextHost.Release();
                _fairyGuiFloatingCombatTextHost = null;
            }

            if (_fairyGuiGuidanceHost != null)
            {
                _fairyGuiGuidanceHost.Release();
                _fairyGuiGuidanceHost = null;
            }

            _fairyGuiRuntimeShell?.Dispose();
            _fairyGuiRuntimeShell = null;
            _fairyGuiPopupService?.Dispose();
            _fairyGuiPopupService = null;
            ReleasePreloadedFairyGuiPackages();
            _fairyGuiLayerHost = null;
            _fairyGuiHost = null;
            _lastFairyGuiSettingsPanelStatus = string.Empty;
            _lastFairyGuiAbilityBindingPanelStatus = string.Empty;
            _lastFairyGuiRuntimeAiBehaviorLoadoutPanelStatus = string.Empty;
            _lastFairyGuiArenaSetupPanelStatus = string.Empty;
            _lastFairyGuiArenaBehaviorGroupPanelStatus = string.Empty;
            _lastFairyGuiRuntimeAiPlannerDevPanelStatus = string.Empty;
            _runtimeAiPlannerDevSceneController = null;
            _playerBehaviorDeckService = null;
            _arenaSessionController = null;
            _fairyGuiPlayerInputBridge = null;
            _playerUiInputProvider = null;
            _fairyGuiFocusInputBridge = null;
            _playerUiInputCommands.Clear();
            _lastProcessedPlayerUiInputSequence = 0L;
            _lastPlayerUiInputQueueFrame = 0L;
            _lastPlayerUiInputQueueGeneration = 0L;
            _hasProcessedPlayerUiInputSequence = false;
            _lastPlayerUiNavigateDirection = 0;
            _lastPlayerModalInputFrame = int.MinValue;
            _lastPlayerUiCancelFrame = int.MinValue;
            ResetModalPanelRefreshScheduler();
            _lastCombatHudRefreshFrame = int.MinValue;
            _lastPlayerModalOpen = false;
            _settingsPanelReturnToMainMenu = false;
            _abilityPanelReturnToMainMenu = false;
            _behaviorDeckPanelReturnToMainMenu = false;
            _fairyGuiPackagePreloadDescriptors.Clear();
            _fairyGuiPackagePreloadAttemptCount = 0;
            _fairyGuiPackagePreloadSuccessCount = 0;
            _lastFairyGuiPackagePreloadStatus = string.Empty;

            if (_debugBootstrap != null)
            {
                _debugBootstrap.Release();
                _debugBootstrap = null;
            }

            if (_skillPanel != null)
            {
                _skillPanel.Release();
                _skillPanel = null;
            }

            if (_skillPanelObject != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_skillPanelObject);
                else
                    UnityEngine.Object.DestroyImmediate(_skillPanelObject);
                _skillPanelObject = null;
            }

            _configured = false;
        }

        public void DetachSlice()
        {
            _runtimeAiPlannerDevSceneController?.ReleaseCardTrialSurface();
            _debugBootstrap?.Release();
            _skillPanel?.Release();
            _fairyGuiFloatingCombatTextHost?.DetachSlice();
            _fairyGuiCombatHudHost?.DetachSlice();
            _fairyGuiStoryDialogHost?.DetachSlice();
            _fairyGuiMainMenuHost?.DetachSlice();
            _fairyGuiNetworkPanelHost?.DetachSlice();
            _fairyGuiSettingsPanelHost?.DetachSlice();
            _fairyGuiAbilityBindingPanelHost?.DetachSlice();
            _fairyGuiBehaviorDeckPanelHost?.DetachSlice();
            _fairyGuiRuntimeAiBehaviorLoadoutPanelHost?.DetachSlice();
            _fairyGuiArenaSetupPanelHost?.DetachSlice();
            _fairyGuiArenaBehaviorGroupPanelHost?.DetachSlice();
            _fairyGuiRuntimeAiPlannerDevPanelHost?.DetachSlice();
            _fairyGuiGuidanceHost?.DetachSlice();
            _runtimeAiLabDeferredPreviewFault.Clear();
        }

        public bool CanRebind(CharacterTestUiRuntimeOptions options)
        {
            if (!_configured || options?.Slice?.Resources?.ResourceManager == null)
                return false;

            if (options.EnableDebugUi != (_debugBootstrap != null && _debugBootstrap.IsDebugUiConfigured))
                return false;
            if (options.EnableSkillPanel != (_skillPanel != null))
                return false;
            if (options.EnableFairyGuiCombatHud != (_fairyGuiCombatHudHost != null))
                return false;
            if (options.EnableFairyGuiFloatingCombatText != (_fairyGuiFloatingCombatTextHost != null))
                return false;
            if (options.EnableFairyGuiGuidance != (_fairyGuiGuidanceHost != null))
                return false;
            if (options.EnableFairyGuiStoryUi != (_fairyGuiStoryDialogHost != null))
                return false;
            if (options.EnableFairyGuiMainMenu != (_fairyGuiMainMenuHost != null))
                return false;
            if (options.EnableFairyGuiNetworkPanel != (_fairyGuiNetworkPanelHost != null))
                return false;
            if (options.EnableFairyGuiSettingsPanel != (_fairyGuiSettingsPanelHost != null))
                return false;
            if (options.EnableFairyGuiAbilityBindingPanel != (_fairyGuiAbilityBindingPanelHost != null))
                return false;
            if (options.EnableFairyGuiRuntimeAiPlannerDevPanel != (_fairyGuiRuntimeAiPlannerDevPanelHost != null))
                return false;
            bool wantsBehaviorDeck = options.EnableFairyGuiRuntimeAiBehaviorLoadoutPanel && options.PlayerBehaviorDeckService != null;
            if (wantsBehaviorDeck != (_fairyGuiBehaviorDeckPanelHost != null))
                return false;
            bool wantsLegacyBehaviorLoadout = options.EnableFairyGuiRuntimeAiBehaviorLoadoutPanel && options.PlayerBehaviorDeckService == null;
            if (wantsLegacyBehaviorLoadout != (_fairyGuiRuntimeAiBehaviorLoadoutPanelHost != null))
                return false;
            if (options.EnableFairyGuiArenaSetupPanel != (_fairyGuiArenaSetupPanelHost != null))
                return false;

            return true;
        }

        public bool TryRebind(CharacterTestUiRuntimeOptions options)
        {
            if (!CanRebind(options))
                return false;

            CharacterTestSliceServices services = options.Slice.Services;
            _runtimeAiPlannerDevSceneController = options.RuntimeAiPlannerDevSceneController;
            _playerBehaviorDeckService = options.PlayerBehaviorDeckService;
            _arenaSessionController = options.ArenaSessionController;

            _fairyGuiRuntimeShell?.UpdateResourceManager(options.Slice.Resources.ResourceManager);

            if (_debugBootstrap != null)
            {
                _debugBootstrap.Release();
                _debugBootstrap.RegisterStoryDebugTarget(options.Slice);
                _debugBootstrap.RegisterRuntimeAiPlannerDebugTarget(
                    options.Slice,
                    () => options.Slice.Services.RuntimeCommands != null
                        && options.Slice.Services.RuntimeCommands.RuntimeAiBehaviorSelectionUnlocked,
                    unlocked => options.Slice.Services.RuntimeCommands?.SetRuntimeAiBehaviorSelectionUnlocked(unlocked),
                    OpenRuntimeAiBehaviorLoadoutPanel,
                    CloseRuntimeAiBehaviorLoadoutPanel,
                    () => (_fairyGuiBehaviorDeckPanelHost != null && _fairyGuiBehaviorDeckPanelHost.IsOpen)
                        || (_fairyGuiRuntimeAiBehaviorLoadoutPanelHost != null
                            && _fairyGuiRuntimeAiBehaviorLoadoutPanelHost.IsOpen),
                    () => LastFairyGuiRuntimeAiBehaviorLoadoutPanelStatus);
                if (options.EnableDebugUi)
                {
                    _debugBootstrap.ConfigureDebugUi(
                        options.Slice,
                        options.IsPausedProvider,
                        options.DebugPanelSettings,
                        options.DebugInitialVisibility,
                        options.DebugShowOnStart,
                        options.DebugInitialTabIndex);
                }
            }

            if (_skillPanel != null && options.EnableSkillPanel)
            {
                _skillPanel.Configure(
                    services,
                    options.SkillPanelSettings,
                    _logger);
            }

            if (_fairyGuiFloatingCombatTextHost != null && options.EnableFairyGuiFloatingCombatText)
            {
                _fairyGuiFloatingCombatTextHost.RebindSlice(options.Slice);
            }

            if (_fairyGuiCombatHudHost != null && options.EnableFairyGuiCombatHud)
            {
                _fairyGuiCombatHudHost.RebindSlice(
                    options.Slice,
                    options.NetworkSessionStatusProvider,
                    options.PlayerBehaviorDeckService);
            }

            if (_fairyGuiStoryDialogHost != null && options.EnableFairyGuiStoryUi)
            {
                _fairyGuiStoryDialogHost.RebindSlice(options.Slice, options.StoryContent);
            }

            if (_fairyGuiMainMenuHost != null && options.EnableFairyGuiMainMenu)
            {
                _fairyGuiMainMenuHost.RebindSlice(options.Slice, options.MainMenuController);
            }

            if (_fairyGuiNetworkPanelHost != null && options.EnableFairyGuiNetworkPanel)
            {
                _fairyGuiNetworkPanelHost.RebindSlice(
                    options.Slice,
                    options.NetworkSessionController,
                    options.NetworkLobbyController);
            }

            if (_fairyGuiSettingsPanelHost != null && options.EnableFairyGuiSettingsPanel)
            {
                _fairyGuiSettingsPanelHost.RebindSlice(options.Slice);
            }

            if (_fairyGuiAbilityBindingPanelHost != null && options.EnableFairyGuiAbilityBindingPanel)
            {
                _fairyGuiAbilityBindingPanelHost.RebindSlice(
                    options.Slice,
                    services.Modules.AbilityHotbar);
            }

            if (_fairyGuiBehaviorDeckPanelHost != null && options.PlayerBehaviorDeckService != null)
            {
                _fairyGuiBehaviorDeckPanelHost.RebindSlice(options.Slice, options.PlayerBehaviorDeckService);
            }
            else if (_fairyGuiRuntimeAiBehaviorLoadoutPanelHost != null)
            {
                _fairyGuiRuntimeAiBehaviorLoadoutPanelHost.RebindSlice(options.Slice);
            }

            if (_fairyGuiArenaSetupPanelHost != null && options.EnableFairyGuiArenaSetupPanel)
            {
                _fairyGuiArenaSetupPanelHost.RebindSlice(options.Slice, options.ArenaSessionController);
            }

            if (_fairyGuiArenaBehaviorGroupPanelHost != null && options.EnableFairyGuiArenaSetupPanel)
            {
                _fairyGuiArenaBehaviorGroupPanelHost.RebindSlice(options.Slice, options.ArenaSessionController);
            }

            if (_fairyGuiRuntimeAiPlannerDevPanelHost != null
                && options.EnableFairyGuiRuntimeAiPlannerDevPanel
                && options.RuntimeAiPlannerDevSceneController != null)
            {
                options.RuntimeAiPlannerDevSceneController.ConfigurePlayerLegalitySource(
                    options.PlayerBehaviorDeckService);
                options.RuntimeAiPlannerDevSceneController.ConfigurePublishSource(
                    options.PlayerBehaviorDeckService);
                _fairyGuiRuntimeAiPlannerDevPanelHost.RebindSlice(
                    options.Slice,
                    options.RuntimeAiPlannerDevSceneController);
                options.RuntimeAiPlannerDevSceneController.ConfigureCardTrialSurface(options.Slice,
                    _fairyGuiLayerHost, _fairyGuiPlayerInputBridge, _fairyGuiHost, _fairyGuiRuntimeShell, _fairyGuiPopupService);
                _fairyGuiRuntimeAiPlannerDevPanelHost.ConfigureBehaviorCardLibrary(
                    options.PlayerBehaviorDeckService,
                    OpenPublishedBehaviorCardInLab);
            }

            if (_fairyGuiGuidanceHost != null && options.EnableFairyGuiGuidance)
            {
                _fairyGuiGuidanceHost.RebindSlice(options.Slice);
            }

            if (options.RuntimeAiPlannerDevSceneController != null && _runtimeAiLabShellCoordinator == null)
            {
                ConfigureRuntimeAiLabShell();
            }

            NormalizeOpenPlayerSurfaces();
            return true;
        }

        private void OpenMainMenuFromNetworkPanel()
        {
            OpenMainMenu();
        }

        private bool ShouldRefreshMainMenu()
        {
            CharacterTestFairyGuiMainMenuHost host = _fairyGuiMainMenuHost;
            return host != null && ShouldRefreshModalPanel(
                Time.frameCount,
                host.IsOpen,
                host.RefreshAttemptCount,
                ref _lastMainMenuRefreshFrame,
                ModalPanelRefreshIntervalFrames);
        }

        private bool ShouldRefreshNetworkPanel()
        {
            CharacterTestFairyGuiNetworkPanelHost host = _fairyGuiNetworkPanelHost;
            return host != null && ShouldRefreshModalPanel(
                Time.frameCount,
                host.IsOpen,
                host.RefreshAttemptCount,
                ref _lastNetworkPanelRefreshFrame,
                ModalPanelRefreshIntervalFrames);
        }

        private bool ShouldRefreshSettingsPanel()
        {
            CharacterTestFairyGuiSettingsPanelHost host = _fairyGuiSettingsPanelHost;
            return host != null && ShouldRefreshModalPanel(
                Time.frameCount,
                host.IsOpen,
                host.RefreshAttemptCount,
                ref _lastSettingsPanelRefreshFrame,
                ModalPanelRefreshIntervalFrames);
        }

        private bool ShouldRefreshAbilityPanel()
        {
            CharacterTestFairyGuiAbilityBindingPanelHost host = _fairyGuiAbilityBindingPanelHost;
            return host != null && ShouldRefreshModalPanel(
                Time.frameCount,
                host.IsOpen,
                host.RefreshAttemptCount,
                ref _lastAbilityPanelRefreshFrame,
                ModalPanelRefreshIntervalFrames);
        }

        private bool ShouldRefreshRuntimeAiBehaviorPanel()
        {
            CharacterTestFairyGuiBehaviorDeckPanelHost deckHost = _fairyGuiBehaviorDeckPanelHost;
            if (deckHost != null)
            {
                return ShouldRefreshModalPanel(
                    Time.frameCount,
                    deckHost.IsOpen,
                    deckHost.RefreshAttemptCount,
                    ref _lastRuntimeAiBehaviorPanelRefreshFrame,
                    ModalPanelRefreshIntervalFrames);
            }

            CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelHost host =
                _fairyGuiRuntimeAiBehaviorLoadoutPanelHost;
            return host != null && ShouldRefreshModalPanel(
                Time.frameCount,
                host.IsOpen,
                host.RefreshAttemptCount,
                ref _lastRuntimeAiBehaviorPanelRefreshFrame,
                ModalPanelRefreshIntervalFrames);
        }

        private bool ShouldRefreshArenaSetupPanel()
        {
            CharacterTestFairyGuiArenaSetupPanelHost host = _fairyGuiArenaSetupPanelHost;
            return host != null && ShouldRefreshModalPanel(
                Time.frameCount,
                host.IsOpen,
                host.RefreshAttemptCount,
                ref _lastArenaSetupPanelRefreshFrame,
                ModalPanelRefreshIntervalFrames);
        }

        private bool ShouldRefreshArenaBehaviorGroupPanel()
        {
            CharacterTestFairyGuiArenaBehaviorGroupPanelHost host = _fairyGuiArenaBehaviorGroupPanelHost;
            return host != null && ShouldRefreshModalPanel(
                Time.frameCount,
                host.IsOpen,
                host.RefreshAttemptCount,
                ref _lastArenaBehaviorGroupPanelRefreshFrame,
                ModalPanelRefreshIntervalFrames);
        }

        private bool ShouldRefreshRuntimeAiPlannerDevPanel()
        {
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost host = _fairyGuiRuntimeAiPlannerDevPanelHost;
            return host != null && ShouldRefreshModalPanel(
                Time.frameCount,
                host.IsOpen,
                host.RefreshAttemptCount,
                ref _lastRuntimeAiPlannerDevPanelRefreshFrame,
                ModalPanelRefreshIntervalFrames);
        }

        internal static bool ShouldRefreshModalPanel(
            int currentFrame,
            bool isOpen,
            int refreshAttemptCount,
            ref int lastRefreshFrame,
            int refreshIntervalFrames)
        {
            if (refreshAttemptCount <= 0)
            {
                lastRefreshFrame = currentFrame;
                return true;
            }

            if (!isOpen)
            {
                lastRefreshFrame = currentFrame;
                return false;
            }

            if (lastRefreshFrame == int.MinValue)
            {
                lastRefreshFrame = currentFrame;
                return true;
            }

            int elapsedFrames = currentFrame - lastRefreshFrame;
            if (elapsedFrames >= 0 && elapsedFrames < Math.Max(1, refreshIntervalFrames))
                return false;

            lastRefreshFrame = currentFrame;
            return true;
        }

        internal static bool ShouldClearPlayerSurfacesForModal(
            bool isPlayerModalOpen,
            ref bool lastPlayerModalOpen)
        {
            if (!isPlayerModalOpen)
            {
                lastPlayerModalOpen = false;
                return false;
            }

            if (lastPlayerModalOpen)
                return false;

            lastPlayerModalOpen = true;
            return true;
        }

        private void ResetModalPanelRefreshScheduler()
        {
            _lastMainMenuRefreshFrame = int.MinValue;
            _lastNetworkPanelRefreshFrame = int.MinValue;
            _lastSettingsPanelRefreshFrame = int.MinValue;
            _lastAbilityPanelRefreshFrame = int.MinValue;
            _lastRuntimeAiBehaviorPanelRefreshFrame = int.MinValue;
            _lastArenaSetupPanelRefreshFrame = int.MinValue;
            _lastArenaBehaviorGroupPanelRefreshFrame = int.MinValue;
            _lastRuntimeAiPlannerDevPanelRefreshFrame = int.MinValue;
        }

        private bool ShouldRefreshCombatHud()
        {
            if (_fairyGuiCombatHudHost == null)
                return false;

            int frame = Time.frameCount;
            if (_lastPlayerModalOpen || !_fairyGuiCombatHudHost.IsOpen)
            {
                _lastCombatHudRefreshFrame = frame;
                return true;
            }

            if (frame - _lastCombatHudRefreshFrame < CombatHudRefreshIntervalFrames)
                return false;

            _lastCombatHudRefreshFrame = frame;
            return true;
        }

        private CharacterTestUiSurfaceMask CaptureSurfaceState()
        {
            CharacterTestUiSurfaceMask state = CharacterTestUiSurfaceMask.None;
            if (_fairyGuiCombatHudHost != null && _fairyGuiCombatHudHost.IsOpen)
                state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.CombatHud);
            if (_fairyGuiFloatingCombatTextHost != null && _fairyGuiFloatingCombatTextHost.ActiveCount > 0)
                state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.FloatingCombatText);
            if (_fairyGuiGuidanceHost != null && _fairyGuiGuidanceHost.HasVisibleContent)
                state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.Guidance);
            if (_fairyGuiMainMenuHost != null && _fairyGuiMainMenuHost.IsOpen)
                state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.MainMenu);
            if (_fairyGuiNetworkPanelHost != null && _fairyGuiNetworkPanelHost.IsOpen)
                state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.NetworkPanel);
            if (_fairyGuiSettingsPanelHost != null && _fairyGuiSettingsPanelHost.IsOpen)
                state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.SettingsPanel);
            if (_fairyGuiAbilityBindingPanelHost != null && _fairyGuiAbilityBindingPanelHost.IsOpen)
                state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.AbilityBindingPanel);
            if (_fairyGuiRuntimeAiBehaviorLoadoutPanelHost != null
                && _fairyGuiRuntimeAiBehaviorLoadoutPanelHost.IsOpen)
            {
                state = CharacterTestUiSurfacePolicy.Add(
                    state,
                    CharacterTestUiSurface.RuntimeAiBehaviorLoadoutPanel);
            }
            if (_fairyGuiBehaviorDeckPanelHost != null && _fairyGuiBehaviorDeckPanelHost.IsOpen)
            {
                state = CharacterTestUiSurfacePolicy.Add(
                    state,
                    CharacterTestUiSurface.RuntimeAiBehaviorLoadoutPanel);
            }
            if (_fairyGuiTacticalDeckPanelHost != null && _fairyGuiTacticalDeckPanelHost.IsOpen)
                state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.TacticalPreparationPanel);
            if (_fairyGuiArenaSetupPanelHost != null && _fairyGuiArenaSetupPanelHost.IsOpen)
                state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.ArenaSetupPanel);
            if (_fairyGuiArenaBehaviorGroupPanelHost != null
                && _fairyGuiArenaBehaviorGroupPanelHost.IsOpen)
            {
                state = CharacterTestUiSurfacePolicy.Add(
                    state,
                    CharacterTestUiSurface.ArenaBehaviorGroupPanel);
            }
            if (_fairyGuiStoryDialogHost != null && _fairyGuiStoryDialogHost.IsOpen)
                state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.StoryDialog);

            return state;
        }

        private bool ShouldClearGameplayPresentation(CharacterTestUiSurfaceMask surfaceState)
        {
            if (HasTacticalLoading) return true;
            bool arenaSetupPanelBlocksGameplayPresentation =
                _arenaSessionController == null
                || _arenaSessionController.Phase == CharacterTestArenaMatchPhase.Setup;
            return CharacterTestUiSurfacePolicy.ShouldClearGameplayPresentation(
                surfaceState,
                arenaSetupPanelBlocksGameplayPresentation);
        }

        private void CloseConflictingPlayerSurfaces(CharacterTestUiSurface openedSurface)
        {
            CharacterTestUiSurfaceMask surfacesToClose =
                CharacterTestUiSurfacePolicy.GetConflictingSurfacesToClose(openedSurface, CaptureSurfaceState());
            CloseSurfaces(surfacesToClose);
        }

        private void NormalizeOpenPlayerSurfaces()
        {
            if (_fairyGuiLayerHost == null || !_fairyGuiLayerHost.TryGetTopModal(out MxUiViewId topModal))
                return;

            if (TryResolvePlayerSurface(topModal, out CharacterTestUiSurface surface))
                CloseConflictingPlayerSurfaces(surface);
        }

        private static bool TryResolvePlayerSurface(
            MxUiViewId viewId,
            out CharacterTestUiSurface surface)
        {
            if (viewId == CharacterTestFairyGuiMainMenuIds.ViewId)
            {
                surface = CharacterTestUiSurface.MainMenu;
                return true;
            }

            if (viewId == CharacterTestFairyGuiNetworkPanelIds.ViewId)
            {
                surface = CharacterTestUiSurface.NetworkPanel;
                return true;
            }

            if (viewId == CharacterTestFairyGuiSettingsPanelIds.ViewId)
            {
                surface = CharacterTestUiSurface.SettingsPanel;
                return true;
            }

            if (viewId == CharacterTestFairyGuiAbilityBindingPanelIds.ViewId)
            {
                surface = CharacterTestUiSurface.AbilityBindingPanel;
                return true;
            }

            if (viewId == CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelIds.ViewId)
            {
                surface = CharacterTestUiSurface.RuntimeAiBehaviorLoadoutPanel;
                return true;
            }

            if (viewId == CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId)
            {
                surface = CharacterTestUiSurface.RuntimeAiBehaviorLoadoutPanel;
                return true;
            }

            if (viewId == CharacterTestFairyGuiTacticalDeckPanelIds.ViewId)
            {
                surface = CharacterTestUiSurface.TacticalPreparationPanel;
                return true;
            }

            if (viewId == CharacterTestFairyGuiArenaSetupPanelIds.ViewId)
            {
                surface = CharacterTestUiSurface.ArenaSetupPanel;
                return true;
            }

            if (viewId == CharacterTestFairyGuiArenaBehaviorGroupPanelIds.ViewId)
            {
                surface = CharacterTestUiSurface.ArenaBehaviorGroupPanel;
                return true;
            }

            if (viewId == CharacterTestFairyGuiStoryDialogIds.ViewId)
            {
                surface = CharacterTestUiSurface.StoryDialog;
                return true;
            }

            surface = default;
            return false;
        }

        private void CloseSurfaces(CharacterTestUiSurfaceMask surfaces)
        {
            if ((surfaces & CharacterTestUiSurfaceMask.MainMenu) != 0)
                _fairyGuiMainMenuHost?.Close();

            if ((surfaces & CharacterTestUiSurfaceMask.NetworkPanel) != 0)
                _fairyGuiNetworkPanelHost?.Close();

            if ((surfaces & CharacterTestUiSurfaceMask.SettingsPanel) != 0)
            {
                _settingsPanelReturnToMainMenu = false;
                _fairyGuiSettingsPanelHost?.Close();
            }

            if ((surfaces & CharacterTestUiSurfaceMask.AbilityBindingPanel) != 0)
            {
                _abilityPanelReturnToMainMenu = false;
                _fairyGuiAbilityBindingPanelHost?.Close();
            }

            if ((surfaces & CharacterTestUiSurfaceMask.RuntimeAiBehaviorLoadoutPanel) != 0)
            {
                _behaviorDeckPanelReturnToMainMenu = false;
                _fairyGuiBehaviorDeckPanelHost?.Close();
                _fairyGuiRuntimeAiBehaviorLoadoutPanelHost?.Close();
            }

            if ((surfaces & CharacterTestUiSurfaceMask.TacticalPreparationPanel) != 0)
                _fairyGuiTacticalDeckPanelHost?.Close();

            if ((surfaces & CharacterTestUiSurfaceMask.ArenaSetupPanel) != 0)
                _fairyGuiArenaSetupPanelHost?.Close();

            if ((surfaces & CharacterTestUiSurfaceMask.ArenaBehaviorGroupPanel) != 0)
                _fairyGuiArenaBehaviorGroupPanelHost?.Close();
        }

        private bool IsStoryDialogSuppressedByMenu()
        {
            return CharacterTestUiSurfacePolicy.IsStoryDialogSuppressed(CaptureSurfaceState());
        }

        private bool IsPlayerModalOpen()
        {
            return HasTacticalLoading || CharacterTestUiSurfacePolicy.HasPlayerModalOpen(CaptureSurfaceState());
        }

        private void PreloadFairyGuiPackages(
            CharacterTestUiRuntimeOptions options,
            CharacterTestSliceServices services)
        {
            _fairyGuiPackagePreloadDescriptors.Clear();
            _fairyGuiPackagePreloadAttemptCount = 0;
            _fairyGuiPackagePreloadSuccessCount = 0;
            _lastFairyGuiPackagePreloadStatus = string.Empty;

            if (_fairyGuiHost == null || options == null || options.Slice?.Resources?.ResourceManager == null)
                return;

            CollectFairyGuiPackagePreloadDescriptors(options, services);
            if (_fairyGuiPackagePreloadDescriptors.Count == 0)
                return;

            IResourceManager resourceManager = options.Slice.Resources.ResourceManager;
            var bridge = new MxFairyGuiResourceBridge(resourceManager);
            for (int i = 0; i < _fairyGuiPackagePreloadDescriptors.Count; i++)
            {
                MxFairyGuiPackageDescriptor descriptor = _fairyGuiPackagePreloadDescriptors[i];
                _fairyGuiPackagePreloadAttemptCount++;
                MxFairyGuiPackageLoadResult loadResult = bridge.LoadPackage(descriptor);
                if (!loadResult.Success)
                {
                    RecordFairyGuiPackagePreloadFailure(descriptor, loadResult.Failure.Message);
                    continue;
                }

                string failure;
                if (!_fairyGuiHost.EnsurePackage(loadResult.Scope, out failure))
                {
                    loadResult.Scope.Release();
                    RecordFairyGuiPackagePreloadFailure(descriptor, failure);
                    continue;
                }

                _preloadedFairyGuiPackageIds.Add(descriptor.PackageId);
                _fairyGuiPackagePreloadSuccessCount++;
            }

            if (_fairyGuiPackagePreloadSuccessCount > 0 && string.IsNullOrEmpty(_lastFairyGuiPackagePreloadStatus))
            {
                _lastFairyGuiPackagePreloadStatus =
                    "Preloaded FairyGUI packages="
                    + _fairyGuiPackagePreloadSuccessCount
                    + "/"
                    + _fairyGuiPackagePreloadAttemptCount
                    + ".";
            }
        }

        private void CollectFairyGuiPackagePreloadDescriptors(
            CharacterTestUiRuntimeOptions options,
            CharacterTestSliceServices services)
        {
            if (options.EnableFairyGuiCombatHud)
                AddFairyGuiPackagePreloadDescriptor(CharacterTestFairyGuiCombatHudManifest.CreatePackageDescriptor());

            if (options.EnableFairyGuiGuidance)
                AddFairyGuiPackagePreloadDescriptor(MxPlayerGuidanceCardManifest.CreatePackageDescriptor());

            if (options.EnableFairyGuiStoryUi)
                AddFairyGuiPackagePreloadDescriptor(CharacterTestFairyGuiStoryDialogManifest.CreatePackageDescriptor());

            if (options.EnableFairyGuiMainMenu && options.MainMenuController != null)
                AddFairyGuiPackagePreloadDescriptor(CharacterTestFairyGuiMainMenuManifest.CreatePackageDescriptor());

            if (options.EnableFairyGuiNetworkPanel && options.NetworkSessionController != null)
                AddFairyGuiPackagePreloadDescriptor(CharacterTestFairyGuiNetworkPanelManifest.CreatePackageDescriptor());

            if (options.EnableFairyGuiSettingsPanel && options.SettingsRuntime != null)
                AddFairyGuiPackagePreloadDescriptor(CharacterTestFairyGuiSettingsPanelManifest.CreatePackageDescriptor());

            if (options.EnableFairyGuiAbilityBindingPanel && services?.Modules.AbilityHotbar != null)
                AddFairyGuiPackagePreloadDescriptor(
                    CharacterTestFairyGuiAbilityBindingPanelManifest.CreatePackageDescriptor());

            if (options.EnableFairyGuiRuntimeAiBehaviorLoadoutPanel)
            {
                AddFairyGuiPackagePreloadDescriptor(options.PlayerBehaviorDeckService != null
                    ? CharacterTestFairyGuiBehaviorDeckPanelManifest.CreatePackageDescriptor()
                    : CharacterTestFairyGuiRuntimeAiBehaviorLoadoutPanelManifest.CreatePackageDescriptor());
            }

            if (options.EnableFairyGuiArenaSetupPanel && options.ArenaSessionController != null)
            {
                AddFairyGuiPackagePreloadDescriptor(
                    CharacterTestFairyGuiArenaSetupPanelManifest.CreatePackageDescriptor());
                AddFairyGuiPackagePreloadDescriptor(
                    CharacterTestFairyGuiArenaBehaviorGroupPanelManifest.CreatePackageDescriptor());
            }

            if (options.EnableFairyGuiRuntimeAiPlannerDevPanel
                && options.RuntimeAiPlannerDevSceneController != null)
            {
                AddFairyGuiPackagePreloadDescriptor(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelManifest.CreatePackageDescriptor());
            }
        }

        private void AddFairyGuiPackagePreloadDescriptor(MxFairyGuiPackageDescriptor descriptor)
        {
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.PackageId))
                return;

            for (int i = 0; i < _fairyGuiPackagePreloadDescriptors.Count; i++)
            {
                if (string.Equals(
                        _fairyGuiPackagePreloadDescriptors[i].PackageId,
                        descriptor.PackageId,
                        StringComparison.Ordinal))
                {
                    return;
                }
            }

            _fairyGuiPackagePreloadDescriptors.Add(descriptor);
        }

        private void ReleasePreloadedFairyGuiPackages()
        {
            if (_fairyGuiHost == null)
            {
                _preloadedFairyGuiPackageIds.Clear();
                return;
            }

            for (int i = _preloadedFairyGuiPackageIds.Count - 1; i >= 0; i--)
                _fairyGuiHost.ReleasePackage(_preloadedFairyGuiPackageIds[i]);

            _preloadedFairyGuiPackageIds.Clear();
        }

        private void RecordFairyGuiPackagePreloadFailure(
            MxFairyGuiPackageDescriptor descriptor,
            string message)
        {
            if (!string.IsNullOrEmpty(_lastFairyGuiPackagePreloadStatus))
                return;

            string packageId = descriptor != null ? descriptor.PackageId : string.Empty;
            _lastFairyGuiPackagePreloadStatus =
                "FairyGUI package preload skipped. package="
                + packageId
                + " message="
                + (message ?? string.Empty);
        }

        private IMxFairyGuiInputContextBridge CreatePlayerInputBridge(
            IInputProvider inputProvider,
            IMxCursorController cursorController)
        {
            return new MxFairyGuiInputContextBridge(_ =>
                new PlayerInteractiveUiScope(
                    this,
                    inputProvider != null
                        ? inputProvider.PushContext(InputContext.UI, InputContextPolicy.Exclusive)
                        : null,
                    cursorController != null
                        ? cursorController.Push(MxCursorRequest.Released("CharacterTest.UI.Modal"))
                        : null));
        }

        private bool IsUnprocessedPlayerUiInputCommand(InputCommand command)
        {
            return command.Phase == InputCommandPhase.Pressed
                && (command.Intent == InputIntent.Submit || command.Intent == InputIntent.Cancel)
                && (!_hasProcessedPlayerUiInputSequence
                    || command.Sequence > _lastProcessedPlayerUiInputSequence);
        }

        private bool IsUnprocessedGuidanceInputCommand(InputCommand command)
        {
            return command.Phase == InputCommandPhase.Pressed
                && (command.Intent == InputIntent.GuidancePrevious
                    || command.Intent == InputIntent.GuidanceNext)
                && (!_hasProcessedGuidanceInputSequence
                    || command.Sequence > _lastProcessedGuidanceInputSequence);
        }

        private static int ResolveNavigateDirection(Vector2 navigate)
        {
            const float threshold = 0.5f;
            float absX = Mathf.Abs(navigate.x);
            float absY = Mathf.Abs(navigate.y);
            if (absX < threshold && absY < threshold)
                return 0;

            if (absY >= absX)
                return navigate.y < 0f ? 1 : -1;

            return navigate.x > 0f ? 1 : -1;
        }

        private void ProcessPlayerUiCancel(MxUiCommand command)
        {
            if (!TryResolvePlayerSurface(command.SourceViewId, out CharacterTestUiSurface preferredSurface)
                || !CharacterTestUiSurfacePolicy.TryGetCancelTarget(
                    CaptureSurfaceState(),
                    preferredSurface,
                    out CharacterTestUiSurface target))
            {
                return;
            }

            switch (target)
            {
                case CharacterTestUiSurface.MainMenu:
                    _fairyGuiMainMenuHost?.CommandSink?.Enqueue(new MxUiCommand(
                        command.SourceViewId,
                        CharacterTestFairyGuiMainMenuCommandIds.Close,
                        command.Payload));
                    break;
                case CharacterTestUiSurface.NetworkPanel:
                    _fairyGuiNetworkPanelHost?.CommandSink?.Enqueue(command);
                    break;
                case CharacterTestUiSurface.SettingsPanel:
                    _fairyGuiSettingsPanelHost?.EnqueueRoutedCommand(command);
                    break;
                case CharacterTestUiSurface.AbilityBindingPanel:
                    _fairyGuiAbilityBindingPanelHost?.CommandSink?.Enqueue(command);
                    break;
                case CharacterTestUiSurface.RuntimeAiBehaviorLoadoutPanel:
                    if (_fairyGuiBehaviorDeckPanelHost != null)
                        _fairyGuiBehaviorDeckPanelHost.CommandSink?.Enqueue(command);
                    else
                        _fairyGuiRuntimeAiBehaviorLoadoutPanelHost?.CommandSink?.Enqueue(command);
                    break;
                case CharacterTestUiSurface.ArenaSetupPanel:
                    CloseArenaSetupPanel();
                    break;
                case CharacterTestUiSurface.ArenaBehaviorGroupPanel:
                    _fairyGuiArenaBehaviorGroupPanelHost?.CommandSink?.Enqueue(new MxUiCommand(
                        command.SourceViewId,
                        CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.Close,
                        command.Payload));
                    break;
            }
        }

        private sealed class CharacterTestPlayerUiCancelCommandSink : IMxUiCommandSink
        {
            private readonly CharacterTestUiRuntime _runtime;

            public CharacterTestPlayerUiCancelCommandSink(CharacterTestUiRuntime runtime)
            {
                _runtime = runtime;
            }

            public void Enqueue(MxUiCommand command)
            {
                _runtime.ProcessPlayerUiCancel(command);
            }
        }

        private sealed class PlayerInteractiveUiScope : IDisposable
        {
            private CharacterTestUiRuntime _runtime;
            private IDisposable _inputScope;
            private IDisposable _cursorScope;

            public PlayerInteractiveUiScope(CharacterTestUiRuntime runtime, IDisposable inputScope, IDisposable cursorScope)
            {
                _runtime = runtime;
                _runtime._lastPlayerModalInputFrame = Time.frameCount;
                _inputScope = inputScope;
                _cursorScope = cursorScope;
            }

            public void Dispose()
            {
                if (_runtime == null) return;
                _runtime._lastPlayerModalInputFrame = Time.frameCount;
                _runtime = null;

                IDisposable cursorScope = _cursorScope;
                _cursorScope = null;
                cursorScope?.Dispose();

                IDisposable inputScope = _inputScope;
                _inputScope = null;
                inputScope?.Dispose();
            }
        }
    }

    internal sealed class CharacterTestUiPerformanceCounterSource
    {
        public const string SourceName = "CharacterTestUi";
        public const string CombatHudRefreshAttemptsCounterId = "ui.combatHud.refreshAttempts";
        public const string CombatHudRefreshSuccessCounterId = "ui.combatHud.refreshSuccess";
        public const string CombatHudRefreshSkippedCounterId = "ui.combatHud.refreshSkipped";
        public const string FloatingTextRefreshAttemptsCounterId = "ui.floatingText.refreshAttempts";
        public const string FloatingTextRefreshSkippedCounterId = "ui.floatingText.refreshSkipped";
        public const string FloatingTextActiveCounterId = "ui.floatingText.active";
        public const string FloatingTextQueuedCounterId = "ui.floatingText.queued";
        public const string FloatingTextSpawnedCounterId = "ui.floatingText.spawned";
        public const string FloatingTextDroppedCounterId = "ui.floatingText.dropped";
        public const string MainMenuRefreshAttemptsCounterId = "ui.mainMenu.refreshAttempts";
        public const string MainMenuRefreshSuccessCounterId = "ui.mainMenu.refreshSuccess";
        public const string MainMenuRefreshSkippedCounterId = "ui.mainMenu.refreshSkipped";
        public const string NetworkPanelRefreshAttemptsCounterId = "ui.networkPanel.refreshAttempts";
        public const string NetworkPanelRefreshSuccessCounterId = "ui.networkPanel.refreshSuccess";
        public const string NetworkPanelRefreshSkippedCounterId = "ui.networkPanel.refreshSkipped";
        public const string SettingsPanelRefreshAttemptsCounterId = "ui.settingsPanel.refreshAttempts";
        public const string SettingsPanelRefreshSuccessCounterId = "ui.settingsPanel.refreshSuccess";
        public const string SettingsPanelRefreshSkippedCounterId = "ui.settingsPanel.refreshSkipped";
        public const string AbilityPanelRefreshAttemptsCounterId = "ui.abilityPanel.refreshAttempts";
        public const string AbilityPanelRefreshSuccessCounterId = "ui.abilityPanel.refreshSuccess";
        public const string AbilityPanelRefreshSkippedCounterId = "ui.abilityPanel.refreshSkipped";
        public const string StoryDialogRefreshSuccessCounterId = "ui.storyDialog.refreshSuccess";
        public const string StoryDialogRefreshSkippedCounterId = "ui.storyDialog.refreshSkipped";

        private const string CombatHudCategory = "CharacterTestUi.CombatHud";
        private const string FloatingTextCategory = "CharacterTestUi.FloatingText";
        private const string ModalCategory = "CharacterTestUi.Modal";
        private const string StoryCategory = "CharacterTestUi.Story";

        internal static FrameworkPerformanceCounterSnapshot Capture(CharacterTestUiRuntime runtime)
        {
            if (runtime == null)
                return Capture(CharacterTestUiPerformanceSummary.Empty);

            CharacterTestFairyGuiCombatHudHost combatHud = runtime.FairyGuiCombatHudHost;
            CharacterTestFairyGuiFloatingCombatTextHost floatingText = runtime.FairyGuiFloatingCombatTextHost;
            CharacterTestFairyGuiMainMenuHost mainMenu = runtime.FairyGuiMainMenuHost;
            CharacterTestFairyGuiNetworkPanelHost networkPanel = runtime.FairyGuiNetworkPanelHost;
            CharacterTestFairyGuiSettingsPanelHost settingsPanel = runtime.FairyGuiSettingsPanelHost;
            CharacterTestFairyGuiAbilityBindingPanelHost abilityPanel = runtime.FairyGuiAbilityBindingPanelHost;
            CharacterTestFairyGuiStoryDialogHost storyDialog = runtime.FairyGuiStoryDialogHost;

            return Capture(new CharacterTestUiPerformanceSummary(
                combatHud != null ? combatHud.RefreshAttemptCount : 0,
                combatHud != null ? combatHud.SuccessfulRefreshCount : 0,
                combatHud != null ? combatHud.SkippedRefreshCount : 0,
                floatingText != null ? floatingText.RefreshAttemptCount : 0,
                floatingText != null ? floatingText.SkippedRefreshCount : 0,
                floatingText != null ? floatingText.ActiveCount : 0,
                floatingText != null ? floatingText.QueuedCount : 0,
                floatingText != null ? floatingText.SpawnedCount : 0,
                floatingText != null ? floatingText.DroppedCount : 0,
                mainMenu != null ? mainMenu.RefreshAttemptCount : 0,
                mainMenu != null ? mainMenu.SuccessfulRefreshCount : 0,
                mainMenu != null ? mainMenu.SkippedRefreshCount : 0,
                networkPanel != null ? networkPanel.RefreshAttemptCount : 0,
                networkPanel != null ? networkPanel.SuccessfulRefreshCount : 0,
                networkPanel != null ? networkPanel.SkippedRefreshCount : 0,
                settingsPanel != null ? settingsPanel.RefreshAttemptCount : 0,
                settingsPanel != null ? settingsPanel.SuccessfulRefreshCount : 0,
                settingsPanel != null ? settingsPanel.SkippedRefreshCount : 0,
                abilityPanel != null ? abilityPanel.RefreshAttemptCount : 0,
                abilityPanel != null ? abilityPanel.SuccessfulRefreshCount : 0,
                abilityPanel != null ? abilityPanel.SkippedRefreshCount : 0,
                storyDialog != null ? storyDialog.SuccessfulRefreshCount : 0,
                storyDialog != null ? storyDialog.SkippedRefreshCount : 0));
        }

        internal static FrameworkPerformanceCounterSnapshot Capture(
            CharacterTestUiPerformanceSummary summary)
        {
            return new FrameworkPerformanceCounterSnapshot(
                SourceName,
                enabled: true,
                new[]
                {
                    Sample(CombatHudRefreshAttemptsCounterId, "Combat HUD Refresh Attempts", CombatHudCategory, summary.CombatHudRefreshAttemptCount, "attempts"),
                    Sample(CombatHudRefreshSuccessCounterId, "Combat HUD Refresh Success", CombatHudCategory, summary.CombatHudSuccessfulRefreshCount, "refreshes"),
                    Sample(CombatHudRefreshSkippedCounterId, "Combat HUD Refresh Skipped", CombatHudCategory, summary.CombatHudSkippedRefreshCount, "skips"),
                    Sample(FloatingTextRefreshAttemptsCounterId, "Floating Text Refresh Attempts", FloatingTextCategory, summary.FloatingTextRefreshAttemptCount, "attempts"),
                    Sample(FloatingTextRefreshSkippedCounterId, "Floating Text Refresh Skipped", FloatingTextCategory, summary.FloatingTextSkippedRefreshCount, "skips"),
                    Sample(FloatingTextActiveCounterId, "Floating Text Active", FloatingTextCategory, summary.FloatingTextActiveCount, "texts"),
                    Sample(FloatingTextQueuedCounterId, "Floating Text Queued", FloatingTextCategory, summary.FloatingTextQueuedCount, "events"),
                    Sample(FloatingTextSpawnedCounterId, "Floating Text Spawned", FloatingTextCategory, summary.FloatingTextSpawnedCount, "texts"),
                    Sample(FloatingTextDroppedCounterId, "Floating Text Dropped", FloatingTextCategory, summary.FloatingTextDroppedCount, "events"),
                    Sample(MainMenuRefreshAttemptsCounterId, "Main Menu Refresh Attempts", ModalCategory, summary.MainMenuRefreshAttemptCount, "attempts"),
                    Sample(MainMenuRefreshSuccessCounterId, "Main Menu Refresh Success", ModalCategory, summary.MainMenuSuccessfulRefreshCount, "refreshes"),
                    Sample(MainMenuRefreshSkippedCounterId, "Main Menu Refresh Skipped", ModalCategory, summary.MainMenuSkippedRefreshCount, "skips"),
                    Sample(NetworkPanelRefreshAttemptsCounterId, "Network Panel Refresh Attempts", ModalCategory, summary.NetworkPanelRefreshAttemptCount, "attempts"),
                    Sample(NetworkPanelRefreshSuccessCounterId, "Network Panel Refresh Success", ModalCategory, summary.NetworkPanelSuccessfulRefreshCount, "refreshes"),
                    Sample(NetworkPanelRefreshSkippedCounterId, "Network Panel Refresh Skipped", ModalCategory, summary.NetworkPanelSkippedRefreshCount, "skips"),
                    Sample(SettingsPanelRefreshAttemptsCounterId, "Settings Panel Refresh Attempts", ModalCategory, summary.SettingsPanelRefreshAttemptCount, "attempts"),
                    Sample(SettingsPanelRefreshSuccessCounterId, "Settings Panel Refresh Success", ModalCategory, summary.SettingsPanelSuccessfulRefreshCount, "refreshes"),
                    Sample(SettingsPanelRefreshSkippedCounterId, "Settings Panel Refresh Skipped", ModalCategory, summary.SettingsPanelSkippedRefreshCount, "skips"),
                    Sample(AbilityPanelRefreshAttemptsCounterId, "Ability Panel Refresh Attempts", ModalCategory, summary.AbilityPanelRefreshAttemptCount, "attempts"),
                    Sample(AbilityPanelRefreshSuccessCounterId, "Ability Panel Refresh Success", ModalCategory, summary.AbilityPanelSuccessfulRefreshCount, "refreshes"),
                    Sample(AbilityPanelRefreshSkippedCounterId, "Ability Panel Refresh Skipped", ModalCategory, summary.AbilityPanelSkippedRefreshCount, "skips"),
                    Sample(StoryDialogRefreshSuccessCounterId, "Story Dialog Refresh Success", StoryCategory, summary.StoryDialogSuccessfulRefreshCount, "refreshes"),
                    Sample(StoryDialogRefreshSkippedCounterId, "Story Dialog Refresh Skipped", StoryCategory, summary.StoryDialogSkippedRefreshCount, "skips")
                });
        }

        private static FrameworkPerformanceCounterSample Sample(
            string counterId,
            string displayName,
            string category,
            long value,
            string unit)
        {
            return new FrameworkPerformanceCounterSample(
                counterId,
                displayName,
                category,
                Math.Max(0L, value),
                unit,
                FrameworkPerformanceCounterCost.NoAlloc);
        }
    }

    internal readonly struct CharacterTestUiPerformanceSummary
    {
        public CharacterTestUiPerformanceSummary(
            int combatHudRefreshAttemptCount,
            int combatHudSuccessfulRefreshCount,
            int combatHudSkippedRefreshCount,
            int floatingTextRefreshAttemptCount,
            int floatingTextSkippedRefreshCount,
            int floatingTextActiveCount,
            int floatingTextQueuedCount,
            int floatingTextSpawnedCount,
            int floatingTextDroppedCount,
            int mainMenuRefreshAttemptCount,
            int mainMenuSuccessfulRefreshCount,
            int mainMenuSkippedRefreshCount,
            int networkPanelRefreshAttemptCount,
            int networkPanelSuccessfulRefreshCount,
            int networkPanelSkippedRefreshCount,
            int settingsPanelRefreshAttemptCount,
            int settingsPanelSuccessfulRefreshCount,
            int settingsPanelSkippedRefreshCount,
            int abilityPanelRefreshAttemptCount,
            int abilityPanelSuccessfulRefreshCount,
            int abilityPanelSkippedRefreshCount,
            int storyDialogSuccessfulRefreshCount,
            int storyDialogSkippedRefreshCount)
        {
            CombatHudRefreshAttemptCount = Math.Max(0, combatHudRefreshAttemptCount);
            CombatHudSuccessfulRefreshCount = Math.Max(0, combatHudSuccessfulRefreshCount);
            CombatHudSkippedRefreshCount = Math.Max(0, combatHudSkippedRefreshCount);
            FloatingTextRefreshAttemptCount = Math.Max(0, floatingTextRefreshAttemptCount);
            FloatingTextSkippedRefreshCount = Math.Max(0, floatingTextSkippedRefreshCount);
            FloatingTextActiveCount = Math.Max(0, floatingTextActiveCount);
            FloatingTextQueuedCount = Math.Max(0, floatingTextQueuedCount);
            FloatingTextSpawnedCount = Math.Max(0, floatingTextSpawnedCount);
            FloatingTextDroppedCount = Math.Max(0, floatingTextDroppedCount);
            MainMenuRefreshAttemptCount = Math.Max(0, mainMenuRefreshAttemptCount);
            MainMenuSuccessfulRefreshCount = Math.Max(0, mainMenuSuccessfulRefreshCount);
            MainMenuSkippedRefreshCount = Math.Max(0, mainMenuSkippedRefreshCount);
            NetworkPanelRefreshAttemptCount = Math.Max(0, networkPanelRefreshAttemptCount);
            NetworkPanelSuccessfulRefreshCount = Math.Max(0, networkPanelSuccessfulRefreshCount);
            NetworkPanelSkippedRefreshCount = Math.Max(0, networkPanelSkippedRefreshCount);
            SettingsPanelRefreshAttemptCount = Math.Max(0, settingsPanelRefreshAttemptCount);
            SettingsPanelSuccessfulRefreshCount = Math.Max(0, settingsPanelSuccessfulRefreshCount);
            SettingsPanelSkippedRefreshCount = Math.Max(0, settingsPanelSkippedRefreshCount);
            AbilityPanelRefreshAttemptCount = Math.Max(0, abilityPanelRefreshAttemptCount);
            AbilityPanelSuccessfulRefreshCount = Math.Max(0, abilityPanelSuccessfulRefreshCount);
            AbilityPanelSkippedRefreshCount = Math.Max(0, abilityPanelSkippedRefreshCount);
            StoryDialogSuccessfulRefreshCount = Math.Max(0, storyDialogSuccessfulRefreshCount);
            StoryDialogSkippedRefreshCount = Math.Max(0, storyDialogSkippedRefreshCount);
        }

        public static CharacterTestUiPerformanceSummary Empty => default;
        public int CombatHudRefreshAttemptCount { get; }
        public int CombatHudSuccessfulRefreshCount { get; }
        public int CombatHudSkippedRefreshCount { get; }
        public int FloatingTextRefreshAttemptCount { get; }
        public int FloatingTextSkippedRefreshCount { get; }
        public int FloatingTextActiveCount { get; }
        public int FloatingTextQueuedCount { get; }
        public int FloatingTextSpawnedCount { get; }
        public int FloatingTextDroppedCount { get; }
        public int MainMenuRefreshAttemptCount { get; }
        public int MainMenuSuccessfulRefreshCount { get; }
        public int MainMenuSkippedRefreshCount { get; }
        public int NetworkPanelRefreshAttemptCount { get; }
        public int NetworkPanelSuccessfulRefreshCount { get; }
        public int NetworkPanelSkippedRefreshCount { get; }
        public int SettingsPanelRefreshAttemptCount { get; }
        public int SettingsPanelSuccessfulRefreshCount { get; }
        public int SettingsPanelSkippedRefreshCount { get; }
        public int AbilityPanelRefreshAttemptCount { get; }
        public int AbilityPanelSuccessfulRefreshCount { get; }
        public int AbilityPanelSkippedRefreshCount { get; }
        public int StoryDialogSuccessfulRefreshCount { get; }
        public int StoryDialogSkippedRefreshCount { get; }
    }
}
