using MxFramework.Audio;
using MxFramework.Input;
using MxFramework.Runtime;
using MxFramework.Runtime.Unity;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeContext
    {
        public CharacterTestRuntimeContext(MonoBehaviour owner, CharacterTestSceneProfile profile)
        {
            Owner = owner;
            Profile = profile;
        }

        public MonoBehaviour Owner { get; }
        public CharacterTestSceneProfile Profile { get; }
        public UnityRuntimeLogger Logger { get; set; }
        public RuntimeLogBuffer LogBuffer { get; } = new RuntimeLogBuffer(96);
        public CharacterTestFixedTickDriver FixedTickDriver { get; } = new CharacterTestFixedTickDriver();
        public CharacterTestNetworkSyncProbe NetworkSyncProbe { get; } = new CharacterTestNetworkSyncProbe();

        public CharacterTestRuntimeSessionBuilder SessionBuilder { get; set; }
        public CharacterTestNetworkCoordinator NetworkCoordinator { get; set; }
        public CharacterTestMainMenuCoordinator MainMenuCoordinator { get; set; }
        public CharacterTestDebugPresentationController DebugPresentationController { get; set; }
        public CharacterTestArenaSessionController ArenaSessionController { get; set; }
        public CharacterTestRuntimeAiPlannerDevSceneController RuntimeAiPlannerDevSceneController { get; set; }

        public string ActiveLevelId { get; set; } = string.Empty;
        public string ActiveLevelContentHash { get; set; } = string.Empty;
        public CharacterTestWorldObjectSceneCache ActiveLevelWorldCache { get; set; }
        public GameSlice Slice { get; set; }
        public ICharacterTestNetworkRunner NetworkRunner { get; set; }
        public CharacterTestUiRuntime UiRuntime { get; set; }
        public CharacterTestSettingsRuntime SettingsRuntime { get; set; }
        public AudioMixController AudioMixController { get; set; }
        public AudioMixSnapshotController AudioMixSnapshotController { get; set; }
        public AudioMusicStateController AudioMusicStateController { get; set; }
        public AudioPersistentEventController AudioAmbienceController { get; set; }
        public CharacterTestLocalizationRuntime LocalizationRuntime { get; set; }
        public IInputProvider InputProvider { get; set; }
        public IMxCursorController CursorController { get; set; }
        public CharacterTestThirdPersonCameraRigBridge ThirdPersonCameraBridge { get; set; }
        public CharacterTestMovableNaturalObjectUnityViewBridge MovableNaturalObjectViewBridge { get; set; }
        public CharacterTestRuntimeAiPlannerLocalPlayerDeckService PlayerBehaviorDeckService { get; set; }

        public FakeInputProvider NetworkSyncProbeInputProvider { get; set; }
        public bool NetworkSyncProbeInputProviderActive { get; set; }
        public CharacterTestNetworkAutomationRunner NetworkAutomationRunner { get; set; }
        public bool NetworkSyncProbeAutoRunEnabled { get; set; }
        public bool NetworkSyncProbeAutoRunPending { get; set; }
        public bool NetworkSyncProbeAutoRunConsumed { get; set; }
        public CharacterTestReplayPlaybackStatus NetworkReplayStatus { get; set; } =
            CharacterTestReplayPlaybackStatus.Unavailable("CharacterTest replay has not run.");
        public bool NetworkReplayValidationPending { get; set; }

        public bool Paused { get; set; }
        public bool ManualPaused { get; set; }
        public int PreviousTargetFrameRate { get; set; }
        public bool HasPreviousTargetFrameRate { get; set; }
        public CharacterTestNetworkSessionKind ActiveNetworkSessionKind { get; set; } =
            CharacterTestNetworkSessionKind.Offline;
        public CharacterTestNetworkSessionOptions ActiveNetworkSessionOptions { get; set; }
        public string LastNetworkSessionMessage { get; set; } = "Offline";
        public string RuntimeClientPeerId { get; set; }
        public bool NetworkGameplayEnterPending { get; set; }
        public bool NetworkGameplayEntered { get; set; }
        public bool MainMenuVisible { get; set; }
        public bool HasEnteredGameplayFromMainMenu { get; set; }

        public bool IsPaused => Paused;
        public bool HasSceneFlow => Slice != null && Slice.Services.HasSceneFlow;
        public SceneFlowController SceneFlowController => Slice?.Services.SceneFlowController;
        public GlobalResourceRuntimeMode EffectiveResourceMode => Profile != null
            ? CharacterTestRuntimeSessionBuilder.ResolveResourceMode(Profile.ResourceMode, Application.isEditor)
            : GlobalResourceRuntimeMode.Editor;
        public CharacterTestNetworkSessionKind ActiveNetworkSessionKindValue => ActiveNetworkSessionKind;
        public bool IsNetworkSessionActive => NetworkRunner != null;
    }
}
